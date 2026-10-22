using System;
using System.Collections.Generic;

namespace Ironbound.Features.Equipment;

// Read-only prediction of slot restoration followed by Valheim's two-pass MoveAll.
// Does not mutate inventories or assume that unused carry weight provides grid space.
internal sealed class GraveRecoveryPlan
{
    internal readonly struct Item
    {
        internal readonly string Name;
        internal readonly int Amount, Limit, Quality, MaxQuality, WorldLevel, X, Y, Slot;
        internal readonly bool TopFirst;
        internal Item(string name, int amount, int limit, int quality, int maxQuality, int worldLevel,
            int x, int y, bool topFirst, int slot = -1)
        {
            Name = name; Amount = amount; Limit = limit; Quality = quality; MaxQuality = maxQuality;
            WorldLevel = worldLevel; X = x; Y = y; TopFirst = topFirst; Slot = slot;
        }
    }

    private sealed class Stack
    {
        internal Item Item;
        internal int Amount, X, Y;
        internal Stack(Item item, int x, int y, int amount)
        { Item = item; X = x; Y = y; Amount = amount; }
    }

    internal readonly int[] Slots;
    internal readonly bool AllFit;

    private GraveRecoveryPlan(int[] slots, bool allFit) { Slots = slots; AllFit = allFit; }

    internal static GraveRecoveryPlan Create(int width, int height, IReadOnlyList<Item> carried, IReadOnlyList<Item> grave)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var stacks = new List<Stack>();
        var occupied = new HashSet<int>();
        foreach (Item item in carried)
        {
            stacks.Add(new Stack(item, item.X, item.Y, item.Amount));
            if (item.Slot >= 0) occupied.Add(item.Slot);
        }
        int[] slots = new int[grave.Count], remaining = new int[grave.Count];
        for (int i = 0; i < grave.Count; i++)
        {
            slots[i] = -1;
            Item item = grave[i];
            if (item.Amount > 0 && item.Slot >= 0 && occupied.Add(item.Slot))
            {
                slots[i] = item.Slot;
                // Reserved character slots participate in stacking, but never occupy a grid cell.
                stacks.Add(new Stack(item, -1, -1, item.Amount));
            }
            else remaining[i] = Math.Max(0, item.Amount);
        }
        // First try the grave's saved grid positions, including partial stack merges.
        for (int i = 0; i < grave.Count; i++)
        {
            Item item = grave[i];
            if (remaining[i] == 0 || item.X < 0 || item.Y < 0 || item.X >= width || item.Y >= height) continue;
            Stack? target = At(stacks, item.X, item.Y);
            if (target == null)
            {
                stacks.Add(new Stack(item, item.X, item.Y, remaining[i]));
                remaining[i] = 0;
            }
            else if (target.Item.Name == item.Name && target.Item.WorldLevel == item.WorldLevel &&
                (target.Item.MaxQuality <= 1 || target.Item.Quality == item.Quality))
                Merge(target, ref remaining[i]);
        }
        // Then use matching partial stacks and the normal top/bottom-first free-cell search.
        bool fit = true;
        for (int i = 0; i < grave.Count; i++)
        {
            Item item = grave[i];
            if (remaining[i] == 0) continue;
            if (item.Limit > 1)
                foreach (Stack target in stacks)
                    if (target.Item.Name == item.Name && target.Item.Quality == item.Quality &&
                        target.Item.WorldLevel == item.WorldLevel) Merge(target, ref remaining[i]);
            if (remaining[i] == 0) continue;
            bool placed = false;
            for (int row = 0; row < height && !placed; row++)
            {
                int y = item.TopFirst ? row : height - row - 1;
                for (int x = 0; x < width; x++)
                {
                    if (At(stacks, x, y) != null) continue;
                    stacks.Add(new Stack(item, x, y, remaining[i]));
                    remaining[i] = 0;
                    placed = true;
                    break;
                }
            }
            if (!placed) fit = false;
        }
        return new GraveRecoveryPlan(slots, fit);
    }

    private static void Merge(Stack target, ref int remaining)
    {
        int amount = Math.Min(remaining, Math.Max(0, target.Item.Limit - target.Amount));
        target.Amount += amount;
        remaining -= amount;
    }

    private static Stack? At(List<Stack> stacks, int x, int y)
    {
        foreach (Stack stack in stacks) if (stack.X == x && stack.Y == y) return stack;
        return null;
    }
}
