using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Ironbound.Features.NearbyStorage;

// Where Valheim picks one of several items (which ore or grain a smelter or windmill takes,
// what a cooking station or fermenter takes, which "any one" recipe ingredient pays), it walks
// its own ordered list and takes the first match. With chests attached, a chest item could win
// over one the player carries. Run that same choice over the carried inventory first and
// consult nearby chests only when nothing carried qualifies. Vanilla order holds within each.
internal static class InventoryFirst
{
    // While set, the scoped chest augmentation stands down (ResourceContextPatch, scope lookups).
    [ThreadStatic] internal static bool Suspended;

    internal static T Carried<T>(Func<T> choose)
    {
        ResourceScope? saved = ResourceScope.Current;
        bool wasSuspended = Suspended;
        Suspended = true;
        ResourceScope.Current = null;
        try { return choose(); }
        finally
        {
            Suspended = wasSuspended;
            ResourceScope.Current = saved;
        }
    }
}

[HarmonyPatch]
internal static class InventoryFirstStationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type type in new[] { typeof(Smelter), typeof(CookingStation), typeof(Fermenter) })
            yield return AccessTools.Method(type, "FindCookableItem", new[] { typeof(Inventory) });
    }

    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance, MethodBase __originalMethod, Inventory inventory, ref ItemDrop.ItemData? __result)
    {
        // Only station interactions open a scope around these; hover text and others stay vanilla.
        if (InventoryFirst.Suspended || ResourceScope.Current == null || !ResourceScope.Current.Matches(inventory)) return true;
        ItemDrop.ItemData? carried = InventoryFirst.Carried(() =>
            (ItemDrop.ItemData?)__originalMethod.Invoke(__instance, new object[] { inventory }));
        if (carried == null) return true;
        __result = carried;
        return false;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
internal static class InventoryFirstIngredientPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Player __instance, Inventory inventory, Recipe recipe, int qualityLevel,
        out int amount, out int extraAmount, int craftMultiplier, ref ItemDrop.ItemData? __result)
    {
        amount = extraAmount = 0;
        // ResourceContextPatch opens this method's scope after this prefix, so decide from the settings.
        StorageSettings? settings = StorageRuntime.Settings;
        if (InventoryFirst.Suspended || settings == null || !settings.Enabled.Value ||
            __instance != Player.m_localPlayer)
            return true;
        int carriedAmount = 0, carriedExtra = 0;
        ItemDrop.ItemData? carried = InventoryFirst.Carried(() =>
            __instance.GetFirstRequiredItem(inventory, recipe, qualityLevel, out carriedAmount, out carriedExtra, craftMultiplier));
        if (carried == null) return true;
        amount = carriedAmount;
        extraAmount = carriedExtra;
        __result = carried;
        return false;
    }
}
