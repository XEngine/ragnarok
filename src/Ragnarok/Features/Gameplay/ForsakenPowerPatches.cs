using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ragnarok.Features.Gameplay;

// Replace the cooldown value at its assignment, preserving vanilla activation checks and RPCs.
[HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
internal static class PowerCooldownPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.LoadsField(AccessTools.Field(typeof(StatusEffect), nameof(StatusEffect.m_cooldown)))) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameplaySettings), nameof(GameplaySettings.Cooldown)));
        }
        if (matches != 1) throw new InvalidOperationException("Forsaken power cooldown patch no longer matches this Valheim version.");
    }
}

// This path runs on the receiving player's owner, including powers shared over the network.
// Only the active instance is changed; ObjectDB's shared effect templates remain untouched.
[HarmonyPatch(typeof(SEMan), "Internal_AddStatusEffect")]
internal static class PowerDurationPatch
{
    private static void Postfix(SEMan __instance, int nameHash)
    {
        if (GameplaySettings.Current == null || !GameplaySettings.Powers.Contains(nameHash)) return;
        var effect = __instance.GetStatusEffect(nameHash);
        if (effect) effect.m_ttl = GameplaySettings.Current.PowerDuration.Value * 60f;
    }
}
