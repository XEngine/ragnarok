using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI;

// Pieces of the style guide shared by the inventory, character and chest panels.
internal static class Ornaments
{
    // A section label: Alegreya italic, lowercase, muted ("nearby chests", "worn").
    internal static TextMeshProUGUI Section(Transform parent, Theme theme, string text, float x, float y, float w) =>
        Ugui.Text(parent, text, text.ToLowerInvariant(), theme.Section, 16, Palette.Muted).At(x, y - 4, w, 24);

    // A divider from the style guide: a gold hairline with a small diamond at its middle.
    internal static void Rule(Transform parent, Theme theme, float x, float y, float w)
    {
        Ugui.Image(parent, "Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(x, y, w, 1);
        Ugui.Image(parent, "Rule Gem", theme.Diamond, Palette.Gold).At(x + w / 2 - 4, y - 3.5f, 8, 8);
    }

    // A page title: Alegreya, gold (32 for a page, 24 for a window).
    internal static TextMeshProUGUI Title(Transform parent, Theme theme, string text, float size) =>
        Ugui.Text(parent, "Title", text, theme.Heading, size, Palette.Gold);

    // A button drawn as a key cap: dark, bone-edged, its label in Scada bold.
    internal static Button Keycap(Transform parent, Theme theme, string label, float x, float y, float w, float h, Action onClick,
        float size = 13)
    {
        Button button = Ugui.Button(parent, label, theme.Button, theme.ButtonHover, onClick);
        button.At(x, y, w, h);
        TextMeshProUGUI text = Ugui.Text(button.transform, "Label", label, theme.Strong, size, Palette.Bone, TextAlignmentOptions.Center)
            .Stretch(4, 0, 4, 0);
        button.OnHover(on => text.color = on ? Palette.GoldBright : Palette.Bone);
        return button;
    }
}

// The inventory stats line from the style guide: armour and weight on a gold baseline that ends in a cap
// at each side ("◢ 🛡 48 • ⚖ 150/300 ◣"), right-aligned to a point. The weight turns red when over.
internal sealed class StatLine
{
    private const float Glyph = 14, CapWidth = 18;

    private readonly RectTransform root;
    private readonly bool hang;
    private readonly float? left;
    private readonly Image? shieldIcon, dot;
    private readonly TextMeshProUGUI? armor;
    private readonly Image weightIcon, line, leftCap;
    private readonly TextMeshProUGUI weight;
    private string shown = "";

    // `right`/`baseline`: where the line ends and sits, from the parent's top-left.
    // `hang`: the tab hangs below the line (under a frame's foot) instead of standing on it. `left`: the
    // line starts there instead of ending at `right`.
    internal StatLine(Theme theme, Transform parent, float right, float baseline, bool withArmor, bool hang = false, float? left = null)
    {
        this.hang = hang;
        this.left = left;
        root = Ugui.Node(parent, "Stat Line");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        root.anchoredPosition = new Vector2(right, -baseline);
        root.sizeDelta = Vector2.zero;
        line = Ugui.Image(root, "Baseline", theme.White, Palette.Gold.WithAlpha(0.8f));
        leftCap = Ugui.Image(root, "Cap Left", theme.Cap, Palette.Gold);
        leftCap.rectTransform.localScale = new Vector3(-1, hang ? -1 : 1, 1);
        Ugui.Image(root, "Cap Right", theme.Cap, Palette.Gold).At(-CapWidth, CapY, CapWidth, CapWidth)
            .rectTransform.localScale = new Vector3(1, hang ? -1 : 1, 1);
        if (withArmor)
        {
            shieldIcon = Ugui.Image(root, "Armor Icon", theme.GlyphShield, Palette.Bone);
            armor = Ugui.Text(root, "Armor", "", theme.Strong, 16, Palette.Bone);
            armor.overflowMode = TextOverflowModes.Overflow;
            dot = Ugui.Image(root, "Dot", theme.Diamond, Palette.Gold);
        }
        weightIcon = Ugui.Image(root, "Weight Icon", theme.GlyphWeight, Palette.Bone);
        weight = Ugui.Text(root, "Weight", "", theme.Strong, 16, Palette.Bone);
        weight.overflowMode = TextOverflowModes.Overflow;
    }

    internal void Set(int? armorValue, float total, float max)
    {
        bool over = max > 0 && total > max;
        string value = Mathf.CeilToInt(total) + (max > 0 ? " / " + Mathf.CeilToInt(max) : "");
        Color tone = over ? Palette.Alert : Palette.Bone;
        weight.color = weightIcon.color = tone;
        string key = armorValue + "|" + value;
        if (key == shown) return;
        shown = key;
        weight.text = value;
        if (armor != null) armor.text = armorValue?.ToString() ?? "";

        // Laid out right to left from the right cap.
        float x = -CapWidth - 6;
        x = Place(weight, x);
        x = Place(weightIcon, x - 5, Glyph);
        if (armor != null)
        {
            x = Place(dot!, x - 9, 5) - 9;
            x = Place(armor, x);
            x = Place(shieldIcon!, x - 5, Glyph);
        }
        x -= 6;
        leftCap.At(x, CapY, CapWidth, CapWidth);
        line.At(x - CapWidth, 0, -x + CapWidth, 1);
        if (left is { } start) root.anchoredPosition = new Vector2(start + CapWidth - x, root.anchoredPosition.y);
    }

    // Caps stand on the line, or (flipped) hang from it.
    private float CapY => hang ? CapWidth - 1 : -CapWidth + 1;

    // The middle of the tab's text, above or below the line.
    private float Middle => hang ? 12 : -12;

    private float Place(TextMeshProUGUI text, float right)
    {
        float w = Mathf.Ceil(text.GetPreferredValues(text.text).x) + 1;
        text.At(right - w, Middle - 10, w, 20);
        return right - w;
    }

    private float Place(Image image, float right, float size)
    {
        image.At(right - size, Middle - size / 2, size, size);
        return right - size;
    }
}
