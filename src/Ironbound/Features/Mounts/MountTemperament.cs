namespace Ironbound.Features.Mounts;

internal enum MountResponse { Willing, Hesitate, Refuse }

// One server roll per summon. A reaction grants a short grace period so retrying
// cannot produce an unlucky chain of refusals. No persistent animal state changes.
internal static class MountTemperament
{
    internal const double GraceSeconds = 30;
    internal const double RetrySeconds = 5;
    internal const double HesitationSeconds = 2;

    internal static double Chance(bool hungry, bool frightened, float riding) =>
        (hungry ? (frightened ? 0.15 : 0.08) : (frightened ? 0.10 : 0.02)) *
        (1 - 0.5 * MountProgression.Factor(riding));

    internal static MountResponse Respond(bool hungry, bool frightened, float riding, double roll, bool grace)
    {
        if (grace) return MountResponse.Willing;
        double chance = Chance(hungry, frightened, riding);
        return roll < chance / 3 ? MountResponse.Refuse :
            roll < chance ? MountResponse.Hesitate : MountResponse.Willing;
    }

    internal static string Message(bool hungry, bool frightened, MountResponse response)
    {
        string mood = frightened ? "is frightened" : hungry ? "is hungry" : "feels stubborn";
        return response == MountResponse.Refuse
            ? "Your mount " + mood + " and ignores your whistle. Try again in a few seconds. No stamina spent."
            : "Your mount " + mood + " and hesitates… Give it a moment.";
    }
}
