using Ironbound.HUD;

internal static class StatusCategoryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (string name in new[] { "Poison", "Burning", "Smoked", "Wet", "Cold", "Freezing", "Frost", "Lightning", "Spirit", "Tared", "Encumbered", "Puke" })
            check(StatusCategories.Classify(name, false, false) == StatusCategory.Debuff, "Harmful effect left by the portrait: " + name);
        foreach (string name in new[] { "Rested", "Resting", "CampFire", "Shelter" })
            check(StatusCategories.Classify(name, false, false) == StatusCategory.Ongoing, "Helpful ongoing effect moved below compass: " + name);
        foreach (string name in new[] { "Wishbone", "BeltStrength", "Wisplight", "ModdedEquipmentBonus" })
            check(StatusCategories.Classify(name, true, false) == StatusCategory.Passive, "Equipment effect did not use passive group: " + name);
        check(StatusCategories.Classify("ModdedPoison", false, true) == StatusCategory.Debuff, "Modded damage-over-time implementation was not treated as harmful");
        check(StatusCategories.Classify("Burning", true, false) == StatusCategory.Debuff, "Equipment metadata overrode harmful classification");
        check(StatusCategories.Classify("ModdedBurn", true, true) == StatusCategory.Debuff, "Equipment damage-over-time effect was grouped as passive");
        check(StatusCategories.Classify("Poison (Clone)", false, false) == StatusCategory.Debuff, "Cloned effect lost native classification");
        check(StatusCategories.Classify("smoked", false, false) == StatusCategory.Debuff, "Effect name case changed classification");
        check(StatusCategories.Classify("ModdedBlessing", false, false) == StatusCategory.Ongoing, "Unknown beneficial mod effect was guessed harmful");
        Console.WriteLine("PASS: passive, ongoing and harmful status routing, including mod damage types and native fire/burning distinction.");
    }
}
