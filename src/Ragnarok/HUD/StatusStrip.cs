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
    internal Sprite? Icon;
    internal bool Flash;
    internal bool Comfort;     // Resting, campfire or shelter: what the comfort level comes from.

    internal static readonly int[] ComfortSources =
        { SEMan.s_statusEffectResting, SEMan.s_statusEffectCampFire, SEMan.s_statusEffectShelter };
}

// Compact buff tiles beneath the character name. Four columns keep long lists bounded.
internal sealed class StatusStrip : IDisposable
{
    private readonly Theme theme;
    private readonly MonochromeIcons icons;
    private readonly RectTransform root;
    private readonly GridLayoutGroup grid;
    private readonly Dictionary<int, Tile> tiles = new();
    private readonly List<int> removed = new();
    private sealed class Tile
    {
        internal RectTransform Root = null!;
        internal Image Icon = null!, Edge = null!, Plate = null!;
        internal TextMeshProUGUI Timer = null!, Name = null!;
        internal TextOutline TimerOutline = null!, NameOutline = null!;
        internal CanvasGroup Fade = null!;
        internal float Born;
    }

    internal StatusStrip(Theme theme, MonochromeIcons icons, Transform parent)
    {
        this.theme = theme;
        this.icons = icons;
        root = Ugui.Node(parent, "Ragnarok Status");
        grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.spacing = new Vector2(0, 4);
        grid.cellSize = new Vector2(42, 38);
        root.sizeDelta = new Vector2(168, 38);
    }

    internal RectTransform Rect => root;
    internal static float HeightFor(int count, bool labels)
    {
        int rows = Mathf.CeilToInt(count / 4f);
        return rows == 0 ? 0 : rows * (labels ? 60 : 38) + (rows - 1) * 4;
    }
    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);
    internal void SetScale(float scale) => root.localScale = Vector3.one * scale;
    internal void Place(HudPlacement placement) => placement.Apply(root);

    internal void Set(List<HudStatus> statuses, int comfort, bool labels, string key)
    {
        removed.Clear();
        foreach (int id in tiles.Keys)
            if (!statuses.Exists(s => s.Key == id)) removed.Add(id);
        foreach (int id in removed)
        {
            UnityEngine.Object.Destroy(tiles[id].Root.gameObject);
            tiles.Remove(id);
        }
        Vector2 cellSize = new(labels ? 46 : 42, labels ? 60 : 38);
        bool layoutChanged = grid.cellSize != cellSize;
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(0, 4);
        root.sizeDelta = new Vector2(cellSize.x * 4 + grid.spacing.x * 3, HeightFor(statuses.Count, labels));
        for (int i = 0; i < statuses.Count; i++)
        {
            HudStatus status = statuses[i];
            bool added = !tiles.TryGetValue(status.Key, out Tile tile);
            if (added)
            {
                tile = new Tile { Root = Ugui.Node(root, "Buff"), Born = Time.unscaledTime };
                tile.Plate = Ugui.Image(tile.Root, "Plate", theme.HudTile, Palette.Hex(0x0D1415)).At(0, 0, 34, 34);
                tile.Edge = Ugui.Image(tile.Root, "Rim", theme.HudTileEdge, Palette.Moss).At(0, 0, 34, 34);
                tile.Icon = Ugui.Image(tile.Root, "Icon", null, Palette.GoldBright).At(9, 9, 16, 16);
                tile.Icon.preserveAspect = true;
                tile.Timer = Ugui.Text(tile.Root, "Timer", "", theme.Strong, 9, Palette.Bone, TextAlignmentOptions.Center).At(2, 20, 30, 12);
                tile.Timer.enableAutoSizing = true;
                tile.Timer.fontSizeMin = 8;
                tile.Timer.fontSizeMax = 9;
                RectTransform labelBounds = Ugui.Node(tile.Root, "Label bounds").At(1, 38, 44, 20);
                labelBounds.gameObject.AddComponent<RectMask2D>();
                tile.Name = Ugui.Text(labelBounds, "Name", "", theme.Strong, 9, Palette.Bone, TextAlignmentOptions.Top).At(1, 0, 42, 18);
                tile.Name.enableAutoSizing = true;
                tile.Name.fontSizeMin = 8;
                tile.Name.fontSizeMax = 9;
                tile.Name.maxVisibleLines = 1;
                tile.Name.richText = false;
                tile.Timer.overflowMode = TextOverflowModes.Ellipsis;
                tile.TimerOutline = new TextOutline(tile.Timer);
                tile.NameOutline = new TextOutline(tile.Name);
                tile.Fade = tile.Root.gameObject.AddComponent<CanvasGroup>();
                tile.Fade.blocksRaycasts = false;
                tiles.Add(status.Key, tile);
            }
            if (added || layoutChanged)
            {
                float x = labels ? 6 : 0;
                tile.Plate.At(x, 0, 34, 34);
                tile.Edge.At(x, 0, 34, 34);
                tile.Icon.At(x + 9, 3, 16, 16);
                tile.Timer.At(x + 2, 20, 30, 12);
            }
            tile.Root.SetSiblingIndex(i);
            bool rested = status.Key == SEMan.s_statusEffectRested;
            bool fire = status.Key == SEMan.s_statusEffectResting || status.Key == SEMan.s_statusEffectCampFire;
            bool shelter = status.Key == SEMan.s_statusEffectShelter;
            bool freezing = status.Key == "Freezing".GetStableHashCode();
            bool wet = status.Name.IndexOf("wet", StringComparison.OrdinalIgnoreCase) >= 0;
            tile.Icon.sprite = rested ? theme.HudMoon : fire ? theme.HudFlame : shelter ? theme.IconHome
                : wet ? theme.IconDrop : icons.Get(status.Icon) ?? theme.GlyphEffect;
            Color accent = freezing ? Palette.Bone : rested ? Palette.Gold : fire ? Palette.Hex(0xE8A052) : wet ? Palette.Frost : Palette.Bone;
            tile.Icon.color = status.Flash ? accent.WithAlpha(0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 8)) : accent;
            tile.Edge.color = freezing ? Palette.Health : shelter ? Palette.Bone : rested ? Palette.Gold : wet ? Palette.Frost.WithAlpha(0.7f) : Palette.Hex(0x4D5551);
            tile.Plate.color = freezing ? Palette.Health : wet ? Palette.Hex(0x1C3546) : Palette.Hex(0x0D1415);
            tile.Timer.color = wet ? Palette.Frost : Palette.Bone;
            tile.Timer.text = status.Key == SEMan.s_statusEffectResting ? comfort.ToString()
                : string.IsNullOrEmpty(status.Value) ? "\u2014" : status.Value;
            tile.Name.gameObject.SetActive(labels);
            tile.Name.text = Localization.instance != null ? Localization.instance.Localize(status.Name) : status.Name;
            tile.TimerOutline.Sync();
            tile.NameOutline.Sync();
            tile.Fade.alpha = Mathf.Clamp01((Time.unscaledTime - tile.Born) / 0.3f);
        }
    }

    public void Dispose() { if (root) UnityEngine.Object.Destroy(root.gameObject); }
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
