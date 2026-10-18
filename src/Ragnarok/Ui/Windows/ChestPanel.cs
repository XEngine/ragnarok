using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ragnarok.Features.Equipment;

namespace Ragnarok.UI.Windows;

// The Ragnarök chest: the same frame, header and slots as the inventory, drawn over Valheim's container
// panel. Like the inventory, Valheim's own panel and grid stay alive but invisible underneath, and every
// click is forwarded to its handlers, so Take all, Place stacks, moving, splitting and dropping work as
// before. Follows the Inventory style.
internal sealed class ChestPanel : IDisposable
{
    private const float Pad = 16, Gap = 6, Footer = 28, MinWidth = 470;

    private static readonly AccessTools.FieldRef<InventoryGrid, Vector2i> GamepadSelection =
        AccessTools.FieldRefAccess<InventoryGrid, Vector2i>("m_selected");

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Func<bool> wanted;
    private readonly Func<float> slotSize;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly HashSet<Transform> maskedChildren = new();
    private readonly HashSet<CanvasGroup> maskedGroups = new();
    private Image? vanillaBackground;
    private readonly List<SlotView> slots = new();
    private InventoryGui? gui;
    private RectTransform? root;
    private Image? frame;
    private ItemCard? card;
    private TextMeshProUGUI? title;
    private StatLine? stats;
    private int builtWidth, builtHeight;
    private float builtSize;
    private SlotView? hovered;

    internal ChestPanel(InterfaceSettings settings, Theme theme, Func<bool> wanted, Func<float> slotSize)
    {
        this.settings = settings;
        this.theme = theme;
        this.wanted = wanted;
        this.slotSize = slotSize;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            // New game scene: the previous objects went with the old one.
            undo.Clear();
            maskedChildren.Clear();
            maskedGroups.Clear();
            vanillaBackground = null;
            slots.Clear();
            root = null;
            card = null;
            gui = current;
        }
        if (!gui) return;
        if (!wanted())
        {
            Restore();
            return;
        }

        RectTransform panel = gui!.m_container;
        if (!panel || !panel.gameObject.activeInHierarchy || !Player.m_localPlayer || !gui.ContainerGrid) return;
        Inventory? inventory = gui.ContainerGrid.GetInventory();
        if (inventory == null) return;

