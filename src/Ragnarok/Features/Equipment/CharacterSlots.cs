using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ragnarok.Features.Equipment;

// Character and provision slots for the Ragnarök inventory. They are positions in the player's own
// inventory far below its grid (fixed rows GearRow and ProvisionRow), so items there save, weigh and
// get used like any other, but vanilla's grid, free-slot search and "inventory full" checks never see
// them: a helmet or a stack of meads in these slots leaves every grid slot free. Fixed rows keep them
// in place when something (a world setting, Better Archery) changes the inventory's height.
//
// Worn gear lives in its character slot: equipping moves it in, taking it off moves it back to the grid.
// Utility has two extra slots: Valheim wears one utility item, so items there are not equipped, but
// their status effect (Megingjord's strength, the wishbone's finder, Wisplight's light) is added to
// the player's equipment effects. With Better Archery, its worn quiver gets a gear slot too.
// Valheim loads items at their saved position without a bounds check, so nothing is lost when the
// slots are turned off; they are emptied back into the grid instead. Code that drops or re-homes
// "out of bounds" items (Valheim's DropInvalidItems, Better Archery's repairs) is kept off them.
internal static class CharacterSlots
{
    internal const int GearRow = 64, ProvisionRow = 65;

    internal static readonly ItemDrop.ItemData.ItemType[] Gear =
    {
        ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs,
        ItemDrop.ItemData.ItemType.Shoulder, ItemDrop.ItemData.ItemType.Utility, ItemDrop.ItemData.ItemType.Trinket,
        ItemDrop.ItemData.ItemType.Utility, ItemDrop.ItemData.ItemType.Utility
    };

    // Gear slots 6 and 7: the extra utility slots. Slot 8: Better Archery's quiver.
    internal static readonly int[] ExtraUtility = { 6, 7 };
    internal const int QuiverIndex = 8;

    internal const int Provisions = 6;

    private static readonly MethodInfo Changed = AccessTools.Method(typeof(Inventory), "Changed");
    private static readonly MethodInfo UpdateEffects = AccessTools.Method(typeof(Humanoid), "UpdateEquipmentStatusEffects");
    private static readonly AccessTools.FieldRef<Inventory, List<ItemDrop.ItemData>> Items =
        AccessTools.FieldRefAccess<Inventory, List<ItemDrop.ItemData>>("m_inventory");
    private static readonly List<ItemDrop.ItemData> appliedExtras = new();
    private static Inventory? migrated;

    // Set by the plugin: on while the Ragnarök inventory (which draws these slots) is in use.
    internal static Func<bool> Enabled = () => false;
    internal static Func<bool> ExtraUtilityEnabled = () => false;

    internal static bool IsPlayer(Inventory inventory) =>
        Player.m_localPlayer && Player.m_localPlayer.GetInventory() == inventory;

    internal static Vector2i GearSlot(int index) => new(index, GearRow);

    internal static Vector2i ProvisionSlot(int index) => new(index, ProvisionRow);

    internal static bool IsGearSlot(Vector2i pos) => pos.y == GearRow && pos.x >= 0 && pos.x <= QuiverIndex;

    internal static bool IsProvisionSlot(Vector2i pos) => pos.y == ProvisionRow && pos.x >= 0 && pos.x < Provisions;

    internal static bool IsExtraUtility(Vector2i pos) => pos.y == GearRow && Array.IndexOf(ExtraUtility, pos.x) >= 0;

    // The gear slots whose item is really worn: everything but the extra utility slots.
    internal static bool IsWornSlot(Vector2i pos) => IsGearSlot(pos) && !IsExtraUtility(pos);

    internal static bool IsSlot(Vector2i pos) => IsGearSlot(pos) || IsProvisionSlot(pos);

    // The rows Valheim's grid shows. Better Archery's spacer and quiver rows sit below them.
    internal static int GridRows(Inventory inventory) =>
        IsPlayer(inventory) && BetterArcheryCompat.Active ? BetterArcheryCompat.BaseRows : inventory.GetHeight();

    internal static bool InGrid(Inventory inventory, Vector2i pos) =>
        pos.x >= 0 && pos.y >= 0 && pos.x < inventory.GetWidth() && pos.y < GridRows(inventory);

