using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;

namespace Ironbound.Configuration;

internal static class ConfigMigration
{
    // Called before any Bind, so legacy entries are still in BepInEx's unbound dictionary.
    internal static void Upgrade(ConfigFile config)
    {
        if (BrandingMigration.ImportConfig(config.ConfigFilePath)) config.Reload();
        if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config, null)
            is not Dictionary<ConfigDefinition, string> entries)
            throw new InvalidOperationException("Cannot read unbound configuration; refusing to reset saved settings.");
        var values = new Dictionary<(string Section, string Key), string>();
        foreach (var entry in entries)
        {
            values[(entry.Key.Section, entry.Key.Key)] = entry.Value;
        }
        ConfigSchema.Migrate(values);
        bool changed = values.Count != entries.Count;
        foreach (var entry in values)
            changed |= !entries.TryGetValue(new ConfigDefinition(entry.Key.Section, entry.Key.Key), out string old) || old != entry.Value;
        if (!changed) return;
        if (File.Exists(config.ConfigFilePath))
        {
            string backup = config.ConfigFilePath + ".before-modules.bak";
            if (!File.Exists(backup)) File.Copy(config.ConfigFilePath, backup);
        }
        entries.Clear();
        foreach (var entry in values)
            entries[new ConfigDefinition(entry.Key.Section, entry.Key.Key)] = entry.Value;
    }
}
