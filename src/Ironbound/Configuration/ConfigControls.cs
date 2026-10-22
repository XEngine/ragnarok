using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.Configuration;

// Builds a control for a config entry. Each control registers a refresher so it
// follows changes made elsewhere (other controls, F7 reload, restore defaults).
internal sealed class ConfigControls
{
    private static readonly Dictionary<string, string> Units = new()
    {
        ["NearbyChests.Range"] = " m",
        ["HUD.Vitals.MarginX"] = " px", ["HUD.Vitals.MarginY"] = " px",
        ["HUD.Status.MarginX"] = " px", ["HUD.Status.MarginY"] = " px",
        ["HUD.Keys.MarginX"] = " px", ["HUD.Keys.MarginY"] = " px"
    };
    private static readonly Dictionary<string, string> EnumNames = new() { ["Ironbound"] = "Ironbound" };

    private readonly Theme theme;
    private readonly ConfigWindow window;

    internal readonly List<Action> Refreshers = new();
    private readonly List<TMP_InputField> fields = new();

    internal void Clear()
    {
        Refreshers.Clear();
        foreach (TMP_InputField field in fields)
        {
            UiState.TypingFields.Remove(field);
            if (!field) continue;
            field.onEndEdit.RemoveAllListeners();
            field.onValueChanged.RemoveAllListeners();
        }
        fields.Clear();
    }

    internal ConfigControls(Theme theme, ConfigWindow window)
    {
        this.theme = theme;
        this.window = window;
    }

    internal void Refresh()
    {
        foreach (Action refresh in Refreshers) refresh();
    }

    internal void Add(ConfigEntryBase entry, Transform parent, Rect r)
    {
        switch (entry.BoxedValue)
        {
            case bool: Switch(entry, parent, r); break;
            case ScreenAnchor: AnchorGrid(entry, parent, r); break;
            case Enum: Segments(entry, parent, r); break;
            case float or int when entry.Description?.AcceptableValues is AcceptableValueBase range: Slider(entry, range, parent, r); break;
            case KeyboardShortcut: KeyBinding(entry, parent, r); break;
            case string: TextField(entry, parent, r); break;
            default:
                TextMeshProUGUI value = Ugui.Text(parent, "Value", "", theme.Strong, 13, Palette.GoldBright, TextAlignmentOptions.MidlineRight).At(r);
                Refreshers.Add(() => value.text = entry.BoxedValue?.ToString() ?? "");
                break;
        }
    }

    private void Set(ConfigEntryBase entry, object value)
    {
        if (window.CanEdit(entry)) entry.BoxedValue = value;
    }

