using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public interface IResearchTreeWindowFactory
{
    ResearchTreeWindow Ensure(GameObject panelObject);
}

public sealed class ResearchTreeWindowFactory : IResearchTreeWindowFactory
{
    private readonly IObjectResolver objectResolver;

    public ResearchTreeWindowFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public ResearchTreeWindow Ensure(GameObject panelObject)
    {
        if (panelObject == null)
        {
            throw new ArgumentNullException(nameof(panelObject));
        }

        ResearchTreeWindow window = panelObject.GetComponent<ResearchTreeWindow>();
        if (window == null)
        {
            window = panelObject.AddComponent<ResearchTreeWindow>();
        }

        objectResolver.Inject(window);
        window.ConfigureHost();
        return window;
    }
}

public sealed class ResearchTreeWindow : MonoBehaviour
{
    private const float ToolbarHeight = 58f;
    private const float DesktopInspectorWidth = 400f;
    private const float PortraitInspectorFraction = 0.31f;
    private const float BottomTabSafeArea = 64f;
    private const float TopHudSafeArea = 82f;
    private const float RefreshInterval = 0.35f;
    private const float MinZoom = 0.55f;
    private const float MaxZoom = 1.45f;

    private readonly List<GameObject> nodeObjects = new List<GameObject>();
    private readonly List<GameObject> queueObjects = new List<GameObject>();
    private readonly List<RectTransform> queueRows = new List<RectTransform>();
    private readonly Dictionary<string, ResearchNodeState> nodeStates =
        new Dictionary<string, ResearchNodeState>(StringComparer.Ordinal);

    private IResearchProjectCatalog projectCatalog;
    private IResearchGraphLayoutService layoutService;
    private IResearchQueueCommandService queueCommands;
    private IBlueprintResearchRuntimeProvider runtimeProvider;
    private IResearchBlueprintArchiveQuery archiveQuery;
    private IFacilityShopCatalog facilityCatalog;
    private ITmpKoreanFontService fontService;
    private IDungeonUserSettingsService settingsService;
    private DungeonUserSettingsRuntimeTargets runtimeTargets;
    private IGameTimeScaleController timeScaleController;

    private RectTransform windowRoot;
    private RectTransform graphViewport;
    private RectTransform graphRoot;
    private RectTransform nodeRoot;
    private ResearchConnectorGraphic connectorGraphic;
    private RectTransform inspectorRoot;
    private RectTransform detailRoot;
    private RectTransform queueRoot;
    private TMP_InputField searchInput;
    private TMP_Text fieldFilterLabel;
    private TMP_Text feedbackText;
    private TMP_Text detailText;
    private Button projectActionButton;
    private TMP_Text projectActionLabel;
    private Button detailTabButton;
    private Button queueTabButton;
    private ResearchGraphLayout graphLayout;
    private ResearchProjectSO selectedProject;
    private ResearchField? selectedField;
    private float zoom = 1f;
    private float nextRefreshAt;
    private bool layoutReady;
    private bool initialViewApplied;
    private bool portrait;
    private bool showQueueOnPortrait;
    private Vector2 lastScreenSize;
    private bool pauseCaptured;
    private bool wasPaused;
    private float previousTimeScale;
    private bool queueDragActive;

    [Inject]
    public void Construct(
        IResearchProjectCatalog projectCatalog,
        IResearchGraphLayoutService layoutService,
        IResearchQueueCommandService queueCommands,
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IResearchBlueprintArchiveQuery archiveQuery,
        IFacilityShopCatalog facilityCatalog,
        ITmpKoreanFontService fontService,
        IDungeonUserSettingsService settingsService,
        DungeonUserSettingsRuntimeTargets runtimeTargets,
        IGameTimeScaleController timeScaleController)
    {
        this.projectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
        this.layoutService = layoutService
            ?? throw new ArgumentNullException(nameof(layoutService));
        this.queueCommands = queueCommands
            ?? throw new ArgumentNullException(nameof(queueCommands));
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.archiveQuery = archiveQuery
            ?? throw new ArgumentNullException(nameof(archiveQuery));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        this.settingsService = settingsService
            ?? throw new ArgumentNullException(nameof(settingsService));
        this.runtimeTargets = runtimeTargets
            ?? throw new ArgumentNullException(nameof(runtimeTargets));
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
    }

