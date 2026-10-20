using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.UI;

// One fill and one continuous contour: no stacked panel/repair plates or joining seam.
internal sealed class CraftFrameGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    private float wingWidth, wingTop, wingHeight;
    private bool wing, border;
    private const float Stroke = 0.8f, Fringe = 0.6f, Row = 0.75f;

    internal static CraftFrameGraphic Create(Transform parent, string name, float wingWidth,
        float wingTop, float wingHeight, bool border, Color color)
    {
        var frame = Ugui.Node(parent, name).gameObject.AddComponent<CraftFrameGraphic>();
        frame.wingWidth = wingWidth;
        frame.wingTop = wingTop;
        frame.wingHeight = wingHeight;
        frame.border = border;
        frame.color = color;
        frame.raycastTarget = !border;
        frame.Stretch(left: -wingWidth);
        return frame;
    }

    internal void ShowWing(bool visible)
    {
        if (wing == visible) return;
        wing = visible;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float width = rect.width - wingWidth, height = rect.height;
        if (width <= 0 || height <= 0) return;
        float origin = rect.xMin + wingWidth;
        float row = Mathf.Max(Row, height / 1600);
        for (float y = 0; y < height; y += row)
        {
            float end = Mathf.Min(y + row, height), sample = (y + end) / 2;
            if (!CraftFrameShape.Bounds(width, height, wingWidth, wingTop, wingHeight, wing, sample, 0, out float left, out float right)) continue;
            if (!border || !CraftFrameShape.Bounds(width, height, wingWidth, wingTop, wingHeight, wing, sample, Stroke, out float insideLeft, out float insideRight))
                Band(mesh, origin + left, origin + right, rect.yMax - y, rect.yMax - end);
            else
            {
                Band(mesh, origin + left, origin + insideLeft, rect.yMax - y, rect.yMax - end);
                Band(mesh, origin + insideRight, origin + right, rect.yMax - y, rect.yMax - end);
            }
        }
    }

    private void Band(VertexHelper mesh, float left, float right, float top, float bottom)
    {
        if (right <= left) return;
        float fringe = Mathf.Min(Fringe, (right - left) / 3);
        Color clear = color.WithAlpha(0);
        Quad(mesh, left, left + fringe, top, bottom, clear, color);
        Quad(mesh, left + fringe, right - fringe, top, bottom, color, color);
        Quad(mesh, right - fringe, right, top, bottom, color, clear);
    }

    private static void Quad(VertexHelper mesh, float left, float right, float top, float bottom, Color a, Color b)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, top), a, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), b, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), b, Vector2.zero);
        mesh.AddVert(new Vector3(left, bottom), a, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    // The graphic's bounding box includes unused space above/below the wing;
    // that space must pass clicks through to neighboring inventory panels.
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
        Rect rect = rectTransform.rect;
        float x = point.x - rect.xMin - wingWidth, y = rect.yMax - point.y;
        return CraftFrameShape.Bounds(rect.width - wingWidth, rect.height, wingWidth, wingTop, wingHeight,
            wing, y, 0, out float left, out float right) && x >= left && x <= right;
    }
}