    internal static string Pretty(string key) =>
        System.Text.RegularExpressions.Regex.Replace(key, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

    private void Switch(ConfigEntryBase entry, Transform parent, Rect r)
    {
        TextMeshProUGUI state = Ugui.Text(parent, "State", "", theme.Strong, 13, Palette.GoldBright, TextAlignmentOptions.MidlineRight)
            .At(r.x, r.y, r.width - 68, r.height);
        Rect trackRect = new(r.xMax - 56, r.y + (r.height - 24) / 2, 56, 24);
        Image track = Ugui.Image(parent, "Switch", theme.HudKeySurface, Color.white, raycast: true).At(trackRect);
        Button button = track.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        Image outline = Ugui.Image(track.transform, "Iron edge", theme.HudKeyEdge, Palette.Iron).Stretch();
        Image line = Ugui.Image(track.transform, "Line", theme.White, Palette.GoldBright).At(4, 11, 30, 2);
        Image knob = Ugui.Image(track.transform, "Knob", theme.HudKeySurface, Color.white).At(4, 3, 18, 18);
        bool hover = false;

        void Refresh()
        {
            bool on = (bool)entry.BoxedValue;
            state.text = on ? UiLocalization.Text("config.on_caps", "ON") : UiLocalization.Text("config.off_caps", "OFF");
            state.color = on ? Palette.GoldBright : Palette.Faint;
            track.color = on ? Palette.SwitchOn : Palette.Ink;
            Color edge = on ? (hover ? Palette.GoldBright : Palette.Gold) : (hover ? Palette.Muted : Palette.Faint);
            outline.color = edge;
            line.enabled = on;
            knob.At(on ? 34 : 4, 3, 18, 18);
            knob.color = on ? Palette.GoldBright : Palette.Faint;
        }

        button.onClick.AddListener(() => Set(entry, !(bool)entry.BoxedValue));
        track.OnHover(h => { hover = h; Refresh(); });
        Refreshers.Add(Refresh);
    }

    // A miniature screen with nine positions; the selected one is lit like a waypoint.
    private void AnchorGrid(ConfigEntryBase entry, Transform parent, Rect r)
    {
        const float cellW = 30, cellH = 18, gap = 3;
        float gridW = cellW * 3 + gap * 2, gridH = cellH * 3 + gap * 2;
        Rect grid = new(r.xMax - gridW, r.center.y - gridH / 2, gridW, gridH);
        TextMeshProUGUI name = Ugui.Text(parent, "Position", "", theme.Strong, 12, Palette.GoldBright, TextAlignmentOptions.MidlineRight)
            .At(r.x, r.y, r.width - gridW - 16, r.height);
        foreach (Image line in Ugui.Outline(parent, new Rect(grid.x - 4, grid.y - 4, grid.width + 8, grid.height + 8)))
            line.color = Palette.Rule;

        List<(Button Button, Image Marker)> cells = new();
        for (int i = 0; i < 9; i++)
        {
            ScreenAnchor anchor = (ScreenAnchor)i;
            Button cell = Ugui.Button(parent, "Cell " + anchor, theme.ConfigButton, theme.ConfigButtonHover, () => Set(entry, anchor))
                .At(grid.x + (i % 3) * (cellW + gap), grid.y + (i / 3) * (cellH + gap), cellW, cellH);
            Image marker = Ugui.Image(cell.transform, "Marker", theme.Diamond, Palette.GoldBright).At(cellW / 2 - 4, cellH / 2 - 4, 8, 8);
            cells.Add((cell, marker));
        }

        Refreshers.Add(() =>
        {
            int selected = (int)(ScreenAnchor)entry.BoxedValue;
            name.text = UiLocalization.Text("enum." + (ScreenAnchor)selected, Pretty(((ScreenAnchor)selected).ToString()));
            for (int i = 0; i < cells.Count; i++)
            {
                bool on = i == selected;
                Ugui.SpriteSwap(cells[i].Button, on ? theme.ConfigSelected : theme.ConfigButton, on ? theme.ConfigSelectedHover : theme.ConfigButtonHover);
                cells[i].Marker.enabled = on;
            }
        });
    }

    private void Segments(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Type type = entry.SettingType;
        string[] names = Enum.GetNames(type);
        if (names.Length > 6) { Cycle(entry, parent, r, names); return; }
        float width = r.width / names.Length;
        List<(string Name, Button Button, TextMeshProUGUI Label)> segments = new();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            float gap = i > 0 ? 3 : 0;
            Button button = Ugui.Button(parent, "Segment " + name, theme.ConfigButton, theme.ConfigButtonHover,
                () => Set(entry, Enum.Parse(type, name))).At(r.x + i * width + gap, r.y, width - gap, r.height);
            string text = UiLocalization.Text("enum." + name, EnumNames.TryGetValue(name, out string pretty) ? pretty : Pretty(name));
            TextMeshProUGUI label = Ugui.Text(button.transform, "Label", text, theme.Strong, 11, Palette.Muted,
                TextAlignmentOptions.Center).Stretch();
            segments.Add((name, button, label));
        }

        Refreshers.Add(() =>
        {
            string current = entry.BoxedValue.ToString();
            foreach ((string name, Button button, TextMeshProUGUI label) in segments)
            {
                bool selected = name == current;
                Ugui.SpriteSwap(button, selected ? theme.ConfigSelected : theme.ConfigButton, selected ? theme.ConfigSelectedHover : theme.ConfigButtonHover);
                label.color = selected ? Palette.GoldBright : Palette.Muted;
            }
        });
    }