    public void ConfigureHost()
    {
        RectTransform host = GetComponent<RectTransform>();
        if (host == null)
        {
            return;
        }

        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.pivot = new Vector2(0.5f, 0.5f);
        host.offsetMin = new Vector2(0f, BottomTabSafeArea);
        host.offsetMax = new Vector2(0f, -TopHudSafeArea);
        Image hostImage = GetComponent<Image>();
        if (hostImage != null)
        {
            hostImage.color = DungeonUiTheme.Panel;
        }

        foreach (Transform child in transform)
        {
            if (child.name is "Title" or "Body")
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void OnEnable()
    {
        CaptureOptionalPause();
        Refresh();
    }

    private void OnDisable()
    {
        RestoreOptionalPause();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GetComponent<UITab>()?.CloseTab();
            return;
        }

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        if (screenSize != lastScreenSize)
        {
            lastScreenSize = screenSize;
            ApplyResponsiveLayout();
        }

        if (!queueDragActive && Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + RefreshInterval;
            RefreshDynamicContent();
        }
    }

    public void Refresh()
    {
        EnsureLayout();
        graphLayout = layoutService.Build(projectCatalog.Projects);
        if (selectedProject == null)
        {
            selectedProject = projectCatalog.Projects.FirstOrDefault();
        }

        ResizeGraph();
        RebuildNodesAndConnections();
        RebuildInspector();
        if (!layoutReady)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        if (!initialViewApplied)
        {
            FitView();
            initialViewApplied = true;
        }
    }

    public void Pan(Vector2 delta)
    {
        if (graphRoot == null)
        {
            return;
        }

        graphRoot.anchoredPosition += delta;
    }

    public void Zoom(PointerEventData eventData)
    {
        if (graphRoot == null || graphViewport == null)
        {
            return;
        }

        float next = Mathf.Clamp(
            zoom * (eventData.scrollDelta.y > 0f ? 1.1f : 0.9f),
            MinZoom,
            MaxZoom);
        if (Mathf.Approximately(next, zoom))
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            graphViewport,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointer);
        Vector2 graphPoint = (pointer - graphRoot.anchoredPosition) / zoom;
        zoom = next;
        graphRoot.localScale = Vector3.one * zoom;
        graphRoot.anchoredPosition = pointer - graphPoint * zoom;
    }

    public void SelectProject(ResearchProjectSO project)
    {
        if (project == null)
        {
            return;
        }

        selectedProject = project;
        RebuildNodesAndConnections();
        RebuildInspector();
    }

    public bool CenterProject(ResearchProjectSO project)
    {
        if (project == null
            || graphRoot == null
            || graphViewport == null
            || graphLayout == null
            || !graphLayout.NodeRects.TryGetValue(project.ProjectId.Value, out Rect rect))
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        Vector2 nodeCenter = new Vector2(rect.center.x, -rect.center.y);
        Vector3 nodeWorldPosition = graphRoot.TransformPoint(nodeCenter);
        Vector2 nodePositionInViewport = graphViewport.InverseTransformPoint(nodeWorldPosition);
        graphRoot.anchoredPosition += graphViewport.rect.center - nodePositionInViewport;
        return true;
    }

    public void MoveQueueEntry(int fromIndex, Vector2 pointerScreenPosition)
    {
        if (queueRows.Count == 0)
        {
            return;
        }

        int targetIndex = fromIndex;
        float closest = float.MaxValue;
        for (int index = 0; index < queueRows.Count; index++)
        {
            Vector3 center = queueRows[index].TransformPoint(queueRows[index].rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, center);
            float distance = Mathf.Abs(screen.y - pointerScreenPosition.y);
            if (distance < closest)
            {
                closest = distance;
                targetIndex = index;
            }
        }

        ResearchQueueCommandResult result = queueCommands.Move(fromIndex, targetIndex);
        ShowFeedback(result.Message, result.Succeeded);
        RefreshDynamicContent();
    }

    public void BeginQueueDrag()
    {
        queueDragActive = true;
    }

    public void EndQueueDrag()
    {
        queueDragActive = false;
        nextRefreshAt = Time.unscaledTime + RefreshInterval;
    }

    private void EnsureLayout()
    {
        if (layoutReady)
        {
            return;
        }

        ConfigureHost();
        ClearGeneratedChildren();
        windowRoot = CreateRect("ResearchTreeWindow", transform);
        Stretch(windowRoot);
        CreateImage(windowRoot.gameObject, DungeonUiTheme.Panel);

        RectTransform toolbar = CreateRect("Toolbar", windowRoot);
        toolbar.anchorMin = new Vector2(0f, 1f);
        toolbar.anchorMax = Vector2.one;
        toolbar.pivot = new Vector2(0.5f, 1f);
        toolbar.sizeDelta = new Vector2(0f, ToolbarHeight);
        CreateImage(toolbar.gameObject, DungeonUiTheme.SurfaceRaised);

        TMP_Text title = CreateText(toolbar, "Title", "연구", 26f, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.16f, 1f), 18f, 0f, -4f, 0f);

        searchInput = CreateInput(toolbar, "Search", "연구·시설·조합식 검색");
        SetRect(searchInput.GetComponent<RectTransform>(),
            new Vector2(0.16f, 0.14f), new Vector2(0.51f, 0.86f), 0f, 0f, 0f, 0f);
        searchInput.onValueChanged.AddListener(_ => RefreshDynamicContent());

