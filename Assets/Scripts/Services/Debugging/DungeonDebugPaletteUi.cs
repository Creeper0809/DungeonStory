using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class DungeonDebugTargetResolver
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IMainCameraProvider cameraProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;

    public DungeonDebugTargetResolver(
        IGridSystemProvider gridSystemProvider,
        IMainCameraProvider cameraProvider,
        IWorldItemStackRuntime itemStackRuntime)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.cameraProvider = cameraProvider
            ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public bool TryResolve(
        DungeonDebugTargetKind targetKind,
        Vector2 screenPosition,
        out DungeonDebugTargetSelection selection,
        out string failureReason)
    {
        selection = new DungeonDebugTargetSelection();
        failureReason = string.Empty;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "그리드가 준비되지 않았습니다.";
            return false;
        }

        Camera camera = cameraProvider.Camera;
        Vector3 screen = new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z);
        Vector3 world = camera.ScreenToWorldPoint(screen);
        Vector2Int gridPosition = grid.GetXY(world);
        bool validCell = grid.IsValidGridPos(gridPosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(world);

        selection.GridPosition = gridPosition;
        selection.HasGridPosition = validCell;
        selection.Kind = targetKind;

        switch (targetKind)
        {
            case DungeonDebugTargetKind.GridCell:
                if (!validCell)
                {
                    failureReason = "그리드 바깥입니다.";
                    return false;
                }

                return true;

            case DungeonDebugTargetKind.Character:
                selection.Character = FindExact<CharacterActor>(hits);
                selection.SourceObject = selection.Character;
                break;

            case DungeonDebugTargetKind.Wildlife:
                selection.Wildlife = FindExact<WildlifeActor>(hits);
                selection.SourceObject = selection.Wildlife;
                break;

            case DungeonDebugTargetKind.Building:
                selection.Building = FindExact<BuildableObject>(hits);
                if (selection.Building == null && validCell)
                {
                    selection.Building = ResolveExactGridStructure(grid.GetGridCell(gridPosition));
                }
                selection.SourceObject = selection.Building;
                break;

            case DungeonDebugTargetKind.ItemPile:
                WorldItemStackMarker marker = FindExact<WorldItemStackMarker>(hits);
                if (marker != null)
                {
                    selection.GridPosition = marker.Position;
                    selection.HasGridPosition = true;
                    selection.ItemStack = itemStackRuntime
                        .GetStacksAt(marker.Position, includeStored: true)
                        .OrderBy(stack => stack.State)
                        .ThenByDescending(stack => stack.Quantity)
                        .FirstOrDefault();
                    selection.SourceObject = marker;
                }
                break;

            case DungeonDebugTargetKind.None:
                return true;
        }

        if (selection.Matches(targetKind))
        {
            return true;
        }

        failureReason = $"커서 아래에 정확한 {TargetLabel(targetKind)} 대상이 없습니다.";
        return false;
    }

    private static T FindExact<T>(IEnumerable<Collider2D> hits)
        where T : Component
    {
        if (hits == null)
        {
            return null;
        }

        foreach (Collider2D hit in hits
                     .Where(hit => hit != null)
                     .OrderByDescending(hit => hit.transform.position.z))
        {
            T candidate = hit.GetComponentInParent<T>();
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                return candidate;
            }
        }

        return null;
    }

    private static BuildableObject ResolveExactGridStructure(GridCell cell)
    {
        BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
        return building != null
            && !building.isDestroy
            && WorldInfoClickSelectionService.IsExactGridStructureDefinition(building.BuildingData)
                ? building
                : null;
    }

    private static string TargetLabel(DungeonDebugTargetKind kind)
    {
        return kind switch
        {
            DungeonDebugTargetKind.Character => "캐릭터",
            DungeonDebugTargetKind.Wildlife => "야생동물",
            DungeonDebugTargetKind.Building => "건물",
            DungeonDebugTargetKind.ItemPile => "아이템 마커",
            DungeonDebugTargetKind.GridCell => "그리드 칸",
            _ => "대상"
        };
    }
}

