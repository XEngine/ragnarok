using System;
using System.Collections.Generic;
using Ragnarok.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// The provision slots on the vanilla HUD: Valheim's own hotbar slots, in a row just past the hotbar's
// eight, holding the filled provision slots with their key on each. Click or press the key to use one.
// Shown while the Ragnarök HUD (which has its own provision bar) is off.
internal sealed class VanillaProvisionBar : IDisposable
{
    private readonly InterfaceSettings settings;
    private readonly Func<bool> ragnarokHud;
    private readonly List<Cell> cells = new();
    private HotkeyBar? hotbar;
    private RectTransform? root;

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
        if (!root) Build(hotbar!);
        root!.gameObject.SetActive(hotbar!.gameObject.activeInHierarchy);

        // Just past the hotbar's eight slots, in its own parent's space, so it never shifts.
        Transform bar = hotbar.transform;
        root.localPosition = bar.localPosition + new Vector3(8.5f * hotbar.m_elementSpace, 0, 0);
        root.localScale = bar.localScale;

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
    }

    public void Dispose()
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        cells.Clear();
    }

    private void Build(HotkeyBar bar)
    {
        root = Ugui.Node(bar.transform.parent, "Ragnarok Provisions");
        RectTransform source = (RectTransform)bar.transform;
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
