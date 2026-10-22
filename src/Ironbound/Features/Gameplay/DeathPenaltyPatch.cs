using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ironbound.Features.Gameplay;

// Change only the death caller, preserving difficulty scaling and the player's death-protection
// checks. Other callers of LowerAllSkills (including commands) are unaffected.
[HarmonyPatch(typeof(Skills), nameof(Skills.OnDeath))]
internal static class DeathPenaltyPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(AccessTools.Method(typeof(Skills), nameof(Skills.LowerAllSkills))))
            {
                matches++;
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(DeathPenaltyPatch), nameof(Apply));
            }
            yield return instruction;
        }
        if (matches != 1) throw new InvalidOperationException("Skill death-penalty patch no longer matches this Valheim version.");
    }

    private static void Apply(Skills skills, float normalLoss)
    {
        int modifier = GameplaySettings.Current?.SkillLossModifier.Value ?? 0;
        // Vanilla also clears skill XP and displays a loss message even for a zero factor.
        if (modifier == -100) return;
        skills.LowerAllSkills(DeathPenalty.Scale(normalLoss, modifier));
    }
}
