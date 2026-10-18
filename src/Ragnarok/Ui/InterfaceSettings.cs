using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Ui;

internal enum HudStyle
{
    Vanilla,
    Ragnarok
}

// Edges of Ragnarök's frames: clean square edges, or worn and chipped like old metal.
internal enum FrameEdges
{
    Solid,
    Chipped
}

internal enum InventoryStyle
{
    FollowHud,
    Vanilla,
    Ragnarok
}

// Same order as UnityEngine.TextAnchor, so the two convert directly.
internal enum ScreenAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

// Shade: a feathered Animus-style field. Plate: a chamfered obsidian plate with a gold rim.
// Ragnarök look or Valheim's, for parts that are restyled rather than replaced.
internal enum PanelStyle
{
    FollowHud,
    Vanilla,
    Ragnarok
}

internal enum HudBackground
{
    None,
    Shade,
    Plate
}

internal sealed class HudPlacement
{
    internal readonly ConfigEntry<ScreenAnchor> Position;
    internal readonly ConfigEntry<float> MarginX;
    internal readonly ConfigEntry<float> MarginY;
    internal readonly ConfigEntry<HudBackground> Background;
    internal readonly ConfigEntry<float> BackgroundOpacity;

    internal HudPlacement(ConfigFile config, string section, string what, ScreenAnchor position, float marginX = 40f, float marginY = 40f)
    {
        Position = config.Bind(section, "Position", position, $"Screen position of the {what}.");
        MarginX = config.Bind(section, "MarginX", marginX, new ConfigDescription(
            $"Horizontal distance of the {what} from the chosen screen edge, in pixels at 1080p. " +
            "When centred horizontally, positive values move it right.", new AcceptableValueRange<float>(-500f, 500f)));
        MarginY = config.Bind(section, "MarginY", marginY, new ConfigDescription(
            $"Vertical distance of the {what} from the chosen screen edge, in pixels at 1080p. " +
            "When centred vertically, positive values move it up.", new AcceptableValueRange<float>(-500f, 500f)));
        Background = config.Bind(section, "Background", HudBackground.Shade,
            $"Backdrop behind the {what}. Shade is a soft feathered field; Plate is a chamfered plate with a gold rim.");
        BackgroundOpacity = config.Bind(section, "BackgroundOpacity", 0.5f, new ConfigDescription(
            $"Opacity of the backdrop behind the {what}.", new AcceptableValueRange<float>(0f, 1f)));
    }

    // Anchors, pivots and offsets a HUD element so margins always point inward from the chosen edge.
    internal void Apply(RectTransform rect)
    {
        int index = (int)Position.Value;
        Vector2 point = new((index % 3) * 0.5f, 1f - (index / 3) * 0.5f);
        if (rect.pivot != point) rect.anchorMin = rect.anchorMax = rect.pivot = point;
        Vector2 offset = new(point.x == 1f ? -MarginX.Value : MarginX.Value, point.y == 1f ? -MarginY.Value : MarginY.Value);
        if (rect.anchoredPosition != offset) rect.anchoredPosition = offset;
    }
}

// Dotted sections ("HUD.Vitals") appear as sub-pages under their parent in the F10 window.
internal sealed class InterfaceSettings
{
    internal const string HudSection = "HUD", VitalsSection = "HUD.Vitals", StatusSection = "HUD.Status", KeysSection = "HUD.Keys";
    internal const string MinimapSection = "HUD.Minimap";

