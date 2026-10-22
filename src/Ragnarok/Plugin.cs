using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Ragnarok.Features.NearbyStorage;
using Ragnarok.Features.Mounts;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
// Soft: loads Better Archery first when present, so its quiver API can be patched and read.
[BepInDependency(Equipment.BetterArcheryCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(JotunnIntegration.Guid, JotunnIntegration.MinimumVersion)]
#if !DEBUG
// ScriptEngine's eager Cecil reader cannot resolve enum attributes from nested plugins.
// Jotunn discovers release plugins at startup; development instances use our config handshake.
[Jotunn.Utils.NetworkCompatibility(Jotunn.Utils.CompatibilityLevel.VersionCheckOnly, Jotunn.Utils.VersionStrictness.Minor)]
#endif
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.kernelpanic.ragnarok";
    public const string PluginName = "Ragnarök";
    public const string PluginVersion = "0.5.5";

    private Harmony? harmony;
    private JotunnIntegration? jotunn;
    private UiLocalization? localization;
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
    private CrosshairAmmo? crosshairAmmo;
    private MountHudLayout? mountHud;
    private CombatIndicator? combatIndicator;
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
    private LargeMapLayer? largeMapLayer;
    private TamedAnimalMarkers? tamedAnimals;
    private NotificationFeed? notifications;
    private PopupCommands? popupCommands;
    private Compendium? compendium;
    private SkillsBook? skillsBook;
    private TrophiesBook? trophiesBook;
    private AchievementsBook? achievementsBook;
    private CraftPanel? craftPanel;
    private VanillaCraftFilter? vanillaCraftFilter;
    private VanillaProvisionBar? vanillaProvisions;
    private CompassStrip? compass;
    private ServerConfig? serverConfig;
    private MountService? mounts;
    private MountClient? mountClient;
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
        JotunnIntegration.Require();
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
        Equipment.BetterArcheryQuiverSlotsPatch.ShownByRagnarok = () => InventoryPanel.LayoutOf(ui) > 0;
        StorageRuntime.Settings = new StorageSettings(Config);
        containers = new ContainerRegistry(StorageRuntime.Settings);
        StorageRuntime.Registry = containers;
        mounts = new MountService(new MountSettings(Config), Logger);
        serverConfig = new ServerConfig(Config, Logger, PluginVersion, new ConfigEntryBase[]
        {
            StorageRuntime.Settings.Enabled, StorageRuntime.Settings.Range, ui.Features.CharacterSlots, ui.Features.ExtraUtilitySlots,
            ui.Features.AutoEquipOnReclaim, gameplay.PowerDuration, gameplay.PowerCooldown, gameplay.HarvestMultiplier,
            gameplay.SkillLossModifier, gameplay.SkillXpModifier,
            gameplay.RunCost, gameplay.AttackCost, gameplay.JumpCost, gameplay.EitrCost
        }.Concat(stationModifiers.Synced).Concat(sailingAndTurrets.Synced).Concat(mounts.Settings.Synced), new ConfigEntryBase[]
        {
            StorageRuntime.Settings.Enabled, StorageRuntime.Settings.Range, ui.Features.AutoEquipOnReclaim
        }.Concat(mounts.Settings.Synced));
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
                PolicyRuntimeChecks.Run(Logger);
                if (theme != null) UiLifecycleRuntimeChecks.Run(theme, ui, Logger);
            }
            int preview = Array.IndexOf(args, "-ragnarok-ui-preview");
            int mountChecks = Array.IndexOf(args, "-ragnarok-mount-checks");
            if (mountChecks >= 0 && mountChecks + 1 < args.Length)
            {
                string directory = Path.GetFullPath(args[mountChecks + 1]);
                if (!WorldPreview.Isolate(Path.Combine(directory, "saves")))
                    throw new InvalidOperationException("Mount checks require isolated throwaway saves.");
                StartCoroutine(MountRuntimeChecks.Run(directory));
            }
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
        localization = new UiLocalization(Info.Metadata);
        jotunn = JotunnIntegration.Create(Logger);
        mountClient = new MountClient(mounts!, jotunn);
        theme = new Theme();
        hud = new RagnarokHud(ui, theme);
        inventoryPanel = new InventoryPanel(ui, theme);
        inventoryScale = new InventoryScale(ui, () => inventoryPanel.Wanted);
        chestPanel = new ChestPanel(ui, theme, () => inventoryPanel.Wanted, () => inventoryPanel.SlotSize);
        hoverPrompt = new HoverPrompt(ui, theme);
        crosshairAmmo = new CrosshairAmmo(ui, theme);
        mountHud = new MountHudLayout(ui, theme);
        combatIndicator = new CombatIndicator(ui, theme);
        chestHoverPreview = new ChestHoverPreview(ui, theme);
        actionBar = new RagnarokActionBar(ui, theme);
        buildTask = new BuildTaskPanel(ui, theme);
        buildMenu = new BuildMenuPanel(ui, theme);
        chestSearch = new ChestSearch(ui, theme);
        weaponDial = new WeaponDial(ui, theme, jotunn);
        infoPanel = new InfoPanel(ui, theme, () => inventoryPanel.Wanted);
        vanillaQuickStack = new VanillaQuickStack(ui, theme, inventoryPanel);
        vanillaChestSort = new VanillaChestSort(theme, () => inventoryPanel.Wanted);
        vanillaCharacter = new VanillaCharacterPanel(ui, theme, inventoryPanel);
        minimapInfo = new MinimapInfo(ui, theme);
        minimapSkin = new MinimapSkin(ui, theme);
        largeMapLayer = new LargeMapLayer();
        tamedAnimals = new TamedAnimalMarkers(ui);
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
        vanillaProvisions = new VanillaProvisionBar(ui, () => hud.Active);
        // The filters apply to Valheim's recipe list whenever a panel shows them.
        CraftFilter.Enabled = () => craftPanel.Wanted || ui.Features.CraftFilters.Value;
        CraftFilter.SortEnabled = () => craftPanel.Wanted || ui.Features.CraftFilters.Value;
        Theme.ChipEdges = () => ui.Edges.Value == FrameEdges.Chipped;
        Ugui.ScrollRows = () => (ui.FollowSystemScroll.Value ? SystemScroll.Lines() : null) ?? ui.ScrollSpeed.Value;
        compass = new CompassStrip(ui, theme);
        // Both inventories draw the slots: the Ragnarök panel, or the vanilla-style character panel.
        window = new ConfigWindow(Config, ui, theme, PluginVersion, ChestsInRange, jotunn);
        if (jotunn != null) jotunn.GuiRecreated = () => window?.SetOpen(false);
        window.AddAction("HUD.Notifications", "Test notifications",
            "Sends a sample unlock and two small messages through Valheim, so they show in the style that is on. In a world only.",
            NotificationFeed.Test);
        window.AddAction("QualityOfLife.Mounts", "Release mount bond",
            "Release your bond without approaching the mount. Its saddle and position are preserved.",
            () => mounts?.Send(2, ZDOID.None));
        window.AddStatus("QualityOfLife.Mounts", () => mounts!.Available ? mounts.CompanionSummary : "Waiting for a Ragnarok mount server, or enter a local world.");
        VanillaHudPatch.HideVanilla = () => hud.Active;
        VanillaHudPatch.HideStatus = () => hud.Active && ui.HudStatus.Value;
        VanillaHudPatch.HidePower = () => hud.Active && ui.HudPower.Value;
        VanillaHudPatch.HideKeys = () => hud.Active && ui.HudKeys.Value;
    }

    private void Update()
    {
        serverConfig?.Tick();
        mounts?.Tick();
        mountClient?.Tick();
        stationModifiers?.Tick();
        window?.Update();
        chestSearch?.Update();
        weaponDial?.Update();
        jotunn?.Tick();
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
        mountClient?.LateUpdate();
        // Slot persistence and equipment effects must not depend on a UI instance.
        if (Player.m_localPlayer)
        {
            if (Equipment.CharacterSlots.Enabled())
                Equipment.CharacterSlots.Settle(Player.m_localPlayer,
                    InventoryGui.instance ? Equipment.SlotActions.DragItem(InventoryGui.instance) : null);
            else Equipment.CharacterSlots.Empty(Player.m_localPlayer.GetInventory());
            Equipment.CharacterSlots.SyncEffects(Player.m_localPlayer);
        }
        chestSearch?.LateUpdate();
        actionBar?.Tick();
        hoverPrompt?.Tick();
        crosshairAmmo?.Tick();
        chestHoverPreview?.Tick();
        Equipment.TombstoneRecovery.Tick();
        combatIndicator?.Tick();
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
        largeMapLayer?.Tick();
        tamedAnimals?.Tick();
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
        // Place after vitals/build cards have settled for this frame.
        mountHud?.Tick();
        // UI focus can change after Update; share the resulting state before the next frame.
        jotunn?.Tick();
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
        void Release(string name, Action release) => UiLifetime.Cleanup(release,
            exception => Logger.LogError($"Ragnarok cleanup failed ({name}): {exception}"));
        // Release input and patches before UI cleanup: a broken renderer must not
        // leave its game-input/cursor hooks active after unloading.
        UiState.Reset();
        WeaponDial.Open = false;
        Release("input integration", () => jotunn?.Dispose());
        Release("patches", () => harmony?.UnpatchSelf());
        Release("coroutines", StopAllCoroutines);
        Release("mount controls", () => mountClient?.Dispose());
        Release("mount service", () => mounts?.Dispose());
        Release("chestSearch", () => chestSearch?.Dispose());
        Release("weaponDial", () => weaponDial?.Dispose());
        PlantHoverStatus.Enabled = () => false;
        Release("actionBar", () => actionBar?.Dispose());
        Release("buildTask", () => buildTask?.Dispose());
        Release("buildMenu", () => buildMenu?.Dispose());
        Release("hoverPrompt", () => hoverPrompt?.Dispose());
        Release("crosshairAmmo", () => crosshairAmmo?.Dispose());
        Release("mount HUD layout", () => mountHud?.Dispose());
        Release("combat indicator", () => combatIndicator?.Dispose());
        Release("chestHoverPreview", () => chestHoverPreview?.Dispose());
        SailingAndTurrets.Current = null;
        Release("turret markers", () =>
        {
            foreach (var turret in UnityEngine.Object.FindObjectsByType<Turret>(FindObjectsSortMode.None))
                if (turret.m_marker) turret.m_marker.m_radius = turret.m_viewDistance;
        });
        Release("stationModifiers", () => stationModifiers?.Dispose());
        GameplaySettings.Current = null;
        Release("Equipment.TombstoneRecovery.Reset", () => Equipment.TombstoneRecovery.Reset());
        Release("serverConfig", () => serverConfig?.Dispose());
        Release("containers", () => containers?.Dispose());
        Release("VanillaHudPatch.Restore", () => VanillaHudPatch.Restore());
        VanillaHudPatch.HideVanilla = () => false;
        VanillaHudPatch.HideStatus = () => false;
        VanillaHudPatch.HidePower = () => false;
        VanillaHudPatch.HideKeys = () => false;
        Equipment.CharacterSlots.Enabled = () => false;
        Equipment.CharacterSlots.ExtraUtilityEnabled = () => false;
        Release("Equipment.CharacterSlots.ClearEffects", () => Equipment.CharacterSlots.ClearEffects());
        UiState.Reset();
        Release("window", () => window?.Dispose());
        Release("vanillaQuickStack", () => vanillaQuickStack?.Dispose());
        Release("vanillaChestSort", () => vanillaChestSort?.Dispose());
        Release("vanillaCharacter", () => vanillaCharacter?.Dispose());
        Release("minimapInfo", () => minimapInfo?.Dispose());
        Release("minimapSkin", () => minimapSkin?.Dispose());
        Release("largeMapLayer", () => largeMapLayer?.Dispose());
        Release("tamedAnimals", () => tamedAnimals?.Dispose());
        Release("popupCommands", () => popupCommands?.Dispose());
        Release("notifications", () => notifications?.Dispose());
        Release("compendium", () => compendium?.Dispose());
        Release("skillsBook", () => skillsBook?.Dispose());
        Release("trophiesBook", () => trophiesBook?.Dispose());
        Release("achievementsBook", () => achievementsBook?.Dispose());
        Release("craftPanel", () => craftPanel?.Dispose());
        Release("vanillaCraftFilter", () => vanillaCraftFilter?.Dispose());
        Release("CraftAmount.Unload", () => CraftAmount.Unload());
        Release("vanillaProvisions", () => vanillaProvisions?.Dispose());
        CraftFilter.Enabled = () => false;
        CraftFilter.SortEnabled = () => false;
        Release("CraftFilter.Reset", () => CraftFilter.Reset());
        Release("crafting refresh", () => { if (InventoryGui.instance && Player.m_localPlayer && InventoryGui.IsVisible()) CraftFilter.Changed(); });
        NotificationFeed.Instance = null;
        Release("compass", () => compass?.Dispose());
        Release("infoPanel", () => infoPanel?.Dispose());
        Release("chestPanel", () => chestPanel?.Dispose());
        Release("inventoryScale", () => inventoryScale?.Dispose());
        Release("inventoryPanel", () => inventoryPanel?.Dispose());
        Release("hud", () => hud?.Dispose());
        Release("theme", () => theme?.Dispose());
        Release("localization", () => localization?.Dispose());
        ResourceScope.Current = null;
        Release("CraftingChestGate.Reset", () => CraftingChestGate.Reset());
        StorageRuntime.Registry = null!;
        Equipment.ProvisionKeys.Keys = Array.Empty<ConfigEntry<KeyboardShortcut>>();
        Equipment.BetterArcheryQuiverSlotsPatch.ShownByRagnarok = () => false;
        Release("Features.QualityOfLife.ItemTrash.Reset", () => Features.QualityOfLife.ItemTrash.Reset());
        Theme.ChipEdges = () => false;
        Ugui.ScrollRows = () => 3f;
        Logger.LogInfo("Ragnarok unloaded.");
    }
}
