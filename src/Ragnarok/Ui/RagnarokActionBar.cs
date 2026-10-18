using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Ui;

// Keep HotkeyBar.Update alive for input/selection. Only hide its rendering and mouse targets;
// the replacement uses the same inventory row and forwards touch clicks to vanilla's handler.
internal sealed class RagnarokActionBar : IDisposable
{
    // Pad leaves room at both sides (so the slots stay centred) for the inventory badge at the right.
    private const float Slot = 58, Gap = 7, Pad = 96, Top = 30, Width = Pad * 2 + Slot * 8 + Gap * 7, Height = 100;
    private static readonly AccessTools.FieldRef<HotkeyBar, int> Selected = AccessTools.FieldRefAccess<HotkeyBar, int>("m_selected");
    private static readonly MethodInfo Click = AccessTools.Method(typeof(HotkeyBar), "ElementClicked");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<Cell> cells = new();
    private HotkeyBar? original;
    private CanvasGroup? mask;
    private bool ownsMask, oldBlocks, oldInteractable;
    private float oldAlpha;
    private RectTransform? root;
    private StatLine? stats;
    private TMP_Text? inventoryKey, switchKey;
    private GameObject? switcher;
    private Image? upper, lower;
    private float nextKeyRefresh;

    private sealed class Cell
    {
        internal Image Icon = null!, Ring = null!, Equipped = null!, Durability = null!;
        internal TMP_Text Key = null!, Count = null!, Quality = null!, Queued = null!;
    }

