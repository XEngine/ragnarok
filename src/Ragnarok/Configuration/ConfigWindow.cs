using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ragnarok.Configuration;

// Dashboard-style configuration window built with Unity UI. Pages come from the plugin's
// ConfigFile, so any new Config.Bind section appears without extra UI code.
internal sealed class ConfigWindow : IDisposable
{
    private const float Width = 1120, Height = 716, HeaderHeight = 120, SidebarWidth = 248, FooterHeight = 56;
    private const float RowHeight = 80, RowGap = 10, ControlWidth = 300;
    private const float ContentX = SidebarWidth + 36, ContentWidth = Width - SidebarWidth - 72;
    private const float ScrollY = HeaderHeight + 22 + 92, ScrollHeight = Height - FooterHeight - 12 - ScrollY;
    private const float RowWidth = ContentWidth;

    private static readonly Dictionary<string, (string Title, string Blurb)> Sections = new()
    {
        ["Overview"] = ("Overview", "Status of Ragnarök in this session, with the settings you will change most."),
        ["HUD.Window"] = ("Window", "The key that opens this window, and the size of Ragnarök panels."),
        ["HUD"] = ("HUD", "Keep Valheim's HUD or switch to the Ragnarök HUD. Each part has its own page below."),
        ["HUD.ChestPreview"] = ("Chest Preview", "A compact view of accessible chest contents under your crosshair. Position and size are personal settings."),
        ["HUD.Hotbar"] = ("Hotbar", "An eight-slot Ragnarök action bar. Adjust its scale, position, slot spacing and panel padding; vanilla HUD keeps Valheim's original hotbar."),
        ["QualityOfLife.Plants"] = ("Plants", "Plant health, harvest readiness and estimated growth time in the hover text, with either HUD style."),
        ["HUD.WeaponDial"] = ("Weapon Dial", "Hold a key, aim at a weapon or tool with the mouse, let go to take it in hand."),
        ["QualityOfLife.ChestSearch"] = ("Chest Search", "Search nearby accessible chests and follow a temporary dashed yellow guide to a result."),
        ["HUD.Vitals"] = ("Vitals", "Health, stamina, eitr and food: what the cluster shows and where it sits."),
        ["HUD.Keys"] = ("Key Hints", "The context key guide for building, combat, inventory and fishing: whether it is shown and where it sits."),
        ["HUD.Notifications"] = ("Notifications", "Plain text unlocks, pickups and small messages with inline quantities."),
        ["HUD.Compass"] = ("Compass", "The heading strip at the top centre of the screen."),
        ["HUD.Minimap"] = ("Minimap", "Day and clock above the minimap; current weather and the next one below it."),
        ["HUD.Status"] = ("Status Effects", "Buffs, debuffs and comfort: whether the strip is shown and where it sits."),
        ["HUD.Inventory"] = ("Inventory", "The Ragnarök inventory grid and item card, or Valheim's own."),
        ["QualityOfLife.Provisions"] = ("Provisions", "The provision slots in the HUD, and the keys that eat or drink from them."),
        ["HUD.Crafting"] = ("Crafting", "The crafting panel: Ragnarök style with search and category filters, or Valheim's."),
        ["HUD.Compendium"] = ("Compendium", "Valheim's texts dialog as a Ragnarök screen: chapters, entries and an art panel."),
        ["HUD.Skills"] = ("Skills", "Valheim's skills dialog as a Ragnarök screen: every skill, its level, progress and bonus."),
        ["HUD.Trophies"] = ("Trophies", "Valheim's trophy wall as a Ragnarök screen: every trophy, the ones you have taken lit."),
        ["HUD.Achievements"] = ("Achievements", "Valheim's achievements as a Ragnarök screen: every badge, the chosen one's conditions."),
        ["QualityOfLife.InventorySort"] = ("Sort", "How the Sort buttons lay out your inventory: by name, by type or by weight."),
        ["QualityOfLife.QuickStack"] = ("Quick Stack", "What the Quick Stack button leaves in your inventory when it fills nearby chests."),
        ["QualityOfLife.NearbyChests"] = ("Nearby Chests", "Craft, build and fuel stations from closed chests around you."),
        ["Gameplay"] = ("Gameplay", "Equipment rules, balance modifiers and server authority."),
        ["QualityOfLife"] = ("Quality Of Life", "Convenience features for storage, inventory, crafting and interaction."),
        ["Gameplay.Equipment"] = ("Equipment", "Character slots and additional utility equipment."),
        ["QualityOfLife.DeathRecovery"] = ("Death Recovery", "Restore worn equipment when reclaiming your tombstone."),
        ["QualityOfLife.Crafting"] = ("Crafting Filters", "Search and filter the vanilla crafting list."),
        ["QualityOfLife.WeaponDial"] = ("Weapon Dial", "Enable the radial selector and choose its activation key."),
        ["QualityOfLife.Hotbar"] = ("Hotbar Rows", "Access a second inventory row with a modifier key."),
        ["HUD.Provisions"] = ("Provision Bar", "Show provision slots on the HUD."),
        ["HUD.Character"] = ("Character", "Character identity, food and visible companions."),
        ["HUD.Power"] = ("Forsaken Power", "Placement and appearance of the power seal."),
        ["HUD.SkillToast"] = ("Skill Notifications", "Placement of skill improvement notifications."),
        ["Gameplay.ForsakenPowers"] = ("Forsaken Powers", "How long powers last and how soon you can activate them again. Changes apply on the next activation."),
        ["Gameplay.Harvesting"] = ("Harvesting", "Multiply plant and honey harvests. Normal pickup and inventory limits still apply."),
        ["Gameplay.Death"] = ("Death Penalty", "Adjust normal skill loss on death: negative values reduce it; -100 removes it."),
        ["Gameplay.Skills"] = ("Skill Experience", "Adjust XP earned by all skills. Positive percentages increase gain; negative percentages reduce it."),
        ["Gameplay.ResourceCosts"] = ("Stamina & Eitr", "Separate percentage modifiers for running, attacks, jumps and weapon eitr costs."),
        ["Gameplay.Sailing"] = ("Sailing", "Forward and reverse propulsion, rudder response, steering strength and water-impact damage."),
        ["Gameplay.Ballista"] = ("Ballista", "Player targeting, ammunition, firing rate, rotation, detection range and bolt behavior."),
        ["Gameplay.Smelter"] = ("Smelter", "Coal capacity, ore queue and production speed."),
        ["Gameplay.BlastFurnace"] = ("Blast Furnace", "Coal capacity, ore queue and production speed."),
        ["Gameplay.CharcoalKiln"] = ("Charcoal Kiln", "Wood queue and charcoal production speed."),
        ["Gameplay.Windmill"] = ("Windmill", "Barley queue and milling speed. Wind still affects output."),
        ["Gameplay.SpinningWheel"] = ("Spinning Wheel", "Flax queue and spinning speed."),
        ["Gameplay.EitrRefinery"] = ("Eitr Refinery", "Fuel capacity, input queue and refining speed."),
        ["Gameplay.SapExtractor"] = ("Sap Extractor", "Sap storage and extraction speed. The root must still have sap."),
        ["Gameplay.Beehive"] = ("Beehive", "Honey storage and production speed. Harvest yield is controlled separately."),
        ["Gameplay.Fermenter"] = ("Fermenter", "Fermentation speed. One batch at a time, with the normal yield."),
        ["Gameplay.Oven"] = ("Oven", "Fuel capacity and cooking speed for fueled cooking stations."),
        ["Gameplay.CookingStations"] = ("Cooking Stations", "Cooking speed for fire-heated cooking stations. Faster cooking also means faster burning."),
        ["Gameplay.Server"] = ("Server", "When you host or run a server: whether players who join play with your gameplay settings."),
        ["HUD.Development"] = ("Development", "Tools for testing live changes while the game runs.")
    };

