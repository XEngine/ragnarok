using System;

namespace Ragnarok.Features.Gameplay;

internal static class DeathPenalty
{
    internal static float Scale(float normalLoss, int modifierPercent) =>
        Math.Max(0f, Math.Min(1f, normalLoss * (1f + modifierPercent / 100f)));
}
