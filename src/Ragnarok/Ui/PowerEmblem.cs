using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

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
            MaxCooldown = Mathf.Max(1f, QualityOfLife.Cooldown(se.m_cooldown), cooldown)
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

// The Forsaken power after the style guide: a square brass-edged plate holding the boss glyph, its key in
// a cap astride the plate's top edge, and beside it the power's name in Alegreya over its state: "Ready"
// in gold, "Active: 4:45" while it runs, "Not ready" with the recharge time while it cools. A shade
// drains down the plate as the power recharges, and the plate flares once when it is ready again.
internal sealed class PowerEmblem
{
    internal const float PlateWidth = 48, PlateHeight = 48, KeyOverhang = 12;

    private readonly RectTransform root;
    private readonly MonochromeIcons icons;
    private readonly Theme theme;
    private readonly Image glow, plate, shade, icon, placeholder;
    private readonly TextMeshProUGUI title, state, key;
    private readonly GameObject keyChip;
    private float readyAt = -10f, nextKeyRefresh;
    private bool wasCooling, initialized;
    private string shownTitle = "", shownState = "";

    // `r` is the area: the plate on its left, the text to its right.
    internal PowerEmblem(Theme theme, MonochromeIcons icons, Transform parent, Rect r)
    {
        this.icons = icons;
        this.theme = theme;
        root = Ugui.Node(parent, "Forsaken Power").At(r);

        glow = Ugui.Image(root, "Ready Glow", theme.SlotRing, Palette.GoldBright).At(-4, KeyOverhang - 4, PlateWidth + 8, PlateHeight + 8);
        plate = Ugui.Image(root, "Plate", theme.Plate, Color.white).At(0, KeyOverhang, PlateWidth, PlateHeight);
        placeholder = Ugui.Image(root, "Empty", theme.Diamond, Palette.Faint).At(PlateWidth / 2 - 7, KeyOverhang + PlateHeight / 2 - 7, 14, 14);
        icon = Ugui.Image(root, "Icon", null, Palette.Bone).At(6, KeyOverhang + 6, PlateWidth - 12, PlateHeight - 12);
        icon.preserveAspect = true;
        // Recharging: a shade over the plate's face that shrinks from the top as the power returns.
        shade = Ugui.Image(root, "Recharge", theme.White, Palette.Ink.WithAlpha(0.62f)).At(1, KeyOverhang + 1, PlateWidth - 2, PlateHeight - 2);
        shade.type = Image.Type.Filled;
        shade.fillMethod = Image.FillMethod.Vertical;
        shade.fillOrigin = (int)Image.OriginVertical.Bottom;

        Image chip = Ugui.Image(root, "Key", theme.Button, Color.white).At(PlateWidth / 2 - 11, 0, 22, 21);
        keyChip = chip.gameObject;
        key = Ugui.Text(chip.transform, "Label", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        key.overflowMode = TextOverflowModes.Overflow;

        title = Ugui.Text(root, "Name", "", theme.Heading, 20, Palette.Bone).At(PlateWidth + 12, KeyOverhang - 1, r.width - PlateWidth - 12, 30);
        state = Ugui.Text(root, "State", "", theme.Strong, 13, Palette.GoldBright).At(PlateWidth + 12, KeyOverhang + 25, r.width - PlateWidth - 12, 20);
    }

    internal void SetVisible(bool visible)
    {
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
    }

    internal void Set(HudPower power)
    {
        float now = Time.unscaledTime;
        bool active = power.ActiveRemaining > 0;
        bool cooling = power.Has && power.Cooldown > 0;
        if (initialized && wasCooling && !cooling && power.Has) readyAt = now;
        wasCooling = cooling;
        initialized = true;

        // Ragnarök's boss glyph when there is one, otherwise Valheim's icon made monochrome.
        Sprite? mono = !power.Has ? null : theme.PowerGlyph(power.Name) ?? icons.Get(power.Icon);
        if (icon.sprite != mono) icon.sprite = mono;
        icon.enabled = mono;
        placeholder.enabled = !mono;
        icon.color = active ? Color.Lerp(Palette.Bone, Palette.Alert, 0.4f) : cooling ? Palette.Bone.WithAlpha(0.45f) : Palette.Bone;

        float recharge = cooling && !active ? Mathf.Clamp01(power.Cooldown / power.MaxCooldown) : 0f;
        if (!Mathf.Approximately(shade.fillAmount, recharge)) shade.fillAmount = recharge;
        shade.enabled = recharge > 0;

        string name = power.Has ? power.Title ?? "" : "Forsaken power";
        if (name != shownTitle) title.text = shownTitle = name;
        title.color = power.Has ? Palette.Bone : Palette.Muted;
        string text = !power.Has ? "None"
            : active ? "Active: " + Clock(power.ActiveRemaining)
            : cooling ? "Not ready  " + Clock(power.Cooldown)
            : "Ready";
        if (text != shownState) state.text = shownState = text;
        state.color = !power.Has || (cooling && !active) ? Palette.Muted : Palette.GoldBright;

        bool showKey = power.Has;
        if (keyChip.activeSelf != showKey) keyChip.SetActive(showKey);
        if (showKey && now >= nextKeyRefresh)
        {
            nextKeyRefresh = now + 1f;
            key.text = KeyName();
        }
        key.color = cooling && !active ? Palette.Muted : Palette.Bone;

        float flare = 1f - Mathf.Clamp01((now - readyAt) / 0.8f);
        glow.enabled = flare > 0;
        if (flare > 0) glow.color = Palette.GoldBright.WithAlpha(flare * 0.8f);
    }

    private static string Clock(float seconds) =>
        "<mspace=0.6em>" + StatusEffect.GetTimeString(seconds) + "</mspace>";

    private static string KeyName()
    {
        if (ZInput.instance == null) return "F";
        string bound = ZInput.instance.GetBoundKeyString("GP", true);
        if (string.IsNullOrEmpty(bound)) return "";
        string localized = Localization.instance != null ? Localization.instance.Localize(bound) : bound;
        return localized.Length <= 4 ? localized.ToUpper(CultureInfo.InvariantCulture) : localized;
    }
}
