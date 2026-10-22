using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironbound.Networking;

// Server authority over gameplay settings. The server (a dedicated server, or the player hosting) sends
// its values for the settings below to everyone who joins, and again whenever they change. While its
// [Server] LockSettings is on, players play with the server's values and cannot change them; their own
// stay in their config file (the file never receives the server's) and return when they leave. A server
// without Ironbound controls nothing. A dedicated server re-reads its config file when it is edited.
internal sealed class ServerConfig : IDisposable
{
    private const string ConfigRpc = "Ragnarok_ServerConfig", RequestRpc = "Ragnarok_ServerConfigRequest";

    internal static ServerConfig? Instance;

    internal readonly ConfigEntry<bool> Lock;
    private readonly ConfigFile config;
    private readonly ManualLogSource log;
    private readonly string version;
    internal string? RemoteVersion { get; private set; }
    private readonly List<ConfigEntryBase> synced;
    private readonly HashSet<ConfigEntryBase> mandatory;
    // While a server holds a setting: its value, and the player's own (what the config file keeps).
    private readonly Dictionary<ConfigEntryBase, string> held = new(), own = new();
    private readonly FileSystemWatcher? watcher;
    private ZNet? net;
    private bool applying, dirty, serverLocked;
    private int reloadPending;
    private readonly HashSet<ZRpc> registered = new();

