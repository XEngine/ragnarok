#if DEBUG
using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace Ragnarok;

internal static class SkillExperienceChecks
{
    internal static void Run(ManualLogSource log)
    {
        var previous = QualityOfLife.Current;
        float worldRate = Game.m_skillGainRate;
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "ragnarok-xp-check-" + Guid.NewGuid() + ".cfg"), false)
            { SaveOnConfigSet = false };
        var settings = new QualityOfLife(config);
        try
        {
            QualityOfLife.Current = settings;
            Game.m_skillGainRate = 2f;
            var skill = new Skills.Skill(new Skills.SkillDef { m_skill = Skills.SkillType.Swords, m_increseStep = 1f });
            foreach (var test in new[] { (0, 2f), (50, 3f), (100, 4f), (-50, 1f), (-100, 0f) })
            {
                settings.SkillXpModifier.Value = test.Item1;
                skill.m_level = 10f;
                skill.m_accumulator = 0.25f;
                skill.Raise(1f); // Exercise the actual patched game method, not just our formula.
                if (Math.Abs(skill.m_accumulator - (0.25f + test.Item2)) > 0.00001f || skill.m_level != 10f)
                    throw new InvalidOperationException("Skill XP check failed at modifier " + test.Item1);
            }
            skill.m_level = 100f;
            settings.SkillXpModifier.Value = 900;
            skill.Raise(1f);
            if (skill.m_level != 100f || skill.m_accumulator != 0.25f)
                throw new InvalidOperationException("Skill XP cap check failed.");
            log.LogInfo("Ragnarok skill XP checks PASS: percentages, world scaling, retained progress and level cap.");
        }
        finally
        {
            QualityOfLife.Current = previous;
            Game.m_skillGainRate = worldRate;
        }
    }
}
#endif
