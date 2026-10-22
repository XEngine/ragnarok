using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

// HUD widgets are created after the native map in the hierarchy. Give the large
// map its own sorting boundary, including its controls, instead of racing sibling
// order or hiding individual HUD features whenever the map is open.
internal sealed class LargeMapLayer : IDisposable
{
    private GameObject? owner;
    private Canvas? canvas;
    private GraphicRaycaster? raycaster;
    private bool ownsCanvas, ownsRaycaster, originalOverride;
    private int originalOrder, originalLayer;

    internal void Tick()
    {
        GameObject current = Minimap.instance ? Minimap.instance.m_largeRoot : null!;
        if (owner != current) { Dispose(); owner = current; }
        if (!current || !current.activeInHierarchy) return;
        if (!canvas)
        {
            ownsCanvas = !current.TryGetComponent(out Canvas existing);
            canvas = ownsCanvas ? current.AddComponent<Canvas>() : existing;
            originalOverride = canvas.overrideSorting;
            originalOrder = canvas.sortingOrder;
            originalLayer = canvas.sortingLayerID;
            // Graphics in a nested Canvas need a raycaster on that Canvas so
            // native map pins and controls retain their pointer interactions.
            ownsRaycaster = !current.TryGetComponent(out GraphicRaycaster existingRaycaster);
            raycaster = ownsRaycaster ? current.AddComponent<GraphicRaycaster>() : existingRaycaster;
        }
        Canvas parent = current.transform.parent.GetComponentInParent<Canvas>();
        if (parent) canvas!.sortingLayerID = parent.rootCanvas.sortingLayerID;
        canvas!.overrideSorting = true;
        canvas.sortingOrder = 32760; // Above Ironbound windows (32000) and gameplay HUD.
    }

    public void Dispose()
    {
        if (raycaster && ownsRaycaster)
        {
            raycaster!.enabled = false;
            UnityEngine.Object.Destroy(raycaster);
        }
        if (canvas)
        {
            canvas!.overrideSorting = originalOverride;
            canvas.sortingOrder = originalOrder;
            canvas.sortingLayerID = originalLayer;
            if (ownsCanvas) UnityEngine.Object.Destroy(canvas);
        }
        owner = null; canvas = null; raycaster = null;
        ownsCanvas = ownsRaycaster = false;
    }
}
