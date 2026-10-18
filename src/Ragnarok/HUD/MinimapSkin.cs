using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// The Ragnarök minimap, after the style guide: the map cut round in a rope-edged brass ring, the biome's
// name inside the bottom of the circle, and below it two medallions (Valheim's wind arrow, and a compass
// needle that turns with the camera) with the map key between them. Valheim's small map keeps working (map,
// pins, markers, biome name); its frame art is hidden, its mask drawn round, and every change is undone on
// Vanilla or unload.
internal sealed class MinimapSkin : IDisposable
{
    private const float MedallionSize = 34, KeyWidth = 24, KeyHeight = 21;

    // How far above and below the map the skin's own parts reach, for the day and weather strips.
    internal static float AboveMap, BelowMap;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<(UnityEngine.Object Owner, Action Restore)> undo = new();
    private readonly Vector3[] corners = new Vector3[4];
    private Minimap? minimap;
    private RectTransform? front, ring, wind, compass, needle, keyCap;
    private RectTransform? clip;
    private TextMeshProUGUI? key, biome, biomeShadow;
    private TMP_Text? vanillaBiome;
    private float nextKeyRefresh;

    internal MinimapSkin(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    private bool Wanted => settings.MinimapStyle.Value switch
    {
        PanelStyle.Ragnarok => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    internal void Tick()
    {
        Minimap current = Minimap.instance;
        if (current != minimap)
        {
            Dispose();
            minimap = current;
        }
        if (!minimap || !Wanted)
        {
            Dispose();
            return;
        }
        if (!front) Build(minimap!);
        if (!minimap!.m_smallRoot.activeInHierarchy) return;
        Place(minimap);
    }

    public void Dispose()
    {
        for (int i = undo.Count - 1; i >= 0; i--)
            if (undo[i].Owner) undo[i].Restore();
        undo.Clear();
        if (front) UnityEngine.Object.Destroy(front!.gameObject);
        front = null;
        AboveMap = BelowMap = 0;
    }

    private void Build(Minimap map)
    {
        Transform root = map.m_smallRoot.transform;
        HideFrameArt(map);
        clip = RoundMask(map);
        HideVanillaBiome(map.m_biomeNameSmall);

        front = Ugui.Node(root, "Ragnarok Minimap Rim");
        front.SetAsLastSibling();
        ring = Ugui.Image(front, "Ring", theme.Ring, Color.white).rectTransform;
        ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
        // North: a small red diamond on top of the ring.
        Image north = Ugui.Image(ring, "North", theme.Diamond, Palette.GoldBright);
        north.rectTransform.anchorMin = north.rectTransform.anchorMax = new Vector2(0.5f, 1);
        north.rectTransform.sizeDelta = new Vector2(11, 11);
        north.rectTransform.anchoredPosition = new Vector2(0, -6);

        foreach (var cardinal in new[] { ("N", 0f, 1f), ("E", 1f, 0f), ("S", 0f, -1f), ("W", -1f, 0f) })
        {
            var label = Ugui.Text(ring, cardinal.Item1, cardinal.Item1, theme.Strong, 11, Palette.GoldBright, TextAlignmentOptions.Center);
            var r = label.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f + cardinal.Item2 * 0.43f, 0.5f + cardinal.Item3 * 0.43f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(18, 18);
        }
        wind = Medallion("Wind");
        compass = Medallion("Compass");
        needle = Ugui.Image(compass, "Needle", theme.Diamond, Palette.GoldBright).rectTransform;
        needle.anchorMin = needle.anchorMax = needle.pivot = new Vector2(0.5f, 0.5f);
        needle.sizeDelta = new Vector2(7, 24);
        Image hub = Ugui.Image(compass, "Hub", theme.Diamond, Palette.Bone);
        hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hub.rectTransform.sizeDelta = new Vector2(5, 5);

        // The biome's name inside the bottom of the circle, bone over a soft shadow (it sits on the map).
        biomeShadow = Ugui.Text(front, "Biome Shadow", "", theme.Strong, 14, Color.black.WithAlpha(0.8f), TextAlignmentOptions.Center);
        biome = Ugui.Text(front, "Biome", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Center);

        Image cap = Ugui.Image(front, "Map Key", theme.Button, Color.white);
        keyCap = cap.rectTransform;
        keyCap.anchorMin = keyCap.anchorMax = keyCap.pivot = new Vector2(0.5f, 0.5f);
        keyCap.sizeDelta = new Vector2(KeyWidth, KeyHeight);
        key = Ugui.Text(keyCap, "Label", "M", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch(1, 0, 1, 0);
        key.overflowMode = TextOverflowModes.Overflow;

        foreach (Graphic g in front.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

        // Valheim's wind arrow moves into the left medallion (Valheim keeps turning it). It lives inside the
        // map picture, so it comes out of the round clip, or the clip would hide it.
        if (map.m_windMarker)
        {
            RectTransform marker = map.m_windMarker;
            Transform parent = marker.parent;
            int index = marker.GetSiblingIndex();
            Vector3 position = marker.localPosition, scale = marker.localScale;
            marker.SetParent(front, true);
            undo.Add((marker, () =>
            {
                if (parent) marker.SetParent(parent, true);
                marker.SetSiblingIndex(index);
                marker.localPosition = position;
                marker.localScale = scale;
            }));
        }
    }

    private RectTransform Medallion(string name)
    {
        RectTransform rect = Ugui.Image(front!, name, theme.Medallion, Color.white).rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(MedallionSize, MedallionSize);
        return rect;
    }

    // The map cut round: the map picture moves into a disc mask of its own, made exactly where the picture
    // was (same anchors, size and place among its siblings), so Valheim's layout of the map is unchanged.
    // Whatever Valheim keeps inside the picture (pins, markers) is clipped with it. Undone on unload.
    private RectTransform? RoundMask(Minimap map)
    {
        RectTransform picture = map.m_mapImageSmall.rectTransform;
        Transform parent = picture.parent;
        int index = picture.GetSiblingIndex();
        Vector2 anchorMin = picture.anchorMin, anchorMax = picture.anchorMax, pivot = picture.pivot;
        Vector2 position = picture.anchoredPosition, size = picture.sizeDelta;
        Quaternion rotation = picture.localRotation;
        Vector3 scale = picture.localScale;

        RectTransform clip = Ugui.Node(parent, "Ragnarok Map Clip");
        clip.SetSiblingIndex(index);
        clip.anchorMin = anchorMin;
        clip.anchorMax = anchorMax;
        clip.pivot = pivot;
        clip.anchoredPosition = position;
        clip.sizeDelta = size;
        clip.localRotation = rotation;
        clip.localScale = scale;
        Image disc = clip.gameObject.AddComponent<Image>();
        disc.sprite = theme.Disc;
        disc.preserveAspect = true;
        disc.raycastTarget = false;
        clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        picture.SetParent(clip, false);
        picture.anchorMin = Vector2.zero;
        picture.anchorMax = Vector2.one;
        picture.pivot = new Vector2(0.5f, 0.5f);
        picture.anchoredPosition = Vector2.zero;
        picture.sizeDelta = Vector2.zero;
        picture.localRotation = Quaternion.identity;
        picture.localScale = Vector3.one;
        undo.Add((picture, () =>
        {
            picture.SetParent(parent, false);
            picture.SetSiblingIndex(index);
            picture.anchorMin = anchorMin;
            picture.anchorMax = anchorMax;
            picture.pivot = pivot;
            picture.anchoredPosition = position;
            picture.sizeDelta = size;
            picture.localRotation = rotation;
            picture.localScale = scale;
            if (clip) UnityEngine.Object.Destroy(clip.gameObject);
        }));
        return clip;
    }

    private static readonly string[] StencilProperties =
        { "_Stencil", "_StencilComp", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask" };
    private readonly float[] stencil = new float[6];

    // A mask draws the map with its own copy of the map's material, made once. Valheim keeps changing the
    // original (the explored-area texture, zoom and pan), so the copy is brought up to date every frame,
    // keeping its own stencil settings (which do the clipping).
    private void SyncMapMaterial(Graphic picture)
    {
        Material source = picture.material, drawn = picture.materialForRendering;
        if (!source || !drawn || drawn == source) return;
        for (int i = 0; i < StencilProperties.Length; i++)
            stencil[i] = drawn.HasProperty(StencilProperties[i]) ? drawn.GetFloat(StencilProperties[i]) : 0;
        drawn.CopyPropertiesFromMaterial(source);
        for (int i = 0; i < StencilProperties.Length; i++)
            if (drawn.HasProperty(StencilProperties[i])) drawn.SetFloat(StencilProperties[i], stencil[i]);
    }

    // Hide the frame's decorative images. Kept: the map picture (a RawImage), pins and their names,
    // the player, ship and wind markers, and any Mask graphic (masks clip the map; hiding one would
    // hide the map with it).
    private void HideFrameArt(Minimap map)
    {
        HashSet<Transform> keep = new();
        foreach (Component c in new Component[]
                 {
                     map.m_mapImageSmall, map.m_pinRootSmall, map.m_pinNameRootSmall, map.m_smallMarker, map.m_smallShipMarker,
                     map.m_windMarker, map.m_biomeNameSmall
                 })
            if (c) keep.Add(c.transform);

        foreach (Image image in map.m_smallRoot.GetComponentsInChildren<Image>(true))
        {
            if (!image.enabled || image.GetComponent<Mask>() || Kept(image.transform, keep)) continue;
            Image target = image;
            target.enabled = false;
            undo.Add((target, () => target.enabled = true));
        }
    }

    private static bool Kept(Transform t, HashSet<Transform> keep)
    {
        // Ragnarök's own parts here (day/clock and weather strips) are never frame art.
        for (Transform? p = t; p != null; p = p.parent)
            if (keep.Contains(p) || p.name.StartsWith("Ragnarok", StringComparison.Ordinal)) return true;
        return false;
    }

    // Valheim's biome label stays (it is written to and animated) but is not drawn; ours copies its text.
    private void HideVanillaBiome(TMP_Text text)
    {
        vanillaBiome = text;
        if (!text) return;
        bool added = !text.TryGetComponent(out CanvasGroup group);
        if (added) group = text.gameObject.AddComponent<CanvasGroup>();
        float alpha = group.alpha;
        group.alpha = 0;
        undo.Add((text, () =>
        {
            if (added) UnityEngine.Object.Destroy(group);
            else group.alpha = alpha;
        }));
    }

    // Everything follows the round map (it can move: resolution, MinimapInfo making room).
    private void Place(Minimap map)
    {
        RectTransform area = clip ? clip! : map.m_mapImageSmall.rectTransform;
        area.GetWorldCorners(corners);
        RectTransform parent = (RectTransform)front!.parent;
        Vector2 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
        Vector2 centre = (min + max) / 2;
        float radius = Mathf.Min(max.x - min.x, max.y - min.y) / 2;

        front.anchorMin = front.anchorMax = front.pivot = new Vector2(0.5f, 0.5f);
        front.sizeDelta = Vector2.zero;
        Vector2 local = centre - parent.rect.center;
        if ((front.anchoredPosition - local).sqrMagnitude > 0.01f) front.anchoredPosition = local;

        float outer = radius * 128f / Theme.RingInner;
        ring!.sizeDelta = new Vector2(outer * 2, outer * 2);
        // The medallions sit on the ring's lower diagonals, the map key between them under the ring.
        float diagonal = outer * 0.65f;
        wind!.anchoredPosition = new Vector2(-diagonal, -outer - 18);
        compass!.anchoredPosition = new Vector2(diagonal, -outer - 18);
        keyCap!.anchoredPosition = new Vector2(0, -outer - 4 - KeyHeight / 2);
        AboveMap = outer - radius;
        BelowMap = outer - radius + 38;

        SyncMapMaterial(map.m_mapImageSmall);
        Camera camera = Utils.GetMainCamera();
        if (camera) needle!.localEulerAngles = new Vector3(0, 0, camera.transform.eulerAngles.y);
        if (map.m_windMarker) map.m_windMarker.position = wind.position;

        // Our biome label shows what Valheim writes into its own (hidden) one.
        string name = vanillaBiome ? vanillaBiome!.text : "";
        if (biome!.text != name) biome.text = biomeShadow!.text = name;
        biome.At(-radius, radius - 30, radius * 2, 22);
        biomeShadow!.At(-radius + 1, radius - 28.5f, radius * 2, 22);
        if (Time.unscaledTime >= nextKeyRefresh)
        {
            nextKeyRefresh = Time.unscaledTime + 1f;
            string bound = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("Map", true) : "";
            if (!string.IsNullOrEmpty(bound) && Localization.instance != null) bound = Localization.instance.Localize(bound);
            key!.text = string.IsNullOrEmpty(bound) ? "M" : bound.ToUpperInvariant();
        }
    }
}
