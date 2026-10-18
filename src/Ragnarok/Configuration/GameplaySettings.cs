using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace Ragnarok.Configuration;

internal sealed class GameplaySettings
{
    internal static GameplaySettings? Current;
    internal readonly ConfigEntry<float> PowerDuration, PowerCooldown;
    internal readonly ConfigEntry<int> HarvestMultiplier;
    internal readonly ConfigEntry<int> SkillLossModifier;
    internal readonly ConfigEntry<int> SkillXpModifier;
    internal readonly ConfigEntry<int> RunCost, AttackCost, JumpCost, EitrCost;

    internal GameplaySettings(ConfigFile config)
    {
        ConfigEntry<int> Cost(string name, string action) => config.Bind("Gameplay.ResourceCosts", name, 0,
            new ConfigDescription("Percentage adjustment to " + action + ": 0 = normal, -50 = half, -100 = no cost, +50 = 50% more. " +
                "Normal skill/equipment/world modifiers and action requirements still apply.", new AcceptableValueRange<int>(-100, 900)));
        RunCost = Cost("RunStaminaModifierPercent", "running stamina drain");
        AttackCost = Cost("AttackStaminaModifierPercent", "attack, weapon draw and weapon reload stamina costs");
        JumpCost = Cost("JumpStaminaModifierPercent", "jump stamina cost");
        EitrCost = Cost("AttackEitrModifierPercent", "attack, weapon draw and weapon reload eitr costs");
        SkillXpModifier = config.Bind("Gameplay.Skills", "ExperienceGainModifierPercent", 0,
            new ConfigDescription("Adjust XP earned by all skills: 0 = normal, +50 = 50% more, +100 = double, " +
                "-50 = half, -100 = no XP. Multiplies normal XP, including world and active buff modifiers. " +
                "Does not change existing levels, the level cap, or vanilla's per-action overflow behavior.",
                new AcceptableValueRange<int>(-100, 900)));
        SkillLossModifier = config.Bind("Gameplay.Death", "SkillLossModifierPercent", 0,
            new ConfigDescription("Adjust the normal skill-level loss on death after world difficulty modifiers: " +
                "0 = unchanged, -50 = half the loss, -100 = no loss, +100 = double. " +
                "At -100, progress toward the next skill level is also preserved. Normal death protection still applies. " +
                "Does not override the world's separate full skill-reset death rule.",
                new AcceptableValueRange<int>(-100, 100)));
        PowerDuration = config.Bind("Gameplay.ForsakenPowers", "DurationMinutes", 10f,
            new ConfigDescription("Active duration of Forsaken powers, including buffs shared with nearby players. Applies on the next activation.",
                new AcceptableValueRange<float>(0.1f, 60f)));
        PowerCooldown = config.Bind("Gameplay.ForsakenPowers", "CooldownMinutes", 15f,
            new ConfigDescription("Cooldown after activating a Forsaken power. Applies on the next activation.",
                new AcceptableValueRange<float>(0.1f, 120f)));
        HarvestMultiplier = config.Bind("Gameplay.Harvesting", "YieldMultiplier", 1,
            new ConfigDescription("Whole-number yield multiplier for plants, crops, berries, mushrooms and beehive honey. " +
                "Multiplies the normal harvest (including world resource modifiers). Normal auto-pickup and inventory limits apply. " +
                "Does not multiply loose dropped items, mined resources, enemy loot or hive production speed.",
                new AcceptableValueRange<int>(1, 10)));
    }

    internal static float Cooldown(float vanilla) => Current == null ? vanilla : Current.PowerCooldown.Value * 60f;
    internal static int Harvest(int amount) => checked(amount * (Current?.HarvestMultiplier.Value ?? 1));

    internal static readonly HashSet<int> Powers = new()
    {
        "GP_Eikthyr".GetStableHashCode(), "GP_TheElder".GetStableHashCode(), "GP_Bonemass".GetStableHashCode(),
        "GP_Moder".GetStableHashCode(), "GP_Yagluth".GetStableHashCode(), "GP_Queen".GetStableHashCode(),
        "GP_Fader".GetStableHashCode()
    };

    private static readonly HashSet<string> Plants = new(StringComparer.Ordinal)
    {
        "Raspberry", "Blueberries", "Cloudberry", "Mushroom", "MushroomYellow", "MushroomBlue",
        "MushroomJotunPuffs", "MushroomMagecap", "Carrot", "CarrotSeeds", "Turnip", "TurnipSeeds",
        "Onion", "OnionSeeds", "Barley", "Flax", "Dandelion", "Thistle", "Vineberry", "Fiddlehead"
    };

    internal static bool IsPlant(Pickable pickable) => pickable.m_harvestable ||
        (pickable.m_itemPrefab && Plants.Contains(pickable.m_itemPrefab.name));
}
