using System;
using System.Collections.Generic;

namespace Ironbound.Features.QualityOfLife;

internal enum CraftCategory { Weapons, Armor, Ammo, Tools, Health, Stamina, Eitr, Materials, Other, Shields, Meads }
internal enum CraftArmorSlot { All, Head, Chest, Legs, Cape, Utility, Other }

// Pure search and category rules shared with the offline checks.
internal static class CraftSearch
{
    internal static string[] Terms(string query) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    internal static bool Matches(IReadOnlyList<string> terms, string name, IReadOnlyList<string> ingredients)
    {
        foreach (string term in terms)
        {
            if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) continue;
            bool found = false;
            foreach (string ingredient in ingredients)
                if (ingredient.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    internal static CraftCategory Food(float health, float stamina, float eitr) =>
        eitr >= health && eitr >= stamina ? CraftCategory.Eitr : stamina > health ? CraftCategory.Stamina : CraftCategory.Health;

    internal static bool Mead(float health, float stamina, float eitr, bool consumeEffect) =>
        consumeEffect && health <= 0 && stamina <= 0 && eitr <= 0;
}
