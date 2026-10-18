using System;
using System.Reflection;
using HarmonyLib;

namespace Ragnarok.Features.QualityOfLife;

internal static class PlantHoverStatus
{
    internal static Func<bool> Enabled = () => false;
    private static readonly MethodInfo GrowTime = AccessTools.Method(typeof(Plant), "GetGrowTime");

    internal static string Remaining(double seconds)
    {
        long total = (long)Math.Ceiling(Math.Max(0, seconds));
        if (total >= 3600) return $"{total / 3600}h {(total % 3600) / 60}m";
        if (total >= 60) return $"{total / 60}m {total % 60}s";
        return $"{total}s";
    }

    internal static void AppendPlant(Plant plant, ZNetView view, ref string text)
    {
        if (!Enabled() || !view || !view.IsValid() || !ZNet.instance) return;
        // Preserve vanilla's localized health reason. Never call UpdateHealth/Grow from a hover.
        if (plant.GetStatus() != Plant.Status.Healthy)
        {
            text += "\nGrowth blocked — fix the condition above";
            return;
        }
        long planted = view.GetZDO().GetLong(ZDOVars.s_plantTime);
        if (planted <= 0) { text += "\nGrowing — waiting for growth data"; return; }
        double age = (ZNet.instance.GetTime().Ticks - planted) / (double)TimeSpan.TicksPerSecond;
        double remaining = (float)GrowTime.Invoke(plant, null) - Math.Max(0, age);
        text += remaining > 0 ? "\nGrowing — ready in ~" + Remaining(remaining) : "\nMaturing — awaiting growth update";
    }
}

[HarmonyPatch(typeof(Plant), nameof(Plant.GetHoverText))]
internal static class PlantGrowthHoverPatch
{
    private static void Postfix(Plant __instance, ZNetView ___m_nview, ref string __result) =>
        PlantHoverStatus.AppendPlant(__instance, ___m_nview, ref __result);
}

[HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
internal static class HarvestGrowthHoverPatch
{
    private static void Postfix(Pickable __instance, ZNetView ___m_nview, ref string __result)
    {
        if (!PlantHoverStatus.Enabled() || !GameplaySettings.IsPlant(__instance) ||
            !___m_nview || !___m_nview.IsValid() || !ZNet.instance || __instance.GetEnabled == 0) return;
        if (!__instance.GetPicked())
        {
            if (!string.IsNullOrEmpty(__result)) __result += "\nReady to harvest";
            return;
        }
        if (__instance.m_respawnTimeMinutes <= 0) return;
        long picked = ___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime);
        if (picked <= 1) return; // Vanilla has not initialized a respawn timestamp yet.
        double age = (ZNet.instance.GetTime().Ticks - picked) / (double)TimeSpan.TicksPerSecond;
        double remaining = __instance.m_respawnTimeMinutes * 60d - Math.Max(0, age);
        __result = __instance.GetHoverName() + (remaining > 0
            ? "\nRegrowing — ready in ~" + PlantHoverStatus.Remaining(remaining)
            : "\nRegrowth due — awaiting world update");
    }
}
