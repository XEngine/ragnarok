using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Configuration;

// Dotted sections ("HUD.Vitals") appear as sub-pages under their parent in the F10 window.
internal sealed class InterfaceSettings
{
    internal const string HudSection = "HUD", VitalsSection = "HUD.Vitals", StatusSection = "HUD.Status", KeysSection = "HUD.Keys";
    internal const string MinimapSection = "HUD.Minimap";

    internal readonly ConfigEntry<KeyboardShortcut> WindowKey;
    internal readonly ConfigEntry<Jotunn.Managers.InputManager.GamepadButton> WindowGamepad;
    internal readonly ConfigEntry<float> Scale;
    internal readonly ConfigEntry<float> ScrollSpeed;
    internal readonly ConfigEntry<FrameEdges> Edges;
    internal readonly ConfigEntry<bool> FollowSystemScroll;
    internal readonly ConfigEntry<HudStyle> Hud;
    internal readonly ConfigEntry<bool> CrosshairAmmo;
    internal readonly ConfigEntry<float> HudScale;
    internal readonly ConfigEntry<bool> HudNumbers;
    internal readonly ConfigEntry<bool> HudFood;
    internal readonly ConfigEntry<bool> HudPower;
    internal readonly ConfigEntry<bool> HudStatus;
    internal readonly HudPlacement Character, Power, SkillToast;
    internal readonly ConfigEntry<bool> CharacterPortrait, PortraitHelmet;
    internal readonly ConfigEntry<bool> Party;
    internal readonly HudPlacement Vitals;
    internal readonly HudPlacement Status;
    internal readonly ConfigEntry<bool> HudKeys;
    internal readonly HudPlacement Keys;
    internal readonly ConfigEntry<InventoryStyle> Inventory;
    internal readonly ConfigEntry<float> InventoryOpacity;
    internal readonly ConfigEntry<float> InventoryScale;
    internal readonly ConfigEntry<int> BuildColumns, BuildRows;
    internal readonly ConfigEntry<bool> BuildOnlyAvailable, BuildCountNearby;
    internal readonly ConfigEntry<KeyboardShortcut> BuildPreviousCategory, BuildNextCategory, BuildFavoriteKey, BuildSearchKey;
    internal readonly ConfigEntry<bool> ChestPreview;
    internal readonly ConfigEntry<float> ChestPreviewX, ChestPreviewY, ChestPreviewScale;
    internal readonly ConfigEntry<int> ChestPreviewColumns;
    internal readonly ConfigEntry<bool> ActionBar;
    internal readonly ConfigEntry<PanelStyle> WeaponDialStyle;
    internal readonly ConfigEntry<float> ActionBarScale, ActionBarX, ActionBarBottom;
    internal readonly ConfigEntry<float> ActionBarSpacing, ActionBarPaddingX, ActionBarPaddingY;
    internal readonly ConfigEntry<bool> ProvisionBar;
    internal readonly ConfigEntry<ProvisionPlacement> VanillaProvisionPlacement;
    internal readonly ConfigEntry<bool> MinimapInfo;
    internal readonly ConfigEntry<bool> TamedAnimalDots;
    internal readonly ConfigEntry<bool> Clock24h;
    internal readonly ConfigEntry<PanelStyle> MinimapStyle;
    internal readonly ConfigEntry<bool> ChestSearchMapMarker;
    internal readonly ConfigEntry<PanelStyle> NotificationStyle;
    internal readonly ConfigEntry<PanelStyle> CompendiumStyle;
    internal readonly ConfigEntry<PanelStyle> SkillsStyle;
    internal readonly ConfigEntry<PanelStyle> TrophiesStyle;
    internal readonly ConfigEntry<PanelStyle> AchievementsStyle;
    internal readonly ConfigEntry<PanelStyle> CraftingStyle;
    internal readonly HudPlacement Notifications;
    internal readonly ConfigEntry<bool> Compass, CompassHome, CompassPortals, CompassShips, CompassPlayers;
    internal readonly ConfigEntry<float> CompassPortalRange;
    internal readonly ConfigEntry<HudBackground> CompassBackground, MinimapBackground;
    internal readonly ConfigEntry<float> CompassBackgroundOpacity, MinimapBackgroundOpacity;