    internal ServerConfig(ConfigFile config, ManualLogSource log, string version, IEnumerable<ConfigEntryBase> synced,
        IEnumerable<ConfigEntryBase>? mandatory = null)
    {
        this.config = config;
        this.log = log;
        this.version = version;
        this.synced = synced.ToList();
        this.mandatory = new HashSet<ConfigEntryBase>(mandatory ?? Enumerable.Empty<ConfigEntryBase>());
        Lock = config.Bind("Gameplay.Server", "LockSettings", true,
            "When you host or run a dedicated server: players who join use this file's values for the gameplay settings " +
            "(nearby chests, inventory and QoL gameplay settings including skills, resource costs, stations, sailing and ballistas) " +
            "and cannot change them. Off: other gameplay settings are personal. Nearby Chests enable state and shared range, " +
            "and death-reclaim auto equip are always defined and locked by a modded server.");
        config.SettingChanged += OnSettingChanged;
        config.ConfigReloaded += OnReloaded;

        // A dedicated server has no settings window: edits to its config file apply (and reach players) at once.
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            watcher = new FileSystemWatcher(Path.GetDirectoryName(config.ConfigFilePath)!, Path.GetFileName(config.ConfigFilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            watcher.Changed += (_, _) => Interlocked.Exchange(ref reloadPending, 1);
            watcher.EnableRaisingEvents = true;
        }
        Instance = this;
    }

    // Whether the server decides this setting now.
    internal bool Controlled(ConfigEntryBase entry) => held.ContainsKey(entry) || (entry == Lock && serverLocked);

    internal void Tick()
    {
        ZNet current = ZNet.instance;
        if (current != net)
        {
            Release();
            Unregister();
            net = current;
            if (net) Hook(net!);
        }
        if ((!net || (!net!.IsServer() && net.GetServerPeer() == null)) && (held.Count > 0 || serverLocked || RemoteVersion != null)) Release();

        if (Interlocked.Exchange(ref reloadPending, 0) != 0)
        {
            try
            {
                config.Reload();
            }
            catch (Exception exception)
            {
                log.LogError($"Could not reload configuration: {exception.Message}");
            }
        }
        if (dirty && net && net!.IsServer())
        {
            dirty = false;
            foreach (ZNetPeer peer in net.GetPeers())
                if (peer.m_uid != 0 && peer.m_rpc != null) Send(peer.m_rpc);
        }
    }

    public void Dispose()
    {
        Release();
        config.SettingChanged -= OnSettingChanged;
        config.ConfigReloaded -= OnReloaded;
        watcher?.Dispose();
        Unregister();
        if (Instance == this) Instance = null;
    }

    // A network that was already running when Ironbound loaded (F6): join its connections, and ask the server.
    private void Hook(ZNet zNet)
    {
        foreach (ZNetPeer peer in zNet.GetPeers())
            if (peer.m_rpc != null) Register(peer.m_rpc);
        if (zNet.IsServer()) dirty = true;
        else zNet.GetServerPeer()?.m_rpc?.Invoke(RequestRpc);
    }

    private static void Register(ZRpc rpc)
    {
        ServerConfig? instance = Instance;
        if (instance == null || !instance.registered.Add(rpc)) return;
        rpc.Register<ZPackage>(ConfigRpc, (sender, package) => instance.Receive(sender, package));
        rpc.Register(RequestRpc, sender => instance.Answer(sender));
    }

    private void Unregister()
    {
        foreach (ZRpc rpc in registered)
        {
            rpc.Unregister(ConfigRpc);
            rpc.Unregister(RequestRpc);
        }
        registered.Clear();
    }

    private void Unregister(ZRpc rpc)
    {
        if (!registered.Remove(rpc)) return;
        rpc.Unregister(ConfigRpc);
        rpc.Unregister(RequestRpc);
    }

    private void Answer(ZRpc rpc)
    {
        if (net && net!.IsServer()) Send(rpc);
    }

    private void Send(ZRpc rpc)
    {
        ZPackage package = new();
        package.Write(version);
        package.Write(Lock.Value);
        package.Write(synced.Count);
        foreach (ConfigEntryBase entry in synced)
        {
            package.Write(ConfigSchema.WireSection(entry.Definition.Section, entry.Definition.Key));
            package.Write(entry.Definition.Key);
            package.Write(entry.GetSerializedValue());
        }
        rpc.Invoke(ConfigRpc, package);
    }

    private void Receive(ZRpc sender, ZPackage package)
    {
        // Only the connected server may supply authoritative settings.
        if (!net || net!.IsServer() || net.GetServerPeer()?.m_rpc != sender) return;
        try { ApplyPackage(package); }
        catch (Exception exception) { log.LogWarning("Rejected invalid server configuration: " + exception.Message); }
    }

    internal void ApplyPackage(ZPackage package)
    {
        string serverVersion = package.ReadString();
        bool locked = package.ReadBool();
        Dictionary<ConfigDefinition, string> values = new();
        int count = package.ReadInt();
        if (count < 0 || count > 1024) throw new InvalidDataException("Invalid setting count.");
        for (int i = 0; i < count; i++)
        {
            string section = package.ReadString(), key = package.ReadString(), value = package.ReadString();
            values[new ConfigDefinition(ConfigSchema.CanonicalSection(section, key), key)] = value;
        }
        if (serverVersion != version)
            log.LogWarning($"The server runs Ironbound {serverVersion}; this is {version}.");
        // Validate the complete message before replacing any active policy.
        foreach (ConfigEntryBase entry in synced)
        {
            if (!values.TryGetValue(entry.Definition, out string value)) continue;
            object parsed = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
            if (parsed is float number && (float.IsNaN(number) || float.IsInfinity(number)))
                throw new InvalidDataException("Non-finite setting value.");
        }
        serverLocked = locked;
        RemoteVersion = serverVersion;
        foreach (ConfigEntryBase entry in synced)
        {
            if ((locked || mandatory.Contains(entry)) && values.TryGetValue(entry.Definition, out string value)) Hold(entry, value);
            else Release(entry);
        }
        log.LogInfo($"The server sets {held.Count} of Ironbound's settings: " +
                    string.Join(", ", held.Select(kv => $"{kv.Key.Definition.Section}.{kv.Key.Definition.Key}={kv.Value}")));
    }

    private void Hold(ConfigEntryBase entry, string value)
    {
        object parsed = TomlTypeConverter.ConvertToValue(value, entry.SettingType);
        parsed = entry.Description.AcceptableValues?.Clamp(parsed) ?? parsed;
        value = TomlTypeConverter.ConvertToString(parsed, entry.SettingType);
        if (!own.ContainsKey(entry)) own[entry] = entry.GetSerializedValue();
        held[entry] = value;
        Set(entry, value);
    }

    private void Release()
    {
        RemoteVersion = null;
        serverLocked = false;
        foreach (ConfigEntryBase entry in held.Keys.ToList()) Release(entry);
    }

    private void Release(ConfigEntryBase entry)
    {
        held.Remove(entry);
        if (!own.TryGetValue(entry, out string value)) return;
        own.Remove(entry);
        Set(entry, value);
    }

    private void Set(ConfigEntryBase entry, string value)
    {
        applying = true;
        try
        {
            entry.SetSerializedValue(value);
        }
        finally
        {
            applying = false;
        }
    }

    private static string Current(ConfigEntryBase entry) => TomlTypeConverter.ConvertToString(entry.BoxedValue, entry.SettingType);

    private void OnSettingChanged(object sender, SettingChangedEventArgs e)
    {
        if (applying) return;
        ConfigEntryBase entry = e.ChangedSetting;
        if (held.TryGetValue(entry, out string value))
        {
            // Changed on this side (the file reloaded, a control, restore defaults): it becomes the player's
            // own value for later; the server's stays in force.
            own[entry] = Current(entry);
            if (own[entry] != value) Set(entry, value);
            return;
        }
        if (entry == Lock || synced.Contains(entry)) dirty = true;
    }

    private void OnReloaded(object sender, EventArgs e)
    {
        foreach (KeyValuePair<ConfigEntryBase, string> kv in held.ToList())
        {
            string now = Current(kv.Key);
            if (now == kv.Value) continue;
            own[kv.Key] = now;
            Set(kv.Key, kv.Value);
        }
        dirty = true;
    }

    // Each connection learns Ironbound's messages as it opens.
    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    private static class ConnectionPatch
    {
        private static void Postfix(ZNetPeer peer)
        {
            if (Instance != null && peer.m_rpc != null) Register(peer.m_rpc);
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    private static class DisconnectPatch
    {
        private static void Prefix(ZNetPeer peer)
        {
            if (peer.m_rpc != null) Instance?.Unregister(peer.m_rpc);
        }
    }

    // The server sends its settings once a player's handshake is accepted.
    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    private static class HandshakePatch
    {
        private static void Postfix(ZNet __instance, ZRpc rpc)
        {
            if (Instance == null || !__instance.IsServer()) return;
            ZNetPeer? peer = __instance.GetPeers().Find(p => p.m_rpc == rpc);
            if (peer != null && peer.m_uid != 0) Instance.Send(rpc);
        }
    }

    // BepInEx saves before its SettingChanged event. Restore the values present at save entry,
    // including a proposed local edit, so that event can still observe the actual edit.
    private Dictionary<ConfigEntryBase, object>? BeforeSave(ConfigFile file)
    {
        if (file != config || held.Count == 0) return null;
        Dictionary<ConfigEntryBase, object> snapshot = new();
        foreach (KeyValuePair<ConfigEntryBase, string> kv in held)
        {
            snapshot[kv.Key] = kv.Key.BoxedValue;
            string current = Current(kv.Key);
            if (!applying && current != kv.Value) own[kv.Key] = current;
            if (own.TryGetValue(kv.Key, out string mine))
                QuietSet(kv.Key, TomlTypeConverter.ConvertToValue(mine, kv.Key.SettingType));
        }
        return snapshot;
    }

    private static void QuietSet(ConfigEntryBase entry, object value) =>
        AccessTools.Field(entry.GetType(), "_typedValue").SetValue(entry, value);

    [HarmonyPatch(typeof(ConfigEntryBase), nameof(ConfigEntryBase.SetSerializedValue))]
    private static class SerializedEditPatch
    {
        private static void Prefix(ConfigEntryBase __instance, string value)
        {
            ServerConfig? instance = Instance;
            if (instance == null || instance.applying || !instance.held.ContainsKey(__instance)) return;
            // Reloading a personal value equal to the effective server value raises no
            // SettingChanged event. Remember the disk input before the equality check.
            try
            {
                object parsed = TomlTypeConverter.ConvertToValue(value, __instance.SettingType);
                parsed = __instance.Description.AcceptableValues?.Clamp(parsed) ?? parsed;
                instance.own[__instance] = TomlTypeConverter.ConvertToString(parsed, __instance.SettingType);
            }
            catch (Exception) { /* Let BepInEx report malformed config values normally. */ }
        }
    }

    [HarmonyPatch(typeof(ConfigFile), nameof(ConfigFile.Save))]
    private static class SavePatch
    {
        private static void Prefix(ConfigFile __instance, out Dictionary<ConfigEntryBase, object>? __state) =>
            __state = Instance?.BeforeSave(__instance);

        private static void Finalizer(Dictionary<ConfigEntryBase, object>? __state)
        {
            if (__state == null) return;
            foreach (var entry in __state) QuietSet(entry.Key, entry.Value);
        }
    }
}
