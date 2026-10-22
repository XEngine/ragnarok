using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Ironbound.Features.NearbyStorage;

internal readonly struct QuickStackResult
{
    internal readonly int Items, Chests;
    internal readonly bool Pending;

    internal QuickStackResult(int items, int chests, bool pending)
    {
        Items = items;
        Chests = chests;
        Pending = pending;
    }
}

// Puts carried items into nearby chests that already hold the same item, nearest chest first, like
// Valheim's "Stack all" but for every chest in range; with a chest open, only that one. Equipped and quest
// items never move; the caller decides which other kinds are kept. Chests owned by another player are
// requested first, exactly as for crafting, and filled on the next press once the owner hands them over.
internal static class QuickStack
{
    private static readonly MethodInfo Changed = AccessTools.Method(typeof(Inventory), "Changed");

    internal static QuickStackResult Run(Player player, ContainerRegistry registry, System.Func<ItemDrop.ItemData, bool> keep,
        ItemDrop.ItemData? dragged, Container? open = null)
    {
        Inventory inventory = player.GetInventory();
        // With a chest open, only that chest is filled (it is "in use", so never among the nearby ones);
        // otherwise every chest in range, nearest first.
        bool hasOpen = open && open!.IsOwner() && open.GetInventory() != null;
        if (open && (!hasOpen || !registry.CanAccess(open!, player, allowOpen: true)))
            return new QuickStackResult(0, 0, false);
        List<Container> chests = hasOpen ? new List<Container> { open! } : registry.Nearby(player, true);
        foreach (Container chest in chests)
            if (chest != open) registry.Refresh(chest);

        int moved = 0;
        bool pending = false;
        HashSet<Container> used = new();
        foreach (ItemDrop.ItemData item in inventory.GetAllItems().ToList())
        {
            if (item == dragged || item.m_equipped || player.IsItemEquiped(item) || item.m_shared.m_questItem || keep(item)) continue;
            foreach (Container chest in chests)
            {
                bool isOpen = hasOpen && chest == open;
                if (!registry.CanAccess(chest, player, allowOpen: isOpen)) continue;
                Inventory target = chest.GetInventory();
                if (!target.ContainsItemByName(item.m_shared.m_name)) continue;
                if (!isOpen && !registry.IsOwned(chest))
                {
                    registry.Request(chest, player);
                    pending = true;
                    continue;
                }
                int before = item.m_stack;
                // AddItem fills existing stacks, then a free slot; on failure the item keeps
                // only what did not fit, so nothing is duplicated or lost.
                if (target.AddItem(item))
                {
                    inventory.RemoveItem(item);
                    moved += before;
                    used.Add(chest);
                    break;
                }
                if (item.m_stack < before)
                {
                    moved += before - item.m_stack;
                    used.Add(chest);
                    Changed.Invoke(inventory, new object[] { false, false });
                }
            }
        }
        if (moved > 0) registry.InvalidatePreview();
        return new QuickStackResult(moved, used.Count, pending);
    }
}
