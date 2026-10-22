using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironbound.UI;

// Typed Jotunn APIs handle registration/blocking. Private registry access is needed only
// because Jotunn 2.30 has no RemoveButton API. Validate them before registering anything.
internal sealed class JotunnIntegration : IDisposable
{
    internal const string Guid = "com.jotunn.jotunn";
    internal const string MinimumVersion = "2.30.2";
    private static JotunnIntegration? current;
    private readonly ManualLogSource log;
    private readonly Dictionary<string, ButtonConfig> buttons;
    private readonly FieldInfo nativeButtons;
    private readonly PropertyInfo buttonAction;
    private readonly FieldInfo presentationButtons;
    private readonly PropertyInfo layoutButtonName;
    private readonly List<Binding> bindings = new();
    private bool ownsBlock, disposed;
    private int sceneHandle;
    internal Action? GuiRecreated;

    private JotunnIntegration(ManualLogSource log)
    {
        this.log = log;
        _ = InputManager.Instance;
        buttons = AccessTools.Field(typeof(InputManager), "Buttons")?.GetValue(null) as Dictionary<string, ButtonConfig>
            ?? throw new NotSupportedException("Jotunn button cleanup registry changed.");
        nativeButtons = AccessTools.Field(typeof(ZInput), "m_buttons");
        buttonAction = AccessTools.Inner(typeof(ZInput), "ButtonDef").GetProperty("ButtonAction")!;
        presentationButtons = AccessTools.Field(typeof(ZInput), "m_presentationButtons");
        layoutButtonName = AccessTools.Inner(typeof(ZInput), "LayoutButton").GetProperty("Name")!;
        if (nativeButtons == null || buttonAction == null ||
            presentationButtons == null || layoutButtonName == null ||
            !typeof(IDictionary).IsAssignableFrom(presentationButtons.FieldType) || !typeof(IDictionary).IsAssignableFrom(nativeButtons.FieldType))
            throw new NotSupportedException("Custom input cleanup API changed.");
        sceneHandle = SceneManager.GetActiveScene().handle;
        GUIManager.OnCustomGUIAvailable += OnGuiAvailable;
    }

