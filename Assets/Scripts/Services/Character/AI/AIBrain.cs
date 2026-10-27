using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using VContainer;


public class AIBrain : CharacterAbility
{
    private readonly CharacterOperationBlockObservationRuntime operationObservation = new CharacterOperationBlockObservationRuntime();
    private readonly CharacterAiDecisionObservationCache decisionObservationCache = new CharacterAiDecisionObservationCache();
    private readonly CharacterAiPreferredActionRuntime preferredAction = new CharacterAiPreferredActionRuntime();
    private readonly CharacterExternalActionIntentRuntime externalIntent = new CharacterExternalActionIntentRuntime();
    private readonly CharacterAiFailureDiagnosticsRuntime failureDiagnostics = new CharacterAiFailureDiagnosticsRuntime();
    private readonly CharacterAiExecutionLedger executionLedger = new CharacterAiExecutionLedger();
    private static readonly int BranchCount =
        Enum.GetValues(typeof(CharacterAiBranch)).Length;
    public AIAction[] availableActions;
    [SerializeField, ReadOnly, FormerlySerializedAs("bestAction")]
    private AIAction selectedAction;
    public AIAction bestAction { get => selectedAction; private set => selectedAction = value; }
    [SerializeField, FormerlySerializedAs("isBestActionEnd")]
    private bool decisionPending = true;
    public bool isBestActionEnd
    {
        get => decisionPending;
        private set
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
    [SerializeField, FormerlySerializedAs("isExecuted")]
    private bool executionActive;
    public bool isExecuted { get => executionActive; private set => executionActive = value; }
    private AIAction stopCallbackAction;

    internal void HoldDecisionForCurrentExecution() => isBestActionEnd = false;
    internal void RequestDecisionAfterExecution() => isBestActionEnd = true;

    internal void CompleteWorkCancellation(AIAction expectedAction, bool requestImmediateReplan)
    {
        if (!ReferenceEquals(bestAction, expectedAction)
            || expectedAction != null && ReferenceEquals(stopCallbackAction, expectedAction)) return;
        if (expectedAction != null) NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        bestAction = null;
        if (expectedAction != null || requestImmediateReplan)
        {
            decisionPending = true;
            RequestImmediateReplan(clearFailures: false);
        }
        else if (actor != null && actor.isActiveAndEnabled
                 && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            RequestDecisionAfterExecution();
    }

    private void InvokeActionStop(AIAction action, string reason)
    {
        if (action == null || ReferenceEquals(stopCallbackAction, action)) return;
        AIAction previous = stopCallbackAction;
        stopCallbackAction = action;
        try { action.actionset?.OnStop(actor, action, reason); }
        finally { stopCallbackAction = previous; }
    }

    internal ProjectionCheckpoint CaptureProjectionCheckpoint() => new(this);

    internal void RestoreProjectionCheckpoint(ProjectionCheckpoint snapshot)
    {
        if (snapshot == null || !ReferenceEquals(snapshot.Owner, this))
            throw new InvalidOperationException("AI projection checkpoint belongs to a different brain.");
        // 복원 취소는 새 실행이나 판단 요청을 만들지 않는다.
        bestAction = snapshot.Action;
        isExecuted = snapshot.Executed;
        decisionPending = snapshot.Pending;
        currentActionPhase = snapshot.Phase;
        currentActionPhaseDetail = snapshot.Detail;
        currentDestinationDebugLabel = snapshot.Destination;
        executionLedger.SetRuntimePhaseForPresentation(snapshot.Phase);
        phaseTransitionCount = snapshot.PhaseTransitions;
        DebugVersion = snapshot.DebugVersion;
    }

    internal sealed class ProjectionCheckpoint
    {
        internal readonly AIBrain Owner;
        internal readonly AIAction Action;
        internal readonly bool Executed, Pending;
        internal readonly string Phase, Detail, Destination;
        internal readonly long PhaseTransitions;
        internal readonly int DebugVersion;
        internal ProjectionCheckpoint(AIBrain owner)
        {
            Owner = owner; Action = owner.bestAction; Executed = owner.isExecuted;
            Pending = owner.decisionPending; Phase = owner.currentActionPhase;
            Detail = owner.currentActionPhaseDetail; Destination = owner.currentDestinationDebugLabel;
            PhaseTransitions = owner.phaseTransitionCount; DebugVersion = owner.DebugVersion;
        }
    }

#if UNITY_EDITOR
    public void SetSelectedActionForDebug(AIAction action) => bestAction = action;
    public void SetExecutionFlagForDebug(bool executed) => isExecuted = executed;
    public void SetDecisionPendingForDebug(bool pending) => isBestActionEnd = pending;
#endif
    [SerializeField] private float actionFailureCooldown = 1f;
    [SerializeField, Range(0f, 0.5f)] private float actionSwitchScoreMargin = 0.12f;
    [SerializeField, Min(0f)] private float actionTransitionCooldown = 0.75f;
    [SerializeField, Min(0f)] private float defaultActionPersistenceSeconds = 0.75f;
    [SerializeField, Range(1, 8)] private int debugCandidateLimit = 3;
    private readonly List<AIAction> destinationFailedThisDecision = new List<AIAction>();
    private readonly List<AIActionDebugCandidate> lastCandidateScores = new List<AIActionDebugCandidate>();
    private IReadOnlyList<AIActionDebugCandidate> lastCandidateScoresView;
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
    private IRandomStreamProvider randomStreamProvider;
    private CharacterId actionRandomCharacterId;
    private AIBrainActionEvaluator actionEvaluator;
    private AIBrainCandidateSelector candidateSelector;
    private AIBrainPathSearchSession pathSearchSession;
    private FacilityScoringContext facilityScoringContext;
    private AIActionFailure lastActionFailure = AIActionFailure.None;
    private AIActionSet lastFailedActionSet;
    private string currentActionDebugLabel = "\uB300\uAE30";
    private string currentActionPhase = string.Empty;
    private string currentActionPhaseDetail = string.Empty;
    private string currentDestinationDebugLabel = string.Empty;
    private float nextActionSwitchAllowedAt;
    private int directDecisionTickCount;
    private int immediateDecisionRequestCount;
    private string lastImmediateDecisionReason = string.Empty;
    private long phaseTransitionCount;
    private long immediateReplanCount;
    private long interruptedReplanCount;
    private string lastInterruptedReplanDetail = string.Empty;
    private long duplicateExecutionSuppressionCount;
    private long interactionActionReplacementCount;
    private string lastInteractionActionReplacementDetail = string.Empty;
    private long protectedRunningActionReplanCount;
    private string lastProtectedRunningActionReplanDetail = string.Empty;
    private long orphanWorkActionRecoveryCount;
    private string lastOrphanWorkActionRecoveryDetail = string.Empty;
    private string lastInvariantAnomalyDetail = string.Empty;
    private string pendingLiveEpochReplacementDetail = string.Empty;
    public bool IsPathSearchDeferred =>
        pathSearchSession?.IsDeferred == true
        || executionLedger.CommittedPathSearchDeferred;
    public bool IsCommittedPathSearchDeferred => executionLedger.CommittedPathSearchDeferred;
    public long RuntimeCommittedPathSearchDeferralCount =>
        executionLedger.CommittedPathSearchDeferralCount;
    public bool IsActionScoringPending => candidateSelector?.IsPending == true;
#if UNITY_EDITOR
    public long CandidateResetCountForDiagnostics => candidateSelector?.ResetCountForDiagnostics ?? 0;
    public long CandidatePendingResetCountForDiagnostics => candidateSelector?.PendingResetCountForDiagnostics ?? 0;
    public string CandidateLastResetCallerForDiagnostics => candidateSelector?.LastResetCallerForDiagnostics ?? string.Empty;
    public string CandidateLastPendingResetDetailForDiagnostics => candidateSelector?.LastPendingResetDetailForDiagnostics ?? string.Empty;
    public string CandidatePendingDetailForDiagnostics => candidateSelector?.GetPendingDetailForDiagnostics(availableActions) ?? string.Empty;
    public string LastImmediateReplanCallerForDiagnostics { get; private set; } = string.Empty;
    public string PreferredActionTypeForDiagnostics => preferredAction.PreferredNextActionSet?.GetType().Name ?? string.Empty;
    public float PreferredActionUntilForDiagnostics => preferredAction.PreferredNextActionUntil;
#endif
    public bool IsPreferredActionDeferred =>
        preferredAction.IsDeferred;
    public long RuntimePreferredActionDeferredCount =>
        preferredAction.PreferredActionDeferredCount;
    public long RuntimePreferredActionDeferredFallbackSuppressionCount =>
        preferredAction.PreferredActionDeferredFallbackSuppressionCount;
    public long RuntimePreferredActionCommitCount =>
        preferredAction.PreferredActionCommitCount;
    public CharacterAiPreferredActionDisposition RuntimePreferredActionDisposition =>
        preferredAction.PreferredActionDisposition;
    public CharacterAiBranch RuntimePreferredActionDispositionBranch =>
        preferredAction.PreferredActionDispositionBranch;
    public long RuntimePreferredActionDispositionRevision =>
        preferredAction.PreferredActionDispositionRevision;
    public AIActionFailure LastPreferredActionDeferredFailure =>
        preferredAction.LastPreferredActionDeferredFailure;
    public AIActionFailure FirstPreferredActionHardFailure =>
        preferredAction.FirstPreferredActionHardFailure;
    public CharacterAiPreferredActionFailureSource
        FirstPreferredActionHardFailureSource =>
            preferredAction.FirstPreferredActionHardFailureSource;
    public long RuntimePreferredActionHardFailureCount =>
        preferredAction.PreferredActionHardFailureCount;
    public bool HasPreferredActionHardFailureDecisionPending =>
        preferredAction.PreferredActionHardFailureDecisionPending;
    public long RuntimeActionEpoch => executionLedger.CurrentActionEpoch;
    public long RuntimeProgressRevision => executionLedger.RuntimeProgressRevision;
    public long GameplayProgressRevision => executionLedger.GameplayProgressRevision;
    public long RuntimeSchedulerProcessCount => executionLedger.SchedulerProcessCount;
    public long FacilityQueueHeartbeatCount => executionLedger.FacilityQueueHeartbeatCount;
    public long FacilityQueuePositionRevision => executionLedger.FacilityQueuePositionRevision;
    public long FacilityServiceHeartbeatCount => executionLedger.FacilityServiceHeartbeatCount;
    public CharacterAiRuntimePhase CurrentRuntimePhase => executionLedger.CurrentRuntimePhase;
    public int RuntimeLiveReservationCount => executionLedger.LiveReservationCount;
    public int RuntimeLivePathRequestCount => executionLedger.LivePathRequestCount;
    public float MaximumSchedulerDelaySeconds =>
        executionLedger.MaximumSchedulerDelayMilliseconds / 1000f;
    public void ResetSchedulerDelayTelemetryForDiagnostics() =>
        executionLedger.ResetSchedulerDelayTelemetryForDiagnostics();

    internal bool IsActionScoringPendingFor(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext = true)
    {
        return candidateSelector?.IsPendingFor(
            predicate,
            hasDecisionContext) == true;
    }
    public bool IsManualCommandActive => externalIntent.ManualCommandActive;
    public bool HasRunningAction =>
        bestAction?.HasStarted == true && !isBestActionEnd;

    public bool HasRunningWorkAction =>
        HasRunningAction
        && bestAction.actionset?.HasSemanticTag(CharacterAiActionTags.Work) == true;
    public bool IsExternallyDrivenActionActive => externalIntent.ExternallyDrivenActionActive;
    public string ExternalIntentOwnerId => externalIntent.ExternalIntentOwnerId;
    public CharacterActionIntentKind ExternalIntentKind => externalIntent.ExternalIntentKind;
    public long ExternalIntentEpoch => externalIntent.ExternalIntentEpoch;
    public int ExternalIntentTransitionCount => externalIntent.ExternalIntentTransitionCount;
    public int ExternalIntentPreemptionCount => externalIntent.ExternalIntentPreemptionCount;
    public int ExternalIntentRejectedCount => externalIntent.ExternalIntentRejectedCount;
    public int ExternalIntentStaleCompletionCount => externalIntent.ExternalIntentStaleCompletionCount;
    public CharacterAiActionTerminalKind LastExternalIntentTerminalKind =>
        externalIntent.LastExternalIntentTerminalKind;
    public int ExternalIntentTerminalCount => externalIntent.ExternalIntentTerminalCount;
    public long RuntimeActionStartCount => executionLedger.ActionStartCount;
    public long RuntimeActionSwitchCount => executionLedger.ActionSwitchCount;
    public long RuntimePhaseTransitionCount => phaseTransitionCount;
    public long RuntimeImmediateReplanCount => immediateReplanCount;
    public long RuntimeExecutionFailureCount => failureDiagnostics.ExecutionFailureCount;
    public long RuntimeCandidateRejectionCount => failureDiagnostics.CandidateRejectionCount;
    public long RuntimeDuplicateExecutionSuppressionCount =>
        duplicateExecutionSuppressionCount;
    public long RuntimeInteractionActionReplacementCount =>
        interactionActionReplacementCount;
    public long RuntimeProtectedRunningActionReplanCount =>
        protectedRunningActionReplanCount;
    public long RuntimeOrphanWorkActionRecoveryCount =>
        orphanWorkActionRecoveryCount;
    public long RuntimeJobGiverEvaluationRejectionCount =>
        failureDiagnostics.JobGiverEvaluationRejectionCount;
    public long RuntimePeakRepeatedFailureCount => failureDiagnostics.PeakRepeatedFailureCount;
    public AIActionFailureKind RuntimeRepeatedFailureKind => failureDiagnostics.RepeatedFailureKind;
    public long RuntimeInvariantAnomalyCount => executionLedger.InvariantAnomalyCount;
    public CharacterAiRuntimeInvariant LastInvariantAnomalyForDiagnostics =>
        executionLedger.LastInvariantAnomaly;
    public string LastInvariantAnomalyDetailForDiagnostics =>
        lastInvariantAnomalyDetail;
    public long RuntimeFailureLoopCount => failureDiagnostics.FailureLoopCount;
    public string CurrentJobGiverEvaluationRejectionSummary =>
        failureDiagnostics.CurrentJobGiverRejectedBranch == CharacterAiBranch.None
            ? "none"
            : $"{failureDiagnostics.CurrentJobGiverRejectedBranch}/{failureDiagnostics.CurrentJobGiverRejectedFailureKind}: {failureDiagnostics.CurrentJobGiverRejectedReason}";

    public string GetCurrentJobGiverEvaluationRejectionSummary(
        CharacterAiBranch branch)
    {
        return failureDiagnostics.GetCurrentJobGiverEvaluationRejectionSummary(branch);
    }

    public bool IsExternalIntentCurrent(
        in CharacterActionIntentLease lease) => externalIntent.OwnsExternalIntent(lease);
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
            decisionObservationCache.ClearFacilityScores();
            decisionObservationCache.ClearWorldSignalCache();
            decisionObservationCache.InvalidateCrowdSensitivity();
        }

        return decisionPipeline.RunRootDecision(actor);
    }

