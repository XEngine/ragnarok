using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// The Ironbound crafting panel, drawn over Valheim's (which keeps running unseen: recipe list, selection,
// requirements, timing, multi-craft, upgrades and repair stay Valheim's). Ours reads its state each frame
// and presses its buttons. The search box and category chips set CraftFilter, which filters Valheim's
// own list (so the rows here are exactly what Valheim offers).
internal sealed class CraftPanel : IDisposable
{
    internal static RectTransform? RepairExtension { get; private set; }
    // Fits the native 570×650 panel; all actions still use its native controls.
    private const float Pad = 18, SearchY = 72, ChipsY = 114, ListTop = 202, RowHeight = 48, Gap = 18;
    private const float TabWidth = 78, Bottom = 40, Stepper = 100;
    private const float RepairWidth = 104;
    private float ListWidth;


    private static readonly FieldInfo Available = AccessTools.Field(typeof(InventoryGui), "m_availableRecipes");
    private static readonly FieldInfo SelectedField = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
    private static readonly MethodInfo SetRecipe = AccessTools.Method(typeof(InventoryGui), "SetRecipe");
    private static readonly Type Pair = AccessTools.Inner(typeof(InventoryGui), "RecipeDataPair");
    private static readonly PropertyInfo PairRecipe = AccessTools.Property(Pair, "Recipe");
    private static readonly PropertyInfo PairItem = AccessTools.Property(Pair, "ItemData");
    private static readonly PropertyInfo PairCan = AccessTools.Property(Pair, "CanCraft");
    private static readonly AccessTools.FieldRef<GuiBar, float> BarValue = AccessTools.FieldRefAccess<GuiBar, float>("m_value");
    private static readonly AccessTools.FieldRef<GuiBar, float> BarMax = AccessTools.FieldRefAccess<GuiBar, float>("m_maxValue");

    private readonly struct Row
    {
        internal readonly Recipe Recipe;
        internal readonly ItemDrop.ItemData? Item;
        internal readonly bool Can;
        internal readonly int Index;

        internal Row(Recipe recipe, ItemDrop.ItemData? item, bool can, int index)
        {
            Recipe = recipe;
            Item = item;
            Can = can;
            Index = index;
        }
    }

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly List<Row> rows = new();
    private sealed class RowView
    {
        internal Row Row;
        internal Image Background = null!, Bar = null!, Icon = null!, Availability = null!;
        internal TextMeshProUGUI Title = null!, Kind = null!;
        internal int BoundIndex = -1;
        internal int Quality = -1;
    }
    private readonly List<RowView> rowViews = new();
    private int windowStart = -1;
    private readonly List<(CraftCategory? Category, Image Chip, TextMeshProUGUI Label)> chips = new();
    private readonly List<(CraftArmorSlot Slot, Image Chip, TextMeshProUGUI Label)> slotChips = new();
    private readonly List<(Image Tile, Image Edge, Image Icon, TextMeshProUGUI Name, TextMeshProUGUI Amount,
        TextMeshProUGUI Sources, GuideTooltip Tooltip)> requirements = new();
    private readonly List<TextOutline> outlines = new();
    private readonly List<Image> levels = new();
    private bool dirty = true;
    private bool resetRowScroll = true;
    private Image? hoveredRow;
    private string shownDescription = "";
    private int signature;
    private int readListVersion = -1;
    private Recipe? detailRecipe, requirementsRecipe;
    private ItemDrop.ItemData? detailUpgrade;
    private int detailQuality, detailAmount, detailWorld, requirementsQuality, requirementsAmount;
    private string detailLanguage = "", nativeTooltip = "", nativeCraftType = "";
    private float nextDetails, nextRequirements;
    private bool requirementsNearby;
    private InventoryGui? gui;
    private RectTransform? root, listContent, descriptionContent, recipeView, descriptionView;
    private Image? sortButton, listDivider;
    private float contentTop = float.NaN;
    private Vector2 builtSize;
    private Image? stationIcon, recipeIcon, recipeRim, craftButton, progressTrack, progressFill, repairButton, repairIcon, variantButton;
    private CraftFrameGraphic? background, frameEdge;
    private TextMeshProUGUI? stationName, stationLevel, recipeName, recipeType, stationNeed, description, craftLabel, emptyNote, repairLabel;
    private RectTransform? stepper;
    private TMP_InputField? amountField;
    private float detailX, detailWidth;
    private TMP_InputField? search;
    private (Image Chip, TextMeshProUGUI Label) craftTab, upgradeTab;
    private Image? craftableChip, craftableCheck;
    private TextMeshProUGUI? countLabel, sortLabel, resourcesLabel, readyLabel, materialsLabel, shortcutHint;
    private GuideTooltip? recipeTooltip;
    private GuideTooltip? repairTooltip;
    private bool repairHovered;
    private float requirementY;
    private float nextSearch;
    private bool searchPending;
    private int filterContext = -1;
    private int padSelection = -1;

