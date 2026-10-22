using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ironbound.UI.Windows;

// A presentation over the real, still-active BuildUi. Selecting a piece calls
// the native handler; this view never spawns pieces or spends resources.
internal sealed class BuildMenuPanel : IDisposable
{
    private static readonly FieldInfo Native = AccessTools.Field(typeof(global::Hud), "m_buildUi");
    private static readonly Func<Humanoid, ItemDrop.ItemData?> RightItem = AccessTools.MethodDelegate<Func<Humanoid, ItemDrop.ItemData?>>(AccessTools.Method(typeof(Humanoid), "GetRightItem"));
    private static BuildMenuPanel? active;
    private readonly InterfaceSettings settings;
    private readonly Theme theme;
    private BuildUi? owner;
    private BuildMenuData? data;
    private CanvasGroup? nativeGroup;
    private Behaviour? tabHandler;
    private bool tabEnabled;
    private RectTransform? root, body, side, gridView, gridContent, categoryContent;
    private TMP_InputField? search;
    private TMP_Text? toolLabel, heading, count;
    private TMP_Text? onlyLabel, nearbyLabel, emptyLabel, columnValue, rowValue, guide, searchKeyLabel;
    private Image? onlyEdge, nearbyEdge;
    private Image? backdrop;
    private Button? repair, densityFirst;
    private ScrollRect? gridScroll, categoryScroll;
    private readonly List<Button> buttons = new();
    private readonly List<Card> cards = new();
    private readonly List<SideRow> categories = new();
    private readonly List<(Button Button, TMP_Text Label, Image Edge, int Tab)> tabs = new();
    private readonly Dictionary<int, Card> cardIndices = new();
    private Image? onlyCheck, nearbyCheck;
    private BuildMenuData.Entry? chosen;
    private float nextRefresh;
    private Vector2 layoutSize;
    private int openedFrame, columns, rows;
    private float cardWidth, cardHeight, centerWidth, panelHeight;
    private string query = "", appliedQuery = "";
    private bool pendingQuery;
    private float queryReady;
    private PieceTable? lastTool;
    private bool wasOpen, refresh = true;

    private sealed class Card
    {
        internal Button Button = null!;
        internal Image Face = null!, Edge = null!, Icon = null!;
        internal TMP_Text Amount = null!;
        internal BuildMenuPointer Pointer = null!;
        internal Image Star = null!, Upgrade = null!;
        internal BuildMenuData.Entry? Entry;
        internal int Index = -1;
    }
    private sealed class SideRow
    {
        internal Button Button = null!;
        internal Image Edge = null!;
        internal TMP_Text Label = null!;
        internal BuildMenuData.Category? Category;
    }
    internal BuildMenuPanel(InterfaceSettings settings, Theme theme) { this.settings = settings; this.theme = theme; }

