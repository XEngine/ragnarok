using System;

namespace Ironbound.UI;

// Scanline bounds of one rounded panel with a left repair wing. Coordinates start
// at the body's top-left; the wing occupies negative X. Insets form its border.
internal static class CraftFrameShape
{
    internal static bool Bounds(float width, float height, float wingWidth, float wingTop, float wingHeight,
        bool wing, float y, float inset, out float left, out float right)
    {
        left = right = 0;
        if (width <= inset * 2 || height <= inset * 2 || y < inset || y > height - inset) return false;
        float radius = Math.Max(0, Math.Min(12, Math.Min(width, height) / 2) - inset);
        float corner = Corner(y - inset, height - inset * 2, radius);
        left = inset + corner;
        right = width - inset - corner;
        if (wing && wingWidth > inset && wingHeight > inset * 2 && y >= wingTop + inset && y <= wingTop + wingHeight - inset)
        {
            float tabRadius = Math.Max(0, Math.Min(8, wingHeight / 2) - inset);
            float tabLeft = -wingWidth + inset + Corner(y - wingTop - inset, wingHeight - inset * 2, tabRadius);
            left = Math.Min(left, tabLeft);
        }
        return right > left;
    }

    private static float Corner(float y, float height, float radius)
    {
        float edge = Math.Min(y, height - y);
        if (edge >= radius || radius <= 0) return 0;
        float d = radius - Math.Max(0, edge);
        return radius - (float)Math.Sqrt(Math.Max(0, radius * radius - d * d));
    }
}
