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
    private const float FieldHeight = 28, ChipSize = 22, Spacing = 4;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Func<bool> ragnarokPanel;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly List<(CraftCategory? Category, Image Chip, Image Icon)> chips = new();
    private float chipStep;
    private InventoryGui? gui;
    private RectTransform? root;
    private TMP_InputField? search;

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
            Dispose();
            return;
        }
        if (!InventoryGui.IsVisible() || !gui!.m_crafting.gameObject.activeInHierarchy) return;
        if (!root) Build(gui);
        if (search && !search!.isFocused && search.text != CraftFilter.Query) search.SetTextWithoutNotify(CraftFilter.Query);
        if (CraftFilter.ArmorSlot != CraftArmorSlot.All) CraftFilter.SelectSlot(CraftArmorSlot.All);
        // Only the kinds this station makes (from every recipe you know here) get a chip, side by side;
        // "craftable only" keeps its place at the end.
        float chipX = 0;
        foreach (var (category, chip, _) in chips)
        {
            if (category is not { } kind) continue;
            bool show = CraftFilter.Present.Contains(kind);
            if (chip.gameObject.activeSelf != show) chip.gameObject.SetActive(show);
            if (!show) continue;
            RectTransform rect = chip.rectTransform;
            if (rect.anchoredPosition.x != chipX) rect.anchoredPosition = new Vector2(chipX, rect.anchoredPosition.y);
            chipX += chipStep;
        }
        foreach (var (category, chip, icon) in chips)
        {
            bool on = category is { } c ? CraftFilter.Categories.Contains(c) : CraftFilter.CraftableOnly;
            chip.color = on ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            icon.color = on ? Color.white : new Color(1, 1, 1, 0.35f);
        }
    }

    public void Dispose()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        chips.Clear();
        if (search) UiState.TypingFields.Remove(search!);
        search = null;
    }

    private void Build(InventoryGui g)
    {
        RectTransform? list = g.m_crafting.Find("RecipeList") as RectTransform;
        if (!list) return;
        float rows = FieldHeight + Spacing + ChipSize + Spacing * 2;

        // Our block takes the recipe list's top; the list moves down and shortens by as much.
        Vector2 position = list!.anchoredPosition, size = list.sizeDelta;
        list.anchoredPosition = position - new Vector2(0, rows);
        list.sizeDelta = size - new Vector2(0, rows);
        RectTransform moved = list;
        ScrollRect? valheimScroll = list.GetComponentInChildren<ScrollRect>(true);
        if (valheimScroll)
        {
            float sensitivity = valheimScroll!.scrollSensitivity;
            WheelStep step = valheimScroll.gameObject.AddComponent<WheelStep>();
            undo.Add((valheimScroll, () =>
            {
                UnityEngine.Object.Destroy(step);
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
        input.onValueChanged.AddListener(CraftFilter.SetQuery);
        UiState.TypingFields.Add(input);

        // Chips: Valheim's button art, Ragnarök's glyphs, a tooltip naming each.
        Image button = (Image)g.m_craftButton.targetGraphic;
        GameObject? tooltip = g.m_craftButton.GetComponent<UITooltip>()?.m_tooltipPrefab;
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
        float chipSize = Mathf.Min(ChipSize, (width - (kinds.Length - 1) * 3) / kinds.Length);
        float gap = 3;
        chipStep = chipSize + gap;
        for (int i = 0; i < kinds.Length; i++)
        {
            var (category, glyph, name) = kinds[i];
            Image chip = Ugui.Image(root, name, button.sprite, Color.white, raycast: true)
                .At(i * chipStep, FieldHeight + Spacing, chipSize, ChipSize);
            chip.type = Image.Type.Sliced;
            Image icon = Ugui.Image(chip.transform, "Icon", glyph, Color.white).At(2, (ChipSize - chipSize + 4) / 2, chipSize - 4, chipSize - 4);
            Button b = chip.gameObject.AddComponent<Button>();
            b.targetGraphic = chip;
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
    }
}
