#if DEBUG
using System;
using BepInEx.Logging;

namespace Ragnarok.NearbyStorage;

// Opt-in checks against real Valheim Inventory objects; never uses a player's saves or chests.
internal static class RuntimeChecks
{
    internal static void Run(ManualLogSource log)
    {
        Inventory player = new("Ragnarok test player", null, 8, 4);
        Inventory first = new("Ragnarok test chest 1", null, 8, 4);
        Inventory second = new("Ragnarok test chest 2", null, 8, 4);
        Inventory[] sources = { player, first, second };
        int playerChanges = 0, firstChanges = 0, secondChanges = 0;
        player.m_onChanged += () => playerChanges++;
        first.m_onChanged += () => firstChanges++;
        second.m_onChanged += () => secondChanges++;

        Add(player, "$item_wood", 3);
        Add(first, "$item_wood", 4);
        Add(second, "$item_wood", 10);
        InventoryWithdrawal.Remove(sources, "$item_wood", 8, -1, true);
        Assert(player.CountItems("$item_wood") == 0 && first.CountItems("$item_wood") == 0 &&
            second.CountItems("$item_wood") == 9, "split withdrawal");
        Assert(player.NrOfItems() == 0 && first.NrOfItems() == 0, "depleted stack removal");
        Assert(playerChanges == 1 && firstChanges == 1 && secondChanges == 1, "container persistence callbacks");

        bool refused = false;
        try { InventoryWithdrawal.Remove(sources, "$item_wood", 10, -1, true); }
        catch (InvalidOperationException) { refused = true; }
        Assert(refused && second.CountItems("$item_wood") == 9 && secondChanges == 1, "shortage is atomic");

        Add(player, "$item_test_quality", 4, 1);
        Add(first, "$item_test_quality", 5, 2);
        InventoryWithdrawal.Remove(sources, "$item_test_quality", 3, 2, true);
        Assert(player.CountItems("$item_test_quality", 1) == 4 && first.CountItems("$item_test_quality", 2) == 2,
            "ingredient quality filtering");

        int oldWorldLevel = Game.m_worldLevel;
        try
        {
            Game.m_worldLevel = 1;
            Add(player, "$item_test_world", 5, 1, 0);
            Add(second, "$item_test_world", 5, 1, 1);
            InventoryWithdrawal.Remove(sources, "$item_test_world", 3, -1, true);
            Assert(player.CountItems("$item_test_world", -1, false) == 5 && second.CountItems("$item_test_world") == 2,
                "world-level filtering");
        }
        finally { Game.m_worldLevel = oldWorldLevel; }

        ItemDrop.ItemData coal = Add(first, "$item_coal", 2);
        Assert(first.RemoveItem(coal, 1) && coal.m_stack == 1, "fuel stack decrement");
        Assert(first.RemoveOneItem(coal) && !first.ContainsItem(coal), "cooking ingredient removal");
        Assert(ResourceScope.Current == null, "scope restoration");
        log.LogInfo("Ragnarok inventory checks PASS: split spending, shortage atomicity, quality/world filters, save callbacks and single-item consumption.");
    }

    private static ItemDrop.ItemData Add(Inventory inventory, string name, int amount, int quality = 1, int worldLevel = 0)
    {
        ItemDrop.ItemData item = new()
        {
            m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = 50, m_weight = 1f },
            m_stack = amount,
            m_quality = quality,
            m_worldLevel = worldLevel
        };
        inventory.GetAllItems().Add(item);
        return item;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Ragnarok inventory check failed: " + name);
    }
}
#endif