        Button fieldButton = CreateButton(toolbar, "FieldFilter", "전체 분야", CycleField);
        SetRect(fieldButton.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.14f), new Vector2(0.67f, 0.86f), 0f, 0f, 0f, 0f);
        fieldFilterLabel = fieldButton.GetComponentInChildren<TMP_Text>();

        Button fitButton = CreateButton(toolbar, "Fit", "맞춤", FitView);
        SetRect(fitButton.GetComponent<RectTransform>(),
            new Vector2(0.68f, 0.14f), new Vector2(0.77f, 0.86f), 0f, 0f, 0f, 0f);
        Button centerButton = CreateButton(toolbar, "Center", "선택 이동", CenterSelected);
        SetRect(centerButton.GetComponent<RectTransform>(),
            new Vector2(0.78f, 0.14f), new Vector2(0.9f, 0.86f), 0f, 0f, 0f, 0f);
        Button closeButton = CreateButton(toolbar, "Close", "닫기", () => GetComponent<UITab>()?.CloseTab());
        SetRect(closeButton.GetComponent<RectTransform>(),
            new Vector2(0.91f, 0.14f), new Vector2(0.99f, 0.86f), 0f, 0f, 0f, 0f);

        RectTransform main = CreateRect("Main", windowRoot);
        SetRect(main, Vector2.zero, Vector2.one, 0f, 0f, 0f, -ToolbarHeight);

        graphViewport = CreateRect("GraphViewport", main);
        CreateImage(graphViewport.gameObject, DungeonUiTheme.SurfaceMuted);
        graphViewport.gameObject.AddComponent<RectMask2D>();
        ResearchTreePanSurface pan = graphViewport.gameObject.AddComponent<ResearchTreePanSurface>();
        pan.Bind(this);

        graphRoot = CreateRect("GraphRoot", graphViewport);
        graphRoot.anchorMin = new Vector2(0f, 1f);
        graphRoot.anchorMax = new Vector2(0f, 1f);
        graphRoot.pivot = new Vector2(0f, 1f);
        graphRoot.anchoredPosition = Vector2.zero;
        graphRoot.gameObject.AddComponent<CanvasRenderer>();
        connectorGraphic = graphRoot.gameObject.AddComponent<ResearchConnectorGraphic>();
        connectorGraphic.raycastTarget = false;

        nodeRoot = CreateRect("Nodes", graphRoot);
        nodeRoot.anchorMin = new Vector2(0f, 1f);
        nodeRoot.anchorMax = new Vector2(0f, 1f);
        nodeRoot.pivot = new Vector2(0f, 1f);
        nodeRoot.anchoredPosition = Vector2.zero;

        inspectorRoot = CreateRect("Inspector", main);
        CreateImage(inspectorRoot.gameObject, DungeonUiTheme.Surface);
        CreateInspectorContents();

        feedbackText = CreateText(windowRoot, "Feedback", string.Empty, 15f, TextAlignmentOptions.Center);
        feedbackText.color = DungeonUiTheme.Warning;
        feedbackText.rectTransform.anchorMin = new Vector2(0.3f, 0f);
        feedbackText.rectTransform.anchorMax = new Vector2(0.7f, 0f);
        feedbackText.rectTransform.pivot = new Vector2(0.5f, 0f);
        feedbackText.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        feedbackText.rectTransform.sizeDelta = new Vector2(0f, 26f);

        layoutReady = true;
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        ApplyResponsiveLayout();
    }

    private void CreateInspectorContents()
    {
        RectTransform tabs = CreateRect("InspectorTabs", inspectorRoot);
        tabs.anchorMin = new Vector2(0f, 1f);
        tabs.anchorMax = Vector2.one;
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.sizeDelta = new Vector2(0f, 46f);

        detailTabButton = CreateButton(tabs, "DetailTab", "상세", () =>
        {
            showQueueOnPortrait = false;
            ApplyInspectorTabState();
        });
        SetRect(detailTabButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0.5f, 1f), 4f, 3f, -2f, -3f);
        queueTabButton = CreateButton(tabs, "QueueTab", "연구 큐", () =>
        {
            showQueueOnPortrait = true;
            ApplyInspectorTabState();
        });
        SetRect(queueTabButton.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(1f, 1f), 2f, 3f, -4f, -3f);

        detailRoot = CreateRect("Detail", inspectorRoot);
        detailRoot.anchorMin = Vector2.zero;
        detailRoot.anchorMax = Vector2.one;
        detailRoot.offsetMin = new Vector2(14f, 14f);
        detailRoot.offsetMax = new Vector2(-14f, -54f);

        detailText = CreateText(detailRoot, "DetailText", string.Empty, 17f, TextAlignmentOptions.TopLeft);
        detailText.textWrappingMode = TextWrappingModes.Normal;
        SetRect(detailText.rectTransform, new Vector2(0f, 0.17f), Vector2.one, 0f, 0f, 0f, 0f);

        projectActionButton = CreateButton(detailRoot, "ProjectAction", "연구 예약", ToggleSelectedProjectQueue);
        SetRect(projectActionButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(1f, 0.14f), 0f, 0f, 0f, 0f);
        projectActionLabel = projectActionButton.GetComponentInChildren<TMP_Text>();

        queueRoot = CreateRect("Queue", inspectorRoot);
        queueRoot.anchorMin = Vector2.zero;
        queueRoot.anchorMax = Vector2.one;
        queueRoot.offsetMin = new Vector2(12f, 12f);
        queueRoot.offsetMax = new Vector2(-12f, -54f);
    }

    private void ApplyResponsiveLayout()
    {
        if (!layoutReady)
        {
            return;
        }

        portrait = Screen.height > Screen.width;
        if (portrait)
        {
            graphViewport.anchorMin = new Vector2(0f, PortraitInspectorFraction);
            graphViewport.anchorMax = Vector2.one;
            graphViewport.offsetMin = Vector2.zero;
            graphViewport.offsetMax = Vector2.zero;
            inspectorRoot.anchorMin = Vector2.zero;
            inspectorRoot.anchorMax = new Vector2(1f, PortraitInspectorFraction);
            inspectorRoot.offsetMin = Vector2.zero;
            inspectorRoot.offsetMax = Vector2.zero;
        }
        else
        {
            graphViewport.anchorMin = Vector2.zero;
            graphViewport.anchorMax = Vector2.one;
            graphViewport.offsetMin = Vector2.zero;
            graphViewport.offsetMax = new Vector2(-DesktopInspectorWidth, 0f);
            inspectorRoot.anchorMin = new Vector2(1f, 0f);
            inspectorRoot.anchorMax = Vector2.one;
            inspectorRoot.pivot = new Vector2(1f, 0.5f);
            inspectorRoot.anchoredPosition = Vector2.zero;
            inspectorRoot.sizeDelta = new Vector2(DesktopInspectorWidth, 0f);
        }

        ApplyInspectorTabState();
    }

    private void ApplyInspectorTabState()
    {
        if (detailRoot == null || queueRoot == null)
        {
            return;
        }

        detailRoot.gameObject.SetActive(!portrait || !showQueueOnPortrait);
        queueRoot.gameObject.SetActive(!portrait || showQueueOnPortrait);
        detailTabButton.gameObject.SetActive(portrait);
        queueTabButton.gameObject.SetActive(portrait);

        if (!portrait)
        {
            detailRoot.anchorMin = new Vector2(0f, 0.44f);
            detailRoot.offsetMin = new Vector2(14f, 8f);
            queueRoot.anchorMax = new Vector2(1f, 0.43f);
            queueRoot.offsetMax = new Vector2(-12f, -4f);
        }
        else
        {
            detailRoot.anchorMin = Vector2.zero;
            queueRoot.anchorMax = Vector2.one;
        }

        SetButtonColor(detailTabButton, !showQueueOnPortrait || !portrait
            ? DungeonUiTheme.Accent
            : DungeonUiTheme.SurfaceRaised);
        SetButtonColor(queueTabButton, showQueueOnPortrait && portrait
            ? DungeonUiTheme.Accent
            : DungeonUiTheme.SurfaceRaised);
    }

    private void RefreshDynamicContent()
    {
        if (!layoutReady || projectCatalog == null)
        {
            return;
        }

        RebuildNodesAndConnections();
        RebuildInspector();
    }

    private void RebuildNodesAndConnections()
    {
        ClearObjects(nodeObjects);
        nodeStates.Clear();
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            connectorGraphic.SetLines(Array.Empty<ResearchConnectorLine>());
            return;
        }

        string search = searchInput?.text?.Trim() ?? string.Empty;
        foreach (ResearchProjectSO project in projectCatalog.Projects)
        {
            ResearchNodeState state = runtime.GetNodeState(project, out _);
            nodeStates[project.ProjectId.Value] = state;
            if (!graphLayout.NodeRects.TryGetValue(project.ProjectId.Value, out Rect nodeRect))
            {
                continue;
            }

            bool matches = MatchesFilter(project, search);
            CreateNode(project, state, nodeRect, matches);
        }

        List<ResearchConnectorLine> lines = graphLayout.Edges.Select(edge =>
        {
            nodeStates.TryGetValue(edge.To.Value, out ResearchNodeState state);
            return new ResearchConnectorLine(
                edge.Points,
                GetConnectorColor(state, edge.IsShortcut),
                edge.IsShortcut);
        }).ToList();
        connectorGraphic.SetLines(lines);
    }

    private void CreateNode(
        ResearchProjectSO project,
        ResearchNodeState state,
        Rect layoutRect,
        bool matches)
    {
        RectTransform node = CreateRect($"Node_{project.ProjectId.Value}", nodeRoot);
        node.anchorMin = new Vector2(0f, 1f);
        node.anchorMax = new Vector2(0f, 1f);
        node.pivot = new Vector2(0f, 1f);
        node.anchoredPosition = new Vector2(layoutRect.x, -layoutRect.y);
        node.sizeDelta = layoutRect.size;

        Image background = CreateImage(node.gameObject, GetNodeColor(state));
        background.color = WithAlpha(background.color, matches ? 1f : 0.28f);
        Button button = node.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => SelectProject(project));

        if (selectedProject == project)
        {
            Outline outline = node.gameObject.AddComponent<Outline>();
            outline.effectColor = DungeonUiTheme.Accent;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        TMP_Text field = CreateText(node, "Field", FormatField(project.Field), 14f, TextAlignmentOptions.TopLeft);
        field.color = DungeonUiTheme.TextSecondary;
        SetRect(field.rectTransform, new Vector2(0f, 0.7f), new Vector2(1f, 1f), 12f, 3f, -70f, -5f);

        TMP_Text stateText = CreateText(node, "State", FormatNodeState(state), 13f, TextAlignmentOptions.TopRight);
        stateText.color = GetStateTextColor(state);
        SetRect(stateText.rectTransform, new Vector2(0.56f, 0.7f), new Vector2(1f, 1f), 0f, 3f, -10f, -5f);

        TMP_Text name = CreateText(node, "Name", project.DisplayName, 21f, TextAlignmentOptions.MidlineLeft);
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.Normal;
        SetRect(name.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 0.72f), 12f, 0f, -10f, 0f);

        float ratio = runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            ? runtime.State.Projects.GetProgress(project.ProjectId).GetRatio(project)
            : 0f;
        RectTransform progressBack = CreateRect("Progress", node);
        SetRect(progressBack, new Vector2(0f, 0f), new Vector2(1f, 0.12f), 10f, 8f, -10f, -2f);
        CreateImage(progressBack.gameObject, DungeonUiTheme.SurfaceMuted);
        RectTransform progressFill = CreateRect("Fill", progressBack);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(ratio, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        CreateImage(progressFill.gameObject, DungeonUiTheme.Accent);

        nodeObjects.Add(node.gameObject);
    }

    private void RebuildInspector()
    {
        RebuildDetail();
        RebuildQueue();
        ApplyInspectorTabState();
    }

    private void RebuildDetail()
    {
        if (selectedProject == null
            || !runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            detailText.text = "연구 노드를 선택하세요.";
            projectActionButton.interactable = false;
            return;
        }

        ResearchNodeState state = runtime.GetNodeState(selectedProject, out string blocker);
        ResearchProjectProgressState progress =
            runtime.State.Projects.GetProgress(selectedProject.ProjectId);
        string prerequisites = selectedProject.Prerequisites.Count == 0
            ? "없음"
            : string.Join(", ", selectedProject.Prerequisites.Select(project =>
                runtime.State.Projects.IsCompleted(project.ProjectId)
                    ? $"{project.DisplayName} (완료)"
                    : project.DisplayName));
        string blueprint = FormatBlueprintDetail(selectedProject);
        string unlocks = FormatUnlocks(selectedProject);
        detailText.text =
            $"<b>{selectedProject.DisplayName}</b>\n" +
            $"{FormatField(selectedProject.Field)} · {FormatNodeState(state)}\n\n" +
            $"{selectedProject.Description}\n\n" +
            $"<b>진행</b>  {progress.Progress:0.#} / {selectedProject.RequiredWork:0.#}\n" +
            $"<b>선행</b>  {prerequisites}\n" +
            $"<b>설계도</b>  {blueprint}\n" +
            $"<b>해금</b>  {unlocks}" +
            (string.IsNullOrWhiteSpace(blocker) ? string.Empty : $"\n\n<color=#D2A449><b>중단 사유</b>  {blocker}</color>");

        bool queued = runtime.State.Projects.ContainsInQueue(selectedProject.ProjectId);
        projectActionLabel.text = queued ? "연구 큐에서 제거" : "연구 예약";
        projectActionButton.interactable = state != ResearchNodeState.Completed
            && (queued || state is ResearchNodeState.Available
                or ResearchNodeState.ShortcutAvailable
                or ResearchNodeState.Queued
                or ResearchNodeState.Suspended);
        SetButtonColor(projectActionButton, queued
            ? DungeonUiTheme.Warning
            : DungeonUiTheme.Accent);
    }

    private void RebuildQueue()
    {
        ClearObjects(queueObjects);
        queueRows.Clear();
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            return;
        }

        TMP_Text heading = CreateText(queueRoot, "QueueHeading", "연구 큐", 20f, TextAlignmentOptions.TopLeft);
        heading.fontStyle = FontStyles.Bold;
        heading.rectTransform.anchorMin = new Vector2(0f, 1f);
        heading.rectTransform.anchorMax = Vector2.one;
        heading.rectTransform.pivot = new Vector2(0.5f, 1f);
        heading.rectTransform.anchoredPosition = Vector2.zero;
        heading.rectTransform.sizeDelta = new Vector2(0f, 30f);
        queueObjects.Add(heading.gameObject);

        float y = -36f;
        IReadOnlyList<ResearchQueueEntry> queue = runtime.State.Projects.Queue;
        for (int index = 0; index < queue.Count; index++)
        {
            ResearchQueueEntry entry = queue[index];
            if (!projectCatalog.TryGet(entry.ProjectId, out ResearchProjectSO project))
            {
                continue;
            }

            RectTransform row = CreateRect($"Queue_{entry.ProjectId.Value}", queueRoot);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(0f, 54f);
            CreateImage(row.gameObject,
                runtime.State.Projects.ActiveProjectId.Equals(entry.ProjectId)
                    ? DungeonUiTheme.AccentPressed
                    : DungeonUiTheme.SurfaceRaised);

            string prefix = runtime.State.Projects.ActiveProjectId.Equals(entry.ProjectId)
                ? "진행"
                : $"{index + 1}";
            string suffix = entry.IsSuspended ? $"\n{entry.SuspendedReason}" : string.Empty;
            TMP_Text label = CreateText(
                row,
                "Label",
                $"{prefix}  {project.DisplayName}{suffix}",
                entry.IsSuspended ? 13f : 16f,
                TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            SetRect(label.rectTransform, Vector2.zero, Vector2.one, 12f, 2f, -8f, -2f);

            ResearchQueueRowDrag drag = row.gameObject.AddComponent<ResearchQueueRowDrag>();
            drag.Bind(this, index);
            queueRows.Add(row);
            queueObjects.Add(row.gameObject);
            y -= 60f;
        }

        if (queue.Count == 0)
        {
            TMP_Text empty = CreateText(queueRoot, "Empty", "예약된 연구가 없습니다.", 16f, TextAlignmentOptions.TopLeft);
            empty.color = DungeonUiTheme.TextSecondary;
            empty.rectTransform.anchorMin = new Vector2(0f, 1f);
            empty.rectTransform.anchorMax = Vector2.one;
            empty.rectTransform.pivot = new Vector2(0.5f, 1f);
            empty.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            empty.rectTransform.sizeDelta = new Vector2(0f, 34f);
            queueObjects.Add(empty.gameObject);
        }
    }

    private void ToggleSelectedProjectQueue()
    {
        if (selectedProject == null
            || !runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            return;
        }

        ResearchQueueCommandResult result =
            runtime.State.Projects.ContainsInQueue(selectedProject.ProjectId)
                ? queueCommands.Remove(selectedProject.ProjectId)
                : queueCommands.Enqueue(selectedProject.ProjectId);
        ShowFeedback(result.Message, result.Succeeded);
        RefreshDynamicContent();
    }

    private void CycleField()
    {
        ResearchField?[] options = new ResearchField?[] { null }
            .Concat(Enum.GetValues(typeof(ResearchField))
                .Cast<ResearchField>()
                .Select(field => (ResearchField?)field))
            .ToArray();
        int current = Array.IndexOf(options, selectedField);
        selectedField = options[(current + 1) % options.Length];
        fieldFilterLabel.text = selectedField.HasValue
            ? FormatField(selectedField.Value)
            : "전체 분야";
        RefreshDynamicContent();
    }

    private bool MatchesFilter(ResearchProjectSO project, string search)
    {
        if (selectedField.HasValue && project.Field != selectedField.Value)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return project.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || project.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            || FormatUnlocks(project).Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatBlueprintDetail(ResearchProjectSO project)
    {
        if (project.BlueprintRule == ResearchBlueprintRule.None)
        {
            return "필요 없음";
        }

        ResearchBlueprintArchiveStatus status = archiveQuery.GetStatus(project.Blueprint);
        string rule = project.BlueprintRule == ResearchBlueprintRule.Required
            ? "필수"
            : "선행 우회";
        string location = status.IsArchived
            ? status.Location
            : status.IsInTransit
                ? "운반 중"
                : "미보유";
        return $"{project.Blueprint.DisplayName} ({rule}, {location})";
    }

    private string FormatUnlocks(ResearchProjectSO project)
    {
        List<string> values = new List<string>();
        foreach (BlueprintUnlock unlock in project.Unlocks.Where(unlock => unlock != null))
        {
            switch (unlock)
            {
                case IBlueprintBuildingUnlock buildingUnlock:
                {
                    BuildingSO building = FacilityShopService.FindBuildingById(
                        facilityCatalog,
                        buildingUnlock.BuildingId);
                    values.Add(building != null
                        ? FacilityShopService.GetBuildingName(building)
                        : $"시설 {buildingUnlock.BuildingId}");
                    break;
                }
                case BlueprintRecipeUnlock recipe:
                    values.Add(recipe.recipeId);
                    break;
            }
        }

        return values.Count == 0 ? "없음" : string.Join(", ", values.Distinct());
    }

    private void ResizeGraph()
    {
        if (graphLayout == null || graphRoot == null)
        {
            return;
        }

        graphRoot.sizeDelta = graphLayout.Bounds.size;
        nodeRoot.sizeDelta = graphLayout.Bounds.size;
        connectorGraphic.rectTransform.sizeDelta = graphLayout.Bounds.size;
    }

    private void FitView()
    {
        if (graphLayout == null || graphViewport == null || graphRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        Vector2 viewport = graphViewport.rect.size;
        if (viewport.x <= 1f || viewport.y <= 1f)
        {
            return;
        }

        zoom = Mathf.Clamp(
            Mathf.Min(
                (viewport.x - 36f) / graphLayout.Bounds.width,
                (viewport.y - 36f) / graphLayout.Bounds.height),
            MinZoom,
            1f);
        graphRoot.localScale = Vector3.one * zoom;
        graphRoot.anchoredPosition = new Vector2(
            Mathf.Max(18f, (viewport.x - graphLayout.Bounds.width * zoom) * 0.5f),
            -Mathf.Max(18f, (viewport.y - graphLayout.Bounds.height * zoom) * 0.5f));
    }

    private void CenterSelected()
    {
        CenterProject(selectedProject);
    }

    private void ShowFeedback(string message, bool success)
    {
        if (feedbackText == null)
        {
            return;
        }

        feedbackText.text = message ?? string.Empty;
        feedbackText.color = success ? DungeonUiTheme.Good : DungeonUiTheme.Warning;
    }

    private void CaptureOptionalPause()
    {
        if (pauseCaptured || settingsService?.Current.pauseOnResearchTree != true)
        {
            return;
        }

        GameManager gameManager = runtimeTargets?.GameManager;
        wasPaused = gameManager != null && gameManager.isPause;
        previousTimeScale = timeScaleController.Scale;
        pauseCaptured = true;
        if (gameManager != null)
        {
            gameManager.isPause = true;
        }
        timeScaleController.Scale = 0f;
    }

    private void RestoreOptionalPause()
    {
        if (!pauseCaptured)
        {
            return;
        }

        GameManager gameManager = runtimeTargets?.GameManager;
        if (gameManager != null)
        {
            gameManager.isPause = wasPaused;
        }
        timeScaleController.Scale = wasPaused ? 0f : previousTimeScale;
        pauseCaptured = false;
    }

    private void ClearGeneratedChildren()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child.name is "Title" or "Body")
            {
                child.gameObject.SetActive(false);
                continue;
            }
            Destroy(child.gameObject);
        }
    }

    private static void ClearObjects(List<GameObject> objects)
    {
        foreach (GameObject item in objects)
        {
            if (item != null)
            {
                item.SetActive(false);
                Destroy(item);
            }
        }
        objects.Clear();
    }

    private static string FormatField(ResearchField field)
    {
        return field switch
        {
            ResearchField.LifeAndSurvival => "생활·생존",
            ResearchField.CommerceAndCraft => "상업·제작",
            ResearchField.DefenseAndTactics => "방어·전술",
            ResearchField.RecordsAndArcane => "기록·비전",
            ResearchField.CaptivityAndEntertainment => "포로·흥행",
            ResearchField.AuthorityAndHousing => "권위·주거",
            ResearchField.Agriculture => "재배",
            ResearchField.Forestry => "임업",
            ResearchField.Mining => "채광",
            ResearchField.Husbandry => "축산",
            ResearchField.Metallurgy => "금속",
            ResearchField.Textiles => "직물",
            ResearchField.Cuisine => "요리",
            ResearchField.Pharmacology => "약리",
            ResearchField.SurgeryAndTransplant => "외과·이식",
            _ => "기타"
        };
    }

    private static string FormatNodeState(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => "완료",
            ResearchNodeState.Active => "진행 중",
            ResearchNodeState.Queued => "대기",
            ResearchNodeState.Suspended => "일시 중단",
            ResearchNodeState.Available => "연구 가능",
            ResearchNodeState.BlueprintInTransit => "설계도 운반 중",
            ResearchNodeState.ShortcutAvailable => "설계도 우회 가능",
            _ => "조건 부족"
        };
    }

    private static Color GetNodeColor(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => new Color(0.16f, 0.34f, 0.27f, 1f),
            ResearchNodeState.Active => DungeonUiTheme.AccentPressed,
            ResearchNodeState.Queued => new Color(0.23f, 0.31f, 0.36f, 1f),
            ResearchNodeState.Suspended => new Color(0.34f, 0.27f, 0.2f, 1f),
            ResearchNodeState.Available => DungeonUiTheme.SurfaceRaised,
            ResearchNodeState.BlueprintInTransit => new Color(0.28f, 0.31f, 0.22f, 1f),
            ResearchNodeState.ShortcutAvailable => new Color(0.38f, 0.31f, 0.13f, 1f),
            _ => new Color(0.11f, 0.16f, 0.18f, 1f)
        };
    }

    private static Color GetStateTextColor(ResearchNodeState state)
    {
        return state switch
        {
            ResearchNodeState.Completed => DungeonUiTheme.Good,
            ResearchNodeState.Active => Color.white,
            ResearchNodeState.Suspended => DungeonUiTheme.Warning,
            ResearchNodeState.ShortcutAvailable => DungeonUiTheme.Warning,
            _ => DungeonUiTheme.TextSecondary
        };
    }

    private static Color GetConnectorColor(ResearchNodeState state, bool shortcut)
    {
        if (shortcut)
        {
            return new Color(0.83f, 0.64f, 0.23f, 0.9f);
        }
        return state is ResearchNodeState.Completed or ResearchNodeState.Active
            ? DungeonUiTheme.Accent
            : new Color(0.42f, 0.5f, 0.52f, 0.42f);
    }

    private TMP_InputField CreateInput(Transform parent, string name, string placeholder)
    {
        RectTransform root = CreateRect(name, parent);
        CreateImage(root.gameObject, DungeonUiTheme.SurfaceMuted);
        TMP_Text placeholderText = CreateText(root, "Placeholder", placeholder, 16f, TextAlignmentOptions.MidlineLeft);
        placeholderText.color = DungeonUiTheme.TextSecondary;
        SetRect(placeholderText.rectTransform, Vector2.zero, Vector2.one, 12f, 0f, -10f, 0f);
        TMP_Text value = CreateText(root, "Text", string.Empty, 16f, TextAlignmentOptions.MidlineLeft);
        SetRect(value.rectTransform, Vector2.zero, Vector2.one, 12f, 0f, -10f, 0f);
        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.textComponent = value;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    private Button CreateButton(Transform parent, string name, string label, Action onClick)
    {
        RectTransform root = CreateRect(name, parent);
        Image image = CreateImage(root.gameObject, DungeonUiTheme.SurfaceRaised);
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = DungeonUiTheme.SurfaceRaised;
        colors.highlightedColor = DungeonUiTheme.AccentHover;
        colors.pressedColor = DungeonUiTheme.AccentPressed;
        colors.selectedColor = DungeonUiTheme.Accent;
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());
        TMP_Text text = CreateText(root, "Label", label, 16f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        fontService.Apply(text);
        text.text = value;
        text.fontSize = size;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created.GetComponent<RectTransform>();
    }

    private static Image CreateImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button?.targetGraphic != null)
        {
            button.targetGraphic.color = color;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}

