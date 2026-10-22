using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// Pack and chest share the same local presentation controls and procedural surfaces.
internal static class InventoryGuide
{
    internal sealed class Tab
    {
        private readonly PackCategory category;
        private readonly Image fill;
        private readonly TextMeshProUGUI label;

        internal Tab(PackCategory category, Image fill, TextMeshProUGUI label)
        {
            this.category = category;
            this.fill = fill;
            this.label = label;
        }

        internal void Refresh(PackCategory selected)
        {
            bool active = selected == category;
            fill.color = active ? Palette.Hex(0xECE5D6) : Color.clear;
            label.color = active ? Palette.Hex(0x0C0F11) : Palette.Hex(0xECE5D6);
        }
    }

    internal static void Tabs(Theme theme, Transform parent, float x, float y, List<Tab> tabs, Action<PackCategory> select)
    {
        string[] labels = { "All", "Gear", "Materials", "Food", "Trophies" };
        float[] widths = { 34, 44, 68, 44, 62 };
        for (int i = 0; i < labels.Length; i++)
        {
            PackCategory value = (PackCategory)i;
            Image fill = Ugui.Image(parent, labels[i] + " Tab", theme.PackPill, Color.clear).At(x, y, widths[i], 28);
            Ugui.Image(fill.transform, "Outline", theme.PackPillEdge, Palette.Hex(0xECE5D6)).Stretch();
            TextMeshProUGUI label = Ugui.Text(fill.transform, "Label", labels[i], theme.Strong, 12, Palette.Bone,
                TextAlignmentOptions.Center).Stretch(4, 0, 4, 0);
            Ugui.HitArea(fill.transform, "Select category", () => select(value)).Stretch();
            tabs.Add(new Tab(value, fill, label));
            x += widths[i] + 4;
        }
    }

    // Reference header: name/context at the left; armor, weight and a compact capacity meter at the right.
    internal sealed class Summary
    {
        private readonly TextMeshProUGUI name, context, weight, weightCaption;
        private readonly TextMeshProUGUI? armor;
        private readonly CapsuleGraphic track, fill;
        private readonly Image? armorIcon;
        private readonly float meterX;
        private const float MeterWidth = 54, MeterHeight = 6, CenterY = 24, ShieldSize = 13;
        private readonly float nameWidth, captionOffset;

        internal Summary(Theme theme, Transform parent, float width, string caption, bool withArmor, bool vanilla = false)
        {
            captionOffset = vanilla ? 0 : 1.5f;
            TMP_FontAsset? nativeFont = vanilla && InventoryGui.instance
                ? InventoryGui.instance.m_stackAllButton.GetComponentInChildren<TMP_Text>(true).font : null;
            float statX = width - (withArmor ? 243 : 196) - 12;
            nameWidth = Mathf.Max(64, statX - 102);
            name = Ugui.Text(parent, "Name", "", nativeFont ?? theme.Heading, 22, Palette.Hex(0xECE5D6)).At(12, 8, nameWidth, 32);
            name.richText = false;
            name.enableAutoSizing = true;
            name.fontSizeMin = 13;
            name.fontSizeMax = 22;
            context = Ugui.Text(parent, "Context", caption, nativeFont ?? theme.Strong, 10, InventoryCategories.Material, spacing: 1.6f)
                .At(12 + nameWidth + 8, 10, 80, 30);
            if (withArmor)
            {
                armorIcon = Ugui.Image(parent, "Armor Icon", theme.InventoryShield, InventoryCategories.Armor).At(0, CenterY - ShieldSize / 2, ShieldSize, ShieldSize);
                armor = Ugui.Text(parent, "Armor", "", nativeFont ?? theme.Numbers, 14, Palette.Bone).At(0, CenterY - 14, 24, 28);
            }
            weightCaption = Ugui.Text(parent, "Weight Caption", "Weight", nativeFont ?? theme.Strong, 14, InventoryCategories.Material).At(0, CenterY - 14, 47, 28);
            weight = Ugui.Text(parent, "Weight", "", nativeFont ?? theme.Numbers, 14, Palette.Bone).At(0, CenterY - 14, 85, 28);
            meterX = width - 12 - MeterWidth;
            track = CapsuleGraphic.Create(parent, "Carry Track", Palette.Hex(0x303331))
                .At(meterX, CenterY - MeterHeight / 2, MeterWidth, MeterHeight);
            fill = CapsuleGraphic.Create(track.transform, "Carry Fill", Palette.Hex(0xECE5D6));
        }

        internal void Set(string title, int? protection, float total, float capacity)
        {
            if (name.text != title)
            {
                name.fontSize = 22;
                name.text = title;
                float actual = Mathf.Min(nameWidth, name.GetPreferredValues(title).x);
                name.rectTransform.sizeDelta = new Vector2(actual, 32);
                context.rectTransform.anchoredPosition = new Vector2(12 + actual + 8, -10);
            }
            if (armor != null) armor.text = protection?.ToString() ?? "";
            weight.text = Mathf.CeilToInt(total) + (capacity > 0 ? " / " + Mathf.CeilToInt(capacity) : "");
            bool overloaded = capacity > 0 && total > capacity;
            weight.color = fill.color = overloaded ? Palette.Health : Palette.Hex(0xECE5D6);
            track.gameObject.SetActive(capacity > 0);
            float fillWidth = capacity > 0 ? MeterWidth * Mathf.Clamp01(total / capacity) : 0;
            fill.enabled = fillWidth > 0;
            if (fillWidth > 0)
            {
                float height = Mathf.Min(MeterHeight, fillWidth);
                fill.At(0, (MeterHeight - height) / 2, fillWidth, height);
            }
            // Measure live values so a short weight does not leave a large empty gap.
            float weightWidth = Mathf.Ceil(weight.GetPreferredValues(weight.text).x);
            float weightX = meterX - 8 - weightWidth;
            weight.At(weightX, CenterY - 14, weightWidth, 28);
            float captionWidth = Mathf.Ceil(weightCaption.GetPreferredValues(weightCaption.text).x);
            float captionX = weightX - 6 - captionWidth;
            weightCaption.At(captionX, CenterY - 14 + captionOffset, captionWidth, 28);
            if (armor != null && armorIcon != null)
            {
                float armorWidth = Mathf.Ceil(armor.GetPreferredValues(armor.text).x);
                float armorX = captionX - 16 - armorWidth;
                armor.At(armorX, CenterY - 14, armorWidth, 28);
                armorIcon.At(armorX - 6 - ShieldSize, CenterY - ShieldSize / 2, ShieldSize, ShieldSize);
            }
        }
    }

    internal static Button ActionButton(Theme theme, Transform parent, string label, float x, float y,
        float width, float height, Action click, float fontSize = 12)
    {
        Button button = Ugui.Button(parent, label, theme.PackPill, theme.PackPill, click).At(x, y, width, height);
        ((Image)button.targetGraphic).color = Palette.Hex(0x111716, 0.22f);
        Image edge = Ugui.Image(button.transform, "Outline", theme.PackPillEdge, Palette.Hex(0x626B64)).Stretch();
        Ugui.Text(button.transform, "Label", label, theme.Strong, fontSize, Palette.Bone,
            TextAlignmentOptions.Center).Stretch(4, 0, 4, 0);
        button.OnHover(on => edge.color = on ? InventoryCategories.Gold : Palette.Hex(0x626B64));
        return button;
    }
}