    internal RagnarokActionBar(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        var hud = global::Hud.instance;
        var bar = hud ? hud.GetComponentInChildren<HotkeyBar>(true) : null;
        if (settings.Hud.Value != HudStyle.Ragnarok || !settings.ActionBar.Value || !bar)
        {
            Dispose();
            return;
        }
        if (original != bar || !root)
        {
            Dispose();
            original = bar;
            Build(hud!.m_rootObject.transform);
            mask = bar!.GetComponent<CanvasGroup>();
            ownsMask = !mask;
            if (!mask) mask = bar.gameObject.AddComponent<CanvasGroup>();
            oldAlpha = mask!.alpha;
            oldBlocks = mask.blocksRaycasts;
            oldInteractable = mask.interactable;
        }
        mask!.alpha = 0;
        mask.blocksRaycasts = false;
        mask.interactable = false;
        var player = Player.m_localPlayer;
        bool show = player && !player.IsDead() && original!.gameObject.activeInHierarchy;
        root!.gameObject.SetActive(show);
        if (!show) return;
        var parent = (RectTransform)root.parent;
        float scale = Math.Min(settings.ActionBarScale.Value, (parent.rect.width - 20) / Width);
        root.localScale = Vector3.one * scale;
        float limit = Math.Max(0, (parent.rect.width - Width * scale) / 2 - 8);
        root.anchoredPosition = new Vector2(Mathf.Clamp(settings.ActionBarX.Value, -limit, limit),
            Mathf.Clamp(settings.ActionBarBottom.Value, 0, Math.Max(0, parent.rect.height - Height * scale)));
        bool gamepad = ZInput.IsGamepadActive();
        // The guide's stats line over the bar's right end, and the inventory key beside the bar.
        Inventory inventory = player!.GetInventory();
        stats!.Set(Mathf.RoundToInt(player.GetBodyArmor()), inventory.GetTotalWeight(), player.GetMaxCarryWeight());
        if (Time.unscaledTime >= nextKeyRefresh)
        {
            nextKeyRefresh = Time.unscaledTime + 1f;
            inventoryKey!.text = KeyName("Inventory", "TAB");
            switchKey!.text = Equipment.ProvisionKeys.Label(settings.HotbarToggleKey.Value);
        }
        // The row switch: the upper diamond lit for the equipment row, the lower for the consumables.
        if (switcher!.activeSelf != HotbarRows.Enabled) switcher.SetActive(HotbarRows.Enabled);
        upper!.color = HotbarRows.Row == 0 ? Palette.Key : Palette.GoldDim;
        lower!.color = HotbarRows.Row == 1 ? Palette.Key : Palette.GoldDim;
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            var item = inventory.GetItemAt(i, HotbarRows.Row);
            bool equipped = item != null && player.IsItemEquiped(item);
            cell.Key.text = gamepad ? "" : (i + 1).ToString();
            cell.Icon.enabled = item != null;
            cell.Icon.sprite = item?.GetIcon();
            cell.Count.text = item != null && item.m_shared.m_maxStackSize > 1 ? item.m_stack.ToString() : "";
            cell.Quality.text = item != null && item.m_quality > 1 ? item.m_quality.ToString() : "";
            cell.Queued.text = item != null && player.IsEquipActionQueued(item) ? "…" : "";
            cell.Equipped.enabled = equipped;
            cell.Ring.enabled = equipped || (gamepad && Selected(original!) == i);
            cell.Ring.color = gamepad && Selected(original!) == i ? Palette.Bone : Palette.GoldBright;
            bool durable = item != null && item.m_shared.m_useDurability;
            cell.Durability.enabled = durable;
            if (durable)
            {
                float value = Mathf.Clamp01(item!.GetDurabilityPercentage());
                cell.Durability.rectTransform.sizeDelta = new Vector2((Slot - 12) * (value <= 0 ? 1 : value), 2);
                cell.Durability.color = value <= 0 ? Palette.Alert.WithAlpha(0.35f + Mathf.PingPong(Time.unscaledTime * 2, 0.65f)) : value < 0.25f ? Palette.Ember : Palette.Gold;
            }
        }
    }

    private void Build(Transform parent)
    {
        root = Ugui.Node(parent, "Ragnarok Action Bar");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0);
        root.sizeDelta = new Vector2(Width, Height);
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            var button = Ugui.Button(root, "Action " + (i + 1), theme.Slot, theme.SlotHover, () =>
            {
                if (Player.m_localPlayer && original && !UiState.WindowOpen)
                    Click.Invoke(original, new object[] { Player.m_localPlayer, index });
            }).At(Pad + i * (Slot + Gap), Top, Slot, Slot);
            var parentCell = button.transform;
            var cell = new Cell
            {
                Icon = Ugui.ItemIcon(Ugui.Image(parentCell, "Item", null, Color.white)).At(6, 5, Slot - 12, Slot - 12),
                Ring = Ugui.Image(parentCell, "Active Border", theme.SlotRing, Palette.Gold).Stretch(),
                Equipped = Ugui.Image(parentCell, "Equipped", theme.White, Palette.Gold.WithAlpha(0.1f)).Stretch(1, 1, 1, 1),
                Durability = Ugui.Image(parentCell, "Durability", theme.White, Palette.Gold).At(6, Slot - 6, Slot - 12, 2),
                Key = Ugui.Text(parentCell, "Key", "", theme.Strong, 11, Palette.Muted).At(5, 2, 16, 15),
                Count = Ugui.Text(parentCell, "Stack", "", theme.Strong, 13, Palette.Bone, TextAlignmentOptions.BottomRight).At(4, 32, Slot - 8, 19),
                Quality = Ugui.Text(parentCell, "Quality", "", theme.Strong, 10, Palette.GoldBright, TextAlignmentOptions.TopRight).At(Slot - 20, 3, 15, 15),
                Queued = Ugui.Text(parentCell, "Queued", "", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).At(15, 14, 28, 20)
            };
            cells.Add(cell);
        }

        float right = Pad + Slot * 8 + Gap * 7;
        stats = new StatLine(theme, root, right - 4, Top - 6, withArmor: true);
        // Inventory badge: a diamond holding a pack, then the key that opens it.
        float badgeX = right + 12, middle = Top + Slot / 2;
        Ugui.Image(root, "Pack Rim", theme.Diamond, Palette.Gold).At(badgeX, middle - 17, 34, 34);
        Ugui.Image(root, "Pack Core", theme.Diamond, Palette.Obsidian.WithAlpha(0.95f)).At(badgeX + 2, middle - 15, 30, 30);
        Ugui.Image(root, "Pack", theme.GlyphWeight, Palette.Bone).At(badgeX + 9, middle - 8, 16, 16);
        Image cap = Ugui.Image(root, "Inventory Key", theme.Button, Color.white).At(badgeX + 42, middle - 11, 40, 22);
        inventoryKey = Ugui.Text(cap.transform, "Label", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch(2, 0, 2, 0);
        inventoryKey.overflowMode = TextOverflowModes.Overflow;

        // The row switch, mirroring the badge at the left: the key, then two stacked diamonds.
        RectTransform toggle = Ugui.Node(root, "Row Switch").At(Pad - 86, middle - 24, 80, 48);
        switcher = toggle.gameObject;
        Image key = Ugui.Image(toggle, "Key", theme.Button, Color.white).At(0, 13, 40, 22);
        switchKey = Ugui.Text(key.transform, "Label", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch(2, 0, 2, 0);
        switchKey.overflowMode = TextOverflowModes.Overflow;
        upper = Diamond(toggle, 50, 4);
        lower = Diamond(toggle, 50, 24);
    }

    // A diamond with a gold rim around a lit or dark core.
    private Image Diamond(Transform parent, float x, float y)
    {
        Ugui.Image(parent, "Rim", theme.Diamond, Palette.Gold).At(x, y, 20, 20);
        return Ugui.Image(parent, "Core", theme.Diamond, Palette.GoldDim).At(x + 3, y + 3, 14, 14);
    }

    private static string KeyName(string action, string fallback)
    {
        if (ZInput.instance == null) return fallback;
        string bound = ZInput.instance.GetBoundKeyString(action, true);
        if (string.IsNullOrEmpty(bound)) return fallback;
        string localized = Localization.instance != null ? Localization.instance.Localize(bound) : bound;
        return localized.ToUpperInvariant();
    }

    public void Dispose()
    {
        if (mask)
        {
            mask!.alpha = oldAlpha;
            mask.blocksRaycasts = oldBlocks;
            mask.interactable = oldInteractable;
            if (ownsMask) UnityEngine.Object.Destroy(mask);
        }
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        mask = null;
        original = null;
        cells.Clear();
    }
}