    private readonly ConfigFile config;
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly string version;
    private readonly Func<string> chestsInRange;
    private readonly Dictionary<string, List<(string Label, string Description, Action Run)>> actions = new();

    private GameObject? canvas;
    private CanvasScaler scaler = null!;
    private CanvasGroup group = null!;
    private RectTransform window = null!, nav = null!, content = null!;
    private ScrollRect scroll = null!;
    private TextMeshProUGUI pageTitle = null!, pageBlurb = null!, help = null!, closeLabel = null!;
    private Image helpMarker = null!;
    private GameObject resetButton = null!;
    private ConfigControls controls = null!;
    private List<string> pages = new();
    private int page;
    private float openedAt, nextRefresh;
    private string hoverHelp = "";
    private ConfigEntryBase? capturing;

    internal ConfigWindow(ConfigFile config, InterfaceSettings settings, Theme theme, string version, Func<string> chestsInRange)
    {
        this.config = config;
        this.settings = settings;
        this.theme = theme;
        this.version = version;
        this.chestsInRange = chestsInRange;
        config.SettingChanged += OnSettingChanged;
        config.ConfigReloaded += OnConfigReloaded;
    }

    internal ConfigEntryBase? Capturing
    {
        get => capturing;
        set
        {
            capturing = value;
            controls?.Refresh();
        }
    }

