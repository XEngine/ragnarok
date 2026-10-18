using HarmonyLib;

namespace Ragnarok.Features.Gameplay;

// The underlying XP award, shared by all skills. Leave world scaling, level-up callbacks,
// skill caps and saved progress to vanilla; do not also patch RaiseSkill and double-scale XP.
[HarmonyPatch(typeof(Skills.Skill), nameof(Skills.Skill.Raise))]
internal static class SkillExperiencePatch
{
    private static void Prefix(ref float factor) =>
        factor *= 1f + (GameplaySettings.Current?.SkillXpModifier.Value ?? 0) / 100f;
}
