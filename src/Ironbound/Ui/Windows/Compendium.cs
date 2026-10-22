using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Ironbound.UI.Windows;

// The Ironbound Compendium, drawn over Valheim's texts dialog: chapters on the left, the open one expanded
// and the rest fading away; the entry in the middle; the art panel shows the boss the entry speaks of.
internal sealed class Compendium : BookWindow
{
    private const float ListWidth = 330, TextX = 470, TextWidth = 470;

    private static readonly AccessTools.FieldRef<TextsDialog, List<TextsDialog.TextInfo>> Texts =
        AccessTools.FieldRefAccess<TextsDialog, List<TextsDialog.TextInfo>>("m_texts");

    private static readonly (string Word, string Power)[] Bosses =
    {
        ("eikthyr", "GP_Eikthyr"), ("elder", "GP_TheElder"), ("bonemass", "GP_Bonemass"), ("moder", "GP_Moder"), ("yagluth", "GP_Yagluth")
    };

    private sealed class Entry
    {
        internal string Topic = "", Body = "";
    }

    private sealed class Chapter
    {
        internal string Name = "";
        internal readonly List<Entry> Entries = new();
    }

    private readonly List<Chapter> chapters = new();
    private RectTransform? listContent, bodyContent;
    private TextMeshProUGUI? title, body;
    private TextsDialog? texts;
    private int openChapter, openEntry;

    internal Compendium(InterfaceSettings settings, Theme theme) : base(settings, theme) { }

    protected override string Name => "Compendium";
    protected override Tab Selected => Tab.Compendium;

    protected override bool Wanted => settings.CompendiumStyle.Value switch
    {
        PanelStyle.Ironbound => true,
        PanelStyle.Vanilla => false,
        _ => settings.Hud.Value == HudStyle.Ironbound
    };

    protected override Component? Dialog(InventoryGui gui) => gui.m_textsDialog;

    protected override void CloseDialog()
    {
        if (texts) texts!.OnClose();
    }

    protected override void Build(InventoryGui gui)
    {
        texts = gui.m_textsDialog;
        Collect(texts);

        // Chapters scroll on the left; the entry scrolls in the middle.
        Scroller(new Rect(Left - 20, Top, ListWidth + 20, Height - Top - 110), out listContent);

        title = Ugui.Text(window!, "Title", "", theme.Heading, 28, Palette.Bone).At(TextX, Top - 6, TextWidth, 44);
        Scroller(new Rect(TextX, Top + 60, TextWidth + 20, Height - Top - 130), out bodyContent);
        body = Ugui.Text(bodyContent, "Body", "", theme.Body, 15, Palette.Bone, TextAlignmentOptions.TopLeft, wrap: true);
        body.rectTransform.anchorMin = new Vector2(0, 1);
        body.rectTransform.anchorMax = new Vector2(1, 1);
        body.rectTransform.pivot = new Vector2(0, 1);
        body.rectTransform.offsetMin = new Vector2(0, 0);
        body.rectTransform.offsetMax = new Vector2(-20, 0);
        body.lineSpacing = 6;
        body.paragraphSpacing = 14;
        body.overflowMode = TextOverflowModes.Overflow;

        openChapter = 0;
        openEntry = 0;
        ShowChapters();
        ShowEntry();
    }

