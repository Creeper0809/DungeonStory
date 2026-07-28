using System;
using System.Collections.Generic;
using System.Diagnostics;
using BehaviorDesigner.Runtime;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CharacterAiScheduler : MonoBehaviour
{
    private static readonly ProfilerMarker ProcessAiBudgetMarker =
        new ProfilerMarker("CharacterAiScheduler.ProcessAiBudget");

    [SerializeField] private bool driveCharacterUpdates = true;
    [SerializeField] private bool driveBehaviorDesignerTrees = true;
    [SerializeField] private bool registerExistingSceneCharacters = true;
    [SerializeField] private ExternalBehaviorTree characterAiExternalBehavior;
    [SerializeField] private bool limitPathSearches = true;
    [SerializeField] private bool limitFeedbackToVisibleCharacters = true;
    [SerializeField] private bool adaptBudgetsToFrameCost = true;
    [SerializeField, Min(1)] private int maxDecisionsPerFrame = 512;
    [SerializeField, Min(1)] private int maxPathSearchesPerFrame = 256;
    [SerializeField, Min(0)] private int minDecisionsPerFrame;
    [SerializeField, Min(0)] private int minPathSearchesPerFrame;
    [SerializeField, Min(0.1f)] private float targetAiMilliseconds = 4f;
    [SerializeField, Min(8f)] private float targetFrameMilliseconds = 16.667f;
    [SerializeField, Range(0.05f, 1f)] private float frameHeadroomShare = 0.45f;
    [SerializeField, Range(0f, 1f)] private float baselineBudgetRatio = 0.2f;
    [SerializeField, Min(0.01f)] private float minimumUsefulSliceMilliseconds = 0.1f;
    [SerializeField, Min(0.1f)] private float maximumDecisionDeferralSeconds = 2f;
    [SerializeField, Range(1, 30)] private int overdraftCooldownTicks = 4;
    [SerializeField, Min(0f)] private float registrationSpreadSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float ownerDecisionInterval = 0.2f;
    [SerializeField, Min(0.01f)] private float visibleDecisionInterval = 0.35f;
    [SerializeField, Min(0.01f)] private float offscreenDecisionInterval = 1.5f;
    [SerializeField, Min(0.01f)] private float retryDelay = 0.05f;
    [SerializeField, Range(0f, 0.35f)] private float decisionIntervalJitterRatio = 0.16f;
    [SerializeField, Min(0f)] private float viewportMargin = 0.15f;
    [SerializeField, Range(1, 8)] private int offscreenMovementFrameStride = 3;

    private readonly List<CharacterActor> actors = new List<CharacterActor>();
    private readonly HashSet<CharacterActor> actorSet = new HashSet<CharacterActor>();
    private readonly Dictionary<CharacterActor, float> nextDecisionTime = new Dictionary<CharacterActor, float>();
    private readonly Dictionary<CharacterActor, int> decisionScheduleVersions =
        new Dictionary<CharacterActor, int>();
    private readonly Dictionary<CharacterActor, double> actorDecisionCostMilliseconds =
        new Dictionary<CharacterActor, double>();
    private readonly HashSet<CharacterActor> urgentDecisionRequests =
        new HashSet<CharacterActor>();
    private readonly List<ScheduledDecision> decisionScheduleHeap =
        new List<ScheduledDecision>();
    private readonly HashSet<CharacterActor> missingBehaviorTreeLogged = new HashSet<CharacterActor>();
    private readonly HashSet<CharacterActor> missingExternalBehaviorLogged = new HashSet<CharacterActor>();
    private long decisionScheduleSequence;
    private int pathBudgetFrame = -1;
    private int pathSearchesThisFrame;
    private int currentDecisionBudget;
    private int currentPathSearchBudget;
    private double estimatedDecisionMilliseconds;
    private double estimatedPathSearchMilliseconds;
    private double currentFrameBudgetMilliseconds;
    private double smoothedFrameMilliseconds;
    private int schedulerTickSequence;
    private int lastOverdraftTick = int.MinValue / 2;
    private float manualTime;
    private float schedulingTime;
    private long cumulativeProcessedDecisionCount;
    private long cumulativeStarvedDecisionCount;
    private long cumulativeSkippedDecisionCount;
    private long cumulativeLegacyFallbackCount;
    private float maximumObservedDecisionDeferralSeconds;
    private ICharacterWorldQuery characterWorld;
    private IMainCameraProvider mainCameraProvider;
    private ICharacterBehaviorTreeRuntimeConfigurator behaviorTreeConfigurator;
    private IGridPathSearchBroker pathSearchBroker;
    private IGameClock gameClock;
    private IUiClock uiClock;
    private IDynamicFrameWorkBudget frameWorkBudget;
    private ICharacterAiPerformanceRecorder performanceRecorder;
    private IFacilityCandidateCache facilityCandidateCache;
    private IPlayerStaffCommandSource playerStaffCommands;
    private double worldIndexMillisecondsThisFrame;

    public int RegisteredCharacterCount => actors.Count;
    public int LastProcessedDecisionCount { get; private set; }
    public int LastBehaviorTreeTickCount { get; private set; }
    public int LastLegacyFallbackCount { get; private set; }
    public int LastPathSearchCount { get; private set; }
    public int LastBrokerPathSearchCount { get; private set; }
    public int LastBrokerUnboundedPathSearchCount { get; private set; }
    public int LastBrokerPathCacheHitCount { get; private set; }
    public int LastBrokerPathBudgetDeferralCount { get; private set; }
    public double LastProcessingMilliseconds { get; private set; }
    public long LastAllocatedBytes { get; private set; } = -1L;
    public double CurrentFrameBudgetMilliseconds => currentFrameBudgetMilliseconds;
    public double EstimatedDecisionMilliseconds => estimatedDecisionMilliseconds;
    public double EstimatedPathSearchMilliseconds => estimatedPathSearchMilliseconds;
    public double SmoothedFrameMilliseconds => smoothedFrameMilliseconds;
    public long CumulativeProcessedDecisionCount => cumulativeProcessedDecisionCount;
    public long CumulativeStarvedDecisionCount => cumulativeStarvedDecisionCount;
    public long CumulativeSkippedDecisionCount => cumulativeSkippedDecisionCount;
    public long CumulativeLegacyFallbackCount => cumulativeLegacyFallbackCount;
    public float LastOldestDecisionDeferralSeconds { get; private set; }
    public float MaximumObservedDecisionDeferralSeconds =>
        maximumObservedDecisionDeferralSeconds;
    public bool LastBudgetExhausted { get; private set; }
    public ExternalBehaviorTree CharacterAiExternalBehavior => characterAiExternalBehavior;
    public bool IsDrivingAi => enabled && driveCharacterUpdates;
    public int CurrentDecisionBudget => Mathf.Max(0, currentDecisionBudget);
    public int CurrentPathSearchBudget => Mathf.Max(0, currentPathSearchBudget);
    public bool IsPathBudgetActiveForDebug => enabled
        && driveCharacterUpdates
        && limitPathSearches;

    private void Awake()
    {
        ConfigureBehaviorManagerForManualTick();
    }

    private void OnEnable()
    {
        ConfigureBehaviorManagerForManualTick();
        if (registerExistingSceneCharacters)
        {
            RegisterExistingCharactersIfInjected();
        }
    }

    [Inject]
    public void Construct(
        ICharacterWorldQuery characterWorld,
        IMainCameraProvider mainCameraProvider,
        ICharacterBehaviorTreeRuntimeConfigurator behaviorTreeConfigurator,
        IGridPathSearchBroker pathSearchBroker,
        IGameClock gameClock,
        IDynamicFrameWorkBudget frameWorkBudget,
        ICharacterAiPerformanceRecorder performanceRecorder = null,
        IUiClock uiClock = null,
        IFacilityCandidateCache facilityCandidateCache = null,
        IPlayerStaffCommandSource playerStaffCommands = null)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.behaviorTreeConfigurator = behaviorTreeConfigurator
            ?? throw new ArgumentNullException(nameof(behaviorTreeConfigurator));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        this.performanceRecorder = performanceRecorder;
        this.uiClock = uiClock;
        this.facilityCandidateCache = facilityCandidateCache;
        this.playerStaffCommands = playerStaffCommands;
        schedulingTime = uiClock != null
            ? uiClock.Time
            : gameClock.Time;

        if (isActiveAndEnabled && registerExistingSceneCharacters)
        {
            RegisterExistingCharacters();
        }
    }

    private void Start()
    {
        if (registerExistingSceneCharacters)
        {
            RegisterExistingCharacters();
        }
    }

    private void Update()
    {
        if (gameClock.IsPaused)
        {
            LastProcessedDecisionCount = 0;
            LastBehaviorTreeTickCount = 0;
            LastLegacyFallbackCount = 0;
            LastPathSearchCount = 0;
            LastAllocatedBytes = -1L;
            return;
        }

        schedulingTime = uiClock != null
            ? schedulingTime + Mathf.Max(0f, uiClock.DeltaTime)
            : gameClock.Time;
        ProcessAiBudget(schedulingTime);
    }

    public void RegisterActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        RegisterInternal(actor);
    }

    public void UnregisterActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        UnregisterInternal(actor);
    }

    public void RequestImmediateDecisionFor(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        RegisterInternal(actor);
        urgentDecisionRequests.Add(actor);
        ScheduleDecision(actor, CurrentSchedulingTime);
    }

    public bool TryConsumePathSearchBudget()
    {
        if (!enabled || !driveCharacterUpdates || !limitPathSearches)
        {
            return true;
        }

        return TryConsumePathSearchBudgetInternal();
    }

    public bool ShouldShowCharacterFeedbackFor(CharacterActor actor)
    {
        if (!enabled || !limitFeedbackToVisibleCharacters)
        {
            return true;
        }

        return IsHighDetailCharacter(actor);
    }

    public bool ShouldCollectDetailedDiagnosticsFor(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (playerStaffCommands != null)
        {
            return playerStaffCommands.SelectedActor == actor;
        }

        return actor.IsOwner;
    }

    public int GetMovementFrameStrideFor(CharacterActor actor)
    {
        if (!enabled
            || !driveCharacterUpdates
            || offscreenMovementFrameStride <= 1
            || IsHighDetailCharacter(actor))
        {
            return 1;
        }

        return offscreenMovementFrameStride;
    }

    public double GetDecisionWorkSliceMillisecondsFor(CharacterActor actor)
    {
        EnsureAdaptiveBudgetsInitialized();
        double frameShare = currentFrameBudgetMilliseconds * 0.18;
        double predictedCost = GetPredictedDecisionCost(actor);
        double adaptiveSlice = Math.Min(predictedCost * 0.35, frameShare);
        return Math.Clamp(
            adaptiveSlice,
            minimumUsefulSliceMilliseconds,
            Math.Max(minimumUsefulSliceMilliseconds, 0.65));
    }

    public void RunManualTick(float deltaTime)
    {
        manualTime += Mathf.Max(0f, deltaTime);
        ProcessAiBudget(manualTime);
    }

    public void ClearRegistrationsForDebug()
    {
        ResetAdaptiveBudgets();
        manualTime = CurrentSchedulingTime;
        actors.Clear();
        actorSet.Clear();
        nextDecisionTime.Clear();
        decisionScheduleVersions.Clear();
        actorDecisionCostMilliseconds.Clear();
        urgentDecisionRequests.Clear();
        decisionScheduleHeap.Clear();
        missingBehaviorTreeLogged.Clear();
        missingExternalBehaviorLogged.Clear();
        decisionScheduleSequence = 0L;
        cumulativeProcessedDecisionCount = 0L;
        cumulativeStarvedDecisionCount = 0L;
        cumulativeSkippedDecisionCount = 0L;
        cumulativeLegacyFallbackCount = 0L;
        LastOldestDecisionDeferralSeconds = 0f;
        maximumObservedDecisionDeferralSeconds = 0f;
        LastBudgetExhausted = false;
    }

    public void ResetPathSearchBudgetForDebugInstance()
    {
        pathBudgetFrame = -1;
        pathSearchesThisFrame = 0;
        LastPathSearchCount = 0;
        EnsureAdaptiveBudgetsInitialized();
    }

    public float GetNextDecisionDelayForDebug(CharacterActor actor)
    {
        if (actor == null || !nextDecisionTime.TryGetValue(actor, out float dueTime))
        {
            return 0f;
        }

        return Mathf.Max(0f, dueTime - CurrentSchedulingTime);
    }

    private void ProcessAiBudget(float now)
    {
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.PauseHumanoidAi))
        {
            LastProcessedDecisionCount = 0;
            LastBehaviorTreeTickCount = 0;
            LastLegacyFallbackCount = 0;
            LastPathSearchCount = 0;
            LastAllocatedBytes = -1L;
            return;
        }

        using (ProcessAiBudgetMarker.Auto())
        {
            schedulerTickSequence++;
            UpdateFrameTimeBudget();
            frameWorkBudget.SetBacklog(
                DynamicFrameWorkDomain.AiDecision,
                decisionScheduleHeap.Count);
            currentFrameBudgetMilliseconds = Math.Min(
                currentFrameBudgetMilliseconds,
                frameWorkBudget.GetSliceMilliseconds(
                    DynamicFrameWorkDomain.AiDecision,
                    minimumUsefulSliceMilliseconds,
                    targetAiMilliseconds,
                    LastOldestDecisionDeferralSeconds
                        >= maximumDecisionDeferralSeconds));
            long startTimestamp = Stopwatch.GetTimestamp();
            long startAllocatedBytes = performanceRecorder?.DetailedCollectionEnabled == true
                ? GC.GetAllocatedBytesForCurrentThread()
                : -1L;
            try
            {
                BeginPathBudgetWindow();
                worldIndexMillisecondsThisFrame = 0.0;
                AdvanceIncrementalWorldIndexes();
                LastProcessedDecisionCount = 0;
                LastBehaviorTreeTickCount = 0;
                LastLegacyFallbackCount = 0;
                LastBudgetExhausted = false;

                if (actors.Count == 0)
                {
                    LastPathSearchCount = 0;
                    LastOldestDecisionDeferralSeconds = 0f;
                    CaptureBrokerCounters();
                    return;
                }

                int decisionCountBudget = GetDecisionBudgetForFrame();
                int decisionSafetyLimit = ResolveDecisionSafetyLimit();
                while (actors.Count > 0
                    && LastProcessedDecisionCount < decisionSafetyLimit
                    && TryPeekDueDecision(now, out ScheduledDecision scheduled))
                {
                    CharacterActor actor = scheduled.Actor;
                    double elapsedBeforeDecision =
                        GetElapsedMilliseconds(startTimestamp);
                    double predictedDecisionMilliseconds =
                        GetPredictedDecisionCost(actor);
                    bool urgent = urgentDecisionRequests.Contains(actor);
                    bool starved = now - scheduled.DueTime
                        >= maximumDecisionDeferralSeconds;
                    bool withinCountBudget =
                        LastProcessedDecisionCount < decisionCountBudget;
                    bool withinTimeBudget =
                        elapsedBeforeDecision + predictedDecisionMilliseconds
                        <= currentFrameBudgetMilliseconds;
                    bool canOverdraft = LastProcessedDecisionCount == 0
                        && schedulerTickSequence - lastOverdraftTick
                            >= Mathf.Max(1, overdraftCooldownTicks);
                    if ((!withinCountBudget || !withinTimeBudget)
                        && !(canOverdraft && (urgent || starved)))
                    {
                        LastBudgetExhausted = true;
                        break;
                    }

                    if (canOverdraft
                        && (urgent || starved)
                        && (!withinCountBudget || !withinTimeBudget))
                    {
                        lastOverdraftTick = schedulerTickSequence;
                    }

                    if (!TryTakeDueDecision(now, out actor))
                    {
                        break;
                    }

                    urgentDecisionRequests.Remove(actor);
                    bool hasSelectedActionWaitingToStart = HasSelectedActionWaitingToStart(actor);
                    BehaviorTree behaviorTree = actor.BehaviorTree;
                    bool needsInitialTreeTick = behaviorTree != null
                        && behaviorTree.DungeonStoryTickCount == 0;
                    if (!actor.IsAiDecisionPending
                        && !needsInitialTreeTick
                        && !hasSelectedActionWaitingToStart)
                    {
                        cumulativeSkippedDecisionCount++;
                        continue;
                    }

                    bool collectDecisionPerformance =
                        performanceRecorder?.DetailedCollectionEnabled == true;
                    long behaviorStart = Stopwatch.GetTimestamp();
                    long behaviorAllocatedAtStart = collectDecisionPerformance
                        ? GC.GetAllocatedBytesForCurrentThread()
                        : 0L;
                    bool decided = TryRunScheduledDecision(actor);
                    double decisionMilliseconds =
                        GetElapsedMilliseconds(behaviorStart);
                    UpdateEstimatedDecisionCost(actor, decisionMilliseconds);
                    if (collectDecisionPerformance)
                    {
                        performanceRecorder.Record(
                            AiPerformanceCategory.BehaviorTree,
                            decisionMilliseconds,
                            Math.Max(
                                0L,
                                GC.GetAllocatedBytesForCurrentThread()
                                    - behaviorAllocatedAtStart));
                    }
                    AIBrain brain = actor.Brain;
                    bool hasPendingDecisionWork = brain != null
                        && (brain.IsActionScoringPending
                            || brain.IsPathSearchDeferred);
                    float nextDelay = HasSelectedActionWaitingToStart(actor)
                        || hasPendingDecisionWork
                        ? retryDelay
                        : decided ? GetDecisionInterval(actor) : retryDelay;
                    ScheduleDecision(actor, now + nextDelay);
                    LastProcessedDecisionCount++;
                    cumulativeProcessedDecisionCount++;
                    if (starved)
                    {
                        cumulativeStarvedDecisionCount++;
                    }
                    double elapsedMilliseconds =
                        GetElapsedMilliseconds(startTimestamp);
                    if (elapsedMilliseconds >= currentFrameBudgetMilliseconds)
                    {
                        LastBudgetExhausted = true;
                        break;
                    }
                }

                UpdateBacklogTelemetry(now);
#if UNITY_EDITOR
                RefreshBehaviorDesignerVisualsForEditor();
#endif
                LastPathSearchCount = pathSearchesThisFrame;
                CaptureBrokerCounters();
            }
            finally
            {
                LastProcessingMilliseconds =
                    GetElapsedMilliseconds(startTimestamp);
                double pathMilliseconds =
                    Math.Max(0.0, pathSearchBroker.SearchMillisecondsThisFrame);
                frameWorkBudget.ReportConsumed(
                    DynamicFrameWorkDomain.AiDecision,
                    Math.Max(
                        0.0,
                        LastProcessingMilliseconds
                            - pathMilliseconds
                            - worldIndexMillisecondsThisFrame));
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.Pathfinding,
                    LastBrokerPathBudgetDeferralCount);
                frameWorkBudget.ReportConsumed(
                    DynamicFrameWorkDomain.Pathfinding,
                    pathMilliseconds);
                long allocatedBytes = startAllocatedBytes >= 0L
                    ? Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - startAllocatedBytes)
                    : -1L;
                LastAllocatedBytes = allocatedBytes;
                performanceRecorder?.Record(
                    AiPerformanceCategory.Scheduler,
                    LastProcessingMilliseconds,
                    Math.Max(0L, allocatedBytes));
                performanceRecorder?.RecordPathCounters(
                    LastBrokerPathSearchCount,
                    LastBrokerPathCacheHitCount,
                    LastBrokerPathBudgetDeferralCount);
                UpdatePathSearchCostEstimate();
            }
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
    }

    private void AdvanceIncrementalWorldIndexes()
    {
        if (facilityCandidateCache?.HasPendingIndexBuild != true)
        {
            frameWorkBudget.SetBacklog(
                DynamicFrameWorkDomain.WorldIndex,
                0);
            return;
        }

        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.WorldIndex,
            1);
        double indexBudgetMilliseconds =
            frameWorkBudget.GetSliceMilliseconds(
                DynamicFrameWorkDomain.WorldIndex,
                minimumUsefulSliceMilliseconds,
                Math.Max(
                    minimumUsefulSliceMilliseconds,
                    currentFrameBudgetMilliseconds * 0.25));
        if (indexBudgetMilliseconds < minimumUsefulSliceMilliseconds)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        facilityCandidateCache.AdvanceIndex(indexBudgetMilliseconds);
        worldIndexMillisecondsThisFrame =
            GetElapsedMilliseconds(started);
        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.WorldIndex,
            worldIndexMillisecondsThisFrame);
    }

