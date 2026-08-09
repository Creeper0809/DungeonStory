#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;
using DungeonStory.Foundation;

[InitializeOnLoad]
public static class ResearchTreePlayModeVerifier
{
    public const string RequestPath = "Temp/research-tree-playmode.request";
    public const string ProgressPath = "Temp/research-tree-playmode-progress.txt";
    public const string ReportPath = "Artifacts/QA/research-tree-playmode-report.txt";
    public const string DesktopCapturePath = "Artifacts/QA/research-tree-1600x900.png";
    public const string PortraitDetailCapturePath = "Artifacts/QA/research-tree-900x1600-detail.png";
    public const string PortraitQueueCapturePath = "Artifacts/QA/research-tree-900x1600-queue.png";

    private static bool runnerCreated;

    static ResearchTreePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("DungeonStory/Debug/Research/Request Research Tree PlayMode Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(ProgressPath);
        File.Delete(DesktopCapturePath);
        File.Delete(PortraitDetailCapturePath);
        File.Delete(PortraitQueueCapturePath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Research Tree Verification Runner")
            .AddComponent<ResearchTreeVerificationRunner>();
    }
}

public sealed class ResearchTreeVerificationRunner : MonoBehaviour
{
    private const string DetailContractProjectId =
        "research:equipment:powered-armor";
    private const float EffectiveWorkPerGameDay = 180f * 0.55f;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    private Mouse originalMouse;
    private Mouse verificationMouse;
    private Keyboard originalKeyboard;
    private Keyboard verificationKeyboard;
    private DungeonAutomationInputTestCapability automationInput;
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private int originalGameViewSizeIndex = -1;
    private IDungeonUserSettingsService settings;
    private IGameTimeScaleController timeScaleController;
    private IGameSpeedController gameSpeedController;
    private GameManager gameManager;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        ConfigureInput();

        yield return CompleteOwnerSelectionIfVisible();
        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(45f);
        yield return ClearBlockingRunOverlays();

        yield return SelectResolution(1600, 900);
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 15f;
        while ((scope == null || scope.Container == null) && Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
            yield return null;
        }

        Check(scope?.Container != null, "SCOPE", "gameplay lifetime scope resolved");
        if (scope?.Container == null)
        {
            Finish();
            yield break;
        }

        IObjectResolver container = scope.Container;
        settings = container.Resolve<IDungeonUserSettingsService>();
        timeScaleController = container.Resolve<IGameTimeScaleController>();
        gameSpeedController = container.Resolve<IGameSpeedController>();
        IResearchProjectCatalog catalog = container.Resolve<IResearchProjectCatalog>();
        IResearchRewardCatalog rewardCatalog =
            container.Resolve<IResearchRewardCatalog>();
        BlueprintResearchRuntime runtime = container
            .Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        gameManager = FindObjectsByType<GameManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();

        Check(catalog.Projects.Count == 180,
            "CATALOG_COUNT",
            $"projects={catalog.Projects.Count}");
        Check(runtime != null,
            "RUNTIME",
            "research runtime resolved");
        if (runtime == null)
        {
            Finish();
            yield break;
        }

        catalog.TryGet(
            new ResearchProjectId(DetailContractProjectId),
            out ResearchProjectSO detailContractProject);
        Check(detailContractProject != null,
            "DETAIL_MODEL_PROJECT",
            detailContractProject != null
                ? detailContractProject.ProjectId.Value
                : $"missing={DetailContractProjectId}");
        Check(rewardCatalog != null,
            "DETAIL_REWARD_CATALOG",
            rewardCatalog != null ? "resolved" : "missing");

        settings.Update(data => data.pauseOnResearchTree = false);
        SetRunningState(1f);
        yield return Click(FindButton("TopTabButton_Research_연구"));
        yield return WaitForWindow();

        ResearchTreeWindow window = FindWindow();
        Check(window != null, "WINDOW_OPEN", "research tree opened by pointer");
        if (window == null)
        {
            Finish();
            yield break;
        }

