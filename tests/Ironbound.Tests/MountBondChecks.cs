using Ironbound.Features.Mounts;

internal static class MountBondChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var first = new MountIdentity(11, 1);
        var second = new MountIdentity(11, 2);
        var ledger = new MountBondLedger(91);
        check(ledger.TryBind(1, first), "First mount could not bind");
        check(!ledger.TryBind(2, first), "A second player stole a bound mount");
        check(ledger.TryBind(1, second) && ledger.Count == 1, "Changing mount did not replace the old bond");
        check(ledger.TryBind(2, first), "Replacing a mount did not release its old bond");
        var copy = ledger.Copy();
        copy.Release(1);
        check(ledger.Count == 2 && copy.Count == 1, "Uncommitted changes modified authoritative bonds");
        var restored = MountBondLedger.Parse(91, ledger.Serialize());
        check(restored.Count == 2 && restored.TryGet(1, out var mount) && mount.Equals(second), "Save/reload changed persistent identity");
        check(!ledger.TryBind(0, first) && !ledger.TryBind(3, new MountIdentity()), "Invalid identities were accepted");
        foreach (string invalid in new[] { ledger.Serialize().Replace(" 91\n", " 92\n"),
            "RagnarokMountBonds 1 91\n1 11 1\n2 11 1\n", "RagnarokMountBonds 1 91\n1 11 1\n1 11 2\n",
            "RagnarokMountBonds 1 91\n3 0 1\n", "RagnarokMountBonds 1 91\n3 11 0\n", "RagnarokMountBonds 1 91\nmalformed\n" })
        {
            bool rejected = false;
            try { MountBondLedger.Parse(91, invalid); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Corrupt/cross-world/duplicate bond file was accepted");
        }
        check(restored.Release(1) && !restored.TryGet(1, out _), "Release retained the bond");
        string stableMarker = Guid.NewGuid().ToString("N");
        var persistent = new MountBondLedger(91);
        var beforeRestart = new MountIdentity(1, 8777, stableMarker);
        var afterRestart = new MountIdentity(999, 42, stableMarker);
        check(beforeRestart.Equals(afterRestart), "Save/load network-ID remapping broke stable mount identity");
        check(!beforeRestart.Equals(new MountIdentity(1, 8777, Guid.NewGuid().ToString("N"))), "Reused network ID matched a different animal");
        check(!beforeRestart.Equals(new MountIdentity(1, 8777)), "Legacy cached ID matched a tagged mount");
        check(persistent.TryBind(1, beforeRestart) && !persistent.TryBind(2, afterRestart), "Remapped mount escaped exclusive ownership");
        var stableRoundTrip = MountBondLedger.Parse(91, persistent.Serialize());
        check(stableRoundTrip.TryGet(1, out var stable) && stable.Marker == stableMarker && stable.Equals(afterRestart), "Stable marker lost in disk round-trip");
        check(!persistent.TryBind(3, new MountIdentity(1, 2, "not-a-marker")), "Invalid identity marker accepted");
        check(MountRecordPolicy.Validate(true, true, true, 1000, out _), "Healthy saddled mount rejected");
        check(MountRecordPolicy.Validate(true, true, true, 0.1f, out _), "Low-health living mount rejected");
        check(!MountRecordPolicy.Validate(true, true, true, 0, out string dead) && dead.Contains("dead"), "Dead mount accepted");
        check(!MountRecordPolicy.Validate(true, true, false, 1000, out string unsaddled) && unsaddled.Contains("saddle"), "Unsaddled mount accepted");
        check(!MountRecordPolicy.Validate(true, false, true, 1000, out string untamed) && untamed.Contains("tamed"), "Untamed mount accepted");
        check(!MountRecordPolicy.Validate(false, true, true, 1000, out string prefab) && prefab.Contains("prefab"), "Missing prefab accepted");
        foreach (float invalidHealth in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            check(!MountRecordPolicy.Validate(true, true, true, invalidHealth, out _), "Invalid health accepted");
        check(MountArrivalPolicy.WithinReach(2500, 42, -2500, 2507, 42.2f, -2496), "Valid distant-world arrival rejected");
        check(MountArrivalPolicy.WithinReach(0, 0, 0, 12, 6, 0), "Arrival boundary rejected");
        check(!MountArrivalPolicy.WithinReach(0, 0, 0, 12.01f, 0, 0), "Arbitrary distant arrival accepted");
        check(!MountArrivalPolicy.WithinReach(0, 0, 0, 9, 0, 9), "Diagonal arrival escaped radial bound");
        check(!MountArrivalPolicy.WithinReach(0, 0, 0, 1, 6.01f, 0), "Vertical arrival escaped bound");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
        {
            check(!MountArrivalPolicy.WithinReach(0, 0, 0, bad, 0, 0), "Non-finite/overflow arrival accepted");
            check(!MountArrivalPolicy.WithinReach(bad, 0, 0, 1, 0, 0), "Invalid player reference accepted");
        }
        string directory = Path.Combine(Path.GetTempPath(), "Ironbound-mount-ledger-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "bonds.txt");
        try
        {
            var store = new MountBondStore(path, 91);
            store.Commit(ledger);
            check(new MountBondStore(path, 91).Current.Count == 2, "Committed bonds did not survive reopening");
            store.Commit(restored);
            check(new MountBondStore(path, 91).Current.Count == 1 && File.Exists(path + ".bak"), "Atomic replacement/backup failed");
            restored.Release(2);
            check(store.Current.Count == 1, "Caller mutated committed authoritative ledger");
            bool rejected = false;
            try { store.Commit(new MountBondLedger(92)); } catch (InvalidOperationException) { rejected = true; }
            check(rejected && new MountBondStore(path, 91).Current.Count == 1, "Cross-world commit damaged saved bonds");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
        Console.WriteLine("PASS: exclusive mount bonds, stable identity across ID remapping, disk persistence, mount-state validation and bounded arrival proposals.");
    }
}