    // Large enums (controller buttons) remain readable instead of becoming tiny segments.
    private void Cycle(ConfigEntryBase entry, Transform parent, Rect r, string[] names)
    {
        void Step(int direction)
        {
            int index = Array.IndexOf(names, entry.BoxedValue.ToString());
            Set(entry, Enum.Parse(entry.SettingType, names[(index + direction + names.Length) % names.Length]));
        }
        Button previous = Ugui.Button(parent, "Previous", theme.ConfigButton, theme.ConfigButtonHover, () => Step(-1)).At(r.x, r.y, 32, r.height);
        Ugui.Text(previous.transform, "Label", "‹", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        TextMeshProUGUI label = Ugui.Text(parent, "Selection", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center)
            .At(r.x + 36, r.y, r.width - 72, r.height);
        Button next = Ugui.Button(parent, "Next", theme.ConfigButton, theme.ConfigButtonHover, () => Step(1)).At(r.xMax - 32, r.y, 32, r.height);
        Ugui.Text(next.transform, "Label", "›", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Refreshers.Add(() => label.text = UiLocalization.Text("enum." + entry.BoxedValue, Pretty(entry.BoxedValue.ToString())));
    }

    private void Slider(ConfigEntryBase entry, AcceptableValueBase range, Transform parent, Rect r)
    {
        float min = Convert.ToSingle(range.GetType().GetProperty("MinValue")!.GetValue(range, null));
        float max = Convert.ToSingle(range.GetType().GetProperty("MaxValue")!.GetValue(range, null));
        float Maximum() => Math.Min(max, RangeLimit.For(entry)?.Maximum ?? max);
        bool isInt = entry.SettingType == typeof(int);
        bool whole = isInt;
        bool percent = entry.Definition.Key.EndsWith("Opacity", StringComparison.Ordinal);
        string key = entry.Definition.Key;
        string unit = Units.TryGetValue(entry.Definition.Section + "." + key, out string u) ? u
            : key.EndsWith("Percent", StringComparison.Ordinal) ? "%"
            : key.EndsWith("Minutes", StringComparison.Ordinal) ? " min"
            : key.EndsWith("Range", StringComparison.Ordinal) ? " m"
            : key.StartsWith("Margin", StringComparison.Ordinal) ? " px"
            : key.EndsWith("Multiplier", StringComparison.Ordinal) || key.EndsWith("Scale", StringComparison.Ordinal) ? "\u00D7" : "";

        float trackWidth = r.width - 84;
        RectTransform root = Ugui.Node(parent, "Slider").At(r.x, r.y + (r.height - 18) / 2, trackWidth, 18);
        Ugui.Image(root, "Track", theme.White, Palette.Gold.WithAlpha(0.45f)).At(0, 8, trackWidth, 2);
        Ugui.Image(root, "Min Tick", theme.White, Palette.Faint).At(0, 4, 1, 10);
        Ugui.Image(root, "Max Tick", theme.White, Palette.Faint).At(trackWidth - 1, 4, 1, 10);
        RectTransform fillArea = Ugui.Node(root, "Fill Area").At(0, 8, trackWidth, 2);
        Image fill = Ugui.Image(fillArea, "Fill", theme.White, Palette.Gold).Stretch();
        RectTransform handleArea = Ugui.Node(root, "Handle Area").Stretch(9, 0, 9, 0);
        Image handle = Ugui.Image(handleArea, "Handle", theme.Diamond, Palette.Gold, raycast: true);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0, 0);
        handleRect.anchorMax = new Vector2(0, 1);
        handleRect.sizeDelta = new Vector2(18, 0);
        // A wide transparent hit area so the slider is easy to grab anywhere on the track.
        Image hit = Ugui.Image(root, "Hit", null, Color.clear, raycast: true).Stretch(0, -8, 0, -8);
        hit.transform.SetAsFirstSibling();

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.ColorTint;
        slider.colors = new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color(1.25f, 1.2f, 1.1f), pressedColor = new Color(1.3f, 1.25f, 1.1f),
            selectedColor = Color.white, disabledColor = Color.gray, colorMultiplier = 1, fadeDuration = 0.08f
        };
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = Maximum();
        slider.wholeNumbers = whole;

        Image box = Ugui.Image(parent, "Value Box", theme.PackSurface, Palette.Hex(0x080B0C), raycast: true)
            .At(r.xMax - 72, r.y, 72, r.height);
        Ugui.Image(box.transform, "Edge", theme.PackEdge, Palette.Iron).Stretch();
        box.gameObject.SetActive(false);
        RectTransform area = Ugui.Node(box.transform, "Text Area").Stretch(5, 1, 5, 11);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text value = Ugui.Text(area, "Value", "", theme.Strong, 13, Palette.GoldBright, TextAlignmentOptions.Midline).Stretch();
        TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area; field.textComponent = value; field.targetGraphic = box;
        field.characterLimit = 24;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.customCaretColor = true; field.caretColor = Palette.GoldBright;
        field.selectionColor = Palette.Gold.WithAlpha(0.35f);
        Ugui.Text(box.transform, "Unit", percent ? "%" : unit.Trim(), theme.Body, 9, Palette.Muted,
            TextAlignmentOptions.Midline).At(2, r.height - 12, 68, 10);
        box.gameObject.SetActive(true);
        fields.Add(field); UiState.TypingFields.Add(field);

        string DisplayValue()
        {
            float current = RangeLimit.Display(entry);
            return (percent ? current * 100f : current).ToString(whole ? "0" : "0.##", CultureInfo.InvariantCulture);
        }
        field.onEndEdit.AddListener(text =>
        {
            if (!field.wasCanceled && window.CanEdit(entry) &&
                ConfigSliderValue.TryRead(text, isInt, percent, min, Maximum(), out object parsed))
                Set(entry, parsed);
            field.SetTextWithoutNotify(DisplayValue());
        });
        float step = ConfigSliderValue.Step(key, isInt);
        bool updating = false;
        slider.onValueChanged.AddListener(v =>
        {
            if (updating || !window.CanEdit(entry)) return;
            object snapped = ConfigSliderValue.Snap(v, isInt, min, Maximum(), step);
            Set(entry, snapped);
            // Snap the handle even when the setting did not change this frame.
            slider.SetValueWithoutNotify(RangeLimit.Display(entry));
        });
        Refreshers.Add(() =>
        {
            float current = RangeLimit.Display(entry);
            updating = true;
            slider.maxValue = Maximum();
            slider.SetValueWithoutNotify(current);
            updating = false;
            if (!field.isFocused) field.SetTextWithoutNotify(DisplayValue());
        });
    }

