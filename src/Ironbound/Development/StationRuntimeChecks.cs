#if DEBUG
using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace Ironbound.Development;

internal static class StationRuntimeChecks
{
    internal static void Run(ManualLogSource log)
    {
        // Inactive objects avoid station Awake, RPCs and world saves. Exercise real component fields.
        var root = new GameObject("smelter");
        root.SetActive(false);
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "ironbound-station-check-" + Guid.NewGuid() + ".cfg"), false)
            { SaveOnConfigSet = false };
        using var modifiers = new StationModifiers(config);
        try
        {
            var station = root.AddComponent<Smelter>();
            station.m_maxFuel = 20;
            station.m_maxOre = 10;
            station.m_secPerProduct = 30f;
            modifiers.Track(station);
            Check(station.m_maxFuel == 20 && station.m_maxOre == 10 && station.m_secPerProduct == 30f, "default preservation");
            config["Gameplay.Smelter", "FuelCapacity"].BoxedValue = 50;
            config["Gameplay.Smelter", "InputCapacity"].BoxedValue = 40;
            config["Gameplay.Smelter", "SpeedMultiplier"].BoxedValue = 2f;
            modifiers.Tick();
            Check(station.m_maxFuel == 50 && station.m_maxOre == 40 && station.m_secPerProduct == 15f, "live overrides");
            config["Gameplay.Smelter", "SpeedMultiplier"].BoxedValue = 3f;
            modifiers.Tick();
            modifiers.Track(station);
            Check(station.m_secPerProduct == 10f, "no compounded speed or duplicate tracking");
            config["Gameplay.Smelter", "FuelCapacity"].BoxedValue = 0;
            modifiers.Tick();
            Check(station.m_maxFuel == 20, "zero restores original capacity");
            modifiers.Dispose();
            Check(station.m_maxFuel == 20 && station.m_maxOre == 10 && station.m_secPerProduct == 30f, "unload restoration");
            log.LogInfo("Ironbound station checks PASS: defaults, live capacities, speed changes, zero reset and unload restoration.");
        }
        finally { UnityEngine.Object.Destroy(root); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Station check failed: " + label);
    }
}
#endif
