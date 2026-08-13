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
    [SerializeField, Range(1, 8)] private int maxPathSearchesPerFrame = 8;
    [SerializeField, Min(1)] private int minDecisionsPerFrame = 1;
    [SerializeField, Min(1)] private int minPathSearchesPerFrame = 1;
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
    private readonly HashSet<CharacterActor> urgentDecisionRequests =
        new HashSet<CharacterActor>();
    private readonly HashSet<CharacterActor> missingBehaviorTreeLogged = new HashSet<CharacterActor>();
    private readonly HashSet<CharacterActor> missingExternalBehaviorLogged = new HashSet<CharacterActor>();
    private readonly CharacterAiSchedulerBudgetState budgetState =
        new CharacterAiSchedulerBudgetState();
    private readonly CharacterAiDecisionCadencePolicy cadencePolicy =
        new CharacterAiDecisionCadencePolicy();
    private CharacterAiDecisionSchedule decisionSchedule;
    private int schedulerTickSequence;
    private int lastOverdraftTick = int.MinValue / 2;
    private float manualTime;
    private float schedulingTime;
    private long cumulativeProcessedDecisionCount;
    private long cumulativeStarvedDecisionCount;
    private long cumulativeSkippedDecisionCount;
    private long cumulativeLegacyFallbackCount;
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
    private IDungeonDebugRuleQuery debugRules;
    private double worldIndexMillisecondsThisFrame;

    public int RegisteredCharacterCount => actors.Count;
    public bool HasInjectedGameClock => gameClock != null;
    public int LastProcessedDecisionCount { get; private set; }
    public int LastBehaviorTreeTickCount { get; private set; }
    public int LastLegacyFallbackCount { get; private set; }
    public int LastPathSearchCount => budgetState.LastPathSearchCount;
    public int LastBrokerPathSearchCount => budgetState.LastBrokerPathSearchCount;
    public int LastBrokerUnboundedPathSearchCount => budgetState.LastBrokerUnboundedPathSearchCount;
    public int LastBrokerPathCacheHitCount => budgetState.LastBrokerPathCacheHitCount;
    public int LastBrokerPathBudgetDeferralCount => budgetState.LastBrokerPathBudgetDeferralCount;
    public double LastProcessingMilliseconds { get; private set; }
    public long LastAllocatedBytes { get; private set; } = -1L;
    public double CurrentFrameBudgetMilliseconds => budgetState.CurrentFrameBudgetMilliseconds;
    public double EstimatedDecisionMilliseconds => budgetState.EstimatedDecisionMilliseconds;
    public double EstimatedPathSearchMilliseconds => budgetState.EstimatedPathSearchMilliseconds;
    public double SmoothedFrameMilliseconds => budgetState.SmoothedFrameMilliseconds;
    public long CumulativeProcessedDecisionCount => cumulativeProcessedDecisionCount;
    public long CumulativeStarvedDecisionCount => cumulativeStarvedDecisionCount;
    public long CumulativeSkippedDecisionCount => cumulativeSkippedDecisionCount;
    public long CumulativeLegacyFallbackCount => cumulativeLegacyFallbackCount;
    public float LastOldestDecisionDeferralSeconds => budgetState.LastOldestDecisionDeferralSeconds;
    public float MaximumObservedDecisionDeferralSeconds => budgetState.MaximumObservedDecisionDeferralSeconds;
    public bool LastBudgetExhausted { get; private set; }
    public int LastFairnessDecisionFloor { get; private set; }
    public ExternalBehaviorTree CharacterAiExternalBehavior => characterAiExternalBehavior;
    public bool IsDrivingAi => enabled && driveCharacterUpdates;
    public int CurrentDecisionBudget => budgetState.CurrentDecisionBudget;
    public int CurrentPathSearchBudget => budgetState.GetPathSearchBudgetForFrame(
        BuildBudgetSettings(),
        actors.Count);
    private CharacterAiDecisionSchedule DecisionSchedule =>
        decisionSchedule ??= new CharacterAiDecisionSchedule(
            actorSet,
            nextDecisionTime);
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
        ICharacterAiPerformanceRecorder performanceRecorder,
        IUiClock uiClock,
        IFacilityCandidateCache facilityCandidateCache,
        IPlayerStaffCommandSource playerStaffCommands,
        IDungeonDebugRuleQuery debugRules)
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
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
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
            budgetState.ResetLastTickCounters();
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
        DecisionSchedule.Schedule(actor, CurrentSchedulingTime);
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

        return cadencePolicy.IsHighDetailCharacter(
            actor,
            RequireMainCameraProvider().Camera,
            BuildCadenceSettings());
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
            || offscreenMovementFrameStride <= 1)
        {
            return 1;
        }

        return cadencePolicy.GetMovementFrameStride(
            actor,
            schedulerEnabled: true,
            RequireMainCameraProvider().Camera,
            BuildCadenceSettings());
    }

    public double GetDecisionWorkSliceMillisecondsFor(CharacterActor actor)
    {
        return budgetState.GetDecisionWorkSliceMilliseconds(
            actor,
            BuildBudgetSettings(),
            actors.Count);
    }

    public void RunManualTick(float deltaTime)
    {
        manualTime += Mathf.Max(0f, deltaTime);
        ProcessAiBudget(manualTime);
    }

    public void ClearRegistrationsForDebug()
    {
        budgetState.Clear(BuildBudgetSettings(), actors.Count);
        manualTime = CurrentSchedulingTime;
        actors.Clear();
        actorSet.Clear();
        DecisionSchedule.Clear();
        urgentDecisionRequests.Clear();
        missingBehaviorTreeLogged.Clear();
        missingExternalBehaviorLogged.Clear();
        cumulativeProcessedDecisionCount = 0L;
        cumulativeStarvedDecisionCount = 0L;
        cumulativeSkippedDecisionCount = 0L;
        cumulativeLegacyFallbackCount = 0L;
        LastFairnessDecisionFloor = 0;
        LastBudgetExhausted = false;
    }

    public void ResetPathSearchBudgetForDebugInstance()
    {
        budgetState.ResetPathWindowForDebug(BuildBudgetSettings(), actors.Count);
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
        if (debugRules.IsEnabled(DungeonDebugCheat.PauseHumanoidAi))
        {
            LastProcessedDecisionCount = 0;
            LastBehaviorTreeTickCount = 0;
            LastLegacyFallbackCount = 0;
            budgetState.ResetLastTickCounters();
            LastAllocatedBytes = -1L;
            return;
        }

        using (ProcessAiBudgetMarker.Auto())
        {
            schedulerTickSequence++;
            CharacterAiSchedulerBudgetSettings budgetSettings = BuildBudgetSettings();
            budgetState.BeginTick(
                budgetSettings,
                actors.Count,
                uiClock,
                frameWorkBudget,
                DecisionSchedule.Count,
                LastOldestDecisionDeferralSeconds >= maximumDecisionDeferralSeconds,
                LastProcessingMilliseconds);
            long startTimestamp = Stopwatch.GetTimestamp();
            // Keep the AI-owned allocation counter independent from expensive
            // detailed stage profiling. Stress and release diagnostics need a
            // stable GC authority even when detailed samples are disabled.
            long startAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                budgetState.BeginPathBudgetWindow(
                    budgetSettings,
                    actors.Count,
                    gameClock.FrameCount,
                    enabled && driveCharacterUpdates && limitPathSearches,
                    RequirePathSearchBroker());
                worldIndexMillisecondsThisFrame = budgetState.AdvanceIncrementalWorldIndex(
                    facilityCandidateCache,
                    frameWorkBudget,
                    budgetSettings);
                LastProcessedDecisionCount = 0;
                LastBehaviorTreeTickCount = 0;
                LastLegacyFallbackCount = 0;
                LastFairnessDecisionFloor = 0;
                LastBudgetExhausted = false;

                if (actors.Count == 0)
                {
                    budgetState.UpdateBacklogTelemetry(DecisionSchedule, now);
                    budgetState.CapturePathResults(RequirePathSearchBroker());
                    return;
                }

                int decisionCountBudget = budgetState.GetDecisionBudgetForFrame(
                    budgetSettings,
                    actors.Count);
                int decisionSafetyLimit = Mathf.Clamp(
                    maxDecisionsPerFrame,
                    1,
                    4096);
                int guaranteedDecisionFloor = ResolveFairnessDecisionFloor(
                    DecisionSchedule.Count,
                    decisionSafetyLimit);
                LastFairnessDecisionFloor = guaranteedDecisionFloor;
                while (actors.Count > 0
                    && LastProcessedDecisionCount < decisionSafetyLimit
                    && DecisionSchedule.TryPeekDue(
                        now,
                        out CharacterAiScheduledDecision scheduled))
                {
                    CharacterActor actor = scheduled.Actor;
                    double elapsedBeforeDecision =
                        GetElapsedMilliseconds(startTimestamp);
                    double predictedDecisionMilliseconds =
                        budgetState.GetPredictedDecisionCost(actor, budgetSettings);
                    bool urgent = urgentDecisionRequests.Contains(actor);
                    bool starved = now - scheduled.DueTime
                        >= maximumDecisionDeferralSeconds;
                    bool withinCountBudget = LastProcessedDecisionCount
                        < Mathf.Max(decisionCountBudget, guaranteedDecisionFloor);
                    bool withinTimeBudget =
                        LastProcessedDecisionCount < guaranteedDecisionFloor
                        || elapsedBeforeDecision + predictedDecisionMilliseconds
                            <= budgetState.CurrentFrameBudgetMilliseconds;
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

                    if (!DecisionSchedule.TryTakeDue(now, out actor))
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
                    budgetState.RecordDecisionCost(
                        actor,
                        decisionMilliseconds,
                        budgetSettings);
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
                    DecisionSchedule.Schedule(actor, now + nextDelay);
                    LastProcessedDecisionCount++;
                    cumulativeProcessedDecisionCount++;
                    if (starved)
                    {
                        cumulativeStarvedDecisionCount++;
                    }
                    double elapsedMilliseconds =
                        GetElapsedMilliseconds(startTimestamp);
                    if (LastProcessedDecisionCount >= guaranteedDecisionFloor
                        && elapsedMilliseconds >= budgetState.CurrentFrameBudgetMilliseconds)
                    {
                        LastBudgetExhausted = true;
                        break;
                    }
                }

                budgetState.UpdateBacklogTelemetry(DecisionSchedule, now);
#if UNITY_EDITOR
                RefreshBehaviorDesignerVisualsForEditor();
#endif
                budgetState.CapturePathResults(RequirePathSearchBroker());
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
                budgetState.RecordPathSearchCost(
                    RequirePathSearchBroker(),
                    budgetSettings);
            }
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
    {
        return (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
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
        actors.Add(actor);
        float now = CurrentSchedulingTime;
        DecisionSchedule.Schedule(
            actor,
            now + cadencePolicy.GetRegistrationDelay(
                actor,
                mainCameraProvider != null ? mainCameraProvider.Camera : null,
                mainCameraProvider != null,
                BuildCadenceSettings()));
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

    private int ResolveFairnessDecisionFloor(
        int scheduledActorCount,
        int decisionSafetyLimit)
    {
        if (scheduledActorCount <= 0)
        {
            return 0;
        }

        float targetFrameSeconds = Mathf.Max(
            0.001f,
            targetFrameMilliseconds / 1000f);
        float observedFrameSeconds = uiClock != null
            // A slow frame must not demand proportionally more AI work in
            // that same frame. Cap the service horizon at two target frames
            // so fairness catches up gradually without a positive feedback
            // loop that turns a hitch into a decision burst.
            ? Mathf.Clamp(
                uiClock.DeltaTime,
                targetFrameSeconds,
                targetFrameSeconds * 2f)
            : targetFrameSeconds;
        int serviceFloor = Mathf.CeilToInt(
            scheduledActorCount
            * observedFrameSeconds
            / Mathf.Max(0.1f, maximumDecisionDeferralSeconds));
        return Mathf.Clamp(
            Mathf.Max(minDecisionsPerFrame, serviceFloor),
            1,
            decisionSafetyLimit);
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
            DecisionSchedule.Remove(actor);
            return;
        }

        DecisionSchedule.Remove(actor);
        budgetState.RemoveActor(actor);
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
        DecisionSchedule.Remove(actor);
        budgetState.RemoveActor(actor);
        urgentDecisionRequests.Remove(actor);
        actors.RemoveAt(index);
    }

    private float GetDecisionInterval(CharacterActor actor)
    {
        return cadencePolicy.GetNextDecisionInterval(
            actor,
            RequireMainCameraProvider().Camera,
            BuildCadenceSettings());
    }

    private float CurrentSchedulingTime =>
        uiClock != null
            ? schedulingTime
            : gameClock != null
                ? gameClock.Time
                : manualTime;

    private bool TryConsumePathSearchBudgetInternal()
    {
        return budgetState.TryConsumePathSearchBudget(
            BuildBudgetSettings(),
            actors.Count,
            gameClock.FrameCount);
    }

    private CharacterAiSchedulerBudgetSettings BuildBudgetSettings()
    {
        return new CharacterAiSchedulerBudgetSettings
        {
            AdaptBudgetsToFrameCost = adaptBudgetsToFrameCost,
            MaxDecisionsPerFrame = maxDecisionsPerFrame,
            MaxPathSearchesPerFrame = Mathf.Clamp(maxPathSearchesPerFrame, 1, 8),
            MinDecisionsPerFrame = Mathf.Max(1, minDecisionsPerFrame),
            MinPathSearchesPerFrame = Mathf.Max(1, minPathSearchesPerFrame),
            TargetAiMilliseconds = targetAiMilliseconds,
            TargetFrameMilliseconds = targetFrameMilliseconds,
            FrameHeadroomShare = frameHeadroomShare,
            BaselineBudgetRatio = baselineBudgetRatio,
            MinimumUsefulSliceMilliseconds = minimumUsefulSliceMilliseconds,
        };
    }

    private CharacterAiDecisionCadenceSettings BuildCadenceSettings()
    {
        return new CharacterAiDecisionCadenceSettings
        {
            RegistrationSpreadSeconds = registrationSpreadSeconds,
            OwnerDecisionInterval = ownerDecisionInterval,
            VisibleDecisionInterval = visibleDecisionInterval,
            OffscreenDecisionInterval = offscreenDecisionInterval,
            DecisionIntervalJitterRatio = decisionIntervalJitterRatio,
            ViewportMargin = viewportMargin,
            OffscreenMovementFrameStride = offscreenMovementFrameStride,
        };
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
