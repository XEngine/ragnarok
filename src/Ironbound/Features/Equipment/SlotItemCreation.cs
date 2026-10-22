using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ironbound.Features.Equipment;

// Native upgrading removes the old item then creates a replacement at its saved position.
// Teach that creation path about reserved slots without changing quality, RNG, costs or metadata.
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[]
{
    typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string),
    typeof(Vector2i), typeof(bool), typeof(bool), typeof(bool)
})]
internal static class SlotItemCreation
{
    private static readonly MethodInfo FindSlot = AccessTools.Method(typeof(Inventory), "FindEmptySlot");
    private static readonly Func<Inventory, bool, Vector2i> FindEmpty =
        AccessTools.MethodDelegate<Func<Inventory, bool, Vector2i>>(FindSlot);
    internal static bool HasDestination(Inventory inventory, Vector2i position) =>
        CharacterSlots.IsSlot(position) && position.x < inventory.GetWidth() &&
        inventory.GetItemAt(position.x, position.y) == null;

    internal static Vector2i FindDestination(Inventory inventory, bool topFirst, Vector2i position) =>
        HasDestination(inventory, position) ? position : FindEmpty(inventory, topFirst);

    internal static bool AddCreated(Inventory inventory, ItemDrop.ItemData item, Vector2i position) =>
        HasDestination(inventory, position) ? inventory.AddItem(item, position) : inventory.AddItem(item);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();
        MethodInfo find = FindSlot;
        MethodInfo add = AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
        int finds = 0, adds = 0;
        for (int i = 0; i < code.Count; i++)
        {
            MethodInfo replacement;
            if (code[i].Calls(find)) { replacement = AccessTools.Method(typeof(SlotItemCreation), nameof(FindDestination)); finds++; }
            else if (code[i].Calls(add)) { replacement = AccessTools.Method(typeof(SlotItemCreation), nameof(AddCreated)); adds++; }
            else continue;
            // Instance plus the original arguments are already on the stack. Append position.
            CodeInstruction position = new(OpCodes.Ldarg, 7);
            position.MoveLabelsFrom(code[i]);
            code.Insert(i++, position);
            code[i].opcode = OpCodes.Call;
            code[i].operand = replacement;
        }
        if (finds != 1 || adds != 1)
            throw new InvalidOperationException("Inventory item creation changed shape; refusing unsafe slot upgrades.");
        return code;
    }
}
