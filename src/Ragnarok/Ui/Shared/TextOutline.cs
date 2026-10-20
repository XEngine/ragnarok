using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Ragnarok.UI;

// Offset silhouettes remain visible with the game's bundled TMP shader variants.
// They share the font material and are owned/destroyed by the same HUD root.
internal sealed class TextOutline
{
    private readonly TMP_Text source;
    private readonly TextMeshProUGUI[] edges;
    private readonly Vector2[] offsets;
    private static readonly Regex ColorTags = new(@"</?color(?:=[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private string sourceText, outlineText;

    internal TextOutline(TMP_Text source, bool world = false)
    {
        this.source = source;
        edges = new TextMeshProUGUI[world ? 17 : 8];
        offsets = new Vector2[edges.Length];
        sourceText = source.text;
        outlineText = ColorTags.Replace(sourceText, "");
        RectTransform layout = source.rectTransform;
        for (int i = 0; i < edges.Length; i++)
        {
            bool shadow = world && i < 9;
            float angle = (world ? i - 9 : i) * Mathf.PI / 4;
            Color ink = world ? Palette.Hex(0x0A0D0E, shadow ? 0.097f : 1) : Palette.Hex(0x29302F, 0.32f);
            TextMeshProUGUI edge = Ugui.Text(source.transform.parent, source.name + " outline " + i,
                source.richText ? outlineText : sourceText, source.font, source.fontSize, ink, source.alignment);
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = layout.anchorMin;
            rect.anchorMax = layout.anchorMax;
            rect.pivot = layout.pivot;
            rect.sizeDelta = layout.sizeDelta;
            offsets[i] = shadow ? new Vector2(0, -2) + (i == 8 ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3)
                : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (world ? 1 : 0.65f);
            rect.anchoredPosition = layout.anchoredPosition + offsets[i];
            edge.overflowMode = source.overflowMode;
            edge.textWrappingMode = source.textWrappingMode;
            edge.maxVisibleLines = source.maxVisibleLines;
            edge.enableAutoSizing = source.enableAutoSizing;
            edge.fontSizeMin = source.fontSizeMin;
            edge.fontSizeMax = source.fontSizeMax;
            edge.richText = source.richText;
            edge.fontWeight = source.fontWeight;
            edge.characterSpacing = source.characterSpacing;
            edge.lineSpacing = source.lineSpacing;
            edge.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
            edges[i] = edge;
        }
    }

    internal void Sync()
    {
        if (sourceText != source.text)
        {
            sourceText = source.text;
            outlineText = ColorTags.Replace(sourceText, "");
        }
        for (int i = 0; i < edges.Length; i++)
        {
            TextMeshProUGUI edge = edges[i];
            if (edge.gameObject.activeSelf != source.gameObject.activeSelf)
                edge.gameObject.SetActive(source.gameObject.activeSelf);
            edge.enabled = source.enabled;
            edge.text = source.richText ? outlineText : sourceText;
            if (edge.font != source.font) edge.font = source.font;
            if (edge.fontSize != source.fontSize) edge.fontSize = source.fontSize;
            if (edge.overflowMode != source.overflowMode) edge.overflowMode = source.overflowMode;
            if (edge.textWrappingMode != source.textWrappingMode) edge.textWrappingMode = source.textWrappingMode;
            if (edge.maxVisibleLines != source.maxVisibleLines) edge.maxVisibleLines = source.maxVisibleLines;
            if (edge.enableAutoSizing != source.enableAutoSizing) edge.enableAutoSizing = source.enableAutoSizing;
            if (edge.fontSizeMin != source.fontSizeMin) edge.fontSizeMin = source.fontSizeMin;
            if (edge.fontSizeMax != source.fontSizeMax) edge.fontSizeMax = source.fontSizeMax;
            if (edge.characterSpacing != source.characterSpacing) edge.characterSpacing = source.characterSpacing;
            if (edge.lineSpacing != source.lineSpacing) edge.lineSpacing = source.lineSpacing;
            if (edge.spriteAsset != source.spriteAsset) edge.spriteAsset = source.spriteAsset;
            RectTransform rect = edge.rectTransform;
            rect.anchorMin = source.rectTransform.anchorMin;
            rect.anchorMax = source.rectTransform.anchorMax;
            rect.pivot = source.rectTransform.pivot;
            edge.alignment = source.alignment;
            edge.richText = source.richText;
            edge.fontWeight = source.fontWeight;
            Vector2 target = source.rectTransform.anchoredPosition + offsets[i];
            if (rect.anchoredPosition != target) rect.anchoredPosition = target;
            if (rect.sizeDelta != source.rectTransform.sizeDelta) rect.sizeDelta = source.rectTransform.sizeDelta;
        }
    }
}