    internal static bool InSlot(ItemDrop.ItemData item) => IsSlot(item.m_gridPos);

    // Whether an item may sit at a position: grid positions take anything, slots only their kind.
    internal static bool Accepts(Inventory inventory, Vector2i pos, ItemDrop.ItemData item)
    {
        if (IsExtraUtility(pos) && !ExtraUtilityEnabled()) return false;
        if (IsGearSlot(pos))
            return pos.x == QuiverIndex
                ? BetterArcheryCompat.Active && BetterArcheryCompat.IsQuiver(item)
                : item.m_shared.m_itemType == Gear[pos.x];
        if (IsProvisionSlot(pos)) return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable;
        if (IsPlayer(inventory) && BetterArcheryCompat.IsArrowSlot(pos)) return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo;
        return InGrid(inventory, pos);
    }

    // A slot position counts as usable only while the slots are on.
    internal static bool Allows(Inventory inventory, Vector2i pos, ItemDrop.ItemData item) =>
        !IsSlot(pos) || !IsPlayer(inventory) || (Enabled() && Accepts(inventory, pos, item));

    // Whether InventoryGrid.DropItem may run. A drop onto a different, unstackable item swaps the two,
    // and vanilla removes the dragged item before checking the swap fits; so the target's item must be
    // allowed where the dragged one came from, or the dragged item would vanish.
    internal static bool CanDrop(Inventory to, Inventory from, ItemDrop.ItemData item, Vector2i pos)
    {
        if (!Allows(to, pos, item)) return false;
        ItemDrop.ItemData target = to.GetItemAt(pos.x, pos.y);
        if (target == null || target == item) return true;
        bool swaps = target.m_shared.m_name != item.m_shared.m_name || target.m_shared.m_maxStackSize == 1 ||
                     (item.m_shared.m_maxQuality > 1 && target.m_quality != item.m_quality);
        return !swaps || Allows(from, item.m_gridPos, target);
    }

    internal static int GearIndex(ItemDrop.ItemData item) => Array.IndexOf(Gear, item.m_shared.m_itemType);

    // Where an item lives while worn, if it has a character slot.
    internal static Vector2i? WornSlot(ItemDrop.ItemData item)
    {
        int index = GearIndex(item);
        if (index >= 0) return GearSlot(index);
        if (BetterArcheryCompat.Active && BetterArcheryCompat.IsQuiver(item)) return GearSlot(QuiverIndex);
        return null;
    }

