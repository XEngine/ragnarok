using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// The Ragnarök crafting panel, drawn over Valheim's (which keeps running unseen: recipe list, selection,
// requirements, timing, multi-craft, upgrades and repair stay Valheim's). Ours reads its state each frame
// and presses its buttons. The search box and category chips set CraftFilter, which filters Valheim's
// own list (so the rows here are exactly what Valheim offers).
internal sealed class CraftPanel : IDisposable
{
    // Pad keeps the contents clear of the window's corner knots (they reach about 26 px in).
    private const float Pad = 28, Header = 50, SearchY = 62, ChipsY = 100, ListTop = 138, ListWidth = 236, RowHeight = 38, Gap = 14;
    private const float TabWidth = 104, TabsWidth = TabWidth * 3 + 30, Bottom = 42, Stepper = 112;


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
    private readonly List<(Row Row, Image Background, Image Bar)> rowViews = new();
    private readonly List<(CraftCategory? Category, Image Chip, Image Icon)> chips = new();
    private readonly List<(Image Tile, Image Icon, TextMeshProUGUI Name, TextMeshProUGUI Amount)> requirements = new();
    private bool dirty = true;
    private Image? hoveredRow;
    private readonly Dictionary<Image, bool> rowLook = new();
    private string shownDescription = "";
    private int signature;
    private InventoryGui? gui;
    private RectTransform? root, listContent, descriptionContent;
    private Vector2 builtSize;
    private Image? background, stationIcon, recipeIcon, recipeRim, craftButton, progressTrack, progressFill, repairButton, repairIcon, variantButton;
    private TextMeshProUGUI? stationName, stationLevel, recipeName, recipeType, stationNeed, description, craftLabel, hint, emptyNote, repairLabel;
    private RectTransform? stepper;
    private TMP_InputField? amountField;
    private float detailX, detailWidth;
    private TMP_InputField? search;
    private (Image Chip, TextMeshProUGUI Label) craftTab, upgradeTab;

    internal CraftPanel(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal bool Wanted => settings.CraftingStyle.Value switch
    {
        PanelStyle.Ragnarok => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    // The panel while it is on screen (the key guide sits under it), else null.
    internal RectTransform? Shown =>
        root && gui && InventoryGui.IsVisible() && root!.gameObject.activeInHierarchy ? root : null;

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            undo.Clear();
            root = null;
            gui = current;
        }
        if (!gui) return;
        if (!Wanted)
        {
            Restore();
            return;
        }
        if (!InventoryGui.IsVisible() || !Player.m_localPlayer || !gui!.m_crafting.gameObject.activeInHierarchy) return;
        RectTransform panel = gui.m_crafting;
        if (!root || panel.rect.size != builtSize) Build(panel);
        Refresh(Player.m_localPlayer);
    }

    public void Dispose()
    {
        Restore();
    }

