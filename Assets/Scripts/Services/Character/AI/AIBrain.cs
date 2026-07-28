using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using Unity.Profiling;
using VContainer;

public class AIBrain : CharacterAbility
{
    private static readonly ProfilerMarker ActionScoringMarker =
        new ProfilerMarker("CharacterAi.ActionScoring");
    private static readonly ProfilerMarker ActionPrepareMarker =
        new ProfilerMarker("CharacterAi.ActionPrepare");
    private static readonly ProfilerMarker ActionConsiderationsMarker =
        new ProfilerMarker("CharacterAi.ActionConsiderations");
    private const string WaitActionPath = "SO/AI/Action/Wait";
    private const string WorkActionPath = "SO/AI/Action/Work";
    private const string EatActionPath = "SO/AI/Action/Eat";
    private const string RestActionPath = "SO/AI/Action/Rest";
    private const string ToiletActionPath = "SO/AI/Action/Toilet";
    private const string HygieneActionPath = "SO/AI/Action/Hygiene";
    private const string ShoppingActionPath = "SO/AI/Action/Shopping";
    private const string LookAroundActionPath = "SO/AI/Action/LookAround";
    private const string ExitDungeonActionPath = "SO/AI/Action/ExitDungeon";
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
    private GridPathSearchResult pathSearchCache;
    private bool urgentPathSearchRequested;
    private readonly Dictionary<AIActionSet, float> actionFailureCooldownUntil = new Dictionary<AIActionSet, float>();
    private readonly Dictionary<AIAction, ActionEvaluation> actionEvaluationCache =
        new Dictionary<AIAction, ActionEvaluation>();
    private readonly Dictionary<(BuildableObject Building, FacilityRole Role), float>
        facilityScoreCache =
            new Dictionary<(BuildableObject Building, FacilityRole Role), float>();
    private readonly ActionScoringContinuation reusableActionScoringContinuation =
        new ActionScoringContinuation();
    private ActionScoringContinuation actionScoringContinuation;
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
    private int directDecisionTickCount;
    public bool IsPathSearchDeferred { get; private set; }
    public bool IsActionScoringPending => actionScoringContinuation != null;
    public bool IsManualCommandActive => manualCommandActive;
    public bool IsExternallyDrivenActionActive => externallyDrivenActionActive;
    public AIActionFailure LastActionFailure => lastActionFailure;
    public IReadOnlyList<AIActionDebugCandidate> LastCandidateScores =>
        lastCandidateScoresView ??= ReadOnlyView.List(lastCandidateScores);
    public string CurrentActionDebugLabel => currentActionDebugLabel;
    public string CurrentActionPhase => currentActionPhase;
    public string CurrentActionPhaseDetail => currentActionPhaseDetail;
    public string CurrentDestinationDebugLabel => currentDestinationDebugLabel;
    internal ICharacterAiPerformanceRecorder PerformanceRecorder => performanceRecorder;
    public int DebugVersion { get; private set; }
    private float Now => gameClock != null ? gameClock.Time : 0f;

    private readonly struct ActionEvaluation
    {
        public ActionEvaluation(
            bool canConsider,
            AIActionFailure failure,
            BuildableObject destination)
        {
            CanConsider = canConsider;
            Failure = failure;
            Destination = destination;
        }

        public bool CanConsider { get; }
        public AIActionFailure Failure { get; }
        public BuildableObject Destination { get; }
    }

    private sealed class ActionScoringContinuation
    {
        public Predicate<AIActionSet> Predicate;
        public bool HasDecisionContext;
        public int NextActionIndex;
        public AIAction BestCandidate;
        public float BestScore = float.MinValue;
        public AIActionFailure BestFailure = AIActionFailure.Create(
            AIActionFailureKind.NoAction,
            "일치하는 AI 행동이 없습니다.");

        public void Reset(
            Predicate<AIActionSet> predicate,
            bool hasDecisionContext)
        {
            Predicate = predicate;
            HasDecisionContext = hasDecisionContext;
            NextActionIndex = 0;
            BestCandidate = null;
            BestScore = float.MinValue;
            BestFailure = AIActionFailure.Create(
                AIActionFailureKind.NoAction,
                "선택할 수 있는 AI 행동이 없습니다.");
        }

        public bool Matches(
            Predicate<AIActionSet> predicate,
            bool hasDecisionContext)
        {
            if (HasDecisionContext != hasDecisionContext
                || Predicate == null
                || predicate == null)
            {
                return false;
            }

            return ReferenceEquals(Predicate, predicate)
                || (ReferenceEquals(Predicate.Target, predicate.Target)
                    && Predicate.Method == predicate.Method);
        }
    }

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

        if (actionScoringContinuation == null)
        {
            directDecisionTickCount++;
            actor.Blackboard?.BeginDecisionTrace(directDecisionTickCount);
            actor.Blackboard?.ClearJobGiverCandidateCache();
            actionEvaluationCache.Clear();
            facilityScoreCache.Clear();
        }

