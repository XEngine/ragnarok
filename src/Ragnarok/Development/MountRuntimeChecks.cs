#if DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using Ragnarok.Features.Mounts;
using UnityEngine;

namespace Ragnarok.Development;

internal static class MountRuntimeChecks
{
    internal static IEnumerator Run(string directory)
    {
        Directory.CreateDirectory(directory);
        yield return WorldPreview.Enter();
        Player player = Player.m_localPlayer;
        ZDO? record = null;
        bool passed = false;
        try
        {
            Vector3 origin;
            float ground;
            float terrainDeadline = Time.unscaledTime + 100;
            while (true)
            {
                origin = player.transform.position + player.transform.right * 8;
                if (!player.InIntro() && ZoneSystem.instance.GetGroundHeight(origin, out ground)) break;
                Assert(Time.unscaledTime < terrainDeadline, "Test player/ground did not finish loading");
                yield return null;
            }
            origin.y = ground + 1;
            GameObject prefab = ZNetScene.instance.GetPrefab("Lox");
            Assert(prefab, "Lox prefab unavailable");
            var animal = UnityEngine.Object.Instantiate(prefab!, origin, Quaternion.identity);
            yield return new WaitForSecondsRealtime(1);
            var character = animal.GetComponent<Character>();
            var tameable = animal.GetComponent<Tameable>();
            record = animal.GetComponent<ZNetView>().GetZDO();
            character.SetTamed(true);
            record.Set(ZDOVars.s_haveSaddleHash, true);
            AccessTools.Method(typeof(Tameable), "SetSaddle").Invoke(tameable, new object[] { true });
            character.SetHealth(237);
            record.Set(ZDOVars.s_tamedName, "Ragnarok transport check");
            ZDOID identity = record.m_uid;
            Vector3 nearby = origin + Vector3.forward * 5;
            Assert(MountTransport.MoveOwned(record, nearby, Quaternion.identity, out string error), error);
            Assert(record.m_uid == identity && Vector3.Distance(character.transform.position, nearby) < 0.1f,
                "Loaded move changed identity or did not move the animal");
            Debug.Log("[Ragnarok] Mount check PASS: loaded owner movement preserves identity.");

            // Let the actual scene streamer unload this same network object.
            Vector3 distant = new(2500, 150, 2500);
            Assert(MountTransport.MoveOwned(record, distant, Quaternion.identity, out error), error);
            var body = animal.GetComponent<Rigidbody>();
            if (body) body.isKinematic = true;
            float deadline = Time.unscaledTime + 12;
            while (ZNetScene.instance.FindInstance(identity) && Time.unscaledTime < deadline) yield return null;
            Assert(!ZNetScene.instance.FindInstance(identity), "Distant mount did not unload");
            Assert(ReferenceEquals(ZDOMan.instance.GetZDO(identity), record), "Unload replaced or destroyed the persistent mount");
            Assert(MountTransport.MoveUnloadedOnServer(record, origin, Quaternion.identity, out error), error);
            deadline = Time.unscaledTime + 15;
            while (!ZNetScene.instance.FindInstance(identity) && Time.unscaledTime < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            GameObject returned = ZNetScene.instance.FindInstance(identity);
            Assert(returned, "Persistent mount did not instantiate at arrival");
            var returnedCharacter = returned!.GetComponent<Character>();
            Assert(returnedCharacter.GetZDOID() == identity, "Unloaded summon changed identity");
            Assert(MountTransport.HasSaddle(returned.GetComponent<Tameable>()) && returnedCharacter.IsTamed(), "Saddle/tame state was lost");
            Assert(Math.Abs(returnedCharacter.GetHealth() - 237) < 0.1f, "Health was changed by summoning");
            Assert(record.GetString(ZDOVars.s_tamedName) == "Ragnarok transport check", "Name was lost");
            Assert(Character.GetAllCharacters().Count(c => c && c.GetZDOID() == identity) == 1, "Animal was duplicated");
            Debug.Log("[Ragnarok] Mount check PASS: unloaded sector return preserves ZDO, saddle, name, health and exactly one animal.");
            MountService service = MountService.Instance!;
            Assert(service != null && service.Available, "Mount service did not initialize");
            Vector3 bindingSpot = player.transform.position + player.transform.right * 4;
            Assert(ZoneSystem.instance.GetGroundHeight(bindingSpot, out ground), "Binding ground unavailable");
            bindingSpot.y = ground + 0.2f;
            Assert(MountTransport.MoveOwned(record, bindingSpot, Quaternion.identity, out error), error);
            service!.Send(1, identity);
            Assert(service.Bound == identity, "Binding failed: " + service.LastMessage);
            yield return new WaitForSecondsRealtime(0.85f);
            Assert(MountTransport.MoveOwned(record, distant, Quaternion.identity, out error), error);
            body = returned.GetComponent<Rigidbody>();
            if (body) body.isKinematic = true;
            deadline = Time.unscaledTime + 12;
            while (ZNetScene.instance.FindInstance(identity) && Time.unscaledTime < deadline) yield return null;
            Assert(!ZNetScene.instance.FindInstance(identity), "Service test mount did not unload");
            service.Send(3, ZDOID.None);
            Assert(service.LastMessage.StartsWith("Calling your mount…", StringComparison.Ordinal), "Whistle rejected: " + service.LastMessage);
            yield return new WaitForSecondsRealtime(2);
            Assert(service.LastMessage == "Your mount has arrived.", "Service summon failed: " + service.LastMessage);
            Assert(Vector3.Distance(record.GetPosition(), player.transform.position) < 12, "Service destination too far away");
            service.Send(3, ZDOID.None);
            Assert(service.LastMessage.Contains("moment"), "Success cooldown was not enforced");
            yield return new WaitForSecondsRealtime(service.Settings.Cooldown.Value + 1);
            service.Send(3, ZDOID.None);
            Assert(service.LastMessage.StartsWith("Calling your mount…", StringComparison.Ordinal), "Second whistle rejected: " + service.LastMessage);
            Vector3 beforeCombat = record.GetPosition();
            service.MarkCombat(player);
            Assert(service.LastMessage.Contains("cancelled"), "Combat did not cancel windup");
            yield return new WaitForSecondsRealtime(2);
            Assert(Vector3.Distance(beforeCombat, record.GetPosition()) < 2, "Cancelled summon moved mount");
            service.Send(3, ZDOID.None);
            Assert(service.LastMessage.Contains("combat"), "Combat quiet period was not enforced");
            yield return new WaitForSecondsRealtime(0.85f);
            service.Send(2, ZDOID.None);
            Assert(service.Bound == ZDOID.None, "Release failed: " + service.LastMessage);
            Assert(record.GetBool(ZDOVars.s_haveSaddleHash), "Release removed saddle");
            Debug.Log("[Ragnarok] Mount check PASS: service bind, unloaded summon, cooldown, combat cancellation/rejection and release.");
            passed = true;
        }
        finally
        {
            if (record != null && ZDOMan.instance != null)
            {
                GameObject loaded = ZNetScene.instance ? ZNetScene.instance.FindInstance(record.m_uid) : null!;
                if (loaded) ZNetScene.instance!.Destroy(loaded);
                else if (record.IsOwner()) ZDOMan.instance.DestroyZDO(record);
            }
            File.WriteAllText(Path.Combine(directory, "result.txt"), passed ? "PASS" : "FAIL; inspect game log");
            Application.Quit();
        }
    }

    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException("Mount runtime check: " + message); }
}
#endif