    private void KeyBinding(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Button button = Ugui.Button(parent, "Key", theme.ConfigButton, theme.ConfigButtonHover,
            () => window.Capturing = window.Capturing == entry ? null : entry).At(r);
        TextMeshProUGUI label = Ugui.Text(button.transform, "Label", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Refreshers.Add(() =>
        {
            bool waiting = window.Capturing == entry;
            Ugui.SpriteSwap(button, waiting ? theme.ConfigSelected : theme.ConfigButton, waiting ? theme.ConfigSelectedHover : theme.ConfigButtonHover);
            label.text = waiting ? UiLocalization.Text("control.key_wait", "PRESS A KEY…") : ((KeyboardShortcut)entry.BoxedValue).ToString();
            label.color = waiting ? Color.white : Palette.Bone;
        });
    }

    private void TextField(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Image background = Ugui.Image(parent, "Field", theme.ConfigField, Color.white, raycast: true).At(r);
        // Assign the text parts before TMP_InputField enables.
        background.gameObject.SetActive(false);
        RectTransform area = Ugui.Node(background.transform, "Text Area").Stretch(10, 6, 10, 6);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "", theme.Body, 13, Palette.Bone).Stretch();
        TMP_InputField field = background.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.targetGraphic = background;
        field.characterLimit = 120;
        field.customCaretColor = true;
        field.caretColor = Palette.GoldBright;
        field.selectionColor = Palette.Gold.WithAlpha(0.35f);
        field.text = (string)entry.BoxedValue;
        Ugui.SpriteSwap(field, theme.ConfigField, theme.ConfigFieldFocus);
        field.spriteState = new SpriteState { highlightedSprite = theme.ConfigFieldFocus, pressedSprite = theme.ConfigFieldFocus, selectedSprite = theme.ConfigFieldFocus };
        background.gameObject.SetActive(true);

        fields.Add(field); UiState.TypingFields.Add(field);
        field.onValueChanged.AddListener(v => Set(entry, v));
        Refreshers.Add(() =>
        {
            if (!field.isFocused) field.SetTextWithoutNotify((string)entry.BoxedValue);
        });
    }
}