        Check(Mathf.Approximately(Time.timeScale, 1f)
                && (gameManager == null || !gameManager.isPause),
            "DEFAULT_NO_PAUSE",
            $"timeScale={Time.timeScale:0.##}; paused={gameManager != null && gameManager.isPause}");
        Check(CountNodeButtons(window) == 180,
            "ALL_NODES_VISIBLE",
            $"nodes={CountNodeButtons(window)}");
        Check(FindChild(window.transform, "GraphViewport") != null
                && FindChild(window.transform, "Inspector") != null,
            "SURFACE_STRUCTURE",
            "graph and inspector are present");

        if (detailContractProject != null && rewardCatalog != null)
        {
            yield return SelectAndVerifyDetailContract(
                window,
                detailContractProject,
                runtime,
                rewardCatalog,
                "DESKTOP");
        }

        yield return Capture(
            ResearchTreePlayModeVerifier.DesktopCapturePath,
            1600,
            900,
            "DESKTOP_CAPTURE");

        List<string> queuedIds = new List<string>();
        for (int iteration = 0; iteration < 3; iteration++)
        {
            ResearchProjectSO candidate = catalog.Projects
                .Where(project => !queuedIds.Contains(project.ProjectId.Value))
                .Where(project => runtime.GetNodeState(project, out _) == ResearchNodeState.Available)
                .FirstOrDefault();
            if (candidate != null)
            {
                window.CenterProject(candidate);
                yield return null;
                yield return null;
            }
            Check(candidate != null,
                $"QUEUE_CANDIDATE_{iteration + 1}",
                candidate != null ? candidate.ProjectId.Value : "no available project");
            if (candidate == null)
            {
                break;
            }

            yield return Click(FindButton($"Node_{candidate.ProjectId.Value}"));
            yield return Click(FindButton("ProjectAction"));
            queuedIds.Add(candidate.ProjectId.Value);
            Check(runtime.State.Projects.ContainsInQueue(candidate.ProjectId),
                $"QUEUE_POINTER_{iteration + 1}",
                $"project={candidate.ProjectId.Value}; queue={runtime.State.Projects.Queue.Count}");
        }

        Check(runtime.State.Projects.Queue.Count >= 3,
            "QUEUE_THREE",
            $"queue={runtime.State.Projects.Queue.Count}");
        if (runtime.State.Projects.Queue.Count >= 3)
        {
            string beforeSecond = runtime.State.Projects.Queue[1].ProjectId.Value;
            string beforeThird = runtime.State.Projects.Queue[2].ProjectId.Value;
            RectTransform from = FindChild(window.transform, $"Queue_{beforeThird}") as RectTransform;
            RectTransform to = FindChild(window.transform, $"Queue_{beforeSecond}") as RectTransform;
            yield return Drag(from, to);
            string afterSecond = runtime.State.Projects.Queue[1].ProjectId.Value;
            Check(afterSecond == beforeThird,
                "QUEUE_DRAG_REORDER",
                $"before={beforeSecond},{beforeThird}; afterSecond={afterSecond}");
        }

        RectTransform graphViewport = FindChild(window.transform, "GraphViewport") as RectTransform;
        RectTransform graphRoot = FindChild(window.transform, "GraphRoot") as RectTransform;
        Vector2 panBefore = graphRoot != null ? graphRoot.anchoredPosition : Vector2.zero;
        yield return DragSurface(
            graphViewport,
            ScreenCenter(graphViewport) + new Vector2(-70f, 30f),
            ScreenCenter(graphViewport) + new Vector2(80f, -25f));
        Vector2 panAfter = graphRoot != null ? graphRoot.anchoredPosition : Vector2.zero;
        Check(Vector2.Distance(panBefore, panAfter) > 10f,
            "GRAPH_PAN",
            $"position={panBefore}->{panAfter}");

        float zoomBefore = graphRoot != null ? graphRoot.localScale.x : 0f;
        yield return Scroll(graphViewport, ScreenCenter(graphViewport), 1f);
        float zoomAfter = graphRoot != null ? graphRoot.localScale.x : 0f;
        Check(zoomAfter > zoomBefore,
            "CURSOR_ZOOM",
            $"zoom={zoomBefore:0.###}->{zoomAfter:0.###}");

