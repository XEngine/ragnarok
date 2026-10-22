using System;
using System.Collections.Generic;

namespace Ironbound.HUD;

// One visible pickup; matching arrivals add to its quantity. Other items wait in order.
internal sealed class PickupQueue
{
    internal sealed class Item
    {
        internal readonly string Name;
        internal int Amount, Revision;
        internal Item(string name, int amount) { Name = name; Amount = amount; }
    }

    private readonly Queue<Item> pending = new();
    internal Item? Current { get; private set; }
    internal bool HasItems => Current != null || pending.Count > 0;

    internal void Add(string name, int amount)
    {
        if (amount <= 0) return;
        Item? match = Current?.Name == name ? Current : null;
        if (match == null)
            foreach (Item item in pending)
                if (item.Name == name) { match = item; break; }
        if (match == null) pending.Enqueue(new Item(name, amount));
        else
        {
            match.Amount = (int)Math.Min(int.MaxValue, (long)match.Amount + amount);
            match.Revision++;
        }
    }

    internal Item? Next() => Current = pending.Count > 0 ? pending.Dequeue() : null;
    internal void Clear() { pending.Clear(); Current = null; }
}
