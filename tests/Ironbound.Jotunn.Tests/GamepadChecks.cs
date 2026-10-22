using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using Jotunn.Managers;
using Ironbound.UI;
using UnityEngine;

internal static class GamepadChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    internal static void Run(ManualLogSource log)
    {
        UiState.Reset(); Input.Pressed = KeyCode.None;
        var settingsKey = new ConfigEntry<InputManager.GamepadButton>(InputManager.GamepadButton.None);
        var dialKey = new ConfigEntry<InputManager.GamepadButton>(InputManager.GamepadButton.RightShoulder);
        var foreign = new ZInput.LayoutButton { Name = "AnotherMod" };
        ZInput.instance.m_presentationButtons.Add(foreign, new object());
        using (var integration = JotunnIntegration.Create(log))
        {
            var settings = integration.AddGamepadButton("SettingsController", settingsKey, settingsToggle: true);
            var dial = integration.AddGamepadButton("DialController", dialKey);
            Check(!settings.Registered && !settings.Down, "None does not reserve a controller button");
            Input.Controller = InputManager.GamepadButton.RightShoulder;
            Check(dial.Down && dial.Held, "controller press and hold through Joy binding");
            Input.Controller = InputManager.GamepadButton.None;
            Check(!dial.Held, "controller release is observed");
            settingsKey.Value = InputManager.GamepadButton.StartButton;
            UiState.WindowOpen = true; integration.Tick();
            Input.Controller = InputManager.GamepadButton.StartButton;
            Check(settings.Down, "controller settings toggle can close its blocking window");
            Input.Controller = InputManager.GamepadButton.RightShoulder;
            Check(!dial.Down && !dial.Held, "controller dial blocked while settings owns input");
            UiState.WindowOpen = false; integration.Tick();
            Check(dial.Held, "controller dial resumes after UI closes");
            var oldAction = ZInput.instance.m_buttons["Joy!" + dial.Name].ButtonAction;
            dialKey.Value = InputManager.GamepadButton.LeftShoulder;
            Check(oldAction.Disposed && ZInput.instance.m_presentationButtons.Count == 3, "rebinding disposes action and replaces presentation");
            Input.Controller = InputManager.GamepadButton.LeftShoulder;
            Check(dial.Down, "new controller binding works");
            dialKey.Value = InputManager.GamepadButton.None;
            Check(!dial.Registered && !ZInput.instance.m_buttons.ContainsKey("Joy!" + dial.Name), "None removes Joy action");
            Check(ZInput.instance.m_presentationButtons.Count == 2, "None removes controller presentation");
        }
        Check(settingsKey.Subscriptions == 0 && dialKey.Subscriptions == 0, "controller listeners removed on unload");
        Check(ZInput.instance.m_presentationButtons.Count == 1 && ZInput.instance.m_presentationButtons.ContainsKey(foreign), "foreign controller presentation retained");
        ZInput.instance.m_presentationButtons.Remove(foreign);
        Input.Controller = InputManager.GamepadButton.None; UiState.Reset();
        Console.WriteLine("PASS: " + checks + " controller registration, UI blocking, rebinding and cleanup checks.");
    }
}
