using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Keep HotkeyBar.Update alive for input/selection. Only hide its rendering and mouse targets;
// the replacement uses the same inventory row and forwards touch clicks to vanilla's handler.
internal sealed class RagnarokActionBar : IDisposable
{
    // Pad leaves room at both sides (so the slots stay centred) for the inventory badge at the right.
    private const float Slot = 58, Pad = 96, ContentInset = 10, HeaderBaseline = 14;
    private float Top => 30 + PaddingY;
    private float Gap => settings.ActionBarSpacing.Value;
    private float PaddingX => settings.ActionBarPaddingX.Value;
    private float PaddingY => settings.ActionBarPaddingY.Value;
    private float Start => Pad + PaddingX;
    private float Width => Pad * 2 + PaddingX * 2 + Slot * 8 + Gap * 7;
    private float Height => Top + Slot + PaddingY;
    private Vector3 builtMargins;
    private Vector3 Margins => new(Gap, PaddingX, PaddingY);
    private static readonly AccessTools.FieldRef<HotkeyBar, int> Selected = AccessTools.FieldRefAccess<HotkeyBar, int>("m_selected");
    private static readonly MethodInfo Click = AccessTools.Method(typeof(HotkeyBar), "ElementClicked");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<Cell> cells = new();
    private readonly List<TextOutline> textOutlines = new();
    private HotkeyBar? original;
    private CanvasGroup? mask;
    private bool ownsMask, oldBlocks, oldInteractable;
    private float oldAlpha;
    private RectTransform? root;
    internal static RectTransform? EquippedCard { get; private set; }
    internal static RectTransform? Shown { get; private set; }
    internal static float KeyDockInset { get; private set; }
    private static float requestedGuideSpace;
    private float guideLift, guideVelocity;
    private TMP_Text? inventoryKey;
    private float nextKeyRefresh;

    private sealed class Cell
    {
        internal RectTransform Rect = null!;
        internal Image Background = null!, LevelStar = null!, Edge = null!, Cooldown = null!, Icon = null!, Ring = null!, Equipped = null!, DurabilityTrack = null!, Durability = null!;
        internal TMP_Text Timer = null!, Key = null!, Count = null!, Quality = null!, Queued = null!;
    }

    internal RagnarokActionBar(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
    }