    internal readonly ConfigEntry<KeyboardShortcut> WindowKey;
    internal readonly ConfigEntry<float> Scale;
    internal readonly ConfigEntry<float> ScrollSpeed;
    internal readonly ConfigEntry<FrameEdges> Edges;
    internal readonly ConfigEntry<bool> FollowSystemScroll;
    internal readonly ConfigEntry<HudStyle> Hud;
    internal readonly ConfigEntry<float> HudScale;
    internal readonly ConfigEntry<bool> HudNumbers;
    internal readonly ConfigEntry<bool> HudFood;
    internal readonly ConfigEntry<bool> HudPower;
    internal readonly ConfigEntry<bool> HudStatus;
    internal readonly ConfigEntry<bool> StatusLabels;
    internal readonly ConfigEntry<KeyboardShortcut> StatusLabelsKey;
    internal readonly ConfigEntry<bool> HotbarSecondRow;
    internal readonly ConfigEntry<KeyboardShortcut> HotbarToggleKey;
    internal readonly HudPlacement Vitals;
    internal readonly HudPlacement Status;
    internal readonly ConfigEntry<bool> HudKeys;
    internal readonly HudPlacement Keys;
    internal readonly ConfigEntry<InventoryStyle> Inventory;
    internal readonly ConfigEntry<bool> CharacterSlots;
    internal readonly ConfigEntry<float> InventoryOpacity;
    internal readonly ConfigEntry<float> InventoryScale;
    internal readonly ConfigEntry<bool> ExtraUtilitySlots;
    internal readonly ConfigEntry<bool> AutoEquipOnReclaim;
    internal readonly ConfigEntry<bool> ChestPreview;
    internal readonly ConfigEntry<float> ChestPreviewX, ChestPreviewY, ChestPreviewScale;
    internal readonly ConfigEntry<int> ChestPreviewColumns;
    internal readonly ConfigEntry<bool> ActionBar;
    internal readonly ConfigEntry<bool> PlantStatus;
    internal readonly ConfigEntry<bool> ChestSearch;
    internal readonly ConfigEntry<float> ChestSearchRange;
    internal readonly ConfigEntry<KeyboardShortcut> ChestSearchKey;
    internal readonly ConfigEntry<bool> WeaponDial;
    internal readonly ConfigEntry<KeyboardShortcut> WeaponDialKey;
    internal readonly ConfigEntry<PanelStyle> WeaponDialStyle;
    internal readonly ConfigEntry<float> ActionBarScale, ActionBarX, ActionBarBottom;
    internal readonly ConfigEntry<bool> ProvisionBar;
    internal readonly ConfigEntry<bool> SortKeepHotbar;
    internal readonly ConfigEntry<bool> MinimapInfo;
    internal readonly ConfigEntry<bool> Clock24h;
    internal readonly ConfigEntry<PanelStyle> MinimapStyle;
    internal readonly ConfigEntry<PanelStyle> NotificationStyle;
    internal readonly ConfigEntry<PanelStyle> CompendiumStyle;
    internal readonly ConfigEntry<PanelStyle> SkillsStyle;
    internal readonly ConfigEntry<PanelStyle> TrophiesStyle;
    internal readonly ConfigEntry<PanelStyle> AchievementsStyle;
    internal readonly ConfigEntry<PanelStyle> CraftingStyle;
    internal readonly ConfigEntry<bool> CraftFilters;
    internal readonly HudPlacement Notifications;
    internal readonly ConfigEntry<bool> Compass, CompassHome, CompassPortals, CompassShips, CompassPlayers;
    internal readonly ConfigEntry<float> CompassPortalRange;
    internal readonly ConfigEntry<HudBackground> CompassBackground, MinimapBackground;
    internal readonly ConfigEntry<float> CompassBackgroundOpacity, MinimapBackgroundOpacity;
    internal readonly ConfigEntry<KeyboardShortcut>[] ProvisionKeys = new ConfigEntry<KeyboardShortcut>[Equipment.CharacterSlots.Provisions];
    internal readonly ConfigEntry<bool> QuickStack;
    internal readonly ConfigEntry<bool> KeepWeapons;
    internal readonly ConfigEntry<bool> KeepConsumables;
    internal readonly ConfigEntry<bool> KeepTools;