        return decisionPipeline.RunRootDecision(actor);
    }

    public bool HasResumableDecisionPipeline =>
        actor != null && decisionPipeline != null;

    [Inject]
    public void ConstructAIBrain(
        ICharacterAiActionAssetCatalog actionAssetCatalog,
        ICharacterAiSchedulingService aiSchedulingService,
        ISocialReputationBiasService socialReputationBiasService,
        IFacilityCandidateCache facilityCandidateCache,
        ICharacterAiFacilityLookup facilityLookup,
        ICharacterAiJobGiverCatalog jobGiverCatalog,
        ICharacterAiDecisionPipeline decisionPipeline,
        IRoomFacilityPolicy roomFacilityPolicy,
        IGridPathSearchBroker pathSearchBroker,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        ICharacterAiPerformanceRecorder performanceRecorder = null)
    {
        this.actionAssetCatalog = actionAssetCatalog
            ?? throw new ArgumentNullException(nameof(actionAssetCatalog));
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.facilityLookup = facilityLookup
            ?? throw new ArgumentNullException(nameof(facilityLookup));
        this.jobGiverCatalog = jobGiverCatalog
            ?? throw new ArgumentNullException(nameof(jobGiverCatalog));
        this.decisionPipeline = decisionPipeline
            ?? throw new ArgumentNullException(nameof(decisionPipeline));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.performanceRecorder = performanceRecorder;
        actionRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-ai");
        facilityScoringContext = new FacilityScoringContext(
            socialReputationBiasService,
            roomFacilityPolicy);
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
        List<AIAction> actions = new List<AIAction>();
        AddRequiredAction(actions, WorkActionPath, CharacterAiBranch.Work);
        AddRuntimeRescueAction(actions);
        AddRuntimeHaulAction(actions);
        AddRuntimeHuntAction(actions);
        AddRuntimeSubstanceUseAction(actions);
        AddRequiredAction(actions, EatActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, RestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, ToiletActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddRequiredAction(actions, WaitActionPath, CharacterAiBranch.Wait);
        availableActions = actions.ToArray();
        BindActionClocks();
    }

    public void UseStaffWorkActions()
    {
        List<AIAction> actions = availableActions != null
            ? availableActions
                .Where(action => action?.actionset != null
                    && action.actionset.Branch != CharacterAiBranch.ExitDungeon)
                .ToList()
            : new List<AIAction>();
        AddRequiredAction(actions, WorkActionPath, CharacterAiBranch.Work);
        AddRequiredAction(actions, WaitActionPath, CharacterAiBranch.Wait);
        AddRequiredAction(actions, EatActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, RestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, ToiletActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddRuntimeRescueAction(actions);
        AddRuntimeHaulAction(actions);
        AddRuntimeHuntAction(actions);
        AddRuntimeSubstanceUseAction(actions);
        availableActions = actions.ToArray();
        BindActionClocks();
        isBestActionEnd = true;
        ClearPathSearchCache();
    }

    private void AddRuntimeHaulAction(List<AIAction> actions)
    {
        if (actions == null
            || actions.Any(action => action?.actionset is AIHaul))
        {
            return;
        }

        AIHaul haul = ScriptableObject.CreateInstance<AIHaul>();
        haul.hideFlags = HideFlags.HideAndDontSave;
        haul.actionName = "운반";
        actions.Add(new AIAction
        {
            actionset = haul
        });
    }

    private void AddRuntimeRescueAction(List<AIAction> actions)
    {
        if (actions == null
            || actions.Any(action => action?.actionset is AIRescue))
        {
            return;
        }

        AIRescue rescue = ScriptableObject.CreateInstance<AIRescue>();
        rescue.hideFlags = HideFlags.HideAndDontSave;
        rescue.actionName = "구조";
        actions.Add(new AIAction
        {
            actionset = rescue
        });
    }

    private void AddRuntimeHuntAction(List<AIAction> actions)
    {
        if (actions == null
            || actions.Any(action => action?.actionset is AIHunt))
        {
            return;
        }

        AIHunt hunt = ScriptableObject.CreateInstance<AIHunt>();
        hunt.hideFlags = HideFlags.HideAndDontSave;
        hunt.actionName = "사냥";
        actions.Add(new AIAction
        {
            actionset = hunt
        });
    }

    private void AddRuntimeSubstanceUseAction(List<AIAction> actions)
    {
        if (actions == null
            || actions.Any(action => action?.actionset is AISubstanceUse))
        {
            return;
        }

        AISubstanceUse substanceUse =
            ScriptableObject.CreateInstance<AISubstanceUse>();
        substanceUse.hideFlags = HideFlags.HideAndDontSave;
        substanceUse.actionName = "정책에 따라 복용";
        actions.Add(new AIAction
        {
            actionset = substanceUse
        });
    }

    private void NormalizeConfiguredActions()
    {
        if (availableActions == null)
        {
            Debug.LogError($"{name}: AI actions are not configured.");
            return;
        }

        AIAction[] configuredActions = availableActions
            .Where((action) => action != null && action.actionset != null)
            .ToArray();
        if (configuredActions.Length != availableActions.Length)
        {
            Debug.LogWarning($"{name}: Removed null AI action entries. Configure the missing actions on the prefab or asset.");
        }

        availableActions = configuredActions;
        BindActionClocks();
        if (availableActions.Length == 0)
        {
            Debug.LogError($"{name}: AI actions are empty after validation.");
        }
    }

    private void EnsureVisitorActions()
    {
        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return;
        }

        List<AIAction> actions = availableActions != null
            ? availableActions.ToList()
            : new List<AIAction>();
        AddRequiredAction(actions, EatActionPath, CharacterAiBranch.Eat);
        AddRequiredAction(actions, RestActionPath, CharacterAiBranch.Rest);
        AddRequiredAction(actions, ToiletActionPath, CharacterAiBranch.Toilet);
        AddRequiredAction(actions, HygieneActionPath, CharacterAiBranch.Hygiene);
        AddRequiredAction(actions, ShoppingActionPath, CharacterAiBranch.Shopping);
        AddRequiredAction(actions, LookAroundActionPath, CharacterAiBranch.LookAround);
        AddRequiredAction(actions, ExitDungeonActionPath, CharacterAiBranch.ExitDungeon);
        availableActions = actions.ToArray();
        BindActionClocks();
    }

    private void AddRequiredAction(
        List<AIAction> actions,
        string resourcePath,
        CharacterAiBranch branch)
    {
        if (actions.Any(action => action?.actionset != null && action.actionset.Branch == branch))
        {
            return;
        }

        actions.Add(new AIAction
        {
            actionset = RequireActionCatalog().GetRequiredAction(resourcePath, branch)
        });
    }

    private ICharacterAiActionAssetCatalog RequireActionCatalog()
    {
        return RuntimeDependency.Require(actionAssetCatalog, this);
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService()
    {
        return RuntimeDependency.Require(aiSchedulingService, this);
    }

    public FacilityScoringContext RequireFacilityScoringContext()
    {
        RuntimeDependency.Ensure(
            facilityScoringContext.IsConfigured,
            this,
            nameof(FacilityScoringContext));
        return facilityScoringContext;
    }

    public IFacilityCandidateCache RequireFacilityCandidateCache()
    {
        return RuntimeDependency.Require(facilityCandidateCache, this);
    }

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

    public ICharacterAiFacilityLookup RequireFacilityLookup()
    {
        return RuntimeDependency.Require(facilityLookup, this);
    }

    public ICharacterAiJobGiverCatalog RequireJobGiverCatalog()
    {
        return RuntimeDependency.Require(jobGiverCatalog, this);
    }

    public ICharacterAiDecisionPipeline RequireDecisionPipeline()
    {
        return RuntimeDependency.Require(decisionPipeline, this);
    }

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

        actionEvaluationCache.Clear();
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
        failure = AIActionFailure.None;
        AIAction action = candidate.Action;
        if (action == null || action.actionset == null)
        {
            failure = candidate.Failure.HasFailure
                ? candidate.Failure
                : AIActionFailure.Create(AIActionFailureKind.NoAction, "JobGiver candidate has no action.");
            actor?.Blackboard?.ReportActionFailure(null, failure);
            return false;
        }

        ReleaseFinishedActionReservation();
        if (actor == null
            || !actor.CanRunAi
            || availableActions == null
            || availableActions.Length == 0)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoAction, "No available AI actions.");
            actor?.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        if (!availableActions.Contains(action))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoAction, "JobGiver candidate is not registered in AIBrain.");
            actor?.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        actionEvaluationCache.Remove(action);
        if (!TryGetActionEvaluation(action, out ActionEvaluation evaluation))
        {
            failure = evaluation.Failure;
            RememberCandidateFailure(action, failure);
            actor?.Blackboard?.ReportActionFailure(action.actionset, failure);
            return false;
        }

        if (!action.SetResolvedDestinationWithFailure(
                actor,
                candidate.Destination ?? evaluation.Destination,
                out failure))
        {
            RememberCandidateFailure(action, failure);
            actor?.Blackboard?.ReportActionFailure(action.actionset, failure);
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
        actor?.Blackboard?.Commit(action, currentActionDebugLabel);
        MarkDebugDirty();
        return true;
    }

    public bool TryFindBestScoredAction(
        Predicate<AIActionSet> predicate,
        out CharacterAiActionCandidate candidate)
    {
        bool collectPerformance =
            performanceRecorder?.DetailedCollectionEnabled == true;
        long started = collectPerformance
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long allocatedAtStart = collectPerformance
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        try
        {
            using (ActionScoringMarker.Auto())
            {
            CharacterAiDecisionContext context = default;
            return TryFindBestScoredActionCore(
                predicate,
                false,
                in context,
                out candidate);
            }
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.ActionScoring,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency,
                    Math.Max(
                        0L,
                        GC.GetAllocatedBytesForCurrentThread()
                            - allocatedAtStart));
            }
        }
    }

    public bool TryFindBestScoredAction(
        Predicate<AIActionSet> predicate,
        in CharacterAiDecisionContext context,
        out CharacterAiActionCandidate candidate)
    {
        bool collectPerformance =
            performanceRecorder?.DetailedCollectionEnabled == true;
        long started = collectPerformance
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long allocatedAtStart = collectPerformance
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        try
        {
            using (ActionScoringMarker.Auto())
            {
            return TryFindBestScoredActionCore(
                predicate,
                true,
                in context,
                out candidate);
            }
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.ActionScoring,
                    (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency,
                    Math.Max(
                        0L,
                        GC.GetAllocatedBytesForCurrentThread()
                            - allocatedAtStart));
            }
        }
    }

    private bool TryFindBestScoredActionCore(
        Predicate<AIActionSet> predicate,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        out CharacterAiActionCandidate candidate)
    {
        candidate = default;
        if (predicate == null)
        {
            candidate = new CharacterAiActionCandidate(
                null,
                0f,
                AIActionFailure.Create(AIActionFailureKind.NoAction, "Action predicate is missing."),
                actor != null && actor.ShouldCollectDetailedAiDiagnostics
                    ? "Action predicate is missing."
                    : string.Empty);
            return false;
        }

        if (actor == null
            || !actor.CanRunAi
            || availableActions == null
            || availableActions.Length == 0)
        {
            AIActionFailure failure = AIActionFailure.Create(AIActionFailureKind.NoAction, "No available AI actions.");
            candidate = new CharacterAiActionCandidate(
                null,
                0f,
                failure,
                actor != null && actor.ShouldCollectDetailedAiDiagnostics
                    ? failure.ToString()
                    : string.Empty);
            return false;
        }

        if (actionScoringContinuation != null
            && !actionScoringContinuation.Matches(
                predicate,
                hasDecisionContext))
        {
            actionEvaluationCache.Clear();
            facilityScoreCache.Clear();
            actionScoringContinuation = null;
        }

        if (actionScoringContinuation == null)
        {
            reusableActionScoringContinuation.Reset(
                predicate,
                hasDecisionContext);
            actionScoringContinuation = reusableActionScoringContinuation;
        }

        ActionScoringContinuation continuation = actionScoringContinuation;
        double sliceMilliseconds = aiSchedulingService != null
            ? aiSchedulingService.GetDecisionWorkSliceMilliseconds(actor)
            : double.PositiveInfinity;
        long sliceStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        int processedThisSlice = 0;
        while (continuation.NextActionIndex < availableActions.Length)
        {
            AIAction action =
                availableActions[continuation.NextActionIndex];
            processedThisSlice++;
            if (action == null || action.actionset == null || !predicate(action.actionset))
            {
                continuation.NextActionIndex++;
                if (ShouldYieldActionScoringSlice(
                    sliceStarted,
                    sliceMilliseconds,
                    processedThisSlice))
                {
                    break;
                }

                continue;
            }

            if (!TryGetActionEvaluation(
                    action,
                    hasDecisionContext,
                    in context,
                    out ActionEvaluation evaluation))
            {
                action.score = 0f;
                AIActionFailure failure = evaluation.Failure;
                if (GetFailureDebugPriority(failure.Kind)
                    > GetFailureDebugPriority(continuation.BestFailure.Kind))
                {
                    continuation.BestFailure = failure;
                }

                if (failure.Kind == AIActionFailureKind.PathSearchDeferred)
                {
                    break;
                }

                continuation.NextActionIndex++;
                if (ShouldYieldActionScoringSlice(
                    sliceStarted,
                    sliceMilliseconds,
                    processedThisSlice))
                {
                    break;
                }

                continue;
            }

            continuation.NextActionIndex++;
            float selectionScore = GetSelectionScore(action);
            if (continuation.BestCandidate == null
                || selectionScore > continuation.BestScore)
            {
                continuation.BestCandidate = action;
                continuation.BestScore = selectionScore;
            }

            if (ShouldYieldActionScoringSlice(
                sliceStarted,
                sliceMilliseconds,
                processedThisSlice))
            {
                break;
            }
        }

        if (continuation.NextActionIndex < availableActions.Length)
        {
            AIActionFailure pending = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "행동 후보 평가 예산 대기");
            candidate = new CharacterAiActionCandidate(
                null,
                0f,
                pending,
                actor.ShouldCollectDetailedAiDiagnostics
                    ? $"{continuation.NextActionIndex}/{availableActions.Length}개 평가"
                    : string.Empty);
            return false;
        }

        AIAction bestCandidate = continuation.BestCandidate;
        float bestScore = continuation.BestScore;
        AIActionFailure bestFailure = continuation.BestFailure;
        actionScoringContinuation = null;
        if (bestCandidate == null)
        {
            candidate = new CharacterAiActionCandidate(
                null,
                0f,
                bestFailure,
                actor != null && actor.ShouldCollectDetailedAiDiagnostics
                    ? bestFailure.ToString()
                    : string.Empty);
            return false;
        }

        float score = Mathf.Max(0f, bestScore);
        candidate = new CharacterAiActionCandidate(
            bestCandidate,
            score,
            AIActionFailure.None,
            actor != null && actor.ShouldCollectDetailedAiDiagnostics
                ? $"{GetActionLabel(bestCandidate.actionset)} score={score:0.###}"
                : string.Empty,
            actionEvaluationCache.TryGetValue(bestCandidate, out ActionEvaluation bestEvaluation)
                ? bestEvaluation.Destination
                : null);
        return score > 0f;
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
                actionEvaluationCache.Remove(action);
            }
        }
    }

    private static bool ShouldYieldActionScoringSlice(
        long started,
        double sliceMilliseconds,
        int processedCount)
    {
        if (processedCount <= 0)
        {
            return false;
        }

        double elapsed =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        return elapsed >= Math.Max(0.05, sliceMilliseconds);
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

            if (!CanConsiderAction(action, out AIActionFailure failure))
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
        IsPathSearchDeferred = false;
        if (actor == null || !TryGetRuntimeGrid(out Grid grid)) return null;

        Vector2Int start = actor.GetNowXY();
        if (pathSearchCache == null ||
            pathSearchCache.sourceGrid != grid ||
            pathSearchCache.start != start ||
            pathSearchCache.traversalVersion != grid.TraversalVersion)
        {
            GridPathSearchPriority priority = urgentPathSearchRequested
                ? GridPathSearchPriority.Urgent
                : GridPathSearchPriority.Normal;
            if (!pathSearchBroker.TryGetSearch(
                    grid,
                    start,
                    out pathSearchCache,
                    priority,
                    GridTraversalContext.ForCharacter(actor)))
            {
                IsPathSearchDeferred = true;
                return null;
            }
        }

        urgentPathSearchRequested = false;
        return pathSearchCache;
    }

    public bool TryGetRuntimeGrid(out Grid resolvedGrid)
    {
        return TryGetGrid(out resolvedGrid);
    }

    public void ClearPathSearchCache()
    {
        pathSearchCache = null;
    }

    public void RequestImmediateReplan(bool clearFailures = false)
    {
        if (externallyDrivenActionActive)
        {
            externalReplanClearFailures |= clearFailures;
            return;
        }

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
        IsPathSearchDeferred = false;
        actionScoringContinuation = null;

        if (clearFailures)
        {
            actionFailureCooldownUntil.Clear();
            lastActionFailure = AIActionFailure.None;
            lastFailedActionSet = null;
            noActionLogCooldownUntil = 0f;
        }

        MarkDebugDirty();
        RequireAiSchedulingService().RequestImmediateDecision(actor);
    }

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
        IsPathSearchDeferred = false;
        actionScoringContinuation = null;
        MarkDebugDirty();
    }

    public void BeginExternallyDrivenAction(string actionLabel, string phase, string detail = null)
    {
        if (externallyDrivenActionActive)
        {
            UpdateExternallyDrivenAction(actionLabel, phase, detail);
            return;
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
        externalReplanClearFailures = false;
        isExecuted = true;
        isBestActionEnd = false;
        IsPathSearchDeferred = false;
        actionScoringContinuation = null;
        ClearPathSearchCache();
        MarkDebugDirty();
    }

    public void UpdateExternallyDrivenAction(
        string actionLabel,
        string phase,
        string detail = null)
    {
        if (!externallyDrivenActionActive)
        {
            BeginExternallyDrivenAction(actionLabel, phase, detail);
            return;
        }

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

    public void EndExternallyDrivenAction(bool clearFailures = true)
    {
        if (!externallyDrivenActionActive)
        {
            return;
        }

        bool shouldClearFailures =
            clearFailures || externalReplanClearFailures;
        externallyDrivenActionActive = false;
        externalReplanClearFailures = false;
        RequestImmediateReplan(shouldClearFailures);
    }

    public void NotifyActionStarted()
    {
        if (bestAction == null || bestAction.actionset == null)
        {
            return;
        }

        bestAction.BindClock(gameClock);
        bestAction.MarkStarted(Now);
        currentActionDebugLabel = GetActionLabel(bestAction.actionset);
        currentDestinationDebugLabel = GetDestinationLabel(bestAction.destination);
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
                $"{GetDestinationLabel(bestAction.destination)} 선택",
                0.1f);
        }
        MarkDebugDirty();
    }

    public void SetActionPhase(string phase, BuildableObject destination = null, string detail = null)
    {
        currentActionPhase = phase ?? string.Empty;
        currentActionPhaseDetail = detail ?? string.Empty;
        if (destination != null)
        {
            currentDestinationDebugLabel = GetDestinationLabel(destination);
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
        stopReason = string.Empty;
        if (actor == null || bestAction == null || bestAction.actionset == null)
        {
            return false;
        }

        AIActionSet runningActionSet = bestAction.actionset;
        if (!runningActionSet.IsContinuous || !bestAction.HasStarted)
        {
            return false;
        }

        if (!runningActionSet.CanContinue(actor, bestAction, out stopReason))
        {
            if (string.IsNullOrWhiteSpace(stopReason))
            {
                stopReason = "Current action can no longer continue.";
            }
            return true;
        }

        if (bestAction.RunningSeconds < GetMinimumPersistenceSeconds(runningActionSet))
        {
            return false;
        }

        if (CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(actor, bestAction, out stopReason))
        {
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(CharacterAiInterruptReason.MoodImpulseChanged))
            {
                stopReason = "의도 유지 중이라 기분 충동을 잠시 보류";
                return false;
            }

            return true;
        }

        if (runningActionSet.CanInterrupt(actor, bestAction, out stopReason))
        {
            if (string.IsNullOrWhiteSpace(stopReason))
            {
                stopReason = "Current action requested interruption.";
            }

            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(CharacterAiInterruptReason.CurrentActionStopped))
            {
                stopReason = "의도 유지 중이라 행동 전환 보류";
                return false;
            }

            return true;
        }

        return false;
    }

    public bool CanInterruptCurrentActionForSurvivalEmergency(out string interruptReason)
    {
        interruptReason = string.Empty;
        if (actor == null || bestAction == null || bestAction.actionset == null)
        {
            return false;
        }

        AIActionSet runningActionSet = bestAction.actionset;
        if (!runningActionSet.IsContinuous
            || !bestAction.HasStarted
            || !runningActionSet.AllowsSurvivalEmergencyInterrupt
            || bestAction.RunningSeconds < GetMinimumPersistenceSeconds(runningActionSet))
        {
            return false;
        }

        if (actor.Blackboard != null
            && !actor.Blackboard.CanBreakCommitment(CharacterAiInterruptReason.SurvivalEmergency))
        {
            return false;
        }

        interruptReason = "생존 위기로 현재 행동 중단";
        return true;
    }

    public bool CanContinueCurrentAction(out string status)
    {
        status = string.Empty;
        if (actor == null || bestAction == null || bestAction.actionset == null)
        {
            status = "No running action.";
            return false;
        }

        AIActionSet runningActionSet = bestAction.actionset;
        if (!runningActionSet.IsContinuous)
        {
            status = $"{GetActionLabel(runningActionSet)} is not continuous.";
            return false;
        }

        if (!bestAction.HasStarted)
        {
            status = $"{GetActionLabel(runningActionSet)} has not started.";
            return false;
        }

        if (!runningActionSet.CanContinue(actor, bestAction, out string stopReason))
        {
            status = string.IsNullOrWhiteSpace(stopReason)
                ? "Current action can no longer continue."
                : stopReason;
            return false;
        }

        if (bestAction.RunningSeconds >= GetMinimumPersistenceSeconds(runningActionSet)
            && CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(actor, bestAction, out string moodReason))
        {
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(CharacterAiInterruptReason.MoodImpulseChanged))
            {
                status = "의도 유지 중";
                return true;
            }

            status = moodReason;
            return false;
        }

        if (bestAction.RunningSeconds >= GetMinimumPersistenceSeconds(runningActionSet)
            && runningActionSet.CanInterrupt(actor, bestAction, out string interruptReason))
        {
            status = string.IsNullOrWhiteSpace(interruptReason)
                ? "Current action requested interruption."
                : interruptReason;
            if (actor.Blackboard != null
                && !actor.Blackboard.CanBreakCommitment(CharacterAiInterruptReason.CurrentActionStopped))
            {
                status = "의도 유지 중";
                return true;
            }

            return false;
        }

        status = $"{GetActionLabel(runningActionSet)} running {bestAction.RunningSeconds:0.0}s";
        return true;
    }

    private bool TryGetActionEvaluation(
        AIAction action,
        out ActionEvaluation evaluation)
    {
        CharacterAiDecisionContext context = default;
        return TryGetActionEvaluation(
            action,
            false,
            in context,
            out evaluation);
    }

    private bool TryGetActionEvaluation(
        AIAction action,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        out ActionEvaluation evaluation)
    {
        if (action != null
            && actionEvaluationCache.TryGetValue(action, out ActionEvaluation cached))
        {
            evaluation = cached;
            return cached.CanConsider;
        }

        AIActionFailure failure;
        BuildableObject destination;
        bool canConsider;
        if (hasDecisionContext)
        {
            canConsider = CanConsiderAction(
                action,
                in context,
                out failure,
                out destination);
        }
        else
        {
            canConsider = CanConsiderAction(
                action,
                out failure,
                out destination);
        }
        evaluation = new ActionEvaluation(
            canConsider,
            failure,
            destination);
        if (action != null
            && failure.Kind != AIActionFailureKind.PathSearchDeferred)
        {
            actionEvaluationCache[action] = evaluation;
        }

        return canConsider;
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
        urgentPathSearchRequested = true;
        isExecuted = false;
        isBestActionEnd = true;
        IsPathSearchDeferred = false;
        actionScoringContinuation = null;
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
        currentActionPhaseDetail = GetPathDebugLabel(action);
        currentDestinationDebugLabel = GetDestinationLabel(action?.destination);
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

    private static string GetPathDebugLabel(AIAction action)
    {
        if (action == null)
        {
            return "\uACBD\uB85C \uC5C6\uC74C";
        }

        int stepCount = action.pathSteps.Count;
        return $"{action.planKind} / {stepCount}\uCE78";
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

        if (!CanUseAction(action, out AIActionFailure _))
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

        if (!CanUseAction(action, out AIActionFailure failure))
        {
            if (failure.Kind != AIActionFailureKind.PathSearchDeferred)
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
        interruptAction = null;
        interruptReason = string.Empty;
        if (runningAction == null
            || runningAction.actionset == null
            || availableActions == null)
        {
            return false;
        }

        int runningPriority = runningAction.actionset.InterruptPriority;
        float bestScore = float.MinValue;
        int bestPriority = int.MinValue;

        foreach (AIAction action in availableActions)
        {
            if (action == null
                || action == runningAction
                || action.actionset == null
                || action.actionset.InterruptPriority <= runningPriority)
            {
                continue;
            }

            if (!CanUseAction(action, out AIActionFailure _))
            {
                action.ReleaseReservation(actor);
                continue;
            }

            int priority = action.actionset.InterruptPriority;
            float score = GetSelectionScore(action);
            if (priority > bestPriority || (priority == bestPriority && score > bestScore))
            {
                if (interruptAction != null && interruptAction != action)
                {
                    interruptAction.ReleaseReservation(actor);
                }

                bestPriority = priority;
                bestScore = score;
                interruptAction = action;
            }
            else
            {
                action.ReleaseReservation(actor);
            }
        }

        if (interruptAction == null)
        {
            return false;
        }

        interruptReason = $"\uC0C1\uC704 \uD589\uB3D9: {GetActionLabel(interruptAction.actionset)}";
        return true;
    }

    private bool CanConsiderAction(AIAction action, out string failureReason)
    {
        bool result = CanConsiderAction(action, out AIActionFailure failure);
        failureReason = failure.ToString();
        return result;
    }

    private bool CanConsiderAction(AIAction action, out AIActionFailure failure)
    {
        return CanConsiderAction(action, out failure, out _);
    }

    private bool CanConsiderAction(
        AIAction action,
        out AIActionFailure failure,
        out BuildableObject destination)
    {
        CharacterAiDecisionContext context = default;
        return CanConsiderAction(
            action,
            false,
            in context,
            out failure,
            out destination);
    }

    private bool CanConsiderAction(
        AIAction action,
        in CharacterAiDecisionContext context,
        out AIActionFailure failure,
        out BuildableObject destination)
    {
        return CanConsiderAction(
            action,
            true,
            in context,
            out failure,
            out destination);
    }

    private bool CanConsiderAction(
        AIAction action,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        out AIActionFailure failure,
        out BuildableObject destination)
    {
        destination = null;
        failure = AIActionFailure.None;
        if (action == null || action.actionset == null)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoAction, "행동 없음");
            return false;
        }

        if (IsActionCoolingDown(action.actionset))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.Cooldown);
            return false;
        }

        long prepareStarted = performanceRecorder?.DetailedCollectionEnabled == true
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long prepareAllocatedAtStart = prepareStarted != 0L
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        bool prepared;
        using (ActionPrepareMarker.Auto())
        {
            prepared = hasDecisionContext
                ? action.actionset.TryPrepareCandidate(
                    actor,
                    in context,
                    null,
                    out destination,
                    out failure)
                : action.actionset.TryPrepareCandidate(
                    actor,
                    null,
                    out destination,
                    out failure);
        }
        if (prepareStarted != 0L)
        {
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - prepareStarted)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
            performanceRecorder.Record(
                AiPerformanceCategory.ActionPrepare,
                elapsedMilliseconds,
                Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread()
                        - prepareAllocatedAtStart));
            CharacterAiSlowOperationTrace.Record(
                "prepare",
                actor,
                action.actionset,
                null,
                elapsedMilliseconds);
        }

        if (!prepared)
        {
            if (!failure.HasFailure)
            {
                failure = AIActionFailure.Create(AIActionFailureKind.CannotStart);
            }

            failure = RefineActionFailure(action, failure);
            return false;
        }

        long scoreStarted = performanceRecorder?.DetailedCollectionEnabled == true
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        long scoreAllocatedAtStart = scoreStarted != 0L
            ? GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        float actionScore;
        using (ActionConsiderationsMarker.Auto())
        {
            actionScore = hasDecisionContext
                ? action.CalculateScore(actor, in context)
                : action.CalculateScore(actor);
        }
        if (scoreStarted != 0L)
        {
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - scoreStarted)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
            performanceRecorder.Record(
                AiPerformanceCategory.ActionConsiderationScore,
                elapsedMilliseconds,
                Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread()
                        - scoreAllocatedAtStart));
            CharacterAiSlowOperationTrace.Record(
                "considerations",
                actor,
                action.actionset,
                null,
                elapsedMilliseconds);
        }
        if (actionScore <= 0f)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoScore);
            return false;
        }

        return true;
    }

    private bool CanUseAction(AIAction action, out string failureReason)
    {
        bool result = CanUseAction(action, out AIActionFailure failure);
        failureReason = failure.ToString();
        return result;
    }

    private bool CanUseAction(AIAction action, out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (!CanConsiderAction(action, out failure))
        {
            return false;
        }

        if (action.SetDestinationWithFailure(actor, out failure))
        {
            return true;
        }

        failure = RefineActionFailure(action, failure);
        return false;
    }


    private bool IsActionCoolingDown(AIActionSet actionSet)
    {
        return actionSet != null
            && actionFailureCooldownUntil.TryGetValue(actionSet, out float until)
            && Now < until;
    }

    private void RecordActionFailure(AIActionSet actionSet, AIActionFailure failure)
    {
        if (actionSet == null) return;

        actionFailureCooldownUntil[actionSet] = Now + Mathf.Max(0.1f, actionFailureCooldown);
        lastFailedActionSet = actionSet;
        lastActionFailure = failure.HasFailure ? failure : AIActionFailure.Create(AIActionFailureKind.Unknown);
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

    private AIActionFailure RefineActionFailure(AIAction action, AIActionFailure failure)
    {
        if (action?.actionset != null
            && action.actionset.HasSemanticTag(CharacterAiActionTags.Work)
            && actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate)
            && candidate.Building != null)
        {
            return AIActionFailure.Create(
                candidate.FailureKind != AIActionFailureKind.None
                    ? candidate.FailureKind
                    : failure.Kind,
                candidate.FailureReason,
                candidate.Building);
        }

        return failure;
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

        if (GetFailureDebugPriority(failure.Kind) <= GetFailureDebugPriority(lastActionFailure.Kind))
        {
            return;
        }

        lastFailedActionSet = action.actionset;
        lastActionFailure = failure;
        if (ShouldCooldownCandidateFailure(failure.Kind))
        {
            actionFailureCooldownUntil[action.actionset] = Now + Mathf.Max(0.1f, actionFailureCooldown);
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
            AIActionFailureKind.PathSearchDeferred => 10,
            AIActionFailureKind.Cooldown => 5,
            AIActionFailureKind.NoScore => 1,
            _ => 0
        };
    }

    public string GetDebugSummary(int candidateCount = 3)
    {
        string actionLabel = bestAction != null && bestAction.actionset != null
            ? GetActionLabel(bestAction.actionset)
            : currentActionDebugLabel;
        string runningLabel = bestAction != null && bestAction.HasStarted
            ? $" ({bestAction.RunningSeconds:0.0}s)"
            : string.Empty;
        string reservationLabel = bestAction != null && bestAction.HasReservation
            ? $"\n\uC608\uC57D: {GetDestinationLabel(bestAction.ReservedDestination)}"
            : string.Empty;
        string phaseLabel = string.IsNullOrWhiteSpace(currentActionPhase)
            ? "\uC5C6\uC74C"
            : currentActionPhase;
        if (!string.IsNullOrWhiteSpace(currentActionPhaseDetail))
        {
            phaseLabel += $" / {currentActionPhaseDetail}";
        }

        string destinationLabel = bestAction != null && bestAction.destination != null
            ? GetDestinationLabel(bestAction.destination)
            : string.IsNullOrWhiteSpace(currentDestinationDebugLabel)
                ? "\uC5C6\uC74C"
                : currentDestinationDebugLabel;
        string pathLabel = GetPathDebugLabel(bestAction);
        string switchLabel = Now < nextActionSwitchAllowedAt
            ? $"\n\uC804\uD658\uC644\uCDA9: {nextActionSwitchAllowedAt - Now:0.0}s"
            : string.Empty;
        string moodLabel = GetMoodDebugLabel();
        string failureLabel = lastActionFailure.HasFailure
            ? lastActionFailure.ToString()
            : "\uC815\uC0C1";
        string failedActionLabel = lastFailedActionSet != null
            ? GetActionLabel(lastFailedActionSet)
            : string.Empty;
        string reason = string.IsNullOrWhiteSpace(failedActionLabel)
            ? failureLabel
            : $"{failedActionLabel}: {failureLabel}";
        string haulLabel = GetHaulDebugLabel();
        string constructionLabel = GetConstructionSafetyDebugLabel();

        IEnumerable<AIActionDebugCandidate> candidates = lastCandidateScores
            .OrderByDescending((candidate) => candidate.Score)
            .Take(Mathf.Clamp(candidateCount, 1, debugCandidateLimit));
        string candidateText = string.Join(
            ", ",
            candidates.Select((candidate) =>
                candidate.Failure.HasFailure
                    ? $"{candidate.ActionLabel} {candidate.Score:0.00}({candidate.Failure.Kind})"
                    : $"{candidate.ActionLabel} {candidate.Score:0.00}"));

        string baseText =
            $"\uD589\uB3D9: {actionLabel}{runningLabel}"
            + $"\n\uB2E8\uACC4: {phaseLabel}"
            + $"\n\uBAA9\uD45C: {destinationLabel}"
            + $"\n\uACBD\uB85C: {pathLabel}"
            + reservationLabel
            + switchLabel
            + $"\n\uAE30\uBD84: {moodLabel}"
            + $"\n\uC774\uC720: {reason}"
            + haulLabel
            + constructionLabel;
        return string.IsNullOrWhiteSpace(candidateText)
            ? baseText
            : $"{baseText}\n\uD6C4\uBCF4: {candidateText}";
    }

    private string GetHaulDebugLabel()
    {
        AbilityHaul haul = actor != null ? actor.GetComponent<AbilityHaul>() : null;
        if (haul == null)
        {
            return string.Empty;
        }

        CharacterCarryInventory carry = actor.GetComponent<CharacterCarryInventory>();
        IWorldItemStackRuntime itemRuntime = actor.WorldItemStackRuntime;
        float currentWeight = carry != null
            ? carry.GetCurrentWeight(itemRuntime?.CatalogProvider)
            : 0f;
        float maxWeight = carry != null
            ? carry.GetMaxAllowedWeight(itemRuntime?.HaulingSettingsProvider)
            : 0f;
        return $"\n운반 계획: {haul.CurrentPlanSummary}"
            + $"\n적재: {currentWeight:0.#}/{maxWeight:0.#}kg"
            + $"\n정리 사유: {haul.CurrentUnloadReason}";
    }

    private string GetConstructionSafetyDebugLabel()
    {
        ConstructionSite site = bestAction != null
            ? bestAction.destination as ConstructionSite
            : null;
        if (site == null)
        {
            return string.Empty;
        }

        ConstructionSafetyResult safety = site.LastSafetyResult.Message.Length > 0
            ? site.LastSafetyResult
            : site.GetConstructionSafetyState(actor);
        string prefix = safety.IsForcedWarning ? "강제 공사" : safety.IsSafe ? "공사 안전" : "공사 대기";
        return $"\n{prefix}: {safety.Message}";
    }

    private static string GetDestinationLabel(BuildableObject destination)
    {
        if (destination == null)
        {
            return "\uC5C6\uC74C";
        }

        if (destination.BuildingData != null
            && !string.IsNullOrWhiteSpace(destination.BuildingData.objectName))
        {
            return destination.BuildingData.objectName;
        }

        return destination.name;
    }

    private string GetMoodDebugLabel()
    {
        if (actor == null || actor.Stats == null)
        {
            return "\uC5C6\uC74C";
        }

        return $"{actor.Stats.Mood:0.#}";
    }

    public int GetDebugHash()
    {
        unchecked
        {
            int hash = DebugVersion;
            hash = (hash * 31) + (bestAction?.actionset != null ? bestAction.actionset.GetInstanceID() : 0);
            hash = (hash * 31) + (int)lastActionFailure.Kind;
            foreach (AIActionDebugCandidate candidate in lastCandidateScores.Take(debugCandidateLimit))
            {
                hash = (hash * 31) + candidate.ActionLabel.GetHashCode();
                hash = (hash * 31) + Mathf.RoundToInt(candidate.Score * 1000f);
                hash = (hash * 31) + (int)candidate.Failure.Kind;
            }

            return hash;
        }
    }

    private void MarkDebugDirty()
    {
        DebugVersion++;
    }

}
public enum AIActionPlanKind
{
    None,
    NoDestination,
    DestinationOnly,
    MovePath
}

