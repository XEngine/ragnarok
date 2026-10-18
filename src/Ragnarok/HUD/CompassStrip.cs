using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// A heading strip at the top centre of the screen, like the compass bars of Assassin's Creed: the
// marks slide as the camera turns, the crimson notch is where you look, the number below is the exact
// bearing. North is +Z, the top of Valheim's map. Shown with either HUD style.
internal sealed class CompassStrip : IDisposable
{
    private const float Width = 560, Height = 30, Field = 180; // degrees visible across the strip

    private static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(float Degrees, Graphic Mark, bool Letter)> marks = new();
    private GameObject? hudRoot;
    private RectTransform? root, track;
    private TextMeshProUGUI? bearing;
    private HudBackdrop? backdrop;
    private readonly List<Marker> markers = new();
    // Follow: a loaded character or ship to track every frame; Position is used when it is gone.
    private readonly List<(Vector3 Position, Sprite Icon, Color Color, string Name, Transform? Follow)> targets = new();
    private float nextScan;
    private int shownBearing = -1;

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
            root = null;
            marks.Clear();
            markers.Clear();
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
        root.localScale = Vector3.one * settings.HudScale.Value;
        backdrop!.Apply(settings.CompassBackground, settings.CompassBackgroundOpacity);

        Vector3 forward = GameCamera.instance.transform.forward;
        float heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
        float perDegree = Width / Field;
        foreach (var (degrees, mark, letter) in marks)
        {
            float offset = Mathf.DeltaAngle(heading, degrees);
            bool visible = Mathf.Abs(offset) < Field / 2;
            if (mark.enabled != visible) mark.enabled = visible;
            if (!visible) continue;
            RectTransform rect = mark.rectTransform;
            rect.anchoredPosition = new Vector2(offset * perDegree, rect.anchoredPosition.y);
            // Fade towards the ends so marks slide in and out instead of popping.
            float fade = Mathf.Clamp01((Field / 2 - Mathf.Abs(offset)) / 25f);
            Color c = mark.color;
            c.a = (letter ? 1f : 0.7f) * fade;
            mark.color = c;
        }
        bool centred = PlaceMarkers(heading, perDegree);
        // A marker under the notch takes the bearing's place.
        if (bearing!.enabled == centred) bearing.enabled = !centred;
        int rounded = Mathf.RoundToInt(heading) % 360;
        if (rounded != shownBearing)
        {
            shownBearing = rounded;
            bearing.text = rounded + "°";
        }
    }

    // Home, portals and ships as icons under the strip at their bearing, with the distance beneath.
    // The world is scanned twice a second; positions are updated every frame. Returns whether one sits
    // near the centre.
    private bool PlaceMarkers(float heading, float perDegree)
    {
        Vector3 me = Player.m_localPlayer.transform.position;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Scan(me);
        }
        while (markers.Count < targets.Count) markers.Add(new Marker(theme, root!));
        bool centred = false;
        for (int i = 0; i < markers.Count; i++)
        {
            Marker marker = markers[i];
            if (i >= targets.Count)
            {
                marker.Hide();
                continue;
            }
            var (position, icon, color, name, follow) = targets[i];
            if (follow) position = follow!.position;
            Vector3 to = position - me;
            float offset = Mathf.DeltaAngle(heading, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            if (Mathf.Abs(offset) >= Field / 2)
            {
                marker.Hide();
                continue;
            }
            float fade = Mathf.Clamp01((Field / 2 - Mathf.Abs(offset)) / 25f);
            marker.Show(icon, color, offset * perDegree, name, Distance(new Vector2(to.x, to.z).magnitude), fade);
            centred |= Mathf.Abs(offset) < 8f;
        }
        return centred;
    }

    private void Scan(Vector3 me)
    {
        targets.Clear();
        PlayerProfile? profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
        if (settings.CompassHome.Value && profile != null && profile.HaveCustomSpawnPoint())
            targets.Add((profile.GetCustomSpawnPoint(), theme.IconHome, Palette.GoldBright, "", null));

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
            for (int i = 0; i < portals.Count && i < 6; i++) targets.Add((portals[i].Position, theme.IconPortal, Palette.Eitr, "", null));
        }

        if (settings.CompassShips.Value)
        {
            Ship aboard = Ship.GetLocalShip();
            foreach (IMonoUpdater updater in Ship.Instances)
                if (updater is Ship ship && ship && ship != aboard && Vector3.Distance(me, ship.transform.position) <= 400f)
                    targets.Add((ship.transform.position, theme.IconShip, Palette.Bone, "", ship.transform));
        }

        // Other players, only those who share their position ("visible to other players" on the map).
        // Nearby ones are read from their character, the rest from the server's player list.
        if (settings.CompassPlayers.Value && ZNet.instance != null)
        {
            ZDOID self = Player.m_localPlayer.GetZDOID();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (!info.m_publicPosition || info.m_characterID == self || info.m_characterID.IsNone()) continue;
                Transform? follow = null;
                foreach (Player other in Player.GetAllPlayers())
                    if (other && other.GetZDOID() == info.m_characterID) follow = other.transform;
                targets.Add((info.m_position, theme.IconPlayer, Palette.Stamina, info.m_name, follow));
            }
        }
    }

    private static string Distance(float metres) =>
        metres >= 1000f ? (metres / 1000f).ToString("0.0") + "km" : Mathf.RoundToInt(metres) + "m";

    private sealed class Marker
    {
        private readonly RectTransform rect;
        private readonly Image icon;
        private readonly TextMeshProUGUI distance;
        private string shown = "";

        internal Marker(Theme theme, Transform parent)
        {
            rect = Ugui.Node(parent, "Marker");
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(44, 34);
            icon = Ugui.Image(rect, "Icon", null, Color.white);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1);
            icon.rectTransform.pivot = new Vector2(0.5f, 1);
            icon.rectTransform.sizeDelta = new Vector2(18, 18);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            distance = Ugui.Text(rect, "Distance", "", theme.Strong, 10, Palette.Bone, TextAlignmentOptions.Top);
            distance.rectTransform.anchorMin = distance.rectTransform.anchorMax = new Vector2(0.5f, 1);
            distance.rectTransform.pivot = new Vector2(0.5f, 1);
            distance.rectTransform.sizeDelta = new Vector2(90, 24);
            distance.rectTransform.anchoredPosition = new Vector2(0, -19);
            distance.overflowMode = TextOverflowModes.Overflow;
        }

        // A named marker (a player) shows the name over the distance.
        internal void Show(Sprite sprite, Color color, float x, string name, string range, float fade)
        {
            string label = name.Length == 0 ? range : $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}</color>\n{range}";
            if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
            rect.anchoredPosition = new Vector2(x, -(Height + 3));
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.color = color.WithAlpha(fade);
            distance.color = Palette.Bone.WithAlpha(0.85f * fade);
            if (label != shown) distance.text = shown = label;
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
        marks.Clear();
        markers.Clear();
    }

    private void Build(Transform parent)
    {
        root = Ugui.Node(parent, "Ragnarok Compass");
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1);
        root.pivot = new Vector2(0.5f, 1);
        root.anchoredPosition = new Vector2(0, -14);
        root.sizeDelta = new Vector2(Width, Height + 20);

        // The vitals' backdrop (shade or plate, per [HUD.Compass]), behind the strip only.
        RectTransform band = Ugui.Node(root, "Band").At(0, 0, Width, Height);
        backdrop = new HudBackdrop(theme, band, new Vector4(30, 8, 30, 8));
        Ugui.Image(root, "Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(40, Height - 1, Width - 80, 1);
        track = Ugui.Node(root, "Track").At(0, 0, Width, Height);
        track.gameObject.AddComponent<RectMask2D>();

        for (int d = 0; d < 360; d += 15)
        {
            bool letter = d % 45 == 0;
            if (letter)
            {
                bool cardinal = d % 90 == 0;
                TextMeshProUGUI text = Ugui.Text(track, Points[d / 45], Points[d / 45], theme.Strong,
                    cardinal ? 17 : 12, d == 0 ? Palette.Alert : cardinal ? Palette.Gold : Palette.Bone, TextAlignmentOptions.Center);
                Centre(text.rectTransform, 40, Height, 0);
                text.overflowMode = TextOverflowModes.Overflow;
                marks.Add((d, text, true));
            }
            else
            {
                Image tick = Ugui.Image(track, "Tick " + d, theme.White, Palette.Muted);
                Centre(tick.rectTransform, 1, 8, -(Height / 2 - 6));
                marks.Add((d, tick, false));
            }
        }

        // Where you look: a crimson notch over the strip, the exact bearing beneath it.
        Image notch = Ugui.Image(root, "Notch", theme.Diamond, Palette.GoldBright);
        notch.rectTransform.anchorMin = notch.rectTransform.anchorMax = new Vector2(0.5f, 1);
        notch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        notch.rectTransform.anchoredPosition = new Vector2(0, -Height);
        notch.rectTransform.sizeDelta = new Vector2(10, 10);
        bearing = Ugui.Text(root, "Bearing", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Top);
        bearing.rectTransform.anchorMin = bearing.rectTransform.anchorMax = new Vector2(0.5f, 1);
        bearing.rectTransform.pivot = new Vector2(0.5f, 1);
        bearing.rectTransform.anchoredPosition = new Vector2(0, -Height - 6);
        bearing.rectTransform.sizeDelta = new Vector2(60, 16);
        bearing.overflowMode = TextOverflowModes.Overflow;
    }

    // Mark centred horizontally in the track, vertically offset from its middle.
    private static void Centre(RectTransform rect, float width, float height, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0, y);
    }
}