#if UNITY_EDITOR
    private void RefreshBehaviorDesignerVisualsForEditor()
    {
        if (!driveBehaviorDesignerTrees || !Application.isPlaying)
        {
            return;
        }

        GameObject selectedObject = Selection.activeGameObject;
        CharacterActor selectedActor = selectedObject != null
            ? selectedObject.GetComponent<CharacterActor>() ?? selectedObject.GetComponentInParent<CharacterActor>()
            : null;
        if (selectedActor == null || !actors.Contains(selectedActor))
        {
            return;
        }

        BehaviorTree behaviorTree = ConfigureCharacterBehaviorTree(selectedActor);
        behaviorTree?.DungeonStoryRefreshVisualStatus(selectedActor);
    }
#endif

    private void RegisterExistingCharacters()
    {
        foreach (CharacterActor actor in RequireCharacterWorld().Characters)
        {
            RegisterInternal(actor);
        }
    }

    private void RegisterExistingCharactersIfInjected()
    {
        if (characterWorld != null)
        {
            RegisterExistingCharacters();
        }
    }

    private ICharacterWorldQuery RequireCharacterWorld()
    {
        if (characterWorld == null)
        {
            throw new InvalidOperationException(
                $"{nameof(CharacterAiScheduler)} requires {nameof(ICharacterWorldQuery)} injection.");
        }

        return characterWorld;
    }

    private void RegisterInternal(CharacterActor actor)
    {
        if (actor == null || !actorSet.Add(actor))
        {
            return;
        }

        actor.EnsureRuntimeState();
        EnsureAdaptiveBudgetsInitialized();
        actors.Add(actor);
        float now = CurrentSchedulingTime;
        float spread = Mathf.Min(
            Mathf.Max(0f, registrationSpreadSeconds),
            GetRegistrationDecisionInterval(actor));
        ScheduleDecision(actor, now + spread * ResolveActorStableFraction(actor));
    }

    private bool TryRunScheduledDecision(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        actor.EnsureRuntimeState();
        AIBrain brain = actor.Brain;
        if (brain != null && brain.HasResumableDecisionPipeline)
        {
            LastBehaviorTreeTickCount++;
            CharacterAiDecisionTickResult result = brain.RunDecisionTreeDirect();
            if (driveBehaviorDesignerTrees)
            {
                actor.BehaviorTree?.DungeonStoryRecordDirectTick(actor, result);
            }

            return result.Handled;
        }

        LastLegacyFallbackCount++;
        cumulativeLegacyFallbackCount++;
        return TryRunLegacyFallbackDecision(actor, brain);
    }

    private static bool TryRunLegacyFallbackDecision(
        CharacterActor actor,
        AIBrain brain)
    {
        brain ??= actor != null ? actor.GetComponent<AIBrain>() : null;
        if (brain == null)
        {
            return false;
        }

        if (HasSelectedActionWaitingToStart(actor))
        {
            return actor.TryExecuteSelectedAiAction();
        }

        bool decided = brain.DecideAction();
        if (!decided)
        {
            return false;
        }

        return HasSelectedActionWaitingToStart(actor)
            ? actor.TryExecuteSelectedAiAction()
            : true;
    }

    private static bool HasSelectedActionWaitingToStart(CharacterActor actor)
    {
        AIBrain brain = actor != null ? actor.Brain : null;
        AIAction selectedAction = brain != null ? brain.bestAction : null;
        return actor != null
            && actor.CanRunAi
            && brain != null
            && selectedAction != null
            && selectedAction.actionset != null
            && !selectedAction.HasStarted
            && !brain.isBestActionEnd;
    }

    private static void ConfigureBehaviorManagerForManualTick()
    {
        if (BehaviorManager.instance == null)
        {
            Behavior.CreateBehaviorManager();
        }

        if (BehaviorManager.instance != null)
        {
            // DungeonStory patch: scheduler owns character BT cadence.
            BehaviorManager.instance.UpdateInterval = UpdateIntervalType.Manual;
        }
    }

    private BehaviorTree ConfigureCharacterBehaviorTree(CharacterActor actor)
    {
        return RequireBehaviorTreeConfigurator().Configure(actor, characterAiExternalBehavior);
    }

    private void UnregisterInternal(CharacterActor actor)
    {
        if (!actorSet.Remove(actor))
        {
            nextDecisionTime.Remove(actor);
            decisionScheduleVersions.Remove(actor);
            return;
        }

        nextDecisionTime.Remove(actor);
        decisionScheduleVersions.Remove(actor);
        actorDecisionCostMilliseconds.Remove(actor);
        urgentDecisionRequests.Remove(actor);
        int index = actors.IndexOf(actor);
        if (index >= 0)
        {
            actors.RemoveAt(index);
        }
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= actors.Count)
        {
            return;
        }

        CharacterActor actor = actors[index];
        actorSet.Remove(actor);
        nextDecisionTime.Remove(actor);
        decisionScheduleVersions.Remove(actor);
        actorDecisionCostMilliseconds.Remove(actor);
        urgentDecisionRequests.Remove(actor);
        actors.RemoveAt(index);
    }

    private void ScheduleDecision(CharacterActor actor, float dueTime)
    {
        if (actor == null || !actorSet.Contains(actor))
        {
            return;
        }

        if (nextDecisionTime.TryGetValue(actor, out float existingDueTime)
            && existingDueTime <= dueTime + 0.0001f)
        {
            return;
        }

        int version = decisionScheduleVersions.TryGetValue(actor, out int currentVersion)
            ? currentVersion + 1
            : 1;
        decisionScheduleVersions[actor] = version;
        nextDecisionTime[actor] = dueTime;
        PushScheduledDecision(new ScheduledDecision(
            actor,
            dueTime,
            version,
            decisionScheduleSequence++));
        CompactScheduleHeapIfNeeded();
    }

    private bool TryPeekDueDecision(float now, out ScheduledDecision scheduled)
    {
        scheduled = default;
        while (decisionScheduleHeap.Count > 0)
        {
            ScheduledDecision next = decisionScheduleHeap[0];
            if (next.Actor == null
                || !actorSet.Contains(next.Actor)
                || !decisionScheduleVersions.TryGetValue(next.Actor, out int activeVersion)
                || activeVersion != next.Version)
            {
                PopScheduledDecision();
                continue;
            }

            if (next.DueTime > now)
            {
                return false;
            }

            scheduled = next;
            return true;
        }

        return false;
    }

    private bool TryTakeDueDecision(float now, out CharacterActor actor)
    {
        actor = null;
        while (decisionScheduleHeap.Count > 0)
        {
            ScheduledDecision next = decisionScheduleHeap[0];
            if (next.DueTime > now)
            {
                return false;
            }

            PopScheduledDecision();
            if (next.Actor == null
                || !actorSet.Contains(next.Actor)
                || !decisionScheduleVersions.TryGetValue(next.Actor, out int activeVersion)
                || activeVersion != next.Version)
            {
                continue;
            }

            nextDecisionTime.Remove(next.Actor);
            actor = next.Actor;
            return true;
        }

        return false;
    }

    private void PushScheduledDecision(ScheduledDecision entry)
    {
        int index = decisionScheduleHeap.Count;
        decisionScheduleHeap.Add(entry);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (!entry.IsEarlierThan(decisionScheduleHeap[parent]))
            {
                break;
            }

            decisionScheduleHeap[index] = decisionScheduleHeap[parent];
            index = parent;
        }

        decisionScheduleHeap[index] = entry;
    }

    private void PopScheduledDecision()
    {
        int lastIndex = decisionScheduleHeap.Count - 1;
        ScheduledDecision tail = decisionScheduleHeap[lastIndex];
        decisionScheduleHeap.RemoveAt(lastIndex);
        if (lastIndex == 0)
        {
            return;
        }

        int index = 0;
        while (true)
        {
            int left = (index << 1) + 1;
            if (left >= lastIndex)
            {
                break;
            }

            int right = left + 1;
            int child = right < lastIndex
                && decisionScheduleHeap[right].IsEarlierThan(decisionScheduleHeap[left])
                    ? right
                    : left;
            if (!decisionScheduleHeap[child].IsEarlierThan(tail))
            {
                break;
            }

            decisionScheduleHeap[index] = decisionScheduleHeap[child];
            index = child;
        }

        decisionScheduleHeap[index] = tail;
    }

    private void CompactScheduleHeapIfNeeded()
    {
        int maximumUsefulEntries = Mathf.Max(128, actorSet.Count * 4 + 128);
        if (decisionScheduleHeap.Count <= maximumUsefulEntries)
        {
            return;
        }

        decisionScheduleHeap.Clear();
        foreach (KeyValuePair<CharacterActor, float> pair in nextDecisionTime)
        {
            CharacterActor actor = pair.Key;
            if (actor == null
                || !actorSet.Contains(actor)
                || !decisionScheduleVersions.TryGetValue(actor, out int version))
            {
                continue;
            }

            PushScheduledDecision(new ScheduledDecision(
                actor,
                pair.Value,
                version,
                decisionScheduleSequence++));
        }
    }

    private readonly struct ScheduledDecision
    {
        public ScheduledDecision(
            CharacterActor actor,
            float dueTime,
            int version,
            long sequence)
        {
            Actor = actor;
            DueTime = dueTime;
            Version = version;
            Sequence = sequence;
        }

        public CharacterActor Actor { get; }
        public float DueTime { get; }
        public int Version { get; }
        public long Sequence { get; }

        public bool IsEarlierThan(ScheduledDecision other)
        {
            return DueTime < other.DueTime
                || (DueTime == other.DueTime
                    && Sequence < other.Sequence);
        }
    }

    private float GetDecisionInterval(CharacterActor actor)
    {
        float interval;
        if (actor != null && actor.IsOwner)
        {
            interval = ownerDecisionInterval;
        }
        else
        {
            interval = IsHighDetailCharacter(actor)
                ? visibleDecisionInterval
                : offscreenDecisionInterval;
        }

        return interval * ResolveActorIntervalJitter(actor);
    }

    private float CurrentSchedulingTime =>
        uiClock != null
            ? schedulingTime
            : gameClock != null
                ? gameClock.Time
                : manualTime;

    private float GetRegistrationDecisionInterval(CharacterActor actor)
    {
        if (actor != null && actor.IsOwner)
        {
            return ownerDecisionInterval;
        }

        if (mainCameraProvider == null)
        {
            return offscreenDecisionInterval;
        }

        return GetDecisionInterval(actor);
    }

    private float ResolveActorIntervalJitter(CharacterActor actor)
    {
        if (actor == null || decisionIntervalJitterRatio <= 0f)
        {
            return 1f;
        }

        float fraction = ResolveActorStableFraction(actor);
        return Mathf.Lerp(1f - decisionIntervalJitterRatio, 1f + decisionIntervalJitterRatio, fraction);
    }

    private static float ResolveActorStableFraction(CharacterActor actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        float raw = Mathf.Abs(
            Mathf.Sin(actor.GetInstanceID() * 12.9898f)
            * 43758.5453f);
        return raw - Mathf.Floor(raw);
    }

    private bool IsHighDetailCharacter(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        if (actor.IsOwner)
        {
            return true;
        }

        Camera camera = RequireMainCameraProvider().Camera;
        if (camera == null)
        {
            return true;
        }

        Vector3 viewport = camera.WorldToViewportPoint(actor.transform.position);
        return viewport.z >= 0f
            && viewport.x >= -viewportMargin
            && viewport.x <= 1f + viewportMargin
            && viewport.y >= -viewportMargin
            && viewport.y <= 1f + viewportMargin;
    }

    private bool TryConsumePathSearchBudgetInternal()
    {
        ResetPathBudgetIfNeeded();
        if (pathSearchesThisFrame >= GetPathSearchBudgetForFrame())
        {
            return false;
        }

        pathSearchesThisFrame++;
        return true;
    }

    private void BeginPathBudgetWindow()
    {
        EnsureAdaptiveBudgetsInitialized();
        RequirePathSearchBroker().BeginFrame(
            GetPathSearchBudgetForFrame(),
            enabled && driveCharacterUpdates && limitPathSearches,
            currentFrameBudgetMilliseconds * 0.3);
        pathBudgetFrame = gameClock.FrameCount;
        pathSearchesThisFrame = 0;
        LastPathSearchCount = 0;
        LastBrokerPathSearchCount = 0;
        LastBrokerUnboundedPathSearchCount = 0;
        LastBrokerPathCacheHitCount = 0;
        LastBrokerPathBudgetDeferralCount = 0;
    }

    private void CaptureBrokerCounters()
    {
        IGridPathSearchBroker broker = RequirePathSearchBroker();
        LastBrokerPathSearchCount = broker.SearchesThisFrame;
        LastBrokerUnboundedPathSearchCount = broker.UnboundedSearchesThisFrame;
        LastBrokerPathCacheHitCount = broker.CacheHitsThisFrame;
        LastBrokerPathBudgetDeferralCount = broker.BudgetDeferralsThisFrame;
    }

    private void ResetPathBudgetIfNeeded()
    {
        if (pathBudgetFrame == gameClock.FrameCount)
        {
            return;
        }

        pathBudgetFrame = gameClock.FrameCount;
        pathSearchesThisFrame = 0;
    }

    private int GetDecisionBudgetForFrame()
    {
        EnsureAdaptiveBudgetsInitialized();
        return Mathf.Clamp(
            currentDecisionBudget,
            Mathf.Max(0, minDecisionsPerFrame),
            ResolveDecisionSafetyLimit());
    }

    private int GetPathSearchBudgetForFrame()
    {
        EnsureAdaptiveBudgetsInitialized();
        return Mathf.Clamp(
            currentPathSearchBudget,
            Mathf.Max(0, minPathSearchesPerFrame),
            ResolvePathSearchSafetyLimit());
    }

    private int ResolveDecisionSafetyLimit()
    {
        return Mathf.Clamp(
            Mathf.Max(maxDecisionsPerFrame, actors.Count),
            64,
            4096);
    }

    private int ResolvePathSearchSafetyLimit()
    {
        return Mathf.Clamp(
            Mathf.Max(maxPathSearchesPerFrame, actors.Count),
            32,
            4096);
    }

    private void UpdateBacklogTelemetry(float now)
    {
        if (!TryPeekDueDecision(now, out ScheduledDecision scheduled))
        {
            LastOldestDecisionDeferralSeconds = 0f;
            return;
        }

        LastOldestDecisionDeferralSeconds = Mathf.Max(
            0f,
            now - scheduled.DueTime);
        maximumObservedDecisionDeferralSeconds = Mathf.Max(
            maximumObservedDecisionDeferralSeconds,
            LastOldestDecisionDeferralSeconds);
    }

    private void EnsureAdaptiveBudgetsInitialized()
    {
        if (estimatedDecisionMilliseconds <= 0.0
            || estimatedPathSearchMilliseconds <= 0.0)
        {
            ResetAdaptiveBudgets();
        }
    }

    private void ResetAdaptiveBudgets()
    {
        estimatedDecisionMilliseconds = 0.25;
        estimatedPathSearchMilliseconds = 0.35;
        currentFrameBudgetMilliseconds = Mathf.Max(
            minimumUsefulSliceMilliseconds,
            targetAiMilliseconds * baselineBudgetRatio);
        smoothedFrameMilliseconds = targetFrameMilliseconds;
        RecalculateWorkUnitBudgets();
    }

    private void UpdateEstimatedDecisionCost(
        CharacterActor actor,
        double elapsedMilliseconds)
    {
        if (elapsedMilliseconds <= 0.0)
        {
            return;
        }

        double cappedSample = Math.Min(
            elapsedMilliseconds,
            Math.Max(
                targetAiMilliseconds,
                estimatedDecisionMilliseconds * 4.0));
        if (estimatedDecisionMilliseconds <= 0.0)
        {
            estimatedDecisionMilliseconds = cappedSample;
            return;
        }

        const double recentSampleWeight = 0.2;
        estimatedDecisionMilliseconds +=
            (cappedSample - estimatedDecisionMilliseconds)
            * recentSampleWeight;

        if (actor == null)
        {
            return;
        }

        if (!actorDecisionCostMilliseconds.TryGetValue(
                actor,
                out double actorEstimate)
            || actorEstimate <= 0.0)
        {
            actorDecisionCostMilliseconds[actor] = cappedSample;
            return;
        }

        actorDecisionCostMilliseconds[actor] =
            actorEstimate
            + (cappedSample - actorEstimate)
            * recentSampleWeight;
    }

    private double GetPredictedDecisionCost(CharacterActor actor)
    {
        if (actor != null
            && actorDecisionCostMilliseconds.TryGetValue(
                actor,
                out double actorEstimate)
            && actorEstimate > 0.0)
        {
            return Math.Max(
                minimumUsefulSliceMilliseconds,
                actorEstimate);
        }

        return Math.Max(
            minimumUsefulSliceMilliseconds,
            estimatedDecisionMilliseconds);
    }

    private void UpdateFrameTimeBudget()
    {
        EnsureAdaptiveBudgetsInitialized();
        if (!adaptBudgetsToFrameCost)
        {
            currentFrameBudgetMilliseconds = targetAiMilliseconds;
            RecalculateWorkUnitBudgets();
            return;
        }

        double observedFrameMilliseconds = uiClock != null
            ? Math.Max(0.0, uiClock.DeltaTime * 1000.0)
            : targetFrameMilliseconds;
        if (smoothedFrameMilliseconds <= 0.0)
        {
            smoothedFrameMilliseconds = observedFrameMilliseconds;
        }
        else
        {
            const double frameSampleWeight = 0.12;
            smoothedFrameMilliseconds +=
                (observedFrameMilliseconds - smoothedFrameMilliseconds)
                * frameSampleWeight;
        }

        double headroomMilliseconds = targetFrameMilliseconds
            - smoothedFrameMilliseconds;
        double baselineMilliseconds =
            targetAiMilliseconds * baselineBudgetRatio;
        if (headroomMilliseconds < 0.0)
        {
            double overrunRatio = Math.Min(
                1.0,
                -headroomMilliseconds
                / Math.Max(1.0, targetFrameMilliseconds * 0.5));
            baselineMilliseconds *= 1.0 - overrunRatio;
        }

        double desiredBudgetMilliseconds = baselineMilliseconds
            + Math.Max(0.0, headroomMilliseconds) * frameHeadroomShare;
        if (LastProcessingMilliseconds > targetAiMilliseconds)
        {
            desiredBudgetMilliseconds *= Math.Max(
                0.2,
                targetAiMilliseconds
                / Math.Max(targetAiMilliseconds, LastProcessingMilliseconds));
        }

        desiredBudgetMilliseconds = Math.Clamp(
            desiredBudgetMilliseconds,
            0.0,
            targetAiMilliseconds);
        double adjustmentWeight = desiredBudgetMilliseconds
            < currentFrameBudgetMilliseconds
                ? 0.45
                : 0.16;
        currentFrameBudgetMilliseconds +=
            (desiredBudgetMilliseconds - currentFrameBudgetMilliseconds)
            * adjustmentWeight;
        if (currentFrameBudgetMilliseconds < minimumUsefulSliceMilliseconds)
        {
            currentFrameBudgetMilliseconds = 0.0;
        }

        RecalculateWorkUnitBudgets();
    }

    private void RecalculateWorkUnitBudgets()
    {
        double usableBudgetMilliseconds = Math.Max(
            0.0,
            currentFrameBudgetMilliseconds);
        currentDecisionBudget = usableBudgetMilliseconds
            < minimumUsefulSliceMilliseconds
                ? 0
                : Mathf.Clamp(
                    (int)Math.Floor(
                        usableBudgetMilliseconds
                        / Math.Max(
                            minimumUsefulSliceMilliseconds,
                            estimatedDecisionMilliseconds)),
                    Mathf.Max(0, minDecisionsPerFrame),
                    ResolveDecisionSafetyLimit());

        double pathBudgetMilliseconds =
            usableBudgetMilliseconds * 0.3;
        currentPathSearchBudget = pathBudgetMilliseconds
            < minimumUsefulSliceMilliseconds
                ? 0
                : Mathf.Clamp(
                    (int)Math.Floor(
                        pathBudgetMilliseconds
                        / Math.Max(
                            minimumUsefulSliceMilliseconds,
                            estimatedPathSearchMilliseconds)),
                    Mathf.Max(0, minPathSearchesPerFrame),
                    ResolvePathSearchSafetyLimit());
    }

    private void UpdatePathSearchCostEstimate()
    {
        IGridPathSearchBroker broker = pathSearchBroker;
        if (broker == null || broker.SearchesThisFrame <= 0)
        {
            return;
        }

        double rawSample = broker.SearchMillisecondsThisFrame
            / broker.SearchesThisFrame;
        if (rawSample <= 0.0)
        {
            return;
        }

        double sample = Math.Min(
            rawSample,
            Math.Max(
                targetAiMilliseconds * 0.5,
                estimatedPathSearchMilliseconds * 4.0));
        const double recentSampleWeight = 0.2;
        estimatedPathSearchMilliseconds +=
            (sample - estimatedPathSearchMilliseconds)
            * recentSampleWeight;
    }

    private IMainCameraProvider RequireMainCameraProvider()
    {
        return mainCameraProvider
            ?? throw new InvalidOperationException($"{nameof(CharacterAiScheduler)} requires {nameof(IMainCameraProvider)} injection.");
    }

    private ICharacterBehaviorTreeRuntimeConfigurator RequireBehaviorTreeConfigurator()
    {
        return behaviorTreeConfigurator
            ?? throw new InvalidOperationException($"{nameof(CharacterAiScheduler)} requires {nameof(ICharacterBehaviorTreeRuntimeConfigurator)} injection.");
    }

    private IGridPathSearchBroker RequirePathSearchBroker()
    {
        return pathSearchBroker
            ?? throw new InvalidOperationException($"{nameof(CharacterAiScheduler)} requires {nameof(IGridPathSearchBroker)} injection.");
    }
}
