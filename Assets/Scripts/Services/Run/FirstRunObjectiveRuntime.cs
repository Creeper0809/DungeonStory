using System;
using DungeonStory.Foundation;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

public enum FirstRunObjectiveId
{
    None,
    ChooseOwner,
    MakeUsableRoom,
    AcquireBlueprint,
    CompleteResearch,
    CompleteSettlement,
    DefendInvasion,
    AdvanceOffense,
    RevealTruth
}

public readonly struct FirstRunObjectiveSnapshot
{
    public FirstRunObjectiveSnapshot(
        bool hasOwner,
        bool hasUsableRoom,
        int researchTaskCount,
        int completedResearchCount,
        float activeResearchRatio,
        int settlementCount,
        int defendedInvasionCount,
        int currentDay,
        DungeonRunPhase phase,
        DungeonRunOutcome outcome,
        int completedRunCount,
        int completedOffenseTargetCount = 0,
        int totalOffenseTargetCount = 0,
        bool truthRevealed = false,
        bool hasResearchBlueprint = false)
    {
        HasOwner = hasOwner;
        HasUsableRoom = hasUsableRoom;
        ResearchTaskCount = Mathf.Max(0, researchTaskCount);
        CompletedResearchCount = Mathf.Max(0, completedResearchCount);
        ActiveResearchRatio = Mathf.Clamp01(activeResearchRatio);
        SettlementCount = Mathf.Max(0, settlementCount);
        DefendedInvasionCount = Mathf.Max(0, defendedInvasionCount);
        CurrentDay = Mathf.Max(1, currentDay);
        Phase = phase;
        Outcome = outcome;
        CompletedRunCount = Mathf.Max(0, completedRunCount);
        CompletedOffenseTargetCount = Mathf.Max(0, completedOffenseTargetCount);
        TotalOffenseTargetCount = Mathf.Max(0, totalOffenseTargetCount);
        TruthRevealed = truthRevealed;
        HasResearchBlueprint = hasResearchBlueprint;
    }

    public bool HasOwner { get; }
    public bool HasUsableRoom { get; }
    public int ResearchTaskCount { get; }
    public int CompletedResearchCount { get; }
    public float ActiveResearchRatio { get; }
    public int SettlementCount { get; }
    public int DefendedInvasionCount { get; }
    public int CurrentDay { get; }
    public DungeonRunPhase Phase { get; }
    public DungeonRunOutcome Outcome { get; }
    public int CompletedRunCount { get; }
    public int CompletedOffenseTargetCount { get; }
    public int TotalOffenseTargetCount { get; }
    public bool TruthRevealed { get; }
    public bool HasResearchBlueprint { get; }
}

public readonly struct FirstRunObjectivePresentation
{
    public FirstRunObjectivePresentation(
        FirstRunObjectiveId id,
        int step,
        string title,
        string detail)
    {
        Id = id;
        Step = Mathf.Clamp(step, 0, FirstRunObjectiveResolver.TotalSteps);
        Title = title ?? string.Empty;
        Detail = detail ?? string.Empty;
    }

    public FirstRunObjectiveId Id { get; }
    public int Step { get; }
    public string Title { get; }
    public string Detail { get; }
    public bool IsVisible => Id != FirstRunObjectiveId.None;
}

public static class FirstRunObjectiveResolver
{
    public const int TotalSteps = 8;

