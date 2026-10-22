using System;

namespace Ironbound.Features.Mounts;

// Bounds the owner's physics proposal against the server's replicated player
// position. A proposal never authorizes arbitrary or non-finite coordinates.
internal static class MountArrivalPolicy
{
    internal static bool WithinReach(float px, float py, float pz, float x, float y, float z)
    {
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
            float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z) ||
            float.IsNaN(px) || float.IsNaN(py) || float.IsNaN(pz) ||
            float.IsInfinity(px) || float.IsInfinity(py) || float.IsInfinity(pz)) return false;
        double dx = (double)x - px, dy = (double)y - py, dz = (double)z - pz;
        return dx * dx + dz * dz <= 144 && Math.Abs(dy) <= 6;
    }
}
