using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ironbound.UI;

// Group Valheim's untranslated tooltip lines before localization. Values, skill ranges and
// effect descriptions stay native; unfamiliar/modded lines are retained rather than discarded.
internal static class RecipeDetails
{
    private static readonly Regex Tags = new("<[^>]*>");
    private static readonly Regex Key = new(@"^\$[a-zA-Z0-9_]+");
    private static readonly string[] Titles =
    {
        "PHYSICAL DAMAGE", "ELEMENTAL DAMAGE", "BLOCKING & PROTECTION", "ATTACK & RESOURCE USE",
        "NOURISHMENT", "EQUIPMENT", "EFFECTS & SPECIAL PROPERTIES"
    };

    internal static string Format(string tooltip, string flavor)
    {
        string remaining = tooltip.StartsWith(flavor, StringComparison.Ordinal) ? tooltip.Substring(flavor.Length) : tooltip;
        var groups = new List<string>[Titles.Length];
        for (int i = 0; i < groups.Length; i++) groups[i] = new List<string>();
        // Native special-effect blocks follow the basic properties after a blank line.
        // Keep their continuation lines together, including modded/free-form descriptions.
        int previous = 5;
        bool blank = false, effects = false, seen = false;
        foreach (string raw in remaining.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) { blank = seen; continue; }
            string key = Key.Match(Tags.Replace(line, "")).Value;
            if (blank && seen) effects = true;
            int group = effects ? 6 : Group(key);
            if (key.Length == 0 && seen && !blank) group = previous;
            if (blank && groups[group].Count > 0) groups[group].Add("");
            groups[group].Add(line);
            previous = group;
            seen = true;
            blank = false;
        }
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(flavor)) text.Append("<i>").Append(flavor.Trim()).Append("</i>");
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].Count == 0) continue;
            if (text.Length > 0) text.Append("\n\n");
            text.Append("<size=10><b><cspace=0.8><color=")
                .Append(i == 1 ? "#8FC1E8" : "#A99F8D").Append('>')
                .Append(Titles[i]).Append("</color></cspace></b></size>\n");
            text.Append(string.Join("\n", groups[i]));
        }
        return text.ToString().Replace("<color=orange>", "<color=#ECE5D6>")
            .Replace("<color=yellow>", "<color=#A99F8D>")
            .Replace("<color=#ff8080ff>", "<color=#F07B70>")
            .Replace("<color=#ffff80ff>", "<color=#E8C04A>")
            .Replace("<color=#9090ffff>", "<color=#9A7BEA>");
    }

    private static int Group(string key) => key switch
    {
        "$inventory_damage" or "$inventory_blunt" or "$inventory_slash" or "$inventory_pierce" or
            "$inventory_chop" or "$inventory_pickaxe" => 0,
        "$inventory_fire" or "$inventory_frost" or "$inventory_lightning" or "$inventory_poison" or "$inventory_spirit" => 1,
        "$item_armor" or "$item_blockarmor" or "$item_blockforce" or "$item_parrybonus" or "$item_parryadrenaline" => 2,
        "$item_knockback" or "$item_backstab" or "$item_staminause" or "$item_staminahold" or "$item_eitruse" or
            "$item_healthuse" or "$item_healthhitreturn" or "$item_eitrhitreturn" or
            "$item_damagemultipliertotal" or "$item_damagemultiplierhp" => 3,
        "$item_food_health" or "$item_food_stamina" or "$item_food_eitr" or "$item_food_duration" or "$item_food_regen" => 4,
        "$item_weight" or "$item_durability" or "$item_quality" or "$item_repairlevel" or "$item_value" or
            "$item_onehanded" or "$item_twohanded" or "$item_crafter" or "$item_noteleport" => 5,
        _ => 6
    };
}