    public static FirstRunObjectivePresentation Resolve(FirstRunObjectiveSnapshot state)
    {
        if (state.CompletedRunCount > 0 || state.Outcome != DungeonRunOutcome.None)
        {
            return Hidden();
        }

        if (!state.HasOwner)
        {
            return Show(
                FirstRunObjectiveId.ChooseOwner,
                1,
                "던전의 주인을 선택하세요",
                "이번 런을 이끌 종족과 능력을 정합니다.");
        }

        if (!state.HasUsableRoom)
        {
            return Show(
                FirstRunObjectiveId.MakeUsableRoom,
                2,
                "운영 가능한 방을 만드세요",
                "벽과 내벽 문으로 닫힌 공간을 완성하세요.");
        }

        if (!state.HasResearchBlueprint
            && state.ResearchTaskCount == 0
            && state.CompletedResearchCount == 0)
        {
            return Show(
                FirstRunObjectiveId.AcquireBlueprint,
                3,
                "첫 설계도를 확보하세요",
                "상점 탭에서 오늘의 설계도를 구입하세요.");
        }

        if (state.CompletedResearchCount == 0)
        {
            int progress = Mathf.RoundToInt(state.ActiveResearchRatio * 100f);
            return Show(
                FirstRunObjectiveId.CompleteResearch,
                4,
                state.ResearchTaskCount > 0
                    ? "첫 연구를 끝내세요"
                    : "설계도를 보관하고 연구를 예약하세요",
                state.ResearchTaskCount > 0
                    ? $"연구 {progress}% · 연구 시설에 작업 인력을 배치하세요."
                    : "설계도를 연구실 보관대로 운반한 뒤 연구 트리에서 노드를 예약하세요.");
        }

        if (state.SettlementCount == 0)
        {
            return Show(
                FirstRunObjectiveId.CompleteSettlement,
                5,
                "첫 영업일을 마치세요",
                $"현재 Day {state.CurrentDay} · 시설을 운영해 첫 정산을 맞이하세요.");
        }

        if (state.DefendedInvasionCount == 0)
        {
            return Show(
                FirstRunObjectiveId.DefendInvasion,
                6,
                "첫 침입을 막아내세요",
                "방어 시설과 주인의 체력을 점검하세요.");
        }

        int finalTargetIndex = Mathf.Max(1, state.TotalOffenseTargetCount - 1);
        if (state.CompletedOffenseTargetCount < finalTargetIndex)
        {
            return Show(
                FirstRunObjectiveId.AdvanceOffense,
                7,
                "오펜스 경로를 개척하세요",
                $"진실 추적 {state.CompletedOffenseTargetCount}/{state.TotalOffenseTargetCount} · 오펜스 탭에서 앞선 목표부터 원정을 완료하세요.");
        }

        return Show(
            FirstRunObjectiveId.RevealTruth,
            8,
            "던전의 진실을 밝히세요",
            "오펜스의 마지막 심장부 원정을 완료하면 이번 런에서 승리합니다.");
    }

    private static FirstRunObjectivePresentation Show(
        FirstRunObjectiveId id,
        int step,
        string title,
        string detail)
    {
        return new FirstRunObjectivePresentation(id, step, title, detail);
    }

    private static FirstRunObjectivePresentation Hidden()
    {
        return new FirstRunObjectivePresentation(FirstRunObjectiveId.None, 0, string.Empty, string.Empty);
    }
}

public interface IFirstRunObjectiveRuntime
{
    FirstRunObjectiveId CurrentObjective { get; }
    bool IsVisible { get; }
    RectTransform PanelRect { get; }
    void RefreshNow();
}

public sealed class FirstRunObjectiveProgressContext
{
    public FirstRunObjectiveProgressContext(
        IOwnerRunManagerProvider ownerProvider,
        IGridSystemProvider gridProvider,
        IRoomLayoutCache roomLayoutCache,
        DungeonSceneRuntimeReferences sceneRuntimes,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IOffenseQuery offense,
        IDungeonRunFlowRuntime runFlow)
    {
        OwnerProvider = ownerProvider
            ?? throw new ArgumentNullException(nameof(ownerProvider));
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        RoomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        Settlement = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .Settlement
            ?? throw new InvalidOperationException(
                $"{nameof(FirstRunObjectiveRuntime)} requires a loaded {nameof(OperatingDaySettlementRuntime)}.");
        progressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        MetaProgression = progressionRuntimes.MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(FirstRunObjectiveRuntime)} requires a loaded {nameof(MetaProgressionRuntime)}.");
        Research = progressionRuntimes.BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(FirstRunObjectiveRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        Offense = offense ?? throw new ArgumentNullException(nameof(offense));
        RunFlow = runFlow ?? throw new ArgumentNullException(nameof(runFlow));
    }

