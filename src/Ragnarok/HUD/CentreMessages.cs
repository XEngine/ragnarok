using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Presentation only: native discovery animation/audio still runs behind a mask.
// No input focus, camera effects, gameplay events, save edits or network messages.
internal sealed class CentreMessages : IDisposable
{
    private sealed class Banner
    {
        internal string Caption = "", Title = "", Detail = "";
        internal bool Death;
    }
    private static readonly AccessTools.FieldRef<MessageHud, GameObject> NativeBiome =
        AccessTools.FieldRefAccess<MessageHud, GameObject>("m_biomeMsgInstance");
    private static readonly AccessTools.FieldRef<Character, HitData> LastHit =
        AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");
    private static readonly Regex Colors = new(@"</?color(?:=[^>]*)?>", RegexOptions.IgnoreCase);
    private readonly Theme theme;
    private readonly RectTransform root, major, minor, skill;
    private readonly CanvasGroup majorFade, minorFade, skillFade;
    private readonly TextMeshProUGUI caption, title, detail, shortText, skillName, skillDetail, skillLevel;
    private readonly Image diamond, leftRule, rightRule;
    private readonly List<TextOutline> outlines = new();
    private readonly Queue<Banner> pending = new();
    private readonly Vector3[] corners = new Vector3[4];
    private Banner? current;
    private float born, shortBorn = -10, skillBorn = -10;
    private GameObject? nativeBiome;
    private CanvasGroup? biomeMask;
    private float biomeAlpha;
    private bool ownedMask;
    private Player? observed;
    private string power = "";
    private int day;

    internal CentreMessages(Theme theme, Transform parent)
    {
        this.theme = theme;
        root = Ugui.Node(parent, "Ragnarok Centre Messages").Stretch();
        root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        major = Ugui.Node(root, "Major message");
        major.anchorMin = major.anchorMax = new Vector2(0.5f, 0.62f);
        major.pivot = new Vector2(0.5f, 0.5f);
        major.sizeDelta = new Vector2(760, 180);
        majorFade = major.gameObject.AddComponent<CanvasGroup>();
        majorFade.alpha = 0;
        leftRule = Ugui.Image(major, "Left rule", theme.White, Palette.Gold).At(252, 12, 72, 1);
        rightRule = Ugui.Image(major, "Right rule", theme.White, Palette.Gold).At(436, 12, 72, 1);
        diamond = Ugui.Image(major, "Diamond", theme.Diamond, Palette.Gold).At(374, 6, 12, 12);
        caption = Ugui.Text(major, "Caption", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center, spacing: 3)
            .At(80, 28, 600, 28);
        title = Ugui.Text(major, "Title", "", theme.Heading, 48, Palette.Bone, TextAlignmentOptions.Center).At(16, 57, 728, 68);
        title.enableAutoSizing = true;
        title.fontSizeMin = 25;
        title.fontSizeMax = 48;
        detail = Ugui.Text(major, "Detail", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.Top, wrap: true).At(70, 137, 620, 42);
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 11;
        detail.fontSizeMax = 14;
        minor = Ugui.Node(root, "System line");
        minor.anchorMin = minor.anchorMax = new Vector2(0.5f, 0.32f);
        minor.pivot = new Vector2(0.5f, 0.5f);
        minor.sizeDelta = new Vector2(780, 64);
        minorFade = minor.gameObject.AddComponent<CanvasGroup>();
        minorFade.alpha = 0;
        shortText = Ugui.Text(minor, "Message", "", theme.Strong, 21, Palette.Bone, TextAlignmentOptions.Center, wrap: true).Stretch();
        shortText.enableAutoSizing = true;
        shortText.fontSizeMin = 14;
        shortText.fontSizeMax = 21;
        foreach (TextMeshProUGUI label in new[] { caption, title, detail, shortText })
            outlines.Add(new TextOutline(label, world: true));

        skill = Ugui.Node(root, "Skill milestone");
        skill.anchorMin = skill.anchorMax = skill.pivot = new Vector2(1, 1);
        skill.sizeDelta = new Vector2(400, 78);
        skillFade = skill.gameObject.AddComponent<CanvasGroup>();
        skillFade.alpha = 0;
        Ugui.Image(skill, "Surface", theme.PackSurface, Palette.Hex(0x0C0F11, 0.96f)).Stretch();
        Ugui.Image(skill, "Outline", theme.PackEdge, InventoryCategories.Gold).Stretch();
        Ugui.Image(skill, "Level ring", theme.HudRing, InventoryCategories.Gold).At(14, 13, 52, 52);
        skillLevel = Ugui.Text(skill, "Level", "", theme.Numbers, 23, Palette.Bone, TextAlignmentOptions.Center).At(14, 13, 52, 52);
        Ugui.Text(skill, "Caption", "MILESTONE", theme.Strong, 10, InventoryCategories.Gold, spacing: 2).At(80, 8, 300, 16);
        skillName = Ugui.Text(skill, "Skill", "", theme.Heading, 19, Palette.Bone).At(80, 26, 300, 25);
        skillName.enableAutoSizing = true;
        skillName.fontSizeMin = 12;
        skillName.fontSizeMax = 19;
        skillDetail = Ugui.Text(skill, "Effect", "", theme.Body, 11.5f, Palette.Muted, wrap: true).At(80, 52, 300, 22);
        skillDetail.enableAutoSizing = true;
        skillDetail.fontSizeMin = 9;
        skillDetail.fontSizeMax = 11.5f;
    }

