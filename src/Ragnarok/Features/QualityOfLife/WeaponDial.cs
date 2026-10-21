using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

// The weapon dial: hold its key and every weapon, shield and tool you carry (hotbar first) is laid around
// a ring at the centre of the screen, one segment each. Move the mouse (or the right stick) towards one and
// let go of the key to take it in hand; let go with the pointer in the middle to cancel. A needle from the
// centre hub shows where you are aiming. While it is open the camera stays still and attacks, blocks and
// jumps are off, as with Valheim's own emote dial; walking still works.
internal sealed class WeaponDial : IDisposable
{
    // Ring, hub and icon geometry in 1080p units. Dead and Reach are the invisible stick the mouse moves: a
    // short stick, so the slightest move picks the segment in that direction (only the very centre cancels).
    private const float Inner = 118, Outer = 262, Hub = 100, IconRadius = 190, Icon = 66, Gap = 4, Dead = 5, Reach = 60;
    private const int MaxItems = 12;
    private const float BackgroundOpacity = 0.9f;

    // Read by the patches below: Valheim treats the dial as one of its radial menus.
    internal static bool Open;

    // The item the dial is putting in hand, while Valheim's equip action for it runs, and until when at most.
    // Running would clear that action every frame (Valheim cancels equipping while you run), so running
    // pauses until it is done; with Shift still held, it picks up again by itself.
    internal static ItemDrop.ItemData? Equipping;
    internal static float EquippingUntil;

    private static readonly ItemDrop.ItemData.ItemType[] Kinds =
    {
        ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.TwoHandedWeapon,
        ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft, ItemDrop.ItemData.ItemType.Bow, ItemDrop.ItemData.ItemType.Shield,
        ItemDrop.ItemData.ItemType.Tool, ItemDrop.ItemData.ItemType.Torch
    };

    // The dial's colours and type. Ragnarök: smoked glass, a gold choice and halo
    // with diamond gems between the segments, Norse headings. Valheim: dark glass, its orange highlight and
    // its own font, like the game's emote dial.
    private sealed class Look
    {
        internal Color Fill, FillOn, FillHand, Edge, EdgeOn, EdgeHand, Hub, Accent, AccentDim, Title, Muted, Aura;
        internal TMP_FontAsset? Heading, Body;
        internal bool Ornate;
    }

    private Look look = null!;

    private Look Choose()
    {
        bool ragnarok = settings.WeaponDialStyle.Value switch
        {
            PanelStyle.Ragnarok => true,
            PanelStyle.Vanilla => false,
            _ => settings.Hud.Value == HudStyle.Ragnarok
        };
        if (ragnarok)
            return new Look
            {
                Fill = Palette.HudGlass.WithAlpha(BackgroundOpacity), FillOn = Palette.Hex(0x3A3123, BackgroundOpacity), FillHand = Palette.Hex(0x222925, BackgroundOpacity),
                Edge = Palette.Hex(0x626B64, 0.45f), EdgeOn = InventoryCategories.Gold, EdgeHand = Palette.Bone.WithAlpha(0.65f),
                Hub = Palette.HudGlass.WithAlpha(BackgroundOpacity), Accent = InventoryCategories.Gold, AccentDim = Palette.GoldDim, Title = Palette.Hex(0xF2D597), Muted = Palette.Bone.WithAlpha(0.85f),
                Heading = theme.Heading, Body = theme.Strong, Ornate = true, Aura = new Color(0.8f, 0.68f, 0.48f)
            };
        TMP_FontAsset? valheim = InventoryGui.instance && InventoryGui.instance.m_containerName ? InventoryGui.instance.m_containerName.font : theme.Body;
        Color orange = new(1f, 0.62f, 0.16f);
        return new Look
        {
            Fill = new Color(0, 0, 0, BackgroundOpacity), FillOn = orange.WithAlpha(BackgroundOpacity), FillHand = new Color(0.3f, 0.3f, 0.3f, BackgroundOpacity),
            Edge = new Color(1, 1, 1, 0.22f), EdgeOn = new Color(1f, 0.85f, 0.5f), EdgeHand = new Color(1, 1, 1, 0.6f),
            Hub = new Color(0, 0, 0, BackgroundOpacity), Accent = orange, AccentDim = new Color(1, 1, 1, 0.4f), Title = new Color(1f, 0.82f, 0.45f),
            Muted = new Color(0.85f, 0.85f, 0.85f), Heading = valheim, Body = valheim, Ornate = false, Aura = orange
        };
    }