    // ScriptEngine does not enforce BepInDependency as the normal loader does.
    internal static void Require()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var plugin) || plugin.Metadata.Version < new System.Version(MinimumVersion))
            throw new InvalidOperationException("Ironbound requires Jotunn " + MinimumVersion + " or newer. Install it in BepInEx/plugins and restart Valheim.");
    }

    internal static JotunnIntegration Create(ManualLogSource log)
    {
        Require();
        var integration = new JotunnIntegration(log);
        current = integration;
        log.LogInfo("Jotunn input/UI integration enabled.");
        return integration;
    }

    internal Binding AddButton(string name, ConfigEntry<KeyboardShortcut> key, bool settingsToggle = false)
    {
        var binding = new Binding(this, name, key, settingsToggle);
        bindings.Add(binding);
        binding.Refresh();
        return binding;
    }

    internal Binding AddGamepadButton(string name, ConfigEntry<InputManager.GamepadButton> key, bool settingsToggle = false)
    {
        var binding = new Binding(this, name, key, settingsToggle);
        bindings.Add(binding);
        binding.Refresh();
        return binding;
    }

    // Only our settings toggle bypasses the game-button guard, so it can close its own window.
    internal static bool AllowsButton(string name)
    {
        if (current == null) return false;
        foreach (Binding binding in current.bindings)
            if (binding.SettingsToggle && binding.Registered && binding.Name == name) return true;
        return false;
    }

    internal void Tick()
    {
        if (disposed) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.handle != sceneHandle)
        {
            // Jotunn resets its shared block counter on scene changes.
            ownsBlock = false;
            sceneHandle = scene.handle;
        }
        bool wanted = scene.name == "main" && (UiState.ModalOpen || UiState.Typing);
        if (wanted == ownsBlock) return;
        GUIManager.BlockInput(wanted);
        ownsBlock = wanted;
    }

    private void OnGuiAvailable()
    {
        ownsBlock = false; // The event follows Jotunn's ResetInputBlock.
        sceneHandle = SceneManager.GetActiveScene().handle;
        GuiRecreated?.Invoke();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        GUIManager.OnCustomGUIAvailable -= OnGuiAvailable;
        GuiRecreated = null;
        try
        {
            foreach (Binding binding in bindings)
                UiLifetime.Cleanup(binding.Dispose, exception => log.LogWarning("Input binding cleanup failed: " + exception));
        }
        finally
        {
            bindings.Clear();
            try
            {
                if (ownsBlock && SceneManager.GetActiveScene().handle == sceneHandle) GUIManager.BlockInput(false);
            }
            finally
            {
                ownsBlock = false;
                if (ReferenceEquals(current, this)) current = null;
            }
        }
    }

    internal sealed class Binding : IDisposable
    {
        private readonly JotunnIntegration owner;
        private readonly string shortName;
        private readonly ConfigEntry<KeyboardShortcut>? key;
        private readonly ConfigEntry<InputManager.GamepadButton>? gamepad;
        private ButtonConfig? config;
        private bool disposed;
        internal readonly bool SettingsToggle;
        internal string Name => shortName + "!" + Plugin.PluginGuid;
        internal bool Registered => config != null;

        internal Binding(JotunnIntegration owner, string name, ConfigEntry<KeyboardShortcut> key, bool settingsToggle)
        {
            this.owner = owner;
            shortName = name;
            this.key = key;
            SettingsToggle = settingsToggle;
            key.SettingChanged += OnChanged;
        }

        internal Binding(JotunnIntegration owner, string name, ConfigEntry<InputManager.GamepadButton> gamepad, bool settingsToggle)
        {
            this.owner = owner; shortName = name; this.gamepad = gamepad; SettingsToggle = settingsToggle;
            gamepad.SettingChanged += OnChanged;
        }

        // Modifiers remain with the caller: notably, releasing Shift while holding the
        // weapon-dial key must not select a weapon before the main key is released.
        internal bool Down => Registered ? ZInput.instance != null && ZInput.GetButtonDown(Name) :
            key != null && key.Value.MainKey != KeyCode.None && Input.GetKeyDown(key.Value.MainKey);
        internal bool Held => Registered ? ZInput.instance != null && ZInput.GetButton(Name) :
            key != null && key.Value.MainKey != KeyCode.None && Input.GetKey(key.Value.MainKey);

        private void OnChanged(object sender, EventArgs args) => Refresh();

        internal void Refresh()
        {
            if (disposed) return;
            Unregister();
            KeyCode main = key?.Value.MainKey ?? KeyCode.None;
            InputManager.GamepadButton controller = gamepad?.Value ?? InputManager.GamepadButton.None;
            if (main == KeyCode.None && controller == InputManager.GamepadButton.None) return;
            // Keep mouse bindings on the same legacy API as our key picker. Valheim's
            // KeyCodeToPath conversion can change the identity of the side buttons.
            // Down/Held already use direct Input polling when no button is registered.
            if (main >= KeyCode.Mouse0 && main <= KeyCode.Mouse6) return;
            try
            {
                if (owner.buttons.ContainsKey(Name)) throw new InvalidOperationException("Button name is already registered: " + Name);
                // Both callers gate their own context. The dial must keep reading its
                // held key while its radial menu makes Player.TakeInput return false.
                config = new ButtonConfig
                {
                    Name = shortName, Key = main, GamepadButton = controller,
                    ActiveInGUI = true, ActiveInCustomGUI = SettingsToggle
                };
                // Do not hand ConfigEntry to Jotunn: this owner handles rebinding and
                // removes its subscription on unload without leaving a config registry entry.
                InputManager.Instance.AddButton(Plugin.PluginGuid, config);
            }
            catch (Exception exception)
            {
                Unregister();
                owner.log.LogWarning("Jotunn button registration failed" + (key != null ? "; using native key: " : "; controller binding disabled: ") + exception.Message);
            }
        }

        private void Unregister()
        {
            if (config == null) return;
            if (owner.buttons.TryGetValue(Name, out ButtonConfig existing) && ReferenceEquals(existing, config))
            {
                owner.buttons.Remove(Name);
                if (ZInput.instance != null && owner.nativeButtons.GetValue(ZInput.instance) is IDictionary native)
                {
                    foreach (string name in new[] { Name, "Joy!" + Name })
                        if (native.Contains(name))
                        {
                            object button = native[name]!;
                            native.Remove(name);
                            (owner.buttonAction.GetValue(button) as IDisposable)?.Dispose();
                        }
                    if (owner.presentationButtons.GetValue(ZInput.instance) is IDictionary presentations)
                        foreach (object layout in presentations.Keys.Cast<object>().Where(layout => (string)owner.layoutButtonName.GetValue(layout) == "Joy!" + Name).ToArray())
                            presentations.Remove(layout);
                }
            }
            config = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (key != null) key.SettingChanged -= OnChanged;
            if (gamepad != null) gamepad.SettingChanged -= OnChanged;
            Unregister();
        }
    }
}