    internal readonly FeatureSettings Features;

    internal InterfaceSettings(ConfigFile config)
    {
        Features = new FeatureSettings(config);
        WeaponDialStyle = config.Bind("HUD.WeaponDial", "Style", PanelStyle.FollowHud,
            "Ironbound: obsidian and crimson with a gold halo. Vanilla: dark glass with Valheim's orange highlight and font. FollowHud matches the HUD style.");
        const string actionBar = "HUD.Hotbar";
        ActionBar = config.Bind(actionBar, "Enabled", true, "Use the Ironbound action-bar panel while the Ironbound HUD is selected. Vanilla HUD keeps the original hotbar.");
        ActionBarScale = config.Bind(actionBar, "Scale", 1f, new ConfigDescription("Size of the action-bar panel.", new AcceptableValueRange<float>(0.5f, 1.5f)));
        ActionBarX = config.Bind(actionBar, "OffsetX", 0f, new ConfigDescription("Horizontal offset from bottom centre, in HUD pixels.", new AcceptableValueRange<float>(-1000f, 1000f)));
        ActionBarBottom = config.Bind(actionBar, "BottomMargin", 24f, new ConfigDescription("Distance above the bottom edge, in HUD pixels.", new AcceptableValueRange<float>(0f, 1000f)));
        ActionBarSpacing = config.Bind(actionBar, "SlotSpacing", 4f, new ConfigDescription("Space between hotbar slots, in unscaled HUD pixels.", new AcceptableValueRange<float>(0f, 30f)));
        ActionBarPaddingX = config.Bind(actionBar, "HorizontalPadding", 8f, new ConfigDescription("Inner left and right margins of the hotbar background, in unscaled HUD pixels.", new AcceptableValueRange<float>(2f, 40f)));
        ActionBarPaddingY = config.Bind(actionBar, "VerticalPadding", 10f, new ConfigDescription("Inner top and bottom margins of the hotbar background. Allows room for the raised equipped slot.", new AcceptableValueRange<float>(6f, 24f)));
        const string preview = "HUD.ChestPreview";
        ChestPreview = config.Bind(preview, "Enabled", true, "Show a read-only item grid when aiming at an accessible chest within normal interaction range.");
        ChestPreviewX = config.Bind(preview, "OffsetX", 80f, new ConfigDescription("Horizontal offset from screen centre; positive moves right, in HUD pixels. With the Ironbound HUD the action prompt (\"[E] Open\") shares this left edge.", new AcceptableValueRange<float>(-2000f, 2000f)));
        ChestPreviewY = config.Bind(preview, "OffsetY", 80f, new ConfigDescription("Vertical offset from screen centre; positive moves down, in HUD pixels.", new AcceptableValueRange<float>(-1200f, 1200f)));
        ChestPreviewScale = config.Bind(preview, "Scale", 1f, new ConfigDescription("Size of the compact chest preview.", new AcceptableValueRange<float>(0.5f, 2f)));
        ChestPreviewColumns = config.Bind(preview, "Columns", 6, new ConfigDescription("Item columns in the compact preview. Empty inventory slots are omitted.", new AcceptableValueRange<int>(1, 12)));
        WindowKey = config.Bind("HUD.Window", "ConfigWindowKey", new KeyboardShortcut(KeyCode.F10),
            "Open or close the Ironbound configuration window.");
        WindowGamepad = config.Bind("HUD.Window", "GamepadButton", Jotunn.Managers.InputManager.GamepadButton.None,
            "Optional controller button to toggle settings. None leaves controller input unchanged. Choose a button not used by your other mods.");
        Scale = config.Bind("HUD.Window", "Scale", 1f, new ConfigDescription(
            "Size of Ironbound windows relative to a 1080p screen.", new AcceptableValueRange<float>(0.75f, 1.5f)));
        Edges = config.Bind("HUD.Window", "FrameEdges", FrameEdges.Solid,
            "Edges of Ironbound's frames, bars, slots and buttons: Solid (clean square edges) or Chipped (worn, jagged metal).");
        FollowSystemScroll = config.Bind("HUD.Window", "FollowWindowsScroll", true,
            "Scroll lists as far per mouse-wheel notch as Windows does (Mouse settings: lines to scroll each time). " +
            "Off, or when there is no Windows setting, ScrollSpeed is used.");
        ScrollSpeed = config.Bind("HUD.Window", "ScrollSpeed", 3f, new ConfigDescription(
            "How far one mouse-wheel notch scrolls Ironbound's lists (crafting, Compendium, this window), in rows of about " +
            "40 pixels. Also applies to Valheim's recipe list while the Ironbound crafting filters are on.",
            new AcceptableValueRange<float>(1f, 8f)));

        Hud = config.Bind(HudSection, "Style", HudStyle.Vanilla,
            "Vanilla keeps Valheim's HUD. Ironbound replaces health, stamina, eitr, food and status effects with the Ironbound HUD.");
        CrosshairAmmo = config.Bind(HudSection, "CrosshairAmmo", true,
            "Show the selected arrow/bolt icon and carried count below the crosshair while a bow or crossbow is equipped. Includes quiver stacks. Works with either HUD style; local display only.");
        HudScale = config.Bind(HudSection, "Scale", 1f, new ConfigDescription(
            "Size of every part of the Ironbound HUD.", new AcceptableValueRange<float>(0.6f, 1.6f)));

        HudNumbers = config.Bind(VitalsSection, "ShowNumbers", true, "Show current and maximum values beside the bars.");
        HudFood = config.Bind(VitalsSection, "ShowFood", true, "Show active foods and their remaining time beside Forsaken power.");
        HudPower = config.Bind(VitalsSection, "ShowForsakenPower", true,
            "Show your Forsaken power as the emblem beside the bars, replacing Valheim's power box.");
        Vitals = new HudPlacement(config, VitalsSection, "health, stamina and eitr", ScreenAnchor.BottomCenter, 0f, 118f);

        NotificationStyle = config.Bind("HUD.Notifications", "Style", PanelStyle.FollowHud,
            "Ironbound shows animated pickup text above the vitals, recipe milestones beside the minimap and a separate feed for other messages; " +
            "Vanilla keeps Valheim's. FollowHud uses the Ironbound notifications whenever the Ironbound HUD is on. " +
            "Includes compact skill pills, milestone banners and outlined centre messages.");
        Notifications = new HudPlacement(config, "HUD.Notifications", "notifications", ScreenAnchor.BottomLeft, 40f, 140f);

        CraftingStyle = config.Bind("HUD.Crafting", "Style", PanelStyle.FollowHud,
            "Ironbound shows the crafting panel in Ironbound style with a search box and category filters; Vanilla keeps " +
            "Valheim's. FollowHud uses the Ironbound panel whenever the Ironbound HUD is on. Crafting itself is Valheim's either way.");


        CompendiumStyle = config.Bind("HUD.Compendium", "Style", PanelStyle.FollowHud,
            "Ironbound shows the Compendium (the texts dialog: journal, Hugin's lessons, runestones) as a Ironbound screen with " +
            "chapters and an art panel; Vanilla keeps Valheim's. FollowHud uses the Ironbound screen whenever the Ironbound HUD is on.");
        SkillsStyle = config.Bind("HUD.Skills", "Style", PanelStyle.FollowHud,
            "Ironbound shows the skills dialog as a Ironbound screen in the Compendium's frame (every skill with its level, the chosen " +
            "one's bonus, progress and description); Vanilla keeps Valheim's. FollowHud uses the Ironbound screen whenever the Ironbound HUD is on.");
        TrophiesStyle = config.Bind("HUD.Trophies", "Style", PanelStyle.FollowHud,
            "Ironbound shows the trophies as a Ironbound screen in the Compendium's frame (every trophy in the game, the ones you have taken lit, " +
            "the chosen one's lore beside them); Vanilla keeps Valheim's. FollowHud uses the Ironbound screen whenever the Ironbound HUD is on.");
        AchievementsStyle = config.Bind("HUD.Achievements", "Style", PanelStyle.FollowHud,
            "Ironbound shows the achievements as a Ironbound screen in the Compendium's frame (every badge, the unlocked ones lit, the chosen " +
            "one's conditions beside them); Vanilla keeps Valheim's. FollowHud uses the Ironbound screen whenever the Ironbound HUD is on.");

        HudStatus = config.Bind(StatusSection, "Enabled", true,
            "Show buffs and debuffs in the Ironbound status strip. Off brings back Valheim's top-right list.");
        Status = new HudPlacement(config, StatusSection, "status effect strip", ScreenAnchor.TopLeft, 150f, 72f);

        HudKeys = config.Bind(KeysSection, "Enabled", true,
            "Show the context key guide (building, combat, inventory, fishing) as a compact vertical list. Off brings back Valheim's bottom bar.");
        Keys = new HudPlacement(config, KeysSection, "key guide", ScreenAnchor.MiddleRight, 24f, 0f);

        Inventory = config.Bind("HUD.Inventory", "Style", InventoryStyle.FollowHud,
            "Ironbound shows the Ironbound grid and item card; Vanilla uses Valheim's wood, native slots and tooltips with the same categories, Quick Stack and character dashboard. FollowHud uses the Ironbound inventory " +
            "whenever the Ironbound HUD is on. Clicks, Shift-split, Ctrl-move and right-click use work as in Valheim.");
        InventoryScale = config.Bind("HUD.Inventory", "Scale", 1f, new ConfigDescription(
            "Size of the Ironbound inventory and its character column, growing from the top-left into the free space " +
            "before the info board and crafting panel. Vanilla uses its native size and shrinks only as needed to fit the new panels. " +
            "1 is Valheim's own size.", new AcceptableValueRange<float>(0.75f, 1.3f)));
        InventoryOpacity = config.Bind("HUD.Inventory", "BackgroundOpacity", 0.95f, new ConfigDescription(
            "Smoked-glass opacity of the Ironbound inventory, crafting, chest and character panels.", new AcceptableValueRange<float>(0.2f, 1f)));
        BuildColumns = config.Bind("HUD.BuildMenu", "Columns", 5, new ConfigDescription(
            "Number of build-piece columns in the Ironbound picker. Also adjustable below the grid.", new AcceptableValueRange<int>(4, 16)));
        BuildRows = config.Bind("HUD.BuildMenu", "Rows", 4, new ConfigDescription(
            "Number of fully visible build-piece rows in the Ironbound picker. Also adjustable below the grid.", new AcceptableValueRange<int>(3, 10)));
        BuildOnlyAvailable = config.Bind("HUD.BuildMenu", "OnlyBuildable", false, "Show only pieces the native game currently considers buildable.");
        BuildCountNearby = config.Bind("HUD.BuildMenu", "CountNearbyChests", true,
            "Include permitted nearby building stock in preview counts. Server building-storage rules still control actual resource use.");
        BuildPreviousCategory = config.Bind("HUD.BuildMenu", "PreviousCategoryKey", new KeyboardShortcut(KeyCode.Q), "Previous category while the build menu is open.");
        BuildNextCategory = config.Bind("HUD.BuildMenu", "NextCategoryKey", new KeyboardShortcut(KeyCode.E), "Next category while the build menu is open.");
        BuildFavoriteKey = config.Bind("HUD.BuildMenu", "FavoriteKey", new KeyboardShortcut(KeyCode.F), "Toggle the inspected piece's native favorite while the build menu is open.");
        BuildSearchKey = config.Bind("HUD.BuildMenu", "SearchKey", new KeyboardShortcut(KeyCode.Slash), "Focus build-piece search. The slash character is also recognized on non-US keyboard layouts.");


        ProvisionBar = config.Bind("HUD.Provisions", "ShowInHud", true,
            "Show the carried provision shortcut row on the vanilla HUD. The Ironbound HUD shows active food beside Forsaken power.");
        VanillaProvisionPlacement = config.Bind("HUD.Provisions", "VanillaPlacement", ProvisionPlacement.BesidePower,
            "Where the provision row sits on the vanilla HUD. BesidePower: next to the Forsaken power icon, at its size. AfterHotbar: in the top row after hotbar slot 8.");
        MinimapInfo = config.Bind(MinimapSection, "Enabled", true,
            "Show the day and clock above the minimap, and the current and next weather below it (with either HUD style).");
        Clock24h = config.Bind(MinimapSection, "TwentyFourHourClock", true, "24-hour clock (14:30). Off: 12-hour (2:30 PM).");
        TamedAnimalDots = config.Bind(MinimapSection, "TamedAnimalDots", true,
            "Show small yellow dots for loaded, living tamed animals in explored areas. Tamed animals are shared; Valheim does not assign a permanent player owner. Local display only; dots disappear when animals die or unload.");
        ChestSearchMapMarker = config.Bind("QualityOfLife.NearbyChests", "ChestSearchMarker", false,
            "Mark the selected chest-search result on the map for the existing 20-second guide. Respects fog, access and server range limits. Local display only.");
        MinimapStyle = config.Bind(MinimapSection, "Style", PanelStyle.FollowHud,
            "Ironbound frames the minimap in Ironbound style (dark plate, gold rim, crimson north mark) and restyles the biome " +
            "name; Vanilla keeps Valheim's frame. FollowHud uses the Ironbound frame whenever the Ironbound HUD is on.");
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



        CharacterPortrait = config.Bind("HUD.Character", "ShowPortrait", true,
            "Show a front-facing portrait of your character inside the rune circle. Disable to use the rune glyph.");
        PortraitHelmet = config.Bind("HUD.Character", "ShowHelmet", true,
            "Show the equipped helmet in the character portrait. Turn off to show the face and normal hair. Only affects the portrait.");
        Character = new HudPlacement(config, "HUD.Character", "character and food panel", ScreenAnchor.TopLeft, 40f, 34f);
        Power = new HudPlacement(config, "HUD.Power", "Forsaken power", ScreenAnchor.BottomLeft, 40f, 34f);
        SkillToast = new HudPlacement(config, "HUD.SkillToast", "skill improved toast (default docks above the health/vitals panel)", ScreenAnchor.BottomCenter, 0f, 228f);
        Party = config.Bind("HUD.Character", "ShowPlayers", true,
            "Show up to three players sharing their map position. Health is shown only for loaded characters; no party membership is inferred.");
        MigrateFrom03(config);
        var layoutVersion = config.Bind("HUD", "LayoutVersion", 0, "Internal layout migration version.");
        if (layoutVersion.Value < 1)
        {
            // Only relocate the old defaults; retain custom placements.
            MigratePlacement(Vitals, ScreenAnchor.BottomLeft, 40, 40);
            MigratePlacement(Status, ScreenAnchor.BottomRight, 40, 40);
            MigratePlacement(Notifications, ScreenAnchor.MiddleLeft, 40, 0);
            layoutVersion.Value = 1;
        }
        if (layoutVersion.Value < 2)
        {
            layoutVersion.Value = 2;
        }
        if (layoutVersion.Value < 3)
        {
            MigratePlacement(Vitals, ScreenAnchor.BottomCenter, 0, 144);
            layoutVersion.Value = 3;
        }
        if (layoutVersion.Value < 4)
        {
            if (ActionBarSpacing.Value == 7f) ActionBarSpacing.Value = 4f;
            layoutVersion.Value = 4;
        }
        if (layoutVersion.Value < 5)
        {
            MigratePlacement(SkillToast, ScreenAnchor.MiddleRight, 40, -190);
            layoutVersion.Value = 5;
        }
        if (layoutVersion.Value < 6)
        {
            if (Mathf.Approximately(InventoryOpacity.Value, 0.95f))
                InventoryOpacity.Value = Palette.HudGlassOpacity;
            layoutVersion.Value = 6;
        }
        if (layoutVersion.Value < 7)
        {
            if (Mathf.Approximately(InventoryOpacity.Value, Palette.HudGlassOpacity))
                InventoryOpacity.Value = 0.42f;
            layoutVersion.Value = 7;
        }
        if (layoutVersion.Value < 8)
        {
            if (Mathf.Approximately(InventoryOpacity.Value, 0.42f))
                InventoryOpacity.Value = 0.72f;
            layoutVersion.Value = 8;
        }
        if (layoutVersion.Value < 9)
        {
            if (Mathf.Approximately(InventoryOpacity.Value, 0.72f))
                InventoryOpacity.Value = (float)InventoryOpacity.DefaultValue;
            layoutVersion.Value = 9;
        }
        if (layoutVersion.Value < 10)
        {
            if (Mathf.Approximately(InventoryOpacity.Value, 0.85f))
                InventoryOpacity.Value = (float)InventoryOpacity.DefaultValue;
            layoutVersion.Value = 10;
        }
        if (layoutVersion.Value < 11)
        {
            if (BuildColumns.Value == 12 && BuildRows.Value == 6) { BuildColumns.Value = 5; BuildRows.Value = 4; }
            layoutVersion.Value = 11;
        }
        if (layoutVersion.Value < 12)
        {
            if (Status.Position.Value == ScreenAnchor.TopLeft && Status.MarginX.Value == 150f)
                Status.MarginX.Value = 0;
            layoutVersion.Value = 12;
        }
        if (layoutVersion.Value < 13)
        {
            // Align the strip with the name (character x=40 + name x=110),
            // rather than the screen edge introduced by layout version 12.
            if (Status.Position.Value == ScreenAnchor.TopLeft && Status.MarginX.Value == 0f)
                Status.MarginX.Value = 150;
            layoutVersion.Value = 13;
        }
    }

