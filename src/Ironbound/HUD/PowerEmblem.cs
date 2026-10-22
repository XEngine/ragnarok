using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

internal struct HudPower
{
    internal bool Has;
    internal Sprite? Icon;
    internal string Name; // The power's status effect, e.g. "GP_Eikthyr".
    internal string Title; // Its display name, e.g. "Eikthyr".
    internal float Cooldown, MaxCooldown;
    internal float ActiveRemaining, ActiveDuration;

    internal static HudPower FromPlayer(Player player)
    {
        player.GetGuardianPowerHUD(out StatusEffect se, out float cooldown);
        if (!se) return default;
        HudPower power = new()
        {
            Has = true,
            Icon = se.m_icon,
            Name = se.name,
            Title = Localization.instance.Localize(se.m_name),
            Cooldown = Mathf.Max(0, cooldown),
            MaxCooldown = Mathf.Max(1f, GameplaySettings.Cooldown(se.m_cooldown), cooldown)
        };
        StatusEffect active = player.GetSEMan().GetStatusEffect(se.NameHash());
        if (active && active.m_ttl > 0)
        {
            power.ActiveRemaining = Mathf.Max(0, active.GetRemaningTime());
            power.ActiveDuration = active.m_ttl;
        }
        return power;
    }

    // A power partway through its recharge, for previews outside a world.
    internal static HudPower Demo() => new() { Has = true, Name = "GP_Eikthyr", Title = "Eikthyr", Cooldown = 252, MaxCooldown = 600 };
}

// One clean duration/recharge ring around a dark disc, with the key beside the caption.
internal sealed class PowerEmblem
{
    internal const float Size = 96, Width = 310;
    private readonly RectTransform root;
    private readonly MonochromeIcons icons;
    private readonly Theme theme;
    private readonly Image glow, icon, placeholder, progress, track;
    private readonly TextMeshProUGUI caption, title, state, key;
    private readonly RectTransform keyChip;
    private readonly List<TextOutline> outlines = new();
    private float readyAt = -10f, nextKeyRefresh, keyWidth = 26;
    private bool wasCooling, initialized;

