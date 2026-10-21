using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.Mounts;

// The authenticated connection identifies the requester. Only the server owns
// bonds and selects destinations; only the animal's current owner moves it.
internal sealed class MountService : IDisposable
{
    private const string RequestRpc = "Ragnarok_MountRequest1", ReplyRpc = "Ragnarok_MountReply1",
        MoveRpc = "Ragnarok_MountMove1", AckRpc = "Ragnarok_MountAck1", GestureRpc = "Ragnarok_MountGesture1",
        CombatRpc = "Ragnarok_MountCombat1", SpotRpc = "Ragnarok_MountSpot3", SpotReplyRpc = "Ragnarok_MountSpotReply3",
        MarkRpc = "Ragnarok_MountMark1", MarkAckRpc = "Ragnarok_MountMarkAck1",
        ReceiptRpc = "Ragnarok_MountReceipt3", ReceiptAckRpc = "Ragnarok_MountReceiptAck3";
    internal const string MarkerKey = "ragnarok.mount.identity";
    private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Records =
        AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
    private static readonly Action<ZNet, bool> SaveWorld = AccessTools.MethodDelegate<Action<ZNet, bool>>(
        AccessTools.Method(typeof(ZNet), "SaveWorld", new[] { typeof(bool) }));
    internal static MountService? Instance;
    internal readonly MountSettings Settings;
    private readonly ManualLogSource log;
    private readonly HashSet<ZRpc> registered = new();
    private readonly HashSet<ZRpc> greeted = new();
    private readonly Dictionary<long, Pending> pending = new();
    private readonly Dictionary<long, double> cooldowns = new(), requests = new();
    private readonly HashSet<string> executed = new();
    private readonly Dictionary<ZDOID, double> combat = new();
    private readonly Dictionary<string, ZDO?> marked = new(StringComparer.Ordinal);
    private readonly Dictionary<long, Binding> binding = new();
    private readonly Dictionary<string, OwnerTask> ownerTasks = new();
    private readonly Dictionary<ZRpc, int> protocols = new();
    private ZNet? net;
    private long world;
    private MountBondStore? store;
    private MountProgressStore? progress;
    private bool storeFailed;
    private bool serverSeen;
    private float helloAt;
    private float pruneAt;
    private float saveMarkersAt;
    internal bool Available { get; private set; }
    internal ZDOID Bound { get; private set; }
    internal string BoundName { get; private set; } = "No mount bound";
    internal string LastMessage { get; private set; } = "";
    internal string BoundCondition { get; private set; } = "";
    internal string SummonEstimate
    {
        get
        {
            Player player = Player.m_localPlayer;
            if (!player) return "";
            float ride = LocalRide(player);
            float cost = MountProgression.Cost(player.GetMaxStamina(), ride, Settings.StaminaPercent.Value);
            return $"Riding {ride:0} · summon {cost:0.#} stamina ({Settings.StaminaPercent.Value * (1 - MountProgression.Factor(ride)):0.#}%) · whistle {MountProgression.Windup(ride, Settings.ExpertWindup.Value):0.##}s";
        }
    }
    internal string CompanionSummary => BoundName + (BoundCondition.Length > 0 ? "\n" + BoundCondition : "") + "\n" + SummonEstimate;
    internal Action<ZDOID>? Whistled;
    internal Action<ZDOID>? ArrivalConfirmed;
    private const string ArrivedMessage = "Your mount has arrived.";

    private sealed class Pending
    {
        internal readonly string Token = Guid.NewGuid().ToString("N");
        internal Actor Player = null!;
        internal ZRpc? Requester, Owner;
        internal ZDO Animal = null!;
        internal Vector3 Destination;
        internal Quaternion Rotation;
        internal double Due, Deadline;
        internal bool Sent, Acknowledged;
        internal bool Winding, AwaitingSpot, CommitReady;
        internal string SpotToken = "";
        internal float Ride, Cost;
    }

    private sealed class Actor
    {
        internal ZDO Data = null!;
        internal Player? Live;
        internal long User => Data.GetLong(ZDOVars.s_playerID);
    }

    private sealed class Binding
    {
        internal readonly string Token = Guid.NewGuid().ToString("N");
        internal ZDO Animal = null!, Player = null!;
        internal string Marker = "";
        internal ZRpc? Requester, Owner;
        internal double Deadline;
        internal bool Acknowledged;
    }

    private sealed class OwnerTask
    {
        internal ZRpc Server = null!;
        internal ZDOID Animal;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal string Marker = "";
        internal float Deadline;
        internal bool Requested;
    }

    internal MountService(MountSettings settings, ManualLogSource log)
    { Settings = settings; this.log = log; Instance = this; }

