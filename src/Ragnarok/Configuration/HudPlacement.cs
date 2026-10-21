using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Configuration;

internal enum HudStyle
{
    Vanilla,
    Ragnarok
}

// Edges of Ragnarök's frames: clean square edges, or worn and chipped like old metal.
internal enum FrameEdges
{
    Solid,
    Chipped
}

internal enum InventoryStyle
{
    FollowHud,
    Vanilla,
    Ragnarok
}

// Same order as UnityEngine.TextAnchor, so the two convert directly.
internal enum ScreenAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

// Shade: a feathered Animus-style field. Plate: a chamfered obsidian plate with a gold rim.
// Ragnarök look or Valheim's, for parts that are restyled rather than replaced.
// Where the vanilla HUD's provision row sits.
internal enum ProvisionPlacement
{
    BesidePower,
    AfterHotbar
}

internal enum PanelStyle
{
    FollowHud,
    Vanilla,
    Ragnarok
}

internal enum HudBackground
{
    None,
    Shade,
    Plate
}

internal sealed class HudPlacement
{
    internal readonly ConfigEntry<ScreenAnchor> Position;
    internal readonly ConfigEntry<float> MarginX;
    internal readonly ConfigEntry<float> MarginY;
    internal readonly ConfigEntry<HudBackground> Background;
    internal readonly ConfigEntry<float> BackgroundOpacity;

    internal HudPlacement(ConfigFile config, string section, string what, ScreenAnchor position, float marginX = 40f, float marginY = 40f)
    {
        Position = config.Bind(section, "Position", position, $"Screen position of the {what}.");
        MarginX = config.Bind(section, "MarginX", marginX, new ConfigDescription(
            $"Horizontal distance of the {what} from the chosen screen edge, in pixels at 1080p. " +
            "When centred horizontally, positive values move it right.", new AcceptableValueRange<float>(-500f, 500f)));
        MarginY = config.Bind(section, "MarginY", marginY, new ConfigDescription(
            $"Vertical distance of the {what} from the chosen screen edge, in pixels at 1080p. " +
            "When centred vertically, positive values move it up.", new AcceptableValueRange<float>(-500f, 500f)));
        Background = config.Bind(section, "Background", HudBackground.None,
            "Legacy backdrop setting, retained for compatibility. HUD backdrops have been removed.");
        BackgroundOpacity = config.Bind(section, "BackgroundOpacity", 0.5f, new ConfigDescription(
            "Legacy backdrop opacity; ignored.", new AcceptableValueRange<float>(0f, 1f)));
    }

    // Anchors, pivots and offsets a HUD element so margins always point inward from the chosen edge.
    internal void Apply(RectTransform rect)
    {
        int index = (int)Position.Value;
        Vector2 point = new((index % 3) * 0.5f, 1f - (index / 3) * 0.5f);
        if (rect.pivot != point) rect.anchorMin = rect.anchorMax = rect.pivot = point;
        Vector2 offset = new(point.x == 1f ? -MarginX.Value : MarginX.Value, point.y == 1f ? -MarginY.Value : MarginY.Value);
        if (rect.anchoredPosition != offset) rect.anchoredPosition = offset;
    }
}