    internal void Tick()
    {
        // The HUD measures its guide later in the frame; consume that request once next frame.
        float guideSpace = requestedGuideSpace;
        requestedGuideSpace = 0;
        var hud = global::Hud.instance;
        var bar = hud ? hud.GetComponentInChildren<HotkeyBar>(true) : null;
        if (settings.Hud.Value != HudStyle.Ragnarok || !settings.ActionBar.Value || !bar)
        {
            Dispose();
            return;
        }
        if (original != bar || !root || builtMargins != Margins)
        {
            Dispose();
            original = bar;
            Build(hud!.m_rootObject.transform);
            mask = bar!.GetComponent<CanvasGroup>();
            ownsMask = !mask;
            if (!mask) mask = bar.gameObject.AddComponent<CanvasGroup>();
            oldAlpha = mask!.alpha;
            oldBlocks = mask.blocksRaycasts;
            oldInteractable = mask.interactable;
        }
        mask!.alpha = 0;
        mask.blocksRaycasts = false;
        mask.interactable = false;
        var player = Player.m_localPlayer;
        bool show = player && !player.IsDead() && original!.gameObject.activeInHierarchy;
        root!.gameObject.SetActive(show);
        Shown = show ? root : null;
        KeyDockInset = show ? PaddingY : 0;
        if (!show) return;
        var parent = (RectTransform)root.parent;
        float scale = Math.Min(settings.ActionBarScale.Value, (parent.rect.width - 20) / Width);
        root.localScale = Vector3.one * scale;
        float limit = Math.Max(0, (parent.rect.width - Width * scale) / 2 - 8);
        float maxBottom = Mathf.Max(0, parent.rect.height - Height * scale);
        float bottom = Mathf.Clamp(settings.ActionBarBottom.Value, 0, maxBottom);
        float wantedLift = Mathf.Clamp(guideSpace - PaddingY * scale - bottom, 0, maxBottom - bottom);
        guideLift = Mathf.SmoothDamp(guideLift, wantedLift, ref guideVelocity, 0.16f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(guideLift - wantedLift) < 0.05f && Mathf.Abs(guideVelocity) < 0.05f)
        {
            guideLift = wantedLift;
            guideVelocity = 0;
        }
        root.anchoredPosition = new Vector2(Mathf.Clamp(settings.ActionBarX.Value, -limit, limit),
            Mathf.Clamp(bottom + guideLift, 0, maxBottom));
        bool gamepad = ZInput.IsGamepadActive();
        // The inventory key sits beside the bar; armor and weight live under the profile.
        Inventory inventory = player!.GetInventory();
        if (Time.unscaledTime >= nextKeyRefresh)
        {
            nextKeyRefresh = Time.unscaledTime + 1f;
            inventoryKey!.text = KeyName("Inventory", "TAB");
        }
        EquippedCard = null;
        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            var item = inventory.GetItemAt(i, 0);
            bool equipped = item != null && player.IsItemEquiped(item);
            bool breaking = item != null && item.m_shared.m_useDurability && item.GetDurabilityPercentage() < 0.1f;
            if (equipped && (EquippedCard == null || item == player.GetCurrentWeapon())) EquippedCard = cell.Rect;
            cell.Key.text = gamepad || item == null ? "" : (i + 1).ToString();
            // Keep the button's raycast/input surface while making empty slots transparent.
            // State fills replace the base well; stacking translucent wells makes them opaque.
            cell.Background.color = item == null || equipped || breaking ? Color.clear : Color.white;
            cell.Icon.enabled = item != null;
            cell.Icon.sprite = item?.GetIcon();
            cell.Edge.sprite = item == null ? theme.HudActionEmptyEdge : theme.HudActionEdge;
            cell.Edge.color = item == null ? Palette.Hex(0x64695E, 0.65f) : Palette.Hex(0x626B64, 0.45f);
            float remaining = 0, duration = 0;
            StatusEffect? consume = item?.m_shared.m_consumeStatusEffect;
            if (consume)
            {
                foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
                    if ((effect.NameHash() == consume!.NameHash() ||
                        (!string.IsNullOrEmpty(consume.m_category) && effect.m_category == consume.m_category)) &&
                        effect.GetRemaningTime() > remaining)
                    {
                        remaining = effect.GetRemaningTime();
                        duration = effect.m_ttl;
                    }
            }
            cell.Cooldown.enabled = remaining > 0;
            cell.Cooldown.fillAmount = duration > 0 ? Mathf.Clamp01(remaining / duration) : 0;
            cell.Timer.text = remaining > 0 ? StatusEffect.GetTimeString(remaining) : "";
            cell.Icon.color = Color.white.WithAlpha(remaining > 0 ? 0.35f : 1);
            cell.Count.text = item != null && item.m_shared.m_maxStackSize > 1 ? item.m_stack.ToString() : "";
            string quality = item != null && item.m_quality > 1 ? item.m_quality.ToString() : "";
            if (cell.Quality.text != quality)
            {
                cell.Quality.text = quality;
                float digitsWidth = Mathf.Ceil(cell.Quality.GetPreferredValues(quality).x);
                cell.LevelStar.rectTransform.anchoredPosition = new Vector2(Slot - ContentInset - digitsWidth - 9, -(HeaderBaseline - 7));
            }
            cell.LevelStar.enabled = quality.Length > 0;
            cell.Queued.text = item != null && player.IsEquipActionQueued(item) ? "…" : "";
            cell.Rect.anchoredPosition = new Vector2(Start + i * (Slot + Gap),
                Mathf.Lerp(cell.Rect.anchoredPosition.y, -Top + (equipped ? 6 : 0), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime)));
            cell.Equipped.enabled = equipped || breaking;
            cell.Equipped.color = breaking ? Palette.Hex(0x291D1B, Palette.HudGlassOpacity) : Palette.Hex(0x2B271B, Palette.HudGlassOpacity);
            cell.Key.color = breaking ? Palette.Bone : Palette.Hex(0x9A978B);
            cell.LevelStar.color = Palette.GoldBright;
            cell.Ring.enabled = breaking || equipped || (gamepad && Selected(original!) == i);
            cell.Ring.sprite = item == null ? theme.HudActionEmptyEdge : theme.HudActionEdge;
            cell.Ring.color = breaking ? Palette.Hex(0xA43E35)
                : gamepad && Selected(original!) == i ? Palette.Bone : Palette.Hex(0xD4A955);
            bool durable = item != null && item.m_shared.m_useDurability;
            cell.DurabilityTrack.enabled = cell.Durability.enabled = durable;
            if (durable)
            {
                float value = Mathf.Clamp01(item!.GetDurabilityPercentage());
                cell.Durability.rectTransform.sizeDelta = new Vector2((Slot - ContentInset * 2) * value, 2);
                cell.Durability.color = breaking ? Palette.Health : value < 0.25f ? Palette.Ember : Palette.Success;
            }
        }
        foreach (TextOutline outline in textOutlines) outline.Sync();
    }

    // Reserve space for held-item hints without changing the user's saved bottom margin.
    internal static void MakeRoomForKeys(float space)
    {
        if (Shown) requestedGuideSpace = Mathf.Max(0, space);
    }

    private void Build(Transform parent)
    {
        root = Ugui.Node(parent, "Ragnarok Action Bar");
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0);
        root.sizeDelta = new Vector2(Width, Height);
        builtMargins = Margins;
        nextKeyRefresh = 0;
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            var button = Ugui.Button(root, "Action " + (i + 1), theme.HudActionSlot, theme.HudActionSlot, () =>
            {
                if (Player.m_localPlayer && original && !UiState.WindowOpen)
                    Click.Invoke(original, new object[] { Player.m_localPlayer, index });
            }).At(Start + i * (Slot + Gap), Top, Slot, Slot);
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Palette.HudGlass;
            colors.highlightedColor = Palette.Hex(0x1C2221, 0.68f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Palette.Hex(0x292F2B, 0.72f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            var parentCell = button.transform;
            Image edge = Ugui.Image(parentCell, "Slot edge", theme.HudActionEdge, Palette.Hex(0x626B64, 0.45f)).Stretch();
            // State fill sits behind the item and labels, keeping their artwork readable.
            Image equippedFill = Ugui.Image(parentCell, "Equipped or breaking", theme.HudActionSlot, Palette.Hex(0x2B271B, Palette.HudGlassOpacity)).Stretch();
            var cell = new Cell
            {
                Rect = (RectTransform)button.transform,
                Background = button.image,
                Edge = edge,
                Icon = Ugui.ItemIcon(Ugui.Image(parentCell, "Item", null, Color.white)).At(11, 10, Slot - 22, Slot - 22),
                Ring = Ugui.Image(parentCell, "Active Border", theme.HudActionEdge, Palette.Hex(0xD4A955)).Stretch(),
                Equipped = equippedFill,
                Cooldown = Ugui.Image(parentCell, "Cooldown sweep", theme.HudActionSlot, Color.black.WithAlpha(0.35f)).Stretch(),
                Timer = Ugui.Text(parentCell, "Cooldown remaining", "", theme.Numbers, 14, Palette.Bone, TextAlignmentOptions.Center).Stretch(),
                DurabilityTrack = Ugui.Image(parentCell, "Durability track", theme.White, Palette.Hex(0x343833)).At(ContentInset, Slot - 10, Slot - ContentInset * 2, 2),
                Durability = Ugui.Image(parentCell, "Durability", theme.White, Palette.Success).At(ContentInset, Slot - 10, Slot - ContentInset * 2, 2),
                Key = Ugui.Text(parentCell, "Key", "", theme.Strong, 11, Palette.Muted, TextAlignmentOptions.BaselineLeft).At(ContentInset, HeaderBaseline, 16, 0),
                Count = Ugui.Text(parentCell, "Stack", "", theme.Numbers, 13, Palette.Bone, TextAlignmentOptions.BottomRight).At(4, 32, Slot - 8, 19),
                LevelStar = Ugui.Image(parentCell, "Level star", theme.HudStar, Palette.GoldBright).At(Slot - ContentInset - 15, HeaderBaseline - 7, 7, 7),
                Quality = Ugui.Text(parentCell, "Quality", "", theme.Numbers, 10, Palette.GoldBright, TextAlignmentOptions.BaselineRight).At(Slot - ContentInset - 18, HeaderBaseline, 18, 0),
                Queued = Ugui.Text(parentCell, "Queued", "", theme.Strong, 18, Palette.Bone, TextAlignmentOptions.Center).At(15, 14, 28, 20)
            };
            cell.Key.overflowMode = cell.Quality.overflowMode = TextOverflowModes.Overflow;
            cell.Cooldown.type = Image.Type.Filled;
            cell.Cooldown.fillMethod = Image.FillMethod.Radial360;
            cell.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            foreach (TMP_Text label in new[] { cell.Key, cell.Quality, cell.Count, cell.Timer, cell.Queued })
                textOutlines.Add(new TextOutline(label));
            cells.Add(cell);
        }

        float right = Start + Slot * 8 + Gap * 7;
        // Inventory badge: a diamond holding a pack, then the key that opens it.
        float badgeX = right + PaddingX + 12, middle = Top + Slot / 2;
        Ugui.Image(root, "Pack Rim", theme.Diamond, Palette.Gold).At(badgeX, middle - 17, 34, 34);
        Ugui.Image(root, "Pack Core", theme.Diamond, Palette.HudGlass).At(badgeX + 2, middle - 15, 30, 30);
        Ugui.Image(root, "Pack", theme.HudBackpack, Palette.Bone).At(badgeX + 7, middle - 10, 20, 20);

        Image cap = Ugui.Image(root, "Inventory Key", theme.Button, Color.white.WithAlpha(Palette.HudGlassOpacity)).At(badgeX + 42, middle - 11, 40, 22);
        inventoryKey = Ugui.Text(cap.transform, "Label", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Center).Stretch(2, 0, 2, 0);
        inventoryKey.overflowMode = TextOverflowModes.Overflow;
        textOutlines.Add(new TextOutline(inventoryKey));

    }

    private static string KeyName(string action, string fallback)
    {
        if (ZInput.instance == null) return fallback;
        string bound = ZInput.instance.GetBoundKeyString(action, true);
        if (string.IsNullOrEmpty(bound)) return fallback;
        return Localization.instance != null ? Localization.instance.Localize(bound) : bound;
    }

    public void Dispose()
    {
        if (mask)
        {
            mask!.alpha = oldAlpha;
            mask.blocksRaycasts = oldBlocks;
            mask.interactable = oldInteractable;
            if (ownsMask) UnityEngine.Object.Destroy(mask);
        }
        if (root) UnityEngine.Object.Destroy(root!.gameObject);
        if (Shown == root)
        {
            Shown = null;
            KeyDockInset = 0;
            requestedGuideSpace = 0;
        }
        guideLift = guideVelocity = 0;
        root = null;
        EquippedCard = null;
        mask = null;
        original = null;
        cells.Clear();
        textOutlines.Clear();
    }
}
