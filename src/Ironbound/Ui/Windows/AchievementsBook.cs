using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// The Ironbound achievements screen, drawn over Valheim's achievements panel in the Compendium's frame:
// every achievement on the left as a wall of badges in Valheim's order (unlocked ones lit), the chosen
// one in the middle with its conditions, and its badge in the art panel. Like Valheim, it keeps secrets:
// a secret achievement stays hidden until unlocked, and a condition not yet met shows as ???.
internal sealed class AchievementsBook : BookWindow
{
    private const float ListWidth = 330, TextX = 470, TextWidth = 470, Tile = 58, Gap = 8, Line = 30;
    private const int Columns = 5;

    private readonly MonochromeIcons icons = new();
    private readonly List<Achievement> all = new();
    private GameObject? panel;
    private RectTransform? listContent, conditions;
    private TextMeshProUGUI? total, cheated, title, kind, countValue, body, conditionsLabel;
    private RectTransform? countFill;
    private Achievement? chosen;
    private int signature;

    internal AchievementsBook(InterfaceSettings settings, Theme theme) : base(settings, theme) { }

    protected override string Name => "Achievements";
    protected override Tab Selected => Tab.Achievements;

    protected override bool Wanted => settings.AchievementsStyle.Value switch
    {
        PanelStyle.Ironbound => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ironbound
    };

    protected override Component? Dialog(InventoryGui gui) => gui.m_achievementsPanel;

    protected override void CloseDialog()
    {
        if (panel) InventoryGui.instance?.OnCloseAchievements();
    }

    public override void Dispose()
    {
        base.Dispose();
        icons.Dispose();
    }

    protected override void Build(InventoryGui gui)
    {
        panel = gui.m_achievementsPanel.gameObject;
        Collect();
        total = Ugui.Text(window!, "Total", "", theme.Strong, 14, Palette.Bone).At(Left, 100, ListWidth, 26);
        // Valheim's note when cheats have closed achievements for this character or world.
        cheated = Ugui.Text(window!, "Cheated", "", theme.Subtle, 13, Palette.Alert, TextAlignmentOptions.TopLeft, wrap: true)
            .At(Left, Height - 104, TextX - 36 - Left - 20, 34);

        Scroller(new Rect(Left - 20, Top, ListWidth + 20, Height - Top - 120), out listContent);

        title = Ugui.Text(window!, "Title", "", theme.Heading, 28, Palette.Bone).At(TextX, Top - 6, TextWidth, 44);
        kind = Ugui.Text(window!, "Kind", "", theme.Subtle, 14, Palette.Muted).At(TextX, Top + 38, TextWidth, 20);

        float y = Top + 84;
        Ugui.Text(window!, "Count Label", "completion", theme.Strong, 13, Palette.Muted).At(TextX, y - 4, 200, 24);
        countValue = Ugui.Text(window!, "Count Value", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.MidlineRight)
            .At(TextX + TextWidth - 160, y, 160, 18);
        Ugui.Image(window!, "Groove", theme.HudBar, Palette.Hex(0x303631)).At(TextX, y + 24, TextWidth, 6);
        countFill = Ugui.Image(window!, "Fill", theme.HudBar, Palette.Gold).At(TextX, y + 24, 0, 6).rectTransform;

        body = Ugui.Text(window!, "Body", "", theme.Body, 15, Palette.Bone, TextAlignmentOptions.TopLeft, wrap: true)
            .At(TextX, y + 60, TextWidth, 70);
        body.lineSpacing = 6;
        conditionsLabel = Ugui.Text(window!, "Conditions Label", "", theme.Strong, 13, Palette.Muted).At(TextX, y + 140, TextWidth, 24);
        Scroller(new Rect(TextX, y + 170, TextWidth + 20, Height - (y + 170) - 80), out conditions);

        signature = 0;
        Refresh();
    }

