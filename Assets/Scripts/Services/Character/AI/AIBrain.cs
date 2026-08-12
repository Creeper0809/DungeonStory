using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using VContainer;
public class AIBrain : CharacterAbility
{
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
    private readonly List<AIAction> destinationFailedThisDecision = new List<AIAction>();
    private readonly List<AIActionDebugCandidate> lastCandidateScores = new List<AIActionDebugCandidate>();
    private IReadOnlyList<AIActionDebugCandidate> lastCandidateScoresView;
    private AIActionSet preferredNextActionSet;
    private float preferredNextActionUntil = float.NegativeInfinity;
    private WorkTypeId preferredWorkTypeId;
    private float preferredWorkTypeUntil = float.NegativeInfinity;
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
    private long executionFailureCount;
    private long noActionFailureCount;
    private long candidateRejectionCount;
    private long jobGiverEvaluationRejectionCount;
    private long currentRepeatedFailureCount;
    private long peakRepeatedFailureCount;
    private AIActionFailureKind repeatedFailureKind;
    private AIActionSet repeatedFailureActionSet;
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
    public bool IsPathSearchDeferred => pathSearchSession?.IsDeferred == true;
    public bool IsActionScoringPending => candidateSelector?.IsPending == true;

    internal bool IsActionScoringPendingFor(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext = true)
    {
        return candidateSelector?.IsPendingFor(
            predicate,
            hasDecisionContext) == true;
    }
    public bool IsManualCommandActive => manualCommandActive;
    public bool IsExternallyDrivenActionActive => externallyDrivenActionActive;
    public string ExternalIntentOwnerId => externalIntentOwnerId;
    public CharacterActionIntentKind ExternalIntentKind => externalIntentKind;
    public long ExternalIntentEpoch => externalIntentEpoch;
    public int ExternalIntentTransitionCount => externalIntentTransitionCount;
    public int ExternalIntentPreemptionCount => externalIntentPreemptionCount;
    public int ExternalIntentRejectedCount => externalIntentRejectedCount;
    public int ExternalIntentStaleCompletionCount => externalIntentStaleCompletionCount;
    public long RuntimeActionStartCount => actionStartCount;
    public long RuntimeActionSwitchCount => actionSwitchCount;
    public long RuntimePhaseTransitionCount => phaseTransitionCount;
    public long RuntimeImmediateReplanCount => immediateReplanCount;
    public long RuntimeExecutionFailureCount => executionFailureCount;
    public long RuntimeCandidateRejectionCount => candidateRejectionCount;
    public long RuntimeJobGiverEvaluationRejectionCount =>
        jobGiverEvaluationRejectionCount;
    public long RuntimePeakRepeatedFailureCount => peakRepeatedFailureCount;
    public AIActionFailureKind RuntimeRepeatedFailureKind => repeatedFailureKind;
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

        RequireActionEvaluator().ClearEvaluations();
        facilityScoreCache.Clear();

