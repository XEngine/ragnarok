using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ironbound.Features.Mounts;

// Server-owned reward history and ordered completion receipts. Eligibility is
// persisted before delivery, preventing rebind/reconnect/reload XP farming.
internal sealed class MountProgressStore
{
    internal sealed class Receipt
    {
        internal long User, Sequence;
        internal float Cost, Xp;
    }
    private readonly string path;
    private readonly long world;
    private readonly Dictionary<long, long> sequences = new(), summonTimes = new();
    private readonly HashSet<string> bindings = new(StringComparer.Ordinal);
    private readonly List<Receipt> receipts = new();
    internal MountProgressStore(string path, long world)
    {
        this.path = path; this.world = world;
        if (File.Exists(path)) Read(File.ReadAllText(path));
    }
    internal bool HasRoom(long user) => receipts.Count < 4096 && receipts.Count(r => r.User == user) < 32;
    internal Receipt? Next(long user) => receipts.Where(r => r.User == user).OrderBy(r => r.Sequence).FirstOrDefault();
    internal bool FirstBond(long user, string marker) => !bindings.Contains(user.ToString(CultureInfo.InvariantCulture) + ":" + marker);
    internal Receipt Complete(long user, float cost, float xp, long now, float quietSeconds, string marker = "")
    {
        if (user == 0 || !HasRoom(user) || (!sequences.ContainsKey(user) && sequences.Count >= 65536) ||
            !MountProgression.Finite(cost) || cost < 0 || cost > 1000000 ||
            !MountProgression.Finite(xp) || xp < 0 || xp > 5 || now < 0 || !MountProgression.Finite(quietSeconds) || quietSeconds < 0)
            throw new InvalidOperationException("Invalid mount progression completion.");
        string key = user.ToString(CultureInfo.InvariantCulture) + ":" + marker;
        bool binding = marker.Length > 0;
        if (binding && (!Guid.TryParseExact(marker, "N", out _) || (!bindings.Contains(key) && bindings.Count >= 65536)))
            throw new InvalidOperationException("Mount reward history is invalid or full.");
        long oldSequence = sequences.TryGetValue(user, out long sequence) ? sequence : 0;
        long oldTime = summonTimes.TryGetValue(user, out long time) ? time : -1;
        bool first = binding && !bindings.Contains(key);
        bool reward = binding ? first : oldTime < 0 || now - oldTime >= quietSeconds;
        var receipt = new Receipt { User = user, Sequence = checked(oldSequence + 1), Cost = cost, Xp = reward ? xp : 0 };
        sequences[user] = receipt.Sequence;
        if (first) bindings.Add(key);
        if (!binding && reward && xp > 0) summonTimes[user] = now;
        receipts.Add(receipt);
        try { Save(); }
        catch
        {
            receipts.Remove(receipt);
            if (oldSequence == 0) sequences.Remove(user); else sequences[user] = oldSequence;
            if (first) bindings.Remove(key);
            if (oldTime < 0) summonTimes.Remove(user); else summonTimes[user] = oldTime;
            throw;
        }
        return receipt;
    }
    internal void Ack(long user, long sequence)
    {
        Receipt? receipt = Next(user);
        if (receipt == null || receipt.Sequence != sequence) return;
        receipts.Remove(receipt);
        try { Save(); } catch { receipts.Add(receipt); throw; }
    }
    private static string N<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text = new StringBuilder("RagnarokMountProgress 1 " + N(world) + "\n");
        foreach (var pair in sequences.OrderBy(p => p.Key)) text.AppendLine("P " + N(pair.Key) + " " + N(pair.Value) + " " + N(summonTimes.TryGetValue(pair.Key, out long time) ? time : -1));
        foreach (string key in bindings.OrderBy(k => k, StringComparer.Ordinal)) text.AppendLine("B " + key);
        foreach (Receipt receipt in receipts.OrderBy(r => r.User).ThenBy(r => r.Sequence))
            text.AppendLine("R " + N(receipt.User) + " " + N(receipt.Sequence) + " " + N(receipt.Cost) + " " + N(receipt.Xp));
        string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text.ToString()); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private void Read(string text)
    {
        using var reader = new StringReader(text);
        if (reader.ReadLine() != "RagnarokMountProgress 1 " + N(world)) throw new InvalidDataException("Mount progression world/version mismatch.");
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(' ');
            if (parts[0] == "P" && parts.Length == 4)
            {
                long user = long.Parse(parts[1], CultureInfo.InvariantCulture), sequence = long.Parse(parts[2], CultureInfo.InvariantCulture), time = long.Parse(parts[3], CultureInfo.InvariantCulture);
                if (user == 0 || sequence <= 0 || time < -1 || sequences.ContainsKey(user) || sequences.Count >= 65536) throw new InvalidDataException("Invalid mount progression player.");
                sequences.Add(user, sequence); if (time >= 0) summonTimes.Add(user, time);
            }
            else if (parts[0] == "B" && parts.Length == 2)
            {
                string[] key = parts[1].Split(':');
                if (key.Length != 2 || !long.TryParse(key[0], out long user) || user == 0 || !Guid.TryParseExact(key[1], "N", out _) ||
                    !sequences.ContainsKey(user) || bindings.Count >= 65536 || !bindings.Add(parts[1])) throw new InvalidDataException("Invalid mount binding reward.");
            }
            else if (parts[0] == "R" && parts.Length == 5)
            {
                var r = new Receipt { User = long.Parse(parts[1], CultureInfo.InvariantCulture), Sequence = long.Parse(parts[2], CultureInfo.InvariantCulture),
                    Cost = float.Parse(parts[3], CultureInfo.InvariantCulture), Xp = float.Parse(parts[4], CultureInfo.InvariantCulture) };
                if (!sequences.TryGetValue(r.User, out long sequence) || r.Sequence <= 0 || r.Sequence > sequence || !HasRoom(r.User) ||
                    !MountProgression.Finite(r.Cost) || r.Cost < 0 || r.Cost > 1000000 || !MountProgression.Finite(r.Xp) || r.Xp < 0 || r.Xp > 5 ||
                    receipts.Any(other => other.User == r.User && other.Sequence == r.Sequence)) throw new InvalidDataException("Invalid mount completion receipt.");
                receipts.Add(r);
            }
            else throw new InvalidDataException("Invalid mount progression record.");
        }
    }
}
