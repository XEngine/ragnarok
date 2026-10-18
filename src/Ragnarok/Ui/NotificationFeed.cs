using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// Ragnarök notifications: Valheim's unlock messages (new recipe, piece, station, material, trophy,
// item) become cards, its small top-left messages (pickups, toggles) become slim rows, stacked where
// [HUD.Notifications] places them. Big centre messages stay Valheim's. Both still go to the message log.
internal sealed class NotificationFeed : IDisposable
{
    private const float CardWidth = 320, CardHeight = 58, RowHeight = 28, Spacing = 6;
    private const float CardLife = 5.5f, RowLife = 3.5f, FadeIn = 0.2f, FadeOut = 0.45f;
    private const int MaxShown = 7;

    internal static NotificationFeed? Instance;

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<Entry> entries = new();
    private GameObject? hudRoot;
    private RectTransform? root;

    internal NotificationFeed(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        Instance = this;
    }

    // Valheim's messages are taken over only while this is true (and a HUD exists to draw them in).
    internal bool Active => Wanted && global::Hud.instance;

    private bool Wanted => settings.NotificationStyle.Value switch
    {
        PanelStyle.Ragnarok => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    internal void Tick()
    {
        GameObject? parent = global::Hud.instance ? global::Hud.instance.m_rootObject : null;
        if (parent != hudRoot)
        {
            root = null;
            entries.Clear();
            hudRoot = parent;
        }
        if (!hudRoot || !Wanted)
        {
            Dispose();
            return;
        }
        if (!root)
        {
            root = Ugui.Node(hudRoot!.transform, "Ragnarok Notifications");
            root.sizeDelta = new Vector2(CardWidth, 400);
        }
        settings.Notifications.Apply(root!);
        root!.localScale = Vector3.one * settings.HudScale.Value;

        // Newest at the top; each entry eases towards its slot and fades by age.
        float now = Time.unscaledTime, y = 0;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry e = entries[i];
            float age = now - e.Born, life = e.Card ? CardLife : RowLife;
            if (age > life + FadeOut || entries.Count - i > MaxShown)
            {
                e.Destroy();
                entries.RemoveAt(i);
                continue;
            }
            float alpha = Mathf.Clamp01(age / FadeIn) * (1f - Mathf.Clamp01((age - life) / FadeOut));
            float slide = (1f - Mathf.Clamp01(age / FadeIn)) * -24f;
            e.Place(slide, y, alpha, settings.Notifications);
            y += e.Height + Spacing;
        }
    }

    public void Dispose()
    {
        foreach (Entry e in entries) e.Destroy();
        entries.Clear();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
    }

    internal void Unlock(Sprite? icon, string topic, string name)
    {
        if (!root) return;
        entries.Add(Entry.MakeCard(theme, root!, icon, topic, name));
    }

    // A repeat of a recent row (same text and icon) adds to its amount instead of stacking up.
    internal void Message(Sprite? icon, string text, int amount)
    {
        if (!root) return;
        foreach (Entry e in entries)
            if (!e.Card && e.Text == text && e.Icon == icon && Time.unscaledTime - e.Born < RowLife)
            {
                e.Bump(amount);
                return;
            }
        entries.Add(Entry.MakeRow(theme, root!, icon, text, amount));
    }

