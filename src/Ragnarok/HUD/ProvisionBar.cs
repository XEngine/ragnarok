using System.Collections.Generic;
using Ragnarok.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// The filled provision slots beside the food cells, like a second hotbar: icon, amount and the
// slot's hotkey. Empty slots are skipped, so the bar is only as long as what you carry.
internal sealed class ProvisionBar
{
    private const float Size = 34, Step = 40;

    private readonly RectTransform root;
    private readonly Image divider;
    private readonly List<Cell> cells = new();

    internal ProvisionBar(Theme theme, Transform parent, float x, float y)
    {
        root = Ugui.Node(parent, "Provisions").At(x, y, 0, Size);
        divider = Ugui.Image(root, "Divider", theme.Diamond, Palette.Gold).At(0, Size / 2 - 3, 6, 6);
        for (int i = 0; i < CharacterSlots.Provisions; i++) cells.Add(new Cell(theme, root));
    }

    // Returns the width drawn, 0 when nothing shows.
    internal float Set(Player? player, bool show)
    {
        int shown = 0;
        if (show && player && CharacterSlots.Enabled())
        {
            Inventory inventory = player!.GetInventory();
            for (int i = 0; i < CharacterSlots.Provisions; i++)
            {
                Vector2i slot = CharacterSlots.ProvisionSlot(i);
                ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
                if (item == null) continue;
                string key = i < ProvisionKeys.Keys.Length ? ProvisionKeys.Label(ProvisionKeys.Keys[i].Value) : "";
                cells[shown].Set(item, key, 12 + shown * Step);
                shown++;
            }
        }
        for (int i = shown; i < cells.Count; i++) cells[i].Hide();
        divider.enabled = shown > 0;
        return shown == 0 ? 0 : 12 + shown * Step;
    }

    private sealed class Cell
    {
        private readonly RectTransform rect;
        private readonly Image icon;
        private readonly TextMeshProUGUI amount, key;
        private readonly TextOutline amountOutline, keyOutline;
        private string shownAmount = "", shownKey = "";

        internal Cell(Theme theme, Transform parent)
        {
            rect = Ugui.Node(parent, "Provision").At(0, 0, Size, Size);
            Ugui.Image(rect, "Tile", theme.White, Palette.HudGlass).At(0, 0, Size, Size);
            icon = Ugui.Image(rect, "Icon", null, Color.white).At(4, 4, Size - 8, Size - 8);
            Ugui.ItemIcon(icon);
            amount = Ugui.Text(rect, "Amount", "", theme.Numbers, 11, Palette.Bone, TextAlignmentOptions.BottomRight).At(0, Size - 15, Size - 3, 14);
            amount.overflowMode = TextOverflowModes.Overflow;
            key = Ugui.Text(rect, "Key", "", theme.Strong, 9, Palette.Muted, TextAlignmentOptions.TopLeft).At(3, 1, Size, 12);
            key.overflowMode = TextOverflowModes.Overflow;
            amountOutline = new TextOutline(amount);
            keyOutline = new TextOutline(key);
        }

        internal void Set(ItemDrop.ItemData item, string label, float x)
        {
            if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);
            if (!Mathf.Approximately(rect.anchoredPosition.x, x)) rect.anchoredPosition = new Vector2(x, 0);
            Sprite sprite = item.GetIcon();
            if (icon.sprite != sprite) icon.sprite = sprite;
            string count = item.m_shared.m_maxStackSize > 1 ? item.m_stack.ToString() : "";
            if (count != shownAmount) amount.text = shownAmount = count;
            if (label != shownKey) key.text = shownKey = label;
            amountOutline.Sync();
            keyOutline.Sync();
        }

        internal void Hide()
        {
            if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
        }
    }
}
