using System;
using HarmonyLib;
using Ironbound.Features.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ironbound.Features.QualityOfLife;

// The inventory Delete button: destroys the item held on the cursor (the split amount when
// splitting), from the player's inventory or the open chest. Click the button while holding an
// item, or press on an item and release it over the button. Mirrors Valheim's drop-outside path
// (unequip, remove, clear the cursor) without spawning the item into the world. With confirmation
// on, a small card beside the button asks first; a click elsewhere or Escape cancels it.
internal static class ItemTrash
{
    private const float CardWidth = 210, CardHeight = 70, CardGap = 6, CardPad = 8;
    private static readonly AccessTools.FieldRef<InventoryGui, int> DragAmount =
        AccessTools.FieldRefAccess<InventoryGui, int>("m_dragAmount");
    private static readonly Action<InventoryGui, ItemDrop.ItemData?, Inventory?, int> SetupDragItem =
        AccessTools.MethodDelegate<Action<InventoryGui, ItemDrop.ItemData?, Inventory?, int>>(
            AccessTools.Method(typeof(InventoryGui), "SetupDragItem"));
    private static readonly Vector3[] Corners = new Vector3[4];

    private static RectTransform? card;

    // Makes the Delete button a drop target too: releasing a held item over it presses it.
    internal static void Attach(Button button, Action press) =>
        button.gameObject.AddComponent<DeleteTarget>().Press = press;

    internal static void Reset()
    {
        if (card) UnityEngine.Object.Destroy(card!.gameObject);
        card = null;
    }

    // Reports through report: at once, or after the card is answered.
    internal static void Press(InventoryGui gui, Button anchor, bool confirm, Action<string> report)
    {
        Player player = Player.m_localPlayer;
        ItemDrop.ItemData? item = SlotActions.DragItem(gui);
        Inventory? from = SlotActions.DragInventory(gui);
        if (card) return;
        if (!player || item == null || from == null)
        {
            report("Pick up an item, then press Delete");
            return;
        }
        if (!from.ContainsItem(item))
        {
            SetupDragItem(gui, null, null, 1);
            return;
        }
        if (item.m_shared.m_questItem)
        {
            report("Quest items cannot be deleted");
            return;
        }

        int amount = Held(gui, item);
        if (!confirm)
        {
            report(Delete(gui, player, item, from, amount));
            return;
        }
        card = Card(anchor, $"Destroy {Label(item, amount)}?", () =>
        {
            Reset();
            if (InventoryGui.instance && Player.m_localPlayer)
                report(Delete(InventoryGui.instance, Player.m_localPlayer, item, from, amount));
        });
    }

    // The card may be answered after the stack changed: delete what is still there, at most the asked amount.
    private static string Delete(InventoryGui gui, Player player, ItemDrop.ItemData item, Inventory from, int amount)
    {
        if (!from.ContainsItem(item)) return "The item is gone";
        amount = Mathf.Min(amount, item.m_stack);
        string label = Label(item, amount);
        // A partial delete (a split from equipped ammunition) leaves the rest worn.
        if (from == player.GetInventory() && amount >= item.m_stack)
        {
            player.RemoveEquipAction(item);
            player.UnequipItem(item, triggerEquipEffects: false);
        }
        bool held = SlotActions.DragItem(gui) == item;
        from.RemoveItem(item, amount);
        if (held) SetupDragItem(gui, null, null, 1);
        return "Deleted " + label;
    }

