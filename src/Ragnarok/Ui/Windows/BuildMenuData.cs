using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Ragnarok.Features.NearbyStorage;

namespace Ragnarok.UI.Windows;

// Native lists/favorites/selection own behavior. This adapter only snapshots preview data.
internal sealed class BuildMenuData
{
    internal sealed class Entry
    {
        internal Piece Piece = null!;
        internal BuildUiPieceButton Source = null!;
        internal string Name = "", Description = "", Search = "";
        internal bool Buildable, Favorite, Free;
        internal int Capacity;
        internal Sprite? UpgradeIcon;
    }
    internal sealed class Category
    {
        internal BuildUiTagButton Source = null!;
        internal string Name = "";
        internal bool Selected;
    }
    internal readonly struct Stock
    {
        internal readonly int Bag, Chests;
        internal int Total => Bag + Chests;
        internal Stock(int bag, int chests) { Bag = bag; Chests = chests; }
    }
    private static readonly FieldInfo Buttons = AccessTools.Field(typeof(BuildUi), "m_pieceButtons");
    private static readonly FieldInfo Tags = AccessTools.Field(typeof(BuildUi), "m_tagButtons");
    private static readonly FieldInfo ShowAll = AccessTools.Field(typeof(BuildUi), "m_showAllTagsButton");
    private static readonly FieldInfo TagToggle = AccessTools.Field(typeof(BuildUiTagButton), "m_toggledOnObject");
    private static readonly FieldInfo TagName = AccessTools.Field(typeof(BuildUiTagButton), "m_displayName");
    private static readonly FieldInfo NativeIcon = AccessTools.Field(typeof(BuildUiPieceButton), "m_icon");
    private static readonly FieldInfo NativeUpgrade = AccessTools.Field(typeof(BuildUiPieceButton), "m_upgradeArrow");
    private static readonly FieldInfo CurrentList = AccessTools.Field(typeof(BuildUi), "m_currentPieceList");
    private static readonly FieldInfo Special = AccessTools.Field(typeof(BuildUi), "m_specialPieceButton");
    private static readonly Action<BuildUi, int, bool> SelectList = AccessTools.MethodDelegate<Action<BuildUi, int, bool>>(AccessTools.Method(typeof(BuildUi), "SelectPieceList"));
    private static readonly Action<BuildUi, BuildUiPieceButton?> HoverPiece = AccessTools.MethodDelegate<Action<BuildUi, BuildUiPieceButton?>>(AccessTools.Method(typeof(BuildUi), "OnHoverPiece"));
    private static readonly Action<BuildUiPieceButton> ClickPiece = AccessTools.MethodDelegate<Action<BuildUiPieceButton>>(AccessTools.Method(typeof(BuildUiPieceButton), "OnClickButton"));
    private static readonly Action<BuildUi, Piece> SelectPiece = AccessTools.MethodDelegate<Action<BuildUi, Piece>>(AccessTools.Method(typeof(BuildUi), "OnSelectPiece"));
    private static readonly Func<BuildUi, Piece, bool> IsFavorite = AccessTools.MethodDelegate<Func<BuildUi, Piece, bool>>(AccessTools.Method(typeof(BuildUi), "IsFavoritePiece"));
    private static readonly Func<BuildUi, Piece, int, bool> AddFavorite = AccessTools.MethodDelegate<Func<BuildUi, Piece, int, bool>>(AccessTools.Method(typeof(BuildUi), "ToggleFavorite"));
    private static readonly Action<BuildUi, Piece> RemoveFavorite = AccessTools.MethodDelegate<Action<BuildUi, Piece>>(AccessTools.Method(typeof(BuildUi), "RemoveFromFavorites"));
    private readonly BuildUi owner;
    private readonly InterfaceSettings settings;
    private readonly Dictionary<Piece, Entry> cache = new();
    private ZNetScene? scene;
    internal readonly List<Entry> Pieces = new(), Visible = new();
    internal readonly List<Category> Categories = new();
    internal readonly Dictionary<string, Stock> Materials = new();
    internal int Tab;
    internal string Heading = "Building";
    internal Piece? Repair;
    internal bool NearbyAllowed;

    internal BuildMenuData(BuildUi owner, InterfaceSettings settings) { this.owner = owner; this.settings = settings; }

    internal Entry Read(Piece piece)
    {
        if (cache.TryGetValue(piece, out Entry? entry)) return entry;
        entry = new Entry { Piece = piece, Name = Local(piece.m_name), Description = Local(piece.m_description) };
        entry.Search = entry.Name + " " + entry.Description;
        foreach (Piece.Requirement req in piece.m_resources)
            if (req.m_resItem) entry.Search += " " + Local(req.m_resItem.m_itemData.m_shared.m_name);
        cache.Add(piece, entry);
        return entry;
    }

