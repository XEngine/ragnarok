using BepInEx.Configuration;

namespace Ragnarok.Features.NearbyStorage;

internal sealed class StorageSettings
{
    internal readonly ConfigEntry<bool> Enabled;
    internal readonly ConfigEntry<float> Range;
    internal readonly ConfigEntry<bool> Crafting;
    internal readonly ConfigEntry<bool> Building;
    internal readonly ConfigEntry<bool> Stations;

    internal StorageSettings(ConfigFile config)
    {
        const string section = "QualityOfLife.NearbyChests";
        Enabled = config.Bind(section, "Enabled", true, "Use resources from nearby player-built chests.");
        Range = config.Bind(section, "Range", 20f, new ConfigDescription(
            "Maximum distance in metres from the player to a chest. Chest owners enforce their own range for network requests.",
            new AcceptableValueRange<float>(1f, 50f)));
        Crafting = config.Bind(section, "Crafting", true, "Craft and upgrade using nearby chest resources.");
        Building = config.Bind(section, "Building", true, "Build using nearby chest resources.");
        Stations = config.Bind(section, "Stations", true,
            "Pull fuel and ingredients when using smelters, kilns, fires, cooking stations and fermenters. No automatic refilling.");
    }
}