    public bool HasResumableDecisionPipeline =>
        actor != null && decisionPipeline != null;

#if UNITY_EDITOR
    public void ConfigureDeterministicActionScoringForDiagnostics(bool enabled)
    {
        if (!Application.isEditor)
        {
            throw new InvalidOperationException(
                "Deterministic action scoring is editor-only.");
        }

        candidateSelector?.ConfigureDeterministicFullPassForDiagnostics(enabled);
    }

    public bool LogisticsMeasurementOnlyForDiagnostics =>
        candidateSelector?.LogisticsMeasurementOnlyForDiagnostics == true;

    public void ConfigureLogisticsMeasurementForDiagnostics(bool enabled)
    {
        if (!Application.isEditor)
        {
            throw new InvalidOperationException(
                "Logistics-only AI measurement is editor-only.");
        }

        candidateSelector?.ConfigureLogisticsMeasurementForDiagnostics(enabled);
    }
#endif

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
        preferredAction.BindClock(gameClock);
        randomStreamProvider = requiredExecution.RandomStreams;
        actionRandom = null;
        actionRandomCharacterId = default;
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
        IRandomStreamProvider provider = randomStreamProvider
            ?? throw new InvalidOperationException(
                $"{nameof(AIBrain)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
        CacheLocalReferences();
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        if (actionRandom == null || !actionRandomCharacterId.Equals(characterId))
        {
            actionRandom = provider.Get(
                CharacterRandomStreamScopeIds.Decision(characterId));
            actionRandomCharacterId = characterId;
        }

        return actionRandom;
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
        operationObservation.ClearAllOperationDiagnostics();
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
            availableActions,
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
        return decisionObservationCache.TryGetCachedFacilityScore(building, role, out score);
    }

