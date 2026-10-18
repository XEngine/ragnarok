using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;

namespace Ragnarok.Features.Gameplay;

internal sealed class SailingAndTurrets
{
    internal static SailingAndTurrets? Current;
    internal readonly List<ConfigEntryBase> Synced = new();
    private readonly List<ConfigEntry<float>> factors = new();
    internal readonly ConfigEntry<bool> IgnorePlayers, InfiniteAmmo;

    // Stable indices used by the field-read patches below.
    internal const int Forward = 0, Reverse = 1, Rudder = 2, Steering = 3, Impact = 4,
        FireRate = 5, TurnRate = 6, Range = 7, Velocity = 8, Spread = 9;

    internal SailingAndTurrets(ConfigFile config)
    {
        void Factor(string section, string name, string description, float min, float max)
        {
            var entry = config.Bind(section, name, 1f, new ConfigDescription(description + " 1 = original value.",
                new AcceptableValueRange<float>(min, max)));
            factors.Add(entry);
            Synced.Add(entry);
        }
        Factor("Gameplay.Sailing", "ForwardPropulsionMultiplier", "Forward sailing and rowing propulsion. 2 doubles force, not guaranteed boat speed; wind and water drag still apply.", 0.1f, 5f);
        Factor("Gameplay.Sailing", "ReversePropulsionMultiplier", "Reverse rowing propulsion.", 0.1f, 5f);
        Factor("Gameplay.Sailing", "RudderSpeedMultiplier", "How quickly steering input moves the rudder.", 0.1f, 5f);
        Factor("Gameplay.Sailing", "SteeringStrengthMultiplier", "How strongly the hull responds to the rudder, including steering while moving.", 0.1f, 5f);
        Factor("Gameplay.Sailing", "WaterImpactDamageMultiplier", "Damage from wave/water impacts. 0 disables this damage; 0.5 halves it. Does not change collisions, attacks, capsizing or biome hazards.", 0f, 10f);
        Factor("Gameplay.Ballista", "FireRateMultiplier", "Firing rate: 2 halves the shot cooldown. The turret must still acquire and aim at a target.", 0.1f, 10f);
        Factor("Gameplay.Ballista", "TurnSpeedMultiplier", "Turret rotation speed.", 0.1f, 10f);
        Factor("Gameplay.Ballista", "TargetRangeMultiplier", "Target detection distance. Visibility and aiming-angle restrictions still apply.", 0.1f, 5f);
        Factor("Gameplay.Ballista", "ProjectileSpeedMultiplier", "Bolt velocity, also used in target lead prediction. Projectile lifetime is unchanged.", 0.1f, 5f);
        Factor("Gameplay.Ballista", "ProjectileSpreadMultiplier", "Random aim spread: 0.5 halves spread, 0 removes random spread. Lower is more accurate; moving targets can still evade shots.", 0f, 5f);
        IgnorePlayers = config.Bind("Gameplay.Ballista", "IgnorePlayers", false,
            "Do not select or fire at players. Already-fired bolts and players standing in the path of other targets can still be hit.");
        InfiniteAmmo = config.Bind("Gameplay.Ballista", "InfiniteAmmo", false,
            "Do not consume ammunition when firing. Load at least one bolt to select the ammo type. Stored ammo is preserved when toggling this setting.");
        Synced.Add(IgnorePlayers);
        Synced.Add(InfiniteAmmo);
    }

    internal static float Scale(float value, int index)
    {
        float factor = Current?.factors[index].Value ?? 1f;
        return index == FireRate ? value / factor : value * factor;
    }

    internal static float Rowing(float value, Ship ship) =>
        Scale(value, ship.GetSpeedSetting() == Ship.Speed.Back ? Reverse : Forward);
    internal static bool TargetPlayers(bool original) => original && !(Current?.IgnorePlayers.Value ?? false);
    internal static int ShotAmmoLimit(int original) => Current?.InfiniteAmmo.Value == true ? 0 : original;
}

