using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// Borrow native artwork/fonts, never mutate the game's shared sprites or materials.
internal static class VanillaInventoryStyle
{
    internal static TMP_Text Text(InventoryGui gui, Transform parent, string name, string text,
        float size, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text label = UnityEngine.Object.Instantiate(gui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true), parent);
        label.name = name;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    internal static Button Button(InventoryGui gui, Transform parent, string name, Rect rect, Action click)
    {
        Image source = (Image)gui.m_stackAllButton.targetGraphic;
        Image image = Ugui.Image(parent, name, source.sprite, Color.white, raycast: true).At(rect);
        image.type = source.type;
        image.material = source.material;
        image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = gui.m_stackAllButton.colors;
        button.onClick.AddListener(() => click());
        Text(gui, image.transform, "Label", name, 14, TextAlignmentOptions.Center).Stretch(4, 0, 4, 0);
        return button;
    }

    internal static void Board(Transform sourcePanel, Transform parent)
    {
        Transform source = sourcePanel.Find("Bkg");
        Image? original = source ? source.GetComponent<Image>() : null;
        Image image = Ugui.Image(parent, "Bkg", original ? original!.sprite : null,
            original ? original!.color : Palette.Hex(0x30251B), raycast: true).Stretch(-10, -10, -10, -10);
        if (!original) return;
        image.type = original!.type;
        image.material = original.material;
        image.pixelsPerUnitMultiplier = original.pixelsPerUnitMultiplier;
    }
}

// Category selection spotlights native cells without hiding/reordering any items
// or interfering with native pointer/gamepad handling. Restore even on style switches.
internal sealed class VanillaCategoryGrid : IDisposable
{
    private readonly Dictionary<CanvasGroup, (float Alpha, bool Owned)> groups = new();
    private readonly List<CanvasGroup> expired = new();

    internal void Refresh(InventoryGrid grid, Inventory inventory, PackCategory category, bool dragging)
    {
        // Native grids rebuild their cells when switching chest dimensions.
        expired.Clear();
        foreach (var pair in groups)
            if (!pair.Key) expired.Add(pair.Key!); // Destroyed Unity object, still a non-null dictionary key.
        foreach (CanvasGroup group in expired) groups.Remove(group);
        int index = 0;
        foreach (Transform child in grid.m_gridRoot)
        {
            InventoryElement element = child.GetComponent<InventoryElement>();
            if (!element) continue;
            ItemDrop.ItemData item = inventory.GetItemAt(index % inventory.GetWidth(), index / inventory.GetWidth());
            index++;
            bool owned = !child.TryGetComponent(out CanvasGroup group);
            if (owned) group = child.gameObject.AddComponent<CanvasGroup>();
            if (!groups.ContainsKey(group)) groups.Add(group, (group.alpha, owned));
            bool match = dragging || category == PackCategory.All || item == null ||
                InventoryCategories.Of(item.m_shared) == category || element.m_selected.activeSelf;
            group.alpha = groups[group].Alpha * (match ? 1 : 0.28f);
        }
    }

    public void Dispose()
    {
        foreach (var pair in groups)
            if (pair.Key)
            {
                pair.Key.alpha = pair.Value.Alpha;
                if (pair.Value.Owned) UnityEngine.Object.Destroy(pair.Key);
            }
        groups.Clear();
        expired.Clear();
    }
}
