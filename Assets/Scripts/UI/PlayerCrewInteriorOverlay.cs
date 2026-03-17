using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerCrewInteriorOverlay : MonoBehaviour
{
    public static bool IsInputCaptured { get; private set; }

    [Header("Refs")]
    [SerializeField] private ModuleManager moduleManager;
    [SerializeField] private PlayerCrewManager crewManager;

    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private bool buildOnAwake = true;
    [SerializeField] private bool startVisible = false;
    [SerializeField] private int sortingOrder = 15000;
    [SerializeField] private Vector2 crewPopupPivot   = new Vector2(1f, 1f);
    [SerializeField] private Vector2 modulePopupPivot = new Vector2(1f, 1f);

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(920f, 520f);
    [SerializeField, Range(0.35f, 1.00f)] private float panelWidthRatio = 0.96f;
    [SerializeField, Range(0.30f, 1.00f)] private float panelHeightRatio = 0.90f;
    [SerializeField] private Vector2 minPanelSize = new Vector2(900f, 520f);
    [SerializeField] private Vector2 maxPanelSize = new Vector2(2200f, 1400f);
    [SerializeField] private float actionPanelHeight = 118f;

    [Header("Style")]
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.38f);
    [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.08f, 0.90f);
    [SerializeField] private Color borderColor = new Color(0.78f, 0.82f, 0.88f, 0.26f);
    [SerializeField] private Color dividerColor = new Color(0.80f, 0.84f, 0.90f, 0.20f);
    [SerializeField] private Color titleColor = new Color(0.94f, 0.96f, 0.98f, 0.96f);
    [SerializeField] private Color labelColor = new Color(0.72f, 0.76f, 0.82f, 0.88f);
    [SerializeField] private Color normalColor = new Color(0.74f, 0.88f, 0.84f, 1f);
    [SerializeField] private Color damagedColor = new Color(0.98f, 0.78f, 0.36f, 1f);
    [SerializeField] private Color destroyedColor = new Color(0.92f, 0.32f, 0.26f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.44f, 0.48f, 0.53f, 0.85f);
    [SerializeField] private Color fireColor = new Color(1.0f, 0.3f, 0.15f, 1f);

    private GameObject overlayRoot;
    private RectTransform mainPanelRect;
    private Canvas mainCanvas;
    private Font builtinFont;
    private ModuleDamageController[] cachedModules;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private readonly List<CrewRow> crewRows = new List<CrewRow>();
    private readonly List<ModuleRowUI> damagedRows = new List<ModuleRowUI>();
    private readonly List<ModuleRowUI> destroyedRows = new List<ModuleRowUI>();

    // Action panel
    private Text actionTitleText;
    private Text selectionText;
    private Button suppressDriverButton;
    private Button suppressGunnerButton;
    private Button suppressLoaderButton;
    private Button suppressMGButton;
    private GameObject suppressionButtonsRow;

    // Crew swap popup
    private GameObject crewPopupPanel;
    private RectTransform crewPopupRect;
    private ModuleType popupCrewType = ModuleType.Commander; // Commander = none

    // Module repair popup
    private GameObject modulePopupPanel;
    private RectTransform modulePopupRect;
    private ModuleDamageController popupModule = null;

    private sealed class CrewRow
    {
        public ModuleType type;
        public int indexWithinType;
        public Button button;
        public Text labelText;
        public Text valueText;
        public RectTransform rowRect;
    }

    private sealed class ModuleRowUI
    {
        public GameObject root;
        public Text text;
        public Image background;
        public ModuleDamageController module;
    }

    // ── Unity Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        if (moduleManager == null) moduleManager = GetComponent<ModuleManager>();
        if (crewManager == null)   crewManager   = GetComponent<PlayerCrewManager>();

        CacheModules();

        if (buildOnAwake)
            BuildUI();
    }

    private void OnDestroy()
    {
        IsInputCaptured = false;
        DestroyOverlayRoot();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        if (overlayRoot != null && (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
        {
            bool wasVisible = overlayRoot.activeSelf;
            BuildUI();
            if (overlayRoot != null)
                overlayRoot.SetActive(wasVisible);
            IsInputCaptured = wasVisible;
        }

        if (overlayRoot != null && overlayRoot.activeSelf)
        {
            // 팝업 외부 클릭 시 닫기
            if (Input.GetMouseButtonDown(0))
            {
                Camera cam = mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? Camera.main : null;
                if (crewPopupPanel != null && crewPopupPanel.activeSelf
                    && !RectTransformUtility.RectangleContainsScreenPoint(crewPopupRect, Input.mousePosition, cam))
                    HideCrewPopup();
                if (modulePopupPanel != null && modulePopupPanel.activeSelf
                    && !RectTransformUtility.RectangleContainsScreenPoint(modulePopupRect, Input.mousePosition, cam))
                    HideModulePopup();
            }

            RefreshUI();
        }
    }

    // ── Build ────────────────────────────────────────────────────────

    [ContextMenu("Build Crew Interior UI")]
    public void BuildUI()
    {
        DestroyOverlayRoot();
        crewRows.Clear();
        damagedRows.Clear();
        destroyedRows.Clear();
        HideAllPopups();
        builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("PlayerCrewInteriorOverlayUI",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlayRoot = root;

        mainCanvas = root.GetComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        RectTransform backdrop = CreateImageRect("Backdrop", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, backdropColor);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;

        panelSize = GetResponsivePanelSize();
        lastScreenWidth  = Screen.width;
        lastScreenHeight = Screen.height;

        RectTransform panel = CreateImageRect("Panel", root.transform, new Vector2(0.5f, 0.5f), panelSize, panelColor);
        mainPanelRect = panel;
        AddOutline(panel.gameObject, borderColor, 1f);

        BuildHeader(panel);
        BuildBody(panel);
        BuildCrewPopup(panel);
        BuildModulePopup(panel);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        RefreshUI();

        overlayRoot.SetActive(startVisible);
        IsInputCaptured = startVisible;
    }

    private void BuildHeader(RectTransform panel)
    {
        Text title = CreateText("Title", panel, "TANK STATUS", 30, TextAnchor.MiddleCenter, titleColor);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot     = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        title.rectTransform.sizeDelta = new Vector2(-40f, 34f);

        Text hint = CreateText("Hint", panel, "TAB TO CLOSE", 14, TextAnchor.MiddleCenter, labelColor);
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.pivot     = new Vector2(0.5f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -58f);
        hint.rectTransform.sizeDelta = new Vector2(-40f, 20f);

        RectTransform headerLine = CreateImageRect("HeaderLine", panel, new Vector2(0.5f, 1f), new Vector2(panelSize.x - 32f, 1f), dividerColor);
        headerLine.anchoredPosition = new Vector2(0f, -88f);
    }

    private void BuildBody(RectTransform panel)
    {
        GameObject body = new GameObject("Body", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        body.transform.SetParent(panel, false);

        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(36f, actionPanelHeight + 32f);
        bodyRect.offsetMax = new Vector2(-36f, -108f);

        HorizontalLayoutGroup hlg = body.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24f;
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;

        ContentSizeFitter bodyFitter = body.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform crewColumn = CreateColumn(body.transform, "CREW");

        GameObject dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerGo.transform.SetParent(body.transform, false);
        Image dividerImage = dividerGo.GetComponent<Image>();
        dividerImage.color = dividerColor;
        dividerImage.raycastTarget = false;
        LayoutElement dividerLayout = dividerGo.AddComponent<LayoutElement>();
        dividerLayout.minWidth      = 1f;
        dividerLayout.preferredWidth = 1f;
        dividerLayout.flexibleWidth  = 0f;
        dividerLayout.flexibleHeight = 1f;

        RectTransform moduleColumn = CreateColumn(body.transform, "MODULE");

        BuildCrewColumn(crewColumn);
        BuildModuleColumn(moduleColumn);
        BuildActionPanel(panel);
    }

    private RectTransform CreateColumn(Transform parent, string header)
    {
        GameObject column = new GameObject(header + "Column", typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(parent, false);

        LayoutElement layout = column.AddComponent<LayoutElement>();
        layout.flexibleWidth  = 1f;
        layout.flexibleHeight = 1f;
        layout.minWidth       = 0f;
        layout.preferredWidth = panelSize.x * 0.46f;

        VerticalLayoutGroup vlg = column.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(0, 0, 8, 0);

        Text headerText = CreateText(header + "Header", column.GetComponent<RectTransform>(), header, 24, TextAnchor.MiddleLeft, titleColor);
        LayoutElement headerLayout = headerText.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 30f;

        return column.GetComponent<RectTransform>();
    }

    private void BuildCrewColumn(RectTransform column)
    {
        AddCrewRow(column, "Commander", ModuleType.Commander, 0);
        AddCrewRow(column, "Gunner",         ModuleType.Gunner,    0);
        AddCrewRow(column, "Loader",         ModuleType.Loader,    0);
        AddCrewRow(column, "Driver",         ModuleType.Driver,    0);
        AddDividerLine(column, "DriverToMGLine");
        AddCrewRow(column, "Machine Gunner", ModuleType.MachineGunner, 0);
    }

    private void AddCrewRow(RectTransform parent, string label, ModuleType type, int indexWithinType)
    {
        GameObject row = new GameObject(label + "Row",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.02f);
        rowImage.raycastTarget = true;

        Button rowButton = row.GetComponent<Button>();
        rowButton.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = rowButton.colors;
        cb.normalColor      = new Color(1f, 1f, 1f, 0.02f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.12f);
        cb.selectedColor    = new Color(1f, 1f, 1f, 0.10f);
        cb.disabledColor    = new Color(1f, 1f, 1f, 0.01f);
        rowButton.colors = cb;

        RectTransform rowRect    = row.GetComponent<RectTransform>();
        ModuleType    capturedType = type;
        rowButton.onClick.AddListener(() => OnCrewRowClicked(capturedType, rowRect));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 34f;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;

        Text nameText = CreateText(label + "Name", row.transform, label, 21, TextAnchor.MiddleLeft, titleColor);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;
        nameLayout.minWidth      = 180f;

        Text valueText = CreateText(label + "Value", row.transform, "OK", 19, TextAnchor.MiddleRight, normalColor);
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow   = VerticalWrapMode.Truncate;
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 220f;

        AddDividerLine(parent, label + "Line");

        crewRows.Add(new CrewRow
        {
            type             = type,
            indexWithinType  = indexWithinType,
            button           = rowButton,
            labelText        = nameText,
            valueText        = valueText,
            rowRect          = rowRect
        });
    }

    private void AddDividerLine(RectTransform parent, string name)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        line.GetComponent<Image>().color = dividerColor;
        LayoutElement lineLayout = line.AddComponent<LayoutElement>();
        lineLayout.preferredHeight = 1f;
    }

    private void BuildModuleColumn(RectTransform column)
    {
        AddSectionHeader(column, "Damaged",   damagedColor);
        for (int i = 0; i < 6; i++)
            damagedRows.Add(AddModuleRow(column, "DamagedRow" + i));

        AddSpacer(column, 10f);
        AddSectionHeader(column, "Destroyed", destroyedColor);
        for (int i = 0; i < 6; i++)
            destroyedRows.Add(AddModuleRow(column, "DestroyedRow" + i));
    }

    private void AddSectionHeader(RectTransform parent, string title, Color color)
    {
        Text header = CreateText(title + "Header", parent, title, 20, TextAnchor.MiddleLeft, color);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
    }

    private ModuleRowUI AddModuleRow(RectTransform parent, string name)
    {
        GameObject rowRoot = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ModuleRowClickHandler));
        rowRoot.transform.SetParent(parent, false);

        Image bg = rowRoot.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.015f);
        bg.raycastTarget = true;

        rowRoot.GetComponent<LayoutElement>().preferredHeight = 24f;

        Text rowText = CreateText(name + "Text", rowRoot.transform, string.Empty, 18, TextAnchor.MiddleLeft, labelColor);
        rowText.rectTransform.anchorMin  = Vector2.zero;
        rowText.rectTransform.anchorMax  = Vector2.one;
        rowText.rectTransform.offsetMin  = new Vector2(8f, 0f);
        rowText.rectTransform.offsetMax  = new Vector2(-8f, 0f);
        rowText.horizontalOverflow = HorizontalWrapMode.Overflow;
        rowText.verticalOverflow   = VerticalWrapMode.Truncate;

        ModuleRowUI rowUi = new ModuleRowUI { root = rowRoot, text = rowText, background = bg, module = null };
        rowRoot.GetComponent<ModuleRowClickHandler>().OnClicked = btn => OnModuleRowClicked(rowUi, btn);

        return rowUi;
    }

    private void AddSpacer(RectTransform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private void BuildActionPanel(RectTransform panel)
    {
        GameObject footer = new GameObject("ActionPanel",
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        footer.transform.SetParent(panel, false);

        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin         = new Vector2(0.5f, 0f);
        footerRect.anchorMax         = new Vector2(0.5f, 0f);
        footerRect.pivot             = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition  = new Vector2(0f, 18f);
        footerRect.sizeDelta         = new Vector2(panelSize.x - 72f, actionPanelHeight);

        footer.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

        VerticalLayoutGroup footerLayout = footer.GetComponent<VerticalLayoutGroup>();
        footerLayout.padding              = new RectOffset(18, 18, 12, 12);
        footerLayout.spacing              = 12f;
        footerLayout.childAlignment       = TextAnchor.UpperLeft;
        footerLayout.childControlHeight   = true;
        footerLayout.childControlWidth    = true;
        footerLayout.childForceExpandWidth  = true;
        footerLayout.childForceExpandHeight = false;

        actionTitleText = CreateText("ActionTitle", footer.transform, "STATUS", 16, TextAnchor.MiddleLeft, titleColor);
        actionTitleText.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

        selectionText = CreateText("SelectionText", footer.transform,
            "Click crew to reassign.  Click a module to repair.", 18, TextAnchor.MiddleLeft, labelColor);
        selectionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

        // 화재 진압 버튼 행
        suppressionButtonsRow = new GameObject("SuppressionButtons",
            typeof(RectTransform), typeof(HorizontalLayoutGroup));
        suppressionButtonsRow.transform.SetParent(footer.transform, false);

        HorizontalLayoutGroup suppressLayout = suppressionButtonsRow.GetComponent<HorizontalLayoutGroup>();
        suppressLayout.spacing              = 26f;
        suppressLayout.childAlignment       = TextAnchor.MiddleLeft;
        suppressLayout.childControlHeight   = true;
        suppressLayout.childControlWidth    = false;
        suppressLayout.childForceExpandWidth  = false;
        suppressLayout.childForceExpandHeight = false;
        suppressionButtonsRow.AddComponent<LayoutElement>().preferredHeight = 40f;

        suppressDriverButton = CreateSuppressionButton(suppressionButtonsRow.transform, "Driver",          ModuleType.Driver);
        suppressGunnerButton = CreateSuppressionButton(suppressionButtonsRow.transform, "Gunner",          ModuleType.Gunner);
        suppressLoaderButton = CreateSuppressionButton(suppressionButtonsRow.transform, "Loader",          ModuleType.Loader);
        suppressMGButton     = CreateSuppressionButton(suppressionButtonsRow.transform, "MG",  ModuleType.MachineGunner);

        suppressionButtonsRow.SetActive(false);
    }

    private void BuildCrewPopup(RectTransform panel)
    {
        crewPopupPanel = BuildPopupBase(panel, "CrewPopupPanel", 220f, crewPopupPivot);
        crewPopupRect  = crewPopupPanel.GetComponent<RectTransform>();
        crewPopupPanel.SetActive(false);
    }

    private void BuildModulePopup(RectTransform panel)
    {
        modulePopupPanel = BuildPopupBase(panel, "ModulePopupPanel", 260f, modulePopupPivot);
        modulePopupRect  = modulePopupPanel.GetComponent<RectTransform>();
        modulePopupPanel.SetActive(false);
    }

    private GameObject BuildPopupBase(RectTransform panel, string name, float width, Vector2 pivot)
    {
        GameObject popup = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        popup.transform.SetParent(panel, false);

        Image bg = popup.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.10f, 0.13f, 0.97f);
        bg.raycastTarget = true;
        AddOutline(popup, borderColor, 1f);

        VerticalLayoutGroup vlg = popup.GetComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(6, 6, 6, 6);
        vlg.spacing              = 4f;
        vlg.childAlignment       = TextAnchor.UpperLeft;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = popup.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform rect = popup.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 0f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot     = pivot;

        return popup;
    }

    // ── Popup ─────────────────────────────────────────────────────

    private void OnCrewRowClicked(ModuleType crewType, RectTransform anchor)
    {
        if (crewType == ModuleType.Commander) return;

        // 같은 크루 다시 클릭 → 닫기
        if (crewPopupPanel != null && crewPopupPanel.activeSelf && popupCrewType == crewType)
        {
            HideCrewPopup();
            return;
        }

        if (crewManager == null) return;

        ModuleType[] targets = crewManager.GetAvailablePlayerSwapTargets(crewType);
        if (targets == null || targets.Length == 0) { HideCrewPopup(); return; }

        var options = new List<(string, UnityEngine.Events.UnityAction)>();
        foreach (ModuleType seat in targets)
        {
            ModuleType capturedSeat = seat;
            options.Add(($"→ {GetSeatDisplayName(seat)}", () =>
            {
                crewManager.StartPlayerCrewMove(crewType, capturedSeat);
                HideCrewPopup();
            }));
        }

        popupCrewType = crewType;
        ShowPopupAt(crewPopupPanel, crewPopupRect, anchor, options);
    }

    private void OnModuleRowClicked(ModuleRowUI row, PointerEventData.InputButton button)
    {
        if (row?.module == null || button != PointerEventData.InputButton.Left) return;

        ModuleDamageController module = row.module;

        // 같은 모듈 다시 클릭 → 닫기
        if (modulePopupPanel != null && modulePopupPanel.activeSelf && popupModule == module)
        {
            HideModulePopup();
            return;
        }

        if (crewManager == null) return;

        ModuleType[] crewTypes = { ModuleType.Driver, ModuleType.Gunner, ModuleType.Loader, ModuleType.MachineGunner };
        var options = new List<(string, UnityEngine.Events.UnityAction)>();
        foreach (ModuleType crew in crewTypes)
        {
            if (!crewManager.CanAssignRepairCrew(crew, module)) continue;
            float dur = crewManager.GetRepairDuration(module);
            ModuleType capturedCrew = crew;
            ModuleType assignedSeat = crewManager.GetAssignedSeatForCrew(crew);
            string roleLabel = (assignedSeat != ModuleType.Commander)
                ? GetSeatDisplayName(assignedSeat)
                : GetSeatDisplayName(crew);
            options.Add(($"Assign {roleLabel} ({dur:0.0}s)", () =>
            {
                crewManager.StartPlayerRepair(capturedCrew, module);
                HideModulePopup();
            }));
        }

        if (options.Count == 0) { HideModulePopup(); return; }

        popupModule = module;
        ShowPopupAt(modulePopupPanel, modulePopupRect, row.root.GetComponent<RectTransform>(), options);
    }

    private void ShowPopupAt(GameObject panel, RectTransform rect, RectTransform anchor, List<(string label, UnityEngine.Events.UnityAction action)> options)
    {
        if (panel == null) return;

        // 기존 버튼 제거
        for (int i = panel.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(panel.transform.GetChild(i).gameObject);

        // 새 버튼 추가
        foreach (var (label, action) in options)
            AddPopupButton(panel, label, action);

        // 앵커 기준 위치 계산 (오른쪽 상단 모서리)
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners); // [2] = top-right

        Vector2 localPos = mainPanelRect.InverseTransformPoint(corners[2]);
        rect.anchoredPosition = localPos + new Vector2(panelSize.x * 0.5f - 4f, panelSize.y * 0.5f);
        panel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void AddPopupButton(GameObject panel, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(panel.transform, false);

        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.04f);

        Button btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(1f, 1f, 1f, 0.04f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.22f);
        cb.selectedColor    = new Color(1f, 1f, 1f, 0.10f);
        btn.colors = cb;
        btn.onClick.AddListener(action);

        go.GetComponent<LayoutElement>().preferredHeight = 30f;

        Text text = CreateText(label + "Text", go.transform, label, 16, TextAnchor.MiddleLeft, titleColor);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(10f, 0f);
        text.rectTransform.offsetMax = new Vector2(-6f, 0f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow   = VerticalWrapMode.Truncate;
    }

    private void HideCrewPopup()
    {
        if (crewPopupPanel != null) crewPopupPanel.SetActive(false);
        popupCrewType = ModuleType.Commander;
    }

    private void HideModulePopup()
    {
        if (modulePopupPanel != null) modulePopupPanel.SetActive(false);
        popupModule = null;
    }

    private void HideAllPopups()
    {
        HideCrewPopup();
        HideModulePopup();
    }

    // ── Refresh ──────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (moduleManager == null) moduleManager = GetComponent<ModuleManager>();
        if (moduleManager == null) return;

        CacheModules();
        RefreshCrewRows();
        RefreshModuleRows();
        RefreshActionPanel();
    }

    private void RefreshCrewRows()
    {
        for (int i = 0; i < crewRows.Count; i++)
        {
            CrewRow row = crewRows[i];
            ModuleDamageController module = FindModule(row.type, row.indexWithinType);

            if (module == null)
            {
                // 모듈 미설정 크루는 살아있는 것으로 간주
                if (row.type == ModuleType.Commander)
                {
                    row.valueText.text  = "Player";
                    row.valueText.color = normalColor;
                    if (row.button != null) row.button.interactable = false;
                }
                else
                {
                    row.valueText.text  = crewManager != null ? crewManager.GetCrewSeatLabel(row.type) : "OK";
                    row.valueText.color = normalColor;
                    if (row.button != null) row.button.interactable = true;
                }
                if (row.labelText != null) row.labelText.color = (popupCrewType == row.type) ? normalColor : titleColor;
                continue;
            }

            if (crewManager != null && crewManager.IsPlayerRepairInProgress && crewManager.RepairingCrewType == row.type)
            {
                string moduleName = crewManager.RepairingModule != null ? crewManager.RepairingModule.PartName : "Module";
                row.valueText.text  = $"Repairing: {moduleName}";
                row.valueText.color = damagedColor;
                if (row.button != null) row.button.interactable = false;
            }
            else if (crewManager != null && crewManager.IsPlayerSuppressionInProgress && crewManager.SuppressingCrewType == row.type)
            {
                row.valueText.text  = "Extinguishing";
                row.valueText.color = fireColor;
                if (row.button != null) row.button.interactable = false;
            }
            else if (crewManager != null && crewManager.IsPlayerSwapInProgress && crewManager.MovingCrewType == row.type)
            {
                row.valueText.text  = crewManager.GetCrewSeatLabel(row.type);
                row.valueText.color = damagedColor;
                if (row.button != null) row.button.interactable = false;
            }
            else if (module.State == ModuleState.Destroyed)
            {
                row.valueText.text  = "KIA";
                row.valueText.color = destroyedColor;
                if (row.button != null) row.button.interactable = false;
            }
            else if (module.State == ModuleState.Damaged)
            {
                row.valueText.text  = crewManager != null ? crewManager.GetCrewSeatLabel(row.type) : $"{Mathf.RoundToInt(module.Hp01 * 100f)}%";
                row.valueText.color = damagedColor;
                if (row.button != null) row.button.interactable = row.type != ModuleType.Commander;
            }
            else
            {
                string seatLabel = crewManager != null ? crewManager.GetCrewSeatLabel(row.type) : "OK";
                bool isReassigned = crewManager != null
                    && crewManager.GetAssignedSeatForCrew(row.type) != row.type
                    && crewManager.GetAssignedSeatForCrew(row.type) != ModuleType.Commander;
                row.valueText.text  = seatLabel;
                row.valueText.color = isReassigned ? damagedColor : normalColor;
                if (row.button != null) row.button.interactable = row.type != ModuleType.Commander;
            }

            if (row.labelText != null)
                row.labelText.color = (popupCrewType == row.type) ? normalColor : titleColor;
        }
    }

    private void RefreshModuleRows()
    {
        List<ModuleDamageController> damaged   = new List<ModuleDamageController>();
        List<ModuleDamageController> destroyed = new List<ModuleDamageController>();

        for (int i = 0; i < cachedModules.Length; i++)
        {
            ModuleDamageController module = cachedModules[i];
            if (module == null || IsCrewType(module.Type)) continue;

            if (module.State == ModuleState.Destroyed) destroyed.Add(module);
            else if (module.State == ModuleState.Damaged) damaged.Add(module);
        }

        ApplyModuleList(damagedRows,   damaged,   damagedColor,   "None");
        ApplyModuleList(destroyedRows, destroyed, destroyedColor, "None");
    }

    private void ApplyModuleList(List<ModuleRowUI> rows, List<ModuleDamageController> values, Color activeColor, string emptyText)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            ModuleRowUI row = rows[i];
            if (i < values.Count)
            {
                ModuleDamageController module = values[i];
                row.module        = module;
                bool isBeingRepaired = crewManager != null
                    && crewManager.IsPlayerRepairInProgress
                    && crewManager.RepairingModule == module;
                string repairSuffix = isBeingRepaired
                    ? $" ← {GetCurrentRoleLabel(crewManager.RepairingCrewType)} ({crewManager.PlayerRepairSecondsRemaining:0.0}s)"
                    : string.Empty;
                row.text.text     = BuildModuleRowLabel(module) + repairSuffix;
                row.text.color    = isBeingRepaired ? normalColor : activeColor;
                row.background.color = (popupModule == module)
                    ? new Color(1f, 1f, 1f, 0.10f)
                    : new Color(1f, 1f, 1f, 0.015f);
            }
            else if (i == 0 && values.Count == 0)
            {
                row.module        = null;
                row.text.text     = emptyText;
                row.text.color    = emptyColor;
                row.background.color = new Color(1f, 1f, 1f, 0.015f);
            }
            else
            {
                row.module        = null;
                row.text.text     = string.Empty;
                row.background.color = new Color(1f, 1f, 1f, 0.015f);
            }
        }
    }

    private void RefreshActionPanel()
    {
        if (selectionText == null) return;
        if (crewManager == null) crewManager = GetComponent<PlayerCrewManager>();

        // 화재 진압 — 최우선
        bool fireActive = moduleManager != null && moduleManager.IsOnFire;
        if (fireActive)
        {
            int remaining = crewManager != null
                ? crewManager.MaxSuppression - crewManager.SuppressionCount
                : 0;

            actionTitleText.text  = "FIRE EXTINGUISHING";
            actionTitleText.color = fireColor;

            if (crewManager != null && crewManager.IsPlayerSuppressionInProgress)
                selectionText.text = $"{crewManager.SuppressingCrewType} extinguishing... ({crewManager.PlayerSuppressionSecondsRemaining:0.0}s)  [{remaining}/{crewManager.MaxSuppression} remaining]";
            else
                selectionText.text = $"Assign a crew to extinguish the fire.  [{remaining}/{(crewManager != null ? crewManager.MaxSuppression : 3)} remaining]";

            suppressionButtonsRow?.SetActive(true);
            SetButtonState(suppressDriverButton, crewManager != null && crewManager.CanAssignFireSuppression(ModuleType.Driver));
            SetButtonState(suppressGunnerButton, crewManager != null && crewManager.CanAssignFireSuppression(ModuleType.Gunner));
            SetButtonState(suppressLoaderButton, crewManager != null && crewManager.CanAssignFireSuppression(ModuleType.Loader));
            SetButtonState(suppressMGButton,     crewManager != null && crewManager.CanAssignFireSuppression(ModuleType.MachineGunner));
            return;
        }

        // 기본 상태
        suppressionButtonsRow?.SetActive(false);
        actionTitleText.text  = "STATUS";
        actionTitleText.color = titleColor;

        int suppressLeft = crewManager != null ? crewManager.MaxSuppression - crewManager.SuppressionCount : 3;
        int maxSuppress  = crewManager != null ? crewManager.MaxSuppression : 3;
        selectionText.text = $"Click crew to reassign.  Click a module to repair.  [Extinguisher: {suppressLeft}/{maxSuppress}]";
    }

    // ── Actions ──────────────────────────────────────────────────

    private void TryAssignFireSuppression(ModuleType crewType)
    {
        if (crewManager == null) crewManager = GetComponent<PlayerCrewManager>();
        crewManager?.StartFireSuppression(crewType);
    }

    // ── Button Factories ─────────────────────────────────────────

    private Button CreateActionButton(Transform parent, string label)
    {
        GameObject go = new GameObject(label,
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor      = new Color(1f, 1f, 1f, 0.08f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.16f);
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.24f);
        cb.selectedColor    = new Color(1f, 1f, 1f, 0.16f);
        cb.disabledColor    = new Color(1f, 1f, 1f, 0.03f);
        button.colors = cb;

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth  = 200f;
        layout.preferredHeight = 34f;

        Text text = CreateText("Label", go.transform, label, 16, TextAnchor.MiddleLeft, titleColor);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(14f, 0f);
        text.rectTransform.offsetMax = new Vector2(-10f, 0f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = 16;

        return button;
    }

    private Button CreateSuppressionButton(Transform parent, string crewLabel, ModuleType crewType)
    {
        Button button = CreateActionButton(parent, crewLabel);
        button.onClick.AddListener(() => TryAssignFireSuppression(crewType));
        return button;
    }

    private void SetButtonState(Button button, bool enabled)
    {
        if (button == null) return;
        button.interactable = enabled;
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string BuildModuleRowLabel(ModuleDamageController module)
    {
        if (module == null) return string.Empty;

        string label = string.IsNullOrWhiteSpace(module.PartName)
            ? module.Type.ToString()
            : module.PartName;

        return module.State == ModuleState.Damaged
            ? $"- {label} ({Mathf.RoundToInt(module.Hp01 * 100f)}%)"
            : "- " + label;
    }

    private ModuleDamageController FindModule(ModuleType type, int indexWithinType)
    {
        if (cachedModules == null) return null;

        int found = 0;
        for (int i = 0; i < cachedModules.Length; i++)
        {
            ModuleDamageController module = cachedModules[i];
            if (module == null || module.Type != type) continue;
            if (found == indexWithinType) return module;
            found++;
        }

        return null;
    }

    private void CacheModules()
    {
        if (moduleManager == null) moduleManager = GetComponent<ModuleManager>();
        cachedModules = moduleManager != null
            ? moduleManager.GetComponentsInChildren<ModuleDamageController>(true)
            : GetComponentsInChildren<ModuleDamageController>(true);
    }

    private static bool IsCrewType(ModuleType type)
    {
        return type == ModuleType.Commander
            || type == ModuleType.Gunner
            || type == ModuleType.Loader
            || type == ModuleType.Driver
            || type == ModuleType.MachineGunner;
    }

    private string GetCurrentRoleLabel(ModuleType crewType)
    {
        if (crewManager == null) return GetSeatDisplayName(crewType);
        ModuleType seat = crewManager.GetAssignedSeatForCrew(crewType);
        return (seat != ModuleType.Commander) ? GetSeatDisplayName(seat) : GetSeatDisplayName(crewType);
    }

    private static string GetSeatDisplayName(ModuleType seatType)
    {
        switch (seatType)
        {
            case ModuleType.Driver:       return "Driver";
            case ModuleType.Gunner:       return "Gunner";
            case ModuleType.Loader:       return "Loader";
            case ModuleType.MachineGunner: return "Machine Gunner";
            default:                      return seatType.ToString();
        }
    }

    private void Toggle()
    {
        if (overlayRoot == null)
            BuildUI();

        bool next = !overlayRoot.activeSelf;
        overlayRoot.SetActive(next);
        IsInputCaptured = next;

        if (!next) HideAllPopups();
        else RefreshUI();
    }

    private RectTransform CreateImageRect(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void AddOutline(GameObject go, Color color, float thickness)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor    = color;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font      = builtinFont != null ? builtinFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text      = content;
        text.fontSize  = fontSize;
        text.alignment = alignment;
        text.color     = color;
        text.raycastTarget = false;
        return text;
    }

    private void DestroyOverlayRoot()
    {
        if (overlayRoot == null) return;

        if (Application.isPlaying) Destroy(overlayRoot);
        else DestroyImmediate(overlayRoot);

        overlayRoot = null;
        IsInputCaptured = false;
    }

    private Vector2 GetResponsivePanelSize()
    {
        float width  = Mathf.Clamp(Screen.width  * panelWidthRatio,  minPanelSize.x, maxPanelSize.x);
        float height = Mathf.Clamp(Screen.height * panelHeightRatio, minPanelSize.y, maxPanelSize.y);
        return new Vector2(width, height);
    }
}

public sealed class ModuleRowClickHandler : MonoBehaviour, IPointerClickHandler
{
    public Action<PointerEventData.InputButton> OnClicked;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClicked?.Invoke(eventData.button);
    }
}