    internal void Update()
    {
        if (capturing != null)
        {
            CaptureKey();
            return;
        }
        if (settings.WindowKey.Value.IsDown())
            SetOpen(!UiState.WindowOpen);
        else if (UiState.WindowOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
            UiState.SuppressMenuUntilFrame = Time.frameCount + 1;
        }
        if (!UiState.WindowOpen || !canvas) return;

        // Short fade and rise so the state change reads, without lingering motion.
        float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.16f);
        float ease = 1f - (1f - t) * (1f - t);
        group.alpha = ease;
        window.anchoredPosition = new Vector2(0, -(1 - ease) * 12);

        // Rescale only when the mouse is released, so the size slider does not move under the cursor.
        if (!Input.GetMouseButton(0)) ApplyScale();
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.5f;
            controls.Refresh();
        }
        UpdateFooter();
    }

    internal void SetOpen(bool open)
    {
        if (open == UiState.WindowOpen) return;
        UiState.WindowOpen = open;
        capturing = null;
        hoverHelp = "";
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (!open)
        {
            if (canvas) canvas!.SetActive(false);
            return;
        }
        if (!canvas) Build();
        canvas!.SetActive(true);
        openedAt = Time.unscaledTime;
        group.alpha = 0;
        ApplyScale();
        pages = BuildPages();
        page = Mathf.Clamp(page, 0, pages.Count - 1);
        ShowPage(page);
    }

    internal void SelectPage(int index)
    {
        if (UiState.WindowOpen && index >= 0 && index < pages.Count) ShowPage(index);
    }

    public void Dispose()
    {
        config.SettingChanged -= OnSettingChanged;
        config.ConfigReloaded -= OnConfigReloaded;
        if (canvas) UnityEngine.Object.Destroy(canvas);
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (UiState.WindowOpen && canvas) controls.Refresh();
    }

    private void OnConfigReloaded(object sender, EventArgs e) => OnSettingChanged(sender, null!);

    private void ApplyScale()
    {
        float scale = settings.Scale.Value;
        Vector2 reference = new(1920f / scale, 1080f / scale);
        if (scaler.referenceResolution != reference) scaler.referenceResolution = reference;
    }

    private List<string> BuildPages()
    {
        List<string> known = Sections.Keys.ToList();
        List<string> sections = config.Keys.Where(k => ConfigSchema.Visible(k.Section, k.Key)).Select(k => k.Section).Distinct().ToList();
        // A sub-section is listed under its parent even if the parent has no settings of its own.
        foreach (string parent in sections.Where(IsChild).Select(ParentOf).Distinct().ToList())
            if (!sections.Contains(parent)) sections.Add(parent);

        List<string> roots = new[] { "Gameplay", "QualityOfLife", "HUD" }.Where(sections.Contains)
            .Concat(sections.Where(s => !IsChild(s) && s != "Gameplay" && s != "QualityOfLife" && s != "HUD").OrderBy(s => s)).ToList();
        List<string> ordered = new() { "Overview" };
        foreach (string root in roots)
        {
            ordered.Add(root);
            ordered.AddRange(known.Where(s => IsChild(s) && ParentOf(s) == root && sections.Contains(s)));
            ordered.AddRange(sections.Where(s => IsChild(s) && ParentOf(s) == root && !known.Contains(s)).OrderBy(s => s));
        }
        return ordered;
    }

    private static bool IsChild(string section) => section.IndexOf('.') > 0;

    private static string ParentOf(string section) => section.Substring(0, section.IndexOf('.'));

    // Static frame ---------------------------------------------------------------

    private void Build()
    {
        canvas = new GameObject("Ragnarok Config Window", typeof(RectTransform)) { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(canvas);
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 32000;
        scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvas.AddComponent<GraphicRaycaster>();
        group = canvas.AddComponent<CanvasGroup>();
        controls = new ConfigControls(theme, this);

        // Full-screen backdrop that also blocks clicks to any game UI underneath.
        Ugui.Image(canvas.transform, "Backdrop", theme.White, Palette.Backdrop, raycast: true).Stretch();

        window = Ugui.Image(canvas.transform, "Window", theme.Window, Color.white, raycast: true).rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(Width, Height);

        Ugui.Image(window, "Sidebar", theme.White, Palette.Panel).At(1, HeaderHeight, SidebarWidth, Height - HeaderHeight - FooterHeight);
        Ugui.Image(window, "Sidebar Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(SidebarWidth, HeaderHeight, 1, Height - HeaderHeight - FooterHeight);
        Ugui.Image(window, "Footer Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(1, Height - FooterHeight, Width - 2, 1);

        Header();
        // The section list scrolls above the note once sub-pages make it taller than the sidebar.
        ScrollRect navScroll = Scroller("Navigation", new Rect(1, HeaderHeight, SidebarWidth - 4, Height - HeaderHeight - FooterHeight - 84), 0);
        nav = (RectTransform)navScroll.content;
        Ugui.Image(window, "Note Rule", theme.White, Palette.GoldDim).At(28, Height - FooterHeight - 74, 24, 1);
        Ugui.Text(window, "Note", "Changes apply instantly and are saved to the config file.", theme.Subtle, 13, Palette.Muted, wrap: true)
            .At(28, Height - FooterHeight - 64, SidebarWidth - 48, 48);

        // The page title (Alegreya, 32) over its blurb, then the guide's divider.
        pageTitle = Ugui.Text(window, "Page Title", "", theme.Heading, 32, Palette.Gold).At(ContentX, HeaderHeight + 14, ContentWidth, 50);
        pageBlurb = Ugui.Text(window, "Page Blurb", "", theme.Body, 14, Palette.Muted).At(ContentX, HeaderHeight + 62, ContentWidth, 22);
        Ugui.Image(window, "Page Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(ContentX, HeaderHeight + 97, ContentWidth, 1);
        ScrollArea();
        Footer();
    }

    private void Header()
    {
        Ugui.Text(window, "Eyebrow", "valheim · mod configuration", theme.Section, 16, Palette.Muted).At(36, 12, 600, 24);
        Ugui.Text(window, "Title", "Ragnarök", theme.Heading, 44, Palette.Gold).At(34, 26, 600, 64);
        Ugui.Text(window, "Byline", "by KernelPanic", theme.Subtle, 13, Palette.Muted).At(36, 84, 400, 18);

        Button close = Ugui.Button(window, "Close", theme.Button, theme.ButtonHover, () => SetOpen(false)).At(Width - 36 - 130, 34, 130, 36);
        closeLabel = Ugui.Text(close.transform, "Label", "Close  ·  Esc", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        close.OnHover(h => closeLabel.color = h ? Palette.GoldBright : Palette.Bone);
        Image chip = Ugui.Image(window, "Version", theme.Card, Color.white).At(Width - 36 - 130 - 96, 34, 84, 36);
        Ugui.Text(chip.transform, "Label", "v" + version, theme.Strong, 15, Palette.GoldBright, TextAlignmentOptions.Center).Stretch();

        Ornament(window, new Rect(24, HeaderHeight - 8, Width - 48, 2), Palette.Gold.WithAlpha(0.85f));
    }

    // The guide's divider: a gold hairline with a small diamond at its middle.
    private void Ornament(Transform parent, Rect line, Color color) =>
        Ornaments.Rule(parent, theme, line.x, Mathf.Round(line.center.y), line.width);

    private void ScrollArea()
    {
        scroll = Scroller("Scroll", new Rect(ContentX, ScrollY, ContentWidth + 16, ScrollHeight), 16);
        content = (RectTransform)scroll.content;
    }

    // A vertical scroll view with a thin gold handle on its right edge; gutter keeps the handle clear of the content.
    private ScrollRect Scroller(string name, Rect area, float gutter)
    {
        RectTransform view = Ugui.Node(window, name).At(area);
        RectTransform viewport = Ugui.Image(view, "Viewport", null, Color.clear, raycast: true).Stretch(0, 0, gutter, 0).rectTransform;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform inner = Ugui.Node(viewport, "Content");
        inner.anchorMin = new Vector2(0, 1);
        inner.anchorMax = new Vector2(1, 1);
        inner.pivot = new Vector2(0, 1);
        inner.sizeDelta = Vector2.zero;

        RectTransform bar = Ugui.Image(view, "Scrollbar", theme.White, Palette.Rule, raycast: true).rectTransform;
        bar.anchorMin = new Vector2(1, 0);
        bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(1, 0.5f);
        bar.sizeDelta = new Vector2(3, 0);
        RectTransform slide = Ugui.Node(bar, "Sliding Area").Stretch();
        Image handle = Ugui.Image(slide, "Handle", theme.White, Palette.Gold, raycast: true).Stretch(-1, 0, -1, 0);
        Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;

        ScrollRect rect = view.gameObject.AddComponent<ScrollRect>();
        rect.viewport = viewport;
        rect.content = inner;
        rect.horizontal = false;
        rect.movementType = ScrollRect.MovementType.Clamped;
        Ugui.WheelSpeed(rect);
        rect.inertia = false;
        rect.verticalScrollbar = scrollbar;
        rect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return rect;
    }

    private void Footer()
    {
        helpMarker = Ugui.Image(window, "Help Marker", theme.Diamond, Palette.Faint).At(36, Height - FooterHeight / 2 - 5, 10, 10);
        help = Ugui.Text(window, "Help", "", theme.Body, 13, Palette.Muted).At(58, Height - FooterHeight, Width - 480, FooterHeight);
        Button reset = Ugui.Button(window, "Restore Defaults", theme.Button, theme.ButtonHover, () => RestoreDefaults(pages[page]))
            .At(Width - 36 - 190, Height - FooterHeight + 10, 190, 36);
        TextMeshProUGUI label = Ugui.Text(reset.transform, "Label", "Restore defaults", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        reset.OnHover(h => label.color = h ? Palette.GoldBright : Palette.Bone);
        resetButton = reset.gameObject;
    }

    private void UpdateFooter()
    {
        string text = capturing != null ? "Press the new key combination. Escape cancels."
            : hoverHelp.Length > 0 ? hoverHelp
            : $"{settings.WindowKey.Value} or Escape closes this window.  Hover a setting for details.";
        if (help.text != text) help.text = text;
        helpMarker.color = capturing != null || hoverHelp.Length > 0 ? Palette.GoldBright : Palette.Faint;
    }

    // Pages ----------------------------------------------------------------------

    private void ShowPage(int index)
    {
        page = index;
        capturing = null;
        hoverHelp = "";
        controls.Refreshers.Clear();
        Ugui.Clear(content);
        Navigation();

        string key = pages[page];
        pageTitle.text = IsChild(key) ? Title(ParentOf(key)) + "  ·  " + Title(key) : Title(key);
        pageBlurb.text = Sections.TryGetValue(key, out var meta) ? meta.Blurb : "";
        resetButton.SetActive(key != "Overview");
        float height = key == "Overview" ? Overview() : Section(key);
        content.sizeDelta = new Vector2(0, height);
        content.anchoredPosition = Vector2.zero;
        controls.Refresh();
    }

    private void Navigation()
    {
        Ugui.Clear(nav);
        Ugui.Text(nav, "Label", "sections", theme.Section, 16, Palette.Muted).At(28, 16, 200, 24);
        float y = 48;
        int number = 0;
        string current = pages[page];
        for (int i = 0; i < pages.Count; i++)
        {
            int target = i;
            bool active = i == page;
            if (IsChild(pages[i]))
            {
                y = SubItem(i, y, active, i + 1 >= pages.Count || !IsChild(pages[i + 1]));
                continue;
            }
            number++;
            // A parent whose sub-page is open keeps a lit index, so the trail stays readable.
            bool branch = IsChild(current) && ParentOf(current) == pages[i];
            Rect item = new(12, y, SidebarWidth - 24, 48);
            Image background = Ugui.Image(nav, "Item Background", theme.White, active ? Palette.CardHover : Color.clear).At(item);
            if (active)
            {
                Ugui.Image(nav, "Active Bar", theme.White, Palette.GoldBright).At(item.x, item.y, 3, item.height);
                Ugui.Image(nav, "Active Rule", theme.White, Palette.GoldDim).At(item.x + 3, item.yMax - 1, item.width - 3, 1);
                Ugui.Image(nav, "Active Marker", theme.Diamond, Palette.Gold).At(item.xMax - 20, item.center.y - 4, 8, 8);
            }
            Ugui.Text(nav, "Index", number.ToString("00"), theme.Strong, 12, active || branch ? Palette.Gold : Palette.Faint)
                .At(item.x + 18, item.y, 30, item.height);
            Color idle = branch ? Palette.Bone : Palette.Muted;
            TextMeshProUGUI label = Ugui.Text(nav, "Title", Title(pages[i]), theme.Strong, 15,
                active ? Palette.Bone : idle).At(item.x + 52, item.y, item.width - 76, item.height);

            Button hit = Ugui.HitArea(nav, "Item", () => { if (target != page) ShowPage(target); }).At(item);
            if (!active)
                hit.OnHover(h =>
                {
                    background.color = h ? Palette.Card : Color.clear;
                    label.color = h ? Palette.Bone : idle;
                });
            y += 52;
        }
        nav.sizeDelta = new Vector2(0, y + 8);
    }

    // Sub-pages hang from their parent on a thin gold rule, like branches on a rune stave. The stem runs
    // unbroken from the parent's lower edge to the last child's tick; the tick ends where the item begins.
    private float SubItem(int index, float y, bool active, bool last)
    {
        const float stem = 39, tick = 12;
        Rect item = new(stem + tick + 1, y - 4, SidebarWidth - stem - tick - 13, 36);
        Image background = Ugui.Image(nav, "Sub Background", theme.White, active ? Palette.CardHover : Color.clear).At(item);
        Ugui.Image(nav, "Branch Stem", theme.White, Palette.Rule).At(stem, y - 4, 1, last ? 19 : 40);
        Image branch = Ugui.Image(nav, "Branch Tick", theme.White, active ? Palette.Gold : Palette.Rule).At(stem + 1, y + 14, tick, 1);
        if (active)
        {
            Ugui.Image(nav, "Active Bar", theme.White, Palette.GoldBright).At(item.x, item.y, 2, item.height);
            Ugui.Image(nav, "Active Marker", theme.Diamond, Palette.Gold).At(item.xMax - 20, item.center.y - 3, 6, 6);
        }
        Image marker = Ugui.Image(nav, "Sub Marker", theme.Diamond, active ? Palette.Gold : Palette.Faint).At(item.x + 12, item.center.y - 3, 6, 6);
        TextMeshProUGUI label = Ugui.Text(nav, "Sub Title", Title(pages[index]), theme.Strong, 13,
            active ? Palette.Bone : Palette.Muted).At(item.x + 28, item.y, item.width - 52, item.height);

        int target = index;
        Button hit = Ugui.HitArea(nav, "Sub Item", () => { if (target != page) ShowPage(target); }).At(item);
        if (!active)
            hit.OnHover(h =>
            {
                background.color = h ? Palette.Card : Color.clear;
                label.color = h ? Palette.Bone : Palette.Muted;
                marker.color = h ? Palette.GoldDim : Palette.Faint;
                branch.color = h ? Palette.GoldDim : Palette.Rule;
            });
        return y + 40;
    }

    // A button row at the end of a section's page, e.g. "Test notifications".
    internal void AddAction(string section, string label, string description, Action run)
    {
        if (!actions.TryGetValue(section, out var list)) actions[section] = list = new();
        list.Add((label, description, run));
    }

    private float ActionRows(string section, float y)
    {
        if (!actions.TryGetValue(section, out var list)) return y;
        foreach (var (label, description, run) in list)
        {
            Image row = Ugui.Image(content, "Action " + label, theme.Card, Color.white, raycast: true).At(0, y, RowWidth, RowHeight);
            row.OnHover(h =>
            {
                row.sprite = h ? theme.CardHover : theme.Card;
                hoverHelp = h ? description : hoverHelp == description ? "" : hoverHelp;
            });
            float textWidth = RowWidth - ControlWidth - 64;
            Ugui.Text(row.transform, "Label", label, theme.Strong, 15, Palette.Bone).At(22, 12, textWidth, 24);
            Ugui.Text(row.transform, "Description", description, theme.Body, 13, Palette.Muted, TextAlignmentOptions.TopLeft, wrap: true)
                .At(22, 38, textWidth, 36);
            Button button = Ugui.Button(row.transform, "Run", theme.Button, theme.ButtonHover, run)
                .At(RowWidth - ControlWidth - 22, (RowHeight - 34) / 2, ControlWidth, 34);
            Ugui.Text(button.transform, "Label", label, theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Center)
                .Stretch();
            y += RowHeight + RowGap;
        }
        return y;
    }

    private float Section(string section)
    {
        if (!IsChild(section) && Entries(section).Count == 0)
        {
            float top = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                if (!IsChild(pages[i]) || ParentOf(pages[i]) != section) continue;
                int target = i;
                string child = pages[i];
                var button = Ugui.Button(content, child, theme.Card, theme.CardHover, () => ShowPage(target))
                    .At(0, top, RowWidth, 68);
                Ugui.Text(button.transform, "Title", Title(child), theme.Strong, 16, Palette.Bone).At(18, 8, RowWidth - 36, 24);
                Ugui.Text(button.transform, "Description", Sections.TryGetValue(child, out var metadata) ? metadata.Blurb : "",
                    theme.Body, 12, Palette.Muted).At(18, 34, RowWidth - 36, 24);
                top += 78;
            }
            return top;
        }
        float y = 0;
        foreach (ConfigEntryBase entry in Entries(section))
        {
            Image row = Ugui.Image(content, "Row " + entry.Definition.Key, theme.Card, Color.white, raycast: true).At(0, y, RowWidth, RowHeight);
            Image accent = Ugui.Image(row.transform, "Hover Accent", theme.White, Palette.GoldBright).At(0, 14, 2, RowHeight - 28);
            accent.enabled = false;
            string description = entry.Description?.Description ?? "";
            row.OnHover(h =>
            {
                row.sprite = h ? theme.CardHover : theme.Card;
                accent.enabled = h;
                hoverHelp = h ? description : hoverHelp == description ? "" : hoverHelp;
            });

            float textWidth = RowWidth - ControlWidth - 64;
            Ugui.Text(row.transform, "Label", ConfigControls.Pretty(entry.Definition.Key), theme.Strong, 15, Palette.Bone).At(22, 12, textWidth, 24);
            Ugui.Text(row.transform, "Description", description, theme.Body, 13, Palette.Muted, TextAlignmentOptions.TopLeft, wrap: true)
                .At(22, 38, textWidth, 36);
            Control(entry, row.transform, new Rect(RowWidth - ControlWidth - 22, (RowHeight - 34) / 2, ControlWidth, 34));
            y += RowHeight + RowGap;
        }
        return ActionRows(section, y);
    }

    // A setting's control. While the server decides the setting it is dimmed and locked, noted above.
    private void Control(ConfigEntryBase entry, Transform parent, Rect r)
    {
        RectTransform holder = Ugui.Node(parent, "Control").Stretch();
        controls.Add(entry, holder, r);
        CanvasGroup group = holder.gameObject.AddComponent<CanvasGroup>();
        TextMeshProUGUI note = Ugui.Text(parent, "Server Note", "set by server", theme.Subtle, 12, Palette.Gold, TextAlignmentOptions.MidlineRight).At(r.x, r.y - 18, r.width, 16);

        void Refresh()
        {
            bool locked = ServerConfig.Instance?.Controlled(entry) == true;
            group.alpha = locked ? 0.4f : 1f;
            group.interactable = group.blocksRaycasts = !locked;
            note.enabled = locked;
        }

        Refresh();
        controls.Refreshers.Add(Refresh);
    }

    private float Overview()
    {
        const float statHeight = 118, gap = 14;
        float width = (RowWidth - gap * 3) / 4;
        ConfigEntry<bool>? chests = Find<bool>("QualityOfLife.NearbyChests", "Enabled");
        ConfigEntry<float>? range = Find<float>("QualityOfLife.NearbyChests", "Range");
        Stat(0, width, "HUD", Palette.Alert, () =>
        {
            bool ragnarok = settings.Hud.Value == HudStyle.Ragnarok;
            return (ragnarok ? "Ragnarök" : "Vanilla", ragnarok ? "Custom HUD active" : "Opt in below or on the HUD page");
        });
        Stat(width + gap, width, "Nearby Chests", Palette.Gold, () =>
            (chests?.Value == true ? "On" : "Off", range != null ? $"Range {range.Value:0} m" : ""));
        Stat((width + gap) * 2, width, "In Range", Palette.Eitr, () => (chestsInRange(), "Chests you can use now"));
        Stat((width + gap) * 3, width, "Build", Palette.Ember, () => ("v" + version, "F6 reloads code in development"));
        float y = statHeight + gap + 6;

        List<ConfigEntryBase> quick = new ConfigEntryBase?[]
        {
            settings.Hud, chests, Find<bool>("QualityOfLife.NearbyChests", "Crafting"), Find<bool>("QualityOfLife.NearbyChests", "Building"), Find<bool>("QualityOfLife.NearbyChests", "Stations")
        }.Where(e => e != null).Cast<ConfigEntryBase>().ToList();
        List<ConfigEntry<KeyboardShortcut>> keys = config.Select(kv => kv.Value).OfType<ConfigEntry<KeyboardShortcut>>().ToList();

        float leftWidth = Mathf.Round((RowWidth - gap) * 0.6f), rightWidth = RowWidth - gap - leftWidth;
        float quickHeight = 54 + quick.Count * 56, keysHeight = 54 + (keys.Count + 1) * 40;

        Image left = Ugui.Image(content, "Quick Settings", theme.Card, Color.white).At(0, y, leftWidth, quickHeight);
        CardTitle(left.transform, "quick settings");
        float rowY = 52;
        foreach (ConfigEntryBase entry in quick)
        {
            if (rowY > 52) Ugui.Image(left.transform, "Rule", theme.White, Palette.Rule.WithAlpha(0.6f)).At(20, rowY - 4, leftWidth - 40, 1);
            RectTransform row = Ugui.Image(left.transform, "Row", null, Color.clear, raycast: true).At(20, rowY, leftWidth - 40, 48).rectTransform;
            string description = entry.Description?.Description ?? "";
            row.OnHover(h => hoverHelp = h ? description : hoverHelp == description ? "" : hoverHelp);
            Ugui.Text(row, "Label", $"{Title(entry.Definition.Section)}  ·  {ConfigControls.Pretty(entry.Definition.Key)}", theme.Strong, 15, Palette.Bone)
                .At(0, 0, leftWidth - 300, 48);
            Control(entry, row, new Rect(leftWidth - 40 - 250, 7, 250, 34));
            rowY += 56;
        }

        Image right = Ugui.Image(content, "Controls", theme.Card, Color.white).At(leftWidth + gap, y, rightWidth, keysHeight);
        CardTitle(right.transform, "controls");
        KeyLine(right.transform, 52, rightWidth, "Reload code", () => "F6");
        float keyY = 52;
        foreach (ConfigEntry<KeyboardShortcut> key in keys)
        {
            keyY += 40;
            KeyLine(right.transform, keyY, rightWidth, ConfigControls.Pretty(key.Definition.Key.Replace("Key", "")), () => key.Value.ToString());
        }
        return y + Mathf.Max(quickHeight, keysHeight) + 8;
    }

    private void Stat(float x, float width, string label, Color accent, Func<(string Value, string Caption)> read)
    {
        Image card = Ugui.Image(content, "Stat " + label, theme.Card, Color.white).At(x, 0, width, 118);
        Ugui.Image(card.transform, "Accent", theme.White, accent).At(18, 18, 24, 2);
        Ugui.Text(card.transform, "Label", label.ToLowerInvariant(), theme.Section, 16, Palette.Muted).At(18, 22, width - 36, 24);
        TextMeshProUGUI value = Ugui.Text(card.transform, "Value", "", theme.Display, 32, Palette.Bone).At(18, 42, width - 36, 48);
        TextMeshProUGUI caption = Ugui.Text(card.transform, "Caption", "", theme.Body, 13, Palette.Muted).At(18, 86, width - 36, 20);
        controls.Refreshers.Add(() =>
        {
            (string v, string c) = read();
            if (value.text != v) value.text = v;
            if (caption.text != c) caption.text = c;
        });
    }

    private void CardTitle(Transform card, string title)
    {
        Ugui.Image(card, "Marker", theme.Diamond, Palette.Gold).At(20, 22, 8, 8);
        Ugui.Text(card, "Title", title, theme.Section, 17, Palette.Muted).At(36, 12, 400, 26);
    }

    private void KeyLine(Transform card, float y, float width, string action, Func<string> key)
    {
        Ugui.Text(card, "Action", action, theme.Strong, 15, Palette.Bone).At(20, y, width - 150, 34);
        Image cap = Ugui.Image(card, "Key", theme.CardHover, Color.white).At(width - 20 - 100, y + 3, 100, 28);
        TextMeshProUGUI label = Ugui.Text(cap.transform, "Label", "", theme.Strong, 13, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        controls.Refreshers.Add(() => label.text = key());
    }

    // Behaviour --------------------------------------------------------------------

    private void CaptureKey()
    {
        if (!Input.anyKeyDown) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Capturing = null;
            return;
        }
        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            // Side and extra mouse buttons (back, forward) can be bound; left, right and middle cannot: they
            // click the window itself.
            if (key is KeyCode.None or >= KeyCode.Mouse0 and <= KeyCode.Mouse2 || IsModifier(key) || !Input.GetKeyDown(key)) continue;
            List<KeyCode> modifiers = new();
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) modifiers.Add(KeyCode.LeftControl);
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) modifiers.Add(KeyCode.LeftShift);
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) modifiers.Add(KeyCode.LeftAlt);
            ConfigEntryBase entry = capturing!;
            Capturing = null;
            entry.BoxedValue = new KeyboardShortcut(key, modifiers.ToArray());
            return;
        }
    }

    private static bool IsModifier(KeyCode key) => key is KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl
        or KeyCode.RightControl or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftCommand or KeyCode.RightCommand;

    private void RestoreDefaults(string section)
    {
        foreach (ConfigEntryBase entry in Entries(section))
            entry.BoxedValue = entry.DefaultValue;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        controls.Refresh();
    }

    private List<ConfigEntryBase> Entries(string section) =>
        config.Where(kv => kv.Key.Section == section && ConfigSchema.Visible(kv.Key.Section, kv.Key.Key)).Select(kv => kv.Value).ToList();

    private ConfigEntry<TValue>? Find<TValue>(string section, string key) =>
        config.TryGetEntry(section, key, out ConfigEntry<TValue> entry) ? entry : null;

    private static string Title(string section) =>
        Sections.TryGetValue(section, out var meta) ? meta.Title : ConfigControls.Pretty(section.Substring(section.LastIndexOf('.') + 1));
}
