using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.Features.QualityOfLife;

internal sealed class ChestSearch : IDisposable
{
    private static readonly MethodInfo Access = AccessTools.Method(typeof(Container), "CheckAccess");
    private static readonly MethodInfo SetCrouch = AccessTools.Method(typeof(Player), "SetCrouch");
    private static readonly AccessTools.FieldRef<InventoryGui, Container> OpenContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private GameObject? canvas;
    private RectTransform? panel, guide;
    private TMP_InputField? input;
    private TMP_Text? footer;
    private readonly List<(Button Button, Image Icon, TMP_Text Label)> rows = new();
    private readonly List<Result> results = new();
    private readonly List<Image> dashes = new();
    private Container? target;
    private float expires, nextScan;
    private int selected, totalResults;
    private bool vanilla;
    private const int MaxRows = 8;
    private const float GuideDuration = 20f;

    private sealed class Result
    {
        internal Container Chest = null!;
        internal Sprite Icon = null!;
        internal string Name = "";
        internal int Count;
        internal float Distance;
    }

    internal ChestSearch(InterfaceSettings settings, Theme theme)
    {
        this.settings = settings;
        this.theme = theme;
        ForsakenPowerPatch.Blocked = () => UiState.SearchOpen || ShortcutHeld();
    }

    // The search shortcut's keys are held (either Control counts for the default Ctrl+F).
    private bool ShortcutHeld()
    {
        KeyboardShortcut key = settings.Features.ChestSearchKey.Value;
        if (key.MainKey == KeyCode.None || !Input.GetKey(key.MainKey)) return false;
        return key.Modifiers.All(m => Input.GetKey(m) || (m == KeyCode.LeftControl && Input.GetKey(KeyCode.RightControl)));
    }

    internal void Update()
    {
        var player = Player.m_localPlayer;
        if (!settings.Features.ChestSearch.Value || !player || player.IsDead()) { Close(); ClearGuide(); return; }
        if (UiState.WindowOpen || InventoryGui.IsVisible() || Menu.IsVisible()) Close();
        bool shortcut = settings.Features.ChestSearchKey.Value.IsDown();
        // Support either Control key for the default shortcut.
        if (settings.Features.ChestSearchKey.Value.MainKey == KeyCode.F && settings.Features.ChestSearchKey.Value.Modifiers.SequenceEqual(new[] { KeyCode.LeftControl }))
            shortcut |= Input.GetKeyDown(KeyCode.F) && Input.GetKey(KeyCode.RightControl);
        if (shortcut && !UiState.WindowOpen && !InventoryGui.IsVisible() && !Menu.IsVisible() &&
            !Console.IsVisible() && !Minimap.IsOpen() && !StoreGui.IsVisible() && (!Chat.instance || !Chat.instance.HasFocus()))
        {
            if (UiState.SearchOpen) Close(); else Open();
        }
        if (!UiState.SearchOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        if (vanilla != (settings.Hud.Value == HudStyle.Vanilla)) { Close(); return; }
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; Search(); }
        if (Input.GetKeyDown(KeyCode.DownArrow)) selected = Math.Min(selected + 1, results.Count - 1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) selected = Math.Max(0, selected - 1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Choose(selected); return; }
        for (int i = 0; i < rows.Count; i++)
            ((Image)rows[i].Button.targetGraphic).color = i == selected ? new Color(1f, 0.85f, 0.45f) : Color.white;
    }

    private bool Accessible(Container chest, Player player)
    {
        if (!chest || !chest.isActiveAndEnabled || chest.GetInventory() == null || chest.GetComponent<TombStone>()) return false;
        var piece = chest.GetComponent<Piece>();
        if (!piece || piece.GetCreator() == 0 || chest.m_wagon || chest.m_rootObjectOverride || chest.m_autoDestroyEmpty) return false;
        var view = chest.GetComponent<ZNetView>();
        return view && view.IsValid() && Vector3.Distance(player.transform.position, chest.transform.position) <= settings.Features.ChestSearchRange.Value &&
            (!chest.m_checkGuardStone || PrivateArea.CheckAccess(chest.transform.position, 0f, flash: false)) &&
            (bool)Access.Invoke(chest, new object[] { player.GetPlayerID() });
    }

    private void Open()
    {
        if (!InventoryGui.instance) return;
        ClearGuide();
        DestroyUi();
        Build();
        UiState.SearchOpen = true;
        SetCrouch.Invoke(Player.m_localPlayer, new object[] { false });
        input!.ActivateInputField();
        nextScan = 0;
        selected = 0;
        Search();
    }

    private void Close()
    {
        if (!UiState.SearchOpen) return;
        UiState.SearchOpen = false;
        UiState.SuppressMenuUntilFrame = Time.frameCount + 1;
        input?.DeactivateInputField();
        if (panel) panel!.gameObject.SetActive(false);
    }

