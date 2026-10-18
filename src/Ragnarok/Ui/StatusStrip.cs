using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

internal struct HudStatus
{
    internal int Key;
    internal string Name;      // Localization token, localized when the strip is rebuilt.
    internal string Value;     // Timer or other icon text; empty when the effect has none.
    internal Sprite? Icon;
    internal bool Flash;
    internal bool Comfort;     // Resting, campfire or shelter: what the comfort level comes from.

    internal static readonly int[] ComfortSources =
        { SEMan.s_statusEffectResting, SEMan.s_statusEffectCampFire, SEMan.s_statusEffectShelter };
}

// The status effects after the style guide: a column of rows, each a light icon and the effect's name
// with its time ("Rested 8:29", "Feather Fall"), hung from a gold rail with a diamond at its head and the
// baseline's end cap at its foot. Resting reads as the comfort level ("Comfort 9"); warmth and shelter keep
// their own rows. New effects fade in.
internal sealed class StatusStrip : IDisposable
{
    private const float IconSize = 20, Indent = 16;

    private readonly Theme theme;
    private readonly MonochromeIcons icons;
    private readonly RectTransform root;
    private readonly VerticalLayoutGroup layout;
    private readonly HudBackdrop backdrop;
    private readonly List<int> signature = new();
    private readonly List<int> scratch = new();
    private readonly Dictionary<int, (TextMeshProUGUI Text, string Label)> values = new();
    private readonly Dictionary<int, Image> iconImages = new();
    private readonly Dictionary<int, (CanvasGroup Group, float Born)> fades = new();
    private readonly HashSet<int> shown = new();
    private readonly TextMeshProUGUI labelsKey;

    internal StatusStrip(Theme theme, MonochromeIcons icons, Transform parent)
    {
        this.theme = theme;
        this.icons = icons;
        root = Ugui.Node(parent, "Ragnarok Status");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1, 0);
        root.anchoredPosition = new Vector2(-40, 40);
        layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.padding = new RectOffset((int)Indent, 0, 34, 12);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        backdrop = new HudBackdrop(theme, root, new Vector4(16, 6, 20, 6));

        // The rail down the left side, the labels key in a cap at its head and the end cap at its foot.
        RectTransform rail = Ugui.Image(root, "Rail", theme.White, Palette.Gold.WithAlpha(0.8f)).rectTransform;
        rail.anchorMin = new Vector2(0, 0);
        rail.anchorMax = new Vector2(0, 1);
        rail.pivot = new Vector2(0, 0.5f);
        rail.offsetMin = new Vector2(3, 2);
        rail.offsetMax = new Vector2(4, -24);
        RectTransform head = Ugui.Image(root, "Rail Head", theme.Button, Color.white).rectTransform;
        head.anchorMin = head.anchorMax = head.pivot = new Vector2(0, 1);
        head.sizeDelta = new Vector2(24, 22);
        head.anchoredPosition = new Vector2(-8, 0);
        labelsKey = Ugui.Text(head, "Key", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch(2, 0, 2, 0);
        labelsKey.overflowMode = TextOverflowModes.Overflow;
        RectTransform foot = Ugui.Image(root, "Rail Foot", theme.Cap, Palette.Gold).rectTransform;
        foot.anchorMin = foot.anchorMax = new Vector2(0, 0);
        foot.pivot = Vector2.zero;
        foot.sizeDelta = new Vector2(13, 13);
        foot.anchoredPosition = new Vector2(2, 2);
        foreach (RectTransform part in new[] { rail, head, foot })
            part.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
    }

    internal void SetVisible(bool visible)
    {
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
    }

    internal void SetScale(float scale) => root.localScale = Vector3.one * scale;

    internal RectTransform Rect => root;

    internal void Place(HudPlacement placement)
    {
        placement.Apply(root);
        backdrop.Apply(placement);
    }