    internal void CacheFacilityScore(
        BuildableObject building,
        FacilityRole role,
        float score)
    {
        decisionObservationCache.CacheFacilityScore(building, role, score);
    }

    internal float GetFacilityCrowdSensitivity(CharacterActor sourceActor)
    {
        return decisionObservationCache.GetFacilityCrowdSensitivity(sourceActor);
    }

    internal bool TryGetCachedWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        out CharacterAiWorldSignalSnapshot snapshot)
    {
        return decisionObservationCache.TryGetCachedWorldSignal(branch, actorPosition, out snapshot);
    }

    internal void CacheWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        CharacterAiWorldSignalSnapshot snapshot)
    {
        decisionObservationCache.CacheWorldSignal(branch, actorPosition, snapshot);
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
        if (externalIntent.ExternallyDrivenActionActive) return false;
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
        decisionObservationCache.ClearFacilityScores();
        decisionObservationCache.ClearWorldSignalCache();
        decisionObservationCache.InvalidateCrowdSensitivity();

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

    internal bool CanRestoreFacilityUse(FacilityUseExecutionState state) =>
        state != null && CanRestoreBuildingUse(state.actionType, state.actionBranch);

    internal bool CanRestoreShopUse(ShopUseExecutionState state) =>
        state != null && CanRestoreBuildingUse(state.actionType, state.actionBranch);

    private bool CanRestoreBuildingUse(string actionType, int actionBranch) =>
        availableActions != null
        && availableActions.Where(value => value?.actionset != null
                && value.actionset.GetType().FullName == actionType
                && (int)value.actionset.Branch == actionBranch)
            .Select(value => value.actionset).Distinct().Count() == 1;

    internal AIAction RestoreFacilityUseAction(Facility target, FacilityUseExecutionState state) =>
        state == null ? throw new ArgumentNullException(nameof(state))
            : RestoreBuildingUseAction(target, state.actionType, state.actionBranch);

    internal AIAction RestoreShopUseAction(Shop target, ShopUseExecutionState state) =>
        state == null ? throw new ArgumentNullException(nameof(state))
            : RestoreBuildingUseAction(target, state.actionType, state.actionBranch);

    private AIAction RestoreBuildingUseAction(BuildableObject target, string actionType, int actionBranch)
    {
        if (target == null || !CanRestoreBuildingUse(actionType, actionBranch)
            || bestAction != null || externalIntent.ManualCommandActive || externalIntent.ExternallyDrivenActionActive)
            throw new InvalidOperationException("Cannot bind the saved facility execution to this actor.");
        AIActionSet definition = availableActions.Where(value => value?.actionset != null
                && value.actionset.GetType().FullName == actionType
                && (int)value.actionset.Branch == actionBranch)
            .Select(value => value.actionset).Distinct().Single();
        var action = new AIAction(definition, AIActionPlan.AtDestination(target));
        action.BindClock(gameClock);
        SetSelectedAction(action, "시설 이용 재개");
        isBestActionEnd = false;
        lastActionFailure = AIActionFailure.None;
        lastFailedActionSet = null;
        actor.Blackboard?.Commit(action, currentActionDebugLabel);
        NotifyActionStarted();
        return action;
    }

    public bool TryCommitActionCandidate(
        CharacterAiActionCandidate candidate,
        out AIActionFailure failure)
    {
        if (externalIntent.ExternallyDrivenActionActive)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.CannotStart,
                $"Selected action cannot replace active external owner {externalIntent.ExternalIntentOwnerId} ({externalIntent.ExternalIntentEpoch}).");
            return false;
        }
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
            action.actionset == preferredAction.PreferredNextActionSet;
        SetSelectedAction(action, "\uC120\uD0DD");
        if (committingPreferred)
        {
            preferredAction.RecordCandidateCommitted(action.actionset.Branch);
            preferredAction.ClearPreferredAction(
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
            decisionObservationCache.ClearFacilityScores,
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
            decisionObservationCache.ClearFacilityScores,
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
        if (preferredAction.PreferredNextActionSet != null)
        {
            if (Now > preferredAction.PreferredNextActionUntil)
            {
                preferredAction.ExpireDefinitionForScoring();
            }
            else if (action.actionset == preferredAction.PreferredNextActionSet)
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
        executionLedger.ClearCommittedPathSearchDeferral();
    }

    public void RequestImmediateReplan(
        bool clearFailures = false,
        [CallerMemberName] string callerMember = "")
    {
        if (externalIntent.ExternallyDrivenActionActive)
        {
            externalIntent.RequestClearFailures(clearFailures);
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
                operationObservation.ClearCurrentOperationBlock();
            }

            if (protectedRunningActionReplanCount <= 4
                || (protectedRunningActionReplanCount
                    & (protectedRunningActionReplanCount - 1)) == 0)
            {
                PublishDiagnostic(
                    CharacterAiDiagnosticTransition.RunningActionReplanDeferred,
                    "running-action-replan-deferred",
                    lastProtectedRunningActionReplanDetail,
                    protectedRunningActionReplanCount);
            }

            MarkDebugDirty();
            RequestImmediateDecision(
                $"Running action preserved for replan request from {callerMember}.");
            return;
        }

        immediateReplanCount++;
#if UNITY_EDITOR
        LastImmediateReplanCallerForDiagnostics = callerMember ?? string.Empty;
#endif

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
            operationObservation.ClearCurrentOperationBlock();
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
        decisionObservationCache.ClearFacilityScores();
        decisionObservationCache.ClearWorldSignalCache();
        decisionObservationCache.InvalidateCrowdSensitivity();
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
        return preferredAction.IsActionPreferredForNextDecision<TActionSet>();
    }

    public bool IsBranchPreferredForNextDecision(
        CharacterAiBranch branch)
    {
        return preferredAction.IsBranchPreferredForNextDecision(branch);
    }

    internal bool IsRoutineGroupPreferredForNextDecision(
        CharacterAiBranch routineGroup)
    {
        return preferredAction.IsRoutineGroupPreferredForNextDecision(routineGroup);
    }

    internal bool HasPreferredActionMatching(
        Predicate<AIActionSet> actionGroupMatcher)
    {
        return preferredAction.HasPreferredActionMatching(actionGroupMatcher);
    }

    internal Predicate<AIActionSet> GetPreferredActionMatcher()
    {
        return preferredAction.GetPreferredActionMatcher();
    }

    internal bool TryRetainPreferredBranchDeferred(
        CharacterAiBranch branch,
        AIActionFailure failure)
    {
        return preferredAction.TryRetainPreferredBranchDeferred(branch, failure);
    }

    internal bool RetirePreferredBranchAfterHardFailure(
        CharacterAiBranch branch,
        AIActionFailure failure,
        CharacterAiPreferredActionFailureSource source)
    {
        return preferredAction.RetirePreferredBranchAfterHardFailure(branch, failure, source);
    }

    internal bool TryConsumePreferredActionHardFailureDecision(
        out AIActionFailure failure,
        out CharacterAiPreferredActionFailureSource source)
    {
        return preferredAction.TryConsumePreferredActionHardFailureDecision(out failure, out source);
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
        preferredAction.RecordDeferredFallbackSuppression();
        RecordRuntimeTrace(
            CharacterAiRuntimeTraceKind.DeferredRetry,
            failureKind: preferredAction.LastPreferredActionDeferredFailure.Kind,
            pathState: preferredAction.LastPreferredActionDeferredFailure.Kind
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
            preferredAction.PreferWorkType(workTypeId, persistenceSeconds);
        }

        return preferred;
    }

    public bool TryGetPreferredWorkType(out WorkTypeId workTypeId)
    {
        return preferredAction.TryGetPreferredWorkType(out workTypeId);
    }

    public bool HasPreferredWorkType =>
        preferredAction.HasPreferredWorkType;

    public void ConsumePreferredWorkType(WorkTypeId workTypeId)
    {
        preferredAction.ConsumePreferredWorkType(workTypeId);
    }

    private bool SetPreferredAction(
        AIActionSet actionSet,
        float persistenceSeconds)
    {
        operationObservation.ClearCurrentOperationBlock();
        preferredAction.SetPreferredAction(actionSet, persistenceSeconds);
        InvalidateQueuedActionForNextDecision();
        return preferredAction.PreferredNextActionSet != null;
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
        operationObservation.ClearCurrentOperationBlock();
        // A direct player order is the top-level command authority.  Retire the
        // currently owned autonomous survival intent before entering manual mode;
        // its coroutine may still unwind, but its lease epoch can no longer mutate
        // this command.
        if (externalIntent.ExternallyDrivenActionActive)
        {
            EndOwnedExternalIntent(
                clearFailures: true,
                CharacterAiActionTerminalKind.Cancelled);
        }

        if (bestAction != null || queuedAction != null)
        {
            StopCurrentActionForReplan("플레이어 이동 명령");
        }

        externalIntent.BeginManualCommand();
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
        externalIntent.EndManualCommand();
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
        operationObservation.ClearCurrentOperationBlock();
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
        CharacterExternalIntentAdmission admission = externalIntent.Admit(ownerId, kind);
        if (admission == CharacterExternalIntentAdmission.Rejected) return false;
        if (admission == CharacterExternalIntentAdmission.Renewed)
        {
            UpdateExternalPresentation(actionLabel, phase, detail);
            lease = externalIntent.CurrentLease;
            return true;
        }
        bool preempting = admission == CharacterExternalIntentAdmission.Preempt;

        operationObservation.ClearCurrentOperationBlock();
        NotifyActionTerminal(CharacterAiActionTerminalKind.Cancelled);
        InvokeActionStop(bestAction, "외부 구동 행동으로 전환");
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
        externalIntent.Begin(ownerId, kind);
        executionLedger.SetRuntimePhaseForPresentation(currentActionPhase);
        phaseTransitionCount++;
        AdvanceGameplayProgress();
        RecordRuntimeTrace(CharacterAiRuntimeTraceKind.PhaseChanged);
        externalIntent.RecordTransition(preempting);
        isExecuted = true;
        isBestActionEnd = false;
        ClearPathSearchCache();
        RequireCandidateSelector().Reset();
        ClearPathSearchCache();
        MarkDebugDirty();
        lease = new CharacterActionIntentLease(
            externalIntent.ExternalIntentOwnerId,
            externalIntent.ExternalIntentKind,
            externalIntent.ExternalIntentEpoch);
        return true;
    }

    public bool UpdateExternallyDrivenAction(
        in CharacterActionIntentLease lease,
        string actionLabel,
        string phase,
        string detail = null)
    {
        if (!externalIntent.OwnsExternalIntent(lease))
        {
            externalIntent.RejectUpdate();
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
        executionLedger.SetRuntimePhaseForPresentation(nextPhase);
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
        if (!externalIntent.OwnsExternalIntent(lease))
        {
            externalIntent.RejectStaleCompletion();
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures,
            CharacterAiActionTerminalKind.Completed);
    }

    public bool FailExternallyDrivenAction(
        in CharacterActionIntentLease lease)
    {
        if (!externalIntent.OwnsExternalIntent(lease))
        {
            externalIntent.RejectStaleCompletion();
            return false;
        }

        return EndOwnedExternalIntent(
            clearFailures: false,
            CharacterAiActionTerminalKind.Failed);
    }

    public bool CancelExternallyDrivenAction(
        in CharacterActionIntentLease lease)
    {
        if (!externalIntent.OwnsExternalIntent(lease))
        {
            externalIntent.RejectStaleCompletion();
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
        if (!externalIntent.ExternallyDrivenActionActive
            || !string.Equals(
                externalIntent.ExternalIntentOwnerId,
                ownerId,
                StringComparison.Ordinal))
        {
            externalIntent.RejectStaleCompletion();
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
        if (terminalKind != CharacterAiActionTerminalKind.Failed)
        {
            operationObservation.ClearCurrentOperationBlock();
        }
        bool shouldClearFailures = externalIntent.End(clearFailures, terminalKind);
        RequestImmediateReplan(shouldClearFailures);
        return true;
    }

    public void NotifyActionStarted()
    {
        if (bestAction == null || bestAction.actionset == null)
        {
            return;
        }

        if (executionLedger.ActionEpochLive)
        {
            lastInvariantAnomalyDetail = string.IsNullOrWhiteSpace(
                    pendingLiveEpochReplacementDetail)
                ? $"start-over-live-epoch; epoch={executionLedger.CurrentActionEpoch}; "
                  + $"branch={executionLedger.CurrentActionBranch}; action={currentActionDebugLabel}; "
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
        executionLedger.NoteActionStarted(bestAction.actionset);
        currentActionDebugLabel = GetActionLabel(bestAction.actionset);
        currentDestinationDebugLabel = AIBrainDebugFormatter.GetDestinationLabel(bestAction.destination);
        currentActionPhase = "\uC2DC\uC791";
        currentActionPhaseDetail = string.Empty;
        int startingActionId = bestAction.actionset.GetInstanceID();
        pendingLiveEpochReplacementDetail = string.Empty;
        executionLedger.BeginEpoch(startingActionId,
            bestAction.destination != null ? bestAction.destination.GetInstanceID() : 0,
            bestAction.actionset.Branch, CaptureTraceContext());
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
        if (!executionLedger.TryCloseEpoch(ref terminalKind, CaptureTraceContext())) return;
        if (terminalKind != CharacterAiActionTerminalKind.Failed) operationObservation.ClearCurrentOperationBlock();
        executionLedger.RecordTerminal(terminalKind, CaptureTraceContext());
    }

    public bool EndExpectedAction(
        AIAction expectedAction,
        CharacterAiActionTerminalKind terminalKind,
        bool clearFailures)
    {
        if (stopCallbackAction != null
            && ReferenceEquals(stopCallbackAction, expectedAction ?? bestAction))
            return false;
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
        // 종료 종류를 확정한 뒤 다음 판단을 한 번 요청한다.
        decisionPending = true;
        RequestImmediateReplan(clearFailures: false);
        return true;
    }

    private void ClearFailureForCompletedAction(AIAction completedAction)
    {
        operationObservation.ClearCurrentOperationBlockFor(completedAction);
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
            PublishDiagnostic(
                CharacterAiDiagnosticTransition.DuplicateExecutionSuppressed,
                "duplicate-execution-suppressed",
                actionLabel,
                duplicateExecutionSuppressionCount);
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
        PublishDiagnostic(
            CharacterAiDiagnosticTransition.OrphanWorkActionRecovered,
            "orphan-work-action-recovered",
            lastOrphanWorkActionRecoveryDetail,
            orphanWorkActionRecoveryCount);
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
        RecordExecutorDeferredObservation(expectedAction, reason);
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
            InvokeActionStop(expectedAction, failure.Reason);
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
        operationObservation.ClearAllOperationDiagnostics();
        if (externalIntent.ExternallyDrivenActionActive)
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
            InvokeActionStop(actionToStop, reason ?? "lifecycle-transition");
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
        externalIntent.ResetLifecyclePresence();
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
            PublishDiagnostic(
                CharacterAiDiagnosticTransition.InteractionActionReplaced,
                "interaction-action-replaced",
                lastInteractionActionReplacementDetail,
                interactionActionReplacementCount);
        }

        MarkDebugDirty();
    }

    public void SetActionPhase(string phase, BuildableObject destination = null, string detail = null)
    {
        operationObservation.RetireForDifferentDestination(destination);
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
        executionLedger.SetRuntimePhaseForPresentation(nextPhase);
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
        if (externalIntent.ExternallyDrivenActionActive)
        {
            return false;
        }

        AIAction actionToStop = bestAction;
        AIAction queuedActionToClear = queuedAction;
        if (actionToStop == null && queuedActionToClear == null)
        {
            return false;
        }

        operationObservation.ClearCurrentOperationBlock();
        interruptedReplanCount++;
        lastInterruptedReplanDetail =
            $"caller={caller}; reason={reason ?? string.Empty}; "
            + $"action={GetActionLabel(actionToStop?.actionset)}; "
            + $"phase={currentActionPhase}; "
            + $"destination={AIBrainDebugFormatter.GetDestinationLabel(actionToStop?.destination)}";

        InvokeActionStop(actionToStop, reason);
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
            PublishDiagnostic(
                CharacterAiDiagnosticTransition.Replan,
                "replan",
                reason);
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
        if (externalIntent.ExternallyDrivenActionActive || bestAction == null)
        {
            return false;
        }

        AIAction actionToSuspend = bestAction;
        AIAction queuedActionToClear = queuedAction;
        operationObservation.ClearCurrentOperationBlock();
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
        operationObservation.ClearCurrentOperationBlock();
        if (executionLedger.ActionEpochLive)
        {
            pendingLiveEpochReplacementDetail =
                $"selected-over-live-epoch; epoch={executionLedger.CurrentActionEpoch}; "
                + $"oldBranch={executionLedger.CurrentActionBranch}; oldAction={currentActionDebugLabel}; "
                + $"oldDefinition={executionLedger.CurrentActionDefinitionId}; "
                + $"oldDestination={executionLedger.CurrentActionDestinationId}; "
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
        executionLedger.ClearCommittedPathSearchDeferral();
        bestAction = action;
        executionLedger.SetSelectedDefinition(
            action?.actionset != null ? action.actionset.GetInstanceID() : 0,
            action?.destination != null ? action.destination.GetInstanceID() : 0,
            action?.actionset?.Branch ?? CharacterAiBranch.None);
        currentActionDebugLabel = GetActionLabel(action?.actionset);
        currentActionPhase = phase ?? string.Empty;
        executionLedger.SetRuntimePhase(CharacterAiRuntimePhase.Selected);
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
        if (preferredAction.PreferredNextActionSet == null)
        {
            preferredAction.ClearDeferredPending();
            return false;
        }

        if (Now > preferredAction.PreferredNextActionUntil)
        {
            preferredAction.ClearPreferredAction();
            return false;
        }

        AIAction action = availableActions?
            .FirstOrDefault(candidate => candidate?.actionset == preferredAction.PreferredNextActionSet);
        if (action == null)
        {
            preferredAction.ClearPreferredAction();
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
        preferredAction.RecordCommitted(action.actionset.Branch);
        preferredAction.ClearPreferredAction(preserveWorkType: action.actionset is AIWork);
        isBestActionEnd = false;
        isExecuted = false;
        return true;
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

    private void RecordActionFailure(
        AIActionSet actionSet,
        AIActionFailure failure,
        DomainFailure domainFailure = default,
        CharacterOperationBlockAxis axis = CharacterOperationBlockAxis.Unknown,
        Func<int> sourceRevisionReader = null,
        int sourceRevision = 0)
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
        if (executionLedger.ActionEpochLive
            && bestAction?.actionset == actionSet)
        {
            if (RecordCurrentOperationBlock(
                actionSet,
                lastActionFailure,
                domainFailure,
                axis,
                CharacterAiRetryKind.FailureCooldown,
                usesCooldown: true,
                recordHistory: true))
            {
                operationObservation.BindSourceRevision(
                    sourceRevisionReader, sourceRevision);
            }
        }
        TrackExecutionFailure(actionSet, lastActionFailure.Kind, isNoAction: false);
        actor?.AiMemory?.RecordDecision(
            CharacterAiBranch.InterruptCheck,
            CharacterAiUtilityText.GetIntention(actionSet.Branch),
            lastActionFailure.ToString(),
            -0.25f);
        actor?.Blackboard?.ReportActionFailure(actionSet, lastActionFailure);
        PublishDiagnostic(
            CharacterAiDiagnosticTransition.ActionFailed,
            lastActionFailure.Kind.ToString(),
            $"{GetActionLabel(actionSet)} - {lastActionFailure}");
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
        PublishDiagnostic(
            CharacterAiDiagnosticTransition.NoAction,
            "no-action",
            "\uC2E4\uD589 \uAC00\uB2A5\uD55C \uD589\uB3D9 \uC5C6\uC74C");
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

        failureDiagnostics.RecordCandidateRejection(failure.Kind);

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
        failureDiagnostics.BeginJobGiverEvaluationPass();
    }

    internal void RecordJobGiverEvaluationRejection(
        CharacterAiBranch branch,
        AIActionFailure failure,
        string reason)
    {
        AIActionFailureKind kind = failure.HasFailure
            ? failure.Kind
            : AIActionFailureKind.Unknown;
        failureDiagnostics.RecordJobGiverKind(branch, kind);

        // Deferred states are scheduler backpressure, not rejected decisions.
        // Keep their typed per-branch counters for diagnosis, but do not inflate
        // the user-facing rejection total or replace the current hard failure.
        if (failure.IsDeferred)
        {
            return;
        }

        failureDiagnostics.RecordHardJobGiverRejection(branch, kind,
            string.IsNullOrWhiteSpace(reason) ? failure.ToString() : reason);
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
        ReportRuntimeActionFailure(
            failure,
            DomainFailure.None,
            CharacterOperationBlockAxis.Unknown,
            requestImmediateReplan);
    }

    internal void ReportRuntimeActionFailure(
        AIActionFailure failure,
        DomainFailure domainFailure,
        CharacterOperationBlockAxis axis,
        bool requestImmediateReplan)
    {
        ReportRuntimeActionFailure(
            failure,
            domainFailure,
            axis,
            sourceRevisionReader: null,
            sourceRevision: 0,
            requestImmediateReplan: requestImmediateReplan);
    }

    internal void ReportRuntimeActionFailure(
        AIActionFailure failure,
        DomainFailure domainFailure,
        CharacterOperationBlockAxis axis,
        Func<int> sourceRevisionReader,
        int sourceRevision,
        bool requestImmediateReplan)
    {
        AIActionSet actionSet = bestAction?.actionset;
        if (actionSet != null)
        {
            RecordActionFailure(
                actionSet,
                failure,
                domainFailure,
                axis,
                sourceRevisionReader,
                sourceRevision);
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
            PublishDiagnostic(
                CharacterAiDiagnosticTransition.ExecutionFailed,
                lastActionFailure.Kind.ToString(),
                lastActionFailure.ToString());
            MarkDebugDirty();
        }

        if (requestImmediateReplan)
        {
            RequestImmediateReplan(clearFailures: false);
        }
    }

    internal void PublishDiagnostic(
        CharacterAiDiagnosticTransition transition,
        string reasonCode,
        string detail,
        long count = 0L)
    {
        CacheLocalReferences();
        if (actor == null)
        {
            return;
        }

        actor.PublishAiDiagnostic(new CharacterAiDiagnosticEvent(
            actor,
            transition,
            reasonCode,
            detail,
            count));
    }

    public CharacterOperationBlockSnapshot CaptureCurrentOperationBlock()
    {
        return operationObservation.CaptureCurrentOperationBlock(
            actionEvaluator, executionLedger.ActionEpochLive,
            executionLedger.CurrentActionEpoch, bestAction);
    }

    public CharacterOperationBlockSnapshot CaptureRecentOperationFailure() =>
        operationObservation.RecentOperationFailure;

    internal void ClearOperationDiagnosticsForLifecycle() =>
        operationObservation.ClearAllOperationDiagnostics();

    private bool RecordCurrentOperationBlock(
        AIActionSet actionSet,
        AIActionFailure failure,
        DomainFailure domainFailure,
        CharacterOperationBlockAxis axis,
        CharacterAiRetryKind retryKind,
        bool usesCooldown,
        bool recordHistory)
    {
        return operationObservation.Record(
            actionSet, failure, domainFailure, axis, retryKind, usesCooldown,
            recordHistory, actor, bestAction, executionLedger.CurrentActionEpoch,
            actionEvaluator, gameClock);
    }

    private void RecordExecutorDeferredObservation(
        AIAction expectedAction,
        string reason)
    {
        if (expectedAction?.actionset == null)
        {
            return;
        }

        RecordCurrentOperationBlock(
            expectedAction.actionset,
            AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                string.IsNullOrWhiteSpace(reason)
                    ? "executor-deferred"
                    : reason,
                expectedAction.destination),
            DomainFailure.None,
            CharacterOperationBlockAxis.Unknown,
            CharacterAiRetryKind.ExecutorDeferred,
            usesCooldown: false,
            recordHistory: false);
    }







    public CharacterAiRuntimeDiagnosticsSnapshot CaptureRuntimeDiagnostics()
    {
        return new CharacterAiRuntimeDiagnosticsSnapshot(
            executionLedger.ActionStartCount,
            executionLedger.ActionSwitchCount,
            executionLedger.SameActionRestartCount,
            phaseTransitionCount,
            immediateReplanCount,
            interruptedReplanCount,
            lastInterruptedReplanDetail,
            failureDiagnostics.ExecutionFailureCount,
            failureDiagnostics.NoActionFailureCount,
            failureDiagnostics.CandidateRejectionCount,
            duplicateExecutionSuppressionCount,
            interactionActionReplacementCount,
            lastInteractionActionReplacementDetail,
            protectedRunningActionReplanCount,
            lastProtectedRunningActionReplanDetail,
            orphanWorkActionRecoveryCount,
            lastOrphanWorkActionRecoveryDetail,
            failureDiagnostics.CurrentRepeatedFailureCount,
            failureDiagnostics.PeakRepeatedFailureCount,
            failureDiagnostics.RepeatedFailureKind,
            failureDiagnostics.LastExecutionFailureDetail,
            failureDiagnostics.CopyExecutionFailures(),
            failureDiagnostics.CopyCandidateRejections(),
            failureDiagnostics.JobGiverEvaluationRejectionCount,
            failureDiagnostics.CopyJobGiverRejections(),
            CopyRuntimeTrace(),
            CaptureRuntimeGateSnapshot());
    }

    private void TrackExecutionFailure(
        AIActionSet actionSet,
        AIActionFailureKind kind,
        bool isNoAction)
    {
        failureDiagnostics.TrackExecutionFailure(actionSet, kind, isNoAction,
            lastActionFailure.HasFailure ? lastActionFailure.ToString() : kind.ToString(),
            actionFailureCooldown, Now, executionLedger, CaptureTraceContext());
    }

    private static int GetBranchIndex(CharacterAiBranch branch)
    {
        int index = (int)branch;
        return index >= 0 && index < BranchCount ? index : 0;
    }

    private CharacterAiBranch ResolveRuntimeBranch()
    {
        CharacterAiBranch branch = bestAction?.actionset?.Branch
            ?? executionLedger.CurrentActionBranch;
        return GetBranchIndex(branch) == 0
            ? CharacterAiBranch.None
            : branch;
    }

    private void AdvanceRuntimeProgress(long progressMilli = 0L)
    {
        executionLedger.AdvanceRuntimeProgress(CaptureTraceContext(), progressMilli);
    }

    private void AdvanceGameplayProgress(long progressMilli = 0L)
    {
        executionLedger.AdvanceGameplayProgress(CaptureTraceContext(), progressMilli);
    }

    public void NotifyMovementStarted(int stepCount)
    {
        executionLedger.NotifyMovementStarted(CaptureTraceContext(), stepCount);
    }

    public void NotifyMovementProgress(int stepIndex, int stepCount)
    {
        executionLedger.NotifyMovementProgress(CaptureTraceContext(), stepIndex, stepCount);
    }

    /// <summary>
    /// Records real domain work without treating scheduler bookkeeping as
    /// progress. Long-running non-AbilityWork actions such as rescue use this
    /// so the watchdog observes WU advancement instead of phase text changes.
    /// </summary>
    public void NotifyGameplayWorkProgress(float completedWu)
    {
        executionLedger.NotifyGameplayWorkProgress(CaptureTraceContext(), completedWu);
    }

    public void NotifyFacilityQueueHeartbeat(int queuePosition)
    {
        executionLedger.NotifyFacilityQueueHeartbeat(CaptureTraceContext(), queuePosition);
    }

    public void NotifyFacilityServiceHeartbeat()
    {
        executionLedger.NotifyFacilityServiceHeartbeat(CaptureTraceContext());
    }

    public void NotifyMovementTerminal(GridMoveFailureReason failureReason)
    {
        executionLedger.NotifyMovementTerminal(CaptureTraceContext(), failureReason);
    }

    public int NotifyPathRequested(
        bool repath,
        CharacterAiBranch branch = CharacterAiBranch.None)
    {
        return executionLedger.NotifyPathRequested(CaptureTraceContext(), repath, branch);
    }

    public void NotifyPathResult(
        int requestId,
        CharacterAiPathTraceState state,
        int stepCount)
    {
        executionLedger.NotifyPathResult(CaptureTraceContext(), requestId, state, stepCount);
    }

    public void NotifyReservationAcquired(AIAction action)
    {
        if (action == null || !action.HasReservation) return;
        executionLedger.NotifyReservationAcquired(action.actionset?.Branch ?? ResolveRuntimeBranch(),
            GetReservationDiagnosticId(action), CaptureTraceContext());
    }

    public void NotifyReservationReleased(AIAction action)
    {
        if (action == null) return;
        executionLedger.NotifyReservationReleased(action.actionset?.Branch ?? ResolveRuntimeBranch(),
            GetReservationDiagnosticId(action), CaptureTraceContext());
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

    public void NotifyRetryScheduled(float delaySeconds) =>
        NotifyRetryScheduled(delaySeconds, CharacterAiRetryKind.DecisionRetry);

    public void NotifyRetryScheduled(
        float delaySeconds,
        CharacterAiRetryKind retryKind)
    {
        executionLedger.NotifyRetryScheduled(CaptureTraceContext(), delaySeconds, retryKind);
    }

    public void NotifyRetryAttempted()
    {
        executionLedger.NotifyRetryAttempted(CaptureTraceContext());
    }

    public void NotifySchedulerDecisionProcessed(
        float dueTime,
        float processedAt,
        bool decided,
        bool retryScheduled)
    {
        executionLedger.NotifySchedulerDecisionProcessed(CaptureTraceContext(), dueTime, processedAt, decided, retryScheduled);
        AuditRuntimeInvariants();
    }

    public void AuditRuntimeInvariants()
    {
        CharacterAiRuntimeInvariant mask = CharacterAiRuntimeInvariant.None;
        if (executionLedger.ActionEpochLive && bestAction == null)
            mask |= CharacterAiRuntimeInvariant.LiveEpochWithoutAction;
        if (HasRunningAction && !executionLedger.ActionEpochLive)
            mask |= CharacterAiRuntimeInvariant.RunningWithoutEpoch;
        if (isExecuted
            && bestAction == null
            && !externalIntent.ManualCommandActive
            && !externalIntent.ExternallyDrivenActionActive)
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
        executionLedger.AuditRuntimeInvariants(mask, actualReservations, CaptureTraceContext());
    }

    private void RecordInvariantAnomaly(CharacterAiRuntimeInvariant invariant)
    {
        executionLedger.RecordInvariantAnomaly(CaptureTraceContext(), invariant);
    }

    public CharacterAiRuntimeGateSnapshot CaptureRuntimeGateSnapshot()
    {
        return executionLedger.CaptureRuntimeGateSnapshot(failureDiagnostics.FailureLoopCount);
    }

    private CharacterAiTraceContext CaptureTraceContext()
    {
        AIAction action = bestAction;
        return new CharacterAiTraceContext(Now,
            action?.actionset != null ? action.actionset.GetInstanceID() : (int?)null,
            action?.destination != null ? action.destination.GetInstanceID() : (int?)null,
            action?.actionset?.Branch,
            string.IsNullOrEmpty(currentActionPhase) ? 0 : StringComparer.Ordinal.GetHashCode(currentActionPhase));
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
        executionLedger.RecordRuntimeTrace(CaptureTraceContext(), kind, failureKind, terminalKind, pathState, reservationState, invariant, pathRequestId, pathStepIndex, pathStepCount, reservationId, retryAttempt, delayMilliseconds, progressMilli);
    }

    private CharacterAiRuntimeTraceEvent[] CopyRuntimeTrace()
    {
        return executionLedger.CopyRuntimeTrace();
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
