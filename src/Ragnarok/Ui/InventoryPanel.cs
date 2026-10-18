using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ragnarok.Equipment;

namespace Ragnarok.Ui;

// The Ragnarök inventory: our own grid, header and item tooltip drawn over Valheim's player panel.
// Valheim's grid stays alive but invisible underneath; every click is forwarded to its own
// handlers, so picking up, splitting, equipping, moving to chests and dropping work unchanged.
internal sealed class InventoryPanel : IDisposable
{
    private const float Pad = 8, Gap = 4, SideGap = 16, FooterHeight = 26;

    private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem = QuickStackCommand.DragItem;
    private static readonly AccessTools.FieldRef<InventoryGrid, Vector2i> GamepadSelection =
        AccessTools.FieldRefAccess<InventoryGrid, Vector2i>("m_selected");

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly List<SlotView> slots = new();
    private readonly Dictionary<int, SlotView> extras = new();
    private readonly List<Image> backgrounds = new();
    private InventoryGui? gui;
    private RectTransform? root;
    private ItemCard? card;
    private StatLine? stats;
    private Vector2 builtSize;
    private float panelHeight;
    private int builtWidth, builtHeight;
    private int builtCharacter;
    // Whether the second row is a hotbar too (HotbarRows), so its slots carry the keys 1-8 as well.
    private bool builtSecond;
    private SlotView? hovered;
    private TextMeshProUGUI? chests, result;
    private float resultTime = -10;
    private readonly List<(BepInEx.Configuration.ConfigEntry<bool> Entry, Image Chip, Image Icon)> toggles = new();

    internal InventoryPanel(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    // The inventory's slot size, so the chest panel draws the same slots.
    internal float SlotSize { get; private set; } = 60;

    internal bool Wanted => settings.Inventory.Value switch
    {
        InventoryStyle.Ragnarok => true,
        InventoryStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            // New game scene: the previous objects went with the old one.
            undo.Clear();
            slots.Clear();
            extras.Clear();
            root = null;
            card = null;
            gui = current;
        }
        if (!gui) return;

        // Character slots are kept in order every frame, inventory open or not: gear put on from the
        // hotbar moves into its slot, gear taken off goes back to the grid.
        if (Player.m_localPlayer)
        {
            if (CharacterSlots.Enabled()) CharacterSlots.Settle(Player.m_localPlayer, DragItem(gui!));
            else CharacterSlots.Empty(Player.m_localPlayer.GetInventory());
            CharacterSlots.SyncEffects(Player.m_localPlayer);
        }

        if (!Wanted)
        {
            Restore();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer) return;

        Inventory inventory = Player.m_localPlayer.GetInventory();
        RectTransform panel = gui!.m_player;
        if (!root || panel.rect.size != builtSize || inventory.GetWidth() != builtWidth || CharacterSlots.GridRows(inventory) != builtHeight ||
            builtCharacter != CharacterLayout || builtSecond != HotbarRows.Enabled)
            Build(panel, inventory);
        Refresh(Player.m_localPlayer, inventory);
    }

    public void Dispose() => Restore();

