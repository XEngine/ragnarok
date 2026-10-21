using System;
using System.Linq;
using BepInEx.Configuration;

namespace Ragnarok.Configuration;

// Personal preferences stay on disk; the effective radius always obeys the current host.
internal sealed class RangeLimit
{
    private readonly ConfigEntry<float> maximum;

    internal RangeLimit(ConfigEntry<float> maximum) => this.maximum = maximum;

    internal float Maximum => maximum.Value;

    internal static RangeLimit? For(ConfigEntryBase entry) =>
        entry.Description.Tags.OfType<RangeLimit>().FirstOrDefault();

    internal static float Effective(ConfigEntry<float> preference, ConfigEntry<float> maximum) =>
        Math.Min(float.IsNaN(preference.Value) ? (float)preference.DefaultValue : preference.Value,
            float.IsNaN(maximum.Value) ? (float)maximum.DefaultValue : maximum.Value);

    internal static float Display(ConfigEntryBase entry) =>
        Math.Min(Convert.ToSingle(entry.BoxedValue), For(entry)?.Maximum ?? float.MaxValue);
}
