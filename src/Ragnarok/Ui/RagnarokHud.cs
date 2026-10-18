using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

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
    internal float Health, MaxHealth, Stamina, MaxStamina, Eitr, MaxEitr;
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

// Ragnarök HUD after the style guide, stacked from the top: the Forsaken power plate, the food row (a
// meal badge, the eaten foods with their time left, then the provisions), the health bar riding a gold
// baseline from its red-knot emblem, and slim stamina and eitr bars beneath. Bars grow with their maximum
// and keep a lagging "chip" segment that shows recent loss.
// In a world it is parented to Valheim's HUD root, so it follows the game's UI scale and Ctrl+F3.
internal sealed class RagnarokHud : IDisposable
{
    private const float PowerRow = PowerEmblem.KeyOverhang + PowerEmblem.PlateHeight + 14, FoodRow = 46;
    // Axis: the plate, meal badge and emblem are centred on one vertical line, joined by a gold braid.
    private const float Axis = PowerEmblem.PlateWidth / 2, BraidWidth = 12;
    private const float EmblemSize = 46, BarX = 30, HealthHeight = 22, SlimHeight = 8, FoodX = 50, FoodStep = 76;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<HudFood> foods = new();
    private readonly List<HudStatus> statuses = new();
    private readonly MonochromeIcons icons = new();
    private GameObject? root, previewCanvas;
    private StatusStrip? strip;
    private KeyHintsPanel? keys;
    private Bar[] bars = Array.Empty<Bar>();
    private FoodSlot[] slots = Array.Empty<FoodSlot>();
    private PowerEmblem? emblem;
    private ProvisionBar? provisions;
    private HudBackdrop? vitalsBackdrop;
    private RectTransform? knot;
    private float healthY;

    internal bool Preview;

