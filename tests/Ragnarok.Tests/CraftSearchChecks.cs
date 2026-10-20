using Ragnarok.Features.QualityOfLife;

internal static class CraftSearchChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Match(string query, string name, string[] ingredients, bool expected) =>
            check(CraftSearch.Matches(CraftSearch.Terms(query), name, ingredients) == expected, "Craft search failed: " + query);
        Match("", "Bronze Axe", [], true);
        Match(" \t \n", "Bronze Axe", [], true);
        Match("AXE", "Bronze Axe", [], true);
        Match("bronze", "Cultivator", ["Bronze", "Core wood"], true);
        Match("axe bronze", "Axe", ["Bronze", "Wood"], true);
        Match("wood leather", "Crude Bow", ["Wood", "Leather scraps"], true);
        Match("wood iron", "Crude Bow", ["Wood", "Leather scraps"], false);
        Match("bronze", "Carrot Soup", ["Carrot", "Mushroom"], false);
        Match("mush", "Carrot Soup", ["Carrot", "Mushroom"], true);
        Match("boş", "BOŞ", [], true);
        check(CraftSearch.Food(45, 15, 0) == CraftCategory.Health, "Health food misclassified");
        check(CraftSearch.Food(15, 45, 0) == CraftCategory.Stamina, "Stamina food misclassified");
        check(CraftSearch.Food(28, 14, 85) == CraftCategory.Eitr, "Eitr food misclassified");
        check(CraftSearch.Food(23, 23, 0) == CraftCategory.Health, "Balanced food tie changed");
        check(CraftSearch.Food(25, 25, 25) == CraftCategory.Eitr, "Eitr tie changed");
        check(CraftSearch.Mead(0, 0, 0, true), "Healing/resistance mead excluded");
        check(!CraftSearch.Mead(0, 0, 0, false), "Ordinary material classified as mead");
        check(!CraftSearch.Mead(45, 15, 0, true), "Buff food classified as mead");
        Console.WriteLine("PASS: crafting name/ingredient search, multi-word matching, food dominance and mead rules.");
    }
}
