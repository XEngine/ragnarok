using System;
using UnityEngine;

namespace Ragnarok.Ui;

// While a chest is open, the crafting panel steps aside: it fades out and stops catching clicks, so the
// inventory and the chest have the screen. Valheim keeps the panel alive underneath; closing the chest
// brings it back as it was. Only with the Ragnarök inventory.
internal sealed class ChestFocus : IDisposable
{
    private readonly Func<bool> wanted;
    private CanvasGroup? group;
    private bool added;
    private float alpha;
    private bool blocks, interactable;

    internal ChestFocus(Func<bool> wanted) => this.wanted = wanted;

    // Whether the crafting panel is hidden right now (the key guide then keeps its own place).
    internal bool Hiding => group;

    internal void Tick()
    {
        InventoryGui gui = InventoryGui.instance;
        bool chest = gui && wanted() && InventoryGui.IsVisible() && gui.m_container && gui.m_container.gameObject.activeInHierarchy &&
                     gui.m_crafting;
        if (!chest)
        {
            Dispose();
            return;
        }
        if (!group)
        {
            GameObject panel = gui!.m_crafting!.gameObject;
            added = !panel.TryGetComponent(out CanvasGroup existing);
            group = added ? panel.AddComponent<CanvasGroup>() : existing;
            alpha = group!.alpha;
            blocks = group.blocksRaycasts;
            interactable = group.interactable;
        }
        group!.alpha = 0;
        group.blocksRaycasts = group.interactable = false;
    }

    public void Dispose()
    {
        if (group)
        {
            if (added) UnityEngine.Object.Destroy(group);
            else
            {
                group!.alpha = alpha;
                group.blocksRaycasts = blocks;
                group.interactable = interactable;
            }
        }
        group = null;
    }
}