    internal void Tick()
    {
        global::Hud hud = global::Hud.instance;
        BuildUi? current = hud ? Native.GetValue(hud) as BuildUi : null;
        if (!current || settings.Hud.Value != HudStyle.Ironbound) { Dispose(); return; }
        if (owner != current) { Dispose(); owner = current; data = new BuildMenuData(current!, settings); }
        bool open = current!.gameObject.activeInHierarchy && Player.m_localPlayer;
        if (!open)
        {
            if (root) root!.gameObject.SetActive(false);
            if (wasOpen) data?.Hover(null);
            if (active == this) active = null;
            wasOpen = false; return;
        }
        if (!root) Build(hud!);
        if (!root) return;
        root!.gameObject.SetActive(true); active = this;
        backdrop!.color = Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value);
        nativeGroup!.alpha = 0; nativeGroup.blocksRaycasts = nativeGroup.interactable = false;
        if (!wasOpen)
        {
            wasOpen = true; openedFrame = Time.frameCount; refresh = true;
            chosen = null; gridContent!.anchoredPosition = Vector2.zero;
        }
        if (lastTool != Player.m_localPlayer!.GetBuildTool())
        {
            lastTool = Player.m_localPlayer.GetBuildTool(); query = appliedQuery = ""; pendingQuery = false;
            search!.SetTextWithoutNotify(""); ResetGrid();
        }
        Layout();
        if (columns < 1 || rows < 1 || root.rect.width < 32 || root.rect.height < 32) return;
        if (pendingQuery && Time.unscaledTime >= queryReady)
        { pendingQuery = false; appliedQuery = query; ResetGrid(); }
        if (Time.frameCount > openedFrame && !UiState.ModalOpen && !UnifiedPopup.IsVisible() && !Console.IsVisible() && !Menu.IsVisible()) HandleInput();
        if (!owner!.gameObject.activeInHierarchy) { root.gameObject.SetActive(false); active = null; return; }
        if (refresh || Time.unscaledTime >= nextRefresh)
        {
            refresh = false; nextRefresh = Time.unscaledTime + 0.2f;
            data!.Refresh(appliedQuery);
            if (chosen == null || !chosen.Piece)
                chosen = data.Visible.Find(entry => entry.Piece == Player.m_localPlayer!.GetSelectedPiece()) ??
                    (data.Visible.Count > 0 ? data.Visible[0] : null);
            RefreshSidebar();
            heading!.text = data.Heading;
            count!.text = data.Visible.Count + (data.Visible.Count == 1 ? " piece" : " pieces");
            onlyLabel!.text = "Only what I can build";
            bool nearby = settings.BuildCountNearby.Value && data.NearbyAllowed;
            nearbyLabel!.text = data.NearbyAllowed ? "Count nearby chests" : "Nearby storage disabled";
            onlyCheck!.enabled = settings.BuildOnlyAvailable.Value; nearbyCheck!.enabled = nearby;
            onlyEdge!.color = settings.BuildOnlyAvailable.Value ? Palette.Gold : Palette.Hex(0x303631);
            nearbyEdge!.color = nearby ? Palette.Gold : Palette.Hex(0x303631);
            ItemDrop.ItemData? tool = RightItem(Player.m_localPlayer!);
            toolLabel!.text = tool == null ? "Building tool" : BuildMenuData.Local(tool.m_shared.m_name) +
                (tool.m_shared.m_useDurability ? " · durability " + Mathf.RoundToInt(tool.GetDurabilityPercentage() * 100) + "%" : "");
            UpdateGuide();
        }
        PaintCards();
        GameObject? selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected && selected!.transform.IsChildOf(owner.transform) && cards.Count > 0 && cards[0].Entry != null)
            EventSystem.current?.SetSelectedGameObject(cards[0].Button.gameObject);
        else if (Time.frameCount == openedFrame && ZInput.IsGamepadActive() && cards.Count > 0 && cards[0].Entry != null)
            EventSystem.current?.SetSelectedGameObject(cards[0].Button.gameObject);
    }

    private void Build(global::Hud hud)
    {
        // Own a separate mask, leaving native/other-mod groups untouched. Never
        // reuse a group queued for deferred destruction by an earlier F6 instance.
        nativeGroup = owner!.gameObject.AddComponent<CanvasGroup>();
        // Our search filters cards, so keep native pooled buttons active and their
        // availability checks running even if the old picker had a query saved.
        if (AccessTools.Field(typeof(BuildUi), "m_searchField").GetValue(owner) is TMP_InputField nativeSearch)
        {
            nativeSearch.SetTextWithoutNotify("");
            AccessTools.Method(typeof(BuildUi), "UpdateSearch").Invoke(owner, new object[] { "" });
        }
        tabHandler = AccessTools.Field(typeof(BuildUi), "m_tabHandler").GetValue(owner) as Behaviour;
        if (tabHandler) { tabEnabled = tabHandler!.enabled; tabHandler.enabled = false; }
        root = Ugui.Node(hud.m_rootObject.transform, "Ironbound Build Menu").Stretch();
        root.SetAsLastSibling();
        body = Ugui.Node(root, "Layout");
        body.anchorMin = body.anchorMax = body.pivot = new Vector2(0.5f, 0.5f);
        body.anchoredPosition = Vector2.zero;
        backdrop = Ugui.Image(body, "Background", theme.PackSurface, Palette.Hex(0x0C0F11, settings.InventoryOpacity.Value), true).Stretch();
        Ugui.Image(body, "Frame", theme.PackEdge, Palette.Hex(0x3D4541)).Stretch();
        repair = Button(body, "Repair mode", () => { if (data!.Repair) data.Choose(data.Repair!); });
        repair.At(20, 22, 40, 36);
        Ugui.Image(repair.transform, "Hammer", theme.IconHammer, Palette.Gold).Stretch(8, 6, 8, 6);
        Edge(repair.transform).color = Palette.Gold.WithAlpha(0.5f);
        GuideTooltip.Attach(theme, repair.GetComponent<RectTransform>()).Set("Repair mode", "HAMMER",
            "Select the hammer to repair building pieces.");
        Text(body, "Title", "Build", theme.Heading, 27, Palette.Bone).At(72, 16, 176, 28);
        toolLabel = Text(body, "Tool", "", theme.Body, 13, Palette.Muted).At(72, 44, 176, 18);
        toolLabel.enableAutoSizing = true; toolLabel.fontSizeMin = 10; toolLabel.fontSizeMax = 13;
        toolLabel.overflowMode = TextOverflowModes.Ellipsis;
        MakeSearch();
        Button available = Button(body, "Only buildable", () => { settings.BuildOnlyAvailable.Value = !settings.BuildOnlyAvailable.Value; ResetGrid(); });
        available.At(0, 22, 166, 36);
        onlyLabel = Label(available, "", 12); onlyEdge = Edge(available.transform);
        onlyLabel.rectTransform.offsetMin = new Vector2(29, 0);
        Ugui.Image(available.transform, "Check box", theme.PackPillEdge, Palette.Muted).At(10, 10, 16, 16);
        onlyCheck = Ugui.Image(available.transform, "Check", theme.IconCheck, Palette.Gold).At(11, 11, 14, 14);
        GuideTooltip.Attach(theme, available.GetComponent<RectTransform>()).Set("Only what I can build", "FILTER",
            "Uses the native game's current material, station and building requirements.");
        Button nearby = Button(body, "Nearby counts", () => { settings.BuildCountNearby.Value = !settings.BuildCountNearby.Value; refresh = true; });
        nearby.At(0, 22, 176, 36); nearbyLabel = Label(nearby, "", 12); nearbyEdge = Edge(nearby.transform);
        nearbyLabel.rectTransform.offsetMin = new Vector2(29, 0);
        Ugui.Image(nearby.transform, "Check box", theme.PackPillEdge, Palette.Muted).At(10, 10, 16, 16);
        nearbyCheck = Ugui.Image(nearby.transform, "Check", theme.IconCheck, Palette.Gold).At(11, 11, 14, 14);
        GuideTooltip.Attach(theme, nearby.GetComponent<RectTransform>()).Set("Count nearby chests", "PREVIEW COUNTS",
            "Includes permitted nearby stock in the displayed counts. Actual resource use follows the server's building-storage settings.");
        available.name = "Only buildable"; nearby.name = "Nearby counts";
        Button close = Button(body, "Close", Close); Label(close, "Close", 13);
        side = Surface(body, "Sidebar", Palette.Hex(0x070A0B)).rectTransform;
        string[] names = { "Categories", "Materials", "Recent", "Favourites" };
        int[] order = { 3, 2, 0, 1 };
        foreach (int index in order)
        {
            int tab = index;
            Button button = Button(side, names[index], () => { data!.SwitchTab(tab); ResetGrid(); categoryContent!.anchoredPosition = Vector2.zero; });
            TMP_Text label = Label(button, names[index], index < 2 ? 12 : 15);
            label.font = index < 2 ? theme.Strong : theme.Heading;
            if (index >= 2)
            {
                Ugui.Image(button.transform, "Glyph", index == 3 ? theme.HudStar : theme.IconSort, Palette.Muted).At(12, 8, 14, 14);
                label.rectTransform.offsetMin = new Vector2(34, 0); label.alignment = TextAlignmentOptions.MidlineLeft;
            }
            tabs.Add((button, label, Edge(button.transform), index));
        }
        Rule(side, 16, 112, 198);
        categoryContent = Scroll(side, "Categories", out categoryScroll);
        heading = Text(body, "Category heading", "Building", theme.Heading, 22, Palette.Bone);
        count = Text(body, "Piece count", "", theme.Body, 13, Palette.Muted, TextAlignmentOptions.MidlineRight);
        Image gridWell = Surface(body, "Piece list", Palette.Hex(0x070A0B));
        gridWell.name = "Piece list";
        gridContent = Scroll(gridWell.transform, "Pieces", out gridScroll); gridView = (RectTransform)gridScroll!.transform;
        emptyLabel = Text(gridWell.transform, "Empty", "No matching pieces", theme.Body, 16, Palette.Muted, TextAlignmentOptions.Midline).At(12, 30, 300, 40);
        BuildDensity();
        guide = Text(body, "Guide", "", theme.Body, 12, Palette.Muted, TextAlignmentOptions.Midline, wrap: true);
        UiState.TypingFields.Add(search!);
        layoutSize = Vector2.zero;
    }

    private void MakeSearch()
    {
        Image field = Surface(body!, "Search", Palette.Hex(0x080B0C));
        field.At(266, 22, 260, 36); field.raycastTarget = true; field.gameObject.SetActive(false);
        Ugui.Image(field.transform, "Glyph", theme.IconSearch, Palette.Muted).At(12, 10, 16, 16);
        RectTransform area = Ugui.Node(field.transform, "Text area").Stretch(36, 5, 28, 5);
        area.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = Text(area, "Text", "", theme.Body, 13, Palette.Bone).Stretch();
        TMP_Text placeholder = Text(area, "Placeholder", "Search pieces or materials", theme.Body, 13, Palette.Muted).Stretch();
        search = field.gameObject.AddComponent<TMP_InputField>();
        search.textViewport = area; search.textComponent = text; search.placeholder = placeholder; search.targetGraphic = field;
        search.characterLimit = 80; search.SetTextWithoutNotify(query);
        search.onValueChanged.AddListener(value => { query = value; pendingQuery = true; queryReady = Time.unscaledTime + 0.15f; });
        searchKeyLabel = Text(field.transform, "Search key", "/", theme.Strong, 14, Color.white, TextAlignmentOptions.MidlineRight).Stretch(4, 0, 12, 0);
        field.gameObject.SetActive(true);
    }

    private Image Surface(Transform parent, string name, Color color)
    {
        Image image = Ugui.Image(parent, name, theme.PackSurface, color);
        Edge(image.transform); return image;
    }
    private Image Edge(Transform parent) => Ugui.Image(parent, "Edge", theme.PackEdge, Palette.Hex(0x303631)).Stretch();
    private TMP_Text Text(Transform parent, string name, string text, TMP_FontAsset? font, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool wrap = false, float spacing = 0) =>
        Ugui.Text(parent, name, text, font, size, color, alignment, spacing, wrap);
    private Button Button(Transform parent, string name, Action action)
    {
        Button button = Ugui.Button(parent, name, theme.PackPill, theme.PackPill, action);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Palette.Hex(0x101516); colors.highlightedColor = Palette.Hex(0x29291F);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = Palette.Hex(0x493D25);
        colors.disabledColor = Palette.Hex(0x101314); colors.colorMultiplier = 1; colors.fadeDuration = 0.06f;
        button.colors = colors; buttons.Add(button); return button;
    }
    private TMP_Text Label(Button button, string text, float size) =>
        Text(button.transform, "Label", text, theme.Strong, size, Palette.Bone, TextAlignmentOptions.Midline).Stretch(6, 0, 6, 0);
    private void Rule(Transform parent, float x, float y, float width) => Ugui.Image(parent, "Rule", theme.White, Palette.Hex(0x303631)).At(x, y, width, 1);
    private RectTransform Scroll(Transform parent, string name, out ScrollRect scroll)
    {
        RectTransform view = Ugui.Image(parent, name, null, Color.clear, true).rectTransform;
        view.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Ugui.Node(view, "Content");
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1); content.sizeDelta = Vector2.zero;
        scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.viewport = view; scroll.content = content;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32; Ugui.WheelSpeed(scroll);
        // Capsule rail/thumb retain a generous invisible drag area.
        RectTransform rail = Ugui.Node(view, "Scrollbar").Stretch(0, 3, 0, 3);
        rail.anchorMin = new Vector2(1, 0); rail.anchorMax = Vector2.one;
        rail.offsetMin = new Vector2(-10, 3); rail.offsetMax = new Vector2(0, -3);
        Image track = Ugui.Image(rail, "Track", theme.HudBar, Palette.Bone.WithAlpha(0.08f)).Stretch(4, 0, 3, 0);
        track.pixelsPerUnitMultiplier = 6;
        RectTransform handle = Ugui.Node(rail, "Handle").Stretch();
        Image thumb = Ugui.Image(handle, "Thumb", theme.HudBar, Palette.Gold.WithAlpha(0.65f), true).Stretch(3, 0, 2, 0);
        thumb.pixelsPerUnitMultiplier = 4;
        Scrollbar bar = rail.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = thumb; bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    private void Layout()
    {
        Rect screenRect = root!.rect;
        Vector2 screen = screenRect.size;
        if (screen.x < 32 || screen.y < 32) return;
        float bottom = screenRect.yMin + 24, top = screenRect.yMax - 24;
        if (Ironbound.HUD.BuildTaskPanel.MenuClearanceTop is Vector3 taskTop)
            bottom = Mathf.Max(bottom, root.InverseTransformPoint(taskTop).y + 12);
        float scale = Mathf.Clamp(Mathf.Min((screen.x - 48) / 1120, (top - bottom) / 580), 0.01f, 1);
        // Reserve a stable task slot; hovering pieces must never move the grid.
        body!.anchoredPosition = new Vector2(0, Mathf.Max(screenRect.center.y,
            bottom + 580 * scale / 2));
        const float width = 1120;
        Vector2 sizeNow = new(width, 580);
        int newColumns = settings.BuildColumns.Value, newRows = settings.BuildRows.Value;
        if (layoutSize == sizeNow && columns == newColumns && rows == newRows)
        { body!.localScale = Vector3.one * scale; return; }
        layoutSize = sizeNow; columns = newColumns; rows = newRows;
        body!.sizeDelta = sizeNow; body.localScale = Vector3.one * scale;
        const float left = 20, sideWidth = 230, gap = 16;
        panelHeight = 472;
        float gridX = left + sideWidth + gap;
        centerWidth = width - left - gridX;
        side!.At(left, 78, sideWidth, panelHeight);
        foreach (var tab in tabs)
        {
            if (tab.Tab == 3) tab.Button.At(12, 10, sideWidth - 24, 30);
            else if (tab.Tab == 2) tab.Button.At(12, 44, sideWidth - 24, 30);
            else tab.Button.At(12 + tab.Tab * 104, 80, 100, 24);
        }
        categoryScroll!.At(10, 120, sideWidth - 20, panelHeight - 132);
        heading!.At(gridX, 78, centerWidth - 385, 30);
        heading!.enableAutoSizing = true; heading.fontSizeMin = 14; heading.fontSizeMax = 22;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        count!.At(gridX + centerWidth - 85, 78, 85, 30);
        Transform well = gridView!.parent;
        well.GetComponent<RectTransform>().At(gridX, 112, centerWidth, 438);
        gridView.Stretch(10, 10, 4, 10);
        emptyLabel!.At(10, 32, centerWidth - 20, 40);
        const float searchWidth = 260, headerY = 22, headerHeight = 36;
        search!.GetComponent<RectTransform>().At(gridX, headerY, searchWidth, headerHeight);
        body.Find("Only buildable").GetComponent<RectTransform>().At(gridX + searchWidth + 8, headerY, 166, headerHeight);
        body.Find("Nearby counts").GetComponent<RectTransform>().At(gridX + searchWidth + 182, headerY, 176, headerHeight);
        body.Find("Close").GetComponent<RectTransform>().At(width - 92, headerY, 72, headerHeight);
        body.Find("Density").GetComponent<RectTransform>().At(gridX + centerWidth - 369, 80, 272, 26);
        guide!.At(20, 556, width - 40, 18);
        UpdateGuide();
        columnValue!.text = columns.ToString(); rowValue!.text = rows.ToString();
    }

    private void BuildDensity()
    {
        RectTransform row = Ugui.Node(body!, "Density");
        Text(row, "Columns", "Columns", theme.Body, 11, Palette.Muted).At(0, 0, 48, 26);
        Text(row, "Rows", "Rows", theme.Body, 11, Palette.Muted).At(146, 0, 34, 26);
        columnValue = Text(row, "Column value", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Midline).At(74, 0, 24, 26);
        rowValue = Text(row, "Row value", "", theme.Strong, 12, Palette.Bone, TextAlignmentOptions.Midline).At(206, 0, 24, 26);
        string[] names = { "Fewer columns", "More columns", "Fewer rows", "More rows" };
        float[] positions = { 50, 100, 182, 232 };
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Button button = Button(row, names[i], () =>
            {
                if (index == 0) settings.BuildColumns.Value = Mathf.Max(4, settings.BuildColumns.Value - 1);
                if (index == 1) settings.BuildColumns.Value = Mathf.Min(16, settings.BuildColumns.Value + 1);
                if (index == 2) settings.BuildRows.Value = Mathf.Max(3, settings.BuildRows.Value - 1);
                if (index == 3) settings.BuildRows.Value = Mathf.Min(10, settings.BuildRows.Value + 1);
            });
            button.At(positions[i], 1, 22, 24); Label(button, i % 2 == 0 ? "-" : "+", 14);
            GuideTooltip.Attach(theme, button.GetComponent<RectTransform>()).Set(names[i], "GRID DENSITY",
                i < 2 ? "More columns fit more pieces across the window." : "More rows fit more pieces down the window.");
            if (i == 0) densityFirst = button;
        }
    }

    private void RefreshSidebar()
    {
        foreach (var tab in tabs)
        {
            bool on = tab.Tab == data!.Tab;
            tab.Edge.color = on ? Palette.Gold : Color.clear;
            tab.Label.color = on ? Palette.Gold : Palette.Bone;
        }
        while (categories.Count < data!.Categories.Count)
        {
            SideRow row = new();
            row.Button = Button(categoryContent!, "Category", () =>
            {
                if (row.Category?.Source) { row.Category.Source.GetComponent<Button>().onClick.Invoke(); ResetGrid(); }
            });
            row.Edge = Edge(row.Button.transform); row.Label = Label(row.Button, "", 13);
            row.Label.enableAutoSizing = true; row.Label.fontSizeMin = 10; row.Label.fontSizeMax = 13;
            row.Label.alignment = TextAlignmentOptions.MidlineLeft; row.Label.rectTransform.offsetMin = new Vector2(12, 0);
            categories.Add(row);
        }
        for (int i = 0; i < categories.Count; i++)
        {
            SideRow row = categories[i]; bool show = i < data.Categories.Count;
            row.Button.gameObject.SetActive(show); if (!show) { row.Category = null; continue; }
            row.Category = data.Categories[i]; row.Button.At(0, i * 30, 200, 27);
            row.Label.text = row.Category.Name; row.Label.color = row.Category.Selected ? Palette.Gold : Palette.Bone;
            row.Edge.color = row.Category.Selected ? Palette.Gold : Color.clear;
        }
        categoryContent!.sizeDelta = new Vector2(0, data.Categories.Count * 30);
        repair!.interactable = data.Repair;
    }

    private void ResetGrid()
    {
        refresh = true; chosen = null; data?.Hover(null);
        if (gridContent) gridContent!.anchoredPosition = Vector2.zero;
    }
    private void Inspect(BuildMenuData.Entry entry)
    {
        chosen = entry; data?.Hover(entry);
    }

    private void PaintCards()
    {
        if (data == null || gridView!.rect.height <= 0) return;
        float width = gridView.rect.width - 14, height = gridView.rect.height;
        cardWidth = (width - (columns - 1) * 8) / columns;
        cardHeight = (height - (rows - 1) * 8) / rows;
        BuildMenuData.Entry? selectedEntry = null;
        if (ZInput.IsGamepadActive() && !ZInput.IsMouseActive() && EventSystem.current)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            foreach (Card card in cards) if (card.Button.gameObject == selected) { selectedEntry = card.Entry; break; }
            int index = selectedEntry == null ? -1 : data.Visible.IndexOf(selectedEntry);
            if (index >= 0)
            {
                float top = (index / columns) * (cardHeight + 8), offset = gridContent!.anchoredPosition.y;
                if (top < offset) offset = top;
                else if (top + cardHeight > offset + height) offset = top + cardHeight - height;
                gridContent.anchoredPosition = new Vector2(0, Mathf.Max(0, offset));
            }
        }
        var window = BuildMenuRules.Window(data.Visible.Count, columns, rows, gridContent!.anchoredPosition.y, cardHeight + 8);
        int first = window.First, visibleCount = window.Count;
        float contentHeight = Mathf.CeilToInt(data.Visible.Count / (float)columns) * (cardHeight + 8) - (data.Visible.Count > 0 ? 8 : 0);
        gridContent.sizeDelta = new Vector2(0, contentHeight);
        gridContent.anchoredPosition = new Vector2(0, Mathf.Clamp(gridContent.anchoredPosition.y, 0, Mathf.Max(0, contentHeight - height)));
        emptyLabel!.enabled = data.Visible.Count == 0;
        while (cards.Count < visibleCount) AddCard();
        cardIndices.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i]; int index = first + i;
            bool show = i < visibleCount && index < data.Visible.Count;
            card.Button.gameObject.SetActive(show);
            if (!show) { card.Entry = null; card.Index = -1; continue; }
            BuildMenuData.Entry entry = data.Visible[index]; card.Entry = entry; card.Index = index;
            cardIndices[index] = card;
            card.Button.At((index % columns) * (cardWidth + 8), (index / columns) * (cardHeight + 8), cardWidth, cardHeight);
            card.Icon.sprite = entry.Piece.m_icon; card.Icon.color = entry.Buildable ? Color.white : Palette.Bone.WithAlpha(0.35f);
            float iconSize = Mathf.Max(12, Mathf.Min(68, Mathf.Min(cardWidth - 12, cardHeight - 12)));
            card.Icon.At((cardWidth - iconSize) / 2, (cardHeight - iconSize) / 2, iconSize, iconSize);
            card.Upgrade.sprite = entry.UpgradeIcon;
            card.Upgrade.enabled = entry.Piece.m_isUpgrade && entry.UpgradeIcon;
            float markerSize = Mathf.Min(18, Mathf.Min(cardWidth, cardHeight) * 0.3f);
            card.Upgrade.At(4, 4, markerSize, markerSize);
            card.Amount.At(4, cardHeight - 19, cardWidth - 10, 16);
            card.Amount.text = entry.Free ? "" : "\u00D7" + entry.Capacity;
            card.Amount.color = entry.Buildable ? Palette.Success : Palette.Muted;
            card.Edge.color = chosen == entry ? Palette.Gold : Palette.Hex(0x303631);
            card.Star.color = entry.Favorite ? Palette.Gold : Palette.Faint;
            card.Star.transform.parent.GetComponent<RectTransform>().At(cardWidth - 27, 4, 23, 23);
        }
        foreach (Card card in cards)
            if (card.Entry != null && card.Pointer.Hovering) { Inspect(card.Entry); break; }
        foreach (Card card in cards)
        {
            if (card.Entry == null) continue;
            Navigation nav = new() { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = card.Index % columns > 0 && cardIndices.TryGetValue(card.Index - 1, out Card? left) ? left.Button : tabs[0].Button;
            nav.selectOnRight = card.Index % columns < columns - 1 && cardIndices.TryGetValue(card.Index + 1, out Card? right) ? right.Button : card.Button;
            nav.selectOnUp = cardIndices.TryGetValue(card.Index - columns, out Card? up) ? up.Button : densityFirst;
            nav.selectOnDown = cardIndices.TryGetValue(card.Index + columns, out Card? down) ? down.Button : card.Button;
            card.Button.navigation = nav;
            if (selectedEntry != null && card.Entry == selectedEntry && EventSystem.current?.currentSelectedGameObject != card.Button.gameObject)
                EventSystem.current?.SetSelectedGameObject(card.Button.gameObject);
        }
    }

    private void AddCard()
    {
        Card card = new();
        card.Button = Button(gridContent!, "Piece", () => { if (card.Entry != null) data!.Click(card.Entry); });
        card.Face = (Image)card.Button.targetGraphic; card.Edge = Edge(card.Button.transform);
        card.Icon = Ugui.ItemIcon(Ugui.Image(card.Button.transform, "Icon", null, Color.white));
        card.Upgrade = Ugui.ItemIcon(Ugui.Image(card.Button.transform, "Station upgrade", null, Palette.Gold));
        card.Amount = Text(card.Button.transform, "Capacity", "", theme.Strong, 10, Palette.Success, TextAlignmentOptions.MidlineRight);
        card.Amount.enableAutoSizing = true; card.Amount.fontSizeMin = 7; card.Amount.fontSizeMax = 11;
        Button favorite = Button(card.Button.transform, "Favorite", () => { if (card.Entry != null) { data!.Favorite(card.Entry.Piece); refresh = true; } });
        favorite.targetGraphic.color = Color.clear;
        card.Star = Ugui.Image(favorite.transform, "Star", theme.HudStar, Palette.Faint).Stretch(4, 4, 4, 4);
        favorite.navigation = new Navigation { mode = Navigation.Mode.None };
        BuildMenuFocus focus = card.Button.gameObject.AddComponent<BuildMenuFocus>();
        focus.Selected = () => { if (card.Entry != null) Inspect(card.Entry); };
        focus.Deselected = () => data?.Hover(null);
        card.Pointer = card.Button.gameObject.AddComponent<BuildMenuPointer>();
        card.Pointer.Entered = () => { if (card.Entry != null) Inspect(card.Entry); };
        card.Pointer.Exited = () => { if (!ZInput.IsTouchActive()) data?.Hover(null); };
        card.Pointer.MiddleClick = () => { if (card.Entry != null) { data!.Favorite(card.Entry.Piece); refresh = true; } };
        cards.Add(card);
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB") ||
            (!search!.isFocused && ZInput.GetButtonDown("BuildMenu"))) { Close(); return; }
        if (search!.isFocused || UiState.Typing) return;
        bool typedSlash = Input.inputString.IndexOf('/') >= 0 && settings.BuildSearchKey.Value.MainKey == KeyCode.Slash;
        foreach (KeyCode modifier in settings.BuildSearchKey.Value.Modifiers) typedSlash &= Input.GetKey(modifier);
        if (typedSlash || settings.BuildSearchKey.Value.IsDown() || ZInput.GetButtonDown("JoyLStick"))
        { EventSystem.current?.SetSelectedGameObject(search.gameObject); search.ActivateInputField(); return; }
        if ((settings.BuildFavoriteKey.Value.IsDown() || ZInput.GetButtonDown("JoyButtonX")) && chosen != null) { data!.Favorite(chosen.Piece); refresh = true; }
        if (ZInput.GetButtonDown("JoyButtonY") && data!.Repair) { data.Choose(data.Repair!); return; }
        int direction = settings.BuildPreviousCategory.Value.IsDown() || ZInput.GetButtonDown("JoyLTrigger") ? -1 :
            settings.BuildNextCategory.Value.IsDown() || ZInput.GetButtonDown("JoyRTrigger") ? 1 : 0;
        if (direction != 0 && data!.Categories.Count > 0)
        {
            int current = data.Categories.FindIndex(category => category.Selected);
            if (current < 0) current = direction > 0 ? -1 : 0;
            int next = (current + direction + data.Categories.Count) % data.Categories.Count;
            data.Categories[next].Source.GetComponent<Button>().onClick.Invoke(); ResetGrid();
        }
    }
    private void UpdateGuide()
    {
        if (!guide) return;
        string previous = settings.BuildPreviousCategory.Value.ToString(), next = settings.BuildNextCategory.Value.ToString();
        string favorite = settings.BuildFavoriteKey.Value.ToString(), searchKey = settings.BuildSearchKey.Value.ToString().Replace("Slash", "/");
        searchKeyLabel!.text = searchKey;
        guide!.text = "<color=#FFFFFF><b>" + previous + " / " + next + "</b></color> categories    <color=#FFFFFF><b>" + searchKey +
            "</b></color> search    <color=#FFFFFF><b>" + favorite + "</b></color> favourite    Click a piece to select    <color=#FFFFFF><b>" + CloseKey() + "</b></color> close";
    }
    private static string CloseKey() => ZInput.instance != null ? BuildMenuData.Local(ZInput.instance.GetBoundKeyString("BuildMenu", true)) : "RMB";
    private void Close() { data?.Hover(null); global::Hud.CloseBuildUi(); if (root) root!.gameObject.SetActive(false); if (active == this) active = null; }

    public void Dispose()
    {
        if (owner) data?.Hover(null);
        if (active == this) active = null;
        if (search) { UiState.TypingFields.Remove(search!); search!.onValueChanged.RemoveAllListeners(); }
        foreach (Button button in buttons) if (button) button.onClick.RemoveAllListeners();
        buttons.Clear(); cards.Clear(); categories.Clear(); tabs.Clear(); cardIndices.Clear();
        if (root) { root!.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        if (nativeGroup)
        {
            nativeGroup!.alpha = 1; nativeGroup.blocksRaycasts = nativeGroup.interactable = true;
            UnityEngine.Object.Destroy(nativeGroup);
        }
        if (tabHandler) tabHandler!.enabled = tabEnabled;
        root = body = side = gridView = gridContent = categoryContent = null;
        nativeGroup = null; tabHandler = null; owner = null; data = null; search = null;
        chosen = null; lastTool = null; wasOpen = false; refresh = true; layoutSize = Vector2.zero;
    }

    [HarmonyPatch(typeof(BuildUi), "NavigationUpdate")]
    private static class NativeNavigation
    {
        private static bool Prefix(BuildUi __instance) => active == null || active.owner != __instance ||
            !__instance.gameObject.activeInHierarchy || active.settings.Hud.Value != HudStyle.Ironbound;
    }
}

internal sealed class BuildMenuPointer : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    internal Action? MiddleClick, Entered, Exited;
    internal bool Hovering { get; private set; }
    public void OnPointerEnter(PointerEventData data) { Hovering = true; Entered?.Invoke(); }
    public void OnPointerExit(PointerEventData data) { Hovering = false; Exited?.Invoke(); }
    private void OnDisable() { if (Hovering) { Hovering = false; Exited?.Invoke(); } }
    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Middle) MiddleClick?.Invoke();
    }
    private void OnDestroy() { MiddleClick = Entered = Exited = null; }
}
internal sealed class BuildMenuFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    internal Action? Selected, Deselected;
    public void OnSelect(BaseEventData data) => Selected?.Invoke();
    public void OnDeselect(BaseEventData data) => Deselected?.Invoke();
    private void OnDestroy() => Selected = Deselected = null;
}
