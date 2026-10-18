using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.Equipment;

// Clicks on a character, provision or quiver slot, from either character panel (Ragnarök or vanilla
// style). They go to Valheim's own player-grid handlers with the slot's position, so picking up,
// splitting, swapping and moving to chests work as anywhere else; the patches below add the slot rules.
internal static class SlotActions
{
    internal static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
        AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
    internal static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory =
        AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");

    // Same modifiers as Valheim: Shift splits, Ctrl moves to the open chest (or drops), right click uses.
    internal static void Click(InventoryGui gui, InventoryGrid grid, Vector2i pos, ItemDrop.ItemData? item, bool right)
    {
        if (right)
        {
            grid.m_onRightClick?.Invoke(grid, item, pos);
            return;
        }
        InventoryGrid.Modifier mod = InventoryGrid.Modifier.Select;
        if (ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift)) mod = InventoryGrid.Modifier.Split;
        else if (ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) mod = InventoryGrid.Modifier.Move;
        // Refuse a drop the slot cannot take before Valheim starts (it would unequip things first).
        ItemDrop.ItemData? drag = DragItem(gui);
        Inventory? from = DragInventory(gui);
        if (drag != null && from != null && !CharacterSlots.CanDrop(grid.GetInventory(), from, drag, pos)) return;
        grid.m_onSelected?.Invoke(grid, item, pos, mod);
    }

    // Gear in a worn slot is put on; a utility item in an extra slot is not worn (its effect is added).
    internal static void Fit(Player player, Inventory inventory, Vector2i pos)
    {
        if (inventory.GetItemAt(pos.x, pos.y) is not { } item) return;
        bool worn = player.IsItemEquiped(item);
        if (CharacterSlots.IsWornSlot(pos) && !worn) player.EquipItem(item);
        else if (CharacterSlots.IsExtraUtility(pos) && worn) player.UnequipItem(item);
    }
}

// Every drop, on any grid or slot: gear dropped on its slot is put on, gear dragged from its slot into
// the grid is taken off (Valheim would put it back on), and an item swapped into a slot fits it.
[HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
internal static class SlotSelectPatch
{
    internal sealed class Drop
    {
        internal ItemDrop.ItemData Item = null!;
        internal Vector2i Origin;
        internal bool FromGear, FromWorn;
    }

    private static void Prefix(InventoryGui __instance, out Drop? __state)
    {
        __state = null;
        Player player = Player.m_localPlayer;
        ItemDrop.ItemData drag = SlotActions.DragItem(__instance);
        if (!player || drag == null || !CharacterSlots.Enabled()) return;
        bool mine = SlotActions.DragInventory(__instance) == player.GetInventory();
        __state = new Drop
        {
            Item = drag,
            Origin = drag.m_gridPos,
            FromGear = mine && CharacterSlots.IsGearSlot(drag.m_gridPos),
            FromWorn = mine && CharacterSlots.IsWornSlot(drag.m_gridPos)
        };
    }

    private static void Postfix(InventoryGrid grid, Vector2i pos, Drop? __state)
    {
        Player player = Player.m_localPlayer;
        if (__state == null || !player) return;
        Inventory own = player.GetInventory();
        bool onOwn = grid.GetInventory() == own;
        if (onOwn) SlotActions.Fit(player, own, pos);
        // A swap leaves the target's item in the slot the drag came from.
        if (__state.FromGear) SlotActions.Fit(player, own, __state.Origin);
        if (__state.FromWorn && onOwn && CharacterSlots.InGrid(own, pos) && own.GetItemAt(pos.x, pos.y) is { } landed &&
            player.IsItemEquiped(landed))
            player.UnequipItem(landed);
    }
}

// Right click on a utility item with one already worn: fill a free extra utility slot instead of
// swapping (and from an extra slot, back to the grid). Valheim's grid and both panels come through here.
[HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
internal static class SlotRightClickPatch
{
    private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
    {
        Player player = Player.m_localPlayer;
        if (item == null || !player || grid.GetInventory() != player.GetInventory() || SlotActions.DragItem(__instance) != null)
            return true;
        return !CharacterSlots.UseUtility(player, item);
    }
}
