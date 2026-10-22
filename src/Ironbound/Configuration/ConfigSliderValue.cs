using System;
using System.Globalization;

namespace Ironbound.Configuration;

// Keep the boxed runtime type required by ConfigEntry<T>, including integer
// sliders alongside floating point ranges in the same configuration window.
internal static class ConfigSliderValue
{
    internal static object Convert(float value, bool integer, float min, float max)
    {
        double bounded = Math.Max(min, Math.Min(max, value));
        if (integer) return (int)Math.Round(bounded);
        return (float)Math.Max(min, Math.Min(max, Math.Round(bounded, 2)));
    }

    internal static float Step(string key, bool integer)
    {
        if (key.EndsWith("Percent", StringComparison.Ordinal)) return 5;
        if (key.EndsWith("Capacity", StringComparison.Ordinal)) return 10;
        if (key.EndsWith("Range", StringComparison.Ordinal)) return 5;
        if (key.StartsWith("Margin", StringComparison.Ordinal)) return 10;
        if (key.EndsWith("Minutes", StringComparison.Ordinal)) return 1;
        if (key.EndsWith("Opacity", StringComparison.Ordinal) || key.EndsWith("Scale", StringComparison.Ordinal)) return 0.05f;
        return integer ? 1 : 0.1f;
    }

    internal static object Snap(float value, bool integer, float min, float max, float step)
    {
        if (value <= min) return Convert(min, integer, min, max);
        if (value >= max) return Convert(max, integer, min, max);
        return Convert((float)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step), integer, min, max);
    }

    internal static bool TryRead(string text, bool integer, bool percent, float min, float max, out object value)
    {
        value = Convert(min, integer, min, max);
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) &&
            !float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)) return false;
        if (float.IsNaN(parsed) || float.IsInfinity(parsed)) return false;
        value = Convert(percent ? parsed / 100f : parsed, integer, min, max);
        return true;
    }
}
