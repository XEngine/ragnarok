using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironbound.Features.NearbyStorage;

internal static class InventoryWithdrawal
{
    internal static void Remove(IReadOnlyList<Inventory> sources, string name, int amount, int quality, bool matchWorldLevel)
    {
        // Read real contents, bypassing the scoped CountItems augmentation.
        int[] available = sources.Select(i => i.GetAllItems().Where(item => item.m_shared.m_name == name &&
            (quality < 0 || item.m_quality == quality) && (!matchWorldLevel || item.m_worldLevel >= Game.m_worldLevel))
            .Sum(item => item.m_stack)).ToArray();
        if (!WithdrawalPlan.TryCreate(amount, available, out int[] withdrawals))
            throw new InvalidOperationException("Ironbound: resources changed after the availability check; refusing a partial withdrawal. " +
                $"item={name} requested={amount} quality={quality} worldLevel={matchWorldLevel}/{Game.m_worldLevel} " +
                $"available=[{string.Join(",", available)}] (player first, then usable chests)");

        ResourceScope? saved = ResourceScope.Current;
        ResourceScope.Current = null;
        try
        {
            for (int i = 0; i < sources.Count; i++)
                if (withdrawals[i] > 0) sources[i].RemoveItem(name, withdrawals[i], quality, matchWorldLevel);
        }
        finally { ResourceScope.Current = saved; }
    }
}
