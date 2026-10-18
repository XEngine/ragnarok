using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.Equipment;

// Item custom data survives grave serialization and the clones made by inventory transfers.
internal static class TombstoneRecovery
{
    internal const string Marker = "com.kernelpanic.ragnarok.reclaim";
    internal static Func<bool> Enabled = () => false;
    private static readonly Dictionary<ItemDrop.ItemData, Pending> Queue = new();
    private sealed class Pending
    {
        internal Player Player = null!;
        internal int Extra;
        internal float Until;
        internal float NextAttempt;
    }

    internal static void Remember(Player player)
    {
        if (player != Player.m_localPlayer) return;
        Queue.Clear();
        var extras = new HashSet<ItemDrop.ItemData>(CharacterSlots.ActiveExtras(player));
        foreach (var item in player.GetInventory().GetAllItems())
        {
            item.m_customData.Remove(Marker);
            if (Enabled() && (player.IsItemEquiped(item) || extras.Contains(item)))
                item.m_customData[Marker] = player.GetPlayerID() + ":" +
                    (extras.Contains(item) ? item.m_gridPos.x : -1) + ":" + Guid.NewGuid().ToString("N");
        }
    }

    internal static HashSet<string>? BeforeTransfer(Inventory destination, Inventory source)
    {
        var player = Player.m_localPlayer;
        if (!Enabled() || !player || destination != player.GetInventory()) return null;
        var markers = new HashSet<string>();
        string owner = player.GetPlayerID() + ":";
        foreach (var item in source.GetAllItems())
            if (item.m_customData.TryGetValue(Marker, out var value) && value.StartsWith(owner, StringComparison.Ordinal))
                markers.Add(value);
        if (markers.Count == 0) return null;
        foreach (var grave in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
        {
            var container = grave.GetComponent<Container>();
            var view = grave.GetComponent<ZNetView>();
            if (container && container.GetInventory() == source && view && view.IsValid() &&
                view.GetZDO().GetLong(ZDOVars.s_owner) == player.GetPlayerID()) return markers;
        }
        return null;
    }

    internal static void AfterTransfer(Inventory destination, HashSet<string>? markers)
    {
        var player = Player.m_localPlayer;
        if (markers == null || !player || destination != player.GetInventory()) return;
        foreach (var item in destination.GetAllItems())
        {
            if (!item.m_customData.TryGetValue(Marker, out var value) || !markers.Contains(value)) continue;
            string[] fields = value.Split(':');
            if (fields.Length != 3 || !int.TryParse(fields[1], out int extra)) continue;
            if (extra != -1 && Array.IndexOf(CharacterSlots.ExtraUtility, extra) < 0) continue;
            Queue[item] = new Pending { Player = player, Extra = extra, Until = Time.unscaledTime + 10f };
        }
    }

    // Defer until transfers and other mods' TakeAll callbacks finish, but skip equip timers.
    internal static void Tick()
    {
        foreach (var pair in Queue.ToArray())
        {
            var item = pair.Key;
            var pending = pair.Value;
            var player = pending.Player;
            if (!player || player != Player.m_localPlayer || !player.GetInventory().ContainsItem(item))
            {
                Queue.Remove(item);
                continue;
            }
            bool done = !Enabled() || player.IsDead() || Time.unscaledTime > pending.Until;
            if (!done && Time.unscaledTime < pending.NextAttempt) continue;
            pending.NextAttempt = Time.unscaledTime + 0.25f;
            if (!done && pending.Extra >= 0)
            {
                var inventory = player.GetInventory();
                var slot = CharacterSlots.GearSlot(pending.Extra);
                if (CharacterSlots.Enabled() && CharacterSlots.ExtraUtilityEnabled() &&
                    item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility &&
                    inventory.GetItemAt(slot.x, slot.y) == null)
                {
                    item.m_gridPos = slot;
                    CharacterSlots.SyncEffects(player);
                }
                done = true; // Never replace a different utility already occupying that slot.
            }
            else if (!done) done = player.IsItemEquiped(item) || player.EquipItem(item, false);
            if (!done) continue;
            item.m_customData.Remove(Marker);
            Queue.Remove(item);
            CharacterSlots.Notify(player.GetInventory());
        }
    }

    internal static void Reset()
    {
        Queue.Clear();
        Enabled = () => false;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
internal static class RememberGraveEquipmentPatch
{
    private static void Prefix(Player __instance) => TombstoneRecovery.Remember(__instance);
    private static void Postfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer) return;
        // Quest items and equipment retained by world death settings were never reclaimed.
        foreach (var item in __instance.GetInventory().GetAllItems())
            item.m_customData.Remove(TombstoneRecovery.Marker);
    }
}

[HarmonyPatch]
internal static class ReclaimEquipmentPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(Inventory).GetMethods()
        .Where(method => method.Name == nameof(Inventory.MoveAll) || method.Name == nameof(Inventory.MoveItemToThis));
    private static void Prefix(Inventory __instance, Inventory fromInventory, out HashSet<string>? __state) =>
        __state = TombstoneRecovery.BeforeTransfer(__instance, fromInventory);
    private static void Postfix(Inventory __instance, HashSet<string>? __state) =>
        TombstoneRecovery.AfterTransfer(__instance, __state);
}
