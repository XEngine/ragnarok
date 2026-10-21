using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

internal struct HudStatus
{
    internal int Key;
    internal string Name;      // Localization token, localized when the strip is rebuilt.
    internal string Value;     // Timer or other icon text; empty when the effect has none.
    internal string? Description;
    internal Sprite? Icon;
    internal bool Flash;
    internal StatusCategory Category;
    internal bool Comfort;     // Resting, campfire or shelter: what the comfort level comes from.

    internal static readonly int[] ComfortSources =
        { SEMan.s_statusEffectResting, SEMan.s_statusEffectCampFire, SEMan.s_statusEffectShelter };
}

// Original game icons, grouped into passive/ongoing buffs or a separate harmful row.
internal sealed class StatusStrip : IDisposable
{
    private const float HeadingHeight = 18;
    private float CellWidth => debuffStyle ? 72 : 58;
    private float CellHeight => debuffStyle ? 82 : 76;
    private float TileWidth => debuffStyle ? 62 : 48;
    private float TileHeight => debuffStyle ? 78 : 72;
    private readonly Theme theme;
    private readonly bool centered;
    private readonly bool debuffStyle;
    private readonly RectTransform root;
    private readonly Dictionary<int, Tile> tiles = new();
    private readonly List<int> removed = new();
    private readonly TextMeshProUGUI[] headings = new TextMeshProUGUI[3];
    private readonly TextOutline[] headingOutlines = new TextOutline[3];
    private sealed class Tile
    {
        internal RectTransform Root = null!;
        internal Image Icon = null!;
        internal TextMeshProUGUI Timer = null!, Name = null!;
        internal TextOutline TimerOutline = null!, NameOutline = null!;
        internal CanvasGroup Fade = null!;
        internal float Born, NextTooltip;
        internal GuideTooltip Tooltip = null!;
    }

    internal StatusStrip(Theme theme, Transform parent, bool centered = false, bool debuffStyle = false)
    {
        this.theme = theme;
        this.centered = centered;
        this.debuffStyle = debuffStyle;
        root = Ugui.Node(parent, "Ragnarok Status");
        root.sizeDelta = new Vector2(CellWidth * 4, 0);
        for (int i = 0; i < headings.Length; i++)
        {
            headings[i] = Ugui.Text(root, "Status category", "", theme.Strong, 9, Palette.Muted,
                spacing: 1.2f).At(0, 0, CellWidth * 4, HeadingHeight);
            if (centered) headings[i].alignment = TextAlignmentOptions.Center;
            headingOutlines[i] = new TextOutline(headings[i]);
        }
    }

