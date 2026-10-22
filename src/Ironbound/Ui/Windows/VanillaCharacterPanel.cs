using System;
using System.Collections.Generic;
using Ironbound.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// A native wood side board aligned to the inventory, with native slot prefabs,
// stacked Worn/Provisions and the shared stats/set dashboard below them.
internal sealed class VanillaCharacterPanel : IDisposable
{
    private const float Caption = 22, Gap = 6, Overhang = 10;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly InventoryPanel ironbound;
    private readonly List<Cell> cells = new();
    private InventoryGui? gui;
    private RectTransform? root;
    private int built, builtWidth;
    private Vector2 builtSize;
    private CharacterDashboard? dashboard;

    internal VanillaCharacterPanel(InterfaceSettings settings, Theme theme, InventoryPanel ironbound)
    {
        this.settings = settings;
        this.theme = theme;
        this.ironbound = ironbound;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            root = null;
            dashboard = null;
            cells.Clear();
            gui = current;
        }
        if (!gui) return;
        int layout = InventoryPanel.LayoutOf(settings);
        if (ironbound.Wanted || layout == 0)
        {
            Dispose();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer) return;
        int width = Player.m_localPlayer.GetInventory().GetWidth();
        if (!root || built != layout || builtWidth != width || builtSize != gui!.m_player.rect.size)
        {
            Dispose();
            Build(gui!, layout, width);
        }
        Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null;
        dashboard = null;
        cells.Clear();
    }

    private void Build(InventoryGui inventoryGui, int layout, int columns)
    {
        built = layout;
        builtWidth = columns;
        RectTransform player = inventoryGui.m_player;
        InventoryGrid grid = inventoryGui.m_playerGrid;
        builtSize = player.rect.size;
        bool quiver = BetterArcheryCompat.Active;
        float width = grid.m_elementSpace * 4 + 24, height = builtSize.y;
        float inner = width - 24;
        float cell = Mathf.Max(1, Mathf.Floor(Mathf.Min(grid.m_elementSpace,
            (inner - Gap * 4) / 5, (height - (quiver ? 236 : 204)) / (quiver ? 4 : 3))));
        root = Ugui.Node(player, "Ironbound Character Vanilla").At(builtSize.x + Overhang * 2 + Gap, 0, width, height);
        VanillaInventoryStyle.Board(player, root);
        TMP_Text captionFont = inventoryGui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true);
        AddCaption(captionFont, "Worn", 12, 6, inner);
        const float slotsTop = 30;
        int[] positions = { 0, 1, 2, 3, 5, 4, 6, 7 };
        for (int i = 0; i < CharacterSlots.Gear.Length; i++)
        {
            if (!settings.Features.ExtraUtilitySlots.Value && Array.IndexOf(CharacterSlots.ExtraUtility, i) >= 0) continue;
            int position = positions[i];
            AddCell(grid, CharacterSlots.GearSlot(i), 12 + position % 5 * (cell + Gap),
                slotsTop + position / 5 * (cell + Gap), cell, Capital(InventoryPanel.GearNames[i]), false);
        }
        if (quiver)
            AddCell(grid, CharacterSlots.GearSlot(CharacterSlots.QuiverIndex), 12 + 3 * (cell + Gap),
                slotsTop + cell + Gap, cell, "Quiver", false);
        float provisionTitle = slotsTop + cell * 2 + Gap + 10;
        AddCaption(captionFont, "Provisions", 12, provisionTitle, inner);
        float provisionCell = Mathf.Min(cell, (inner - Gap * 5) / 6);
        float provisionTop = provisionTitle + Caption;
        for (int i = 0; i < CharacterSlots.Provisions; i++)
            AddCell(grid, CharacterSlots.ProvisionSlot(i), 12 + i * (provisionCell + Gap), provisionTop, provisionCell, "Food", true);
        if (quiver)
        {
            float arrowTitle = provisionTop + provisionCell + 10;
            AddCaption(captionFont, "Arrows", 12, arrowTitle, inner);
            for (int i = 0; i < BetterArcheryCompat.ArrowSlots; i++)
                AddCell(grid, BetterArcheryCompat.ArrowSlot(i), 12 + i * (cell + Gap), arrowTitle + Caption, cell, "Arrow", true);
        }
        dashboard = new CharacterDashboard(theme, root, 12, height - 12 - CharacterDashboard.Height, inner, vanilla: true);
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
        placeholder.color = new Color(1, 1, 1, 0.7f);
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
        dashboard?.Refresh(player, inventory);
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
