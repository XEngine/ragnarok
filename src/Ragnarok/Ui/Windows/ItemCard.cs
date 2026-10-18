using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// Valheim's item tooltip in the Ragnarök style guide, opened beside the hovered slot: the name in Alegreya
// with the level diamond on the frame's corner, the description and kind, then each stat group as large
// rows (an icon, the figure in bold, its range quiet) over paired detail cells, status effects as their own
// blocks, notes in italics, and a footer with the durability rule and the weight. The vanilla text is sorted
// by its localization tokens, so every stat, mod-added line and translation still appears somewhere.
internal sealed class ItemCard
{
    internal const string Name = "Ragnarok Item Tooltip";
    private const float Width = 320, Pad = 14, Offset = 10, Big = 26, Cell = 22, CellGap = 6, Gap = 10;

    private static readonly Regex Tags = new("<.*?>", RegexOptions.Compiled);
    private static readonly Regex Token = new(@"^\$([a-z_]+)", RegexOptions.Compiled);
    private static readonly Regex Number = new(@"^\s*([-+]?[\d.,]+%?[a-z/]*)\s*(.*)$", RegexOptions.Compiled);

    private enum Group { Damage, Defence, Food, Other }

    // The figures that head a group, with their glyph and colour; the rest of a group become detail cells.
    private readonly Dictionary<string, (Sprite Glyph, Color Color)> heads;

    private static readonly HashSet<string> DamageTokens = new()
    {
        "item_knockback", "item_backstab", "item_staminause", "item_eitruse", "item_healthuse", "item_staminahold",
        "item_healthhitreturn", "item_eitrhitreturn", "item_damagemultipliertotal", "item_damagemultiplierhp"
    };

    private static readonly HashSet<string> DefenceTokens = new()
    {
        "item_armor", "item_blockarmor", "item_blockforce", "item_parrybonus", "item_parryadrenaline"
    };

    private static readonly HashSet<string> Skipped = new() { "item_onehanded", "item_twohanded", "item_quality" };

    private readonly Theme theme;
    private readonly RectTransform root, content;
    private readonly TextMeshProUGUI title, levelLabel;
    private readonly RectTransform level;
    private readonly TextMeshProUGUI levelText;
    private ItemDrop.ItemData? shown;
    private int shownStack, shownQuality;
    private float shownDurability;
    private bool shownEquipped;

