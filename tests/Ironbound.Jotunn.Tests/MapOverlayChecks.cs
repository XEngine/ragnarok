using System;
using Jotunn.Managers;
using Ironbound.Features.QualityOfLife;
using UnityEngine;

internal static class MapOverlayChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var manager = MinimapManager.Instance;
        var foreign = manager.GetMapOverlay("AnotherMod");
        foreign.OverlayTex.SetPixel(0, 0, new Color(1, 1, 1, 1));
        for (int round = 0; round < 3; round++)
        {
            var marker = new ChestSearchMapOverlay();
            marker.Show(new Vector3(10, 0, 10));
            var overlay = manager.Overlays["Ironbound Chest Search"];
            var texture = overlay.OverlayTex;
            Check(!overlay.IgnoreFog, "marker respects exploration fog");
            Check(texture.Pixels.Count == 25, "diamond marker is drawn");
            marker.Show(new Vector3(10, 0, 10));
            Check(texture.Applies == 1, "unchanged target does not upload texture each frame");
            overlay.Enabled = false;
            marker.Show(new Vector3(10, 0, 10));
            Check(!overlay.Enabled, "user's Jotunn overlay visibility choice is preserved during a guide");
            marker.Show(new Vector3(20, 0, 20));
            Check(texture.Pixels.Count == 25 && !texture.Pixels.ContainsKey((10, 10)), "old marker erased on target change");
            marker.Show(new Vector3(0, 0, 0));
            Check(texture.Pixels.Count == 10, "edge marker is clipped safely");
            marker.Hide();
            Check(!overlay.Enabled, "expiry/access loss hides overlay");
            marker.Show(new Vector3(10, 0, 10));
            Check(overlay.Enabled, "new guide reuses owned layer");
            marker.Dispose(); marker.Dispose();
            Check(manager.Overlays.Count == 1 && manager.Overlays.ContainsKey("AnotherMod"), "unload removes only owned layer");
            Check(texture.Destroyed && overlay.Toggle.gameObject.Destroyed && overlay.Toggle.onValueChanged.Listeners == 0, "owned texture/toggle/listeners cleaned");
            Check(manager.CompositePixels == 1, "unload redraw removes stale marker while preserving other overlays");
        }
        Check(manager.Starts == 1, "late-load minimap initialization is performed once");
        manager.Overlays.Clear();
        Console.WriteLine("PASS: " + checks + " map overlay drawing, clipping, bounded uploads and unload checks.");
    }
}