    internal void System(string raw, bool failure = false)
    {
        if (raw.Contains("$msg_youdied"))
        {
            Character? attacker = Player.m_localPlayer ? LastHit(Player.m_localPlayer)?.GetAttacker() : null;
            string cause = attacker ? "Slain by " + Localization.instance.Localize(attacker!.GetHoverName()) : "";
            pending.Clear();
            current = null;
            Queue("ᚾ ᚨ ᚢ ᛞ", Localization.instance.Localize(raw), cause, death: true);
            return;
        }
        shortText.text = Colors.Replace(Localization.instance.Localize(raw), "");
        shortText.color = failure || raw.Contains("nostamina") || raw.Contains("noeitr") || raw.Contains("noroom") || raw.Contains("inventoryfull")
            ? Palette.Hex(0xF7A99F) : raw.Contains("blocked") || raw.Contains("cant") || raw.Contains("cannot")
            || raw.Contains("missing") || raw.Contains("nobuildzone") || raw.Contains("privatezone") || raw.Contains("needspace")
            || raw.Contains("invalidplacement") || raw.Contains("wrongbiome") || raw.Contains("enemiesnearby")
            || raw.Contains("needcultivated") || raw.Contains("needdirt") || raw.Contains("powernotready")
            ? Palette.Hex(0xF6C77A) : Palette.Hex(0xECE5D6);
        shortBorn = Time.unscaledTime;
    }

    private void Queue(string label, string name, string description, bool death = false)
    {
        if (pending.Count >= 8) pending.Dequeue();
        pending.Enqueue(new Banner { Caption = label, Title = name, Detail = description, Death = death });
    }

    // UI-only samples: bypass progression observers and the native message log.
    internal void Preview(string kind)
    {
        switch (kind)
        {
            case "death": Queue("ᚾ ᚨ ᚢ ᛞ", "You died", "Slain by The Elder", death: true); break;
            case "discovery": Queue("DISCOVERED", "Black Forest", "DAY 47"); break;
            case "power": Queue("FORSAKEN POWER GAINED", "Eikthyr", "Press " +
                Localization.instance.Localize(ZInput.instance != null ? ZInput.instance.GetBoundKeyString("GP", true) : "F") + " to use this power"); break;
            case "day": Queue("NEW DAY", "Day 47", ""); break;
        }
    }

    internal void Clear()
    {
        pending.Clear();
        current = null;
        shortBorn = skillBorn = -10;
        majorFade.alpha = minorFade.alpha = skillFade.alpha = 0;
    }

    internal void Skill(string name, int level, string description)
    {
        skillName.text = name + " · LEVEL " + level;
        skillLevel.text = level.ToString();
        skillDetail.text = description;
        skillBorn = Time.unscaledTime;
    }

