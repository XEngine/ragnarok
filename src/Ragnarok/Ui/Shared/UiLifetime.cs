using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI;

// Unity can destroy a scene object's native side while its managed owner remains.
// Use Unity's lifetime check before touching native properties during cleanup.
internal static class UiLifetime
{
    internal static void ResetAlpha(CanvasGroup? group) { if (group) group!.alpha = 0; }
    internal static void DetachTexture(RawImage? image) { if (image) image!.texture = null; }
    internal static void DestroyRoot(RectTransform? root)
    {
        if (!root) return;
        root!.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(root.gameObject);
    }

    // An unload must attempt every release, even if one component fails.
    internal static void Cleanup(Action release, Action<Exception> report)
    {
        try { release(); }
        catch (Exception exception) { report(exception); }
    }
}
