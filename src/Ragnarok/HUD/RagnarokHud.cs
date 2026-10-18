using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

internal struct HudFood
{
    internal Sprite? Icon;
    internal float Remaining; // 0..1 of burn time left
    internal float Seconds;   // Real seconds left, as Valheim counts them.
    internal bool CanEat;
    internal float Health, Stamina, Eitr; // What the food gives at full strength.
}

internal struct HudSnapshot
{
    internal float Health, MaxHealth, Stamina, MaxStamina, Eitr, MaxEitr, Adrenaline, MaxAdrenaline;
    internal List<HudFood> Foods;
    internal List<HudStatus> Statuses;
    internal int Comfort;
    internal HudPower Power;

    private static readonly List<StatusEffect> effects = new();

    internal static HudSnapshot? FromLocalPlayer(List<HudFood> buffer, List<HudStatus> statuses)
    {
        Player player = Player.m_localPlayer;
        if (!player || player.IsDead()) return null;
        statuses.Clear();
        effects.Clear();
        player.GetSEMan().GetHUDStatusEffects(effects);
        foreach (StatusEffect effect in effects)
        {
            if (effect.m_hidden) continue;
            int key = effect.NameHash();
            bool comfort = Array.IndexOf(HudStatus.ComfortSources, key) >= 0;
            statuses.Add(new HudStatus
            {
                Key = key, Name = effect.m_name, Value = comfort ? "" : effect.GetIconText() ?? "",
                Icon = effect.m_icon, Flash = effect.m_flashIcon, Comfort = comfort
            });
        }
        buffer.Clear();
        foreach (Player.Food food in player.GetFoods())
        {
            float burn = Mathf.Max(1f, food.m_item.m_shared.m_foodBurnTime);
            ItemDrop.ItemData.SharedData shared = food.m_item.m_shared;
            buffer.Add(new HudFood
            {
                Icon = food.m_item.GetIcon(), Remaining = Mathf.Clamp01(food.m_time / burn), CanEat = food.CanEatAgain(),
                Seconds = food.m_time / Game.m_foodRate,
                Health = shared.m_food, Stamina = shared.m_foodStamina, Eitr = shared.m_foodEitr
            });
        }
        return new HudSnapshot
        {
            Health = player.GetHealth(), MaxHealth = player.GetMaxHealth(),
            Stamina = player.GetStamina(), MaxStamina = player.GetMaxStamina(),
            Eitr = player.GetEitr(), MaxEitr = player.GetMaxEitr(),
            Adrenaline = player.GetAdrenaline(), MaxAdrenaline = player.GetMaxAdrenaline(),
            Foods = buffer, Statuses = statuses, Comfort = player.GetComfortLevel(),
            Power = HudPower.FromPlayer(player)
        };
    }

    // Sample values for previews outside a world.
    internal static HudSnapshot Demo(float time, List<HudFood> buffer, List<HudStatus> statuses)
    {
        statuses.Clear();
        statuses.Add(new HudStatus { Key = SEMan.s_statusEffectRested, Name = "$se_rested", Value = StatusEffect.GetTimeString(743 - time) });
        statuses.Add(new HudStatus { Key = SEMan.s_statusEffectResting, Name = "", Value = "", Comfort = true });
        statuses.Add(new HudStatus { Key = SEMan.s_statusEffectCampFire, Name = "Warm", Value = "", Comfort = true });
        statuses.Add(new HudStatus { Key = SEMan.s_statusEffectShelter, Name = "Sheltered", Value = "", Comfort = true });
        buffer.Clear();
        buffer.Add(new HudFood { Remaining = 0.82f, Seconds = 1460, Health = 35, Stamina = 12 });
        buffer.Add(new HudFood { Remaining = 0.55f, Seconds = 980, Health = 13, Stamina = 40 });
        buffer.Add(new HudFood { Remaining = 0.28f, Seconds = 42, CanEat = true, Health = 15, Stamina = 15, Eitr = 60 });
        return new HudSnapshot
        {
            Health = 96 + Mathf.Sin(time * 0.7f) * 30, MaxHealth = 160,
            Stamina = 88 + Mathf.Sin(time * 1.3f) * 30, MaxStamina = 130,
            Eitr = 42, MaxEitr = 80,
            Foods = buffer, Statuses = statuses, Comfort = 7,
            Power = HudPower.Demo()
        };
    }
}

