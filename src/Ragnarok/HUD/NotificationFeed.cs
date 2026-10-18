using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Ragnarök notifications: Valheim's unlock messages (new recipe, piece, station, material, trophy,
// item) and small top-left messages (pickups, toggles) become plain text rows, stacked where
// [HUD.Notifications] places them. Skill gains use their own toast; other centre messages stay vanilla. Both still go to the message log.
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
    private RectTransform? root, skillRoot;

    internal NotificationFeed(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        Instance = this;
    }

    // Valheim's messages are taken over only while this is true (and a HUD exists to draw them in).
    internal bool Active
    {
        get
        {
            if (!Wanted || !global::Hud.instance) return false;
            if (!root || hudRoot != global::Hud.instance.m_rootObject) Tick();
            return root;
        }
    }

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
            Dispose();
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
            root.sizeDelta = new Vector2(CardWidth, 0);
            skillRoot = Ugui.Node(hudRoot.transform, "Ragnarok Skill Toasts");
            skillRoot.sizeDelta = new Vector2(CardWidth, 200);
        }
        float feedHeight = 0;
        float time = Time.unscaledTime;
        for (int i = entries.Count - 1; i >= 0; i--)
            if (time - entries[i].Born <= (entries[i].Card ? CardLife : RowLife) + FadeOut && entries.Count - i <= MaxShown && !entries[i].Skill)
                feedHeight += entries[i].Height + Spacing;
        root!.sizeDelta = new Vector2(CardWidth, Mathf.Max(0, feedHeight - Spacing));
        settings.Notifications.Apply(root!);
        settings.SkillToast.Apply(skillRoot!);
        skillRoot!.localScale = Vector3.one * settings.HudScale.Value;
        root!.localScale = Vector3.one * settings.HudScale.Value;

        // Newest nearest the chosen edge; entries ease towards their slots and fade by age.
        float now = Time.unscaledTime, y = 0, skillY = 0;
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
            if (e.Skill)
            {
                e.Place(-slide, skillY, alpha, settings.SkillToast);
                skillY += e.Height + Spacing;
            }
            else
            {
                bool bottom = (int)settings.Notifications.Position.Value >= 6;
                e.Place(slide, bottom ? root.sizeDelta.y - y - e.Height : y, alpha, settings.Notifications);
                y += e.Height + Spacing;
            }
        }
    }

    public void Dispose()
    {
        foreach (Entry e in entries) e.Destroy();
        entries.Clear();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        if (skillRoot) UnityEngine.Object.Destroy(skillRoot.gameObject);
        skillRoot = null;
    }

    internal void Unlock(Sprite? icon, string topic, string name)
    {
        if (!root) return;
        Message(icon, topic + ": " + Highlight(name), 0);
    }

    internal static string Highlight(string text) => "<color=#C5A65C>" + text + "</color>";

    internal void SkillImproved(Sprite? icon, string text)
    {
        if (!skillRoot) return;
        Entry entry = Entry.MakeCard(theme, skillRoot, icon,
            Localization.instance.Localize("$msg_skillup"), text);
        entry.Skill = true;
        entries.Add(entry);
    }

    // A repeat of a recent row (same text and icon) adds to its amount instead of stacking up.
    internal void Message(Sprite? icon, string text, int amount)
    {
        if (!root) return;
        foreach (Entry e in entries)
            if (!e.Card && e.Text == text && e.Icon == icon && e.HasAmount == (amount > 0) && Time.unscaledTime - e.Born < RowLife)
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
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "$msg_skillup $skill_run: 42");
    }

    private sealed class Entry
    {
        internal readonly bool Card;
        internal bool Skill;
        internal readonly string Text;
        internal readonly Sprite? Icon;
        internal float Born;
        internal readonly float Height;
        private readonly RectTransform rect;
        private readonly CanvasGroup group;
        private readonly HudBackdrop? backdrop;
        private readonly TextMeshProUGUI? rowText;
        internal bool HasAmount => amount > 0;
        private int amount;

        private Entry(bool card, string text, Sprite? icon, RectTransform rect, HudBackdrop? backdrop, float height,
            TextMeshProUGUI? rowText, int amount)
        {
            Card = card;
            Text = text;
            Icon = icon;
            Born = Time.unscaledTime;
            Height = height;
            this.rect = rect;
            this.backdrop = backdrop;
            this.rowText = rowText;
            this.amount = amount;
            group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
        }

        internal static Entry MakeCard(Theme theme, RectTransform parent, Sprite? icon, string topic, string name)
        {
            RectTransform rect = Ugui.Node(parent, "Unlock").At(0, 0, CardWidth, CardHeight);
            HudBackdrop backdrop = new(theme, rect, new Vector4(14, 6, 14, 6));
            Ugui.Image(rect, "Surface", theme.Minimal, Color.white).Stretch();
            Ugui.Image(rect, "Accent", theme.White, Palette.GoldBright).At(0, 8, 3, CardHeight - 16);
            Ugui.Image(rect, "Rim", theme.Slot, Color.white).At(12, 7, 44, 44);

            Image picture = Ugui.Image(rect, "Icon", icon, Color.white).At(22, 17, 24, 24);
            Ugui.ItemIcon(picture);
            picture.enabled = icon;
            // Small gold category over a parchment title.
            Ugui.Text(rect, "Topic", topic.ToUpperInvariant(), theme.Strong, 11, Palette.GoldBright).At(66, 4, CardWidth - 76, 21);
            Ugui.Text(rect, "Name", name, theme.Heading, 18, Palette.Bone).At(66, 22, CardWidth - 76, 27);
            return new Entry(true, name, icon, rect, backdrop, CardHeight, null, 0);
        }

        internal static Entry MakeRow(Theme theme, RectTransform parent, Sprite? icon, string text, int amount)
        {
            RectTransform rect = Ugui.Node(parent, "Message").At(0, 0, CardWidth, RowHeight);
            TextMeshProUGUI label = Ugui.Text(rect, "Text", text, theme.Strong, 16, Palette.Bone)
                .At(0, 0, CardWidth, RowHeight);
            Entry entry = new(false, text, icon, rect, null, RowHeight, label, amount);
            entry.ShowAmount();
            return entry;
        }

        internal void Bump(int more)
        {
            amount += Mathf.Max(0, more);
            Born = Time.unscaledTime - FadeIn; // Stay fully shown, restart the clock.
            ShowAmount();
        }

        private void ShowAmount()
        {
            if (rowText != null) rowText.text = Text + (amount > 0 ? " x" + amount : "");
        }

        internal void Place(float x, float y, float alpha, HudPlacement placement)
        {
            Vector2 target = new(x, -y);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 0.35f);
            group.alpha = alpha;
            backdrop?.Apply(placement);
        }

        internal void Destroy()
        {
            if (rect) UnityEngine.Object.Destroy(rect.gameObject);
        }
    }
}

