using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Color
    {
        public float a;
        public Color(float r, float g, float b, float a) { this.a = a; }
        public static Color clear => new Color(0, 0, 0, 0);
    }
    public struct Color32 { }
    public static class Mathf { public static int RoundToInt(float value) => (int)Math.Round(value); public static float Sqrt(float value) => (float)Math.Sqrt(value); public static float Clamp01(float value) => Math.Max(0, Math.Min(1, value)); }
    public static class Debug { public static void LogWarning(object text) { Console.WriteLine(text); } }
    public sealed class Texture2D : Object
    {
        public int width = 32, height = 32;
        public string name = "";
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D() { }
        public Texture2D(int width, int height, TextureFormat format, bool mipmaps) { this.width = width; this.height = height; }
        public void SetPixels(Color[] pixels) { for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) SetPixel(x, y, pixels[y * width + x]); }
        internal readonly Dictionary<(int, int), Color> Pixels = new();
        internal int Applies;
        public void SetPixels32(Color32[] pixels)
        {
            if (pixels.Length != width * height) throw new Exception("wrong clear size");
            Pixels.Clear();
        }
        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) throw new Exception("pixel outside map");
            if (color.a == 0) Pixels.Remove((x, y)); else Pixels[(x, y)] = color;
        }
        public void Apply(bool mipmaps, bool unreadable) { Applies++; }
    }
}
namespace UnityEngine.UI
{
    public sealed class Toggle : UnityEngine.Object
    {
        public GameObject gameObject = new();
        public readonly ToggleEvent onValueChanged = new();
        public sealed class ToggleEvent
        {
            internal int Listeners = 1;
            public void RemoveAllListeners() { Listeners = 0; }
        }
    }
}
internal sealed class Minimap : UnityEngine.Object
{
    public static Minimap instance = new();
    public enum PinType { None }
    public sealed class PinData { public UnityEngine.Vector3 m_pos; public UnityEngine.Sprite? m_icon; public bool Saved; }
    internal readonly List<PinData> Pins = new();
    private bool IsExplored(UnityEngine.Vector3 position) => position.x >= 0;
    public PinData AddPin(UnityEngine.Vector3 pos, PinType type, string name, bool save, bool check)
    { var pin = new PinData { m_pos = pos, Saved = save }; Pins.Add(pin); return pin; }
    public void RemovePin(PinData pin) => Pins.Remove(pin);
}
namespace Jotunn.Managers
{
    public sealed class MinimapManager
    {
        public static MinimapManager Instance { get; } = new();
        internal readonly Dictionary<string, MapOverlay> Overlays = new();
        internal object? OverlayPanel;
        internal int Starts, Redraws, CompositePixels;
        private void Minimap_Start() { Starts++; OverlayPanel = new object(); }
        public sealed class MapOverlay : MapOverlayBase
        {
            public readonly Texture2D OverlayTex = new();
            public bool Enabled { get; set; } = true;
            internal bool IgnoreFog;
        }
        public class MapOverlayBase { internal readonly UnityEngine.UI.Toggle Toggle = new(); }
        public MapOverlay GetMapOverlay(string name, bool ignoreFog = false)
        {
            if (!Overlays.TryGetValue(name, out var overlay)) Overlays.Add(name, overlay = new MapOverlay { IgnoreFog = ignoreFog });
            return overlay;
        }
        public bool RemoveMapOverlay(string name) => Overlays.Remove(name);
        public List<string> GetOverlayNames() => Overlays.Keys.ToList();
        public Vector2 WorldToOverlayCoords(Vector3 position, int size) => new Vector2(position.x, position.z);
        private IEnumerator DrawOverlay()
        {
            Redraws++; CompositePixels = Overlays.Values.Where(layer => layer.Enabled).Sum(layer => layer.OverlayTex.Pixels.Count);
            yield return null;
        }
    }
}