        float size = slotSize();
        if (!root || inventory.GetWidth() != builtWidth || inventory.GetHeight() != builtHeight || !Mathf.Approximately(size, builtSize))
            Build(panel, inventory, size);
        HideVanilla(panel);
        Refresh(Player.m_localPlayer, inventory);
    }

    public void Dispose() => Restore();

    private void Restore()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        maskedChildren.Clear();
        maskedGroups.Clear();
        vanillaBackground = null;
        ClearPanel();
    }

    private void ClearPanel()
    {
        if (root)
        {
            root!.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
        }
        card?.Destroy();
        root = null;
        card = null;
        slots.Clear();
        hovered = null;
    }

    private void Build(RectTransform panel, Inventory inventory, float size)
    {
        // Keep the vanilla masks across layout rebuilds. Destroy is deferred: restoring
        // and immediately hiding again would reuse groups scheduled for destruction.
        ClearPanel();
        builtWidth = inventory.GetWidth();
        builtHeight = inventory.GetHeight();
        builtSize = size;
        int cols = builtWidth, rows = builtHeight;

        // After the style guide: the slots on a plain board, the chest's name and its actions along the foot,
        // its weight on a tab hanging under the left edge.
        float gridHeight = rows * size + (rows - 1) * Gap;
        float width = Mathf.Max(Pad * 2 + cols * size + (cols - 1) * Gap, MinWidth);
        float footerY = Pad + gridHeight + 10, height = footerY + Footer + Pad;

        root = Ugui.Node(panel, "Ragnarok Chest").At(0, 0, width, height);
        // Raycast target: clicks on the frame must not fall through to Valheim's "drop item" area.
        frame = Ugui.Image(root, "Frame", theme.Board, Color.white, raycast: true).Stretch();
        stats = new StatLine(theme, root, 0, height, withArmor: false, hang: true, left: 14);

        InventoryGrid grid = gui!.ContainerGrid;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                SlotView slot = new(theme, root, new Rect(Pad + x * (size + Gap), Pad + y * (size + Gap), size, size), new Vector2i(x, y), null);
                slot.Input.Down = button => Click(grid, slot, button);
                slot.Input.Hover = on => hovered = on ? slot : hovered == slot ? null : hovered;
                slots.Add(slot);
            }

        // Right to left: Place stacks and Take all (Valheim's own buttons, pressed through), then the sort
        // keys (A-Z, type, weight); the name takes what is left.
        const float chip = 36, chipGap = 3, take = 78, place = 108;
        float right = width - Pad;
        FooterButton("Place stacks", right - place, footerY, place, gui.m_stackAllButton);
        FooterButton("Take all", right - place - 4 - take, footerY, take, gui.m_takeAllButton);
        float sortX = right - place - 4 - take - 12 - chip * 3 - chipGap * 2;
        SortChip(Equipment.SortMode.Name, "A–Z", sortX, footerY, chip);
        SortChip(Equipment.SortMode.Category, "Type", sortX + chip + chipGap, footerY, chip);
        SortChip(Equipment.SortMode.Weight, "Kg", sortX + (chip + chipGap) * 2, footerY, chip);
        title = Ugui.Text(root, "Title", "", theme.Heading, 19, Palette.Bone).At(Pad + 2, footerY - 1, sortX - Pad - 10, Footer);
        // Long names ("Black Metal Chest") shrink to fit rather than being cut.
        title.enableAutoSizing = true;
        title.fontSizeMin = 13;
        title.fontSizeMax = 19;
        card = new ItemCard(theme, panel);
    }

    private void SortChip(Equipment.SortMode mode, string label, float x, float y, float w) =>
        Ornaments.Keycap(root!, theme, label, x, y, w, Footer, () =>
        {
            if (QuickStackCommand.SortChest(gui!, mode) is { } done) Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, done);
        }, 12);

    private void FooterButton(string label, float x, float y, float w, Button vanilla) =>
        Ornaments.Keycap(root!, theme, label, x, y, w, Footer, () =>
        {
            if (vanilla) vanilla.onClick.Invoke();
        });

    // Valheim's panel and grid stay active (they run the item logic) but draw and catch nothing.
    private void HideVanilla(RectTransform panel)
    {
        if (!vanillaBackground && panel.TryGetComponent(out Image background))
        {
            vanillaBackground = background;
            bool enabled = background.enabled;
            undo.Add((background, () => background.enabled = enabled));
        }
        if (vanillaBackground) vanillaBackground!.enabled = false;
        foreach (Transform child in panel)
        {
            if (child.name is "Ragnarok Chest" or ItemCard.Name) continue;
            if (maskedChildren.Add(child) && !child.TryGetComponent<CanvasGroup>(out _))
            {
                CanvasGroup added = child.gameObject.AddComponent<CanvasGroup>();
                undo.Add((added, () => UnityEngine.Object.Destroy(added)));
            }
            // Some nested UI groups ignore their parents; mask those too, including
            // any elements the game creates after opening or resizing the chest grid.
            foreach (CanvasGroup group in child.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (!maskedGroups.Add(group)) continue;
                float alpha = group.alpha;
                bool enabled = group.enabled, blocks = group.blocksRaycasts, interactable = group.interactable;
                CanvasGroup saved = group;
                undo.Add((saved, () =>
                {
                    saved.alpha = alpha;
                    saved.enabled = enabled;
                    saved.blocksRaycasts = blocks;
                    saved.interactable = interactable;
                }));
            }
        }
        foreach (CanvasGroup group in maskedGroups)
            if (group)
            {
                group.enabled = true;
                group.alpha = 0;
                group.blocksRaycasts = group.interactable = false;
            }
    }

    private void Click(InventoryGrid grid, SlotView slot, PointerEventData.InputButton button)
    {
        if (button is PointerEventData.InputButton.Left or PointerEventData.InputButton.Right)
            SlotActions.Click(gui!, grid, slot.Position, slot.Item, button == PointerEventData.InputButton.Right);
    }

    private void Refresh(Player player, Inventory inventory)
    {
        foreach (SlotView slot in slots) slot.Item = null;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            int index = item.m_gridPos.y * builtWidth + item.m_gridPos.x;
            if (index >= 0 && index < slots.Count) slots[index].Item = item;
        }

        Color tint = Color.white.WithAlpha(settings.InventoryOpacity.Value);
        if (frame!.color != tint) frame.color = tint;
        title!.text = Localization.instance.Localize(inventory.GetName());
        stats!.Set(null, inventory.GetTotalWeight(), 0);

        InventoryGrid grid = gui!.ContainerGrid;
        bool pad = ZInput.IsGamepadActive() && !ZInput.IsMouseActive() && grid.m_uiGroup && grid.m_uiGroup.IsActive;
        Vector2i selected = GamepadSelection(grid);
        ItemDrop.ItemData drag = QuickStackCommand.DragItem(gui);
        SlotView? focus = hovered;
        foreach (SlotView slot in slots)
        {
            bool cursor = pad && slot.Position == selected;
            if (cursor) focus = slot;
            slot.Refresh(player, drag, cursor, slot == hovered);
        }
        card!.Show(focus?.Item, player, focus?.Area ?? default);
    }
}