    internal InterfaceSettings(ConfigFile config)
    {
        ChestSearch = config.Bind("HUD.ChestSearch", "Enabled", true, "Search accessible player-built chests near you. Server-controlled.");
        ChestSearchRange = config.Bind("HUD.ChestSearch", "Range", 40f, new ConfigDescription("Chest search radius in metres. Server-controlled.", new AcceptableValueRange<float>(5f, 100f)));
        ChestSearchKey = config.Bind("HUD.ChestSearch", "Shortcut", new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl), "Open item search. Ctrl+F by default.");
        WeaponDial = config.Bind("HUD.WeaponDial", "Enabled", true,
            "Hold the dial key to lay your weapons, shields and tools around the screen's centre; move the mouse towards one and let go to take it in hand. Let go in the middle to cancel.");
        WeaponDialKey = config.Bind("HUD.WeaponDial", "Key", new KeyboardShortcut(KeyCode.V),
            "Hold to open the weapon dial. V by default; the side mouse buttons work too.");
        WeaponDialStyle = config.Bind("HUD.WeaponDial", "Style", PanelStyle.FollowHud,
            "Ragnarok: obsidian and crimson with a gold halo. Vanilla: dark glass with Valheim's orange highlight and font. FollowHud matches the HUD style.");
        PlantStatus = config.Bind("HUD.Plants", "ShowGrowthStatus", true,
            "Show plant growth status and estimated remaining time in the normal hover text. Uses elapsed world seconds (not accelerated day/night hours). Works with either HUD style.");
        const string actionBar = "HUD.Hotbar";
        ActionBar = config.Bind(actionBar, "Enabled", true, "Use the Ragnarök action-bar panel while the Ragnarök HUD is selected. Vanilla HUD keeps the original hotbar.");
        ActionBarScale = config.Bind(actionBar, "Scale", 1f, new ConfigDescription("Size of the action-bar panel.", new AcceptableValueRange<float>(0.5f, 1.5f)));
        ActionBarX = config.Bind(actionBar, "OffsetX", 0f, new ConfigDescription("Horizontal offset from bottom centre, in HUD pixels.", new AcceptableValueRange<float>(-1000f, 1000f)));
        HotbarSecondRow = config.Bind(actionBar, "SecondRow", true,
            "Two hotbars: the inventory's top row (equipment) and the row under it (consumables). Hold the switch key to show the second row; keys 1-8 then use it (Alt+1 uses its first slot). Let go and the first row is back. While on, shortcuts that use the switch key as their modifier (the provision keys' Alt+4 by default) go to the hotbar instead. Ragnarök HUD and action bar only.");
        HotbarToggleKey = config.Bind(actionBar, "SwitchRowKey", new KeyboardShortcut(KeyCode.LeftAlt),
            "Hold to show the second hotbar. Left Alt by default (either Alt works).");
        ActionBarBottom = config.Bind(actionBar, "BottomMargin", 24f, new ConfigDescription("Distance above the bottom edge, in HUD pixels.", new AcceptableValueRange<float>(0f, 1000f)));
        const string preview = "HUD.ChestPreview";
        ChestPreview = config.Bind(preview, "Enabled", true, "Show a read-only item grid when aiming at an accessible chest within normal interaction range.");
        ChestPreviewX = config.Bind(preview, "OffsetX", 80f, new ConfigDescription("Horizontal offset from screen centre; positive moves right, in HUD pixels. With the Ragnarök HUD the action prompt (\"[E] Open\") shares this left edge.", new AcceptableValueRange<float>(-2000f, 2000f)));
        ChestPreviewY = config.Bind(preview, "OffsetY", 80f, new ConfigDescription("Vertical offset from screen centre; positive moves down, in HUD pixels.", new AcceptableValueRange<float>(-1200f, 1200f)));
        ChestPreviewScale = config.Bind(preview, "Scale", 1f, new ConfigDescription("Size of the compact chest preview.", new AcceptableValueRange<float>(0.5f, 2f)));
        ChestPreviewColumns = config.Bind(preview, "Columns", 6, new ConfigDescription("Item columns in the compact preview. Empty inventory slots are omitted.", new AcceptableValueRange<int>(1, 12)));
        WindowKey = config.Bind("Interface", "ConfigWindowKey", new KeyboardShortcut(KeyCode.F10),
            "Open or close the Ragnarök configuration window.");
        Scale = config.Bind("Interface", "Scale", 1f, new ConfigDescription(
            "Size of Ragnarök windows relative to a 1080p screen.", new AcceptableValueRange<float>(0.75f, 1.5f)));
        Edges = config.Bind("Interface", "FrameEdges", FrameEdges.Solid,
            "Edges of Ragnarök's frames, bars, slots and buttons: Solid (clean square edges) or Chipped (worn, jagged metal).");
        FollowSystemScroll = config.Bind("Interface", "FollowWindowsScroll", true,
            "Scroll lists as far per mouse-wheel notch as Windows does (Mouse settings: lines to scroll each time). " +
            "Off, or when there is no Windows setting, ScrollSpeed is used.");
        ScrollSpeed = config.Bind("Interface", "ScrollSpeed", 3f, new ConfigDescription(
            "How far one mouse-wheel notch scrolls Ragnarök's lists (crafting, Compendium, this window), in rows of about " +
            "40 pixels. Also applies to Valheim's recipe list while the Ragnarök crafting filters are on.",
            new AcceptableValueRange<float>(1f, 8f)));

