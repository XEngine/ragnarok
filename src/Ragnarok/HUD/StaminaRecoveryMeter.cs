using System;

namespace Ragnarok.HUD;

// Observed positive recovery during native stat updates, never inferred from
// arbitrary frame-to-frame bar movement (which also contains spending/potions).
internal sealed class StaminaRecoveryMeter
{
    private float gain, elapsed;
    internal float Rate { get; private set; }

    internal void Observe(float before, float after, float dt)
    {
        if (!Finite(before) || !Finite(after) || !Finite(dt) || dt <= 0 || dt > 1) return;
        float recovered = Math.Max(0, after - before);
        // Stop immediately on interrupted recovery rather than showing a stale rate.
        if (recovered == 0) { gain = elapsed = Rate = 0; return; }
        gain += recovered; elapsed += dt;
        if (elapsed >= 0.2f) { Rate = gain / elapsed; gain = elapsed = 0; }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
