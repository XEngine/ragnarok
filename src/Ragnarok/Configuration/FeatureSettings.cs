using BepInEx.Configuration;
using UnityEngine;

namespace Ragnarok.Configuration;

// Behavior options are separate from local presentation settings. Server-owned entries are
// explicitly registered by Plugin; keyboard bindings and filters remain local.
internal sealed class FeatureSettings
{
    internal readonly ConfigEntry<bool> ChestSearch;
    internal readonly ConfigEntry<float> ChestSearchRange;
    internal readonly ConfigEntry<KeyboardShortcut> ChestSearchKey;
    internal readonly ConfigEntry<bool> WeaponDial;
    internal readonly ConfigEntry<KeyboardShortcut> WeaponDialKey;
    internal readonly ConfigEntry<bool> PlantStatus;
    internal readonly ConfigEntry<bool> HotbarSecondRow;
    internal readonly ConfigEntry<KeyboardShortcut> HotbarToggleKey;
    internal readonly ConfigEntry<bool> CharacterSlots;
    internal readonly ConfigEntry<bool> ExtraUtilitySlots;
    internal readonly ConfigEntry<bool> AutoEquipOnReclaim;
    internal readonly ConfigEntry<bool> CraftFilters;
    internal readonly ConfigEntry<bool> SortKeepHotbar;
    internal readonly ConfigEntry<bool> QuickStack;
    internal readonly ConfigEntry<bool> KeepWeapons;
    internal readonly ConfigEntry<bool> KeepConsumables;
    internal readonly ConfigEntry<bool> KeepTools;
    internal readonly ConfigEntry<KeyboardShortcut>[] ProvisionKeys = new ConfigEntry<KeyboardShortcut>[Equipment.CharacterSlots.Provisions];

    internal FeatureSettings(ConfigFile config)
    {
        ChestSearch = config.Bind("QualityOfLife.ChestSearch", "Enabled", true, "Search accessible player-built chests near you. Server-controlled.");
        ChestSearchRange = config.Bind("QualityOfLife.ChestSearch", "Range", 40f, new ConfigDescription("Chest search radius in metres. Server-controlled.", new AcceptableValueRange<float>(5f, 100f)));
        ChestSearchKey = config.Bind("QualityOfLife.ChestSearch", "Shortcut", new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl), "Open item search. Ctrl+F by default.");
        WeaponDial = config.Bind("QualityOfLife.WeaponDial", "Enabled", true,
            "Hold the dial key to lay your weapons, shields and tools around the screen's centre; move the mouse towards one and let go to take it in hand. Let go in the middle to cancel.");
        WeaponDialKey = config.Bind("QualityOfLife.WeaponDial", "Key", new KeyboardShortcut(KeyCode.V),
            "Hold to open the weapon dial. V by default; the side mouse buttons work too.");
        PlantStatus = config.Bind("QualityOfLife.Plants", "ShowGrowthStatus", true,
            "Show plant growth status and estimated remaining time in the normal hover text. Uses elapsed world seconds (not accelerated day/night hours). Works with either HUD style.");
        HotbarSecondRow = config.Bind("QualityOfLife.Hotbar", "SecondRow", true,
            "Two hotbars: the inventory's top row (equipment) and the row under it (consumables). Hold the switch key to show the second row; keys 1-8 then use it (Alt+1 uses its first slot). Let go and the first row is back. While on, shortcuts that use the switch key as their modifier (the provision keys' Alt+4 by default) go to the hotbar instead. Ragnarök HUD and action bar only.");
        HotbarToggleKey = config.Bind("QualityOfLife.Hotbar", "SwitchRowKey", new KeyboardShortcut(KeyCode.LeftAlt),
            "Hold to show the second hotbar. Left Alt by default (either Alt works).");
        CharacterSlots = config.Bind("Gameplay.Equipment", "CharacterSlots", true,
            "Show the character panel beside the inventory (Ragnarök or vanilla style): worn gear (head, chest, legs, cape, utility, trinket) and six " +
            "provision slots for food and meads. Items there do not take grid slots. Off moves them back into the grid.");
        ExtraUtilitySlots = config.Bind("Gameplay.Equipment", "ExtraUtilitySlots", true,
            "Two more utility slots in the character panel. Valheim wears one utility item; items in the extra slots add their " +
            "effect too (Megingjord, wishbone, Wisplight at once). Only the first shows on your character.");
        AutoEquipOnReclaim = config.Bind("QualityOfLife.DeathRecovery", "AutoEquipOnReclaim", true,
            "Immediately equip the gear worn at death when reclaiming your own tombstone, including extra utility slots. " +
            "Use takes everything directly when it fits, counting empty character/provision slots as well as the backpack and respecting carry weight. " +
            "Take All restores marked gear into empty character slots before filling the backpack. " +
            "Applies to deaths recorded while this option is enabled; spare equipment stays in the backpack.");
        CraftFilters = config.Bind("QualityOfLife.Crafting", "VanillaFilters", true,
            "With Valheim's crafting panel, add recipe/ingredient search, category and armor filters, sorting, quantity controls and bag/nearby material counts using native art and fonts.");
        SortKeepHotbar = config.Bind("QualityOfLife.InventorySort", "KeepHotbar", true,
            "Sorting leaves the hotbar rows (the top row, and the second one when HUD.Hotbar SecondRow is on) as they are and sorts the rows below them.");
        QuickStack = config.Bind("QualityOfLife.QuickStack", "Enabled", true,
            "The Quick Stack button: puts items into nearby chests that already hold the same kind. A server can turn it off for everyone.");
        KeepWeapons = config.Bind("QualityOfLife.QuickStack", "KeepWeapons", true,
            "Legacy preference for unscoped Quick Stack callers: retain weapons, shields and ammunition. Both inventory styles use their selected category instead.");
        KeepConsumables = config.Bind("QualityOfLife.QuickStack", "KeepConsumables", true,
            "Legacy preference for unscoped Quick Stack callers: retain food, meads and other consumables. Both inventory styles use their selected category instead.");
        KeepTools = config.Bind("QualityOfLife.QuickStack", "KeepTools", true,
            "Legacy preference for unscoped Quick Stack callers: retain tools and torches. Both inventory styles use their selected category instead.");
        for (int i = 0; i < ProvisionKeys.Length; i++)
            ProvisionKeys[i] = config.Bind("QualityOfLife.Provisions", $"Slot{i + 1}Key", new KeyboardShortcut(KeyCode.Alpha4 + i, KeyCode.LeftAlt),
                $"Use the item in provision slot {i + 1} (eat, drink), like a hotbar key. Valheim's own hotbar key does not fire with it. " +
                "Defaults start at Alt+4 to stay clear of Better Archery's quiver keys (Alt+1-3).");
        // The first release used Alt+1-6, which Better Archery's quiver also uses: move untouched defaults once.
        bool firstDefaults = true;
        for (int i = 0; i < ProvisionKeys.Length; i++)
            firstDefaults &= ProvisionKeys[i].Value.Serialize() == new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt).Serialize();
        if (firstDefaults)
            foreach (ConfigEntry<KeyboardShortcut> key in ProvisionKeys)
                key.Value = (KeyboardShortcut)key.DefaultValue;

    }
}
