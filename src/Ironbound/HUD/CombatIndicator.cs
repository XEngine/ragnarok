using System;
using System.Collections.Generic;
using Ironbound.Features.Mounts;
using UnityEngine;

namespace Ironbound.HUD;

// A synthetic HUD tile, never an effect added to SEMan or saved to the player.
internal sealed class CombatIndicator : IDisposable
{
    internal static HudStatus? Status { get; private set; }
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<HudStatus> single = new();
    private StatusStrip? vanilla;
    private Transform? parent;
    private Sprite? icon;
    private Player? observed;
    private float nextRefresh;

    internal CombatIndicator(InterfaceSettings settings, Theme theme)
    { this.settings = settings; this.theme = theme; Status = null; }

    internal void Tick()
    {
        Player player = Player.m_localPlayer;
        if (!player || player.IsDead() || player.InIntro())
        { Status = null; observed = null; DestroyVanilla(); return; }
        if (observed != player) { observed = player; nextRefresh = 0; }
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.1f;
            float quiet = MountService.Instance?.Settings.CombatQuiet.Value ?? 15;
            if (MountSafety.InCombat(player, quiet, out float remaining, out bool engaged))
            {
                if (!icon && ObjectDB.instance)
                {
                    GameObject prefab = ObjectDB.instance.GetItemPrefab("SwordBronze");
                    if (prefab) icon = prefab.GetComponent<ItemDrop>().m_itemData.GetIcon();
                }
                Status = new HudStatus
                {
                    Key = "ragnarok.combat.indicator".GetStableHashCode(),
                    Name = UiLocalization.Text("combat.name", "In combat"), Icon = icon,
                    Value = engaged ? UiLocalization.Text("combat.active", "Active") : Mathf.CeilToInt(remaining) + "s",
                    Description = UiLocalization.Text("combat.description", "Attacks, damage or enemy attention block mount summoning. The countdown shows the remaining quiet time; your mount must also be out of combat."),
                    Category = StatusCategory.Ongoing
                };
            }
            else Status = null;
        }
        var hud = global::Hud.instance;
        bool showVanilla = settings.Hud.Value == HudStyle.Vanilla && Status.HasValue && hud &&
            hud.m_rootObject.activeInHierarchy && !global::Hud.IsUserHidden() && !InventoryGui.IsVisible() && !UiState.ModalOpen;
        if (!showVanilla) { DestroyVanilla(); return; }
        Transform current = hud!.m_rootObject.transform;
        if (vanilla == null || parent != current || !vanilla.Rect)
        { DestroyVanilla(); parent = current; vanilla = new StatusStrip(theme, current, centered: true); }
        single.Clear(); single.Add(Status!.Value);
        vanilla.Set(single, 0); vanilla.SetScale(settings.HudScale.Value); vanilla.SetVisible(true);
        vanilla.BelowCompass(CompassStrip.Shown);
    }

    private void DestroyVanilla() { vanilla?.Dispose(); vanilla = null; parent = null; single.Clear(); }
    public void Dispose() { DestroyVanilla(); Status = null; observed = null; icon = null; }
}