    internal void Tick()
    {
        ZNet current = ZNet.instance;
        long currentWorld = current && current.GetWorld() != null ? current.GetWorldUID() : 0;
        if (current != net || currentWorld != world)
        {
            Reset(); net = current; world = currentWorld;
        }
        if (!net || world == 0 || !ZNetScene.instance || ZDOMan.instance == null) return;
        if (Time.unscaledTime >= pruneAt)
        {
            pruneAt = Time.unscaledTime + 30;
            double expired = net!.GetTimeSeconds() - Settings.CombatQuiet.Value - 5;
            foreach (ZDOID id in combat.Where(p => p.Value < expired).Select(p => p.Key).ToArray()) combat.Remove(id);
        }
        foreach (ZNetPeer peer in net!.GetPeers())
            if (peer.m_rpc != null && peer.m_uid != 0) Register(peer.m_rpc);
        foreach (ZRpc rpc in registered.ToArray())
            if (Peer(rpc) == null) Unregister(rpc);
        RunOwnerTasks();
        if (net.IsServer())
        {
            if (store == null && !storeFailed)
            {
                try
                {
                    string path = Path.Combine(Utils.GetSaveDataPath(FileHelpers.FileSource.Local),
                        "ragnarok", "mount-bonds", world.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".txt");
                    store = new MountBondStore(path, world);
                    progress = new MountProgressStore(path + ".progress", world);
                    RecoverLegacyBonds(path + ".recover");
                    log.LogInfo("Mount server ready; world-scoped bond store loaded.");
                    IndexBonds();
                    foreach (MountIdentity id in store.Current.Mounts.Take(32))
                    {
                        ZDO? animal = Resolve(id);
                        if (animal != null) LogMountState(animal, "bound record loaded");
                        else if (string.IsNullOrEmpty(id.Marker))
                            log.LogWarning("Legacy mount bond requires recovery/rebinding; cached network IDs cannot identify an animal after save/load.");
                    }
                }
                catch (Exception e) { store = null; progress = null; storeFailed = true; log.LogError("Mount bonds/progression could not load; feature disabled: " + e); }
            }
            Available = store != null;
            if (Player.m_localPlayer)
            {
                UpdateLocalBond(Player.m_localPlayer.GetPlayerID());
                if (Time.unscaledTime >= helloAt)
                { helloAt = Time.unscaledTime + 3; DeliverReceipt(null, Player.m_localPlayer.GetPlayerID()); }
            }
            foreach (var pair in binding.ToArray()) AdvanceBinding(pair.Key, pair.Value);
            foreach (var pair in pending.ToArray()) Advance(pair.Key, pair.Value);
            if (saveMarkersAt > 0 && Time.unscaledTime >= saveMarkersAt)
            {
                saveMarkersAt = 0;
                // The public profile-save helper reads a local player even on
                // dedicated servers. Save the server world directly instead.
                try { SaveWorld(net, false); }
                catch (Exception e) { log.LogError("Mount marker world save failed: " + e); }
            }
        }
        else if (Player.m_localPlayer && Time.unscaledTime >= helloAt)
        {
            helloAt = Time.unscaledTime + 3;
            net.GetServerPeer()?.m_rpc?.Invoke(RequestRpc, Package(0, ZDOID.None));
        }
    }

    private static ZPackage Package(int action, ZDOID mount)
    { var p = new ZPackage(); p.Write(action); p.Write(mount); p.Write(3); return p; }

    private void Register(ZRpc rpc)
    {
        if (!registered.Add(rpc)) return;
        rpc.Register<ZPackage>(ReceiptRpc, (sender, p) => Guard(() =>
        {
            if (!FromServer(sender)) return;
            long receiptWorld = p.ReadLong(), user = p.ReadLong(), sequence = p.ReadLong();
            float cost = p.ReadSingle(), xp = p.ReadSingle();
            if (receiptWorld != world || !ApplyReceipt(user, sequence, cost, xp)) return;
            var ack = new ZPackage(); ack.Write(receiptWorld); ack.Write(sequence); sender.Invoke(ReceiptAckRpc, ack);
        }));
        rpc.Register<ZPackage>(ReceiptAckRpc, (sender, p) => Guard(() =>
        {
            if (!net || !net!.IsServer() || p.ReadLong() != world) return;
            Actor? actor = RequestPlayer(sender);
            long sequence = p.ReadLong();
            if (actor != null) progress?.Ack(actor.User, sequence);
        }));
        rpc.Register<ZDOID>(CombatRpc, (sender, id) =>
        {
            if (!net || !net!.IsServer()) return;
            ZDO subject = ZDOMan.instance.GetZDO(id);
            ZNetPeer? peer = Peer(sender);
            if (subject != null && peer != null && subject.GetOwner() == peer.m_uid)
                combat[id] = net.GetTimeSeconds();
        });
        rpc.Register<ZPackage>(RequestRpc, (sender, p) => Guard(() =>
        {
            int action = p.ReadInt(); ZDOID id = p.ReadZDOID();
            protocols[sender] = p.GetPos() < p.Size() ? p.ReadInt() : 1;
            Request(sender, action, id);
        }));
        rpc.Register<ZPackage>(ReplyRpc, (sender, p) => Guard(() =>
        {
            if (!FromServer(sender)) return;
            serverSeen = true;
            bool wasAvailable = Available;
            Available = p.ReadBool(); Bound = p.ReadZDOID(); BoundName = p.ReadString();
            if (Available && !wasAvailable) log.LogInfo("Mount server handshake received.");
            string message = p.ReadString();
            BoundCondition = p.GetPos() < p.Size() ? p.ReadString() : "";
            if (message.Length > 0) Message(message);
        }));
        rpc.Register<ZPackage>(MoveRpc, (sender, p) => Guard(() =>
        {
            if (!FromServer(sender)) return;
            string token = p.ReadString(); ZDOID id = p.ReadZDOID();
            Vector3 position = p.ReadVector3(); Quaternion rotation = p.ReadQuaternion();
            QueueOwnerTask(token, new OwnerTask { Server = sender, Animal = id, Position = position, Rotation = rotation });
        }));
        rpc.Register<ZPackage>(MarkRpc, (sender, p) => Guard(() =>
        {
            if (!FromServer(sender)) return;
            string token = p.ReadString(); ZDOID id = p.ReadZDOID(); string marker = p.ReadString();
            if (!Guid.TryParseExact(marker, "N", out _)) return;
            QueueOwnerTask(token, new OwnerTask { Server = sender, Animal = id, Marker = marker });
        }));
        rpc.Register<ZPackage>(MarkAckRpc, (sender, p) => Guard(() =>
        {
            if (!net || !net!.IsServer()) return;
            string token = p.ReadString(); bool ok = p.ReadBool(); string reason = p.ReadString();
            foreach (var pair in binding.ToArray())
            {
                Binding tx = pair.Value;
                if (tx.Owner != sender || tx.Token != token) continue;
                if (!ok) { binding.Remove(pair.Key); Reply(tx.Requester, pair.Key, reason); }
                else tx.Acknowledged = true;
                break;
            }
        }));
        rpc.Register<ZPackage>(AckRpc, (sender, p) => Guard(() =>
        {
            if (!net || !net!.IsServer()) return;
            string token = p.ReadString(); bool ok = p.ReadBool(); string reason = p.ReadString();
            foreach (var pair in pending.ToArray())
            {
                Pending tx = pair.Value;
                if (!tx.Sent || tx.Owner != sender || tx.Token != token) continue;
                if (!ok) { cooldowns.Remove(pair.Key); Finish(pair.Key, tx, reason); }
                else tx.Acknowledged = true;
                break;
            }
        }));
        rpc.Register<ZDOID>(GestureRpc, (sender, id) => { if (FromServer(sender)) Whistled?.Invoke(id); });
        rpc.Register<ZPackage>(SpotRpc, (sender, p) => Guard(() =>
        {
            if (!FromServer(sender)) return;
            string token = p.ReadString(); int prefab = p.ReadInt();
            if (token.Length != 32) return;
            Player player = Player.m_localPlayer;
            string reason = "Player is unavailable.";
            Vector3 position = Vector3.zero;
            bool safe = Settings.Enabled.Value && player &&
                MountSafety.PlayerReady(player, Settings.CombatQuiet.Value, out reason) &&
                MountSafety.Arrival(player, prefab, out position, out reason);
            float ride = player ? LocalRide(player) : 0, maximum = player ? player.GetMaxStamina() : 0;
            float stamina = player ? Mathf.Clamp(maximum * player.GetStaminaPercentage(), 0, maximum) : 0;
            float cost = MountProgression.Cost(maximum, ride, Settings.StaminaPercent.Value);
            if (safe && stamina + 0.001f < cost) { safe = false; reason = $"You need {cost:0.#} stamina to summon your mount."; }
            var reply = new ZPackage(); reply.Write(token); reply.Write(safe); reply.Write(position); reply.Write(reason);
            reply.Write(ride); reply.Write(maximum); reply.Write(stamina);
            log.LogInfo("Mount arrival check: " + (safe ? "safe" : reason));
            sender.Invoke(SpotReplyRpc, reply);
        }));
        rpc.Register<ZPackage>(SpotReplyRpc, (sender, p) => Guard(() =>
        {
            if (!net || !net!.IsServer()) return;
            string token = p.ReadString(); bool safe = p.ReadBool(); Vector3 position = p.ReadVector3(); string reason = p.ReadString();
            float ride = p.ReadSingle(), maximum = p.ReadSingle(), stamina = p.ReadSingle();
            foreach (var pair in pending.ToArray())
            {
                Pending tx = pair.Value;
                if (!tx.AwaitingSpot || tx.Requester != sender || tx.SpotToken != token) continue;
                tx.AwaitingSpot = false;
                if (!safe) { Finish(pair.Key, tx, reason); break; }
                if (!MountProgression.Snapshot(ride, maximum, stamina))
                { Finish(pair.Key, tx, "Invalid Riding or stamina snapshot; summon cancelled."); break; }
                tx.Ride = ride; tx.Cost = MountProgression.Cost(maximum, ride, Settings.StaminaPercent.Value);
                if (stamina + 0.001f < tx.Cost)
                { Finish(pair.Key, tx, $"You need {tx.Cost:0.#} stamina to summon your mount."); break; }
                tx.Destination = position;
                if (!Validate(tx, out reason) || !DestinationValid(tx))
                { Finish(pair.Key, tx, reason.Length > 0 ? reason : "Arrival check expired. Whistle again outdoors."); break; }
                if (tx.Winding) tx.CommitReady = true;
                else StartWhistle(pair.Key, tx);
                break;
            }
        }));
    }

