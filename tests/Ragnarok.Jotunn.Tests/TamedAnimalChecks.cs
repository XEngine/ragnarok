using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Ragnarok.HUD;
using UnityEngine;

namespace UnityEngine
{
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public sealed class Sprite : Object
    {
        public string name = "";
        internal Texture2D Texture = null!;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit) => new Sprite { Texture = texture };
    }
    public sealed class Transform { public Vector3 position = new Vector3(10, 0, 10); }
}
namespace Ragnarok.Configuration
{
    internal sealed class InterfaceSettings { internal readonly ConfigEntry<bool> TamedAnimalDots = new(true); }
}
internal sealed class Tameable : UnityEngine.Object { }
internal sealed class Character : UnityEngine.Object
{
    internal static readonly List<Character> Loaded = new();
    public readonly Transform transform = new();
    public bool isActiveAndEnabled = true;
    internal bool Tamed = true, Dead, Human;
    internal Tameable? Tameable = new();
    public static List<Character> GetAllCharacters() => Loaded;
    public bool IsPlayer() => Human;
    public bool IsDead() => Dead;
    public bool IsTamed() => Tamed;
    public T? GetComponent<T>() where T : class => Tameable as T;
}
internal static class TamedAnimalChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var settings = new Ragnarok.Configuration.InterfaceSettings();
        var markers = new TamedAnimalMarkers(settings);
        var map = Minimap.instance;
        var foreign = map.AddPin(new Vector3(), Minimap.PinType.None, "foreign", true, false);
        var tame = new Character();
        var hidden = new Character(); hidden.transform.position = new Vector3(-1, 0, 0);
        Character.Loaded.AddRange(new[] { tame, hidden, new Character { Tamed = false },
            new Character { Dead = true }, new Character { Human = true }, new Character { Tameable = null } });
        for (int round = 0; round < 3; round++)
        {
            Time.unscaledTime += 1;
            markers.Tick();
            Check(map.Pins.Count == 2, "Only a living explored tame animal should receive a dot");
            var pin = map.Pins[1];
            Check(!pin.Saved && pin.m_icon != null, "Animal pin must be temporary with a custom icon");
            var sprite = pin.m_icon!;
            var texture = sprite.Texture;
            tame.transform.position = new Vector3(20 + round, 0, 10);
            Time.unscaledTime += 1;
            markers.Tick();
            Check(map.Pins.Count == 2 && pin.m_pos.x == 20 + round, "Moving animal duplicated or failed to move its dot");
            settings.TamedAnimalDots.Value = false;
            markers.Tick();
            Check(map.Pins.Count == 1 && map.Pins[0] == foreign, "Disable removed a foreign pin or retained an animal dot");
            Check(sprite.Destroyed && texture.Destroyed, "Owned dot assets leaked");
            settings.TamedAnimalDots.Value = true;
        }
        markers.Tick();
        tame.Dead = true;
        Time.unscaledTime += 1;
        markers.Tick();
        Check(map.Pins.Count == 1, "Dead animal retained a dot");
        tame.Dead = false;
        Time.unscaledTime += 1;
        markers.Tick();
        Character.Loaded.Clear();
        Time.unscaledTime += 1;
        markers.Tick();
        Check(map.Pins.Count == 1, "Unloaded animal retained a dot");
        markers.Dispose(); markers.Dispose();
        Check(map.Pins.Count == 1, "Repeated unload affected another pin");
        map.RemovePin(foreign);
        Console.WriteLine($"PASS: {checks} tamed animal marker filtering, movement, death/unload and reload cleanup checks.");
    }
}