        if (TryUsePreferredAction())
        {
            return true;
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

        SetSelectedAction(action, "\uC120\uD0DD");
        if (action.actionset == preferredNextActionSet)
        {
            preferredNextActionSet = null;
            preferredNextActionUntil = float.NegativeInfinity;
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
    }

    public void RequestImmediateReplan(bool clearFailures = false)
    {
        if (externallyDrivenActionActive)
        {
            externalReplanClearFailures |= clearFailures;
            return;
        }

        immediateReplanCount++;

        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
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
        preferredNextActionSet = actionSet;
        preferredNextActionUntil = preferredNextActionSet != null
            ? Now + Mathf.Max(2f, persistenceSeconds)
            : float.NegativeInfinity;
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
            EndOwnedExternalIntent(clearFailures: true);
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
        pathSearchSession?.Clear();
        RequireCandidateSelector().Reset();
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
        externalIntentTransitionCount++;
        if (preempting)
        {
            externalIntentPreemptionCount++;
        }
        externalReplanClearFailures = false;
        isExecuted = true;
        isBestActionEnd = false;
        pathSearchSession?.Clear();
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
        currentActionDebugLabel = string.IsNullOrWhiteSpace(actionLabel)
            ? "외부 행동"
            : actionLabel;
        currentActionPhase = phase ?? string.Empty;
        currentActionPhaseDetail = detail ?? string.Empty;
        currentDestinationDebugLabel = string.Empty;
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

        return EndOwnedExternalIntent(clearFailures);
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

        return EndOwnedExternalIntent(clearFailures);
    }

    private bool EndOwnedExternalIntent(bool clearFailures)
    {
        bool shouldClearFailures =
            clearFailures || externalReplanClearFailures;
        externallyDrivenActionActive = false;
        externalIntentOwnerId = string.Empty;
        externalIntentKind = CharacterActionIntentKind.None;
        externalReplanClearFailures = false;
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

        bestAction.BindClock(gameClock);
        bestAction.MarkStarted(Now);
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
        ResetRepeatedExecutionFailureStreak();
        currentActionDebugLabel = GetActionLabel(bestAction.actionset);
        currentDestinationDebugLabel = AIBrainDebugFormatter.GetDestinationLabel(bestAction.destination);
        currentActionPhase = "\uC2DC\uC791";
        currentActionPhaseDetail = string.Empty;
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

    public void SetActionPhase(string phase, BuildableObject destination = null, string detail = null)
    {
        string nextPhase = phase ?? string.Empty;
        string nextDestination = destination != null
            ? AIBrainDebugFormatter.GetDestinationLabel(destination)
            : currentDestinationDebugLabel;
        if (!string.Equals(currentActionPhase, nextPhase, StringComparison.Ordinal)
            || !string.Equals(currentDestinationDebugLabel, nextDestination, StringComparison.Ordinal))
        {
            phaseTransitionCount++;
        }
        currentActionPhase = nextPhase;
        currentActionPhaseDetail = detail ?? string.Empty;
        if (destination != null)
        {
            currentDestinationDebugLabel = nextDestination;
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

    public bool StopCurrentActionForReplan(string reason)
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

        actionToStop?.actionset?.OnStop(actor, actionToStop, reason);
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

    private void SetSelectedAction(AIAction action, string phase)
    {
        bestAction = action;
        currentActionDebugLabel = GetActionLabel(action?.actionset);
        currentActionPhase = phase ?? string.Empty;
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

    private bool TryUsePreferredAction()
    {
        if (preferredNextActionSet == null)
        {
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
            if (!failure.IsDeferred)
            {
                ClearPreferredAction();
            }

            return false;
        }

        SetSelectedAction(action, "예약된 다음 행동");
        ClearPreferredAction(preserveWorkType: action.actionset is AIWork);
        isBestActionEnd = false;
        isExecuted = false;
        return true;
    }

    private void ClearPreferredAction(bool preserveWorkType = false)
    {
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

        RequireActionEvaluator().StartCooldown(actionSet, actionFailureCooldown);
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
            RequireActionEvaluator().StartCooldown(action.actionset, actionFailureCooldown);
        }
    }

    private void ReleaseFinishedActionReservation()
    {
        if (bestAction == null || !isBestActionEnd)
        {
            return;
        }

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

    public CharacterAiRuntimeDiagnosticsSnapshot CaptureRuntimeDiagnostics()
    {
        return new CharacterAiRuntimeDiagnosticsSnapshot(
            actionStartCount,
            actionSwitchCount,
            sameActionRestartCount,
            phaseTransitionCount,
            immediateReplanCount,
            interruptedReplanCount,
            executionFailureCount,
            noActionFailureCount,
            candidateRejectionCount,
            currentRepeatedFailureCount,
            peakRepeatedFailureCount,
            repeatedFailureKind,
            (long[])executionFailuresByKind.Clone(),
            (long[])candidateRejectionsByKind.Clone(),
            jobGiverEvaluationRejectionCount,
            (long[])jobGiverEvaluationRejectionsByBranchAndKind.Clone());
    }

    private void TrackExecutionFailure(
        AIActionSet actionSet,
        AIActionFailureKind kind,
        bool isNoAction)
    {
        executionFailureCount++;
        if (isNoAction)
        {
            noActionFailureCount++;
        }
        IncrementFailureKind(executionFailuresByKind, kind);

        if (kind == repeatedFailureKind && actionSet == repeatedFailureActionSet)
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
    }

    private void ResetRepeatedExecutionFailureStreak()
    {
        currentRepeatedFailureCount = 0L;
        repeatedFailureKind = AIActionFailureKind.None;
        repeatedFailureActionSet = null;
    }

    private static void IncrementFailureKind(long[] counts, AIActionFailureKind kind)
    {
        int index = (int)kind;
        if (index > 0 && index < counts.Length)
        {
            counts[index]++;
        }
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