    private sealed class Slot
    {
        internal ItemDrop.ItemData Item = null!;
        internal Image Fill = null!, Edge = null!, Icon = null!, Aura = null!;
        internal Image? Durability, DurabilityTrack;
        internal TextMeshProUGUI Metadata = null!;
        internal RectTransform IconRect = null!, Group = null!;
        internal CanvasGroup Fade = null!;
        internal readonly List<RectTransform> Marks = new();
        internal Vector2 Direction;
        // Animated: how far it stands out from the ring, and how lit its glow and ornaments are.
        internal float Push, Glow;
    }

    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private readonly List<Slot> slots = new();
    private GameObject? canvas;
    private RectTransform? needle, knob;
    private Image? hubRing;
    private RectTransform? dial;
    private CanvasGroup? dialFade;
    private float openedAt;
    private TextMeshProUGUI? title, detail, category, durability;
    private readonly List<TextOutline> outlines = new();
    private float nextDetails;
    private Vector2 aim;
    private int chosen = -1;
    private readonly JotunnIntegration.Binding? dialInput;
    private readonly JotunnIntegration.Binding? dialGamepad;
    private bool openedWithGamepad;

    internal WeaponDial(InterfaceSettings settings, Theme theme, JotunnIntegration? jotunn = null)
    {
        this.settings = settings;
        this.theme = theme;
        dialInput = jotunn?.AddButton("RagnarokWeaponDial", settings.Features.WeaponDialKey);
        dialGamepad = jotunn?.AddGamepadButton("RagnarokWeaponDialController", settings.Features.WeaponDialGamepad);
    }

    internal void Update()
    {
        Player player = Player.m_localPlayer;
        if (!Open)
        {
            if (settings.Features.WeaponDial.Value && player && !player.IsDead() && Pressed() && Free()) Show(player);
            return;
        }
        // Something else took over the screen: close without changing weapons.
        if (!player || player.IsDead() || !settings.Features.WeaponDial.Value || !Free())
        {
            Hide();
            return;
        }
        if (!Held())
        {
            ItemDrop.ItemData? pick = chosen >= 0 ? slots[chosen].Item : null;
            Hide();
            if (pick == null || pick.m_equipped || !player.GetInventory().ContainsItem(pick)) return;
            player.UseItem(null, pick, true);
            Equipping = player.IsEquipActionQueued(pick) ? pick : null;
            EquippingUntil = Time.time + 4f;
            return;
        }
        Aim();
    }

    public void Dispose()
    {
        dialInput?.Dispose();
        dialGamepad?.Dispose();
        Equipping = null;
        Hide();
        ClearCanvas();
    }

    // The key went down this frame, with its modifiers held (either Control, Shift or Alt counts).
    private bool Pressed()
    {
        if (dialGamepad?.Down == true) { openedWithGamepad = true; return true; }
        if (!(dialInput?.Down ?? Input.GetKeyDown(settings.Features.WeaponDialKey.Value.MainKey)) || !Modifiers()) return false;
        openedWithGamepad = false;
        return true;
    }

    private bool Held() => openedWithGamepad ? dialGamepad?.Held == true :
        dialInput?.Held ?? (settings.Features.WeaponDialKey.Value.MainKey != KeyCode.None && Input.GetKey(settings.Features.WeaponDialKey.Value.MainKey));

