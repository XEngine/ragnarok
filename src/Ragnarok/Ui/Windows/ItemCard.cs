using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// Reference tooltip: rounded gold outline, title/kind, italic description, plain
// label/value rows and weight/quality footer. Vanilla supplies every stat and effect.
internal sealed class ItemCard
{
    internal const string Name = "Ragnarok Item Tooltip";
    private const float Width = 320, Pad = 16, Offset = 16, HoverDelay = 0.25f;

    private static readonly Regex Tags = new("<.*?>", RegexOptions.Compiled);
    private static readonly Regex Token = new(@"^\$([a-z_]+)", RegexOptions.Compiled);

    private enum Group { Damage, Defence, Food, Other }

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
    private readonly TextMeshProUGUI title, kind;
    private readonly Vector3[] corners = new Vector3[4];
    private ItemDrop.ItemData? shown;
    private int shownStack, shownQuality;
    private float shownDurability;
    private bool shownEquipped;
    private string? shownDetails;
    private string shownDetailsHeading = "", shownDetailsCaption = "";
    private ItemDrop.ItemData? hovered;
    private float hoveredSince, detailsSince;
    private bool detailsPending;
    private readonly Image[] stars = new Image[8];
    private ItemDrop.ItemData? shownComparison;
    private bool comparing;
    private int comparisonQuality;
    private Parsed? comparisonStats;
    private static readonly Regex Figure = new(@"^([+-]?\d+(?:[.,]\d+)?)\s*(%|/s)?(?:\s*\([^)]*\))?$");
    private static readonly HashSet<string> DamageFigures = new()
    {
        "inventory_damage", "inventory_blunt", "inventory_slash", "inventory_pierce", "inventory_chop", "inventory_pickaxe",
        "inventory_fire", "inventory_frost", "inventory_lightning", "inventory_poison", "inventory_spirit"
    };