// Unlock messages become Ragnarök text rows while the feed is active; the log still records them.
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
        bool skill = text.StartsWith("$msg_skillup ", StringComparison.Ordinal);
        if ((!skill && type != MessageHud.MessageType.TopLeft) || feed == null || !feed.Active) return true;
        if (global::Hud.IsUserHidden() && !showDespiteHiddenHUD) return false;
        string localized = Localization.instance.Localize(text);
        if (skill) feed.SkillImproved(icon, Localization.instance.Localize(text.Substring("$msg_skillup ".Length)));
        else
        {
            string styled = text;
            int itemStart = text.IndexOf("$item_", StringComparison.Ordinal);
            if (itemStart >= 0)
            {
                int itemEnd = itemStart;
                while (itemEnd < text.Length && (char.IsLetterOrDigit(text[itemEnd]) || text[itemEnd] == '$' || text[itemEnd] == '_')) itemEnd++;
                string item = text.Substring(itemStart, itemEnd - itemStart);
                styled = text.Substring(0, itemStart) + NotificationFeed.Highlight(Localization.instance.Localize(item)) + text.Substring(itemEnd);
            }
            feed.Message(icon, Localization.instance.Localize(styled), amount);
        }
        if (log) AddLog?.Invoke(__instance, new object[] { localized });
        return false;
    }
}
