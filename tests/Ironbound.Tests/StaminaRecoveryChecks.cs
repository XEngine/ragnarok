using Ironbound.HUD;

internal static class StaminaRecoveryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var meter = new StaminaRecoveryMeter();
        meter.Observe(10, 11, .1f);
        meter.Observe(11, 12, .1f);
        check(Math.Abs(meter.Rate - 10) < .001, "Observed recovery must use elapsed game time");
        meter.Observe(12, 12, .02f);
        check(meter.Rate == 0, "Interrupted/full recovery must clear stale rate immediately");
        meter.Observe(20, 15, .1f);
        check(meter.Rate == 0, "Spending must not become a negative regeneration rate");
        meter.Observe(15, 16, .2f);
        check(Math.Abs(meter.Rate - 5) < .001, "Slower recovery must update the displayed rate");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            meter.Observe(invalid, 1, .2f); meter.Observe(1, invalid, .2f); meter.Observe(1, 2, invalid);
        }
        meter.Observe(1, 2, 0); meter.Observe(1, 2, -1); meter.Observe(1, 2, 10);
        check(Math.Abs(meter.Rate - 5) < .001, "Invalid samples must not corrupt rate");
        check(Math.Abs(HealthRecoveryRate.PerSecond(12, 1.5f, true) - 1.8f) < .001, "Health must convert ten-second food ticks and apply status modifiers");
        check(HealthRecoveryRate.PerSecond(12, 1.5f, false) == 0, "Full health must show no active recovery");
        check(HealthRecoveryRate.PerSecond(12, 0, true) == 0 && HealthRecoveryRate.PerSecond(0, 2, true) == 0, "No-food or suppressed recovery must show zero");
        check(HealthRecoveryRate.PerSecond(12, -1, true) == 0 && HealthRecoveryRate.PerSecond(float.NaN, 1, true) == 0 &&
            HealthRecoveryRate.PerSecond(12, float.PositiveInfinity, true) == 0, "Invalid health rates must not enter HUD text");
        Console.WriteLine("PASS: observed stamina recovery, interruptions, invalid samples and food/status health regeneration units.");
    }
}
