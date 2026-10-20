using Ragnarok.Configuration;

internal static class ConfigSchemaChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var cases = new (string Old, string Key, string New)[]
        {
            ("Interface", "Scale", "HUD.Window"),
            ("Inventory", "Style", "HUD.Inventory"),
            ("Inventory", "CharacterSlots", "Gameplay.Equipment"),
            ("Inventory", "ExtraUtilitySlots", "Gameplay.Equipment"),
            ("Inventory", "AutoEquipOnReclaim", "QualityOfLife.DeathRecovery"),
            ("Inventory.Crafting", "Style", "HUD.Crafting"),
            ("Inventory.Crafting", "VanillaFilters", "QualityOfLife.Crafting"),
            ("Inventory.Provisions", "ShowInHud", "HUD.Provisions"),
            ("Inventory.Provisions", "Slot1Key", "QualityOfLife.Provisions"),
            ("HUD.WeaponDial", "Enabled", "QualityOfLife.WeaponDial"),
            ("HUD.WeaponDial", "Key", "QualityOfLife.WeaponDial"),
            ("HUD.WeaponDial", "Style", "HUD.WeaponDial"),
            ("HUD.Hotbar", "SecondRow", "QualityOfLife.Hotbar"),
            ("HUD.Hotbar", "SwitchRowKey", "QualityOfLife.Hotbar"),
            ("HUD.Hotbar", "Scale", "HUD.Hotbar"),
            ("HUD.ChestSearch", "Enabled", "QualityOfLife.ChestSearch"),
            ("HUD.ChestSearch", "Range", "QualityOfLife.ChestSearch"),
            ("HUD.Plants", "ShowGrowthStatus", "QualityOfLife.Plants"),
            ("Inventory.Sort", "KeepHotbar", "QualityOfLife.InventorySort"),
            ("Inventory.QuickStack", "Enabled", "QualityOfLife.QuickStack"),
            ("NearbyChests", "Range", "QualityOfLife.NearbyChests"),
            ("QoL.Death", "SkillLossModifierPercent", "Gameplay.Death"),
            ("QoL.Skills", "ExperienceGainModifierPercent", "Gameplay.Skills"),
            ("QoL.ForsakenPowers", "DurationMinutes", "Gameplay.ForsakenPowers"),
            ("QoL.Smelter", "FuelCapacity", "Gameplay.Smelter"),
            ("Server", "LockSettings", "Gameplay.Server"),
            ("Development", "ReloadConfigKey", "HUD.Development")
        };
        foreach (var item in cases)
        {
            check(ConfigSchema.CanonicalSection(item.Old, item.Key) == item.New, "Wrong group for " + item.Old + "." + item.Key);
            check(ConfigSchema.CanonicalSection(item.New, item.Key) == item.New, "Migration is not idempotent");
            check(ConfigSchema.WireSection(item.New, item.Key) == item.Old, "Legacy peer cannot read wire name");
            foreach (bool reverse in new[] { false, true })
            {
                var data = new Dictionary<(string Section, string Key), string>();
                if (reverse) data[(item.New, item.Key)] = "custom-new";
                if (item.Old != item.New) data[(item.Old, item.Key)] = "custom-old";
                data[(item.New, item.Key)] = "custom-new";
                data[("Unrelated", "Option")] = "retain";
                ConfigSchema.Migrate(data);
                ConfigSchema.Migrate(data);
                check(data.Count == 2 && data[(item.New, item.Key)] == "custom-new", "New value did not win conflict");
                check(data[("Unrelated", "Option")] == "retain", "Unknown setting was lost");
            }
            var legacyOnly = new Dictionary<(string Section, string Key), string> { [(item.Old, item.Key)] = "user value" };
            ConfigSchema.Migrate(legacyOnly);
            check(legacyOnly.Count == 1 && legacyOnly[(item.New, item.Key)] == "user value", "Saved value was reset");
        }
        check(!ConfigSchema.Visible("HUD", "LayoutVersion"), "Internal migration marker exposed as a user control");
        check(ConfigSchema.Visible("HUD", "Style"), "HUD style was hidden");
        foreach (string section in new[] { "Vitals", "Status", "Keys", "Minimap", "Compass", "Character", "Power", "Notifications", "SkillToast" })
        {
            check(!ConfigSchema.Visible("HUD." + section, "Background"), "Removed backdrop control exposed in " + section);
            check(!ConfigSchema.Visible("HUD." + section, "BackgroundOpacity"), "Removed backdrop opacity exposed in " + section);
            check(ConfigSchema.Visible("HUD." + section, "MarginX"), "HUD placement hidden in " + section);
        }
        check(ConfigSchema.Visible("HUD.Inventory", "BackgroundOpacity"), "Inventory panel opacity was hidden with HUD backdrops");
        Console.WriteLine("PASS: configuration migration, conflict precedence, idempotence and legacy wire names.");
    }
}