    // Per frame while enabled: worn gear into its slot, gear taken off back to the grid.
    internal static void Settle(Player player, ItemDrop.ItemData? dragged)
    {
        Inventory inventory = player.GetInventory();
        Migrate(inventory);
        bool moved = false;
        // Worn first: gear put on from the grid or from an extra utility slot trades places with
        // whatever sits in its slot, so a utility item put on from an extra slot swaps with the old one.
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            if (item == dragged || !player.IsItemEquiped(item) || WornSlot(item) is not { } slot) continue;
            if (item.m_gridPos == slot || !(InGrid(inventory, item.m_gridPos) || IsExtraUtility(item.m_gridPos))) continue;
            ItemDrop.ItemData occupant = inventory.GetItemAt(slot.x, slot.y);
            if (occupant != null && occupant == dragged) continue;
            if (occupant != null) occupant.m_gridPos = item.m_gridPos;
            item.m_gridPos = slot;
            moved = true;
        }
        // Then off: gear no longer worn leaves its slot for the grid, as do extra utility items once
        // those slots are turned off.
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            if (item == dragged || player.IsItemEquiped(item)) continue;
            bool leave = IsWornSlot(item.m_gridPos) || (IsExtraUtility(item.m_gridPos) && !ExtraUtilityEnabled());
            if (!leave || FreeGridSlot(inventory) is not { } free) continue;
            item.m_gridPos = free;
            moved = true;
        }
        if (moved) Notify(inventory);
    }

    // Before the fixed rows, the slots were the two rows right under the grid. Once per inventory,
    // carry anything still there across (only without Better Archery, whose rows those are now).
    private static void Migrate(Inventory inventory)
    {
        if (migrated == inventory) return;
        migrated = inventory;
        if (BetterArcheryCompat.Active) return;
        int gear = inventory.GetHeight(), provisions = gear + 1;
        bool moved = false;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            Vector2i pos = item.m_gridPos;
            if (pos.y == gear && pos.x >= 0 && pos.x < Gear.Length) item.m_gridPos = GearSlot(pos.x);
            else if (pos.y == provisions && pos.x >= 0 && pos.x < Provisions) item.m_gridPos = ProvisionSlot(pos.x);
            else continue;
            moved = true;
        }
        if (moved) Notify(inventory);
    }

    // Right click on a utility item, before Valheim's own use: with a utility already worn, an item
    // from the grid goes to a free extra slot instead of replacing it, and an item in an extra slot
    // goes back to the grid. Returns false to let Valheim handle the click (equip, unequip, swap).
    internal static bool UseUtility(Player player, ItemDrop.ItemData item)
    {
        if (!Enabled() || !ExtraUtilityEnabled() || item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility) return false;
        Inventory inventory = player.GetInventory();
        if (IsExtraUtility(item.m_gridPos))
        {
            if (FreeGridSlot(inventory) is not { } free) return false;
            item.m_gridPos = free;
            Notify(inventory);
            return true;
        }
        if (!InGrid(inventory, item.m_gridPos)) return false;
        Vector2i worn = GearSlot(GearIndex(item));
        if (inventory.GetItemAt(worn.x, worn.y) == null) return false;
        foreach (int x in ExtraUtility)
        {
            if (inventory.GetItemAt(x, GearRow) != null) continue;
            item.m_gridPos = GearSlot(x);
            Notify(inventory);
            return true;
        }
        return false;
    }

    // Items in the extra utility slots whose effect applies right now.
    internal static IEnumerable<ItemDrop.ItemData> ActiveExtras(Player player)
    {
        if (!Enabled() || !ExtraUtilityEnabled()) yield break;
        Inventory inventory = player.GetInventory();
        foreach (int x in ExtraUtility)
        {
            ItemDrop.ItemData item = inventory.GetItemAt(x, GearRow);
            if (item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility && !player.IsItemEquiped(item))
                yield return item;
        }
    }

    // Called per frame: when the extra utility items change, Valheim recomputes the equipment effects
    // (through the patch below, which adds theirs).
    internal static void SyncEffects(Player player)
    {
        List<ItemDrop.ItemData> now = ActiveExtras(player).ToList();
        if (now.SequenceEqual(appliedExtras)) return;
        appliedExtras.Clear();
        appliedExtras.AddRange(now);
        UpdateEffects.Invoke(player, null);
    }

    // On unload: drop the extra effects (the patch is gone, so Valheim's own pass removes them).
    internal static void ClearEffects()
    {
        appliedExtras.Clear();
        if (Player.m_localPlayer) UpdateEffects.Invoke(Player.m_localPlayer, null);
    }

    internal static void AddExtraEffects(HashSet<StatusEffect> effects, Humanoid humanoid)
    {
        if (humanoid != Player.m_localPlayer || humanoid is not Player player) return;
        foreach (ItemDrop.ItemData item in ActiveExtras(player))
            if (item.m_shared.m_equipStatusEffect)
                effects.Add(item.m_shared.m_equipStatusEffect);
    }

    // While disabled: anything left in the slots goes back into the grid, as room allows.
    internal static void Empty(Inventory inventory)
    {
        Migrate(inventory);
        bool moved = false;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
        {
            if (!IsSlot(item.m_gridPos) || FreeGridSlot(inventory) is not { } free) continue;
            item.m_gridPos = free;
            moved = true;
        }
        if (moved) Notify(inventory);
    }

    internal static Vector2i? FreeGridSlot(Inventory inventory)
    {
        for (int y = GridRows(inventory) - 1; y >= 0; y--)
            for (int x = 0; x < inventory.GetWidth(); x++)
                if (inventory.GetItemAt(x, y) == null)
                    return new Vector2i(x, y);
        return null;
    }

    internal static int ItemsInGrid(Inventory inventory)
    {
        int count = 0;
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            if (InGrid(inventory, item.m_gridPos)) count++;
        return count;
    }

    internal static bool AnyOutsideGrid(Inventory inventory)
    {
        foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            if (!InGrid(inventory, item.m_gridPos)) return true;
        return false;
    }

    internal static void Notify(Inventory inventory) => Changed.Invoke(inventory, new object[] { false, false });

    // Lift the slot items out of an inventory while code that drops "invalid" items runs, then put
    // them back (SlotShelter patches).
    internal static List<ItemDrop.ItemData>? Shelter(Inventory? inventory)
    {
        if (inventory == null) return null;
        List<ItemDrop.ItemData> items = Items(inventory);
        List<ItemDrop.ItemData> held = items.Where(i => IsSlot(i.m_gridPos)).ToList();
        if (held.Count == 0) return null;
        items.RemoveAll(held.Contains);
        return held;
    }

    internal static void Unshelter(Inventory? inventory, List<ItemDrop.ItemData>? held)
    {
        if (inventory == null || held == null) return;
        List<ItemDrop.ItemData> items = Items(inventory);
        foreach (ItemDrop.ItemData item in held)
            if (!items.Contains(item)) items.Add(item);
        Notify(inventory);
    }
}