        TMP_InputField search = FindInput("Search");
        yield return Click(search);
        search.text = "의료";
        yield return null;
        yield return null;
        Image matchingNode = FindNodeByText(window, "의료 회복");
        int dimmed = window.GetComponentsInChildren<Button>(true)
            .Where(button => button.name.StartsWith("Node_", StringComparison.Ordinal))
            .Select(button => button.targetGraphic as Image)
            .Count(image => image != null && image.color.a < 0.5f);
        Check(matchingNode != null && matchingNode.color.a > 0.9f && dimmed > 0,
            "SEARCH_DIMMING",
            $"matchingAlpha={(matchingNode != null ? matchingNode.color.a : -1f):0.##}; dimmed={dimmed}");
        search.text = string.Empty;
        yield return null;

        yield return Click(FindButton("Close"));
        Check(FindWindow() == null,
            "CLOSE_POINTER",
            "close button closed research tree");

        settings.Update(data => data.pauseOnResearchTree = true);
        SetRunningState(5f);
        yield return Click(FindButton("TopTabButton_Research_연구"));
        yield return WaitForWindow();
        Check(Mathf.Approximately(Time.timeScale, 0f)
                && (gameManager == null || gameManager.isPause),
            "OPTIONAL_PAUSE",
            $"timeScale={Time.timeScale:0.##}; paused={gameManager != null && gameManager.isPause}");
        ClickWhilePaused(FindButton("Close"));
        yield return null;
        yield return null;
        Check(Mathf.Approximately(Time.timeScale, 5f)
                && (gameManager == null || !gameManager.isPause),
            "PAUSE_STATE_RESTORE",
            $"timeScale={Time.timeScale:0.##}; paused={gameManager != null && gameManager.isPause}");

        settings.Update(data => data.pauseOnResearchTree = false);
        SetRunningState(1f);
        yield return Click(FindButton("TopTabButton_Research_연구"));
        yield return WaitForWindow();
        yield return SelectResolution(900, 1600);
        window = FindWindow();
        graphViewport = FindChild(window.transform, "GraphViewport") as RectTransform;
        RectTransform inspector = FindChild(window.transform, "Inspector") as RectTransform;
        Check(IsInsideScreen(graphViewport) && IsInsideScreen(inspector),
            "PORTRAIT_BOUNDS",
            $"graph={DescribeRect(graphViewport)}; inspector={DescribeRect(inspector)}");
        Check(FindButton("DetailTab") != null && FindButton("QueueTab") != null,
            "PORTRAIT_TABS",
            "detail and queue tabs visible");
        if (detailContractProject != null && rewardCatalog != null)
        {
            yield return SelectAndVerifyDetailContract(
                window,
                detailContractProject,
                runtime,
                rewardCatalog,
                "PORTRAIT");
        }
        yield return Capture(
            ResearchTreePlayModeVerifier.PortraitDetailCapturePath,
            900,
            1600,
            "PORTRAIT_DETAIL_CAPTURE");
        yield return Click(FindButton("QueueTab"));
        yield return Capture(
            ResearchTreePlayModeVerifier.PortraitQueueCapturePath,
            900,
            1600,
            "PORTRAIT_QUEUE_CAPTURE");

        yield return SelectResolution(1600, 900);
        yield return Click(FindButton("Close"));
        settings.Update(data => data.pauseOnResearchTree = false);
        SetRunningState(1f);
        Finish();
    }