    internal void Refresh(string query)
    {
        Player player = Player.m_localPlayer;
        if (!player) return;
        if (scene != ZNetScene.instance) { scene = ZNetScene.instance; cache.Clear(); }
        Tab = (int)CurrentList.GetValue(owner);
        Categories.Clear();
        var all = ShowAll.GetValue(owner) as BuildUiTagButton;
        if (all && all!.gameObject.activeInHierarchy) AddCategory(all);
        if (Tags.GetValue(owner) is List<BuildUiTagButton> tags)
            foreach (BuildUiTagButton tag in tags)
                if (tag && tag != all && tag.gameObject.activeInHierarchy) AddCategory(tag);
        Heading = Tab switch { 1 => "Materials", 2 => "Recent", 3 => "Favourites", _ => "Building" };
        foreach (Category category in Categories) if (category.Selected) Heading = category.Name;
        Repair = Special.GetValue(owner) is BuildUiPieceButton special && special ? special.Piece : null;
        Pieces.Clear(); Visible.Clear(); Materials.Clear();
        NearbyAllowed = StorageRuntime.Settings is { } storage && storage.Enabled.Value && storage.Building.Value && StorageRuntime.Registry != null;
        using ResourceScope? scope = NearbyAllowed && settings.BuildCountNearby.Value
            ? new ResourceScope(player, StorageRuntime.Registry!, false) : null;
        string[] terms = query.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (Buttons.GetValue(owner) is not List<BuildUiPieceButton> buttons) return;
        foreach (BuildUiPieceButton source in buttons)
        {
            if (!source || !source.Piece) continue;
            Entry entry = Read(source.Piece); entry.Source = source;
            entry.UpgradeIcon = NativeUpgrade.GetValue(source) is Image arrow ? arrow.sprite : null;
            Color tint = NativeIcon.GetValue(source) is Image icon ? icon.color : Color.white;
            entry.Buildable = tint.r > 0.95f && tint.g > 0.95f && tint.b > 0.95f;
            entry.Favorite = IsFavorite(owner, entry.Piece);
            entry.Free = player.NoCostCheat() || (ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(entry.Piece.FreeBuildKey()));
            entry.Capacity = int.MaxValue;
            foreach (Piece.Requirement req in entry.Piece.m_resources)
            {
                if (!req.m_resItem || req.m_amount <= 0) continue;
                string name = req.m_resItem.m_itemData.m_shared.m_name;
                if (!Materials.TryGetValue(name, out Stock stock))
                {
                    int total = player.GetInventory().CountItems(name, -1, true);
                    int nearby = scope?.Count(name, -1, true) ?? 0;
                    stock = new Stock(total - nearby, nearby); Materials.Add(name, stock);
                }
                entry.Capacity = Mathf.Min(entry.Capacity, stock.Total / req.m_amount);
            }
            if (entry.Capacity == int.MaxValue) entry.Free = true;
            if (entry.Free) entry.Capacity = 0;
            Pieces.Add(entry);
            bool matches = true;
            foreach (string term in terms)
                if (entry.Search.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) < 0) { matches = false; break; }
            if (matches && (!settings.BuildOnlyAvailable.Value || entry.Buildable)) Visible.Add(entry);
        }
    }

    private void AddCategory(BuildUiTagButton source)
    {
        string name = (string)TagName.GetValue(source);
        if (string.IsNullOrEmpty(name)) name = source.CurrentText;
        Categories.Add(new Category
        {
            Source = source, Name = Local(name),
            Selected = TagToggle.GetValue(source) is GameObject toggle && toggle.activeSelf
        });
    }
    internal void SwitchTab(int index) => SelectList(owner, index, true);
    internal void Choose(Piece piece)
    {
        Player player = Player.m_localPlayer;
        if (player && player.GetBuildTool() && player.GetBuildTool().IsPieceAvailable(piece)) SelectPiece(owner, piece);
    }
    internal void Hover(Entry? entry) => HoverPiece(owner, entry != null && entry.Source && entry.Source.Piece == entry.Piece ? entry.Source : null);
    internal void Click(Entry entry)
    {
        if (entry.Source && entry.Source.Piece == entry.Piece) ClickPiece(entry.Source);
    }
    internal void Favorite(Piece piece)
    {
        if (IsFavorite(owner, piece)) RemoveFavorite(owner, piece);
        else AddFavorite(owner, piece, -1);
        AccessTools.Method(typeof(BuildUi), "UpdatePieceButtons").Invoke(owner, null);
    }
    internal static string Local(string text) => Localization.instance.Localize(text ?? "");
}
