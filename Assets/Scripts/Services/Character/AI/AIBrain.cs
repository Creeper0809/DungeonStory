using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using VContainer;

public enum CharacterAiPreferredActionDisposition
{
    None = 0,
    Deferred = 1,
    Selected = 2
}

public enum CharacterAiPreferredActionFailureSource
{
    None = 0,
    DirectActionEvaluation = 1,
    JobGiverActionEvaluation = 2,
    JobGiverCandidateCommit = 3,
    BehaviorTaskActionEvaluation = 4
}

public class AIBrain : CharacterAbility
{
    private const int RuntimeTraceCapacity = 32;
    private static readonly int FailureKindCount =
        Enum.GetValues(typeof(AIActionFailureKind)).Length;
    private static readonly int BranchCount =
        Enum.GetValues(typeof(CharacterAiBranch)).Length;
    public AIAction[] availableActions;
    [ReadOnly]public AIAction bestAction;
    [SerializeField, FormerlySerializedAs("isBestActionEnd")]
    private bool decisionPending = true;
    public bool isBestActionEnd
    {
        get => decisionPending;
        set
        {
            if (decisionPending == value)
            {
                return;
            }

            // This is a legacy scheduler latch, not an action outcome.  Actual
            // executors must close their epoch through EndExpectedAction (or a
            // typed lifecycle API) so Failed/Cancelled cannot be rewritten as
            // Completed merely because an old caller requests another decision.
            decisionPending = value;
            if (value && actor != null && aiSchedulingService != null)
            {
                aiSchedulingService.RequestImmediateDecision(actor);
            }
        }
    }
    public bool isExecuted = false;
    [SerializeField] private float actionFailureCooldown = 1f;
    [SerializeField, Range(0f, 0.5f)] private float actionSwitchScoreMargin = 0.12f;
    [SerializeField, Min(0f)] private float actionTransitionCooldown = 0.75f;
    [SerializeField, Min(0f)] private float defaultActionPersistenceSeconds = 0.75f;
    [SerializeField, Range(1, 8)] private int debugCandidateLimit = 3;
    private readonly Dictionary<(BuildableObject Building, FacilityRole Role), float> facilityScoreCache =
        new Dictionary<(BuildableObject Building, FacilityRole Role), float>();
    private readonly Dictionary<CharacterAiBranch, CharacterAiWorldSignalSnapshot> worldSignalCache =
        new Dictionary<CharacterAiBranch, CharacterAiWorldSignalSnapshot>();
    private Vector2Int worldSignalCachePosition;
    private bool hasWorldSignalCachePosition;
    private bool hasCachedFacilityCrowdSensitivity;
    private float cachedFacilityCrowdSensitivity = 1f;
    private readonly List<AIAction> destinationFailedThisDecision = new List<AIAction>();
    private readonly List<AIActionDebugCandidate> lastCandidateScores = new List<AIActionDebugCandidate>();
    private IReadOnlyList<AIActionDebugCandidate> lastCandidateScoresView;
    private AIActionSet preferredNextActionSet;
    private float preferredNextActionUntil = float.NegativeInfinity;
    private WorkTypeId preferredWorkTypeId;
    private float preferredWorkTypeUntil = float.NegativeInfinity;
    private bool preferredActionDeferredPending;
    private long preferredActionDeferredCount;
    private long preferredActionDeferredFallbackSuppressionCount;
    private long preferredActionCommitCount;
    private AIActionFailure lastPreferredActionDeferredFailure;
    private AIActionFailure firstPreferredActionHardFailure;
    private CharacterAiPreferredActionFailureSource
        firstPreferredActionHardFailureSource;
    private long preferredActionHardFailureCount;
    private bool preferredActionHardFailureDecisionPending;
    private Predicate<AIActionSet> preferredActionMatcher;
    private CharacterAiPreferredActionDisposition preferredActionDisposition;
    private CharacterAiBranch preferredActionDispositionBranch;
    private long preferredActionDispositionRevision;
    private float noActionLogCooldownUntil;
    private AIAction queuedAction;
    private ICharacterAiActionAssetCatalog actionAssetCatalog;
    private ICharacterAiSchedulingService aiSchedulingService;
    private IFacilityCandidateCache facilityCandidateCache;
    private ICharacterAiFacilityLookup facilityLookup;
    private ICharacterAiJobGiverCatalog jobGiverCatalog;
    private ICharacterAiDecisionPipeline decisionPipeline;
    private IGridPathSearchBroker pathSearchBroker;
    private ICharacterAiPerformanceRecorder performanceRecorder;
    private IGameClock gameClock;
    private IRandomStream actionRandom;
    private AIBrainActionEvaluator actionEvaluator;
    private AIBrainCandidateSelector candidateSelector;
    private AIBrainPathSearchSession pathSearchSession;
    private FacilityScoringContext facilityScoringContext;
    private AIActionFailure lastActionFailure = AIActionFailure.None;
    private string lastExecutionFailureDetail = string.Empty;
    private AIActionSet lastFailedActionSet;
    private string currentActionDebugLabel = "\uB300\uAE30";
    private string currentActionPhase = string.Empty;
    private string currentActionPhaseDetail = string.Empty;
    private string currentDestinationDebugLabel = string.Empty;
    private float nextActionSwitchAllowedAt;
    private bool manualCommandActive;
    private bool externallyDrivenActionActive;
    private bool externalReplanClearFailures;
    private string externalIntentOwnerId = string.Empty;
    private CharacterActionIntentKind externalIntentKind;
    private long externalIntentEpoch;
    private int externalIntentTransitionCount;
    private int externalIntentPreemptionCount;
    private int externalIntentRejectedCount;
    private int externalIntentStaleCompletionCount;
    private int directDecisionTickCount;
    private int immediateDecisionRequestCount;
    private string lastImmediateDecisionReason = string.Empty;
    private readonly long[] executionFailuresByKind =
        new long[Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private readonly long[] candidateRejectionsByKind =
        new long[Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private readonly long[] jobGiverEvaluationRejectionsByBranchAndKind =
        new long[
            Enum.GetValues(typeof(CharacterAiBranch)).Length
            * Enum.GetValues(typeof(AIActionFailureKind)).Length];
    private long actionStartCount;
    private long actionSwitchCount;
    private long sameActionRestartCount;
    private long phaseTransitionCount;
    private long immediateReplanCount;
    private long interruptedReplanCount;
    private string lastInterruptedReplanDetail = string.Empty;
    private long executionFailureCount;
    private long noActionFailureCount;
    private long candidateRejectionCount;
    private long jobGiverEvaluationRejectionCount;
    private long duplicateExecutionSuppressionCount;
    private long interactionActionReplacementCount;
    private string lastInteractionActionReplacementDetail = string.Empty;
    private long protectedRunningActionReplanCount;
    private string lastProtectedRunningActionReplanDetail = string.Empty;
    private long orphanWorkActionRecoveryCount;
    private string lastOrphanWorkActionRecoveryDetail = string.Empty;
    private long currentRepeatedFailureCount;
    private long peakRepeatedFailureCount;
    private AIActionFailureKind repeatedFailureKind;
    private AIActionSet repeatedFailureActionSet;
    private float repeatedFailureLastAt = float.NegativeInfinity;
    private AIActionSet lastStartedActionSet;
    private CharacterAiBranch currentJobGiverRejectedBranch;
    private AIActionFailureKind currentJobGiverRejectedFailureKind;
    private string currentJobGiverRejectedReason = string.Empty;
    private int currentJobGiverEvaluationPassRevision;
    private readonly int[] currentJobGiverRejectedRevisionByBranch =
        new int[BranchCount];
    private readonly AIActionFailureKind[] currentJobGiverRejectedKindByBranch =
        new AIActionFailureKind[BranchCount];
    private readonly string[] currentJobGiverRejectedReasonByBranch =
        new string[BranchCount];
    private readonly CharacterAiRuntimeTraceEvent[] runtimeTrace =
        new CharacterAiRuntimeTraceEvent[RuntimeTraceCapacity];
    private int runtimeTraceWriteIndex;
    private int runtimeTraceCount;
    private long runtimeTraceSequence;
    private long currentActionEpoch;
    private CharacterAiActionTerminalKind lastExternalIntentTerminalKind;
    private int externalIntentTerminalCount;
    private bool actionEpochLive;
    private int currentActionDefinitionId;
    private int currentActionDestinationId;
    private CharacterAiBranch currentActionBranch;
    private CharacterAiRuntimePhase currentRuntimePhase;
    private long runtimeProgressRevision;
    private long gameplayProgressRevision;
    private long facilityQueueHeartbeatCount;
    private int facilityQueuePosition = -1;
    private long facilityQueuePositionRevision;
    private long facilityServiceHeartbeatCount;
    private long actionTerminalCount;
    private long actionCompletedCount;
    private long actionFailedCount;
    private long actionCancelledCount;
    private long pathRequestCount;
    private long pathResultCount;
    private int livePathRequestCount;
    private int currentPathRequestId;
    private int nextPathRequestId;
    private long reservationAcquireCount;
    private long reservationReleaseCount;
    private int liveReservationCount;
    private long retryScheduleCount;
    private long retryAttemptCount;
    private int currentRetryAttempt;
    private long schedulerProcessCount;
    private long schedulerOverdueCount;
    private int maximumSchedulerDelayMilliseconds;
    private long invariantAnomalyCount;
    private CharacterAiRuntimeInvariant lastInvariantAnomaly;
    private string lastInvariantAnomalyDetail = string.Empty;
    private string pendingLiveEpochReplacementDetail = string.Empty;
    private long failureLoopCount;
    private CharacterAiRuntimeInvariant activeInvariantMask;
    // Per-branch accounting is deliberately stored in fixed arrays. The hot
    // path only mutates primitive counters; audit snapshots are the only place
    // that allocates a formatted/copy representation.
    private readonly long[] branchActionStarts = new long[BranchCount];
    private readonly long[] branchActionTerminals = new long[BranchCount];
    private readonly int[] branchLiveActions = new int[BranchCount];
    private readonly long[] branchGameplayProgress = new long[BranchCount];
    private readonly long[] branchPathRequests = new long[BranchCount];
    private readonly long[] branchPathResults = new long[BranchCount];
    private readonly int[] branchLivePathRequests = new int[BranchCount];
    private readonly long[] branchReservationAcquires = new long[BranchCount];
    private readonly long[] branchReservationReleases = new long[BranchCount];
    private readonly int[] branchLiveReservations = new int[BranchCount];
    private CharacterAiBranch currentPathRequestBranch;
    private bool committedPathSearchDeferred;
    private long committedPathSearchDeferralCount;
    public bool IsPathSearchDeferred =>
        pathSearchSession?.IsDeferred == true
        || committedPathSearchDeferred;
    public bool IsCommittedPathSearchDeferred => committedPathSearchDeferred;
    public long RuntimeCommittedPathSearchDeferralCount =>
        committedPathSearchDeferralCount;
    public bool IsActionScoringPending => candidateSelector?.IsPending == true;
    public bool IsPreferredActionDeferred =>
        preferredActionDeferredPending
        && preferredNextActionSet != null
        && Now <= preferredNextActionUntil;
    public long RuntimePreferredActionDeferredCount =>
        preferredActionDeferredCount;
    public long RuntimePreferredActionDeferredFallbackSuppressionCount =>
        preferredActionDeferredFallbackSuppressionCount;
    public long RuntimePreferredActionCommitCount =>
        preferredActionCommitCount;
    public CharacterAiPreferredActionDisposition RuntimePreferredActionDisposition =>
        preferredActionDisposition;
    public CharacterAiBranch RuntimePreferredActionDispositionBranch =>
        preferredActionDispositionBranch;
    public long RuntimePreferredActionDispositionRevision =>
        preferredActionDispositionRevision;
    public AIActionFailure LastPreferredActionDeferredFailure =>
        lastPreferredActionDeferredFailure;
    public AIActionFailure FirstPreferredActionHardFailure =>
        firstPreferredActionHardFailure;
    public CharacterAiPreferredActionFailureSource
        FirstPreferredActionHardFailureSource =>
            firstPreferredActionHardFailureSource;
    public long RuntimePreferredActionHardFailureCount =>
        preferredActionHardFailureCount;
    public bool HasPreferredActionHardFailureDecisionPending =>
        preferredActionHardFailureDecisionPending;
    public long RuntimeActionEpoch => currentActionEpoch;
    public long RuntimeProgressRevision => runtimeProgressRevision;
    public long GameplayProgressRevision => gameplayProgressRevision;
    public long RuntimeSchedulerProcessCount => schedulerProcessCount;
    public long FacilityQueueHeartbeatCount => facilityQueueHeartbeatCount;
    public long FacilityQueuePositionRevision => facilityQueuePositionRevision;
    public long FacilityServiceHeartbeatCount => facilityServiceHeartbeatCount;
    public CharacterAiRuntimePhase CurrentRuntimePhase => currentRuntimePhase;
    public int RuntimeLiveReservationCount => liveReservationCount;
    public int RuntimeLivePathRequestCount => livePathRequestCount;
    public float MaximumSchedulerDelaySeconds =>
        maximumSchedulerDelayMilliseconds / 1000f;
    public void ResetSchedulerDelayTelemetryForDiagnostics() =>
        maximumSchedulerDelayMilliseconds = 0;

    internal bool IsActionScoringPendingFor(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext = true)
    {
        return candidateSelector?.IsPendingFor(
            predicate,
            hasDecisionContext) == true;
    }
    public bool IsManualCommandActive => manualCommandActive;
    public bool HasRunningAction =>
        bestAction?.HasStarted == true && !isBestActionEnd;

    public bool HasRunningWorkAction =>
        HasRunningAction
        && bestAction.actionset?.HasSemanticTag(CharacterAiActionTags.Work) == true;
    public bool IsExternallyDrivenActionActive => externallyDrivenActionActive;
    public string ExternalIntentOwnerId => externalIntentOwnerId;
    public CharacterActionIntentKind ExternalIntentKind => externalIntentKind;
    public long ExternalIntentEpoch => externalIntentEpoch;
    public int ExternalIntentTransitionCount => externalIntentTransitionCount;
    public int ExternalIntentPreemptionCount => externalIntentPreemptionCount;
    public int ExternalIntentRejectedCount => externalIntentRejectedCount;
    public int ExternalIntentStaleCompletionCount => externalIntentStaleCompletionCount;
    public CharacterAiActionTerminalKind LastExternalIntentTerminalKind =>
        lastExternalIntentTerminalKind;
    public int ExternalIntentTerminalCount => externalIntentTerminalCount;
    public long RuntimeActionStartCount => actionStartCount;
    public long RuntimeActionSwitchCount => actionSwitchCount;
    public long RuntimePhaseTransitionCount => phaseTransitionCount;
    public long RuntimeImmediateReplanCount => immediateReplanCount;
    public long RuntimeExecutionFailureCount => executionFailureCount;
    public long RuntimeCandidateRejectionCount => candidateRejectionCount;
    public long RuntimeDuplicateExecutionSuppressionCount =>
        duplicateExecutionSuppressionCount;
    public long RuntimeInteractionActionReplacementCount =>
        interactionActionReplacementCount;
    public long RuntimeProtectedRunningActionReplanCount =>
        protectedRunningActionReplanCount;
    public long RuntimeOrphanWorkActionRecoveryCount =>
        orphanWorkActionRecoveryCount;
    public long RuntimeJobGiverEvaluationRejectionCount =>
        jobGiverEvaluationRejectionCount;
    public long RuntimePeakRepeatedFailureCount => peakRepeatedFailureCount;
    public AIActionFailureKind RuntimeRepeatedFailureKind => repeatedFailureKind;
    public long RuntimeInvariantAnomalyCount => invariantAnomalyCount;
    public CharacterAiRuntimeInvariant LastInvariantAnomalyForDiagnostics =>
        lastInvariantAnomaly;
    public string LastInvariantAnomalyDetailForDiagnostics =>
        lastInvariantAnomalyDetail;
    public long RuntimeFailureLoopCount => failureLoopCount;
    public string CurrentJobGiverEvaluationRejectionSummary =>
        currentJobGiverRejectedBranch == CharacterAiBranch.None
            ? "none"
            : $"{currentJobGiverRejectedBranch}/{currentJobGiverRejectedFailureKind}: {currentJobGiverRejectedReason}";

    public string GetCurrentJobGiverEvaluationRejectionSummary(
        CharacterAiBranch branch)
    {
        int index = (int)branch;
        if (index < 0
            || index >= BranchCount
            || currentJobGiverRejectedRevisionByBranch[index]
                != currentJobGiverEvaluationPassRevision)
        {
            return "none";
        }

        return $"{branch}/{currentJobGiverRejectedKindByBranch[index]}: "
            + currentJobGiverRejectedReasonByBranch[index];
    }

    public bool IsExternalIntentCurrent(
        in CharacterActionIntentLease lease) => OwnsExternalIntent(lease);
    public AIActionFailure LastActionFailure => lastActionFailure;
    public IReadOnlyList<AIActionDebugCandidate> LastCandidateScores => lastCandidateScoresView ??= ReadOnlyView.List(lastCandidateScores);
    public string CurrentActionDebugLabel => currentActionDebugLabel;
    public string CurrentActionPhase => currentActionPhase;
    public string CurrentActionPhaseDetail => currentActionPhaseDetail;
    public string CurrentDestinationDebugLabel => currentDestinationDebugLabel;
    internal ICharacterAiPerformanceRecorder PerformanceRecorder => performanceRecorder;
    public int DebugVersion { get; private set; }
    private float Now => gameClock != null ? gameClock.Time : 0f;

    public CharacterAiDecisionTickResult RunDecisionTreeDirect()
    {
        if (actor == null || decisionPipeline == null)
        {
            return new CharacterAiDecisionTickResult(
                false,
                CharacterAiBranch.None,
                "Direct BT",
                "AI decision pipeline is unavailable.");
        }

        if (!IsActionScoringPending)
        {
            directDecisionTickCount++;
            actor.Blackboard?.BeginDecisionTrace(directDecisionTickCount);
            actor.Blackboard?.ClearJobGiverCandidateCache();
            RequireActionEvaluator().ClearEvaluations();
            facilityScoreCache.Clear();
            ClearWorldSignalCache();
            hasCachedFacilityCrowdSensitivity = false;
        }

        return decisionPipeline.RunRootDecision(actor);
    }

    public bool HasResumableDecisionPipeline =>
        actor != null && decisionPipeline != null;

    [Inject]
    public void ConstructAIBrain(
        AIBrainDecisionServices decisions,
        AIBrainExecutionServices execution)
    {
        AIBrainDecisionServices requiredDecisions = decisions
            ?? throw new ArgumentNullException(nameof(decisions));
        AIBrainExecutionServices requiredExecution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        actionAssetCatalog = requiredDecisions.ActionAssets;
        aiSchedulingService = requiredDecisions.Scheduling;
        facilityCandidateCache = requiredDecisions.FacilityCandidates;
        facilityLookup = requiredDecisions.Facilities;
        jobGiverCatalog = requiredDecisions.JobGivers;
        decisionPipeline = requiredDecisions.Decisions;
        performanceRecorder = requiredDecisions.Performance;
        pathSearchBroker = requiredExecution.PathSearch;
        gameClock = requiredExecution.Clock;
        actionRandom = requiredExecution.ActionRandom;
        facilityScoringContext = requiredExecution.FacilityScoring;
        actionEvaluator = new AIBrainActionEvaluator(gameClock, performanceRecorder);
        candidateSelector = new AIBrainCandidateSelector(
            actionEvaluator,
            aiSchedulingService,
            performanceRecorder);
        pathSearchSession = new AIBrainPathSearchSession(pathSearchBroker);
        BindActionClocks();
    }

    public float NextRandom01()
    {
        return RequireActionRandom().NextFloat();
    }

    public float NextRandom(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, NextRandom01());
    }

    public int NextRandomIndex(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "A random index requires a positive count.");
        }

