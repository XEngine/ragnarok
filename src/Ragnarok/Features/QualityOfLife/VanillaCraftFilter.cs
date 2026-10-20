using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// The crafting search and category filters on Valheim's own crafting panel, in its own style: a search
// box and a row of small button chips with Ragnarök's white glyphs above the recipe list, which moves
// down to make room. Shown while the Ragnarök crafting panel is off.
internal sealed class VanillaCraftFilter : IDisposable
{
    private const float FieldHeight = 28, ChipSize = 28, Spacing = 4;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Func<bool> ragnarokPanel;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly List<(CraftCategory? Category, Image Chip, Image Icon)> chips = new();
    private Button? clearButton, sortButton;
    private InventoryGui? gui;
    private RectTransform? root;
    private TMP_InputField? search;
    private readonly List<(CraftArmorSlot Slot, Button Button, TMP_Text Label)> armorChips = new();
    private readonly List<Button> ownedButtons = new();
    private RectTransform? nativeList;
    private Vector2 listPosition, listSize;
    private TMP_Text? sortLabel, shortcut;
    private VanillaCraftExtras? extras;
    private float nextSearch;
    private bool pendingSearch;
    private int context = -1;
    private float reserved = -1;

    internal VanillaCraftFilter(InterfaceSettings settings, Theme theme, Func<bool> ragnarokPanel)
    {
        this.settings = settings;
        this.theme = theme;
        this.ragnarokPanel = ragnarokPanel;
    }