    internal RectTransform Rect => root;
    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);
    internal void SetScale(float scale) => root.localScale = Vector3.one * scale;
    internal void Place(HudPlacement placement) => placement.Apply(root);
    internal void BelowCompass(RectTransform? compass)
    {
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1);
        root.anchoredPosition = new Vector2(0, -132);
        if (compass && compass!.gameObject.activeInHierarchy)
            root.position = compass.TransformPoint(new Vector3(compass.rect.center.x, compass.rect.yMin - 12, 0));
        Utils.ClampUIToScreen(root);
    }

    internal void Set(List<HudStatus> statuses, int comfort)
    {
        removed.Clear();
        foreach (int id in tiles.Keys)
            if (!statuses.Exists(s => s.Key == id)) removed.Add(id);
        foreach (int id in removed)
        {
            tiles[id].Root.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(tiles[id].Root.gameObject);
            tiles.Remove(id);
        }
        float y = 0;
        float width = centered ? Mathf.Max(156, CellWidth * Mathf.Clamp(statuses.Count, 1, 4)) : CellWidth * 4;
        for (int category = 0; category < 3; category++)
        {
            int count = 0;
            foreach (HudStatus status in statuses) if ((int)status.Category == category) count++;
            var heading = headings[category];
            heading.gameObject.SetActive(count > 0);
            if (count == 0) continue;
            if (y > 0) y += 8;
            heading.text = category == 0 ? UiLocalization.Text("status.passive", "PASSIVE")
                : category == 1 ? UiLocalization.Text("status.ongoing", "ONGOING")
                : UiLocalization.Text("status.debuffs", "DEBUFFS");
            heading.rectTransform.anchoredPosition = new Vector2(0, -y);
            heading.rectTransform.sizeDelta = new Vector2(width, HeadingHeight);
            y += HeadingHeight;
            int index = 0;
            foreach (HudStatus status in statuses)
            {
                if ((int)status.Category != category) continue;
                if (!tiles.TryGetValue(status.Key, out Tile tile))
                {
                    tile = CreateTile();
                    tiles.Add(status.Key, tile);
                }
                int rowCount = Math.Min(4, count - index / 4 * 4);
                float inset = centered ? (width - (rowCount * CellWidth - 10)) / 2 : 0;
                tile.Root.At(inset + index % 4 * CellWidth, y + index / 4 * CellHeight, TileWidth, TileHeight);
                Update(tile, status, comfort);
                index++;
            }
            y += Mathf.CeilToInt(count / 4f) * CellHeight;
        }
        root.sizeDelta = new Vector2(width, y);
        foreach (TextOutline outline in headingOutlines) outline.Sync();
    }

    private Tile CreateTile()
    {
        var tile = new Tile { Root = Ugui.Node(root, "Status effect"), Born = Time.unscaledTime };
        Ugui.Image(tile.Root, "Tooltip hit area", null, Color.clear, raycast: true).Stretch();
        float iconSize = debuffStyle ? 48 : 32;
        float iconX = (TileWidth - iconSize) / 2;
        tile.Icon = Ugui.ItemIcon(Ugui.Image(tile.Root, "Original status icon", null, Color.white)).At(iconX, 0, iconSize, iconSize);
        tile.Icon.preserveAspect = true;
        tile.Timer = Ugui.Text(tile.Root, "Timer", "", theme.Numbers, debuffStyle ? 16 : 11, Palette.Bone,
            TextAlignmentOptions.Center).At(debuffStyle ? iconX : 0, debuffStyle ? 28 : 35,
                debuffStyle ? iconSize : TileWidth, debuffStyle ? 20 : 15);
        tile.Timer.enableAutoSizing = true;
        tile.Timer.fontSizeMin = debuffStyle ? 12 : 9;
        tile.Timer.fontSizeMax = debuffStyle ? 16 : 11;
        float labelWidth = TileWidth + 8, labelHeight = debuffStyle ? 22 : 18;
        RectTransform bounds = Ugui.Node(tile.Root, "Label bounds").At(-4, debuffStyle ? 52 : 53, labelWidth, labelHeight);
        bounds.gameObject.AddComponent<RectMask2D>();
        tile.Name = Ugui.Text(bounds, "Name", "", theme.Strong, debuffStyle ? 12 : 11, Palette.Bone,
            TextAlignmentOptions.Center).At(0, 0, labelWidth, labelHeight);
        tile.Name.enableAutoSizing = true;
        tile.Name.fontSizeMin = 8;
        tile.Name.fontSizeMax = debuffStyle ? 12 : 11;
        tile.Name.maxVisibleLines = 1;
        tile.Name.richText = false;
        tile.Timer.overflowMode = TextOverflowModes.Ellipsis;
        tile.TimerOutline = debuffStyle ? new TextOutline(tile.Timer, world: true, opacity: 0.8f) : new TextOutline(tile.Timer);
        tile.NameOutline = new TextOutline(tile.Name);
        tile.Fade = tile.Root.gameObject.AddComponent<CanvasGroup>();
        tile.Fade.blocksRaycasts = true;
        tile.Tooltip = GuideTooltip.Attach(theme, tile.Root);
        return tile;
    }

    private void Update(Tile tile, HudStatus status, int comfort)
    {
        bool harmful = status.Category == StatusCategory.Debuff;
        tile.Icon.sprite = status.Icon;
        tile.Icon.enabled = status.Icon;
        tile.Icon.color = Color.white.WithAlpha(status.Flash ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 8) : 1);
        tile.Timer.text = status.Key == SEMan.s_statusEffectResting ? comfort.ToString()
            : string.IsNullOrEmpty(status.Value) ? "\u2014" : status.Value;
        tile.Timer.enabled = !debuffStyle || !string.IsNullOrEmpty(status.Value);
        tile.Name.text = Localization.instance != null ? Localization.instance.Localize(status.Name) : status.Name;
        if (Time.unscaledTime >= tile.NextTooltip)
        {
            tile.NextTooltip = Time.unscaledTime + 1;
            StatusEffect? effect = Player.m_localPlayer ? Player.m_localPlayer.GetSEMan().GetStatusEffect(status.Key) : null;
            string description = status.Description ?? (effect ? effect!.GetTooltipString() : "");
            if (Localization.instance != null) description = Localization.instance.Localize(description);
            string duration = string.IsNullOrEmpty(status.Value) ? "" : status.Value + "\n\n";
            tile.Tooltip.Set(tile.Name.text, harmful ? "STATUS ? DEBUFF" : status.Category == StatusCategory.Passive
                ? "STATUS ? PASSIVE" : "STATUS ? BUFF", duration + description);
        }
        tile.TimerOutline.Sync();
        tile.NameOutline.Sync();
        tile.Fade.alpha = Mathf.Clamp01((Time.unscaledTime - tile.Born) / 0.3f);
    }

    public void Dispose()
    {
        if (root) { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        tiles.Clear();
    }
}

// Optional monochrome conversion retained for the Forsaken power display.
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
        int width = Mathf.Min(128, (int)r.width), height = Mathf.Min(128, (int)r.height);
        RenderTexture previous = RenderTexture.active;
        RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Texture2D copy = new(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // Crop on the GPU before readback; never allocate/blit a whole atlas for a tiny glyph.
            Graphics.Blit(atlas, temp, new Vector2(r.width / atlas.width, r.height / atlas.height),
                new Vector2(r.x / atlas.width, r.y / atlas.height));
            RenderTexture.active = temp;
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        }
        catch
        {
            UnityEngine.Object.Destroy(copy);
            throw;
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