    private bool FromServer(ZRpc sender) => net && !net!.IsServer() && net.GetServerPeer()?.m_rpc == sender;
    private ZNetPeer? Peer(ZRpc rpc) => net ? net!.GetPeers().Find(p => p.m_rpc == rpc) : null;
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { log.LogWarning("Rejected mount operation: " + e.Message); }
    }

    internal void Send(int action, ZDOID mount)
    {
        if (!Available || !net)
        {
            Message(serverSeen ? "The server mount service is not ready. Check its mount settings/log." :
                "Waiting for the mount server handshake. Check that Ragnarok is loaded on the server.");
            return;
        }
        if (net!.IsServer()) Guard(() => Request(null, action, mount));
        else net.GetServerPeer()?.m_rpc?.Invoke(RequestRpc, Package(action, mount));
    }

    private Actor? RequestPlayer(ZRpc? sender)
    {
        if (sender == null)
        {
            Player local = Player.m_localPlayer;
            var view = local ? local.GetComponent<ZNetView>() : null;
            return view && view.IsValid() ? new Actor { Data = view.GetZDO(), Live = local } : null;
        }
        ZNetPeer? peer = Peer(sender);
        if (peer == null || peer.m_uid == 0 || peer.m_characterID == ZDOID.None) return null;
        ZDO data = ZDOMan.instance.GetZDO(peer.m_characterID);
        if (data == null || data.GetOwner() != peer.m_uid || data.GetLong(ZDOVars.s_playerID) == 0 ||
            (peer.m_playerID != 0 && peer.m_playerID != data.GetLong(ZDOVars.s_playerID))) return null;
        GameObject prefab = ZNetScene.instance.GetPrefab(data.GetPrefab());
        if (!prefab || !prefab.GetComponent<Player>()) return null;
        GameObject instance = ZNetScene.instance.FindInstance(peer.m_characterID);
        return new Actor { Data = data, Live = instance ? instance.GetComponent<Player>() : null };
    }

    private void Request(ZRpc? sender, int action, ZDOID id)
    {
        if (!net || !net!.IsServer() || (sender != null && Peer(sender) == null)) return;
        Actor? player = RequestPlayer(sender);
        // Advertising the service must not depend on a streamed player prefab.
        // A dedicated server can have the player's persistent ZDO without a live
        // Player, and the initial character ZDO may still be in transit.
        long user = player?.User ?? (sender == null ? 0 : Peer(sender)!.m_playerID);
        if (sender != null && (!protocols.TryGetValue(sender, out int protocol) || protocol != 3))
        {
            Reply(sender, user, action != 0 || greeted.Add(sender) ? "Reload Ragnarok with F6: the server requires mount progression 0.5.4 or newer." : "", false);
            return;
        }
        if (action == 0)
        {
            if (sender != null && greeted.Add(sender))
                log.LogInfo($"Mount handshake answered; player record ready={player != null}, loaded player={player?.Live != null}.");
            Reply(sender, user, ""); DeliverReceipt(sender, user); return;
        }
        if (store == null) { Reply(sender, user, "Server mount bonds could not load. Check the server log."); return; }
        if (player == null || user == 0) { Reply(sender, user, "Your player record is still loading. Try again shortly."); return; }
        if (action == 4)
        {
            if (pending.TryGetValue(user, out Pending cancelled) && !cancelled.Sent)
                Finish(user, cancelled, "Summon cancelled by combat.");
            return;
        }
        double now = net.GetTimeSeconds();
        if (requests.TryGetValue(user, out double recent) && now - recent < 0.75) return;
        requests[user] = now;
        if (progress == null || !progress.HasRoom(user))
        { DeliverReceipt(sender, user); Reply(sender, user, "Mount reward delivery is pending; reload and try again shortly."); return; }
        if (!Settings.Enabled.Value) { Reply(sender, user, "Mount summoning is disabled by the server."); return; }
        if (pending.ContainsKey(user) || binding.ContainsKey(user)) { Reply(sender, user, "A mount action is already in progress."); return; }
        if (player.Data.GetFloat(ZDOVars.s_health) <= 0 || player.Data.GetBool(ZDOVars.s_dead))
        { Reply(sender, user, "Finish your current action first."); return; }
        if (action == 2)
        {
            var next = store.Current.Copy(); next.Release(user); store.Commit(next);
            Reply(sender, user, "Mount bond released."); return;
        }
        if (action == 1)
        {
            ZDO animal = ZDOMan.instance.GetZDO(id);
            string reason = "Approach a tamed, saddled mount first.";
            if (animal == null || Vector3.Distance(player.Data.GetPosition(), animal.GetPosition()) > 6 ||
                !MountSafety.MountReady(animal, 0, out reason) || !MountSafety.WardAccess(animal.GetPosition(), user, includeUnloaded: true))
            { Reply(sender, user, reason); return; }
            var next = store.Current.Copy();
            if (Bond(user) == id)
            { next.Release(user); store.Commit(next); Reply(sender, user, "Mount bond released."); return; }
            BeginBinding(user, player, animal, sender); return;
        }
        if (action != 3) return;
        if (cooldowns.TryGetValue(user, out double until) && now < until)
        { Reply(sender, user, "Your mount needs a moment. Try again shortly."); return; }
        if (!store.Current.TryGet(user, out MountIdentity bound)) { Reply(sender, user, "Bind a saddled, tamed mount first."); return; }
        if (string.IsNullOrEmpty(bound.Marker)) { Reply(sender, user, "Your old mount bond needs recovery or rebinding after this update."); return; }
        ZDO mount = Resolve(bound)!;
        var tx = new Pending { Player = player, Requester = sender, Animal = mount, Deadline = now + 15 };
        if (!Validate(tx, out string failure)) { Reply(sender, user, failure); return; }
        pending.Add(user, tx);
        if (sender != null) AskSpot(tx);
        else if (LocalSnapshot(tx, out failure) && MountSafety.Arrival(player.Live!, mount, out tx.Destination, out failure)) StartWhistle(user, tx);
        else Finish(user, tx, failure);
    }

    private void StartWhistle(long user, Pending tx)
    {
        tx.Winding = true; tx.Due = net!.GetTimeSeconds() + MountProgression.Windup(tx.Ride, Settings.ExpertWindup.Value);
        LogMountState(tx.Animal, "whistle windup");
        ZDOID playerId = tx.Player.Data.m_uid;
        Whistled?.Invoke(playerId);
        foreach (ZNetPeer peer in net.GetPeers()) if (peer.m_uid != 0) peer.m_rpc.Invoke(GestureRpc, playerId);
        Reply(tx.Requester, user, $"Calling your mount… ({tx.Cost:0.#} stamina on arrival)");
    }

    private void AskSpot(Pending tx)
    {
        tx.AwaitingSpot = true; tx.SpotToken = Guid.NewGuid().ToString("N");
        log.LogInfo("Mount summon: requesting " + (tx.Winding ? "final" : "initial") + " arrival check.");
        var p = new ZPackage(); p.Write(tx.SpotToken); p.Write(tx.Animal.GetPrefab());
        tx.Requester!.Invoke(SpotRpc, p);
    }

    private bool DestinationValid(Pending tx)
    {
        Vector3 point = tx.Destination;
        return MountArrivalPolicy.WithinReach(tx.Player.Data.GetPosition().x, tx.Player.Data.GetPosition().y,
            tx.Player.Data.GetPosition().z, point.x, point.y, point.z) &&
            !Character.InInterior(point) && MountSafety.WardAccess(point, tx.Player.User, includeUnloaded: true);
    }

    private bool Validate(Pending tx, out string reason)
    {
        reason = "Your mount is unavailable. Rebind another mount, or release the bond.";
        Actor? current = RequestPlayer(tx.Requester);
        if (!Settings.Enabled.Value || tx.Animal == null || current == null || current.Data != tx.Player.Data ||
            current.User != tx.Player.User || ZDOMan.instance.GetZDO(tx.Animal.m_uid) != tx.Animal) return false;
        long user = tx.Player.User;
        if (current.Data.GetFloat(ZDOVars.s_health) <= 0 || current.Data.GetBool(ZDOVars.s_dead) ||
            !MountSafety.Quiet(current.Data, Settings.CombatQuiet.Value))
        { reason = "You cannot summon a mount during combat or while dead."; return false; }
        if ((current.Live && !MountSafety.PlayerReady(current.Live!, Settings.CombatQuiet.Value, out reason)) ||
            !MountSafety.MountReady(tx.Animal, Settings.CombatQuiet.Value, out reason)) return false;
        double now = net!.GetTimeSeconds();
        if ((combat.TryGetValue(tx.Player.Data.m_uid, out double playerCombat) && now - playerCombat < Settings.CombatQuiet.Value) ||
            (combat.TryGetValue(tx.Animal.m_uid, out double mountCombat) && now - mountCombat < Settings.CombatQuiet.Value))
        { reason = "You or your mount are still in combat."; return false; }
        if (!MountSafety.WardAccess(tx.Animal.GetPosition(), user, includeUnloaded: true))
        { reason = "Your mount is inside a protected area."; return false; }
        tx.Rotation = Quaternion.Euler(0, tx.Player.Data.GetRotation().eulerAngles.y, 0);
        return true;
    }

    private void Advance(long user, Pending tx)
    {
        try
        {
            double now = net!.GetTimeSeconds();
            if (now >= tx.Deadline || (tx.Requester != null && Peer(tx.Requester) == null))
            { Finish(user, tx, tx.Sent ? "Mount transfer was not confirmed. Check nearby before trying again." : "Summon timed out or player disconnected."); return; }
            if (tx.Sent)
            {
                if (tx.Acknowledged && Vector3.Distance(tx.Animal.GetPosition(), tx.Destination) < 1)
                    Finish(user, tx, ArrivedMessage);
                else if (now >= tx.Deadline || (tx.Owner != null && Peer(tx.Owner) == null))
                    Finish(user, tx, "Mount transfer was not confirmed. Check nearby before trying again.");
                return;
            }
            if (tx.AwaitingSpot || !tx.Winding || now < tx.Due) return;
            if (!Validate(tx, out string reason)) { Finish(user, tx, reason); return; }
            if (tx.Requester != null)
            {
                if (!tx.CommitReady) { AskSpot(tx); return; }
                if (!DestinationValid(tx)) { Finish(user, tx, "Arrival check expired. Whistle again outdoors."); return; }
            }
            else if (!LocalSnapshot(tx, out reason) || !MountSafety.Arrival(tx.Player.Live!, tx.Animal, out tx.Destination, out reason))
            { Finish(user, tx, reason); return; }
            // From this point, a missing acknowledgment cannot trigger another move.
            tx.Sent = true; cooldowns[user] = now + Settings.Cooldown.Value;
            long owner = tx.Animal.GetOwner();
            ZNetPeer peer = net.GetPeer(owner);
            if (owner == ZNet.GetUID() || peer == null)
            {
                bool moved = tx.Animal.IsOwner()
                    ? MountTransport.MoveOwned(tx.Animal, tx.Destination, tx.Rotation, out reason)
                    : MountTransport.MoveUnloadedOnServer(tx.Animal, tx.Destination, tx.Rotation, out reason);
                if (!moved) cooldowns.Remove(user);
                Finish(user, tx, moved ? ArrivedMessage : reason);
            }
            else
            {
                if (!protocols.TryGetValue(peer.m_rpc, out int protocol) || protocol != 3)
                { cooldowns.Remove(user); Finish(user, tx, "The mount's current owner must update/reload Ragnarok before summoning."); return; }
                tx.Owner = peer.m_rpc;
                var command = new ZPackage(); command.Write(tx.Token); command.Write(tx.Animal.m_uid);
                command.Write(tx.Destination); command.Write(tx.Rotation);
                peer.m_rpc.Invoke(MoveRpc, command);
            }
        }
        catch (Exception e) { log.LogError("Mount summon failed: " + e); Finish(user, tx, "Mount summon failed safely. Try again shortly."); }
    }

    private void IndexBonds()
    {
        marked.Clear();
        var wanted = new HashSet<string>(store!.Current.Mounts.Where(m => !string.IsNullOrEmpty(m.Marker)).Select(m => m.Marker), StringComparer.Ordinal);
        foreach (ZDO animal in Records(ZDOMan.instance).Values)
        {
            string marker = animal.GetString(MarkerKey);
            if (!wanted.Contains(marker)) continue;
            // Duplicate tags fail closed, never select one animal arbitrarily.
            marked[marker] = marked.ContainsKey(marker) ? null : animal;
        }
    }

    private ZDO? Resolve(MountIdentity id) => !string.IsNullOrEmpty(id.Marker) && marked.TryGetValue(id.Marker, out ZDO? animal) &&
        animal != null && ZDOMan.instance.GetZDO(animal.m_uid) == animal && animal.GetString(MarkerKey) == id.Marker ? animal : null;

    private void BeginBinding(long user, Actor player, ZDO animal, ZRpc? requester)
    {
        if (binding.Values.Any(b => b.Animal == animal)) { Reply(requester, user, "That mount is being bound. Try again shortly."); return; }
        string marker = animal.GetString(MarkerKey);
        bool existing = marker.Length > 0;
        if (!existing) marker = Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(marker, "N", out _)) { Reply(requester, user, "This mount has an invalid identity marker."); return; }
        var next = store!.Current.Copy();
        if (!next.TryBind(user, new MountIdentity(animal.m_uid.UserID, animal.m_uid.ID, marker)))
        { Reply(requester, user, "That mount is already bound to another player."); return; }
        var tx = new Binding { Animal = animal, Player = player.Data, Requester = requester, Marker = marker, Deadline = net!.GetTimeSeconds() + 8 };
        binding.Add(user, tx);
        if (existing) tx.Acknowledged = true;
        else if (animal.IsOwner())
        { animal.Set(MarkerKey, marker); ZDOMan.instance.ForceSendZDO(animal.m_uid); tx.Acknowledged = true; }
        else
        {
            ZNetPeer owner = net.GetPeer(animal.GetOwner());
            if (owner == null || !protocols.TryGetValue(owner.m_rpc, out int protocol) || protocol != 3)
            { binding.Remove(user); Reply(requester, user, "The mount's owner must update/reload Ragnarok before binding."); return; }
            tx.Owner = owner.m_rpc;
            ZDOMan.instance.ForceSendZDO(owner.m_uid, animal.m_uid);
            var command = new ZPackage(); command.Write(tx.Token); command.Write(animal.m_uid); command.Write(marker);
            owner.m_rpc.Invoke(MarkRpc, command);
        }
        AdvanceBinding(user, tx);
    }

    private void AdvanceBinding(long user, Binding tx)
    {
        try
        {
            Actor? actor = RequestPlayer(tx.Requester);
            string reason = "Mount binding cancelled; approach it and try again.";
            if (!Settings.Enabled.Value || actor == null || actor.User != user || actor.Data != tx.Player ||
                ZDOMan.instance.GetZDO(tx.Animal.m_uid) != tx.Animal || net!.GetTimeSeconds() >= tx.Deadline ||
                Vector3.Distance(actor.Data.GetPosition(), tx.Animal.GetPosition()) > 6 ||
                !MountSafety.MountReady(tx.Animal, 0, out reason) ||
                !MountSafety.WardAccess(tx.Animal.GetPosition(), user, includeUnloaded: true))
            { binding.Remove(user); Reply(tx.Requester, user, reason); return; }
            if (!tx.Acknowledged || tx.Animal.GetString(MarkerKey) != tx.Marker) return;
            // Check for a duplicate tag before committing the exclusive bond.
            if (Records(ZDOMan.instance).Values.Count(z => z.GetString(MarkerKey) == tx.Marker) != 1)
            { binding.Remove(user); Reply(tx.Requester, user, "Duplicate mount identity detected; binding cancelled."); return; }
            var next = store!.Current.Copy();
            if (!next.TryBind(user, new MountIdentity(tx.Animal.m_uid.UserID, tx.Animal.m_uid.ID, tx.Marker)))
            { binding.Remove(user); Reply(tx.Requester, user, "That mount is already bound to another player."); return; }
            store.Commit(next); IndexBonds(); binding.Remove(user); saveMarkersAt = Time.unscaledTime + 5;
            CompleteProgress(tx.Requester, user, 0, Settings.BindXp.Value, tx.Marker);
            Reply(tx.Requester, user, "Mount bound. Whistle outdoors to call it.");
        }
        catch (Exception e) { binding.Remove(user); log.LogError("Mount binding failed: " + e); Reply(tx.Requester, user, "Could not save the mount bond; try again shortly."); }
    }

    private void QueueOwnerTask(string token, OwnerTask task)
    {
        if (token.Length != 32 || executed.Contains(token)) return;
        if (executed.Count >= 4096 || ownerTasks.Count >= 16) return;
        executed.Add(token); task.Deadline = Time.unscaledTime + 5; ownerTasks.Add(token, task);
        RunOwnerTasks();
    }

    private void RunOwnerTasks()
    {
        foreach (var pair in ownerTasks.ToArray())
        {
            OwnerTask task = pair.Value;
            if (!FromServer(task.Server)) { ownerTasks.Remove(pair.Key); continue; }
            ZDO animal = ZDOMan.instance.GetZDO(task.Animal);
            if (animal == null && Time.unscaledTime < task.Deadline)
            {
                if (!task.Requested) { task.Requested = true; ZDOMan.instance.RequestZDO(task.Animal); }
                continue;
            }
            string reason = "Mount authority changed or its record is unavailable; try again.";
            bool ok = false;
            try
            {
                if (Settings.Enabled.Value && animal != null && animal.IsOwner() &&
                    MountSafety.MountReady(animal, task.Marker.Length > 0 ? 0 : Settings.CombatQuiet.Value, out reason))
                {
                    if (task.Marker.Length > 0)
                    {
                        string existing = animal.GetString(MarkerKey);
                        ok = existing.Length == 0 || existing == task.Marker;
                        if (ok) { animal.Set(MarkerKey, task.Marker); ZDOMan.instance.ForceSendZDO(animal.m_uid); }
                        else reason = "Mount identity changed; binding cancelled.";
                    }
                    else ok = MountTransport.MoveOwned(animal, task.Position, task.Rotation, out reason);
                }
            }
            catch (Exception e) { reason = "Mount owner operation failed safely."; log.LogWarning(reason + " " + e); }
            ownerTasks.Remove(pair.Key);
            log.LogInfo("Mount owner " + (task.Marker.Length > 0 ? "identity" : "movement") + " result: " + (ok ? "OK" : reason));
            var ack = new ZPackage(); ack.Write(pair.Key); ack.Write(ok); ack.Write(reason);
            task.Server.Invoke(task.Marker.Length > 0 ? MarkAckRpc : AckRpc, ack);
        }
    }

    // A one-shot, administrator-provided recovery file contains player ID<TAB>
    // exact known mount name. Never infer old bonds from a reused network ID.
    private void RecoverLegacyBonds(string path)
    {
        if (!File.Exists(path)) return;
        bool recovered = false;
        foreach (string line in File.ReadAllLines(path).Take(32))
        {
            string[] parts = line.Split('\t');
            if (parts.Length != 2 || !long.TryParse(parts[0], out long user) || parts[1].Length == 0 ||
                !store!.Current.TryGet(user, out MountIdentity old) || !string.IsNullOrEmpty(old.Marker)) continue;
            List<ZDO> candidates = Records(ZDOMan.instance).Values.Where(z =>
                z.GetBool(ZDOVars.s_tamed) && z.GetBool(ZDOVars.s_haveSaddleHash) && Name(z.m_uid) == parts[1] && MountSafety.MountValid(z, out _)).ToList();
            if (candidates.Count != 1)
            {
                log.LogWarning("Legacy mount recovery refused: exact display-name matches=" + candidates.Count);
                foreach (ZDO candidate in Records(ZDOMan.instance).Values.Where(z => z.GetBool(ZDOVars.s_tamed)).Take(16))
                    LogMountState(candidate, "recovery candidate display-name=" + Name(candidate.m_uid));
                continue;
            }
            ZDO animal = candidates[0];
            if (ZNetScene.instance.FindInstance(animal.m_uid) || net!.GetPeer(animal.GetOwner()) != null)
            { log.LogWarning("Legacy mount recovery refused: animal has a live instance or connected owner."); continue; }
            string marker = animal.GetString(MarkerKey);
            if (marker.Length > 0 && Records(ZDOMan.instance).Values.Count(z => z.GetString(MarkerKey) == marker) != 1)
            { log.LogWarning("Legacy mount recovery refused: duplicate identity marker."); continue; }
            if (marker.Length == 0) marker = Guid.NewGuid().ToString("N");
            var next = store.Current.Copy();
            if (!next.TryBind(user, new MountIdentity(animal.m_uid.UserID, animal.m_uid.ID, marker))) continue;
            animal.SetOwner(ZNet.GetUID()); animal.Set(MarkerKey, marker);
            ZDOMan.instance.ForceSendZDO(animal.m_uid);
            store.Commit(next); recovered = true; LogMountState(animal, "legacy exact-name recovery");
        }
        File.Move(path, path + ".processed-" + Guid.NewGuid().ToString("N"));
        if (recovered) saveMarkersAt = Time.unscaledTime + 5;
    }

    private void Finish(long user, Pending tx, string message)
    {
        if (!pending.Remove(user)) return;
        if (message == ArrivedMessage) CompleteProgress(tx.Requester, user, tx.Cost, Settings.SummonXp.Value);
        log.LogInfo("Mount summon result: " + message); Reply(tx.Requester, user, message);
    }

    private static float LocalRide(Player player) => Mathf.Clamp(player.GetSkills().GetSkillLevel(Skills.SkillType.Ride), 0, 100);

    private bool LocalSnapshot(Pending tx, out string reason)
    {
        reason = "Player stamina is unavailable.";
        Player? player = tx.Player.Live;
        if (!player) return false;
        tx.Ride = LocalRide(player!);
        float maximum = player!.GetMaxStamina();
        float stamina = Mathf.Clamp(maximum * player.GetStaminaPercentage(), 0, maximum);
        if (!MountProgression.Snapshot(tx.Ride, maximum, stamina)) return false;
        tx.Cost = MountProgression.Cost(maximum, tx.Ride, Settings.StaminaPercent.Value);
        if (stamina + 0.001f < tx.Cost) { reason = $"You need {tx.Cost:0.#} stamina to summon your mount."; return false; }
        reason = ""; return true;
    }

    private void CompleteProgress(ZRpc? requester, long user, float cost, float xp, string marker = "")
    {
        try
        {
            progress!.Complete(user, cost, xp, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Settings.XpCooldown.Value, marker);
            DeliverReceipt(requester, user);
        }
        catch (Exception e)
        {
            // The animal/bond has already committed; never replay movement to
            // compensate for a disk or receipt failure. Fail future actions closed.
            storeFailed = true; store = null; Available = false;
            log.LogError("Mount progression could not commit; service disabled until reload: " + e);
        }
    }

    private void DeliverReceipt(ZRpc? requester, long user)
    {
        MountProgressStore.Receipt? receipt = progress?.Next(user);
        if (receipt == null) return;
        if (requester == null)
        {
            if (ApplyReceipt(user, receipt.Sequence, receipt.Cost, receipt.Xp)) progress!.Ack(user, receipt.Sequence);
            return;
        }
        var p = new ZPackage(); p.Write(world); p.Write(user); p.Write(receipt.Sequence); p.Write(receipt.Cost); p.Write(receipt.Xp);
        requester.Invoke(ReceiptRpc, p);
    }

    private bool ApplyReceipt(long user, long sequence, float cost, float xp)
    {
        Player player = Player.m_localPlayer;
        if (!player || player.GetPlayerID() != user || sequence <= 0 || !MountProgression.Finite(cost) || cost < 0 || cost > 1000000 ||
            !MountProgression.Finite(xp) || xp < 0 || xp > 5) return false;
        string key = "ragnarok.mount.receipt." + world.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (player.m_customData.TryGetValue(key, out string previous) && long.TryParse(previous, out long applied) && applied >= sequence) return true;
        // Persist a per-world monotonic high-water mark before side effects.
        // The server delivers only the oldest outstanding receipt for this player.
        player.m_customData[key] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (cost > 0)
        {
            // Use native owner-side stamina handling, compensating for the world
            // rate so the displayed percentage remains exact. A zero-rate world
            // retains its native free-stamina policy.
            float rate = Game.m_staminaRate;
            if (rate > 0 && MountProgression.Finite(rate)) player.UseStamina(cost / rate);
        }
        if (xp > 0) player.RaiseSkill(Skills.SkillType.Ride, xp);
        log.LogInfo($"Mount completion applied once: receipt={sequence}, stamina={cost}, Riding XP factor={xp}.");
        return true;
    }

    private string Condition(ZDOID id)
    {
        ZDO animal = id == ZDOID.None ? null! : ZDOMan.instance.GetZDO(id);
        if (animal == null) return "";
        GameObject prefab = ZNetScene.instance.GetPrefab(animal.GetPrefab());
        Character character = prefab ? prefab.GetComponent<Character>() : null!;
        float maximum = animal.GetFloat(ZDOVars.s_maxHealth, character ? character.m_health : 0);
        float health = animal.GetFloat(ZDOVars.s_health, maximum);
        string state = health <= 0 ? "dead" : maximum > 0 ? $"{Mathf.Clamp01(health / maximum) * 100:0}% health" : "health unavailable";
        return state + (animal.GetBool(ZDOVars.s_haveSaddleHash) ? " · saddled" : " · no saddle") +
            (QuietForDetails(animal) ? "" : " · recent combat");
    }
    private bool QuietForDetails(ZDO animal) => MountSafety.Quiet(animal, Settings.CombatQuiet.Value);

    private void Reply(ZRpc? rpc, long user, string message, bool? available = null)
    {
        if (rpc == null) { UpdateLocalBond(user); if (message.Length > 0) Message(message); return; }
        ZDOID id = Bond(user); var p = new ZPackage(); p.Write(available ?? (store != null)); p.Write(id); p.Write(BondName(user)); p.Write(message); p.Write(Condition(id));
        rpc.Invoke(ReplyRpc, p);
    }
    private ZDOID Bond(long user) => store != null && store.Current.TryGet(user, out MountIdentity id) ? Resolve(id)?.m_uid ?? ZDOID.None : ZDOID.None;
    private string BondName(long user) => store != null && store.Current.TryGet(user, out MountIdentity id)
        ? string.IsNullOrEmpty(id.Marker) ? "Old bond needs recovery or rebinding" : Resolve(id) == null ? "Bound mount unavailable" : Name(Bond(user))
        : "No mount bound";
    private void UpdateLocalBond(long user) { Bound = Bond(user); BoundName = BondName(user); BoundCondition = Condition(Bound); }
    private static string Name(ZDOID id)
    {
        if (id == ZDOID.None) return "No mount bound";
        ZDO animal = ZDOMan.instance.GetZDO(id);
        if (animal == null) return "Bound mount unavailable";
        string name = animal.GetString(ZDOVars.s_tamedName);
        var prefab = ZNetScene.instance.GetPrefab(animal.GetPrefab());
        var character = prefab ? prefab.GetComponent<Character>() : null;
        return name.Length > 0 ? name : character ? Localization.instance.Localize(character.m_name) : "Bound mount";
    }
    internal void Message(string message)
    {
        LastMessage = message; log.LogInfo("Mount message: " + message);
        // Play cosmetics only after the server confirms the original animal moved.
        if (message == ArrivedMessage && Bound != ZDOID.None) ArrivalConfirmed?.Invoke(Bound);
        if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.Center, message);
    }

    internal void LogMountState(ZDO animal, string stage)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(animal.GetPrefab());
        var character = prefab ? prefab.GetComponent<Character>() : null;
        var tame = prefab ? prefab.GetComponent<Tameable>() : null;
        log.LogInfo($"Mount {stage}: id={animal.m_uid}, marker={animal.GetString(MarkerKey)}, name={animal.GetString(ZDOVars.s_tamedName)}, " +
            $"prefab={(prefab ? prefab.name : "missing")}, position={animal.GetPosition()}, " +
            $"owner={animal.GetOwner()}, loaded={(bool)ZNetScene.instance.FindInstance(animal.m_uid)}, " +
            $"tamed={animal.GetBool(ZDOVars.s_tamed)}, saddle={animal.GetBool(ZDOVars.s_haveSaddleHash)}, " +
            $"health={animal.GetFloat(ZDOVars.s_health, float.NaN)}, max={animal.GetFloat(ZDOVars.s_maxHealth, float.NaN)}, " +
            $"base={(character ? character.m_health : 0)}, saddleComponent={(tame && tame.m_saddle)}.");
    }
    internal static MountIdentity Identity(ZDOID id) => new(id.UserID, id.ID);
    internal static ZDOID NetworkId(MountIdentity id) => new(id.User, id.Object);

    internal void MarkCombat(Character character)
    {
        if (!net || world == 0 || !character) return;
        if (!(character is Player))
        {
            var tame = character.GetComponent<Tameable>();
            if (!tame || !tame.IsTamed() || !MountTransport.HasSaddle(tame)) return;
        }
        var view = character.GetComponent<ZNetView>();
        if (!view || !view.IsValid() || !view.IsOwner()) return;
        ZDO subject = view.GetZDO(); double now = net!.GetTimeSeconds();
        if (combat.TryGetValue(subject.m_uid, out double recent) && now - recent < 0.5) return;
        combat[subject.m_uid] = now;
        subject.Set(MountSafety.CombatKey, (long)(now * 1000));
        ZDOMan.instance.ForceSendZDO(subject.m_uid);
        if (net.IsServer())
        {
            if (character == Player.m_localPlayer && pending.TryGetValue(Player.m_localPlayer.GetPlayerID(), out Pending tx) && !tx.Sent)
                Finish(Player.m_localPlayer.GetPlayerID(), tx, "Summon cancelled by combat.");
        }
        else
        {
            net.GetServerPeer()?.m_rpc?.Invoke(CombatRpc, subject.m_uid);
            if (character == Player.m_localPlayer) net.GetServerPeer()?.m_rpc?.Invoke(RequestRpc, Package(4, ZDOID.None));
        }
    }

    private void Unregister(ZRpc rpc)
    {
        rpc.Unregister(RequestRpc); rpc.Unregister(ReplyRpc); rpc.Unregister(MoveRpc);
        rpc.Unregister(AckRpc); rpc.Unregister(GestureRpc); rpc.Unregister(CombatRpc);
        rpc.Unregister(SpotRpc); rpc.Unregister(SpotReplyRpc); rpc.Unregister(MarkRpc); rpc.Unregister(MarkAckRpc);
        rpc.Unregister(ReceiptRpc); rpc.Unregister(ReceiptAckRpc);
        registered.Remove(rpc); greeted.Remove(rpc);
        protocols.Remove(rpc);
    }
    private void Reset()
    {
        foreach (ZRpc rpc in registered.ToArray()) Unregister(rpc);
        pending.Clear(); cooldowns.Clear(); requests.Clear(); executed.Clear(); combat.Clear(); store = null; progress = null; storeFailed = false;
        marked.Clear(); binding.Clear(); ownerTasks.Clear();
        protocols.Clear(); saveMarkersAt = 0;
        Available = false; serverSeen = false; Bound = ZDOID.None; BoundName = "No mount bound"; BoundCondition = ""; helloAt = 0;
    }
    public void Dispose() { Reset(); Whistled = null; ArrivalConfirmed = null; if (Instance == this) Instance = null; }
}
