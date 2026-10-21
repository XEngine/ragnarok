using Ragnarok.Features.Mounts;

internal static class MountProgressChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (int ride in new[] { 0, 25, 50, 75, 100 })
        {
            check(Math.Abs(MountProgression.Cost(200, ride, 10) - 20 * (1 - ride / 100f)) < 0.0001f, "Wrong Riding stamina curve");
            check(Math.Abs(MountProgression.Windup(ride, 0.6f) - (1.2f - 0.6f * ride / 100)) < 0.0001f, "Wrong Riding whistle curve");
            check(MountProgression.Tier(ride) == ride / 25, "Wrong Riding visual milestone");
        }
        check(MountProgression.Cost(200, 100, 10) == 0, "Expert summons are not free");
        check(MountProgression.Cost(200, 0, 0) == 0, "Server free summons ignored");
        check(MountProgression.Cost(200, -50, 10) == 20 && MountProgression.Cost(200, 200, 10) == 0, "Out-of-range Riding escaped clamp");
        check(MountProgression.Snapshot(0, 100, 0) && MountProgression.Snapshot(100, 100, 100), "Valid stamina/skill boundary rejected");
        check(!MountProgression.Snapshot(-1, 100, 20) && !MountProgression.Snapshot(101, 100, 20) &&
            !MountProgression.Snapshot(0, 0, 0) && !MountProgression.Snapshot(0, 100, -1) && !MountProgression.Snapshot(0, 100, 101), "Invalid owner snapshot accepted");
        foreach (float value in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            check(!MountProgression.Snapshot(value, 100, 100) && !MountProgression.Snapshot(0, value, 100) && !MountProgression.Snapshot(0, 100, value), "Non-finite owner snapshot accepted");
        float previous = float.MaxValue;
        for (int ride = 0; ride <= 100; ride++)
        {
            float cost = MountProgression.Cost(237, ride, 10);
            check(cost >= 0 && cost <= previous, "Riding increased the cost or made it negative"); previous = cost;
        }

        string directory = Path.Combine(Path.GetTempPath(), "Ragnarok-mount-progress-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "progress.txt"), marker = Guid.NewGuid().ToString("N");
        try
        {
            var store = new MountProgressStore(path, 91);
            var first = store.Complete(1, 0, 0.5f, 1000, 300, marker);
            check(first.Sequence == 1 && first.Xp == 0.5f && !store.FirstBond(1, marker), "First bond did not grant or record XP");
            store = new MountProgressStore(path, 91);
            var again = store.Complete(1, 0, 0.5f, 1001, 300, marker);
            check(again.Xp == 0 && again.Sequence == 2, "Rebinding farmed XP after reload");
            check(store.Complete(2, 0, 0.5f, 1001, 300, marker).Xp == 0.5f, "Different player's first bond lost XP");
            check(store.Complete(1, 20, 0.25f, 1100, 300).Xp == 0.25f, "First summon lost XP");
            store = new MountProgressStore(path, 91);
            var cooldown = store.Complete(1, 20, 0.25f, 1399, 300);
            check(cooldown.Xp == 0 && cooldown.Cost == 20, "Reward cooldown changed cost or reset across restart");
            check(store.Complete(1, 20, 0.25f, 1400, 300).Xp == 0.25f, "XP cooldown boundary rejected");
            check(store.Complete(1, 20, 0.25f, 1200, 300).Xp == 0, "Backward clock farmed XP");
            check(store.Next(1)?.Sequence == 1, "Receipt delivery skipped oldest completion");
            store.Ack(1, 2); store.Ack(2, 1);
            check(store.Next(1)?.Sequence == 1 && store.Next(2) == null, "Out-of-order/cross-player acknowledgment removed a receipt");
            store.Ack(1, 1); store.Ack(1, 1);
            check(store.Next(1)?.Sequence == 2, "Duplicate acknowledgment skipped a receipt");
            store = new MountProgressStore(path, 91);
            check(store.Next(1)?.Sequence == 2 && store.Next(2) == null, "Receipt acknowledgments did not persist");
            bool rejected = false;
            try { _ = new MountProgressStore(path, 92); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Progress history crossed worlds");
            for (int i = 0; i < 32; i++) store.Complete(3, 0, 0, 1500, 300);
            check(!store.HasRoom(3) && store.HasRoom(4), "Unacknowledged completion bound is not per player");
            rejected = false;
            try { store.Complete(3, 0, 1, 1500, 300); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Unbounded undelivered receipts accepted");
            string blocked = Path.Combine(directory, "blocked");
            Directory.CreateDirectory(blocked);
            var failed = new MountProgressStore(blocked, 91);
            rejected = false;
            try { failed.Complete(4, 0, 0.5f, 1500, 300, marker); } catch (IOException) { rejected = true; }
            check(rejected && failed.FirstBond(4, marker) && failed.Next(4) == null, "Disk failure consumed binding eligibility");
            Directory.Delete(blocked);
            check(failed.Complete(4, 0, 0.5f, 1500, 300, marker).Sequence == 1, "Disk failure consumed a receipt sequence");
            foreach (string invalid in new[] { "RagnarokMountProgress 1 91\nR 1 1 5 1\n", "RagnarokMountProgress 1 91\nP 1 1 -1\nR 1 1 NaN 1\n",
                "RagnarokMountProgress 1 91\nP 1 1 -1\nR 1 1 5 1\nR 1 1 5 1\n", "RagnarokMountProgress 1 91\nP 1 1 -2\n" })
            {
                string bad = Path.Combine(directory, "bad.txt"); File.WriteAllText(bad, invalid);
                rejected = false;
                try { _ = new MountProgressStore(bad, 91); } catch (InvalidDataException) { rejected = true; }
                check(rejected, "Malformed progression receipt accepted");
            }
        }
        finally
        {
            string resolved = Path.GetFullPath(directory);
            if (Path.GetDirectoryName(resolved) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) ||
                !Path.GetFileName(resolved).StartsWith("Ragnarok-mount-progress-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the isolated progression test directory.");
            Directory.Delete(resolved, true);
        }
        Console.WriteLine("PASS: Riding cost/windup/milestones, invalid snapshots, persistent first-bond and XP cooldown limits, ordered receipts, duplicate acknowledgments and atomic failure rollback.");
    }
}