    private IEnumerator SelectAndVerifyDetailContract(
        ResearchTreeWindow window,
        ResearchProjectSO project,
        BlueprintResearchRuntime runtime,
        IResearchRewardCatalog rewardCatalog,
        string surface)
    {
        RectTransform viewport =
            FindChild(window.transform, "GraphViewport") as RectTransform;
        bool centered = window.CenterProject(project);
        yield return null;
        yield return null;
        Button node = FindButton($"Node_{project.ProjectId.Value}");
        Check(centered
                && node != null
                && IsButtonVisibleInGraph(node, viewport),
            $"{surface}_DETAIL_NODE_VISIBLE",
            $"project={project.ProjectId.Value}; centered={centered}; "
                + $"node={node != null}; viewport={DescribeRect(viewport)}");
        yield return Click(node);
        Canvas.ForceUpdateCanvases();
        yield return null;

        TMP_Text detail = FindChild(window.transform, "DetailText")
            ?.GetComponent<TMP_Text>();
        RectTransform detailViewport =
            FindChild(window.transform, "DetailViewport") as RectTransform;
        ScrollRect detailScroll =
            FindChild(window.transform, "DetailScroll")?.GetComponent<ScrollRect>();
        RectTransform inspector =
            FindChild(window.transform, "Inspector") as RectTransform;
        string actual = detail?.text ?? string.Empty;
        ResearchNodeState state = runtime.GetNodeState(project, out string blocker);
        ResearchProjectProgressState progress =
            runtime.State.Projects.GetProgress(project.ProjectId);
        IReadOnlyList<ResearchRewardEntry> rewards =
            rewardCatalog.GetRewards(project.ProjectId);
        float prerequisiteWork = CalculateRemainingPrerequisiteWork(
            project,
            runtime);
        float totalRemainingWork = prerequisiteWork
            + Mathf.Max(0f, project.RequiredWork - progress.Progress);
        float expectedDays = totalRemainingWork / EffectiveWorkPerGameDay;
        int expectedShifts = Mathf.CeilToInt(expectedDays);
        string expectedUnlockCards = string.Join(", ", rewards.Select(reward =>
            $"[{reward.Kind}] {reward.DisplayName}"));

        detail?.ForceMeshUpdate();
        bool detailVisible = detail != null
            && detail.gameObject.activeInHierarchy
            && detail.enabled
            && detail.canvasRenderer.GetInheritedAlpha() > 0.01f
            && detail.rectTransform.rect.width > 1f
            && detail.rectTransform.rect.height > 1f
            && detailViewport != null
            && IsInsideScreen(detailViewport)
            && detailScroll != null
            && detailScroll.vertical
            && detailScroll.viewport == detailViewport
            && detailScroll.content == detail.rectTransform
            && detail.transform.IsChildOf(inspector)
            && detail.textInfo.characterCount > 0
            && detail.preferredHeight <= detail.rectTransform.rect.height + 1f;
        Check(detailVisible,
            $"{surface}_DETAIL_VISIBLE",
            detail != null
                ? $"active={detail.gameObject.activeInHierarchy}; "
                    + $"characters={detail.textInfo.characterCount}; "
                    + $"preferredHeight={detail.preferredHeight:0.#}; "
                    + $"rect={DescribeRect(detail.rectTransform)}"
                : "DetailText missing");

        bool modelReady = state == ResearchNodeState.Locked
            && !string.IsNullOrWhiteSpace(blocker)
            && project.RequiredWork > 0f
            && prerequisiteWork > 0f
            && rewards.Count > 0
            && rewards.All(reward =>
                !string.IsNullOrWhiteSpace(reward.RewardId)
                && !string.IsNullOrWhiteSpace(reward.DisplayName));
        Check(modelReady,
            $"{surface}_DETAIL_MODEL_READY",
            $"project={project.ProjectId.Value}; state={state}; "
                + $"required={project.RequiredWork:0.#}; "
                + $"prerequisite={prerequisiteWork:0.#}; "
                + $"rewards={rewards.Count}; blocker={blocker}");

        Check(actual.StartsWith(
                $"<b>{project.DisplayName}</b>",
                StringComparison.Ordinal),
            $"{surface}_DETAIL_SELECTION_BOUND",
            $"project={project.ProjectId.Value}; textLength={actual.Length}");
        Check(actual.Contains(
                $"<b>진행</b>  {progress.Progress:0.#} / {project.RequiredWork:0.#}",
                StringComparison.Ordinal),
            $"{surface}_DETAIL_REQUIRED_WORK",
            $"progress={progress.Progress:0.#}; required={project.RequiredWork:0.#}");
        Check(actual.Contains(
                $"<b>잔여 선행 작업량</b> {prerequisiteWork:0.#} (중복 제거)",
                StringComparison.Ordinal),
            $"{surface}_DETAIL_PREREQUISITE_WORK",
            $"deduplicated={prerequisiteWork:0.#}");
        Check(actual.Contains(
                $"<b>예상</b> {expectedShifts}교대 · {expectedDays:0.0}게임일",
                StringComparison.Ordinal),
            $"{surface}_DETAIL_ESTIMATE",
            $"shifts={expectedShifts}; gameDays={expectedDays:0.0}");
        Check(!string.IsNullOrWhiteSpace(expectedUnlockCards)
                && actual.Contains(
                    $"<b>해금</b>  {expectedUnlockCards}",
                    StringComparison.Ordinal),
            $"{surface}_DETAIL_REWARD_CARDS",
            $"cards={rewards.Count}; text={expectedUnlockCards}");
        Check(!string.IsNullOrWhiteSpace(blocker)
                && actual.Contains(
                    $"<b>중단 사유</b>  {blocker}",
                    StringComparison.Ordinal),
            $"{surface}_DETAIL_LOCK_REASON",
            $"state={state}; blocker={blocker}");
    }

