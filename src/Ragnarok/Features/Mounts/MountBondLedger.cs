using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Ragnarok.Features.Mounts;

internal readonly struct MountIdentity : IEquatable<MountIdentity>
{
    internal readonly long User;
    internal readonly uint Object;
    internal readonly string Marker;
    internal MountIdentity(long user, uint instance, string marker = "") { User = user; Object = instance; Marker = marker.ToLowerInvariant(); }
    internal bool Valid => User != 0 && Object != 0 && (string.IsNullOrEmpty(Marker) || Guid.TryParseExact(Marker, "N", out _));
    public bool Equals(MountIdentity other) => !string.IsNullOrEmpty(Marker) || !string.IsNullOrEmpty(other.Marker)
        ? !string.IsNullOrEmpty(Marker) && Marker == other.Marker : User == other.User && Object == other.Object;
    public override bool Equals(object? other) => other is MountIdentity id && Equals(id);
    public override int GetHashCode() => !string.IsNullOrEmpty(Marker) ? Marker.GetHashCode() : User.GetHashCode() * 397 ^ Object.GetHashCode();
}

// World-scoped, one-to-one bonds. Clone before editing so disk commit failures can
// leave the authoritative in-memory ledger unchanged. No client-supplied ownership.
internal sealed class MountBondLedger
{
    private readonly Dictionary<long, MountIdentity> bonds = new();
    internal readonly long World;
    internal int Count => bonds.Count;
    internal IEnumerable<MountIdentity> Mounts => bonds.Values;
    internal MountBondLedger(long world) => World = world;
    internal MountBondLedger Copy()
    {
        var copy = new MountBondLedger(World);
        foreach (var pair in bonds) copy.bonds.Add(pair.Key, pair.Value);
        return copy;
    }

    internal bool TryGet(long player, out MountIdentity mount) => bonds.TryGetValue(player, out mount);
    internal bool TryBind(long player, MountIdentity mount)
    {
        if (player == 0 || !mount.Valid) return false;
        foreach (var pair in bonds)
            if (pair.Key != player && pair.Value.Equals(mount)) return false;
        bonds[player] = mount;
        return true;
    }
    internal bool Release(long player) => bonds.Remove(player);

    internal string Serialize()
    {
        string Text<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
        return "RagnarokMountBonds 2 " + Text(World) + "\n" + string.Join("\n", bonds.OrderBy(p => p.Key)
            .Select(p => Text(p.Key) + " " + Text(p.Value.User) + " " + Text(p.Value.Object) + " " +
                (string.IsNullOrEmpty(p.Value.Marker) ? "-" : p.Value.Marker))) + "\n";
    }

    internal static MountBondLedger Parse(long world, string text)
    {
        using var reader = new StringReader(text);
        string? header = reader.ReadLine();
        bool legacy = header == "RagnarokMountBonds 1 " + world.ToString(CultureInfo.InvariantCulture);
        if (!legacy && header != "RagnarokMountBonds 2 " + world.ToString(CultureInfo.InvariantCulture))
            throw new InvalidDataException("Mount bond file belongs to another world or version.");
        var ledger = new MountBondLedger(world);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(' ');
            if (parts.Length != (legacy ? 3 : 4) || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long player) ||
                !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long user) ||
                !uint.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint instance) ||
                (!legacy && parts[3] != "-" && !Guid.TryParseExact(parts[3], "N", out _)) ||
                ledger.bonds.ContainsKey(player) || ledger.Count >= 4096 || !ledger.TryBind(player, new MountIdentity(user, instance,
                    legacy || parts[3] == "-" ? "" : parts[3])))
                throw new InvalidDataException("Invalid or conflicting mount bond record.");
        }
        return ledger;
    }
}