public sealed class DungeonDebugTargetingSurface : MonoBehaviour, IPointerClickHandler
{
    private Action<Vector2, PointerEventData.InputButton, bool> callback;

    public void Initialize(Action<Vector2, PointerEventData.InputButton, bool> callback)
    {
        this.callback = callback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool repeat = eventData != null
            && (UnityEngine.InputSystem.Keyboard.current?.shiftKey.isPressed ?? false);
        callback?.Invoke(
            eventData?.position ?? Vector2.zero,
            eventData?.button ?? PointerEventData.InputButton.Left,
            repeat);
    }
}

public sealed class DungeonDebugPaletteUiController :
    IStartable,
    ITickable,
    IDisposable
{
    private const int SortingOrder = 935;

    private readonly IDungeonDebugModeService modeService;
    private readonly IDungeonDebugCommandRegistry commandRegistry;
    private readonly DungeonDebugTargetResolver targetResolver;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;

    private readonly List<Button> categoryButtons = new List<Button>();
    private readonly List<GameObject> rowObjects = new List<GameObject>();
    private GameObject runtimeRoot;
    private GameObject palette;
    private GameObject targetingShield;
    private Button openButton;
    private TMP_InputField searchInput;
    private TMP_InputField numericInput;
    private TMP_Text statusText;
    private RectTransform listContent;
    private RectTransform paletteRect;
    private RectTransform categoryTabsRect;
    private GridLayoutGroup categoryTabsLayout;
    private RectTransform commandViewportRect;
    private Vector2Int lastScreenSize;
    private DungeonDebugCategory activeCategory = DungeonDebugCategory.Cheats;
    private IDungeonDebugCommand pendingCommand;
    private DungeonDebugTargetSelection currentSelection = new DungeonDebugTargetSelection();

    public DungeonDebugPaletteUiController(
        IDungeonDebugModeService modeService,
        IDungeonDebugCommandRegistry commandRegistry,
        DungeonDebugTargetResolver targetResolver,
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        this.commandRegistry = commandRegistry ?? throw new ArgumentNullException(nameof(commandRegistry));
        this.targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        this.canvasProvider = canvasProvider ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
    }

    public DungeonDebugTargetSelection CurrentSelection => currentSelection;
    public bool IsTargeting => pendingCommand != null;
    public bool IsPaletteVisible => palette != null && palette.activeSelf;

    public void Start()
    {
        CreateUi(canvasProvider.GetOrCreateCanvas());
        modeService.StateChanged += RefreshModeState;
        RefreshModeState();
    }

    public void Tick()
    {
        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (currentScreenSize != lastScreenSize)
        {
            ApplyResponsiveLayout();
        }

        if (pendingCommand != null && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            CancelTargeting("대상 선택을 취소했습니다.");
        }
    }

    public void Dispose()
    {
        modeService.StateChanged -= RefreshModeState;
        if (runtimeRoot != null)
        {
            UnityEngine.Object.Destroy(runtimeRoot);
        }
    }

    private void CreateUi(Canvas parentCanvas)
    {
        runtimeRoot = new GameObject(
            "DungeonDebugRuntimeUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        runtimeRoot.transform.SetParent(parentCanvas.transform, false);
        Stretch(runtimeRoot.GetComponent<RectTransform>());
        Canvas overlay = runtimeRoot.GetComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = SortingOrder;

        targetingShield = new GameObject(
            "DebugTargetingShield",
            typeof(RectTransform),
            typeof(Image),
            typeof(DungeonDebugTargetingSurface));
        targetingShield.transform.SetParent(runtimeRoot.transform, false);
        Stretch(targetingShield.GetComponent<RectTransform>());
        Image shieldImage = targetingShield.GetComponent<Image>();
        shieldImage.color = new Color(0f, 0f, 0f, 0.001f);
        shieldImage.raycastTarget = true;
        targetingShield.GetComponent<DungeonDebugTargetingSurface>().Initialize(HandleWorldTargetClick);
        targetingShield.SetActive(false);

        openButton = CreateButton(
            runtimeRoot.transform,
            "DungeonDebugOpenButton",
            "디버그",
            TogglePalette);
        RectTransform openRect = openButton.GetComponent<RectTransform>();
        openRect.anchorMin = openRect.anchorMax = new Vector2(0.5f, 1f);
        openRect.pivot = new Vector2(0.5f, 1f);
        openRect.anchoredPosition = new Vector2(0f, -16f);
        openRect.sizeDelta = new Vector2(138f, 48f);
        DungeonUiTheme.StyleButton(openButton, selected: true);

        palette = new GameObject("DungeonDebugPalette", typeof(RectTransform), typeof(Image));
        palette.transform.SetParent(runtimeRoot.transform, false);
        paletteRect = palette.GetComponent<RectTransform>();
        palette.GetComponent<Image>().color = DungeonUiTheme.Panel;

        TMP_Text title = CreateText(
            palette.transform,
            "DebugPaletteTitle",
            "개발자 디버그",
            25f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(title.rectTransform, new Vector2(0.025f, 0.91f), new Vector2(0.76f, 0.985f));

        Button close = CreateButton(palette.transform, "DebugPaletteClose", "닫기", ClosePalette);
        SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.86f, 0.915f), new Vector2(0.975f, 0.985f));

        searchInput = CreateInput(palette.transform, "DebugSearch", "명령 검색");
        SetAnchors(searchInput.GetComponent<RectTransform>(), new Vector2(0.025f, 0.825f), new Vector2(0.7f, 0.9f));
        searchInput.onValueChanged.AddListener(_ => RebuildRows());

        numericInput = CreateInput(palette.transform, "DebugNumericValue", "수치");
        numericInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        numericInput.text = "10";
        SetAnchors(numericInput.GetComponent<RectTransform>(), new Vector2(0.72f, 0.825f), new Vector2(0.975f, 0.9f));

        CreateCategoryTabs();
        CreateCommandList();

        statusText = CreateText(
            palette.transform,
            "DebugStatus",
            "명령을 선택하세요.",
            15f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(statusText.rectTransform, new Vector2(0.025f, 0.015f), new Vector2(0.975f, 0.075f));
        statusText.color = DungeonUiTheme.TextSecondary;

        palette.SetActive(false);
        ApplyResponsiveLayout();
    }

    private void CreateCategoryTabs()
    {
        GameObject tabs = new GameObject(
            "DebugCategoryTabs",
            typeof(RectTransform),
            typeof(GridLayoutGroup));
        tabs.transform.SetParent(palette.transform, false);
        categoryTabsRect = tabs.GetComponent<RectTransform>();
        categoryTabsLayout = tabs.GetComponent<GridLayoutGroup>();
        categoryTabsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        categoryTabsLayout.spacing = new Vector2(5f, 5f);

        DungeonDebugCategory[] categories =
        {
            DungeonDebugCategory.Cheats,
            DungeonDebugCategory.Spawn,
            DungeonDebugCategory.Character,
            DungeonDebugCategory.BuildingWork,
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugCategory.EventsDefense,
            DungeonDebugCategory.Overlay,
            DungeonDebugCategory.History
        };
        foreach (DungeonDebugCategory category in categories)
        {
            DungeonDebugCategory captured = category;
            Button button = CreateButton(
                tabs.transform,
                $"DebugTab_{captured}",
                CategoryLabel(captured),
                () => SelectCategory(captured));
            categoryButtons.Add(button);
        }
    }

    private void CreateCommandList()
    {
        GameObject viewportObject = new GameObject(
            "DebugCommandViewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D),
            typeof(ScrollRect));
        viewportObject.transform.SetParent(palette.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        commandViewportRect = viewport;
        viewportObject.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        GameObject contentObject = new GameObject(
            "DebugCommandContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        listContent = contentObject.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = Vector2.one;
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = new Vector2(8f, 0f);
        listContent.offsetMax = new Vector2(-8f, 0f);
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 6, 6);
        layout.spacing = 5f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
    }

    private void ApplyResponsiveLayout()
    {
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        bool portrait = Screen.height > Screen.width;
        if (paletteRect != null)
        {
            if (portrait)
            {
                paletteRect.anchorMin = new Vector2(0.03f, 0.04f);
                paletteRect.anchorMax = new Vector2(0.97f, 0.84f);
                paletteRect.pivot = new Vector2(0.5f, 0.5f);
                paletteRect.offsetMin = paletteRect.offsetMax = Vector2.zero;
            }
            else
            {
                paletteRect.anchorMin = paletteRect.anchorMax = new Vector2(0.5f, 1f);
                paletteRect.pivot = new Vector2(0.5f, 1f);
                paletteRect.anchoredPosition = new Vector2(0f, -76f);
                paletteRect.sizeDelta = new Vector2(920f, 650f);
            }
        }

        if (categoryTabsRect != null)
        {
            SetAnchors(
                categoryTabsRect,
                new Vector2(0.025f, portrait ? 0.66f : 0.735f),
                new Vector2(0.975f, 0.81f));
        }

        if (categoryTabsLayout != null)
        {
            int columns = portrait ? 4 : 8;
            float availableWidth = paletteRect != null
                ? Mathf.Max(320f, paletteRect.rect.width)
                : 900f;
            float horizontalPadding = availableWidth * 0.05f;
            float cellWidth = Mathf.Max(
                72f,
                (availableWidth - horizontalPadding - 5f * (columns - 1)) / columns);
            categoryTabsLayout.constraintCount = columns;
            categoryTabsLayout.cellSize = new Vector2(cellWidth, portrait ? 42f : 44f);
        }

        if (commandViewportRect != null)
        {
            SetAnchors(
                commandViewportRect,
                new Vector2(0.025f, 0.09f),
                new Vector2(0.975f, portrait ? 0.645f : 0.72f));
        }
    }

    private void TogglePalette()
    {
        if (!modeService.IsDeveloperModeEnabled)
        {
            return;
        }

        bool visible = !palette.activeSelf;
        palette.SetActive(visible);
        if (visible)
        {
            runtimeRoot.transform.SetAsLastSibling();
            RebuildRows();
        }
        else
        {
            CancelTargeting(string.Empty);
        }
    }

    private void ClosePalette()
    {
        palette.SetActive(false);
        CancelTargeting(string.Empty);
    }

    private void SelectCategory(DungeonDebugCategory category)
    {
        activeCategory = category;
        RebuildRows();
    }

    private void RebuildRows()
    {
        if (listContent == null || !palette.activeSelf)
        {
            return;
        }

        foreach (GameObject row in rowObjects)
        {
            if (row != null)
            {
                UnityEngine.Object.Destroy(row);
            }
        }
        rowObjects.Clear();

        for (int index = 0; index < categoryButtons.Count; index++)
        {
            DungeonUiTheme.StyleButton(
                categoryButtons[index],
                selected: index == (int)activeCategory);
        }

        if (activeCategory == DungeonDebugCategory.History)
        {
            BuildHistoryRows();
            return;
        }

        string search = searchInput != null ? searchInput.text?.Trim() ?? string.Empty : string.Empty;
        IEnumerable<IDungeonDebugCommand> commands = commandRegistry.Commands
            .Where(command => command.Category == activeCategory)
            .Where(command => string.IsNullOrWhiteSpace(search)
                || command.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || command.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || command.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(command => command.IsDangerous)
            .ThenBy(command => command.DisplayName, StringComparer.Ordinal);

        foreach (IDungeonDebugCommand command in commands)
        {
            CreateCommandRow(command);
        }
    }

    private void CreateCommandRow(IDungeonDebugCommand command)
    {
        GameObject row = new GameObject(
            $"DebugCommand_{command.Id}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(listContent, false);
        row.GetComponent<Image>().color = command.IsDangerous
            ? WithAlpha(DungeonUiTheme.Danger, 0.32f)
            : DungeonUiTheme.Surface;
        row.GetComponent<LayoutElement>().preferredHeight = 72f;

        TMP_Text name = CreateText(
            row.transform,
            "Name",
            command.DisplayName,
            17f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(name.rectTransform, new Vector2(0.02f, 0.48f), new Vector2(0.66f, 0.94f));
        name.color = command.IsDangerous ? new Color(1f, 0.78f, 0.76f) : DungeonUiTheme.TextPrimary;

        string target = command.TargetKind == DungeonDebugTargetKind.None
            ? string.Empty
            : $" · 대상: {TargetLabel(command.TargetKind)}";
        TMP_Text description = CreateText(
            row.transform,
            "Description",
            command.Description + target,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(description.rectTransform, new Vector2(0.02f, 0.06f), new Vector2(0.76f, 0.5f));
        description.color = DungeonUiTheme.TextSecondary;

        Button execute = CreateButton(
            row.transform,
            "Execute",
            command.TargetKind == DungeonDebugTargetKind.None ? "실행" : "대상 지정",
            () => SelectCommand(command));
        SetAnchors(execute.GetComponent<RectTransform>(), new Vector2(0.78f, 0.17f), new Vector2(0.98f, 0.83f));
        DungeonUiTheme.StyleButton(execute, destructive: command.IsDangerous);
        rowObjects.Add(row);
    }

    private void BuildHistoryRows()
    {
        IReadOnlyList<DungeonDebugCommandHistorySaveData> history = modeService.RecentCommands;
        if (history.Count == 0)
        {
            CreatePlainRow("아직 상태를 변경한 디버그 명령이 없습니다.", 56f);
            return;
        }

        foreach (DungeonDebugCommandHistorySaveData entry in history.Reverse())
        {
            CreatePlainRow(
                $"{entry.gameTime}  {entry.commandId}\n{entry.target} · {entry.result}",
                66f);
        }
    }

    private void CreatePlainRow(string value, float height)
    {
        GameObject row = new GameObject(
            "DebugHistoryRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(listContent, false);
        row.GetComponent<Image>().color = DungeonUiTheme.Surface;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = CreateText(
            row.transform,
            "Text",
            value,
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(text.rectTransform, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.92f));
        rowObjects.Add(row);
    }

    private void SelectCommand(IDungeonDebugCommand command)
    {
        if (command == null)
        {
            return;
        }

        if (command.TargetKind == DungeonDebugTargetKind.None)
        {
            Execute(command, new DungeonDebugTargetSelection(), repeat: false);
            return;
        }

        pendingCommand = command;
        targetingShield.SetActive(true);
        targetingShield.transform.SetSiblingIndex(0);
        statusText.text =
            $"{command.DisplayName}: 정확한 {TargetLabel(command.TargetKind)}을 좌클릭하세요. "
            + "Shift 반복 · 우클릭/Esc 취소";
        statusText.color = DungeonUiTheme.Warning;
    }

    private void HandleWorldTargetClick(
        Vector2 screenPosition,
        PointerEventData.InputButton button,
        bool repeat)
    {
        if (pendingCommand == null)
        {
            return;
        }

        if (button == PointerEventData.InputButton.Right)
        {
            CancelTargeting("대상 선택을 취소했습니다.");
            return;
        }

        if (button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!targetResolver.TryResolve(
                pendingCommand.TargetKind,
                screenPosition,
                out DungeonDebugTargetSelection selection,
                out string failureReason))
        {
            statusText.text = failureReason;
            statusText.color = DungeonUiTheme.Danger;
            return;
        }

        currentSelection = selection;
        IDungeonDebugCommand command = pendingCommand;
        Execute(command, selection, repeat);
        if (!repeat)
        {
            pendingCommand = null;
            targetingShield.SetActive(false);
        }
        else
        {
            statusText.text += " · 반복 대상 선택 중";
        }
    }

    private void Execute(
        IDungeonDebugCommand command,
        DungeonDebugTargetSelection selection,
        bool repeat)
    {
        float numeric = command.DefaultNumericValue;
        if (numericInput != null
            && float.TryParse(
                numericInput.text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsed))
        {
            numeric = parsed;
        }

        DungeonDebugCommandResult result = commandRegistry.Execute(
            command,
            new DungeonDebugExecutionContext
            {
                Target = selection ?? new DungeonDebugTargetSelection(),
                NumericValue = numeric,
                TextValue = searchInput?.text ?? string.Empty,
                RepeatRequested = repeat
            });
        statusText.text = result.Message;
        statusText.color = result.Success ? DungeonUiTheme.Good : DungeonUiTheme.Danger;
        RebuildRows();
    }

    private void CancelTargeting(string message)
    {
        pendingCommand = null;
        if (targetingShield != null)
        {
            targetingShield.SetActive(false);
        }

        if (!string.IsNullOrWhiteSpace(message) && statusText != null)
        {
            statusText.text = message;
            statusText.color = DungeonUiTheme.TextSecondary;
        }
    }

    private void RefreshModeState()
    {
        bool enabled = modeService.IsDeveloperModeEnabled;
        if (openButton != null)
        {
            openButton.gameObject.SetActive(enabled);
        }

        if (!enabled)
        {
            if (palette != null)
            {
                palette.SetActive(false);
            }
            CancelTargeting(string.Empty);
        }
        else if (palette != null && palette.activeSelf)
        {
            RebuildRows();
        }
    }

    private Button CreateButton(Transform parent, string name, string label, Action callback)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => callback?.Invoke());
        TMP_Text text = CreateText(
            buttonObject.transform,
            "Label",
            label,
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private TMP_InputField CreateInput(Transform parent, string name, string placeholder)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        TMP_Text text = CreateText(
            inputObject.transform,
            "Text",
            string.Empty,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(text.rectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
        text.color = DungeonUiTheme.TextPrimary;

        TMP_Text hint = CreateText(
            inputObject.transform,
            "Placeholder",
            placeholder,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        SetAnchors(hint.rectTransform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f));
        hint.color = WithAlpha(DungeonUiTheme.TextSecondary, 0.6f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = hint;
        input.targetGraphic = inputObject.GetComponent<Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = fontService.Resolve();
        text.text = value ?? string.Empty;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = DungeonUiTheme.TextPrimary;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.characterSpacing = 0f;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static string CategoryLabel(DungeonDebugCategory category)
    {
        return category switch
        {
            DungeonDebugCategory.Cheats => "치트",
            DungeonDebugCategory.Spawn => "소환",
            DungeonDebugCategory.Character => "캐릭터",
            DungeonDebugCategory.BuildingWork => "건물·작업",
            DungeonDebugCategory.SurvivalWildlife => "생존·야생",
            DungeonDebugCategory.EventsDefense => "사건·방어",
            DungeonDebugCategory.Overlay => "범위",
            DungeonDebugCategory.History => "기록",
            _ => category.ToString()
        };
    }

    private static string TargetLabel(DungeonDebugTargetKind targetKind)
    {
        return targetKind switch
        {
            DungeonDebugTargetKind.GridCell => "칸",
            DungeonDebugTargetKind.Character => "캐릭터",
            DungeonDebugTargetKind.Building => "건물",
            DungeonDebugTargetKind.ItemPile => "아이템",
            DungeonDebugTargetKind.Wildlife => "야생동물",
            _ => "전체"
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