// Rune & Iron: identity and food at top left, vitals above the action bar,
// and an independent Forsaken seal at bottom left. All follow the game's HUD root.
internal sealed class RagnarokHud : IDisposable
{
    private const float VitalsWidth = 513, FoodStep = 60;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<HudFood> foods = new();
    private readonly List<HudStatus> statuses = new();
    private readonly MonochromeIcons icons = new();
    private GameObject? root, previewCanvas;
    private StatusStrip? strip;
    private CharacterHud? character;
    private RectTransform? powerRoot, foodRoot;
    private KeyHintsPanel? keys;
    private Bar[] bars = Array.Empty<Bar>();
    private FoodSlot[] slots = Array.Empty<FoodSlot>();
    private PowerEmblem? emblem;
    private ProvisionBar? provisions;
    private HudBackdrop? vitalsBackdrop;
    private RectTransform? knot;
    private TextMeshProUGUI? weaponName;
    private Image? criticalVignette;
    private readonly List<TextOutline> vitalsTextOutlines = new();

    internal bool Preview { get; set; }

    internal RagnarokHud(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        StaminaFeedbackPatch.Exhausted = () =>
        {
            if (Active && bars.Length > 1) bars[1].MarkExhausted();
        };
    }

    internal bool Active => settings.Hud.Value == HudStyle.Ragnarok;

    // A panel the key guide moves under while it is open (the Ragnarök crafting panel), else null.
    internal Func<RectTransform?> KeysDock = () => null;