        Hud = config.Bind(HudSection, "Style", HudStyle.Vanilla,
            "Vanilla keeps Valheim's HUD. Ragnarok replaces health, stamina, eitr, food and status effects with the Ragnarök HUD.");
        HudScale = config.Bind(HudSection, "Scale", 1f, new ConfigDescription(
            "Size of every part of the Ragnarök HUD.", new AcceptableValueRange<float>(0.6f, 1.6f)));

        HudNumbers = config.Bind(VitalsSection, "ShowNumbers", true, "Show current and maximum values beside the bars.");
        HudFood = config.Bind(VitalsSection, "ShowFood", true, "Show active foods and their remaining time above the bars.");
        HudPower = config.Bind(VitalsSection, "ShowForsakenPower", true,
            "Show your Forsaken power as the emblem beside the bars, replacing Valheim's power box.");
        Vitals = new HudPlacement(config, VitalsSection, "health, stamina, eitr and food cluster", ScreenAnchor.BottomLeft);

        NotificationStyle = config.Bind("HUD.Notifications", "Style", PanelStyle.FollowHud,
            "Ragnarok shows unlocks (new recipe, piece, station, item) as cards and pickups and small messages as rows; " +
            "Vanilla keeps Valheim's. FollowHud uses the Ragnarök notifications whenever the Ragnarök HUD is on. " +
            "Big centre messages always stay Valheim's.");
        Notifications = new HudPlacement(config, "HUD.Notifications", "notifications", ScreenAnchor.MiddleLeft, 40f, 0f);

        CraftingStyle = config.Bind("Inventory.Crafting", "Style", PanelStyle.FollowHud,
            "Ragnarok shows the crafting panel in Ragnarök style with a search box and category filters; Vanilla keeps " +
            "Valheim's. FollowHud uses the Ragnarök panel whenever the Ragnarök HUD is on. Crafting itself is Valheim's either way.");

        CraftFilters = config.Bind("Inventory.Crafting", "VanillaFilters", true,
            "With Valheim's crafting panel, add the search box and category filters above its recipe list.");

        CompendiumStyle = config.Bind("Inventory.Compendium", "Style", PanelStyle.FollowHud,
            "Ragnarok shows the Compendium (the texts dialog: journal, Hugin's lessons, runestones) as a Ragnarök screen with " +
            "chapters and an art panel; Vanilla keeps Valheim's. FollowHud uses the Ragnarök screen whenever the Ragnarök HUD is on.");
        SkillsStyle = config.Bind("Inventory.Skills", "Style", PanelStyle.FollowHud,
            "Ragnarok shows the skills dialog as a Ragnarök screen in the Compendium's frame (every skill with its level, the chosen " +
            "one's bonus, progress and description); Vanilla keeps Valheim's. FollowHud uses the Ragnarök screen whenever the Ragnarök HUD is on.");
        TrophiesStyle = config.Bind("Inventory.Trophies", "Style", PanelStyle.FollowHud,
            "Ragnarok shows the trophies as a Ragnarök screen in the Compendium's frame (every trophy in the game, the ones you have taken lit, " +
            "the chosen one's lore beside them); Vanilla keeps Valheim's. FollowHud uses the Ragnarök screen whenever the Ragnarök HUD is on.");
        AchievementsStyle = config.Bind("Inventory.Achievements", "Style", PanelStyle.FollowHud,
            "Ragnarok shows the achievements as a Ragnarök screen in the Compendium's frame (every badge, the unlocked ones lit, the chosen " +
            "one's conditions beside them); Vanilla keeps Valheim's. FollowHud uses the Ragnarök screen whenever the Ragnarök HUD is on.");

