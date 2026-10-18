using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

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
    private static readonly Dictionary<string, string> EnumNames = new() { ["Ragnarok"] = "Ragnarök" };

    private readonly Theme theme;
    private readonly ConfigWindow window;

    internal readonly List<Action> Refreshers = new();

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
                TextMeshProUGUI value = Ugui.Text(parent, "Value", "", theme.Strong, 15, Palette.GoldBright, TextAlignmentOptions.MidlineRight).At(r);
                Refreshers.Add(() => value.text = entry.BoxedValue?.ToString() ?? "");
                break;
        }
    }

    internal static string Pretty(string key) =>
        System.Text.RegularExpressions.Regex.Replace(key, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

    private void Switch(ConfigEntryBase entry, Transform parent, Rect r)
    {
        TextMeshProUGUI state = Ugui.Text(parent, "State", "", theme.Strong, 15, Palette.GoldBright, TextAlignmentOptions.MidlineRight)
            .At(r.x, r.y, r.width - 68, r.height);
        Rect trackRect = new(r.xMax - 56, r.y + (r.height - 24) / 2, 56, 24);
        Image track = Ugui.Image(parent, "Switch", theme.White, Color.white, raycast: true).At(trackRect);
        Button button = track.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        Image[] outline = Ugui.Outline(track.transform, new Rect(0, 0, 56, 24));
        Image line = Ugui.Image(track.transform, "Line", theme.White, Palette.GoldBright).At(4, 11, 30, 2);
        Image knob = Ugui.Image(track.transform, "Knob", theme.Diamond, Color.white).At(4, 3, 18, 18);
        bool hover = false;

        void Refresh()
        {
            bool on = (bool)entry.BoxedValue;
            state.text = on ? "ON" : "OFF";
            state.color = on ? Palette.GoldBright : Palette.Faint;
            track.color = on ? Palette.SwitchOn : Palette.Ink;
            Color edge = on ? (hover ? Palette.GoldBright : Palette.Gold) : (hover ? Palette.Muted : Palette.Faint);
            foreach (Image side in outline) side.color = edge;
            line.enabled = on;
            knob.At(on ? 34 : 4, 3, 18, 18);
            knob.color = on ? Palette.GoldBright : Palette.Faint;
        }

        button.onClick.AddListener(() => entry.BoxedValue = !(bool)entry.BoxedValue);
        track.OnHover(h => { hover = h; Refresh(); });
        Refreshers.Add(Refresh);
    }

    // A miniature screen with nine positions; the selected one is lit like a waypoint.
    private void AnchorGrid(ConfigEntryBase entry, Transform parent, Rect r)
    {
        const float cellW = 30, cellH = 18, gap = 3;
        float gridW = cellW * 3 + gap * 2, gridH = cellH * 3 + gap * 2;
        Rect grid = new(r.xMax - gridW, r.center.y - gridH / 2, gridW, gridH);
        TextMeshProUGUI name = Ugui.Text(parent, "Position", "", theme.Strong, 14, Palette.GoldBright, TextAlignmentOptions.MidlineRight)
            .At(r.x, r.y, r.width - gridW - 16, r.height);
        foreach (Image line in Ugui.Outline(parent, new Rect(grid.x - 4, grid.y - 4, grid.width + 8, grid.height + 8)))
            line.color = Palette.Rule;

        List<(Button Button, Image Marker)> cells = new();
        for (int i = 0; i < 9; i++)
        {
            ScreenAnchor anchor = (ScreenAnchor)i;
            Button cell = Ugui.Button(parent, "Cell " + anchor, theme.Button, theme.ButtonHover, () => entry.BoxedValue = anchor)
                .At(grid.x + (i % 3) * (cellW + gap), grid.y + (i / 3) * (cellH + gap), cellW, cellH);
            Image marker = Ugui.Image(cell.transform, "Marker", theme.Diamond, Palette.GoldBright).At(cellW / 2 - 4, cellH / 2 - 4, 8, 8);
            cells.Add((cell, marker));
        }

        Refreshers.Add(() =>
        {
            int selected = (int)(ScreenAnchor)entry.BoxedValue;
            name.text = Pretty(((ScreenAnchor)selected).ToString());
            for (int i = 0; i < cells.Count; i++)
            {
                bool on = i == selected;
                Ugui.SpriteSwap(cells[i].Button, on ? theme.SegmentOn : theme.Button, on ? theme.SegmentOnHover : theme.ButtonHover);
                cells[i].Marker.enabled = on;
            }
        });
    }

    private void Segments(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Type type = entry.SettingType;
        string[] names = Enum.GetNames(type);
        float width = r.width / names.Length;
        List<(string Name, Button Button, TextMeshProUGUI Label)> segments = new();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            float gap = i > 0 ? 3 : 0;
            Button button = Ugui.Button(parent, "Segment " + name, theme.Button, theme.ButtonHover,
                () => entry.BoxedValue = Enum.Parse(type, name)).At(r.x + i * width + gap, r.y, width - gap, r.height);
            string text = EnumNames.TryGetValue(name, out string pretty) ? pretty : Pretty(name);
            TextMeshProUGUI label = Ugui.Text(button.transform, "Label", text, theme.Strong, 13, Palette.Muted,
                TextAlignmentOptions.Center).Stretch();
            segments.Add((name, button, label));
        }

        Refreshers.Add(() =>
        {
            string current = entry.BoxedValue.ToString();
            foreach ((string name, Button button, TextMeshProUGUI label) in segments)
            {
                bool selected = name == current;
                Ugui.SpriteSwap(button, selected ? theme.SegmentOn : theme.Button, selected ? theme.SegmentOnHover : theme.ButtonHover);
                label.color = selected ? Palette.GoldBright : Palette.Muted;
            }
        });
    }

    private void Slider(ConfigEntryBase entry, AcceptableValueBase range, Transform parent, Rect r)
    {
        float min = Convert.ToSingle(range.GetType().GetProperty("MinValue")!.GetValue(range, null));
        float max = Convert.ToSingle(range.GetType().GetProperty("MaxValue")!.GetValue(range, null));
        bool isInt = entry.SettingType == typeof(int);
        bool whole = isInt || max - min > 10;
        bool percent = entry.Definition.Key.EndsWith("Opacity", StringComparison.Ordinal);
        string unit = Units.TryGetValue(entry.Definition.Section + "." + entry.Definition.Key, out string u) ? u : whole ? "" : "×";

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
        slider.maxValue = max;
        slider.wholeNumbers = whole;

        Image box = Ugui.Image(parent, "Value Box", theme.Card, Color.white).At(r.xMax - 72, r.y, 72, r.height);
        TextMeshProUGUI value = Ugui.Text(box.transform, "Value", "", theme.Strong, 15, Palette.GoldBright, TextAlignmentOptions.Center).Stretch();

        bool updating = false;
        slider.onValueChanged.AddListener(v =>
        {
            if (updating) return;
            float snapped = whole ? Mathf.Round(v) : Mathf.Round(v * 20f) / 20f;
            entry.BoxedValue = isInt ? Mathf.RoundToInt(snapped) : snapped;
        });
        Refreshers.Add(() =>
        {
            float current = Convert.ToSingle(entry.BoxedValue);
            updating = true;
            slider.SetValueWithoutNotify(current);
            updating = false;
            value.text = percent ? (current * 100f).ToString("0", CultureInfo.InvariantCulture) + "%"
                : current.ToString(whole ? "0" : "0.00", CultureInfo.InvariantCulture) + unit;
        });
    }

    private void KeyBinding(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Button button = Ugui.Button(parent, "Key", theme.Button, theme.ButtonHover,
            () => window.Capturing = window.Capturing == entry ? null : entry).At(r);
        TextMeshProUGUI label = Ugui.Text(button.transform, "Label", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        Refreshers.Add(() =>
        {
            bool waiting = window.Capturing == entry;
            Ugui.SpriteSwap(button, waiting ? theme.SegmentOn : theme.Button, waiting ? theme.SegmentOnHover : theme.ButtonHover);
            label.text = waiting ? "PRESS A KEY…" : ((KeyboardShortcut)entry.BoxedValue).ToString();
            label.color = waiting ? Color.white : Palette.Bone;
        });
    }

    private void TextField(ConfigEntryBase entry, Transform parent, Rect r)
    {
        Image background = Ugui.Image(parent, "Field", theme.Field, Color.white, raycast: true).At(r);
        // Assign the text parts before TMP_InputField enables.
        background.gameObject.SetActive(false);
        RectTransform area = Ugui.Node(background.transform, "Text Area").Stretch(10, 6, 10, 6);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = Ugui.Text(area, "Text", "", theme.Body, 15, Palette.Bone).Stretch();
        TMP_InputField field = background.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = text;
        field.targetGraphic = background;
        field.characterLimit = 120;
        field.customCaretColor = true;
        field.caretColor = Palette.GoldBright;
        field.selectionColor = Palette.Gold.WithAlpha(0.35f);
        field.text = (string)entry.BoxedValue;
        Ugui.SpriteSwap(field, theme.Field, theme.FieldFocus);
        field.spriteState = new SpriteState { highlightedSprite = theme.FieldFocus, pressedSprite = theme.FieldFocus, selectedSprite = theme.FieldFocus };
        background.gameObject.SetActive(true);

        field.onValueChanged.AddListener(v => entry.BoxedValue = v);
        Refreshers.Add(() =>
        {
            if (!field.isFocused) field.SetTextWithoutNotify((string)entry.BoxedValue);
        });
    }
}