    public IOwnerRunManagerProvider OwnerProvider { get; }
    public IGridSystemProvider GridProvider { get; }
    public IRoomLayoutCache RoomLayoutCache { get; }
    public BlueprintResearchRuntime Research { get; }
    public OperatingDaySettlementRuntime Settlement { get; }
    public MetaProgressionRuntime MetaProgression { get; }
    public IOffenseQuery Offense { get; }
    public IDungeonRunFlowRuntime RunFlow { get; }
}

public sealed class FirstRunObjectivePresentationContext
{
    public FirstRunObjectivePresentationContext(
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fontService,
        IUiClock uiClock)
    {
        CanvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        FontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        UiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public IDungeonUiCanvasProvider CanvasProvider { get; }
    public ITmpKoreanFontService FontService { get; }
    public IUiClock UiClock { get; }
}

public sealed class FirstRunObjectiveRuntime :
    IFirstRunObjectiveRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("FirstRunObjectiveRuntime.Tick");

    private const float RefreshInterval = 0.25f;

    private readonly IOwnerRunManagerProvider ownerProvider;
    private readonly IGridSystemProvider gridProvider;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly BlueprintResearchRuntime research;
    private readonly OperatingDaySettlementRuntime settlement;
    private readonly MetaProgressionRuntime metaProgression;
    private readonly IOffenseQuery offense;
    private readonly IDungeonRunFlowRuntime runFlow;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fontService;
    private readonly IUiClock uiClock;

    private GameObject root;
    private TMP_Text progressLabel;
    private TMP_Text titleLabel;
    private TMP_Text detailLabel;
    private float nextRefreshAt;

    public FirstRunObjectiveRuntime(
        FirstRunObjectiveProgressContext progress,
        FirstRunObjectivePresentationContext presentation)
    {
        progress = progress ?? throw new ArgumentNullException(nameof(progress));
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        ownerProvider = progress.OwnerProvider;
        gridProvider = progress.GridProvider;
        roomLayoutCache = progress.RoomLayoutCache;
        research = progress.Research;
        settlement = progress.Settlement;
        metaProgression = progress.MetaProgression;
        offense = progress.Offense;
        runFlow = progress.RunFlow;
        canvasProvider = presentation.CanvasProvider;
        fontService = presentation.FontService;
        uiClock = presentation.UiClock;
    }

    public FirstRunObjectiveId CurrentObjective { get; private set; }
    public bool IsVisible => false;
    public RectTransform PanelRect => root != null ? root.GetComponent<RectTransform>() : null;

    public void Start()
    {
        RefreshNow();
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (uiClock.Time < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = uiClock.Time + RefreshInterval;
        RefreshNow();
    }

    public void Dispose()
    {
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
            root = null;
        }
    }