        HudStatus = config.Bind(StatusSection, "Enabled", true,
            "Show buffs and debuffs in the Ragnarök status strip. Off brings back Valheim's top-right list.");
        StatusLabels = config.Bind(StatusSection, "ShowLabels", true,
            "Name each effect beside its icon (\"Rested 8:29\"). Off shows only the icons and their time left. The labels key flips this in game.");
        StatusLabelsKey = config.Bind(StatusSection, "LabelsKey", new KeyboardShortcut(KeyCode.B),
            "Show or hide the status effect names. B by default (V is Valheim's auto-pickup and Ragnarök's weapon dial).");
        Status = new HudPlacement(config, StatusSection, "status effect strip", ScreenAnchor.BottomRight);

        HudKeys = config.Bind(KeysSection, "Enabled", true,
            "Show the context key guide (building, combat, inventory, fishing) as a compact vertical list. Off brings back Valheim's bottom bar.");
        Keys = new HudPlacement(config, KeysSection, "key guide", ScreenAnchor.MiddleRight, 24f, 0f);

        Inventory = config.Bind("Inventory", "Style", InventoryStyle.FollowHud,
            "Ragnarok shows the Ragnarök inventory grid and item card; Vanilla keeps Valheim's. FollowHud uses the Ragnarök inventory " +
            "whenever the Ragnarök HUD is on. Clicks, Shift-split, Ctrl-move and right-click use work as in Valheim.");
        InventoryScale = config.Bind("Inventory", "Scale", 1f, new ConfigDescription(
            "Size of the Ragnarök inventory and its character column (Valheim's inventory keeps its own size), growing from the top-left into the free space " +
            "before the info board and crafting panel (it stops short of them and of the screen's edges). In either style. " +
            "1 is Valheim's own size.", new AcceptableValueRange<float>(0.75f, 1.3f)));
        InventoryOpacity = config.Bind("Inventory", "BackgroundOpacity", 0.95f, new ConfigDescription(
            "Opacity of the Ragnarök inventory and character panels' metal background.", new AcceptableValueRange<float>(0.2f, 1f)));
        CharacterSlots = config.Bind("Inventory", "CharacterSlots", true,
            "Show the character panel beside the inventory (Ragnarök or vanilla style): worn gear (head, chest, legs, cape, utility, trinket) and six " +
            "provision slots for food and meads. Items there do not take grid slots. Off moves them back into the grid.");
        ExtraUtilitySlots = config.Bind("Inventory", "ExtraUtilitySlots", true,
            "Two more utility slots in the character panel. Valheim wears one utility item; items in the extra slots add their " +
            "effect too (Megingjord, wishbone, Wisplight at once). Only the first shows on your character.");

        AutoEquipOnReclaim = config.Bind("Inventory", "AutoEquipOnReclaim", true,
            "Immediately equip the gear worn at death when reclaiming your own tombstone, including extra utility slots. " +
            "Applies to deaths recorded while this option is enabled; spare equipment stays in the backpack.");

        const string provisions = "Inventory.Provisions";
        ProvisionBar = config.Bind(provisions, "ShowInHud", true,
            "Show the filled provision slots beside the food in the Ragnarök HUD, with their hotkeys.");
        for (int i = 0; i < ProvisionKeys.Length; i++)
            ProvisionKeys[i] = config.Bind(provisions, $"Slot{i + 1}Key", new KeyboardShortcut(KeyCode.Alpha4 + i, KeyCode.LeftAlt),
                $"Use the item in provision slot {i + 1} (eat, drink), like a hotbar key. Valheim's own hotbar key does not fire with it. " +
                "Defaults start at Alt+4 to stay clear of Better Archery's quiver keys (Alt+1-3).");
        // The first release used Alt+1-6, which Better Archery's quiver also uses: move untouched defaults once.
        bool firstDefaults = true;
        for (int i = 0; i < ProvisionKeys.Length; i++)
            firstDefaults &= ProvisionKeys[i].Value.Serialize() == new KeyboardShortcut(KeyCode.Alpha1 + i, KeyCode.LeftAlt).Serialize();
        if (firstDefaults)
            foreach (ConfigEntry<KeyboardShortcut> key in ProvisionKeys)
                key.Value = (KeyboardShortcut)key.DefaultValue;