    internal CraftPanel(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal bool Wanted => settings.CraftingStyle.Value switch
    {
        PanelStyle.Ironbound => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ironbound
    };

    // The panel while it is on screen (the key guide sits under it), else null.
    internal RectTransform? Shown =>
        root && gui && InventoryGui.IsVisible() && root!.gameObject.activeInHierarchy ? root : null;

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            Restore();
            gui = current;
        }
        if (!gui) return;
        if (!Wanted)
        {
            bool hadPanel = root;
            Restore();
            if (hadPanel) CraftFilter.Changed();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer || !gui!.m_crafting.gameObject.activeInHierarchy) return;
        RectTransform panel = gui.m_crafting;
        if (!root || panel.rect.size != builtSize)
        {
            Build(panel);
            // Opening already built/filtered the native list. Rebuild only when
            // switching styles on an existing list that has not been sorted for us.
            if (CraftFilter.LastListOwner != gui || !CraftFilter.LastListSorted) CraftFilter.Changed();
        }
        if (filterContext != CraftFilter.ContextVersion)
        {
            filterContext = CraftFilter.ContextVersion;
            searchPending = false;
            search!.SetTextWithoutNotify(CraftFilter.Query);
            dirty = true;
        }
        if (searchPending && Time.unscaledTime >= nextSearch)
        {
            searchPending = false;
            CraftFilter.SetQuery(search!.text);
            dirty = true;
        }
        if (search && !searchPending && !search!.isFocused && search.text != CraftFilter.Query)
            search.SetTextWithoutNotify(CraftFilter.Query);
        // Character input follows the keyboard layout (for example, Shift+7 on Turkish Q).
        // Retain the dedicated slash key for layouts where it is directly available.
        bool searchShortcut = Input.inputString.IndexOf('/') >= 0 || Input.GetKeyDown(KeyCode.Slash);
        if (searchShortcut && !UiState.Typing && !UiState.ModalOpen && !Console.IsVisible() &&
            !Menu.IsVisible() && (!Chat.instance || !Chat.instance.HasFocus()))
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(search!.gameObject);
            search!.ActivateInputField();
        }
        string close = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Inventory", true) : "TAB";
        if (string.IsNullOrEmpty(close)) close = "TAB";
        shortcutHint!.text = "<color=#FFFFFF><b>/</b></color> search   <color=#FFFFFF><b>" +
            Localization.instance.Localize(close) + "</b></color> close";
        Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        Restore();
    }

    private void Restore()
    {
        bool ownedAmount = !ReferenceEquals(root, null);
        RepairExtension = null;
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null;
        recipeView = descriptionView = null;
        sortButton = listDivider = null;
        contentTop = float.NaN;
        rowViews.Clear();
        windowStart = -1;
        resetRowScroll = true;
        chips.Clear();
        requirements.Clear();
        slotChips.Clear();
        outlines.Clear();
        levels.Clear();
        repairHovered = false;
        shownDescription = "";
        readListVersion = -1;
        detailRecipe = requirementsRecipe = null;
        detailUpgrade = null;
        nextDetails = nextRequirements = 0f;
        nativeTooltip = nativeCraftType = "";
        if (search) UiState.TypingFields.Remove(search!);
        search = null;
        searchPending = false;
        if (amountField) UiState.TypingFields.Remove(amountField!);
        amountField = null;
        if (ownedAmount) CraftAmount.Reset();
    }

    // Construction ---------------------------------------------------------------------------

    private void Build(RectTransform panel)
    {
        Restore();
        HideVanilla(panel);
        builtSize = panel.rect.size;
        float width = builtSize.x, height = builtSize.y;
        ListWidth = Mathf.Clamp(width * 0.31f, 170, 236);
        dirty = true;
        root = Ugui.Node(panel, "Ironbound Crafting").Stretch();
        background = CraftFrameGraphic.Create(root, "Frame", RepairWidth, ListTop - 64, 40, false, Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value));
        frameEdge = CraftFrameGraphic.Create(root, "Frame edge", RepairWidth, ListTop - 64, 40, true, Palette.Hex(0x626B64, 0.65f));
        Image stationWell = Well(root, "Station", Pad, 12, 44, 44);
        stationWell.GetComponentsInChildren<Image>()[1].color = InventoryCategories.Gold;
        stationIcon = Ugui.Image(stationWell.transform, "Icon", theme.IconHammer, Color.white).At(9, 9, 26, 26);
        Ugui.ItemIcon(stationIcon);
        float tabsX = width - Pad - TabWidth * 2 - 4;
        stationName = Ugui.Text(root, "Station", "", theme.Heading, 23, Palette.Bone)
            .At(Pad + 56, 7, Mathf.Max(80, tabsX - Pad - 66), 32);
        stationName.enableAutoSizing = true;
        stationName.fontSizeMin = 14;
        stationName.fontSizeMax = 23;
        for (int i = 0; i < 5; i++)
            levels.Add(Ugui.Image(root, "Station level", theme.White, InventoryCategories.Gold).At(Pad + 56 + i * 16, 45, 13, 4));
        stationLevel = Ugui.Text(root, "Level", "", theme.Strong, 9, Palette.Muted).At(Pad + 140, 37, 95, 17);
        Ugui.Image(root, "Craft / Upgrade", theme.PackPillEdge, Palette.Hex(0x626B64, 0.55f)).At(tabsX - 4, 12, TabWidth * 2 + 8, 36);
        craftTab = Tab("Craft", tabsX, () => gui!.OnTabCraftPressed());
        upgradeTab = Tab("Upgrade", tabsX + TabWidth, () => gui!.OnTabUpgradePressed());
        repairButton = Ugui.Image(root, "Repair extension", null, Color.clear).At(-RepairWidth, ListTop - 64, RepairWidth, 40);
        RepairExtension = repairButton.rectTransform;
        repairIcon = Ugui.Image(repairButton.transform, "Icon", theme.IconRepair, InventoryCategories.Gold).At(10, 11, 18, 18);
        repairLabel = Ugui.Text(repairButton.transform, "Label", "Repair", theme.Strong, 14, Palette.Bone).Stretch(36, 0, 8, 0);
        Ugui.HitArea(repairButton.transform, "Hit", () =>
        {
            if (SupportsRepair(Player.m_localPlayer) && gui!.m_repairButton.gameObject.activeInHierarchy && gui.m_repairButton.interactable)
                gui.m_repairButton.onClick.Invoke();
        }).Stretch().OnHover(on => repairHovered = on);
        repairTooltip = GuideTooltip.Attach(theme, repairButton.rectTransform);
        repairButton.gameObject.SetActive(false);
        SearchField(width);
        FilterChips(width);
        countLabel = Ugui.Text(root, "Result count", "", theme.Body, 11, Palette.Muted).At(Pad, ListTop - 24, ListWidth / 2, 18);
        Image sort = Ugui.Image(root, "Sort", theme.PackPill, Color.clear).At(Pad + ListWidth / 2, ListTop - 24, ListWidth / 2, 18);
        sortButton = sort;
        sortLabel = Ugui.Text(sort.transform, "Label", "", theme.Body, 10, Palette.Muted, TextAlignmentOptions.MidlineRight).Stretch();
        Ugui.HitArea(sort.transform, "Hit", () => { CraftFilter.CraftableFirst = !CraftFilter.CraftableFirst; CraftFilter.Changed(); }).Stretch();
        RectTransform view = Ugui.Node(root, "Recipes").At(Pad, ListTop, ListWidth, height - ListTop - Pad - 26);
        recipeView = view;
        listContent = Scroll(view);
        emptyNote = Ugui.Text(view, "Empty", "No matching recipes", theme.Body, 13, Palette.Muted, TextAlignmentOptions.Center)
            .At(0, 16, ListWidth, 32);
        Image clear = Ugui.Image(view, "Clear filters", theme.PackPill, Color.clear).At(0, 56, ListWidth, 26);
        Ugui.Text(clear.transform, "Label", "Clear search & filters", theme.Strong, 11, InventoryCategories.Gold, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(clear.transform, "Hit", () =>
        {
            searchPending = false;
            search!.SetTextWithoutNotify("");
            CraftFilter.SetQuery("");
            CraftFilter.CraftableOnly = false;
            CraftFilter.Select(null);
            dirty = true;
        }).Stretch();
        clear.gameObject.SetActive(false);
        clearEmpty = clear;
        listDivider = Ugui.Image(root, "List divider", theme.White, Palette.Hex(0x626B64, 0.4f))
            .At(Pad + ListWidth + Gap / 2, ListTop - 24, 1, height - ListTop - Pad - 2);
        Ugui.Image(root, "Footer rule", theme.White, Palette.Hex(0x626B64, 0.4f)).At(Pad, height - Pad - 21, width - Pad * 2, 1);
        resourcesLabel = Ugui.Text(root, "Material sources", "", theme.Body, 10, Palette.Muted)
            .At(Pad, height - Pad - 14, ListWidth, 16);
        shortcutHint = Ugui.Text(root, "Search shortcut", "", theme.Body, 10.5f, InventoryCategories.Material,
            TextAlignmentOptions.MidlineRight).At(width - Pad - 210, height - Pad - 14, 210, 16);
        Detail(width, height);
        foreach (TMP_Text label in new[] { stationName, recipeName!, recipeType!, stationNeed!, readyLabel! })
            outlines.Add(new TextOutline(label));
    }

    private Image? clearEmpty;

    private Image Well(Transform parent, string name, float x, float y, float width, float height)
    {
        Image well = Ugui.Image(parent, name, theme.PackSurface, Palette.Hex(0x0C0F11, 0.18f)).At(x, y, width, height);
        Ugui.Image(well.transform, "Edge", theme.PackEdge, Palette.Hex(0x626B64, 0.45f)).Stretch();
        return well;
    }

    private (Image, TextMeshProUGUI) Tab(string label, float x, Action press)
    {
        Image chip = Ugui.Image(root!, label, theme.PackPill, Color.clear).At(x, 16, TabWidth, 28);
        TextMeshProUGUI text = Ugui.Text(chip.transform, "Label", label, theme.Heading, 14, Palette.Muted, TextAlignmentOptions.Center).Stretch(4, 0, 4, 0);
        Ugui.HitArea(chip.transform, "Hit", press).Stretch();
        return (chip, text);
    }

    private void SearchField(float width)
    {
        const float readyWidth = 118;
        Image field = Well(root!, "Search", Pad, SearchY, width - Pad * 2 - readyWidth - 10, 32);
        field.raycastTarget = true;
        field.gameObject.SetActive(false);
        Ugui.Image(field.transform, "Glyph", theme.IconSearch, Palette.Muted).At(8, 8, 16, 16);
        RectTransform area = Ugui.Node(field.transform, "Text Area").Stretch(30, 5, 24, 5);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Ugui.Text(area, "Placeholder", "Search recipes or ingredients…", theme.Body, 13, Palette.Muted).Stretch();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "", theme.Body, 13, Palette.Bone).Stretch();
        search = field.gameObject.AddComponent<TMP_InputField>();
        search.textViewport = area;
        search.textComponent = text;
        search.placeholder = placeholder;
        search.targetGraphic = field;
        search.characterLimit = 80;
        search.customCaretColor = true;
        search.caretColor = InventoryCategories.Gold;
        search.selectionColor = InventoryCategories.Gold.WithAlpha(0.3f);
        search.text = CraftFilter.Query;
        search.transition = Selectable.Transition.None;
        field.gameObject.SetActive(true);
        UiState.TypingFields.Add(search);
        search.onValueChanged.AddListener(_ => { searchPending = true; nextSearch = Time.unscaledTime + 0.15f; });
        search.onDeselect.AddListener(_ => { if (searchPending) nextSearch = 0; });
        RectTransform close = Ugui.Node(field.transform, "Clear search").Stretch();
        close.anchorMin = new Vector2(1, 0);
        close.offsetMin = new Vector2(-24, 0);
        Ugui.Text(close, "Label", "×", theme.Strong, 16, Palette.Muted, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(close, "Hit", () => { search!.text = ""; nextSearch = 0; }).Stretch();
        craftableChip = Ugui.Image(root!, "Craftable only", theme.PackPill, Color.clear).At(width - Pad - readyWidth, SearchY, readyWidth, 32);
        craftableCheck = Ugui.Image(craftableChip.transform, "Check", theme.IconCheck, Palette.Muted).At(7, 9, 14, 14);
        Ugui.Text(craftableChip.transform, "Label", "Craftable only", theme.Strong, 12, Palette.Bone).Stretch(28, 0, 4, 0);
        Ugui.HitArea(craftableChip.transform, "Hit", () => { CraftFilter.ToggleCraftable(); dirty = true; }).Stretch();
    }

    private void FilterChips(float width)
    {
        AddChip(null, "All");
        foreach (var kind in new[] { CraftCategory.Weapons, CraftCategory.Shields, CraftCategory.Armor, CraftCategory.Tools,
            CraftCategory.Ammo, CraftCategory.Health, CraftCategory.Stamina, CraftCategory.Eitr, CraftCategory.Meads,
            CraftCategory.Materials, CraftCategory.Other }) AddChip(kind, kind.ToString());
        foreach (CraftArmorSlot slot in Enum.GetValues(typeof(CraftArmorSlot)))
        {
            CraftArmorSlot selected = slot;
            string name = slot == CraftArmorSlot.All ? "All armor" : slot.ToString();
            Image chip = Ugui.Image(root!, name + " filter", theme.PackPill, Color.clear).At(Pad, 150, 56, 22);
            TextMeshProUGUI text = Ugui.Text(chip.transform, "Label", name, theme.Body, 11, Palette.Muted, TextAlignmentOptions.Center).Stretch(6, 0, 6, 0);
            Ugui.HitArea(chip.transform, "Hit", () => { CraftFilter.SelectSlot(selected); dirty = true; }).Stretch();
            slotChips.Add((slot, chip, text));
        }
    }

    private void AddChip(CraftCategory? category, string name)
    {
        Image chip = Ugui.Image(root!, name + " filter", theme.PackPill, Color.clear).At(Pad, ChipsY, 60, 26);
        Ugui.Image(chip.transform, "Edge", theme.PackPillEdge, Palette.Hex(0x626B64, 0.55f)).Stretch();
        TextMeshProUGUI text = Ugui.Text(chip.transform, "Label", name, theme.Strong, 11.5f, Palette.Muted, TextAlignmentOptions.Center).Stretch(10, 0, 10, 0);
        float width = Mathf.Ceil(text.preferredWidth) + 20;
        chip.rectTransform.sizeDelta = new Vector2(width, 26);
        Ugui.HitArea(chip.transform, "Hit", () => { CraftFilter.Select(category); dirty = true; }).Stretch();
        chips.Add((category, chip, text));
    }

    private RectTransform Scroll(RectTransform view)
    {
        RectTransform viewport = Ugui.Image(view, "Viewport", null, Color.clear, raycast: true).Stretch().rectTransform;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Ugui.Node(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0, 1);
        content.sizeDelta = Vector2.zero;
        ScrollRect scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Ugui.WheelSpeed(scroll);
        scroll.inertia = false;
        return content;
    }

    // Right side: the recipe (icon, name, upgrade line, station need, description), its requirements,
    // and the repair / style / craft buttons at the bottom.
    private void Detail(float width, float height)
    {
        float x = Pad + ListWidth + Gap, w = width - Pad - x;
        recipeRim = Well(root!, "Recipe", x, ListTop, 54, 54);
        recipeRim.raycastTarget = true;
        recipeTooltip = GuideTooltip.Attach(theme, recipeRim.rectTransform);
        recipeIcon = Ugui.Image(recipeRim.transform, "Icon", null, Color.white).At(8, 8, 38, 38);
        Ugui.ItemIcon(recipeIcon);
        recipeName = Ugui.Text(root!, "Name", "", theme.Heading, 22, Palette.Hex(0xF2D597))
            .At(x + 66, ListTop - 2, w - 66, 32);
        recipeName.enableAutoSizing = true;
        recipeName.fontSizeMin = 13;
        recipeName.fontSizeMax = 22;
        recipeType = Ugui.Text(root!, "Type", "", theme.Strong, 10, Palette.Muted, spacing: 1)
            .At(x + 66, ListTop + 28, w - 66, 18);
        stationNeed = Ugui.Text(root!, "Station Need", "", theme.Body, 10, Palette.Muted).At(x + 66, ListTop + 43, w - 66, 16);
        requirementY = height - Pad - Bottom - 32 - 74 - 20;
        float descTop = ListTop + 65;
        RectTransform view = Ugui.Node(root!, "Description").At(x, descTop, w, Mathf.Max(24, requirementY - descTop - 24));
        descriptionView = view;
        descriptionContent = Scroll(view);
        description = Ugui.Text(descriptionContent, "Text", "", theme.Body, 13, Palette.Bone.WithAlpha(0.95f), TextAlignmentOptions.TopLeft, wrap: true);
        RectTransform d = description.rectTransform;
        d.anchorMin = new Vector2(0, 1);
        d.anchorMax = new Vector2(1, 1);
        d.pivot = new Vector2(0, 1);
        d.offsetMin = Vector2.zero;
        d.offsetMax = new Vector2(-4, 0);
        description.overflowMode = TextOverflowModes.Overflow;
        materialsLabel = Ugui.Text(root!, "Materials label", "REQUIRED MATERIALS", theme.Strong, 9, Palette.Muted, spacing: 1)
            .At(x, requirementY - 15, w / 2, 13);
        Ugui.Text(root!, "Have / need", "Have / need", theme.Body, 10, Palette.Muted, TextAlignmentOptions.MidlineRight)
            .At(x + w / 2, requirementY - 15, w / 2, 13);
        for (int i = 0; i < 4; i++)
        {
            Image tile = Well(root!, "Requirement", x, requirementY, w / 4, 74);
            tile.raycastTarget = true;
            GuideTooltip tooltip = GuideTooltip.Attach(theme, tile.rectTransform);
            Image edge = tile.GetComponentsInChildren<Image>()[1];
            Image icon = Ugui.Image(tile.transform, "Icon", null, Color.white).At(12, 4, 24, 24);
            Ugui.ItemIcon(icon);
            TextMeshProUGUI name = Ugui.Text(tile.transform, "Name", "", theme.Body, 11, Palette.Bone, TextAlignmentOptions.Center)
                .At(4, 29, 60, 13);
            name.enableAutoSizing = true;
            name.fontSizeMin = 8;
            name.fontSizeMax = 11;
            TextMeshProUGUI amount = Ugui.Text(tile.transform, "Amount", "", theme.Numbers, 14, Palette.Bone, TextAlignmentOptions.Center).At(2, 42, 64, 17);
            amount.overflowMode = TextOverflowModes.Overflow;
            TextMeshProUGUI sources = Ugui.Text(tile.transform, "Sources", "", theme.Body, 9, Palette.Muted, TextAlignmentOptions.Center)
                .At(2, 59, 64, 13);
            sources.enableAutoSizing = true;
            sources.fontSizeMin = 7;
            sources.fontSizeMax = 9;
            requirements.Add((tile, edge, icon, name, amount, sources, tooltip));
        }
        detailX = x;
        detailWidth = w;
        float by = height - Pad - Bottom - 26;
        readyLabel = Ugui.Text(root!, "Craft readiness", "", theme.Strong, 11, Palette.Muted).At(x, by - 23, w, 20);
        variantButton = Well(root!, "Style", x, by, 56, Bottom);
        Ugui.Text(variantButton.transform, "Label", "Style", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(variantButton.transform, "Hit", () => gui!.m_variantButton.onClick.Invoke()).Stretch();
        AmountStepper(x, by);
        craftButton = Ugui.Image(root!, "Craft", theme.PackSurface, InventoryCategories.Gold).At(x, by, w, Bottom);
        Ugui.Image(craftButton.transform, "Edge", theme.PackEdge, InventoryCategories.Gold.WithAlpha(0.55f)).Stretch();
        craftLabel = Ugui.Text(craftButton.transform, "Label", "", theme.Heading, 16, Palette.Ink, TextAlignmentOptions.Center).Stretch(6, 0, 6, 0);
        Ugui.HitArea(craftButton.transform, "Hit", () =>
        {
            if (gui!.m_craftButton.interactable) gui.m_craftButton.onClick.Invoke();
        }).Stretch();
        progressTrack = Well(root!, "Progress", x, by + 10, w, 20);
        progressFill = Ugui.Image(progressTrack.transform, "Fill", theme.White, InventoryCategories.Gold).Stretch(3, 3, 3, 3);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
    }

    // How many to craft: - [ N ] +, the number typed or stepped (1 to as many as you can afford).
    private void AmountStepper(float x, float y)
    {
        stepper = Ugui.Node(root!, "Amount").At(x, y, Stepper, Bottom);
        Image minus = Well(stepper, "Less", 0, 0, 28, Bottom);
        Ugui.Text(minus.transform, "Label", "–", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(minus.transform, "Hit", () => SetAmount(CraftAmount.Amount - 1)).Stretch();
        Image plus = Well(stepper, "More", Stepper - 28, 0, 28, Bottom);
        Ugui.Text(plus.transform, "Label", "+", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(plus.transform, "Hit", () => SetAmount(CraftAmount.Amount + 1)).Stretch();

        Image box = Well(stepper, "Box", 30, 0, Stepper - 60, Bottom);
        box.raycastTarget = true;
        amountField = box.gameObject.AddComponent<TMP_InputField>();
        RectTransform area = Ugui.Node(box.transform, "Text Area").Stretch(4, 0, 4, 0);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "1", theme.Strong, 16, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        amountField.textViewport = area;
        amountField.textComponent = text;
        amountField.contentType = TMP_InputField.ContentType.IntegerNumber;
        amountField.characterLimit = 2;
        amountField.text = CraftAmount.Amount.ToString();
        amountField.onEndEdit.AddListener(value => SetAmount(int.TryParse(value, out int n) ? n : 1));
        UiState.TypingFields.Add(amountField);
    }

    // Kept between 1 and what the materials (yours and the nearby chests') allow, at most 99.
    private void SetAmount(int amount)
    {
        CraftAmount.Amount = Mathf.Clamp(amount, 1, Mathf.Max(1, Affordable()));
        if (gui) CraftAmount.Apply(gui!);
        if (amountField) amountField!.SetTextWithoutNotify(CraftAmount.Amount.ToString());
    }

    private bool CanAfford(int amount)
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui || SelectedField.GetValue(gui) is not { } selected || PairRecipe.GetValue(selected) is not Recipe recipe) return true;
        return player.NoCostCheat() || player.HaveRequirements(recipe, false, 1, amount);
    }

    private int Affordable()
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui || SelectedField.GetValue(gui) is not { } selected || PairRecipe.GetValue(selected) is not Recipe recipe) return 1;
        if (player.NoCostCheat()) return CraftAmount.Max;
        int n = 1;
        while (n < CraftAmount.Max && player.HaveRequirements(recipe, false, 1, n + 1)) n++;
        return n;
    }

    // The bottom row, left to right: style (if any), the amount (new items only), craft in the rest.
    private void Arrange(bool style, bool amount)
    {
        float x = detailX;
        if (style) x += 56 + 6;
        if (stepper!.gameObject.activeSelf != amount) stepper.gameObject.SetActive(amount);
        if (amount)
        {
            stepper.anchoredPosition = new Vector2(x, stepper.anchoredPosition.y);
            x += Stepper + 6;
        }
        float w = detailX + detailWidth - x;
        foreach (Image part in new[] { craftButton!, progressTrack! })
        {
            RectTransform rect = part.rectTransform;
            if (Mathf.Abs(rect.anchoredPosition.x - x) > 0.5f || Mathf.Abs(rect.sizeDelta.x - w) > 0.5f)
            {
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                rect.sizeDelta = new Vector2(w, rect.sizeDelta.y);
            }
        }
    }

    // Valheim's panel and its children stay active (they run the crafting) but draw and catch nothing.
    private void HideVanilla(RectTransform panel)
    {
        if (panel.TryGetComponent(out Image image) && image.enabled)
        {
            image.enabled = false;
            undo.Add((image, () => image.enabled = true));
        }
        foreach (Transform child in panel)
        {
            if (child.name == "Ironbound Crafting") continue;
            bool added = !child.TryGetComponent(out CanvasGroup group);
            if (added) group = child.gameObject.AddComponent<CanvasGroup>();
            float alpha = group.alpha;
            bool blocks = group.blocksRaycasts, interactable = group.interactable;
            group.alpha = 0;
            group.blocksRaycasts = group.interactable = false;
            CanvasGroup g = group;
            undo.Add((g, () =>
            {
                if (added) UnityEngine.Object.DestroyImmediate(g);
                else { g.alpha = alpha; g.blocksRaycasts = blocks; g.interactable = interactable; }
            }));
        }
    }

    // Per frame ------------------------------------------------------------------------------

    private void Refresh(Player player)
    {
        InventoryGui g = gui!;
        background!.color = Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value);

        // Station header and tabs, as Valheim set them this frame.
        stationName!.text = g.m_craftingStationName.text;
        bool atStation = g.m_craftingStationIcon.gameObject.activeSelf;
        stationIcon!.enabled = true;
        stationIcon.sprite = atStation ? g.m_craftingStationIcon.sprite : theme.IconHammer;
        stationLevel!.text = g.m_craftingStationLevelRoot.gameObject.activeSelf ? "Level " + g.m_craftingStationLevel.text : "By hand";
        SetTab(craftTab, g.m_tabCraft.gameObject.activeSelf, !g.m_tabCraft.interactable);
        SetTab(upgradeTab, g.m_tabUpgrade.gameObject.activeSelf, !g.m_tabUpgrade.interactable);

        float totalWidth = 0;
        int visibleChips = 0;
        foreach (var (category, chip, _) in chips)
        {
            bool show = category == null || CraftFilter.Present.Contains(category.Value);
            chip.gameObject.SetActive(show);
            if (show) { totalWidth += chip.rectTransform.sizeDelta.x; visibleChips++; }
        }
        float fit = Mathf.Min(1, (builtSize.x - Pad * 2 - Mathf.Max(0, visibleChips - 1) * 5) / Mathf.Max(1, totalWidth));
        float chipX = Pad;
        foreach (var (category, chip, label) in chips)
        {
            if (!chip.gameObject.activeSelf) continue;
            chip.rectTransform.anchoredPosition = new Vector2(chipX, -ChipsY);
            chip.rectTransform.localScale = Vector3.one * fit;
            chipX += chip.rectTransform.sizeDelta.x * fit + 5;
            bool on = category is { } c ? CraftFilter.Categories.Contains(c) : CraftFilter.Categories.Count == 0;
            Color accent = category switch
            {
                CraftCategory.Health => Palette.Health,
                CraftCategory.Stamina => Palette.Stamina,
                CraftCategory.Eitr => Palette.Hex(0x9A7BEA),
                _ => InventoryCategories.Gold
            };
            chip.color = on ? accent.WithAlpha(0.18f) : Color.clear;
            label.color = on ? accent : Palette.Muted;
            chip.transform.GetChild(0).GetComponent<Image>().color = on ? accent.WithAlpha(0.65f) : Palette.Hex(0x626B64, 0.45f);
        }
        chipX = Pad;
        bool armor = CraftFilter.Categories.Contains(CraftCategory.Armor);
        foreach (var (slot, chip, label) in slotChips)
        {
            bool show = armor && (slot == CraftArmorSlot.All || CraftFilter.PresentSlots.Contains(slot));
            chip.gameObject.SetActive(show);
            if (!show) continue;
            float width = Mathf.Ceil(label.preferredWidth) + 14;
            chip.At(chipX, 150, width, 22);
            chipX += width + 4;
            bool on = CraftFilter.ArmorSlot == slot;
            chip.color = on ? InventoryCategories.Gold.WithAlpha(0.14f) : Color.clear;
            label.color = on ? Palette.Bone : Palette.Muted;
        }
        ArrangeFilterSpace(armor);
        craftableChip!.color = CraftFilter.CraftableOnly ? InventoryCategories.Gold.WithAlpha(0.16f) : Color.clear;
        craftableCheck!.color = CraftFilter.CraftableOnly ? InventoryCategories.Gold : Palette.Muted.WithAlpha(0.4f);
        sortLabel!.text = CraftFilter.CraftableFirst ? "Craftable first" : "Name A–Z";
        bool nearby = NearbyStorage.StorageRuntime.Settings is { } storage && storage.Enabled.Value;
        resourcesLabel!.text = nearby ? "Pack + nearby chests" : "Pack materials";
        if (int.TryParse(g.m_craftingStationLevel.text, out int stationTier))
            for (int i = 0; i < levels.Count; i++) levels[i].color = i < stationTier ? InventoryCategories.Gold : Palette.Hex(0x626B64, 0.4f);
        else foreach (Image level in levels) level.color = Palette.Hex(0x626B64, 0.4f);
        ReadRecipes(g);
        if (dirty) RebuildRows();
        Highlight(g);
        ShowRecipe(g, player);
        countLabel!.text = rows.Count + " recipes";
        foreach (TextOutline outline in outlines) outline.Sync();
    }

    private void ArrangeFilterSpace(bool hasSubfilters)
    {
        float top = hasSubfilters ? ListTop : ListTop - 32;
        if (contentTop == top) return;
        contentTop = top;
        countLabel!.At(Pad, top - 24, ListWidth / 2, 18);
        sortButton!.At(Pad + ListWidth / 2, top - 24, ListWidth / 2, 18);
        recipeView!.At(Pad, top, ListWidth, builtSize.y - top - Pad - 26);
        listDivider!.At(Pad + ListWidth + Gap / 2, top - 24, 1, builtSize.y - top - Pad - 2);
        recipeRim!.At(detailX, top, 54, 54);
        recipeName!.At(detailX + 66, top - 2, detailWidth - 66, 32);
        recipeType!.At(detailX + 66, top + 28, detailWidth - 66, 18);
        stationNeed!.At(detailX + 66, top + 43, detailWidth - 66, 16);
        float descriptionTop = top + 65;
        descriptionView!.At(detailX, descriptionTop, detailWidth, Mathf.Max(24, requirementY - descriptionTop - 24));
        // Keep scrolling within the resized viewport and let gamepad selection recheck visibility.
        listContent!.anchoredPosition = new Vector2(0, Mathf.Clamp(listContent.anchoredPosition.y, 0,
            Mathf.Max(0, listContent.rect.height - recipeView!.rect.height)));
        padSelection = -1;
    }

    private void SetTab((Image Chip, TextMeshProUGUI Label) tab, bool visible, bool selected)
    {
        if (tab.Chip.gameObject.activeSelf != visible) tab.Chip.gameObject.SetActive(visible);
        tab.Chip.color = selected ? InventoryCategories.Gold : Color.clear;
        tab.Label.color = selected ? Palette.Ink : Palette.Muted;
    }

    private void ReadRecipes(InventoryGui g)
    {
        if (readListVersion == CraftFilter.ListVersion && CraftFilter.LastListOwner == g) return;
        readListVersion = CraftFilter.ListVersion;
        IList list = (IList)Available.GetValue(g);
        int hash = list.Count;
        bool changedOrder = rows.Count != list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            object pair = list[i];
            Recipe recipe = (Recipe)PairRecipe.GetValue(pair);
            ItemDrop.ItemData? item = PairItem.GetValue(pair) as ItemDrop.ItemData;
            bool can = (bool)PairCan.GetValue(pair);
            if (i < rows.Count)
            {
                changedOrder |= rows[i].Recipe != recipe || rows[i].Item != item;
                rows[i] = new Row(recipe, item, can, i);
            }
            else rows.Add(new Row(recipe, item, can, i));
            unchecked { hash = hash * 31 + (recipe ? recipe.GetInstanceID() : 0) * 7 + (item?.GetHashCode() ?? 0) + (can ? 1 : 0); }
        }
        if (rows.Count > list.Count) rows.RemoveRange(list.Count, rows.Count - list.Count);
        resetRowScroll |= changedOrder;
        if (hash != signature)
        {
            signature = hash;
            dirty = true;
        }
    }

    private void RebuildRows()
    {
        dirty = false;
        hoveredRow = null;
        padSelection = -1;
        windowStart = -1;
        foreach (RowView view in rowViews) view.BoundIndex = -1;
        listContent!.sizeDelta = new Vector2(0, rows.Count * RowHeight);
        if (resetRowScroll) { listContent.anchoredPosition = Vector2.zero; resetRowScroll = false; }
        // Keep scroll position when only material availability changes.
        float height = ((RectTransform)listContent.parent).rect.height;
        listContent.anchoredPosition = new Vector2(0, Mathf.Clamp(listContent.anchoredPosition.y, 0,
            Mathf.Max(0, rows.Count * RowHeight - height)));
        emptyNote!.enabled = rows.Count == 0;
        clearEmpty!.gameObject.SetActive(rows.Count == 0);
    }

    private void UpdateRowWindow()
    {
        float height = ((RectTransform)listContent!.parent).rect.height;
        int first = Mathf.Clamp(Mathf.FloorToInt(listContent.anchoredPosition.y / RowHeight), 0, Mathf.Max(0, rows.Count - 1));
        int count = Mathf.Min(rows.Count, Mathf.CeilToInt(height / RowHeight) + 2);
        bool moved = windowStart != first;
        windowStart = first;
        if (moved) hoveredRow = null;
        while (rowViews.Count < count)
        {
            RowView view = new();
            Image bg = Ugui.Image(listContent!, "Recipe", theme.PackSurface, Palette.Hex(0x111716, 0.18f), raycast: true)
                .At(0, 0, ListWidth - 6, RowHeight - 5);
            view.Background = bg;
            view.Bar = Ugui.Image(bg.transform, "Selection edge", theme.PackEdge, InventoryCategories.Gold).Stretch();
            view.Bar.enabled = false;
            view.Icon = Ugui.ItemIcon(Ugui.Image(bg.transform, "Icon", null, Color.white).At(7, 6, 30, 30));
            view.Title = Ugui.Text(bg.transform, "Name", "", theme.Strong, 13, Palette.Bone).At(44, 4, ListWidth - 64, 19);
            view.Title.enableAutoSizing = true; view.Title.fontSizeMin = 10; view.Title.fontSizeMax = 13;
            view.Kind = Ugui.Text(bg.transform, "Kind", "", theme.Body, 10, Palette.Muted).At(44, 23, ListWidth - 64, 14);
            view.Availability = Ugui.Image(bg.transform, "Availability", theme.Disc, Color.white).At(ListWidth - 16, 18, 5, 5);
            Ugui.HitArea(bg.transform, "Hit", () =>
            {
                if (view.BoundIndex < 0 || view.BoundIndex >= rows.Count) return;
                CraftAmount.Amount = 1;
                SetRecipe.Invoke(gui, new object[] { view.Row.Index, false });
            }).Stretch().OnHover(on => hoveredRow = on ? bg : hoveredRow == bg ? null : hoveredRow);
            rowViews.Add(view);
        }
        for (int i = 0; i < rowViews.Count; i++)
        {
            RowView view = rowViews[i];
            int index = first + i;
            bool visible = i < count && index < rows.Count;
            if (view.Background.gameObject.activeSelf != visible) view.Background.gameObject.SetActive(visible);
            if (!visible) { view.BoundIndex = -1; view.Row = default; continue; }
            Row r = rows[index];
            bool rebind = view.BoundIndex != index || view.Row.Recipe != r.Recipe || view.Row.Item != r.Item ||
                view.Row.Can != r.Can || view.Quality != (r.Item?.m_quality ?? 0);
            view.Row = r;
            if (!rebind) continue;
            view.BoundIndex = index;
            view.Quality = r.Item?.m_quality ?? 0;
            view.Background.At(0, index * RowHeight, ListWidth - 6, RowHeight - 5);
            view.Icon.sprite = r.Recipe.m_item.m_itemData.GetIcon();
            ItemDrop.ItemData.SharedData shared = r.Recipe.m_item.m_itemData.m_shared;
            string name = Localization.instance.Localize(shared.m_name);
            if (r.Recipe.m_amount > 1) name += " ×" + r.Recipe.m_amount;
            view.Title.text = name;
            string kind = CraftFilter.Of(shared).ToString();
            if (CraftFilter.Of(shared) == CraftCategory.Armor) kind += " · " + CraftFilter.Slot(shared.m_itemType);
            if (r.Item != null) kind = "Quality " + r.Item.m_quality + " to " + (r.Item.m_quality + 1);
            view.Kind.text = kind;
            view.Availability.color = r.Can ? Palette.Hex(0x9BC48A) : Palette.Hex(0xF08B80);
        }
    }

    private void Highlight(InventoryGui g)
    {
        object selected = SelectedField.GetValue(g);
        Recipe? recipe = PairRecipe.GetValue(selected) as Recipe;
        ItemDrop.ItemData? item = PairItem.GetValue(selected) as ItemDrop.ItemData;
        // Scroll to the selected logical recipe before binding the visible row pool.
        if (recipe && ZInput.IsGamepadActive() && !ZInput.IsMouseActive())
        {
            foreach (Row row in rows)
            {
                if (row.Recipe != recipe || row.Item != item || padSelection == row.Index) continue;
                padSelection = row.Index;
                float height = ((RectTransform)listContent!.parent).rect.height;
                float scroll = listContent.anchoredPosition.y, top = row.Index * RowHeight;
                if (top < scroll) scroll = top;
                else if (top + RowHeight > scroll + height) scroll = top + RowHeight - height;
                scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, listContent.rect.height - height));
                listContent.anchoredPosition = new Vector2(0, scroll);
                break;
            }
        }
        UpdateRowWindow();
        foreach (RowView view in rowViews)
        {
            if (view.BoundIndex < 0) continue;
            bool on = recipe && view.Row.Recipe == recipe && view.Row.Item == item;
            if (view.Bar.enabled != on) view.Bar.enabled = on;
            view.Background.color = on ? InventoryCategories.Gold.WithAlpha(0.18f) :
                view.Background == hoveredRow ? Palette.Bone.WithAlpha(0.08f) : Palette.Hex(0x111716, 0.18f);
        }
    }

    private void ShowRecipe(InventoryGui g, Player player)
    {
        bool has = g.m_recipeName.enabled && g.m_recipeIcon.enabled;
        recipeRim!.gameObject.SetActive(has);
        recipeName!.text = has ? g.m_recipeName.text : "";
        if (has) recipeIcon!.sprite = g.m_recipeIcon.sprite;
        object selectedRecipe = SelectedField.GetValue(g);
        Recipe? selected = has ? PairRecipe.GetValue(selectedRecipe) as Recipe : null;
        ItemDrop.ItemData? upgrading = PairItem.GetValue(selectedRecipe) as ItemDrop.ItemData;
        int quality = upgrading == null ? 1 : upgrading.m_quality + 1;
        recipeType!.text = selected ? CraftFilter.Of(selected!.m_item.m_itemData.m_shared) + " · " +
            (upgrading == null ? "Quality " + quality : "Quality " + upgrading.m_quality + " to " + quality) : "";

        bool needsStation = has && g.m_minStationLevelIcon.gameObject.activeSelf;
        stationNeed!.text = needsStation ? "Needs station level " + g.m_minStationLevelText.text : "";
        Color warn = g.m_minStationLevelText.color;
        stationNeed.color = needsStation && warn.r > 0.9f && warn.g < 0.3f ? Palette.Alert : Palette.Muted;

        int amount = selected ? selected!.m_amount * (upgrading == null ? CraftAmount.Amount : 1) : 0;
        string language = Localization.instance.GetSelectedLanguage();
        if (selected != detailRecipe || upgrading != detailUpgrade || quality != detailQuality || amount != detailAmount ||
            Game.m_worldLevel != detailWorld || language != detailLanguage || Time.unscaledTime >= nextDetails)
        {
            detailRecipe = selected; detailUpgrade = upgrading; detailQuality = quality; detailAmount = amount;
            detailWorld = Game.m_worldLevel; detailLanguage = language; nextDetails = Time.unscaledTime + 0.2f;
            string text = "";
            if (selected)
            {
                ItemDrop.ItemData data = selected!.m_item.m_itemData;
                string native = ItemDrop.ItemData.GetTooltip(data, quality, true, Game.m_worldLevel, amount);
                text = Restyle(Localization.instance.Localize(RecipeDetails.Format(native, data.m_shared.m_description)));
            }
            if (text != shownDescription)
            {
                shownDescription = text;
                description!.text = text;
                description.ForceMeshUpdate();
                descriptionContent!.sizeDelta = new Vector2(0, description.preferredHeight + 10);
                descriptionContent.anchoredPosition = Vector2.zero;
            }
        }
        string nativeDescription = has ? g.m_recipeDecription.text : "";
        string craftType = has && g.m_itemCraftType.gameObject.activeSelf ? g.m_itemCraftType.text : "";
        if (nativeDescription != nativeTooltip || craftType != nativeCraftType)
        {
            nativeTooltip = nativeDescription; nativeCraftType = craftType;
            string fullTooltip = Restyle(nativeDescription);
            if (craftType.Length > 0) fullTooltip += "\n\n" + Restyle(craftType);
            recipeTooltip!.Set(has ? g.m_recipeName.text : "", "RECIPE · ITEM DETAILS", fullTooltip);
        }

        ShowRequirements(g, player, has);

        // Craft button, or the progress bar while Valheim crafts.
        bool crafting = g.m_craftProgressPanel.gameObject.activeSelf;
        craftButton!.gameObject.SetActive(has && !crafting);
        progressTrack!.gameObject.SetActive(crafting);
        if (crafting)
        {
            float max = Mathf.Max(0.01f, BarMax(g.m_craftProgressBar));
            progressFill!.fillAmount = Mathf.Clamp01(BarValue(g.m_craftProgressBar) / max);
        }
        bool ready = g.m_craftButton.interactable;
        craftButton.color = ready ? InventoryCategories.Gold : Palette.Hex(0x343B37, 0.45f);
        craftLabel!.text = g.m_craftButton.GetComponentInChildren<TMP_Text>().text;
        craftLabel.color = ready ? Palette.Ink : Palette.Muted;
        if (crafting) readyLabel!.text = "Crafting…";
        else if (!has) readyLabel!.text = "Choose a recipe";
        else if (ready) readyLabel!.text = upgrading == null ? "Ready to craft" : "Ready to upgrade";
        else if (needsStation && warn.r > 0.9f && warn.g < 0.3f) readyLabel!.text = "Higher station level required";
        else if (missingMaterials) readyLabel!.text = "Missing materials";
        else readyLabel!.text = "Requirements not met";
        readyLabel!.color = crafting ? InventoryCategories.Gold : ready ? Palette.Hex(0x9BC48A) : has ? Palette.Hex(0xF08B80) : Palette.Muted;

        // A disabled native control is not an available action. Hide the entire extension,
        // including its contour, rather than leaving a muted repair label behind.
        bool repair = SupportsRepair(player) && g.m_repairButton.gameObject.activeInHierarchy && g.m_repairButton.interactable;
        if (!repair) repairHovered = false;
        repairButton!.gameObject.SetActive(repair);
        background!.ShowWing(repair);
        frameEdge!.ShowWing(repair);
        repairIcon!.color = repairHovered ? Palette.GoldBright : InventoryCategories.Gold;
        repairLabel!.color = repairHovered ? Palette.GoldBright : Palette.Bone;
        repairTooltip!.Set("Repair", "EQUIPMENT", "Repair a damaged item using this station.");
        bool style = has && g.m_variantButton.gameObject.activeSelf;
        variantButton!.gameObject.SetActive(style);

        // The amount: new items only (upgrades are one item), not while a craft runs; kept affordable.
        bool newItem = has && PairItem.GetValue(SelectedField.GetValue(g)) == null;
        if (!newItem && !crafting && CraftAmount.Amount != 1) SetAmount(1);
        else if (newItem && !crafting && CraftAmount.Amount > 1 && !amountField!.isFocused && !CanAfford(CraftAmount.Amount)) SetAmount(CraftAmount.Amount);
        if (amountField && !amountField!.isFocused && amountField.text != CraftAmount.Amount.ToString())
            amountField.SetTextWithoutNotify(CraftAmount.Amount.ToString());
        Arrange(style, newItem);
    }

    // The selected recipe's requirements worked out as Valheim does (same station and upgrader rules),
    // The total have / need count includes eligible nearby chests when nearby crafting is on.
    // A second line separates the bag and chest contributions without changing resource authority.
    // More than four page through every two seconds, like Valheim's list.
    private readonly List<Piece.Requirement> needed = new();
    private bool missingMaterials;

    private void ShowRequirements(InventoryGui g, Player player, bool has)
    {
        object selected = SelectedField.GetValue(g);
        Recipe? recipe = has ? PairRecipe.GetValue(selected) as Recipe : null;
        ItemDrop.ItemData? upgrade = PairItem.GetValue(selected) as ItemDrop.ItemData;
        int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
        int multiplier = upgrade == null && (CraftAmount.Amount > 1 || ZInput.GetButton("AltPlace")) ? g.m_multiCraftAmount : 1;
        NearbyStorage.StorageSettings? storage = NearbyStorage.StorageRuntime.Settings;
        bool nearby = storage != null && storage.Enabled.Value && NearbyStorage.StorageRuntime.Registry != null;
        if (recipe == requirementsRecipe && quality == requirementsQuality && multiplier == requirementsAmount &&
            nearby == requirementsNearby && Time.unscaledTime < nextRequirements) return;
        requirementsRecipe = recipe; requirementsQuality = quality; requirementsAmount = multiplier;
        requirementsNearby = nearby; nextRequirements = Time.unscaledTime + 0.2f;
        needed.Clear();
        missingMaterials = false;
        materialsLabel!.text = recipe && recipe!.m_requireOnlyOneIngredient ? "ANY ONE INGREDIENT" : "REQUIRED MATERIALS";
        CraftingStation station = player.GetCurrentCraftingStation();
        if (recipe)
            foreach (Piece.Requirement req in recipe!.m_resources)
            {
                if (!req.m_resItem || req.GetAmount(quality) <= 0) continue;
                if (station ? station.m_upgrader != req.m_upgraderResource : req.m_upgraderResource) continue;
                if (recipe.m_requireOnlyOneIngredient && !player.IsKnownMaterial(req.m_resItem.m_itemData.m_shared.m_name)) continue;
                needed.Add(req);
            }

        int pages = Mathf.Max(1, Mathf.CeilToInt(needed.Count / (float)requirements.Count));
        int first = (int)(Time.unscaledTime / 2f) % pages * requirements.Count;
        NearbyStorage.ResourceScope? scope = null;
        try
        {
            if (nearby)
                scope = new NearbyStorage.ResourceScope(player, NearbyStorage.StorageRuntime.Registry!, false);
            foreach (Piece.Requirement resource in needed)
                if (player.GetInventory().CountItems(resource.m_resItem.m_itemData.m_shared.m_name) < resource.GetAmount(quality) * multiplier)
                    missingMaterials = true;
            int shownCount = Mathf.Min(requirements.Count, needed.Count - first);
            float tileWidth = (detailWidth - Mathf.Max(0, shownCount - 1) * 6) / Mathf.Max(1, shownCount);
            for (int i = 0; i < requirements.Count; i++)
            {
                var (tile, edge, icon, name, amount, sources, tooltip) = requirements[i];
                int index = first + i;
                bool shown = index < needed.Count;
                if (tile.gameObject.activeSelf != shown) tile.gameObject.SetActive(shown);
                if (!shown) continue;
                Piece.Requirement req = needed[index];
                ItemDrop.ItemData.SharedData shared = req.m_resItem.m_itemData.m_shared;
                int need = req.GetAmount(quality) * multiplier;
                int have = player.GetInventory().CountItems(shared.m_name, -1, true);
                // CountItems is augmented by the active scope; subtract the same chest count
                // to recover carried stock without counting the chests twice.
                int inChests = scope?.Count(shared.m_name, -1, true) ?? 0;
                int inBag = have - inChests;
                icon.sprite = req.m_resItem.m_itemData.GetIcon();
                name.text = Localization.instance.Localize(shared.m_name);
                tile.At(detailX + i * (tileWidth + 6), requirementY, tileWidth, 74);
                icon.At(tileWidth / 2 - 12, 4, 24, 24);
                name.At(4, 28, tileWidth - 8, 14);
                amount.At(2, 41, tileWidth - 4, 18);
                amount.text = have + " / " + need;
                sources.At(2, 59, tileWidth - 4, 13);
                sources.text = scope != null ? "Bag " + inBag + " · Chests " + inChests : "Bag " + inBag;
                tooltip.Set(name.text, "Crafting materials", "In your bag: " + inBag +
                    "\nNearby chests: " + inChests + (scope == null ? " (nearby crafting disabled)" : "") +
                    "\nTotal available: " + have + "\nRequired: " + need);
                bool available = have >= need;
                amount.color = available ? Palette.Hex(0x9BC48A) : Palette.Hex(0xF08B80);
                edge.color = available ? Palette.Hex(0x626B64, 0.45f) : Palette.Hex(0xD64A3E, 0.65f);
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static bool SupportsRepair(Player? player)
    {
        CraftingStation? station = player ? player!.GetCurrentCraftingStation() : null;
        return station && station!.m_canRepair;
    }

    private static string Restyle(string text) => text
        .Replace("<color=orange>", "<color=#CDAE7C>").Replace("<color=\"orange\">", "<color=#CDAE7C>")
        .Replace("<color=yellow>", "<color=#EADFC0>").Replace("<color=\"yellow\">", "<color=#EADFC0>")
        .Replace("<color=red>", "<color=#B3202B>").Replace("<color=\"red\">", "<color=#B3202B>");
}
