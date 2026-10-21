using System;
using System.Collections.Generic;
using HarmonyLib;
using Ragnarok.Configuration;
using UnityEngine;

namespace Ragnarok.HUD;

// Temporary local pins. Never write animal ZDOs, claim ownership or save map pins.
internal sealed class TamedAnimalMarkers : IDisposable
{
    private static readonly Func<Minimap, Vector3, bool> IsExplored =
        AccessTools.MethodDelegate<Func<Minimap, Vector3, bool>>(AccessTools.Method(typeof(Minimap), "IsExplored"));
    private readonly InterfaceSettings settings;
    private readonly Dictionary<Character, Minimap.PinData> pins = new();
    private readonly HashSet<Character> seen = new();
    private readonly List<Character> removed = new();
    private Minimap? map;
    private Player? player;
    private Texture2D? texture;
    private Sprite? dot;
    private float nextScan;

    internal TamedAnimalMarkers(InterfaceSettings settings) => this.settings = settings;

    internal void Tick()
    {
        if (map != Minimap.instance || player != Player.m_localPlayer) Dispose();
        if (!settings.TamedAnimalDots.Value || !Minimap.instance || !Player.m_localPlayer)
        { Dispose(); return; }
        map = Minimap.instance;
        player = Player.m_localPlayer;
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.25f;
        seen.Clear();
        foreach (Character animal in Character.GetAllCharacters())
        {
            if (!animal || !animal.isActiveAndEnabled || animal.IsPlayer() || animal.IsDead() ||
                !animal.IsTamed() || !animal.GetComponent<Tameable>()) continue;
            Vector3 position = animal.transform.position;
            if (!IsExplored(map, position)) continue;
            seen.Add(animal);
            if (!pins.TryGetValue(animal, out Minimap.PinData pin))
            {
                EnsureDot();
                pin = map.AddPin(position, Minimap.PinType.None, "", false, false);
                pin.m_icon = dot;
                pins.Add(animal, pin);
            }
            pin.m_pos = position;
        }
        removed.Clear();
        foreach (var pair in pins)
            if (!seen.Contains(pair.Key)) { map.RemovePin(pair.Value); removed.Add(pair.Key); }
        foreach (Character animal in removed) pins.Remove(animal);
    }

    private void EnsureDot()
    {
        if (dot) return;
        // Transparent padding keeps the dot smaller than a normal pin at every zoom.
        const int size = 32;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Ragnarok tamed animal dot", filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                pixels[y * size + x] = distance <= 4 ? new Color(1, 0.85f, 0.12f, 1)
                    : new Color(0.12f, 0.1f, 0.025f, Mathf.Clamp01(6 - distance));
            }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        dot = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        dot.name = "Ragnarok tamed animal dot";
    }

    public void Dispose()
    {
        if (map)
            foreach (Minimap.PinData pin in pins.Values) map!.RemovePin(pin);
        pins.Clear();
        seen.Clear();
        removed.Clear();
        if (dot) UnityEngine.Object.Destroy(dot);
        if (texture) UnityEngine.Object.Destroy(texture);
        dot = null;
        texture = null;
        map = null;
        player = null;
        nextScan = 0;
    }
}
