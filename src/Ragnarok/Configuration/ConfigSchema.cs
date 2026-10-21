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
        ("HUD.Minimap", "ChestSearchMarker", "QualityOfLife.NearbyChests")
    };

    internal static string CanonicalSection(string section, string key)
    {
        if ((section == "QualityOfLife.ChestSearch" || section == "HUD.ChestSearch") && key == "Shortcut")
            return "QualityOfLife.NearbyChests";
        if ((section == "QualityOfLife.QuickStack" || section == "Inventory.QuickStack") && key != "Enabled")
            return "QualityOfLife.NearbyChests";
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

    // Nearby Chests owns the combined policy. Old subfeature switches/ranges cannot
    // override it; retain the established Nearby Chests enable/radius preferences.
    private static bool RetiredChestSetting(string section, string key) =>
        ((section == "QualityOfLife.NearbyChests" || section == "NearbyChests" ||
          section == "QualityOfLife.QuickStack" || section == "Inventory.QuickStack") &&
            (key == "KeepWeapons" || key == "KeepConsumables" || key == "KeepTools")) ||
        ((section == "QualityOfLife.ChestSearch" || section == "HUD.ChestSearch") &&
            (key == "Enabled" || key == "Range" || key == "PersonalRange")) ||
        ((section == "QualityOfLife.QuickStack" || section == "Inventory.QuickStack") && key == "Enabled") ||
        ((section == "QualityOfLife.NearbyChests" || section == "NearbyChests") &&
            (key == "Crafting" || key == "Building" || key == "Stations"));

    // New-format values win when both forms exist. Unknown settings are left untouched.
    internal static void Migrate(IDictionary<(string Section, string Key), string> values)
    {
        foreach (var item in new List<KeyValuePair<(string Section, string Key), string>>(values))
        {
            if (RetiredChestSetting(item.Key.Section, item.Key.Key) ||
                ((item.Key.Section == "HUD.Hotbar" || item.Key.Section == "QualityOfLife.Hotbar") &&
                    (item.Key.Key == "SecondRow" || item.Key.Key == "SwitchRowKey")))
            {
                values.Remove(item.Key);
                continue;
            }
            var target = (CanonicalSection(item.Key.Section, item.Key.Key), item.Key.Key);
            if (target == item.Key) continue;
            if (!values.ContainsKey(target)) values[target] = item.Value;
            values.Remove(item.Key);
        }
    }

    internal static bool Visible(string section, string key) => !(section == "HUD" && key == "LayoutVersion") &&
        !((key == "Background" || key == "BackgroundOpacity") &&
          (section == "HUD.Vitals" || section == "HUD.Status" || section == "HUD.Keys" ||
           section == "HUD.Minimap" || section == "HUD.Compass" || section == "HUD.Character" ||
           section == "HUD.Power" || section == "HUD.Notifications" || section == "HUD.SkillToast"));
}
