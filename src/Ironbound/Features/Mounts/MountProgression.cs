using System;

namespace Ironbound.Features.Mounts;

internal static class MountProgression
{
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool Snapshot(float ride, float maximum, float stamina) => Finite(ride) && ride >= 0 && ride <= 100 &&
        Finite(maximum) && maximum > 0 && maximum <= 1000000 && Finite(stamina) && stamina >= 0 && stamina <= maximum + 0.01f;
    internal static float Factor(float ride) => Finite(ride) ? Math.Max(0, Math.Min(1, ride / 100)) : 0;
    internal static float Cost(float maximum, float ride, float percent) => maximum * percent / 100 * (1 - Factor(ride));
    internal static float Windup(float ride, float expert) => 1.2f + (expert - 1.2f) * Factor(ride);
    internal static int Tier(float ride) => (int)(Factor(ride) * 4);
}