    // Valheim's list: badges sorted by the tier in their locked icon's name (01-09), as its own panel does.
    private void Collect()
    {
        all.Clear();
        if (Achievements.m_instance == null) return;
        List<(int Tier, int Index, Achievement Achievement)> found = new();
        foreach (AchievementList list in Achievements.m_instance.m_achievementLists)
        foreach (Achievement achievement in list.m_achievements)
        {
            string icon = achievement.m_iconLocked ? achievement.m_iconLocked.name : "";
            int tier = icon.Length >= 2 && int.TryParse(icon.Substring(0, 2), out int t) && t <= 9 ? t : 9;
            if (tier >= 1) found.Add((tier, found.Count, achievement));
        }
        all.AddRange(found.OrderBy(f => f.Tier).ThenBy(f => f.Index).Select(f => f.Achievement));
    }

    private static bool Hidden(Achievement a) => a.m_isSecret && !a.m_unlocked;

    // Rebuilt when one unlocks or the choice changes.
    protected override void Refresh()
    {
        if (!listContent || !Player.m_localPlayer || all.Count == 0) return;
        if (chosen == null || !all.Contains(chosen)) chosen = all[0];

        int unlocked = all.Count(a => a.m_unlocked);
        int now = all.IndexOf(chosen) * 1000 + unlocked;
        if (now == signature) return;
        signature = now;

        total!.text = $"<color=#9A9184>UNLOCKED</color>   {unlocked} / {all.Count}";
        countValue!.text = $"{unlocked} / {all.Count}";
        countFill!.sizeDelta = new Vector2(TextWidth * unlocked / Mathf.Max(1, all.Count), 6);
        InventoryGui gui = InventoryGui.instance;
        cheated!.text = gui && gui.m_achievementsCheatedText ? gui.m_achievementsCheatedText.text : "";
        ShowWall();
        ShowChosen(chosen);
    }

    private void ShowWall()
    {
        Ugui.Clear(listContent!);
        for (int i = 0; i < all.Count; i++)
        {
            Achievement achievement = all[i];
            bool lit = achievement.m_unlocked, selected = achievement == chosen;
            Rect r = new(20 + i % Columns * (Tile + Gap), i / Columns * (Tile + Gap), Tile, Tile);

            Image edge = Choice(listContent!, r, selected);
            Color rest = selected ? Palette.Gold : Palette.Hex(0x303631);
            // Unlocked: the badge itself. Locked: Valheim's locked badge, dimmed.
            Image icon = Ugui.Image(listContent!, "Icon", lit ? achievement.m_icon : achievement.m_iconLocked,
                lit ? Color.white : new Color(1, 1, 1, 0.35f)).At(r.x + 6, r.y + 6, Tile - 12, Tile - 12);
            icon.preserveAspect = true;

            if (!selected)
                Ugui.HitArea(listContent!, "Pick", () => { chosen = achievement; signature = 0; }).At(r)
                    .OnHover(on => edge.color = on ? Palette.Gold : rest);
        }
        int rows = (all.Count + Columns - 1) / Columns;
        listContent!.sizeDelta = new Vector2(0, rows * (Tile + Gap) + 8);
    }

    private void ShowChosen(Achievement achievement)
    {
        bool hidden = Hidden(achievement);
        title!.text = Localization.instance.Localize(hidden ? "$inventory_achievement_secret" : achievement.m_name);
        kind!.text = (achievement.m_isSecret ? "Secret · " : "") + (achievement.m_unlocked ? "Unlocked" : "Locked");
        kind.color = achievement.m_unlocked ? Palette.Gold : Palette.Muted;
        body!.text = Localization.instance.Localize(hidden ? "$inventory_achievement_secret_description" : achievement.m_description);

        List<(string Name, float Have, float Need)> rows = hidden || !achievement.m_clickable ? new() : Conditions(achievement);
        int met = rows.Count(c => c.Have >= c.Need);
        conditionsLabel!.text = rows.Count == 0 ? "" : $"CONDITIONS   <color=#EDE6D6>{met} / {rows.Count}</color>";
        ShowConditions(rows);

        Sprite? badge = icons.Get(achievement.m_unlocked ? achievement.m_icon : achievement.m_iconLocked);
        glyph!.sprite = badge ? badge : theme.Diamond;
    }

