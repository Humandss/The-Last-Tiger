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

    private GameObject overlayRoot;
    private Font builtinFont;
    private ModuleDamageController[] cachedModules;
    private int lastScreenWidth;
    private int lastScreenHeight;

    private readonly List<CrewRow> crewRows = new List<CrewRow>();
    private readonly List<ModuleRowUI> damagedRows = new List<ModuleRowUI>();
    private readonly List<ModuleRowUI> destroyedRows = new List<ModuleRowUI>();
    private Text actionTitleText;
    private Text selectionText;
    private Button moveDriverButton;
    private Button moveGunnerButton;
    private Button moveLoaderButton;
    private Button repairDriverButton;
    private Button repairGunnerButton;
    private Button repairLoaderButton;
    private Button repairMachineGunnerButton;
    private GameObject moveButtonsRow;
    private GameObject repairButtonsRow;
    private ModuleType selectedCrewType = ModuleType.Commander;
    private ModuleDamageController selectedRepairModule;

    private sealed class CrewRow
    {
        public ModuleType type;
        public int indexWithinType;
        public Button button;
        public Text labelText;
        public Text valueText;
    }

    private sealed class ModuleRowUI
    {
        public GameObject root;
        public Text text;
        public Image background;
        public ModuleDamageController module;
    }

    private void Awake()
    {
        if (moduleManager == null)
            moduleManager = GetComponent<ModuleManager>();
        if (crewManager == null)
            crewManager = GetComponent<PlayerCrewManager>();

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
            RefreshUI();
    }

    [ContextMenu("Build Crew Interior UI")]
    public void BuildUI()
    {
        DestroyOverlayRoot();
        crewRows.Clear();
        damagedRows.Clear();
        destroyedRows.Clear();
        builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = new GameObject("PlayerCrewInteriorOverlayUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        overlayRoot = root;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

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
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        RectTransform panel = CreateImageRect("Panel", root.transform, new Vector2(0.5f, 0.5f), panelSize, panelColor);
        AddOutline(panel.gameObject, borderColor, 1f);

        BuildHeader(panel);
        BuildBody(panel);
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
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
        title.rectTransform.sizeDelta = new Vector2(-40f, 34f);

        Text hint = CreateText("Hint", panel, "TAB TO CLOSE", 14, TextAnchor.MiddleCenter, labelColor);
        hint.rectTransform.anchorMin = new Vector2(0f, 1f);
        hint.rectTransform.anchorMax = new Vector2(1f, 1f);
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
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
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        ContentSizeFitter bodyFitter = body.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        RectTransform crewColumn = CreateColumn(body.transform, "CREW");
        GameObject dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerGo.transform.SetParent(body.transform, false);
        RectTransform divider = dividerGo.GetComponent<RectTransform>();
        Image dividerImage = dividerGo.GetComponent<Image>();
        dividerImage.color = dividerColor;
        dividerImage.raycastTarget = false;
        LayoutElement dividerLayout = divider.gameObject.AddComponent<LayoutElement>();
        dividerLayout.minWidth = 1f;
        dividerLayout.preferredWidth = 1f;
        dividerLayout.flexibleWidth = 0f;
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

        RectTransform rect = column.GetComponent<RectTransform>();
        LayoutElement layout = column.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;
        layout.minWidth = 0f;
        layout.preferredWidth = panelSize.x * 0.46f;

        VerticalLayoutGroup vlg = column.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.padding = new RectOffset(0, 0, 8, 0);

        Text headerText = CreateText(header + "Header", rect, header, 24, TextAnchor.MiddleLeft, titleColor);
        LayoutElement headerLayout = headerText.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 30f;

        return rect;
    }

    private void BuildCrewColumn(RectTransform column)
    {
        AddCrewRow(column, "Commander(You)", ModuleType.Commander, 0);
        AddCrewRow(column, "Gunner", ModuleType.Gunner, 0);
        AddCrewRow(column, "Loader", ModuleType.Loader, 0);
        AddCrewRow(column, "Driver", ModuleType.Driver, 0);
        AddDividerLine(column, "DriverToMGLine");
        AddCrewRow(column, "Machine Gunner", ModuleType.MachineGunner, 0);
    }

    private void AddCrewRow(RectTransform parent, string label, ModuleType type, int indexWithinType)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);

        Image rowImage = row.GetComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.02f);
        rowImage.raycastTarget = true;

        Button rowButton = row.GetComponent<Button>();
        rowButton.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = rowButton.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.02f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.12f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0.10f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0.01f);
        rowButton.colors = cb;
        ModuleType capturedType = type;
        rowButton.onClick.AddListener(() => SelectCrew(capturedType));

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 34f;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        Text nameText = CreateText(label + "Name", row.transform, label, 21, TextAnchor.MiddleLeft, titleColor);
        LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
        nameLayout.flexibleWidth = 1f;
        nameLayout.minWidth = 180f;

        Text valueText = CreateText(label + "Value", row.transform, "OK", 19, TextAnchor.MiddleRight, normalColor);
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueText.verticalOverflow = VerticalWrapMode.Truncate;
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 220f;

        AddDividerLine(parent, label + "Line");

        crewRows.Add(new CrewRow
        {
            type = type,
            indexWithinType = indexWithinType,
            button = rowButton,
            labelText = nameText,
            valueText = valueText
        });
    }

    private void AddDividerLine(RectTransform parent, string name)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        Image lineImage = line.GetComponent<Image>();
        lineImage.color = dividerColor;
        LayoutElement lineLayout = line.AddComponent<LayoutElement>();
        lineLayout.preferredHeight = 1f;
    }

    private void BuildModuleColumn(RectTransform column)
    {
        AddSectionHeader(column, "Damaged", damagedColor);
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
        LayoutElement layout = header.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 26f;
    }

    private ModuleRowUI AddModuleRow(RectTransform parent, string name)
    {
        GameObject rowRoot = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(ModuleRowClickHandler));
        rowRoot.transform.SetParent(parent, false);

        Image bg = rowRoot.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.015f);
        bg.raycastTarget = true;

        LayoutElement layout = rowRoot.GetComponent<LayoutElement>();
        layout.preferredHeight = 24f;

        Text row = CreateText(name + "Text", rowRoot.transform, string.Empty, 18, TextAnchor.MiddleLeft, labelColor);
        row.rectTransform.anchorMin = Vector2.zero;
        row.rectTransform.anchorMax = Vector2.one;
        row.rectTransform.offsetMin = new Vector2(8f, 0f);
        row.rectTransform.offsetMax = new Vector2(-8f, 0f);
        row.horizontalOverflow = HorizontalWrapMode.Overflow;
        row.verticalOverflow = VerticalWrapMode.Truncate;

        ModuleRowUI rowUi = new ModuleRowUI
        {
            root = rowRoot,
            text = row,
            background = bg,
            module = null
        };

        ModuleRowClickHandler clickHandler = rowRoot.GetComponent<ModuleRowClickHandler>();
        clickHandler.OnClicked = button => OnModuleRowClicked(rowUi, button);

        return rowUi;
    }

    private void AddSpacer(RectTransform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(parent, false);
        LayoutElement layout = spacer.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
    }

    private void BuildActionPanel(RectTransform panel)
    {
        GameObject footer = new GameObject("ActionPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        footer.transform.SetParent(panel, false);

        RectTransform footerRect = footer.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0.5f, 0f);
        footerRect.anchorMax = new Vector2(0.5f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 18f);
        footerRect.sizeDelta = new Vector2(panelSize.x - 72f, actionPanelHeight);

        Image footerImage = footer.GetComponent<Image>();
        footerImage.color = new Color(1f, 1f, 1f, 0.03f);

        VerticalLayoutGroup footerLayout = footer.GetComponent<VerticalLayoutGroup>();
        footerLayout.padding = new RectOffset(18, 18, 12, 12);
        footerLayout.spacing = 12f;
        footerLayout.childAlignment = TextAnchor.UpperLeft;
        footerLayout.childControlHeight = true;
        footerLayout.childControlWidth = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = false;

        actionTitleText = CreateText("RoleTitle", footer.transform, "CURRENT CREW ROLE", 16, TextAnchor.MiddleLeft, titleColor);
        LayoutElement roleTitleLayout = actionTitleText.gameObject.AddComponent<LayoutElement>();
        roleTitleLayout.preferredHeight = 22f;

        selectionText = CreateText("SelectionText", footer.transform, "Select a crew member to move.", 18, TextAnchor.MiddleLeft, labelColor);
        LayoutElement selectionLayout = selectionText.gameObject.AddComponent<LayoutElement>();
        selectionLayout.preferredHeight = 24f;

        moveButtonsRow = new GameObject("MoveButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        moveButtonsRow.transform.SetParent(footer.transform, false);

        HorizontalLayoutGroup buttonsLayout = moveButtonsRow.GetComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 44f;
        buttonsLayout.childAlignment = TextAnchor.MiddleLeft;
        buttonsLayout.childControlHeight = true;
        buttonsLayout.childControlWidth = false;
        buttonsLayout.childForceExpandWidth = false;
        buttonsLayout.childForceExpandHeight = false;

        LayoutElement buttonsRowLayout = moveButtonsRow.AddComponent<LayoutElement>();
        buttonsRowLayout.preferredHeight = 40f;

        moveDriverButton = CreateActionButton(moveButtonsRow.transform, "Move Into Driver", ModuleType.Driver);
        moveGunnerButton = CreateActionButton(moveButtonsRow.transform, "Move Into Gunner", ModuleType.Gunner);
        moveLoaderButton = CreateActionButton(moveButtonsRow.transform, "Move Into Loader", ModuleType.Loader);

        repairButtonsRow = new GameObject("RepairButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        repairButtonsRow.transform.SetParent(footer.transform, false);

        HorizontalLayoutGroup repairLayout = repairButtonsRow.GetComponent<HorizontalLayoutGroup>();
        repairLayout.spacing = 26f;
        repairLayout.childAlignment = TextAnchor.MiddleLeft;
        repairLayout.childControlHeight = true;
        repairLayout.childControlWidth = false;
        repairLayout.childForceExpandWidth = false;
        repairLayout.childForceExpandHeight = false;

        LayoutElement repairButtonsRowLayout = repairButtonsRow.AddComponent<LayoutElement>();
        repairButtonsRowLayout.preferredHeight = 40f;

        repairDriverButton = CreateRepairButton(repairButtonsRow.transform, "Assign Driver", ModuleType.Driver);
        repairGunnerButton = CreateRepairButton(repairButtonsRow.transform, "Assign Gunner", ModuleType.Gunner);
        repairLoaderButton = CreateRepairButton(repairButtonsRow.transform, "Assign Loader", ModuleType.Loader);
        repairMachineGunnerButton = CreateRepairButton(repairButtonsRow.transform, "Assign Machine Gunner", ModuleType.MachineGunner);
    }

    private Button CreateActionButton(Transform parent, string label, ModuleType targetSeat)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.08f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.16f);
        cb.pressedColor = new Color(1f, 1f, 1f, 0.24f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0.16f);
        cb.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        button.colors = cb;
        button.onClick.AddListener(() => TryMoveSelectedCrew(targetSeat));

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = 280f;
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

    private Button CreateRepairButton(Transform parent, string label, ModuleType crewType)
    {
        Button button = CreateActionButton(parent, label, ModuleType.Commander);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => TryAssignRepairCrew(crewType));
        return button;
    }

    private void RefreshUI()
    {
        if (moduleManager == null)
            moduleManager = GetComponent<ModuleManager>();
        if (moduleManager == null)
            return;

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
                row.valueText.text = "EMPTY";
                row.valueText.color = emptyColor;
                if (row.button != null)
                    row.button.interactable = false;
                continue;
            }

            if (crewManager != null && crewManager.IsPlayerRepairInProgress && crewManager.RepairingCrewType == row.type)
            {
                row.valueText.text = "Repairing";
                row.valueText.color = damagedColor;
                if (row.button != null)
                    row.button.interactable = false;
            }
            else if (crewManager != null && crewManager.IsPlayerSwapInProgress && crewManager.MovingCrewType == row.type)
            {
                row.valueText.text = crewManager.GetCrewSeatLabel(row.type);
                row.valueText.color = damagedColor;
                if (row.button != null)
                    row.button.interactable = false;
            }
            else if (module.State == ModuleState.Destroyed)
            {
                row.valueText.text = "KIA";
                row.valueText.color = destroyedColor;
                if (row.button != null)
                    row.button.interactable = false;
            }
            else if (module.State == ModuleState.Damaged)
            {
                row.valueText.text = crewManager != null ? crewManager.GetCrewSeatLabel(row.type) : $"{Mathf.RoundToInt(module.Hp01 * 100f)}%";
                row.valueText.color = damagedColor;
                if (row.button != null)
                    row.button.interactable = row.type != ModuleType.Commander;
            }
            else
            {
                row.valueText.text = crewManager != null ? crewManager.GetCrewSeatLabel(row.type) : "OK";
                row.valueText.color = normalColor;
                if (row.button != null)
                    row.button.interactable = row.type != ModuleType.Commander;
            }

            if (row.labelText != null)
                row.labelText.color = selectedCrewType == row.type ? normalColor : titleColor;
        }
    }

    private void RefreshModuleRows()
    {
        List<ModuleDamageController> damaged = new List<ModuleDamageController>();
        List<ModuleDamageController> destroyed = new List<ModuleDamageController>();

        for (int i = 0; i < cachedModules.Length; i++)
        {
            ModuleDamageController module = cachedModules[i];
            if (module == null || IsCrewType(module.Type))
                continue;

            if (module.State == ModuleState.Destroyed)
                destroyed.Add(module);
            else if (module.State == ModuleState.Damaged)
                damaged.Add(module);
        }

        if (selectedRepairModule != null && selectedRepairModule.State == ModuleState.Healthy)
            selectedRepairModule = null;

        ApplyModuleList(damagedRows, damaged, damagedColor, "None");
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
                row.module = module;
                row.text.text = BuildModuleRowLabel(module);
                row.text.color = activeColor;
                row.background.color = selectedRepairModule == module
                    ? new Color(1f, 1f, 1f, 0.10f)
                    : new Color(1f, 1f, 1f, 0.015f);
            }
            else if (i == 0 && values.Count == 0)
            {
                row.module = null;
                row.text.text = emptyText;
                row.text.color = emptyColor;
                row.background.color = new Color(1f, 1f, 1f, 0.015f);
            }
            else
            {
                row.module = null;
                row.text.text = string.Empty;
                row.background.color = new Color(1f, 1f, 1f, 0.015f);
            }
        }
    }

    private void RefreshActionPanel()
    {
        if (selectionText == null) return;

        if (crewManager == null)
            crewManager = GetComponent<PlayerCrewManager>();

        if (selectedRepairModule != null)
        {
            actionTitleText.text = "REPAIR ASSIGNMENT";
            string moduleLabel = string.IsNullOrWhiteSpace(selectedRepairModule.PartName)
                ? selectedRepairModule.Type.ToString()
                : selectedRepairModule.PartName;

            if (crewManager != null && crewManager.IsPlayerRepairInProgress)
            {
                selectionText.text = $"{crewManager.RepairingCrewType} repairing {moduleLabel} ({crewManager.PlayerRepairSecondsRemaining:0.0}s)";
            }
            else
            {
                float repairDuration = crewManager != null ? crewManager.GetRepairDuration(selectedRepairModule) : 0f;
                selectionText.text = $"{moduleLabel} selected. Choose a crew to repair ({repairDuration:0.0}s).";
            }

            SetButtonsVisible(false, true);
            SetMoveButtonState(repairDriverButton, crewManager != null && crewManager.CanAssignRepairCrew(ModuleType.Driver, selectedRepairModule));
            SetMoveButtonState(repairGunnerButton, crewManager != null && crewManager.CanAssignRepairCrew(ModuleType.Gunner, selectedRepairModule));
            SetMoveButtonState(repairLoaderButton, crewManager != null && crewManager.CanAssignRepairCrew(ModuleType.Loader, selectedRepairModule));
            SetMoveButtonState(repairMachineGunnerButton, crewManager != null && crewManager.CanAssignRepairCrew(ModuleType.MachineGunner, selectedRepairModule));
            return;
        }

        actionTitleText.text = "CURRENT CREW ROLE";
        if (selectedCrewType == ModuleType.Commander)
        {
            selectionText.text = "Select Gunner, Loader, Driver, or Machine Gunner.";
            SetButtonsVisible(true, false);
            SetMoveButtonState(moveDriverButton, false);
            SetMoveButtonState(moveGunnerButton, false);
            SetMoveButtonState(moveLoaderButton, false);
            return;
        }

        string seatLabel = crewManager != null ? crewManager.GetCrewSeatLabel(selectedCrewType) : "Unknown";
        if (crewManager != null && crewManager.IsPlayerSwapInProgress)
            selectionText.text = $"{selectedCrewType} moving to {crewManager.MovingTargetSeat} ({crewManager.PlayerSwapSecondsRemaining:0.0}s)";
        else
            selectionText.text = $"{selectedCrewType} selected. Current seat: {seatLabel}";

        SetButtonsVisible(true, false);
        bool canMoveDriver = crewManager != null && crewManager.CanPlayerMoveCrew(selectedCrewType, ModuleType.Driver);
        bool canMoveGunner = crewManager != null && crewManager.CanPlayerMoveCrew(selectedCrewType, ModuleType.Gunner);
        bool canMoveLoader = crewManager != null && crewManager.CanPlayerMoveCrew(selectedCrewType, ModuleType.Loader);

        SetMoveButtonState(moveDriverButton, canMoveDriver);
        SetMoveButtonState(moveGunnerButton, canMoveGunner);
        SetMoveButtonState(moveLoaderButton, canMoveLoader);
    }

    private void SelectCrew(ModuleType crewType)
    {
        selectedCrewType = crewType;
        selectedRepairModule = null;
        RefreshUI();
    }

    private void TryMoveSelectedCrew(ModuleType targetSeat)
    {
        if (crewManager == null)
            crewManager = GetComponent<PlayerCrewManager>();
        if (crewManager == null) return;

        if (crewManager.StartPlayerCrewMove(selectedCrewType, targetSeat))
            RefreshUI();
    }

    private void TryAssignRepairCrew(ModuleType crewType)
    {
        if (crewManager == null)
            crewManager = GetComponent<PlayerCrewManager>();
        if (crewManager == null || selectedRepairModule == null)
            return;

        if (crewManager.StartPlayerRepair(crewType, selectedRepairModule))
            RefreshUI();
    }

    private void OnModuleRowClicked(ModuleRowUI row, PointerEventData.InputButton button)
    {
        if (row == null || row.module == null)
            return;

        if (button != PointerEventData.InputButton.Left)
            return;

        selectedRepairModule = row.module;
        RefreshUI();
    }

    private void SetMoveButtonState(Button button, bool enabled)
    {
        if (button == null) return;
        button.interactable = enabled;
    }

    private void SetButtonsVisible(bool showMoveButtons, bool showRepairButtons)
    {
        if (moveButtonsRow != null)
            moveButtonsRow.SetActive(showMoveButtons);
        if (repairButtonsRow != null)
            repairButtonsRow.SetActive(showRepairButtons);
    }

    private static string BuildModuleRowLabel(ModuleDamageController module)
    {
        if (module == null)
            return string.Empty;

        string label = string.IsNullOrWhiteSpace(module.PartName)
            ? module.Type.ToString()
            : module.PartName;

        if (module.State == ModuleState.Damaged)
            return $"- {label} ({Mathf.RoundToInt(module.Hp01 * 100f)}%)";

        return "- " + label;
    }

    private ModuleDamageController FindModule(ModuleType type, int indexWithinType)
    {
        if (cachedModules == null)
            return null;

        int found = 0;
        for (int i = 0; i < cachedModules.Length; i++)
        {
            ModuleDamageController module = cachedModules[i];
            if (module == null || module.Type != type)
                continue;

            if (found == indexWithinType)
                return module;

            found++;
        }

        return null;
    }

    private void CacheModules()
    {
        if (moduleManager == null)
            moduleManager = GetComponent<ModuleManager>();

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

    private void Toggle()
    {
        if (overlayRoot == null)
            BuildUI();

        bool next = !overlayRoot.activeSelf;
        overlayRoot.SetActive(next);
        IsInputCaptured = next;

        if (next)
            RefreshUI();
    }

    private RectTransform CreateImageRect(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void AddOutline(GameObject go, Color color, float thickness)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(thickness, -thickness);
    }

    private Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = builtinFont != null ? builtinFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private void DestroyOverlayRoot()
    {
        if (overlayRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(overlayRoot);
        else
            DestroyImmediate(overlayRoot);

        overlayRoot = null;
        IsInputCaptured = false;
    }

    private Vector2 GetResponsivePanelSize()
    {
        float width = Mathf.Clamp(Screen.width * panelWidthRatio, minPanelSize.x, maxPanelSize.x);
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