    private void Search()
    {
        results.Clear();
        string query = input!.text.Trim();
        if (query.Length > 0)
        {
            foreach (var chest in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (!Accessible(chest, Player.m_localPlayer)) continue;
                foreach (var group in chest.GetInventory().GetAllItems().GroupBy(item => item.m_shared.m_name))
                {
                    string name = Localization.instance.Localize(group.Key);
                    if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    results.Add(new Result { Chest = chest, Name = name, Icon = group.First().GetIcon(), Count = group.Sum(item => item.m_stack),
                        Distance = Vector3.Distance(Player.m_localPlayer.transform.position, chest.transform.position) });
                }
            }
        }
        results.Sort((a, b) => { int distance = a.Distance.CompareTo(b.Distance); return distance != 0 ? distance : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture); });
        totalResults = results.Count;
        if (results.Count > MaxRows) results.RemoveRange(MaxRows, results.Count - MaxRows);
        selected = Mathf.Clamp(selected, 0, Math.Max(0, results.Count - 1));
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Button.gameObject.SetActive(i < results.Count);
            if (i >= results.Count) continue;
            Result result = results[i];
            rows[i].Icon.sprite = result.Icon;
            rows[i].Label.text = $"{result.Name} ×{result.Count}\n{Localization.instance.Localize(result.Chest.GetHoverName())} · {result.Distance:0} m";
        }
        footer!.text = query.Length == 0 ? "Type an item name to search nearby chests" : totalResults == 0 ? "No matching items in accessible chests" :
            totalResults > MaxRows ? $"Nearest {MaxRows} of {totalResults} matches · refine your search" : "↑ ↓ Select   Enter Locate   Esc Close · guide lasts 20 seconds";
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= results.Count) return;
        var result = results[index];
        if (!Accessible(result.Chest, Player.m_localPlayer)) { Search(); return; }
        target = result.Chest;
        expires = Time.unscaledTime + GuideDuration;
        Close();
    }

    internal void LateUpdate()
    {
        // Control may have toggled sneak earlier in this frame, before our shortcut was read.
        if (UiState.SearchOpen && Player.m_localPlayer)
            SetCrouch.Invoke(Player.m_localPlayer, new object[] { false });
        if (!target) { ClearGuide(); return; }
        var player = Player.m_localPlayer;
        if (!player || player.IsDead() || Time.unscaledTime >= expires || !Accessible(target!, player) ||
            (InventoryGui.instance && InventoryGui.IsVisible() && OpenContainer(InventoryGui.instance) == target))
        { ClearGuide(); return; }
        var camera = Utils.GetMainCamera();
        if (!camera || !guide) return;
        bool visible = global::Hud.instance && global::Hud.instance.m_rootObject.activeInHierarchy && !UiState.ModalOpen;
        guide!.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 start = player.transform.position + Vector3.up * 0.8f;
        Vector3 end = target!.transform.position + Vector3.up * 0.6f;
        float distance = Vector3.Distance(start, end);
        Vector3 control = (start + end) * 0.5f + Vector3.up * Mathf.Clamp(distance * 0.16f, 0.4f, 3.5f);
        Vector3 Curve(float t) => (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
        int count = Mathf.Clamp(Mathf.CeilToInt(distance / 0.45f), 6, 100);
        float elapsed = GuideDuration - (expires - Time.unscaledTime);
        float flow = Mathf.Repeat(elapsed * 2.5f, 1f);
        float revealed = Mathf.Clamp01(elapsed / 0.7f);
        // An extra segment supplies the partial dash entering at the character end.
        while (dashes.Count < count + 1)
        {
            var dash = Ugui.Image(guide, "Magic Trail", theme.White, new Color(1f, 0.94f, 0.58f));
            Ugui.Image(dash.transform, "Outer Glow", theme.White, new Color(1f, 0.65f, 0.05f, 0.09f)).Stretch(0, -5, 0, -5);
            Ugui.Image(dash.transform, "Inner Glow", theme.White, new Color(1f, 0.8f, 0.18f, 0.22f)).Stretch(0, -2, 0, -2);
            dashes.Add(dash);
        }
        for (int i = 0; i < dashes.Count; i++)
        {
            var dash = dashes[i];
            if (i > count) { dash.gameObject.SetActive(false); continue; }
            float moving = i - 1 + flow;
            float t = Mathf.Max(0, moving / count);
            float endT = Mathf.Min(revealed, (moving + 0.62f) / count);
            if (endT <= t) { dash.gameObject.SetActive(false); continue; }
            Vector3 a = camera.WorldToScreenPoint(Curve(t));
            Vector3 b = camera.WorldToScreenPoint(Curve(endT));
            bool inFront = a.z > 0.1f && b.z > 0.1f;
            dash.gameObject.SetActive(inFront);
            if (!inFront) continue;
            float shimmer = 0.65f + 0.35f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 16f - Time.unscaledTime * 5f), 3);
            float fade = Mathf.Clamp01((expires - Time.unscaledTime) / 0.8f);
            dash.color = new Color(1f, 0.91f, 0.4f, shimmer * fade);
            foreach (var glow in dash.GetComponentsInChildren<Image>())
            {
                if (glow == dash) continue;
                var color = glow.color;
                color.a = (glow.name == "Outer Glow" ? 0.09f : 0.22f) * shimmer * fade;
                glow.color = color;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(guide, a, null, out var p);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(guide, b, null, out var q);
            var rect = dash.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = p;
            rect.sizeDelta = new Vector2(Vector2.Distance(p, q), 2.5f);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg);
        }
    }

    private void Build()
    {
        vanilla = settings.Hud.Value == HudStyle.Vanilla;
        var gui = InventoryGui.instance;
        var font = vanilla ? gui.m_recipeDecription.font : theme.Body;
        Sprite frame = vanilla ? gui.m_player.Find("Bkg").GetComponent<Image>().sprite : theme.Window;
        Sprite button = vanilla ? ((Image)gui.m_stackAllButton.targetGraphic).sprite : theme.Button;
        canvas = new GameObject("Ragnarok Chest Search", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 31000;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1;
        guide = Ugui.Node(canvas.transform, "Chest Guide").Stretch();
        panel = Ugui.Node(canvas.transform, "Search Modal").Stretch();
        Ugui.Image(panel, "Shade", theme.White, new Color(0, 0, 0, 0.4f), true).Stretch();
        var window = Ugui.Image(panel, "Window", frame, Color.white, true).rectTransform;
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(600, 610);
        Ugui.Text(window, "Title", "FIND IN NEARBY CHESTS", vanilla ? gui.m_containerName.font : theme.Heading, 22, vanilla ? gui.m_containerName.color : Palette.Gold).At(28, 22, 540, 30);
        var field = Ugui.Image(window, "Search", vanilla ? button : theme.Field, Color.white, true).At(28, 64, 544, 42);
        field.gameObject.SetActive(false);
        var area = Ugui.Node(field.transform, "Text Area").Stretch(12, 4, 12, 4);
        area.gameObject.AddComponent<RectMask2D>();
        var text = Ugui.Text(area, "Query", "", font, 19, Color.white).Stretch();
        var placeholder = Ugui.Text(area, "Placeholder", "Search items…", font, 19, new Color(1, 1, 1, 0.5f)).Stretch();
        input = field.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area; input.textComponent = text; input.placeholder = placeholder; input.targetGraphic = field;
        input.characterLimit = 64;
        input.onValueChanged.AddListener(_ => { selected = 0; nextScan = 0; });
        field.gameObject.SetActive(true);
        UiState.TypingFields.Add(input);
        for (int i = 0; i < MaxRows; i++)
        {
            int index = i;
            var row = Ugui.Button(window, "Result " + i, button, vanilla ? button : theme.ButtonHover, () => Choose(index)).At(28, 118 + i * 53, 544, 48);
            var icon = Ugui.ItemIcon(Ugui.Image(row.transform, "Item", null, Color.white)).At(6, 4, 40, 40);
            var label = Ugui.Text(row.transform, "Description", "", font, 16, Color.white).At(56, 2, 478, 44);
            label.richText = false;
            rows.Add((row, icon, label));
        }
        footer = Ugui.Text(window, "Help", "", font, 14, vanilla ? Color.white : Palette.Muted).At(28, 554, 544, 30);
    }

    private void ClearGuide() { target = null; if (guide) guide!.gameObject.SetActive(false); }
    private void DestroyUi()
    {
        if (input) UiState.TypingFields.Remove(input!);
        if (canvas) UnityEngine.Object.Destroy(canvas);
        canvas = null; panel = guide = null; input = null;
        rows.Clear(); dashes.Clear(); results.Clear();
    }
    public void Dispose()
    {
        ForsakenPowerPatch.Blocked = () => false;
        Close();
        ClearGuide();
        DestroyUi();
    }
}

// Valheim reads the search shortcut's F as "use the Forsaken power" in the same frame (and an F typed into
// the search box too): no power while the shortcut is held or the search is open.
[HarmonyPatch(typeof(Player), nameof(Player.StartGuardianPower))]
internal static class ForsakenPowerPatch
{
    internal static Func<bool> Blocked = () => false;

    private static bool Prefix() => !Blocked();
}