    private static float CalculateRemainingPrerequisiteWork(
        ResearchProjectSO project,
        BlueprintResearchRuntime runtime)
    {
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        float total = 0f;

        void Visit(ResearchProjectSO current)
        {
            foreach (ResearchProjectSO prerequisite in current?.Prerequisites
                         ?? Array.Empty<ResearchProjectSO>())
            {
                if (prerequisite == null
                    || !visited.Add(prerequisite.ProjectId.Value)
                    || runtime.State.Projects.IsCompleted(prerequisite.ProjectId))
                {
                    continue;
                }

                ResearchProjectProgressState progress =
                    runtime.State.Projects.GetProgress(prerequisite.ProjectId);
                total += Mathf.Max(
                    0f,
                    prerequisite.RequiredWork - progress.Progress);
                Visit(prerequisite);
            }
        }

        Visit(project);
        return total;
    }

    private IEnumerator CompleteOwnerSelectionIfVisible()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        Button owner = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            owner = Resources.FindObjectsOfTypeAll<Button>()
                .Where(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.interactable
                    && button.name.StartsWith(
                        "OwnerOption_",
                        StringComparison.Ordinal))
                .OrderBy(button => button.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (owner != null
                || FindButton("StartPartyConfirm") != null
                || FindButton("PreparationStartRunButton") != null)
            {
                break;
            }
            yield return null;
        }

