using Ironbound.Features.Mounts;

internal static class MountTemperamentChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mood in new[] { (false, false, .02), (true, false, .08), (false, true, .10), (true, true, .15) })
        {
            double chance = MountTemperament.Chance(mood.Item1, mood.Item2, 0);
            check(Math.Abs(chance - mood.Item3) < .000001, "Wrong temperament probability");
            check(Math.Abs(MountTemperament.Chance(mood.Item1, mood.Item2, 100) - chance / 2) < .000001, "Riding must halve temperament risk");
            check(MountTemperament.Respond(mood.Item1, mood.Item2, 0, 0, false) == MountResponse.Refuse, "Low roll must refuse");
            check(MountTemperament.Respond(mood.Item1, mood.Item2, 0, chance / 3, false) == MountResponse.Hesitate, "Refusal boundary must hesitate");
            check(MountTemperament.Respond(mood.Item1, mood.Item2, 0, chance, false) == MountResponse.Willing, "Chance boundary must succeed");
            check(MountTemperament.Respond(mood.Item1, mood.Item2, 0, 0, true) == MountResponse.Willing, "Grace must prevent repeated reactions");
            int[] counts = new int[3];
            for (int i = 0; i < 30000; i++) counts[(int)MountTemperament.Respond(mood.Item1, mood.Item2, 0, (i + .5) / 30000, false)]++;
            check(counts[1] == (int)Math.Round(chance * 20000) && counts[2] == (int)Math.Round(chance * 10000), "Distribution must favor hesitation over refusal two to one");
        }
        check(MountTemperament.Chance(true, true, -10) == .15 && MountTemperament.Chance(true, true, 200) == .075, "Riding bounds escaped clamp");
        check(MountTemperament.HesitationSeconds < MountTemperament.RetrySeconds && MountTemperament.RetrySeconds < MountTemperament.GraceSeconds, "Retry must fall inside grace period");
        check(MountTemperament.Message(true, false, MountResponse.Refuse).Contains("hungry") && MountTemperament.Message(false, true, MountResponse.Hesitate).Contains("frightened"), "Response must explain mount mood");
        Console.WriteLine("PASS: mount temperament odds, Riding reduction, refusal/hesitation boundaries and retry grace.");
    }
}