public sealed class AIActionPlan
{
    private static readonly IReadOnlyList<GridMoveStep> EmptyPath =
        ReadOnlyView.List(Array.Empty<GridMoveStep>());

    private AIActionPlan(
        AIActionPlanKind kind,
        BuildableObject destination,
        IEnumerable<GridMoveStep> pathSteps)
    {
        GridMoveStep[] path = pathSteps?.ToArray() ?? Array.Empty<GridMoveStep>();
        if (path.Any((step) => !step.IsValid))
        {
            throw new ArgumentException("An AI action path cannot contain null steps.", nameof(pathSteps));
        }

        bool valid = kind switch
        {
            AIActionPlanKind.None => destination == null && path.Length == 0,
            AIActionPlanKind.NoDestination => destination == null && path.Length == 0,
            AIActionPlanKind.DestinationOnly => destination != null && path.Length == 0,
            AIActionPlanKind.MovePath => destination != null && path.Length > 0,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentException(
                $"Invalid AI action plan: kind={kind}, destination={destination != null}, steps={path.Length}.");
        }

        Kind = kind;
        Destination = destination;
        PathSteps = path.Length == 0 ? EmptyPath : ReadOnlyView.List(path);
    }

    public static AIActionPlan None { get; } =
        new AIActionPlan(AIActionPlanKind.None, null, null);