// Moves into a slot (drag, split, swap) go through this private AddItem, which rejects rows past the
// grid. Allow the slots, and only for items of the slot's kind.
[HarmonyPatch(typeof(Inventory), "AddItem",
    new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) })]
internal static class SlotAddItemPatch
{
    private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int x, int y, ref bool skipValidPositionCheck, ref bool __result)
    {
        Vector2i pos = new(x, y);
        if (!CharacterSlots.IsSlot(pos) || !CharacterSlots.IsPlayer(__instance)) return true;
        if (!CharacterSlots.Allows(__instance, pos, item))
        {
            __result = false;
            return false;
        }
        skipValidPositionCheck = true;
        return true;
    }
}

// Adds the extra utility items' effects to the set Valheim builds from the worn gear, just before it
// diffs that set against the active effects. Inserted ahead of the first read of m_equipmentStatusEffects.
[HarmonyPatch(typeof(Humanoid), "UpdateEquipmentStatusEffects")]
internal static class ExtraUtilityEffectsPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();
        FieldInfo active = AccessTools.Field(typeof(Humanoid), "m_equipmentStatusEffects");
        int created = code.FindIndex(i => i.opcode == OpCodes.Newobj && i.operand is ConstructorInfo c &&
                                          c.DeclaringType == typeof(HashSet<StatusEffect>));
        int read = created < 0 ? -1 : code.FindIndex(created, i => i.LoadsField(active));
        if (created < 0 || read < 1 || !code[created + 1].IsStloc() || !code[read - 1].IsLdarg(0))
        {
            UnityEngine.Debug.LogError("[Ragnarok] Extra utility slots: UpdateEquipmentStatusEffects changed shape; their effects are off.");
            return code;
        }
        CodeInstruction load = Load(code[created + 1]);
        load.MoveLabelsFrom(code[read - 1]);
        code.InsertRange(read - 1, new[]
        {
            load,
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CharacterSlots), nameof(CharacterSlots.AddExtraEffects)))
        });
        return code;
    }

    private static CodeInstruction Load(CodeInstruction store)
    {
        if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
        if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
        if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
        if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
        return new CodeInstruction(OpCodes.Ldloc, store.operand);
    }
}

// Covers every grid, vanilla's chest panel included: refuse drops that would swap an item into a
// slot that cannot take it (vanilla would lose the dragged item).
[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
internal static class SlotDropItemPatch
{
    private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
    {
        if (CharacterSlots.CanDrop(__instance.GetInventory(), fromInventory, item, pos)) return true;
        __result = false;
        return false;
    }
}

// Valheim counts every item against the grid's size; only items in the grid take grid space. Runs
// after Better Archery's own version (which leaves out its quiver) and settles on the grid count.
[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetEmptySlots))]
internal static class SlotGetEmptySlotsPatch
{
    private static void Postfix(Inventory __instance, ref int __result)
    {
        if (!CharacterSlots.AnyOutsideGrid(__instance)) return;
        __result = __instance.GetWidth() * CharacterSlots.GridRows(__instance) - CharacterSlots.ItemsInGrid(__instance);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveEmptySlot))]