    private void Restore()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        if (card != null) card.Destroy();
        root = null;
        card = null;
        slots.Clear();
        extras.Clear();
        hovered = null;
    }

    // Construction ---------------------------------------------------------------------------

    private void Build(RectTransform panel, Inventory inventory)
    {
        Restore();
        HideVanilla(panel);
        builtSize = panel.rect.size;
        builtWidth = inventory.GetWidth();
        builtHeight = CharacterSlots.GridRows(inventory);
        builtSecond = HotbarRows.Enabled;
        float width = builtSize.x, height = builtSize.y;

        int cols = builtWidth, rows = builtHeight;
        // Slots keep the size they had in Valheim's panel; the board then fits tightly around them.
        float areaHeight = height - 56 - Pad;
        float size = Mathf.Floor(Mathf.Min((width - Pad * 2 - Gap * (cols - 1)) / cols, (areaHeight - Gap * (rows - 1)) / rows, 64));
        SlotSize = size;
        float gridRight = Pad + cols * size + (cols - 1) * Gap, gridBottom = Pad + rows * size + (rows - 1) * Gap;
        width = gridRight + Pad;
        panelHeight = gridBottom + 10 + FooterHeight + Pad;

        // After the style guide: the slots on a plain board, the stats on a tab over its top edge, and
        // Quick stack and sorting along its foot.
        root = Ugui.Node(panel, "Ragnarok Inventory").At(0, 0, width, panelHeight);
        // Raycast target: clicks on the frame must not fall through to Valheim's "drop item" area.
        backgrounds.Clear();
        backgrounds.Add(Ugui.Image(root, "Frame", theme.Board, Color.white, raycast: true).Stretch());
        stats = new StatLine(theme, root, width - 14, -5, withArmor: true);

        InventoryGrid grid = gui!.m_playerGrid;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                SlotView slot = new(theme, root, new Rect(Pad + x * (size + Gap), Pad + y * (size + Gap), size, size),
                    new Vector2i(x, y), y == 0 || (y == 1 && builtSecond) ? (x + 1).ToString() : null);
                slot.Input.Down = button => Click(grid, slot, button);
                slot.Input.Hover = on => hovered = on ? slot : hovered == slot ? null : hovered;
                slots.Add(slot);
            }

        FooterRow(Pad, gridBottom + 10, gridRight - Pad);
        builtCharacter = CharacterLayout;
        if (builtCharacter > 0) CharacterColumn(grid, inventory, width + 10, Pad + 4, size);
        card = new ItemCard(theme, panel);
    }

    // Along the foot: Quick stack with its "keep" toggles and the nearby chest count (or the last
    // result), the sort keys at the right.
    private void FooterRow(float x, float y, float w)
    {
        Ornaments.Keycap(root!, theme, "Quick stack", x, y, 104, FooterHeight, QuickStackNow);

        // "Keep" toggles as small icon chips beside the button; hovering one names it in the result line.
        toggles.Clear();
        float chip = FooterHeight, gap = 4, row = x + 110;
        AddToggle(settings.KeepWeapons, theme.IconSword, "weapons", row, y, chip);
        AddToggle(settings.KeepConsumables, theme.IconFood, "food", row + chip + gap, y, chip);
        AddToggle(settings.KeepTools, theme.IconHammer, "tools", row + (chip + gap) * 2, y, chip);

        float sortGap = 3, sortWidth = 38, sortX = x + w - sortWidth * 3 - sortGap * 2;
        float textX = row + chip * 3 + gap * 2 + 10, textW = sortX - textX - 8;
        chests = Ugui.Text(root!, "Chests", "", theme.Body, 12, Palette.Muted).At(textX, y, textW, FooterHeight);
        result = Ugui.Text(root!, "Result", "", theme.Subtle, 12, Palette.Bone).At(textX, y, textW, FooterHeight);

        AddSort(Equipment.SortMode.Name, "A–Z", "Sort by name", sortX, y, sortWidth);
        AddSort(Equipment.SortMode.Category, "Type", "Sort by type", sortX + sortWidth + sortGap, y, sortWidth);
        AddSort(Equipment.SortMode.Weight, "Kg", "Sort by weight", sortX + (sortWidth + sortGap) * 2, y, sortWidth);
    }

    private void AddSort(Equipment.SortMode mode, string label, string hint, float x, float y, float w)
    {
        Button chip = Ornaments.Keycap(root!, theme, label, x, y, w, FooterHeight, () =>
        {
            if (QuickStackCommand.Sort(settings, gui!, mode) is not { } text) return;
            result!.text = text;
            resultTime = Time.unscaledTime;
        }, 12);
        chip.OnHover(on =>
        {
            if (on) ShowHint(hint);
        });
    }

    // Which character panel is built: 0 none; else 1, +1 with the extra utility slots, +2 with Better Archery's quiver.
    internal static int LayoutOf(InterfaceSettings settings) => !settings.CharacterSlots.Value
        ? 0
        : 1 + (settings.ExtraUtilitySlots.Value ? 1 : 0) + (BetterArcheryCompat.Active ? 2 : 0);

    private int CharacterLayout => LayoutOf(settings);

    // The character panel, its own frame right of the inventory: worn gear as a paper doll (body down
    // the left, cape, trinket and Better Archery's quiver in the middle, the three utility slots down the
    // right), then provisions (food, meads) that ride along without taking grid space, then the quiver's
    // three arrow slots when Better Archery is installed.
    internal static readonly string[] GearNames = { "HEAD", "CHEST", "LEGS", "CAPE", "UTILITY", "TRINKET", "UTILITY", "UTILITY" };
    internal static readonly Vector2Int[] GearCells =
    {
        new(0, 0), new(0, 1), new(0, 2), new(1, 0), new(2, 0), new(1, 1), new(2, 1), new(2, 2)
    };

    private void CharacterColumn(InventoryGrid grid, Inventory inventory, float x, float top, float size)
    {
        bool quiver = BetterArcheryCompat.Active;
        float gear = size * 3 + Gap * 2, provisions = size * 2 + Gap, between = SideGap;
        float w = Pad * 2 + gear + between + provisions + (quiver ? between + size : 0);
        backgrounds.Add(Ugui.Image(root!, "Character Frame", theme.Board, Color.white, raycast: true).At(x, 0, w, panelHeight));

        float left = x + Pad, right = left + gear + between, slotsTop = top + 22;
        // The labels step in from the board's corner flourish.
        Caption("worn", left + 14, top, gear - 14);
        Caption("provisions", right + 4, top, provisions);
        Ugui.Image(root!, "Character Divider", theme.White, Palette.Rule).At(right - between / 2, top, 1, panelHeight - top - Pad);

        for (int i = 0; i < CharacterSlots.Gear.Length; i++)
        {
            if (!settings.ExtraUtilitySlots.Value && Array.IndexOf(CharacterSlots.ExtraUtility, i) >= 0) continue;
            Vector2Int cell = GearCells[i];
            AddExtra(grid, CharacterSlots.GearSlot(i),
                new Rect(left + cell.x * (size + Gap), slotsTop + cell.y * (size + Gap), size, size), GearNames[i], null, worn: false);
        }
        for (int i = 0; i < CharacterSlots.Provisions; i++)
        {
            int col = i % 2, row = i / 2;
            AddExtra(grid, CharacterSlots.ProvisionSlot(i),
                new Rect(right + col * (size + Gap), slotsTop + row * (size + Gap), size, size), null, theme.IconFood, worn: true);
        }
        if (!quiver) return;

        // Better Archery: the worn quiver fills the empty middle cell, its arrows get their own column.
        AddExtra(grid, CharacterSlots.GearSlot(CharacterSlots.QuiverIndex),
            new Rect(left + (size + Gap), slotsTop + 2 * (size + Gap), size, size), "QUIVER", null, worn: false);
        float arrows = right + provisions + between;
        Ugui.Image(root!, "Arrows Divider", theme.White, Palette.Rule).At(arrows - between / 2, top, 1, panelHeight - top - Pad);
        Caption("arrows", arrows, top, size + 40);
        for (int i = 0; i < BetterArcheryCompat.ArrowSlots; i++)
            AddExtra(grid, BetterArcheryCompat.ArrowSlot(i),
                new Rect(arrows, slotsTop + i * (size + Gap), size, size), "ARROW", null, worn: true);
    }

    private void AddExtra(InventoryGrid grid, Vector2i position, Rect area, string? label, Sprite? glyph, bool worn)
    {
        SlotView slot = new(theme, root!, area, position, null, label, glyph, worn);
        slot.Input.Down = button => Click(grid, slot, button);
        slot.Input.Hover = on => hovered = on ? slot : hovered == slot ? null : hovered;
        extras[Key(position)] = slot;
    }

    private static int Key(Vector2i pos) => pos.y * 64 + pos.x;

    private void Caption(string text, float x, float y, float w) => Ornaments.Section(root!, theme, text, x, y, w);

    // A lit chip means "kept in the inventory"; clicking flips the setting, which also saves it.
    // The icon is a Ragnarök line glyph, so the chip needs no label.
    private void AddToggle(BepInEx.Configuration.ConfigEntry<bool> entry, Sprite glyph, string label, float x, float y, float size)
    {
        Image chip = Ugui.Image(root!, label, theme.Button, Color.white).At(x, y, size, size);
        Image icon = Ugui.Image(chip.transform, "Icon", glyph, Color.white).At(4, 4, size - 8, size - 8);
        Ugui.HitArea(chip.transform, "Hit", () =>
        {
            entry.Value = !entry.Value;
            ShowHint(label, entry.Value);
        }).Stretch().OnHover(on =>
        {
            if (on) ShowHint(label, entry.Value);
        });
        toggles.Add((entry, chip, icon));
    }

    private void ShowHint(string label, bool keep) => ShowHint(keep ? "Keep " + label : "Stack " + label);

    private void ShowHint(string text)
    {
        if (!result) return;
        result!.text = text;
        resultTime = Time.unscaledTime - 1.5f;
    }

    private void QuickStackNow()
    {
        if (QuickStackCommand.Run(settings, gui!) is not { } text) return;
        result!.text = text;
        resultTime = Time.unscaledTime;
    }

    // Valheim's panel and grid stay active (they run the item logic) but draw and catch nothing.
    private void HideVanilla(RectTransform panel)
    {
        if (panel.TryGetComponent(out Image background) && background.enabled)
        {
            background.enabled = false;
            undo.Add((background, () => background.enabled = true));
        }
        foreach (Transform child in panel)
        {
            // The chest panel is a child of Valheim's player panel; it must stay visible.
            if (child.name is "Ragnarok Inventory" or ItemCard.Name || child == gui!.m_container) continue;
            bool added = !child.TryGetComponent(out CanvasGroup group);
            if (added) group = child.gameObject.AddComponent<CanvasGroup>();
            float alpha = group.alpha;
            bool blocks = group.blocksRaycasts, interactable = group.interactable;
            group.alpha = 0;
            group.blocksRaycasts = group.interactable = false;
            CanvasGroup g = group;
            undo.Add((g, () =>
            {
                if (added) UnityEngine.Object.Destroy(g);
                else { g.alpha = alpha; g.blocksRaycasts = blocks; g.interactable = interactable; }
            }));
        }
    }

    // Slot clicks go to Valheim's handlers; Equipment.SlotActions and its patches add the slot rules.
    private void Click(InventoryGrid grid, SlotView slot, PointerEventData.InputButton button)
    {
        if (button is PointerEventData.InputButton.Left or PointerEventData.InputButton.Right)
            SlotActions.Click(gui!, grid, slot.Position, slot.Item, button == PointerEventData.InputButton.Right);
    }

    // Per frame ------------------------------------------------------------------------------

    private void Refresh(Player player, Inventory inventory)
    {
        foreach (SlotView slot in slots) slot.Item = null;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            int index = item.m_gridPos.y * builtWidth + item.m_gridPos.x;
            if (index >= 0 && index < slots.Count) slots[index].Item = item;
        }

        // The panels' metal at the configured opacity (they still catch clicks when faint).
        Color tint = Color.white.WithAlpha(settings.InventoryOpacity.Value);
        foreach (Image background in backgrounds)
            if (background && background.color != tint) background.color = tint;

        foreach (SlotView slot in extras.Values) slot.Item = null;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            if (extras.TryGetValue(Key(item.m_gridPos), out SlotView extra) && !CharacterSlots.InGrid(inventory, item.m_gridPos))
                extra.Item = item;

        InventoryGrid grid = gui!.m_playerGrid;
        bool pad = ZInput.IsGamepadActive() && !ZInput.IsMouseActive() && grid.m_uiGroup && grid.m_uiGroup.IsActive;
        Vector2i selected = GamepadSelection(grid);
        ItemDrop.ItemData drag = DragItem(gui);
        SlotView? focus = hovered;
        foreach (SlotView slot in slots)
        {
            bool cursor = pad && slot.Position == selected;
            if (cursor) focus = slot;
            slot.Refresh(player, drag, cursor, slot == hovered);
        }
        foreach (SlotView slot in extras.Values) slot.Refresh(player, drag, false, slot == hovered);

        stats!.Set(Mathf.RoundToInt(player.GetBodyArmor()), inventory.GetTotalWeight(), player.GetMaxCarryWeight());

        int nearby = NearbyStorage.StorageRuntime.Registry?.Nearby(player).Count ?? 0;
        chests!.text = nearby == 0 ? "None in range" : nearby == 1 ? "1 chest in range" : nearby + " chests in range";
        float age = Time.unscaledTime - resultTime;
        result!.alpha = age < 3f ? 1f : Mathf.Clamp01(1f - (age - 3f));
        chests.alpha = 1f - result.alpha;
        foreach (var t in toggles)
        {
            bool on = t.Entry.Value;
            t.Chip.sprite = on ? theme.SegmentOn : theme.Button;
            t.Icon.color = on ? Palette.Bone : Palette.Faint;
        }

        card!.Show(focus?.Item, player, focus?.Area ?? default);
    }
}

