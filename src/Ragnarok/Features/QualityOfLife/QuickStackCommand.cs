using System;
using HarmonyLib;

namespace Ragnarok.Features.QualityOfLife;

// Both inventory styles supply their selected category.
// Both paths preserve assigned gear/provisions/quiver slots and the transfer layer's protections.
internal static class QuickStackCommand
{
    internal static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
        AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");

    internal static string? Run(InventoryGui gui, Func<ItemDrop.ItemData, bool> include)
    {
        Player player = Player.m_localPlayer;
        if (!player || !gui || NearbyStorage.StorageRuntime.Registry is not { } registry) return null;
        if (!NearbyStorage.StorageRuntime.Settings.Enabled.Value)
            return ServerConfig.Instance?.Controlled(NearbyStorage.StorageRuntime.Settings.Enabled) == true ? "Nearby Chests is off on this server" : "Nearby Chests is off";
        NearbyStorage.QuickStackResult r =
            NearbyStorage.QuickStack.Run(player, registry,
                item => Assigned(item) || !include(item),
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

    // Category-scoped stacking must retain deliberate gear/provision/quiver placement too.
    private static bool Assigned(ItemDrop.ItemData item) =>
        Equipment.CharacterSlots.InSlot(item) || Equipment.BetterArcheryCompat.IsArrowSlot(item.m_gridPos);
}
