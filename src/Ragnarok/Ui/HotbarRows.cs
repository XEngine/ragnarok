using System;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Ui;

// Two hotbars, after the style guide's ALT toggle: the inventory's top row (equipment) and the row under
// it (consumables). While the switch key is held the action bar shows the second row and keys 1-8 use it
// (Alt+1 eats what is in the second row's first slot); let go and it is back to the first row.
// Only with the Ragnarök HUD and action bar.
internal sealed class HotbarRows : IDisposable
{
    private static readonly Func<Player, bool> TakeInput =
        AccessTools.MethodDelegate<Func<Player, bool>>(AccessTools.Method(typeof(Player), "TakeInput"));

    private static Func<bool> enabled = () => false;
    private static Func<KeyCode> switchKey = () => KeyCode.None;

    internal HotbarRows(InterfaceSettings settings)
    {
        enabled = () => settings.Hud.Value == HudStyle.Ragnarok && settings.ActionBar.Value && settings.HotbarSecondRow.Value;
        switchKey = () => settings.HotbarToggleKey.Value.MainKey;
    }

    internal static bool Enabled => enabled();

    internal static KeyCode SwitchKey => switchKey();

    // Whether the player is taking game input (not typing in chat, the console or a sign).
    internal static bool Playing(Player player) => player && TakeInput(player);

    // The inventory row the hotbar uses now: 1 (consumables) while the switch key is held, else 0.
    internal static int Row => enabled() && Holding && Playing(Player.m_localPlayer) ? 1 : 0;

    // How many top rows are hotbars, which sorting leaves in place.
    internal static int Count => enabled() ? 2 : 1;

    // The switch key is held (either Alt, Ctrl or Shift counts when it is one of those).
    private static bool Holding
    {
        get
        {
            KeyCode key = switchKey();
            return key != KeyCode.None && (Input.GetKey(key) || Input.GetKey(Twin(key)));
        }
    }

    // A shortcut that uses the switch key as its modifier belongs to the second row while it is on
    // (Ragnarök's Alt+4 provision key would otherwise eat a provision instead of slot 4).
    internal static bool Claims(BepInEx.Configuration.KeyboardShortcut shortcut)
    {
        if (!enabled()) return false;
        KeyCode key = switchKey();
        foreach (KeyCode modifier in shortcut.Modifiers)
            if (modifier == key || modifier == Twin(key)) return true;
        return false;
    }

    private static KeyCode Twin(KeyCode key) => key switch
    {
        KeyCode.LeftAlt => KeyCode.RightAlt,
        KeyCode.RightAlt => KeyCode.LeftAlt,
        KeyCode.LeftControl => KeyCode.RightControl,
        KeyCode.RightControl => KeyCode.LeftControl,
        KeyCode.LeftShift => KeyCode.RightShift,
        KeyCode.RightShift => KeyCode.LeftShift,
        _ => key
    };

    public void Dispose()
    {
        enabled = () => false;
        switchKey = () => KeyCode.None;
    }
}

// Keys 1-8 (and the action bar's clicks, which go through here) use the active row.
[HarmonyPatch(typeof(Player), nameof(Player.UseHotbarItem))]
internal static class HotbarRowPatch
{
    private static bool Prefix(Player __instance, int index)
    {
        int row = HotbarRows.Row;
        // Better Archery claims Alt+number for its quiver; its own prefix handles those.
        if (row == 0 || Equipment.BetterArcheryCompat.Active) return true;
        ItemDrop.ItemData item = __instance.GetInventory().GetItemAt(index - 1, row);
        if (item != null) __instance.UseItem(null, item, false);
        return false;
    }
}
