using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Ragnarok.UI.Windows;

// The Ragnarök Skills screen, drawn over Valheim's skills dialog in the Compendium's frame: every skill
// on the left with its level and a thin mastery line, the chosen one in the middle (its level large,
// bonuses from gear and food, progress to the next level, mastery towards 100, its description), and its
// glyph in the art panel.
internal sealed class SkillsBook : BookWindow
{
    private const float ListWidth = 330, TextX = 470, TextWidth = 470, Row = 48;

    private readonly MonochromeIcons icons = new();
    private SkillsDialog? dialog;
    private RectTransform? listContent;
    private TextMeshProUGUI? total, title, level, bonus, nextValue, description;
    private RectTransform? nextFill, masteryFill, bonusFill;
    private Skills.SkillType chosen = Skills.SkillType.None;
    private int signature;

    internal SkillsBook(InterfaceSettings settings, Theme theme) : base(settings, theme) { }

    protected override string Name => "Skills";
    protected override Tab Selected => Tab.Skills;

    protected override bool Wanted => settings.SkillsStyle.Value switch
    {
        PanelStyle.Ragnarok => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ragnarok
    };

    protected override Component? Dialog(InventoryGui gui) => gui.m_skillsDialog;

    protected override void CloseDialog()
    {
        if (dialog) dialog!.OnClose();
    }

    public override void Dispose()
    {
        base.Dispose();
        icons.Dispose();
    }

    protected override void Build(InventoryGui gui)
    {
        dialog = gui.m_skillsDialog;
        total = Ugui.Text(window!, "Total", "", theme.Strong, 14, Palette.Bone).At(Left, 100, ListWidth, 26);

        Scroller(new Rect(Left - 20, Top, ListWidth + 20, Height - Top - 110), out listContent);
        Ugui.Image(window!, "Column Rule", theme.White, Palette.Gold.WithAlpha(0.45f)).At(TextX - 36, Top, 1, Height - Top - 60);

        Ugui.Image(window!, "Title Gem", theme.Diamond, Palette.Gold).At(TextX, Top + 14, 12, 12);
        title = Ugui.Text(window!, "Title", "", theme.Heading, 32, Palette.Gold).At(TextX + 24, Top - 10, TextWidth, 50);

        // The level, large, with the bonus from gear and food beside it.
        level = Ugui.Text(window!, "Level", "", theme.Display, 76, Palette.Bone).At(TextX, Top + 38, 160, 112);
        level.overflowMode = TextOverflowModes.Overflow;
        bonus = Ugui.Text(window!, "Bonus", "", theme.Display, 28, Palette.Gold).At(TextX + 130, Top + 63, 200, 42);
        Ugui.Text(window!, "Level Label", "level", theme.Section, 16, Palette.Muted).At(TextX + 4, Top + 136 - 4, 160, 24);

        // Progress to the next level, then mastery towards 100 (the bonus shown past the trained level).
        float y = Top + 178;
        Ugui.Text(window!, "Next Label", "next level", theme.Section, 16, Palette.Muted).At(TextX, y - 4, 200, 24);
        nextValue = Ugui.Text(window!, "Next Value", "", theme.Strong, 14, Palette.Bone, TextAlignmentOptions.MidlineRight)
            .At(TextX + TextWidth - 120, y, 120, 18);
        nextFill = Bar(y + 24, out _);
        y += 52;
        Ugui.Text(window!, "Mastery Label", "mastery", theme.Section, 16, Palette.Muted).At(TextX, y - 4, 200, 24);
        masteryFill = Bar(y + 24, out bonusFill);

        description = Ugui.Text(window!, "Description", "", theme.Body, 15, Palette.Muted, TextAlignmentOptions.TopLeft, wrap: true)
            .At(TextX, y + 64, TextWidth, Height - y - 64 - 90);
        description.lineSpacing = 12;
        description.overflowMode = TextOverflowModes.Overflow;

        signature = 0;
        Refresh();
    }

    // A thin gold bar across the column, in a dark groove; returns its fill (and a dimmer fill behind it).
    private RectTransform Bar(float y, out RectTransform under)
    {
        Ugui.Image(window!, "Groove", theme.White, Palette.Ink).At(TextX, y, TextWidth, 6);
        under = Ugui.Image(window!, "Under", theme.White, Palette.GoldDim.WithAlpha(0.6f)).At(TextX, y, 0, 6).rectTransform;
        return Ugui.Image(window!, "Fill", theme.White, Palette.Gold).At(TextX, y, 0, 6).rectTransform;
    }