    // `labels`: name each effect, or show only its icon and time. `key`: the key that flips that.
    internal void Set(List<HudStatus> statuses, int comfort, bool labels, string key)
    {
        if (labelsKey.text != key) labelsKey.text = key;
        scratch.Clear();
        foreach (HudStatus status in statuses)
            scratch.Add(status.Key * 2 + (string.IsNullOrEmpty(status.Value) ? 0 : 1));
        if (!Same(scratch, signature)) Rebuild(statuses);

        float now = Time.unscaledTime;
        foreach (HudStatus status in statuses)
        {
            if (values.TryGetValue(status.Key, out var value))
            {
                string figure = status.Key == SEMan.s_statusEffectResting ? comfort.ToString() : Timer(status.Value);
                SetText(value.Text, !labels ? figure : figure.Length > 0 ? value.Label + "  " + figure : value.Label);
            }
            if (iconImages.TryGetValue(status.Key, out Image icon))
                icon.color = status.Flash && Mathf.Sin(now * 10f) > 0 ? Palette.Ember : Palette.Bone;
        }

        // New effects fade in so a change is noticed without an animation that lingers.
        foreach (KeyValuePair<int, (CanvasGroup Group, float Born)> fade in fades)
            if (fade.Value.Group) fade.Value.Group.alpha = Mathf.Clamp01((now - fade.Value.Born) / 0.3f);
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root.gameObject);
    }

    private static readonly string[] Kept = { HudBackdrop.Name, "Rail", "Rail Head", "Rail Foot" };

    private void Rebuild(List<HudStatus> statuses)
    {
        signature.Clear();
        signature.AddRange(scratch);
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Array.IndexOf(Kept, child.name) < 0) UnityEngine.Object.Destroy(child.gameObject);
        }
        values.Clear();
        layers.Clear();
        iconImages.Clear();
        fades.Clear();

        HashSet<int> current = new();
        foreach (HudStatus status in statuses)
        {
            current.Add(status.Key);
            string label = status.Key == SEMan.s_statusEffectResting
                ? Localization.instance.Localize("$se_rested_comfort")
                : Localization.instance.Localize(status.Name);
            RectTransform row = Ugui.Node(root, status.Key.ToString());
            HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 10;
            line.childAlignment = TextAnchor.MiddleLeft;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = line.childForceExpandHeight = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 22;
            iconImages[status.Key] = Icon(row, status.Icon);
            TextMeshProUGUI text = Readable(row, "Label", label, 14);
            values[status.Key] = (text, label);
        }

        foreach (int key in current)
            if (!shown.Contains(key) && root.Find(key.ToString()) is { } added)
                fades[key] = (added.gameObject.AddComponent<CanvasGroup>(), Time.unscaledTime);
        shown.Clear();
        shown.UnionWith(current);
    }

    // Shadow copies of a readable text (they follow its value in SetText).
    private readonly Dictionary<TextMeshProUGUI, List<TextMeshProUGUI>> layers = new();

    private static readonly Vector2[] Edge = { new(-1, 0), new(1, 0), new(0, -1), new(0, 1) };

    // Scada bold in bone over a soft shadow and a faint dark edge (text copies, as TMP's outline would need
    // a material per font), so it reads over snow and fire without looking outlined.
    private TextMeshProUGUI Readable(Transform parent, string name, string value, float size)
    {
        TextMeshProUGUI holder = Ugui.Text(parent, name, value, theme.Strong, size, Color.clear);
        holder.overflowMode = TextOverflowModes.Overflow;
        List<TextMeshProUGUI> copies = new();
        copies.Add(Layer(holder, "Shadow", new Vector2(1f, -1.5f), new Color(0, 0, 0, 0.7f)));
        foreach (Vector2 offset in Edge) copies.Add(Layer(holder, "Edge", offset, new Color(0, 0, 0, 0.35f)));
        copies.Add(Layer(holder, "Face", Vector2.zero, Palette.Bone));
        layers[holder] = copies;
        return holder;
    }

    private static TextMeshProUGUI Layer(TextMeshProUGUI holder, string name, Vector2 offset, Color color)
    {
        TextMeshProUGUI copy = Ugui.Text(holder.transform, name, holder.text, holder.font, holder.fontSize, color, holder.alignment,
            holder.characterSpacing).Stretch();
        copy.overflowMode = TextOverflowModes.Overflow;
        copy.rectTransform.offsetMin += offset;
        copy.rectTransform.offsetMax += offset;
        return copy;
    }

    private Image Icon(RectTransform row, Sprite? sprite)
    {
        Sprite? mono = icons.Get(sprite);
        RectTransform slot = Ugui.Node(row, "Icon Slot");
        LayoutElement size = slot.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = size.preferredHeight = IconSize;
        Image shadow = Ugui.Image(slot, "Shadow", mono ?? theme.Diamond, Color.black.WithAlpha(0.6f)).Stretch(1, 1.5f, -1, -1.5f);
        shadow.preserveAspect = true;
        Image image = Ugui.Image(slot, "Icon", mono ?? theme.Diamond, Palette.Bone).Stretch(mono ? 0 : 5, mono ? 0 : 5, mono ? 0 : 5, mono ? 0 : 5);
        image.preserveAspect = true;
        return image;
    }

    // Fixed-width digits so timers do not jitter as they count down.
    private static string Timer(string value) => value.Length == 0 ? value : "<mspace=0.56em>" + value + "</mspace>";

    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text.text == value) return;
        text.text = value;
        if (layers.TryGetValue(text, out List<TextMeshProUGUI> copies))
            foreach (TextMeshProUGUI copy in copies) copy.text = value;
    }

    private static bool Same(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}

// Valheim's status icons are painted in colour; the strip shows them as light monochrome glyphs.
// Icons usually live in non-readable atlases, so each one is copied through a RenderTexture once.
internal sealed class MonochromeIcons : IDisposable
{
    private readonly Dictionary<Sprite, Sprite?> cache = new();

    internal Sprite? Get(Sprite? source)
    {
        if (!source) return null;
        if (cache.TryGetValue(source!, out Sprite? mono)) return mono;
        try { mono = Convert(source!); }
        catch (Exception) { mono = source; } // Fall back to the original colours.
        cache[source!] = mono;
        return mono;
    }

    public void Dispose()
    {
        foreach (KeyValuePair<Sprite, Sprite?> pair in cache)
        {
            if (!pair.Value || pair.Value == pair.Key) continue;
            UnityEngine.Object.Destroy(pair.Value!.texture);
            UnityEngine.Object.Destroy(pair.Value);
        }
        cache.Clear();
    }

    private static Sprite Convert(Sprite source)
    {
        Texture2D atlas = source.texture;
        Rect r = source.textureRect;
        RenderTexture previous = RenderTexture.active;
        RenderTexture temp = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
        Texture2D copy = new((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            Graphics.Blit(atlas, temp);
            RenderTexture.active = temp;
            copy.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temp);
        }

        Color[] pixels = copy.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            // Brightness keeps the glyph's internal detail; dark outlines fade out.
            float luma = p.r * 0.299f + p.g * 0.587f + p.b * 0.114f;
            float light = Mathf.Clamp01(Mathf.Pow(luma, 0.55f) * 1.25f);
            pixels[i] = new Color(1, 1, 1, p.a * Mathf.SmoothStep(0.05f, 0.55f, light));
        }
        copy.SetPixels(pixels);
        copy.Apply();
        return Sprite.Create(copy, new Rect(0, 0, copy.width, copy.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
    }
}