// One inventory slot: chamfered tile, icon, stack, quality diamond, durability, food and markers.
internal sealed class SlotView
{
    internal readonly Vector2i Position;
    internal readonly Rect Area;
    internal readonly SlotInput Input;
    internal ItemDrop.ItemData? Item;

    private readonly Theme theme;
    private readonly Image tile, ring, icon, durabilityTrack, durability, qualityGem, food, noTeleport;
    private readonly TextMeshProUGUI amount, quality;
    private readonly Component? placeholder;
    private readonly bool markWorn;
    private string shownAmount = "", shownQuality = "";

    // label/glyph: a faint placeholder for an empty character slot. markWorn: ring equipped items
    // (pointless in the gear slots, where everything is worn).
    internal SlotView(Theme theme, Transform parent, Rect r, Vector2i position, string? key, string? label = null, Sprite? glyph = null,
        bool markWorn = true)
    {
        this.theme = theme;
        this.markWorn = markWorn;
        Position = position;
        Area = r;
        float s = r.width;
        tile = Ugui.Image(parent, $"Slot {position.x},{position.y}", theme.Slot, Color.white, raycast: true).At(r);
        Input = tile.gameObject.AddComponent<SlotInput>();
        Transform t = tile.transform;
        if (label != null)
            placeholder = Ugui.Text(t, "Placeholder", label.ToLowerInvariant(), theme.Subtle, 11, Palette.Dim, TextAlignmentOptions.Center)
                .Stretch(2, 0, 2, 0);
        else if (glyph != null)
            placeholder = Ugui.Image(t, "Placeholder", glyph, Palette.Faint.WithAlpha(0.5f)).At(s * 0.3f, s * 0.3f, s * 0.4f, s * 0.4f);

        icon = Ugui.Image(t, "Icon", null, Color.white).At(s * 0.14f, s * 0.12f, s * 0.72f, s * 0.72f);
        Ugui.ItemIcon(icon);
        ring = Ugui.Image(t, "Ring", theme.SlotRing, Color.white).Stretch();

        if (key != null)
            Ugui.Text(t, "Key", key, theme.Strong, 11, Palette.Muted, TextAlignmentOptions.TopLeft).At(5, 3, 16, 14).overflowMode =
                TextOverflowModes.Overflow;
        noTeleport = Ugui.Image(t, "No Teleport", theme.NoTeleport, Palette.Crimson).At(key != null ? 15 : 4, 4, 12, 12);

        amount = Ugui.Text(t, "Amount", "", theme.Strong, 14, Palette.Tan, TextAlignmentOptions.BottomRight).At(2, s - 22, s - 6, 18);
        amount.overflowMode = TextOverflowModes.Overflow;

        // The level, a quiet number in the top-right corner.
        qualityGem = Ugui.Image(t, "Quality", null, Color.clear).At(s - 19, 3, 16, 14);
        quality = Ugui.Text(qualityGem.transform, "Level", "", theme.Strong, 11, Palette.Faint, TextAlignmentOptions.TopRight).Stretch();
        quality.overflowMode = TextOverflowModes.Overflow;

        food = Ugui.Image(t, "Food", theme.Diamond, Color.white).At(6, s - 13, 8, 8);
        durabilityTrack = Ugui.Image(t, "Durability Track", theme.White, Palette.Ink.WithAlpha(0.9f)).At(6, s - 6, s - 12, 3);
        durability = Ugui.Image(durabilityTrack.transform, "Durability", theme.White, Palette.Gold).At(0, 0, s - 12, 3);
    }