    internal ItemCard(Theme theme, RectTransform parent)
    {
        this.theme = theme;
        root = Ugui.Node(parent, Name);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        Ugui.Image(root, "Frame", theme.PackSurface, Palette.Hex(0x0C0F11, 0.98f)).Stretch();
        Ugui.Image(root, "Outline", theme.PackEdge, InventoryCategories.Gold).Stretch();
        title = Ugui.Text(root, "Name", "", theme.Heading, 21, InventoryCategories.Gold).At(Pad, 12, Width - Pad * 2, 31);
        title.enableAutoSizing = true;
        title.fontSizeMin = 15;
        title.fontSizeMax = 21;
        kind = Ugui.Text(root, "Kind", "", theme.Strong, 10.5f, InventoryCategories.Material, spacing: 1.8f)
            .At(Pad, 44, Width - Pad * 2, 19);
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i] = Ugui.Image(root, "Quality star " + i, theme.HudStar, InventoryCategories.Gold).At(0, 22, 8, 8);
            stars[i].enabled = false;
        }
        content = Ugui.Node(root, "Content").At(0, 72, Width, 0);
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
        if (root)
        {
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
        }
    }

    // Non-item details share the exact frame, fonts, spacing and screen fitting
    // of item cards. The caller owns this card under its own UI hierarchy.
    internal void ShowDetails(string? body, Rect area, string heading = "Set Bonus", string caption = "EQUIPMENT · SET EFFECTS")
    {
        if (!root) return;
        bool visible = !string.IsNullOrEmpty(body);
        if (!visible) detailsPending = false;
        else if (!detailsPending) { detailsPending = true; detailsSince = Time.unscaledTime; }
        visible &= Time.unscaledTime - detailsSince >= HoverDelay;
        if (root.gameObject.activeSelf != visible)
        {
            root.gameObject.SetActive(visible);
            if (visible) Raise();
        }
        if (!visible) return;
        if (shownDetails != body || shownDetailsHeading != heading || shownDetailsCaption != caption)
        {
            shownDetails = body;
            shownDetailsHeading = heading;
            shownDetailsCaption = caption;
            title.text = heading;
            title.rectTransform.sizeDelta = new Vector2(Width - Pad * 2, 31);
            foreach (Image star in stars) star.enabled = false;
            kind.text = caption;
            Ugui.Clear(content);
            float height = Paragraph(Restyle(body!), theme.Body, 13, Palette.Bone, Pad, Width - Pad * 2, 0);
            root.sizeDelta = new Vector2(Width, 72 + height + Pad);
        }
        Position(area);
    }

    // `slot` is the hovered slot's rect in the panel's top-left coordinates.
    internal void Show(ItemDrop.ItemData? item, Player player, Rect slot)
    {
        if (hovered != item) { hovered = item; hoveredSince = Time.unscaledTime; }
        bool on = item != null && Time.unscaledTime - hoveredSince >= HoverDelay;
        if (root.gameObject.activeSelf != on)
        {
            root.gameObject.SetActive(on);
            if (on) Raise();
        }
        if (item == null || !on)
        {
            shown = null;
            return;
        }
        ItemDrop.ItemData? equipped = Comparison(player, item);
        bool compare = equipped != null && (ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift));
        if (item != shown || item.m_stack != shownStack || item.m_quality != shownQuality || equipped != shownComparison || compare != comparing ||
            (equipped?.m_quality ?? 0) != comparisonQuality ||
            item.m_equipped != shownEquipped || !Mathf.Approximately(item.m_durability, shownDurability))
        {
            shown = item;
            shownStack = item.m_stack;
            shownQuality = item.m_quality;
            shownEquipped = item.m_equipped;
            shownDurability = item.m_durability;
            shownComparison = equipped;
            comparisonQuality = equipped?.m_quality ?? 0;
            comparing = compare;
            Fill(item, player);
        }
        Position(slot);
    }

    private void Position(Rect anchor)
    {
        if ((!ZInput.IsGamepadActive() || ZInput.IsMouseActive()) && root.parent is RectTransform parent)
        {
            Canvas? canvas = parent.GetComponentInParent<Canvas>();
            Camera? camera = canvas && canvas!.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, ZInput.pointerPosition, camera, out Vector2 point))
                anchor = new Rect(point.x - parent.rect.xMin, parent.rect.yMax - point.y, 0, 0);
        }
        root.anchoredPosition = new Vector2(anchor.xMax + Offset, -anchor.y);
        FitOnScreen(anchor);
    }

    private Rect Bounds(RectTransform rect, Transform space)
    {
        rect.GetWorldCorners(corners);
        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (Vector3 corner in corners)
        {
            Vector2 p = space.InverseTransformPoint(corner);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void FitOnScreen(Rect slot)
    {
        Canvas? canvas = root.parent.GetComponentInParent<Canvas>();
        if (!canvas || canvas!.rootCanvas.transform is not RectTransform screen) return;
        Rect limit = Bounds(screen, root.parent);
        root.localScale = Vector3.one;
        float scale = Mathf.Min(1, (limit.height - 16) / root.rect.height, (limit.width - 16) / Width);
        root.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        Rect bounds = Bounds(root, root.parent);
        if (bounds.xMax > limit.xMax - 8)
        {
            root.anchoredPosition = new Vector2(slot.xMin - Width * root.localScale.x - Offset, -slot.y);
            bounds = Bounds(root, root.parent);
        }
        float x = Mathf.Max(0, limit.xMin + 8 - bounds.xMin) - Mathf.Max(0, bounds.xMax - limit.xMax + 8);
        float y = Mathf.Max(0, limit.yMin + 8 - bounds.yMin) - Mathf.Max(0, bounds.yMax - limit.yMax + 8);
        root.anchoredPosition += new Vector2(x, y);
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
        comparisonStats = comparing && shownComparison != null ? Parse(shownComparison.GetTooltip()) : null;
        title.text = Localization.instance.Localize(item.m_shared.m_name);
        int starCount = item.m_shared.m_maxQuality > 1 ? Mathf.Min(stars.Length, item.m_shared.m_maxQuality) : 0;
        for (int i = 0; i < stars.Length; i++)
        {
            stars[i].enabled = i < starCount;
            if (i < starCount)
            {
                stars[i].At(Width - Pad - (starCount - i) * 10, 22, 8, 8);
                stars[i].color = i < item.m_quality ? InventoryCategories.Gold : Palette.Hex(0x4D5551);
            }
        }
        title.rectTransform.sizeDelta = new Vector2(Width - Pad * 2 - (starCount > 0 ? starCount * 10 + 8 : 0), 31);
        string category = Kind(item).ToUpperInvariant();
        if (item.m_shared.m_skillType is Skills.SkillType.Axes or Skills.SkillType.Pickaxes)
            category = "WEAPON · TOOL";
        string state = item.m_equipped ? " · EQUIPPED" : player.IsEquipActionQueued(item) ? " · EQUIPPING" : "";
        kind.text = category + state;
        Parsed p = Parse(item.GetTooltip());
        Ugui.Clear(content);
        float w = Width - Pad * 2, y = 0;
        if (p.About.Count > 0)
            y = Paragraph(Restyle(string.Join(" ", p.About.ToArray())), theme.Subtle, 13, Palette.Bone, Pad, w, y) + 12;
        foreach (Group group in new[] { Group.Damage, Group.Defence, Group.Food, Group.Other })
            if (p.Rows.TryGetValue(group, out var rows))
                foreach (Line line in rows) y = Row(line, w, y);
        foreach (Effect effect in p.Effects)
        {
            y += 6;
            if (effect.Name.Length > 0)
                y = Paragraph(effect.Name, theme.Strong, 12, InventoryCategories.Gold, Pad, w, y) + 3;
            foreach (string about in effect.About)
                y = Paragraph(Restyle(about), theme.Subtle, 12, Palette.Muted, Pad, w, y) + 3;
            foreach (Line line in effect.Rows) y = Row(line, w, y);
        }
        if (p.Durability is { } durability) y = Row(durability, w, y);
        foreach (string note in p.Notes)
            y = Paragraph(note, theme.Subtle, 11.5f, InventoryCategories.Material, Pad, w, y + 3);
        if (p.NoTeleport)
            y = Paragraph(Localization.instance.Localize("$item_noteleport"), theme.Subtle, 12, Palette.Health, Pad, w, y + 4);
        y += 10;
        Ugui.Image(content, "Rule", theme.White, Palette.Hex(0x303531)).At(Pad, y, w, 0.75f);
        y += 10;
        List<string> footer = new();
        if (p.Weight is { } weight) footer.Add(weight.Label + " " + Tags.Replace(weight.Value, ""));
        if (item.m_shared.m_maxQuality > 1)
            footer.Add(Localization.instance.Localize("$item_quality") + " " + item.m_quality);
        if (p.Value is { } value) footer.Add(value.Label + " " + Tags.Replace(value.Value, ""));
        y = Paragraph(string.Join(" · ", footer.ToArray()), theme.Body, 11.5f, InventoryCategories.Material, Pad, w, y);
        if (shownComparison != null)
            y = Paragraph("vs " + Localization.instance.Localize(shownComparison.m_shared.m_name), theme.Body, 10.5f,
                InventoryCategories.Material, Pad, w, y + 4);
        if (item.IsEquipable() || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
        {
            string action = item.m_equipped ? "Unequip" : item.IsEquipable() ? "Equip" : "Use";
            string hints = "<color=#FFFFFF><b>RMB</b></color> " + action;
            if (shownComparison != null) hints += "    <color=#FFFFFF><b>SHIFT</b></color> Compare";
            y = Paragraph(hints, theme.Body, 10.5f, InventoryCategories.Material, Pad, w, y + 6);
        }
        root.sizeDelta = new Vector2(Width, 72 + y + Pad);
    }

    private float Row(Line line, float width, float y)
    {
        float leftWidth = width * 0.46f, rightWidth = width * 0.51f;
        TextMeshProUGUI label = Ugui.Text(content, "Label", line.Label, theme.Body, 13, Palette.Bone,
            TextAlignmentOptions.TopLeft, wrap: true);
        TextMeshProUGUI value = Ugui.Text(content, "Value", Restyle(line.Value) + Delta(line), theme.Numbers, 14, Palette.Bone,
            TextAlignmentOptions.TopRight, wrap: true);
        float height = Mathf.Max(24, label.GetPreferredValues(label.text, leftWidth, 0).y,
            value.GetPreferredValues(value.text, rightWidth, 0).y + 4);
        label.At(Pad, y, leftWidth, height);
        value.At(Pad + width - rightWidth, y, rightWidth, height);
        label.overflowMode = value.overflowMode = TextOverflowModes.Overflow;
        return y + height;
    }

    private static ItemDrop.ItemData? Comparison(Player player, ItemDrop.ItemData item)
    {
        if (!item.IsEquipable()) return null;
        bool weapon = CraftFilter.Of(item.m_shared.m_itemType) == CraftCategory.Weapons;
        foreach (ItemDrop.ItemData candidate in player.GetInventory().GetAllItems())
            if (candidate != item && candidate.m_equipped && candidate.m_shared.m_itemType == item.m_shared.m_itemType &&
                (!weapon || candidate.m_shared.m_skillType == item.m_shared.m_skillType)) return candidate;
        return null;
    }

    private string Delta(Line line)
    {
        if (comparisonStats == null || line.Token.Length == 0) return "";
        bool lower = line.Token is "item_staminause" or "item_eitruse" or "item_staminahold";
        bool comparable = lower || DamageFigures.Contains(line.Token) || DefenceTokens.Contains(line.Token);
        if (!comparable) return "";
        Line? before = null;
        foreach (var rows in comparisonStats.Rows.Values)
        {
            before = rows.Find(other => other.Token == line.Token && other.Label == line.Label);
            if (before != null) break;
        }
        if (before == null) return "";
        Match current = Figure.Match(Tags.Replace(line.Value, "").Trim());
        Match previous = Figure.Match(Tags.Replace(before.Value, "").Trim());
        if (!current.Success || !previous.Success || current.Groups[2].Value != previous.Groups[2].Value ||
            !float.TryParse(current.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ||
            !float.TryParse(previous.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float b)) return "";
        float delta = a - b;
        if (Mathf.Abs(delta) < 0.005f) return "";
        bool better = lower ? delta < 0 : delta > 0;
        return " <size=80%><color=" + (better ? "#9BC48A" : "#F7A99F") + ">" +
            delta.ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + current.Groups[2].Value + "</color></size>";
    }

    private float Paragraph(string text, TMP_FontAsset? font, float size, Color color, float x, float w, float y)
    {
        TextMeshProUGUI label = Ugui.Text(content, "Text", text, font, size, color, TextAlignmentOptions.TopLeft, wrap: true);
        label.overflowMode = TextOverflowModes.Overflow;
        float h = label.GetPreferredValues(label.text, w, 0).y;
        label.At(x, y, w, h);
        return y + h;
    }

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