    public void RefreshNow()
    {
        FirstRunObjectivePresentation presentation = FirstRunObjectiveResolver.Resolve(CaptureSnapshot());
        CurrentObjective = presentation.Id;

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private FirstRunObjectiveSnapshot CaptureSnapshot()
    {
        bool hasOwner = ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager != null
            && ownerManager.CurrentOwnerActor != null;

        bool hasUsableRoom = false;
        if (gridProvider.TryGetGrid(out Grid grid))
        {
            foreach (RoomInstance room in roomLayoutCache.GetLayout(grid).Rooms)
            {
                if (room != null && room.IsUsable && !room.IsSelfContained)
                {
                    hasUsableRoom = true;
                    break;
                }
            }
        }

        int taskCount = 0;
        int completedResearchCount = 0;
        float activeResearchRatio = 0f;
        bool hasResearchBlueprint = false;
        if (research != null)
        {
            taskCount = research.State.Projects.Queue.Count;
            completedResearchCount = research.State.Projects.CompletedProjectIds.Count;
            hasResearchBlueprint = research.ShopUnlockState.AcquiredBlueprintIds.Count > 0;
            ResearchProjectId activeProjectId = research.State.Projects.ActiveProjectId;
            if (activeProjectId.IsValid
                && research.ProjectCatalog != null
                && research.ProjectCatalog.TryGet(activeProjectId, out ResearchProjectSO activeProject))
            {
                activeResearchRatio = research.State.Projects
                    .GetProgress(activeProjectId)
                    .GetRatio(activeProject);
            }
            else if (research.State.TryGetActiveTask(out BlueprintResearchTask activeTask))
            {
                taskCount = Mathf.Max(taskCount, research.State.Tasks.Count);
                completedResearchCount = Mathf.Max(
                    completedResearchCount,
                    research.State.CompletedBlueprintIds.Count);
                activeResearchRatio = activeTask.ProgressRatio;
            }
        }

        int settlementCount = settlement.ReportHistory.Count;

        int defendedInvasionCount = 0;
        int completedRunCount = 0;
        MetaProgressionRuntime meta = metaProgression;
        if (meta != null)
        {
            settlementCount = Mathf.Max(settlementCount, meta.RunProgress.SettlementCount);
            defendedInvasionCount = meta.RunProgress.DefendedInvasionCount;
            completedRunCount = meta.State.CompletedRunCount;
        }

        OffenseCampaignSnapshot campaign = offense.Capture();
        int completedOffenseTargetCount = campaign.CompletedTargetCount;
        int totalOffenseTargetCount = campaign.CampaignTargetCount;
        bool truthRevealed = campaign.TruthRevealed;

        return new FirstRunObjectiveSnapshot(
            hasOwner,
            hasUsableRoom,
            taskCount,
            completedResearchCount,
            activeResearchRatio,
            settlementCount,
            defendedInvasionCount,
            runFlow.CurrentDay,
            runFlow.Phase,
            runFlow.Outcome,
            completedRunCount,
            completedOffenseTargetCount,
            totalOffenseTargetCount,
            truthRevealed,
            hasResearchBlueprint);
    }

    private void EnsureView()
    {
        if (root != null)
        {
            return;
        }

        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        if (canvas == null)
        {
            return;
        }

        root = new GameObject(
            "FirstRunObjectivePanel",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -164f);
        rect.sizeDelta = new Vector2(390f, 94f);

        Image panelImage = root.GetComponent<Image>();
        panelImage.color = new Color(
            DungeonUiTheme.Panel.r,
            DungeonUiTheme.Panel.g,
            DungeonUiTheme.Panel.b,
            0.96f);
        panelImage.raycastTarget = false;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        CreateAccentBar(root.transform);
        progressLabel = CreateLabel(
            root.transform,
            "Progress",
            new Vector2(18f, -29f),
            new Vector2(-14f, -8f),
            15f,
            DungeonUiTheme.TextSecondary,
            FontStyles.Bold);
        titleLabel = CreateLabel(
            root.transform,
            "Title",
            new Vector2(18f, -60f),
            new Vector2(-14f, -30f),
            21f,
            DungeonUiTheme.TextPrimary,
            FontStyles.Bold);
        detailLabel = CreateLabel(
            root.transform,
            "Detail",
            new Vector2(18f, -88f),
            new Vector2(-14f, -62f),
            15f,
            DungeonUiTheme.TextSecondary,
            FontStyles.Normal);
    }

    private static void CreateAccentBar(Transform parent)
    {
        GameObject barObject = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(5f, 0f);
        Image barImage = barObject.GetComponent<Image>();
        barImage.color = DungeonUiTheme.Accent;
        barImage.raycastTarget = false;
    }

    private TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float fontSize,
        Color color,
        FontStyles fontStyle)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        label.characterSpacing = 0f;
        fontService.Apply(label);
        return label;
    }
}
