using HarmonyLib;
using UnityEngine;

namespace Ragnarok.HUD;

// Read-only average HP per game second, not the frequency of native heal ticks.
internal static class HealthRegeneration
{
    private static readonly AccessTools.FieldRef<SE_Stats, float> RemainingHealingTicks =
        AccessTools.FieldRefAccess<SE_Stats, float>("m_healthOverTimeTicks");
    private static readonly AccessTools.FieldRef<SE_Stats, float> HealingPerTick =
        AccessTools.FieldRefAccess<SE_Stats, float>("m_healthOverTimeTickHP");

    internal static float PerSecond(Player player)
    {
        if (player.IsDead() || player.GetHealth() >= player.GetMaxHealth()) return 0;
        float foodHealing = 0;
        foreach (Player.Food food in player.GetFoods()) foodHealing += food.m_item.m_shared.m_foodRegen;
        float multiplier = 1;
        SEMan effects = player.GetSEMan();
        effects.ModifyHealthRegen(ref multiplier);
        // Player.UpdateFood heals the summed food value once every ten seconds.
        float rate = Mathf.Max(0, foodHealing * multiplier) / 10;
        foreach (StatusEffect effect in effects.GetStatusEffects())
        {
            if (effect is not SE_Stats stats) continue;
            if (stats.m_tickInterval > 0 && stats.m_healthPerTick > 0 &&
                player.GetHealthPercentage() >= stats.m_healthPerTickMinHealthPercentage)
                rate += stats.m_healthPerTick / stats.m_tickInterval;
            if (stats.m_healthOverTimeInterval > 0 && RemainingHealingTicks(stats) > 0)
                rate += Mathf.Max(0, HealingPerTick(stats)) / stats.m_healthOverTimeInterval;
        }
        return float.IsNaN(rate) || float.IsInfinity(rate) ? 0 : rate;
    }
}