    internal RagnarokHud(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
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
        // Turning the power plate on or off moves everything under it, so rebuild the cluster.
        if (!root || root!.transform.parent != parent || (emblem != null) != settings.HudPower.Value) Build(parent!);

        HudSnapshot? snapshot = Preview ? HudSnapshot.Demo(Time.unscaledTime, foods, statuses) : HudSnapshot.FromLocalPlayer(foods, statuses);
        root!.SetActive(snapshot.HasValue);
        strip!.SetVisible(snapshot.HasValue && settings.HudStatus.Value);
        keys!.SetVisible(snapshot.HasValue && settings.HudKeys.Value);
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
        bars[0].Set(data.Health, data.MaxHealth, 2.0f);
        bars[1].Set(data.Stamina, data.MaxStamina, 1.6f);
        bars[2].Root.gameObject.SetActive(data.MaxEitr > 0);
        if (data.MaxEitr > 0) bars[2].Set(data.Eitr, data.MaxEitr, 1.6f);
        foreach (Bar bar in bars) bar.ShowNumbers(settings.HudNumbers.Value);

        // The emblem beats while health is low.
        float health = data.MaxHealth > 0 ? data.Health / data.MaxHealth : 1f;
        float beat = health < 0.25f ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 6.5f)), 6f) : 0f;
        knot!.localScale = Vector3.one * (1f + beat * 0.08f);

        // Size the cluster to its content so right and centre positions sit flush with their margin.
        float barWidth = Mathf.Max(bars[0].Root.sizeDelta.x, bars[1].Root.sizeDelta.x + 22, data.MaxEitr > 0 ? bars[2].Root.sizeDelta.x + 22 : 0);
        float provisionWidth = provisions!.Set(Preview ? null : Player.m_localPlayer, settings.ProvisionBar.Value);
        float width = Mathf.Max(BarX + barWidth + 64, FoodX + slots.Length * FoodStep + provisionWidth, emblem != null ? 230 : 0);
        float height = healthY + (data.MaxEitr > 0 ? 70 : 56);
        RectTransform rootRect = (RectTransform)root.transform;
        if (!Mathf.Approximately(rootRect.sizeDelta.x, width) || !Mathf.Approximately(rootRect.sizeDelta.y, height))
            rootRect.sizeDelta = new Vector2(width, height);
        settings.Vitals.Apply(rootRect);
        vitalsBackdrop?.Apply(settings.Vitals);

        emblem?.Set(data.Power);

        for (int i = 0; i < slots.Length; i++)
            slots[i].Set(settings.HudFood.Value, i < data.Foods.Count ? data.Foods[i] : null);
    }

    public void Dispose()
    {
        DestroyRoot();
        icons.Dispose();
        if (previewCanvas) UnityEngine.Object.Destroy(previewCanvas);
    }

    private void DestroyRoot()
    {
        if (root) UnityEngine.Object.Destroy(root);
        root = null;
        emblem = null;
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
        RectTransform rect = Ugui.Node(parent, "Ragnarok HUD");
        rect.sizeDelta = new Vector2(420, 200);
        settings.Vitals.Apply(rect);
        root = rect.gameObject;
        vitalsBackdrop = new HudBackdrop(theme, rect, new Vector4(20, 8, 20, 8));
        strip = new StatusStrip(theme, icons, parent);
        keys = new KeyHintsPanel(theme, icons, parent);

        float y = 0;
        emblem = settings.HudPower.Value ? new PowerEmblem(theme, icons, rect, new Rect(0, 0, 260, PowerRow)) : null;
        if (emblem != null) y += PowerRow;

        // Food row: the meal badge, then the three foods, then the provisions.
        if (emblem != null) Chain(rect, PowerEmblem.KeyOverhang + PowerEmblem.PlateHeight, y + 3);
        Ugui.Image(rect, "Meal Rim", theme.Diamond, Palette.Gold).At(Axis - 16, y + 1, 32, 32);
        Ugui.Image(rect, "Meal Core", theme.Diamond, Palette.Obsidian.WithAlpha(0.95f)).At(Axis - 14, y + 3, 28, 28);
        Ugui.Image(rect, "Meal", theme.GlyphMeal, Palette.Bone).At(Axis - 8, y + 9, 16, 16);
        slots = new FoodSlot[3];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new FoodSlot(theme, rect, new Rect(FoodX + i * FoodStep, y + 2, FoodStep - 6, 30));
        provisions = new ProvisionBar(theme, rect, FoodX + slots.Length * FoodStep, y);
        Chain(rect, y + 31, y + FoodRow + 2);
        y += FoodRow;

        healthY = y;
        float barY = y + (EmblemSize - HealthHeight) / 2;
        bars = new[]
        {
            new Bar(theme, rect, "Health", BarX, barY, HealthHeight, Palette.Health, large: true),
            new Bar(theme, rect, "Stamina", BarX + 22, barY + HealthHeight + 16, SlimHeight, Palette.Stamina, large: false),
            new Bar(theme, rect, "Eitr", BarX + 22, barY + HealthHeight + 30, SlimHeight, Palette.Eitr, large: false)
        };
        // The emblem sits over the health bar's start.
        knot = Ugui.Image(rect, "Emblem", theme.Emblem, Color.white).At(Axis - EmblemSize / 2, y, EmblemSize, EmblemSize).rectTransform;
        knot.pivot = new Vector2(0.5f, 0.5f);
        knot.anchoredPosition += new Vector2(EmblemSize / 2, -EmblemSize / 2);
    }

    // A length of gold braid down the axis, from `top` to `bottom`, tucked under what it joins.
    private void Chain(RectTransform parent, float top, float bottom)
    {
        Image braid = Ugui.Image(parent, "Braid", theme.Braid, Color.white).At(Axis - BraidWidth / 2, top, BraidWidth, bottom - top);
        braid.type = Image.Type.Tiled;
    }

    private sealed class Bar
    {
        internal readonly RectTransform Root;
        private readonly Image lag, fill, shine;
        private readonly TextMeshProUGUI text, shadow;
        private readonly bool large;
        private float lagValue, lagHold;
        private int shownValue = -1, shownMax = -1;

        // Large: the health bar, its number centred inside it and a gold baseline beneath with diamond
        // marks at the quarters and the end cap. Slim: stamina and eitr, their number to the right.
        internal Bar(Theme theme, Transform parent, string name, float x, float y, float height, Color color, bool large)
        {
            this.large = large;
            Root = Ugui.Node(parent, name).At(x, y, 200, height);
            Ugui.Image(Root, "Well", theme.White, Palette.Ink.WithAlpha(0.78f)).Stretch();
            lag = Filled(theme, "Lag", Color.Lerp(color, Color.white, 0.5f).WithAlpha(0.5f));
            fill = Filled(theme, "Fill", color);
            // A soft light along the top of the fill and a darker foot, for a little depth.
            shine = Ugui.Image(Root, "Shine", theme.White, Color.white.WithAlpha(large ? 0.16f : 0.12f));
            shine.rectTransform.anchorMin = new Vector2(0, 0.62f);
            shine.rectTransform.anchorMax = Vector2.one;
            shine.rectTransform.sizeDelta = Vector2.zero;
            RectTransform foot = Ugui.Image(Root, "Foot", theme.White, Color.black.WithAlpha(0.22f)).rectTransform;
            foot.anchorMin = Vector2.zero;
            foot.anchorMax = new Vector2(1, 0.3f);
            foot.sizeDelta = Vector2.zero;

            if (large)
            {
                // Baseline from under the emblem to past the bar's end, finished by the cap.
                RectTransform line = Ugui.Image(Root, "Baseline", theme.White, Palette.Gold.WithAlpha(0.85f)).rectTransform;
                line.anchorMin = new Vector2(0, 0);
                line.anchorMax = new Vector2(1, 0);
                line.pivot = new Vector2(0, 1);
                line.offsetMin = new Vector2(-12, -5);
                line.offsetMax = new Vector2(10, -4);
                RectTransform cap = Ugui.Image(Root, "Cap", theme.Cap, Palette.Gold).rectTransform;
                cap.anchorMin = cap.anchorMax = new Vector2(1, 0);
                cap.pivot = new Vector2(0, 0);
                cap.sizeDelta = new Vector2(14, 14);
                cap.anchoredPosition = new Vector2(4, -5);
                for (int i = 1; i < 4; i++)
                {
                    RectTransform mark = Ugui.Image(Root, "Mark", theme.Diamond, Palette.Gold).rectTransform;
                    mark.anchorMin = mark.anchorMax = new Vector2(i / 4f, 0);
                    mark.sizeDelta = new Vector2(5, 5);
                    mark.anchoredPosition = new Vector2(0, -4.5f);
                }
            }

            shadow = Number(theme, "Shadow", Color.black.WithAlpha(0.75f), new Vector2(1, -1));
            text = Number(theme, "Value", large ? Color.white : Palette.Muted, Vector2.zero);
        }

        internal void Set(float value, float max, float lengthPerPoint)
        {
            float width = Mathf.Clamp(140 + max * lengthPerPoint, 180, 560) * (large ? 1f : 0.86f);
            if (!Mathf.Approximately(Root.sizeDelta.x, width)) Root.sizeDelta = new Vector2(width, Root.sizeDelta.y);

            float pct = max > 0 ? Mathf.Clamp01(value / max) : 0;
            if (pct >= lagValue) { lagValue = pct; lagHold = 0.45f; }
            else if ((lagHold -= Time.unscaledDeltaTime) <= 0) lagValue = Mathf.MoveTowards(lagValue, pct, Time.unscaledDeltaTime * 0.5f);
            fill.fillAmount = pct;
            lag.fillAmount = lagValue;
            shine.rectTransform.anchorMax = new Vector2(pct, 1);

            int shown = Mathf.CeilToInt(value), shownTop = Mathf.CeilToInt(max);
            if (shown == shownValue && shownTop == shownMax) return;
            shownValue = shown;
            shownMax = shownTop;
            text.text = shadow.text = shown.ToString();
            // Low health turns its number red, as in the guide.
            if (large) text.color = pct < 0.25f ? Palette.Alert : Color.white;
        }

        internal void ShowNumbers(bool show)
        {
            if (text.enabled == show) return;
            text.enabled = shadow.enabled = show;
        }

        private Image Filled(Theme theme, string name, Color color)
        {
            Image image = Ugui.Image(Root, name, theme.White, color).Stretch();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            return image;
        }

        private TextMeshProUGUI Number(Theme theme, string name, Color color, Vector2 offset)
        {
            TextMeshProUGUI label = Ugui.Text(Root, name, "", theme.Strong, large ? 14 : 11, color,
                large ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
            label.overflowMode = TextOverflowModes.Overflow;
            RectTransform rect = label.rectTransform;
            if (large)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = offset;
                rect.offsetMax = offset;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = offset + new Vector2(8, 0);
                rect.sizeDelta = new Vector2(60, 16);
            }
            return label;
        }
    }

    // One eaten food, unframed as in the guide: its icon and the time left beside it. When it can be eaten
    // again the time glows ember. An empty slot is a faint dark cell, so the three slots read before eating.
    private sealed class FoodSlot
    {
        private readonly GameObject root;
        private readonly Image cell, icon;
        private readonly TextMeshProUGUI countdown, shadow;
        private int shownSeconds = -1;

        internal FoodSlot(Theme theme, Transform parent, Rect r)
        {
            RectTransform rect = Ugui.Node(parent, "Food").At(r);
            root = rect.gameObject;
            cell = Ugui.Image(rect, "Empty", theme.White, Palette.Obsidian.WithAlpha(0.45f)).Stretch();
            icon = Ugui.Image(rect, "Icon", null, Color.white).At(0, 0, r.height, r.height);
            Ugui.ItemIcon(icon);
            shadow = Ugui.Text(rect, "Shadow", "", theme.Strong, 14, Color.black.WithAlpha(0.7f)).At(r.height + 5, 1.5f, r.width - r.height - 4, r.height);
            countdown = Ugui.Text(rect, "Countdown", "", theme.Strong, 14, Palette.Bone).At(r.height + 4, 0, r.width - r.height - 4, r.height);
            countdown.overflowMode = shadow.overflowMode = TextOverflowModes.Overflow;
        }

        internal void Set(bool visible, HudFood? food)
        {
            if (root.activeSelf != visible) root.SetActive(visible);
            if (!visible) return;
            bool has = food.HasValue;
            cell.enabled = !has;
            icon.enabled = countdown.enabled = shadow.enabled = has;
            if (food is not { } f) return;
            if (icon.sprite != f.Icon) icon.sprite = f.Icon;
            icon.enabled = f.Icon;
            int seconds = Mathf.CeilToInt(f.Seconds);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                // Same format as Valheim: whole minutes, then seconds in the last minute.
                countdown.text = shadow.text = seconds >= 60 ? Mathf.CeilToInt(seconds / 60f) + "m" : Mathf.Max(0, seconds) + "s";
            }
            float blink = f.CanEat ? 0.55f + Mathf.Sin(Time.unscaledTime * 5f) * 0.45f : 1f;
            countdown.color = f.CanEat ? Palette.Ember.WithAlpha(blink) : Palette.Bone;
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
