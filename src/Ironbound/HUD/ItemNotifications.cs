using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.HUD;

// Local presentation only. Native notification hooks retain responsibility for logging.
internal sealed class ItemNotifications : IDisposable
{
    private const float PickupLife = 2.2f, UnlockLife = 4.5f;
    private readonly PickupQueue pickups = new();
    private readonly Queue<(Sprite? Icon, string Topic, string Name)> unlocks = new();
    private readonly RectTransform parent, pickupRoot, pickupMotion, unlockRoot, unlockMotion;
    private readonly CanvasGroup pickupFade, unlockFade;
    private readonly TextMeshProUGUI pickupText, unlockTopic, unlockName;
    private readonly Image unlockIcon;
    private readonly List<TextOutline> outlines = new();
    private readonly Vector3[] corners = new Vector3[4];
    private float pickupBorn, pickupPulse, unlockBorn;
    private int pickupRevision = -1;
    private bool unlocking;
    private bool disposed;
    internal bool Alive => !disposed && pickupRoot && unlockRoot;
    internal bool HasPickup => pickups.HasItems;
    internal RectTransform? ActiveUnlock => unlocking ? unlockRoot : null;

    internal ItemNotifications(Theme theme, Transform hud)
    {
        parent = (RectTransform)hud;
        pickupRoot = Ugui.Node(hud, "Ironbound Item Pickups");
        pickupRoot.anchorMin = pickupRoot.anchorMax = pickupRoot.pivot = new Vector2(0.5f, 0);
        pickupRoot.sizeDelta = new Vector2(580, 44);
        pickupMotion = Ugui.Node(pickupRoot, "Pickup motion").Stretch();
        pickupFade = Fade(pickupRoot);
        pickupText = Ugui.Text(pickupMotion, "Picked item and quantity", "", theme.Heading, 25,
            Palette.Bone, TextAlignmentOptions.Center).Stretch();
        pickupText.enableAutoSizing = true;
        pickupText.fontSizeMin = 15;
        pickupText.fontSizeMax = 25;
        outlines.Add(new TextOutline(pickupText, world: true));

        unlockRoot = Ugui.Node(hud, "Ironbound Recipe Milestones");
        unlockRoot.anchorMin = unlockRoot.anchorMax = unlockRoot.pivot = new Vector2(1, 1);
        unlockRoot.sizeDelta = new Vector2(300, 90);
        unlockMotion = Ugui.Node(unlockRoot, "Unlock motion").Stretch();
        unlockFade = Fade(unlockRoot);
        // A quiet milestone surface, separate from the frameless pickup line.
        Ugui.Image(unlockMotion, "Surface", theme.PackSurface, Palette.Hex(0x0C0F11, 0.8f)).Stretch();
        Ugui.Image(unlockMotion, "Milestone accent", theme.White, Palette.Gold).At(0, 10, 2, 70);
        unlockIcon = Ugui.ItemIcon(Ugui.Image(unlockMotion, "Recipe icon", null, Color.white)).At(16, 30, 38, 38);
        unlockTopic = Ugui.Text(unlockMotion, "Unlock topic", "", theme.Strong, 11, Palette.Gold,
            spacing: 1.4f).At(66, 13, 218, 19);
        unlockName = Ugui.Text(unlockMotion, "Unlocked recipe", "", theme.Heading, 20, Palette.Bone,
            wrap: true).At(66, 34, 218, 46);
        unlockName.enableAutoSizing = true;
        unlockName.fontSizeMin = 13;
        unlockName.fontSizeMax = 20;
        outlines.Add(new TextOutline(unlockTopic, world: true));
        outlines.Add(new TextOutline(unlockName, world: true));
    }

    private static CanvasGroup Fade(RectTransform rect)
    {
        CanvasGroup fade = rect.gameObject.AddComponent<CanvasGroup>();
        fade.blocksRaycasts = fade.interactable = false;
        fade.alpha = 0;
        return fade;
    }

    internal void Pickup(string name, int amount) => pickups.Add(name, amount);
    internal void Unlock(Sprite? icon, string topic, string name) => unlocks.Enqueue((icon, topic, name));

    internal void Tick(float hudScale)
    {
        if (!Alive) return;
        bool visible = !global::Hud.IsUserHidden();
        pickupRoot.gameObject.SetActive(visible);
        unlockRoot.gameObject.SetActive(visible);
        float scale = Mathf.Min(hudScale, Mathf.Max(1, parent.rect.width - 24) / 580);
        pickupRoot.localScale = Vector3.one * scale;
        unlockRoot.localScale = Vector3.one * Mathf.Min(hudScale, Mathf.Max(1, parent.rect.width - 24) / 300);
        Place();
        float now = Time.unscaledTime;
        if (pickups.Current == null || now - pickupBorn >= PickupLife)
        {
            pickups.Next();
            pickupBorn = now;
            pickupRevision = -1;
        }
        if (pickups.Current is { } item)
        {
            if (pickupRevision != item.Revision)
            {
                pickupText.text = "<color=#E6C779><b>" + item.Name + "</b></color>  <b>+" + item.Amount + "</b>";
                pickupRevision = item.Revision;
                pickupPulse = now;
            }
            float age = now - pickupBorn, pulse = now - pickupPulse;
            // Fast lift with a damped overshoot, and a fresh pulse when the count increases.
            float pop = 1 + 0.22f * Mathf.Exp(-11 * pulse) * Mathf.Sin(pulse * 23);
            pickupMotion.localScale = Vector3.one * pop;
            pickupMotion.anchoredPosition = new Vector2(0, -16 * Mathf.Exp(-14 * age) + 5 * Mathf.Clamp01(age / PickupLife));
            pickupFade.alpha = Mathf.Clamp01(age / 0.09f) * Mathf.Clamp01((PickupLife - age) / 0.35f);
        }
        else pickupFade.alpha = 0;

        if (unlocking && now - unlockBorn >= UnlockLife) unlocking = false;
        if (!unlocking && unlocks.Count > 0)
        {
            var next = unlocks.Dequeue();
            unlockIcon.sprite = next.Icon;
            unlockIcon.enabled = next.Icon;
            unlockTopic.text = next.Topic.ToUpperInvariant();
            unlockName.text = next.Name;
            unlocking = true;
            unlockBorn = now;
        }
        float unlockAge = now - unlockBorn;
        unlockFade.alpha = unlocking ? Mathf.Clamp01(unlockAge / 0.25f) * Mathf.Clamp01((UnlockLife - unlockAge) / 0.5f) : 0;
        unlockMotion.anchoredPosition = new Vector2(24 * Mathf.Exp(-10 * unlockAge), 0);
        foreach (TextOutline outline in outlines) outline.Sync();
    }

    private void Place()
    {
        RectTransform? vitals = IronboundHud.ShownVitals;
        pickupRoot.anchoredPosition = new Vector2(0, 240);
        if (vitals && vitals!.gameObject.activeInHierarchy)
            pickupRoot.position = vitals.TransformPoint(new Vector3(vitals.rect.center.x, vitals.rect.yMax + 18, 0));
        Utils.ClampUIToScreen(pickupRoot);

        NotificationPlacement.BesideMinimap(unlockRoot, parent, corners);
    }

    internal void Clear()
    {
        pickups.Clear();
        unlocks.Clear();
        unlocking = false;
        UiLifetime.ResetAlpha(pickupFade);
        UiLifetime.ResetAlpha(unlockFade);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Clear();
        UiLifetime.DestroyRoot(pickupRoot);
        UiLifetime.DestroyRoot(unlockRoot);
    }
}
