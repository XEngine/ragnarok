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

// Templates retain vanilla lifetimes; only the preview text reflects the next
// activation's configured duration. Existing active effects keep their own TTL.
[HarmonyPatch(typeof(SE_Stats), nameof(SE_Stats.GetTooltipString))]
internal static class PowerTooltipPatch
{
    private static void Postfix(SE_Stats __instance, ref string __result)
    {
        if (GameplaySettings.Current == null || !GameplaySettings.Powers.Contains(__instance.NameHash()) ||
            __instance.m_character) return;
        string original = "$se_ttl: <color=orange>" + __instance.m_ttl + "</color>";
        string duration = StatusEffect.GetTimeString(GameplaySettings.Current.PowerDuration.Value * 60f);
        __result = __result.Replace(original, "$se_ttl: <color=orange>" + duration + "</color>");
    }
}
