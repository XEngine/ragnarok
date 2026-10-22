using System;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.Mounts;

// Movement primitive only: callers authenticate requests and validate combat/arrival.
// Never create a replacement ZDO or destroy the original animal.
internal static class MountTransport
{
    internal static readonly Func<Tameable, bool> HasSaddle =
        AccessTools.MethodDelegate<Func<Tameable, bool>>(AccessTools.Method(typeof(Tameable), "HaveSaddle"));

    internal static bool MoveUnloadedOnServer(ZDO animal, Vector3 destination, Quaternion rotation, out string reason)
    {
        reason = "";
        if (!ZNet.instance || !ZNet.instance.IsServer() || ZNetScene.instance.FindInstance(animal.m_uid))
        { reason = "Unloaded transport requires the server and no live instance."; return false; }
        long owner = animal.GetOwner();
        if (owner != 0 && owner != ZNet.GetUID() && ZNet.instance.GetPeer(owner) != null)
        { reason = "The connected mount owner must approve movement."; return false; }
        // Only the server may take authority over unowned/offline-owned persistent
        // animals. A connected owner's live object is never claimed unilaterally.
        animal.SetOwner(ZNet.GetUID());
        return MoveOwned(animal, destination, rotation, out reason);
    }
    internal static bool MoveOwned(ZDO animal, Vector3 destination, Quaternion rotation, out string reason)
    {
        reason = "";
        if (!animal.IsOwner()) { reason = "Mount authority changed; try again."; return false; }
        GameObject instance = ZNetScene.instance.FindInstance(animal.m_uid);
        if (instance)
        {
            Character character = instance.GetComponent<Character>();
            Tameable tameable = instance.GetComponent<Tameable>();
            if (!character || character.IsDead() || !tameable || !tameable.IsTamed() ||
                !HasSaddle(tameable) || tameable.HaveRider())
            { reason = "The mount is dead, unsaddled or being ridden."; return false; }
            BaseAI ai = instance.GetComponent<BaseAI>();
            if (ai) ai.StopMoving();
            Rigidbody body = instance.GetComponent<Rigidbody>();
            instance.transform.SetPositionAndRotation(destination, rotation);
            if (ai)
            {
                ai.SetPatrolPoint();
                if (ai is MonsterAI monster) monster.SetFollowTarget(null);
            }
            if (body)
            {
                body.position = destination;
                body.rotation = rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            var sync = instance.GetComponent<ZSyncTransform>();
            if (sync) sync.SyncNow();
        }
        else if (!MountSafety.MountValid(animal, out reason)) return false;
        animal.SetPosition(destination);
        animal.SetRotation(rotation);
        animal.Set(ZDOVars.s_bodyVelocity, Vector3.zero);
        // Settle at arrival instead of walking back to an old patrol/follow target.
        animal.Set(ZDOVars.s_patrol, true);
        animal.Set(ZDOVars.s_patrolPoint, destination);
        ZDOMan.instance.ForceSendZDO(animal.m_uid);
        return true;
    }
}
