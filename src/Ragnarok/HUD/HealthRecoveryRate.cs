namespace Ragnarok.HUD;

internal static class HealthRecoveryRate
{
    // Native food regeneration is HP per ten-second tick. Display its per-second
    // equivalent so vanilla ticks and Steady Regeneration share a stable readout.
    internal static float PerSecond(float foodRegen, float modifier, bool missingHealth)
    {
        float rate = foodRegen / 10 * modifier;
        return missingHealth && !float.IsNaN(rate) && !float.IsInfinity(rate) && rate > 0 ? rate : 0;
    }
}
