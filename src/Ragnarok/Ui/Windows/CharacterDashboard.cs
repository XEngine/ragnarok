using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI.Windows;

// Local presentation only. Read the equipped count and active effect from the game;
// never add effects or count spare armor/extra utility slots toward a set.
internal sealed class CharacterDashboard
{
    internal const float Height = 108;
    private static readonly Color SetSurface = Palette.Hex(0x24323B), SetBorder = Palette.Hex(0x678495);
    private static readonly Func<Humanoid, string, int> SetCount =
        AccessTools.MethodDelegate<Func<Humanoid, string, int>>(AccessTools.Method(typeof(Humanoid), "GetSetCount"));
    private static readonly Regex Tags = new("<[^>]*>");
    // Native set IDs from the installed 1.0 armor prefabs, independent of language
    // and of the status effect's name (which describes the bonus, not the armor).
    private static readonly Dictionary<string, string> SetNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["troll"] = "Troll Leather Set",
        ["root_armor"] = "Root Set",
        ["fenring_armor"] = "Fenris Set",
        ["AshlandsMediumArmor"] = "Ask Set",
        ["berserker_armor"] = "Bear Set",
        ["berserker_armor_undead"] = "Vilebone Set",
        ["DeepNorthMediumArmor"] = "Vanguard Set",
        ["lox"] = "Lox Fur Set",
        ["harvester"] = "Harvest Set"
    };
    private readonly TextMeshProUGUI[] values = new TextMeshProUGUI[4], limits = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI bonuses;
    private readonly UITooltip? tooltip;
    private readonly ItemCard? detailsCard;
    private readonly Rect hoverArea;
    private bool detailsHovered;
    private string fullDetails = "";
    private readonly Image band, bandEdge;
    private readonly bool vanilla;
    private readonly HashSet<string> seen = new();
    private readonly StringBuilder text = new(), details = new();
    private float refresh;

    internal CharacterDashboard(Theme theme, Transform parent, float x, float y, float width, bool vanilla = false)
    {
        this.vanilla = vanilla;
        InventoryGui gui = InventoryGui.instance;
        TMP_FontAsset? nativeFont = vanilla && gui ? gui.m_stackAllButton.GetComponentInChildren<TMP_Text>(true).font : null;
        Sprite surface = vanilla && gui ? ((Image)gui.m_stackAllButton.targetGraphic).sprite : theme.PackSurface;
        RectTransform root = Ugui.Node(parent, "Character dashboard").At(x, y, width, Height);
        string[] labels = { "ARMOR", "HEALTH", "STAMINA", "EITR" };
        Color[] colors = { Palette.Bone, Palette.Health, Palette.Stamina, Palette.Hex(0x9A7BEA) };
        float cardWidth = (width - 18) / 4;
        for (int i = 0; i < 4; i++)
        {
            RectTransform card = Ugui.Node(root, labels[i] + " Card").At(i * (cardWidth + 6), 0, cardWidth, 48);
            Ugui.Image(card, "Surface", surface, vanilla ? Color.white : Palette.Hex(0x0C0F11, 0.18f)).Stretch();
            if (!vanilla) Ugui.Image(card, "Outline", theme.PackEdge, Palette.Bone.WithAlpha(0.55f)).Stretch();
            TextMeshProUGUI label = Ugui.Text(card, "Label", labels[i], nativeFont ?? theme.Strong, 8.5f, Palette.Bone, spacing: 1.2f)
                .At(7, 4, cardWidth - 14, 12);
            label.enableAutoSizing = true;
            label.fontSizeMin = 6;
            label.fontSizeMax = 8.5f;
            values[i] = Ugui.Text(card, "Current", "", nativeFont ?? theme.Numbers, 21, colors[i]).At(7, 15, cardWidth - 14, 23);
            values[i].enableAutoSizing = true;
            values[i].fontSizeMin = 12;
            values[i].fontSizeMax = 21;
            limits[i] = Ugui.Text(card, "Maximum", "", nativeFont ?? theme.Strong, 8, InventoryCategories.Material).At(7, 35, cardWidth - 14, 10);
        }

        RectTransform area = Ugui.Node(root, "Equipped sets").At(0, 56, width, 52);
        band = Ugui.Image(area, "Surface", surface, vanilla ? Color.white : SetSurface.WithAlpha(0.25f)).Stretch();
        bandEdge = Ugui.Image(area, "Outline", theme.PackEdge, SetBorder.WithAlpha(0.45f)).Stretch();
        bandEdge.enabled = !vanilla;
        bonuses = Ugui.Text(area, "Set progress and effects", "", nativeFont ?? theme.Strong, 11, Palette.Bone,
            TextAlignmentOptions.MidlineLeft).Stretch(8, 5, 8, 5);
        bonuses.enableAutoSizing = true;
        bonuses.fontSizeMin = 9;
        bonuses.fontSizeMax = 11;
        bonuses.overflowMode = TextOverflowModes.Ellipsis;
        band.raycastTarget = true;
        hoverArea = new Rect(0, 0, width, 52);
        if (vanilla)
        {
            tooltip = area.gameObject.AddComponent<UITooltip>();
            if (gui && gui.m_playerGrid && gui.m_playerGrid.m_elementPrefab)
            {
                UITooltip original = gui.m_playerGrid.m_elementPrefab.GetComponentInChildren<UITooltip>(true);
                if (original) tooltip.m_tooltipPrefab = original.m_tooltipPrefab;
            }
            tooltip.m_topic = "SET BONUS";
        }
        else
        {
            detailsCard = new ItemCard(theme, area);
            band.OnHover(on =>
            {
                detailsHovered = on;
                if (!on) detailsCard.ShowDetails(null, hoverArea);
            });
        }
    }

    internal void Refresh(Player player, Inventory inventory)
    {
        Value(0, Mathf.RoundToInt(player.GetBodyArmor()), null);
        Value(1, player.GetHealth(), player.GetMaxHealth());
        Value(2, player.GetStamina(), player.GetMaxStamina());
        Value(3, player.GetEitr(), player.GetMaxEitr());
        if (Time.unscaledTime >= refresh)
        {
            refresh = Time.unscaledTime + 0.25f;
            RefreshSets(player, inventory);
        }
        if (detailsHovered) detailsCard?.ShowDetails(fullDetails, hoverArea);
    }

    private void Value(int index, float current, float? maximum)
    {
        string number = Mathf.CeilToInt(current).ToString();
        if (values[index].text != number) values[index].text = number;
        string limit = maximum.HasValue ? "/ " + Mathf.CeilToInt(maximum.Value) + " MAX" : "PROTECTION";
        if (limits[index].text != limit) limits[index].text = limit;
    }

    private void RefreshSets(Player player, Inventory inventory)
    {
        seen.Clear();
        text.Clear();
        details.Clear();
        bool anyActive = false;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            ItemDrop.ItemData.SharedData data = item.m_shared;
            StatusEffect effect = data.m_setStatusEffect;
            if (!item.m_equipped || !effect || string.IsNullOrEmpty(data.m_setName) || data.m_setSize <= 1) continue;
            string key = data.m_setName + "\u001f" + data.m_setSize + "\u001f" + effect.name;
            if (!seen.Add(key)) continue;
            int count = SetCount(player, data.m_setName);
            if (count == 0) continue;
            StatusEffect active = player.GetSEMan().GetStatusEffect(effect);
            bool enabled = count >= data.m_setSize && active;
            anyActive |= enabled;
            StatusEffect shown = active ? active : effect;
            string name = SetDisplayName(data.m_setName);
            string full = Plain(shown.GetTooltipString()).Trim();
            string summary = CompactEffect(shown, full);
            if (text.Length > 0) text.Append('\n');
            text.Append(name).Append(" · <b>").Append(count).Append('/').Append(data.m_setSize)
                .Append("</b> · <color=").Append(enabled ? "#8FC1E8" : "#A99F8D").Append('>')
                .Append(summary).Append(enabled ? "" : " · Locked").Append("</color>");
            if (details.Length > 0) details.Append("\n\n");
            details.Append(name).Append(" · ").Append(count).Append('/').Append(data.m_setSize)
                .Append(enabled ? " · Active\n" : " · Locked\n").Append(full);
        }
        if (text.Length == 0)
        {
            text.Append("NO SET BONUS · <color=#A99F8D>Equip matching pieces</color>");
            details.Append("Equip matching pieces to unlock a set effect.");
        }
        string next = text.ToString();
        if (bonuses.text != next) bonuses.text = next;
        fullDetails = details.ToString();
        if (tooltip != null && tooltip.m_text != fullDetails) tooltip.m_text = fullDetails;
        band.color = vanilla ? Color.white : SetSurface.WithAlpha(anyActive ? 0.35f : 0.25f);
        bandEdge.color = SetBorder.WithAlpha(anyActive ? 0.65f : 0.45f);
    }

    private static string SetDisplayName(string id)
    {
        if (SetNames.TryGetValue(id, out string name)) return name;
        // Keep unknown/modded names readable without guessing a vanilla set.
        name = Plain(id).Trim();
        if (name.Length == 0) return "Unknown Set";
        name = char.ToUpper(name[0]) + name.Substring(1);
        return name.EndsWith(" set", StringComparison.OrdinalIgnoreCase) ? name : name + " Set";
    }

    private static string CompactEffect(StatusEffect effect, string full)
    {
        // SE_Stats prefixes numerical effects with its flavor description. Omit
        // that prefix only when separate effect lines remain; preserve modded text.
        string description = Plain(effect.m_tooltip).Trim();
        string summary = full;
        if (description.Length > 0 && summary.StartsWith(description, StringComparison.Ordinal))
        {
            string remainder = summary.Substring(description.Length).Trim();
            if (remainder.Length > 0) summary = remainder;
        }
        string[] lines = summary.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].Trim();
        return lines.Length > 0 ? string.Join(" · ", lines) : Plain(effect.m_name);
    }

    private static string Plain(string value) => Tags.Replace(Localization.instance.Localize(value ?? ""), "");
}