    // Each condition with the player's count towards it, read from the same stats Valheim's details show.
    private static List<(string Name, float Have, float Need)> Conditions(Achievement achievement)
    {
        List<(string, float, float)> rows = new();
        PlayerProfile? profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
        int difficulty = (int)achievement.m_difficultyRequirement;
        if (profile?.m_playerStats == null || difficulty < 0 || difficulty >= profile.m_playerStats.Length) return rows;
        PlayerProfile.PlayerStats stats = profile.m_playerStats[difficulty];

        foreach (Achievement.PlayerStatRequirement r in achievement.m_statTrigger)
        {
            stats.m_stats.TryGetValue(r.m_stat, out float have);
            rows.Add(("$stat_" + r.m_stat, have, r.m_amountAboveEquals));
        }
        foreach (Achievement.EnemyStatRequirement r in achievement.m_enemyStatsTriggers)
        {
            float have = 0;
            if ((int)r.m_modifier < stats.m_enemyStats.Length) stats.m_enemyStats[(int)r.m_modifier].TryGetValue(r.m_stat, out have);
            rows.Add((r.m_stat, have, r.m_amount));
        }
        foreach ((List<Achievement.DictStatRequirement> triggers, Dictionary<string, float> source) in new[]
                 {
                     (achievement.m_foodEatenTriggers, stats.m_foodEatenStats), (achievement.m_itemCraftTriggers, stats.m_itemCraftStats),
                     (achievement.m_itemPickupTriggers, stats.m_itemPickupStats), (achievement.m_knownCommandsTriggers, stats.m_knownCommands),
                     (achievement.m_knownWorldKeysTriggers, stats.m_knownWorldKeys), (achievement.m_knownWorldTriggers, stats.m_knownWorlds),
                     (achievement.m_pickableTriggers, stats.m_pickableStats), (achievement.m_piecePlacedTriggers, stats.m_piecesPlacedStats)
                 })
        {
            if (triggers == null || source == null) continue;
            foreach (Achievement.DictStatRequirement r in triggers)
            {
                source.TryGetValue(r.m_stat, out float have);
                rows.Add((r.m_stat, have, r.m_amount));
            }
        }
        return rows;
    }

    // Met: its name and count in gold, with a full bar. Not yet: ??? and an empty groove, as Valheim keeps it.
    private void ShowConditions(List<(string Name, float Have, float Need)> rows)
    {
        Ugui.Clear(conditions!);
        float y = 0, width = TextWidth - 10;
        foreach ((string name, float have, float need) in rows)
        {
            bool met = have >= need;
            string label = Localization.instance.Localize(name);
            if (string.IsNullOrEmpty(label)) label = name;
            Ugui.Image(conditions!, "Gem", theme.Diamond, met ? Palette.Gold : Palette.Faint).At(0, y + 9, 8, 8);
            Ugui.Text(conditions!, "Name", met ? label : "???", theme.Body, 14, met ? Palette.Bone : Palette.Faint).At(18, y, width - 130, 24);
            Ugui.Text(conditions!, "Count", met ? $"{have:0} / {need:0}" : "??? / ???", theme.Body, 13, met ? Palette.Gold : Palette.Faint,
                TextAlignmentOptions.MidlineRight).At(width - 120, y, 120, 24);
            Ugui.Image(conditions!, "Groove", theme.HudBar, Palette.Hex(0x303631)).At(18, y + 24, width - 18, 2);
            if (met) Ugui.Image(conditions!, "Fill", theme.White, Palette.GoldDim).At(18, y + 24, width - 18, 2);
            y += Line;
        }
        conditions!.sizeDelta = new Vector2(0, y);
        conditions.anchoredPosition = Vector2.zero;
    }
}