public sealed class ResearchTreePanSurface :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IScrollHandler
{
    private ResearchTreeWindow owner;
    private Vector2 previous;

    public void Bind(ResearchTreeWindow window)
    {
        owner = window;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        previous = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - previous;
        previous = eventData.position;
        owner?.Pan(delta);
    }

    public void OnScroll(PointerEventData eventData)
    {
        owner?.Zoom(eventData);
    }
}

public sealed class ResearchQueueRowDrag :
    MonoBehaviour,
    IBeginDragHandler,
    IEndDragHandler
{
    private ResearchTreeWindow owner;
    private int index;

    public void Bind(ResearchTreeWindow window, int queueIndex)
    {
        owner = window;
        index = queueIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginQueueDrag();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.MoveQueueEntry(index, eventData.position);
        owner?.EndQueueDrag();
    }
}

public readonly struct ResearchConnectorLine
{
    public ResearchConnectorLine(
        IReadOnlyList<Vector2> points,
        Color color,
        bool dotted)
    {
        Points = points ?? Array.Empty<Vector2>();
        Color = color;
        Dotted = dotted;
    }

    public IReadOnlyList<Vector2> Points { get; }
    public Color Color { get; }
    public bool Dotted { get; }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ResearchConnectorGraphic : MaskableGraphic
{
    private readonly List<ResearchConnectorLine> lines = new List<ResearchConnectorLine>();

    public void SetLines(IEnumerable<ResearchConnectorLine> source)
    {
        lines.Clear();
        lines.AddRange(source ?? Array.Empty<ResearchConnectorLine>());
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (ResearchConnectorLine line in lines)
        {
            for (int index = 0; index + 1 < line.Points.Count; index++)
            {
                Vector2 from = ToCanvasPoint(line.Points[index]);
                Vector2 to = ToCanvasPoint(line.Points[index + 1]);
                if (line.Dotted)
                {
                    AddDottedSegment(vh, from, to, 3f, 10f, 7f, line.Color);
                }
                else
                {
                    AddSegment(vh, from, to, 4f, line.Color);
                }
            }
        }
    }

    private static Vector2 ToCanvasPoint(Vector2 layoutPoint)
    {
        return new Vector2(layoutPoint.x, -layoutPoint.y);
    }

    private static void AddDottedSegment(
        VertexHelper vh,
        Vector2 from,
        Vector2 to,
        float width,
        float dash,
        float gap,
        Color color)
    {
        float length = Vector2.Distance(from, to);
        if (length <= 0.01f)
        {
            return;
        }
        Vector2 direction = (to - from) / length;
        for (float cursor = 0f; cursor < length; cursor += dash + gap)
        {
            Vector2 dashStart = from + direction * cursor;
            Vector2 dashEnd = from + direction * Mathf.Min(length, cursor + dash);
            AddSegment(vh, dashStart, dashEnd, width, color);
        }
    }

    private static void AddSegment(
        VertexHelper vh,
        Vector2 from,
        Vector2 to,
        float width,
        Color color)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(from - normal, color, Vector2.zero);
        vh.AddVert(from + normal, color, Vector2.zero);
        vh.AddVert(to + normal, color, Vector2.zero);
        vh.AddVert(to - normal, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
