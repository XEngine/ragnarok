using System;
using System.Collections.Generic;

namespace Ironbound.HUD;

internal static class RangedAmmo
{
    internal static bool UsesArrowsOrBolts(ItemDrop.ItemData? weapon) => weapon != null &&
        (weapon.m_shared.m_skillType == Skills.SkillType.Bows || weapon.m_shared.m_skillType == Skills.SkillType.Crossbows) &&
        !string.IsNullOrEmpty(weapon.m_shared.m_ammoType);

    // Only the selected ammunition, across all carried stacks (including quiver rows).
    // Chest resources and other arrow/bolt varieties never inflate the readout.
    internal static int Count(IEnumerable<ItemDrop.ItemData> inventory, ItemDrop.ItemData ammo)
    {
        long count = 0;
        foreach (ItemDrop.ItemData item in inventory)
            if (item.m_shared.m_name == ammo.m_shared.m_name && item.m_shared.m_ammoType == ammo.m_shared.m_ammoType)
                count += Math.Max(0, item.m_stack);
        return (int)Math.Min(int.MaxValue, count);
    }
}