    internal void Tick()
    {
        Transform? parent = Preview ? PreviewParent() : Active && global::Hud.instance ? global::Hud.instance.m_rootObject.transform : null;
        if (!parent)
        {
            DestroyRoot();
            return;
        }
        // Rebuild when the HUD owner or power visibility changes.
        if (!root || root!.transform.parent != parent || (emblem != null) != settings.HudPower.Value) Build(parent!);

        HudSnapshot? snapshot = Preview ? HudSnapshot.Demo(Time.unscaledTime, foods, statuses) : HudSnapshot.FromLocalPlayer(foods, statuses);
        root!.SetActive(snapshot.HasValue);
        character!.SetVisible(snapshot.HasValue);
        powerRoot!.gameObject.SetActive(snapshot.HasValue && settings.HudPower.Value);
        strip!.SetVisible(snapshot.HasValue && settings.HudStatus.Value);
        keys!.SetVisible(snapshot.HasValue && settings.HudKeys.Value);
        criticalVignette!.gameObject.SetActive(snapshot.HasValue);
        if (snapshot is not { } data) return;
        if (settings.HudStatus.Value)
        {
            strip.SetScale(settings.HudScale.Value);
            strip.Place(settings.Status);
            strip.Set(data.Statuses, data.Comfort, settings.StatusLabels.Value, Equipment.ProvisionKeys.Label(settings.StatusLabelsKey.Value));
        }
        if (settings.HudKeys.Value)
        {
            if (!keys.Dock(Preview ? null : KeysDock(), settings.HudStatus.Value ? strip.Rect : null))
            {
                keys.SetScale(settings.HudScale.Value);
                keys.Place(settings.Keys);
            }
            keys.Tick(Preview);
        }
        root.transform.localScale = Vector3.one * settings.HudScale.Value;
        character.Tick(Preview, data.Statuses.Count);
        powerRoot.localScale = Vector3.one * settings.HudScale.Value;
        settings.Power.Apply(powerRoot);
        bars[0].Set(data.Health, data.MaxHealth);
        bars[1].Set(data.Stamina, data.MaxStamina);
        bars[2].Root.gameObject.SetActive(data.MaxEitr > 0);
        if (data.MaxEitr > 0) bars[2].Set(data.Eitr, data.MaxEitr);
        foreach (Bar bar in bars) bar.ShowNumbers(settings.HudNumbers.Value);

        // The emblem beats while health is low.
        float health = data.MaxHealth > 0 ? data.Health / data.MaxHealth : 1f;
        float beat = health < 0.25f ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 6.5f)), 6f) : 0f;
        criticalVignette.color = Palette.Health.WithAlpha(health < 0.25f ? 0.12f + beat * 0.16f : 0);
        knot!.localScale = Vector3.one * (1f + beat * 0.08f);

        bool adrenaline = data.Adrenaline > 0;
        bars[3].Root.gameObject.SetActive(adrenaline);
        bars[3].Root.anchoredPosition = new Vector2(0, -(data.MaxEitr > 0 ? 82 : 64));
        if (adrenaline) bars[3].Set(data.Adrenaline, Mathf.Max(data.MaxAdrenaline, data.Adrenaline));
        ItemDrop.ItemData? weapon = Preview ? null : Player.m_localPlayer.GetCurrentWeapon();
        weaponName!.text = Preview ? "Bronze axe" : weapon != null ? Localization.instance.Localize(weapon.m_shared.m_name) : "";
        foreach (TextOutline textOutline in vitalsTextOutlines) textOutline.Sync();
        provisions!.Set(Preview ? null : Player.m_localPlayer, settings.ProvisionBar.Value);
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(VitalsWidth, adrenaline && data.MaxEitr > 0 ? 94 : 76);
        settings.Vitals.Apply(rootRect);
        vitalsBackdrop?.Apply(settings.Vitals);

        emblem?.Set(data.Power);

        for (int i = 0; i < slots.Length; i++)
            slots[i].Set(settings.HudFood.Value, i < data.Foods.Count ? data.Foods[i] : null);
    }

    public void Dispose()
    {
        DestroyRoot();
        StaminaFeedbackPatch.Exhausted = null;
        icons.Dispose();
        if (previewCanvas) UnityEngine.Object.Destroy(previewCanvas);
    }

    private void DestroyRoot()
    {
        if (root) UnityEngine.Object.Destroy(root);
        vitalsTextOutlines.Clear();
        root = null;
        if (criticalVignette) UnityEngine.Object.Destroy(criticalVignette!.gameObject);
        criticalVignette = null;
        bars = Array.Empty<Bar>();
        emblem = null;
        if (powerRoot) UnityEngine.Object.Destroy(powerRoot!.gameObject);
        powerRoot = null;
        character?.Dispose();
        character = null;
        strip?.Dispose();
        strip = null;
        keys?.Dispose();
        keys = null;
    }

    private Transform PreviewParent()
    {
        if (previewCanvas) return previewCanvas!.transform;
        previewCanvas = new GameObject("Ragnarok HUD Preview", typeof(RectTransform)) { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(previewCanvas);
        Canvas canvas = previewCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        CanvasScaler scaler = previewCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        return previewCanvas.transform;
    }

    private void Build(Transform parent)
    {
        DestroyRoot();
        criticalVignette = Ugui.Image(parent, "Critical health vignette", theme.HudVignette, Color.clear).Stretch();
        criticalVignette.transform.SetAsFirstSibling();
        RectTransform rect = Ugui.Node(parent, "Ragnarok HUD");
        rect.sizeDelta = new Vector2(420, 200);
        settings.Vitals.Apply(rect);
        root = rect.gameObject;
        vitalsBackdrop = new HudBackdrop(theme, rect, new Vector4(20, 8, 20, 8));
        strip = new StatusStrip(theme, icons, parent);
        keys = new KeyHintsPanel(theme, icons, parent);

        character = new CharacterHud(settings, theme, parent);
        foodRoot = character.FoodRoot;
        powerRoot = Ugui.Node(parent, "Ragnarok Power");
        powerRoot.sizeDelta = new Vector2(PowerEmblem.Width, PowerEmblem.Size);
        emblem = settings.HudPower.Value ? new PowerEmblem(theme, icons, powerRoot, new Rect(0, 0, PowerEmblem.Width, PowerEmblem.Size)) : null;

        Ugui.Text(foodRoot, "Food label", "FOOD", theme.Strong, 11, Palette.Bone, spacing: 3).At(0, 0, 200, 18);
        slots = new FoodSlot[3];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new FoodSlot(theme, foodRoot, new Rect(i * FoodStep, 24, 46, 64));
        provisions = new ProvisionBar(theme, foodRoot, 0, 96);
        weaponName = Ugui.Text(rect, "Equipped weapon", "", theme.Heading, 16.5f, Palette.Bone, TextAlignmentOptions.Center)
            .At(0, -2, VitalsWidth, 28);
        weaponName.overflowMode = TextOverflowModes.Overflow;
        OutlineVitalsText(weaponName);
        bars = new[]
        {
            new Bar(theme, rect, "Health", new Rect(0, 48, 232, 9), Palette.Health, 0, OutlineVitalsText),
            new Bar(theme, rect, "Stamina", new Rect(281, 48, 232, 9), Palette.Stamina, 1, OutlineVitalsText),
            new Bar(theme, rect, "Eitr", new Rect(0, 66, VitalsWidth, 6), Palette.Hex(0x9A7BEA), 2, OutlineVitalsText),
            new Bar(theme, rect, "Adrenaline", new Rect(0, 84, VitalsWidth, 6), Palette.Ember, 2, OutlineVitalsText)
        };
        knot = Ugui.Node(rect, "Vitals diamond").At(VitalsWidth / 2, 42, 0, 0);
        Ugui.Image(knot, "Rim", theme.Diamond, Palette.Gold).At(-16, -16, 32, 32);
        Ugui.Image(knot, "Core", theme.Diamond, Palette.Ink).At(-14, -14, 28, 28);
        Ugui.Image(knot, "Gem", theme.Diamond, Palette.GoldBright).At(-5, -5, 10, 10);
    }

    private void OutlineVitalsText(TextMeshProUGUI label)
    {
        vitalsTextOutlines.Add(new TextOutline(label));
    }

    private sealed class Bar
    {
        internal readonly RectTransform Root;
        private readonly Image lag, fill, glow, outline, hatch, well;
        private readonly Color depletedColor;
        private readonly CanvasGroup visibility;
        private readonly TextMeshProUGUI value, caption;
        private readonly string name;
        private readonly int kind;
        private float lagValue, lagHold, previous = -1, previousMax, lastChange;
        private bool exhausted;

        // Health and stamina share one baseline; the thin resources have inline labels.
        internal Bar(Theme theme, Transform parent, string name, Rect area, Color color, int kind,
            Action<TextMeshProUGUI> outlineText)
        {
            this.name = name;
            this.kind = kind;
            Root = Ugui.Node(parent, name).At(area);
            float inset = kind == 2 ? name == "Eitr" ? 34 : 82 : 0;
            float width = kind == 2 ? area.width - inset - 70 : area.width;
            Sprite sprite = theme.HudBar;
            Ugui.Image(Root, "Dark bar border", sprite, Palette.Hex(0x29302F, 0.65f))
                .At(inset - 0.75f, -0.75f, width + 1.5f, area.height + 1.5f);
            RectTransform track = Ugui.Node(Root, "Track").At(inset, 0, width, area.height);
            // Idle fading belongs to the resource graphics, keeping labels and borders readable.
            visibility = track.gameObject.AddComponent<CanvasGroup>();
            visibility.blocksRaycasts = false;
            glow = Ugui.Image(track, "Critical glow", theme.HudGlow, Color.clear).Stretch(-10, -12, -10, -12);
            outline = Ugui.Image(track, "Critical outline", theme.HudBar, Color.clear).Stretch(-1, -1, -1, -1);
            depletedColor = Color.Lerp(color, Palette.Hex(0x41483F), 0.85f).WithAlpha(0.6f);
            well = Ugui.Image(track, "Well", sprite, depletedColor).Stretch();
            lag = Ugui.Image(track, "Recent loss", sprite, Color.Lerp(color, Palette.Hex(0x7C8071), 0.55f).WithAlpha(0.75f)).Stretch();
            fill = Ugui.Image(track, "Fill", sprite, color).Stretch();
            hatch = Ugui.Image(track, "Exhausted track", theme.HudHatch, Palette.Stamina).Stretch();
            hatch.enabled = false;
            caption = Ugui.Text(Root, "Caption", name.ToUpperInvariant(), theme.Strong, 10, Palette.Bone,
                kind == 0 ? TextAlignmentOptions.BaselineRight : kind == 1 ? TextAlignmentOptions.BaselineLeft : TextAlignmentOptions.MidlineLeft);
            value = Ugui.Text(Root, "Value", "", theme.Strong, 18, Palette.Bone,
                kind == 0 ? TextAlignmentOptions.BaselineRight : kind == 1 ? TextAlignmentOptions.BaselineLeft : TextAlignmentOptions.MidlineLeft);
            if (kind == 0)
            {
                caption.At(0, -4, area.width - 80, 0);
                value.At(area.width - 76, -4, 76, 0);
            }
            else if (kind == 1)
            {
                value.At(0, -4, 76, 0);
                caption.At(80, -4, area.width - 80, 0);
            }
            else
            {
                caption.At(0, area.height / 2 - 10, inset - 2, 20);
                const float valueWidth = 66;
                value.At(area.width - valueWidth, area.height / 2 - 10, valueWidth, 20);
                value.fontSize = 13;
            }
            value.overflowMode = caption.overflowMode = TextOverflowModes.Overflow;
            outlineText(caption);
            outlineText(value);
        }

        internal void Set(float current, float maximum)
        {
            float pct = maximum > 0 ? Mathf.Clamp01(current / maximum) : 0;
            float now = Time.unscaledTime;
            bool initialized = previous >= 0;
            bool changed = !initialized || Mathf.Abs(current - previous) > 0.001f || maximum != previousMax;
            if (changed) lastChange = now;
            if (!initialized || maximum != previousMax) lagValue = pct;
            else if (current < previous - 0.001f) lagHold = 0.6f;
            if (pct >= lagValue) lagValue = pct;
            else if ((lagHold -= Time.unscaledDeltaTime) <= 0)
                lagValue = Mathf.MoveTowards(lagValue, pct, Time.unscaledDeltaTime * 0.5f);
            if (kind == 1)
            {
                if (current <= 0.01f && maximum > 0) exhausted = true;
                else if (initialized && current > previous + 0.001f) exhausted = false;
            }
            previous = current;
            previousMax = maximum;
            bool critical = kind == 0 && pct < 0.25f && maximum > 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6.5f);
            glow.color = Palette.Health.WithAlpha(critical ? 0.2f + pulse * 0.2f : 0);
            outline.color = Palette.Health.WithAlpha(critical ? 0.28f + pulse * 0.22f : 0);
            // Keep some extra coverage behind critical feedback without a black empty track.
            well.color = critical ? depletedColor.WithAlpha(0.8f) : depletedColor;
            hatch.enabled = kind == 1 && exhausted;
            visibility.alpha = Mathf.MoveTowards(visibility.alpha,
                pct >= 0.999f && now - lastChange > 4 && !critical && !exhausted ? 0.4f : 1f, Time.unscaledDeltaTime * 3);
            fill.rectTransform.anchorMax = new Vector2(pct, 1);
            lag.rectTransform.anchorMax = new Vector2(lagValue, 1);
            fill.enabled = pct > 0;
            lag.enabled = lagValue > 0;
            string numbers = Mathf.CeilToInt(current) + (kind == 2 ? " <size=85%>/ " : " <size=75%>/ ") + Mathf.CeilToInt(maximum) + "</size>";
            if (value.text != numbers)
            {
                value.text = numbers;
                if (kind < 2)
                {
                    // Keep the label four pixels from the actual digits as their count changes.
                    float numberWidth = Mathf.Ceil(value.GetPreferredValues(numbers).x);
                    float remaining = Mathf.Max(0, Root.sizeDelta.x - numberWidth - 4);
                    value.At(kind == 0 ? Root.sizeDelta.x - numberWidth : 0, -4, numberWidth, 0);
                    caption.At(kind == 0 ? 0 : numberWidth + 4, -4, remaining, 0);
                }
            }
            value.color = name == "Health" && pct < 0.25f ? Palette.Alert : Palette.Bone;
        }

        internal void MarkExhausted()
        {
            exhausted = true;
            lastChange = Time.unscaledTime;
        }

        internal void ShowNumbers(bool show)
        {
            value.enabled = show;
            // Keep the resource label next to its track when values are hidden.
            if (kind < 2) caption.color = Palette.Bone;
        }
    }

    // Food is a glyph inside a circular remaining-duration meter, with its timer below.
    private sealed class FoodSlot
    {
        private readonly GameObject root;
        private readonly CanvasGroup opacity;
        private readonly Theme theme;
        private readonly Image ring, progress, icon;
        private readonly TextMeshProUGUI countdown;

        internal FoodSlot(Theme theme, Transform parent, Rect r)
        {
            this.theme = theme;
            RectTransform rect = Ugui.Node(parent, "Food").At(r);
            root = rect.gameObject;
            opacity = root.AddComponent<CanvasGroup>();
            opacity.blocksRaycasts = false;
            Ugui.Image(rect, "Well", theme.Disc, Palette.Hex(0x101B1E)).At(2, 2, 42, 42);
            ring = Ugui.Image(rect, "Duration Track", theme.HudRing, Palette.Hex(0x514C3D)).At(0, 0, 46, 46);
            progress = Ugui.Image(rect, "Remaining", theme.HudRing, Palette.Gold).At(0, 0, 46, 46);
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Radial360;
            progress.fillOrigin = (int)Image.Origin360.Top;
            progress.fillClockwise = true;
            icon = Ugui.Image(rect, "Food Kind", theme.IconFood, Palette.Tan).At(12, 12, 22, 22);
            countdown = Ugui.Text(rect, "Countdown", "", theme.Strong, 11, Palette.Bone, TextAlignmentOptions.Center).At(-3, 49, 52, 18);
            countdown.overflowMode = TextOverflowModes.Overflow;
        }

        internal void Set(bool visible, HudFood? food)
        {
            root.SetActive(visible);
            if (!visible) return;
            bool has = food.HasValue;
            ring.color = Palette.Hex(0x514C3D);
            opacity.alpha = has ? 1f : 0.3f;
            progress.enabled = icon.enabled = has;
            countdown.text = "\u2014";
            if (food is not { } f) return;
            progress.fillAmount = Mathf.Clamp01(f.Remaining);
            bool magic = f.Eitr > 0;
            bool stamina = !magic && f.Stamina > f.Health;
            icon.sprite = magic ? theme.HudBottle : stamina ? theme.HudBowl : theme.IconFood;
            icon.color = magic ? Palette.Hex(0xB672A2) : stamina ? Palette.Hex(0xE8A052) : Palette.Hex(0xE1AB82);
            int seconds = Mathf.CeilToInt(f.Seconds);
            countdown.text = seconds >= 60 ? Mathf.CeilToInt(seconds / 60f) + "m" : Mathf.Max(0, seconds) + "s";
            // Keep the number legible when food can be refreshed; pulse the ring instead.
            progress.color = Palette.Gold.WithAlpha(f.CanEat ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5) : 1);
        }
    }

}

