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
    internal const string ProvisionMarker = "com.kernelpanic.ragnarok.reclaimslot";
    private const int ProvisionOffset = 16;
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
            item.m_customData.Remove(ProvisionMarker);
            if (Enabled() && (player.IsItemEquiped(item) || extras.Contains(item)))
                item.m_customData[Marker] = player.GetPlayerID() + ":" +
                    (extras.Contains(item) ? item.m_gridPos.x : -1) + ":" + Guid.NewGuid().ToString("N");
            if (Enabled() && CharacterSlots.Enabled() && CharacterSlots.IsProvisionSlot(item.m_gridPos))
                item.m_customData[ProvisionMarker] = player.GetPlayerID() + ":" + item.m_gridPos.x + ":" + Guid.NewGuid().ToString("N");
        }
    }

    internal static HashSet<string>? BeforeTransfer(Inventory destination, Inventory source)
    {
        var player = Player.m_localPlayer;
        if (!Enabled() || !player || destination != player.GetInventory()) return null;
        var markers = new HashSet<string>();
        string owner = player.GetPlayerID() + ":";
        foreach (var item in source.GetAllItems())
        {
            if (item.m_customData.TryGetValue(Marker, out var value) && value.StartsWith(owner, StringComparison.Ordinal))
                markers.Add(value);
            if (item.m_customData.TryGetValue(ProvisionMarker, out var provision) && provision.StartsWith(owner, StringComparison.Ordinal))
                markers.Add(provision);
        }
        if (markers.Count == 0) return null;
        return OwnGrave(source, player) != null ? markers : null;
    }

    private static ZNetView? OwnGrave(Inventory source, Player player)
    {
        foreach (var grave in UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
        {
            var container = grave.GetComponent<Container>();
            var view = grave.GetComponent<ZNetView>();
            if (container && container.GetInventory() == source && view && view.IsValid() &&
                view.GetZDO().GetLong(ZDOVars.s_owner) == player.GetPlayerID()) return view;
        }
        return null;
    }

    private static bool Tag(ItemDrop.ItemData item, string key, long owner, out int slot)
    {
        slot = -1;
        if (!item.m_customData.TryGetValue(key, out string value)) return false;
        return GraveRecoveryTag.TryRead(value, owner, out slot);
    }

    private static int RecoverySlot(ItemDrop.ItemData item, Player player)
    {
        if (!CharacterSlots.Enabled()) return -1;
        long owner = player.GetPlayerID();
        if (Tag(item, ProvisionMarker, owner, out int provision) && provision >= 0 && provision < CharacterSlots.Provisions &&
            provision < player.GetInventory().GetWidth() &&
            CharacterSlots.Accepts(player.GetInventory(), CharacterSlots.ProvisionSlot(provision), item))
            return ProvisionOffset + provision;
        if (item.m_stack != 1 || !Tag(item, Marker, owner, out int extra)) return -1;
        if (extra >= 0)
            return Array.IndexOf(CharacterSlots.ExtraUtility, extra) >= 0 && CharacterSlots.ExtraUtilityEnabled() &&
                extra < player.GetInventory().GetWidth() &&
                item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility ? extra : -1;
        return extra == -1 && CharacterSlots.WornSlot(item) is { } slot && slot.x < player.GetInventory().GetWidth() ? slot.x : -1;
    }

    private static int OccupiedSlot(ItemDrop.ItemData item) => CharacterSlots.IsGearSlot(item.m_gridPos) ? item.m_gridPos.x :
        CharacterSlots.IsProvisionSlot(item.m_gridPos) ? ProvisionOffset + item.m_gridPos.x : -1;

    private static Vector2i Position(int slot) => slot >= ProvisionOffset
        ? CharacterSlots.ProvisionSlot(slot - ProvisionOffset) : CharacterSlots.GearSlot(slot);

    private static GraveRecoveryPlan Plan(Player player, Inventory source)
    {
        GraveRecoveryPlan.Item Snapshot(ItemDrop.ItemData item, int slot)
        {
            var type = item.m_shared.m_itemType;
            bool topFirst = item.IsWeapon() || type == ItemDrop.ItemData.ItemType.Tool || type == ItemDrop.ItemData.ItemType.Shield ||
                type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Misc || type == ItemDrop.ItemData.ItemType.Trinket;
            return new GraveRecoveryPlan.Item(item.m_shared.m_name, item.m_stack, item.m_shared.m_maxStackSize,
                item.m_quality, item.m_shared.m_maxQuality, item.m_worldLevel, item.m_gridPos.x, item.m_gridPos.y, topFirst, slot);
        }
        Inventory own = player.GetInventory();
        return GraveRecoveryPlan.Create(own.GetWidth(), CharacterSlots.GridRows(own),
            own.GetAllItems().Select(item => Snapshot(item, OccupiedSlot(item))).ToArray(),
            source.GetAllItems().Select(item => Snapshot(item, RecoverySlot(item, player))).ToArray());
    }

    internal static bool CanAutoReclaim(TombStone grave, Player player)
    {
        if (!Enabled() || !CharacterSlots.Enabled() || player != Player.m_localPlayer || !player || player.IsDead()) return false;
        Container container = grave.GetComponent<Container>();
        if (!container || !OwnGrave(container.GetInventory(), player)) return false;
        Inventory source = container.GetInventory(), own = player.GetInventory();
        if (own.GetTotalWeight() + source.GetTotalWeight() > player.GetMaxCarryWeight()) return false;
        GraveRecoveryPlan plan = Plan(player, source);
        return plan.AllFit && plan.Slots.Any(slot => slot >= 0);
    }

    internal static void RestoreSlots(Inventory destination, Inventory source, HashSet<string>? markers)
    {
        Player player = Player.m_localPlayer;
        if (markers == null || !Enabled() || !CharacterSlots.Enabled() || !player || destination != player.GetInventory()) return;
        ZNetView? view = OwnGrave(source, player);
        // Native TakeAll/Open must already have obtained container ownership before any write.
        if (!view || !view!.IsOwner()) return;
        ItemDrop.ItemData[] items = source.GetAllItems().ToArray();
        GraveRecoveryPlan plan = Plan(player, source);
        for (int i = 0; i < items.Length; i++)
        {
            if (plan.Slots[i] < 0) continue;
            Vector2i slot = Position(plan.Slots[i]);
            if (!source.ContainsItem(items[i]) || destination.GetItemAt(slot.x, slot.y) != null) continue;
            destination.MoveItemToThis(source, items[i], items[i].m_stack, slot.x, slot.y);
        }
    }

    internal static void AfterTransfer(Inventory destination, HashSet<string>? markers)
    {
        var player = Player.m_localPlayer;
        if (markers == null || !player || destination != player.GetInventory()) return;
        foreach (var item in destination.GetAllItems())
        {
            if (item.m_customData.TryGetValue(ProvisionMarker, out var provision) && markers.Contains(provision))
                item.m_customData.Remove(ProvisionMarker);
            if (!item.m_customData.TryGetValue(Marker, out var value) || !markers.Contains(value)) continue;
            if (!GraveRecoveryTag.TryRead(value, player.GetPlayerID(), out int extra)) continue;
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
                    (inventory.GetItemAt(slot.x, slot.y) == null || inventory.GetItemAt(slot.x, slot.y) == item))
                {
                    item.m_gridPos = slot;
                    CharacterSlots.SyncEffects(player);
                }
                done = true; // Never replace a different utility already occupying that slot.
            }
            else if (!done) done = player.IsItemEquiped(item) || player.EquipItem(item, false);
            if (!done) continue;
            if (Enabled() && CharacterSlots.Enabled() && !player.IsDead()) CharacterSlots.Settle(player, null);
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
        {
            item.m_customData.Remove(TombstoneRecovery.Marker);
            item.m_customData.Remove(TombstoneRecovery.ProvisionMarker);
        }
    }
}

[HarmonyPatch]
internal static class ReclaimEquipmentPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(Inventory).GetMethods()
        .Where(method => method.Name == nameof(Inventory.MoveAll) || method.Name == nameof(Inventory.MoveItemToThis));
    private static void Prefix(Inventory __instance, Inventory fromInventory, MethodBase __originalMethod, out HashSet<string>? __state)
    {
        __state = TombstoneRecovery.BeforeTransfer(__instance, fromInventory);
        if (__originalMethod.Name == nameof(Inventory.MoveAll)) TombstoneRecovery.RestoreSlots(__instance, fromInventory, __state);
    }
    private static void Postfix(Inventory __instance, HashSet<string>? __state) =>
        TombstoneRecovery.AfterTransfer(__instance, __state);
}

// Keep native Interact, its ownership/access RPCs, carry-weight gate and recovery callbacks.
// Only correct the fit prediction to account for empty worn/provision slots.
[HarmonyPatch(typeof(TombStone), "EasyFitInInventory")]
internal static class GraveSlotFitPatch
{
    private static void Postfix(TombStone __instance, Player player, ref bool __result)
    {
        if (!__result && TombstoneRecovery.CanAutoReclaim(__instance, player)) __result = true;
    }
}