    private static void MigratePlacement(HudPlacement placement, ScreenAnchor old, float x, float y)
    {
        if (placement.Position.Value != old || placement.MarginX.Value != x || placement.MarginY.Value != y) return;
        placement.Position.Value = (ScreenAnchor)placement.Position.DefaultValue;
        placement.MarginX.Value = (float)placement.MarginX.DefaultValue;
        placement.MarginY.Value = (float)placement.MarginY.DefaultValue;
    }

    // Retain old local keys without rendering or exposing the removed backdrop feature.
    private static (ConfigEntry<HudBackground>, ConfigEntry<float>) Backdrop(ConfigFile config, string section, string what) =>
        (config.Bind(section, "Background", HudBackground.None,
                "Legacy backdrop setting, retained for compatibility. HUD backdrops have been removed."),
            config.Bind(section, "BackgroundOpacity", 0.5f, new ConfigDescription(
                "Legacy backdrop opacity; ignored.", new AcceptableValueRange<float>(0f, 1f))));

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

        foreach ((string oldKey, ConfigEntryBase entry) in moves)
        {
            ConfigDefinition old = new(HudSection, oldKey);
            if (!orphans.TryGetValue(old, out string value)) continue;
            entry.SetSerializedValue(value);
            orphans.Remove(old);
        }
    }
}
