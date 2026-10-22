using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

// Rune & Iron heading rail. Camera bearings and existing public world targets are read-only;
// every generated object and text silhouette belongs to this HUD root.
internal sealed class CompassStrip : IDisposable
{
    private const float Width = 720, Height = 94, RailY = 30, Field = 180;
    private const float CompassScale = 0.85f, EdgeFadeFraction = 0.4f;
    private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(float Degrees, RectTransform Rect, CanvasGroup Fade)> marks = new();
    private readonly List<Marker> markers = new();
    private readonly List<(Vector3 Position, Color Color, string Name, Transform? Follow)> targets = new();
    private readonly List<(float Left, float Right)> labelBounds = new();
    private GameObject? hudRoot;
    private RectTransform? root;
    internal static RectTransform? Shown { get; private set; }
    private TextMeshProUGUI? footer;
    private TextOutline? footerOutline;
    private Texture2D? railTexture;
    private Sprite? railSprite;
    private float nextScan, nextContext;
    private int shownBearing = -1;
    private string shownContext = "";
    private string shownDirection = "";

    internal CompassStrip(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        GameObject? parent = global::Hud.instance ? global::Hud.instance.m_rootObject : null;
        if (parent != hudRoot)
        {
            Dispose();
            hudRoot = parent;
        }
        if (!hudRoot || !settings.Compass.Value || !GameCamera.instance || !Player.m_localPlayer)
        {
            if (!settings.Compass.Value || !hudRoot) Dispose();
            else if (root) root!.gameObject.SetActive(false);
            return;
        }
        if (!root) Build(hudRoot!.transform);
        root!.gameObject.SetActive(true);
        Shown = root;
        // Fit narrow HUD canvases while retaining the user's overall HUD scale.
        float available = ((RectTransform)hudRoot!.transform).rect.width - 32;
        float scale = settings.HudScale.Value * CompassScale;
        if (available > 0) scale = Mathf.Min(scale, available / Width);
        root.localScale = Vector3.one * scale;

        Vector3 forward = GameCamera.instance.transform.forward;
        float heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
        float perDegree = Width / Field;
        foreach (var (degrees, rect, fade) in marks)
        {
            float offset = Mathf.DeltaAngle(heading, degrees);
            bool visible = Mathf.Abs(offset) < Field / 2;
            if (rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
            if (!visible) continue;
            rect.anchoredPosition = new Vector2(offset * perDegree, rect.anchoredPosition.y);
            fade.alpha = SideOpacity(offset / (Field / 2));
        }
        PlaceMarkers(heading, perDegree);
        UpdateFooter(heading);
    }

    private void UpdateFooter(float heading)
    {
        int rounded = Mathf.RoundToInt(heading) % 360;
        string direction = Points[Mathf.RoundToInt(heading / 45f) % 8];
        bool changed = rounded != shownBearing || direction != shownDirection;
        shownBearing = rounded;
        shownDirection = direction;
        if (Time.unscaledTime >= nextContext)
        {
            nextContext = Time.unscaledTime + 0.5f;
            string biome = Player.m_localPlayer.GetCurrentBiomeData().GetName().ToUpperInvariant();
            string value = "  ·  " + biome;
            if (EnvMan.instance) value += "  ·  DAY " + EnvMan.instance.GetDay();
            if (shownContext != value)
            {
                shownContext = value;
                changed = true;
            }
        }
        if (!changed) return;
        // One TMP line shares a real glyph baseline and handles all spacing itself.
        // The bold face is Barlow 700, explicitly linked from the label's 600 face.
        footer!.text = "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Gold) +
            "><size=138%><b>" + rounded + "° " + direction + "</b></size></color>" + shownContext;
        footerOutline!.Sync();
    }

    private void PlaceMarkers(float heading, float perDegree)
    {
        Vector3 me = Player.m_localPlayer.transform.position;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Scan(me);
        }
        while (markers.Count < targets.Count) markers.Add(new Marker(theme, root!));
        labelBounds.Clear();
        for (int i = 0; i < markers.Count; i++)
        {
            Marker marker = markers[i];
            if (i >= targets.Count)
            {
                marker.Hide();
                continue;
            }
            var (position, color, name, follow) = targets[i];
            if (follow) position = follow!.position;
            Vector3 to = position - me;
            float offset = Mathf.DeltaAngle(heading, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (Mathf.Abs(offset) >= Field / 2)
            {
                marker.Hide();
                continue;
            }
            float x = offset * perDegree;
            float fade = SideOpacity(offset / (Field / 2));
            marker.Show(color, x, name + " " + Distance(new Vector2(to.x, to.z).magnitude), fade);
            float left = x - marker.LabelWidth / 2, right = x + marker.LabelWidth / 2;
            bool showLabel = left >= -Width / 2 && right <= Width / 2;
            foreach (var bound in labelBounds)
                if (left < bound.Right + 6 && right > bound.Left - 6) showLabel = false;
            marker.ShowLabel(showLabel);
            if (showLabel) labelBounds.Add((left, right));
        }
    }

    private void Scan(Vector3 me)
    {
        targets.Clear();
        PlayerProfile? profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
        if (settings.CompassHome.Value && profile != null && profile.HaveCustomSpawnPoint())
            targets.Add((profile.GetCustomSpawnPoint(), Palette.Bone, "Home", null));

        if (settings.CompassPortals.Value && ZDOMan.instance != null)
        {
            float range = settings.CompassPortalRange.Value;
            List<(float Distance, Vector3 Position)> portals = new();
            foreach (ZDO zdo in ZDOMan.instance.GetPortalList())
            {
                Vector3 position = zdo.GetPosition();
                float distance = Vector3.Distance(me, position);
                if (distance <= range && distance > 3f) portals.Add((distance, position));
            }
            portals.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < portals.Count && i < 6; i++)
                targets.Add((portals[i].Position, Palette.Eitr, "Portal", null));
        }
        if (settings.CompassShips.Value)
        {
            Ship aboard = Ship.GetLocalShip();
            foreach (IMonoUpdater updater in Ship.Instances)
                if (updater is Ship ship && ship && ship != aboard && Vector3.Distance(me, ship.transform.position) <= 400f)
                    targets.Add((ship.transform.position, Palette.Bone, "Ship", ship.transform));
        }
        if (settings.CompassPlayers.Value && ZNet.instance != null)
        {
            ZDOID self = Player.m_localPlayer.GetZDOID();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (!info.m_publicPosition || info.m_characterID == self || info.m_characterID.IsNone()) continue;
                Transform? follow = null;
                foreach (Player other in Player.GetAllPlayers())
                    if (other && other.GetZDOID() == info.m_characterID) follow = other.transform;
                targets.Add((info.m_position, Palette.Gold, info.m_name, follow));
            }
        }
    }

    private static string Distance(float metres) =>
        metres >= 1000f ? (metres / 1000f).ToString("0.0") + "km" : Mathf.RoundToInt(metres) + "m";

    // Full contrast in the middle; smooth, symmetric fade over the outer 20% at each end.
    // Shared by the moving marks and the rail texture so no hard line remains at the edges.
    private static float SideOpacity(float normalizedX) =>
        Mathf.SmoothStep(0, 1, (1 - Mathf.Abs(normalizedX)) / EdgeFadeFraction);

    private void BuildRail()
    {
        const int samples = 256;
        railTexture = new Texture2D(samples, 1, TextureFormat.RGBA32, false)
        {
            name = "Ironbound compass side fade",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[samples];
        for (int x = 0; x < samples; x++)
            pixels[x] = Color.white.WithAlpha(SideOpacity(2f * x / (samples - 1) - 1));
        railTexture.SetPixels(pixels);
        railTexture.Apply(false, true);
        railSprite = Sprite.Create(railTexture, new Rect(0, 0, samples, 1), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect);
    }

    private sealed class Marker
    {
        private readonly RectTransform rect;
        private readonly CanvasGroup fade;
        private readonly Image icon;
        private readonly TextMeshProUGUI label;
        private readonly TextOutline outline;
        private string shown = "";
        internal float LabelWidth { get; private set; } = 40;

        internal Marker(Theme theme, Transform parent)
        {
            rect = Ugui.Node(parent, "Marker");
            Top(rect, 0, 36, 110, 34);
            fade = rect.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = fade.interactable = false;
            icon = Ugui.Image(rect, "Diamond", theme.Diamond, Palette.Bone);
            Top(icon.rectTransform, 0, 0, 12, 12);
            label = Ugui.Text(rect, "Label", "", theme.Strong, 11.5f, Palette.Bone, TextAlignmentOptions.Center);
            Top(label.rectTransform, 0, 13, 110, 19);
            label.richText = false; // Player names are plain text, never TMP markup.
            outline = new TextOutline(label);
        }

        internal void Show(Color color, float x, string text, float alpha)
        {
            if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
            rect.anchoredPosition = new Vector2(x, -36);
            icon.color = color;
            fade.alpha = alpha;
            if (text != shown)
            {
                label.text = shown = text;
                LabelWidth = Mathf.Clamp(label.GetPreferredValues(text).x + 4, 40, 110);
            }
        }

        internal void ShowLabel(bool show)
        {
            label.enabled = show;
            outline.Sync();
        }

        internal void Hide()
        {
            if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
        }
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        Shown = null;
        footer = null;
        footerOutline = null;
        if (railSprite) UnityEngine.Object.Destroy(railSprite!);
        if (railTexture) UnityEngine.Object.Destroy(railTexture!);
        railSprite = null;
        railTexture = null;
        marks.Clear();
        markers.Clear();
        targets.Clear();
        labelBounds.Clear();
        shownBearing = -1;
        shownContext = shownDirection = "";
        nextScan = nextContext = 0;
    }

    private void Build(Transform parent)
    {
        root = Ugui.Node(parent, "Ironbound Compass");
        Top(root, 0, 14, Width, Height);
        RectTransform band = Ugui.Node(root, "Band").At(0, 0, Width, Height);
        BuildRail();
        // A one-pixel rail with a faint dark underlay for bright terrain.
        Ugui.Image(root, "Rule Shadow", railSprite, Palette.Ink.WithAlpha(0.5f)).At(0, RailY - 0.5f, Width, 2);
        Ugui.Image(root, "Rule", railSprite, Palette.Bone.WithAlpha(0.7f)).At(0, RailY, Width, 0.75f);
        RectTransform track = Ugui.Node(root, "Track").At(0, 0, Width, 38);
        track.gameObject.AddComponent<RectMask2D>();
        for (int degrees = 0; degrees < 360; degrees += 5)
        {
            RectTransform mark = Mark(track, degrees, "Tick " + degrees, 1, 16);
            float height = degrees % 45 == 0 ? 12 : degrees % 15 == 0 ? 8 : 4;
            Image tick = Ugui.Image(mark, "Tick", theme.White, Palette.Bone.WithAlpha(0.8f));
            tick.At(0, RailY - height / 2, 0.75f, height);
            if (degrees % 45 != 0) continue;
            RectTransform letter = Mark(track, degrees, Points[degrees / 45], 60, 26);
            bool cardinal = degrees % 90 == 0;
            TextMeshProUGUI text = Ugui.Text(letter, "Letter", Points[degrees / 45], theme.Heading,
                cardinal ? 22 : 16, degrees == 0 ? Palette.Gold : Palette.Bone, TextAlignmentOptions.Center);
            text.Stretch();
            text.overflowMode = TextOverflowModes.Overflow;
            _ = new TextOutline(text);
        }
        footer = Ugui.Text(root, "Bearing Biome Day", "", theme.Strong, 13, Palette.Bone,
            TextAlignmentOptions.Bottom, spacing: 1.5f);
        Top(footer.rectTransform, 0, 70, Width, 24);
        footer.enableAutoSizing = true;
        footer.fontSizeMin = 10;
        footer.fontSizeMax = 13;
        footer.maxVisibleLines = 1;
        footerOutline = new TextOutline(footer);
    }

    private RectTransform Mark(Transform parent, float degrees, string name, float width, float height)
    {
        RectTransform rect = Ugui.Node(parent, name);
        Top(rect, 0, 0, width, height);
        CanvasGroup fade = rect.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = fade.interactable = false;
        marks.Add((degrees, rect, fade));
        return rect;
    }

    private static void Top(RectTransform rect, float x, float y, float width, float height, float pivot = 0.5f)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
        rect.pivot = new Vector2(pivot, 1);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }
}