    internal ItemCard(Theme theme, RectTransform parent)
    {
        this.theme = theme;
        heads = new Dictionary<string, (Sprite, Color)>
        {
            ["inventory_damage"] = (theme.GlyphSlash, Palette.GoldBright),
            ["inventory_blunt"] = (theme.GlyphBlunt, Palette.GoldBright),
            ["inventory_slash"] = (theme.GlyphSlash, Palette.GoldBright),
            ["inventory_pierce"] = (theme.GlyphPierce, Palette.GoldBright),
            ["inventory_chop"] = (theme.GlyphChop, Palette.GoldBright),
            ["inventory_pickaxe"] = (theme.GlyphPickaxe, Palette.GoldBright),
            ["inventory_fire"] = (theme.GlyphFire, Palette.Ember),
            ["inventory_frost"] = (theme.GlyphFrost, Palette.Eitr),
            ["inventory_lightning"] = (theme.GlyphLightning, Palette.Key),
            ["inventory_poison"] = (theme.GlyphPoison, Palette.Hex(0x8DBA55)),
            ["inventory_spirit"] = (theme.GlyphSpirit, Palette.Hex(0xD6D2F0)),
            ["item_armor"] = (theme.GlyphShield, Palette.GoldBright),
            ["item_blockarmor"] = (theme.GlyphShield, Palette.GoldBright),
            ["item_food_health"] = (theme.GlyphHealth, Palette.Alert),
            ["item_food_stamina"] = (theme.GlyphStamina, Palette.Stamina),
            ["item_food_eitr"] = (theme.GlyphEitr, Palette.Eitr)
        };

        root = Ugui.Node(parent, Name);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        Ugui.Image(root, "Frame", theme.Tooltip, Color.white).Stretch();

        title = Ugui.Text(root, "Name", "", theme.Heading, 21, Palette.Bone).At(Pad, 11, Width - Pad * 2, 30);
        levelLabel = Ugui.Text(root, "Level Label", "lvl.", theme.Subtle, 11, Palette.Faint, TextAlignmentOptions.MidlineRight)
            .At(Width - 78, 4, 34, 16);
        // The level sits in a diamond astride the frame's top edge, near its right corner.
        level = Ugui.Node(root, "Level");
        level.At(Width - 40, -13, 26, 26);
        Ugui.Image(level, "Rim", theme.Diamond, Palette.Gold).Stretch();
        Ugui.Image(level, "Core", theme.Diamond, Palette.Obsidian).Stretch(2, 2, 2, 2);
        levelText = Ugui.Text(level, "Number", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch();
        levelText.overflowMode = TextOverflowModes.Overflow;

        content = Ugui.Node(root, "Content");
        content.At(0, 44, Width, 0);
        root.gameObject.SetActive(false);
    }

    // The card draws above every other panel of the inventory screen (the info board, crafting), like a
    // high z-index: its own canvas, sorted above the screen's. Unity drops the override while the card is
    // hidden, so it is set each time the card opens.
    private void Raise()
    {
        if (!root.TryGetComponent(out Canvas canvas)) canvas = root.gameObject.AddComponent<Canvas>();
        Canvas? screen = root.parent ? root.parent.GetComponentInParent<Canvas>() : null;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = screen ? screen!.rootCanvas.sortingLayerID : 0;
        canvas.sortingOrder = (screen ? screen!.rootCanvas.sortingOrder : 0) + 100;
    }

    internal void Destroy()
    {
        if (root) Object.Destroy(root.gameObject);
    }

    // `slot` is the hovered slot's rect in the panel's top-left coordinates.
    internal void Show(ItemDrop.ItemData? item, Player player, Rect slot)
    {
        bool on = item != null;
        if (root.gameObject.activeSelf != on)
        {
            root.gameObject.SetActive(on);
            if (on) Raise();
        }
        if (item == null)
        {
            shown = null;
            return;
        }
        root.anchoredPosition = new Vector2(slot.xMax + Offset, -slot.y);
        if (item == shown && item.m_stack == shownStack && item.m_quality == shownQuality &&
            item.m_equipped == shownEquipped && Mathf.Approximately(item.m_durability, shownDurability)) return;
        shown = item;
        shownStack = item.m_stack;
        shownQuality = item.m_quality;
        shownEquipped = item.m_equipped;
        shownDurability = item.m_durability;
        Fill(item, player);
    }

    // Parsing --------------------------------------------------------------------------------

    private sealed class Line
    {
        internal string Token = "", Label = "", Value = "";
    }

    private sealed class Effect
    {
        internal string Name = "";
        internal readonly List<string> About = new();
        internal readonly List<Line> Rows = new();
    }

    private sealed class Parsed
    {
        internal readonly List<string> About = new();
        internal readonly List<string> Notes = new();
        internal bool NoTeleport;
        internal Line? Weight, Value, Durability;
        internal readonly Dictionary<Group, List<Line>> Rows = new();
        internal readonly List<Effect> Effects = new();
    }

    // The raw tooltip still has its $tokens and comes in blank-separated blocks: the description, the stat
    // lines, then effect paragraphs. An effect opens with its name in orange; Valheim puts a blank line
    // between an effect's description and its figures, so a block of figures joins the effect before it.
    private static Parsed Parse(string raw)
    {
        Parsed p = new();
        List<List<string>> blocks = new() { new List<string>() };
        foreach (string line in raw.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                if (blocks[blocks.Count - 1].Count > 0) blocks.Add(new List<string>());
                continue;
            }
            blocks[blocks.Count - 1].Add(trimmed);
        }

        for (int b = 0; b < blocks.Count; b++)
        {
            List<string> block = blocks[b];
            if (block.Count == 0) continue;
            if (b == 0)
            {
                foreach (string line in block) p.About.Add(Localization.instance.Localize(line));
                continue;
            }
            if (b == 1)
            {
                foreach (string line in block) Stat(p, Split(line));
                continue;
            }
            Effect? open = p.Effects.Count > 0 ? p.Effects[p.Effects.Count - 1] : null;
            Line first = Split(block[0]);
            bool named = block[0].StartsWith("<color=orange>") && first.Label.Length == 0 || first.Token == "item_seteffect";
            if (!named && open != null && open.Rows.Count == 0 && AllFigures(block))
            {
                foreach (string line in block) open.Rows.Add(Split(line));
                continue;
            }
            Effect effect = new();
            int start = 0;
            if (named)
            {
                effect.Name = Tags.Replace(first.Token == "item_seteffect" ? first.Label + ": " + first.Value : first.Value, "").Trim();
                start = 1;
            }
            for (int i = start; i < block.Count; i++)
            {
                Line line = Split(block[i]);
                if (line.Label.Length > 0) effect.Rows.Add(line);
                else effect.About.Add(Tags.Replace(line.Value, ""));
            }
            p.Effects.Add(effect);
        }
        return p;
    }

