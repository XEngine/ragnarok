using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// A temporary marker for the already-authorized selected search result. The caller
// rechecks access/range every frame; this layer never enumerates chests or alters fog.
internal sealed class ChestSearchMapOverlay : IDisposable
{
    private const string Name = "Ragnarok Chest Search";
    private MinimapManager.MapOverlay? overlay;
    private Texture2D? texture;
    private Minimap? map;
    private int x = int.MinValue, y;
    private bool failed;
    private bool guideActive;

    internal void Show(Vector3 position)
    {
        if (failed || !Minimap.instance) return;
        try
        {
            if (map != Minimap.instance) Dispose();
            if (overlay == null)
            {
                var manager = MinimapManager.Instance;
                // F6 can initialize this manager after Minimap.Start already ran.
                if (AccessTools.Field(typeof(MinimapManager), "OverlayPanel").GetValue(manager) == null)
                    AccessTools.Method(typeof(MinimapManager), "Minimap_Start").Invoke(manager, null);
                if (manager.GetOverlayNames().Contains(Name)) throw new InvalidOperationException("Chest search overlay is already registered.");
                map = Minimap.instance;
                overlay = manager.GetMapOverlay(Name, ignoreFog: false);
                texture = overlay.OverlayTex;
                texture.SetPixels32(new Color32[texture.width * texture.height]);
                x = int.MinValue;
            }
            Vector2 point = MinimapManager.Instance.WorldToOverlayCoords(position, texture!.width);
            int nextX = Mathf.RoundToInt(point.x), nextY = Mathf.RoundToInt(point.y);
            if (x != nextX || y != nextY)
            {
                Draw(x, y, Color.clear);
                x = nextX; y = nextY;
                Draw(x, y, new Color(1f, 0.8f, 0.18f, 1f));
                texture.Apply(false, false);
            }
            if (!guideActive) { overlay.Enabled = true; guideActive = true; }
        }
        catch (Exception exception)
        {
            failed = true;
            try { Dispose(); } catch (Exception cleanup) { Debug.LogWarning("[Ragnarok] Map cleanup failed: " + cleanup.Message); }
            Debug.LogWarning("[Ragnarok] Chest-search map overlay disabled: " + exception.Message);
        }
    }

    private void Draw(int centerX, int centerY, Color color)
    {
        if (centerX == int.MinValue || !texture) return;
        for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int px = centerX + dx, py = centerY + dy;
                if (Math.Abs(dx) + Math.Abs(dy) > 3 || px < 0 || py < 0 || px >= texture!.width || py >= texture.height) continue;
                texture.SetPixel(px, py, color);
            }
    }

    internal void Hide()
    {
        guideActive = false;
        if (overlay != null && map && map == Minimap.instance) overlay.Enabled = false;
    }

    public void Dispose()
    {
        if (overlay == null) return;
        var owned = overlay;
        overlay = null;
        try
        {
            // RemoveMapOverlay only removes the registry entry in Jotunn 2.30.2.
            // Its texture and toggle still belong to this layer and need explicit cleanup.
            if (map && map == Minimap.instance && MinimapManager.Instance.GetOverlayNames().Contains(Name) &&
                ReferenceEquals(MinimapManager.Instance.GetMapOverlay(Name), owned))
            {
                owned.Enabled = false;
                MinimapManager.Instance.RemoveMapOverlay(Name);
                // Redraw now so the old marker does not remain in the shared composite.
                var redraw = (IEnumerator)AccessTools.Method(typeof(MinimapManager), "DrawOverlay").Invoke(MinimapManager.Instance, null);
                try { redraw.MoveNext(); } finally { (redraw as IDisposable)?.Dispose(); }
            }
        }
        catch (Exception exception) { Debug.LogWarning("[Ragnarok] Could not redraw map after overlay removal: " + exception.Message); }
        finally
        {
            var toggle = AccessTools.Field(typeof(MinimapManager.MapOverlayBase), "Toggle")?.GetValue(owned) as Toggle;
            if (toggle)
            {
                toggle!.onValueChanged.RemoveAllListeners();
                toggle.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(toggle.gameObject);
            }
            if (texture) UnityEngine.Object.Destroy(texture);
            texture = null; map = null; x = int.MinValue; guideActive = false;
        }
    }
}