    internal void Refresh(Player player, ItemDrop.ItemData? drag, bool cursor, bool hover)
    {
        ItemDrop.ItemData? item = Item;
        tile.sprite = hover || cursor ? theme.SlotHover : theme.Slot;

        if (placeholder) SetActive(placeholder!, item == null);
        bool equipped = markWorn && item != null && item.m_equipped;
        bool queued = item != null && player.IsEquipActionQueued(item);
        SetActive(ring, cursor || equipped || queued);
        if (cursor) ring.color = Palette.Crimson;
        else if (equipped) ring.color = Color.white;
        else if (queued) ring.color = Palette.Muted;

        SetActive(icon, item != null);
        if (item == null)
        {
            SetActive(amount, false);
            SetActive(qualityGem, false);
            SetActive(food, false);
            SetActive(noTeleport, false);
            SetActive(durabilityTrack, false);
            return;
        }

        Sprite sprite = item.GetIcon();
        if (icon.sprite != sprite) icon.sprite = sprite;
        icon.color = item == drag ? new Color(1, 1, 1, 0.3f) : Color.white;

        ItemDrop.ItemData.SharedData shared = item.m_shared;
        bool stack = shared.m_maxStackSize > 1;
        SetActive(amount, stack);
        if (stack) SetText(amount, ref shownAmount, item.m_stack.ToString());

        bool levelled = shared.m_maxQuality > 1;
        SetActive(qualityGem, levelled);
        if (levelled) SetText(quality, ref shownQuality, item.m_quality.ToString());

        bool worn = shared.m_useDurability && item.m_durability < item.GetMaxDurability();
        SetActive(durabilityTrack, worn);
        if (worn)
        {
            float pct = item.GetDurabilityPercentage();
            durability.rectTransform.sizeDelta = new Vector2((durabilityTrack.rectTransform.rect.width) * Mathf.Max(pct, 0.04f), 3);
            durability.color = item.m_durability <= 0f
                ? (Mathf.Sin(Time.time * 10f) > 0 ? Palette.Crimson : Color.clear)
                : pct < 0.25f ? Palette.Ember : Palette.Gold;
        }

        bool isFood = shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable &&
                      (shared.m_food > 0 || shared.m_foodStamina > 0 || shared.m_foodEitr > 0);
        SetActive(food, isFood);
        if (isFood) food.color = FoodColor(shared);

        SetActive(noTeleport, !shared.m_teleportable && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll));
    }

    // Same colour as the HUD's food pips: the stat this food mostly restores.
    private static Color FoodColor(ItemDrop.ItemData.SharedData s)
    {
        if (s.m_foodEitr >= s.m_food && s.m_foodEitr >= s.m_foodStamina) return Palette.Eitr;
        return s.m_food >= s.m_foodStamina ? Palette.Health : Palette.Stamina;
    }

    private static void SetActive(Component c, bool on)
    {
        if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void SetText(TMP_Text text, ref string shown, string value)
    {
        if (shown == value) return;
        shown = value;
        text.text = value;
    }
}

internal sealed class SlotInput : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    internal Action<PointerEventData.InputButton>? Down;
    internal Action<bool>? Hover;

    public void OnPointerDown(PointerEventData eventData) => Down?.Invoke(eventData.button);

    public void OnPointerEnter(PointerEventData eventData) => Hover?.Invoke(true);

    public void OnPointerExit(PointerEventData eventData) => Hover?.Invoke(false);

    private void OnDisable() => Hover?.Invoke(false);
}