    // Valheim's list: active effects and the log first, then known texts (A-Z), then stats last.
    private void Collect(TextsDialog dialog)
    {
        chapters.Clear();
        List<TextsDialog.TextInfo> all = Texts(dialog);
        Dictionary<string, string> keys = new();
        foreach (KeyValuePair<string, string> known in Player.m_localPlayer.GetKnownTexts())
            keys[Localization.instance.Localize(known.Key.Replace("\u0016", ""))] = known.Key.ToLowerInvariant();

        Chapter journal = new() { Name = "Journal" }, hugin = new() { Name = "Hugin's Lessons" },
            stones = new() { Name = "Runestones" }, notes = new() { Name = "Notes" };
        foreach (TextsDialog.TextInfo info in all)
        {
            Entry entry = new() { Topic = info.m_topic, Body = Restyle(info.m_text) };
            if (!keys.TryGetValue(info.m_topic, out string key)) journal.Entries.Add(entry);
            else if (key.Contains("tutorial")) hugin.Entries.Add(entry);
            else if (key.Contains("lore") || key.Contains("rune")) stones.Entries.Add(entry);
            else notes.Entries.Add(entry);
        }
        foreach (Chapter c in new[] { journal, hugin, stones, notes })
            if (c.Entries.Count > 0) chapters.Add(c);
    }

    private static string Restyle(string text) => text
        .Replace("<color=orange>", "<color=#CDAE7C>").Replace("<color=\"orange\">", "<color=#CDAE7C>")
        .Replace("<color=yellow>", "<color=#EADFC0>").Replace("<color=\"yellow\">", "<color=#EADFC0>");

    // Chapters top to bottom: the open one large and white with its entries under it, the rest fading.
    private void ShowChapters()
    {
        Ugui.Clear(listContent!);
        float y = 0;
        for (int c = 0; c < chapters.Count; c++)
        {
            int index = c;
            bool open = c == openChapter;
            Color color = open ? Palette.Gold : Palette.Bone;
            Choice(listContent!, new Rect(8, y, ListWidth - 8, 38), open);
            TextMeshProUGUI heading = Ugui.Text(listContent!, chapters[c].Name, chapters[c].Name, theme.Strong,
                15, color).At(20, y, ListWidth - 24, 38);
            if (!open)
                Ugui.HitArea(listContent!, "Open " + chapters[c].Name, () => { openChapter = index; openEntry = 0; ShowChapters(); ShowEntry(); })
                    .At(8, y, ListWidth - 8, 38).OnHover(on => heading.color = on ? Palette.GoldBright : color);
            y += 44;
            if (!open) continue;

            for (int e = 0; e < chapters[c].Entries.Count; e++)
            {
                int entry = e;
                bool selected = e == openEntry;
                Choice(listContent!, new Rect(20, y, ListWidth - 20, 28), selected);
                TextMeshProUGUI name = Ugui.Text(listContent!, "Entry", chapters[c].Entries[e].Topic, theme.Strong, 14,
                    selected ? Palette.Gold : Palette.Bone).At(30, y, ListWidth - 36, 28);
                if (!selected)
                    Ugui.HitArea(listContent!, "Pick", () => { openEntry = entry; ShowChapters(); ShowEntry(); }).At(20, y, ListWidth - 20, 28)
                        .OnHover(on => name.color = on ? Palette.Gold : Palette.Bone);
                y += 32;
            }
            y += 14;
        }
        listContent!.sizeDelta = new Vector2(0, y);
    }

    private void ShowEntry()
    {
        Entry? entry = chapters.Count == 0 ? null : chapters[openChapter].Entries.ElementAtOrDefault(openEntry);
        title!.text = entry?.Topic ?? "";
        body!.text = entry?.Body ?? "";
        body.ForceMeshUpdate();
        bodyContent!.sizeDelta = new Vector2(0, body.preferredHeight + 20);
        bodyContent.anchoredPosition = Vector2.zero;

        // The boss the entry speaks of, else your own Forsaken power, else a great rune diamond.
        string text = ((entry?.Topic ?? "") + " " + (entry?.Body ?? "")).ToLowerInvariant();
        string? power = Bosses.FirstOrDefault(b => text.Contains(b.Word)).Power;
        if (power == null && Player.m_localPlayer)
        {
            Player.m_localPlayer.GetGuardianPowerHUD(out StatusEffect se, out _);
            if (se) power = se.name;
        }
        Sprite? sprite = theme.PowerGlyph(power);
        glyph!.sprite = sprite ? sprite : theme.Diamond;
    }
}
