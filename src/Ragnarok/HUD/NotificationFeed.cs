using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Separate pickup, unlock, skill and general-message channels. Native log stays intact.
internal sealed class NotificationFeed : IDisposable
{
    private const float Width = 360, Height = 46, Spacing = 6, Life = 4;
    private const float CardOpacity = Palette.HudGlassOpacity;
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<Entry> entries = new();
    private GameObject? hudRoot;
    private RectTransform? root, skillRoot;
    private Entry? skill;
    private CentreMessages? centre;
    private ItemNotifications? items;
    internal static NotificationFeed? Instance;
    internal bool VitalsSpaceBusy => Wanted && (items?.HasPickup == true || skill != null);

    internal NotificationFeed(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        Instance = this;
    }

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
        if (parent != hudRoot) { Dispose(); hudRoot = parent; }
        if (!hudRoot || !Wanted) { Dispose(); return; }
        if (!root || !skillRoot || items == null || !items.Alive)
        {
            Dispose();
            root = Ugui.Node(hudRoot!.transform, "Ragnarok Notifications");
            skillRoot = Ugui.Node(hudRoot.transform, "Ragnarok Skill Toasts");
            skillRoot.sizeDelta = new Vector2(Width, 52);
            centre = new CentreMessages(theme, hudRoot.transform);
            items = new ItemNotifications(theme, hudRoot.transform);
        }
        float now = Time.unscaledTime;
        for (int i = entries.Count - 1; i >= 0; i--)
            if (now - entries[i].Born >= Life) { entries[i].Destroy(); entries.RemoveAt(i); }
        float feedHeight = Mathf.Max(0, entries.Count * (Height + Spacing) - Spacing);
        root!.sizeDelta = new Vector2(Width, feedHeight);
        settings.Notifications.Apply(root);
        settings.SkillToast.Apply(skillRoot!);
        root.localScale = skillRoot!.localScale = Vector3.one * settings.HudScale.Value;
        if (hudRoot!.transform is RectTransform canvas)
            skillRoot.localScale = Vector3.one * Mathf.Min(settings.HudScale.Value,
                Mathf.Max(1, canvas.rect.width - 32) / skillRoot.rect.width);
        RectTransform? vitals = RagnarokHud.ShownVitals;
        if (vitals && vitals!.gameObject.activeInHierarchy && settings.SkillToast.Position.Value == ScreenAnchor.BottomCenter &&
            settings.SkillToast.MarginX.Value == 0 && settings.SkillToast.MarginY.Value == 228)
        {
            skillRoot.position = vitals.TransformPoint(new Vector3(vitals.rect.center.x, vitals.rect.yMax, 0));
            skillRoot.position += vitals.TransformVector(new Vector3(0, items?.HasPickup == true ? 76 : 12, 0));
            Utils.ClampUIToScreen(skillRoot);
        }
        root.gameObject.SetActive(!global::Hud.IsUserHidden());
        skillRoot.gameObject.SetActive(!global::Hud.IsUserHidden());
        // Oldest at the top; newest at the bottom for the default bottom-left stack.
        for (int i = 0; i < entries.Count; i++) entries[i].Place(i * (Height + Spacing), now, settings.Notifications);
        if (skill != null)
        {
            if (now - skill.Born >= Life) { skill.Destroy(); skill = null; }
            else skill.Place(0, now, settings.SkillToast);
        }
        centre!.Tick(settings.HudScale.Value);
        items!.Tick(settings.HudScale.Value);
    }

    public void Dispose()
    {
        foreach (Entry entry in entries) entry.Destroy();
        entries.Clear();
        skill?.Destroy();
        skill = null;
        var oldItems = items;
        items = null;
        oldItems?.Dispose();
        var oldCentre = centre;
        centre = null;
        oldCentre?.Dispose();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        if (skillRoot) { skillRoot!.gameObject.SetActive(false); UnityEngine.Object.Destroy(skillRoot.gameObject); }
        root = skillRoot = null;
    }

    private void Add(Entry entry)
    {
        if (entries.Count == 4) { entries[0].Destroy(); entries.RemoveAt(0); }
        entries.Add(entry);
    }

    internal void Unlock(Sprite? icon, string topic, string name) => items?.Unlock(icon, topic, name);

    internal void Pickup(string name, int amount) => items?.Pickup(name, amount);

    internal static string Highlight(string text) => "<b>" + text + "</b>";

    internal void SkillImproved(Sprite? icon, string raw)
    {
        if (!skillRoot) return;
        string localized = Localization.instance.Localize(raw);
        Match match = Regex.Match(raw, @"\$skill_(\w+):\s*(\d+)");
        if (match.Success && int.TryParse(match.Groups[2].Value, out int level) && level > 0 && level % 10 == 0)
        {
            string name = Localization.instance.Localize("$skill_" + match.Groups[1].Value);
            string description = "";
            if (Enum.TryParse(match.Groups[1].Value, true, out Skills.SkillType type) && Player.m_localPlayer)
            {
                Skills.SkillDef? definition = Player.m_localPlayer.GetSkills().m_skills.Find(s => s.m_skill == type);
                if (definition != null) description = Localization.instance.Localize(definition.m_description);
            }
            centre!.Skill(name, level, description);
            skill?.Destroy();
            skill = null;
            return;
        }
        skill?.Destroy();
        skill = Entry.Pill(theme, skillRoot!, localized);
    }

    internal void Centre(string raw) => centre?.System(raw);

    internal void Preview(string kind)
    {
        string Local(string raw) => Localization.instance.Localize(raw);
        switch (kind)
        {
            case "clear":
                foreach (Entry entry in entries) entry.Destroy();
                entries.Clear();
                skill?.Destroy();
                skill = null;
                centre?.Clear();
                items?.Clear();
                break;
            case "pickup":
                ItemDrop.ItemData? wood = ObjectDB.instance?.GetItemPrefab("Wood")?.GetComponent<ItemDrop>()?.m_itemData;
                string text = Local(wood?.m_shared.m_name ?? "Wood");
                Pickup(text, 2);
                Pickup(text, 3);
                break;
            case "unlock": Unlock(null, "New recipe", "Bronze Mace · Forge"); break;
            case "rested": Message(null, "You feel rested", 0, "rested"); break;
            case "wet": Message(null, "You are wet", 0, "wet"); break;
            case "warning": Message(null, "Inventory full", 0, "inventoryfull"); break;
            case "skill": SkillImproved(null, "$skill_run: 42"); break;
            case "milestone": SkillImproved(null, "$skill_run: 40"); break;
            case "system": Centre("Your bed is too far from a fire"); break;
            case "blocked": Centre("Building blocked · enemies nearby"); break;
            case "failure": centre?.System("Not enough stamina", failure: true); break;
            default: centre?.Preview(kind); break;
        }
    }

    internal void Message(Sprite? icon, string text, int amount, string raw = "")
    {
        if (!root) return;
        foreach (Entry entry in entries)
            if (entry.Text == text && entry.Icon == icon && entry.HasAmount == (amount > 0) && Time.unscaledTime - entry.Born < Life)
            { entry.Bump(amount); return; }
        bool wet = raw.IndexOf("wet", StringComparison.OrdinalIgnoreCase) >= 0;
        bool danger = raw.Contains("inventoryfull") || raw.Contains("noroom") || raw.Contains("freez");
        bool rested = raw.IndexOf("rested", StringComparison.OrdinalIgnoreCase) >= 0;
        Color accent = danger ? Palette.Hex(0xD64A3E) : wet ? Palette.Hex(0x8FC1E8) : rested ? InventoryCategories.Gold : Palette.Hex(0x626B64);
        Color surface = danger ? Palette.Hex(0x2D100D, CardOpacity) : wet ? Palette.Hex(0x17252D, CardOpacity) : Palette.Hex(0x0C0F11, CardOpacity);
        Entry next = Entry.Card(theme, root!, icon ?? (wet ? theme.IconDrop : rested ? theme.HudMoon : danger ? theme.GlyphAlert : null), "", text, accent, surface, amount);
        next.Icon = icon; // Merge by the source identity, not its presentation fallback.
        Add(next);
    }

    internal static void Test()
    {
        if (!MessageHud.instance) { Debug.Log("[Ragnarok] Load a world before testing notifications."); return; }
        ItemDrop.ItemData? sword = ObjectDB.instance?.GetItemPrefab("SwordIron")?.GetComponent<ItemDrop>()?.m_itemData;
        ItemDrop.ItemData? wood = ObjectDB.instance?.GetItemPrefab("Wood")?.GetComponent<ItemDrop>()?.m_itemData;
        MessageHud.instance.QueueUnlockMsg(sword?.GetIcon(), "$msg_newrecipe", sword?.m_shared.m_name ?? "Iron sword");
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "$msg_added " + (wood?.m_shared.m_name ?? "Wood"), 3, wood?.GetIcon());
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "$msg_added " + (wood?.m_shared.m_name ?? "Wood"), 2, wood?.GetIcon());
        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, "The path ahead is clear");
        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, "$msg_skillup $skill_run: 42");
    }

    private sealed class Entry
    {
        internal readonly string Text;
        internal Sprite? Icon;
        internal float Born;
        internal bool HasAmount => amount > 0;
        private readonly RectTransform rect;
        private readonly CanvasGroup fade;
        private readonly TextMeshProUGUI? count;
        private int amount;

        private Entry(Theme theme, RectTransform rect, string text, Sprite? icon, TextMeshProUGUI? count, int amount)
        {
            this.rect = rect;
            Text = text;
            Icon = icon;
            this.count = count;
            this.amount = amount;
            Born = Time.unscaledTime;
            fade = rect.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = fade.interactable = false;
            fade.alpha = 0;
            ShowAmount();
        }

        internal static Entry Card(Theme theme, Transform parent, Sprite? icon, string topic, string text, Color accent, Color surface, int amount)
        {
            RectTransform rect = Ugui.Node(parent, "Notification").At(0, 0, Width, Height);
            Ugui.Image(rect, "Glass", theme.PackSurface, surface).Stretch();
            Ugui.Image(rect, "Edge", theme.PackEdge, accent.WithAlpha(0.45f)).Stretch();
            Ugui.Image(rect, "Icon well", theme.HudKeySurface, Palette.Hex(0x0C0F11, 0.4f)).At(8, 8, 30, 30);
            Ugui.Image(rect, "Icon rim", theme.HudKeyEdge, accent.WithAlpha(0.65f)).At(8, 8, 30, 30);
            Image picture = Ugui.Image(rect, "Icon", icon ?? theme.Diamond, icon ? Color.white : Palette.Muted).At(15, 15, 16, 16);
            Ugui.ItemIcon(picture);
            if (topic.Length > 0)
                Ugui.Text(rect, "Topic", topic, theme.Strong, 10, accent, spacing: 1.5f).At(48, 5, Width - 60, 15);
            TextMeshProUGUI label = Ugui.Text(rect, "Text", text, theme.Strong, 13, Palette.Bone, wrap: true)
                .At(48, topic.Length > 0 ? 20 : 5, Width - 100, topic.Length > 0 ? 22 : 36);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10;
            label.fontSizeMax = 13;
            TextMeshProUGUI count = Ugui.Text(rect, "Amount", "", theme.Numbers, 13, InventoryCategories.Gold, TextAlignmentOptions.MidlineRight)
                .At(Width - 48, 0, 36, Height);
            return new Entry(theme, rect, text, icon, count, amount);
        }

        internal static Entry Pill(Theme theme, RectTransform parent, string text)
        {
            const float height = 52;
            RectTransform rect = Ugui.Node(parent, "Skill improved").At(0, 0, Width, height);
            Ugui.Image(rect, "Glass", theme.SkillPill, Palette.Hex(0x0C0F11, CardOpacity)).Stretch();
            Ugui.Image(rect, "Edge", theme.SkillPillEdge, Palette.Hex(0x626B64, 0.55f)).Stretch();
            Ugui.Image(rect, "Badge", theme.Disc, InventoryCategories.Gold).At(11, 11, 30, 30);
            Ugui.Image(rect, "Arrow", theme.SkillUp, Palette.Hex(0x101719)).At(18, 18, 16, 16).preserveAspect = true;
            TextMeshProUGUI caption = Ugui.Text(rect, "Caption", Localization.instance.Localize("$msg_skillup"), theme.Body, 14, Palette.Muted);
            float captionWidth = Mathf.Clamp(caption.preferredWidth, 60, 180);
            caption.At(53, 0, captionWidth, height);
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 11;
            caption.fontSizeMax = 14;
            TextMeshProUGUI name = Ugui.Text(rect, "Skill", text.Replace(": ", " "), theme.Heading, 18, Palette.Bone,
                TextAlignmentOptions.MidlineRight);
            float nameX = 53 + captionWidth + 20;
            float width = Mathf.Min(nameX + name.preferredWidth + 20, 560);
            rect.sizeDelta = parent.sizeDelta = new Vector2(width, height);
            name.At(nameX, 0, width - nameX - 20, height);
            name.enableAutoSizing = true;
            name.fontSizeMin = 12;
            name.fontSizeMax = 18;
            return new Entry(theme, rect, text, null, null, 0);
        }

        internal void Bump(int more) { amount += Mathf.Max(0, more); Born = Time.unscaledTime - 0.15f; ShowAmount(); }
        private void ShowAmount() { if (count) count!.text = amount > 0 ? "×" + amount : ""; }
        internal void Place(float y, float now, HudPlacement placement)
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, new Vector2(0, -y), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            float age = now - Born;
            fade.alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((Life - age) / 0.3f);
        }
        internal void Destroy() { if (rect) { rect.gameObject.SetActive(false); UnityEngine.Object.Destroy(rect.gameObject); } }
    }
}
// Unlock cards keep the native message log; their visual queue belongs to the feed.
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

