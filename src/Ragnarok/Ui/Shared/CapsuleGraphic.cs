using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI;

// A capsule at the current rect size: no sprite borders or clipped end caps.
// A narrow transparent fringe smooths the edge at small HUD sizes.
internal sealed class CapsuleGraphic : MaskableGraphic
{
    internal static CapsuleGraphic Create(Transform parent, string name, Color color)
    {
        CapsuleGraphic graphic = Ugui.Node(parent, name).gameObject.AddComponent<CapsuleGraphic>();
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        const int steps = 12, points = (steps + 1) * 2;
        float radius = Mathf.Min(rect.width, rect.height) / 2;
        float innerRadius = Mathf.Max(0, radius - 0.6f);
        Vector2 center = rect.center;
        mesh.AddVert(center, color, Vector2.zero);
        Color transparent = color;
        transparent.a = 0;
        for (int i = 0; i < points; i++)
        {
            bool right = i <= steps;
            float angle = (right ? -90 + i * 180f / steps : 90 + (i - steps - 1) * 180f / steps) * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 cap = new(right ? rect.xMax - radius : rect.xMin + radius, center.y);
            mesh.AddVert(cap + direction * innerRadius, color, Vector2.zero);
            mesh.AddVert(cap + direction * radius, transparent, Vector2.zero);
        }
        for (int i = 0; i < points; i++)
        {
            int inner = 1 + i * 2, next = 1 + ((i + 1) % points) * 2;
            mesh.AddTriangle(0, inner, next);
            mesh.AddTriangle(inner, inner + 1, next + 1);
            mesh.AddTriangle(inner, next + 1, next);
        }
    }
}
