using Ragnarok.Features.Equipment;

internal static class GraveRecoveryChecks
{
    internal static void Run(Action<bool, string> check)
    {
        static GraveRecoveryPlan.Item Item(string name, int x, int y, int amount = 1, int limit = 1,
            int slot = -1, int quality = 1, int maxQuality = 1, int world = 0) =>
            new(name, amount, limit, quality, maxQuality, world, x, y, false, slot);
        var stone = Item("stone", 0, 0);
        var helmet = Item("helmet", 0, 0, slot: 0);
        var chest = Item("chest", 1, 0, slot: 1);

        var plan = GraveRecoveryPlan.Create(1, 1, [stone], [helmet, chest]);
        check(plan.AllFit && plan.Slots.SequenceEqual([0, 1]), "Full grid must not prevent recovery into empty worn slots");
        plan = GraveRecoveryPlan.Create(1, 1, [stone, Item("new helmet", 0, 64, slot: 0)], [helmet]);
        check(!plan.AllFit && plan.Slots[0] == -1, "Recovery must never overwrite occupied gear slots");
        plan = GraveRecoveryPlan.Create(1, 1, [stone], [helmet, helmet]);
        check(!plan.AllFit && plan.Slots.SequenceEqual([0, -1]), "Duplicate gear cannot reserve a slot twice");
        plan = GraveRecoveryPlan.Create(1, 1, [stone], [helmet, Item("wood", 1, 0)]);
        check(!plan.AllFit && plan.Slots[0] == 0, "Overflow stays in grave while manual Take All can still restore gear");
        plan = GraveRecoveryPlan.Create(1, 1, [], [helmet, Item("wood", 0, 0)]);
        check(plan.AllFit, "Gear must be reserved before the ordinary grave grid is reclaimed");
        plan = GraveRecoveryPlan.Create(1, 1, [Item("wood", 0, 0, 25, 50)], [helmet, Item("wood", 0, 0, 25, 50)]);
        check(plan.AllFit, "Matching partial stack and empty worn slot allow direct reclaim");
        plan = GraveRecoveryPlan.Create(1, 1, [Item("wood", 0, 0, 25, 50)],
            [Item("wood", 2, 0, 20, 50), Item("wood", 3, 0, 20, 50)]);
        check(!plan.AllFit, "Multiple incoming stacks must share existing capacity without double counting it");
        plan = GraveRecoveryPlan.Create(2, 1, [Item("wood", 0, 0, 25, 50)],
            [Item("wood", 1, 0, 25, 50), Item("stone", 0, 0)]);
        check(!plan.AllFit, "Prediction must honor native preferred positions instead of assuming optimal repacking");
        plan = GraveRecoveryPlan.Create(1, 1, [Item("wood", 0, 0, 25, 50, maxQuality: 3)],
            [Item("wood", 0, 0, 25, 50, quality: 2, maxQuality: 3)]);
        check(!plan.AllFit, "Upgradeable item qualities cannot share a stack");
        plan = GraveRecoveryPlan.Create(1, 1, [Item("wood", 0, 0, 25, 50)],
            [Item("wood", 0, 0, 25, 50, world: 1)]);
        check(!plan.AllFit, "World-level variants cannot share a stack");
        plan = GraveRecoveryPlan.Create(1, 1, [stone],
            [Item("food", 1, 0, 20, 50, slot: 16), Item("food", 0, 0, 10, 50)]);
        check(plan.AllFit && plan.Slots.SequenceEqual([16, -1]), "Restored provisions can accept later matching stack amounts");
        plan = GraveRecoveryPlan.Create(1, 1, [stone, Item("other food", 0, 65, 1, 10, slot: 16)],
            [Item("food", 1, 0, 20, 50, slot: 16)]);
        check(!plan.AllFit && plan.Slots[0] == -1, "Occupied provision slots cannot be overwritten");
        plan = GraveRecoveryPlan.Create(1, 1, [stone], [Item("unmarked armor", 0, 0)]);
        check(!plan.AllFit && plan.Slots[0] == -1, "Unmarked items do not gain hidden storage capacity");

        string nonce = "0123456789abcdef0123456789abcdef";
        check(GraveRecoveryTag.TryRead("42:-1:" + nonce, 42, out int extra) && extra == -1,
            "Existing equipment tags must remain readable");
        check(GraveRecoveryTag.TryRead("42:5:" + nonce, 42, out int provision) && provision == 5,
            "Saved provision position must survive serialization");
        foreach (string? invalid in new[] { null, "", "43:0:" + nonce, "42:0:invalid", "42:bad:" + nonce, "42:0:" + nonce + ":extra" })
            check(!GraveRecoveryTag.TryRead(invalid, 42, out _), "Malformed/foreign recovery tags must be rejected");

        Random random = new(1986);
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            int[] targets = Enumerable.Range(0, random.Next(1, 15)).Select(_ => random.Next(0, 8)).ToArray();
            var incoming = targets.Select(slot => Item("gear", 0, 0, slot: slot)).ToArray();
            plan = GraveRecoveryPlan.Create(1, 1, [stone], incoming);
            int[] reserved = plan.Slots.Where(slot => slot >= 0).ToArray();
            check(reserved.Distinct().Count() == reserved.Length, "Reservations must be unique");
            check(plan.AllFit == (targets.Distinct().Count() == targets.Length), "A full grid accepts only uniquely available gear slots");
            check(incoming.Select(item => item.Slot).SequenceEqual(targets) && incoming.All(item => item.Amount == 1),
                "Fit checks must never mutate source item snapshots");
        }
        Console.WriteLine("PASS: full-grid grave recovery, slot conflicts, native stack/position rules, owner tags and 1,000 randomized reservations.");
    }
}
