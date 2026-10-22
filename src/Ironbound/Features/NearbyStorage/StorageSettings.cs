using BepInEx.Configuration;
using Ironbound.Configuration;
using UnityEngine;

namespace Ironbound.Features.NearbyStorage;

internal sealed class StorageSettings
{
    internal readonly ConfigEntry<bool> Enabled;
    internal readonly ConfigEntry<float> Range;
    internal readonly ConfigEntry<float> PersonalRange;
    internal float EffectiveRange => RangeLimit.Effective(PersonalRange, Range);

    internal readonly ConfigEntry<KeyboardShortcut> ChestSearchKey;

    internal StorageSettings(ConfigFile config)
    {
        const string section = "QualityOfLife.NearbyChests";
        ChestSearchKey = config.Bind("QualityOfLife.NearbyChests", "Shortcut", new KeyboardShortcut(KeyCode.F, KeyCode.LeftControl), "Open item search. Ctrl+F by default.");

        Enabled = config.Bind(section, "Enabled", true, "Use nearby player-built chests for crafting, building, station supplies, Quick Stack and item search. One switch controls all chest actions. Defined and locked by the server.");
        Range = config.Bind(section, "Range", 100f, new ConfigDescription(
            "Shared maximum radius in metres for crafting, building, station supplies, Quick Stack and search. Only loaded, accessible player-built chests are available. Defined and locked by the server; chest owners enforce this maximum for network requests.",
            new AcceptableValueRange<float>(1f, 500f)));
        PersonalRange = config.Bind(section, "PersonalRange", 500f, new ConfigDescription(
            "Your shared radius in metres for all nearby chest actions, limited to the server maximum. Your preference is retained when changing servers.",
            new AcceptableValueRange<float>(1f, 500f), new RangeLimit(Range)));
    }
}