    private static bool AllFigures(List<string> block)
    {
        foreach (string line in block)
            if (Split(line).Label.Length == 0) return false;
        return true;
    }

    // "$token: value" into its parts, localized; a line without a colon is all value.
    private static Line Split(string raw)
    {
        string token = Token.Match(raw) is { Success: true } m ? m.Groups[1].Value : "";
        string localized = Localization.instance.Localize(raw);
        int colon = localized.IndexOf(':');
        if (colon <= 0) return new Line { Token = token, Value = localized };
        return new Line
        {
            Token = token,
            Label = Tags.Replace(localized.Substring(0, colon), "").Trim(),
            Value = localized.Substring(colon + 1).Trim()
        };
    }

    private static void Stat(Parsed p, Line line)
    {
        string token = line.Token;
        if (Skipped.Contains(token)) return;
        switch (token)
        {
            case "item_weight": p.Weight = line; return;
            case "item_value": p.Value = line; return;
            case "item_durability": p.Durability = line; return;
            case "item_noteleport": p.NoTeleport = true; return;
            case "item_repairlevel":
            case "item_crafter":
                p.Notes.Add(line.Label + ": " + Tags.Replace(line.Value, ""));
                return;
        }
        if (line.Label.Length == 0)
        {
            p.Notes.Add(Tags.Replace(line.Value, ""));
            return;
        }
        if (token.StartsWith("inventory_dmgmod"))
        {
            // "Resistant VS Fire" reads better as "Vs. Fire" against "Resistant".
            string plain = Tags.Replace(line.Value, "");
            int vs = plain.IndexOf(" VS ", System.StringComparison.OrdinalIgnoreCase);
            if (vs > 0) line = new Line { Token = token, Label = "Vs. " + plain.Substring(vs + 4).Trim(), Value = plain.Substring(0, vs).Trim() };
            Add(p, Group.Defence, line);
        }
        else if (DefenceTokens.Contains(token)) Add(p, Group.Defence, line);
        else if (token.StartsWith("inventory_") || DamageTokens.Contains(token)) Add(p, Group.Damage, line);
        else if (token.StartsWith("item_food")) Add(p, Group.Food, line);
        else Add(p, Group.Other, line);
    }

    private static void Add(Parsed p, Group group, Line line)
    {
        if (!p.Rows.TryGetValue(group, out var list)) p.Rows[group] = list = new();
        list.Add(line);
    }

    // Layout ---------------------------------------------------------------------------------

