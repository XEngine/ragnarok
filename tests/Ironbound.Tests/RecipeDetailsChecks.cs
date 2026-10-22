using Ironbound.UI;

internal static class RecipeDetailsChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string text = RecipeDetails.Format("Forge-born.\n\n$item_weight: <color=orange>2</color>\n" +
            "$inventory_slash: <color=orange>38</color> <color=yellow>(12-25)</color>\n" +
            "$inventory_fire: 10\n$item_blockarmor: 20\n$item_staminause: 8\n\n" +
            "<color=orange>$mod_effect</color>\nUnfamiliar effect remains here.\n$inventory_fire: 7", "Forge-born.");
        check(text.StartsWith("<i>Forge-born.</i>"), "Flavor should be italic and appear once");
        check(text.Split("Forge-born.").Length == 2, "Duplicate recipe flavor");
        check(text.IndexOf("PHYSICAL DAMAGE") < text.IndexOf("$inventory_slash"), "Physical damage not grouped");
        check(text.IndexOf("ELEMENTAL DAMAGE") < text.IndexOf("$inventory_fire: 10"), "Elemental damage not grouped");
        check(text.Contains("(12-25)"), "Native skill damage range lost");
        check(text.Contains("BLOCKING & PROTECTION") && text.Contains("ATTACK & RESOURCE USE"), "Combat sections missing");
        check(text.IndexOf("EFFECTS & SPECIAL PROPERTIES") < text.IndexOf("$mod_effect"), "Unknown effect lost or misplaced");
        check(text.IndexOf("$mod_effect") < text.IndexOf("Unfamiliar effect") &&
            text.IndexOf("Unfamiliar effect") < text.IndexOf("$inventory_fire: 7"), "Effect continuation split apart");
        check(!text.Contains("<color=orange>") && text.Contains("<color=#ECE5D6>38"), "Stat palette not applied");
        string food = RecipeDetails.Format("$item_food_health: 40\n$item_food_duration: 1200\n$item_food_regen: 2", "");
        check(food.Contains("NOURISHMENT") && !food.Contains("PHYSICAL DAMAGE"), "Food sections incorrect");
        check(RecipeDetails.Format("", "") == "", "Empty recipe should have no headings");
        Console.WriteLine("PASS: recipe detail grouping, native ranges, effect continuations, palette and food sections.");
    }
}
