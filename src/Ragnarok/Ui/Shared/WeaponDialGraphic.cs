using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI;

// Analytic dial art: a small polar mesh replaces megabytes of rasterized textures.
// Image inheritance keeps the dial's existing colour/transform animation API.
internal sealed class WeaponDialGraphic : Image
{
    private float inner, outer, halfSpan, gap;
    private bool border, shadow;

    internal static WeaponDialGraphic Create(Transform parent, string name, Color color,
        float inner, float outer, int count = 1, float gap = 0, bool border = false, bool shadow = false)
    {
        var graphic = Ugui.Node(parent, name).gameObject.AddComponent<WeaponDialGraphic>();
        graphic.inner = inner;
        graphic.outer = outer;
        graphic.halfSpan = Mathf.PI / count;
        graphic.gap = gap;
        graphic.border = border;
        graphic.shadow = shadow;
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (outer <= inner) return;
        bool sector = halfSpan < Mathf.PI;
        int steps = Mathf.Max(8, Mathf.CeilToInt(halfSpan * 2 * outer / 8));
        int columns = sector ? steps + 7 : steps + 1;
        int rows = shadow ? 25 : inner > 0 ? 8 : 5;
        float scale = rectTransform.rect.width / (2 * (outer + 2));
        Vector2 center = rectTransform.rect.center;
        for (int row = 0; row < rows; row++)
        {
            float radius = Radius(row);
            float limit = halfSpan - gap / (2 * Mathf.Max(1, radius));
            for (int col = 0; col < columns; col++)
            {
                float angle;
                if (!sector) angle = Mathf.Lerp(-Mathf.PI, Mathf.PI, col / (float)steps);
                else
                {
                    // Sample the feather and both sides of the border explicitly.
                    float fringe = 0.5f / Mathf.Max(1, radius), stroke = 3 / Mathf.Max(1, radius);
                    if (col == 0) angle = -limit - fringe;
                    else if (col == 1) angle = -limit + fringe;
                    else if (col == 2) angle = -limit + 2 / Mathf.Max(1, radius);
                    else if (col == columns - 3) angle = limit - 2 / Mathf.Max(1, radius);
                    else if (col == columns - 2) angle = limit - fringe;
                    else if (col == columns - 1) angle = limit + fringe;
                    else angle = Mathf.Lerp(-limit + stroke, limit - stroke, (col - 3) / (float)steps);
                }
                float distance = outer - radius;
                if (inner > 0) distance = Mathf.Min(distance, radius - inner);
                if (sector) distance = Mathf.Min(distance, (halfSpan - Mathf.Abs(angle)) * radius - gap / 2);
                float alpha = Mathf.Clamp01(distance + 0.5f);
                if (border) alpha *= Mathf.Clamp01(2.5f - distance);
                if (shadow) alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((outer - radius) / 150));
                mesh.AddVert(center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (radius * scale),
                    color.WithAlpha(color.a * alpha), Vector2.zero);
                if (row == 0 || col == 0) continue;
                int index = row * columns + col;
                mesh.AddTriangle(index - columns - 1, index - columns, index);
                mesh.AddTriangle(index - columns - 1, index, index - 1);
            }
        }
    }

    private float Radius(int row)
    {
        if (shadow) return outer * row / 24;
        if (inner <= 0)
            return row switch { 0 => 0, 1 => outer - 3, 2 => outer - 2, 3 => outer - 0.5f, _ => outer + 0.5f };
        return row switch
        {
            0 => inner - 0.5f, 1 => inner + 0.5f, 2 => inner + 2, 3 => inner + 3,
            4 => outer - 3, 5 => outer - 2, 6 => outer - 0.5f, _ => outer + 0.5f
        };
    }
}
