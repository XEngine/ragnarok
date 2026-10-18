using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Day and clock above the minimap, current and next weather below it. Works with either HUD style.
// The next weather is Valheim's own pick for the next weather period: EnvMan seeds Random with the
// period number and draws from the biome's weighted list, so the same draw done early is exact (as
// long as no event, boss or special zone overrides the weather; then it shows a dash).
internal sealed class MinimapInfo : IDisposable
{
    private const float StripHeight = 24, Margin = 6;

    private static readonly MethodInfo? Override = AccessTools.Method(typeof(EnvMan), "GetEnvironmentOverride");
    private static readonly MethodInfo? Biome = AccessTools.Method(typeof(EnvMan), "GetBiome");
    private static readonly MethodInfo? Weighted = AccessTools.Method(typeof(EnvMan), "SelectWeightedEnvironment");

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private Minimap? minimap;
    private RectTransform? shifted;
    private Vector2 shiftedFrom;
    private RectTransform? top, bottom;
    private TextMeshProUGUI? day, clock, now, next;
    private Image? sun;
    private readonly List<HudBackdrop> backdrops = new();
    private string shownDay = "", shownClock = "", shownNow = "", shownNext = "";
    private long predictedPeriod = -1;
    private Heightmap.Biome predictedBiome;
    private string predicted = "";
    private Vector2Int screen;

    internal MinimapInfo(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        Minimap current = Minimap.instance;
        if (current != minimap)
        {
            top = bottom = null;
            shifted = null;
            minimap = current;
        }
        if (!minimap || !settings.MinimapInfo.Value || !EnvMan.instance || !ZNet.instance)
        {
            Dispose();
            return;
        }
        if (!top) Build(minimap!);
        bool show = minimap!.m_smallRoot.activeInHierarchy;
        top!.gameObject.SetActive(show);
        // The weather strip would run into the crafting panel under the map; it waits while the inventory is open.
        bottom!.gameObject.SetActive(show && !InventoryGui.IsVisible());
        if (!show) return;
        foreach (HudBackdrop backdrop in backdrops) backdrop.Apply(settings.MinimapBackground, settings.MinimapBackgroundOpacity);
        MakeRoom(minimap);
        Place(minimap);
        Refresh();
    }

    public void Dispose()
    {
        if (top) UnityEngine.Object.Destroy(top!.gameObject);
        if (bottom) UnityEngine.Object.Destroy(bottom!.gameObject);
        top = bottom = null;
        backdrops.Clear();
        if (shifted) shifted!.anchoredPosition = shiftedFrom;
        shifted = null;
    }

    private void Build(Minimap map)
    {
        Transform parent = map.m_smallRoot.transform;
        top = Strip(parent, "Ragnarok Day Clock");
        sun = Ugui.Image(top, "Sun", theme.Diamond, Palette.Gold).At(10, 8, 8, 8);
        day = Ugui.Text(top, "Day", "", theme.Strong, 15, Palette.Gold).At(24, 0, 140, StripHeight);
        clock = Ugui.Text(top, "Clock", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.MidlineRight);
        Right(clock, 10);

        bottom = Strip(parent, "Ragnarok Weather");
        now = Ugui.Text(bottom, "Now", "", theme.Strong, 13, Palette.Bone).At(10, 0, 160, StripHeight);
        next = Ugui.Text(bottom, "Next", "", theme.Subtle, 12, Palette.Muted, TextAlignmentOptions.MidlineRight);
        Right(next, 10);
    }

    private RectTransform Strip(Transform parent, string name)
    {
        RectTransform strip = Ugui.Node(parent, name);
        strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0.5f, 0.5f);
        strip.sizeDelta = new Vector2(200, StripHeight);
        backdrops.Add(new HudBackdrop(theme, strip, new Vector4(16, 6, 16, 6)));
        Ugui.Image(strip, "Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).Stretch(left: 12, top: StripHeight - 1, right: 12);
        return strip;
    }