    public static AIActionPlan WithoutDestination { get; } =
        new AIActionPlan(AIActionPlanKind.NoDestination, null, null);

    public AIActionPlanKind Kind { get; }
    public BuildableObject Destination { get; }
    public IReadOnlyList<GridMoveStep> PathSteps { get; }

    public static AIActionPlan AtDestination(BuildableObject destination)
    {
        return new AIActionPlan(AIActionPlanKind.DestinationOnly, destination, null);
    }

    public static AIActionPlan MoveTo(
        BuildableObject destination,
        IEnumerable<GridMoveStep> pathSteps)
    {
        return new AIActionPlan(AIActionPlanKind.MovePath, destination, pathSteps);
    }
}

[Serializable]
public class AIAction
{
    private static readonly Dictionary<Type, ProfilerMarker> ConsiderationTypeMarkers =
        new Dictionary<Type, ProfilerMarker>();
    public AIActionSet actionset;
    private float _score;
    public float score
    {
        get { return _score; }
        set
        {
            _score = Mathf.Clamp01(value);
        }
    }
    private AIActionPlan plan = AIActionPlan.None;
    public float startedAt = -1f;
    private AIActionSet reservedActionSet;
    private BuildableObject reservedDestination;
    [NonSerialized] private IGameClock gameClock;
    public bool HasStarted => startedAt >= 0f;
    public float RunningSeconds => HasStarted && gameClock != null
        ? Mathf.Max(0f, gameClock.Time - startedAt)
        : 0f;
    public bool HasReservation => reservedActionSet != null && reservedDestination != null;
    public BuildableObject ReservedDestination => reservedDestination;
    public AIActionPlan Plan => plan;
    public BuildableObject destination => plan.Destination;
    public IReadOnlyList<GridMoveStep> pathSteps => plan.PathSteps;
    public AIActionPlanKind planKind => plan.Kind;

