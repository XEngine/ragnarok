using System;
using System.Collections.Generic;
using Ragnarok.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// The provision slots on the vanilla HUD: Valheim's own hotbar slots holding the filled provision slots
// with their key on each, beside the Forsaken power icon (or just past the hotbar's eight). Click or
// press the key to use one. Shown while the Ragnarök HUD (which has its own provision bar) is off.
internal sealed class VanillaProvisionBar : IDisposable
{
    private readonly InterfaceSettings settings;
    private readonly Func<bool> ragnarokHud;
    // Key labels such as "ALT+4" are several characters wide; keep them below the slot numbers' size.
    private const float BindingScale = 0.6f, PowerGap = 8f;
    private static readonly Vector3[] Corners = new Vector3[4];
    private readonly List<Cell> cells = new();
    private HotkeyBar? hotbar;
    private RectTransform? root;
    private ProvisionPlacement builtPlacement;

    private sealed class Cell
    {
        internal GameObject Go = null!;
        internal Image Icon = null!;
        internal TMP_Text Amount = null!, Binding = null!;
        internal ItemDrop.ItemData? Item;
    }

    internal VanillaProvisionBar(InterfaceSettings settings, Func<bool> ragnarokHud)
    {
        this.settings = settings;
        this.ragnarokHud = ragnarokHud;
    }

    internal void Tick()
    {
        HotkeyBar? current = global::Hud.instance ? global::Hud.instance.GetComponentInChildren<HotkeyBar>(true) : null;
        if (current != hotbar)
        {
            root = null;
            cells.Clear();
            hotbar = current;
        }
        Player player = Player.m_localPlayer;
        if (!hotbar || ragnarokHud() || !settings.ProvisionBar.Value || !CharacterSlots.Enabled() || !player || player.IsDead())
        {
            Dispose();
            return;
        }
        ProvisionPlacement placement = settings.VanillaProvisionPlacement.Value;
        global::Hud hud = global::Hud.instance!; // The hotbar above was found through it.
        if (placement == ProvisionPlacement.BesidePower && (!hud.m_gpRoot || !hud.m_gpIcon)) placement = ProvisionPlacement.AfterHotbar;
        if (root && builtPlacement != placement) Dispose();
        if (!root) Build(hotbar!, placement, hud);
        root!.gameObject.SetActive(hotbar!.gameObject.activeInHierarchy);

        Inventory inventory = player.GetInventory();
        int shown = 0;
        for (int i = 0; i < CharacterSlots.Provisions; i++)
        {
            Vector2i slot = CharacterSlots.ProvisionSlot(i);
            ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
            if (item == null) continue;
            Cell c = cells[shown++];
            c.Item = item;
            if (!c.Go.activeSelf) c.Go.SetActive(true);
            c.Go.transform.localPosition = new Vector3((shown - 1) * hotbar.m_elementSpace, 0, 0);
            c.Icon.sprite = item.GetIcon();
            c.Icon.gameObject.SetActive(true);
            bool stack = item.m_shared.m_maxStackSize > 1;
            c.Amount.gameObject.SetActive(stack);
            if (stack) c.Amount.text = item.m_stack + "/" + item.m_shared.m_maxStackSize;
            c.Binding.text = i < ProvisionKeys.Keys.Length ? ProvisionKeys.Label(ProvisionKeys.Keys[i].Value) : "";
        }
        for (int i = shown; i < cells.Count; i++)
        {
            cells[i].Item = null;
            if (cells[i].Go.activeSelf) cells[i].Go.SetActive(false);
        }

        Transform bar = hotbar.transform;
        if (placement == ProvisionPlacement.AfterHotbar)
        {
            // Just past the hotbar's eight slots, in its own parent's space, so it never shifts.
            root.localPosition = bar.localPosition + new Vector3(8.5f * hotbar.m_elementSpace, 0, 0);
            root.localScale = bar.localScale;
        }
        else PlaceBesidePower(hud, bar);
    }

    // Right of the power icon, centred on it, with slots the icon's height (between 60% and 100% of
    // the hotbar's). World-space corners keep this independent of the HUD's anchors and canvas scale.
    private void PlaceBesidePower(global::Hud hud, Transform bar)
    {
        RectTransform icon = hud.m_gpIcon.rectTransform;
        RectTransform first = (RectTransform)cells[0].Go.transform;
        Transform parent = root!.parent;
        float parentScale = Mathf.Max(1e-4f, parent.lossyScale.y);
        icon.GetWorldCorners(Corners);
        float iconHeight = (Corners[1].y - Corners[0].y) / parentScale;
        Vector3 iconRight = (Corners[2] + Corners[3]) * 0.5f;
        float hotbarScale = bar.lossyScale.y / parentScale;
        float scale = Mathf.Clamp(iconHeight / Mathf.Max(1f, first.rect.height), hotbarScale * 0.6f, hotbarScale);
        root.localScale = new Vector3(scale, scale, 1f);

        first.GetWorldCorners(Corners);
        Vector3 firstLeft = (Corners[0] + Corners[1]) * 0.5f;
        Vector3 target = iconRight + new Vector3(PowerGap * parent.lossyScale.x, 0, 0);
        root.position += new Vector3(target.x - firstLeft.x, target.y - firstLeft.y, 0);
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        cells.Clear();
    }

    private void Build(HotkeyBar bar, ProvisionPlacement placement, global::Hud hud)
    {
        builtPlacement = placement;
        // The power's own root hides while no power is chosen; its parent keeps the row on screen.
        Transform parent = placement == ProvisionPlacement.BesidePower ? hud.m_gpRoot.parent : bar.transform.parent;
        root = Ugui.Node(parent, "Ragnarok Provisions");
        RectTransform source = placement == ProvisionPlacement.BesidePower ? hud.m_gpRoot : (RectTransform)bar.transform;
        root.anchorMin = source.anchorMin;
        root.anchorMax = source.anchorMax;
        root.pivot = source.pivot;
        root.sizeDelta = source.sizeDelta;
        cells.Clear();
        for (int i = 0; i < CharacterSlots.Provisions; i++)
        {
            GameObject go = UnityEngine.Object.Instantiate(bar.m_elementPrefab, root);
            go.name = "Provision";
            Cell cell = new()
            {
                Go = go,
                Icon = go.transform.Find("icon").GetComponent<Image>(),
                Amount = go.transform.Find("amount").GetComponent<TMP_Text>(),
                Binding = go.transform.Find("binding").GetComponent<TMP_Text>()
            };
            go.transform.Find("durability").gameObject.SetActive(false);
            go.transform.Find("equiped").gameObject.SetActive(false);
            go.transform.Find("queued").gameObject.SetActive(false);
            go.transform.Find("selected").gameObject.SetActive(false);
            cell.Binding.enableAutoSizing = false;
            cell.Binding.fontSize *= BindingScale;
            cell.Binding.textWrappingMode = TextWrappingModes.NoWrap;
            cell.Binding.overflowMode = TextOverflowModes.Overflow;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (cell.Item != null && Player.m_localPlayer) Player.m_localPlayer.UseItem(null, cell.Item, false);
            });
            go.SetActive(false);
            cells.Add(cell);
        }
    }
}
