using Ironbound.Configuration;

internal static class ConfigSliderChecks
{
    internal static void Run(Action<bool, string> check)
    {
        object capacity = ConfigSliderValue.Convert(42.4f, true, 0, 1000);
        check(capacity is int && (int)capacity == 42, "Capacity must box as Int32 for ConfigEntry<int>");
        object penalty = ConfigSliderValue.Convert(-50.2f, true, -100, 100);
        check(penalty is int && (int)penalty == -50, "Signed gameplay percentages retain integer type");
        object speed = ConfigSliderValue.Convert(1.25f, false, 0.1f, 20);
        check(speed is float && (float)speed == 1.25f, "Wide float ranges preserve fractional station speeds");
        check((float)ConfigSliderValue.Convert(0.1f, false, 0.1f, 120) == 0.1f, "Sub-unit durations remain selectable");
        check((float)ConfigSliderValue.Convert(0.95f, false, 0, 1) == 0.95f, "Opacity precision");
        check((int)ConfigSliderValue.Convert(1001, true, 0, 1000) == 1000, "Capacity upper bound");
        check((float)ConfigSliderValue.Convert(-1, false, 0.1f, 20) == 0.1f, "Float lower bound");
        check((int)ConfigSliderValue.Convert(0, true, 0, 1000) == 0, "Original-capacity sentinel remains available");
        check((int)ConfigSliderValue.Snap(13, true, -100, 900, 5) == 15, "Percentage drag snaps by five");
        check((int)ConfigSliderValue.Snap(27, true, 0, 1000, 10) == 30, "Capacity drag snaps by ten");
        check((int)ConfigSliderValue.Snap(-13, true, -100, 900, 5) == -15, "Negative percentage snapping");
        check((float)ConfigSliderValue.Snap(0.1f, false, 0.1f, 120, 1) == 0.1f, "Slider minimum remains reachable");
        check(ConfigSliderValue.TryRead("37", true, false, 0, 1000, out object exact) && exact is int && (int)exact == 37,
            "Typed values bypass coarse drag steps");
        check(ConfigSliderValue.TryRead("95", false, true, 0, 1, out object opacity) && (float)opacity == 0.95f,
            "Opacity text uses displayed percent units");
        check(ConfigSliderValue.TryRead("1.25", false, false, 0.1f, 20, out object fractional) && (float)fractional == 1.25f,
            "Exact typed speed bypasses multiplier drag steps");
        check(ConfigSliderValue.TryRead("2000", true, false, 0, 1000, out object clamped) && (int)clamped == 1000,
            "Typed values clamp to setting bounds");
        foreach (string invalid in new[] { "", "oops", "NaN", "Infinity" })
            check(!ConfigSliderValue.TryRead(invalid, false, false, 0, 1, out _), "Invalid numeric text rejected");
        check(ConfigSliderValue.Step("FuelCapacity", true) == 10 && ConfigSliderValue.Step("SkillLossModifierPercent", true) == 5,
            "Setting-specific drag steps");
        check(ConfigSliderValue.Step("PersonalRange", false) == 5, "Personal radius uses metre-scale steps");
        check(ConfigSliderValue.TryRead("100", false, false, 1, 25, out object capped) && (float)capped == 25,
            "Personal radius input respects the current server maximum");
        check((float)ConfigSliderValue.Snap(25, false, 1, 25, 5) == 25, "Server maximum remains reachable");
        Console.WriteLine("PASS: config slider runtime types, fractional ranges, signed values, bounds, snapping and exact text input.");
    }
}
