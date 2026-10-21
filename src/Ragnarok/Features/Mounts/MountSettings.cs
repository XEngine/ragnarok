using BepInEx.Configuration;
using UnityEngine;

namespace Ragnarok.Features.Mounts;

internal sealed class MountSettings
{
    internal readonly ConfigEntry<bool> Enabled;
    internal readonly ConfigEntry<float> CombatQuiet, Cooldown;
    internal readonly ConfigEntry<float> StaminaPercent, SummonXp, BindXp, XpCooldown, ExpertWindup;
    internal readonly ConfigEntry<KeyboardShortcut> BindKey, WhistleKey;
    internal ConfigEntryBase[] Synced => new ConfigEntryBase[] { Enabled, CombatQuiet, Cooldown, StaminaPercent, SummonXp, BindXp, XpCooldown, ExpertWindup };
    internal MountSettings(ConfigFile config)
    {
        const string section = "QualityOfLife.Mounts";
        Enabled = config.Bind(section, "Enabled", true, "Allow one bound saddle mount per player and outdoor whistle summons. Defined and locked by the server. Requires Ragnarok on the server.");
        CombatQuiet = config.Bind(section, "CombatQuietSeconds", 15f, new ConfigDescription("Required time without attacks, damage or enemy targeting before summoning. Applies to the player and mount. Server-controlled.", new AcceptableValueRange<float>(5, 120)));
        Cooldown = config.Bind(section, "SummonCooldownSeconds", 10f, new ConfigDescription("Minimum interval between successful summons. Server-controlled.", new AcceptableValueRange<float>(5, 120)));
        StaminaPercent = config.Bind(section, "SummonStaminaPercent", 10f, new ConfigDescription("Percentage of maximum stamina charged on confirmed arrival at Riding 0, falling linearly to zero at Riding 100. Server-controlled.", new AcceptableValueRange<float>(0, 100)));
        SummonXp = config.Bind(section, "SummonRidingExperience", 0.25f, new ConfigDescription("Native Riding XP factor for a successful summon, limited by the reward cooldown. Not whole skill levels. Server-controlled.", new AcceptableValueRange<float>(0, 5)));
        BindXp = config.Bind(section, "BindingRidingExperience", 0.5f, new ConfigDescription("Native Riding XP factor for the first bond with each animal per player in this world. Rebinding gives no further reward. Server-controlled.", new AcceptableValueRange<float>(0, 5)));
        XpCooldown = config.Bind(section, "SummonExperienceCooldownSeconds", 300f, new ConfigDescription("Minimum real seconds between summon XP rewards, persisted across server restarts. Server-controlled.", new AcceptableValueRange<float>(60, 3600)));
        ExpertWindup = config.Bind(section, "ExpertWhistleSeconds", 0.6f, new ConfigDescription("Whistle windup at Riding 100; at Riding 0 it is 1.2 seconds. Server-controlled.", new AcceptableValueRange<float>(0.2f, 1.2f)));
        BindKey = config.Bind(section, "BindKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftShift), "Hold while looking at a nearby tamed saddled mount to bind it, or release your existing bond. Shift+H by default.");
        WhistleKey = config.Bind(section, "WhistleKey", new KeyboardShortcut(KeyCode.H), "Whistle to summon your bound mount to safe outdoor ground. H by default. No automatic mounting.");
    }
}
