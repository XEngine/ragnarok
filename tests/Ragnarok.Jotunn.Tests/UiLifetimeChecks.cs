using System;
using BepInEx;
using Jotunn.Managers;
using Ragnarok.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine
{
    internal sealed class CanvasGroup : Object
    {
        private float value = 1;
        public float alpha { get => value; set { if (Destroyed) throw new NullReferenceException("destroyed native CanvasGroup"); this.value = value; } }
    }
    internal sealed class RectTransform : Object
    {
        private readonly GameObject node = new();
        public GameObject gameObject => Destroyed ? throw new NullReferenceException("destroyed native Transform") : node;
    }
}
namespace UnityEngine.UI
{
    internal sealed class RawImage : UnityEngine.Object
    {
        private object? value;
        public object? texture { get => value; set { if (Destroyed) throw new NullReferenceException("destroyed native RawImage"); this.value = value; } }
    }
}
internal static class UiLifetimeChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var group = new CanvasGroup();
        UiLifetime.ResetAlpha(group);
        Check(group.alpha == 0, "Live notification fade was not reset");
        UnityEngine.Object.Destroy(group);
        Check(!ReferenceEquals(group, null) && !group, "Double must model Unity fake-null");
        UiLifetime.ResetAlpha(group); UiLifetime.ResetAlpha(null);
        Check(true, "Destroyed native fade cleanup threw");
        var image = new RawImage { texture = new object() };
        UiLifetime.DetachTexture(image);
        Check(image.texture == null, "Live portrait texture was not detached");
        UnityEngine.Object.Destroy(image);
        UiLifetime.DetachTexture(image); UiLifetime.DetachTexture(null);
        Check(true, "Destroyed native portrait cleanup threw");
        var root = new RectTransform();
        var node = root.gameObject;
        UiLifetime.DestroyRoot(root);
        Check(node.Destroyed && !node.activeSelf, "Owned root not hidden/destroyed");
        UnityEngine.Object.Destroy(root);
        UiLifetime.DestroyRoot(root); UiLifetime.DestroyRoot(null);
        Check(true, "Destroyed native root cleanup threw");
        int failures = 0;
        bool inputReleased = false, restReleased = false;
        using var localization = new UiLocalization(new BepInPlugin("lifecycle-regression"));
        UiLifetime.Cleanup(() => inputReleased = true, _ => failures++);
        UiLifetime.Cleanup(() => throw new InvalidOperationException("broken renderer"), _ => failures++);
        UiLifetime.Cleanup(localization.Dispose, _ => failures++);
        UiLifetime.Cleanup(() => restReleased = true, _ => failures++);
        Check(inputReleased && restReleased && failures == 1, "Failed renderer interrupted remaining releases");
        Check(!LocalizationManager.Instance.Localizations.ContainsKey("lifecycle-regression"), "Failed renderer left localization registered");
        Console.WriteLine($"PASS: {checks} destroyed-native-object and failure-isolated unload checks.");
    }
}
