using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// A local readout. Resolving ammunition never equips, consumes or transfers it.
internal sealed class CrosshairAmmo : IDisposable
{
    private static readonly Func<Humanoid, ItemDrop.ItemData, ItemDrop.ItemData> FindAmmo =
        AccessTools.MethodDelegate<Func<Humanoid, ItemDrop.ItemData, ItemDrop.ItemData>>(AccessTools.Method(typeof(Attack), "FindAmmo"));
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly Vector3[] corners = new Vector3[4];
    private RectTransform? root;
    private Image? icon;
    private TextMeshProUGUI? label;
    private TextOutline? outline;
    private ItemDrop.ItemData? lastWeapon;
    private float nextRefresh;

    internal CrosshairAmmo(InterfaceSettings settings, Theme theme) { this.settings = settings; this.theme = theme; }

    internal void Tick()
    {
        var hud = global::Hud.instance;
        var player = Player.m_localPlayer;
        if (root && (!hud || root!.parent != hud.m_rootObject.transform)) Dispose();
        ItemDrop.ItemData? weapon = player ? player.GetCurrentWeapon() : null;
        bool show = settings.CrosshairAmmo.Value && hud && player && !player.IsDead() &&
            RangedAmmo.UsesArrowsOrBolts(weapon) && hud.m_crosshair && hud.m_crosshair.enabled &&
            hud.m_crosshair.gameObject.activeInHierarchy && !global::Hud.IsUserHidden() &&
            !InventoryGui.IsVisible() && !Menu.IsVisible() && !Console.IsVisible() && !Minimap.IsOpen() &&
            !UiState.ModalOpen && !WeaponDial.Open && (!Chat.instance || !Chat.instance.HasFocus());
        if (!show)
        {
            if (root) root!.gameObject.SetActive(false);
            lastWeapon = null;
            return;
        }
        if (!root) Build(hud!.m_rootObject.transform);
        root!.gameObject.SetActive(true);
        float scale = settings.HudScale.Value;
        root.localScale = Vector3.one * scale;
        hud!.m_crosshair!.rectTransform.GetWorldCorners(corners);
        root.position = (corners[0] + corners[3]) * 0.5f + root.parent.TransformVector(new Vector3(32 * scale, -10 * scale, 0));
        if (Time.unscaledTime >= nextRefresh || lastWeapon != weapon)
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            lastWeapon = weapon;
            ItemDrop.ItemData? ammo = FindAmmo(player!, weapon!);
            int count = ammo == null ? 0 : RangedAmmo.Count(player!.GetInventory().GetAllItems(), ammo);
            bool available = ammo != null && count > 0;
            label!.text = "×" + count;
            label.color = available ? Palette.Muted.WithAlpha(0.6f) : Palette.Health.WithAlpha(0.65f);
            icon!.sprite = available ? ammo!.GetIcon() : null;
            icon.color = Color.white.WithAlpha(0.9f);
            icon.enabled = icon.sprite;
            float width = Mathf.Min(320, label.GetPreferredValues().x + 4);
            float iconWidth = icon.enabled ? 28 : 0;
            root.sizeDelta = new Vector2(width + iconWidth, 24);
            icon.rectTransform.anchoredPosition = new Vector2(0, -3);
            label.rectTransform.anchoredPosition = new Vector2(iconWidth, 0);
            label.rectTransform.sizeDelta = new Vector2(width, 24);
        }
        outline!.Sync();
        Utils.ClampUIToScreen(root);
    }

    private void Build(Transform parent)
    {
        root = Ugui.Node(parent, "Ragnarok Crosshair Ammo");
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 1);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        icon = Ugui.ItemIcon(Ugui.Image(root, "Ammo icon", null, Color.white)).At(0, 3, 20, 20);
        label = Ugui.Text(root, "Ammo count", "", theme.Strong, 12, Palette.Muted,
            TextAlignmentOptions.MidlineLeft).At(28, 0, 40, 24);
        outline = new TextOutline(label, opacity: 0.35f, radiusScale: 0.75f);
        nextRefresh = 0;
    }

    public void Dispose()
    {
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        root = null;
        icon = null;
        label = null;
        outline = null;
        lastWeapon = null;
        nextRefresh = 0;
    }
}
