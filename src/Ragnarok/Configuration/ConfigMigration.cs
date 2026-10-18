using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;

namespace Ragnarok.Configuration;

internal static class ConfigMigration
{
    // Called before any Bind, so legacy entries are still in BepInEx's unbound dictionary.
    internal static void Upgrade(ConfigFile config)
    {
        if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config, null)
            is not Dictionary<ConfigDefinition, string> entries)
            throw new InvalidOperationException("Cannot read unbound configuration; refusing to reset saved settings.");
        var values = new Dictionary<(string Section, string Key), string>();
        bool changed = false;
        foreach (var entry in entries)
        {
            values[(entry.Key.Section, entry.Key.Key)] = entry.Value;
            changed |= ConfigSchema.CanonicalSection(entry.Key.Section, entry.Key.Key) != entry.Key.Section;
        }
        if (!changed) return;
        if (File.Exists(config.ConfigFilePath))
        {
            string backup = config.ConfigFilePath + ".before-modules.bak";
            if (!File.Exists(backup)) File.Copy(config.ConfigFilePath, backup);
        }
        ConfigSchema.Migrate(values);
        entries.Clear();
        foreach (var entry in values)
            entries[new ConfigDefinition(entry.Key.Section, entry.Key.Key)] = entry.Value;
    }
}