    internal void Tick(float scale, RectTransform? recipe)
    {
        float now = Time.unscaledTime;
        bool visible = !global::Hud.IsUserHidden();
        root.gameObject.SetActive(visible);
        float available = Mathf.Max(1, root.rect.width - 32);
        major.localScale = Vector3.one * Mathf.Min(scale, available / 760);
        minor.localScale = Vector3.one * Mathf.Min(scale, available / 780);
        skill.localScale = Vector3.one * Mathf.Min(scale, available / 400);
        NotificationPlacement.BesideMinimap(skill, root, corners);
        if (recipe && recipe!.gameObject.activeInHierarchy)
        {
            recipe.GetWorldCorners(corners);
            skill.position = corners[3] - root.TransformVector(new Vector3(0, 8, 0));
            Utils.ClampUIToScreen(skill);
        }
        PollDiscovery();
        PollProgression();
        if (current == null && pending.Count > 0)
        {
            current = pending.Dequeue();
            born = now;
            caption.text = current.Caption;
            caption.font = current.Death ? theme.Runic : theme.Strong;
            caption.color = current.Death ? Palette.Hex(0xF09A5A) : Palette.Bone;
            title.text = current.Title;
            title.color = current.Death || current.Caption == "DISCOVERED" || current.Caption == "NEW DAY"
                ? Palette.Hex(0xECE5D6) : Palette.Hex(0xF2D597);
            detail.text = current.Detail;
            diamond.color = current.Death ? Palette.Hex(0xD64A3E) : InventoryCategories.Gold;
            leftRule.color = rightRule.color = current.Death ? Palette.Hex(0xB65D4F) : InventoryCategories.Gold;
        }
        majorFade.alpha = current == null ? 0 : Fade(now - born);
        if (current != null && now - born >= 4.2f) current = null;
        minorFade.alpha = Mathf.Clamp01((2.5f - (now - shortBorn)) / 0.3f);
        skillFade.alpha = Fade(now - skillBorn);
        foreach (TextOutline outline in outlines) outline.Sync();
    }

    private static float Fade(float age) => Mathf.Clamp01(age / 0.4f) * Mathf.Clamp01((4.2f - age) / 0.8f);

    private void PollProgression()
    {
        Player player = Player.m_localPlayer;
        if (!player || !EnvMan.instance || player.IsTeleporting() || player.IsDead()) return;
        string selected = player.GetGuardianPowerName() ?? "";
        int currentDay = EnvMan.instance.GetDay();
        if (observed == player)
        {
            if (day != currentDay && currentDay > day) Queue("NEW DAY", "Day " + currentDay, "");
            if (power != selected && selected.Length > 0 && !player.IsTeleporting() && !player.IsDead())
            {
                player.GetGuardianPowerHUD(out StatusEffect effect, out _);
                if (effect)
                {
                    string bound = ZInput.instance != null ? ZInput.instance.GetBoundKeyString("GP", true) : "F";
                    string hint = string.IsNullOrEmpty(bound) ? "Bind Forsaken power in controls" : "Press " + Localization.instance.Localize(bound) + " to use this power";
                    Queue("FORSAKEN POWER GAINED", Localization.instance.Localize(effect.m_name), hint);
                }
            }
        }
        observed = player;
        power = selected;
        day = currentDay;
    }

    private void PollDiscovery()
    {
        GameObject? instance = MessageHud.instance ? NativeBiome(MessageHud.instance) : null;
        if (instance == nativeBiome)
        {
            // Its native animator may animate alpha; keep only its presentation masked.
            if (biomeMask) biomeMask!.alpha = 0;
            return;
        }
        RestoreDiscovery();
        nativeBiome = instance;
        if (!instance) return;
        Transform text = Utils.FindChild(instance!.transform, "Title");
        if (!text || !text.TryGetComponent(out TMP_Text label)) return;
        ownedMask = !instance.TryGetComponent(out CanvasGroup group);
        if (ownedMask) group = instance.AddComponent<CanvasGroup>();
        biomeMask = group;
        biomeAlpha = group.alpha;
        group.alpha = 0;
        Queue("DISCOVERED", label.text, EnvMan.instance ? "DAY " + EnvMan.instance.GetDay() : "");
    }

    private void RestoreDiscovery()
    {
        if (biomeMask)
        {
            biomeMask!.alpha = biomeAlpha;
            // Remove only our UI component immediately so a same-frame plugin
            // reload cannot pick up a group already queued for destruction.
            if (ownedMask) UnityEngine.Object.DestroyImmediate(biomeMask);
        }
        biomeMask = null;
        nativeBiome = null;
    }

    public void Dispose()
    {
        RestoreDiscovery();
        if (root) { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        pending.Clear();
    }
}
