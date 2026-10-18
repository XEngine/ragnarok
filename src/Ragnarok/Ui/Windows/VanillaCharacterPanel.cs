using System;
using System.Collections.Generic;
using Ragnarok.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// The character slots for Valheim's own inventory: worn gear, then provisions (and Better Archery's quiver
// and arrows), on a second wooden board hung just under the inventory, lined up with its columns. The
// board is Valheim's own wood, drawn the way Valheim draws all its boards (the inventory's board is never
// stretched past its size), and the slots are Valheim's slot prefab, so it reads as part of the game's
// inventory. While a chest is open it folds away, so the chest sits where it always does.
internal sealed class VanillaCharacterPanel : IDisposable
{
    // Gap is the wood between the two boards; both boards' art overhangs its rectangle by Overhang.
    private const float Top = 6, Caption = 22, Bottom = 12, Gap = 6, Overhang = 10;

    private readonly InterfaceSettings settings;
    private readonly InventoryPanel ragnarok;
    private readonly List<Cell> cells = new();
    private readonly Vector3[] corners = new Vector3[4];
    private InventoryGui? gui;
    private RectTransform? root;
    private int built, builtWidth;
    private bool folded;

    internal VanillaCharacterPanel(InterfaceSettings settings, InventoryPanel ragnarok)
    {
        this.settings = settings;
        this.ragnarok = ragnarok;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            root = null;
            cells.Clear();
            gui = current;
        }
        if (!gui) return;
        int layout = InventoryPanel.LayoutOf(settings);
        if (ragnarok.Wanted || layout == 0)
        {
            Dispose();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer) return;
        int width = Player.m_localPlayer.GetInventory().GetWidth();
        if (!root || built != layout || builtWidth != width)
        {
            Dispose();
            Build(gui!, layout, width);
        }
        Fold(gui!.m_container && gui.m_container.gameObject.activeSelf);
        if (!folded) Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        cells.Clear();
    }

    // Folded while a chest is open: the board hidden.
    private void Fold(bool fold)
    {
        if (fold == folded) return;
        folded = fold;
        root!.gameObject.SetActive(!fold);
    }

    private void Build(InventoryGui inventoryGui, int layout, int columns)
    {
        built = layout;
        builtWidth = columns;
        RectTransform player = inventoryGui.m_player;
        InventoryGrid grid = inventoryGui.m_playerGrid;
        float cell = grid.m_elementSpace, rowWidth = columns * cell;
        Rect pr = player.rect;
        float left = GridLeft(grid, player, cell) - pr.xMin;

        List<(Vector2i Position, string Label, bool Worn)> worn = new(), provisions = new();
        for (int i = 0; i < CharacterSlots.Gear.Length; i++)
            if (settings.Features.ExtraUtilitySlots.Value || Array.IndexOf(CharacterSlots.ExtraUtility, i) < 0)
                worn.Add((CharacterSlots.GearSlot(i), Capital(InventoryPanel.GearNames[i]), false));
        for (int i = 0; i < CharacterSlots.Provisions; i++)
            provisions.Add((CharacterSlots.ProvisionSlot(i), "Food", true));
        if (BetterArcheryCompat.Active)
        {
            worn.Add((CharacterSlots.GearSlot(CharacterSlots.QuiverIndex), "Quiver", false));
            for (int i = 0; i < BetterArcheryCompat.ArrowSlots; i++)
                provisions.Add((BetterArcheryCompat.ArrowSlot(i), "Arrow", true));
        }
        // A row longer than the grid gets slightly smaller slots, so it never pokes out of the board.
        float wornCell = Mathf.Min(cell, rowWidth / worn.Count), provisionCell = Mathf.Min(cell, rowWidth / provisions.Count);
        float height = Top + Caption + wornCell + Caption + provisionCell + Bottom;

        // Same width as the inventory, hung under it: its top art edge Gap below the inventory's bottom art edge.
        root = Ugui.Node(player, "Ragnarok Character Vanilla");
        root.anchorMin = root.anchorMax = Vector2.zero;
        root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(0, -(Overhang * 2 + Gap));
        root.sizeDelta = new Vector2(pr.width, height);

        // Valheim's wood board (raycast: clicks on it must not fall through to the "drop item" area).
        Transform? bkg = player.Find("Bkg");
        Image? source = bkg ? bkg!.GetComponent<Image>() : null;
        Image back = Ugui.Image(root, "Bkg", source ? source!.sprite : null, source ? source!.color : new Color(0, 0, 0, 0.75f), raycast: true)
            .Stretch(-Overhang, -Overhang, -Overhang, -Overhang);
        if (source)
        {
            back.type = source!.type;
            back.material = source.material;
            back.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        }

        TMP_Text captionFont = inventoryGui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true);
        float y = Top;
        AddCaption(captionFont, "Worn", left, y, rowWidth);
        y += Caption;
        for (int i = 0; i < worn.Count; i++)
            AddCell(grid, worn[i].Position, left + i * wornCell, y, wornCell, worn[i].Label, worn[i].Worn);
        y += wornCell;
        AddCaption(captionFont, "Provisions", left, y, rowWidth);
        y += Caption;
        for (int i = 0; i < provisions.Count; i++)
            AddCell(grid, provisions[i].Position, left + i * provisionCell, y, provisionCell, provisions[i].Label, provisions[i].Worn);

        folded = true;
        root.gameObject.SetActive(false);
    }

    // Where the grid's first column starts, in the panel's space.
    private float GridLeft(InventoryGrid grid, RectTransform player, float cell)
    {
        Transform gridRoot = grid.m_gridRoot;
        RectTransform? first = gridRoot && gridRoot.childCount > 0 ? gridRoot.GetChild(0) as RectTransform : null;
        RectTransform measured = first ? first! : grid.m_gridRoot;
        measured.GetWorldCorners(corners);
        float min = float.MaxValue, max = float.MinValue;
        foreach (Vector3 corner in corners)
        {
            float x = player.InverseTransformPoint(corner).x;
            min = Mathf.Min(min, x);
            max = Mathf.Max(max, x);
        }
        return first ? (min + max) / 2 - cell / 2 : min;
    }

    private static string Capital(string name) => name.Substring(0, 1) + name.Substring(1).ToLowerInvariant();

    private void AddCaption(TMP_Text font, string text, float x, float y, float w)
    {
        TMP_Text caption = UnityEngine.Object.Instantiate(font, root);
        caption.name = text;
        caption.text = text;
        caption.fontSize = 14;
        caption.alignment = TextAlignmentOptions.BottomLeft;
        caption.raycastTarget = false;
        caption.At(x + 6, y, w - 12, Caption - 3);
    }

    private void AddCell(InventoryGrid grid, Vector2i position, float x, float y, float size, string label, bool worn)
    {
        GameObject go = UnityEngine.Object.Instantiate(grid.m_elementPrefab, root);
        go.name = $"Slot {position.x},{position.y}";
        RectTransform rect = (RectTransform)go.transform;
        // Centre the prefab in its cell, like Valheim's grid spacing; a smaller cell shrinks it to match.
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x + size / 2, -(y + size / 2));
        rect.localScale = Vector3.one * (size / grid.m_elementSpace);

        InventoryElement element = go.GetComponent<InventoryElement>();
        element.Initialize(position.x, position.y);
        element.m_selected.SetActive(false);

        // Valheim's key-number label becomes the empty slot's name.
        TMP_Text placeholder = go.transform.Find("binding").GetComponent<TMP_Text>();
        placeholder.enabled = true;
        placeholder.text = label;
        placeholder.fontSize = 13;
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.color = new Color(1, 1, 1, 0.35f);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.overflowMode = TextOverflowModes.Overflow;
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = placeholder.rectTransform.offsetMax = Vector2.zero;

        Cell c = new(element, placeholder, position, worn);
        UIInputHandler input = go.GetComponentInChildren<UIInputHandler>();
        input.m_onLeftClick += _ => SlotActions.Click(gui!, grid, position, c.Item, right: false);
        input.m_onRightDown += _ => SlotActions.Click(gui!, grid, position, c.Item, right: true);
        cells.Add(c);
    }

    private void Refresh(Player player)
    {
        Inventory inventory = player.GetInventory();
        ItemDrop.ItemData drag = SlotActions.DragItem(gui!);
        RectTransform anchor = gui!.m_playerGrid.m_tooltipAnchor;
        foreach (Cell c in cells)
            c.Show(inventory.GetItemAt(c.Position.x, c.Position.y), player, drag, anchor);
    }

    private sealed class Cell
    {
        internal readonly Vector2i Position;
        internal ItemDrop.ItemData? Item;
        private readonly InventoryElement element;
        private readonly TMP_Text placeholder;
        private readonly bool markWorn;
        private ItemDrop.ItemData? tooltipFor;
        private int tooltipStack = -1;

        internal Cell(InventoryElement element, TMP_Text placeholder, Vector2i position, bool markWorn)
        {
            this.element = element;
            this.placeholder = placeholder;
            Position = position;
            this.markWorn = markWorn;
        }

        // Mirrors InventoryGrid.UpdateGui for one element.
        internal void Show(ItemDrop.ItemData? item, Player player, ItemDrop.ItemData drag, RectTransform anchor)
        {
            Item = item;
            placeholder.enabled = item == null;
            element.m_icon.enabled = item != null;
            if (item == null)
            {
                element.m_durability.gameObject.SetActive(false);
                element.m_amount.enabled = element.m_quality.enabled = false;
                element.m_equiped.enabled = element.m_queued.enabled = element.m_noteleport.enabled = element.m_food.enabled = false;
                element.m_tooltip.m_text = element.m_tooltip.m_topic = "";
                tooltipFor = null;
                return;
            }

            element.m_icon.sprite = item.GetIcon();
            element.m_icon.color = item == drag ? Color.grey : Color.white;
            bool worn = item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability();
            element.m_durability.gameObject.SetActive(worn);
            if (worn)
            {
                if (item.m_durability <= 0f)
                {
                    element.m_durability.SetValue(1f);
                    element.m_durability.SetColor(Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.clear);
                }
                else
                {
                    element.m_durability.SetValue(item.GetDurabilityPercentage());
                    element.m_durability.ResetColor();
                }
            }
            // Everything in the worn row is in use (the extra utility slots add their effect without being
            // "equipped"), so it all gets Valheim's blue equipped backing; elsewhere only equipped items do.
            element.m_equiped.enabled = !markWorn || item.m_equipped;
            element.m_queued.enabled = player.IsEquipActionQueued(item);
            element.m_noteleport.enabled = !item.m_shared.m_teleportable && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);

            ItemDrop.ItemData.SharedData s = item.m_shared;
            bool food = s.m_itemType == ItemDrop.ItemData.ItemType.Consumable && (s.m_food > 0 || s.m_foodStamina > 0 || s.m_foodEitr > 0);
            element.m_food.enabled = food;
            if (food)
                element.m_food.color =
                    s.m_food < s.m_foodEitr / 2f && s.m_foodStamina < s.m_foodEitr / 2f ? new Color(0.6f, 0.6f, 1f) :
                    s.m_foodStamina < s.m_food / 2f ? new Color(1f, 0.5f, 0.5f) :
                    s.m_food < s.m_foodStamina / 2f ? new Color(1f, 1f, 0.5f) : Color.white;

            element.m_quality.enabled = s.m_maxQuality > 1;
            if (s.m_maxQuality > 1) element.m_quality.text = item.m_quality.ToString();
            element.m_amount.enabled = s.m_maxStackSize > 1;
            if (s.m_maxStackSize > 1) element.m_amount.text = $"{item.m_stack}/{s.m_maxStackSize}";

            // GetTooltip builds a long string; only when the item changes.
            if (tooltipFor == item && tooltipStack == item.m_stack) return;
            tooltipFor = item;
            tooltipStack = item.m_stack;
            element.m_tooltip.Set(s.m_name, item.GetTooltip(), anchor);
        }
    }
}
