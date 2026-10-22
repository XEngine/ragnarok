namespace Ironbound.Features.Mounts;

internal static class MountRecordPolicy
{
    internal static bool Validate(bool rideable, bool tamed, bool saddle, float health, out string reason)
    {
        reason = !rideable ? "Your bound mount's rideable prefab is unavailable on this machine." :
            !tamed ? "Your bound mount is no longer tamed." :
            !saddle ? "Your bound mount no longer has a saddle equipped." :
            float.IsNaN(health) || float.IsInfinity(health) ? "Your mount's health state is invalid." :
            health <= 0 ? "Your bound mount is dead and cannot be summoned." : "";
        return reason.Length == 0;
    }
}