        return RequireActionRandom().NextInt(0, count);
    }

    private IRandomStream RequireActionRandom()
    {
        return actionRandom
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
    }

    private void BindActionClocks()
    {
        if (availableActions == null || gameClock == null)
        {
            return;
        }

        foreach (AIAction action in availableActions)
        {
            action?.BindClock(gameClock);
        }
    }

    public override void Initializtion(CharacterSO data)
    {
        base.Initializtion(data);
        if (data != null && data.role == CharacterRole.Owner)
        {
            UseOwnerWorkActions();
        }
        else
        {
            NormalizeConfiguredActions();
            EnsureVisitorActions();
        }
        isBestActionEnd = true;
        ClearPathSearchCache();
    }

    public void UseOwnerWorkActions()
    {
        availableActions = AIBrainActionConfiguration.ConfigureOwner(
            RequireActionCatalog(),
            gameClock);
    }

    public void UseStaffWorkActions()
    {
        availableActions = AIBrainActionConfiguration.ConfigureStaff(
            availableActions,
            RequireActionCatalog(),
            gameClock);
        isBestActionEnd = true;
        ClearPathSearchCache();
    }

    private void NormalizeConfiguredActions()
    {
        availableActions = AIBrainActionConfiguration.NormalizeConfigured(
            availableActions,
            name,
            gameClock);
    }

    private void EnsureVisitorActions()
    {
        availableActions = AIBrainActionConfiguration.EnsureVisitorActions(
            availableActions,
            actor,
            RequireActionCatalog(),
            gameClock);
    }

    private ICharacterAiActionAssetCatalog RequireActionCatalog() =>
        actionAssetCatalog ?? throw MissingDependency(nameof(ICharacterAiActionAssetCatalog));

    private AIBrainActionEvaluator RequireActionEvaluator()
    {
        return actionEvaluator
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires constructed action evaluation services.");
    }

    private AIBrainCandidateSelector RequireCandidateSelector()
    {
        return candidateSelector
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires constructed candidate selection services.");
    }

    private AIBrainPathSearchSession RequirePathSearchSession()
    {
        return pathSearchSession
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires constructed path-search services.");
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService() =>
        aiSchedulingService ?? throw MissingDependency(nameof(ICharacterAiSchedulingService));

    public FacilityScoringContext RequireFacilityScoringContext()
    {
        if (!facilityScoringContext.IsConfigured)
        {
            throw MissingDependency(nameof(FacilityScoringContext));
        }

        return facilityScoringContext;
    }

    public IFacilityCandidateCache RequireFacilityCandidateCache() =>
        facilityCandidateCache ?? throw MissingDependency(nameof(IFacilityCandidateCache));

    internal bool TryGetCachedFacilityScore(
        BuildableObject building,
        FacilityRole role,
        out float score)
    {
        return facilityScoreCache.TryGetValue((building, role), out score);
    }

    internal void CacheFacilityScore(
        BuildableObject building,
        FacilityRole role,
        float score)
    {
        if (building != null)
        {
            facilityScoreCache[(building, role)] = Mathf.Clamp01(score);
        }
    }

    internal float GetFacilityCrowdSensitivity(CharacterActor sourceActor)
    {
        if (hasCachedFacilityCrowdSensitivity)
        {
            return cachedFacilityCrowdSensitivity;
        }

        cachedFacilityCrowdSensitivity = sourceActor?.Stats != null
            ? sourceActor.Stats.GetCrowdSensitivityMultiplier()
            : 1f;
        hasCachedFacilityCrowdSensitivity = true;
        return cachedFacilityCrowdSensitivity;
    }

    internal bool TryGetCachedWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        out CharacterAiWorldSignalSnapshot snapshot)
    {
        if (!hasWorldSignalCachePosition
            || worldSignalCachePosition != actorPosition)
        {
            snapshot = default;
            return false;
        }

        return worldSignalCache.TryGetValue(branch, out snapshot);
    }

    internal void CacheWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        CharacterAiWorldSignalSnapshot snapshot)
    {
        if (!hasWorldSignalCachePosition
            || worldSignalCachePosition != actorPosition)
        {
            worldSignalCache.Clear();
            worldSignalCachePosition = actorPosition;
            hasWorldSignalCachePosition = true;
        }

        worldSignalCache[branch] = snapshot;
    }

    private void ClearWorldSignalCache()
    {
        worldSignalCache.Clear();
        hasWorldSignalCachePosition = false;
    }

    public ICharacterAiFacilityLookup RequireFacilityLookup() =>
        facilityLookup ?? throw MissingDependency(nameof(ICharacterAiFacilityLookup));

    public ICharacterAiJobGiverCatalog RequireJobGiverCatalog() =>
        jobGiverCatalog ?? throw MissingDependency(nameof(ICharacterAiJobGiverCatalog));

    public ICharacterAiDecisionPipeline RequireDecisionPipeline() =>
        decisionPipeline ?? throw MissingDependency(nameof(ICharacterAiDecisionPipeline));

    private static InvalidOperationException MissingDependency(string dependencyName) =>
        new InvalidOperationException(
            $"{nameof(AIBrain)} requires {dependencyName} injection before use.");

    public bool DecideAction()
    {
        ReleaseFinishedActionReservation();

        if (actor != null && actor.CanRunAi)
        {
            EnsureVisitorActions();
        }

        if (actor == null
            || !actor.CanRunAi
            || !TryGetRuntimeGrid(out _)
            || availableActions == null
            || availableActions.Length == 0)
        {
            bestAction = null;
            isBestActionEnd = true;
            isExecuted = false;
            return false;
        }

        actionEvaluator?.ClearEvaluations();
        facilityScoreCache.Clear();
        ClearWorldSignalCache();
        hasCachedFacilityCrowdSensitivity = false;

        if (TryUsePreferredAction(out bool preferredActionDeferred))
        {
            return true;
        }

        if (preferredActionDeferred)
        {
            // The preferred action owns this decision until its bounded retry
            // or preference expiry. Falling through here lets an unrelated
            // wait/haul commit clear path backpressure and strand the still
            // live preference.
            PreservePreferredDeferredDecisionOwnership();
            return false;
        }

        if (TryConsumePreferredActionHardFailureDecision(
                out _,
                out _))
        {
            // The explicit preference already produced a typed terminal
            // selection failure. Let the scheduler own the next decision
            // instead of committing an unrelated fallback in this tick.
            return false;
        }

        if (TryUseQueuedAction())
        {
            return true;
        }

        return DecideActionByScoreThenDestination();

    }

    public bool TryCommitActionCandidate(
        CharacterAiActionCandidate candidate,
        out AIActionFailure failure)
    {
        ReleaseFinishedActionReservation();
        if (!AIBrainCandidateCommitter.TryPrepareCommit(
            actor,
            availableActions,
            RequireActionEvaluator(),
            in candidate,
            out AIAction action,
            out failure))
        {
            RememberCandidateFailure(action, failure);
            return false;
        }

        bool committingPreferred =
            action.actionset == preferredNextActionSet;
        SetSelectedAction(action, "\uC120\uD0DD");
        if (committingPreferred)
        {
            preferredActionCommitCount = checked(
                preferredActionCommitCount + 1L);
            RecordPreferredActionDisposition(
                CharacterAiPreferredActionDisposition.Selected,
                action.actionset.Branch);
            ClearPreferredAction(
                preserveWorkType: action.actionset is AIWork);
        }

        isBestActionEnd = false;
        isExecuted = false;
        lastActionFailure = AIActionFailure.None;
        lastFailedActionSet = null;
        actor.Blackboard?.Commit(action, currentActionDebugLabel);
        MarkDebugDirty();
        return true;
    }

    public bool TryFindBestScoredAction(
        Predicate<AIActionSet> predicate,
        out CharacterAiActionCandidate candidate)
    {
        return RequireCandidateSelector().TryFind(
            actor,
            availableActions,
            predicate,
            GetSelectionScore,
            facilityScoreCache.Clear,
            out candidate);
    }

    public bool TryFindBestScoredAction(
        Predicate<AIActionSet> predicate,
        in CharacterAiDecisionContext context,
        out CharacterAiActionCandidate candidate)
    {
        return RequireCandidateSelector().TryFind(
            actor,
            availableActions,
            predicate,
            in context,
            GetSelectionScore,
            facilityScoreCache.Clear,
            out candidate);
    }

    internal void InvalidateActionEvaluations(
        Predicate<AIActionSet> predicate)
    {
        if (predicate == null || availableActions == null)
        {
            return;
        }

        for (int index = 0; index < availableActions.Length; index++)
        {
            AIAction action = availableActions[index];
            if (action?.actionset != null && predicate(action.actionset))
            {
                RequireActionEvaluator().RemoveEvaluation(action);
            }
        }
    }

    private bool DecideActionByScoreThenDestination()
    {
        isBestActionEnd = false;
        isExecuted = false;
        destinationFailedThisDecision.Clear();
        lastCandidateScores.Clear();
        lastActionFailure = AIActionFailure.None;
        lastFailedActionSet = null;

        foreach (AIAction action in availableActions)
        {
            if (action == null) continue;

            if (!RequireActionEvaluator().CanConsider(actor, action, out AIActionFailure failure))
            {
                action.score = 0f;
                RememberCandidateFailure(action, failure);
            }

            RecordCandidateDebug(action, failure);
        }

        while (TryFindHighestScoredAction(out AIAction candidate))
        {
            if (candidate.SetDestinationWithFailure(actor, out AIActionFailure failure))
            {
                SetSelectedAction(candidate, "\uC120\uD0DD");
                destinationFailedThisDecision.Clear();
                return true;
            }

            destinationFailedThisDecision.Add(candidate);
            RecordActionFailure(candidate.actionset, failure);
        }

        destinationFailedThisDecision.Clear();
        bestAction = null;
        isBestActionEnd = true;
        RecordNoActionFailure();
        return false;
    }

    private bool TryFindHighestScoredAction(out AIAction bestCandidate)
    {
        bestCandidate = null;
        float highestScore = float.MinValue;
        foreach (AIAction action in availableActions)
        {
            if (action == null
                || action.score <= 0f
                || destinationFailedThisDecision.Contains(action))
            {
                continue;
            }

            float selectionScore = GetSelectionScore(action);
            if (selectionScore > highestScore)
            {
                highestScore = selectionScore;
                bestCandidate = action;
            }
        }

        return bestCandidate != null;
    }

    private float GetSelectionScore(AIAction action)
    {
        if (action == null)
        {
            return 0f;
        }

        float selectionScore = action.score;
        if (preferredNextActionSet != null)
        {
            if (Now > preferredNextActionUntil)
            {
                preferredNextActionSet = null;
                preferredNextActionUntil = float.NegativeInfinity;
            }
            else if (action.actionset == preferredNextActionSet)
            {
                selectionScore += 1f;
            }
        }

        if (actor != null
            && actor.Blackboard != null
            && actor.Blackboard.TryGetCommitmentBonus(action, out float commitmentBonus))
        {
            selectionScore += commitmentBonus;
        }

        if (bestAction != null
            && bestAction.actionset != null
            && action.actionset == bestAction.actionset)
        {
            selectionScore += actionSwitchScoreMargin;
        }
        else if (bestAction != null
            && !isBestActionEnd
            && Now < nextActionSwitchAllowedAt)
        {
            selectionScore -= actionSwitchScoreMargin;
        }

        return selectionScore;
    }

    public GridPathSearchResult GetPathSearch(CharacterActor actor)
    {
        return actor != null && TryGetRuntimeGrid(out Grid grid)
            ? RequirePathSearchSession().Get(
                grid,
                actor.GetNowXY(),
                GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)))
            : null;
    }

    public bool TryGetRuntimeGrid(out Grid resolvedGrid)
    {
        return TryGetGrid(out resolvedGrid);
    }

    public void ClearPathSearchCache()
    {
        pathSearchSession?.Clear();
        committedPathSearchDeferred = false;
    }

    public void RequestImmediateReplan(
        bool clearFailures = false,
        [CallerMemberName] string callerMember = "")
    {
        if (externallyDrivenActionActive)
        {
            externalReplanClearFailures |= clearFailures;
            return;
        }

        // A replan request is frequently used as a scheduler wake-up after world
        // state changes.  It must not silently destroy an in-flight facility,
        // meal, rest, or other multi-frame action.  Callers that truly intend to
        // interrupt must first use StopCurrentActionForReplan, which invokes the
        // action's typed cancellation path and releases its ownership cleanly.
        if (HasRunningAction)
        {
            protectedRunningActionReplanCount++;
            lastProtectedRunningActionReplanDetail =
                $"caller={callerMember ?? string.Empty}; "
                + $"action={GetActionLabel(bestAction?.actionset)}; "
                + $"phase={currentActionPhase}; "
                + $"destination={AIBrainDebugFormatter.GetDestinationLabel(bestAction?.destination)}; "
                + $"clearFailures={clearFailures}";
            InvalidateQueuedActionForNextDecision();
            ClearDecisionLocalCandidateState();
            if (clearFailures)
            {
                RequireActionEvaluator().ClearCooldowns();
                lastActionFailure = AIActionFailure.None;
                lastFailedActionSet = null;
                noActionLogCooldownUntil = 0f;
            }

            if (protectedRunningActionReplanCount <= 4
                || (protectedRunningActionReplanCount
                    & (protectedRunningActionReplanCount - 1)) == 0)
            {
                actor?.AddActivity(CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    "running-action-replan-deferred",
                    "Destructive AI replan deferred while an action owns its lifecycle: "
                    + lastProtectedRunningActionReplanDetail));
            }

            MarkDebugDirty();
            RequestImmediateDecision(
                $"Running action preserved for replan request from {callerMember}.");
            return;
        }

        immediateReplanCount++;

        // Immediate replans are scheduler wake-ups, not an ownership override.
        // A protected system command (visitor/captive/wildlife hand-off, or an
        // equivalent domain-owned traversal) must reach its own terminal
        // boundary. Explicit interrupt paths still cancel it directly.
        actor?.GetAbility<AbilityMove>()?.TryCancelForImmediateAiReplan();
        bestAction?.ReleaseReservation(actor);
        queuedAction?.ReleaseReservation(actor);
        actor?.Blackboard?.ClearCommitment(
            CharacterAiInterruptReason.ManualReplan,
            clearFailures ? "Immediate replan with cleared failures." : "Immediate replan.");
        bestAction = null;
        queuedAction = null;
        currentActionPhase = string.Empty;
        currentActionPhaseDetail = string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();

        isExecuted = false;
        isBestActionEnd = actor == null || actor.CanRunAi;
        pathSearchSession?.Clear();
        RequireCandidateSelector().Reset();
        ClearDecisionLocalCandidateState();

        if (clearFailures)
        {
            RequireActionEvaluator().ClearCooldowns();
            lastActionFailure = AIActionFailure.None;
            lastFailedActionSet = null;
            noActionLogCooldownUntil = 0f;
        }

        MarkDebugDirty();
        RequestImmediateDecision(
            clearFailures
                ? "Immediate replan with cleared failures."
                : "Immediate replan.");
    }

    private void ClearDecisionLocalCandidateState()
    {
        // JobGiver candidates and action evaluations are valid only for the
        // world/priority snapshot that produced them. A replan is an explicit
        // boundary between such snapshots, including direct work-priority and
        // target commands. Keeping either cache lets a stale CannotStart or
        // destination win over the newly authored command on the next tick.
        actor?.Blackboard?.ClearJobGiverCandidateCache();
        RequireActionEvaluator().ClearEvaluations();
        facilityScoreCache.Clear();
        ClearWorldSignalCache();
        hasCachedFacilityCrowdSensitivity = false;
    }

    /// <summary>
    /// Wakes the scheduler without discarding the current action, reservation,
    /// or movement. Threshold-driven needs use this path so the decision
    /// pipeline can decide whether the action is interruptible instead of a
    /// stat mutation cancelling work by itself.
    /// </summary>
    public void RequestImmediateDecision(string reason)
    {
        immediateDecisionRequestCount++;
        lastImmediateDecisionReason = reason?.Trim() ?? string.Empty;
        MarkDebugDirty();
        RequireAiSchedulingService().RequestImmediateDecision(actor);
    }

    public int ImmediateDecisionRequestCount => immediateDecisionRequestCount;
    public string LastImmediateDecisionReason => lastImmediateDecisionReason;

    public void RequestImmediateReplanForAction<TActionSet>(bool clearFailures = false)
        where TActionSet : AIActionSet
    {
        PreferActionOnNextDecision<TActionSet>();
        RequestImmediateReplan(clearFailures);
    }

    public bool PreferActionOnNextDecision<TActionSet>(float persistenceSeconds = 90f)
        where TActionSet : AIActionSet
    {
        return SetPreferredAction(
            availableActions?
            .Select(action => action?.actionset)
            .OfType<TActionSet>()
            .FirstOrDefault(),
            persistenceSeconds);
    }

    public bool IsActionPreferredForNextDecision<TActionSet>()
        where TActionSet : AIActionSet
    {
        return preferredNextActionSet is TActionSet
            && Now <= preferredNextActionUntil;
    }

    public bool IsBranchPreferredForNextDecision(
        CharacterAiBranch branch)
    {
        return branch != CharacterAiBranch.None
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && preferredNextActionSet.Branch == branch;
    }

    internal bool IsRoutineGroupPreferredForNextDecision(
        CharacterAiBranch routineGroup)
    {
        CharacterAiBranch preferredBranch = preferredNextActionSet != null
            && Now <= preferredNextActionUntil
                ? preferredNextActionSet.Branch
                : CharacterAiBranch.None;
        return routineGroup switch
        {
            CharacterAiBranch.DutyWork =>
                preferredBranch == CharacterAiBranch.Work,
            CharacterAiBranch.SurvivalNeeds =>
                preferredBranch == CharacterAiBranch.ExitDungeon
                || preferredBranch == CharacterAiBranch.Eat
                || preferredBranch == CharacterAiBranch.Drink
                || preferredBranch == CharacterAiBranch.Rest
                || preferredBranch == CharacterAiBranch.Toilet
                || preferredBranch == CharacterAiBranch.Hygiene,
            CharacterAiBranch.LeisureVisit =>
                preferredBranch == CharacterAiBranch.LeisureVisit
                || preferredBranch == CharacterAiBranch.Shopping
                || preferredBranch == CharacterAiBranch.LookAround,
            CharacterAiBranch.Idle =>
                preferredBranch == CharacterAiBranch.Wait,
            _ => false
        };
    }

    internal bool HasPreferredActionMatching(
        Predicate<AIActionSet> actionGroupMatcher)
    {
        return actionGroupMatcher != null
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && actionGroupMatcher(preferredNextActionSet);
    }

    internal Predicate<AIActionSet> GetPreferredActionMatcher()
    {
        return preferredActionMatcher ??= MatchesPreferredActionSet;
    }

    private bool MatchesPreferredActionSet(AIActionSet actionSet)
    {
        return actionSet != null
            && preferredNextActionSet != null
            && Now <= preferredNextActionUntil
            && ReferenceEquals(actionSet, preferredNextActionSet);
    }

    internal bool TryRetainPreferredBranchDeferred(
        CharacterAiBranch branch,
        AIActionFailure failure)
    {
        if (!IsBranchPreferredForNextDecision(branch))
        {
            return false;
        }

        if (!failure.IsDeferred)
        {
            return false;
        }

        preferredActionDeferredPending = true;
        preferredActionDeferredCount = checked(
            preferredActionDeferredCount + 1L);
        lastPreferredActionDeferredFailure = failure;
        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.Deferred,
            branch);
        return true;
    }

    internal bool RetirePreferredBranchAfterHardFailure(
        CharacterAiBranch branch,
        AIActionFailure failure,
        CharacterAiPreferredActionFailureSource source)
    {
        if (!failure.HasFailure
            || failure.IsDeferred
            || !IsBranchPreferredForNextDecision(branch))
        {
            return false;
        }

        preferredActionHardFailureCount = checked(
            preferredActionHardFailureCount + 1L);
        if (!firstPreferredActionHardFailure.HasFailure)
        {
            firstPreferredActionHardFailure = failure;
            firstPreferredActionHardFailureSource = source;
        }
        preferredActionHardFailureDecisionPending = true;

        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.None,
            branch);
        ClearPreferredAction();
        return true;
    }

    internal bool TryConsumePreferredActionHardFailureDecision(
        out AIActionFailure failure,
        out CharacterAiPreferredActionFailureSource source)
    {
        failure = firstPreferredActionHardFailure;
        source = firstPreferredActionHardFailureSource;
        if (!preferredActionHardFailureDecisionPending)
        {
            return false;
        }

        preferredActionHardFailureDecisionPending = false;
        return true;
    }

    private void RecordPreferredActionDisposition(
        CharacterAiPreferredActionDisposition disposition,
        CharacterAiBranch branch)
    {
        preferredActionDisposition = disposition;
        preferredActionDispositionBranch = branch;
        preferredActionDispositionRevision = checked(
            preferredActionDispositionRevision + 1L);
    }

    internal void PreservePreferredDeferredDecisionOwnership()
    {
        if (!IsPreferredActionDeferred)
        {
            return;
        }

        bestAction = null;
        isBestActionEnd = true;
        isExecuted = false;
        preferredActionDeferredFallbackSuppressionCount = checked(
            preferredActionDeferredFallbackSuppressionCount + 1L);
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.DeferredRetry,
            failureKind: lastPreferredActionDeferredFailure.Kind,
            pathState: lastPreferredActionDeferredFailure.Kind
                == AIActionFailureKind.PathSearchDeferred
                ? CharacterAiPathTraceState.Deferred
                : CharacterAiPathTraceState.None);
    }

    public bool PreferWorkActionOnNextDecision(
        WorkTypeId workTypeId,
        float persistenceSeconds = 90f)
    {
        AIWork workAction = availableActions?
            .Select(action => action?.actionset)
            .OfType<AIWork>()
            .FirstOrDefault(action => action.WorkTypeId == workTypeId)
            ?? availableActions?
                .Select(action => action?.actionset)
                .OfType<AIWork>()
                .FirstOrDefault(action => !action.WorkTypeId.IsValid);
        bool preferred = SetPreferredAction(workAction, persistenceSeconds);
        if (preferred)
        {
            preferredWorkTypeId = workTypeId;
            preferredWorkTypeUntil = Now + Mathf.Max(2f, persistenceSeconds);
        }

        return preferred;
    }

    public bool TryGetPreferredWorkType(out WorkTypeId workTypeId)
    {
        workTypeId = preferredWorkTypeId;
        if (!workTypeId.IsValid)
        {
            return false;
        }

        if (Now <= preferredWorkTypeUntil)
        {
            return true;
        }

        ClearPreferredWorkType();
        workTypeId = default;
        return false;
    }

    public bool HasPreferredWorkType =>
        preferredWorkTypeId.IsValid && Now <= preferredWorkTypeUntil;

    public void ConsumePreferredWorkType(WorkTypeId workTypeId)
    {
        if (preferredWorkTypeId == workTypeId)
        {
            ClearPreferredWorkType();
        }
    }

    private bool SetPreferredAction(
        AIActionSet actionSet,
        float persistenceSeconds)
    {
        ClearPreferredWorkType();
        preferredActionDeferredPending = false;
        lastPreferredActionDeferredFailure = AIActionFailure.None;
        firstPreferredActionHardFailure = AIActionFailure.None;
        firstPreferredActionHardFailureSource =
            CharacterAiPreferredActionFailureSource.None;
        preferredActionHardFailureDecisionPending = false;
        preferredNextActionSet = actionSet;
        preferredNextActionUntil = preferredNextActionSet != null
            ? Now + Mathf.Max(2f, persistenceSeconds)
            : float.NegativeInfinity;
        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.None,
            preferredNextActionSet?.Branch ?? CharacterAiBranch.None);
        InvalidateQueuedActionForNextDecision();
        return preferredNextActionSet != null;
    }

    public void InvalidateQueuedActionForNextDecision()
    {
        queuedAction?.ReleaseReservation(actor);
        queuedAction = null;
        ClearPathSearchCache();
        MarkDebugDirty();
    }

    public void BeginManualMoveCommand(Vector2Int destination)
    {
        // A direct player order is the top-level command authority.  Retire the
        // currently owned autonomous survival intent before entering manual mode;
        // its coroutine may still unwind, but its lease epoch can no longer mutate
        // this command.
        if (externallyDrivenActionActive)
        {
            EndOwnedExternalIntent(
                clearFailures: true,
                CharacterAiActionTerminalKind.Cancelled);
        }

        if (bestAction != null || queuedAction != null)
        {
            StopCurrentActionForReplan("플레이어 이동 명령");
        }

        manualCommandActive = true;
        isBestActionEnd = false;
        isExecuted = true;
        currentActionDebugLabel = "직접 이동";
        currentActionPhase = "이동 중";
        currentActionPhaseDetail = $"목표 ({destination.x}, {destination.y})";
        currentDestinationDebugLabel = currentActionPhaseDetail;
        actor?.Blackboard?.ForceClearCommitment(
            CharacterAiInterruptReason.ManualReplan,
            "플레이어 직접 이동");
        MarkDebugDirty();
    }

    public void CompleteManualMoveCommand(Vector2Int destination, bool succeeded)
    {
        manualCommandActive = false;
        isBestActionEnd = true;
        isExecuted = false;
        currentActionDebugLabel = succeeded ? "직접 이동 완료" : "직접 이동 실패";
        currentActionPhase = succeeded ? "도착" : "경로 막힘";
        currentActionPhaseDetail = $"목표 ({destination.x}, {destination.y})";
        currentDestinationDebugLabel = string.Empty;
        ClearPathSearchCache();
        MarkDebugDirty();
    }

    public void ClearSelectedActionForIdle(string idleLabel)
    {
        bestAction?.ReleaseReservation(actor);
        queuedAction?.ReleaseReservation(actor);
        bestAction = null;
        queuedAction = null;
        currentActionPhase = string.Empty;
        currentActionPhaseDetail = string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        currentActionDebugLabel = string.IsNullOrWhiteSpace(idleLabel) ? "\uB300\uAE30" : idleLabel;
        isExecuted = false;
        isBestActionEnd = false;
        ClearPathSearchCache();
        candidateSelector?.Reset();
        MarkDebugDirty();
    }

    public bool TryBeginExternallyDrivenAction(
        string ownerId,
        CharacterActionIntentKind kind,
        string actionLabel,
        string phase,
        string detail,
        out CharacterActionIntentLease lease)
    {
        lease = default;
        if (manualCommandActive)
        {
            externalIntentRejectedCount++;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ownerId)
            || kind == CharacterActionIntentKind.None)
        {
            externalIntentRejectedCount++;
            return false;
        }

        bool preempting = false;
        if (externallyDrivenActionActive)
        {
            if (string.Equals(
                    externalIntentOwnerId,
                    ownerId,
                    StringComparison.Ordinal))
            {
                UpdateExternalPresentation(actionLabel, phase, detail);
                lease = new CharacterActionIntentLease(
                    externalIntentOwnerId,
                    externalIntentKind,
                    externalIntentEpoch);
                return true;
            }

            if (kind <= externalIntentKind)
            {
                externalIntentRejectedCount++;
                return false;
            }

            preempting = true;
        }

        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        bestAction?.actionset?.OnStop(
            actor,
            bestAction,
            "외부 구동 행동으로 전환");
        bestAction?.ReleaseReservation(actor);
        queuedAction?.ReleaseReservation(actor);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        bestAction = null;
        queuedAction = null;
        destinationFailedThisDecision.Clear();
        currentActionDebugLabel = string.IsNullOrWhiteSpace(actionLabel) ? "특수 행동" : actionLabel;
        currentActionPhase = phase ?? string.Empty;
        currentActionPhaseDetail = detail ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        externallyDrivenActionActive = true;
        externalIntentOwnerId = ownerId;
        externalIntentKind = kind;
        externalIntentEpoch = checked(externalIntentEpoch + 1L);
        currentRuntimePhase = ClassifyRuntimePhase(currentActionPhase);
        phaseTransitionCount++;
        AdvanceGameplayProgress();
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.PhaseChanged);
        externalIntentTransitionCount++;
        if (preempting)
        {
            externalIntentPreemptionCount++;
            lastExternalIntentTerminalKind =
                CharacterAiActionTerminalKind.Cancelled;
            externalIntentTerminalCount++;
        }
        else
        {
            lastExternalIntentTerminalKind =
                CharacterAiActionTerminalKind.None;
        }
        externalReplanClearFailures = false;
        isExecuted = true;
        isBestActionEnd = false;
        ClearPathSearchCache();
        RequireCandidateSelector().Reset();
        ClearPathSearchCache();
        MarkDebugDirty();
        lease = new CharacterActionIntentLease(
            externalIntentOwnerId,
            externalIntentKind,
            externalIntentEpoch);
        return true;
    }

    public bool UpdateExternallyDrivenAction(
        in CharacterActionIntentLease lease,
        string actionLabel,
        string phase,
        string detail = null)
    {
        if (!OwnsExternalIntent(lease))
        {
            externalIntentRejectedCount++;
            return false;
        }

        UpdateExternalPresentation(actionLabel, phase, detail);
        return true;
    }

    private void UpdateExternalPresentation(
        string actionLabel,
        string phase,
        string detail)
    {
        string nextPhase = phase ?? string.Empty;
        bool transitioned = !string.Equals(
            currentActionPhase,
            nextPhase,
            StringComparison.Ordinal);
        currentActionDebugLabel = string.IsNullOrWhiteSpace(actionLabel)
            ? "외부 행동"
            : actionLabel;
        currentActionPhase = nextPhase;
        currentActionPhaseDetail = detail ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        currentRuntimePhase = ClassifyRuntimePhase(nextPhase);
        if (transitioned)
        {
            phaseTransitionCount++;
            AdvanceGameplayProgress();
            RecordRuntimeTrace(CharacterAiRuntimeTraceKind.PhaseChanged);
        }
        isExecuted = true;
        isBestActionEnd = false;
        MarkDebugDirty();
    }

    public bool EndExternallyDrivenAction(
        in CharacterActionIntentLease lease,
        bool clearFailures = true)
    {
        if (!OwnsExternalIntent(lease))
        {
            externalIntentStaleCompletionCount++;
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures,
            CharacterAiActionTerminalKind.Completed);
    }

    public bool FailExternallyDrivenAction(
        in CharacterActionIntentLease lease)
    {
        if (!OwnsExternalIntent(lease))
        {
            externalIntentStaleCompletionCount++;
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures: false,
            CharacterAiActionTerminalKind.Failed);
    }

    public bool CancelExternallyDrivenAction(
        in CharacterActionIntentLease lease)
    {
        if (!OwnsExternalIntent(lease))
        {
            externalIntentStaleCompletionCount++;
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures: false,
            CharacterAiActionTerminalKind.Cancelled);
    }

    public bool EndExternallyDrivenAction(
        string ownerId,
        bool clearFailures = true)
    {
        if (!externallyDrivenActionActive
            || !string.Equals(
                externalIntentOwnerId,
                ownerId,
                StringComparison.Ordinal))
        {
            externalIntentStaleCompletionCount++;
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures,
            CharacterAiActionTerminalKind.Completed);
    }

    private bool EndOwnedExternalIntent(
        bool clearFailures,
        CharacterAiActionTerminalKind terminalKind =
            CharacterAiActionTerminalKind.Completed)
    {
        bool shouldClearFailures =
            clearFailures || externalReplanClearFailures;
        externallyDrivenActionActive = false;
        externalIntentOwnerId = string.Empty;
        externalIntentKind = CharacterActionIntentKind.None;
        externalReplanClearFailures = false;
        lastExternalIntentTerminalKind = terminalKind ==
            CharacterAiActionTerminalKind.None
                ? CharacterAiActionTerminalKind.Completed
                : terminalKind;
        externalIntentTerminalCount++;
        RequestImmediateReplan(shouldClearFailures);
        return true;
    }

    private bool OwnsExternalIntent(in CharacterActionIntentLease lease)
    {
        return externallyDrivenActionActive
            && lease.IsValid
            && lease.Epoch == externalIntentEpoch
            && lease.Kind == externalIntentKind
            && string.Equals(
                lease.OwnerId,
                externalIntentOwnerId,
                StringComparison.Ordinal);
    }

    public void NotifyActionStarted()
    {
        if (bestAction == null || bestAction.actionset == null)
        {
            return;
        }

        if (actionEpochLive)
        {
            lastInvariantAnomalyDetail = string.IsNullOrWhiteSpace(
                    pendingLiveEpochReplacementDetail)
                ? $"start-over-live-epoch; epoch={currentActionEpoch}; "
                  + $"branch={currentActionBranch}; action={currentActionDebugLabel}; "
                  + $"selected={GetActionLabel(bestAction.actionset)}; "
                  + $"selectedStarted={bestAction.HasStarted}; ended={isBestActionEnd}; "
                  + $"executed={isExecuted}; phase={currentActionPhase}; "
                  + $"lastInterrupted={lastInterruptedReplanDetail}"
                : pendingLiveEpochReplacementDetail
                  + $"; startSelected={GetActionLabel(bestAction.actionset)}"
                  + $"; selectedStarted={bestAction.HasStarted}"
                  + $"; ended={isBestActionEnd}; executed={isExecuted}";
            RecordInvariantAnomaly(CharacterAiRuntimeInvariant.RunningWithoutEpoch);
            NotifyActionTerminal(CharacterAiActionTerminalKind.Recovered);
        }

        bestAction.BindClock(gameClock);
        bestAction.MarkStarted(Now);
        isExecuted = true;
        actionStartCount++;
        if (lastStartedActionSet == bestAction.actionset)
        {
            sameActionRestartCount++;
        }
        else if (lastStartedActionSet != null)
        {
            actionSwitchCount++;
        }
        lastStartedActionSet = bestAction.actionset;
        currentActionDebugLabel = GetActionLabel(bestAction.actionset);
        currentDestinationDebugLabel = AIBrainDebugFormatter.GetDestinationLabel(bestAction.destination);
        currentActionPhase = "\uC2DC\uC791";
        currentActionPhaseDetail = string.Empty;
        int startingActionId = bestAction.actionset.GetInstanceID();
        currentActionEpoch = checked(currentActionEpoch + 1L);
        actionEpochLive = true;
        currentActionDefinitionId = startingActionId;
        currentActionDestinationId = bestAction.destination != null
            ? bestAction.destination.GetInstanceID()
            : 0;
        currentActionBranch = bestAction.actionset.Branch;
        pendingLiveEpochReplacementDetail = string.Empty;
        int branchIndex = GetBranchIndex(currentActionBranch);
        branchActionStarts[branchIndex]++;
        branchLiveActions[branchIndex]++;
        currentRuntimePhase = CharacterAiRuntimePhase.Starting;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.ActionStarted);
        nextActionSwitchAllowedAt = Now + GetMinimumPersistenceSeconds(bestAction.actionset);
        actor?.AiMemory?.RecordDecision(
            bestAction.actionset.Branch,
            CharacterAiUtilityText.GetIntention(bestAction.actionset.Branch),
            $"{GetActionLabel(bestAction.actionset)} 시작",
            0.05f);
        if (bestAction.destination != null)
        {
            actor?.AiMemory?.RecordFacility(
                bestAction.destination,
                bestAction.actionset.Branch,
                $"{AIBrainDebugFormatter.GetDestinationLabel(bestAction.destination)} 선택",
                0.1f);
        }
        MarkDebugDirty();
    }

    /// <summary>
    /// Closes the currently-live action epoch exactly once. The legacy
    /// isBestActionEnd property is only a scheduler latch; executors must call
    /// this typed terminal path before requesting another decision.
    /// </summary>
    public void NotifyActionTerminal(CharacterAiActionTerminalKind terminalKind)
    {
        if (!actionEpochLive)
        {
            return;
        }

        actionEpochLive = false;
        actionTerminalCount++;
        int branchIndex = GetBranchIndex(currentActionBranch);
        branchActionTerminals[branchIndex]++;
        if (branchLiveActions[branchIndex] > 0)
        {
            branchLiveActions[branchIndex]--;
        }
        else
        {
            RecordInvariantAnomaly(
                CharacterAiRuntimeInvariant.LiveEpochWithoutAction);
        }
        switch (terminalKind)
        {
            case CharacterAiActionTerminalKind.Failed:
                actionFailedCount++;
                break;
            case CharacterAiActionTerminalKind.Cancelled:
            case CharacterAiActionTerminalKind.Recovered:
                actionCancelledCount++;
                break;
            default:
                actionCompletedCount++;
                terminalKind = CharacterAiActionTerminalKind.Completed;
                break;
        }

        currentRuntimePhase = CharacterAiRuntimePhase.Terminal;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.ActionTerminal,
            terminalKind: terminalKind);
    }

    public bool EndExpectedAction(
        AIAction expectedAction,
        CharacterAiActionTerminalKind terminalKind,
        bool clearFailures)
    {
        if (expectedAction != null
            && !ReferenceEquals(bestAction, expectedAction))
        {
            return false;
        }

        AIAction completedAction = expectedAction ?? bestAction;
        NotifyActionTerminal(terminalKind);
        if (clearFailures
            && terminalKind == CharacterAiActionTerminalKind.Completed)
        {
            ClearFailureForCompletedAction(completedAction);
        }
        // Do not route failure/cancellation through the compatibility setter:
        // it represents legacy successful completion and would rewrite the
        // terminal kind to Completed before the next decision.
        decisionPending = true;
        RequestImmediateReplan(clearFailures: false);
        return true;
    }

    private void ClearFailureForCompletedAction(AIAction completedAction)
    {
        AIActionSet actionSet = completedAction?.actionset;
        if (actionSet == null)
        {
            return;
        }

        BuildableObject destination = completedAction.destination;
        RequireActionEvaluator().ClearCooldown(actionSet, destination);
        if (lastFailedActionSet != actionSet)
        {
            return;
        }

        BuildableObject failedDestination = lastActionFailure.Target;
        bool sameFailureScope = ReferenceEquals(failedDestination, destination)
            || (ReferenceEquals(failedDestination, null)
                && ReferenceEquals(destination, null))
            || (!ReferenceEquals(failedDestination, null)
                && !ReferenceEquals(destination, null)
                && failedDestination.GetInstanceID() == destination.GetInstanceID());
        if (!sameFailureScope)
        {
            return;
        }

        lastActionFailure = AIActionFailure.None;
        lastFailedActionSet = null;
        noActionLogCooldownUntil = 0f;
    }

    internal void NotifyDuplicateExecutionSuppressed(AIAction action)
    {
        duplicateExecutionSuppressionCount++;
        string actionLabel = GetActionLabel(action?.actionset);
        currentActionPhaseDetail =
            $"duplicate execution suppressed: {actionLabel}; count={duplicateExecutionSuppressionCount}";

        // Keep normal frames allocation-free and avoid flooding the activity log
        // if a broken behavior-tree leaf asks to execute the same RUNNING action
        // every tick. The counter remains exact; evidence is emitted for the first
        // four occurrences and then at powers of two.
        if (duplicateExecutionSuppressionCount <= 4
            || (duplicateExecutionSuppressionCount
                & (duplicateExecutionSuppressionCount - 1)) == 0)
        {
            actor?.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Changed,
                "duplicate-execution-suppressed",
                $"AI duplicate execution suppressed: action={actionLabel}; "
                + $"count={duplicateExecutionSuppressionCount}"));
        }

        MarkDebugDirty();
    }

    internal bool RecoverOrphanedWorkAction(int workRunId, string reason)
    {
        if (!HasRunningWorkAction || !isExecuted)
        {
            return false;
        }

        orphanWorkActionRecoveryCount++;
        lastOrphanWorkActionRecoveryDetail =
            $"run={workRunId}; reason={reason ?? string.Empty}; "
            + $"action={GetActionLabel(bestAction?.actionset)}; "
            + $"phase={currentActionPhase}; detail={currentActionPhaseDetail}; "
            + $"destination={currentDestinationDebugLabel}";
        bestAction?.ReleaseReservation(actor);
        NotifyActionTerminal(CharacterAiActionTerminalKind.Recovered);
        isBestActionEnd = true;
        isExecuted = false;
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.OrphanRecovery);
        actor?.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Failed,
            "orphan-work-action-recovered",
            $"AI work lifecycle recovered: {lastOrphanWorkActionRecoveryDetail}"));
        MarkDebugDirty();
        RequestImmediateDecision(
            "Orphaned Work action ended after coroutine ownership was released.");
        return true;
    }

    internal bool DeferExpectedActionWithoutImmediateDecision(
        AIAction expectedAction,
        string reason)
    {
        if (expectedAction == null
            || !ReferenceEquals(bestAction, expectedAction))
        {
            return false;
        }

        expectedAction.ReleaseReservation(actor);
        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor?.Blackboard?.ClearCommitment(
            CharacterAiInterruptReason.ManualReplan,
            reason ?? "Deferred action retry.");
        bestAction = null;
        currentActionDebugLabel = "Deferred retry";
        currentActionPhase = "Deferred";
        currentActionPhaseDetail = reason ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();
        isExecuted = false;
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.DeferredRetry);

        // The executor owns the retry timer and will wake the scheduler at the
        // authored retry tick. Setting isBestActionEnd here would immediately
        // schedule the same action again and turn a bounded retry into a hot
        // decision loop.
        decisionPending = false;
        ClearPathSearchCache();
        RequireCandidateSelector().Reset();
        MarkDebugDirty();
        return true;
    }

    internal void FailExpectedActionExecution(
        AIAction expectedAction,
        AIActionFailure failure,
        Exception exception)
    {
        if (expectedAction == null
            || !ReferenceEquals(bestAction, expectedAction))
        {
            return;
        }

        ReportRuntimeActionFailure(failure, requestImmediateReplan: false);
        NotifyActionTerminal(CharacterAiActionTerminalKind.Failed);
        try
        {
            expectedAction.actionset?.OnStop(
                actor,
                expectedAction,
                failure.Reason);
        }
        catch (Exception stopException)
        {
            Debug.LogException(stopException, actor);
        }

        expectedAction.ReleaseReservation(actor);
        queuedAction?.ReleaseReservation(actor);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor?.Blackboard?.ClearCommitment(
            CharacterAiInterruptReason.ManualReplan,
            failure.Reason);
        if (ReferenceEquals(bestAction, expectedAction))
        {
            bestAction = null;
        }
        queuedAction = null;
        currentActionPhase = "Execution failed";
        currentActionPhaseDetail = exception != null
            ? exception.GetType().Name + ": " + exception.Message
            : failure.Reason;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();
        isExecuted = false;
        isBestActionEnd = true;
        ClearPathSearchCache();
        RequireCandidateSelector().Reset();
        MarkDebugDirty();
    }

    public void StopAllAiForLifecycleTransition(string reason)
    {
        if (externallyDrivenActionActive)
        {
            // External actions own a terminal epoch independently from the
            // selected-action epoch. A lifecycle boundary must retire both;
            // clearing the fields directly leaves the prior terminal kind and
            // count behind, which makes ownership conservation unverifiable.
            EndOwnedExternalIntent(
                clearFailures: false,
                CharacterAiActionTerminalKind.Cancelled);
        }
        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        AIAction actionToStop = bestAction;
        try
        {
            actionToStop?.actionset?.OnStop(
                actor,
                actionToStop,
                reason ?? "lifecycle-transition");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, actor);
        }

        actionToStop?.ReleaseReservation(actor);
        queuedAction?.ReleaseReservation(actor);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor?.Blackboard?.ForceClearCommitment(
            CharacterAiInterruptReason.ManualReplan,
            reason ?? "lifecycle-transition");
        bestAction = null;
        queuedAction = null;
        manualCommandActive = false;
        externallyDrivenActionActive = false;
        externalIntentOwnerId = string.Empty;
        externalIntentKind = CharacterActionIntentKind.None;
        currentActionDebugLabel = "Inactive";
        currentActionPhase = "Lifecycle transition";
        currentActionPhaseDetail = reason ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();
        isExecuted = false;
        decisionPending = false;
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.LifecycleCleanup);
        pathSearchSession?.Clear();
        candidateSelector?.Reset();
        MarkDebugDirty();
    }

    internal void NotifyInteractionActionReplaced(
        string detail,
        BuildableObject facility)
    {
        interactionActionReplacementCount++;
        lastInteractionActionReplacementDetail =
            $"detail={detail ?? string.Empty}; "
            + $"replacementAction={GetActionLabel(bestAction?.actionset)}; "
            + $"replacementDestination={AIBrainDebugFormatter.GetDestinationLabel(bestAction?.destination)}; "
            + $"facility={AIBrainDebugFormatter.GetDestinationLabel(facility)}";

        if (interactionActionReplacementCount <= 4
            || (interactionActionReplacementCount
                & (interactionActionReplacementCount - 1)) == 0)
        {
            actor?.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                "interaction-action-replaced",
                "AI interaction ownership was replaced: "
                + lastInteractionActionReplacementDetail));
        }

        MarkDebugDirty();
    }

    public void SetActionPhase(string phase, BuildableObject destination = null, string detail = null)
    {
        string nextPhase = phase ?? string.Empty;
        string nextDestination = destination != null
            ? AIBrainDebugFormatter.GetDestinationLabel(destination)
            : currentDestinationDebugLabel;
        bool transitioned =
            !string.Equals(currentActionPhase, nextPhase, StringComparison.Ordinal)
            || !string.Equals(
                currentDestinationDebugLabel,
                nextDestination,
                StringComparison.Ordinal);
        if (transitioned)
        {
            phaseTransitionCount++;
        }
        currentActionPhase = nextPhase;
        currentActionPhaseDetail = detail ?? string.Empty;
        currentRuntimePhase = ClassifyRuntimePhase(nextPhase);
        if (transitioned)
        {
            AdvanceGameplayProgress();
        }
        if (destination != null)
        {
            currentDestinationDebugLabel = nextDestination;
        }

        if (transitioned)
        {
            RecordRuntimeTrace(CharacterAiRuntimeTraceKind.PhaseChanged);
        }

        MarkDebugDirty();
    }

    private static CharacterAiRuntimePhase ClassifyRuntimePhase(string phase)
    {
        if (string.IsNullOrEmpty(phase)) return CharacterAiRuntimePhase.None;
        if (phase == "선택") return CharacterAiRuntimePhase.Selected;
        if (phase == "시작") return CharacterAiRuntimePhase.Starting;
        if (phase == "이동" || phase.Contains("이동", StringComparison.Ordinal)
            || phase.Contains("접근", StringComparison.Ordinal)
            || phase.Contains("추적", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Moving;
        if (phase.Contains("재탐색", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Repathing;
        if (phase == "도착") return CharacterAiRuntimePhase.Arrived;
        if (phase.Contains("접수", StringComparison.Ordinal)
            || phase.Contains("자리 잡기", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityAdmission;
        if (phase.Contains("대기", StringComparison.Ordinal)
            || phase.Contains("예약", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityQueue;
        if (phase.Contains("이용", StringComparison.Ordinal)
            || phase.Contains("식사 중", StringComparison.Ordinal)
            || phase.Contains("계산 중", StringComparison.Ordinal)
            || phase.Contains("서비스", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.FacilityService;
        if (phase.Contains("작업", StringComparison.Ordinal)
            || phase.Contains("공사", StringComparison.Ordinal)
            || phase.Contains("수리", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Working;
        if (phase.Contains("기다", StringComparison.Ordinal)
            || phase.Contains("둘러보기", StringComparison.Ordinal))
            return CharacterAiRuntimePhase.Waiting;
        return CharacterAiRuntimePhase.Unknown;
    }

    public bool ShouldStopCurrentAction(out string stopReason)
    {
        if (ShouldStopCurrentActionForReplan(out stopReason))
        {
            return true;
        }

        if (bestAction == null || bestAction.actionset == null)
        {
            return false;
        }

        if (TryFindInterruptAction(bestAction, out AIAction interruptAction, out stopReason))
        {
            queuedAction = interruptAction;
            return true;
        }

        return false;
    }

    public bool ShouldStopCurrentActionForReplan(out string stopReason)
    {
        return AIBrainActionContinuationPolicy.ShouldStopForReplan(
            actor,
            bestAction,
            GetMinimumPersistenceSeconds(bestAction?.actionset),
            out stopReason);
    }

    public bool CanInterruptCurrentActionForSurvivalEmergency(out string interruptReason)
    {
        return AIBrainActionContinuationPolicy.CanInterruptForSurvival(
            actor,
            bestAction,
            GetMinimumPersistenceSeconds(bestAction?.actionset),
            out interruptReason);
    }

    public bool CanContinueCurrentAction(out string status)
    {
        return AIBrainActionContinuationPolicy.CanContinue(
            actor,
            bestAction,
            GetMinimumPersistenceSeconds(bestAction?.actionset),
            GetActionLabel,
            out status);
    }

    public bool StopCurrentActionForReplan(
        string reason,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        if (externallyDrivenActionActive)
        {
            return false;
        }

        AIAction actionToStop = bestAction;
        AIAction queuedActionToClear = queuedAction;
        if (actionToStop == null && queuedActionToClear == null)
        {
            return false;
        }

        interruptedReplanCount++;
        lastInterruptedReplanDetail =
            $"caller={caller}; reason={reason ?? string.Empty}; "
            + $"action={GetActionLabel(actionToStop?.actionset)}; "
            + $"phase={currentActionPhase}; "
            + $"destination={AIBrainDebugFormatter.GetDestinationLabel(actionToStop?.destination)}";

        actionToStop?.actionset?.OnStop(actor, actionToStop, reason);
        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        actionToStop?.ReleaseReservation(actor);
        queuedActionToClear?.ReleaseReservation(actor);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();

        bestAction = null;
        queuedAction = null;
        currentActionPhase = "\uC7AC\uACC4\uD68D";
        currentActionPhaseDetail = reason ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();
        RequirePathSearchSession().RequestUrgent();
        isExecuted = false;
        isBestActionEnd = true;
        pathSearchSession?.Clear();
        RequireCandidateSelector().Reset();
        currentActionDebugLabel = "Replanning";
        actor?.AiMemory?.RecordDecision(
            CharacterAiBranch.InterruptCheck,
            CharacterAiUtilityText.GetIntention(actionToStop?.actionset?.Branch ?? CharacterAiBranch.None),
            string.IsNullOrWhiteSpace(reason) ? "행동을 중단하고 다시 판단" : reason,
            -0.15f);
        MarkDebugDirty();

        if (!string.IsNullOrWhiteSpace(reason))
        {
            actor?.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Changed,
                "replan",
                $"AI replan: {reason}"));
        }

        return true;
    }

    /// <summary>
    /// Ends the current action at a domain-approved safe checkpoint. Unlike a
    /// forced replan this does not invoke the action set's OnStop hook, because
    /// the owning executor has already persisted its checkpoint and is
    /// unwinding its own coroutine. It still closes the typed action epoch and
    /// releases transient movement/reservation ownership exactly once.
    /// </summary>
    public bool SuspendCurrentActionAtSafeCheckpoint(string reason)
    {
        if (externallyDrivenActionActive || bestAction == null)
        {
            return false;
        }

        AIAction actionToSuspend = bestAction;
        AIAction queuedActionToClear = queuedAction;
        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        actionToSuspend.ReleaseReservation(actor);
        queuedActionToClear?.ReleaseReservation(actor);
        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();

        bestAction = null;
        queuedAction = null;
        currentActionPhase = "Suspended";
        currentActionPhaseDetail = reason ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
        destinationFailedThisDecision.Clear();
        ClearPathSearchCache();
        RequirePathSearchSession().RequestUrgent();
        isExecuted = false;
        decisionPending = true;
        if (actor != null && aiSchedulingService != null)
        {
            aiSchedulingService.RequestImmediateDecision(actor);
        }
        pathSearchSession?.Clear();
        RequireCandidateSelector().Reset();
        currentActionDebugLabel = "Suspended for emergency";
        MarkDebugDirty();
        return true;
    }

    private void SetSelectedAction(AIAction action, string phase)
    {
        if (actionEpochLive)
        {
            pendingLiveEpochReplacementDetail =
                $"selected-over-live-epoch; epoch={currentActionEpoch}; "
                + $"oldBranch={currentActionBranch}; oldAction={currentActionDebugLabel}; "
                + $"oldDefinition={currentActionDefinitionId}; "
                + $"oldDestination={currentActionDestinationId}; "
                + $"oldBest={GetActionLabel(bestAction?.actionset)}; "
                + $"oldStarted={bestAction?.HasStarted == true}; "
                + $"ended={isBestActionEnd}; executed={isExecuted}; "
                + $"phase={currentActionPhase}; "
                + $"newAction={GetActionLabel(action?.actionset)}; "
                + $"newBranch={action?.actionset?.Branch ?? CharacterAiBranch.None}; "
                + $"selectionPhase={phase ?? string.Empty}";
        }

        // A committed replacement action supersedes any exact-path retry that
        // belonged to a rejected candidate from the same root decision.
        committedPathSearchDeferred = false;
        bestAction = action;
        currentActionDefinitionId = action?.actionset != null
            ? action.actionset.GetInstanceID()
            : 0;
        currentActionDestinationId = action?.destination != null
            ? action.destination.GetInstanceID()
            : 0;
        currentActionBranch = action?.actionset?.Branch ?? CharacterAiBranch.None;
        currentActionDebugLabel = GetActionLabel(action?.actionset);
        currentActionPhase = phase ?? string.Empty;
        currentRuntimePhase = CharacterAiRuntimePhase.Selected;
        AdvanceRuntimeProgress();
        currentActionPhaseDetail = AIBrainDebugFormatter.GetPathLabel(action);
        currentDestinationDebugLabel = AIBrainDebugFormatter.GetDestinationLabel(action?.destination);
        nextActionSwitchAllowedAt = Now + Mathf.Max(0f, actionTransitionCooldown);
        MarkDebugDirty();
    }

    private float GetMinimumPersistenceSeconds(AIActionSet actionSet)
    {
        if (actionSet == null)
        {
            return Mathf.Max(0f, defaultActionPersistenceSeconds);
        }

        return Mathf.Max(
            Mathf.Max(0f, defaultActionPersistenceSeconds),
            Mathf.Max(0f, actionSet.MinimumDuration),
            Mathf.Max(0f, actionTransitionCooldown));
    }

    private bool TryUseQueuedAction()
    {
        if (queuedAction == null)
        {
            return false;
        }

        AIAction action = queuedAction;
        queuedAction = null;
        action.ReleaseReservation(actor);

        if (!RequireActionEvaluator().CanUse(actor, action, out AIActionFailure _))
        {
            return false;
        }

        SetSelectedAction(action, "\uC608\uC57D \uD589\uB3D9");
        isBestActionEnd = false;
        isExecuted = false;
        return true;
    }

    private bool TryUsePreferredAction(out bool deferred)
    {
        deferred = false;
        if (preferredNextActionSet == null)
        {
            preferredActionDeferredPending = false;
            return false;
        }

        if (Now > preferredNextActionUntil)
        {
            ClearPreferredAction();
            return false;
        }

        AIAction action = availableActions?
            .FirstOrDefault(candidate => candidate?.actionset == preferredNextActionSet);
        if (action == null)
        {
            ClearPreferredAction();
            return false;
        }

        if (!RequireActionEvaluator().CanUse(actor, action, out AIActionFailure failure))
        {
            RememberCandidateFailure(action, failure);
            if (failure.IsDeferred)
            {
                deferred = true;
                TryRetainPreferredBranchDeferred(
                    action.actionset.Branch,
                    failure);
            }
            else
            {
                RetirePreferredBranchAfterHardFailure(
                    action.actionset.Branch,
                    failure,
                    CharacterAiPreferredActionFailureSource
                        .DirectActionEvaluation);
            }

            return false;
        }

        SetSelectedAction(action, "예약된 다음 행동");
        preferredActionDeferredPending = false;
        preferredActionCommitCount = checked(
            preferredActionCommitCount + 1L);
        RecordPreferredActionDisposition(
            CharacterAiPreferredActionDisposition.Selected,
            action.actionset.Branch);
        ClearPreferredAction(preserveWorkType: action.actionset is AIWork);
        isBestActionEnd = false;
        isExecuted = false;
        return true;
    }

    private void ClearPreferredAction(bool preserveWorkType = false)
    {
        preferredActionDeferredPending = false;
        preferredNextActionSet = null;
        preferredNextActionUntil = float.NegativeInfinity;
        if (!preserveWorkType)
        {
            ClearPreferredWorkType();
        }
    }

    private void ClearPreferredWorkType()
    {
        preferredWorkTypeId = default;
        preferredWorkTypeUntil = float.NegativeInfinity;
    }

    private bool TryFindInterruptAction(
        AIAction runningAction,
        out AIAction interruptAction,
        out string interruptReason)
    {
        return AIBrainActionContinuationPolicy.TryFindInterrupt(
            actor, runningAction, availableActions, RequireActionEvaluator(),
            GetSelectionScore, out interruptAction, out interruptReason);
    }

    private void RecordActionFailure(AIActionSet actionSet, AIActionFailure failure)
    {
        if (actionSet == null) return;

        AIBrainActionEvaluator evaluator = RequireActionEvaluator();
        if (ShouldUseDestinationCooldown(failure))
        {
            evaluator.StartDestinationCooldown(
                actionSet,
                failure.Target,
                actionFailureCooldown);
        }
        else
        {
            evaluator.StartCooldown(actionSet, actionFailureCooldown);
        }
        lastFailedActionSet = actionSet;
        lastActionFailure = failure.HasFailure ? failure : AIActionFailure.Create(AIActionFailureKind.Unknown);
        TrackExecutionFailure(actionSet, lastActionFailure.Kind, isNoAction: false);
        actor?.AiMemory?.RecordDecision(
            CharacterAiBranch.InterruptCheck,
            CharacterAiUtilityText.GetIntention(actionSet.Branch),
            lastActionFailure.ToString(),
            -0.25f);
        actor?.Blackboard?.ReportActionFailure(actionSet, lastActionFailure);
        actor?.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Failed,
            lastActionFailure.Kind.ToString(),
            $"AI \uC2E4\uD328: {GetActionLabel(actionSet)} - {lastActionFailure}"));
        MarkDebugDirty();
    }
    private void RecordNoActionFailure()
    {
        if (Now < noActionLogCooldownUntil) return;

        noActionLogCooldownUntil = Now + Mathf.Max(0.1f, actionFailureCooldown);
        lastFailedActionSet = null;
        lastActionFailure = AIActionFailure.Create(AIActionFailureKind.NoAction, "\uC2E4\uD589 \uAC00\uB2A5\uD55C \uD589\uB3D9 \uC5C6\uC74C");
        TrackExecutionFailure(null, AIActionFailureKind.NoAction, isNoAction: true);
        currentActionDebugLabel = "\uB300\uAE30";
        actor?.Blackboard?.ReportActionFailure(null, lastActionFailure);
        actor?.AddActivity(CharacterActivityEvent.InternalAi(
            CharacterActivityOutcomes.Blocked,
            "no-action",
            "AI \uB300\uAE30: \uC2E4\uD589 \uAC00\uB2A5\uD55C \uD589\uB3D9 \uC5C6\uC74C"));
        MarkDebugDirty();
    }
    private static string GetActionLabel(AIActionSet actionSet)
    {
        if (actionSet == null) return "\uD589\uB3D9 \uC5C6\uC74C";
        return actionSet.GetDisplayLabel();
    }

    private void RecordCandidateDebug(AIAction action, AIActionFailure failure)
    {
        if (action == null || action.actionset == null)
        {
            return;
        }

        lastCandidateScores.Add(new AIActionDebugCandidate(
            GetActionLabel(action.actionset),
            action.score,
            failure,
            failure.Target != null ? failure.Target : action.destination));
        MarkDebugDirty();
    }

    private void RememberCandidateFailure(AIAction action, AIActionFailure failure)
    {
        if (action == null || action.actionset == null || !failure.HasFailure)
        {
            return;
        }

        candidateRejectionCount++;
        IncrementFailureKind(candidateRejectionsByKind, failure.Kind);

        if (GetFailureDebugPriority(failure.Kind) <= GetFailureDebugPriority(lastActionFailure.Kind))
        {
            return;
        }

        lastFailedActionSet = action.actionset;
        lastActionFailure = failure;
        if (ShouldCooldownCandidateFailure(failure.Kind))
        {
            AIBrainActionEvaluator evaluator = RequireActionEvaluator();
            if (ShouldUseDestinationCooldown(failure))
            {
                evaluator.StartDestinationCooldown(
                    action.actionset,
                    failure.Target,
                    actionFailureCooldown);
            }
            else
            {
                evaluator.StartCooldown(action.actionset, actionFailureCooldown);
            }
        }
    }

    private void ReleaseFinishedActionReservation()
    {
        if (bestAction == null || !isBestActionEnd)
        {
            return;
        }

        NotifyActionTerminal(CharacterAiActionTerminalKind.Completed);
        bestAction.ReleaseReservation(actor);
    }

    private static bool ShouldCooldownCandidateFailure(AIActionFailureKind kind)
    {
        return kind == AIActionFailureKind.DestinationOccupied
            || kind == AIActionFailureKind.NoDestination
            || kind == AIActionFailureKind.DestinationSelectionFailed
            || kind == AIActionFailureKind.Unsupported;
    }

    private static int GetFailureDebugPriority(AIActionFailureKind kind)
    {
        return kind switch
        {
            AIActionFailureKind.DestinationOccupied => 80,
            AIActionFailureKind.PathSearchStarved => 75,
            AIActionFailureKind.NoDestination => 60,
            AIActionFailureKind.DestinationSelectionFailed => 55,
            AIActionFailureKind.NoWork => 45,
            AIActionFailureKind.OffDuty => 35,
            AIActionFailureKind.Unsupported => 25,
            AIActionFailureKind.CannotStart => 20,
            AIActionFailureKind.CandidateEvaluationDeferred => 10,
            AIActionFailureKind.FacilityCandidateDeferred => 10,
            AIActionFailureKind.PathSearchDeferred => 10,
            AIActionFailureKind.Cooldown => 5,
            AIActionFailureKind.NoScore => 1,
            _ => 0
        };
    }

    public string GetDebugSummary(int candidateCount = 3)
    {
        return AIBrainDebugFormatter.Format(CreateDebugSnapshot(), candidateCount);
    }

    internal void BeginJobGiverEvaluationPass()
    {
        if (currentJobGiverEvaluationPassRevision == int.MaxValue)
        {
            Array.Clear(
                currentJobGiverRejectedRevisionByBranch,
                0,
                currentJobGiverRejectedRevisionByBranch.Length);
            currentJobGiverEvaluationPassRevision = 1;
        }
        else
        {
            currentJobGiverEvaluationPassRevision++;
        }

        currentJobGiverRejectedBranch = CharacterAiBranch.None;
        currentJobGiverRejectedFailureKind = AIActionFailureKind.None;
        currentJobGiverRejectedReason = string.Empty;
    }

    internal void RecordJobGiverEvaluationRejection(
        CharacterAiBranch branch,
        AIActionFailure failure,
        string reason)
    {
        AIActionFailureKind kind = failure.HasFailure
            ? failure.Kind
            : AIActionFailureKind.Unknown;
        int index = (int)branch * FailureKindCount + (int)kind;
        if (index >= 0
            && index < jobGiverEvaluationRejectionsByBranchAndKind.Length)
        {
            jobGiverEvaluationRejectionsByBranchAndKind[index]++;
        }

        // Deferred states are scheduler backpressure, not rejected decisions.
        // Keep their typed per-branch counters for diagnosis, but do not inflate
        // the user-facing rejection total or replace the current hard failure.
        if (failure.IsDeferred)
        {
            return;
        }

        jobGiverEvaluationRejectionCount++;
        currentJobGiverRejectedBranch = branch;
        currentJobGiverRejectedFailureKind = kind;
        currentJobGiverRejectedReason = string.IsNullOrWhiteSpace(reason)
            ? failure.ToString()
            : reason;
        int branchIndex = (int)branch;
        if (branchIndex >= 0 && branchIndex < BranchCount)
        {
            currentJobGiverRejectedRevisionByBranch[branchIndex] =
                currentJobGiverEvaluationPassRevision;
            currentJobGiverRejectedKindByBranch[branchIndex] = kind;
            currentJobGiverRejectedReasonByBranch[branchIndex] =
                currentJobGiverRejectedReason;
        }
        MarkDebugDirty();
    }

    private static bool ShouldUseDestinationCooldown(AIActionFailure failure)
    {
        return !ReferenceEquals(failure.Target, null)
            && failure.Kind is AIActionFailureKind.Destroyed
                or AIActionFailureKind.NoPath
                or AIActionFailureKind.DestinationOccupied
                or AIActionFailureKind.FacilityAdmissionRejected
                or AIActionFailureKind.FacilityServiceUnavailable
                or AIActionFailureKind.ResourceUnavailable
                or AIActionFailureKind.ConsumptionFailed;
    }

    internal void ReportRuntimeActionFailure(
        AIActionFailure failure,
        bool requestImmediateReplan)
    {
        AIActionSet actionSet = bestAction?.actionset;
        if (actionSet != null)
        {
            RecordActionFailure(actionSet, failure);
        }
        else
        {
            lastFailedActionSet = null;
            lastActionFailure = failure.HasFailure
                ? failure
                : AIActionFailure.Create(AIActionFailureKind.Unknown);
            TrackExecutionFailure(
                null,
                lastActionFailure.Kind,
                isNoAction: lastActionFailure.Kind == AIActionFailureKind.NoAction);
            actor?.Blackboard?.ReportActionFailure(null, lastActionFailure);
            actor?.AddActivity(CharacterActivityEvent.InternalAi(
                CharacterActivityOutcomes.Failed,
                lastActionFailure.Kind.ToString(),
                $"AI execution failed: {lastActionFailure}"));
            MarkDebugDirty();
        }

        if (requestImmediateReplan)
        {
            RequestImmediateReplan(clearFailures: false);
        }
    }

    public CharacterAiRuntimeDiagnosticsSnapshot CaptureRuntimeDiagnostics()
    {
        return new CharacterAiRuntimeDiagnosticsSnapshot(
            actionStartCount,
            actionSwitchCount,
            sameActionRestartCount,
            phaseTransitionCount,
            immediateReplanCount,
            interruptedReplanCount,
            lastInterruptedReplanDetail,
            executionFailureCount,
            noActionFailureCount,
            candidateRejectionCount,
            duplicateExecutionSuppressionCount,
            interactionActionReplacementCount,
            lastInteractionActionReplacementDetail,
            protectedRunningActionReplanCount,
            lastProtectedRunningActionReplanDetail,
            orphanWorkActionRecoveryCount,
            lastOrphanWorkActionRecoveryDetail,
            currentRepeatedFailureCount,
            peakRepeatedFailureCount,
            repeatedFailureKind,
            lastExecutionFailureDetail,
            (long[])executionFailuresByKind.Clone(),
            (long[])candidateRejectionsByKind.Clone(),
            jobGiverEvaluationRejectionCount,
            (long[])jobGiverEvaluationRejectionsByBranchAndKind.Clone(),
            CopyRuntimeTrace(),
            CaptureRuntimeGateSnapshot());
    }

    private void TrackExecutionFailure(
        AIActionSet actionSet,
        AIActionFailureKind kind,
        bool isNoAction)
    {
        lastExecutionFailureDetail = lastActionFailure.HasFailure
            ? lastActionFailure.ToString()
            : kind.ToString();
        executionFailureCount++;
        if (isNoAction)
        {
            noActionFailureCount++;
        }
        IncrementFailureKind(executionFailuresByKind, kind);
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.ExecutionFailure,
            kind);

        float repeatedFailureWindow = Mathf.Max(
            5f,
            Mathf.Max(0.1f, actionFailureCooldown) * 4f);
        bool withinRepeatedFailureWindow =
            repeatedFailureLastAt > float.NegativeInfinity
            && Now - repeatedFailureLastAt <= repeatedFailureWindow;
        if (withinRepeatedFailureWindow
            && kind == repeatedFailureKind
            && actionSet == repeatedFailureActionSet)
        {
            currentRepeatedFailureCount++;
        }
        else
        {
            repeatedFailureKind = kind;
            repeatedFailureActionSet = actionSet;
            currentRepeatedFailureCount = 1L;
        }

        if (currentRepeatedFailureCount > peakRepeatedFailureCount)
        {
            peakRepeatedFailureCount = currentRepeatedFailureCount;
        }
        repeatedFailureLastAt = Now;
        if (currentRepeatedFailureCount == 4L)
        {
            failureLoopCount++;
        }
    }

    private static void IncrementFailureKind(long[] counts, AIActionFailureKind kind)
    {
        int index = (int)kind;
        if (index > 0 && index < counts.Length)
        {
            counts[index]++;
        }
    }

    private static int GetBranchIndex(CharacterAiBranch branch)
    {
        int index = (int)branch;
        return index >= 0 && index < BranchCount ? index : 0;
    }

    private CharacterAiBranch ResolveRuntimeBranch()
    {
        CharacterAiBranch branch = bestAction?.actionset?.Branch
            ?? currentActionBranch;
        return GetBranchIndex(branch) == 0
            ? CharacterAiBranch.None
            : branch;
    }

    private void AdvanceRuntimeProgress(long progressMilli = 0L)
    {
        runtimeProgressRevision = checked(runtimeProgressRevision + 1L);
        if (progressMilli != 0L)
        {
            RecordRuntimeTrace(
                CharacterAiRuntimeTraceKind.Progress,
                progressMilli: progressMilli);
        }
    }

    private void AdvanceGameplayProgress(long progressMilli = 0L)
    {
        gameplayProgressRevision = checked(gameplayProgressRevision + 1L);
        branchGameplayProgress[GetBranchIndex(ResolveRuntimeBranch())]++;
        AdvanceRuntimeProgress(progressMilli);
    }

    public void NotifyMovementStarted(int stepCount)
    {
        currentRuntimePhase = CharacterAiRuntimePhase.Moving;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.MovementStarted,
            pathStepCount: Mathf.Max(0, stepCount));
    }

    public void NotifyMovementProgress(int stepIndex, int stepCount)
    {
        currentRuntimePhase = CharacterAiRuntimePhase.Moving;
        AdvanceGameplayProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.MovementProgress,
            pathStepIndex: Mathf.Max(0, stepIndex),
            pathStepCount: Mathf.Max(0, stepCount));
    }

    /// <summary>
    /// Records real domain work without treating scheduler bookkeeping as
    /// progress. Long-running non-AbilityWork actions such as rescue use this
    /// so the watchdog observes WU advancement instead of phase text changes.
    /// </summary>
    public void NotifyGameplayWorkProgress(float completedWu)
    {
        if (float.IsNaN(completedWu) || float.IsInfinity(completedWu)
            || completedWu < 0f)
            throw new ArgumentOutOfRangeException(nameof(completedWu));
        if (completedWu <= 0f) return;
        long milliWu = Math.Max(
            1L,
            (long)Math.Round(
                completedWu * 1000d,
                MidpointRounding.AwayFromZero));
        AdvanceGameplayProgress(milliWu);
    }

    public void NotifyFacilityQueueHeartbeat(int queuePosition)
    {
        if (queuePosition < 0)
            throw new ArgumentOutOfRangeException(nameof(queuePosition));
        facilityQueueHeartbeatCount = checked(facilityQueueHeartbeatCount + 1L);
        if (facilityQueuePosition != queuePosition)
        {
            facilityQueuePosition = queuePosition;
            facilityQueuePositionRevision = checked(
                facilityQueuePositionRevision + 1L);
        }
        AdvanceRuntimeProgress();
    }

    public void NotifyFacilityServiceHeartbeat()
    {
        facilityServiceHeartbeatCount = checked(
            facilityServiceHeartbeatCount + 1L);
        AdvanceGameplayProgress();
    }

    public void NotifyMovementTerminal(GridMoveFailureReason failureReason)
    {
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.MovementTerminal,
            failureKind: failureReason == GridMoveFailureReason.None
                ? AIActionFailureKind.None
                : AIActionFailureKind.NoPath,
            pathState: failureReason == GridMoveFailureReason.None
                ? CharacterAiPathTraceState.Found
                : failureReason == GridMoveFailureReason.Cancelled
                    ? CharacterAiPathTraceState.Cancelled
                    : CharacterAiPathTraceState.NoPath);
    }

    public int NotifyPathRequested(
        bool repath,
        CharacterAiBranch branch = CharacterAiBranch.None)
    {
        // A new attempt now owns the prior deferred request. Its terminal
        // result below decides whether the scheduler must retain retry
        // ownership for another bounded slice.
        committedPathSearchDeferred = false;
        int requestId = nextPathRequestId == int.MaxValue
            ? 1
            : nextPathRequestId + 1;
        nextPathRequestId = requestId;
        currentPathRequestId = requestId;
        currentPathRequestBranch = branch != CharacterAiBranch.None
            ? branch
            : ResolveRuntimeBranch();
        int branchIndex = GetBranchIndex(currentPathRequestBranch);
        pathRequestCount++;
        livePathRequestCount++;
        branchPathRequests[branchIndex]++;
        branchLivePathRequests[branchIndex]++;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            repath
                ? CharacterAiRuntimeTraceKind.PathRepath
                : CharacterAiRuntimeTraceKind.PathRequested,
            pathState: CharacterAiPathTraceState.Requested,
            pathRequestId: requestId);
        return requestId;
    }

    public void NotifyPathResult(
        int requestId,
        CharacterAiPathTraceState state,
        int stepCount)
    {
        bool matchesLiveRequest = currentPathRequestId == requestId
            && livePathRequestCount > 0;
        if (matchesLiveRequest)
        {
            committedPathSearchDeferred =
                state == CharacterAiPathTraceState.Deferred;
            if (committedPathSearchDeferred)
            {
                committedPathSearchDeferralCount = checked(
                    committedPathSearchDeferralCount + 1L);
            }
        }
        int branchIndex = GetBranchIndex(
            matchesLiveRequest
                ? currentPathRequestBranch
                : CharacterAiBranch.None);
        pathResultCount++;
        branchPathResults[branchIndex]++;
        if (matchesLiveRequest)
        {
            livePathRequestCount--;
            if (branchLivePathRequests[branchIndex] > 0)
            {
                branchLivePathRequests[branchIndex]--;
            }
            else
            {
                RecordInvariantAnomaly(
                    CharacterAiRuntimeInvariant.PathCounterMismatch);
            }
        }
        else
        {
            RecordInvariantAnomaly(CharacterAiRuntimeInvariant.PathCounterMismatch);
        }
        if (matchesLiveRequest)
        {
            currentPathRequestId = 0;
            currentPathRequestBranch = CharacterAiBranch.None;
        }
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.PathResult,
            pathState: state,
            pathRequestId: requestId,
            pathStepCount: Mathf.Max(0, stepCount));
    }

    public void NotifyReservationAcquired(AIAction action)
    {
        if (action == null || !action.HasReservation)
        {
            return;
        }
        int branchIndex = GetBranchIndex(
            action.actionset?.Branch ?? ResolveRuntimeBranch());
        reservationAcquireCount++;
        liveReservationCount++;
        branchReservationAcquires[branchIndex]++;
        branchLiveReservations[branchIndex]++;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.ReservationAcquired,
            reservationState: CharacterAiReservationTraceState.Acquired,
            reservationId: GetReservationDiagnosticId(action));
    }

    public void NotifyReservationReleased(AIAction action)
    {
        if (action == null)
        {
            return;
        }
        int branchIndex = GetBranchIndex(
            action.actionset?.Branch ?? ResolveRuntimeBranch());
        reservationReleaseCount++;
        branchReservationReleases[branchIndex]++;
        if (liveReservationCount > 0)
        {
            liveReservationCount--;
            if (branchLiveReservations[branchIndex] > 0)
            {
                branchLiveReservations[branchIndex]--;
            }
            else
            {
                RecordInvariantAnomaly(
                    CharacterAiRuntimeInvariant.ReservationCounterMismatch);
            }
        }
        else
        {
            RecordInvariantAnomaly(CharacterAiRuntimeInvariant.ReservationCounterMismatch);
        }
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.ReservationReleased,
            reservationState: CharacterAiReservationTraceState.Released,
            reservationId: GetReservationDiagnosticId(action));
    }

    public void NotifyReservationRefreshed(AIAction action)
    {
        if (action?.HasReservation != true)
        {
            return;
        }
        AdvanceRuntimeProgress();
    }

    public void NotifyReservationFailed(AIAction action)
    {
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.ReservationFailed,
            reservationState: CharacterAiReservationTraceState.Failed,
            reservationId: GetReservationDiagnosticId(action));
    }

    private static int GetReservationDiagnosticId(AIAction action)
    {
        if (action == null) return 0;
        int destinationId = action.ReservedDestination != null
            ? action.ReservedDestination.GetInstanceID()
            : action.destination != null
                ? action.destination.GetInstanceID()
                : 0;
        return RuntimeHelpers.GetHashCode(action) ^ destinationId;
    }

    public void NotifyRetryScheduled(float delaySeconds)
    {
        retryScheduleCount++;
        currentRetryAttempt++;
        AdvanceRuntimeProgress();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.RetryScheduled,
            retryAttempt: currentRetryAttempt,
            delayMilliseconds: Mathf.Max(0, Mathf.RoundToInt(delaySeconds * 1000f)));
    }

    public void NotifyRetryAttempted()
    {
        if (currentRetryAttempt <= 0)
        {
            return;
        }
        retryAttemptCount = checked(retryAttemptCount + 1L);
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.RetryAttempted,
            retryAttempt: currentRetryAttempt);
    }

    public void NotifySchedulerDecisionProcessed(
        float dueTime,
        float processedAt,
        bool decided,
        bool retryScheduled)
    {
        schedulerProcessCount = checked(schedulerProcessCount + 1L);
        if (!retryScheduled && decided)
        {
            currentRetryAttempt = 0;
        }
        AdvanceRuntimeProgress();
        if (processedAt > dueTime + 0.0001f)
        {
            int lagMilliseconds = Mathf.Max(
                0,
                Mathf.RoundToInt((processedAt - dueTime) * 1000f));
            maximumSchedulerDelayMilliseconds = Mathf.Max(
                maximumSchedulerDelayMilliseconds,
                lagMilliseconds);
            if (lagMilliseconds >= 2000)
            {
                schedulerOverdueCount++;
                RecordRuntimeTrace(
                    CharacterAiRuntimeTraceKind.SchedulerOverdue,
                    delayMilliseconds: lagMilliseconds);
            }
        }
        AuditRuntimeInvariants();
    }

    public void AuditRuntimeInvariants()
    {
        CharacterAiRuntimeInvariant mask = CharacterAiRuntimeInvariant.None;
        if (actionEpochLive && bestAction == null)
            mask |= CharacterAiRuntimeInvariant.LiveEpochWithoutAction;
        if (HasRunningAction && !actionEpochLive)
            mask |= CharacterAiRuntimeInvariant.RunningWithoutEpoch;
        if (isExecuted
            && bestAction == null
            && !manualCommandActive
            && !externallyDrivenActionActive)
            mask |= CharacterAiRuntimeInvariant.ExecutedWithoutActionOwner;
        int actualReservations = 0;
        bool bestActionCounted = false;
        bool queuedActionCounted = false;
        // Candidate evaluation may span several scheduler slices. Evaluated
        // candidates can legitimately retain a destination reservation until
        // the continuation chooses a winner or releases the losers, so the
        // invariant authority must include every live action candidate rather
        // than only bestAction and queuedAction. Use the existing fixed action
        // array directly to keep the always-on audit allocation-free.
        if (availableActions != null)
        {
            for (int index = 0; index < availableActions.Length; index++)
            {
                AIAction candidate = availableActions[index];
                if (candidate?.HasReservation != true)
                {
                    continue;
                }

                actualReservations++;
                bestActionCounted |= ReferenceEquals(candidate, bestAction);
                queuedActionCounted |= ReferenceEquals(candidate, queuedAction);
            }
        }
        if (!bestActionCounted && bestAction?.HasReservation == true)
            actualReservations++;
        if (!queuedActionCounted
            && queuedAction?.HasReservation == true
            && !ReferenceEquals(queuedAction, bestAction))
            actualReservations++;
        if (actualReservations != liveReservationCount)
            mask |= CharacterAiRuntimeInvariant.ReservationCounterMismatch;
        if (livePathRequestCount < 0 || livePathRequestCount > 1)
            mask |= CharacterAiRuntimeInvariant.PathCounterMismatch;
        int branchActionLiveTotal = 0;
        int branchPathLiveTotal = 0;
        int branchReservationLiveTotal = 0;
        for (int index = 0; index < BranchCount; index++)
        {
            branchActionLiveTotal += branchLiveActions[index];
            branchPathLiveTotal += branchLivePathRequests[index];
            branchReservationLiveTotal += branchLiveReservations[index];
        }
        if (branchActionLiveTotal != (actionEpochLive ? 1 : 0)
            || branchPathLiveTotal != livePathRequestCount
            || branchReservationLiveTotal != liveReservationCount)
        {
            mask |= CharacterAiRuntimeInvariant.BranchCounterMismatch;
        }

        CharacterAiRuntimeInvariant newlyActive = mask & ~activeInvariantMask;
        activeInvariantMask = mask;
        if (newlyActive != CharacterAiRuntimeInvariant.None)
        {
            RecordInvariantAnomaly(newlyActive);
        }
    }

    private void RecordInvariantAnomaly(CharacterAiRuntimeInvariant invariant)
    {
        if (invariant == CharacterAiRuntimeInvariant.None) return;
        invariantAnomalyCount++;
        lastInvariantAnomaly = invariant;
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.InvariantAnomaly,
            invariant: invariant);
    }

    public CharacterAiRuntimeGateSnapshot CaptureRuntimeGateSnapshot()
    {
        CharacterAiBranchRuntimeGateSnapshot[] branches =
            new CharacterAiBranchRuntimeGateSnapshot[BranchCount];
        for (int index = 0; index < BranchCount; index++)
        {
            branches[index] = new CharacterAiBranchRuntimeGateSnapshot(
                branchActionStarts[index],
                branchActionTerminals[index],
                branchLiveActions[index],
                branchGameplayProgress[index],
                branchPathRequests[index],
                branchPathResults[index],
                branchLivePathRequests[index],
                branchReservationAcquires[index],
                branchReservationReleases[index],
                branchLiveReservations[index]);
        }

        return new CharacterAiRuntimeGateSnapshot(
            actionStartCount,
            actionTerminalCount,
            actionCompletedCount,
            actionFailedCount,
            actionCancelledCount,
            actionEpochLive ? 1 : 0,
            runtimeProgressRevision,
            gameplayProgressRevision,
            facilityQueueHeartbeatCount,
            facilityServiceHeartbeatCount,
            pathRequestCount,
            pathResultCount,
            livePathRequestCount,
            reservationAcquireCount,
            reservationReleaseCount,
            liveReservationCount,
            retryScheduleCount,
            retryAttemptCount,
            schedulerProcessCount,
            schedulerOverdueCount,
            maximumSchedulerDelayMilliseconds,
            invariantAnomalyCount,
            failureLoopCount,
            branches);
    }

    private void RecordRuntimeTrace(
        CharacterAiRuntimeTraceKind kind,
        AIActionFailureKind failureKind = AIActionFailureKind.None,
        CharacterAiActionTerminalKind terminalKind = CharacterAiActionTerminalKind.None,
        CharacterAiPathTraceState pathState = CharacterAiPathTraceState.None,
        CharacterAiReservationTraceState reservationState = CharacterAiReservationTraceState.None,
        CharacterAiRuntimeInvariant invariant = CharacterAiRuntimeInvariant.None,
        int pathRequestId = 0,
        int pathStepIndex = 0,
        int pathStepCount = 0,
        int reservationId = 0,
        int retryAttempt = 0,
        int delayMilliseconds = 0,
        long progressMilli = 0L)
    {
        AIAction action = bestAction;
        int actionDefinitionId = action?.actionset != null
            ? action.actionset.GetInstanceID()
            : currentActionDefinitionId;
        int destinationId = action?.destination != null
            ? action.destination.GetInstanceID()
            : currentActionDestinationId;
        int phaseCode = string.IsNullOrEmpty(currentActionPhase)
            ? 0
            : StringComparer.Ordinal.GetHashCode(currentActionPhase);
        long sequence = checked(runtimeTraceSequence + 1L);
        runtimeTraceSequence = sequence;
        runtimeTrace[runtimeTraceWriteIndex] = new CharacterAiRuntimeTraceEvent(
            sequence,
            currentActionEpoch,
            Now,
            kind,
            action?.actionset?.Branch ?? currentActionBranch,
            failureKind,
            actionDefinitionId,
            destinationId,
            phaseCode,
            runtimeProgressRevision,
            currentRuntimePhase,
            terminalKind,
            pathState,
            reservationState,
            invariant,
            pathRequestId,
            pathStepIndex,
            pathStepCount,
            reservationId,
            retryAttempt,
            delayMilliseconds,
            progressMilli);
        runtimeTraceWriteIndex = (runtimeTraceWriteIndex + 1)
            % RuntimeTraceCapacity;
        runtimeTraceCount = Mathf.Min(
            RuntimeTraceCapacity,
            runtimeTraceCount + 1);
    }

    private CharacterAiRuntimeTraceEvent[] CopyRuntimeTrace()
    {
        if (runtimeTraceCount == 0)
        {
            return Array.Empty<CharacterAiRuntimeTraceEvent>();
        }

        CharacterAiRuntimeTraceEvent[] copy =
            new CharacterAiRuntimeTraceEvent[runtimeTraceCount];
        int start = runtimeTraceCount == RuntimeTraceCapacity
            ? runtimeTraceWriteIndex
            : 0;
        for (int index = 0; index < runtimeTraceCount; index++)
        {
            copy[index] = runtimeTrace[
                (start + index) % RuntimeTraceCapacity];
        }

        return copy;
    }

    public int GetDebugHash()
    {
        return AIBrainDebugFormatter.GetHash(CreateDebugSnapshot());
    }

    private AIBrainDebugSnapshot CreateDebugSnapshot()
    {
        return new AIBrainDebugSnapshot
        {
            Actor = actor,
            BestAction = bestAction,
            CurrentActionLabel = currentActionDebugLabel,
            CurrentPhase = currentActionPhase,
            CurrentPhaseDetail = currentActionPhaseDetail,
            CurrentDestinationLabel = currentDestinationDebugLabel,
            ActionSwitchRemaining = Mathf.Max(0f, nextActionSwitchAllowedAt - Now),
            LastFailure = lastActionFailure,
            LastFailedAction = lastFailedActionSet,
            Candidates = lastCandidateScores,
            CandidateLimit = debugCandidateLimit,
            DebugVersion = DebugVersion
        };
    }

    private void MarkDebugDirty()
    {
        DebugVersion++;
    }

}