    private void Restore()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        rowViews.Clear();
        chips.Clear();
        requirements.Clear();
        rowLook.Clear();
        search = null;
        if (amountField) UiState.TypingFields.Remove(amountField!);
        amountField = null;
        CraftAmount.Reset();
    }

    // Construction ---------------------------------------------------------------------------

    private void Build(RectTransform panel)
    {
        Restore();
        HideVanilla(panel);
        builtSize = panel.rect.size;
        float width = builtSize.x, height = builtSize.y;
        dirty = true;

        root = Ugui.Node(panel, "Ragnarok Crafting");
        root.Stretch();
        background = Ugui.Image(root, "Frame", theme.Window, Color.white, raycast: true).Stretch();

        // Station: icon in a gold diamond, its name, its level; Craft / Upgrade tabs on the right.
        Ugui.Image(root, "Station Rim", theme.Diamond, Palette.Gold).At(Pad, 6, 38, 38);
        Ugui.Image(root, "Station Core", theme.Diamond, Palette.Obsidian).At(Pad + 2, 8, 34, 34);
        stationIcon = Ugui.Image(root, "Station Icon", null, Color.white).At(Pad + 9, 15, 20, 20);
        stationIcon.preserveAspect = true;
        // The window title (Alegreya, 24) over its subtext in Scada italic.
        stationName = Ugui.Text(root, "Station", "", theme.Heading, 24, Palette.Gold).At(Pad + 48, -1, width - TabsWidth - Pad * 2 - 70, 36);
        stationLevel = Ugui.Text(root, "Level", "", theme.Subtle, 13, Palette.Muted).At(Pad + 48, 30, 200, 18);
        // Top right: Repair, a divider, then the Craft / Upgrade tabs; each with its glyph and name.
        float tabsX = width - Pad - TabWidth * 2 - 6;
        craftTab = Tab("Craft", theme.IconHammer, tabsX, () => gui!.OnTabCraftPressed());
        upgradeTab = Tab("Upgrade", theme.IconUpgrade, tabsX + TabWidth + 6, () => gui!.OnTabUpgradePressed());
        Ugui.Image(root, "Tabs Divider", theme.White, Palette.Rule).At(tabsX - 12, 12, 1, 26);
        repairButton = Ugui.Image(root, "Repair", theme.Button, Color.white).At(tabsX - 24 - TabWidth, 12, TabWidth, 26);
        repairIcon = Ugui.Image(repairButton.transform, "Icon", theme.IconRepair, Palette.Bone).At(10, 5, 16, 16);
        repairLabel = Ugui.Text(repairButton.transform, "Label", "Repair", theme.Strong, 13, Palette.Bone, TextAlignmentOptions.Center)
            .Stretch(22, 0, 4, 0);
        Ugui.HitArea(repairButton.transform, "Hit", () =>
        {
            if (gui!.m_repairButton.interactable) gui.m_repairButton.onClick.Invoke();
        }).Stretch();
        Ornaments.Rule(root, theme, Pad, Header, width - Pad * 2);

        SearchField(width);
        FilterChips(width);

        // Recipe list on the left.
        RectTransform view = Ugui.Node(root, "Recipes").At(Pad, ListTop, ListWidth, height - ListTop - Pad);
        listContent = Scroll(view);
        emptyNote = Ugui.Text(view, "Empty", "Nothing matches", theme.Subtle, 13, Palette.Faint, TextAlignmentOptions.Center)
            .At(0, 20, ListWidth, 20);
        Ugui.Image(root, "List Rule", theme.White, Palette.Rule).At(Pad + ListWidth + Gap / 2, ListTop, 1, height - ListTop - Pad);

        Detail(width, height);
    }

    private (Image, TextMeshProUGUI) Tab(string label, Sprite glyph, float x, Action press)
    {
        Image chip = Ugui.Image(root!, label, theme.Button, Color.white).At(x, 12, TabWidth, 26);
        Ugui.Image(chip.transform, "Icon", glyph, Palette.Bone).At(10, 5, 16, 16);
        TextMeshProUGUI text = Ugui.Text(chip.transform, "Label", label, theme.Strong, 13, Palette.Muted, TextAlignmentOptions.Center)
            .Stretch(22, 0, 4, 0);
        Ugui.HitArea(chip.transform, "Hit", press).Stretch();
        return (chip, text);
    }

    private void SearchField(float width)
    {
        Image field = Ugui.Image(root!, "Search", theme.Field, Color.white, raycast: true).At(Pad, SearchY, width - Pad * 2, 30);
        field.gameObject.SetActive(false);
        Ugui.Image(field.transform, "Glyph", theme.IconSearch, Palette.Muted).At(8, 6, 18, 18);
        RectTransform area = Ugui.Node(field.transform, "Text Area").Stretch(34, 5, 10, 5);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Ugui.Text(area, "Placeholder", "Search recipes", theme.Subtle, 14, Palette.Faint).Stretch();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "", theme.Body, 14, Palette.Bone).Stretch();
        search = field.gameObject.AddComponent<TMP_InputField>();
        search.textViewport = area;
        search.textComponent = text;
        search.placeholder = placeholder;
        search.targetGraphic = field;
        search.characterLimit = 40;
        search.customCaretColor = true;
        search.caretColor = Palette.GoldBright;
        search.selectionColor = Palette.Gold.WithAlpha(0.35f);
        search.text = CraftFilter.Query;
        Ugui.SpriteSwap(search, theme.Field, theme.FieldFocus);
        search.spriteState = new SpriteState { highlightedSprite = theme.FieldFocus, pressedSprite = theme.FieldFocus, selectedSprite = theme.FieldFocus };
        field.gameObject.SetActive(true);
        UiState.TypingFields.Add(search);
        search.onValueChanged.AddListener(v =>
        {
            CraftFilter.SetQuery(v);
            dirty = true;
        });
    }

    // Category chips (none lit = everything; any lit = those kinds), then "craftable only" on the right.
    private void FilterChips(float width)
    {
        chips.Clear();
        (CraftCategory Category, Sprite Icon, string Name)[] kinds =
        {
            (CraftCategory.Weapons, theme.IconSword, "Weapons"), (CraftCategory.Armor, theme.IconHelm, "Armour"),
            (CraftCategory.Ammo, theme.IconArrow, "Ammo"), (CraftCategory.Tools, theme.IconHammer, "Tools"),
            (CraftCategory.Health, theme.IconHeart, "Health"), (CraftCategory.Stamina, theme.IconBolt, "Stamina"),
            (CraftCategory.Eitr, theme.IconDrop, "Eitr"), (CraftCategory.Materials, theme.IconIngot, "Materials"),
            (CraftCategory.Other, theme.IconOther, "Other")
        };
        const float size = 28, gap = 6;
        float x = Pad;
        foreach (var (category, icon, name) in kinds)
        {
            CraftCategory c = category;
            AddChip(c, icon, name, x, () =>
            {
                CraftFilter.Toggle(c);
                dirty = true;
            });
            x += size + gap;
        }
        hint = Ugui.Text(root!, "Hint", "", theme.Subtle, 13, Palette.Muted, TextAlignmentOptions.MidlineRight)
            .At(x, ChipsY, width - Pad - size - 10 - x, size);
        AddChip(null, theme.IconCheck, "Craftable only", width - Pad - size, () =>
        {
            CraftFilter.ToggleCraftable();
            dirty = true;
        });
    }

    private void AddChip(CraftCategory? category, Sprite glyph, string name, float x, Action toggle)
    {
        Image chip = Ugui.Image(root!, name, theme.Button, Color.white).At(x, ChipsY, 28, 28);
        Image icon = Ugui.Image(chip.transform, "Icon", glyph, Palette.Faint).At(4, 4, 20, 20);
        Ugui.HitArea(chip.transform, "Hit", toggle).Stretch().OnHover(on =>
        {
            if (hint) hint!.text = on ? name : "";
        });
        chips.Add((category, chip, icon));
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
        recipeRim = Ugui.Image(root!, "Recipe Rim", theme.Diamond, Palette.Gold).At(x, ListTop, 64, 64);
        Ugui.Image(recipeRim.transform, "Core", theme.Diamond, Palette.Obsidian).At(2, 2, 60, 60);
        recipeIcon = Ugui.Image(recipeRim.transform, "Icon", null, Color.white).At(15, 15, 34, 34);
        Ugui.ItemIcon(recipeIcon);
        // As in the item tooltips: the name in Alegreya, the kind and the station it needs in italic subtext.
        recipeName = Ugui.Text(root!, "Name", "", theme.Heading, 22, Palette.Bone).At(x + 74, ListTop - 3, w - 74, 34);
        recipeType = Ugui.Text(root!, "Type", "", theme.Subtle, 13, Palette.Muted).At(x + 74, ListTop + 28, w - 74, 18);
        stationNeed = Ugui.Text(root!, "Station Need", "", theme.Subtle, 13, Palette.Muted).At(x + 74, ListTop + 45, w - 74, 18);

        float reqY = height - Pad - 50 - 76, descTop = ListTop + 74;
        RectTransform view = Ugui.Node(root!, "Description").At(x, descTop, w, reqY - descTop - 8);
        descriptionContent = Scroll(view);
        description = Ugui.Text(descriptionContent, "Text", "", theme.Body, 14, Palette.Bone.WithAlpha(0.85f), TextAlignmentOptions.TopLeft, wrap: true);
        RectTransform d = description.rectTransform;
        d.anchorMin = new Vector2(0, 1);
        d.anchorMax = new Vector2(1, 1);
        d.pivot = new Vector2(0, 1);
        d.offsetMin = Vector2.zero;
        d.offsetMax = new Vector2(-6, 0);
        description.lineSpacing = 6;
        description.overflowMode = TextOverflowModes.Overflow;

        // Up to four requirement tiles, mirroring Valheim's (it pages through more on its own).
        requirements.Clear();
        float tile = (w - 3 * 6) / 4;
        for (int i = 0; i < 4; i++)
        {
            Image t = Ugui.Image(root!, "Requirement", theme.Card, Color.white).At(x + i * (tile + 6), reqY, tile, 70);
            Image icon = Ugui.Image(t.transform, "Icon", null, Color.white).At(tile / 2 - 15, 6, 30, 30);
            Ugui.ItemIcon(icon);
            TextMeshProUGUI name = Ugui.Text(t.transform, "Name", "", theme.Body, 11, Palette.Muted, TextAlignmentOptions.Center)
                .At(4, 36, tile - 8, 14);
            // A roomy box and no ellipsis: TMP hides a line outright when its height does not fit.
            TextMeshProUGUI amount = Ugui.Text(t.transform, "Amount", "", theme.Strong, 15, Palette.Bone, TextAlignmentOptions.Center)
                .At(2, 48, tile - 4, 22);
            amount.overflowMode = TextOverflowModes.Overflow;
            requirements.Add((t, icon, name, amount));
        }

        // Bottom: style (when the item has styles), how many to craft, and the craft button (a progress bar
        // while crafting), which takes whatever room the others leave. Laid out each frame in Arrange.
        detailX = x;
        detailWidth = w;
        float by = height - Pad - Bottom;
        variantButton = Ugui.Image(root!, "Style", theme.Button, Color.white).At(x, by, 64, Bottom);
        Ugui.Text(variantButton.transform, "Label", "Style", theme.Strong, 13, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(variantButton.transform, "Hit", () => gui!.m_variantButton.onClick.Invoke()).Stretch();
        AmountStepper(x, by);

        float cx = x, cw = w;
        craftButton = Ugui.Image(root!, "Craft", theme.Primary, Color.white).At(cx, by, cw, Bottom);
        craftLabel = Ugui.Text(craftButton.transform, "Label", "", theme.Strong, 16, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(craftButton.transform, "Hit", () =>
        {
            if (gui!.m_craftButton.interactable) gui.m_craftButton.onClick.Invoke();
        }).Stretch();
        progressTrack = Ugui.Image(root!, "Progress", theme.HudFrame, Color.white).At(cx, by + 12, cw, 18);
        progressFill = Ugui.Image(progressTrack.transform, "Fill", theme.White, Palette.Gold).Stretch(3, 3, 3, 3);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
    }

    // How many to craft: - [ N ] +, the number typed or stepped (1 to as many as you can afford).
    private void AmountStepper(float x, float y)
    {
        stepper = Ugui.Node(root!, "Amount").At(x, y, Stepper, Bottom);
        Image minus = Ugui.Image(stepper, "Less", theme.Button, Color.white, raycast: true).At(0, 0, 30, Bottom);
        Ugui.Text(minus.transform, "Label", "–", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(minus.transform, "Hit", () => SetAmount(CraftAmount.Amount - 1)).Stretch();
        Image plus = Ugui.Image(stepper, "More", theme.Button, Color.white, raycast: true).At(Stepper - 30, 0, 30, Bottom);
        Ugui.Text(plus.transform, "Label", "+", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Ugui.HitArea(plus.transform, "Hit", () => SetAmount(CraftAmount.Amount + 1)).Stretch();

        Image box = Ugui.Image(stepper, "Box", theme.Field, Color.white, raycast: true).At(33, 0, Stepper - 66, Bottom);
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
        if (style) x += 64 + 6;
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
            if (child.name == "Ragnarok Crafting") continue;
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

    // Per frame ------------------------------------------------------------------------------

    private void Refresh(Player player)
    {
        InventoryGui g = gui!;
        background!.color = Color.white.WithAlpha(settings.InventoryOpacity.Value);

        // Station header and tabs, as Valheim set them this frame.
        stationName!.text = g.m_craftingStationName.text;
        bool atStation = g.m_craftingStationIcon.gameObject.activeSelf;
        stationIcon!.enabled = atStation;
        if (atStation) stationIcon.sprite = g.m_craftingStationIcon.sprite;
        stationLevel!.text = g.m_craftingStationLevelRoot.gameObject.activeSelf ? "Level " + g.m_craftingStationLevel.text : "By hand";
        SetTab(craftTab, g.m_tabCraft.gameObject.activeSelf, !g.m_tabCraft.interactable);
        SetTab(upgradeTab, g.m_tabUpgrade.gameObject.activeSelf, !g.m_tabUpgrade.interactable);

        // Only the kinds this station makes (from every recipe you know here) get a chip, side by side.
        float chipX = Pad;
        foreach (var (category, chip, _) in chips)
        {
            if (category is not { } kind) continue;
            bool show = CraftFilter.Present.Contains(kind);
            if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
            if (!show) continue;
            RectTransform rect = chip.rectTransform;
            if (rect.anchoredPosition.x != chipX) rect.anchoredPosition = new Vector2(chipX, rect.anchoredPosition.y);
            chipX += 34;
        }
        foreach (var (category, chip, icon) in chips)
        {
            bool on = category is { } c ? CraftFilter.Categories.Contains(c) : CraftFilter.CraftableOnly;
            Look(chip, on, theme.SegmentOn, theme.Button);
            icon.color = on ? Palette.Bone : Palette.Faint;
        }

        ReadRecipes(g);
        if (dirty) RebuildRows();
        Highlight(g);
        ShowRecipe(g, player);
    }

    private void SetTab((Image Chip, TextMeshProUGUI Label) tab, bool visible, bool selected)
    {
        if (tab.Chip.gameObject.activeSelf != visible) tab.Chip.gameObject.SetActive(visible);
        Look(tab.Chip, selected, theme.SegmentOn, theme.Button);
        tab.Label.color = selected ? Palette.Bone : Palette.Muted;
    }

    private void ReadRecipes(InventoryGui g)
    {
        IList list = (IList)Available.GetValue(g);
        int hash = list.Count;
        rows.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            object pair = list[i];
            Recipe recipe = (Recipe)PairRecipe.GetValue(pair);
            ItemDrop.ItemData? item = PairItem.GetValue(pair) as ItemDrop.ItemData;
            bool can = (bool)PairCan.GetValue(pair);
            rows.Add(new Row(recipe, item, can, i));
            unchecked { hash = hash * 31 + (recipe ? recipe.GetInstanceID() : 0) * 7 + (item?.GetHashCode() ?? 0) + (can ? 1 : 0); }
        }
        if (hash != signature)
        {
            signature = hash;
            dirty = true;
        }
    }

    private void RebuildRows()
    {
        dirty = false;
        Ugui.Clear(listContent!);
        foreach (var view in rowViews) rowLook.Remove(view.Background);
        rowViews.Clear();
        hoveredRow = null;
        float y = 0;
        foreach (Row row in rows)
        {
            Row r = row;
            Image bg = Ugui.Image(listContent!, "Recipe", theme.Card, Color.white, raycast: true).At(0, y, ListWidth - 8, RowHeight - 4);
            Image bar = Ugui.Image(bg.transform, "Selected", theme.White, Palette.GoldBright).At(0, 6, 3, RowHeight - 16);
            Image icon = Ugui.Image(bg.transform, "Icon", r.Recipe.m_item.m_itemData.GetIcon(), Color.white).At(8, 4, 26, 26);
            Ugui.ItemIcon(icon);
            ItemDrop.ItemData.SharedData shared = r.Recipe.m_item.m_itemData.m_shared;
            string name = Localization.instance.Localize(shared.m_name);
            if (r.Recipe.m_amount > 1) name += $" <color=#{ColorUtility.ToHtmlStringRGBA(Palette.Faint)}>×{r.Recipe.m_amount}</color>";
            Color text = r.Can ? Palette.Bone : Palette.Faint;
            Ugui.Text(bg.transform, "Name", name, theme.Strong, 14, text).At(42, 0, ListWidth - 90, RowHeight - 4);
            // Upgrades: the level it would reach, as a gold diamond.
            if (r.Item != null)
            {
                Image gem = Ugui.Image(bg.transform, "Quality", theme.Diamond, r.Can ? Palette.Gold : Palette.GoldDim).At(ListWidth - 38, 7, 20, 20);
                Ugui.Text(gem.transform, "Level", (r.Item.m_quality + 1).ToString(), theme.Strong, 11, Palette.Ink, TextAlignmentOptions.Center)
                    .Stretch().overflowMode = TextOverflowModes.Overflow;
            }
            Ugui.HitArea(bg.transform, "Hit", () => SetRecipe.Invoke(gui, new object[] { r.Index, false })).Stretch()
                .OnHover(on => hoveredRow = on ? bg : hoveredRow == bg ? null : hoveredRow);
            rowViews.Add((r, bg, bar));
            y += RowHeight;
        }
        listContent!.sizeDelta = new Vector2(0, y);
        emptyNote!.enabled = rowViews.Count == 0;
    }

    private void Highlight(InventoryGui g)
    {
        object selected = SelectedField.GetValue(g);
        Recipe? recipe = PairRecipe.GetValue(selected) as Recipe;
        ItemDrop.ItemData? item = PairItem.GetValue(selected) as ItemDrop.ItemData;
        foreach (var (row, bg, bar) in rowViews)
        {
            bool on = recipe && row.Recipe == recipe && row.Item == item;
            if (bar.enabled != on) bar.enabled = on;
            // Only swap when the look changes: ChipSkin has replaced the sprite with its chipped version.
            Look(bg, on || bg == hoveredRow, theme.CardHover, theme.Card);
        }
    }

    private void ShowRecipe(InventoryGui g, Player player)
    {
        bool has = g.m_recipeName.enabled && g.m_recipeIcon.enabled;
        recipeRim!.gameObject.SetActive(has);
        recipeName!.text = has ? g.m_recipeName.text : "";
        if (has) recipeIcon!.sprite = g.m_recipeIcon.sprite;
        recipeType!.text = has && g.m_itemCraftType.gameObject.activeSelf ? Restyle(g.m_itemCraftType.text) : "";

        bool needsStation = has && g.m_minStationLevelIcon.gameObject.activeSelf;
        stationNeed!.text = needsStation ? "Needs station level " + g.m_minStationLevelText.text : "";
        Color warn = g.m_minStationLevelText.color;
        stationNeed.color = needsStation && warn.r > 0.9f && warn.g < 0.3f ? Palette.Alert : Palette.Muted;

        string text = has ? Restyle(g.m_recipeDecription.text) : "";
        if (text != shownDescription)
        {
            shownDescription = text;
            description!.text = text;
            description.ForceMeshUpdate();
            descriptionContent!.sizeDelta = new Vector2(0, description.preferredHeight + 10);
            descriptionContent.anchoredPosition = Vector2.zero;
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
        Look(craftButton, ready, theme.Primary, theme.ButtonOff);
        craftLabel!.text = g.m_craftButton.GetComponentInChildren<TMP_Text>().text;
        craftLabel.color = ready ? Palette.Bone : Palette.Faint;

        bool repair = g.m_repairButton.gameObject.activeInHierarchy, canRepair = g.m_repairButton.interactable;
        repairButton!.gameObject.SetActive(repair);
        repairIcon!.color = canRepair ? Palette.Bone : Palette.Faint;
        repairLabel!.color = canRepair ? Palette.Bone : Palette.Faint;
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
    // shown as have / need: "100/2". "Have" counts nearby chests when Nearby Chests crafting is on.
    // More than four page through every two seconds, like Valheim's list.
    private readonly List<Piece.Requirement> needed = new();

    private void ShowRequirements(InventoryGui g, Player player, bool has)
    {
        needed.Clear();
        object selected = SelectedField.GetValue(g);
        Recipe? recipe = has ? PairRecipe.GetValue(selected) as Recipe : null;
        ItemDrop.ItemData? upgrade = PairItem.GetValue(selected) as ItemDrop.ItemData;
        int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
        int multiplier = upgrade == null && (CraftAmount.Amount > 1 || ZInput.GetButton("AltPlace")) ? g.m_multiCraftAmount : 1;
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
            NearbyStorage.StorageSettings? storage = NearbyStorage.StorageRuntime.Settings;
            if (storage != null && storage.Enabled.Value && storage.Crafting.Value && NearbyStorage.StorageRuntime.Registry != null)
                scope = new NearbyStorage.ResourceScope(player, NearbyStorage.StorageRuntime.Registry, false);
            for (int i = 0; i < requirements.Count; i++)
            {
                var (tile, icon, name, amount) = requirements[i];
                int index = first + i;
                bool shown = index < needed.Count;
                if (tile.gameObject.activeSelf != shown) tile.gameObject.SetActive(shown);
                if (!shown) continue;
                Piece.Requirement req = needed[index];
                ItemDrop.ItemData.SharedData shared = req.m_resItem.m_itemData.m_shared;
                int need = req.GetAmount(quality) * multiplier;
                int have = player.GetInventory().CountItems(shared.m_name);
                icon.sprite = req.m_resItem.m_itemData.GetIcon();
                name.text = Localization.instance.Localize(shared.m_name);
                amount.text = $"{have}<color=#{ColorUtility.ToHtmlStringRGBA(Palette.Faint)}>/</color>{need}";
                amount.color = have >= need ? Palette.Bone : Palette.Alert;
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    // Swap between two frame looks only when the choice changes: ChipSkin has put its chipped
    // version in place, and reassigning every frame would rebuild the canvas every frame.
    private void Look(Image image, bool lit, Sprite on, Sprite off)
    {
        if (rowLook.TryGetValue(image, out bool shown) && shown == lit) return;
        image.sprite = lit ? on : off;
        rowLook[image] = lit;
    }

    private static string Restyle(string text) => text
        .Replace("<color=orange>", "<color=#CDAE7C>").Replace("<color=\"orange\">", "<color=#CDAE7C>")
        .Replace("<color=yellow>", "<color=#EADFC0>").Replace("<color=\"yellow\">", "<color=#EADFC0>")
        .Replace("<color=red>", "<color=#B3202B>").Replace("<color=\"red\">", "<color=#B3202B>");
}