    internal PowerEmblem(Theme theme, MonochromeIcons icons, Transform parent, Rect r)
    {
        this.icons = icons;
        this.theme = theme;
        root = Ugui.Node(parent, "Forsaken Power").At(r);
        Ugui.Image(root, "Disc", theme.Disc, Palette.HudGlass).At(2, 2, Size - 4, Size - 4);
        track = Ugui.Image(root, "Ring Track", theme.HudRing, Palette.Hex(0x514C3D)).At(0, 0, Size, Size);
        progress = Ugui.Image(root, "Remaining Ring", theme.HudRing, Palette.Gold).At(0, 0, Size, Size);
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Radial360;
        progress.fillOrigin = (int)Image.Origin360.Top;
        progress.fillClockwise = true;
        glow = Ugui.Image(root, "Ready Pulse", theme.HudRing, Palette.GoldBright).At(0, 0, Size, Size);
        glow.enabled = false;
        placeholder = Ugui.Image(root, "Empty", theme.Diamond, Palette.Faint).At(42, 42, 12, 12);
        icon = Ugui.Image(root, "Glyph", null, Palette.Gold).At(24, 24, 48, 48);
        icon.preserveAspect = true;

        const float textX = 112;
        RectTransform chip = Ugui.Node(root, "Key").At(textX, 9, 26, 26);
        keyChip = chip;
        // Sliced corners preserve their soft one-pixel edge at the displayed size.
        Ugui.Image(chip, "Face", theme.HudKeySurface, Palette.HudGlass).Stretch();
        Ugui.Image(chip, "Edge", theme.HudKeyEdge, Palette.Bone.WithAlpha(0.45f)).Stretch();
        key = Ugui.Text(chip, "Label", "", theme.Strong, 13, Palette.Bone.WithAlpha(0.9f), TextAlignmentOptions.Center).Stretch(3, 0, 3, 0);
        key.enableAutoSizing = true;
        key.fontSizeMin = 7;
        key.fontSizeMax = 13;
        key.overflowMode = TextOverflowModes.Ellipsis;
        caption = Ugui.Text(root, "Caption", "FORSAKEN POWER", theme.Strong, 11, Palette.Bone, spacing: 2)
            .At(146, 9, Width - 146, 26);
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 8;
        caption.fontSizeMax = 11;
        title = Ugui.Text(root, "Name", "", theme.Heading, 24, Palette.Bone).At(textX, 38, Width - textX, 33);
        title.richText = false;
        title.enableAutoSizing = true;
        title.fontSizeMin = 14;
        title.fontSizeMax = 24;
        title.overflowMode = TextOverflowModes.Overflow;
        state = Ugui.Text(root, "State", "", theme.Strong, 13, Palette.Bone).At(textX, 72, Width - textX, 24);
        state.overflowMode = TextOverflowModes.Overflow;
        foreach (TextMeshProUGUI label in new[] { key, caption, title, state })
            outlines.Add(new TextOutline(label));
    }

    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    internal void Set(HudPower power)
    {
        float now = Time.unscaledTime;
        bool active = power.Has && power.ActiveRemaining > 0;
        bool cooling = power.Has && power.Cooldown > 0;
        if (initialized && wasCooling && !cooling && power.Has) readyAt = now;
        wasCooling = cooling;
        initialized = true;

        Sprite? glyph = !power.Has ? null : power.Name == "GP_Eikthyr" ? theme.HudStag
            : theme.PowerGlyph(power.Name) ?? icons.Get(power.Icon);
        icon.sprite = glyph;
        icon.enabled = glyph;
        placeholder.enabled = !glyph;
        icon.color = Palette.Gold.WithAlpha(cooling && !active ? 0.5f : 1);
        track.sprite = power.Has ? theme.HudRing : theme.HudDashedRing;
        track.color = power.Has ? Palette.Hex(0x514C3D) : Palette.Muted.WithAlpha(0.55f);
        progress.enabled = power.Has;
        progress.fillAmount = active ? Mathf.Clamp01(power.ActiveRemaining / Mathf.Max(1, power.ActiveDuration))
            : cooling ? 1 - Mathf.Clamp01(power.Cooldown / Mathf.Max(1, power.MaxCooldown)) : 1;

        caption.text = power.Has ? "FORSAKEN POWER" : "UNCLAIMED";
        title.text = (power.Has ? power.Title ?? "" : "A power awaits").ToUpperInvariant();
        title.color = power.Has ? Palette.Bone : Palette.Muted;
        title.At(112, power.Has ? 38 : 34, Width - 112, power.Has ? 33 : 30);
        string effect = power.Name == "GP_Eikthyr" ? "Run & jump" : "Active";
        state.text = !power.Has ? "Defeat a Forsaken. Offer its trophy\nat the Sacrificial Stones."
            : active ? effect + " - " + Clock(power.ActiveRemaining)
            : cooling ? "Cooldown - " + Clock(power.Cooldown)
            : "<color=#C5A65C>Ready</color>";
        state.fontSize = power.Has ? 13 : 11;
        state.alignment = power.Has ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
        state.At(112, power.Has ? 72 : 68, Width - 112, power.Has ? 24 : 28);
        keyChip.gameObject.SetActive(power.Has);
        if (power.Has && now >= nextKeyRefresh)
        {
            nextKeyRefresh = now + 1;
            key.text = KeyName();
            // Measure longer rebound keys instead of squeezing them into a one-letter chip.
            keyWidth = Mathf.Clamp(key.GetPreferredValues(key.text, Mathf.Infinity, 26).x + 10, 26, 70);
        }
        keyChip.At(112, 9, keyWidth, 26);
        caption.At(power.Has ? 112 + keyWidth + 8 : 112, 9,
            power.Has ? Width - 112 - keyWidth - 8 : Width - 112, 26);
        key.color = Palette.Bone.WithAlpha(0.9f);
        float flare = 1 - Mathf.Clamp01((now - readyAt) / 0.8f);
        glow.enabled = power.Has && flare > 0;
        glow.color = Palette.GoldBright.WithAlpha(flare * 0.8f);
        foreach (TextOutline outline in outlines) outline.Sync();
    }

    private static string Clock(float seconds) =>
        "<color=#C5A65C><mspace=0.6em>" + StatusEffect.GetTimeString(seconds) + "</mspace></color>";

    private static string KeyName()
    {
        if (ZInput.instance == null) return "F";
        string bound = ZInput.instance.GetBoundKeyString("GP", true);
        if (string.IsNullOrEmpty(bound)) return "UNBOUND";
        string localized = Localization.instance != null ? Localization.instance.Localize(bound) : bound;
        return localized.Length <= 4 ? localized.ToUpper(CultureInfo.InvariantCulture) : localized;
    }
}
