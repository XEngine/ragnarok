using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;

namespace Ragnarok;

internal sealed class QualityOfLife
{
    internal static QualityOfLife? Current;
    internal readonly ConfigEntry<float> PowerDuration, PowerCooldown;
    internal readonly ConfigEntry<int> HarvestMultiplier;
    internal readonly ConfigEntry<int> SkillLossModifier;
    internal readonly ConfigEntry<int> SkillXpModifier;
    internal readonly ConfigEntry<int> RunCost, AttackCost, JumpCost, EitrCost;

    internal QualityOfLife(ConfigFile config)
    {
        ConfigEntry<int> Cost(string name, string action) => config.Bind("QoL.ResourceCosts", name, 0,
            new ConfigDescription("Percentage adjustment to " + action + ": 0 = normal, -50 = half, -100 = no cost, +50 = 50% more. " +
                "Normal skill/equipment/world modifiers and action requirements still apply.", new AcceptableValueRange<int>(-100, 900)));
        RunCost = Cost("RunStaminaModifierPercent", "running stamina drain");
        AttackCost = Cost("AttackStaminaModifierPercent", "attack, weapon draw and weapon reload stamina costs");
        JumpCost = Cost("JumpStaminaModifierPercent", "jump stamina cost");
        EitrCost = Cost("AttackEitrModifierPercent", "attack, weapon draw and weapon reload eitr costs");
        SkillXpModifier = config.Bind("QoL.Skills", "ExperienceGainModifierPercent", 0,
            new ConfigDescription("Adjust XP earned by all skills: 0 = normal, +50 = 50% more, +100 = double, " +
                "-50 = half, -100 = no XP. Multiplies normal XP, including world and active buff modifiers. " +
                "Does not change existing levels, the level cap, or vanilla's per-action overflow behavior.",
                new AcceptableValueRange<int>(-100, 900)));
        SkillLossModifier = config.Bind("QoL.Death", "SkillLossModifierPercent", 0,
            new ConfigDescription("Adjust the normal skill-level loss on death after world difficulty modifiers: " +
                "0 = unchanged, -50 = half the loss, -100 = no loss, +100 = double. " +
                "At -100, progress toward the next skill level is also preserved. Normal death protection still applies. " +
                "Does not override the world's separate full skill-reset death rule.",
                new AcceptableValueRange<int>(-100, 100)));
        PowerDuration = config.Bind("QoL.ForsakenPowers", "DurationMinutes", 10f,
            new ConfigDescription("Active duration of Forsaken powers, including buffs shared with nearby players. Applies on the next activation.",
                new AcceptableValueRange<float>(0.1f, 60f)));
        PowerCooldown = config.Bind("QoL.ForsakenPowers", "CooldownMinutes", 15f,
            new ConfigDescription("Cooldown after activating a Forsaken power. Applies on the next activation.",
                new AcceptableValueRange<float>(0.1f, 120f)));
        HarvestMultiplier = config.Bind("QoL.Harvesting", "YieldMultiplier", 1,
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

// Replace the cooldown value at its assignment, preserving vanilla activation checks and RPCs.
[HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
internal static class PowerCooldownPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.LoadsField(AccessTools.Field(typeof(StatusEffect), nameof(StatusEffect.m_cooldown)))) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(QualityOfLife), nameof(QualityOfLife.Cooldown)));
        }
        if (matches != 1) throw new InvalidOperationException("Forsaken power cooldown patch no longer matches this Valheim version.");
    }
}

// This path runs on the receiving player's owner, including powers shared over the network.
// Only the active instance is changed; ObjectDB's shared effect templates remain untouched.
[HarmonyPatch(typeof(SEMan), "Internal_AddStatusEffect")]
internal static class PowerDurationPatch
{
    private static void Postfix(SEMan __instance, int nameHash)
    {
        if (QualityOfLife.Current == null || !QualityOfLife.Powers.Contains(nameHash)) return;
        var effect = __instance.GetStatusEffect(nameHash);
        if (effect) effect.m_ttl = QualityOfLife.Current.PowerDuration.Value * 60f;
    }
}

// Drop is called only after RPC_Pick's ownership and already-picked checks.
// Multiply the primary harvest, leaving unrelated extra drops alone.
[HarmonyPatch(typeof(Pickable), "Drop")]
internal static class PlantYieldPatch
{
    private static void Prefix(Pickable __instance, UnityEngine.GameObject prefab, ref int stack)
    {
        if (prefab == __instance.m_itemPrefab && QualityOfLife.IsPlant(__instance))
            stack = QualityOfLife.Harvest(stack);
    }
}

// Apply once to each extracted honey stack after the game's resource modifier.
// Do not change stored honey levels, production timers, or drops from destroying a hive.
[HarmonyPatch(typeof(Beehive), "RPC_Extract")]
internal static class HoneyYieldPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var scale = AccessTools.Method(typeof(Game), nameof(Game.ScaleDrops), new[] { typeof(ItemDrop.ItemData), typeof(int) });
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(scale)) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(QualityOfLife), nameof(QualityOfLife.Harvest)));
        }
        if (matches != 1) throw new InvalidOperationException("Honey yield patch no longer matches this Valheim version.");
    }
}