        if (owner != null)
        {
            yield return Click(owner);
        }
    }

    private IEnumerator ClearBlockingRunOverlays()
    {
        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PLAYABLE_RUN_READY",
            ownerManager?.CurrentOwnerActor != null
                ? "owner=" + ownerManager.CurrentOwnerActor.name
                : "owner or committed party missing");

        foreach (GameObject overlay in Resources
                     .FindObjectsOfTypeAll<GameObject>()
                     .Where(candidate => candidate != null
                         && candidate.scene.IsValid()
                         && candidate.activeInHierarchy
                         && (candidate.name == "OwnerSelectionSurface"
                             || candidate.name == "OwnerSelectionPanel")))
        {
            overlay.SetActive(false);
        }

        yield return null;
        bool blockingOverlayVisible = Resources
            .FindObjectsOfTypeAll<GameObject>()
            .Any(candidate => candidate != null
                && candidate.scene.IsValid()
                && candidate.activeInHierarchy
                && (candidate.name == "OwnerSelectionSurface"
                    || candidate.name == "OwnerSelectionPanel"));
        Check(!blockingOverlayVisible,
            "RUN_OVERLAYS_CLEARED",
            blockingOverlayVisible
                ? "owner selection overlay remained visible"
                : "owner selection overlays cleared");
    }

    private void SetRunningState(float scale)
    {
        int speed = Mathf.Clamp(Mathf.RoundToInt(scale), 1, 5);
        if (gameSpeedController != null)
        {
            gameSpeedController.SetSpeed(speed);
            gameSpeedController.SetPaused(false);
            return;
        }

        if (timeScaleController != null)
        {
            timeScaleController.Scale = speed;
        }
        else
        {
            Time.timeScale = speed;
        }
        if (gameManager != null)
        {
            gameManager.isPause = false;
        }
    }

    private IEnumerator WaitForWindow()
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (FindWindow() == null && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private IEnumerator Click(Selectable selectable)
    {
        Check(selectable != null && selectable.gameObject.activeInHierarchy,
            "POINTER_" + (selectable != null ? selectable.name : "missing"),
            selectable != null ? selectable.name : "missing");
        if (selectable == null)
        {
            yield break;
        }

        string selectableName = selectable.name;
        RectTransform rect = selectable.transform as RectTransform;
        Vector2 point = ScreenCenter(rect);
        bool dispatched = false;
        for (int attempt = 0; attempt < 3 && !dispatched; attempt++)
        {
            QueueMouse(new MouseState { position = point });
            Canvas.ForceUpdateCanvases();
            yield return null;
            dispatched = DispatchPointerClick(point);
            if (!dispatched)
            {
                yield return null;
            }
        }
        Check(dispatched,
            "POINTER_DISPATCH",
            $"target={selectableName}; point={point}");
        yield return null;
        yield return null;
    }

    private void ClickWhilePaused(Selectable selectable)
    {
        Check(selectable != null && selectable.gameObject.activeInHierarchy,
            "POINTER_" + (selectable != null ? selectable.name : "missing"),
            selectable != null ? selectable.name : "missing");
        if (selectable == null)
        {
            return;
        }

        RectTransform rect = selectable.transform as RectTransform;
        Vector2 point = ScreenCenter(rect);
        QueueMouse(new MouseState { position = point });
        bool dispatched = DispatchPointerClick(point);
        Check(dispatched,
            "POINTER_DISPATCH",
            $"target={selectable.name}; point={point}");
    }

    private IEnumerator Drag(RectTransform from, RectTransform to)
    {
        if (from == null || to == null)
        {
            Check(false, "DRAG_TARGETS", "queue row missing");
            yield break;
        }

        Vector2 start = ScreenCenter(from);
        Vector2 end = ScreenCenter(to);
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = start,
            pressPosition = start
        };
        ExecuteEvents.Execute(from.gameObject, pointer, ExecuteEvents.beginDragHandler);
        yield return null;
        pointer.position = end;
        ExecuteEvents.Execute(from.gameObject, pointer, ExecuteEvents.dragHandler);
        yield return null;
        ExecuteEvents.Execute(from.gameObject, pointer, ExecuteEvents.endDragHandler);
        yield return null;
        yield return null;
    }

    private IEnumerator DragSurface(RectTransform surface, Vector2 start, Vector2 end)
    {
        if (surface == null)
        {
            Check(false, "PAN_TARGET", "graph viewport missing");
            yield break;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = start,
            pressPosition = start
        };
        ExecuteEvents.Execute(surface.gameObject, pointer, ExecuteEvents.beginDragHandler);
        yield return null;
        pointer.position = end;
        ExecuteEvents.Execute(surface.gameObject, pointer, ExecuteEvents.dragHandler);
        yield return null;
        ExecuteEvents.Execute(surface.gameObject, pointer, ExecuteEvents.endDragHandler);
        yield return null;
    }

    private IEnumerator Scroll(RectTransform surface, Vector2 point, float direction)
    {
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point,
            scrollDelta = new Vector2(0f, direction)
        };
        ExecuteEvents.Execute(surface.gameObject, pointer, ExecuteEvents.scrollHandler);
        QueueMouse(new MouseState
        {
            position = point,
            scroll = new Vector2(0f, direction * 120f)
        });
        yield return null;
        QueueMouse(new MouseState { position = point });
        yield return null;
    }

    private static bool DispatchPointerClick(Vector2 screenPoint)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint,
            pressPosition = screenPoint
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        foreach (RaycastResult hit in hits)
        {
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (handler == null)
            {
                continue;
            }

            pointer.pointerCurrentRaycast = hit;
            pointer.pointerPressRaycast = hit;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            return true;
        }
        return false;
    }

    private IEnumerator SelectResolution(int width, int height)
    {
        GameViewResolutionController.Select(width, height);
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != width || Screen.height != height)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == width && Screen.height == height,
            $"RESOLUTION_{width}x{height}",
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(string path, int width, int height, string key)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D texture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (texture == null)
        {
            Check(false, key, "capture returned null");
            yield break;
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        int visible = texture.GetPixels32()
            .Count(pixel => pixel.a > 0 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
        Check(texture.width == width
                && texture.height == height
                && visible > texture.width * texture.height / 20,
            key,
            $"size={texture.width}x{texture.height}; visible={visible}");
        Destroy(texture);
    }

    private void ConfigureInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        originalKeyboard = Keyboard.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }
        if (originalKeyboard != null)
        {
            InputSystem.DisableDevice(originalKeyboard);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("ResearchTreeVerificationMouse");
        verificationKeyboard = InputSystem.AddDevice<Keyboard>("ResearchTreeVerificationKeyboard");
        verificationMouse.MakeCurrent();
        verificationKeyboard.MakeCurrent();
        automationInput = new DungeonAutomationInputTestCapability();
        automationInput.Enable();
    }

    private void QueueMouse(MouseState state)
    {
        automationInput.MovePointer(state.position);
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private void TeardownInput()
    {
        automationInput?.Dispose();
        automationInput = null;
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }
        if (verificationKeyboard != null && verificationKeyboard.added)
        {
            InputSystem.RemoveDevice(verificationKeyboard);
        }
        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }
        if (originalKeyboard != null && originalKeyboard.added)
        {
            InputSystem.EnableDevice(originalKeyboard);
            originalKeyboard.MakeCurrent();
        }
        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void Finish()
    {
        settings?.Update(data => data.pauseOnResearchTree = false);
        SetRunningState(1f);
        if (originalGameViewSizeIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex = originalGameViewSizeIndex;
        }
        TeardownInput();
        Application.logMessageReceived -= CaptureLog;
        File.Delete(ResearchTreePlayModeVerifier.RequestPath);
        report.Add($"CONSOLE errors={errors.Count}; warnings={warnings.Count}");
        if (errors.Count > 0 || warnings.Count > 0)
        {
            failures.Add($"Console errors={errors.Count}, warnings={warnings.Count}");
        }
        report.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}");
        foreach (string failure in failures)
        {
            report.Add("FAILURE=" + failure);
        }
        File.WriteAllLines(ResearchTreePlayModeVerifier.ReportPath, report);
        if (failures.Count > 0)
        {
            Debug.LogError("Research Tree PlayMode verification failed: "
                + string.Join(" | ", failures));
        }
        else
        {
            Debug.Log("Research Tree PlayMode verification passed.");
        }
        EditorApplication.ExitPlaymode();
    }

    private void Check(bool passed, string key, string detail)
    {
        string line = $"{key}={(passed ? "PASS" : "FAIL")}; {detail}";
        report.Add(line);
        File.WriteAllText(
            ResearchTreePlayModeVerifier.ProgressPath,
            $"steps={report.Count}\nlast={line}\n");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add(condition);
        }
    }

    private static ResearchTreeWindow FindWindow()
    {
        return Resources.FindObjectsOfTypeAll<ResearchTreeWindow>()
            .FirstOrDefault(window => window != null
                && window.gameObject.scene.IsValid()
                && window.gameObject.activeInHierarchy);
    }

    private static int CountNodeButtons(ResearchTreeWindow window)
    {
        return window != null
            ? window.GetComponentsInChildren<Button>(true)
                .Count(button => button.name.StartsWith("Node_", StringComparison.Ordinal))
            : 0;
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name == name);
    }

    private static TMP_InputField FindInput(string name)
    {
        return Resources.FindObjectsOfTypeAll<TMP_InputField>()
            .FirstOrDefault(input => input != null
                && input.gameObject.scene.IsValid()
                && input.gameObject.activeInHierarchy
                && input.name == name);
    }

    private static Transform FindChild(Transform root, string name)
    {
        return root != null
            ? root.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == name)
                .OrderByDescending(child => child.gameObject.activeInHierarchy)
                .FirstOrDefault()
            : null;
    }

    private static Image FindNodeByText(ResearchTreeWindow window, string text)
    {
        Button button = window.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(candidate => candidate.name.StartsWith("Node_", StringComparison.Ordinal)
                && candidate.GetComponentsInChildren<TMP_Text>(true)
                    .Any(label => label.text == text));
        return button?.targetGraphic as Image;
    }

    private static bool IsButtonVisibleInGraph(Button button, RectTransform viewport)
    {
        if (button == null || viewport == null)
        {
            return false;
        }
        Vector2 center = ScreenCenter(button.transform as RectTransform);
        return RectTransformUtility.RectangleContainsScreenPoint(viewport, center, null);
    }

    private static Vector2 ScreenCenter(RectTransform rect)
    {
        return rect != null
            ? RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners.All(corner =>
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
            return point.x >= -1f
                && point.x <= Screen.width + 1f
                && point.y >= -1f
                && point.y <= Screen.height + 1f;
        });
    }

    private static string DescribeRect(RectTransform rect)
    {
        if (rect == null)
        {
            return "missing";
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return $"{rect.name}:{min}->{max}";
    }
}
#endif
