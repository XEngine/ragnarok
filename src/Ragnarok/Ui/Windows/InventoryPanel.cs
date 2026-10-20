using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ragnarok.Features.Equipment;

namespace Ragnarok.UI.Windows;

// The Ragnarök inventory: our own grid, header and item tooltip drawn over Valheim's player panel.
// Valheim's grid stays alive but invisible underneath; every click is forwarded to its own
// handlers, so picking up, splitting, equipping, moving to chests and dropping work unchanged.
internal sealed class InventoryPanel : IDisposable
{
    private const float Pad = 12, Gap = 6, SideGap = 16, FooterHeight = 26, HeaderHeight = 50;

    private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem = QuickStackCommand.DragItem;
    private static readonly AccessTools.FieldRef<InventoryGrid, Vector2i> GamepadSelection =
        AccessTools.FieldRefAccess<InventoryGrid, Vector2i>("m_selected");

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly HashSet<Transform> maskedChildren = new();
    private readonly List<SlotView> slots = new();
    private readonly Dictionary<int, SlotView> extras = new();
    private readonly List<Image> backgrounds = new();
    private InventoryGui? gui;
    private RectTransform? root;
    private ItemCard? card;
    private InventoryGuide.Summary? summary;
    private CharacterDashboard? dashboard;
    private TextMeshProUGUI? closeHint, quickStackLabel;
    private readonly List<InventoryGuide.Tab> categoryChips = new();
    private PackCategory category;
    private Vector2 builtSize;
    private float panelHeight;
    private int builtWidth, builtHeight;
    private int builtCharacter;
    // Whether the second row is a hotbar too (HotbarRows), so its slots carry the keys 1-8 as well.
    private bool builtSecond;
    private SlotView? hovered;
    private TextMeshProUGUI? chests, result;
    private float resultTime = -10;

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
            Restore();
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
        maskedChildren.Clear();
        ClearPanel();
    }

    private void ClearPanel()
    {
        if (root)
        {
            root!.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(root.gameObject);
        }
        if (card != null) card.Destroy();
        root = null;
        card = null;
        dashboard = null;
        slots.Clear();
        backgrounds.Clear();
        categoryChips.Clear();
        extras.Clear();
        hovered = null;
    }

    // Construction ---------------------------------------------------------------------------

    private void Build(RectTransform panel, Inventory inventory)
    {
        // Keep native masks across rebuilds: Destroy is deferred, so restoring and
        // immediately reusing a CanvasGroup would leave the vanilla grid visible.
        ClearPanel();
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
        float gridRight = Pad + cols * size + (cols - 1) * Gap;
        float gridBottom = HeaderHeight + Pad + rows * size + (rows - 1) * Gap;
        width = gridRight + Pad;
        float categoriesY = gridBottom + 12;
        float actionsY = categoriesY + 28 + 12;
        float hintsY = actionsY + FooterHeight + 10;
        panelHeight = hintsY + 28;

        root = Ugui.Node(panel, "Ragnarok Inventory").At(0, 0, width, panelHeight);
        backgrounds.Clear();
        backgrounds.Add(Ugui.Image(root, "Frame", theme.PackSurface, Palette.HudGlass, raycast: true).Stretch());
        Ugui.Image(root, "Frame Edge", theme.PackEdge, Palette.Hex(0x3D4541)).Stretch();
        summary = new InventoryGuide.Summary(theme, root, width, "INVENTORY", withArmor: true);
        InventoryGuide.Tabs(theme, root, Pad, categoriesY, categoryChips, value => category = value);
        Ugui.Text(root, "Input Hints", "<color=#FFFFFF><b>LMB</b></color> move    <color=#FFFFFF><b>RMB</b></color> use / equip    <color=#FFFFFF><b>SHIFT + LMB</b></color> split    <color=#FFFFFF><b>1–8</b></color> hotbar",
            theme.Body, 10.5f, InventoryCategories.Material).At(Pad, hintsY, width - Pad * 2 - 65, 18);
        closeHint = Ugui.Text(root, "Close Hint", "", theme.Body, 10.5f, InventoryCategories.Material,
            TextAlignmentOptions.MidlineRight).At(width - Pad - 65, hintsY, 65, 18);

        InventoryGrid grid = gui!.m_playerGrid;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                SlotView slot = new(theme, root, new Rect(Pad + x * (size + Gap), HeaderHeight + Pad + y * (size + Gap), size, size),
                    new Vector2i(x, y), y == 0 || (y == 1 && builtSecond) ? (x + 1).ToString() : null, guide: true);
                slot.Input.Down = button => Click(grid, slot, button);
                slot.Input.Hover = on => hovered = on ? slot : hovered == slot ? null : hovered;
                slots.Add(slot);
            }

        FooterRow(Pad, actionsY, gridRight - Pad);
        builtCharacter = CharacterLayout;
        if (builtCharacter > 0) CharacterColumn(grid, inventory, width + 10, size);
        card = new ItemCard(theme, panel);
    }

    // Quick Stack uses the same category selection as the grid. Status sits between it and sorting.
    private void FooterRow(float x, float y, float w)
    {
        Button stack = PackButton("Quick Stack: All", x, y, 150, FooterHeight, QuickStackNow);
        quickStackLabel = stack.GetComponentInChildren<TextMeshProUGUI>(true);
        float row = x + 162;

        float sortGap = 3, sortWidth = 44, sortX = x + w - sortWidth * 3 - sortGap * 2;
        float textX = row, textW = sortX - textX - 8;
        chests = Ugui.Text(root!, "Chests", "", theme.Body, 12, Palette.Muted).At(textX, y, textW, FooterHeight);
        result = Ugui.Text(root!, "Result", "", theme.Subtle, 12, Palette.Bone).At(textX, y, textW, FooterHeight);

        AddSort(Equipment.SortMode.Name, "Name", "Sort by name", sortX, y, sortWidth);
        AddSort(Equipment.SortMode.Category, "Kind", "Sort by kind", sortX + sortWidth + sortGap, y, sortWidth);
        AddSort(Equipment.SortMode.Weight, "Weight", "Sort by weight", sortX + (sortWidth + sortGap) * 2, y, sortWidth);
    }

    private void AddSort(Equipment.SortMode mode, string label, string hint, float x, float y, float w)
    {
        Button chip = PackButton(label, x, y, w, FooterHeight, () =>
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

    private Button PackButton(string label, float x, float y, float width, float height, Action click, float fontSize = 12) =>
        InventoryGuide.ActionButton(theme, root!, label, x, y, width, height, click, fontSize);

    // Which character panel is built: 0 none; else 1, +1 with the extra utility slots, +2 with Better Archery's quiver.
    internal static int LayoutOf(InterfaceSettings settings) => !settings.Features.CharacterSlots.Value
        ? 0
        : 1 + (settings.Features.ExtraUtilitySlots.Value ? 1 : 0) + (BetterArcheryCompat.Active ? 2 : 0);

    private int CharacterLayout => LayoutOf(settings);

    // Same outer dimensions as the side-by-side layout; stack sections inside it.
    internal static readonly string[] GearNames = { "HEAD", "CHEST", "LEGS", "CAPE", "UTILITY", "TRINKET", "UTILITY", "UTILITY" };

    private void CharacterColumn(InventoryGrid grid, Inventory inventory, float x, float size)
    {
        bool quiver = BetterArcheryCompat.Active;
        float previousGear = size * 3 + Gap * 2, previousProvisions = size * 2 + Gap;
        float w = Pad * 2 + previousGear + SideGap + previousProvisions + (quiver ? SideGap + size : 0);
        backgrounds.Add(Ugui.Image(root!, "Character Frame", theme.PackSurface, Palette.HudGlass, raycast: true).At(x, 0, w, panelHeight));
        Ugui.Image(root!, "Character Frame Edge", theme.PackEdge, Palette.Hex(0x3D4541)).At(x, 0, w, panelHeight);
        float left = x + Pad, inner = w - Pad * 2;

        // Two worn rows, one provision row, and optionally one arrow row. Reserve
        // headings/dashboard first, then constrain cells to the existing frame.
        float cell = Mathf.Max(1, Mathf.Floor(Mathf.Min(size, (inner - Gap * 4) / 5,
            (panelHeight - (quiver ? 236 : 204)) / (quiver ? 4 : 3))));
        Caption("WORN", left, Pad, inner);
        float slotsTop = Pad + 22;
        int[] positions = { 0, 1, 2, 3, 5, 4, 6, 7 };
        for (int i = 0; i < CharacterSlots.Gear.Length; i++)
        {
            if (!settings.Features.ExtraUtilitySlots.Value && Array.IndexOf(CharacterSlots.ExtraUtility, i) >= 0) continue;
            int place = positions[i];
            AddExtra(grid, CharacterSlots.GearSlot(i),
                new Rect(left + place % 5 * (cell + Gap), slotsTop + place / 5 * (cell + Gap), cell, cell), GearNames[i], null, worn: false);
        }
        if (quiver)
            AddExtra(grid, CharacterSlots.GearSlot(CharacterSlots.QuiverIndex),
                new Rect(left + 3 * (cell + Gap), slotsTop + cell + Gap, cell, cell), "QUIVER", null, worn: false);

        float provisionTitle = slotsTop + cell * 2 + Gap + 10;
        Ugui.Image(root!, "Provision Divider", theme.White, Palette.Hex(0x3D4541)).At(left, provisionTitle - 5, inner, 1);
        Caption("PROVISIONS", left, provisionTitle, inner);
        float provisionSize = Mathf.Min(cell, (inner - Gap * 5) / 6), provisionTop = provisionTitle + 22;
        for (int i = 0; i < CharacterSlots.Provisions; i++)
            AddExtra(grid, CharacterSlots.ProvisionSlot(i),
                new Rect(left + i * (provisionSize + Gap), provisionTop, provisionSize, provisionSize), null, theme.IconFood, worn: true);
        if (quiver)
        {
            float arrowTitle = provisionTop + provisionSize + 10;
            Caption("ARROWS", left, arrowTitle, inner);
            float arrowSize = Mathf.Min(cell, (inner - Gap * 2) / 3);
            for (int i = 0; i < BetterArcheryCompat.ArrowSlots; i++)
                AddExtra(grid, BetterArcheryCompat.ArrowSlot(i),
                    new Rect(left + i * (arrowSize + Gap), arrowTitle + 22, arrowSize, arrowSize), "ARROW", null, worn: true);
        }
        float dashboardY = panelHeight - Pad - CharacterDashboard.Height;
        Ugui.Image(root!, "Dashboard Divider", theme.White, Palette.Hex(0x3D4541)).At(left, dashboardY - 8, inner, 1);
        dashboard = new CharacterDashboard(theme, root!, left, dashboardY, inner);
    }

    private void AddExtra(InventoryGrid grid, Vector2i position, Rect area, string? label, Sprite? glyph, bool worn)
    {
        SlotView slot = new(theme, root!, area, position, null, label, glyph, worn, guide: true);
        slot.Input.Down = button => Click(grid, slot, button);
        slot.Input.Hover = on => hovered = on ? slot : hovered == slot ? null : hovered;
        extras[Key(position)] = slot;
    }

    private static int Key(Vector2i pos) => pos.y * 64 + pos.x;

    private void Caption(string text, float x, float y, float w) =>
        Ugui.Text(root!, text + " Heading", text, theme.Strong, 11, InventoryCategories.Material, spacing: 2)
            .At(x, y, w, 18);

    private void ShowHint(string text)
    {
        if (!result) return;
        result!.text = text;
        resultTime = Time.unscaledTime - 1.5f;
    }

    private void QuickStackNow()
    {
        PackCategory selected = category;
        if (QuickStackCommand.Run(settings, gui!, item => selected == PackCategory.All ||
            InventoryCategories.Of(item.m_shared) == selected) is not { } text) return;
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
            if (!maskedChildren.Add(child)) continue;
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

        // Glass at the configured opacity; faint panels still catch clicks.
        Color tint = Color.white.WithAlpha(settings.InventoryOpacity.Value);
        foreach (Image background in backgrounds)
            if (background && !Mathf.Approximately(background.color.a, tint.a))
                background.color = background.color.WithAlpha(tint.a);

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
            slot.SetEmphasis(drag != null || cursor || slot == hovered || slot.Item == null || category == PackCategory.All ||
                InventoryCategories.Of(slot.Item.m_shared) == category);
        }
        foreach (SlotView slot in extras.Values) slot.Refresh(player, drag, false, slot == hovered);

        string playerName = player.GetPlayerName();
        if ((string.IsNullOrWhiteSpace(playerName) || playerName == "...") && Game.instance)
            playerName = Game.instance.GetPlayerProfile().GetName();
        dashboard?.Refresh(player, inventory);
        summary!.Set(playerName, Mathf.RoundToInt(player.GetBodyArmor()), inventory.GetTotalWeight(), player.GetMaxCarryWeight());
        string close = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Inventory", true) : "TAB";
        if (string.IsNullOrEmpty(close)) close = "TAB";
        closeHint!.text = "<color=#FFFFFF><b>" + Localization.instance.Localize(close) + "</b></color> close";

        int nearby = NearbyStorage.StorageRuntime.Registry?.Nearby(player).Count ?? 0;
        chests!.text = nearby == 0 ? "None in range" : nearby == 1 ? "1 chest in range" : nearby + " chests in range";
        float age = Time.unscaledTime - resultTime;
        result!.alpha = age < 3f ? 1f : Mathf.Clamp01(1f - (age - 3f));
        chests.alpha = 1f - result.alpha;
        foreach (var chip in categoryChips) chip.Refresh(category);
        quickStackLabel!.text = "Quick Stack: " + category;

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
    private readonly bool markWorn, guide;
    private readonly CanvasGroup emphasis;
    private readonly Image? categoryDot;
    private readonly TextOutline? amountOutline, qualityOutline;
    private string shownAmount = "", shownQuality = "";

    // label/glyph: a faint placeholder for an empty character slot. markWorn: ring equipped items
    // (pointless in the gear slots, where everything is worn).
    internal SlotView(Theme theme, Transform parent, Rect r, Vector2i position, string? key, string? label = null, Sprite? glyph = null,
        bool markWorn = true, bool guide = false)
    {
        this.theme = theme;
        this.markWorn = markWorn;
        this.guide = guide;
        Position = position;
        Area = r;
        float s = r.width, h = r.height;
        tile = Ugui.Image(parent, $"Slot {position.x},{position.y}", guide ? theme.HudActionSlot : theme.Slot, Color.white, raycast: true).At(r);
        Input = tile.gameObject.AddComponent<SlotInput>();
        emphasis = tile.gameObject.AddComponent<CanvasGroup>();
        Transform t = tile.transform;
        if (label != null)
            placeholder = Ugui.Text(t, "Placeholder", label.ToLowerInvariant(), theme.Subtle, 11, guide ? Palette.Bone.WithAlpha(0.38f) : Palette.Dim, TextAlignmentOptions.Center)
                .Stretch(2, 0, 2, 0);
        else if (glyph != null)
            placeholder = Ugui.Image(t, "Placeholder", glyph, Palette.Faint.WithAlpha(guide ? 0.41f : 0.5f)).At(s * 0.3f, s * 0.3f, s * 0.4f, s * 0.4f);

        float iconSize = guide ? Mathf.Min(s, h) * 0.64f : s * 0.72f;
        icon = Ugui.Image(t, "Icon", null, Color.white).At((s - iconSize) / 2, (h - iconSize) / 2, iconSize, iconSize);
        Ugui.ItemIcon(icon);
        ring = Ugui.Image(t, "Ring", guide ? theme.HudActionEdge : theme.SlotRing, Color.white).Stretch();
        if (guide && key == null)
            categoryDot = Ugui.Image(t, "Category Dot", theme.Disc, InventoryCategories.Material).At(5, 5, 4, 4);

        if (key != null)
            Ugui.Text(t, "Key", key, theme.Strong, 11, Palette.Muted, TextAlignmentOptions.TopLeft).At(5, 3, 16, 14).overflowMode =
                TextOverflowModes.Overflow;
        noTeleport = Ugui.Image(t, "No Teleport", theme.NoTeleport, Palette.Crimson).At(guide ? s / 2 - 5 : key != null ? 15 : 4, 3, 10, 10);

        amount = Ugui.Text(t, "Amount", "", theme.Numbers, 13, guide ? Palette.Hex(0xECE5D6) : Palette.Tan, TextAlignmentOptions.BottomRight).At(2, h - 21, s - 7, 17);
        amount.overflowMode = TextOverflowModes.Overflow;

        // The level, a quiet number in the top-right corner.
        qualityGem = Ugui.Image(t, "Quality", null, Color.clear).At(s - 19, 3, 16, 14);
        quality = Ugui.Text(qualityGem.transform, "Level", "", theme.Numbers, 10, Palette.Faint, TextAlignmentOptions.TopRight).Stretch();
        quality.overflowMode = TextOverflowModes.Overflow;

        food = Ugui.Image(t, "Food", theme.Diamond, Color.white).At(6, h - 13, 6, 6);
        durabilityTrack = Ugui.Image(t, "Durability Track", theme.White, Palette.Ink.WithAlpha(0.9f)).At(6, h - 5, s - 12, 2);
        durability = Ugui.Image(durabilityTrack.transform, "Durability", theme.White, Palette.Gold).At(0, 0, s - 12, 2);
        if (guide)
        {
            amountOutline = new TextOutline(amount);
            qualityOutline = new TextOutline(quality);
        }
    }

    internal void Refresh(Player player, ItemDrop.ItemData? drag, bool cursor, bool hover)
    {
        ItemDrop.ItemData? item = Item;
        bool equipped = markWorn && item != null && item.m_equipped;
        bool queued = item != null && player.IsEquipActionQueued(item);
        bool breaking = item != null && item.m_shared.m_useDurability && item.GetDurabilityPercentage() < 0.1f;
        if (guide)
        {
            tile.sprite = theme.HudActionSlot;
            // These wells sit over the glass panel; light tints avoid opaque stacked fills.
            tile.color = item == null ? Palette.Hex(0x0C1110, hover || cursor ? 0.3f : 0.12f) : breaking ? Palette.Hex(0x291C1A, 0.3f) :
                hover || cursor || equipped ? Palette.Hex(0x453C26, 0.3f) : Palette.Hex(0x111716, 0.18f);
            ring.sprite = item == null ? theme.PackEmptyEdge : theme.HudActionEdge;
            ring.color = breaking ? Palette.Hex(0x9F4238) : hover || cursor || equipped || queued
                ? InventoryCategories.Gold : item == null ? Palette.Hex(0x737B72, 0.45f) : Palette.Hex(0x3D4541);
            SetActive(ring, true);
            if (categoryDot)
            {
                SetActive(categoryDot!, item != null);
                if (item != null) categoryDot!.color = InventoryCategories.ColorOf(item.m_shared);
            }
        }
        else
        {
            tile.sprite = hover || cursor ? theme.SlotHover : theme.Slot;
            SetActive(ring, cursor || equipped || queued);
            if (cursor) ring.color = Palette.Crimson;
            else if (equipped) ring.color = Color.white;
            else if (queued) ring.color = Palette.Muted;
        }
        if (placeholder) SetActive(placeholder!, item == null);

        SetActive(icon, item != null);
        if (item == null)
        {
            SetActive(amount, false);
            SetActive(qualityGem, false);
            SetActive(food, false);
            SetActive(noTeleport, false);
            SetActive(durabilityTrack, false);
            amountOutline?.Sync();
            qualityOutline?.Sync();
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

        bool worn = shared.m_useDurability && (guide || item.m_durability < item.GetMaxDurability());
        SetActive(durabilityTrack, worn);
        if (worn)
        {
            float pct = item.GetDurabilityPercentage();
            durability.rectTransform.sizeDelta = new Vector2((durabilityTrack.rectTransform.rect.width) * Mathf.Clamp01(pct), 2);
            durabilityTrack.color = guide ? Palette.Hex(0x3C423C) : Palette.Ink.WithAlpha(0.9f);
            durability.color = guide ? pct < 0.1f ? Palette.Health : pct < 0.3f ? Palette.Stamina : InventoryCategories.Trophy
                : item.m_durability <= 0f
                ? (Mathf.Sin(Time.time * 10f) > 0 ? Palette.Crimson : Color.clear)
                : pct < 0.25f ? Palette.Ember : Palette.Gold;
        }

        bool isFood = shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable &&
                      (shared.m_food > 0 || shared.m_foodStamina > 0 || shared.m_foodEitr > 0);
        SetActive(food, isFood);
        if (isFood) food.color = FoodColor(shared);

        SetActive(noTeleport, !shared.m_teleportable && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll));
        amountOutline?.Sync();
        qualityOutline?.Sync();
    }

    internal void SetEmphasis(bool matches) => emphasis.alpha = matches ? 1 : 0.18f;

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