    // Built from the Delete button itself (its sprite, outline and label font), so the card matches
    // whichever inventory style is showing. It opens beside the button, toward the panel's middle.
    private static RectTransform Card(Button anchor, string question, Action yes)
    {
        Transform parent = anchor.transform.parent;
        Image source = (Image)anchor.targetGraphic;
        Image background = Ugui.Image(parent, "Ironbound Delete Confirm", source.sprite, Palette.Hex(0x111716, 0.97f), raycast: true);
        background.type = source.type;
        background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        RectTransform rect = background.rectTransform;
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        Transform? outline = anchor.transform.Find("Outline");
        if (outline) UnityEngine.Object.Instantiate(outline!.gameObject, rect).GetComponent<RectTransform>().Stretch();

        TMP_Text sourceLabel = anchor.GetComponentInChildren<TMP_Text>(true);
        TMP_Text text = UnityEngine.Object.Instantiate(sourceLabel.gameObject, rect).GetComponent<TMP_Text>();
        text.name = "Question";
        text.text = question;
        text.fontSize = 13;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.Stretch(CardPad, CardPad, CardPad, CardHeight / 2);

        float buttonWidth = (CardWidth - CardPad * 3) / 2, buttonHeight = CardHeight / 2 - CardPad - 2;
        Choice(anchor, rect, "Delete", CardPad, buttonWidth, buttonHeight, yes);
        Choice(anchor, rect, "Cancel", CardPad * 2 + buttonWidth, buttonWidth, buttonHeight, Reset);

        // Above the button in the lower half of the panel, below it in the upper half; right edges aligned.
        anchor.GetComponent<RectTransform>().GetWorldCorners(Corners);
        Vector3 bottomRight = parent.InverseTransformPoint(Corners[3]), topRight = parent.InverseTransformPoint(Corners[2]);
        bool above = (bottomRight.y + topRight.y) / 2 < ((RectTransform)parent).rect.center.y;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(1, above ? 0 : 1);
        rect.localPosition = above ? topRight + new Vector3(0, CardGap, 0) : bottomRight - new Vector3(0, CardGap, 0);
        rect.SetAsLastSibling();
        rect.gameObject.AddComponent<ConfirmCard>();
        return rect;
    }

    private static void Choice(Button anchor, RectTransform card, string label, float x, float width, float height, Action click)
    {
        GameObject go = UnityEngine.Object.Instantiate(anchor.gameObject, card);
        go.name = label;
        // The copy is a plain button: no drop target, and none of the original's click listeners.
        UnityEngine.Object.DestroyImmediate(go.GetComponent<DeleteTarget>());
        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() => click());
        go.GetComponentInChildren<TMP_Text>(true).text = label;
        go.SetActive(true);
        go.GetComponent<RectTransform>().At(x, CardHeight / 2, width, height);
    }

    private static string Label(ItemDrop.ItemData item, int amount)
    {
        string name = Localization.instance.Localize(item.m_shared.m_name);
        return amount > 1 ? $"{amount} {name}" : name;
    }

    private static int Held(InventoryGui gui, ItemDrop.ItemData item) => Mathf.Clamp(DragAmount(gui), 1, Math.Max(1, item.m_stack));

    // Valheim moves items by click, but players also press on an item and let go over the button.
    // A button only clicks when pressed and released on itself, so catch the other release here.
    private sealed class DeleteTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        internal Action? Press;
        private bool over, pressedHere;

        public void OnPointerEnter(PointerEventData eventData) => over = true;

        public void OnPointerExit(PointerEventData eventData) => over = false;

        public void OnPointerDown(PointerEventData eventData) => pressedHere = eventData.button == PointerEventData.InputButton.Left;

        private void Update()
        {
            if (!ZInput.GetMouseButtonUp(0)) return;
            bool dropped = over && !pressedHere;
            pressedHere = false;
            if (dropped && Press != null && InventoryGui.instance && SlotActions.DragItem(InventoryGui.instance) != null) Press();
        }

        // The inventory closed (or the panel was rebuilt): an open question no longer applies.
        // Only the button's own lifetime resets; the panels' teardown runs every frame while unused.
        // Copies made for the card have no Press and leave it alone.
        private void OnDisable()
        {
            over = pressedHere = false;
            if (Press != null) Reset();
        }
    }

    // A click outside the card, or Escape, cancels it.
    private sealed class ConfirmCard : MonoBehaviour
    {
        private int opened;

        private void Start() => opened = Time.frameCount;

        private void Update()
        {
            if (Time.frameCount <= opened) return;
            bool click = ZInput.GetMouseButtonDown(0) || ZInput.GetMouseButtonDown(1);
            if (ZInput.GetKeyDown(KeyCode.Escape, false) || (click && !Inside())) Reset();
        }

        private bool Inside()
        {
            Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
            Camera? eye = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, ZInput.pointerPosition, eye);
        }
    }
}