    private bool Modifiers() => settings.Features.WeaponDialKey.Value.Modifiers.All(m => Input.GetKey(m) || Input.GetKey(Twin(m)));

    private static KeyCode Twin(KeyCode key) => key switch
    {
        KeyCode.LeftControl => KeyCode.RightControl,
        KeyCode.LeftShift => KeyCode.RightShift,
        KeyCode.LeftAlt => KeyCode.RightAlt,
        _ => key
    };

    // Nothing else is on screen that takes the keys or the mouse.
    private static bool Free() =>
        !UiState.ModalOpen && !UiState.Typing && !InventoryGui.IsVisible() && !Menu.IsVisible() && !Console.IsVisible() &&
        !Minimap.IsOpen() && !StoreGui.IsVisible() && !TextInput.IsVisible() && !Game.IsPaused() &&
        (!Chat.instance || !Chat.instance.HasFocus()) && !(Hud.instance && Hud.instance.m_radialMenu && Hud.instance.m_radialMenu.gameObject.activeSelf);

    // Weapons, shields and tools in reading order (hotbar first); not the character panel's slots.
    private static List<ItemDrop.ItemData> Items(Player player) => player.GetInventory().GetAllItems()
        .Where(i => Array.IndexOf(Kinds, i.m_shared.m_itemType) >= 0 && !Equipment.CharacterSlots.InSlot(i))
        .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x).Take(MaxItems).ToList();

    private void Show(Player player)
    {
        List<ItemDrop.ItemData> items = Items(player);
        if (items.Count == 0) return;
        Look nextLook = Choose();
        if (!canvas || slots.Count != items.Count || look.Ornate != nextLook.Ornate ||
            look.Heading != nextLook.Heading || look.Body != nextLook.Body)
        {
            ClearCanvas();
            Build(items);
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                Slot slot = slots[i];
                slot.Item = items[i];
                slot.Icon.sprite = items[i].GetIcon();
                slot.Push = slot.Glow = 0;
            }
        }
        canvas!.SetActive(true);
        openedAt = Time.unscaledTime;
        Open = true;
        aim = Vector2.zero;
        chosen = -1;
        Paint();
        Aim();
    }

    private void Hide()
    {
        Open = false;
        if (canvas) canvas!.SetActive(false);
        // Retain UI objects, not references into a character's inventory between openings.
        foreach (Slot slot in slots) slot.Item = null!;
    }

    private void ClearCanvas()
    {
        slots.Clear();
        outlines.Clear();
        if (canvas)
        {
            canvas!.SetActive(false);
            UnityEngine.Object.Destroy(canvas);
        }
        canvas = null;
    }

    // Construction ---------------------------------------------------------------------------

    private void Build(List<ItemDrop.ItemData> items)
    {
        canvas = new GameObject("Ragnarok Weapon Dial", typeof(RectTransform)) { layer = 5 };
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 26000;
        CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        RectTransform root = Ugui.Node(canvas.transform, "Dial");
        Centre(root, Vector2.zero, Vector2.zero);
        dial = root;
        dialFade = root.gameObject.AddComponent<CanvasGroup>();
        openedAt = Time.unscaledTime;
        look = Choose();
        if (!look.Ornate)
        {
            float shadow = Outer * 2 + 220;
            Centre(WeaponDialGraphic.Create(root, "Shadow", new Color(0, 0, 0, 0.7f), 0, Outer + 110, shadow: true).rectTransform,
                Vector2.zero, new Vector2(shadow, shadow));
        }

        // One ring segment per item, turned to its place; its icon at the segment's middle.
        float size = (Outer + 2) * 2;
        for (int i = 0; i < items.Count; i++)
        {
            ItemDrop.ItemData item = items[i];
            float degrees = i * 360f / items.Count, rad = degrees * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Sin(rad), Mathf.Cos(rad));
            Quaternion turn = Quaternion.Euler(0, 0, -degrees);

            // Each piece moves as one: it slides in when the dial opens and stands out when aimed at.
            RectTransform group = Ugui.Node(root, "Piece");
            Centre(group, Vector2.zero, Vector2.zero);
            CanvasGroup fade = group.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0;

            // The glow: the segment's own shape, a little larger, behind it.
            Image aura = WeaponDialGraphic.Create(group, "Aura", look.Aura.WithAlpha(0), Inner, Outer, items.Count, Gap);
            Centre(aura.rectTransform, Vector2.zero, new Vector2(size, size));
            aura.rectTransform.localRotation = turn;
            Image f = WeaponDialGraphic.Create(group, "Segment", look.Fill, Inner, Outer, items.Count, Gap);
            Centre(f.rectTransform, Vector2.zero, new Vector2(size, size));
            f.rectTransform.localRotation = turn;
            Image e = WeaponDialGraphic.Create(group, "Edge", look.Edge, Inner, Outer, items.Count, Gap, border: true);
            Centre(e.rectTransform, Vector2.zero, new Vector2(size, size));
            e.rectTransform.localRotation = turn;

            Image icon = Ugui.Image(group, item.m_shared.m_name, item.GetIcon(), Color.white);
            Centre(icon.rectTransform, direction * IconRadius, new Vector2(Icon, Icon));
            icon.preserveAspect = true;
            TextMeshProUGUI metadata = Ugui.Text(group, "Key and stack", "", look.Body, 12, look.Muted, TextAlignmentOptions.Center);
            Centre(metadata.rectTransform, direction * IconRadius + new Vector2(0, -51), new Vector2(72, 18));
            Outline(metadata);
            Slot slot = new() { Item = item, Fill = f, Edge = e, Icon = icon, IconRect = icon.rectTransform, Aura = aura, Group = group, Fade = fade, Direction = direction };
            slot.Metadata = metadata;
            // Every reusable slot has a durability bar, toggled for its current item in Paint.
            {
                Vector2 at = direction * IconRadius + new Vector2(0, -64);
                slot.DurabilityTrack = Ugui.Image(group, "Durability track", theme.White, Palette.Hex(0x626B64, 0.45f));
                Centre(slot.DurabilityTrack.rectTransform, at, new Vector2(34, 2));
                slot.Durability = Ugui.Image(group, "Durability", theme.White, Palette.Success);
                Centre(slot.Durability.rectTransform, at, new Vector2(34, 2));
                slot.Durability.rectTransform.pivot = new Vector2(0, 0.5f);
                slot.Durability.rectTransform.anchoredPosition -= new Vector2(17, 0);
            }

            // Gold diamonds open around the piece's outer edge when it is aimed at.
            if (look.Ornate)
            {
                float half = 180f / items.Count - 5f;
                foreach ((float offset, float radius, float gem) in new[] { (0f, Outer + 20, 14f), (-half, Outer + 12, 8f), (half, Outer + 12, 8f) })
                {
                    float r = (degrees + offset) * Mathf.Deg2Rad;
                    RectTransform mark = Ugui.Image(group, "Mark", theme.Diamond, Palette.GoldBright).rectTransform;
                    Centre(mark, new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * radius, new Vector2(gem, gem));
                    mark.localScale = Vector3.zero;
                    slot.Marks.Add(mark);
                }
            }
            slots.Add(slot);
        }

        // The hub: a dark disc ringed in gold; the needle and its knob show the aim from the centre.
        if (look.Ornate) Ornaments(root, items.Count);
        Centre(WeaponDialGraphic.Create(root, "Hub", look.Hub, 0, Hub).rectTransform, Vector2.zero, new Vector2(Hub * 2, Hub * 2));
        hubRing = WeaponDialGraphic.Create(root, "Hub Ring", look.Edge, 0, Hub, border: true);
        Centre(hubRing.rectTransform, Vector2.zero, new Vector2(Hub * 2, Hub * 2));
        needle = Ugui.Image(root, "Needle", theme.White, look.Accent).rectTransform;
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0);
        needle.anchoredPosition = Vector2.zero;
        knob = Ugui.Image(root, "Knob", theme.Diamond, look.EdgeOn).rectTransform;
        Centre(knob, Vector2.zero, new Vector2(16, 16));
        if (!look.Ornate)
            Centre(Ugui.Image(root, "Origin", theme.Diamond, look.AccentDim).rectTransform, Vector2.zero, new Vector2(8, 8));

        category = Ugui.Text(root, "Type and quality", "", look.Body, 10, look.Muted, TextAlignmentOptions.Center, spacing: 1);
        Centre(category.rectTransform, new Vector2(0, 55), new Vector2(Hub * 1.6f, 18));
        category.enableAutoSizing = true;
        category.fontSizeMin = 8;
        category.fontSizeMax = 10;
        title = Ugui.Text(root, "Title", "", look.Heading, 21, look.Title, TextAlignmentOptions.Center);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.enableAutoSizing = true;
        title.fontSizeMin = 13;
        title.fontSizeMax = 21;
        Centre(title.rectTransform, new Vector2(0, 23), new Vector2(Hub * 1.65f, 42));
        detail = Ugui.Text(root, "Detail", "", look.Body, 12, look.Muted, TextAlignmentOptions.Center);
        detail.textWrappingMode = TextWrappingModes.Normal;
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 9;
        detail.fontSizeMax = 12;
        Centre(detail.rectTransform, new Vector2(0, -16), new Vector2(Hub * 1.65f, 32));
        durability = Ugui.Text(root, "Durability", "", look.Body, 12, Palette.Success, TextAlignmentOptions.Center);
        Centre(durability.rectTransform, new Vector2(0, -50), new Vector2(Hub * 1.6f, 20));
        foreach (TMP_Text label in new[] { category, title, detail, durability }) Outline(label);
    }

    private void Outline(TMP_Text label)
    {
        if (look.Ornate) outlines.Add(new TextOutline(label, world: true));
    }

    // Ragnarök's ornaments: the gold halo, and a diamond gem on it between each pair of segments.
    private void Ornaments(RectTransform root, int count)
    {
        float halo = (Outer + 14) * 2;
        Centre(WeaponDialGraphic.Create(root, "Halo", Palette.Gold.WithAlpha(0.7f), 0, Outer + 12, border: true).rectTransform,
            Vector2.zero, new Vector2(halo, halo));
        for (int i = 0; i < count && count > 1; i++)
        {
            float rad = (i + 0.5f) * Mathf.PI * 2 / count;
            Centre(Ugui.Image(root, "Gem", theme.Diamond, Palette.Gold).rectTransform,
                new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * (Outer + 12), new Vector2(10, 10));
        }
    }

    private static void Centre(RectTransform rect, Vector2 at, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
    }

    // Per frame ------------------------------------------------------------------------------

    // The mouse moves an invisible stick (held within Reach); the right stick points directly.
    private void Aim()
    {
        aim += ZInput.GetMouseDelta();
        if (aim.magnitude > Reach) aim = aim.normalized * Reach;
        Vector2 stick = new(ZInput.GetJoyRightStickX(), -ZInput.GetJoyRightStickY());
        if (stick.magnitude > 0.4f) aim = stick.normalized * Reach;

        int now = -1;
        if (aim.magnitude > Dead)
        {
            float degrees = Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg;
            if (degrees < 0) degrees += 360;
            now = Mathf.RoundToInt(degrees / (360f / slots.Count)) % slots.Count;
        }
        if (now != chosen)
        {
            chosen = now;
            Paint();
        }
        else if (Time.unscaledTime >= nextDetails) Paint();

        // The aim: the knob rides round the hub's rim towards the pointer, clear of the name in the middle
        // (the needle stays hidden). Near the centre, where letting go cancels, it waits there.
        float pull = Mathf.Clamp01(aim.magnitude / Reach);
        bool aiming = pull > 0.12f;
        Vector2 tip = aiming ? aim.normalized * Hub : Vector2.zero;
        needle!.gameObject.SetActive(false);
        knob!.anchoredPosition = tip;
        knob.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(aim.x, aim.y) * Mathf.Rad2Deg);
        knob.gameObject.SetActive(aiming);
        Image knobImage = knob.GetComponent<Image>();
        knobImage.color = chosen >= 0 ? look.EdgeOn : look.Muted;
        Animate();
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1 - (1 - t) * (1 - t) * (1 - t);
    }

    // Opening: the dial fades and grows in quickly, and every piece closes in from outside the ring at once.
    // Aiming: the piece eases outwards, its glow and ornaments rising; the others settle back.
    private void Animate()
    {
        float t = Time.unscaledTime - openedAt, step = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 16f);
        dialFade!.alpha = EaseOut(t / 0.1f);
        dial!.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, EaseOut(t / 0.14f));

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            float arrive = EaseOut((t - 0.03f) / 0.18f);
            bool on = i == chosen;
            slot.Push = Mathf.Lerp(slot.Push, on ? 18f : 0f, step);
            slot.Glow = Mathf.Lerp(slot.Glow, on ? 1f : 0f, step);

            slot.Fade.alpha = arrive;
            slot.Group.anchoredPosition = slot.Direction * ((1 - arrive) * 80f + slot.Push);
            slot.Group.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, arrive);
            slot.IconRect.localScale = Vector3.one * (1 + 0.22f * slot.Glow);
            slot.Aura.color = look.Aura.WithAlpha((look.Ornate ? 0.08f : 0.5f) * slot.Glow);
            slot.Aura.rectTransform.localScale = Vector3.one * (1 + 0.07f * slot.Glow);
            // The ornaments pop slightly past full size before settling.
            float x = slot.Glow - 1, pop = slot.Glow < 0.01f ? 0 : 1 + 2.70158f * x * x * x + 1.70158f * x * x;
            foreach (RectTransform mark in slot.Marks) mark.localScale = Vector3.one * pop;
        }
        foreach (TextOutline outline in outlines) outline.Sync();
    }

    // The chosen segment lit (gold or orange), its icon a little larger; what is in hand slightly lighter.
    private void Paint()
    {
        nextDetails = Time.unscaledTime + 0.2f;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            bool on = i == chosen, hand = slot.Item.m_equipped;
            slot.Fill.color = on ? look.FillOn : hand ? look.FillHand : look.Fill;
            slot.Edge.color = on ? look.EdgeOn : hand ? look.EdgeHand : look.Edge;

            slot.Icon.color = on || hand ? Color.white : new Color(1, 1, 1, 0.8f);
            string key = slot.Item.m_gridPos.y == 0 ? (slot.Item.m_gridPos.x + 1).ToString() : "";
            string stack = slot.Item.m_shared.m_maxStackSize > 1 ? "×" + slot.Item.m_stack : "";
            slot.Metadata.text = (key + "  " + stack).Trim();
            if (slot.Durability)
            {
                bool show = slot.Item.m_shared.m_useDurability;
                slot.Durability!.gameObject.SetActive(show);
                slot.DurabilityTrack!.gameObject.SetActive(show);
                if (!show) continue;
                float remaining = slot.Item.GetDurabilityPercentage();
                slot.Durability!.rectTransform.sizeDelta = new Vector2(34 * remaining, 2);
                slot.Durability.color = DurabilityColor(remaining);
            }
        }
        hubRing!.color = chosen >= 0 ? look.Accent : look.Edge;
        if (chosen < 0)
        {
            title!.text = "";
            category!.text = durability!.text = "";
            detail!.text = "Aim to choose";
            return;
        }
        ItemDrop.ItemData item = slots[chosen].Item;
        title!.text = Localization.instance.Localize(item.m_shared.m_name);
        string kind = item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield ? "Shield" :
            item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch ? "Torch" :
            item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool ? "Tool" :
            Label("$skill_" + item.m_shared.m_skillType.ToString().ToLowerInvariant(), "Weapon");
        category!.text = (kind + " · " + Label("$item_quality", "Quality") + " " + item.m_quality +
            (item.m_equipped ? " · In hand" : "")).ToUpperInvariant();
        detail!.text = DamageSummary(item);
        float percentage = item.GetDurabilityPercentage();
        durability!.text = item.m_shared.m_useDurability
            ? Label("$item_durability", "Durability") + " " + Mathf.RoundToInt(percentage * 100) + "%" : "";
        durability.color = DurabilityColor(percentage);
    }

    private static Color DurabilityColor(float remaining) => remaining < 0.1f ? Palette.Health :
        remaining < 0.25f ? Palette.Stamina : Palette.Hex(0x9BC48A);

    private static string Label(string token, string fallback)
    {
        string localized = Localization.instance.Localize(token);
        return localized.StartsWith("[", StringComparison.Ordinal) || localized.StartsWith("$", StringComparison.Ordinal) ? fallback : localized;
    }

    private static string DamageSummary(ItemDrop.ItemData item)
    {
        var damage = item.GetDamage();
        List<string> parts = new();
        void Add(string key, string label, float amount)
        {
            if (amount > 0) parts.Add(Label("$inventory_" + key, label) + " " + amount.ToString("0.#"));
        }
        if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield && item.GetBaseBlockPower() > 0)
            parts.Add(Label("$item_blockarmor", "Block") + " " + item.GetBaseBlockPower().ToString("0.#"));
        Add("damage", "Damage", damage.m_damage);
        Add("blunt", "Blunt", damage.m_blunt);
        Add("slash", "Slash", damage.m_slash);
        Add("pierce", "Pierce", damage.m_pierce);
        Add("chop", "Chop", damage.m_chop);
        Add("pickaxe", "Pickaxe", damage.m_pickaxe);
        Add("fire", "Fire", damage.m_fire);
        Add("frost", "Frost", damage.m_frost);
        Add("lightning", "Lightning", damage.m_lightning);
        Add("poison", "Poison", damage.m_poison);
        Add("spirit", "Spirit", damage.m_spirit);
        return string.Join(" · ", parts);
    }

}

