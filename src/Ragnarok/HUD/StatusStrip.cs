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
        internal float NextTooltip;
        internal GuideTooltip Tooltip = null!;
    }

    internal StatusStrip(Theme theme, MonochromeIcons icons, Transform parent)
    {
        this.theme = theme;
        this.icons = icons;
        root = Ugui.Node(parent, "Ragnarok Status");
        grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.spacing = new Vector2(0, 2);
        grid.cellSize = new Vector2(40, 52);
        root.sizeDelta = new Vector2(160, 52);
    }

    internal RectTransform Rect => root;
    internal static float HeightFor(int count)
    {
        int rows = Mathf.CeilToInt(count / 4f);
        return rows == 0 ? 0 : rows * 52 + (rows - 1) * 2;
    }
    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);
    internal void SetScale(float scale) => root.localScale = Vector3.one * scale;
    internal void Place(HudPlacement placement) => placement.Apply(root);

    internal void Set(List<HudStatus> statuses, int comfort)
    {
        removed.Clear();
        foreach (int id in tiles.Keys)
            if (!statuses.Exists(s => s.Key == id)) removed.Add(id);
        foreach (int id in removed)
        {
            UnityEngine.Object.Destroy(tiles[id].Root.gameObject);
            tiles.Remove(id);
        }
        Vector2 cellSize = new(40, 52);
        bool layoutChanged = grid.cellSize != cellSize;
        grid.cellSize = cellSize;
        grid.spacing = new Vector2(0, 2);
        root.sizeDelta = new Vector2(cellSize.x * 4 + grid.spacing.x * 3, HeightFor(statuses.Count));
        for (int i = 0; i < statuses.Count; i++)
        {
            HudStatus status = statuses[i];
            bool added = !tiles.TryGetValue(status.Key, out Tile tile);
            if (added)
            {
                tile = new Tile { Root = Ugui.Node(root, "Buff"), Born = Time.unscaledTime };
                tile.Plate = Ugui.Image(tile.Root, "Plate", theme.HudTile, Palette.HudGlass).At(0, 0, 34, 34);
                tile.Edge = Ugui.Image(tile.Root, "Rim", theme.HudTileEdge, Palette.Moss).At(0, 0, 34, 34);
                tile.Icon = Ugui.Image(tile.Root, "Icon", null, Palette.GoldBright).At(9, 9, 16, 16);
                tile.Icon.preserveAspect = true;
                tile.Timer = Ugui.Text(tile.Root, "Timer", "", theme.Numbers, 9, Palette.Bone, TextAlignmentOptions.Center).At(2, 20, 30, 12);
                tile.Timer.enableAutoSizing = true;
                tile.Timer.fontSizeMin = 8;
                tile.Timer.fontSizeMax = 9;
                RectTransform labelBounds = Ugui.Node(tile.Root, "Label bounds").At(-3, 35, 40, 16);
                labelBounds.gameObject.AddComponent<RectMask2D>();
                tile.Name = Ugui.Text(labelBounds, "Name", "", theme.Strong, 9, Palette.Bone, TextAlignmentOptions.Center).At(0, 0, 40, 15);
                tile.Name.enableAutoSizing = true;
                tile.Name.fontSizeMin = 8;
                tile.Name.fontSizeMax = 9;
                tile.Name.maxVisibleLines = 1;
                tile.Name.richText = false;
                tile.Timer.overflowMode = TextOverflowModes.Ellipsis;
                tile.TimerOutline = new TextOutline(tile.Timer);
                tile.NameOutline = new TextOutline(tile.Name);
                tile.Fade = tile.Root.gameObject.AddComponent<CanvasGroup>();
                tile.Fade.blocksRaycasts = true;
                tile.Plate.raycastTarget = true;
                tile.Tooltip = GuideTooltip.Attach(theme, tile.Root);
                tiles.Add(status.Key, tile);
            }
            if (added || layoutChanged)
            {
                const float x = 0;
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
            tile.Edge.color = (freezing ? Palette.Health : shelter ? Palette.Bone : rested ? Palette.Gold : wet ? Palette.Frost : Palette.Hex(0x4D5551)).WithAlpha(0.65f);
            tile.Plate.color = freezing ? Palette.Hex(0x2D100D, Palette.HudGlassOpacity) : wet ? Palette.HudWetGlass : Palette.HudGlass;
            tile.Timer.color = wet ? Palette.Frost : Palette.Bone;
            tile.Timer.text = status.Key == SEMan.s_statusEffectResting ? comfort.ToString()
                : string.IsNullOrEmpty(status.Value) ? "\u2014" : status.Value;
            tile.Name.gameObject.SetActive(true);
            tile.Name.text = Localization.instance != null ? Localization.instance.Localize(status.Name) : status.Name;
            if (Time.unscaledTime >= tile.NextTooltip)
            {
                tile.NextTooltip = Time.unscaledTime + 1;
                StatusEffect? effect = Player.m_localPlayer ? Player.m_localPlayer.GetSEMan().GetStatusEffect(status.Key) : null;
                string description = effect ? effect!.GetTooltipString() : "";
                if (Localization.instance != null) description = Localization.instance.Localize(description);
                string duration = string.IsNullOrEmpty(status.Value) ? "" : status.Value + "\n\n";
                tile.Tooltip.Set(tile.Name.text, wet || freezing ? "STATUS · DEBUFF" : rested ? "STATUS · BUFF" : "STATUS EFFECT",
                    duration + description);
            }
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
