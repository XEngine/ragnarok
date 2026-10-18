using TMPro;
using UnityEngine;

namespace Ragnarok.UI;

// Offset silhouettes remain visible with the game's bundled TMP shader variants.
// They share the font material and are owned/destroyed by the same HUD root.
internal sealed class TextOutline
{
    private readonly TMP_Text source;
    private readonly TextMeshProUGUI[] edges = new TextMeshProUGUI[8];
    private readonly Vector2[] offsets = new Vector2[8];

    internal TextOutline(TMP_Text source)
    {
        this.source = source;
        RectTransform layout = source.rectTransform;
        for (int i = 0; i < edges.Length; i++)
        {
            float angle = i * Mathf.PI / 4;
            TextMeshProUGUI edge = Ugui.Text(source.transform.parent, source.name + " outline " + i,
                source.text, source.font, source.fontSize, Palette.Hex(0x29302F, 0.32f), source.alignment);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = layout.anchorMin;
            rect.anchorMax = layout.anchorMax;
            rect.pivot = layout.pivot;
            rect.sizeDelta = layout.sizeDelta;
            offsets[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.65f;
            rect.anchoredPosition = layout.anchoredPosition + offsets[i];
            edge.overflowMode = source.overflowMode;
            edge.textWrappingMode = source.textWrappingMode;
            edge.maxVisibleLines = source.maxVisibleLines;
            edge.enableAutoSizing = source.enableAutoSizing;
            edge.fontSizeMin = source.fontSizeMin;
            edge.fontSizeMax = source.fontSizeMax;
            edge.richText = source.richText;
            edge.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            edges[i] = edge;
        }
    }

    internal void Sync()
    {
        for (int i = 0; i < edges.Length; i++)
        {
            TextMeshProUGUI edge = edges[i];
            if (edge.gameObject.activeSelf != source.gameObject.activeSelf)
                edge.gameObject.SetActive(source.gameObject.activeSelf);
            edge.enabled = source.enabled;
            edge.text = source.text;
            if (edge.overflowMode != source.overflowMode) edge.overflowMode = source.overflowMode;
            if (edge.textWrappingMode != source.textWrappingMode) edge.textWrappingMode = source.textWrappingMode;
            if (edge.maxVisibleLines != source.maxVisibleLines) edge.maxVisibleLines = source.maxVisibleLines;
            if (edge.enableAutoSizing != source.enableAutoSizing) edge.enableAutoSizing = source.enableAutoSizing;
            if (edge.fontSizeMin != source.fontSizeMin) edge.fontSizeMin = source.fontSizeMin;
            if (edge.fontSizeMax != source.fontSizeMax) edge.fontSizeMax = source.fontSizeMax;
            RectTransform rect = edge.rectTransform;
            Vector2 target = source.rectTransform.anchoredPosition + offsets[i];
            if (rect.anchoredPosition != target) rect.anchoredPosition = target;
            if (rect.sizeDelta != source.rectTransform.sizeDelta) rect.sizeDelta = source.rectTransform.sizeDelta;
        }
    }
}
