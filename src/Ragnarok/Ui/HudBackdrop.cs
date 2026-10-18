using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// Optional backdrop behind a HUD part. It stretches past the part's bounds by `padding`
// and is ignored by layout groups, so it follows the content size without affecting it.
internal sealed class HudBackdrop
{
    internal const string Name = "Backdrop";

    private readonly Theme theme;
    private readonly Image image;
    private HudBackground style = (HudBackground)(-1);
    private float opacity = -1;

    internal HudBackdrop(Theme theme, RectTransform parent, Vector4 padding)
    {
        this.theme = theme;
        image = Ugui.Image(parent, Name, null, Color.clear).Stretch(-padding.x, -padding.y, -padding.z, -padding.w);
        image.transform.SetAsFirstSibling();
        image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    internal void Apply(HudPlacement placement) => Apply(placement.Background, placement.BackgroundOpacity);

    internal void Apply(BepInEx.Configuration.ConfigEntry<HudBackground> background, BepInEx.Configuration.ConfigEntry<float> backgroundOpacity)
    {
        HudBackground nextStyle = background.Value;
        float nextOpacity = backgroundOpacity.Value;
        if (nextStyle == style && Mathf.Approximately(nextOpacity, opacity)) return;
        style = nextStyle;
        opacity = nextOpacity;

        image.enabled = style != HudBackground.None && opacity > 0.001f;
        image.sprite = style == HudBackground.Plate ? theme.Plate : theme.Shade;
        image.type = Image.Type.Sliced;
        if (Theme.Current != null && Theme.Current.IsChamfer(image.sprite) && !image.GetComponent<ChipSkin>())
            image.gameObject.AddComponent<ChipSkin>();
        // The shade carries its own darkness in the texture; the plate is tinted by opacity alone.
        image.color = Color.white.WithAlpha(opacity);
    }
}
