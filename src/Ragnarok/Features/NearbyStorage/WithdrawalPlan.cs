using System;
using System.Collections.Generic;

namespace Ragnarok.Features.NearbyStorage;

// Pure allocation logic: order is player inventory, then nearest containers.
internal static class WithdrawalPlan
{
    internal static bool TryCreate(int requested, IReadOnlyList<int> available, out int[] amounts)
    {
        if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested));
        amounts = new int[available.Count];
        long total = 0;
        foreach (int count in available)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(available));
            total += count;
        }
        // Never partially withdraw when the combined inventory cannot pay the cost.
        if (total < requested) return false;
        for (int i = 0; i < available.Count && requested > 0; i++)
        {
            amounts[i] = Math.Min(available[i], requested);
            requested -= amounts[i];
        }
        return true;
    }
}
