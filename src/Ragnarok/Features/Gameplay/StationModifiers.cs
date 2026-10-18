using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.Gameplay;

internal sealed class StationModifiers : IDisposable
{
    internal static StationModifiers? Current;
    internal static readonly Type[] Types = { typeof(Smelter), typeof(SapCollector), typeof(Beehive), typeof(Fermenter), typeof(CookingStation) };
    private readonly ConfigFile config;
    private readonly Dictionary<string, Settings> settings = new(StringComparer.Ordinal);
    private readonly Dictionary<Component, List<Override>> tracked = new();
    internal readonly List<ConfigEntryBase> Synced = new();
    private bool dirty;
    private float nextPrune;

    private sealed class Settings
    {
        internal ConfigEntry<int>? Fuel, Input, Output;
        internal ConfigEntry<float> Speed = null!;
    }

    // Save instance values once. Re-applying a config never compounds a multiplier, and unloading
    // restores the values without changing networked fuel, queued items, or production timestamps.
    private sealed class Override
    {
        internal object Target = null!;
        internal FieldInfo Field = null!;
        internal object Original = null!;
        internal Func<object, object> Value = null!;
        internal void Apply() => Field.SetValue(Target, Value(Original));
        internal void Restore() => Field.SetValue(Target, Original);
    }

    internal StationModifiers(ConfigFile config)
    {
        this.config = config;
        Add("smelter", "Smelter", true, true);
        Add("blastfurnace", "BlastFurnace", true, true);
        Add("charcoal_kiln", "CharcoalKiln", false, true);
        Add("windmill", "Windmill", false, true);
        Add("spinningwheel", "SpinningWheel", false, true);
        Add("eitrrefinery", "EitrRefinery", true, true);
        Add("sap", "SapExtractor", false, false, true);
        Add("honey", "Beehive", false, false, true);
        Add("fermenter", "Fermenter", false, false);
        Add("oven", "Oven", true, false);
        Add("cooking", "CookingStations", false, false);
        config.SettingChanged += Changed;
    }

    private void Add(string key, string page, bool fuel, bool input, bool output = false)
    {
        string section = "Gameplay." + page;
        var entry = new Settings();
        ConfigEntry<int> Capacity(string name, string what)
        {
            var value = config.Bind(section, name, 0, new ConfigDescription(
                "Maximum " + what + ". 0 keeps the station's original capacity. Existing contents are retained when lowering this setting.",
                new AcceptableValueRange<int>(0, 1000)));
            Synced.Add(value);
            return value;
        }
        if (fuel) entry.Fuel = Capacity("FuelCapacity", "fuel units");
        if (input) entry.Input = Capacity("InputCapacity", "queued ingredients");
        if (output) entry.Output = Capacity("OutputCapacity", "stored products");
        entry.Speed = config.Bind(section, "SpeedMultiplier", 1f, new ConfigDescription(
            "Production speed: 1 is original speed, 2 is twice as fast. Wind, shelter, fire and resource requirements still apply. " +
            "Cooking also reaches its burn deadline sooner. Changes apply to existing stations and work in progress.",
            new AcceptableValueRange<float>(0.1f, 20f)));
        Synced.Add(entry.Speed);
        settings.Add(key, entry);
    }

    private void Changed(object sender, SettingChangedEventArgs args)
    {
        if (Synced.Contains(args.ChangedSetting)) dirty = true;
    }

    internal void Initialize()
    {
        foreach (var type in Types)
            foreach (var component in UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None))
                Track((Component)component);
    }

    internal void Track(Component component)
    {
        if (!component || tracked.ContainsKey(component)) return;
        string key;
        if (component is Smelter)
            key = component.gameObject.name.Replace("(Clone)", "").Trim();
        else if (component is SapCollector) key = "sap";
        else if (component is Beehive) key = "honey";
        else if (component is Fermenter) key = "fermenter";
        else if (component is CookingStation cooking) key = cooking.m_useFuel ? "oven" : "cooking";
        else return;
        if (!settings.TryGetValue(key, out var rule)) return;
        var values = new List<Override>();
        void Field(object target, string name, Func<object, object> value)
        {
            var field = AccessTools.Field(target.GetType(), name);
            values.Add(new Override { Target = target, Field = field, Original = field.GetValue(target), Value = value });
        }
        void Capacity(string field, ConfigEntry<int>? entry)
        {
            if (entry != null) Field(component, field, original => entry.Value == 0 ? original : entry.Value);
        }
        void Speed(object target, string field) => Field(target, field, original => (float)original / rule.Speed.Value);
        if (component is Smelter)
        {
            Capacity("m_maxFuel", rule.Fuel);
            Capacity("m_maxOre", rule.Input);
            Speed(component, "m_secPerProduct");
        }
        else if (component is SapCollector)
        {
            Capacity("m_maxLevel", rule.Output);
            Speed(component, "m_secPerUnit");
        }
        else if (component is Beehive)
        {
            Capacity("m_maxHoney", rule.Output);
            Speed(component, "m_secPerUnit");
        }
        else if (component is Fermenter) Speed(component, "m_fermentationDuration");
        else if (component is CookingStation station)
        {
            Capacity("m_maxFuel", rule.Fuel);
            foreach (var conversion in station.m_conversion) Speed(conversion, "m_cookTime");
        }
        tracked.Add(component, values);
        foreach (var value in values) value.Apply();
    }

    internal void Tick()
    {
        if (!dirty && Time.unscaledTime < nextPrune) return;
        nextPrune = Time.unscaledTime + 5f;
        foreach (var pair in tracked.ToArray())
        {
            if (!pair.Key) { tracked.Remove(pair.Key!); continue; }
            if (dirty) foreach (var value in pair.Value) value.Apply();
        }
        dirty = false;
    }

    public void Dispose()
    {
        config.SettingChanged -= Changed;
        foreach (var pair in tracked)
            if (pair.Key) foreach (var value in pair.Value) value.Restore();
        tracked.Clear();
        if (Current == this) Current = null;
    }
}

[HarmonyPatch]
internal static class StationAwakePatch
{
    private static IEnumerable<MethodBase> TargetMethods() => StationModifiers.Types.Select(type => AccessTools.Method(type, "Awake"));
    private static void Postfix(Component __instance) => StationModifiers.Current?.Track(__instance);
}

// Vanilla clamps stored honey to capacity on every production tick. Preserve an overfull hive
// after capacity is lowered while the mod is active; harvesting still resets it normally.
[HarmonyPatch(typeof(Beehive), "IncreseLevel")]
internal static class PreserveStoredHoneyPatch
{
    private static bool Prefix(Beehive __instance, ZNetView ___m_nview) =>
        !___m_nview || !___m_nview.IsValid() || ___m_nview.GetZDO().GetInt(ZDOVars.s_level) < __instance.m_maxHoney;
}
