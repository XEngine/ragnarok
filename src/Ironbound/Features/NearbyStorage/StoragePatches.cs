using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Ironbound.Features.NearbyStorage;

internal static class StorageRuntime
{
    internal static StorageSettings Settings = null!;
    internal static ContainerRegistry Registry = null!;
}

[HarmonyPatch(typeof(Container), "Awake")]
internal static class ContainerAwakePatch
{
    private static void Postfix(Container __instance) => StorageRuntime.Registry.Register(__instance);
}

// Augment inventories only while the game is checking or spending resources.
// Eating, equipment, inventory transfers and ordinary item searches stay untouched.
[HarmonyPatch]
internal static class ResourceContextPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Player), "HaveRequirementItems");
        yield return AccessTools.Method(typeof(Player), "GetFirstRequiredItem");
        yield return AccessTools.Method(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) });
        yield return AccessTools.Method(typeof(Player), "TryPlacePiece");
        yield return AccessTools.Method(typeof(Player), "ConsumeResources");
        yield return AccessTools.Method(typeof(InventoryGui), "SetupRequirement");
        yield return AccessTools.Method(typeof(InventoryGui), "DoCrafting");
        foreach (string name in new[] { "OnAddFuel", "OnAddOre" })
            yield return AccessTools.Method(typeof(Smelter), name);
        foreach (string name in new[] { "Interact", "UseItem" })
        {
            yield return AccessTools.Method(typeof(Fireplace), name);
            yield return AccessTools.Method(typeof(Fermenter), name);
        }
        foreach (string name in new[] { "OnAddFuelSwitch", "OnInteract", "OnUseItem" })
            yield return AccessTools.Method(typeof(CookingStation), name);
    }

    private static void Prefix(object? __instance, MethodBase __originalMethod, object[] __args, out ResourceScope? __state)
    {
        __state = null;
        if (InventoryFirst.Suspended) return;
        StorageSettings settings = StorageRuntime.Settings;
        Player? player = __instance as Player ?? __args.OfType<Player>().FirstOrDefault();
        if (!settings.Enabled.Value || !player || player != Player.m_localPlayer) return;
        string name = __originalMethod.Name;
        bool execute = false;
        if (__originalMethod.DeclaringType == typeof(Player))
        {
            if (name == "HaveRequirementItems" && (bool)__args[1]) return; // Keep discovery rules.
            if (name == "HaveRequirements" && (Player.RequirementMode)__args[1] == Player.RequirementMode.IsKnown) return;
            execute = name == "TryPlacePiece" || name == "ConsumeResources";
            // No placement cost (debug mode, B): Valheim skips the check but still calls ConsumeResources,
            // whose plain remove quietly takes what little you carry. Leave building fully to Valheim then;
            // a chest withdrawal would either spend chest stock for free building or refuse with an error.
            if (player.NoCostCheat() && (execute || name == "HaveRequirements")) return;
        }
        else if (__originalMethod.DeclaringType == typeof(InventoryGui))
        {
            execute = name == "DoCrafting";
        }
        else
        {
            execute = true;
        }
        __state = new ResourceScope(player, StorageRuntime.Registry, execute,
            __originalMethod.DeclaringType == typeof(InventoryGui) && name == "DoCrafting");
    }

    private static Exception? Finalizer(Exception? __exception, ResourceScope? __state)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) })]
internal static class CraftingRequirementsResultPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance, bool discover, bool __result)
    {
        if (!discover) ResourceScope.Current?.ReportCraftingRequirements(__instance, __result);
    }
}

[HarmonyPatch(typeof(Player), "TryPlacePiece")]
internal static class BuildAuthorityPatch
{
    // Availability previews may show remote stock. Recheck owned stock BEFORE spawning the piece.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Player __instance, Piece piece, ref bool __result)
    {
        if (ResourceScope.Current == null || __instance.NoCostCheat()) return true;
        if (__instance.HaveRequirements(piece, Player.RequirementMode.CanBuild)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
internal static class CountItemsPatch
{
    private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (name != null && scope != null && scope.Matches(__instance))
            __result += scope.Count(name, quality, matchWorldLevel);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), new[] { typeof(string), typeof(bool) })]
internal static class HaveItemPatch
{
    private static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (!__result && scope != null && scope.Matches(__instance))
            __result = scope.Count(name, -1, matchWorldLevel) > 0;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItem), new[] { typeof(string), typeof(int), typeof(bool) })]
internal static class GetItemPatch
{
    private static void Postfix(Inventory __instance, string name, int quality, bool isPrefabName, ref ItemDrop.ItemData? __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (__result == null && scope != null && scope.Matches(__instance))
            __result = scope.Find(name, quality, isPrefabName);
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
internal static class RemoveNamedItemPatch
{
    private static bool Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (scope == null || !scope.Execute || !scope.Matches(__instance)) return true;
        scope.Remove(name, amount, itemQuality, worldLevelBased);
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(ItemDrop.ItemData), typeof(int) })]
internal static class RemoveSpecificItemPatch
{
    private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, ref bool __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (scope == null || !scope.Execute || !scope.Matches(__instance) || __instance.ContainsItem(item)) return true;
        __result = scope.Remove(item, amount);
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
internal static class RemoveOneItemPatch
{
    private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (scope == null || !scope.Execute || !scope.Matches(__instance) || __instance.ContainsItem(item)) return true;
        __result = scope.Remove(item, 1);
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.ItemCheated), new[] { typeof(Piece.Requirement[]), typeof(int), typeof(bool) })]
internal static class CheatedResourcePatch
{
    private static void Postfix(Inventory __instance, Piece.Requirement[] resources, int quality, bool matchWorldLevel, ref bool __result)
    {
        ResourceScope? scope = ResourceScope.Current;
        if (!__result && scope != null && scope.Matches(__instance))
            __result = scope.Sources().Any(i => i.ItemCheated(resources, quality, matchWorldLevel));
    }
}
