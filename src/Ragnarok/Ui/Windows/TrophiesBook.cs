using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// The Ragnarök trophy wall, drawn over Valheim's trophies panel in the Compendium's frame: every trophy
// in the game on the left in Valheim's own order (the ones you have taken lit, the rest dark shapes), the
// chosen one's lore in the middle with how much of the wall is filled, and its glyph in the art panel.
internal sealed class TrophiesBook : BookWindow
{
    private const float ListWidth = 330, TextX = 470, TextWidth = 470, Tile = 58, Gap = 8;
    private const int Columns = 5;

    private sealed class Trophy
    {
        internal string Prefab = "", Name = "", Lore = "", Use = "";
        internal Sprite? Icon;
    }

    private readonly MonochromeIcons icons = new();
    private readonly List<Trophy> all = new();
    private HashSet<string> taken = new();
    private GameObject? panel;
    private RectTransform? listContent;
    private TextMeshProUGUI? total, title, kind, countValue, body;
    private RectTransform? countFill;
    private string chosen = "";
    private int signature;

    internal TrophiesBook(InterfaceSettings settings, Theme theme) : base(settings, theme) { }

    protected override string Name => "Trophies";
    protected override Tab Selected => Tab.Trophies;

    protected override bool Wanted => settings.TrophiesStyle.Value switch
    {
        PanelStyle.Ragnarok => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    protected override Component? Dialog(InventoryGui gui) => gui.m_trophiesPanel ? gui.m_trophiesPanel.transform : null;

    protected override void CloseDialog()
    {
        if (panel) InventoryGui.instance?.OnCloseTrophies();
    }

    public override void Dispose()
    {
        base.Dispose();
        icons.Dispose();
    }

    protected override void Build(InventoryGui gui)
    {
        panel = gui.m_trophiesPanel;
        Collect();
        total = Ugui.Text(window!, "Total", "", theme.Strong, 14, Palette.Bone).At(Left, 100, ListWidth, 26);

        Scroller(new Rect(Left - 20, Top, ListWidth + 20, Height - Top - 110), out listContent);

        title = Ugui.Text(window!, "Title", "", theme.Heading, 28, Palette.Bone).At(TextX, Top - 6, TextWidth, 44);
        kind = Ugui.Text(window!, "Kind", "", theme.Subtle, 14, Palette.Muted).At(TextX, Top + 38, TextWidth, 20);

        // How much of the wall is filled.
        float y = Top + 84;
        Ugui.Text(window!, "Count Label", "collection", theme.Strong, 13, Palette.Muted).At(TextX, y - 4, 200, 24);
        countValue = Ugui.Text(window!, "Count Value", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.MidlineRight)
            .At(TextX + TextWidth - 160, y, 160, 18);
        Ugui.Image(window!, "Groove", theme.HudBar, Palette.Hex(0x303631)).At(TextX, y + 24, TextWidth, 6);
        countFill = Ugui.Image(window!, "Fill", theme.HudBar, Palette.Gold).At(TextX, y + 24, 0, 6).rectTransform;

        body = Ugui.Text(window!, "Body", "", theme.Body, 15, Palette.Bone, TextAlignmentOptions.TopLeft, wrap: true)
            .At(TextX, y + 64, TextWidth, Height - y - 64 - 90);
        body.lineSpacing = 6;
        body.paragraphSpacing = 14;
        body.overflowMode = TextOverflowModes.Overflow;

        signature = 0;
        Refresh();
    }

    // Every trophy item in the game, in Valheim's wall order (row by row, as its trophy positions lay them out).
    private void Collect()
    {
        all.Clear();
        if (!ObjectDB.instance) return;
        foreach (GameObject prefab in ObjectDB.instance.m_items)
        {
            if (!prefab || !prefab.TryGetComponent(out ItemDrop drop)) continue;
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            if (shared.m_itemType != ItemDrop.ItemData.ItemType.Trophy) continue;
            string name = Localization.instance.Localize(shared.m_name);
            if (name.EndsWith(" trophy")) name = name.Remove(name.Length - 7);
            string lore = Localization.instance.Localize(shared.m_name + "_lore");
            all.Add(new Trophy
            {
                Prefab = prefab.name, Name = name, Icon = drop.m_itemData.GetIcon(),
                Lore = lore.StartsWith("[") ? "" : lore, Use = Localization.instance.Localize(shared.m_description)
            });
        }
        Dictionary<string, Vector2Int> place = ObjectDB.instance.m_items.Where(p => p && p.GetComponent<ItemDrop>())
            .GroupBy(p => p.name).ToDictionary(g => g.Key, g => g.First().GetComponent<ItemDrop>().m_itemData.m_shared.m_trophyPos);
        all.Sort((a, b) =>
        {
            Vector2Int pa = place[a.Prefab], pb = place[b.Prefab];
            return pa.y != pb.y ? pa.y.CompareTo(pb.y) : pa.x != pb.x ? pa.x.CompareTo(pb.x) : string.CompareOrdinal(a.Name, b.Name);
        });
    }

    // Rebuilt when a trophy is taken or the choice changes.
    protected override void Refresh()
    {
        if (!listContent || !Player.m_localPlayer || all.Count == 0) return;
        List<string> mine = Player.m_localPlayer.GetTrophies();
        if (all.All(t => t.Prefab != chosen))
            chosen = (all.FirstOrDefault(t => mine.Contains(t.Prefab)) ?? all[0]).Prefab;

        int now = chosen.GetHashCode() * 31 + mine.Count;
        if (now == signature) return;
        signature = now;
        taken = new HashSet<string>(mine);

        int count = all.Count(t => taken.Contains(t.Prefab));
        total!.text = $"<color=#9A9184>TAKEN</color>   {count} / {all.Count}";
        countValue!.text = $"{count} / {all.Count}";
        countFill!.sizeDelta = new Vector2(TextWidth * count / Mathf.Max(1, all.Count), 6);
        ShowWall();
        ShowChosen(all.First(t => t.Prefab == chosen));
    }

    private void ShowWall()
    {
        Ugui.Clear(listContent!);
        float x0 = 20;
        for (int i = 0; i < all.Count; i++)
        {
            Trophy trophy = all[i];
            bool have = taken.Contains(trophy.Prefab), selected = trophy.Prefab == chosen;
            Rect r = new(x0 + i % Columns * (Tile + Gap), i / Columns * (Tile + Gap), Tile, Tile);

            Image edge = Choice(listContent!, r, selected);
            Color rest = selected ? Palette.Gold : Palette.Hex(0x303631);
            // Taken: the trophy itself. Not yet: its dark shape.
            Image icon = Ugui.Image(listContent!, "Icon", have ? trophy.Icon : icons.Get(trophy.Icon) ?? theme.Diamond,
                have ? Color.white : Palette.Faint.WithAlpha(0.45f)).At(r.x + 7, r.y + 7, Tile - 14, Tile - 14);
            icon.preserveAspect = true;

            if (!selected)
                Ugui.HitArea(listContent!, "Pick", () => { chosen = trophy.Prefab; signature = 0; }).At(r)
                    .OnHover(on => edge.color = on ? Palette.Gold : rest);
        }
        int rows = (all.Count + Columns - 1) / Columns;
        listContent!.sizeDelta = new Vector2(0, rows * (Tile + Gap) + 8);
    }

    private void ShowChosen(Trophy trophy)
    {
        bool have = taken.Contains(trophy.Prefab);
        title!.text = have ? trophy.Name : "Unknown trophy";
        kind!.text = have ? "Trophy · taken" : "Trophy · not yet taken";
        kind.color = have ? Palette.Gold : Palette.Muted;
        body!.text = have
            ? (trophy.Lore.Length > 0 ? trophy.Lore + "\n\n" : "") + $"<color=#AAA79C>{trophy.Use}</color>"
            : "Its shape hangs on the wall, waiting. Take it from the creature it belongs to and its tale is yours.";

        Sprite? glyphSprite = icons.Get(trophy.Icon);
        glyph!.sprite = glyphSprite ? glyphSprite : theme.Diamond;
    }
}
