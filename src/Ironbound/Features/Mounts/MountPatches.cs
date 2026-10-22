using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.Mounts;

[HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
internal static class MountHoverPatch
{
    private static void Postfix(Tameable __instance, ref string __result)
    {
        MountService? service = MountService.Instance;
        if (service == null || !service.Available || !service.Settings.Enabled.Value ||
            !Player.m_localPlayer || !__instance.IsTamed() || !MountTransport.HasSaddle(__instance)) return;
        var view = __instance.GetComponent<ZNetView>();
        if (!view || !view.IsValid()) return;
        string action = view.GetZDO().m_uid == service.Bound ? "Release mount bond" : "Bind mount";
        __result += "\n[<color=yellow>Hold " + service.Settings.BindKey.Value + "</color>] " + action;
        if (view.GetZDO().m_uid == service.Bound) __result += "\n" + service.SummonEstimate;
    }
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class MountDamagePatch
{
    private static void Postfix(Character __instance) => MountService.Instance?.MarkCombat(__instance);
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
internal static class MountAttackPatch
{
    private static void Postfix(Humanoid __instance, bool __result)
    { if (__result) MountService.Instance?.MarkCombat(__instance); }
}

[HarmonyPatch(typeof(Player), nameof(Player.OnTargeted))]
internal static class MountTargetedPatch
{
    private static void Postfix(Player __instance, bool __1)
    { if (__1) MountService.Instance?.MarkCombat(__instance); }
}

[HarmonyPatch(typeof(Player), "RPC_OnTargeted")]
internal static class MountRemoteTargetedPatch
{
    private static void Postfix(Player __instance, bool __2)
    { if (__2) MountService.Instance?.MarkCombat(__instance); }
}