    // The settings window's test button: through Valheim's own calls, so it shows whichever style is on.
    internal static void Test()
    {
        if (!MessageHud.instance)
        {
            Debug.Log("[Ragnarok] Test notification: no game HUD yet (load a world first).");
            return;
        }
        ItemDrop.ItemData? sword = ObjectDB.instance?.GetItemPrefab("SwordIron")?.GetComponent<ItemDrop>()?.m_itemData;
        ItemDrop.ItemData? wood = ObjectDB.instance?.GetItemPrefab("Wood")?.GetComponent<ItemDrop>()?.m_itemData;
        MessageHud.instance.QueueUnlockMsg(sword?.GetIcon(), "$msg_newrecipe", sword?.m_shared.m_name ?? "Iron sword");
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, wood?.m_shared.m_name ?? "Wood", 3, wood?.GetIcon());
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "$msg_walk $hud_on");
    }

    private sealed class Entry
    {
        internal readonly bool Card;
        internal readonly string Text;
        internal readonly Sprite? Icon;
        internal float Born;
        internal readonly float Height;
        private readonly RectTransform rect;
        private readonly CanvasGroup group;
        private readonly HudBackdrop backdrop;
        private readonly TextMeshProUGUI? amountText;
        private int amount;

        private Entry(bool card, string text, Sprite? icon, RectTransform rect, HudBackdrop backdrop, float height,
            TextMeshProUGUI? amountText, int amount)
        {
            Card = card;
            Text = text;
            Icon = icon;
            Born = Time.unscaledTime;
            Height = height;
            this.rect = rect;
            this.backdrop = backdrop;
            this.amountText = amountText;
            this.amount = amount;
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
        }

        internal static Entry MakeCard(Theme theme, RectTransform parent, Sprite? icon, string topic, string name)
        {
            RectTransform rect = Ugui.Node(parent, "Unlock").At(0, 0, CardWidth, CardHeight);
            HudBackdrop backdrop = new(theme, rect, new Vector4(14, 6, 14, 6));
            Ugui.Image(rect, "Accent", theme.White, Palette.GoldBright).At(0, 8, 3, CardHeight - 16);
            Ugui.Image(rect, "Rim", theme.Diamond, Palette.Gold).At(12, 7, 44, 44);
            Ugui.Image(rect, "Core", theme.Diamond, Palette.Obsidian).At(14, 9, 40, 40);
            Image picture = Ugui.Image(rect, "Icon", icon, Color.white).At(22, 17, 24, 24);
            Ugui.ItemIcon(picture);
            picture.enabled = icon;
                        // The topic as an italic section label over the name in Alegreya, as in the guide.
            Ugui.Text(rect, "Topic", topic.ToLowerInvariant(), theme.Section, 14, Palette.Muted).At(66, 4, CardWidth - 76, 21);
            Ugui.Text(rect, "Name", name, theme.Heading, 18, Palette.Bone).At(66, 22, CardWidth - 76, 27);
            return new Entry(true, name, icon, rect, backdrop, CardHeight, null, 0);
        }

        internal static Entry MakeRow(Theme theme, RectTransform parent, Sprite? icon, string text, int amount)
        {
            RectTransform rect = Ugui.Node(parent, "Message").At(0, 0, CardWidth, RowHeight);
            HudBackdrop backdrop = new(theme, rect, new Vector4(10, 2, 10, 2));
            Ugui.Image(rect, "Gem", theme.Diamond, Palette.Gold).At(4, RowHeight / 2 - 3, 6, 6);
            float x = 16;
            if (icon)
            {
                Image picture = Ugui.Image(rect, "Icon", icon, Color.white).At(x, 3, RowHeight - 6, RowHeight - 6);
                Ugui.ItemIcon(picture);
                x += RowHeight;
            }
            TextMeshProUGUI label = Ugui.Text(rect, "Text", text, theme.Strong, 13, Palette.Bone).At(x, 0, CardWidth - x - 60, RowHeight);
            TextMeshProUGUI count = Ugui.Text(rect, "Amount", "", theme.Strong, 13, Palette.Gold, TextAlignmentOptions.MidlineRight)
                .At(CardWidth - 64, 0, 56, RowHeight);
            Entry entry = new(false, text, icon, rect, backdrop, RowHeight, count, amount);
            entry.ShowAmount();
            return entry;
        }

        internal void Bump(int more)
        {
            amount += Mathf.Max(1, more);
            Born = Time.unscaledTime - FadeIn; // Stay fully shown, restart the clock.
            ShowAmount();
        }

        private void ShowAmount()
        {
            if (amountText != null) amountText.text = amount > 0 ? "×" + amount : "";
        }

        internal void Place(float x, float y, float alpha, HudPlacement placement)
        {
            Vector2 target = new(x, -y);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 0.35f);
            group.alpha = alpha;
            backdrop.Apply(placement);
        }

        internal void Destroy()
        {
            if (rect) UnityEngine.Object.Destroy(rect.gameObject);
        }
    }
}

// Unlock messages become Ragnarök cards while the feed is active; the log still records them.
[HarmonyPatch(typeof(MessageHud), nameof(MessageHud.QueueUnlockMsg))]
internal static class UnlockMessagePatch
{
    private static readonly MethodInfo? AddLog = AccessTools.Method(typeof(MessageHud), "AddLog");

    private static bool Prefix(MessageHud __instance, Sprite icon, string topic, string description)
    {
        NotificationFeed? feed = NotificationFeed.Instance;
        if (feed == null || !feed.Active) return true;
        Localization loc = Localization.instance;
        feed.Unlock(icon, loc.Localize(topic), loc.Localize(description));
        AddLog?.Invoke(__instance, new object[] { topic + ": " + description });
        return false;
    }
}

// Top-left messages become Ragnarök rows; centre messages are left to Valheim.
[HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
internal static class TopLeftMessagePatch
{
    private static readonly MethodInfo? AddLog = AccessTools.Method(typeof(MessageHud), "AddLog");

    private static bool Prefix(MessageHud __instance, MessageHud.MessageType type, string text, int amount, Sprite icon,
        bool showDespiteHiddenHUD, bool log)
    {
        NotificationFeed? feed = NotificationFeed.Instance;
        if (type != MessageHud.MessageType.TopLeft || feed == null || !feed.Active) return true;
        if (global::Hud.IsUserHidden() && !showDespiteHiddenHUD) return false;
        string localized = Localization.instance.Localize(text);
        feed.Message(icon, localized, amount);
        if (log) AddLog?.Invoke(__instance, new object[] { localized });
        return false;
    }
}
