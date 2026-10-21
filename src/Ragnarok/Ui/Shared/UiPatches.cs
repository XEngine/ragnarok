using HarmonyLib;
using UnityEngine;

namespace Ragnarok.UI;

internal static class UiState
{
    internal static bool WindowOpen;
    internal static bool SearchOpen;
    internal static bool ModalOpen => WindowOpen || SearchOpen;
    // Ragnarök text fields outside the settings window (the crafting searches). While one has focus,
    // game keys are off, so typing E or Tab does not close the inventory.
    internal static readonly System.Collections.Generic.List<TMPro.TMP_InputField> TypingFields = new();

    internal static bool Typing
    {
        get
        {
            foreach (TMPro.TMP_InputField input in TypingFields)
                if (input && input.isFocused && input.gameObject.activeInHierarchy) return true;
            return false;
        }
    }

    // Typing into any text field: ours, or the focused field the UI has selected (a field not in the
    // list above, or another mod's). Only movement and character actions use this; game buttons stay
    // with Typing so Valheim's own chat and console keep their keys.
    internal static bool TextFocused
    {
        get
        {
            if (Typing) return true;
            GameObject? selected = UnityEngine.EventSystems.EventSystem.current
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            if (!selected || !selected!.activeInHierarchy) return false;
            if (selected.TryGetComponent(out TMPro.TMP_InputField tmp)) return tmp.isFocused;
            return selected.TryGetComponent(out UnityEngine.UI.InputField legacy) && legacy.isFocused;
        }
    }

    // Escape closes our window; keep the pause menu from opening on the same key press.
    internal static int SuppressMenuUntilFrame;

    internal static bool BlocksGame => ModalOpen || Time.frameCount <= SuppressMenuUntilFrame;

    internal static void Reset()
    {
        WindowOpen = false;
        SearchOpen = false;
        TypingFields.Clear();
        SuppressMenuUntilFrame = 0;
    }
}

// Movement and camera: with this false, PlayerController zeroes the character's controls, however
// the keys are read (keyboard buttons, the input system's stick, other mods).
[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class BlockPlayerInputPatch
{
    private static void Postfix(ref bool __result)
    {
        if (__result && (UiState.BlocksGame || UiState.TextFocused)) __result = false;
    }
}

// Character actions (use, hotbar, sit, powers, provision keys): Valheim already stops them while its
// own windows are open, but not for our search windows and fields.
[HarmonyPatch(typeof(Player), "TakeInput")]
internal static class BlockPlayerActionsPatch
{
    private static void Postfix(Player __instance, ref bool __result)
    {
        if (__result && __instance == Player.m_localPlayer && (UiState.BlocksGame || UiState.TextFocused)) __result = false;
    }
}

// Valheim reads game actions (inventory, map, chat, build menu...) through ZInput buttons,
// while Unity UI reads input separately, so blocking these keeps typing in our window safe.
[HarmonyPatch]
internal static class BlockGameButtonsPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        foreach (string name in new[] { nameof(ZInput.GetButtonDown), nameof(ZInput.GetButtonUp), nameof(ZInput.GetButton) })
            yield return AccessTools.Method(typeof(ZInput), name, new[] { typeof(string) });
    }

    private static bool Prefix(string name, ref bool __result)
    {
        // Include the closing frame: Enter must not become Chat after search consumes it.
        if ((!UiState.BlocksGame && !UiState.Typing) || JotunnIntegration.AllowsButton(name)) return true;
        __result = false;
        return false;
    }

    // Jotunn reads buttons again in its postfix using a reverse patch, bypassing our prefix.
    // Apply the focus guard after that result too, for both native and mod-defined buttons.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(string name, ref bool __result)
    {
        if ((UiState.BlocksGame || UiState.Typing) && !JotunnIntegration.AllowsButton(name)) __result = false;
    }
}

[HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
internal static class BlockScrollZoomPatch
{
    private static void Postfix(ref float __result)
    {
        if (UiState.ModalOpen) __result = 0;
    }
}

[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
internal static class FreeCursorPatch
{
    private static void Postfix()
    {
        if (!UiState.ModalOpen) return;
        ZCursor.LockState = CursorLockMode.None;
        ZCursor.Show();
    }
}

[HarmonyPatch(typeof(Menu), "Update")]
internal static class SuppressPauseMenuPatch
{
    // Only skip while the pause menu is closed, so an already-open menu keeps working.
    private static bool Prefix(Menu __instance) => !UiState.BlocksGame || __instance.m_root.gameObject.activeSelf;
}
