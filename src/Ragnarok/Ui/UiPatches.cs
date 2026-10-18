using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Ui;

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

[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class BlockPlayerInputPatch
{
    private static void Postfix(ref bool __result)
    {
        if (UiState.BlocksGame) __result = false;
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

    private static bool Prefix(ref bool __result)
    {
        // Include the closing frame: Enter must not become Chat after search consumes it.
        if (!UiState.BlocksGame && !UiState.Typing) return true;
        __result = false;
        return false;
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