    private static void Right(TextMeshProUGUI text, float inset)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.4f, 0);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-inset, 0);
    }

    // The minimap hugs the top of the screen; when the clock strip would not fit above it, move the
    // whole small map down just enough (measured from its original place, redone on resolution change).
    private void MakeRoom(Minimap map)
    {
        Vector2Int size = new(Screen.width, Screen.height);
        if (shifted && size == screen) return;
        screen = size;
        RectTransform root = (RectTransform)map.m_smallRoot.transform;
        if (!shifted)
        {
            shifted = root;
            shiftedFrom = root.anchoredPosition;
        }
        root.anchoredPosition = shiftedFrom;
        Vector3[] corners = new Vector3[4];
        map.m_mapImageSmall.rectTransform.GetWorldCorners(corners);
        float scale = Mathf.Max(0.01f, root.lossyScale.y);
        float gap = (Screen.height - corners[1].y) / scale, needed = StripHeight + Margin * 2 + 4 + MinimapSkin.AboveMap;
        if (gap < needed) root.anchoredPosition = shiftedFrom - new Vector2(0, needed - gap);
    }

    // Strips track the map picture: centred, as wide as the map, just outside its top and bottom.
    private void Place(Minimap map)
    {
        Vector3[] corners = new Vector3[4];
        map.m_mapImageSmall.rectTransform.GetWorldCorners(corners);
        Transform parent = top!.parent;
        Vector3 topLeft = parent.InverseTransformPoint(corners[1]), topRight = parent.InverseTransformPoint(corners[2]);
        Vector3 bottomLeft = parent.InverseTransformPoint(corners[0]);
        float width = Mathf.Max(160, topRight.x - topLeft.x);
        float centre = (topLeft.x + topRight.x) / 2;
        // Clear of the Ragnarök minimap's ring above and its medallions below (0 with Valheim's frame).
        SetStrip(top, new Vector2(centre, topLeft.y + Margin + MinimapSkin.AboveMap + StripHeight / 2), width);
        SetStrip(bottom!, new Vector2(centre, bottomLeft.y - Margin - MinimapSkin.BelowMap - StripHeight / 2), width);
    }

    private static void SetStrip(RectTransform strip, Vector2 centre, float width)
    {
        RectTransform parent = (RectTransform)strip.parent;
        // Positions are in the parent's local space; anchors are its centre.
        Vector2 local = centre - parent.rect.center;
        if ((strip.anchoredPosition - local).sqrMagnitude > 0.01f) strip.anchoredPosition = local;
        if (!Mathf.Approximately(strip.sizeDelta.x, width)) strip.sizeDelta = new Vector2(width, StripHeight);
    }

    private void Refresh()
    {
        EnvMan env = EnvMan.instance;
        double seconds = ZNet.instance.GetTimeSeconds();

        Set(day!, ref shownDay, "Day " + env.GetDay());
        // Valheim's visual day: 0.25 sunrise (06:00), 0.5 noon, 0.75 sunset (18:00).
        float fraction = env.GetDayFraction();
        int minutes = Mathf.FloorToInt(fraction * 24 * 60) % (24 * 60);
        int hour = minutes / 60, minute = minutes % 60;
        string time = settings.Clock24h.Value
            ? $"{hour:00}:{minute:00}"
            : $"{(hour % 12 == 0 ? 12 : hour % 12)}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
        Set(clock!, ref shownClock, time);
        bool daytime = fraction > 0.25f && fraction < 0.75f;
        sun!.color = daytime ? Palette.GoldBright : Palette.Eitr;

        EnvSetup currentEnv = env.GetCurrentEnvironment();
        Set(now!, ref shownNow, currentEnv != null ? Friendly(currentEnv.m_name) : "—");

        long duration = Math.Max(1, env.m_environmentDuration);
        long period = (long)seconds / duration;
        int left = Mathf.CeilToInt((float)(duration - seconds % duration));
        string nextName = Predict(env, period + 1);
        Set(next!, ref shownNext, nextName.Length == 0 ? "Next —" : $"Next: {nextName} · {Span(left)}");
    }

    private string Predict(EnvMan env, long period)
    {
        try
        {
            if (Override != null && Override.Invoke(env, null) is string forced && forced.Length > 0) return "";
            if (Biome?.Invoke(env, null) is not BiomeSector sector || Weighted == null) return "";
            if (period == predictedPeriod && sector.Biome == predictedBiome) return predicted;
            predictedPeriod = period;
            predictedBiome = sector.Biome;
            predicted = "";

            List<EnvEntry> available = env.GetAvailableEnvironments(sector);
            if (available == null || available.Count == 0) return predicted;
            Camera camera = Utils.GetMainCamera();
            Vector3 pos = camera ? camera.transform.position : Vector3.zero;
            bool ashlands = WorldGenerator.IsAshlands(pos.x, pos.z), deepNorth = WorldGenerator.IsDeepnorth(pos.x, pos.y);

            UnityEngine.Random.State state = UnityEngine.Random.state;
            UnityEngine.Random.InitState((int)period);
            EnvSetup? pick = Weighted.Invoke(env, new object[] { available }) as EnvSetup;
            UnityEngine.Random.state = state;
            foreach (EnvEntry entry in available)
                if ((entry.m_ashlandsOverride && ashlands) || (entry.m_deepnorthOverride && deepNorth))
                    pick = entry.m_env;
            predicted = pick != null ? Friendly(pick.m_name) : "";
        }
        catch (Exception)
        {
            predicted = "";
        }
        return predicted;
    }

    private static string Span(int seconds) => seconds >= 60 ? Mathf.CeilToInt(seconds / 60f) + "m" : seconds + "s";

    private static void Set(TMP_Text text, ref string shown, string value)
    {
        if (shown == value) return;
        shown = value;
        text.text = value;
    }

    // Valheim's environment names are internal ("Twilight_SnowStorm", "Ashlands_CinderRain"); show plain weather.
    internal static string Friendly(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("meteor")) return "Meteor shower";
        if (n.Contains("cinder")) return "Cinder rain";
        if (n.Contains("ashrain") || n.Contains("ash_rain")) return "Ash rain";
        if (n.Contains("thunder")) return "Thunderstorm";
        if (n.Contains("snowstorm")) return "Blizzard";
        if (n.Contains("snow")) return "Snow";
        if (n.Contains("lightrain")) return "Light rain";
        if (n.Contains("rain")) return "Rain";
        if (n.Contains("storm")) return "Storm";
        if (n.Contains("mist") || n.Contains("fog")) return "Mist";
        if (n.Contains("dark")) return "Dark";
        if (n.Contains("clear")) return "Clear";
        foreach (string boss in new[] { "eikthyr", "gdking", "bonemass", "moder", "goblinking", "queen", "fader" })
            if (n.Contains(boss)) return "Boss storm";
        string plain = name.Replace('_', ' ').Trim();
        return plain.Length == 0 ? "—" : char.ToUpperInvariant(plain[0]) + plain.Substring(1);
    }
}