// Hides the vanilla bars that the Ragnarök HUD replaces. Runs after the animator has applied
// its state for the frame, and restores the original scale when switching back.
[HarmonyPatch(typeof(global::Hud), "LateUpdate")]
internal static class VanillaHudPatch
{
    internal static Func<bool> HideVanilla = () => false;
    internal static Func<bool> HideStatus = () => false;
    internal static Func<bool> HidePower = () => false;
    private static readonly Dictionary<RectTransform, Vector3> hidden = new();

    private static void Postfix(global::Hud __instance)
    {
        if (HideVanilla())
        {
            Hide(__instance.m_healthPanel);
            Hide(__instance.m_staminaBar2Root);
            Hide(__instance.m_eitrBarRoot);
            Hide(__instance.m_adrenalineBarRoot);
            if (HideStatus()) Hide(__instance.m_statusEffectListRoot);
            else Restore(__instance.m_statusEffectListRoot);
            if (HidePower()) Hide(__instance.m_gpRoot);
            else Restore(__instance.m_gpRoot);
            if (HideKeys()) HideKeyHints();
            else RestoreKeyHints();
        }
        else Restore();
    }

    internal static Func<bool> HideKeys = () => false;
    private static CanvasGroup? keyHintsFade;
    private static bool addedFade;
    private static float keyHintsAlpha;

