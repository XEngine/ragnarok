using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Ragnarok.UI;

// Rune & Iron: charcoal, parchment and antique gold, with distinct semantic accents.
internal static class Palette
{
    internal const float HudGlassOpacity = 0.58f;
    internal static readonly Color HudGlass = Hex(0x0C0F11, HudGlassOpacity);
    internal static readonly Color HudWetGlass = Hex(0x17252D, HudGlassOpacity);
    internal static readonly Color Backdrop = Hex(0x050505, 0.62f);
    internal static readonly Color Obsidian = Hex(0x101719);
    internal static readonly Color Panel = Hex(0x101719, 0.78f);
    internal static readonly Color Card = Hex(0x1F201C);
    internal static readonly Color CardHover = Hex(0x2B2D27);
    internal static readonly Color Moss = Hex(0x363A31);
    // Warm metallic edges against cool charcoal surfaces.
    internal static readonly Color Rule = Hex(0x625334);
    internal static readonly Color Gold = Hex(0xC5A65C);
    internal static readonly Color GoldBright = Hex(0xE3CB85);
    internal static readonly Color GoldDim = Hex(0x625334);
    internal static readonly Color Tan = Hex(0xBE9A6E);
    internal static readonly Color Bone = Hex(0xE4DCCB);
    internal static readonly Color Muted = Hex(0xE4DCCB, 0.6f);
    internal static readonly Color Faint = Hex(0xE4DCCB, 0.4f);
    internal static readonly Color Dim = Hex(0xE4DCCB, 0.3f);
    // Keys and "ready" states: pale parchment, bright without the guide's lime.
    internal static readonly Color Key = Hex(0xEADFC0);
    internal static readonly Color Alert = Hex(0xD6241C);
    internal static readonly Color Crimson = Hex(0xA8261E);
    internal static readonly Color Ember = Hex(0xE0612E);
    internal static readonly Color Health = Hex(0xD64A3E);
    internal static readonly Color Stamina = Hex(0xE8C04A);
    internal static readonly Color Eitr = Hex(0x79AFC6);
    internal static readonly Color Success = Hex(0x91AD78);
    internal static readonly Color Frost = Hex(0x79AFC6);
    internal static readonly Color Ink = Hex(0x0F0F0F);
    internal static readonly Color SwitchOn = Hex(0x2E2A22);

    internal static Color Hex(int rgb, float alpha = 1f) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    internal static Color WithAlpha(this Color color, float alpha) => new(color.r, color.g, color.b, alpha);
}

// Sprites and fonts shared by every Ragnarök surface. Textures are generated at runtime,
// so the mod ships no image assets.
internal sealed class Theme : IDisposable
{
    private readonly List<UnityEngine.Object> owned = new();
    private TMP_FontAsset? heading, body, strong, numbers, runic, subtle, serif;
    private float nextFontScan;

    internal readonly Sprite InventoryShield, HudRune, HudRing, HudDashedRing, HudPortraitRing, HudTile, HudTileEdge, HudMoon, HudFlame, HudBowl, HudBottle;
    internal readonly Sprite HudBar, HudStag, HudGlow, HudHatch, HudVignette, HudEmptyEdge, HudStar, HudBackpack;
    internal readonly Sprite HudActionSlot, HudActionEdge, HudActionEmptyEdge, PackEmptyEdge;
    internal readonly Sprite PackSurface, PackEdge, PackPill, PackPillEdge, HudKeySurface, HudKeyEdge;
    internal readonly Sprite SkillPill, SkillPillEdge, SkillUp;
    internal readonly Sprite White;
    internal readonly Sprite Diamond;
    internal readonly Sprite Knot;
    internal readonly Sprite Window;
    internal readonly Sprite Board;
    internal readonly Sprite Card;
    internal readonly Sprite CardHover;
    internal readonly Sprite Button;
    internal readonly Sprite ButtonHover;
    internal readonly Sprite Primary;
    internal readonly Sprite PrimaryHover;
    internal readonly Sprite SegmentOn;
    internal readonly Sprite SegmentOnHover;
    internal readonly Sprite Field;
    internal readonly Sprite FieldFocus;
    internal readonly Sprite HudFrame;
    // The quiet frame for small passing displays (chest peek, action prompt): dark glass, a thin dim rule.
    internal readonly Sprite Minimal;
    // Item tooltips: near-opaque, edged in brass, with the corner flourish.
    internal readonly Sprite Tooltip;
    internal readonly Sprite StatusPlate; // Translucent black plate behind each status effect.
    internal readonly Sprite HudWell; // Mask shape for a bar's contents, chipped like the frame around it.
    // The Compendium's art panel: a diagonal cut with a ragged edge, and the dark warm scene behind it.
    internal readonly Sprite ArtCut, ArtScene;
    internal readonly Sprite Shade;
    internal readonly Sprite Plate;
    internal readonly Sprite Slot;
    internal readonly Sprite SlotHover;
    internal readonly Sprite SlotRing;
    internal readonly Sprite ButtonOff;
    internal readonly Sprite NoTeleport;
    internal readonly Sprite IconSword;
    internal readonly Sprite IconFood;
    internal readonly Sprite IconHammer;
    internal readonly Sprite IconHome, IconPortal, IconShip, IconPlayer;
    // Crafting filters: a T-visor helm, an arrow, stacked ingots; and search, check and "other" marks.
    internal readonly Sprite IconHelm, IconArrow, IconIngot, IconSearch, IconCheck, IconOther, IconStack, IconSort, IconUpgrade, IconRepair, IconHeart, IconBolt, IconDrop;
    // Filled stat glyphs for the tooltips and HUD (white, tinted at use): damage kinds, defence, food,
    // weight, coins, a warning mark and a generic effect star.
    internal readonly Sprite GlyphSlash, GlyphPierce, GlyphBlunt, GlyphChop, GlyphPickaxe, GlyphFire, GlyphFrost,
        GlyphLightning, GlyphPoison, GlyphSpirit, GlyphShield, GlyphHealth, GlyphStamina, GlyphEitr, GlyphWeight,
        GlyphCoin, GlyphAlert, GlyphEffect, GlyphMeal;
    // HUD ornaments: the end cap of a gold baseline (its vertical side on the left; mirror it for a left
    // end), the health emblem (a dark disc, gold rings and a red triquetra; in colour), and the reticle.
    internal readonly Sprite Cap, Emblem, Reticle, Braid, Disc, Ring, Medallion, BarFill, Tick, HatchCap, PowerKey, Arc;

    // The ring's inner edge, of its 128-unit radius: a map of radius R takes a ring of R * 128 / RingInner.
    internal const float RingInner = 116f;
    // Status effect and item icons in single-colour form, for tinting in gold.
    internal readonly MonochromeIcons Mono = new();
    // Living fills for the vitals: blood (health), molten mead (stamina), arcane mist (eitr).
    internal readonly LiquidSkin Blood, Mead, Arcane;

    // The theme in use, for ChipSkin.
    internal static Theme? Current;

    // Whether frames are drawn chipped ([Interface] FrameEdges, set by the plugin). Applies live: ChipSkin
    // swaps every frame to the version for the current choice.
    internal static System.Func<bool> ChipEdges = () => false;

