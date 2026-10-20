using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Ragnarok.HUD;

// The Ragnarök look for Valheim's action prompt and reticle, after the style guide: the four-armed reticle,
// and beside it the hovered thing's name in bold bone, quiet lines in parentheses ("(Fuel 0/6)") and the
// keys in the guide's yellow ("[E] Use"), unframed over a soft shadow. Its left edge is the chest peek's,
// so the two read as one column. Valheim keeps writing the text (so every hover object, key rebind and
// gamepad glyph is right); its own label is made transparent and copied each frame. Only with the
// Ragnarök HUD.
internal sealed class HoverPrompt : IDisposable
{
    // The prompt while it is on screen, for the chest peek to sit under; else null.
    internal static RectTransform? Shown;

    private static readonly Regex Quiet = new(@"^\s*\(.*\)\s*$", RegexOptions.Compiled);

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private RectTransform? root;
    private TextMeshProUGUI? text;
    private TextOutline? outline;
    private TextMeshProUGUI? source;
    private CanvasGroup? fade;
    private bool addedFade;
    private float vanillaAlpha;
    private UnityEngine.UI.Image? crosshair;
    private Sprite? vanillaCrosshair;
    private string last = "";
    private readonly string key = "<color=#" + ColorUtility.ToHtmlStringRGB(Palette.Key) + ">";
    private readonly string muted = "<color=#" + ColorUtility.ToHtmlStringRGBA(Palette.Muted) + ">";

    internal HoverPrompt(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        global::Hud hud = global::Hud.instance;
        if (settings.Hud.Value != HudStyle.Ragnarok || !hud || !hud.m_hoverName)
        {
            Dispose();
            return;
        }
        if (source != hud.m_hoverName)
        {
            Dispose();
            source = hud.m_hoverName;
        }
        HideVanilla();
        Reticle(hud);
        if (!root || root!.parent != hud.m_rootObject.transform) Build(hud.m_rootObject.transform);

        string value = source!.text ?? "";
        bool show = value.Length > 0 && source.gameObject.activeInHierarchy && !InventoryGui.IsVisible() && !WeaponDial.Open;
        if (root!.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        Shown = show ? root : null;
        if (!show) return;
        if (value != last)
        {
            last = value;
            string styled = Style(value);
            text!.text = styled;
            text.spriteAsset = source.spriteAsset;
            Vector2 size = text.GetPreferredValues();
            root.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 4, Mathf.Ceil(size.y) + 4);
        }
        Place();
        outline!.Sync();
    }

    // Keys yellow, a line that is all parentheses quiet, the rest bone.
    private string Style(string value)
    {
        StringBuilder result = new();
        string[] lines = value.Replace("<color=yellow>", key).Replace("<color=\"yellow\">", key).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) result.Append('\n');
            string plain = Regex.Replace(lines[i], "<.*?>", "");
            if (i > 0 && Quiet.IsMatch(plain)) result.Append(muted).Append(lines[i]).Append("</color>");
            else result.Append(lines[i]);
        }
        return result.ToString();
    }

    private void Build(Transform parent)
    {
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = Ugui.Node(parent, "Ragnarok Hover Prompt");
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = group.interactable = false;
        text = Ugui.Text(root, "Text", "", theme.Strong, 15, Palette.Bone);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 4;
        text.Stretch(0, 0, 0, 0);
        outline = new TextOutline(text, world: true);
        last = "";
    }

    // Left edge on the chest peek's, its first line level with the crosshair like Valheim's own prompt.
    private void Place()
    {
        RectTransform parent = (RectTransform)root!.parent;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0, 1);
        root.localScale = Vector3.one;
        float x = Mathf.Clamp(settings.ChestPreviewX.Value, -parent.rect.width / 2 + 8, parent.rect.width / 2 - root.sizeDelta.x - 8);
        root.anchoredPosition = new Vector2(x, 12);
    }

    // The guide's reticle in place of Valheim's dot; Valheim still colours it (yellow over something usable).
    private void Reticle(global::Hud hud)
    {
        if (crosshair == hud.m_crosshair) return;
        RestoreReticle();
        crosshair = hud.m_crosshair;
        if (!crosshair) return;
        vanillaCrosshair = crosshair!.sprite;
        crosshair.sprite = theme.Reticle;
    }

    private void RestoreReticle()
    {
        if (crosshair && crosshair!.sprite == theme.Reticle) crosshair.sprite = vanillaCrosshair;
        crosshair = null;
        vanillaCrosshair = null;
    }

    private void HideVanilla()
    {
        if (fade)
        {
            fade!.alpha = 0;
            return;
        }
        addedFade = !source!.TryGetComponent(out CanvasGroup existing);
        fade = addedFade ? source.gameObject.AddComponent<CanvasGroup>() : existing;
        vanillaAlpha = fade!.alpha;
        fade.alpha = 0;
    }

    public void Dispose()
    {
        if (fade)
        {
            if (addedFade) UnityEngine.Object.Destroy(fade);
            else fade!.alpha = vanillaAlpha;
        }
        fade = null;
        source = null;
        RestoreReticle();
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        root = null;
        text = null;
        outline = null;
        Shown = null;
    }
}
