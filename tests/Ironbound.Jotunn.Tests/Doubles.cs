// Test doubles model the installed Jotunn registration contract and shared block
// counter. The real Harmony library exercises patch ordering; Unity is not running.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ironbound { internal static class Plugin { internal const string PluginGuid = "com.kernelpanic.ironbound"; } }
namespace BepInEx
{
    public sealed class BepInPlugin
    {
        public string GUID { get; }
        public BepInPlugin(string guid) { GUID = guid; }
    }
}
namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        internal int Warnings;
        public void LogInfo(object message) { }
        public void LogWarning(object message) { Warnings++; }
    }
}
namespace BepInEx.Configuration
{
    public struct KeyboardShortcut
    {
        public KeyCode MainKey { get; }
        public KeyboardShortcut(KeyCode key) { MainKey = key; }
    }
    public sealed class ConfigEntry<T>
    {
        private T value;
        private EventHandler? changed;
        internal int Subscriptions;
        public ConfigEntry(T value) { this.value = value; }
        public T Value { get => value; set { this.value = value; changed?.Invoke(this, EventArgs.Empty); } }
        public event EventHandler SettingChanged
        {
            add { changed += value; Subscriptions++; }
            remove { changed -= value; Subscriptions--; }
        }
    }
}
namespace BepInEx.Bootstrap
{
    public sealed class PluginMetadata { public Version Version = new Version("2.30.2"); }
    public sealed class PluginInfo { public object Instance = new object(); public PluginMetadata Metadata = new(); }
    public static class Chainloader { public static readonly Dictionary<string, PluginInfo> PluginInfos = new(); }
}
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object? value) => value != null && !value.Destroyed;
        public static void Destroy(Object? value) { if (value != null) value.Destroyed = true; }
    }
    public class GameObject : Object
    {
        public GameObject gameObject => this;
        public bool activeInHierarchy = true, activeSelf;
        public void SetActive(bool value) { activeSelf = value; activeInHierarchy = value; }
        public bool TryGetComponent<T>(out T result) { result = default!; return false; }
    }
    public enum KeyCode { None, E, V, F10, F9, Mouse0 = 323, Mouse1, Mouse2, Mouse3, Mouse4, Mouse5, Mouse6 }
    public enum CursorLockMode { None }
    public static class Time { public static int frameCount = 10; public static float unscaledTime; }
    public static class PlayerPrefs
    {
        internal static string Language = "English";
        public static string GetString(string key, string fallback) => Language;
    }
    public static class Input
    {
        internal static KeyCode Pressed;
        internal static Jotunn.Managers.InputManager.GamepadButton Controller;
        public static bool GetKeyDown(KeyCode key) => key != KeyCode.None && Pressed == key;
        public static bool GetKey(KeyCode key) => GetKeyDown(key);
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; public int handle; }
    public static class SceneManager
    {
        internal static Scene Scene = new Scene { name = "main", handle = 1 };
        public static Scene GetActiveScene() => Scene;
    }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Object
    {
        public static EventSystem current = new();
        public GameObject? currentSelectedGameObject;
    }
}
namespace UnityEngine.UI
{
    public class InputField : UnityEngine.Object { public bool isFocused; }
}
namespace TMPro
{
    public class TMP_InputField : UnityEngine.Object { public bool isFocused; public GameObject gameObject = new(); }
}
internal sealed class PlayerController { private bool TakeInput() => true; }
internal sealed class Player : UnityEngine.Object
{
    public static Player m_localPlayer = new();
    private bool TakeInput() => true;
}
internal sealed class GameCamera { public void UpdateMouseCapture() { } }
internal static class ZCursor { public static CursorLockMode LockState; public static void Show() { } }
internal sealed class Menu
{
    public GameObject m_root = new();
    private void Update() { }
}
public sealed class ZInput
{
    public static ZInput instance = new();
    internal readonly Dictionary<string, ButtonDef> m_buttons = new();
    internal readonly Dictionary<LayoutButton, object> m_presentationButtons = new();
    public sealed class LayoutButton { public string Name { get; set; } = ""; }
    public sealed class ButtonDef
    {
        internal KeyCode Key;
        internal Jotunn.Managers.InputManager.GamepadButton Controller;
        public TestAction ButtonAction { get; } = new();
    }
    public sealed class TestAction : IDisposable
    {
        internal bool Disposed;
        internal bool ThrowOnDispose;
        public void Dispose() { Disposed = true; if (ThrowOnDispose) throw new InvalidOperationException("test native input release failure"); }
    }
    internal static bool Raw(string name) => instance.m_buttons.TryGetValue(name, out var button) &&
        (Input.GetKey(button.Key) || (button.Controller != Jotunn.Managers.InputManager.GamepadButton.None && Input.Controller == button.Controller));
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool GetButtonDown(string name) => Raw(name);
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool GetButtonUp(string name) => Raw(name);
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool GetButton(string name) => Raw(name);
    public static float GetMouseScrollWheel() => 1;
}
namespace Jotunn.Configs
{
    public sealed class ButtonConfig
    {
        public string Name { get; set; } = "";
        public KeyCode Key { get; set; }
        public bool ActiveInGUI { get; set; }
        public bool ActiveInCustomGUI { get; set; }
        public Jotunn.Managers.InputManager.GamepadButton GamepadButton { get; set; }
    }
}
namespace Jotunn.Managers
{
    public sealed class LocalizationManager
    {
        public static LocalizationManager Instance { get; } = new();
        internal readonly Dictionary<string, Jotunn.Entities.CustomLocalization> Localizations = new();
        public bool AddLocalization(Jotunn.Entities.CustomLocalization localization)
        {
            Localizations.Add(localization.Metadata.GUID, localization); return true;
        }
    }
    public sealed class InputManager
    {
        public enum GamepadButton { None, ButtonSouth, ButtonNorth, StartButton, SelectButton, LeftShoulder, RightShoulder }
        public static InputManager Instance { get; } = new();
        internal static readonly Dictionary<string, Configs.ButtonConfig> Buttons = new();
        internal static bool FailNext;
        public void AddButton(string guid, Configs.ButtonConfig config)
        {
            config.Name += "!" + guid;
            Buttons.Add(config.Name, config);
            if (ZInput.instance != null)
            {
                if (config.GamepadButton == GamepadButton.None)
                    ZInput.instance.m_buttons.Add(config.Name, new ZInput.ButtonDef { Key = config.Key });
                else
                {
                    ZInput.instance.m_buttons.Add("Joy!" + config.Name, new ZInput.ButtonDef { Controller = config.GamepadButton });
                    ZInput.instance.m_presentationButtons.Add(new ZInput.LayoutButton { Name = "Joy!" + config.Name }, new object());
                }
            }
            if (FailNext) { FailNext = false; throw new InvalidOperationException("Simulated partial registration failure"); }
        }
        // Simulates Jotunn's reverse-patch read, which can replace a suppressed result.
        internal static void Postfix(string name, ref bool __result)
        {
            __result = ZInput.Raw(name) || ZInput.Raw("Joy!" + name);
            if (Buttons.TryGetValue(name, out var config) && GUIManager.Requests > 0 && !config.ActiveInCustomGUI)
                __result = false;
        }
    }
    public static class GUIManager
    {
        internal static int Requests, Calls;
        private static Action? available;
        internal static int Subscriptions;
        public static event Action OnCustomGUIAvailable
        {
            add { available += value; Subscriptions++; }
            remove { available -= value; Subscriptions--; }
        }
        public static void BlockInput(bool state) { Calls++; Requests = Math.Max(0, Requests + (state ? 1 : -1)); }
        internal static void Recreate() { Requests = 0; available?.Invoke(); }
    }
}

internal sealed class Localization
{
    public static Localization instance = new();
    internal readonly Dictionary<string, string> m_translations = new();
}
namespace Jotunn.Entities
{
    public sealed class CustomLocalization
    {
        internal readonly BepInEx.BepInPlugin Metadata;
        private readonly Dictionary<string, Dictionary<string, string>> map = new();
        public CustomLocalization(BepInEx.BepInPlugin metadata) { Metadata = metadata; }
        public void AddJsonFile(string language, string json)
        {
            var values = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
            map.Add(language, values);
            foreach (var pair in values)
                if (!Localization.instance.m_translations.ContainsKey(pair.Key)) Localization.instance.m_translations.Add(pair.Key, pair.Value);
        }
        public IReadOnlyDictionary<string, string> GetTranslations(in string language) => map[language];
        public IEnumerable<string> GetLanguages() => map.Keys;
        public bool Contains(in string language, in string token) => map.TryGetValue(language, out var values) && values.ContainsKey(token);
        public string TryTranslate(string token)
        {
            string name = token.TrimStart('$');
            return map.TryGetValue(PlayerPrefs.Language, out var language) && language.TryGetValue(name, out string? value) ? value : map["English"][name];
        }
        public void ClearAll() => map.Clear();
    }
}
