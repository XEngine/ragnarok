using System;
using Ironbound.HUD;

internal static class Skills
{
    internal enum SkillType { None, Bows, Crossbows, Swords }
}
internal static class ItemDrop
{
    internal sealed class ItemData
    {
        internal sealed class SharedData
        {
            internal string m_name = "", m_ammoType = "";
            internal Skills.SkillType m_skillType;
        }
        internal readonly SharedData m_shared = new();
        internal int m_stack;
    }
}
internal static class RangedAmmoChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool result, string message) { checks++; if (!result) throw new Exception(message); }
        ItemDrop.ItemData Ammo(string name, string type, int count)
        {
            var item = new ItemDrop.ItemData { m_stack = count };
            item.m_shared.m_name = name; item.m_shared.m_ammoType = type;
            return item;
        }
        var bow = Ammo("Bow", "arrows", 1);
        bow.m_shared.m_skillType = Skills.SkillType.Bows;
        var crossbow = Ammo("Crossbow", "bolts", 1);
        crossbow.m_shared.m_skillType = Skills.SkillType.Crossbows;
        Check(RangedAmmo.UsesArrowsOrBolts(bow), "Bow did not enable ammo readout");
        Check(RangedAmmo.UsesArrowsOrBolts(crossbow), "Crossbow did not enable ammo readout");
        Check(!RangedAmmo.UsesArrowsOrBolts(null), "Unequipped weapon enabled ammo readout");
        bow.m_shared.m_ammoType = "";
        Check(!RangedAmmo.UsesArrowsOrBolts(bow), "Ammo-free bow enabled misleading readout");
        bow.m_shared.m_ammoType = "arrows";
        bow.m_shared.m_skillType = Skills.SkillType.Swords;
        Check(!RangedAmmo.UsesArrowsOrBolts(bow), "Other weapon enabled arrow readout");
        var selected = Ammo("Wood arrow", "arrows", 12);
        var items = new[] { selected, Ammo("Wood arrow", "arrows", 8),
            Ammo("Fire arrow", "arrows", 30), Ammo("Wood arrow", "bolts", 4), Ammo("Wood arrow", "arrows", -2) };
        Check(RangedAmmo.Count(items, selected) == 20, "Selected ammo stacks mixed with other varieties or categories");
        Check(RangedAmmo.Count(Array.Empty<ItemDrop.ItemData>(), selected) == 0, "Empty inventory did not show zero");
        Check(RangedAmmo.Count(new[] { Ammo("Wood arrow", "arrows", int.MaxValue), selected }, selected) == int.MaxValue,
            "Ammo count overflowed");
        Console.WriteLine($"PASS: {checks} ranged weapon and selected ammunition count checks.");
    }
}
