using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.Mounts;

internal static class MountSafety
{
    internal const string CombatKey = "ragnarok.mount.combat";
    private static readonly Func<PrivateArea, bool> WardEnabled = AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "IsEnabled"));
    private static readonly Func<PrivateArea, Vector3, float, bool> WardInside = AccessTools.MethodDelegate<Func<PrivateArea, Vector3, float, bool>>(AccessTools.Method(typeof(PrivateArea), "IsInside"));
    private static readonly Func<PrivateArea, long, bool> WardPermitted = AccessTools.MethodDelegate<Func<PrivateArea, long, bool>>(AccessTools.Method(typeof(PrivateArea), "IsPermitted"));

    internal static bool WardAccess(Vector3 position, long player, bool includeUnloaded = false)
    {
        bool inside = false;
        foreach (PrivateArea ward in (List<PrivateArea>)AccessTools.Field(typeof(PrivateArea), "m_allAreas").GetValue(null))
        {
            if (!ward || !WardEnabled(ward) || !WardInside(ward, position, 0)) continue;
            inside = true;
            var piece = ward.GetComponent<Piece>();
            if ((piece && piece.GetCreator() == player) || WardPermitted(ward, player)) return true;
        }
        if (!includeUnloaded || !ZNet.instance.IsServer()) return !inside;
        // A distant mount's ward need not have a Unity object. Query persistent
        // records in nearby sectors and use the native enabled/creator/permit data.
        var definitions = new Dictionary<int, float>();
        float radius = 0;
        foreach (GameObject prefab in ((Dictionary<int, GameObject>)AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs")
                     .GetValue(ZNetScene.instance)).Values)
        {
            var ward = prefab ? prefab.GetComponent<PrivateArea>() : null;
            if (!ward) continue;
            definitions[prefab!.name.GetStableHashCode()] = ward.m_radius;
            radius = Mathf.Max(radius, ward.m_radius);
        }
        if (radius > 4096 || float.IsNaN(radius)) return false;
        var nearby = new List<ZDO>();
        var distant = new List<ZDO>();
        int sectors = Mathf.CeilToInt(radius / 64) + 1;
        ZDOMan.instance.FindSectorObjects(ZoneSystem.GetZone(position), new SimulationDistance(sectors, sectors, true), nearby, distant);
        foreach (ZDO record in nearby)
        {
            if (!definitions.TryGetValue(record.GetPrefab(), out float reach) || !record.GetBool(ZDOVars.s_enabled) ||
                Utils.DistanceXZ(position, record.GetPosition()) >= reach) continue;
            inside = true;
            if (record.GetLong(ZDOVars.s_creator) == player) return true;
            int count = Math.Min(4096, record.GetInt(ZDOVars.s_permitted));
            for (int i = 0; i < count; i++)
                if (record.GetLong("pu_id" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)) == player) return true;
        }
        return !inside;
    }

    internal static bool MountValid(ZDO animal, out string reason)
    {
        reason = "";
        GameObject prefab = ZNetScene.instance.GetPrefab(animal.GetPrefab());
        Character character = prefab ? prefab.GetComponent<Character>() : null!;
        Tameable tame = prefab ? prefab.GetComponent<Tameable>() : null!;
        // Match Character.GetHealth/GetMaxHealth: an omitted health field means
        // the maximum, not death. Reading it never heals or writes the record.
        float maxHealth = animal.GetFloat(ZDOVars.s_maxHealth, character ? character.m_health : 0);
        float health = animal.GetFloat(ZDOVars.s_health, maxHealth);
        bool rideable = character && tame && (tame.m_saddle || prefab!.GetComponentInChildren<Sadle>(true));
        bool valid = MountRecordPolicy.Validate(rideable, animal.GetBool(ZDOVars.s_tamed),
            animal.GetBool(ZDOVars.s_haveSaddleHash), health, out reason);
        if (!valid) MountService.Instance?.LogMountState(animal, "validation rejected: " + reason);
        return valid;
    }

    internal static bool Quiet(ZDO subject, float seconds) =>
        ZNet.instance.GetTimeSeconds() - subject.GetLong(CombatKey, -1000000000) / 1000d >= seconds;

    internal static void Temperament(ZDO animal, out bool hungry, out bool frightened)
    {
        // Mirror native Tameable.IsHungry using saved feeding time so distance
        // does not remove hunger. Read prefab duration, never mutate the animal.
        GameObject prefab = ZNetScene.instance.GetPrefab(animal.GetPrefab());
        var tame = prefab ? prefab.GetComponent<Tameable>() : null;
        long fed = animal.GetLong(ZDOVars.s_tameLastFeeding, 0);
        hungry = tame && (ZNet.instance.GetTime().Ticks - (double)fed) / TimeSpan.TicksPerSecond > tame.m_fedDuration;
        GameObject loaded = ZNetScene.instance.FindInstance(animal.m_uid);
        var ai = loaded ? loaded.GetComponent<BaseAI>() : null;
        // A saved alert bit may be stale indefinitely in an unloaded sector.
        frightened = ai && ai.IsAlerted();
    }

    internal static bool PlayerReady(Player player, float quiet, out string reason)
    {
        reason = "";
        if (!player || player.IsDead() || player.IsTeleporting() || player.InIntro() ||
            player.IsAttached() || player.IsRiding() || player.InAttack() || player.InDodge())
        { reason = "Dismount and finish your current action before whistling."; return false; }
        var view = player.GetComponent<ZNetView>();
        if (!view || !view.IsValid() || InCombat(player, quiet, out _, out _))
        { reason = "You cannot summon a mount during combat."; return false; }
        return true;
    }

    // Shared by summon validation and the presentation-only combat indicator.
    internal static bool InCombat(Player player, float quiet, out float remaining, out bool engaged)
    {
        remaining = 0; engaged = false;
        if (!player || !ZNet.instance) return false;
        var view = player.GetComponent<ZNetView>();
        if (!view || !view.IsValid()) return false;
        remaining = Mathf.Max(0, quiet - (float)(ZNet.instance.GetTimeSeconds() - view.GetZDO().GetLong(CombatKey, -1000000000) / 1000d));
        // Successful attacks already stamp CombatKey. The native attack timer
        // starts at zero on spawn, and IsTargeted also includes passive sensing.
        engaged = player.InAttack();
        foreach (BaseAI ai in BaseAI.GetAllInstances())
        {
            if (!ai || Vector3.Distance(ai.transform.position, player.transform.position) > 35) continue;
            var enemy = ai.GetComponent<Character>();
            if (enemy && !enemy.IsDead() && BaseAI.IsEnemy(enemy, player) &&
                ai.GetTargetCreature() == player && ai.IsAlerted())
            { engaged = true; break; }
        }
        return engaged || remaining > 0;
    }

    internal static bool MountReady(ZDO animal, float quiet, out string reason)
    {
        if (!MountValid(animal, out reason)) return false;
        if (animal.GetLong(ZDOVars.s_user, 0) != 0) { reason = "Your mount is being ridden."; return false; }
        // The persisted alert bit also represents fear and can remain set while
        // an animal's sector is unloaded. It is not proof of current combat.
        // Recent attacks/damage remain authoritative through CombatKey, and a
        // connected owner repeats this check against its live AI before moving.
        if (!Quiet(animal, quiet)) { reason = "Your mount is still in combat."; return false; }
        GameObject loaded = ZNetScene.instance.FindInstance(animal.m_uid);
        if (!loaded) return true;
        var tame = loaded.GetComponent<Tameable>();
        var ai = loaded.GetComponent<BaseAI>();
        Character target = ai ? ai.GetTargetCreature() : null!;
        if (tame.HaveRider() || (ai && ai.IsAlerted() && target && !target.IsDead() &&
            BaseAI.IsEnemy(loaded.GetComponent<Character>(), target)))
        { reason = "Your mount is fighting or being ridden."; return false; }
        return true;
    }

    internal static bool Arrival(Player player, ZDO animal, out Vector3 destination, out string reason)
        => Arrival(player, animal.GetPrefab(), out destination, out reason);

    internal static bool Arrival(Player player, int prefabHash, out Vector3 destination, out string reason)
    {
        destination = Vector3.zero;
        reason = "No safe outdoor arrival spot nearby.";
        if (player.InInterior() || player.InShelter()) { reason = "Whistle outdoors, away from roofs and dungeons."; return false; }
        var prefab = ZNetScene.instance.GetPrefab(prefabHash);
        if (!prefab) { reason = "Mount prefab is unavailable on this client."; return false; }
        var capsule = prefab.GetComponent<CapsuleCollider>();
        float scale = Mathf.Max(prefab.transform.localScale.x, prefab.transform.localScale.y, prefab.transform.localScale.z);
        float radius = capsule ? Mathf.Max(0.5f, capsule.radius * scale) : 1.5f;
        float height = capsule ? Mathf.Max(radius * 2, capsule.height * scale) : 3;
        int solid = LayerMask.GetMask("Default", "static_solid", "piece", "piece_nonsolid", "terrain", "vehicle", "character", "character_net");
        int roof = LayerMask.GetMask("piece", "piece_nonsolid", "terrain", "static_solid");
        if (Physics.Raycast(player.transform.position + Vector3.up, Vector3.up, 64, roof, QueryTriggerInteraction.Ignore))
        { reason = "Whistle outdoors, away from roofs and dungeons."; return false; }
        for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                Vector3 point = player.transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (3 + radius + ring * 2);
                if (!ZoneSystem.instance.GetGroundHeight(point, out float ground)) continue;
                point.y = ground + 0.2f;
                if (!Physics.Raycast(point + Vector3.up * 2, Vector3.down, out RaycastHit slope, 4,
                        LayerMask.GetMask("terrain"), QueryTriggerInteraction.Ignore) || slope.normal.y < 0.8f) continue;
                if (ground < ZoneSystem.instance.m_waterLevel + 0.2f || Character.InInterior(point) ||
                    !WardAccess(point, player.GetPlayerID()) ||
                    Physics.Raycast(point + Vector3.up * 0.2f, Vector3.up, 64, roof, QueryTriggerInteraction.Ignore)) continue;
                Vector3 bottom = point + Vector3.up * (radius + 0.1f);
                Vector3 top = point + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);
                if (Physics.CheckCapsule(bottom, top, radius, solid, QueryTriggerInteraction.Ignore)) continue;
                destination = point;
                reason = "";
                return true;
            }
        return false;
    }
}