    // Rebuilt when a level, its progress or the choice changes.
    protected override void Refresh()
    {
        if (!listContent || !Player.m_localPlayer) return;
        Skills skills = Player.m_localPlayer.GetSkills();
        List<Skills.Skill> list = skills.GetSkillList();
        if (list.Count == 0) return;
        if (list.All(s => s.m_info.m_skill != chosen)) chosen = list[0].m_info.m_skill;

        int now = (int)chosen;
        foreach (Skills.Skill s in list)
            now = now * 31 + Mathf.RoundToInt(skills.GetSkillLevel(s.m_info.m_skill) * 10) * 1000 + Mathf.RoundToInt(s.GetLevelPercentage() * 100);
        if (now == signature) return;
        signature = now;

        total!.text = $"<color=#9A9184>TOTAL</color>   {skills.GetTotalSkill():0} / {skills.GetTotalSkillCap():0}";
        ShowList(skills, list);
        ShowChosen(skills, list.First(s => s.m_info.m_skill == chosen));
    }

    private static string SkillName(Skills.Skill skill) =>
        Localization.instance.Localize("$skill_" + skill.m_info.m_skill.ToString().ToLower());

    private void ShowList(Skills skills, List<Skills.Skill> list)
    {
        Ugui.Clear(listContent!);
        float y = 0, lineWidth = ListWidth - 62;
        foreach (Skills.Skill skill in list)
        {
            Skills.SkillType type = skill.m_info.m_skill;
            bool selected = type == chosen;
            if (selected) Ugui.Image(listContent!, "Open Mark", theme.White, Palette.GoldBright).At(0, y + 9, 3, 30);
            Ugui.Image(listContent!, "Icon", icons.Get(skill.m_info.m_icon) ?? theme.Diamond, selected ? Palette.Bone : Palette.Muted)
                .At(20, y + 10, 28, 28).preserveAspect = true;
            TextMeshProUGUI name = Ugui.Text(listContent!, "Name", SkillName(skill), theme.Strong, 14,
                selected ? Palette.Bone : Palette.Muted).At(58, y + 6, lineWidth - 40, 24);
            Ugui.Text(listContent!, "Level", Mathf.FloorToInt(skills.GetSkillLevel(type)).ToString(), theme.Body, 22,
                selected ? Palette.Gold : Palette.Muted, TextAlignmentOptions.MidlineRight).At(ListWidth - 70, y + 4, 66, 28);
            Ugui.Image(listContent!, "Line", theme.White, Palette.Ink).At(58, y + 34, lineWidth, 2);
            Ugui.Image(listContent!, "Line Fill", theme.White, selected ? Palette.Gold : Palette.GoldDim)
                .At(58, y + 34, lineWidth * Mathf.Clamp01(skill.m_level / 100f), 2);
            if (!selected)
                Ugui.HitArea(listContent!, "Pick", () => { chosen = type; signature = 0; }).At(0, y, ListWidth, Row)
                    .OnHover(on => name.color = on ? Palette.GoldBright : Palette.Muted);
            y += Row;
        }
        listContent!.sizeDelta = new Vector2(0, y);
    }

    private void ShowChosen(Skills skills, Skills.Skill skill)
    {
        float effective = skills.GetSkillLevel(skill.m_info.m_skill), trained = Mathf.Floor(skill.m_level);
        title!.text = SkillName(skill);
        level!.text = Mathf.FloorToInt(trained).ToString();
        float extra = Mathf.Floor(effective) - trained;
        bonus!.text = Mathf.Abs(extra) >= 1 ? extra.ToString("+0;-0") : "";
        bonus.color = extra < 0 ? Palette.Alert : Palette.Gold;
        level.ForceMeshUpdate();
        bonus.rectTransform.anchoredPosition = new Vector2(TextX + level.preferredWidth + 12, -(Top + 66));

        bool maxed = skill.m_level >= 100f;
        float next = maxed ? 1f : skill.GetLevelPercentage();
        nextValue!.text = maxed ? "Mastered" : $"{next * 100f:0}%";
        nextFill!.sizeDelta = new Vector2(TextWidth * next, 6);
        masteryFill!.sizeDelta = new Vector2(TextWidth * Mathf.Clamp01(skill.m_level / 100f), 6);
        bonusFill!.sizeDelta = new Vector2(TextWidth * Mathf.Clamp01(Mathf.Max(effective, skill.m_level) / 100f), 6);
        description!.text = Localization.instance.Localize(skill.m_info.m_description);

        Sprite? icon = icons.Get(skill.m_info.m_icon);
        glyph!.sprite = icon ? icon : theme.Diamond;
    }
}
