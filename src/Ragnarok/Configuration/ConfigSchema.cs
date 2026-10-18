using System;
using System.Collections.Generic;

namespace Ragnarok.Configuration;

// Pure naming rules shared by disk migration, the settings UI and the network protocol.
// Wire names deliberately stay compatible with 0.3 clients/servers.
internal static class ConfigSchema
{
    private static readonly Dictionary<string, string> Sections = new(StringComparer.Ordinal)
    {
        ["Interface"] = "HUD.Window",
        ["Inventory"] = "HUD.Inventory",
        ["Inventory.Crafting"] = "HUD.Crafting",
        ["Inventory.Compendium"] = "HUD.Compendium",
        ["Inventory.Skills"] = "HUD.Skills",
        ["Inventory.Trophies"] = "HUD.Trophies",
        ["Inventory.Achievements"] = "HUD.Achievements",
        ["Inventory.Provisions"] = "QualityOfLife.Provisions",
        ["Inventory.Sort"] = "QualityOfLife.InventorySort",
        ["Inventory.QuickStack"] = "QualityOfLife.QuickStack",
        ["NearbyChests"] = "QualityOfLife.NearbyChests",
        ["HUD.Plants"] = "QualityOfLife.Plants",
        ["HUD.ChestSearch"] = "QualityOfLife.ChestSearch",
        ["Server"] = "Gameplay.Server",
        ["Development"] = "HUD.Development"
    };

    private static readonly (string OldSection, string Key, string NewSection)[] Overrides =
    {
        ("Inventory", "CharacterSlots", "Gameplay.Equipment"),
        ("Inventory", "ExtraUtilitySlots", "Gameplay.Equipment"),
        ("Inventory", "AutoEquipOnReclaim", "QualityOfLife.DeathRecovery"),
        ("Inventory.Crafting", "VanillaFilters", "QualityOfLife.Crafting"),
        ("Inventory.Provisions", "ShowInHud", "HUD.Provisions"),
        ("HUD.WeaponDial", "Enabled", "QualityOfLife.WeaponDial"),
        ("HUD.WeaponDial", "Key", "QualityOfLife.WeaponDial"),
        ("HUD.Hotbar", "SecondRow", "QualityOfLife.Hotbar"),
        ("HUD.Hotbar", "SwitchRowKey", "QualityOfLife.Hotbar")
    };

    internal static string CanonicalSection(string section, string key)
    {
        foreach (var item in Overrides)
            if (item.OldSection == section && item.Key == key) return item.NewSection;
        if (section.StartsWith("QoL.", StringComparison.Ordinal)) return "Gameplay." + section.Substring(4);
        return Sections.TryGetValue(section, out string? mapped) ? mapped : section;
    }

    internal static string WireSection(string section, string key)
    {
        foreach (var item in Overrides)
            if (item.NewSection == section && item.Key == key) return item.OldSection;
        foreach (var item in Sections)
            if (item.Value == section) return item.Key;
        if (section.StartsWith("Gameplay.", StringComparison.Ordinal)) return "QoL." + section.Substring(9);
        return section;
    }

    // New-format values win when both forms exist. Unknown settings are left untouched.
    internal static void Migrate(IDictionary<(string Section, string Key), string> values)
    {
        foreach (var item in new List<KeyValuePair<(string Section, string Key), string>>(values))
        {
            var target = (CanonicalSection(item.Key.Section, item.Key.Key), item.Key.Key);
            if (target == item.Key) continue;
            if (!values.ContainsKey(target)) values[target] = item.Value;
            values.Remove(item.Key);
        }
    }

    internal static bool Visible(string section, string key) => !(section == "HUD" && key == "LayoutVersion");
}