    internal Theme()
    {
        Current = this;
        White = Plain(Make(4, 4, (_, _) => Color.white));
        PackSurface = RoundedPanel(12, false);
        PackEdge = RoundedPanel(12, true);
        PackPill = RoundedPanel(14, false);
        PackPillEdge = RoundedPanel(14, true);
        SkillPill = RoundedPanel(25, false);
        SkillPillEdge = RoundedPanel(25, true);
        SkillUp = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(32, 14), new Vector2(32, 50), 0f),
            Segment(p, new Vector2(16, 34), new Vector2(32, 50), 0f),
            Segment(p, new Vector2(32, 50), new Vector2(48, 34), 0f))));
        HudKeySurface = RoundedPanel(5, false);
        HudKeyEdge = RoundedPanel(5, true);
        // HUD-specific shapes are procedural, independent of font glyph coverage and frame-edge settings.
        HudRune = Plain(LineIcon(96, 0, p => Mathf.Min(
            Segment(p, new Vector2(23, 7), new Vector2(23, 57), 0),
            Mathf.Min(Segment(p, new Vector2(23, 57), new Vector2(42, 46), 0),
            Mathf.Min(Segment(p, new Vector2(42, 46), new Vector2(30, 32), 0),
            Segment(p, new Vector2(30, 32), new Vector2(44, 7), 0))))));
        // A small open antler glyph for the power seal; no filled animal silhouette.
        HudStag = Plain(LineIcon(96, 0, p => Lines(
            Segment(p, new Vector2(32, 7), new Vector2(32, 33), 0),
            Segment(p, new Vector2(32, 33), new Vector2(20, 44), 0),
            Segment(p, new Vector2(20, 44), new Vector2(16, 58), 0),
            Segment(p, new Vector2(32, 33), new Vector2(44, 44), 0),
            Segment(p, new Vector2(44, 44), new Vector2(48, 58), 0),
            Segment(p, new Vector2(20, 44), new Vector2(10, 48), 0),
            Segment(p, new Vector2(44, 44), new Vector2(54, 48), 0),
            Segment(p, new Vector2(27, 37), new Vector2(20, 33), 0),
            Segment(p, new Vector2(37, 37), new Vector2(44, 33), 0))));
        HudRing = Plain(Make(128, 128, (x, y) =>
        {
            float radius = new Vector2(x + 0.5f - 64, y + 0.5f - 64).magnitude;
            return Color.white.WithAlpha(Mathf.Clamp01(1f - Mathf.Abs(radius - 61f) + 2f));
        }));
        HudDashedRing = Plain(Make(128, 128, (x, y) =>
        {
            Vector2 p = new(x + 0.5f - 64, y + 0.5f - 64);
            float ring = Mathf.Clamp01(3f - Mathf.Abs(p.magnitude - 61f));
            // Sixteen short dashes, with a softened pixel at each end.
            float phase = Mathf.Repeat(Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2) * 16, 1);
            float dash = Mathf.Clamp01(Mathf.Min(phase, 0.65f - phase) * 24);
            return Color.white.WithAlpha(ring * dash);
        }));
        HudPortraitRing = Plain(Make(256, 256, (x, y) =>
        {
            Vector2 p = new(x + 0.5f - 128, y + 0.5f - 128);
            float alpha = Mathf.Clamp01(2.3f - Mathf.Abs(p.magnitude - 125f));
            float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2) * 24, 1);
            return (angle < 0.12f ? Palette.Bone : Palette.Gold).WithAlpha(alpha);
        }));
        HudTile = Plain(Make(96, 96, (x, y) =>
        {
            Vector2 q = new(Mathf.Abs(x + 0.5f - 48) - 27, Mathf.Abs(y + 0.5f - 48) - 27);
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 19;
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d));
        }));
        HudTileEdge = Plain(Make(96, 96, (x, y) =>
        {
            Vector2 q = new(Mathf.Abs(x + 0.5f - 48) - 27, Mathf.Abs(y + 0.5f - 48) - 27);
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 19;
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 3));
        }));
        HudEmptyEdge = Plain(Make(96, 96, (x, y) =>
        {
            Vector2 q = new(Mathf.Abs(x + 0.5f - 48) - 27, Mathf.Abs(y + 0.5f - 48) - 27);
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 19;
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 3) * ((x + y) % 13 < 8 ? 1 : 0));
        }));
        // Dedicated action-slot frame: roughly eight-pixel corners and a one-pixel rim at 58 HUD units.
        float ActionSlotDistance(int x, int y)
        {
            Vector2 q = new(Mathf.Abs(x + 0.5f - 58) - 41, Mathf.Abs(y + 0.5f - 58) - 41);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 16;
        }
        HudActionSlot = Plain(Make(116, 116, (x, y) =>
            Color.white.WithAlpha(Mathf.Clamp01(0.5f - ActionSlotDistance(x, y)))));
        HudActionEdge = Plain(Make(116, 116, (x, y) =>
        {
            float d = ActionSlotDistance(x, y);
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 2));
        }));
        HudActionEmptyEdge = Plain(Make(116, 116, (x, y) =>
        {
            float d = ActionSlotDistance(x, y);
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 2) * ((x + y) % 16 < 10 ? 1 : 0));
        }));
        // A stronger dashed edge for inventory/chest empty slots at small UI scales.
        PackEmptyEdge = Plain(Make(116, 116, (x, y) =>
        {
            float d = ActionSlotDistance(x, y);
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + 3.5f) * ((x + y) % 20 < 13 ? 1 : 0));
        }));
        Vector2[] star = new Vector2[10];
        for (int i = 0; i < star.Length; i++)
        {
            float angle = Mathf.PI / 2 + i * Mathf.PI / 5;
            float radius = i % 2 == 0 ? 15 : 6.5f;
            star[i] = new Vector2(16 + Mathf.Cos(angle) * radius, 16 + Mathf.Sin(angle) * radius);
        }
        HudStar = Plain(Make(32, 32, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            bool inside = false;
            float distance = float.MaxValue;
            for (int i = 0, j = star.Length - 1; i < star.Length; j = i++)
            {
                Vector2 a = star[i], b = star[j];
                distance = Mathf.Min(distance, Segment(p, a, b, 0));
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f + (inside ? distance : -distance)));
        }));
        HudBackpack = Plain(Make(64, 64, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            float RoundedBox(Vector2 centre, Vector2 half, float radius)
            {
                Vector2 q = new(Mathf.Abs(p.x - centre.x) - half.x + radius, Mathf.Abs(p.y - centre.y) - half.y + radius);
                return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
            }
            float body = Mathf.Abs(RoundedBox(new Vector2(32, 29), new Vector2(19, 24), 7));
            float pocket = Mathf.Abs(RoundedBox(new Vector2(32, 19), new Vector2(12, 8), 3));
            float handle = Mathf.Max(Mathf.Abs((p - new Vector2(32, 52)).magnitude - 7), 52 - p.y);
            float flap = Segment(p, new Vector2(20, 38), new Vector2(44, 38), 0);
            float seam = Mathf.Min(Segment(p, new Vector2(27, 38), new Vector2(27, 33), 0),
                Segment(p, new Vector2(37, 38), new Vector2(37, 33), 0));
            float distance = Mathf.Min(Mathf.Min(body, pocket), Mathf.Min(handle, Mathf.Min(flap, seam)));
            return Color.white.WithAlpha(Mathf.Clamp01(2 - distance));
        }));
        HudMoon = Plain(LineIcon(64, 0, p => Mathf.Max(Circle(p, new Vector2(32, 32), 21), -Circle(p, new Vector2(42, 40), 22))));
        HudFlame = Plain(LineIcon(64, 0, p => Mathf.Min(Circle(p, new Vector2(32, 25), 15),
            Poly(p, new Vector2(18, 30), new Vector2(34, 58), new Vector2(46, 30)))));
        HudBowl = Plain(LineIcon(64, 0, p => Mathf.Min(
            Mathf.Max(Circle(p, new Vector2(32, 32), 23), p.y - 32),
            Mathf.Min(Segment(p, new Vector2(22, 43), new Vector2(26, 51), 0), Segment(p, new Vector2(36, 47), new Vector2(40, 55), 0)))));
        HudBottle = Plain(LineIcon(64, 0, p => Mathf.Min(Box(p, new Vector2(32, 28), new Vector2(13, 22)),
            Box(p, new Vector2(32, 53), new Vector2(9, 3)))));
        HudBar = Track(Sprite.Create(Make(32, 16, (x, y) =>
        {
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 16) - 8, 0);
            float dy = y + 0.5f - 8;
            return Color.white.WithAlpha(Mathf.Clamp01(8 - Mathf.Sqrt(dx * dx + dy * dy)));
        }), new Rect(0, 0, 32, 16), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8)));
        HudGlow = Plain(Make(256, 48, (x, y) =>
        {
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 128) - 108, 0);
            float dy = Mathf.Abs(y + 0.5f - 24);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Color.white.WithAlpha(Mathf.Pow(Mathf.Clamp01(1 - distance / 24), 2));
        }));
        // Match the stamina track's 232:9 aspect at double resolution. Bake the
        // capsule into the hatch alpha so its end caps do not depend on stencil clipping.
        HudHatch = Plain(Make(464, 18, (x, y) =>
        {
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 232) - 223, 0);
            float dy = y + 0.5f - 9;
            float coverage = Mathf.Clamp01(9 - Mathf.Sqrt(dx * dx + dy * dy));
            return Color.white.WithAlpha(coverage * ((x + y) % 20 < 6 ? 0.85f : 0.12f));
        }));
        HudVignette = Plain(Make(256, 144, (x, y) =>
        {
            float edge = Mathf.Max(Mathf.Abs((x + 0.5f) / 128 - 1), Mathf.Abs((y + 0.5f) / 72 - 1));
            return Color.white.WithAlpha(Mathf.Pow(Mathf.Clamp01((edge - 0.7f) / 0.3f), 2));
        }));
        Diamond = Plain(MakeDiamond(64));
        Knot = Plain(Make(96, 32, (x, y) =>
        {
            float distance = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float dx = Mathf.Abs(x + 0.5f - (24 + i * 24)), dy = Mathf.Abs(y + 0.5f - 16);
                float strand = Mathf.Abs(dx / 22f + dy / 11f - 1f) * 9.84f;
                // Interrupt alternate strands at the overlaps to suggest woven metal.
                if (i != 1 && dx > 10 && dx < 16 && ((i == 0 && y > 16) || (i == 2 && y < 16))) continue;
                distance = Mathf.Min(distance, strand);
            }
            return new Color(1f, 1f, 1f, Mathf.Clamp01(1.7f - distance));
        }));
        // Frames after the style guide: flat near-black grounds edged by a single brass line. Windows carry
        // knotwork at their corners, cards and plates a small corner flourish (see SurfaceColor).
        Window = Chamfer(Palette.Obsidian.WithAlpha(0.96f), Palette.Gold.WithAlpha(0.75f), 12, Surface.Carved);
        // The inventory, character and chest panels: the guide's plain frame, a corner flourish and no knots.
        Board = Chamfer(Palette.Obsidian.WithAlpha(0.94f), Palette.Gold.WithAlpha(0.6f), 8);
        Card = Chamfer(Palette.Card, Palette.Gold.WithAlpha(0.22f), 8);
        CardHover = Chamfer(Palette.CardHover, Palette.Gold.WithAlpha(0.75f), 8);
        // Buttons are drawn like the guide's key caps: dark, edged in faded bone.
        Button = Chamfer(Palette.Card, Palette.Bone.WithAlpha(0.4f), 6, Surface.Leather);
        ButtonHover = Chamfer(Palette.CardHover, Palette.GoldBright, 6, Surface.Leather);
        Primary = Chamfer(Palette.Hex(0x3A3123), Palette.Gold, 6, Surface.Leather);
        PrimaryHover = Chamfer(Palette.Hex(0x4A3F2C), Palette.GoldBright, 6, Surface.Leather);
        SegmentOn = Chamfer(Palette.Moss, Palette.Gold, 6);
        SegmentOnHover = Chamfer(Palette.Moss, Palette.GoldBright, 6);
        Field = Chamfer(Palette.Ink.WithAlpha(0.9f), Palette.Moss, 4, Surface.Recess);
        FieldFocus = Chamfer(Palette.Ink.WithAlpha(0.9f), Palette.Gold, 4, Surface.Recess);
        HudFrame = Chamfer(Palette.Obsidian.WithAlpha(0.8f), Palette.Gold.WithAlpha(0.7f), 6);
        Minimal = Chamfer(Palette.Obsidian.WithAlpha(0.82f), Palette.Gold.WithAlpha(0.55f), 5);
        Tooltip = Chamfer(Palette.Obsidian.WithAlpha(0.97f), Palette.Gold.WithAlpha(0.7f), 8);
        HudWell = Chamfer(Color.white, Color.white, 4, Surface.Mask);
        StatusPlate = Chamfer(new Color(0f, 0f, 0f, 0.5f), new Color(0f, 0f, 0f, 0.6f), 6, Surface.Leather);
        ArtCut = Plain(MakeArtCut(256, 384));
        ArtScene = Plain(MakeArtScene(128, 192));
        Shade = Feathered(28);
        Plate = Chamfer(Palette.Obsidian, Palette.Gold.WithAlpha(0.75f), 10);
        Slot = Chamfer(Palette.Hex(0x1C1D1A, 0.92f), Palette.Moss, 5, Surface.Recess);
        SlotHover = Chamfer(Palette.Hex(0x2B2D27, 0.96f), Palette.Gold, 5, Surface.Recess);
        SlotRing = Chamfer(Palette.Gold.WithAlpha(0.06f), Palette.GoldBright, 5, Surface.Mask);
        ButtonOff = Chamfer(Palette.Ink, Palette.Hex(0x2A2A28), 6, Surface.Leather);
        NoTeleport = Plain(MakeNoTeleport(32));
        IconSword = Plain(LineIcon(44, 45f, p => Mathf.Min(Mathf.Min(
            Mathf.Max(Box(p, new Vector2(32, 40), new Vector2(5, 22)), (Mathf.Abs(p.x - 32) + p.y - 62) / 1.4142f),
            Box(p, new Vector2(32, 18), new Vector2(14, 2.5f))),
            Mathf.Min(Box(p, new Vector2(32, 10), new Vector2(2.5f, 6)), Circle(p, new Vector2(32, 3.5f), 3.5f)))));
        IconFood = Plain(LineIcon(44, 0f, p => SmoothMin(
            SmoothMin(Circle(p, new Vector2(40, 40), 15f), Circle(p, new Vector2(27, 27), 7f), 10f),
            Mathf.Min(Segment(p, new Vector2(26, 26), new Vector2(14, 14), 3f),
                Mathf.Min(Circle(p, new Vector2(10, 16), 4.5f), Circle(p, new Vector2(16, 10), 4.5f))), 3f)));
        IconHammer = Plain(LineIcon(44, -45f, p => Mathf.Min(
            Box(p, new Vector2(32, 26), new Vector2(3, 24)),
            Box(p, new Vector2(32, 50), new Vector2(17, 8)))));
        Blood = new LiquidSkin(Plain(MakeBloodFill(256, 32)), Owned(MakeBloodFlow(128, 64)))
        {
            FlowAlpha = 0.7f, Drift = 0.045f, Heave = 0.06f, HeaveSpeed = 0.7f, Shine = 0.06f,
            Lag = new Color(0.32f, 0.02f, 0.03f, 0.9f),
            // Under a quarter: a heartbeat, the blood darkening and its swirls glowing on each beat.
            Mood = (pct, time) =>
            {
                float beat = pct < 0.25f && pct > 0 ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 6.5f)), 6f) : 0f;
                return (beat * 0.6f, beat * 0.25f);
            }
        };
        Mead = new LiquidSkin(Plain(MakeMeadFill(256, 32)), Owned(MakeMeadFlow(128, 32)))
        {
            FlowAlpha = 0.8f, Drift = 0.16f, Heave = 0.04f, HeaveSpeed = 1.6f, Shine = 0.1f, Scale = 120f,
            Lag = new Color(0.45f, 0.28f, 0.05f, 0.8f),
            // Nearly winded: the mead gutters like a struggling flame.
            Mood = (pct, time) =>
            {
                float strain = pct < 0.2f ? Mathf.PerlinNoise(time * 14f, 0.37f) : 0f;
                return (strain * 0.55f, -strain * 0.3f);
            }
        };
        Arcane = new LiquidSkin(Plain(MakeArcaneFill(256, 32)), Owned(MakeArcaneFlow(128, 64)))
        {
            FlowAlpha = 0.75f, Drift = 0.018f, Heave = 0.12f, HeaveSpeed = 0.35f, Shine = 0.05f, Scale = 180f,
            Lag = new Color(0.2f, 0.15f, 0.45f, 0.85f),
            // The mist breathes: its glow rises and falls slowly, the motes twinkling with it.
            Mood = (pct, time) => (0f, Mathf.Sin(time * 1.3f) * 0.15f)
        };

        // Compass markers: a longhouse with its door, a portal ring with an algiz rune, a longship.
        IconHome = Plain(LineIcon(44, 0f, p => Lines(
            Mathf.Min(Box(p, new Vector2(32, 21), new Vector2(15, 12)),
                Poly(p, new Vector2(9, 33), new Vector2(55, 33), new Vector2(32, 55))),
            Box(p, new Vector2(32, 15), new Vector2(4, 6)))));
        IconPortal = Plain(LineIcon(44, 0f, p => Lines(
            Circle(p, new Vector2(32, 32), 23f),
            Segment(p, new Vector2(32, 16), new Vector2(32, 46), 0f),
            Segment(p, new Vector2(32, 32), new Vector2(22, 44), 0f),
            Segment(p, new Vector2(32, 32), new Vector2(42, 44), 0f))));
        IconHelm = Plain(LineIcon(44, 0f, p => Lines(
            Mathf.Min(Mathf.Max(Circle(p, new Vector2(32, 34), 21f), 34f - p.y), Box(p, new Vector2(32, 21), new Vector2(21, 13))),
            Mathf.Min(Box(p, new Vector2(32, 28), new Vector2(14, 3)), Box(p, new Vector2(32, 17), new Vector2(3.5f, 9))))));
        IconArrow = Plain(LineIcon(44, 45f, p => Lines(
            Segment(p, new Vector2(32, 6), new Vector2(32, 46), 0f),
            Poly(p, new Vector2(32, 60), new Vector2(23, 45), new Vector2(41, 45)),
            Segment(p, new Vector2(32, 16), new Vector2(23, 6), 0f),
            Segment(p, new Vector2(32, 16), new Vector2(41, 6), 0f))));
        IconIngot = Plain(LineIcon(44, 0f, p => Lines(
            Poly(p, new Vector2(6, 10), new Vector2(58, 10), new Vector2(50, 26), new Vector2(14, 26)),
            Poly(p, new Vector2(16, 31), new Vector2(48, 31), new Vector2(42, 46), new Vector2(22, 46)))));
        IconSearch = Plain(LineIcon(44, 0f, p => Lines(
            Circle(p, new Vector2(27, 37), 15f), Segment(p, new Vector2(38, 26), new Vector2(54, 10), 0f))));
        IconCheck = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(12, 32), new Vector2(26, 18), 0f), Segment(p, new Vector2(26, 18), new Vector2(52, 46), 0f))));
        IconOther = Plain(LineIcon(44, 0f, p => Lines(
            Circle(p, new Vector2(14, 32), 5f), Circle(p, new Vector2(32, 32), 5f), Circle(p, new Vector2(50, 32), 5f))));
        // Quick Stack: an arrow dropping into an open chest. Sort: an arrow down beside an arrow up.
        IconStack = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(11, 30), new Vector2(11, 7), 0f), Segment(p, new Vector2(11, 7), new Vector2(53, 7), 0f),
            Segment(p, new Vector2(53, 7), new Vector2(53, 30), 0f),
            Segment(p, new Vector2(32, 60), new Vector2(32, 30), 0f),
            Segment(p, new Vector2(32, 26), new Vector2(23, 37), 0f),
            Segment(p, new Vector2(32, 26), new Vector2(41, 37), 0f))));
        IconSort = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(22, 56), new Vector2(22, 10), 0f), Segment(p, new Vector2(22, 8), new Vector2(12, 20), 0f),
            Segment(p, new Vector2(22, 8), new Vector2(32, 20), 0f),
            Segment(p, new Vector2(42, 8), new Vector2(42, 54), 0f), Segment(p, new Vector2(42, 56), new Vector2(32, 44), 0f),
            Segment(p, new Vector2(42, 56), new Vector2(52, 44), 0f))));
        // Upgrade: two chevrons pointing up. Repair: a wrench, its open jaw at the top right.
        IconUpgrade = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(14, 30), new Vector2(32, 48), 0f), Segment(p, new Vector2(32, 48), new Vector2(50, 30), 0f),
            Segment(p, new Vector2(14, 14), new Vector2(32, 32), 0f), Segment(p, new Vector2(32, 32), new Vector2(50, 14), 0f))));
        IconRepair = Plain(LineIcon(44, 0f, p => Lines(
            Mathf.Max(Circle(p, new Vector2(42, 42), 12f), -Box(p, new Vector2(50, 50), new Vector2(6, 6))),
            Segment(p, new Vector2(34, 34), new Vector2(12, 12), 0f))));
        // The cauldron's food kinds: a heart for health, a lightning bolt for stamina, a drop for eitr.
        IconHeart = Plain(LineIcon(44, 0f, p => Lines(Mathf.Min(
            Mathf.Min(Circle(p, new Vector2(22, 41), 11f), Circle(p, new Vector2(42, 41), 11f)),
            Poly(p, new Vector2(12.5f, 36), new Vector2(51.5f, 36), new Vector2(32, 9))))));
        IconBolt = Plain(LineIcon(44, 0f, p => Lines(
            Segment(p, new Vector2(40, 58), new Vector2(22, 31), 0f), Segment(p, new Vector2(22, 31), new Vector2(42, 33), 0f),
            Segment(p, new Vector2(42, 33), new Vector2(24, 6), 0f))));
        IconDrop = Plain(LineIcon(44, 0f, p => Lines(Mathf.Min(
            Circle(p, new Vector2(32, 23), 15f),
            Poly(p, new Vector2(18.5f, 28), new Vector2(45.5f, 28), new Vector2(32, 58))))));
        // Stat glyphs, filled, on the 64-unit grid with y up.
        GlyphSlash = Plain(FillIcon(48, p => Mathf.Max(Circle(p, new Vector2(28, 30), 26f), -Circle(p, new Vector2(40, 21), 25f))));
        GlyphPierce = Plain(FillIcon(48, p => Mathf.Min(Mathf.Min(
            Segment(p, new Vector2(9, 9), new Vector2(42, 42), 3.2f),
            Poly(p, new Vector2(58, 58), new Vector2(34, 52), new Vector2(52, 34))),
            Mathf.Min(Segment(p, new Vector2(9, 9), new Vector2(5, 21), 2.4f), Segment(p, new Vector2(9, 9), new Vector2(21, 5), 2.4f)))));
        GlyphBlunt = Plain(FillIcon(48, p =>
        {
            float d = Mathf.Min(Circle(p, new Vector2(40, 40), 12f), Segment(p, new Vector2(8, 8), new Vector2(34, 34), 3.6f));
            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f) * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle)), side = new(-dir.y, dir.x), c = new(40, 40);
                d = Mathf.Min(d, Poly(p, c + dir * 20f, c + dir * 9f + side * 5f, c + dir * 9f - side * 5f));
            }
            return d;
        }));
        GlyphChop = Plain(FillIcon(48, p => Mathf.Min(Segment(p, new Vector2(16, 5), new Vector2(38, 59), 3.2f),
            Mathf.Max(Circle(p, new Vector2(36, 43), 19f), 33f - p.x))));
        GlyphPickaxe = Plain(FillIcon(48, p => Mathf.Min(
            Mathf.Max(Mathf.Abs(Circle(p, new Vector2(32, 6), 50f)) - 4.5f, Box(p, new Vector2(32, 52), new Vector2(29, 12))),
            Segment(p, new Vector2(32, 54), new Vector2(32, 5), 3.4f))));
        GlyphFire = Plain(FillIcon(48, p => Mathf.Max(
            SmoothMin(Circle(p, new Vector2(32, 22), 17f), Poly(p, new Vector2(17, 27), new Vector2(47, 27), new Vector2(37, 62)), 6f),
            -SmoothMin(Circle(p, new Vector2(32, 17), 7.5f), Poly(p, new Vector2(25.5f, 19), new Vector2(38.5f, 19), new Vector2(34, 38)), 3f))));
        GlyphFrost = Plain(FillIcon(48, p =>
        {
            Vector2 c = new(32, 32);
            float d = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                float angle = (90f + i * 60f) * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle)), side = new(-dir.y, dir.x);
                d = Mathf.Min(d, Segment(p, c, c + dir * 27f, 2.6f));
                d = Mathf.Min(d, Segment(p, c + dir * 15f, c + dir * 22f + side * 7f, 2f));
                d = Mathf.Min(d, Segment(p, c + dir * 15f, c + dir * 22f - side * 7f, 2f));
            }
            return d;
        }));
        GlyphLightning = Plain(FillIcon(48, p => Mathf.Min(
            Poly(p, new Vector2(42, 62), new Vector2(16, 28), new Vector2(37, 30)),
            Poly(p, new Vector2(27, 35), new Vector2(48, 37), new Vector2(21, 2)))));
        GlyphPoison = Plain(FillIcon(48, p => Mathf.Max(
            Mathf.Min(Circle(p, new Vector2(32, 22), 16f), Poly(p, new Vector2(17.4f, 28), new Vector2(46.6f, 28), new Vector2(32, 61))),
            -Circle(p, new Vector2(26, 21), 4.5f))));
        GlyphSpirit = Plain(FillIcon(48, p =>
        {
            float body = Mathf.Min(Circle(p, new Vector2(32, 38), 16f), Box(p, new Vector2(32, 24), new Vector2(16, 14)));
            body = Mathf.Max(body, -Mathf.Min(Circle(p, new Vector2(24, 9), 4.6f), Circle(p, new Vector2(40, 9), 4.6f)));
            return Mathf.Max(body, -Mathf.Min(Circle(p, new Vector2(26, 39), 3.4f), Circle(p, new Vector2(38, 39), 3.4f)));
        }));
        // User-supplied SVG path; sampled cubic curves retain its rounded lower edge.
        InventoryShield = Plain(MakeInventoryShield());
        GlyphShield = Plain(FillIcon(48, p => Mathf.Max(
            Poly(p, new Vector2(10, 56), new Vector2(54, 56), new Vector2(54, 36), new Vector2(32, 4), new Vector2(10, 36)),
            -Mathf.Min(Segment(p, new Vector2(32, 52), new Vector2(32, 14), 1.6f), Segment(p, new Vector2(18, 44), new Vector2(46, 44), 1.6f)))));
        GlyphHealth = Plain(FillIcon(48, p => SmoothMin(
            Mathf.Min(Circle(p, new Vector2(21, 41), 13f), Circle(p, new Vector2(43, 41), 13f)),
            Poly(p, new Vector2(9.5f, 35), new Vector2(54.5f, 35), new Vector2(32, 5)), 3f)));
        GlyphStamina = Plain(FillIcon(48, p => Mathf.Max(Circle(p, new Vector2(30, 32), 25f), -Circle(p, new Vector2(43, 39), 21f))));
        GlyphEitr = Plain(FillIcon(48, p => Mathf.Min(Circle(p, new Vector2(32, 22), 16f),
            Poly(p, new Vector2(17.4f, 28), new Vector2(46.6f, 28), new Vector2(32, 61)))));
        GlyphWeight = Plain(FillIcon(48, p => Mathf.Min(
            SmoothMin(Box(p, new Vector2(32, 20), new Vector2(18, 14)), Circle(p, new Vector2(32, 26), 15f), 4f),
            Mathf.Max(Mathf.Abs(Circle(p, new Vector2(32, 42), 11f)) - 3.6f, 40f - p.y))));
        GlyphCoin = Plain(FillIcon(48, p => Mathf.Max(Circle(p, new Vector2(32, 32), 25f),
            -(Mathf.Abs(Circle(p, new Vector2(32, 32), 17f)) - 2f))));
        GlyphAlert = Plain(FillIcon(48, p => Mathf.Max(Circle(p, new Vector2(32, 32), 28f),
            -Mathf.Min(Box(p, new Vector2(32, 39), new Vector2(3.8f, 12f)), Circle(p, new Vector2(32, 17), 4.4f)))));
        GlyphEffect = Plain(FillIcon(48, p => Mathf.Min(
            Poly(p, new Vector2(32, 61), new Vector2(38, 32), new Vector2(32, 3), new Vector2(26, 32)),
            Poly(p, new Vector2(3, 32), new Vector2(32, 38), new Vector2(61, 32), new Vector2(32, 26)))));
        // Crossed fork and spoon, for the food row's badge: the fork's tines top left, the spoon's bowl top right.
        GlyphMeal = Plain(FillIcon(48, p =>
        {
            Vector2 spoonFrom = new(13, 8), spoonTo = new(36, 38);
            Vector2 along = (spoonTo - spoonFrom).normalized, across = new(-along.y, along.x);
            Vector2 bowl = p - (spoonTo + along * 9f);
            float u = Vector2.Dot(bowl, along) / 11f, v = Vector2.Dot(bowl, across) / 7.5f;
            float spoon = Mathf.Min(Segment(p, spoonFrom, spoonTo, 2.6f), (Mathf.Sqrt(u * u + v * v) - 1f) * 7.5f);
            Vector2 from = new(51, 8), to = new(26, 42);
            Vector2 dir = (to - from).normalized, side = new(-dir.y, dir.x);
            float fork = Mathf.Min(Segment(p, from, to, 2.6f), Segment(p, to - side * 6.5f, to + side * 6.5f, 2.4f));
            for (int i = -1; i <= 1; i++)
                fork = Mathf.Min(fork, Segment(p, to + side * (6.5f * i), to + side * (6.5f * i) + dir * 14f, 1.9f));
            return Mathf.Min(spoon, fork);
        }));
        Cap = Plain(Make(20, 20, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            float d = Segment(p, new Vector2(1.5f, 1f), new Vector2(1.5f, 18f), 0f);
            d = Mathf.Min(d, Segment(p, new Vector2(1.5f, 18f), new Vector2(19f, 1f), 0f));
            d = Mathf.Min(d, Segment(p, new Vector2(5f, 10f), new Vector2(11f, 4f), 0f));
            d = Mathf.Min(d, Mathf.Abs(Circle(p, new Vector2(6f, 5.5f), 2.2f)));
            d = Mathf.Min(d, Segment(p, new Vector2(1.5f, 1f), new Vector2(19f, 1f), 0f));
            return new Color(1, 1, 1, Mathf.Clamp01(1.1f - d));
        }));
        // The health emblem: a dark disc in a brass ring, a bold red triquetra on it, one point down.
        Emblem = Plain(Make(80, 80, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f), c = new(40, 40);
            float r = (p - c).magnitude;
            if (r > 39.5f) return Color.clear;
            Color color = Palette.Hex(0x222120, 0.97f);
            color = Color.Lerp(color, Palette.Gold, Mathf.Clamp01(2.3f - Mathf.Abs(r - 37.2f)));
            color = Color.Lerp(color, Palette.Hex(0x0E0D0C), Mathf.Clamp01(1.2f - Mathf.Abs(r - 34.4f)) * 0.8f);
            color = Color.Lerp(color, Palette.Hex(0xE11D2E), Mathf.Clamp01(3.2f - Triquetra(p, c, 19.5f, 0f)));
            color.a *= Mathf.Clamp01(39.5f - r);
            return color;
        }));
        // A gold braid, tiling upwards: two strands that cross twice per tile, each passing over at one
        // crossing and under at the other, edged in ink so the weave reads at a few pixels wide.
        Braid = Plain(Make(14, 16, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            Color result = Color.clear;
            int over = Mathf.Abs(p.y - 8f) < 4f ? 1 : -1;
            foreach (int strand in new[] { -over, over })
            {
                float phase = p.y / 16f * Mathf.PI * 2f;
                float centre = 7f + strand * 3.6f * Mathf.Sin(phase);
                float slope = strand * 3.6f * Mathf.PI * 2f / 16f * Mathf.Cos(phase);
                float d = Mathf.Abs(p.x - centre) / Mathf.Sqrt(1f + slope * slope);
                float alpha = Mathf.Clamp01(2.9f - d);
                if (alpha <= 0) continue;
                Color layer = Color.Lerp(Palette.Ink, Color.Lerp(Palette.Gold, Palette.GoldBright, Mathf.Clamp01(0.8f - d)), Mathf.Clamp01(2.3f - d));
                result = Color.Lerp(result, layer, alpha);
                result.a = Mathf.Max(result.a, alpha);
            }
            return result;
        }));
        Braid.texture.wrapMode = TextureWrapMode.Repeat;
        // The round minimap: a white disc to mask the map with, the rope-edged ring around it (a dark band
        // between two brass hairlines, twisted strands running round it), and the medallion its buttons sit in.
        Disc = Plain(Make(256, 256, (x, y) =>
        {
            float r = new Vector2(x + 0.5f - 128, y + 0.5f - 128).magnitude;
            return new Color(1, 1, 1, Mathf.Clamp01(127.5f - r));
        }));
        Ring = Plain(Make(256, 256, (x, y) =>
        {
            Vector2 p = new(x + 0.5f - 128, y + 0.5f - 128);
            float r = p.magnitude;
            if (r < RingInner - 1 || r > 128) return Color.clear;
            Color color = Palette.HudGlass;
            // Fine radial divisions between two clean brass rings.
            float angle = Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f);
            float tick = Mathf.Abs(Mathf.Repeat(angle * 60f + 0.5f, 1f) - 0.5f);
            if (tick < 0.045f && r > 121f) color = Palette.Gold.WithAlpha(0.65f);
            color = Color.Lerp(color, Palette.Gold, Mathf.Clamp01(1.4f - Mathf.Abs(r - 126.6f)));
            color = Color.Lerp(color, Palette.GoldBright, Mathf.Clamp01(1.3f - Mathf.Abs(r - RingInner)));
            color.a *= Mathf.Clamp01(128f - r) * Mathf.Clamp01(r - RingInner + 1.5f);
            return color;
        }));
        Medallion = Plain(Make(64, 64, (x, y) =>
        {
            float r = new Vector2(x + 0.5f - 32, y + 0.5f - 32).magnitude;
            if (r > 32) return Color.clear;
            Color color = Palette.HudGlass;
            color = Color.Lerp(color, Palette.Gold, Mathf.Clamp01(1.4f - Mathf.Abs(r - 30.2f)));
            color = Color.Lerp(color, Palette.GoldDim, Mathf.Clamp01(1f - Mathf.Abs(r - 26.5f)));
            color.a *= Mathf.Clamp01(31.8f - r);
            return color;
        }));
        // Vitals: a bar texture to tint (a touch lighter at the top, faintly mottled), the baseline's ticks,
        // and the ruler cap that ends the health bar's baseline (a right triangle with its inner grid).
        BarFill = Plain(Make(64, 32, (x, y) =>
        {
            float shade = Mathf.Lerp(0.8f, 1f, Mathf.Pow(y / 31f, 1.3f));
            shade += (Mathf.PerlinNoise(x * 0.35f, y * 0.35f) - 0.5f) * 0.08f;
            if (y >= 30) shade = Mathf.Min(1f, shade + 0.06f);
            return new Color(shade, shade, shade, 1f);
        }));
        Tick = Plain(Make(12, 8, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            return new Color(1, 1, 1, Mathf.Clamp01(0.8f - Poly(p, new Vector2(0.5f, 0), new Vector2(11.5f, 0), new Vector2(6, 7.5f))));
        }));
        HatchCap = Plain(Make(36, 46, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            const float w = 34.5f, h = 44.5f;
            float hyp = Mathf.Abs((p.x - 1.5f) / (w - 1.5f) + (p.y - 1f) / (h - 1f) - 1f) * 26f;
            bool inside = (p.x - 1.5f) / (w - 1.5f) + (p.y - 1f) / (h - 1f) <= 1.02f && p.x >= 0.5f;
            float d = Mathf.Min(Mathf.Abs(p.x - 1.8f), hyp);
            if (inside)
                foreach (float f in new[] { 1f / 3f, 2f / 3f })
                {
                    d = Mathf.Min(d, Mathf.Abs(p.x - (1.5f + (w - 1.5f) * f)));
                    d = Mathf.Min(d, Mathf.Abs(p.y - (1f + (h - 1f) * f)));
                }
            return new Color(1, 1, 1, inside || hyp < 2f ? Mathf.Clamp01(1.9f - d) : 0f);
        }));
        // The Forsaken power's key: a bevelled grey cap in a brass edge. And the arc that fills round the
        // power as it recharges.
        PowerKey = Plain(Make(52, 52, (x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(x + 0.5f, 52 - x - 0.5f), Mathf.Min(y + 0.5f, 52 - y - 0.5f));
            if (edge < 2.5f) return Palette.Gold;
            if (edge < 5f) return Palette.Hex(0x111111, 0.96f);
            if (edge < 7.5f) return Palette.Hex(0x5E5C58);
            return Color.Lerp(Palette.Hex(0x2E2D2B), Palette.Hex(0x3E3D3A), y / 51f);
        }));
        Arc = Plain(Make(64, 64, (x, y) =>
        {
            float r = new Vector2(x + 0.5f - 32, y + 0.5f - 32).magnitude;
            return new Color(1, 1, 1, Mathf.Clamp01(2f - Mathf.Abs(r - 28.5f)));
        }));
        Reticle = Plain(Make(32, 32, (x, y) =>
        {
            Vector2 p = new(x + 0.5f, y + 0.5f), c = new(16, 16);
            float d = Circle(p, c, 1.3f);
            d = Mathf.Min(d, Segment(p, c + new Vector2(0, 5), c + new Vector2(0, 13), 1f));
            d = Mathf.Min(d, Segment(p, c - new Vector2(0, 5), c - new Vector2(0, 13), 1f));
            d = Mathf.Min(d, Segment(p, c + new Vector2(5, 0), c + new Vector2(13, 0), 1f));
            d = Mathf.Min(d, Segment(p, c - new Vector2(5, 0), c - new Vector2(13, 0), 1f));
            return new Color(1, 1, 1, Mathf.Clamp01(0.6f - d));
        }));
        // Head over shoulders: a dome cut flat at the bottom.
        IconPlayer = Plain(LineIcon(44, 0f, p => Lines(
            Circle(p, new Vector2(32, 45), 9.5f),
            Mathf.Max(Circle(p, new Vector2(32, 10), 21f), 8f - p.y))));
        IconShip = Plain(LineIcon(44, 0f, p => Lines(
            Poly(p, new Vector2(6, 26), new Vector2(58, 26), new Vector2(48, 13), new Vector2(16, 13)),
            Segment(p, new Vector2(30, 26), new Vector2(30, 58), 0f),
            Poly(p, new Vector2(33, 55), new Vector2(33, 31), new Vector2(54, 33)))));
    }

    // A dark field that fades out over `feather` pixels on every side; nine-sliced so any size stays soft.
    private Sprite Feathered(int feather)
    {
        int size = feather * 2 + 8;
        Texture2D texture = Make(size, size, (x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
            float t = Mathf.Clamp01(edge / feather);
            return new Color(0.02f, 0.018f, 0.015f, t * t * (3f - 2f * t));
        });
        return Track(Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(feather, feather, feather, feather)));
    }

    // Rune & Iron guide: Cinzel 600 names/titles, Barlow Semi Condensed 600 labels,
    // 700 numbers, and Noto Sans Runic sigils. All are embedded OFL fonts.
    internal TMP_FontAsset? Heading => heading ??= FontLibrary.Get("Cinzel-SemiBold.ttf") ?? FindFont("Norsebold", "Norse") ?? Body;
    internal TMP_FontAsset? Display => Heading;
    internal TMP_FontAsset? Section => Heading;
    internal TMP_FontAsset? Body => body ??= FontLibrary.Get("BarlowSemiCondensed-Regular.ttf") ?? Fallback();
    internal TMP_FontAsset? Numbers => numbers ??= FontLibrary.Get("BarlowSemiCondensed-Bold.ttf") ?? Body;
    internal TMP_FontAsset? Runic => runic ??= FontLibrary.Get("NotoSansRunic-Regular.ttf") ?? Body;
    internal TMP_FontAsset? Strong
    {
        get
        {
            if (strong) return strong;
            strong = FontLibrary.Get("BarlowSemiCondensed-SemiBold.ttf") ?? Body;
            // Rich text can mix guide-weight numbers into a single, baseline-aligned label.
            if (strong && Numbers) strong!.fontWeightTable[7].regularTypeface = Numbers;
            return strong;
        }
    }
    internal TMP_FontAsset? Subtle => subtle ??= FontLibrary.Get("BarlowSemiCondensed-Italic.ttf") ?? Body;

    private TMP_FontAsset? Fallback()
    {
        if (serif) return serif;
        if (Time.unscaledTime < nextFontScan) return null;
        nextFontScan = Time.unscaledTime + 2f;
        return serif = FindFont("AveriaSerifLibre-Bold", "AveriaSerifLibre-Regular", "AveriaSerif");
    }

    public void Dispose()
    {
        foreach (UnityEngine.Object item in owned)
            if (item) UnityEngine.Object.Destroy(item);
        owned.Clear();
        styles.Clear();
        sizedStyles.Clear();
        Mono.Dispose();
        FontLibrary.Dispose();
        if (Current == this) Current = null;
    }

    private static TMP_FontAsset? FindFont(params string[] names)
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (string name in names)
        {
            TMP_FontAsset? exact = fonts.FirstOrDefault(f => f && f.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (exact) return exact;
        }
        return null;
    }

    private enum Surface { Iron, Carved, Leather, Recess, Mask }

    // The small sliced sprite stands in until ChipSkin supplies a surface at its real size.
    private sealed class ChamferStyle
    {
        internal Color Fill, Border;
        internal Sprite Base = null!;
        internal int Cut, Seed;
        internal Surface Material;
        internal readonly Dictionary<(int, int, bool), Sprite> Sized = new();
    }

    private readonly Dictionary<Sprite, ChamferStyle> styles = new();
    private readonly Dictionary<Sprite, ChamferStyle> sizedStyles = new();

    // Panel with square corners, the shared shape of every frame (`cut` sets the border slicing).
    private Sprite Chamfer(Color fill, Color border, int cut, Surface material = Surface.Iron)
    {
        int size = cut * 2 + 12;
        Texture2D texture = Make(size, size, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f, top = size - py;
            float d = Mathf.Min(Mathf.Min(px, size - px), Mathf.Min(py, top));
            if (d <= -0.5f) return Color.clear;
            Color color = Color.Lerp(border, fill, Mathf.Clamp01(d - 1f));
            color.a *= Mathf.Clamp01(d + 0.5f);
            return color;
        });
        int b = cut + 2;
        Sprite sprite = Track(Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(b, b, b, b)));
        // The chip pattern follows the shape (border size), not the colours: a slot, its hover and its ring,
        // or a button and its pressed state, keep one outline when they swap.
        styles[sprite] = new ChamferStyle { Base = sprite, Fill = fill, Border = border, Cut = cut,
            Seed = 11 + cut * 7, Material = material };
        return sprite;
    }

    // Is this one of the frames (or a chipped version of one)?
    internal bool IsChamfer(Sprite? sprite) => sprite && (styles.ContainsKey(sprite!) || sizedStyles.ContainsKey(sprite!));

    // Both edge options receive material detail. Solid keeps its clean silhouette; Chipped adds wear.
    // Mask sprites stay white/transparent so bar masks and selection overlays retain their meaning.
    internal Sprite? Chipped(Sprite source, int width, int height)
    {
        if (!styles.TryGetValue(source, out ChamferStyle? style) && !sizedStyles.TryGetValue(source, out style)) return null;
        bool chipped = ChipEdges();
        if (!chipped && style!.Material == Surface.Mask) return style.Base;
        width = Mathf.Clamp(width, 4, 2048);
        height = Mathf.Clamp(height, 4, 2048);
        if (style!.Sized.TryGetValue((width, height, chipped), out Sprite? cached)) return cached;
        Sprite sprite = Plain(MakeChipped(style, width, height, chipped));
        style.Sized[(width, height, chipped)] = sprite;
        sizedStyles[sprite] = style;
        return sprite;
    }

    // The frame drawn at its real size, its edges chipped like worn metal: a fine angular roughness all
    // along, now and then a deeper bite (never more than a fifth of the frame's thickness)
    // and a faint grime in the fill.
    private static Texture2D MakeChipped(ChamferStyle style, int width, int height, bool chipped)
    {
        int seed = style.Seed;
        float maxBite = Mathf.Min(3f, Mathf.Min(width, height) * 0.12f);
        return Make(width, height, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f, top = height - py, right = width - px;
            float plain = Mathf.Min(Mathf.Min(px, right), Mathf.Min(py, top));
            float d = plain;
            // Deep inside, the chips cannot reach: skip their noise.
            if (chipped && plain <= maxBite + 3f)
            {
            d = Mathf.Min(
                Mathf.Min(px - Chip(py, seed, maxBite), right - Chip(py, seed + 1, maxBite)),
                Mathf.Min(py - Chip(px, seed + 2, maxBite), top - Chip(px, seed + 3, maxBite)));
            }
            if (d <= -0.5f) return Color.clear;
            Color color = SurfaceColor(style, px, py, width, height, d);
            color.a *= Mathf.Clamp01(d + 0.5f);
            return color;
        });
    }

    // A frame's colour at a point: the flat ground (a faint grain, like the guide's dark paper), a single
    // brass line on its edge, and its ornament: knotwork in the corners of a window, a small flourish in
    // the top-left corner of a card or plate. Slots and fields sit in a soft inner shadow.
    private static Color SurfaceColor(ChamferStyle style, float x, float y, int width, int height, float edge)
    {
        if (style.Material == Surface.Mask)
            return Color.Lerp(style.Border, style.Fill, Mathf.Clamp01(edge - 1f));

        float grain = Hash((int)x, (int)y);
        float cloud = TileNoise(x / 23f, y / 19f, 4096, 4096);
        float finish = 0.95f + cloud * 0.07f + grain * 0.03f;
        Color fill = style.Fill;
        Color inner = new(fill.r * finish, fill.g * finish, fill.b * finish, fill.a);
        if (style.Material == Surface.Recess && edge < 5f)
            inner = Color.Lerp(inner, Palette.Ink.WithAlpha(fill.a), (5f - edge) / 4f * 0.35f);

        // Distances from the nearest corner along each axis, and from the top-left corner (u right, v down).
        float u = x, v = height - y;
        float a = Mathf.Min(x, width - x), b = Mathf.Min(y, height - y);
        bool window = style.Material == Surface.Carved && width >= 120 && height >= 90;
        // A window's edge line stops short of its corners, where the knots sit.
        bool line = !(window && a < KnotReach && b < KnotReach);
        Color result = line ? Color.Lerp(style.Border, inner, Mathf.Clamp01(edge - 1f)) : inner;

        Color ink = style.Border.WithAlpha(Mathf.Max(style.Border.a, 0.85f) * fill.a);
        float ornament = 0f;
        if (window) ornament = CornerKnot(a, b);
        else if (style.Material == Surface.Iron && width >= 56 && height >= 44) ornament = Flourish(u, v);
        if (ornament > 0f) result = Color.Lerp(result, ink, ornament);
        return result;
    }

    private const float KnotReach = 31f;

    // The corner flourish of a card, about 16 pixels: a diagonal closing the corner into a small triangle,
    // with a little triquetra knotted inside it, one tip into the corner.
    private static float Flourish(float u, float v)
    {
        if (u > 18f || v > 18f) return 0f;
        Vector2 p = new(u, v);
        float d = Segment(p, new Vector2(0.5f, 16f), new Vector2(16f, 0.5f), 0f);
        d = Mathf.Min(d, Triquetra(p, new Vector2(5.4f, 5.4f), 3.6f, -45f));
        return Mathf.Clamp01(1.05f - d);
    }

    // A window corner: a triquetra (three interlaced lens loops) bound by a ring, one tip in the corner and
    // the others along the edges, whose lines stop just short of it with a small diamond. `a`, `b` are the
    // distances from the corner.
    private static float CornerKnot(float a, float b)
    {
        if (a >= KnotReach + 2f || b >= KnotReach + 2f) return 0f;
        Vector2 p = new(a, b), centre = new(14f, 14f);
        float d = Mathf.Min(Triquetra(p, centre, 10f, -45f), Mathf.Abs(Circle(p, centre, 7.6f)));
        float diamond = Mathf.Min(Mathf.Abs(p.x - KnotReach) + Mathf.Abs(p.y - 0.5f), Mathf.Abs(p.x - 0.5f) + Mathf.Abs(p.y - KnotReach)) - 1.6f;
        return Mathf.Max(Mathf.Clamp01(1.2f - d), Mathf.Clamp01(0.5f - diamond));
    }

    // Distance to a triquetra's line: three circles of radius `r` whose centres form an equilateral triangle
    // of side r; the knot is each circle's arc that lies inside another circle (the three lenses).
    private static float Triquetra(Vector2 p, Vector2 centre, float r, float degrees)
    {
        float d = float.MaxValue;
        Vector2 c0 = Lobe(0), c1 = Lobe(1), c2 = Lobe(2);
        d = Mathf.Min(d, Arc(c0, c1, c2));
        d = Mathf.Min(d, Arc(c1, c2, c0));
        d = Mathf.Min(d, Arc(c2, c0, c1));
        return d;

        Vector2 Lobe(int i)
        {
            float angle = (degrees + 90f + 120f * i) * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (r / 1.732f);
        }

        float Arc(Vector2 c, Vector2 other1, Vector2 other2)
        {
            Vector2 offset = p - c;
            float length = offset.magnitude;
            if (length < 0.001f) return float.MaxValue;
            Vector2 q = c + offset / length * r;
            bool inside = (q - other1).magnitude <= r + 0.01f || (q - other2).magnitude <= r + 0.01f;
            return inside ? Mathf.Abs(length - r) : float.MaxValue;
        }
    }

    // How far the edge is eaten away at a point along it: an angular roughness (linear, not smoothed) and
    // now and then a deeper rounded bite.
    private static float Chip(float s, int seed, float maxBite)
    {
        float jag = Noise1(s / 6f, seed, smooth: false) * 0.5f;
        float bite = Mathf.Pow(Noise1(s / 24f, seed + 97, smooth: true), 6f) * maxBite;
        return jag + bite;
    }

    private static float Noise1(float t, int seed, bool smooth)
    {
        int i = Mathf.FloorToInt(t);
        float f = t - i;
        if (smooth) f = f * f * (3 - 2 * f);
        return Mathf.Lerp(Hash(i, seed), Hash(i + 1, seed), f);
    }

    private static Texture2D MakeDiamond(int size)
    {
        float c = size / 2f;
        return Make(size, size, (x, y) =>
        {
            float d = (c - Mathf.Abs(x + 0.5f - c) - Mathf.Abs(y + 0.5f - c)) / 1.4142f;
            return d <= -0.5f ? Color.clear : new Color(1, 1, 1, Mathf.Clamp01(d + 0.5f));
        });
    }

    // A portal ring with a rune at its heart, struck through: "cannot pass through a portal".
    private static Texture2D MakeNoTeleport(int size)
    {
        float c = size / 2f;
        return Make(size, size, (x, y) =>
        {
            float px = x + 0.5f - c, py = y + 0.5f - c;
            float r = Mathf.Sqrt(px * px + py * py);
            float ring = Mathf.Abs(r - size * 0.36f) - size * 0.06f;
            float rune = (Mathf.Abs(px) + Mathf.Abs(py)) / 1.4142f - size * 0.09f;
            float slash = Mathf.Abs(px + py) / 1.4142f - size * 0.055f;
            if (r > size * 0.47f) slash = 99;
            // The slash cuts a gap into the ring so the strike reads at tiny sizes.
            float gap = Mathf.Abs(px + py) / 1.4142f - size * 0.13f;
            float d = Mathf.Min(Mathf.Max(Mathf.Min(ring, rune), -gap), slash);
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
    }

    // White line-art glyph: the outline of a shape union given as a signed distance in a 64-unit
    // square, optionally rotated about the centre. Tinted at use, like the other theme sprites.
    private static Texture2D LineIcon(int size, float degrees, Func<Vector2, float> shape)
    {
        float scale = 64f / size, stroke = 2.4f;
        float rad = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return Make(size, size, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) * scale - new Vector2(32, 32);
            p = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos) * 1.12f + new Vector2(32, 32);
            float d = (Mathf.Abs(shape(p)) - stroke) / scale;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
    }

    // White filled glyph: the inside of a shape given as a signed distance in a 64-unit square.
    private static Texture2D MakeInventoryShield()
    {
        // SVG viewBox 0 0 24 24, stroke-width 2, round joins, no fill.
        List<Vector2> path = new() { new(12, 3), new(19, 6), new(19, 12) };
        void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f, u = 1 - t;
                path.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
            }
        }
        Curve(new(19, 12), new(19, 16.5f), new(16, 19.5f), new(12, 21));
        Curve(new(12, 21), new(8, 19.5f), new(5, 16.5f), new(5, 12));
        path.Add(new Vector2(5, 6));
        path.Add(path[0]);
        return Make(96, 96, (x, y) =>
        {
            Vector2 p = new((x + 0.5f) / 4, 24 - (y + 0.5f) / 4);
            float distance = float.MaxValue;
            for (int i = 1; i < path.Count; i++)
                distance = Mathf.Min(distance, Segment(p, path[i - 1], path[i], 1));
            return Color.white.WithAlpha(Mathf.Clamp01(0.5f - distance * 4));
        });
    }

    private static Texture2D FillIcon(int size, Func<Vector2, float> shape)
    {
        float scale = 64f / size;
        return Make(size, size, (x, y) =>
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) * scale;
            float d = shape(p) / scale;
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
    }

    private static float Box(Vector2 p, Vector2 centre, Vector2 half)
    {
        Vector2 d = new(Mathf.Abs(p.x - centre.x) - half.x, Mathf.Abs(p.y - centre.y) - half.y);
        return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
    }

    // Blends two shapes over `k` units, so the drumstick's meat flows into its bone.
    private static float SmoothMin(float a, float b, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1 - h);
    }

    private static float Circle(Vector2 p, Vector2 centre, float radius) => (p - centre).magnitude - radius;

    // Resolve native art from the installed game, never redistribute copied icons.
    internal Sprite? PowerGlyph(string? name)
    {
        if (string.IsNullOrEmpty(name) || !ObjectDB.instance) return null;
        StatusEffect effect = ObjectDB.instance.GetStatusEffect(name!.GetStableHashCode());
        return effect ? effect.m_icon : null;
    }

    // Several separate outlines in one icon: each shape keeps its own stroke.
    private static float Lines(params float[] shapes)
    {
        float d = float.MaxValue;
        foreach (float shape in shapes) d = Mathf.Min(d, Mathf.Abs(shape));
        return d;
    }

    // Convex polygon: the largest signed distance to its edges, normals pointing away from its centre.
    private static float Poly(Vector2 p, params Vector2[] corners)
    {
        Vector2 centre = Vector2.zero;
        foreach (Vector2 c in corners) centre += c;
        centre /= corners.Length;
        float d = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 a = corners[i], b = corners[(i + 1) % corners.Length];
            Vector2 normal = new Vector2(b.y - a.y, a.x - b.x).normalized;
            if (Vector2.Dot(normal, centre - a) > 0) normal = -normal;
            d = Mathf.Max(d, Vector2.Dot(p - a, normal));
        }
        return d;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - radius;
    }

    private Sprite Plain(Texture2D texture) =>
        Track(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f));

    // Nine-sliced white surfaces keep corner radius and hairline weight at inventory sizes.
    private Sprite RoundedPanel(float radius, bool outline)
    {
        const int size = 64;
        Texture2D texture = Make(size, size, (x, y) =>
        {
            Vector2 q = new(Mathf.Abs(x + 0.5f - size / 2) - (size / 2 - radius - 1),
                Mathf.Abs(y + 0.5f - size / 2) - (size / 2 - radius - 1));
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude +
                Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
            float alpha = Mathf.Clamp01(0.5f - d);
            if (outline) alpha *= Mathf.Clamp01(d + 1.5f);
            return Color.white.WithAlpha(alpha);
        });
        return Track(Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100, 0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2)));
    }

    private Sprite Track(Sprite sprite)
    {
        owned.Add(sprite.texture);
        owned.Add(sprite);
        return sprite;
    }

    // Deep arterial red, darker towards the bottom, clotted with soft noise, a wet highlight near the top.
    private static Texture2D MakeBloodFill(int width, int height)
    {
        Color dark = new(0.16f, 0.0f, 0.015f), mid = new(0.52f, 0.02f, 0.04f);
        return Make(width, height, (x, y) =>
        {
            float t = (y + 0.5f) / height;
            Color c = Color.Lerp(dark, mid, Mathf.SmoothStep(0f, 0.85f, t));
            float clot = Fbm(x / (float)width * 10f, t * 2.5f, 10, 4, 64);
            c *= 0.72f + 0.45f * clot;
            c *= 0.65f + 0.35f * Mathf.SmoothStep(0f, 0.3f, t);
            float gloss = Mathf.Exp(-Mathf.Pow((t - 0.8f) / 0.06f, 2)) * 0.26f;
            c += new Color(gloss, gloss * 0.45f, gloss * 0.4f);
            c.a = 1f;
            return c;
        });
    }

    private Texture2D Owned(Texture2D texture)
    {
        owned.Add(texture);
        return texture;
    }

    // Amber to pale gold, streaked along its length, with a bright sheen near the top.
    private static Texture2D MakeMeadFill(int width, int height)
    {
        Color dark = new(0.42f, 0.20f, 0.02f), light = new(0.98f, 0.74f, 0.20f);
        return Make(width, height, (x, y) =>
        {
            float t = (y + 0.5f) / height;
            Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 0.9f, t));
            c *= 0.8f + 0.3f * Fbm(x / (float)width * 14f, t * 1.5f, 14, 4, 64);
            float gloss = Mathf.Exp(-Mathf.Pow((t - 0.78f) / 0.08f, 2)) * 0.3f;
            c += new Color(gloss, gloss * 0.9f, gloss * 0.55f);
            c.a = 1f;
            return c;
        });
    }

    // Thin pale-gold streaks, long and fast, like wind over a sprint.
    private static Texture2D MakeMeadFlow(int width, int height)
    {
        Color streak = new(1f, 0.95f, 0.7f);
        Texture2D texture = Make(width, height, (x, y) =>
        {
            float n = Fbm(x / (float)width * 3f, y / (float)height * 6f, 3, 4, 6);
            float s = Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 5f);
            return new Color(streak.r, streak.g, streak.b, Mathf.Clamp01(s * 0.85f));
        });
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    // A deep indigo well brightening to blue near the top, clouded like a nebula.
    private static Texture2D MakeArcaneFill(int width, int height)
    {
        Color dark = new(0.04f, 0.03f, 0.14f), light = new(0.24f, 0.26f, 0.70f);
        return Make(width, height, (x, y) =>
        {
            float t = (y + 0.5f) / height;
            Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 0.95f, t));
            c *= 0.7f + 0.5f * Fbm(x / (float)width * 8f, t * 2f, 8, 4, 64);
            float gloss = Mathf.Exp(-Mathf.Pow((t - 0.8f) / 0.07f, 2)) * 0.2f;
            c += new Color(gloss * 0.7f, gloss * 0.8f, gloss);
            c.a = 1f;
            return c;
        });
    }

    // Soft glowing wisps where the noise rises, and a scatter of tiny motes of light.
    private static Texture2D MakeArcaneFlow(int width, int height)
    {
        Color low = new(0.45f, 0.55f, 1f), high = new(0.9f, 0.97f, 1f);
        Texture2D texture = Make(width, height, (x, y) =>
        {
            if (Hash(x * 7 + 3, y * 13 + 1) > 0.99f) return Color.white;
            float n = Fbm(x / (float)width * 3f, y / (float)height * 3f, 3, 5, 3);
            float wisp = Mathf.SmoothStep(0.44f, 0.72f, n);
            Color c = Color.Lerp(low, high, wisp);
            c.a = wisp * 0.9f;
            return c;
        });
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    // White where the art shows: right of a diagonal that runs from half-way along the top to the bottom-left
    // corner, its edge torn by noise.
    private static Texture2D MakeArtCut(int width, int height)
    {
        return Make(width, height, (x, y) =>
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            float edge = 0.5f * v + (Noise1(v * 40f, 501, smooth: false) - 0.5f) * 0.02f
                         + Mathf.Pow(Noise1(v * 9f, 777, smooth: true), 6f) * 0.03f;
            return new Color(1, 1, 1, Mathf.Clamp01((u - edge) * width + 0.5f));
        });
    }

    // Smouldering dark: ember-red rising from the bottom into deep obsidian, clouded with smoke.
    private static Texture2D MakeArtScene(int width, int height)
    {
        Color low = new(0.32f, 0.07f, 0.04f), high = new(0.03f, 0.03f, 0.05f), cold = new(0.05f, 0.09f, 0.16f);
        return Make(width, height, (x, y) =>
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            Color c = Color.Lerp(low, high, Mathf.SmoothStep(0f, 0.65f, v));
            c = Color.Lerp(c, cold, Mathf.SmoothStep(0.5f, 1f, v) * u * 0.8f);
            float smoke = Fbm(u * 4f, v * 6f, 64, 5);
            c *= 0.65f + 0.7f * smoke;
            c.a = 1f;
            return c;
        });
    }

    // Tileable swirls: bright veins where the noise ridges, dark clots in its hollows; drawn over the fill
    // and scrolled, so the blood seems to churn.
    private static Texture2D MakeBloodFlow(int width, int height)
    {
        Color clot = new(0.12f, 0f, 0.01f), vein = new(1f, 0.28f, 0.22f);
        Texture2D texture = Make(width, height, (x, y) =>
        {
            float n = Fbm(x / (float)width * 4f, y / (float)height * 4f, 4, 4, 4);
            float ridge = 1f - Mathf.Abs(2f * n - 1f);
            float veins = Mathf.Pow(ridge, 3f);
            Color c = Color.Lerp(clot, vein, Mathf.SmoothStep(0.15f, 0.85f, veins));
            c.a = Mathf.Clamp01(veins * 0.75f + Mathf.SmoothStep(0.55f, 0.2f, n) * 0.45f);
            return c;
        });
        texture.wrapMode = TextureWrapMode.Repeat;
        return texture;
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    // Value noise that repeats every periodX × periodY cells.
    private static float TileNoise(float x, float y, int periodX, int periodY)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int ax = Mod(x0, periodX), bx = Mod(x0 + 1, periodX), ay = Mod(y0, periodY), by = Mod(y0 + 1, periodY);
        return Mathf.Lerp(Mathf.Lerp(Hash(ax, ay), Hash(bx, ay), fx), Mathf.Lerp(Hash(ax, by), Hash(bx, by), fx), fy);
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;

    // Octaves of value noise; with periodY it tiles (each octave doubles both periods).
    private static float Fbm(float x, float y, int periodX, int octaves, int periodY = 0)
    {
        if (periodY <= 0) periodY = periodX;
        float sum = 0, amp = 0.5f, total = 0;
        for (int i = 0; i < octaves; i++)
        {
            int scale = 1 << i;
            sum += amp * TileNoise(x * scale, y * scale, periodX * scale, periodY * scale);
            total += amp;
            amp *= 0.5f;
        }
        return sum / total;
    }

    private static Texture2D Make(int width, int height, Func<int, int, Color> pixel)
    {
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}

// A living bar fill: the fill picture, a tileable layer drifting over it, how fast and how far it moves,
// the colour of the damage trail, and a mood that darkens the fill (and brightens the flow) by value and time.
internal sealed class LiquidSkin
{
    internal readonly Sprite Fill;
    internal readonly Texture2D Flow;
    internal float FlowAlpha = 0.7f, Drift = 0.05f, Heave = 0.06f, HeaveSpeed = 0.7f, Shine = 0.1f;
    internal float Scale = 150f; // Bar pixels per flow tile, so the pattern keeps its size on any bar length.
    internal Color Lag = new(0, 0, 0, 0.6f);
    internal Func<float, float, (float Dim, float Glow)>? Mood;

    internal LiquidSkin(Sprite fill, Texture2D flow)
    {
        // The fill repeats along the bar at a fixed size (tileable across its width), never stretched.
        fill.texture.wrapMode = TextureWrapMode.Repeat;
        Fill = fill;
        Flow = flow;
    }
}
