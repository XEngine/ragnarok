using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ironbound.Features.Gameplay;

internal static class ResourceCosts
{
    internal static float Run(float cost) => cost * (1f + (GameplaySettings.Current?.RunCost.Value ?? 0) / 100f);
    internal static float Jump(float cost) => cost * (1f + (GameplaySettings.Current?.JumpCost.Value ?? 0) / 100f);
    internal static float Attack(float cost) => cost * (1f + (GameplaySettings.Current?.AttackCost.Value ?? 0) / 100f);
    internal static float Eitr(float cost) => cost * (1f + (GameplaySettings.Current?.EitrCost.Value ?? 0) / 100f);
}

// Patch cost calculation so affordability checks and actual spending use the same amount.
[HarmonyPatch(typeof(Attack), "GetAttackStamina")]
internal static class AttackStaminaCostPatch
{
    private static void Postfix(Character ___m_character, ref float __result)
    {
        if (___m_character is Player) __result = ResourceCosts.Attack(__result);
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackEitr), new[] { typeof(Character), typeof(ItemDrop.ItemData) })]
internal static class AttackEitrCostPatch
{
    private static void Postfix(Character character, ref float __result)
    {
        if (character is Player) __result = ResourceCosts.Eitr(__result);
    }
}

// Scale local values rather than changing shared item assets or globally intercepting UseStamina.
// Each source is scaled once; blocking, dodging, swimming and building retain their normal costs.
[HarmonyPatch]
internal static class PlayerResourceCostPatch
{
    private static readonly string[] Methods = { "CheckRun", "OnJump", "UpdateAttackBowDraw", "UpdateWeaponLoading", "QueueReloadAction" };
    private static IEnumerable<MethodBase> TargetMethods() => Methods.Select(name => AccessTools.Method(typeof(Player), name));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            string? helper = null;
            if (instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo field)
            {
                if (__originalMethod.Name == "CheckRun" && field.Name == "m_runStaminaDrain") helper = nameof(ResourceCosts.Run);
                if (__originalMethod.Name == "OnJump" && field.Name == "m_jumpStaminaUsage") helper = nameof(ResourceCosts.Jump);
                if (field.DeclaringType == typeof(Attack) && field.Name == "m_reloadStaminaDrain") helper = nameof(ResourceCosts.Attack);
                if (field.DeclaringType == typeof(Attack) && field.Name == "m_reloadEitrDrain") helper = nameof(ResourceCosts.Eitr);
            }
            if (__originalMethod.Name == "UpdateAttackBowDraw" && instruction.operand is MethodInfo method &&
                (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
            {
                if (method.Name == "GetDrawStaminaDrain") helper = nameof(ResourceCosts.Attack);
                if (method.Name == "GetDrawEitrDrain") helper = nameof(ResourceCosts.Eitr);
            }
            if (helper == null) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ResourceCosts), helper));
        }
        int expected = __originalMethod.Name == "OnJump" ? 3 :
            (__originalMethod.Name == "UpdateAttackBowDraw" || __originalMethod.Name == "QueueReloadAction" ? 2 : 1);
        if (matches != expected) throw new InvalidOperationException("Resource cost patch no longer matches " + __originalMethod.Name);
    }
}
