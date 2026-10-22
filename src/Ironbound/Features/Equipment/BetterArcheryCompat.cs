using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.Equipment;

// Better Archery (ishid4.mods.betterarchery) grows the player inventory by two rows: a hidden spacer
// and a quiver row whose first three positions hold arrows once its leather quiver is worn (the quiver
// itself is an ItemType.None item in a custom "quiver" slot). Read through reflection, so Ironbound
// neither needs nor ships it. Its row expansion stands down when Equipment & Quick Slots or Extended
// Player Inventory is installed, and so does this.
internal static class BetterArcheryCompat
{
    internal const string Guid = "ishid4.mods.betterarchery";

    private static bool resolved;
    private static ConfigEntry<bool>? quiverEnabled;
    private static PropertyInfo? expansionPresent;
    private static FieldInfo? quiverRow;
    private static MethodInfo? baseRows, quiverEquipped;
    private static ConfigEntry<KeyboardShortcut>? holdKey;
    private static ConfigEntry<KeyboardShortcut>[]? slotKeys;

    internal static bool Loaded => Chainloader.PluginInfos.ContainsKey(Guid);

    private static void Resolve()
    {
        if (resolved) return;
        resolved = true;
        Type? main = Loaded ? AccessTools.TypeByName("BetterArchery.BetterArchery") : null;
        if (main == null) return;
        quiverEnabled = AccessTools.Field(main, "ConfigQuiverEnabled")?.GetValue(null) as ConfigEntry<bool>;
        expansionPresent = AccessTools.Property(main, "isInventoryExpansionModPresent");
        quiverRow = AccessTools.Field(main, "QuiverRowIndex");
        baseRows = AccessTools.Method(main, "GetBaseInventoryRows");
        quiverEquipped = AccessTools.Method(main, "IsQuiverEquipped");
        holdKey = AccessTools.Field(main, "HoldingKeyCode")?.GetValue(null) as ConfigEntry<KeyboardShortcut>;
        slotKeys = AccessTools.Field(main, "KeyCodes")?.GetValue(null) as ConfigEntry<KeyboardShortcut>[];
        if (quiverEnabled == null || expansionPresent == null || quiverRow == null || baseRows == null || quiverEquipped == null)
            Debug.LogWarning("[Ironbound] Better Archery found but its quiver API changed; quiver support is off.");
    }

    // Its quiver rows exist on the player inventory right now.
    internal static bool Active
    {
        get
        {
            Resolve();
            if (quiverEnabled == null || expansionPresent == null || quiverRow == null || baseRows == null) return false;
            return quiverEnabled.Value && !(bool)expansionPresent.GetValue(null) && (int)quiverRow.GetValue(null) > 0;
        }
    }

    internal static int BaseRows => (int)baseRows!.Invoke(null, null);

    internal static int QuiverRow => (int)quiverRow!.GetValue(null);

    internal const int ArrowSlots = 3;

    internal static Vector2i ArrowSlot(int index) => new(index, QuiverRow);

    internal static bool IsArrowSlot(Vector2i pos) => Active && pos.y == QuiverRow && pos.x >= 0 && pos.x < ArrowSlots;

    internal static bool QuiverEquipped() => quiverEquipped != null && (bool)quiverEquipped.Invoke(null, null);

    internal static bool IsQuiver(ItemDrop.ItemData item)
    {
        if (!item.m_dropPrefab) return false;
        Component? slot = item.m_dropPrefab.GetComponent("CustomSlotItem");
        return slot && AccessTools.Field(slot!.GetType(), "m_slotName")?.GetValue(slot) as string == "quiver";
    }

    // Its quiver hotkeys, so ours can stay clear of them: "ALT+1".
    internal static IEnumerable<KeyboardShortcut> QuiverKeys()
    {
        Resolve();
        if (slotKeys == null) yield break;
        foreach (ConfigEntry<KeyboardShortcut> key in slotKeys)
        {
            if (key == null) continue;
            KeyCode hold = holdKey?.Value.MainKey ?? KeyCode.None;
            yield return hold == KeyCode.None ? key.Value : new KeyboardShortcut(key.Value.MainKey, hold);
        }
    }
}

// Better Archery draws its own three arrow slots on Valheim's player grid while a quiver is worn.
// A Ironbound character panel (either style) already shows those slots, so skip its copy and
// clear what it drew; otherwise the arrows appear twice. Its own code runs again once ours is off.
[HarmonyPatch]
internal static class BetterArcheryQuiverSlotsPatch
{
    // Set by the plugin: a Ironbound character panel shows the quiver's arrow slots.
    internal static Func<bool> ShownByIronbound = () => false;

    private static readonly MethodInfo? Remove =
        AccessTools.Method("BetterArchery.InventoryGrid_UpdateGui_Patch:RemoveQuiverSlots");

    private static MethodBase? Target() => AccessTools.Method("BetterArchery.InventoryGrid_UpdateGui_Patch:CreateQuiverSlots");

    private static bool Prepare() => BetterArcheryCompat.Loaded && Target() != null && Remove != null;

    private static MethodBase TargetMethod() => Target()!;

    // Its parameters are (InventoryGrid __instance, List<InventoryElement> ___m_elements); pass them on as they are.
    private static bool Prefix(object[] __args)
    {
        if (!ShownByIronbound()) return true;
        Remove!.Invoke(null, __args);
        return false;
    }
}
