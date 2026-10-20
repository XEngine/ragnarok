using UnityEngine;

namespace Ragnarok.UI.Windows;

internal enum PackCategory { All, Gear, Materials, Food, Trophies }

// Local presentation categories only; never used to move or spend inventory contents.
internal static class InventoryCategories
{
    internal static readonly Color Gold = Palette.Hex(0xD4A955);
    internal static readonly Color Armor = Palette.Hex(0x8FC1E8);
    internal static readonly Color Material = Palette.Hex(0xA99F8D);
    internal static readonly Color Food = Palette.Hex(0xEDB183);
    internal static readonly Color Trophy = Palette.Hex(0x9BC48A);

    internal static PackCategory Of(ItemDrop.ItemData.SharedData item)
    {
        if (item.m_itemType == ItemDrop.ItemData.ItemType.Trophy) return PackCategory.Trophies;
        if (item.m_itemType == ItemDrop.ItemData.ItemType.Consumable ||
            CraftFilter.Of(item) is CraftCategory.Health or CraftCategory.Stamina or CraftCategory.Eitr or CraftCategory.Meads)
            return PackCategory.Food;
        return CraftFilter.Of(item.m_itemType) is CraftCategory.Weapons or CraftCategory.Shields or CraftCategory.Armor or
            CraftCategory.Ammo or CraftCategory.Tools ? PackCategory.Gear : PackCategory.Materials;
    }

    internal static Color ColorOf(ItemDrop.ItemData.SharedData item) => Of(item) switch
    {
        PackCategory.Food => Food,
        PackCategory.Trophies => Trophy,
        PackCategory.Gear => CraftFilter.Of(item.m_itemType) == CraftCategory.Armor ? Armor : Gold,
        _ => Material
    };
}