// While the dial is open Valheim treats it as one of its radial menus: no attacks, blocks, jumps or
// dodges, and the camera does not turn with the mouse (which aims the dial instead).
[HarmonyPatch(typeof(Hud), nameof(Hud.InRadial))]
internal static class WeaponDialRadialPatch
{
    // Only with Valheim's radial in place: its input code reads that menu whenever this is true.
    private static void Postfix(ref bool __result)
    {
        if (WeaponDial.Open && Hud.instance && Hud.instance.m_radialMenu) __result = true;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
internal static class WeaponDialLookPatch
{
    private static void Prefix(ref Vector2 __0)
    {
        if (WeaponDial.Open) __0 = Vector2.zero;
    }
}

// Running pauses while the dial's equip action runs (see WeaponDial.Equipping).
[HarmonyPatch(typeof(Player), "CheckRun")]
internal static class WeaponDialRunPatch
{
    private static bool Prefix(Player __instance, ref bool __result)
    {
        ItemDrop.ItemData? item = WeaponDial.Equipping;
        if (item == null || __instance != Player.m_localPlayer) return true;
        if (Time.time > WeaponDial.EquippingUntil || !__instance.IsEquipActionQueued(item))
        {
            WeaponDial.Equipping = null;
            return true;
        }
        __result = false;
        return false;
    }
}