    private void Fill(ItemDrop.ItemData item, Player player)
    {
        ItemDrop.ItemData.SharedData shared = item.m_shared;
        title.text = Localization.instance.Localize(shared.m_name);
        bool levelled = shared.m_maxQuality > 1;
        level.gameObject.SetActive(levelled);
        levelLabel.gameObject.SetActive(levelled);
        if (levelled) levelText.text = item.m_quality.ToString();
        title.rectTransform.sizeDelta = new Vector2(Width - Pad * 2 - (levelled ? 54 : 0), 30);

        Parsed p = Parse(item.GetTooltip());
        Ugui.Clear(content);
        float w = Width - Pad * 2, y = 0;

        if (p.About.Count > 0)
            y = Paragraph(Restyle(string.Join(" ", p.About.ToArray())), theme.Body, 12.5f, Palette.Bone.WithAlpha(0.85f), Pad, w, y) + 6;
        string state = item.m_equipped ? "  ·  <color=#CDAE7C>Equipped</color>" : player.IsEquipActionQueued(item) ? "  ·  Equipping" : "";
        string kind = Kind(item);
        if (kind.Length > 0 || state.Length > 0)
        {
            Ugui.Text(content, "Kind", kind + state, theme.Subtle, 12, Palette.Faint).At(Pad, y, w, 18);
            y += 20;
        }

        bool stats = false;
        foreach (Group group in new[] { Group.Damage, Group.Defence, Group.Food, Group.Other })
        {
            if (!p.Rows.TryGetValue(group, out var rows) || rows.Count == 0) continue;
            y = Rule(y + 4) + 8;
            y = Rows(rows, w, y);
            stats = true;
        }
        foreach (Effect effect in p.Effects)
        {
            y = EffectBlock(effect, shared, w, y + (stats ? 2 : 6));
            stats = true;
        }

        if (p.Notes.Count > 0 || p.NoTeleport)
        {
            y = Rule(y + 4) + 8;
            foreach (string note in p.Notes)
                y = Paragraph(note, theme.Subtle, 12, Palette.Faint, Pad, w, y) + 2;
            if (p.NoTeleport)
            {
                Ugui.Image(content, "Alert", theme.GlyphAlert, Palette.Alert).At(Pad, y + 3, 13, 13);
                Ugui.Text(content, "No Teleport", Localization.instance.Localize("$item_noteleport"), theme.Subtle, 12, Palette.Alert)
                    .At(Pad + 19, y, w - 19, 18);
                y += 20;
            }
        }
        y = Footer(p, item, w, y + 6);
        root.sizeDelta = new Vector2(Width, 44 + y);
    }

    // Large rows head the group (damage kinds, armour, block, food values); the rest pair up into cells.
    private float Rows(List<Line> rows, float w, float y)
    {
        List<Line> cells = new();
        foreach (Line line in rows)
        {
            if (heads.TryGetValue(line.Token, out var head))
            {
                y = Cells(cells, w, y);
                y = Head(line, head.Glyph, head.Color, w, y);
            }
            else cells.Add(line);
        }
        return Cells(cells, w, y);
    }

    // Icon, label and figure in bold, its range or total small and quiet beside it.
    private float Head(Line line, Sprite glyph, Color color, float w, float y)
    {
        Ugui.Image(content, "Icon", glyph, color).At(Pad, y + 5, 16, 16);
        Ugui.Text(content, line.Label, line.Label, theme.Strong, 15, Palette.Bone).At(Pad + 24, y, w * 0.5f, Big);
        (string main, string rest) = Figure(line.Value);
        // Food values are coloured like their bar; vanilla's "(current: 95)" is left out.
        bool food = line.Token.StartsWith("item_food");
        string figure = food
            ? $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{main}</color>"
            : main + (rest.Length > 0 ? $" <size=86%><color=#{Hex(Palette.Muted)}>{rest}</color></size>" : "");
        TextMeshProUGUI value = Ugui.Text(content, "Value", figure, theme.Strong, 15, Palette.Bone, TextAlignmentOptions.MidlineRight)
            .At(Pad + w * 0.4f, y, w * 0.6f, Big);
        value.overflowMode = TextOverflowModes.Overflow;
        return y + Big + 4;
    }

    // Detail cells on moss bands, two to a row; a long label or the last odd one spans the row.
    private float Cells(List<Line> cells, float w, float y)
    {
        int i = 0;
        while (i < cells.Count)
        {
            bool pair = i + 1 < cells.Count && Short(cells[i]) && Short(cells[i + 1]);
            if (pair)
            {
                float half = (w - CellGap) / 2;
                CellAt(cells[i], Pad, half, y);
                CellAt(cells[i + 1], Pad + half + CellGap, half, y);
                i += 2;
            }
            else
            {
                CellAt(cells[i], Pad, w, y);
                i++;
            }
            y += Cell + 4;
        }
        cells.Clear();
        return y;
    }

    private static bool Short(Line line) => line.Label.Length <= 12 && Tags.Replace(line.Value, "").Length <= 8;

    private void CellAt(Line line, float x, float width, float y)
    {
        Image band = Ugui.Image(content, "Cell", theme.White, Palette.Moss.WithAlpha(0.45f)).At(x, y, width, Cell);
        Ugui.Text(band.transform, "Label", line.Label, theme.Body, 12, Palette.Muted).At(7, 0, width * 0.62f - 7, Cell);
        TextMeshProUGUI value = Ugui.Text(band.transform, "Value", Restyle(line.Value), theme.Body, 12, Palette.Bone,
            TextAlignmentOptions.MidlineRight).At(width * 0.35f, 0, width * 0.65f - 7, Cell);
        value.overflowMode = TextOverflowModes.Overflow;
    }

