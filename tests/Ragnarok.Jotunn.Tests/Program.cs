using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
    private static int checks;
    private static ConfigEntry<KeyboardShortcut> Key(KeyCode key) => new(new KeyboardShortcut(key));
    private static void Check(bool result, string message)
    {
        checks++;
        if (!result) throw new Exception(message);
    }

    private static void Main()
    {
        var log = new ManualLogSource();
        LocalizationChecks.Run();
        UiLifetimeChecks.Run();
        MapOverlayChecks.Run();
        TamedAnimalChecks.Run();
        RangedAmmoChecks.Run();
        bool rejected = false; try { JotunnIntegration.Create(log); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && GUIManager.Subscriptions == 0, "missing dependency fails before GUI subscription");
        Chainloader.PluginInfos.Add(JotunnIntegration.Guid, new PluginInfo { Instance = new ProgramMarker() });
        var plugin = Chainloader.PluginInfos[JotunnIntegration.Guid];
        plugin.Metadata.Version = new Version("2.30.1");
        rejected = false; try { JotunnIntegration.Create(log); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && GUIManager.Subscriptions == 0, "old dependency fails before GUI subscription");
        plugin.Metadata.Version = new Version(JotunnIntegration.MinimumVersion);
        JotunnIntegration.Require();
        var external = new Harmony("test.jotunn");
        foreach (string name in new[] { "GetButtonDown", "GetButtonUp", "GetButton" })
            external.Patch(AccessTools.Method(typeof(ZInput), name), postfix: new HarmonyMethod(typeof(InputManager), "Postfix"));
        var ours = new Harmony("test.ragnarok");
        ours.CreateClassProcessor(typeof(BlockGameButtonsPatch)).Patch();
        try
        {
            var settingsKey = Key(KeyCode.F10);
            var dialKey = Key(KeyCode.V);
            ZInput.instance.m_buttons.Add("Use", new ZInput.ButtonDef { Key = KeyCode.E });
            for (int round = 0; round < 3; round++)
            {
                UiState.Reset();
                using (var integration = JotunnIntegration.Create(log)!)
                {
                    Check(integration != null, "adapter enabled");
                    var settings = integration!.AddButton("Settings", settingsKey, settingsToggle: true);
                    var dial = integration.AddButton("Dial", dialKey);
                    Check(InputManager.Buttons.Count == 2 && GUIManager.Subscriptions == 1, "one registration per button and GUI callback");
                    Check(InputManager.Buttons[dial.Name].ActiveInGUI && !InputManager.Buttons[dial.Name].ActiveInCustomGUI,
                        "dial may read held key in radial UI but not a blocking window");
                    Input.Pressed = KeyCode.V;
                    Check(dial.Down && dial.Held, "dial reads registered key");
                    UiState.WindowOpen = true;
                    integration.Tick(); integration.Tick();
                    Check(GUIManager.Requests == 1, "repeated tick acquires only one block");
                    Input.Pressed = settingsKey.Value.MainKey;
                    Check(settings.Down, "settings toggle can close a blocked window");
                    Input.Pressed = KeyCode.E;
                    Check(!ZInput.GetButtonDown("Use") && !ZInput.GetButton("Use") && !ZInput.GetButtonUp("Use"), "E blocked after Jotunn postfix");
                    UiState.WindowOpen = false;
                    var field = new TMPro.TMP_InputField { isFocused = true };
                    UiState.TypingFields.Add(field);
                    integration.Tick();
                    Check(GUIManager.Requests == 1 && !ZInput.GetButtonDown("Use"), "search focus retains one block");
                    field.isFocused = false;
                    integration.Tick();
                    Check(GUIManager.Requests == 0 && ZInput.GetButtonDown("Use"), "blur restores native input");
                    GUIManager.BlockInput(true); // Another mod owns this request.
                    UiState.WindowOpen = true; integration.Tick();
                    Check(GUIManager.Requests == 2, "shares other mod's block counter");
                    UiState.WindowOpen = false; integration.Tick();
                    Check(GUIManager.Requests == 1, "closing releases only our block");
                    GUIManager.BlockInput(false);
                    var oldAction = ZInput.instance.m_buttons[settings.Name].ButtonAction;
                    settingsKey.Value = new KeyboardShortcut(KeyCode.F9);
                    Check(oldAction.Disposed && InputManager.Buttons.Count == 2, "rebinding disposes old action without duplicates");
                    Input.Pressed = KeyCode.F9;
                    Check(settings.Down, "new key works immediately");
                    settingsKey.Value = new KeyboardShortcut(KeyCode.None);
                    Check(!settings.Registered && !settings.Down && !JotunnIntegration.AllowsButton(settings.Name), "None unregisters and disables shortcut");
                    settingsKey.Value = new KeyboardShortcut(KeyCode.F10);
                    Check(settings.Registered, "disabled shortcut can be rebound");
                    InputManager.FailNext = true;
                    var failedKey = Key(KeyCode.V);
                    var failed = integration.AddButton("Failure", failedKey);
                    Check(!failed.Registered && !InputManager.Buttons.ContainsKey(failed.Name) && !ZInput.instance.m_buttons.ContainsKey(failed.Name),
                        "partial registration failure cleans both registries");
                    Input.Pressed = KeyCode.V;
                    Check(failed.Down, "failed registration falls back to native input");
                    failed.Dispose();
                    Check(failedKey.Subscriptions == 0, "individual disposal releases config subscription");
                    int callbacks = 0;
                    integration.GuiRecreated = () => { callbacks++; UiState.WindowOpen = false; };
                    UiState.WindowOpen = true; integration.Tick();
                    SceneManager.Scene = new Scene { name = "start", handle = 2 + round * 2 };
                    GUIManager.Recreate();
                    Check(callbacks == 1 && !UiState.WindowOpen, "scene rebuild closes settings");
                    SceneManager.Scene = new Scene { name = "main", handle = 3 + round * 2 };
                    GUIManager.Recreate();
                    GUIManager.BlockInput(true); // Unloading must preserve another owner's request.
                    UiState.WindowOpen = true; integration.Tick();
                    Check(GUIManager.Requests == 2, "scene rebuild can acquire a fresh request");
                }
                Check(InputManager.Buttons.Count == 0 && ZInput.instance.m_buttons.Count == 1, "unload removes only owned buttons");
                Check(settingsKey.Subscriptions == 0 && dialKey.Subscriptions == 0 && GUIManager.Subscriptions == 0, "unload releases all subscriptions");
                Check(GUIManager.Requests == 1, "unload preserves another mod's block");
                GUIManager.BlockInput(false);
                Check(!JotunnIntegration.AllowsButton("Settings!com.kernelpanic.ragnarok"), "unload clears input guard exception");
                // Rebinding old config entries must not resurrect unloaded registrations.
                settingsKey.Value = new KeyboardShortcut(KeyCode.F10);
                Check(InputManager.Buttons.Count == 0, "old config cannot register stale bindings");
            }
            using (var integration = JotunnIntegration.Create(log)!)
            {
                string name = "Collision!com.kernelpanic.ragnarok";
                var foreign = new Jotunn.Configs.ButtonConfig { Name = name, Key = KeyCode.E };
                InputManager.Buttons.Add(name, foreign);
                var foreignButton = new ZInput.ButtonDef { Key = KeyCode.E };
                ZInput.instance.m_buttons.Add(name, foreignButton);
                var collision = integration.AddButton("Collision", Key(KeyCode.F10));
                collision.Dispose();
                Check(ReferenceEquals(InputManager.Buttons[name], foreign) && !foreignButton.ButtonAction.Disposed,
                    "name collision never removes someone else's registration or action");
                InputManager.Buttons.Remove(name); ZInput.instance.m_buttons.Remove(name);

                ZInput.instance = null!;
                var early = integration.AddButton("Early", Key(KeyCode.V));
                Check(early.Registered && !early.Down, "registration before ZInput exists is safe");
                ZInput.instance = new ZInput();
                ZInput.instance.m_buttons.Add("Use", new ZInput.ButtonDef { Key = KeyCode.E });
                // Models Jotunn registering its stored buttons on ZInput.Load.
                foreach (var entry in InputManager.Buttons)
                    ZInput.instance.m_buttons.Add(entry.Key, new ZInput.ButtonDef { Key = entry.Value.Key });
                Input.Pressed = KeyCode.V;
                UiState.Reset();
                Check(early.Down, "stored binding works after ZInput is created");
                var action = ZInput.instance.m_buttons[early.Name].ButtonAction;
                early.Dispose();
                Check(action.Disposed && !ZInput.instance.m_buttons.ContainsKey(early.Name), "cleanup targets current input instance");

                var mouseKey = Key(KeyCode.V);
                var mouse = integration.AddButton("MouseDial", mouseKey);
                foreach (KeyCode button in new[] { KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3,
                    KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6 })
                {
                    mouseKey.Value = new KeyboardShortcut(button);
                    Check(!mouse.Registered && !InputManager.Buttons.ContainsKey(mouse.Name) && !ZInput.instance.m_buttons.ContainsKey(mouse.Name),
                        "mouse shortcut avoids KeyCodeToPath conversion: " + button);
                    Input.Pressed = button;
                    Check(mouse.Down && mouse.Held, "mouse shortcut opens and stays held: " + button);
                    Input.Pressed = KeyCode.None;
                    Check(!mouse.Down && !mouse.Held, "mouse release closes dial: " + button);
                }
                mouseKey.Value = new KeyboardShortcut(KeyCode.V);
                Input.Pressed = KeyCode.V;
                Check(mouse.Registered && mouse.Down && mouse.Held, "rebinding from mouse to keyboard restores Jotunn registration");
                mouse.Dispose();
                Check(mouseKey.Subscriptions == 0, "mouse binding removes config listener on unload");

                UiState.WindowOpen = true; integration.Tick();
                SceneManager.Scene = new Scene { name = "main", handle = 100 };
                GUIManager.Requests = 1; // Old scene reset, then another mod acquires a block.
            }
            Check(GUIManager.Requests == 1, "dispose across scene changes never releases a new scene's foreign block");
            GUIManager.BlockInput(false);
            GamepadChecks.Run(log);
            Check(log.Warnings == 4, "only deliberately failed registrations emitted warnings");
            SceneManager.Scene = new Scene { name = "main", handle = 1000 };
            var failingInput = JotunnIntegration.Create(log);
            var brokenKey = Key(KeyCode.V);
            var healthyKey = Key(KeyCode.F10);
            var broken = failingInput.AddButton("BrokenDispose", brokenKey);
            failingInput.AddButton("HealthyDispose", healthyKey);
            ZInput.instance.m_buttons[broken.Name].ButtonAction.ThrowOnDispose = true;
            GUIManager.BlockInput(true); // Another mod owns this request.
            UiState.WindowOpen = true;
            failingInput.Tick();
            failingInput.Dispose();
            Check(GUIManager.Requests == 1 && GUIManager.Subscriptions == 0,
                "failed binding cleanup must release our input block and GUI subscription");
            Check(InputManager.Buttons.Count == 0 && brokenKey.Subscriptions == 0 && healthyKey.Subscriptions == 0,
                "failed binding must not prevent later binding cleanup");
            Check(log.Warnings == 5, "binding cleanup failure is reported once");
            GUIManager.BlockInput(false);
            UiState.Reset();
            Console.WriteLine("PASS: " + checks + " Jotunn input/UI checks, including three unload/reload rounds.");
        }
        finally { ours.UnpatchSelf(); external.UnpatchSelf(); UiState.Reset(); }
    }
    private sealed class ProgramMarker { }
}
