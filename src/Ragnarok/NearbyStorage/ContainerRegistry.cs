using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.NearbyStorage;

internal sealed class ContainerRegistry : IDisposable
{
    private const string RequestRpc = "Ragnarok_RequestNearbyChest_v1";
    private static readonly AccessTools.FieldRef<Container, ZNetView> View =
        AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
    private static readonly MethodInfo LoadMethod = AccessTools.Method(typeof(Container), "Load");
    private static readonly MethodInfo AccessMethod = AccessTools.Method(typeof(Container), "CheckAccess");
    private static readonly FieldInfo AllWards = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    private static readonly MethodInfo WardEnabled = AccessTools.Method(typeof(PrivateArea), "IsEnabled");
    private static readonly MethodInfo WardInside = AccessTools.Method(typeof(PrivateArea), "IsInside");
    private static readonly MethodInfo WardPermitted = AccessTools.Method(typeof(PrivateArea), "IsPermitted");

    private readonly StorageSettings settings;
    private readonly Dictionary<Container, ZNetView> containers = new();
    private readonly Dictionary<Container, float> requests = new();
    private readonly Dictionary<Container, int> loadedFrames = new();
    private List<Container> nearby = new();
    private Player? nearbyPlayer;
    private int nearbyFrame = -1;
    private float messageTime;

    internal ContainerRegistry(StorageSettings settings) => this.settings = settings;

    internal void Initialize()
    {
        foreach (Container container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            Register(container);
    }

    internal void Register(Container container)
    {
        if (!container || containers.ContainsKey(container)) return;
        ZNetView view = View(container);
        if (!view || !view.IsValid()) return;
        containers.Add(container, view);
        nearbyFrame = -1;
        // Owner grants authority before any client writes inventory data. Never ClaimOwnership().
        view.Register<long>(RequestRpc, (sender, playerId) => GrantOwnership(container, sender, playerId));
    }

    internal List<Container> Nearby(Player player)
    {
        if (nearbyFrame == Time.frameCount && nearbyPlayer == player) return nearby;
        foreach (Container dead in containers.Keys.Where(c => !c).ToArray())
        {
            containers.Remove(dead);
            requests.Remove(dead);
            loadedFrames.Remove(dead);
        }
        nearbyFrame = Time.frameCount;
        nearbyPlayer = player;
        nearby = containers.Keys.Where(c => CanAccess(c, player))
            .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude)
            .ThenBy(c => c.GetInstanceID()).ToList();
        return nearby;
    }

    internal bool CanAccess(Container container, Player player)
    {
        if (!settings.Enabled.Value || !container || !player || !container.isActiveAndEnabled) return false;
        Piece piece = container.GetComponent<Piece>();
        // Exclude tombstones, naturally generated loot, wagons and ship/root inventories.
        if (!piece || piece.GetCreator() == 0 || container.m_wagon || container.m_rootObjectOverride || container.m_autoDestroyEmpty)
            return false;
        ZNetView view = View(container);
        if (!view || !view.IsValid() || container.GetInventory() == null || container.IsInUse() ||
            (!view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_inUse) != 0)) return false;
        if ((container.transform.position - player.transform.position).sqrMagnitude > settings.Range.Value * settings.Range.Value)
            return false;
        if (!(bool)AccessMethod.Invoke(container, new object[] { player.GetPlayerID() })) return false;
        return HasWardAccess(container.transform.position, player.GetPlayerID());
    }

    // Check the requester's permissions on the owner too, rather than the owner's local-player permissions.
    private static bool HasWardAccess(Vector3 position, long playerId)
    {
        bool insideWard = false;
        foreach (PrivateArea ward in (List<PrivateArea>)AllWards.GetValue(null))
        {
            if (!ward || !(bool)WardEnabled.Invoke(ward, null) ||
                !(bool)WardInside.Invoke(ward, new object[] { position, 0f })) continue;
            insideWard = true;
            Piece piece = ward.GetComponent<Piece>();
            if ((piece && piece.GetCreator() == playerId) ||
                (bool)WardPermitted.Invoke(ward, new object[] { playerId })) return true;
        }
        return !insideWard;
    }

    internal void Refresh(Container container)
    {
        if (!loadedFrames.TryGetValue(container, out int frame) || frame != Time.frameCount)
        {
            LoadMethod.Invoke(container, null);
            loadedFrames[container] = Time.frameCount;
        }
    }

    internal bool IsOwned(Container container) => container && View(container) && View(container).IsOwner();

    internal void Request(Container container, Player player)
    {
        if (IsOwned(container) || !CanAccess(container, player)) return;
        if (requests.TryGetValue(container, out float last) && Time.unscaledTime - last < 1f) return;
        requests[container] = Time.unscaledTime;
        View(container).InvokeRPC(RequestRpc, player.GetPlayerID());
    }

    internal void ShowPending(Player player)
    {
        if (Time.unscaledTime < messageTime) return;
        messageTime = Time.unscaledTime + 2f;
        player.Message(MessageHud.MessageType.Center, "Ragnarök: waiting for a shared chest. Try again in a moment.");
    }

    private void GrantOwnership(Container container, long sender, long playerId)
    {
        if (!IsOwned(container)) return;
        Player requester = Player.GetPlayer(playerId);
        if (!requester) return;
        ZNetView playerView = requester.GetComponent<ZNetView>();
        if (!playerView || !playerView.IsValid() || playerView.GetZDO().GetOwner() != sender) return;
        if (!CanAccess(container, requester)) return;
        ZNetView view = View(container);
        // Match the game's ordered, owner-approved chest-opening handoff.
        ZDOMan.instance.ForceSendZDO(sender, view.GetZDO().m_uid);
        view.GetZDO().SetOwner(sender);
    }

    public void Dispose()
    {
        foreach (ZNetView view in containers.Values)
            if (view) view.Unregister(RequestRpc);
        containers.Clear();
        requests.Clear();
        loadedFrames.Clear();
        nearby.Clear();
        nearbyPlayer = null;
        nearbyFrame = -1;
    }
}
