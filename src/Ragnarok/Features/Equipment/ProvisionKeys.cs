using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.Equipment;

// Hotkeys for the provision slots, used like Valheim's hotbar keys: eat, drink or use the item.
internal static class ProvisionKeys
{
    private static readonly Func<Player, bool> TakeInput =
        AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

    // Set by the plugin.
    internal static ConfigEntry<KeyboardShortcut>[] Keys = Array.Empty<ConfigEntry<KeyboardShortcut>>();

    internal static void Update()
    {
        Player player = Player.m_localPlayer;
        if (!player || !CharacterSlots.Enabled() || !TakeInput(player)) return;
        Inventory inventory = player.GetInventory();
        for (int i = 0; i < Keys.Length && i < CharacterSlots.Provisions; i++)
        {
            if (!Keys[i].Value.IsDown()) continue;
            Vector2i slot = CharacterSlots.ProvisionSlot(i);
            ItemDrop.ItemData item = inventory.GetItemAt(slot.x, slot.y);
            if (item != null) player.UseItem(null, item, false);
        }
    }

    // A provision key with modifiers (Alt+1) also presses Valheim's hotbar key (1).
    internal static bool AnyDown() => CharacterSlots.Enabled() && Keys.Any(k => k.Value.MainKey != KeyCode.None && k.Value.IsDown());

    // Short label for the HUD: "ALT+1", "CTRL+F", "NUM 3".
    internal static string Label(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None) return "";
        List<string> parts = shortcut.Modifiers.Select(Name).Distinct().ToList();
        parts.Add(Name(shortcut.MainKey));
        return string.Join("+", parts);
    }

    private static string Name(KeyCode key)
    {
        string name = key.ToString();
        if (name.StartsWith("Alpha")) return name.Substring(5);
        if (name.StartsWith("Keypad")) return "NUM " + name.Substring(6).ToUpperInvariant();
        if (name.EndsWith("Alt")) return "ALT";
        if (name.EndsWith("Control")) return "CTRL";
        if (name.EndsWith("Shift")) return "SHIFT";
        return name.ToUpperInvariant();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
internal static class ProvisionHotbarPatch
{
    private static bool Prefix() => !ProvisionKeys.AnyDown();
}
