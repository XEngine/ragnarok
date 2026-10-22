using UnityEngine;

namespace Ironbound.HUD;

internal static class NotificationPlacement
{
    internal static void BesideMinimap(RectTransform panel, RectTransform parent, Vector3[] corners)
    {
        float right = parent.rect.xMax - 230, top = parent.rect.yMax - 80;
        Minimap map = Minimap.instance;
        if (map && map.m_smallRoot.activeInHierarchy)
        {
            ((RectTransform)map.m_smallRoot.transform).GetWorldCorners(corners);
            right = parent.InverseTransformPoint(corners[0]).x - 16;
            top = parent.InverseTransformPoint(corners[1]).y - 16;
        }
        float left = right - panel.rect.width * panel.localScale.x;
        RectTransform? compass = CompassStrip.Shown;
        if (compass && compass!.gameObject.activeInHierarchy)
        {
            compass.GetWorldCorners(corners);
            if (left < parent.InverseTransformPoint(corners[2]).x + 12)
                top = Mathf.Min(top, parent.InverseTransformPoint(corners[0]).y - 16);
        }
        panel.position = parent.TransformPoint(new Vector3(right, top, 0));
        Utils.ClampUIToScreen(panel);
    }
}