    // The key guide is hidden by transparency, not scale: the Ragnarök panel reads the vanilla
    // hints' on-screen positions to keep their order, and a zero scale would collapse them.
    private static void HideKeyHints()
    {
        KeyHints hints = KeyHints.instance;
        if (!hints) return;
        if (!keyHintsFade)
        {
            addedFade = !hints.TryGetComponent(out CanvasGroup existing);
            keyHintsFade = addedFade ? hints.gameObject.AddComponent<CanvasGroup>() : existing;
            keyHintsAlpha = keyHintsFade!.alpha;
        }
        keyHintsFade!.alpha = 0;
    }

    private static void RestoreKeyHints()
    {
        if (keyHintsFade)
        {
            if (addedFade) UnityEngine.Object.Destroy(keyHintsFade);
            else keyHintsFade!.alpha = keyHintsAlpha;
        }
        keyHintsFade = null;
    }

    private static void Hide(RectTransform root)
    {
        if (!root) return;
        if (!hidden.ContainsKey(root)) hidden[root] = root.localScale;
        root.localScale = Vector3.zero;
    }

    private static void Restore(RectTransform root)
    {
        if (!root || !hidden.TryGetValue(root, out Vector3 scale)) return;
        root.localScale = scale;
        hidden.Remove(root);
    }

    internal static void Restore()
    {
        foreach (KeyValuePair<RectTransform, Vector3> pair in hidden)
            if (pair.Key) pair.Key.localScale = pair.Value;
        hidden.Clear();
        RestoreKeyHints();
    }
}


// Observe the game's failed stamina action without changing resource or action behavior.
[HarmonyPatch(typeof(global::Hud), nameof(global::Hud.StaminaBarEmptyFlash))]
internal static class StaminaFeedbackPatch
{
    internal static Action? Exhausted;
    private static void Postfix() => Exhausted?.Invoke();
}