internal static class SlotHaveEmptySlotPatch
{
    private static void Postfix(Inventory __instance, ref bool __result)
    {
        if (CharacterSlots.AnyOutsideGrid(__instance)) __result = __instance.GetEmptySlots() > 0;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
internal static class SlotCanAddItemPatch
{
    private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
    {
        if (!CharacterSlots.AnyOutsideGrid(__instance)) return;
        if (stack <= 0) stack = item.m_stack;
        int free = __instance.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) +
                   __instance.GetEmptySlots() * item.m_shared.m_maxStackSize;
        __result = free >= stack;
    }
}

// The grave copies the player's grid size, so items from the slots would land out of sight: give them
// free grave slots, adding rows when the grave is full.
[HarmonyPatch(typeof(Inventory), nameof(Inventory.MoveInventoryToGrave))]
internal static class SlotGravePatch
{
    private static void Postfix(Inventory __instance)
    {
        List<ItemDrop.ItemData> outside = __instance.GetAllItems().Where(i => CharacterSlots.IsSlot(i.m_gridPos)).ToList();
        if (outside.Count == 0) return;
        foreach (ItemDrop.ItemData item in outside)
        {
            if (CharacterSlots.FreeGridSlot(__instance) is not { } free)
            {
                __instance.SetHeight(__instance.GetHeight() + 1);
                free = new Vector2i(0, __instance.GetHeight() - 1);
            }
            item.m_gridPos = free;
        }
        CharacterSlots.Notify(__instance);
    }
}

// Valheim drops "invalid positioned" items whenever the inventory is resized, which happens on every
// spawn (Player.OnSpawned → SetInventorySize). Keep the slot items out of its reach.
[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropInvalidItems))]
internal static class SlotDropInvalidPatch
{
    private static void Prefix(Humanoid __instance, out List<ItemDrop.ItemData>? __state) =>
        __state = __instance is Player ? CharacterSlots.Shelter(__instance.GetInventory()) : null;

    private static Exception? Finalizer(Humanoid __instance, List<ItemDrop.ItemData>? __state, Exception? __exception)
    {
        CharacterSlots.Unshelter(__instance.GetInventory(), __state);
        return __exception;
    }
}

// Better Archery re-homes or drops every item below its quiver row: after a resize (RecoverHiddenRowItems)
// and after taking a tombstone back. Same shelter for both.
[HarmonyPatch]
internal static class SlotBetterArcheryShelterPatch
{
    private static IEnumerable<MethodBase> Targets()
    {
        MethodBase? repair = AccessTools.Method("BetterArchery.QuiverInventoryRepair:RecoverHiddenRowItems");
        if (repair != null) yield return repair;
        Type? tombstone = AccessTools.TypeByName("BetterArchery.Tombstone");
        Type? takeAll = tombstone == null ? null : AccessTools.Inner(tombstone, "TombStone_OnTakeAllSuccess_Patch");
        MethodBase? postfix = takeAll == null ? null : AccessTools.Method(takeAll, "Postfix");
        if (postfix != null) yield return postfix;
    }

    private static bool Prepare() => BetterArcheryCompat.Loaded && Targets().Any();

    private static IEnumerable<MethodBase> TargetMethods() => Targets();

    private static Inventory? Subject(object[] __args) =>
        (__args.OfType<Player>().FirstOrDefault() ?? Player.m_localPlayer)?.GetInventory();

    private static void Prefix(object[] __args, out List<ItemDrop.ItemData>? __state) =>
        __state = CharacterSlots.Shelter(Subject(__args));

    private static Exception? Finalizer(object[] __args, List<ItemDrop.ItemData>? __state, Exception? __exception)
    {
        CharacterSlots.Unshelter(Subject(__args), __state);
        return __exception;
    }
}
