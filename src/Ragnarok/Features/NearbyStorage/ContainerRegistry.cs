using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ragnarok.Features.NearbyStorage;

internal sealed class ContainerRegistry : IDisposable
{
    private const string RequestRpc = "Ragnarok_RequestNearbyChest_v1";
    private static readonly AccessTools.FieldRef<Container, ZNetView> View =
        AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
    private static readonly Func<Container, bool> Load = AccessTools.MethodDelegate<Func<Container, bool>>(AccessTools.Method(typeof(Container), "Load"));
    private static readonly Func<Container, long, bool> CheckAccess =
        AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess"));
    private static readonly FieldInfo AllWards = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    private static readonly Func<PrivateArea, bool> WardEnabled =
        AccessTools.MethodDelegate<Func<PrivateArea, bool>>(AccessTools.Method(typeof(PrivateArea), "IsEnabled"));
    private static readonly Func<PrivateArea, Vector3, float, bool> WardInside =
        AccessTools.MethodDelegate<Func<PrivateArea, Vector3, float, bool>>(AccessTools.Method(typeof(PrivateArea), "IsInside"));
    private static readonly Func<PrivateArea, long, bool> WardPermitted =
        AccessTools.MethodDelegate<Func<PrivateArea, long, bool>>(AccessTools.Method(typeof(PrivateArea), "IsPermitted"));

    private readonly StorageSettings settings;
    private readonly Dictionary<Container, ZNetView> containers = new();
    private readonly Dictionary<Container, float> requests = new();
    private readonly Dictionary<Container, int> loadedFrames = new();
    private List<Container> nearby = new();
    private Player? nearbyPlayer;
    private int nearbyFrame = -1;
    private float nearbyUntil, nearbyRange;
    private Vector3 nearbyPosition;
    private readonly List<Inventory> previewSources = new();
    private readonly Dictionary<(string Name, int Quality, bool World), int> previewCounts = new();
    private Player? previewPlayer;
    private float previewUntil, previewRange;
    private int previewWorld;
    private Vector3 previewPosition;
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
        nearbyUntil = previewUntil = 0f;
        // Owner grants authority before any client writes inventory data. Never ClaimOwnership().
        view.Register<long>(RequestRpc, (sender, playerId) => GrantOwnership(container, sender, playerId));
    }

    internal List<Container> Nearby(Player player, bool fresh = false)
    {
        float range = settings.EffectiveRange;
        Vector3 position = player.transform.position;
        if (nearbyPlayer == player && nearbyRange == range &&
            (position - nearbyPosition).sqrMagnitude < 1f &&
            (nearbyFrame == Time.frameCount || (!fresh && Time.unscaledTime < nearbyUntil))) return nearby;
        foreach (Container dead in containers.Keys.Where(c => !c).ToArray())
        {
            containers.Remove(dead);
            requests.Remove(dead);
            loadedFrames.Remove(dead);
        }
        nearbyFrame = Time.frameCount;
        nearbyUntil = Time.unscaledTime + 0.2f;
        nearbyPosition = position;
        nearbyRange = range;
        nearbyPlayer = player;
        nearby = containers.Keys.Where(c => CanAccess(c, player))
            .OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude)
            .ThenBy(c => c.GetInstanceID()).ToList();
        return nearby;
    }

    // Shared display/discovery snapshot. Spending scopes never use these cached counts.
    internal int PreviewCount(Player player, string name, int quality, bool matchWorldLevel)
    {
        if (!settings.Enabled.Value) return 0;
        if (previewPlayer != player || Time.unscaledTime >= previewUntil || previewRange != settings.EffectiveRange ||
            previewWorld != Game.m_worldLevel || (player.transform.position - previewPosition).sqrMagnitude >= 1f)
        {
            previewPlayer = player;
            previewUntil = Time.unscaledTime + 0.2f;
            previewRange = settings.EffectiveRange;
            previewWorld = Game.m_worldLevel;
            previewPosition = player.transform.position;
            previewSources.Clear();
            previewCounts.Clear();
            foreach (Container container in Nearby(player))
            {
                if (!CanAccess(container, player)) continue;
                Refresh(container);
                previewSources.Add(container.GetInventory());
            }
        }
        var key = (name, quality, matchWorldLevel);
        if (previewCounts.TryGetValue(key, out int count)) return count;
        foreach (Inventory inventory in previewSources) count += inventory.CountItems(name, quality, matchWorldLevel);
        previewCounts[key] = count;
        return count;
    }

    internal void InvalidatePreview() => previewUntil = 0f;

    internal bool CanAccess(Container container, Player player, bool allowOpen = false)
    {
        if (!settings.Enabled.Value || !container || !player || !container.isActiveAndEnabled) return false;
        Piece piece = container.GetComponent<Piece>();
        // Exclude tombstones, naturally generated loot, wagons and ship/root inventories.
        if (!piece || piece.GetCreator() == 0 || container.GetComponent<TombStone>() || container.m_wagon || container.m_rootObjectOverride || container.m_autoDestroyEmpty)
            return false;
        ZNetView view = View(container);
        if (!view || !view.IsValid() || container.GetInventory() == null ||
            (!(allowOpen && view.IsOwner()) && (container.IsInUse() ||
            (!view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_inUse) != 0)))) return false;
        // An owner's personal preference must not restrict another player's request.
        float range = player == Player.m_localPlayer ? settings.EffectiveRange : settings.Range.Value;
        if ((container.transform.position - player.transform.position).sqrMagnitude > range * range)
            return false;
        if (!CheckAccess(container, player.GetPlayerID())) return false;
        return HasWardAccess(container.transform.position, player.GetPlayerID());
    }

    // Check the requester's permissions on the owner too, rather than the owner's local-player permissions.
    private static bool HasWardAccess(Vector3 position, long playerId)
    {
        bool insideWard = false;
        foreach (PrivateArea ward in (List<PrivateArea>)AllWards.GetValue(null))
        {
            if (!ward || !WardEnabled(ward) || !WardInside(ward, position, 0f)) continue;
            insideWard = true;
            Piece piece = ward.GetComponent<Piece>();
            if ((piece && piece.GetCreator() == playerId) ||
                WardPermitted(ward, playerId)) return true;
        }
        return !insideWard;
    }

    internal void Refresh(Container container)
    {
        if (!loadedFrames.TryGetValue(container, out int frame) || frame != Time.frameCount)
        {
            Load(container);
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
        player.Message(MessageHud.MessageType.Center, "Ragnarök: waiting for access to a shared chest.");
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
        previewSources.Clear();
        previewCounts.Clear();
        previewPlayer = null;
        nearbyPlayer = null;
        nearbyFrame = -1;
    }
}
