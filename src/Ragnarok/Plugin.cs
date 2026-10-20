using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Ragnarok.Features.NearbyStorage;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
// Soft: loads Better Archery first when present, so its quiver API can be patched and read.
[BepInDependency(Equipment.BetterArcheryCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.kernelpanic.ragnarok";
    public const string PluginName = "Ragnarök";
    public const string PluginVersion = "0.3.0";

    private Harmony? harmony;
    private ContainerRegistry? containers;
    private InterfaceSettings ui = null!;
    private ConfigWindow? window;
    private Theme? theme;
    private RagnarokHud? hud;
    private InventoryPanel? inventoryPanel;
    private InfoPanel? infoPanel;
    private InventoryScale? inventoryScale;
    private ChestPanel? chestPanel;
    private HoverPrompt? hoverPrompt;
    private ChestHoverPreview? chestHoverPreview;
    private RagnarokActionBar? actionBar;
    private BuildTaskPanel? buildTask;
    private BuildMenuPanel? buildMenu;
    private ChestSearch? chestSearch;
    private WeaponDial? weaponDial;
    private VanillaQuickStack? vanillaQuickStack;
    private VanillaChestSort? vanillaChestSort;
    private VanillaCharacterPanel? vanillaCharacter;
    private MinimapInfo? minimapInfo;
    private MinimapSkin? minimapSkin;
    private NotificationFeed? notifications;
    private PopupCommands? popupCommands;
    private Compendium? compendium;
    private SkillsBook? skillsBook;
    private TrophiesBook? trophiesBook;
    private AchievementsBook? achievementsBook;
    private CraftPanel? craftPanel;
    private HotbarRows? hotbarRows;
    private VanillaCraftFilter? vanillaCraftFilter;
    private VanillaProvisionBar? vanillaProvisions;
    private CompassStrip? compass;
    private ServerConfig? serverConfig;
    private StationModifiers? stationModifiers;

    private ConfigEntry<bool> showBadge = null!;
    private ConfigEntry<string> badgeText = null!;
    private ConfigEntry<KeyboardShortcut> reloadConfigKey = null!;
    private ConfigEntry<KeyboardShortcut> toggleBadgeKey = null!;

    private bool disposed;

    private void Awake()
    {
        bool saveOnChange = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        try
        {
            ConfigMigration.Upgrade(Config);
            Initialize();
            Config.Save();
        }
        catch
        {
            enabled = false;
            OnDestroy();
            throw;
        }
        finally { Config.SaveOnConfigSet = saveOnChange; }
    }

    private void Initialize()
    {
        showBadge = Config.Bind("HUD.Development", "ShowBadge", false,
            "Show the development status badge. F8 toggles it.");
        badgeText = Config.Bind("HUD.Development", "BadgeText", "Ragnarök loaded",
            "Text shown in the badge. Edit this file and press F7 to test live configuration.");
        reloadConfigKey = Config.Bind("HUD.Development", "ReloadConfigKey",
            new KeyboardShortcut(KeyCode.F7), "Reload this plugin's configuration.");
        toggleBadgeKey = Config.Bind("HUD.Development", "ToggleBadgeKey",
            new KeyboardShortcut(KeyCode.F8), "Toggle the development status badge.");

        ui = new InterfaceSettings(Config);
        Equipment.CharacterSlots.Enabled = () => ui.Features.CharacterSlots.Value;
        Equipment.CharacterSlots.ExtraUtilityEnabled = () => ui.Features.ExtraUtilitySlots.Value;
        Equipment.ProvisionKeys.Keys = ui.Features.ProvisionKeys;
        PlantHoverStatus.Enabled = () => ui.Features.PlantStatus.Value;
        var gameplay = new GameplaySettings(Config);
        GameplaySettings.Current = gameplay;
        var sailingAndTurrets = new SailingAndTurrets(Config);
        SailingAndTurrets.Current = sailingAndTurrets;
        stationModifiers = new StationModifiers(Config);
        StationModifiers.Current = stationModifiers;
        Equipment.TombstoneRecovery.Enabled = () => ui.Features.AutoEquipOnReclaim.Value;
        StorageRuntime.Settings = new StorageSettings(Config);
        containers = new ContainerRegistry(StorageRuntime.Settings);
        StorageRuntime.Registry = containers;
        serverConfig = new ServerConfig(Config, Logger, PluginVersion, new ConfigEntryBase[]
        {
            StorageRuntime.Settings.Enabled, StorageRuntime.Settings.Range, StorageRuntime.Settings.Crafting,
            StorageRuntime.Settings.Building, StorageRuntime.Settings.Stations, ui.Features.QuickStack, ui.Features.CharacterSlots, ui.Features.ExtraUtilitySlots,
            ui.Features.AutoEquipOnReclaim, gameplay.PowerDuration, gameplay.PowerCooldown, gameplay.HarvestMultiplier,
            gameplay.SkillLossModifier, gameplay.SkillXpModifier,
            gameplay.RunCost, gameplay.AttackCost, gameplay.JumpCost, gameplay.EitrCost,
            ui.Features.ChestSearch, ui.Features.ChestSearchRange
        }.Concat(stationModifiers.Synced).Concat(sailingAndTurrets.Synced));
        // A dedicated server has no screen: it runs the gameplay side and the settings sync only.
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) BuildInterface();
        harmony = new Harmony(PluginGuid + "." + Guid.NewGuid().ToString("N"));
        {
            harmony.PatchAll(typeof(Plugin).Assembly);
            containers.Initialize();
            stationModifiers.Initialize();
#if DEBUG
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-ragnarok-inventory-checks") >= 0)
            {
                StorageRuntimeChecks.Run(Logger);
                StationRuntimeChecks.Run(Logger);
                SkillExperienceChecks.Run(Logger);
            }
            int preview = Array.IndexOf(args, "-ragnarok-ui-preview");
            if (preview >= 0 && preview + 1 < args.Length)
                StartCoroutine(CapturePreview(args[preview + 1]));
            int inventory = Array.IndexOf(args, "-ragnarok-inventory-preview");
            int saves = Array.IndexOf(args, "-ragnarok-preview-saves");
            if (inventory >= 0 && inventory + 1 < args.Length)
            {
                if (saves >= 0 && saves + 1 < args.Length && WorldPreview.Isolate(args[saves + 1]))
                    StartCoroutine(CaptureInventory(args[inventory + 1]));
                else Logger.LogError("Inventory preview needs -ragnarok-preview-saves <dir>; refusing to touch real saves.");
            }
#endif
        }
        Logger.LogInfo($"Ragnarok {PluginVersion} loaded. {ui.WindowKey.Value}: settings; HUD: {ui.Hud.Value}; nearby chests range {StorageRuntime.Settings.Range.Value}m.");
    }

    private void BuildInterface()
    {
        theme = new Theme();
        hud = new RagnarokHud(ui, theme);
        inventoryPanel = new InventoryPanel(ui, theme);
        inventoryScale = new InventoryScale(ui, () => inventoryPanel.Wanted);
        chestPanel = new ChestPanel(ui, theme, () => inventoryPanel.Wanted, () => inventoryPanel.SlotSize);
        hoverPrompt = new HoverPrompt(ui, theme);
        chestHoverPreview = new ChestHoverPreview(ui, theme);
        actionBar = new RagnarokActionBar(ui, theme);
        buildTask = new BuildTaskPanel(ui, theme);
        buildMenu = new BuildMenuPanel(ui, theme);
        chestSearch = new ChestSearch(ui, theme);
        weaponDial = new WeaponDial(ui, theme);
        infoPanel = new InfoPanel(ui, theme, () => inventoryPanel.Wanted);
        vanillaQuickStack = new VanillaQuickStack(ui, theme, inventoryPanel);
        vanillaChestSort = new VanillaChestSort(theme, () => inventoryPanel.Wanted);
        vanillaCharacter = new VanillaCharacterPanel(ui, theme, inventoryPanel);
        minimapInfo = new MinimapInfo(ui, theme);
        minimapSkin = new MinimapSkin(ui, theme);
        notifications = new NotificationFeed(ui, theme);
        popupCommands = new PopupCommands(notifications);
        compendium = new Compendium(ui, theme);
        skillsBook = new SkillsBook(ui, theme);
        trophiesBook = new TrophiesBook(ui, theme);
        achievementsBook = new AchievementsBook(ui, theme);
        craftPanel = new CraftPanel(ui, theme);
        vanillaCraftFilter = new VanillaCraftFilter(ui, theme, () => craftPanel.Wanted);
        // The key guide moves under the Ragnarök crafting panel while it is open.
        hud.KeysDock = () => craftPanel.Shown;
        hotbarRows = new HotbarRows(ui);
        vanillaProvisions = new VanillaProvisionBar(ui, () => hud.Active);
        // The filters apply to Valheim's recipe list whenever a panel shows them.
        CraftFilter.Enabled = () => craftPanel.Wanted || ui.Features.CraftFilters.Value;
        CraftFilter.SortEnabled = () => craftPanel.Wanted || ui.Features.CraftFilters.Value;
        Theme.ChipEdges = () => ui.Edges.Value == FrameEdges.Chipped;
        Ugui.ScrollRows = () => (ui.FollowSystemScroll.Value ? SystemScroll.Lines() : null) ?? ui.ScrollSpeed.Value;
        compass = new CompassStrip(ui, theme);
        // Both inventories draw the slots: the Ragnarök panel, or the vanilla-style character panel.
        window = new ConfigWindow(Config, ui, theme, PluginVersion, ChestsInRange);
        window.AddAction("HUD.Notifications", "Test notifications",
            "Sends a sample unlock and two small messages through Valheim, so they show in the style that is on. In a world only.",
            NotificationFeed.Test);
        VanillaHudPatch.HideVanilla = () => hud.Active;
        VanillaHudPatch.HideStatus = () => hud.Active && ui.HudStatus.Value;
        VanillaHudPatch.HidePower = () => hud.Active && ui.HudPower.Value;
        VanillaHudPatch.HideKeys = () => hud.Active && ui.HudKeys.Value;
    }

    private void Update()
    {
        serverConfig?.Tick();
        stationModifiers?.Tick();
        window?.Update();
        chestSearch?.Update();
        weaponDial?.Update();
        if (UiState.WindowOpen) return;

        if (reloadConfigKey.Value.IsDown())
        {
            try
            {
                Config.Reload();
                Logger.LogInfo("Ragnarok configuration reloaded.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not reload configuration: {exception}");
            }
        }

        Equipment.ProvisionKeys.Update();
        if (ui.StatusLabelsKey.Value.IsDown() && HotbarRows.Playing(Player.m_localPlayer))
            ui.StatusLabels.Value = !ui.StatusLabels.Value;

        if (toggleBadgeKey.Value.IsDown())
            showBadge.Value = !showBadge.Value;
    }

    private void OnGUI()
    {
        // The development badge is the only IMGUI left; Ragnarök UI is built with Unity UI.
        if (showBadge.Value && !UiState.WindowOpen)
            GUI.Box(new Rect(16, 16, 340, 54),
                $"{badgeText.Value}\nv{PluginVersion} | {ui.WindowKey.Value} settings | F6 code | F7 config");
    }

    private void LateUpdate()
    {
        chestSearch?.LateUpdate();
        actionBar?.Tick();
        hoverPrompt?.Tick();
        chestHoverPreview?.Tick();
        Equipment.TombstoneRecovery.Tick();
        hud?.Tick();
        buildTask?.Tick();
        buildMenu?.Tick();
        // Restore native sizing before the guide panels measure their frames.
        if (inventoryPanel?.Wanted == true)
        {
            vanillaQuickStack?.Dispose();
            vanillaChestSort?.Dispose();
            vanillaCharacter?.Dispose();
        }
        inventoryPanel?.Tick();
        infoPanel?.Tick();
        chestPanel?.Tick();
        vanillaQuickStack?.Tick();
        vanillaChestSort?.Tick();
        vanillaCharacter?.Tick();
        inventoryScale?.Tick();
        minimapInfo?.Tick();
        minimapSkin?.Tick();
        notifications?.Tick();
        popupCommands?.Tick();
        compendium?.Tick();
        skillsBook?.Tick();
        trophiesBook?.Tick();
        achievementsBook?.Tick();
        craftPanel?.Tick();
        vanillaCraftFilter?.Tick();
        vanillaProvisions?.Tick();
        compass?.Tick();
    }

    private static string ChestsInRange()
    {
        Player player = Player.m_localPlayer;
        return player && StorageRuntime.Registry != null ? StorageRuntime.Registry.Nearby(player).Count.ToString() : "—";
    }

#if DEBUG
    // Renders the window and HUD on the main menu with sample values, saves screenshots, then quits.
    private IEnumerator CapturePreview(string directory)
    {
        Directory.CreateDirectory(directory);
        yield return new WaitUntil(() => FejdStartup.instance);
        yield return new WaitForSecondsRealtime(5f);
        hud!.Preview = true;
        window!.SetOpen(true);
        yield return new WaitForSecondsRealtime(1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "config-window.png"));
        yield return new WaitForSecondsRealtime(1f);
        window.SelectPage(2);
        yield return new WaitForSecondsRealtime(0.5f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "config-hud-page.png"));
        yield return new WaitForSecondsRealtime(1f);
        window.SetOpen(false);
        yield return new WaitForSecondsRealtime(1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "hud.png"));
        yield return new WaitForSecondsRealtime(1f);
        Logger.LogInfo("Ragnarok UI preview captured.");
        Application.Quit();
    }

    // Loads a throwaway world, opens the inventory with a sample kit, dumps its hierarchy and screenshots it.
    private IEnumerator CaptureInventory(string directory)
    {
        Directory.CreateDirectory(directory);
        yield return WorldPreview.Enter();
        Player player = Player.m_localPlayer;
        WorldPreview.Stock(player);
        hud!.Preview = false;
        InventoryGui.instance.Show(null);
        yield return new WaitForSecondsRealtime(2f);
        WorldPreview.Dump(InventoryGui.instance.transform, Path.Combine(directory, "inventory-tree.txt"));
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "inventory.png"));
        yield return new WaitForSecondsRealtime(1f);
        Logger.LogInfo("Ragnarok inventory preview captured.");
        Application.Quit();
    }
