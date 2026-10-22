using Ironbound.Configuration;

internal static class BrandingMigrationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Ironbound-branding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string current = Path.Combine(directory, BrandingMigration.CurrentGuid + ".cfg");
            string legacy = Path.Combine(directory, BrandingMigration.LegacyGuid + ".cfg");
            const string original = "[HUD]\nStyle = Ragnarok\n[HUD.Window]\nScale = 1.25\n";
            check(!BrandingMigration.ImportConfig(current), "Fresh install must not invent settings");
            File.WriteAllText(legacy, original);
            check(BrandingMigration.ImportConfig(current) && File.ReadAllText(current) == original, "Rename must copy all existing settings");
            check(File.ReadAllText(legacy) == original, "Legacy config must remain untouched");
            File.WriteAllText(current, "existing Ironbound settings");
            check(!BrandingMigration.ImportConfig(current) && File.ReadAllText(current) == "existing Ironbound settings", "Existing Ironbound preferences must win");
            check(!BrandingMigration.ImportConfig(Path.Combine(directory, "unrelated.cfg")), "Unrelated config must not be imported");
            var settings = new Dictionary<(string Section, string Key), string>
            {
                [("HUD", "Style")] = "Ragnarok", [("Inventory", "Style")] = "Ragnarok",
                [("HUD.Minimap", "Style")] = "ragnarok", [("HUD.Crafting", "Style")] = "Vanilla",
                [("UnknownMod", "Style")] = "Ragnarok", [("HUD.Development", "BadgeText")] = "Ragnarok custom text"
            };
            ConfigSchema.Migrate(settings); ConfigSchema.Migrate(settings);
            check(settings[("HUD", "Style")] == "Ironbound" && settings[("HUD.Inventory", "Style")] == "Ironbound" &&
                settings[("HUD.Minimap", "Style")] == "Ironbound", "Old style enum names must migrate, including legacy sections");
            check(settings[("HUD.Crafting", "Style")] == "Vanilla" && settings[("UnknownMod", "Style")] == "Ragnarok" &&
                settings[("HUD.Development", "BadgeText")] == "Ragnarok custom text", "Rename must preserve unrelated settings and user text");
            settings[("HUD.Development", "BadgeText")] = "Ragnar\u00f6k loaded";
            ConfigSchema.Migrate(settings);
            check(settings[("HUD.Development", "BadgeText")] == "Ironbound loaded", "Legacy default badge must use the new brand");
        }
        finally
        {
            string full = Path.GetFullPath(directory);
            if (Path.GetDirectoryName(full) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
                !Path.GetFileName(full).StartsWith("Ironbound-branding-", StringComparison.Ordinal)) throw new Exception("Unsafe test cleanup path");
            Directory.Delete(full, true);
        }
        Console.WriteLine("PASS: Ironbound config import, old-file preservation, existing-config precedence and legacy style migration.");
    }
}
