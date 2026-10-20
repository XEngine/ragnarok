using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// Companion to the native player header: same categories and named sorting,
// with native take/place actions, slot prefabs and tooltips left intact.
internal sealed class VanillaChestSort : IDisposable
{
    // The native chest already reserves 46 units above its grid: add 44 to
    // reach the shared 90-unit header rather than leaving a second title gap.
    private const float Header = 44, Footer = 36;
    private readonly Theme theme;
    private readonly Func<bool> ragnarok;
    private readonly VanillaCategoryGrid spotlight = new();
    private readonly List<(PackCategory Category, Button Button)> tabs = new();
    private readonly List<(GameObject Object, bool Active)> hidden = new();
    private readonly List<(RectTransform Rect, Vector2 Home)> shifted = new();
    private InventoryGui? gui;
    private RectTransform? root, gridRect;
    private InventoryGuide.Summary? summary;
    private Vector2 gridMin, gridMax;
    private bool layoutChanged, failed;
    private PackCategory category;
    private Button? take, place;

    internal VanillaChestSort(Theme theme, Func<bool> ragnarok) { this.theme = theme; this.ragnarok = ragnarok; }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui) { Dispose(); gui = current; failed = false; }
        if (!gui) return;
        if (ragnarok()) { Dispose(); return; }
        if (!gui!.m_container || !gui.m_container.gameObject.activeInHierarchy || !gui.ContainerGrid || failed) return;
        Inventory inventory = gui.ContainerGrid.GetInventory();
        if (inventory == null) return;
        if (!root)
        {
            try { Build(gui); }
            catch { Dispose(); failed = true; throw; }
        }
        summary!.Set(Localization.instance.Localize(gui.m_containerName.text), null, inventory.GetTotalWeight(), 0);
        foreach (var tab in tabs)
            ((Image)tab.Button.targetGraphic).color = tab.Category == category ? Palette.Hex(0xFFD27F) : Color.white;
        spotlight.Refresh(gui.ContainerGrid, inventory, category, QuickStackCommand.DragItem(gui) != null);
        take!.interactable = gui.m_takeAllButton.interactable;
        place!.interactable = gui.m_stackAllButton.interactable;
    }

    public void Dispose()
    {
        spotlight.Dispose();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null;
        summary = null;
        tabs.Clear();
        foreach (var entry in hidden)
            if (entry.Object) entry.Object.SetActive(entry.Active);
        hidden.Clear();
        foreach (var entry in shifted)
            if (entry.Rect) entry.Rect.anchoredPosition = entry.Home;
        shifted.Clear();
        if (layoutChanged)
        {
            if (gui && gui!.m_container) gui.m_container.sizeDelta -= new Vector2(0, Header + Footer);
            if (gridRect) { gridRect!.offsetMin = gridMin; gridRect.offsetMax = gridMax; }
        }
        layoutChanged = false;
    }

    private void Hide(Transform target)
    {
        if (hidden.Exists(entry => entry.Object == target.gameObject)) return;
        hidden.Add((target.gameObject, target.gameObject.activeSelf));
        target.gameObject.SetActive(false);
    }

    private void Build(InventoryGui inventoryGui)
    {
        RectTransform panel = inventoryGui.m_container;
        gridRect = (RectTransform)inventoryGui.ContainerGrid.transform;
        gridMin = gridRect.offsetMin;
        gridMax = gridRect.offsetMax;
        layoutChanged = true;
        panel.sizeDelta += new Vector2(0, Header + Footer);
        gridRect.offsetMin += new Vector2(0, Footer);
        gridRect.offsetMax -= new Vector2(0, Header);
        foreach (string name in new[] { "sunken", "ContainerScroll" })
            if (panel.Find(name) is RectTransform decoration)
            {
                shifted.Add((decoration, decoration.anchoredPosition));
                decoration.anchoredPosition -= new Vector2(0, (Header - Footer) / 2);
            }
        Hide(inventoryGui.m_containerName.transform);
        Hide(inventoryGui.m_containerWeight.transform.parent);
        Hide(inventoryGui.m_takeAllButton.transform);
        Hide(inventoryGui.m_stackAllButton.transform);
        root = Ugui.Node(panel, "Ragnarok Chest Sort").Stretch();
        float width = panel.rect.width;
        summary = new InventoryGuide.Summary(theme, root, width, "STORAGE", false, vanilla: true);
        string[] labels = { "All", "Gear", "Materials", "Food", "Trophies" };
        float x = 12;
        for (int i = 0; i < labels.Length; i++)
        {
            PackCategory value = (PackCategory)i;
            float tabWidth = i == 2 ? 100 : i == 4 ? 88 : 70;
            Button tab = VanillaInventoryStyle.Button(inventoryGui, root, labels[i], new Rect(x, 50, tabWidth, 30), () => category = value);
            tabs.Add((value, tab));
            x += tabWidth + 4;
        }
        RectTransform footer = Ugui.Node(root, "Actions");
        footer.anchorMin = footer.anchorMax = new Vector2(0, 0);
        footer.pivot = new Vector2(0, 1);
        footer.anchoredPosition = new Vector2(12, Footer - 3);
        footer.sizeDelta = new Vector2(width - 24, 30);
        take = VanillaInventoryStyle.Button(inventoryGui, footer, Localization.instance.Localize("$inventory_takeall"), new Rect(0, 0, 120, 30), () =>
        {
            if (inventoryGui.m_takeAllButton.interactable) inventoryGui.m_takeAllButton.onClick.Invoke();
        });
        place = VanillaInventoryStyle.Button(inventoryGui, footer, "Place Stacks", new Rect(126, 0, 120, 30), () =>
        {
            if (inventoryGui.m_stackAllButton.interactable) inventoryGui.m_stackAllButton.onClick.Invoke();
        });
        AddSort(footer, Equipment.SortMode.Name, "Name", width - 234);
        AddSort(footer, Equipment.SortMode.Category, "Kind", width - 164);
        AddSort(footer, Equipment.SortMode.Weight, "Weight", width - 94);
    }

    private void AddSort(Transform parent, Equipment.SortMode mode, string text, float x) =>
        VanillaInventoryStyle.Button(gui!, parent, text, new Rect(x, 0, 66, 30), () =>
        {
            if (QuickStackCommand.SortChest(gui!, mode) is { } done)
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, done);
        });
}
