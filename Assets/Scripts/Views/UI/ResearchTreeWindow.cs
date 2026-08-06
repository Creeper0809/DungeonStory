using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using static ResearchTreeViewFactory;


public sealed class ResearchTreeWindow :
    MonoBehaviour,
    IResearchTreeInteractionSink
{
    private const float ToolbarHeight = 58f;
    private const float DesktopInspectorWidth = 400f;
    private const float PortraitInspectorFraction = 0.31f;
    private const float BottomTabSafeArea = 64f;
    private const float TopHudSafeArea = 82f;
    private const float RefreshInterval = 0.35f;

    private readonly List<GameObject> nodeObjects = new List<GameObject>();
    private readonly List<GameObject> queueObjects = new List<GameObject>();
    private readonly List<RectTransform> queueRows = new List<RectTransform>();
    private readonly Dictionary<string, ResearchNodeState> nodeStates =
        new Dictionary<string, ResearchNodeState>(StringComparer.Ordinal);

    private IResearchProjectCatalog projectCatalog;
    private IResearchGraphLayoutService layoutService;
    private IResearchQueueCommandService queueCommands;
    private BlueprintResearchRuntime runtime;
    private ResearchTreePresentationRules presentationRules;
    private ResearchTreeViewFactory viewFactory;
    private ResearchTreeViewportController viewportController;
    private ResearchTreePauseScope pauseScope;

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
    private ResearchTreeDetailScrollView detailScrollView;
    private Button projectActionButton;
    private TMP_Text projectActionLabel;
    private Button detailTabButton;
    private Button queueTabButton;
    private ResearchGraphLayout graphLayout;
    private ResearchProjectSO selectedProject;
    private ResearchField? selectedField;
    private float nextRefreshAt;
    private bool layoutReady;
    private bool initialViewApplied;
    private bool portrait;
    private bool showQueueOnPortrait;
    private Vector2 lastScreenSize;
    private bool queueDragActive;

    internal bool IsConstructed => pauseScope != null;

    [Inject]
    public void Construct(
        IResearchProjectCatalog projectCatalog,
        IResearchGraphLayoutService layoutService,
        IResearchQueueCommandService queueCommands,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IResearchBlueprintArchiveQuery archiveQuery,
        IFacilityShopCatalog facilityCatalog,
        IResearchRewardCatalog rewardCatalog,
        ITmpKoreanFontService fontService,
        IDungeonUserSettingsService settingsService,
        IGameSpeedController gameSpeedController)
    {
        this.projectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
        this.layoutService = layoutService
            ?? throw new ArgumentNullException(nameof(layoutService));
        this.queueCommands = queueCommands
            ?? throw new ArgumentNullException(nameof(queueCommands));
        runtime = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(ResearchTreeWindow)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        presentationRules = new ResearchTreePresentationRules(
            archiveQuery,
            facilityCatalog,
            rewardCatalog);
        viewFactory = new ResearchTreeViewFactory(fontService);
        pauseScope = new ResearchTreePauseScope(
            settingsService,
            gameSpeedController);
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
        pauseScope?.Capture();
        Refresh();
    }

    private void OnDisable()
    {
        pauseScope?.Restore();
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
        viewportController.SetLayout(graphLayout);
        if (selectedProject == null)
        {
            selectedProject = projectCatalog.Projects.FirstOrDefault();
        }

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
        viewportController?.Pan(delta);
    }

    public void Zoom(PointerEventData eventData)
    {
        viewportController?.Zoom(eventData);
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
        detailScrollView?.ScrollToTop();
    }

    public bool CenterProject(ResearchProjectSO project)
    {
        return project != null
            && viewportController?.Center(project.ProjectId) == true;
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

        TMP_Text title = viewFactory.CreateText(toolbar, "Title", "연구", 26f, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.16f, 1f), 18f, 0f, -4f, 0f);

        searchInput = viewFactory.CreateInput(toolbar, "Search", "연구·시설·조합식 검색");
        SetRect(searchInput.GetComponent<RectTransform>(),
            new Vector2(0.16f, 0.14f), new Vector2(0.51f, 0.86f), 0f, 0f, 0f, 0f);
        searchInput.onValueChanged.AddListener(_ => RefreshDynamicContent());

        Button fieldButton = viewFactory.CreateButton(toolbar, "FieldFilter", "전체 분야", CycleField);
        SetRect(fieldButton.GetComponent<RectTransform>(),
            new Vector2(0.52f, 0.14f), new Vector2(0.67f, 0.86f), 0f, 0f, 0f, 0f);
        fieldFilterLabel = fieldButton.GetComponentInChildren<TMP_Text>();

        Button fitButton = viewFactory.CreateButton(toolbar, "Fit", "맞춤", FitView);
        SetRect(fitButton.GetComponent<RectTransform>(),
            new Vector2(0.68f, 0.14f), new Vector2(0.77f, 0.86f), 0f, 0f, 0f, 0f);
        Button centerButton = viewFactory.CreateButton(toolbar, "Center", "선택 이동", CenterSelected);
        SetRect(centerButton.GetComponent<RectTransform>(),
            new Vector2(0.78f, 0.14f), new Vector2(0.9f, 0.86f), 0f, 0f, 0f, 0f);
        Button closeButton = viewFactory.CreateButton(toolbar, "Close", "닫기", () => GetComponent<UITab>()?.CloseTab());
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
        viewportController = new ResearchTreeViewportController(
            graphViewport,
            graphRoot,
            nodeRoot,
            connectorGraphic);

        inspectorRoot = CreateRect("Inspector", main);
        CreateImage(inspectorRoot.gameObject, DungeonUiTheme.Surface);
        CreateInspectorContents();

        feedbackText = viewFactory.CreateText(windowRoot, "Feedback", string.Empty, 15f, TextAlignmentOptions.Center);
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

        detailTabButton = viewFactory.CreateButton(tabs, "DetailTab", "상세", () =>
        {
            showQueueOnPortrait = false;
            ApplyInspectorTabState();
        });
        SetRect(detailTabButton.GetComponent<RectTransform>(),
            new Vector2(0f, 0f), new Vector2(0.5f, 1f), 4f, 3f, -2f, -3f);
        queueTabButton = viewFactory.CreateButton(tabs, "QueueTab", "연구 큐", () =>
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

        detailScrollView = ResearchTreeDetailScrollView.Create(
            viewFactory,
            detailRoot);
        detailText = detailScrollView.Text;

        projectActionButton = viewFactory.CreateButton(detailRoot, "ProjectAction", "연구 예약", ToggleSelectedProjectQueue);
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
        if (runtime == null)
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

            bool matches = presentationRules.MatchesFilter(
                project,
                selectedField,
                search);
            CreateNode(project, state, nodeRect, matches);
        }

        List<ResearchConnectorLine> lines = graphLayout.Edges.Select(edge =>
        {
            nodeStates.TryGetValue(edge.To.Value, out ResearchNodeState state);
            return new ResearchConnectorLine(
                edge.Points,
                ResearchTreePresentationRules.GetConnectorColor(
                    state,
                    edge.IsShortcut),
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

        Image background = CreateImage(
            node.gameObject,
            ResearchTreePresentationRules.GetNodeColor(state));
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

        TMP_Text field = viewFactory.CreateText(
            node,
            "Field",
            ResearchTreePresentationRules.FormatField(project.Field),
            14f,
            TextAlignmentOptions.TopLeft);
        field.color = DungeonUiTheme.TextSecondary;
        SetRect(field.rectTransform, new Vector2(0f, 0.7f), new Vector2(1f, 1f), 12f, 3f, -70f, -5f);

        TMP_Text stateText = viewFactory.CreateText(
            node,
            "State",
            ResearchTreePresentationRules.FormatNodeState(state),
            13f,
            TextAlignmentOptions.TopRight);
        stateText.color =
            ResearchTreePresentationRules.GetStateTextColor(state);
        SetRect(stateText.rectTransform, new Vector2(0.56f, 0.7f), new Vector2(1f, 1f), 0f, 3f, -10f, -5f);

        TMP_Text name = viewFactory.CreateText(node, "Name", project.DisplayName, 21f, TextAlignmentOptions.MidlineLeft);
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.Normal;
        SetRect(name.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 0.72f), 12f, 0f, -10f, 0f);

        float ratio = runtime != null
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
            || runtime == null)
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
        if (selectedProject.PrerequisiteLinks.Count > 0)
        {
            prerequisites += "\n" + string.Join("\n",
                selectedProject.PrerequisiteLinks.Select(link =>
                    $"· {link.Kind}: {link.Reason}"));
        }
        string blueprint =
            presentationRules.FormatBlueprintDetail(selectedProject);
        string facilityCapacity = runtime.ResearchFacilityCapacity != null
            ? runtime.ResearchFacilityCapacity.FormatRequirements(selectedProject)
            : "연구 시설  판정 없음";
        float remainingPrerequisiteWork =
            ResearchTreePresentationRules.CalculateRemainingPrerequisiteWork(
            selectedProject,
            runtime);
        float totalRemainingWork = remainingPrerequisiteWork
            + Mathf.Max(0f, selectedProject.RequiredWork - progress.Progress);
        const float effectiveWorkPerGameDay = 180f * 0.55f;
        float expectedDays = totalRemainingWork / effectiveWorkPerGameDay;
        string unlocks = presentationRules.FormatUnlocks(selectedProject)
            + $"\n<b>잔여 선행 작업량</b> {remainingPrerequisiteWork:0.#} (중복 제거)"
            + $"\n<b>예상</b> {Mathf.CeilToInt(expectedDays)}교대 · {expectedDays:0.0}게임일";
        detailText.text =
            $"<b>{selectedProject.DisplayName}</b>\n" +
            $"{ResearchTreePresentationRules.FormatField(selectedProject.Field)} · "
            + $"{ResearchTreePresentationRules.FormatNodeState(state)}\n\n" +
            $"{selectedProject.Description}\n\n" +
            $"<b>진행</b>  {progress.Progress:0.#} / {selectedProject.RequiredWork:0.#}\n" +
            $"<b>선행</b>  {prerequisites}\n" +
            $"<b>설계도</b>  {blueprint}\n" +
            $"<b>수용력</b>  {facilityCapacity}\n" +
            $"<b>해금</b>  {unlocks}" +
            (string.IsNullOrWhiteSpace(blocker) ? string.Empty : $"\n\n<color=#D2A449><b>중단 사유</b>  {blocker}</color>");
        detailScrollView?.RefreshLayout();

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
        if (runtime == null)
        {
            return;
        }

        TMP_Text heading = viewFactory.CreateText(queueRoot, "QueueHeading", "연구 큐", 20f, TextAlignmentOptions.TopLeft);
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
            TMP_Text label = viewFactory.CreateText(
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
            TMP_Text empty = viewFactory.CreateText(queueRoot, "Empty", "예약된 연구가 없습니다.", 16f, TextAlignmentOptions.TopLeft);
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
            || runtime == null)
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
            ? ResearchTreePresentationRules.FormatField(selectedField.Value)
            : "전체 분야";
        RefreshDynamicContent();
    }

    private void FitView()
    {
        viewportController?.Fit();
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

}