        MinimapInfo = config.Bind(MinimapSection, "Enabled", true,
            "Show the day and clock above the minimap, and the current and next weather below it (with either HUD style).");
        Clock24h = config.Bind(MinimapSection, "TwentyFourHourClock", true, "24-hour clock (14:30). Off: 12-hour (2:30 PM).");
        MinimapStyle = config.Bind(MinimapSection, "Style", PanelStyle.FollowHud,
            "Ragnarok frames the minimap in Ragnarök style (dark plate, gold rim, crimson north mark) and restyles the biome " +
            "name; Vanilla keeps Valheim's frame. FollowHud uses the Ragnarök frame whenever the Ragnarök HUD is on.");
        (MinimapBackground, MinimapBackgroundOpacity) = Backdrop(config, MinimapSection, "day, clock and weather strips");

        Compass = config.Bind("HUD.Compass", "Enabled", true,
            "Show a compass strip at the top centre of the screen (with either HUD style). It follows the camera.");
        (CompassBackground, CompassBackgroundOpacity) = Backdrop(config, "HUD.Compass", "compass");
        CompassHome = config.Bind("HUD.Compass", "ShowHome", true, "Mark your bed (your spawn point) on the compass, at any distance.");
        CompassPortals = config.Bind("HUD.Compass", "ShowPortals", true, "Mark portals on the compass, the nearest six within PortalRange.");
        CompassPortalRange = config.Bind("HUD.Compass", "PortalRange", 800f, new ConfigDescription(
            "How far away (metres) a portal can be and still show on the compass.", new AcceptableValueRange<float>(100f, 5000f)));
        CompassPlayers = config.Bind("HUD.Compass", "ShowPlayers", true,
            "Mark other players on the compass with their names, only those who chose to be visible on the map.");
        CompassShips = config.Bind("HUD.Compass", "ShowShips", true,
            "Mark ships near you on the compass (not the one you are aboard).");

        SortKeepHotbar = config.Bind("Inventory.Sort", "KeepHotbar", true,
            "Sorting leaves the hotbar rows (the top row, and the second one when HUD.Hotbar SecondRow is on) as they are and sorts the rows below them.");

        const string stack = "Inventory.QuickStack";
        QuickStack = config.Bind(stack, "Enabled", true,
            "The Quick Stack button: puts items into nearby chests that already hold the same kind. A server can turn it off for everyone.");
        KeepWeapons = config.Bind(stack, "KeepWeapons", true,
            "Quick Stack leaves weapons, shields and ammunition in your inventory.");
        KeepConsumables = config.Bind(stack, "KeepConsumables", true,
            "Quick Stack leaves food, meads and other consumables in your inventory.");
        KeepTools = config.Bind(stack, "KeepTools", true,
            "Quick Stack leaves tools (hammer, hoe, cultivator, fishing rod) and torches in your inventory.");

        MigrateFrom03(config);
    }

    // Same backdrop choice as the vitals and status strip.
    private static (ConfigEntry<HudBackground>, ConfigEntry<float>) Backdrop(ConfigFile config, string section, string what) =>
        (config.Bind(section, "Background", HudBackground.Shade,
                $"Backdrop behind the {what}. Shade is a soft feathered field; Plate is a chamfered plate with a gold rim."),
            config.Bind(section, "BackgroundOpacity", 0.5f, new ConfigDescription(
                $"Opacity of the backdrop behind the {what}.", new AcceptableValueRange<float>(0f, 1f))));

    // 0.3.0 kept every HUD setting in [HUD]; carry those values into the new sub-sections once.
    private void MigrateFrom03(ConfigFile config)
    {
        var moves = new (string OldKey, ConfigEntryBase Entry)[]
        {
            ("ShowNumbers", HudNumbers), ("ShowFood", HudFood), ("ShowStatusEffects", HudStatus),
            ("VitalsPosition", Vitals.Position), ("VitalsMarginX", Vitals.MarginX), ("VitalsMarginY", Vitals.MarginY),
            ("StatusPosition", Status.Position), ("StatusMarginX", Status.MarginX), ("StatusMarginY", Status.MarginY)
        };
        if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config, null) is not Dictionary<ConfigDefinition, string> orphans)
            return;

        bool migrated = false;
        foreach ((string oldKey, ConfigEntryBase entry) in moves)
        {
            ConfigDefinition old = new(HudSection, oldKey);
            if (!orphans.TryGetValue(old, out string value)) continue;
            entry.SetSerializedValue(value);
            orphans.Remove(old);
            migrated = true;
        }
        if (migrated) config.Save();
    }
}
