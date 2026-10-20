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
