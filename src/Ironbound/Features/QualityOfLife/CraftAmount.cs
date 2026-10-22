using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.QualityOfLife;

// Craft N at once. Valheim already crafts several in one go: holding Shift makes it craft five, as one
// longer job on the bench (one progress bar; the materials, skill and items all counted five times). This
// sets that amount to N whenever N is more than one (as if Shift were held), and scales the job's time
// the way Valheim's own five-at-once is (two seconds for one, six for five: +0.5 of a single craft each).
// With N at one, Valheim's Shift for five works as before. Nothing changes while a craft is under way.
internal static class CraftAmount
{
    internal const int Max = 99;

    private static readonly AccessTools.FieldRef<InventoryGui, bool> Touch = AccessTools.FieldRefAccess<InventoryGui, bool>("m_touchMultiCrafting");
    private static readonly AccessTools.FieldRef<InventoryGui, float> Timer = AccessTools.FieldRefAccess<InventoryGui, float>("m_craftTimer");

    // Chosen in the crafting panel; 1 is Valheim's own behaviour.
    internal static int Amount = 1;

    private static InventoryGui? stored;
    private static int vanillaAmount;
    private static float vanillaDuration;
    private static bool forced;

    internal static void Apply(InventoryGui gui)
    {
        if (stored != gui)
        {
            stored = gui;
            vanillaAmount = gui.m_multiCraftAmount;
            vanillaDuration = gui.m_multiCraftDuration;
            forced = false;
        }
        if (Timer(gui) >= 0f) return;
        if (Amount > 1)
        {
            gui.m_multiCraftAmount = Amount;
            gui.m_multiCraftDuration = gui.m_craftDuration * (1 + 0.5f * (Amount - 1));
            Touch(gui) = true;
            forced = true;
        }
        else
        {
            gui.m_multiCraftAmount = vanillaAmount;
            gui.m_multiCraftDuration = vanillaDuration;
            if (forced) Touch(gui) = false;
            forced = false;
        }
    }

    internal static void Reset()
    {
        Amount = 1;
        if (stored) Apply(stored!);
        // Apply defers restoration during an active craft. Keep its original snapshot
        // until that craft ends, even if the panel was rebuilt or its style changed.
        if (!stored || Timer(stored!) < 0f) stored = null;
    }

    internal static void Unload()
    {
        // No patch remains to restore a deferred override after unload. Cancel only
        // our overridden batch before restoring; resources are spent at completion.
        if (stored && forced && Timer(stored!) >= 0f) Timer(stored!) = -1f;
        Reset();
        stored = null;
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
    private static class UpdatePatch
    {
        private static void Prefix(InventoryGui __instance) => Apply(__instance);
    }

    [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
    private static class PressPatch
    {
        private static void Prefix(InventoryGui __instance) => Apply(__instance);

        // Valheim clears the flag after reading it; keep N in force for the next craft.
        private static void Postfix(InventoryGui __instance)
        {
            if (Amount > 1) Touch(__instance) = true;
        }
    }
}
