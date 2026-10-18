using System;
using System.Collections.Generic;
using System.Linq;

namespace Ragnarok.NearbyStorage;

internal sealed class ResourceScope : IDisposable
{
    [ThreadStatic] internal static ResourceScope? Current;
    private readonly ResourceScope? previous;
    private readonly ContainerRegistry registry;
    private readonly List<Container> containers;
    internal readonly Player Player;
    internal readonly Inventory Inventory;
    internal readonly bool Execute;
    private bool pending;

    internal ResourceScope(Player player, ContainerRegistry registry, bool execute)
    {
        previous = Current;
        Player = player;
        Inventory = player.GetInventory();
        this.registry = registry;
        Execute = execute || (previous?.Execute ?? false);
        containers = previous != null && previous.Player == player ? previous.containers : registry.Nearby(player);
        foreach (Container container in containers) registry.Refresh(container);
        Current = this;
    }

    internal bool Matches(Inventory inventory) => ReferenceEquals(Inventory, inventory);

    internal IEnumerable<Inventory> Sources(string? name = null, int quality = -1, bool matchWorldLevel = true)
    {
        foreach (Container container in containers)
        {
            if (!registry.CanAccess(container, Player)) continue;
            Inventory inventory = container.GetInventory();
            if (Execute && !registry.IsOwned(container))
            {
                if (name != null && inventory.CountItems(name, quality, matchWorldLevel) > 0)
                {
                    registry.Request(container, Player);
                    pending = true;
                }
                continue;
            }
            yield return inventory;
        }
    }

    internal int Count(string name, int quality, bool matchWorldLevel) =>
        Sources(name, quality, matchWorldLevel).Sum(i => i.CountItems(name, quality, matchWorldLevel));

    internal ItemDrop.ItemData? Find(string name, int quality, bool isPrefabName)
    {
        foreach (Container container in containers)
        {
            if (!registry.CanAccess(container, Player)) continue;
            ItemDrop.ItemData item = container.GetInventory().GetItem(name, quality, isPrefabName);
            if (item == null) continue;
            if (Execute && !registry.IsOwned(container))
            {
                registry.Request(container, Player);
                pending = true;
                continue;
            }
            return item;
        }
        return null;
    }

    internal void Remove(string name, int amount, int quality, bool matchWorldLevel)
    {
        List<Inventory> sources = new() { Inventory };
        sources.AddRange(Sources(name, quality, matchWorldLevel));
        try { InventoryWithdrawal.Remove(sources, name, amount, quality, matchWorldLevel); }
        catch (InvalidOperationException exception)
        {
            // Diagnostics for a shortfall seen in play: what the scope could see when payment failed.
            int scoped = Count(name, quality, matchWorldLevel);
            int owned = containers.Count(c => c && registry.IsOwned(c));
            throw new InvalidOperationException(exception.Message +
                $" scopeCount={scoped} execute={Execute} nested={previous != null} chests={containers.Count} owned={owned} pending={pending} frame={UnityEngine.Time.frameCount}",
                exception);
        }
    }

    internal bool Remove(ItemDrop.ItemData item, int amount)
    {
        foreach (Inventory source in Sources())
            if (source.ContainsItem(item)) return source.RemoveItem(item, amount);
        return false;
    }

    public void Dispose()
    {
        Current = previous;
        if (pending)
        {
            if (previous != null) previous.pending = true;
            else registry.ShowPending(Player);
        }
    }
}