    public AIAction()
    {
    }

    public AIAction(AIActionSet actionset, AIActionPlan initialPlan)
    {
        this.actionset = actionset;
        plan = initialPlan ?? throw new ArgumentNullException(nameof(initialPlan));
    }

    public void MarkStarted(float time)
    {
        startedAt = time;
    }

    public void BindClock(IGameClock gameClock)
    {
        this.gameClock = gameClock;
    }

    public float CalculateScore(CharacterActor actor)
    {
        CharacterAiDecisionContext context = default;
        return CalculateScoreCore(actor, false, in context);
    }

    public float CalculateScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CalculateScoreCore(actor, true, in context);
    }

    private float CalculateScoreCore(
        CharacterActor actor,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context)
    {
        if (actionset == null)
        {
            this.score = 0f;
            return this.score;
        }

        if (hasDecisionContext)
        {
            actionset.PrepareScoreContext(actor, in context);
        }

        if (actionset.considerations == null || actionset.considerations.Length == 0)
        {
            float baseScore = EvaluatePersonalityScore(actor, actionset);
            this.score = EvaluateAdjustedScore(
                actor,
                actionset,
                hasDecisionContext,
                in context,
                baseScore);
            return this.score;
        }

        int considerationCount = actionset.considerations.Length;
        float totalScore = 0f;
        foreach (var consideration in actionset.considerations)
        {
            if (consideration == null)
            {
                this.score = 0f;
                return this.score;
            }

            long considerationStarted = CharacterAiSlowOperationTrace.Enabled
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
            float considerationScore;
            using (GetConsiderationMarker(consideration.GetType()).Auto())
            {
                considerationScore = Mathf.Clamp01(consideration.ScoreConsideration(actor));
            }
            if (considerationStarted != 0L)
            {
                double elapsedMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - considerationStarted)
                    * 1000.0
                    / System.Diagnostics.Stopwatch.Frequency;
                CharacterAiSlowOperationTrace.Record(
                    "consideration",
                    actor,
                    actionset,
                    consideration,
                    elapsedMilliseconds);
            }
            if (considerationScore <= 0f)
            {
                this.score = 0;
                return this.score;
            }

            totalScore += considerationScore;
        }

        float actionScore = totalScore / considerationCount;
        actionScore *= EvaluatePersonalityScore(actor, actionset);
        this.score = EvaluateAdjustedScore(
            actor,
            actionset,
            hasDecisionContext,
            in context,
            actionScore);
        return this.score;
    }

    private static ProfilerMarker GetConsiderationMarker(Type type)
    {
        if (ConsiderationTypeMarkers.TryGetValue(type, out ProfilerMarker marker))
        {
            return marker;
        }

        marker = new ProfilerMarker("CharacterAi.Consideration." + type.Name);
        ConsiderationTypeMarkers[type] = marker;
        return marker;
    }

    private static float EvaluatePersonalityScore(
        CharacterActor actor,
        AIActionSet actionSet)
    {
        if (!CharacterAiSlowOperationTrace.Enabled)
        {
            return CharacterAiPersonalityUtility.GetActionScoreMultiplier(
                actor,
                actionSet);
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        float value = CharacterAiPersonalityUtility.GetActionScoreMultiplier(
            actor,
            actionSet);
        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        CharacterAiSlowOperationTrace.Record(
            "personality",
            actor,
            actionSet,
            null,
            elapsedMilliseconds);
        return value;
    }

    private static float EvaluateAdjustedScore(
        CharacterActor actor,
        AIActionSet actionSet,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        float baseScore)
    {
        if (!CharacterAiSlowOperationTrace.Enabled)
        {
            return hasDecisionContext
                ? actionSet.AdjustScore(actor, in context, baseScore)
                : actionSet.AdjustScore(actor, baseScore);
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        float value = hasDecisionContext
            ? actionSet.AdjustScore(actor, in context, baseScore)
            : actionSet.AdjustScore(actor, baseScore);
        double elapsedMilliseconds =
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
        CharacterAiSlowOperationTrace.Record(
            "adjust-score",
            actor,
            actionSet,
            null,
            elapsedMilliseconds);
        return value;
    }

    public bool SetDestinationWithFailure(CharacterActor actor, out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (actor == null || actor.Brain == null || actionset == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoGrid,
                "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (!actionset.TryResolveDestinationWithFailure(
                actor,
                null,
                out BuildableObject resolvedDestination,
                out failure))
        {
            return false;
        }

        return SetResolvedDestinationWithFailure(
            actor,
            resolvedDestination,
            out failure);
    }

    public bool SetResolvedDestinationWithFailure(
        CharacterActor actor,
        BuildableObject resolvedDestination,
        out AIActionFailure failure)
    {
        ReleaseReservation(actor);
        plan = AIActionPlan.None;
        startedAt = -1f;
        failure = AIActionFailure.None;
        if (actor == null
            || actor.Brain == null
            || actionset == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoGrid, "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (resolvedDestination == null)
        {
            plan = AIActionPlan.WithoutDestination;
            return !actionset.RequiresDestination;
        }

        if (resolvedDestination.isDestroy)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Destroyed,
                "\uBAA9\uD45C \uD30C\uAD34\uB428",
                resolvedDestination);
            return false;
        }

        if (IsCharacterAtDestination(actor, resolvedDestination))
        {
            plan = AIActionPlan.AtDestination(resolvedDestination);
            return TryReserveResolvedDestination(actor, resolvedDestination, out failure);
        }

        Vector2Int pathDestination = ResolveNearestDestinationCell(
            actor.GetNowXY(),
            resolvedDestination);
        Queue<GridMoveStep> resolvedPath = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            actor.GetNowXY(),
            pathDestination,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(actor));
        if (resolvedPath == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "경로 탐색 예산 대기",
                resolvedDestination);
            return false;
        }

        return ResolvePathPlan(actor, resolvedDestination, resolvedPath, out failure)
            && TryReserveResolvedDestination(actor, resolvedDestination, out failure);
    }

    private static Vector2Int ResolveNearestDestinationCell(
        Vector2Int start,
        BuildableObject destination)
    {
        IReadOnlyList<Vector2Int> positions = destination?.buildPoses;
        if (positions == null || positions.Count == 0)
        {
            return destination != null ? destination.centerPos : start;
        }

        Vector2Int best = positions[0];
        int bestEstimate = EstimateDestinationCost(start, best);
        for (int index = 1; index < positions.Count; index++)
        {
            Vector2Int candidate = positions[index];
            int estimate = EstimateDestinationCost(start, candidate);
            if (estimate < bestEstimate)
            {
                best = candidate;
                bestEstimate = estimate;
            }
        }

        return best;
    }

    private static int EstimateDestinationCost(Vector2Int start, Vector2Int destination)
    {
        int horizontal = Mathf.Abs(start.x - destination.x)
            * DefaultGridTraversalCostPolicy.DryWalkCost;
        int floorChanges = Mathf.Abs(start.y - destination.y);
        return horizontal
            + floorChanges * DefaultGridTraversalCostPolicy.StairFallbackCost;
    }

    public bool TryRebuildPathFromCurrentPosition(CharacterActor actor, out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        BuildableObject currentDestination = destination;
        plan = AIActionPlan.None;
        if (actor == null
            || actor.Brain == null
            || actionset == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoGrid, "AI \uB610\uB294 \uADF8\uB9AC\uB4DC \uC5C6\uC74C");
            return false;
        }

        if (currentDestination == null)
        {
            plan = AIActionPlan.WithoutDestination;
            return !actionset.RequiresDestination;
        }

        if (currentDestination.isDestroy)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Destroyed,
                "\uBAA9\uD45C \uD30C\uAD34\uB428",
                currentDestination);
            return false;
        }

        Vector2Int pathDestination = ResolveNearestDestinationCell(
            actor.GetNowXY(),
            currentDestination);
        Queue<GridMoveStep> rebuiltPath = actor.PathSearchBroker?.GetMovePathTo(
                grid,
                actor.GetNowXY(),
                pathDestination,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(actor));
        if (rebuiltPath == null)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.PathSearchDeferred,
                "경로 탐색 예산 대기",
                currentDestination);
            return false;
        }

        return ResolvePathPlan(actor, currentDestination, rebuiltPath, out failure);
    }

    public void RefreshReservation(CharacterActor actor)
    {
        if (!HasReservation)
        {
            return;
        }

        reservedActionSet.RefreshDestinationReservation(actor, reservedDestination);
    }

    public void ReleaseReservation(CharacterActor actor)
    {
        if (!HasReservation)
        {
            return;
        }

        reservedActionSet.ReleaseDestinationReservation(actor, reservedDestination);
        reservedActionSet = null;
        reservedDestination = null;
    }

    private bool TryReserveResolvedDestination(
        CharacterActor actor,
        BuildableObject destination,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (actionset == null || destination == null)
        {
            return true;
        }

        if (!actionset.TryReserveDestination(actor, destination, out failure))
        {
            if (failure.Target == null)
            {
                failure = new AIActionFailure(failure.Kind, failure.Reason, destination);
            }

            plan = AIActionPlan.None;
            return false;
        }

        reservedActionSet = actionset;
        reservedDestination = destination;
        return true;
    }

    private bool ResolvePathPlan(
        CharacterActor actor,
        BuildableObject destination,
        Queue<GridMoveStep> pathSteps,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (pathSteps != null && pathSteps.Count > 0)
        {
            plan = AIActionPlan.MoveTo(destination, pathSteps);
            return true;
        }

        if (IsCharacterAtDestination(actor, destination))
        {
            plan = AIActionPlan.AtDestination(destination);
            return true;
        }

        failure = AIActionFailure.Create(AIActionFailureKind.NoPath, "\uACBD\uB85C \uC5C6\uC74C", destination);
        plan = AIActionPlan.None;
        return false;
    }

    private static bool IsCharacterAtDestination(CharacterActor actor, BuildableObject destination)
    {
        if (actor == null
            || actor.Brain == null
            || destination == null
            || !actor.Brain.TryGetRuntimeGrid(out Grid grid))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(actor.GetNowXY());
        return cell != null && cell.GetAllOccupants().Contains(destination);
    }
}
