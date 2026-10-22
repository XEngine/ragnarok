using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.NearbyStorage;

// Acquire chest authority during the progress phase, before vanilla DoCrafting can
// silently return on its final requirements check. No resources move while waiting.
[HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
internal static class CraftingChestGate
{
    private static readonly AccessTools.FieldRef<InventoryGui, float> Timer = AccessTools.FieldRefAccess<InventoryGui, float>("m_craftTimer");
    private static readonly AccessTools.FieldRef<InventoryGui, Recipe> Recipe = AccessTools.FieldRefAccess<InventoryGui, Recipe>("m_craftRecipe");
    private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> Upgrade = AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_craftUpgradeItem");
    private static readonly AccessTools.FieldRef<InventoryGui, bool> Multi = AccessTools.FieldRefAccess<InventoryGui, bool>("m_multiCrafting");
    private static InventoryGui? waitingGui;
    private static float? waitStarted;

    internal static void Reset()
    {
        waitingGui = null;
        waitStarted = null;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(InventoryGui __instance, Player player, ref float dt)
    {
        StorageSettings settings = StorageRuntime.Settings;
        if (!settings.Enabled.Value || !player || player != Player.m_localPlayer ||
            Timer(__instance) < 0f || !Recipe(__instance) || player.NoCostCheat() ||
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
        {
            Reset();
            return;
        }
        if (waitingGui != __instance) { Reset(); waitingGui = __instance; }
        Recipe recipe = Recipe(__instance);
        ItemDrop.ItemData upgrade = Upgrade(__instance);
        int quality = upgrade == null ? 1 : upgrade.m_quality + 1;
        int amount = Multi(__instance) ? __instance.m_multiCraftAmount : 1;
        using ResourceScope scope = new(player, StorageRuntime.Registry, true, true);
        // Do not take other peers' chests when owned inventories can already pay.
        scope.RequestRemote = false;
        if (player.HaveRequirements(recipe, false, quality, amount))
        {
            waitStarted = null;
            return;
        }
        scope.RequestRemote = true;
        if (player.HaveRequirements(recipe, false, quality, amount))
        {
            waitStarted = null;
            return;
        }
        if (!scope.Pending) { waitStarted = null; return; }
        // Reset progress as well as dt: an already-complete timer must not finish
        // while authority is still pending. Cancel has the normal vanilla behavior.
        dt = 0f;
        Timer(__instance) = 0f;
        if (!waitStarted.HasValue) waitStarted = Time.unscaledTime;
        if (Time.unscaledTime - waitStarted.Value >= 8f)
        {
            scope.NotifyPending = false;
            Timer(__instance) = -1f;
            player.Message(MessageHud.MessageType.Center,
                "Ironbound: chest access timed out. Check that every player and the server use the same mod build, then retry.");
            Reset();
        }
    }
}