#endif

    private void OnDestroy()
    {
        if (disposed) return;
        disposed = true;
        StopAllCoroutines();
        chestSearch?.Dispose();
        weaponDial?.Dispose();
        PlantHoverStatus.Enabled = () => false;
        actionBar?.Dispose();
        buildTask?.Dispose();
        buildMenu?.Dispose();
        hoverPrompt?.Dispose();
        chestHoverPreview?.Dispose();
        SailingAndTurrets.Current = null;
        foreach (var turret in UnityEngine.Object.FindObjectsByType<Turret>(FindObjectsSortMode.None))
            if (turret.m_marker) turret.m_marker.m_radius = turret.m_viewDistance;
        stationModifiers?.Dispose();
        GameplaySettings.Current = null;
        Equipment.TombstoneRecovery.Reset();
        serverConfig?.Dispose();
        harmony?.UnpatchSelf();
        containers?.Dispose();
        VanillaHudPatch.Restore();
        VanillaHudPatch.HideVanilla = () => false;
        VanillaHudPatch.HideStatus = () => false;
        VanillaHudPatch.HidePower = () => false;
        VanillaHudPatch.HideKeys = () => false;
        Equipment.CharacterSlots.Enabled = () => false;
        Equipment.CharacterSlots.ExtraUtilityEnabled = () => false;
        Equipment.CharacterSlots.ClearEffects();
        UiState.Reset();
        window?.Dispose();
        vanillaQuickStack?.Dispose();
        vanillaChestSort?.Dispose();
        vanillaCharacter?.Dispose();
        minimapInfo?.Dispose();
        minimapSkin?.Dispose();
        popupCommands?.Dispose();
        notifications?.Dispose();
        compendium?.Dispose();
        skillsBook?.Dispose();
        trophiesBook?.Dispose();
        achievementsBook?.Dispose();
        craftPanel?.Dispose();
        hotbarRows?.Dispose();
        vanillaCraftFilter?.Dispose();
        vanillaProvisions?.Dispose();
        CraftFilter.Enabled = () => false;
        CraftFilter.SortEnabled = () => false;
        CraftFilter.Reset();
        if (InventoryGui.instance && Player.m_localPlayer && InventoryGui.IsVisible()) CraftFilter.Changed();
        NotificationFeed.Instance = null;
        compass?.Dispose();
        infoPanel?.Dispose();
        chestPanel?.Dispose();
        inventoryScale?.Dispose();
        inventoryPanel?.Dispose();
        hud?.Dispose();
        theme?.Dispose();
        ResourceScope.Current = null;
        StorageRuntime.Registry = null!;
        Equipment.ProvisionKeys.Keys = Array.Empty<ConfigEntry<KeyboardShortcut>>();
        Theme.ChipEdges = () => false;
        Ugui.ScrollRows = () => 3f;
        Logger.LogInfo("Ragnarok unloaded.");
    }
}