    // A status effect (a mead's, a cape's, a set's): a lighter band across the card with its icon and name,
    // its description, and its figures as cells.
    private float EffectBlock(Effect effect, ItemDrop.ItemData.SharedData shared, float w, float y)
    {
        float top = y;
        Image band = Ugui.Image(content, "Effect", theme.White, Palette.Moss.WithAlpha(0.22f));
        band.transform.SetAsLastSibling();
        y += 8;
        if (effect.Name.Length > 0)
        {
            Sprite? icon = EffectIcon(shared, effect.Name);
            Ugui.Image(content, "Icon", icon ? theme.Mono.Get(icon) ?? icon : theme.GlyphEffect, Palette.GoldBright)
                .At(Pad, y + 4, 17, 17).preserveAspect = true;
            Ugui.Text(content, "Name", effect.Name, theme.Strong, 15, Palette.Bone).At(Pad + 25, y, w - 25, Big);
            y += Big;
        }
        foreach (string about in effect.About)
            y = Paragraph(Restyle(about), theme.Body, 12, Palette.Muted, Pad, w, y) + 2;
        if (effect.Rows.Count > 0) y = Cells(new List<Line>(effect.Rows), w, y + 6);
        y += 6;
        band.At(0, top, Width, y - top);
        band.transform.SetAsFirstSibling();
        return y + 2;
    }

    private static Sprite? EffectIcon(ItemDrop.ItemData.SharedData shared, string name)
    {
        foreach (StatusEffect? effect in new[] { shared.m_consumeStatusEffect, shared.m_equipStatusEffect, shared.m_setStatusEffect })
            if (effect && effect!.m_icon && name.Contains(Localization.instance.Localize(effect.m_name))) return effect.m_icon;
        return null;
    }

    // The footer: the durability rule across the card (bright to the item's share, a diamond on the mark)
    // with the figure beneath; otherwise a hairline. Value on the left, weight on the right.
    private float Footer(Parsed p, ItemDrop.ItemData item, float w, float y)
    {
        if (p.Durability is { } durability)
        {
            float pct = Mathf.Clamp01(item.GetDurabilityPercentage());
            float mark = Width * pct;
            Ugui.Image(content, "Track", theme.White, Palette.Moss).At(0, y, Width, 1);
            Ugui.Image(content, "Worn", theme.White, pct < 0.25f ? Palette.Alert : Palette.Gold).At(0, y - 0.5f, mark, 2);
            Ugui.Image(content, "Mark", theme.Diamond, pct < 0.25f ? Palette.Alert : Palette.Gold).At(mark - 5, y - 4.5f, 10, 10);
            Ugui.Image(content, "Mark Core", theme.Diamond, Palette.Obsidian).At(mark - 3, y - 2.5f, 6, 6);
            y += 9;
            (string main, string rest) = Figure(durability.Value);
            Ugui.Text(content, "Durability",
                $"<color=#{Hex(Palette.Muted)}>{durability.Label}</color> {main}" + (rest.Length > 0 ? $" <color=#{Hex(Palette.Muted)}>{rest}</color>" : ""),
                theme.Body, 12, Palette.Bone).At(Pad, y, w * 0.68f, 22);
        }
        else
        {
            Ugui.Image(content, "Rule", theme.White, Palette.Moss).At(0, y, Width, 1);
            y += 9;
            if (p.Value is { } value)
            {
                Ugui.Image(content, "Coin", theme.GlyphCoin, Palette.Muted).At(Pad, y + 5, 12, 12);
                Ugui.Text(content, "Value", Tags.Replace(value.Value, ""), theme.Body, 12, Palette.Muted).At(Pad + 17, y, w * 0.5f, 22);
            }
        }
        if (p.Weight is { } weight)
        {
            string text = Tags.Replace(weight.Value, "");
            TextMeshProUGUI label = Ugui.Text(content, "Weight", text, theme.Body, 12, Palette.Muted, TextAlignmentOptions.MidlineRight)
                .At(Pad + w * 0.4f, y, w * 0.6f, 22);
            float textWidth = label.GetPreferredValues(text).x;
            Ugui.Image(content, "Weight Icon", theme.GlyphWeight, Palette.Muted).At(Pad + w - textWidth - 17, y + 5, 12, 12);
        }
        return y + 28;
    }