    internal void Tick()
    {
        InventoryGui current = InventoryGui.instance;
        if (current != gui)
        {
            Dispose();
            gui = current;
        }
        if (!gui) return;
        if (ragnarokPanel() || !settings.Features.CraftFilters.Value)
        {
            bool hadPanel = root;
            Dispose();
            if (hadPanel && !ragnarokPanel()) CraftFilter.Changed();
            return;
        }
        if (!InventoryGui.IsVisible() || !gui!.m_crafting.gameObject.activeInHierarchy) return;
        if (!root) Build(gui);
        if (!root) return;
        if (context != CraftFilter.ContextVersion)
        {
            context = CraftFilter.ContextVersion; pendingSearch = false;
            search!.SetTextWithoutNotify(CraftFilter.Query);
        }
        if (pendingSearch && Time.unscaledTime >= nextSearch)
        { pendingSearch = false; CraftFilter.SetQuery(search!.text); }
        if (search && !pendingSearch && !search!.isFocused && search.text != CraftFilter.Query) search.SetTextWithoutNotify(CraftFilter.Query);
        bool slash = Input.inputString.IndexOf('/') >= 0 || Input.GetKeyDown(KeyCode.Slash);
        if (slash && !UiState.Typing && !UiState.ModalOpen && !Console.IsVisible() && !Menu.IsVisible() &&
            (!Chat.instance || !Chat.instance.HasFocus()))
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(search!.gameObject);
            search!.ActivateInputField();
        }
        // Only the kinds this station makes (from every recipe you know here) get a chip, side by side;
        // "craftable only" keeps its place at the end.
        int columns = Mathf.Max(1, Mathf.FloorToInt((root!.rect.width + Spacing) / (ChipSize + Spacing)));
        int visibleCategories = 0;
        foreach (var (category, chip, _) in chips)
        {
            bool show = category is not { } kind || CraftFilter.Present.Contains(kind);
            if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
            if (!show) continue;
            chip.At((visibleCategories % columns) * (ChipSize + Spacing),
                FieldHeight + Spacing + (visibleCategories / columns) * (ChipSize + Spacing), ChipSize, ChipSize);
            visibleCategories++;
        }
        foreach (var (category, chip, icon) in chips)
        {
            bool on = category is { } c ? CraftFilter.Categories.Contains(c) : CraftFilter.CraftableOnly;
            chip.color = on ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            icon.color = on ? Color.white : new Color(1, 1, 1, 0.35f);
        }
        bool armor = CraftFilter.Categories.Contains(CraftCategory.Armor);
        float utilityY = FieldHeight + Spacing + Mathf.CeilToInt(visibleCategories / (float)columns) * (ChipSize + Spacing);
        float utilityWidth = root.rect.width - 60;
        clearButton!.At(0, utilityY, 44, 22);
        sortButton!.At(48, utilityY, Mathf.Max(40, utilityWidth - 48), 22);
        float x = 0;
        int visibleSlots = 0;
        foreach (var entry in armorChips)
            if (entry.Slot == CraftArmorSlot.All || CraftFilter.PresentSlots.Contains(entry.Slot)) visibleSlots++;
        foreach (var (slot, button, label) in armorChips)
        {
            bool show = armor && (slot == CraftArmorSlot.All || CraftFilter.PresentSlots.Contains(slot));
            button.gameObject.SetActive(show);
            if (!show) continue;
            float width = Mathf.Min(44, (root!.rect.width - Mathf.Max(0, visibleSlots - 1) * 3) / Mathf.Max(1, visibleSlots));
            button.At(x, utilityY + 26, width, 22); x += width + 3;
            label.color = CraftFilter.ArmorSlot == slot ? Color.white : new Color(1, 0.85f, 0.6f, 0.65f);
        }
        float space = utilityY + 26 + (armor ? 26 : 0);
        if (reserved != space)
        {
            reserved = space;
            nativeList!.anchoredPosition = listPosition - new Vector2(0, space);
            nativeList.sizeDelta = listSize - new Vector2(0, space);
            root!.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, space);
        }
        sortLabel!.text = CraftFilter.CraftableFirst ? "Craftable first" : "Name A–Z";
        string close = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Inventory", true) : "TAB";
        if (string.IsNullOrEmpty(close)) close = "TAB";
        shortcut!.text = "/ · " + Localization.instance.Localize(close);
        shortcut.At(root.rect.width - 58, utilityY, 58, 22);
        extras?.Tick();
    }

    public void Dispose()
    {
        extras?.Dispose(); extras = null;
        foreach (Button button in ownedButtons) if (button) button.onClick.RemoveAllListeners();
        ownedButtons.Clear(); armorChips.Clear();
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null;
        chips.Clear();
        if (search) { search!.onValueChanged.RemoveAllListeners(); UiState.TypingFields.Remove(search); }
        search = null;
        nativeList = null; reserved = -1; pendingSearch = false; context = -1;
    }

    private void Build(InventoryGui g)
    {
        RectTransform? list = g.m_crafting.Find("RecipeList") as RectTransform;
        if (!list) return;
        float rows = FieldHeight + Spacing + ChipSize + Spacing * 2;

        // Our block takes the recipe list's top; the list moves down and shortens by as much.
        Vector2 position = list!.anchoredPosition, size = list.sizeDelta;
        Vector3 topLeft = list.TransformPoint(new Vector3(list.rect.xMin, list.rect.yMax, 0));
        nativeList = list; listPosition = position; listSize = size;
        list.anchoredPosition = position - new Vector2(0, rows);
        list.sizeDelta = size - new Vector2(0, rows);
        RectTransform moved = list;
        ScrollRect? valheimScroll = list.GetComponentInChildren<ScrollRect>(true);
        if (valheimScroll)
        {
            float sensitivity = valheimScroll!.scrollSensitivity;
            bool added = !valheimScroll.GetComponent<WheelStep>();
            WheelStep step = valheimScroll.GetComponent<WheelStep>() ?? valheimScroll.gameObject.AddComponent<WheelStep>();
            undo.Add((valheimScroll, () =>
            {
                if (added) UnityEngine.Object.Destroy(step);
                valheimScroll.scrollSensitivity = sensitivity;
            }));
        }
        undo.Add((moved, () =>
        {
            moved.anchoredPosition = position;
            moved.sizeDelta = size;
        }));

        root = Ugui.Node(g.m_crafting, "Ragnarok Craft Filter");
        root.anchorMin = list.anchorMin;
        root.anchorMax = list.anchorMax;
        root.pivot = list.pivot;
        root.anchoredPosition = position;
        root.sizeDelta = new Vector2(list.rect.width, rows);
        root.anchorMax = root.anchorMin;
        root.pivot = new Vector2(0, 1);
        root.position = topLeft;
        float width = list.rect.width;

        // Search box: Valheim's dark item panel, its body font, its text colour.
        Transform? panel = g.m_crafting.Find("Decription");
        Sprite? dark = panel && panel!.TryGetComponent(out Image darkImage) ? darkImage.sprite : null;
        Image field = Ugui.Image(root, "Search", dark, dark ? new Color(0, 0, 0, 0.55f) : new Color(0, 0, 0, 0.6f), raycast: true)
            .At(0, 0, width, FieldHeight);
        field.gameObject.SetActive(false);
        Ugui.Image(field.transform, "Glyph", theme.IconSearch, new Color(1, 1, 1, 0.55f)).At(6, 5, 18, 18);
        RectTransform area = Ugui.Node(field.transform, "Text Area").Stretch(30, 4, 8, 4);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text font = g.m_recipeDecription;
        TextMeshProUGUI placeholder = Ugui.Text(area, "Placeholder", "Recipes / ingredients", font.font, 15, new Color(1, 1, 1, 0.35f)).Stretch();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "", font.font, 15, new Color(1f, 0.85f, 0.6f)).Stretch();
        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        search = input;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = field;
        input.characterLimit = 80;
        input.text = CraftFilter.Query;
        field.gameObject.SetActive(true);
        input.onValueChanged.AddListener(_ => { pendingSearch = true; nextSearch = Time.unscaledTime + 0.18f; });
        UiState.TypingFields.Add(input);

        // Chips: Valheim's button art, Ragnarök's glyphs, a tooltip naming each.
        Image button = (Image)g.m_craftButton.targetGraphic;
        GameObject? tooltip = VanillaCraftExtras.BackedTooltip(g) ?? g.m_craftButton.GetComponent<UITooltip>()?.m_tooltipPrefab;
        (CraftCategory? Category, Sprite Glyph, string Name)[] kinds =
        {
            (CraftCategory.Weapons, theme.IconSword, "Weapons"), (CraftCategory.Shields, theme.InventoryShield, "Shields"),
            (CraftCategory.Armor, theme.IconHelm, "Armour"),
            (CraftCategory.Ammo, theme.IconArrow, "Ammo"), (CraftCategory.Tools, theme.IconHammer, "Tools"),
            (CraftCategory.Health, theme.IconHeart, "Health"), (CraftCategory.Stamina, theme.IconBolt, "Stamina"),
            (CraftCategory.Eitr, theme.IconDrop, "Eitr"), (CraftCategory.Materials, theme.IconIngot, "Materials"),
            (CraftCategory.Meads, theme.HudBottle, "Meads"),
            (CraftCategory.Other, theme.IconOther, "Other"), (null, theme.IconCheck, "Craftable only")
        };
        for (int i = 0; i < kinds.Length; i++)
        {
            var (category, glyph, name) = kinds[i];
            Image chip = Ugui.Image(root, name, button.sprite, Color.white, raycast: true)
                .At(0, FieldHeight + Spacing, ChipSize, ChipSize);
            chip.type = Image.Type.Sliced;
            Image icon = Ugui.Image(chip.transform, "Icon", glyph, Color.white).At(4, 4, ChipSize - 8, ChipSize - 8);
            Button b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
            ownedButtons.Add(b);
            CraftCategory? c = category;
            b.onClick.AddListener(() =>
            {
                if (c is { } kind) CraftFilter.Toggle(kind);
                else CraftFilter.ToggleCraftable();
            });
            if (tooltip)
            {
                UITooltip tip = chip.gameObject.AddComponent<UITooltip>();
                tip.m_tooltipPrefab = tooltip;
                tip.m_text = name;
            }
            chips.Add((category, chip, icon));
        }
        clearButton = NativeButton(root, g, "Clear", 0, 54, 46, () =>
        {
            pendingSearch = false; search!.SetTextWithoutNotify("");
            CraftFilter.Categories.Clear(); CraftFilter.ArmorSlot = CraftArmorSlot.All;
            CraftFilter.CraftableOnly = false;
            if (CraftFilter.Query.Length > 0) CraftFilter.SetQuery("");
            else CraftFilter.Changed();
        });
        sortButton = NativeButton(root, g, "", 50, 54, width - 50, () =>
        { CraftFilter.CraftableFirst = !CraftFilter.CraftableFirst; CraftFilter.Changed(); });
        sortLabel = sortButton.GetComponentInChildren<TMP_Text>();
        sortLabel.enableAutoSizing = true; sortLabel.fontSizeMin = 8; sortLabel.fontSizeMax = 11;
        foreach (CraftArmorSlot slot in Enum.GetValues(typeof(CraftArmorSlot)))
        {
            CraftArmorSlot value = slot;
            Button sub = NativeButton(root, g, slot.ToString(), 0, 80, 30, () => CraftFilter.SelectSlot(value));
            armorChips.Add((slot, sub, sub.GetComponentInChildren<TMP_Text>()));
            sub.gameObject.SetActive(false);
        }
        shortcut = Ugui.Text(root, "Shortcuts", "", font.font, 11, font.color, TextAlignmentOptions.MidlineRight).At(0, 84, width, 16);
        shortcut.enableAutoSizing = true; shortcut.fontSizeMin = 8; shortcut.fontSizeMax = 11;
        shortcut.overflowMode = TextOverflowModes.Ellipsis;
        if (tooltip)
        {
            shortcut.raycastTarget = true;
            UITooltip hint = shortcut.gameObject.AddComponent<UITooltip>();
            hint.m_tooltipPrefab = tooltip;
            hint.m_text = "/ focuses recipe search. Your inventory key closes the panel.";
        }
        extras = new VanillaCraftExtras(g);
        CraftFilter.Changed();
    }

    private Button NativeButton(Transform parent, InventoryGui g, string text, float x, float y, float width, Action action)
    {
        Image native = (Image)g.m_craftButton.targetGraphic;
        Button button = Ugui.Button(parent, text, native.sprite, native.sprite, action).At(x, y, width, 22);
        button.transition = Selectable.Transition.ColorTint; button.colors = g.m_craftButton.colors;
        Ugui.Text(button.transform, "Label", text, g.m_recipeDecription.font, 11, g.m_recipeDecription.color,
            TextAlignmentOptions.Midline).Stretch(2, 0, 2, 0);
        ownedButtons.Add(button);
        return button;
    }
}