// Read-time overrides do not mutate shared prefabs or instance defaults. Settings take effect
// immediately on all clients, including the physics/AI owner; unpatching restores vanilla behavior.
[HarmonyPatch]
internal static class SailingValuesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "ApplyControlls", "CustomFixedUpdate", "GetSailForce", "UpdateWaterForce" })
            yield return AccessTools.Method(typeof(Ship), name);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode != OpCodes.Ldfld || !(instruction.operand is FieldInfo field) || field.DeclaringType != typeof(Ship)) continue;
            if (field.Name == "m_backwardForce")
            {
                matches++;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SailingAndTurrets), nameof(SailingAndTurrets.Rowing)));
                continue;
            }
            int index = field.Name switch
            {
                "m_sailForceFactor" => SailingAndTurrets.Forward,
                "m_rudderSpeed" => SailingAndTurrets.Rudder,
                "m_stearForce" => SailingAndTurrets.Steering,
                "m_stearVelForceFactor" => SailingAndTurrets.Steering,
                "m_waterImpactDamage" => SailingAndTurrets.Impact,
                _ => -1
            };
            if (index < 0) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Ldc_I4, index);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SailingAndTurrets), nameof(SailingAndTurrets.Scale)));
        }
        int expected = __originalMethod.Name == "CustomFixedUpdate" ? 4 : 1;
        if (matches != expected) throw new InvalidOperationException("Sailing patch no longer matches " + __originalMethod.Name);
    }
}

[HarmonyPatch]
internal static class BallistaValuesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "UpdateTurretRotation", "UpdateTarget", "ShootProjectile", "IsCoolingDown" })
            yield return AccessTools.Method(typeof(Turret), name);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode != OpCodes.Ldfld || !(instruction.operand is FieldInfo field)) continue;
            string? helper = null;
            int index = -1;
            if (field.DeclaringType == typeof(Turret))
            {
                index = field.Name switch
                {
                    "m_attackCooldown" => SailingAndTurrets.FireRate,
                    "m_turnRate" => SailingAndTurrets.TurnRate,
                    "m_viewDistance" => SailingAndTurrets.Range,
                    _ => -1
                };
                if (field.Name == "m_targetPlayers") helper = nameof(SailingAndTurrets.TargetPlayers);
                if (field.Name == "m_maxAmmo" && __originalMethod.Name == "ShootProjectile") helper = nameof(SailingAndTurrets.ShotAmmoLimit);
            }
            else if (field.DeclaringType == typeof(Attack))
            {
                if (field.Name == "m_projectileVel") index = SailingAndTurrets.Velocity;
                if (field.Name == "m_projectileAccuracy") index = SailingAndTurrets.Spread;
            }
            if (index >= 0)
            {
                yield return new CodeInstruction(OpCodes.Ldc_I4, index);
                helper = nameof(SailingAndTurrets.Scale);
            }
            if (helper == null) continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SailingAndTurrets), helper));
        }
        int expected = __originalMethod.Name == "ShootProjectile" ? 5 : (__originalMethod.Name == "IsCoolingDown" ? 1 : 2);
        if (matches != expected) throw new InvalidOperationException("Ballista patch no longer matches " + __originalMethod.Name + ": " + matches);
    }
}

[HarmonyPatch(typeof(Turret), nameof(Turret.ShootProjectile))]
internal static class BallistaPlayerSafetyPatch
{
    private static bool Prefix(Character ___m_target) =>
        !(SailingAndTurrets.Current?.IgnorePlayers.Value == true && ___m_target is Player);
}

// Keep the placement/range display consistent without changing the original range field.
[HarmonyPatch(typeof(Turret), "UpdateMarker")]
internal static class BallistaRangeMarkerPatch
{
    private static void Prefix(Turret __instance)
    {
        if (__instance.m_marker)
            __instance.m_marker.m_radius = SailingAndTurrets.Scale(__instance.m_viewDistance, SailingAndTurrets.Range);
    }
}
