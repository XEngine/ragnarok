using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironbound.Features.Equipment;

internal enum SortMode { Name, Category, Weight }

// Sorts the player's grid: first merges partial stacks of the same item, then lays everything out in
// reading order. Only the grid moves; character, provision and quiver slots stay put, and so does the
// hotbar row when asked. Moving an equipped item does not take it off.
internal static class InventorySort
{
    // Returns the number of items laid out, or -1 when nothing could be done (an item is being dragged).
    internal static int Run(Player player, SortMode mode, bool keepHotbar, ItemDrop.ItemData? dragged)
    {
        if (dragged != null) return -1;
        Inventory inventory = player.GetInventory();
        int start = keepHotbar ? 1 : 0, rows = CharacterSlots.GridRows(inventory), width = inventory.GetWidth();
        List<ItemDrop.ItemData> items = inventory.GetAllItems()
            .Where(i => CharacterSlots.InGrid(inventory, i.m_gridPos) && i.m_gridPos.y >= start).ToList();

        Merge(player.IsItemEquiped, inventory, items);
        items.Sort(Comparer(mode));

        int index = 0;
        for (int y = start; y < rows && index < items.Count; y++)
            for (int x = 0; x < width && index < items.Count; x++)
                items[index++].m_gridPos = new Vector2i(x, y);
        CharacterSlots.Notify(inventory);
        return items.Count;
    }


    // Sorts an open chest the same way: merge partial stacks, then lay everything out in reading order.
    internal static int Run(Inventory chest, SortMode mode, ItemDrop.ItemData? dragged)
    {
        if (dragged != null) return -1;
        List<ItemDrop.ItemData> items = chest.GetAllItems().ToList();
        Merge(_ => false, chest, items);
        items.Sort(Comparer(mode));
        int width = chest.GetWidth(), index = 0;
        for (int y = 0; y < chest.GetHeight() && index < items.Count; y++)
            for (int x = 0; x < width && index < items.Count; x++)
                items[index++].m_gridPos = new Vector2i(x, y);
        CharacterSlots.Notify(chest);
        return items.Count;
    }

    // Partial stacks of one item fill each other, the equipped and fullest stacks first; stacks left
    // empty leave the inventory. Items carrying custom data are never merged.
    private static void Merge(Func<ItemDrop.ItemData, bool> equipped, Inventory inventory, List<ItemDrop.ItemData> items)
    {
        var groups = items
            .Where(i => i.m_shared.m_maxStackSize > 1 && i.m_customData.Count == 0)
            .GroupBy(i => (i.m_shared.m_name, i.m_quality, i.m_worldLevel))
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var group in groups)
        {
            List<ItemDrop.ItemData> stacks = group
                .OrderByDescending(equipped)
                .ThenByDescending(i => i.m_stack)
                .ToList();
            int remaining = stacks.Sum(i => i.m_stack), max = stacks[0].m_shared.m_maxStackSize;
            foreach (ItemDrop.ItemData stack in stacks)
            {
                stack.m_stack = Math.Min(max, remaining);
                remaining -= stack.m_stack;
                if (stack.m_stack > 0 || equipped(stack)) continue;
                inventory.RemoveItem(stack);
                items.Remove(stack);
            }
        }
    }

    private static Comparison<ItemDrop.ItemData> Comparer(SortMode mode) => mode switch
    {
        SortMode.Category => (a, b) => Chain(Rank(a).CompareTo(Rank(b)), ByName(a, b)),
        SortMode.Weight => (a, b) => Chain(b.GetWeight().CompareTo(a.GetWeight()), ByName(a, b)),
        _ => ByName
    };

    // Same name: higher quality first, then bigger stack.
    private static int ByName(ItemDrop.ItemData a, ItemDrop.ItemData b) =>
        Chain(string.Compare(Name(a), Name(b), StringComparison.InvariantCultureIgnoreCase),
            Chain(b.m_quality.CompareTo(a.m_quality), b.m_stack.CompareTo(a.m_stack)));

    private static int Chain(int first, int then) => first != 0 ? first : then;

    private static string Name(ItemDrop.ItemData item) =>
        Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;

    // Weapons, shields, ammo, tools, armour, food and potions, fish, materials, trophies, the rest.
    private static int Rank(ItemDrop.ItemData item) => item.m_shared.m_itemType switch
    {
        ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon or
            ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow or
            ItemDrop.ItemData.ItemType.Attach_Atgeir => 0,
        ItemDrop.ItemData.ItemType.Shield => 1,
        ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable => 2,
        ItemDrop.ItemData.ItemType.Tool or ItemDrop.ItemData.ItemType.Torch => 3,
        ItemDrop.ItemData.ItemType.Helmet or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs or
            ItemDrop.ItemData.ItemType.Shoulder or ItemDrop.ItemData.ItemType.Hands or
            ItemDrop.ItemData.ItemType.Utility or ItemDrop.ItemData.ItemType.Trinket => 4,
        ItemDrop.ItemData.ItemType.Consumable => 5,
        ItemDrop.ItemData.ItemType.Fish => 6,
        ItemDrop.ItemData.ItemType.Material => 7,
        ItemDrop.ItemData.ItemType.Trophy => 8,
        _ => 9
    };
}
