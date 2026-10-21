using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// Native wood/grid/tooltips, with the same header, category scope and footer as
// the guide inventory. Changes to native layout/widgets are restored on unload.
internal sealed class VanillaQuickStack : IDisposable
{
    internal const string BarName = "Ragnarok Stack Bar";
    private const float Header = 90, Footer = 66;
    private static readonly AccessTools.FieldRef<InventoryGui, float> PlayerHeight =
        AccessTools.FieldRefAccess<InventoryGui, float>("m_playerHeight");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly InventoryPanel panel;
    private readonly VanillaCategoryGrid spotlight = new();
    private readonly List<(PackCategory Category, Button Button)> tabs = new();
    private readonly List<(GameObject Object, bool Active)> hidden = new();
    private InventoryGui? gui;
    private RectTransform? root, footer, gridRect, sunken;
    private InventoryGuide.Summary? summary;
    private TMP_Text? stackLabel, status, close;
    private Button? deleteButton;
    private Vector2 gridMin, gridMax, sunkenHome;
    private float originalHeight, builtWidth;
    private bool layoutChanged, failed;
    private PackCategory category;
    private string message = "";
    private float messageTime = -10;

    internal VanillaQuickStack(InterfaceSettings settings, Theme theme, InventoryPanel panel)
    {
        this.settings = settings;
        this.theme = theme;
        this.panel = panel;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui) { Dispose(); gui = current; failed = false; }
        if (!gui) return;
        if (panel.Wanted) { Dispose(); return; }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer || failed) return;
        if (!root || !Mathf.Approximately(builtWidth, gui!.m_player.rect.width))
        {
            Dispose();
            try { Build(gui!); }
            catch { Dispose(); failed = true; throw; }
        }
        Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        spotlight.Dispose();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = footer = null;
        deleteButton = null;
        summary = null;
        tabs.Clear();
        foreach (var entry in hidden)
            if (entry.Object) entry.Object.SetActive(entry.Active);
        hidden.Clear();
        if (layoutChanged)
        {
            if (gui)
            {
                PlayerHeight(gui!) = originalHeight;
                gui!.m_player.sizeDelta -= new Vector2(0, Header + Footer);
            }
            if (gridRect) { gridRect!.offsetMin = gridMin; gridRect.offsetMax = gridMax; }
            if (sunken) sunken!.anchoredPosition = sunkenHome;
        }
        layoutChanged = false;
    }

    private void Hide(Transform target)
    {
        hidden.Add((target.gameObject, target.gameObject.activeSelf));
        target.gameObject.SetActive(false);
    }

    private void Build(InventoryGui inventoryGui)
    {
        RectTransform player = inventoryGui.m_player;
        originalHeight = PlayerHeight(inventoryGui);
        gridRect = (RectTransform)inventoryGui.m_playerGrid.transform;
        gridMin = gridRect.offsetMin;
        gridMax = gridRect.offsetMax;
        layoutChanged = true;
        PlayerHeight(inventoryGui) = originalHeight + Header + Footer;
        player.sizeDelta += new Vector2(0, Header + Footer);
        gridRect.offsetMin += new Vector2(0, Footer);
        gridRect.offsetMax -= new Vector2(0, Header);
        sunken = player.Find("sunken") as RectTransform;
        if (sunken) { sunkenHome = sunken!.anchoredPosition; sunken.anchoredPosition -= new Vector2(0, Header); }
        foreach (string name in new[] { "Armor", "Weight", "help_Text" })
            if (player.Find(name) is Transform widget) Hide(widget);
        builtWidth = player.rect.width;
        root = Ugui.Node(player, BarName).Stretch();
        summary = new InventoryGuide.Summary(theme, root, builtWidth, "INVENTORY", true, vanilla: true);
        string[] labels = { "All", "Gear", "Materials", "Food", "Trophies" };
        float x = 12;
        for (int i = 0; i < labels.Length; i++)
        {
            PackCategory value = (PackCategory)i;
            float width = i == 2 ? 100 : i == 4 ? 88 : 70;
            Button button = VanillaInventoryStyle.Button(inventoryGui, root, labels[i], new Rect(x, 50, width, 30), () => category = value);
            tabs.Add((value, button));
            x += width + 4;
        }
        // Delete sits at the far end of the tab row; it acts on the item held on the cursor.
        deleteButton = VanillaInventoryStyle.Button(inventoryGui, root, "Delete", new Rect(Mathf.Max(x + 8, builtWidth - 92), 50, 80, 30), PressDelete);
        ItemTrash.Attach(deleteButton, PressDelete);
        footer = Ugui.Node(root, "Actions and keys");
        footer.anchorMin = footer.anchorMax = new Vector2(0, 0);
        footer.pivot = new Vector2(0, 1);
        footer.anchoredPosition = new Vector2(0, Footer - 5);
        footer.sizeDelta = new Vector2(builtWidth, Footer - 5);
        Button stack = VanillaInventoryStyle.Button(inventoryGui, footer, "Quick Stack: All", new Rect(12, 0, 170, 30), Press);
        stackLabel = stack.GetComponentInChildren<TMP_Text>();
        status = VanillaInventoryStyle.Text(inventoryGui, footer, "Status", "", 12).At(190, 0, Mathf.Max(25, builtWidth - 420), 30);
        float sortX = builtWidth - 222;
        AddSort(Equipment.SortMode.Name, "Name", sortX);
        AddSort(Equipment.SortMode.Category, "Kind", sortX + 70);
        AddSort(Equipment.SortMode.Weight, "Weight", sortX + 140);
        TMP_Text hints = VanillaInventoryStyle.Text(inventoryGui, footer, "Input guide",
            "<color=#FFFFFF><b>LMB</b></color> move   <color=#FFFFFF><b>RMB</b></color> use / equip   <color=#FFFFFF><b>SHIFT + LMB</b></color> split   <color=#FFFFFF><b>1–8</b></color> hotbar", 11);
        hints.color = Palette.Muted;
        hints.At(12, 34, builtWidth - 100, 22);
        close = VanillaInventoryStyle.Text(inventoryGui, footer, "Close guide", "", 11, TextAlignmentOptions.MidlineRight)
            .At(builtWidth - 82, 34, 70, 22);
        close.color = Palette.Muted;
    }

    private void AddSort(Equipment.SortMode mode, string name, float x) =>
        VanillaInventoryStyle.Button(gui!, footer!, name, new Rect(x, 0, 66, 30), () =>
        {
            if (QuickStackCommand.Sort(settings, gui!, mode) is { } result) Show(result);
        });

    private void Show(string text) { message = text; messageTime = Time.unscaledTime; }

    private void Press()
    {
        if (QuickStackCommand.Run(gui!, item => category == PackCategory.All || InventoryCategories.Of(item.m_shared) == category) is { } result)
            Show(result);
    }

    private void PressDelete() => ItemTrash.Press(gui!, deleteButton!, settings.Features.ItemDeleteConfirm.Value, Show);

    private void Refresh(Player player)
    {
        Inventory inventory = player.GetInventory();
        summary!.Set(player.GetPlayerName(), Mathf.RoundToInt(player.GetBodyArmor()), inventory.GetTotalWeight(), player.GetMaxCarryWeight());
        foreach (var tab in tabs)
        {
            Image image = (Image)tab.Button.targetGraphic;
            image.color = tab.Category == category ? Palette.Hex(0xFFD27F) : Color.white;
        }
        spotlight.Refresh(gui!.m_playerGrid, inventory, category, QuickStackCommand.DragItem(gui) != null);
        stackLabel!.text = "Quick Stack: " + category;
        bool delete = settings.Features.ItemDelete.Value;
        if (deleteButton!.gameObject.activeSelf != delete) deleteButton.gameObject.SetActive(delete);
        int nearby = NearbyStorage.StorageRuntime.Registry?.Nearby(player).Count ?? 0;
        status!.text = Time.unscaledTime - messageTime < 3 ? message : nearby + (nearby == 1 ? " chest in range" : " chests in range");
        string bound = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Inventory", true) : "TAB";
        if (string.IsNullOrEmpty(bound)) bound = "TAB";
        close!.text = "<color=#FFFFFF><b>" + Localization.instance.Localize(bound) + "</b></color> close";
    }
}