// Route native notifications and centre messages without changing their source
// events or log behavior. Explicit messages over a hidden HUD stay native.
[HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
internal static class TopLeftMessagePatch
{
    private static readonly MethodInfo? AddLog = AccessTools.Method(typeof(MessageHud), "AddLog");

    private static bool Prefix(MessageHud __instance, MessageHud.MessageType type, string text, int amount, Sprite icon,
        bool showDespiteHiddenHUD, bool log)
    {
        NotificationFeed? feed = NotificationFeed.Instance;
        bool skill = text.StartsWith("$msg_skillup ", StringComparison.Ordinal);
        if ((type != MessageHud.MessageType.TopLeft && type != MessageHud.MessageType.Center) || feed == null || !feed.Active) return true;
        if (global::Hud.IsUserHidden()) return showDespiteHiddenHUD;
        string localized = Localization.instance.Localize(text);
        if (skill) feed.SkillImproved(icon, text.Substring("$msg_skillup ".Length));
        else if (type == MessageHud.MessageType.Center) feed.Centre(text);
        else if (amount > 0 && text.StartsWith("$msg_added ", StringComparison.Ordinal))
            feed.Pickup(Localization.instance.Localize(text.Substring("$msg_added ".Length)), amount);
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
            feed.Message(icon, Localization.instance.Localize(styled), amount, text);
        }
        if (log) AddLog?.Invoke(__instance, new object[] { localized });
        return false;
    }
}
