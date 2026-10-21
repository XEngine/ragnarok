using System;
using System.Collections.Generic;

namespace Ragnarok.HUD;

internal enum StatusCategory { Passive, Ongoing, Debuff }

internal static class StatusCategories
{
    // Internal effect names, never translated display text. Unknown mod buffs
    // remain ongoing unless their implementation identifies them as harmful.
    private static readonly HashSet<string> Harmful = new(StringComparer.OrdinalIgnoreCase)
    {
        "Poison", "Burning", "Smoked", "Smoke", "Wet", "Cold", "Freezing",
        "Frost", "Lightning", "Spirit", "Tared", "Encumbered", "Puke"
    };

    internal static StatusCategory Classify(string name, bool equipment, bool damaging)
    {
        const string clone = "(Clone)";
        if (name.EndsWith(clone, StringComparison.Ordinal)) name = name.Substring(0, name.Length - clone.Length).TrimEnd();
        if (damaging || Harmful.Contains(name)) return StatusCategory.Debuff;
        return equipment ? StatusCategory.Passive : StatusCategory.Ongoing;
    }
}
