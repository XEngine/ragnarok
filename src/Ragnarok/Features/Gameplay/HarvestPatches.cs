using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Ragnarok.Features.Gameplay;

// Drop is called only after RPC_Pick's ownership and already-picked checks.
// Multiply the primary harvest, leaving unrelated extra drops alone.
[HarmonyPatch(typeof(Pickable), "Drop")]
internal static class PlantYieldPatch
{
    private static void Prefix(Pickable __instance, UnityEngine.GameObject prefab, ref int stack)
    {
        if (prefab == __instance.m_itemPrefab && GameplaySettings.IsPlant(__instance))
            stack = GameplaySettings.Harvest(stack);
    }
}

// Apply once to each extracted honey stack after the game's resource modifier.
// Do not change stored honey levels, production timers, or drops from destroying a hive.
[HarmonyPatch(typeof(Beehive), "RPC_Extract")]
internal static class HoneyYieldPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var scale = AccessTools.Method(typeof(Game), nameof(Game.ScaleDrops), new[] { typeof(ItemDrop.ItemData), typeof(int) });
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(scale)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameplaySettings), nameof(GameplaySettings.Harvest)));
        }
        if (matches != 1) throw new InvalidOperationException("Honey yield patch no longer matches this Valheim version.");
    }
}
