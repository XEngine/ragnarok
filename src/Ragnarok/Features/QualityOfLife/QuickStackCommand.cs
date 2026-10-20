using System;
using HarmonyLib;

namespace Ragnarok.Features.QualityOfLife;

// Both inventory styles supply their selected category; callers without a scope use Keep settings.
// Both paths preserve assigned gear/provisions/quiver slots and the transfer layer's protections.
internal static class QuickStackCommand
{
    internal static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
        AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");

    internal static string? Run(InterfaceSettings settings, InventoryGui gui, Func<ItemDrop.ItemData, bool>? include = null)
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui || NearbyStorage.StorageRuntime.Registry is not { } registry) return null;
        if (!settings.Features.QuickStack.Value)
            return ServerConfig.Instance?.Controlled(settings.Features.QuickStack) == true ? "Quick stack is off on this server" : "Quick stack is off";
        NearbyStorage.QuickStackResult r =
            NearbyStorage.QuickStack.Run(player, registry,
                item => include == null ? Keep(settings, item) : Assigned(item) || !include(item),
                DragItem(gui), OpenContainer(gui));
        if (r.Pending) registry.ShowPending(player);
        return r.Items > 0
            ? $"{r.Items} item{(r.Items == 1 ? "" : "s")} → {r.Chests} chest{(r.Chests == 1 ? "" : "s")}"
            : r.Pending ? "Waiting for a chest" : "Nothing to stack";
    }

    // The Sort press, shared the same way: returns the line to show.
    internal static string? Sort(InterfaceSettings settings, InventoryGui gui, Equipment.SortMode mode)
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui) return null;
        int count = Equipment.InventorySort.Run(player, mode, settings.Features.SortKeepHotbar.Value, DragItem(gui));
        if (count < 0) return "Put the held item down first";
        return mode switch
        {
            Equipment.SortMode.Category => "Sorted by type",
            Equipment.SortMode.Weight => "Sorted by weight",
            _ => "Sorted by name"
        };
    }

    internal static readonly AccessTools.FieldRef<InventoryGui, Container> OpenContainer =
        AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

    // Sorts the chest that is open (only its owner may change it, which the one looking in is).
    internal static string? SortChest(InventoryGui gui, Equipment.SortMode mode)
    {
        if (!gui) return null;
        Container chest = OpenContainer(gui);
        if (!chest || !chest.IsOwner() || chest.GetInventory() is not { } inventory) return null;
        int count = Equipment.InventorySort.Run(inventory, mode, DragItem(gui));
        if (count < 0) return "Put the held item down first";
        return mode switch
        {
            Equipment.SortMode.Category => "Chest sorted by type",
            Equipment.SortMode.Weight => "Chest sorted by weight",
            _ => "Chest sorted by name"
        };
    }

    internal static bool Keep(InterfaceSettings settings, ItemDrop.ItemData item)
    {
        // Provisions and gear in the character slots were put there on purpose.
        if (Assigned(item)) return true;
        switch (item.m_shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Ammo:
            case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
            case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                return settings.Features.KeepWeapons.Value;
            case ItemDrop.ItemData.ItemType.Consumable:
                return settings.Features.KeepConsumables.Value;
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Torch:
                return settings.Features.KeepTools.Value;
            default:
                return false;
        }
    }

    // Category-scoped stacking must retain deliberate gear/provision/quiver placement too.
    private static bool Assigned(ItemDrop.ItemData item) =>
        Equipment.CharacterSlots.InSlot(item) || Equipment.BetterArcheryCompat.IsArrowSlot(item.m_gridPos);
}