    private float Rule(float y)
    {
        Ugui.Image(content, "Rule", theme.White, Palette.Moss).At(Pad, y, Width - Pad * 2, 1);
        return y + 1;
    }

    private float Paragraph(string text, TMP_FontAsset? font, float size, Color color, float x, float w, float y)
    {
        TextMeshProUGUI label = Ugui.Text(content, "Text", text, font, size, color, TextAlignmentOptions.TopLeft, wrap: true);
        label.overflowMode = TextOverflowModes.Overflow;
        float h = label.GetPreferredValues(label.text, w, 0).y;
        label.At(x, y, w, h);
        return y + h;
    }

    // "15 (9-14)" into "15" and "(9-14)"; "80  (Current: 95)" keeps only "80".
    private static (string Main, string Tail) Figure(string value)
    {
        string plain = Tags.Replace(value, "").Trim();
        Match m = Number.Match(plain);
        if (!m.Success) return (plain, "");
        string rest = m.Groups[2].Value.Trim();
        return (m.Groups[1].Value, rest);
    }

    private static string Hex(Color color) => ColorUtility.ToHtmlStringRGBA(color);

    // Vanilla colours mapped to the palette: main values bone, ranges and totals muted.
    private static string Restyle(string text) => text
        .Replace("<color=orange>", "<color=#D7CBB9>")
        .Replace("<color=yellow>", "<color=#D7CBB999>")
        .Replace("<color=red>", "<color=#D6241C>")
        .Replace("<color=#00FFFF>", "<color=#5DA9D6>")
        .Replace("<color=#ff8080ff>", "<color=#D6241C>")
        .Replace("<color=#ffff80ff>", "<color=#CDAE7C>")
        .Replace("<color=#9090ffff>", "<color=#5DA9D6>");

    // "One-handed sword", "Two-handed tool", "Helmet"...: handedness with the weapon's skill.
    private static string Kind(ItemDrop.ItemData item)
    {
        ItemDrop.ItemData.SharedData shared = item.m_shared;
        string skill = shared.m_skillType switch
        {
            Skills.SkillType.Swords => "sword",
            Skills.SkillType.Axes => "axe",
            Skills.SkillType.Clubs => "club",
            Skills.SkillType.Knives => "knife",
            Skills.SkillType.Spears => "spear",
            Skills.SkillType.Polearms => "atgeir",
            Skills.SkillType.Bows => "bow",
            Skills.SkillType.Crossbows => "crossbow",
            Skills.SkillType.Pickaxes => "pickaxe",
            Skills.SkillType.ElementalMagic or Skills.SkillType.BloodMagic => "staff",
            Skills.SkillType.Unarmed => "fist weapon",
            _ => "weapon"
        };
        return shared.m_itemType switch
        {
            ItemDrop.ItemData.ItemType.OneHandedWeapon => "One-handed " + skill,
            ItemDrop.ItemData.ItemType.TwoHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft => "Two-handed " + skill,
            ItemDrop.ItemData.ItemType.Bow => skill == "crossbow" ? "Crossbow" : "Bow",
            ItemDrop.ItemData.ItemType.Shield => "Shield",
            ItemDrop.ItemData.ItemType.Helmet => "Helmet",
            ItemDrop.ItemData.ItemType.Chest => "Chest armour",
            ItemDrop.ItemData.ItemType.Legs => "Leg armour",
            ItemDrop.ItemData.ItemType.Shoulder => "Cape",
            ItemDrop.ItemData.ItemType.Utility => "Utility",
            ItemDrop.ItemData.ItemType.Trinket => "Trinket",
            ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable => "Ammunition",
            ItemDrop.ItemData.ItemType.Consumable => shared.m_food > 0 || shared.m_foodStamina > 0 || shared.m_foodEitr > 0 ? "Food" : "Consumable",
            ItemDrop.ItemData.ItemType.Tool => "Two-handed tool",
            ItemDrop.ItemData.ItemType.Torch => "One-handed torch",
            ItemDrop.ItemData.ItemType.Trophy => "Trophy",
            ItemDrop.ItemData.ItemType.Fish => "Fish",
            _ => ""
        };
    }
}
