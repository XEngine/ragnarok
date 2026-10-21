using Ragnarok.HUD;

internal static class PickupQueueChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var queue = new PickupQueue();
        queue.Add("Wood", 2);
        queue.Add("Stone", 1);
        queue.Add("Wood", 3);
        var wood = queue.Next();
        check(wood?.Name == "Wood" && wood.Amount == 5, "Queued pickups did not merge in arrival order");
        queue.Add("Wood", 4);
        check(wood!.Amount == 9 && wood.Revision > 0, "Visible pickup did not merge/retrigger its animation");
        queue.Add("Stone", 2);
        var stone = queue.Next();
        check(stone?.Name == "Stone" && stone.Amount == 3, "Distinct queued item lost its quantity");
        check(queue.Next() == null && !queue.HasItems, "Finished pickups remained visible");
        queue.Add("Invalid", 0);
        queue.Add("Invalid", -1);
        check(!queue.HasItems, "Nonpositive pickup generated a notification");
        queue.Add("Ore", int.MaxValue);
        queue.Add("Ore", 1);
        check(queue.Next()!.Amount == int.MaxValue, "Pickup merge overflowed");
        queue.Add("Pending", 1);
        queue.Clear();
        check(!queue.HasItems && queue.Next() == null, "Unload/preview clear retained pickup state");
        var random = new Random(481);
        var expected = new Dictionary<string, int>();
        for (int i = 0; i < 1000; i++)
        {
            string name = "Item " + random.Next(12);
            int amount = random.Next(1, 20);
            queue.Add(name, amount);
            expected[name] = expected.GetValueOrDefault(name) + amount;
        }
        while (queue.Next() is { } item)
        {
            check(expected.Remove(item.Name, out int total) && total == item.Amount, "Pickup burst lost or duplicated quantities");
        }
        check(expected.Count == 0, "Pickup burst dropped an item");
        Console.WriteLine("PASS: pickup aggregation, visible count pulses, FIFO order, cleanup and 1,000-event burst.");
    }
}
