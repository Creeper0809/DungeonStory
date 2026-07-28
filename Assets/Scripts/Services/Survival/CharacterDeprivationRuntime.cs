using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterDeprivationRuntime :
    ICharacterDeprivationRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private enum SafeDrinkTargetKind
    {
        None = 0,
        ItemStack = 1,
        Facility = 2,
        WorldSource = 3
    }

    private enum SafeReliefApproachSearchStatus
    {
        Invalid = 0,
        Pending = 1,
        Reachable = 2
    }

    private readonly struct SafeDrinkPlan
    {
        public SafeDrinkPlan(
            SafeDrinkTargetKind kind,
            Vector2Int targetPosition,
            Vector2Int approachPosition,
            Queue<GridMoveStep> path,
            string targetId = "",
            BuildableObject facility = null)
        {
            Kind = kind;
            TargetPosition = targetPosition;
            ApproachPosition = approachPosition;
            Path = path;
            TargetId = targetId ?? string.Empty;
            Facility = facility;
        }

        public SafeDrinkTargetKind Kind { get; }
        public Vector2Int TargetPosition { get; }
        public Vector2Int ApproachPosition { get; }
        public Queue<GridMoveStep> Path { get; }
        public string TargetId { get; }
        public BuildableObject Facility { get; }
        public bool IsValid => Kind != SafeDrinkTargetKind.None;
    }

    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterDeprivationRuntime.Tick");
    private static readonly ProfilerMarker SafeReliefPlanProfilerMarker =
        new ProfilerMarker("SafeRelief.Plan");
    private static readonly ProfilerMarker SafeReliefStackProfilerMarker =
        new ProfilerMarker("SafeRelief.StackCandidates");
    private static readonly ProfilerMarker SafeReliefFacilityProfilerMarker =
        new ProfilerMarker("SafeRelief.FacilityCandidates");
    private static readonly ProfilerMarker SafeReliefApproachProfilerMarker =
        new ProfilerMarker("SafeRelief.Approach");
    private static readonly ProfilerMarker SafeReliefDirectPathProfilerMarker =
        new ProfilerMarker("SafeRelief.DirectPath");
    private static readonly ProfilerMarker SafeReliefExactPathProfilerMarker =
        new ProfilerMarker("SafeRelief.ExactPath");

    private const float TickInterval = 1f;
    private const float BreakdownCheckInterval = 5f;
    private const float CertainBreakdownDelay = 30f;
    private const float DamageInterval = 10f;
    private const float WarningThreshold = 40f;
    private const float BreakdownThreshold = 70f;
    private const float MaximumBurden = 100f;
    private const float DefaultSuppressionResistance = 35f;
    private const int EmergencyPathRetryFrames = 180;
    private const int AccidentSearchRadius = 32;
    private const int BurdenKindCount = 6;
    private const float SafeReliefNeedThreshold = 80f;
    private const float SafeReliefRetrySeconds = 1.25f;
    private const int MaximumSafeReliefStartsPerFrame = 2;
    private static readonly Func<string, int> EmergencyFoodRankSelector =
        GetEmergencyFoodRank;
    private static readonly WaitForSeconds CannibalAttackDelay =
        new WaitForSeconds(0.75f);
    private static readonly WaitForSeconds CorpseSpawnDelay =
        new WaitForSeconds(0.1f);
    private static readonly WaitForSeconds CollapseDelay =
        new WaitForSeconds(5f);
    private static readonly WaitForSeconds ViolentActionDelay =
        new WaitForSeconds(0.8f);
    private static readonly WaitForSeconds BreakdownIdleDelay =
        new WaitForSeconds(1.5f);

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly ISurvivalFoodRuntime survivalFoodRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IRandomStream breakdownRandom;
    private readonly Dictionary<string, CharacterDeprivationState> states =
        new Dictionary<string, CharacterDeprivationState>(
            512,
            StringComparer.Ordinal);
    private readonly HashSet<string> runningBreakdownActions =
        new HashSet<string>(128, StringComparer.Ordinal);
    private readonly HashSet<string> runningSafeReliefActions =
        new HashSet<string>(128, StringComparer.Ordinal);
    private readonly Dictionary<Vector2Int, string> safeReliefApproachOwners =
        new Dictionary<Vector2Int, string>(128);
    private readonly Dictionary<string, Vector2Int> safeReliefApproachByActor =
        new Dictionary<string, Vector2Int>(128, StringComparer.Ordinal);
    private readonly Dictionary<string, float> nextSafeReliefAttemptAt =
        new Dictionary<string, float>(512, StringComparer.Ordinal);
    private readonly List<WorldItemStockCandidate> safeDrinkStockCandidates =
        new List<WorldItemStockCandidate>(64);
    private readonly Dictionary<string, int> alertLevels =
        new Dictionary<string, int>(512, StringComparer.Ordinal);
    private readonly Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>> actionRoutines =
        new Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>>();
    private readonly List<CharacterActor> tickActors =
        new List<CharacterActor>(512);
    private readonly HashSet<string> liveTickIds =
        new HashSet<string>(512, StringComparer.Ordinal);
    private readonly List<string> staleStateIds = new List<string>(128);
    private IDisposable infectionSubscription;
    private IDisposable infectionReductionSubscription;
    private IDisposable mentalInstabilitySubscription;
    private IDisposable deathSubscription;
    private float nextTickAt;
    private float lastSimulationTickAt;
    private float tickPassStartedAt;
    private int tickActorIndex;
    private float tickElapsed;
    private float tickNow;
    private bool tickPassActive;
    private int pendingWarningAlerts;
    private int pendingDangerAlerts;
    private int safeReliefStartFrame = -1;
    private int safeReliefStartsThisFrame;
    private int safeReliefRequests;
    private int safeReliefPlanFailures;
    private int safeReliefActionsStarted;
    private int safeReliefStoredStackPlans;
    private int safeReliefMoveFailures;
    private int safeReliefBreakdownMoveFailures;
    private int safeReliefBlockedMoveFailures;
    private int safeReliefOtherMoveFailures;
    private int safeReliefStaleStartFailures;
    private int safeReliefWallBlockedFailures;
    private int safeReliefDoorDeniedFailures;
    private int safeReliefDefenseReservationFailures;
    private int safeReliefTraversalChangedFailures;
    private int safeReliefArrivals;
    private int safeReliefInteractionAttempts;
    private int safeReliefSuccesses;
    private int safeReliefActionsFinished;
    private long safeReliefPlannedPathSteps;
    private int safeReliefMaximumPlannedPathSteps;
    private float safeReliefCompletedDurationSeconds;
    private float safeReliefMaximumDurationSeconds;
    private int safeReliefCancelledMoveFailures;
    private int safeReliefMissingPathFailures;
    private int safeReliefMissingMovementHandlerFailures;
    private int safeReliefGridUnavailableFailures;
    private int safeReliefInvalidSpeedFailures;
    private int safeReliefNoFailureReasonFailures;
    private int safeReliefActorDeadMoveFailures;
    private int safeReliefActorMissingMoveFailures;
    private int safeReliefCrossFloorTargetPlans;
    private int safeReliefPathsWithVerticalTraversal;
    private long safeReliefVerticalTraversalSteps;
    private int desperateDrinkAttempts;
    private int desperateDrinkStackMoveFailures;
    private int desperateDrinkStackArrivals;
    private int desperateDrinkStackConsumptions;

    public CharacterDeprivationRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery,
        IRoomLayoutCache roomLayoutCache,
        ICharacterAiWorldRegistry worldRegistry,
        IFacilityCandidateCache facilityCandidateCache,
        ISurvivalFoodRuntime survivalFoodRuntime,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IDynamicFrameWorkBudget frameWorkBudget,
        IRandomStreamProvider randomStreamProvider,
        IUiClock uiClock = null,
        IDoorAccessQuery doorAccessQuery = null)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.filthQuery = filthQuery ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery ?? throw new ArgumentNullException(nameof(waterQuery));
        this.roomLayoutCache = roomLayoutCache ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.survivalFoodRuntime = survivalFoodRuntime ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.uiClock = uiClock;
        this.doorAccessQuery = doorAccessQuery;
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        breakdownRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-deprivation");
    }

    public void Initialize()
    {
        CreateActionRoutines();
        nextTickAt = CadenceTime + TickInterval;
        lastSimulationTickAt = gameClock.Time;
        infectionSubscription = gameEventBus.Subscribe<CharacterInfectionBurdenRequestedEvent>(
            gameEvent => AddInfectionBurden(gameEvent.Actor, gameEvent.Amount));
        infectionReductionSubscription =
            gameEventBus.Subscribe<CharacterInfectionBurdenReductionRequestedEvent>(
                gameEvent => ReduceInfectionBurden(
                    gameEvent.Actor,
                    gameEvent.Amount));
        mentalInstabilitySubscription =
            gameEventBus.Subscribe<CharacterMentalInstabilityBurdenRequestedEvent>(
                gameEvent => AddMentalInstabilityBurden(
                    gameEvent.Actor,
                    gameEvent.Amount));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
    }

    public void Dispose()
    {
        infectionSubscription?.Dispose();
        infectionReductionSubscription?.Dispose();
        mentalInstabilitySubscription?.Dispose();
        deathSubscription?.Dispose();
        infectionSubscription = null;
        infectionReductionSubscription = null;
        mentalInstabilitySubscription = null;
        deathSubscription = null;
        actionRoutines.Clear();
        tickActors.Clear();
        liveTickIds.Clear();
        staleStateIds.Clear();
        safeReliefApproachOwners.Clear();
        safeReliefApproachByActor.Clear();
        nextSafeReliefAttemptAt.Clear();
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
        if (!Application.isPlaying || gameClock.IsPaused)
        {
            return;
        }

        float now = gameClock.Time;
        float cadenceNow = CadenceTime;
        if (!tickPassActive)
        {
            if (cadenceNow < nextTickAt)
            {
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterDeprivation,
                    0);
                return;
            }

            tickElapsed = Mathf.Max(0f, now - lastSimulationTickAt);
            lastSimulationTickAt = now;
            if (tickElapsed <= 0f)
            {
                nextTickAt = cadenceNow + TickInterval;
                return;
            }

            tickNow = now;
            tickPassStartedAt = cadenceNow;
            nextTickAt = cadenceNow + TickInterval;
            tickActors.Clear();
            IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
            for (int i = 0; i < actors.Count; i++)
            {
                tickActors.Add(actors[i]);
            }

            liveTickIds.Clear();
            tickActorIndex = 0;
            pendingWarningAlerts = 0;
            pendingDangerAlerts = 0;
            tickPassActive = true;
        }

        int backlog = tickActors.Count - tickActorIndex;
        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.CharacterDeprivation,
            backlog);
        double sliceMilliseconds = frameWorkBudget.GetSliceMilliseconds(
            DynamicFrameWorkDomain.CharacterDeprivation,
            0.05,
            0.75,
            backlog > 0 && cadenceNow - tickPassStartedAt >= TickInterval);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        int processed = 0;
        while (tickActorIndex < tickActors.Count)
        {
            CharacterActor actor = tickActors[tickActorIndex++];
            processed++;
            if (IsEligibleHumanoid(actor))
            {
                string id = GetPersistentId(actor);
                liveTickIds.Add(id);
                CharacterDeprivationState state = EnsureState(actor);
                TickActor(actor, state, tickElapsed, tickNow);
            }

            if (processed >= 1
                && ElapsedMilliseconds(started) >= sliceMilliseconds)
            {
                break;
            }
        }

        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.CharacterDeprivation,
            ElapsedMilliseconds(started));
        if (tickActorIndex < tickActors.Count)
        {
            return;
        }

        FlushAggregatedAlerts();
        staleStateIds.Clear();
        foreach (KeyValuePair<string, CharacterDeprivationState> pair in states)
        {
            if (!liveTickIds.Contains(pair.Key)
                && pair.Value?.breakdown?.active != true)
            {
                staleStateIds.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleStateIds.Count; i++)
        {
            string stale = staleStateIds[i];
            states.Remove(stale);
            alertLevels.Remove(stale);
        }

        tickActors.Clear();
        liveTickIds.Clear();
        staleStateIds.Clear();
        tickPassActive = false;
        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.CharacterDeprivation,
            0);
    }

    private void FlushAggregatedAlerts()
    {
        if (pendingDangerAlerts > 0)
        {
            gameEventBus.RaiseAlert(
                $"{pendingDangerAlerts}명이 결핍으로 붕괴 위험에 빠졌습니다",
                "건강 탭에서 가장 심한 결핍을 확인하고 음식, 물, 화장실, 휴식 시설을 확보하세요.",
                EventAlertImportance.High,
                "생존");
        }

        if (pendingWarningAlerts > 0)
        {
            gameEventBus.RaiseAlert(
                $"{pendingWarningAlerts}명의 건강에 결핍 부담이 쌓이고 있습니다",
                "결핍 원인을 해결하지 않으면 건강 이상과 돌발 행동으로 이어질 수 있습니다.",
                EventAlertImportance.Medium,
                "생존");
        }
    }

    private static double ElapsedMilliseconds(long started)
    {
        return (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
    }

    private float CadenceTime => uiClock?.Time ?? gameClock.Time;

    public bool HasActiveBreakdown(CharacterActor actor)
    {
        return TryGetState(actor, out CharacterDeprivationState state)
            && state.breakdown != null
            && state.breakdown.active;
    }

    public bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind)
    {
        return TryGetState(actor, out CharacterDeprivationState state)
            && state.breakdown != null
            && state.breakdown.active
            && state.breakdown.kind == kind;
    }

    public bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState)
    {
        if (!TryGetState(actor, out CharacterDeprivationState state))
        {
            displayState = default;
            return false;
        }

        float highestBurden = 0f;
        List<DeprivationBurdenSaveData> burdens = state.burdens;
        if (burdens != null)
        {
            for (int i = 0; i < burdens.Count; i++)
            {
                DeprivationBurdenSaveData burden = burdens[i];
                if (burden != null)
                {
                    highestBurden = Mathf.Max(highestBurden, burden.burden);
                }
            }
        }

        CharacterBreakdownState breakdown = state.breakdown;
        displayState = new CharacterDeprivationDisplayState(
            highestBurden,
            breakdown != null ? breakdown.kind : CharacterBreakdownKind.None,
            breakdown != null && breakdown.active);
        return true;
    }

    public bool TryGetSnapshot(CharacterActor actor, out CharacterDeprivationSnapshot snapshot)
    {
        if (!TryGetState(actor, out CharacterDeprivationState state))
        {
            snapshot = default;
            return false;
        }

        Dictionary<DeprivationKind, float> burdens = state.burdens
            .Where(entry => entry != null)
            .GroupBy(entry => entry.kind)
            .ToDictionary(group => group.Key, group => Mathf.Clamp(group.Last().burden, 0f, 100f));
        snapshot = new CharacterDeprivationSnapshot(
            burdens,
            CloneBreakdown(state.breakdown),
            state.infectionBurden,
            state.tabooMemories?.ToArray() ?? Array.Empty<string>());
        return true;
    }

    public bool TryRunActiveBreakdown(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (!TryGetState(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        if (runningBreakdownActions.Contains(state.persistentId))
        {
            status = GetBreakdownLabel(state.breakdown.kind) + " 진행 중";
            return true;
        }

        if (!actionRoutines.ContainsKey(state.breakdown.kind))
        {
            state.breakdown.kind = ResolveBreakdownKind(state.breakdown.cause);
            status = "붕괴 행동을 다시 고르는 중";
            return true;
        }

        status = GetBreakdownLabel(state.breakdown.kind);
        actor.Brain?.BeginExternallyDrivenAction(
            "결핍 붕괴",
            status,
            "붕괴 행동이 끝날 때까지 유지");
        BeginBreakdownAction(actor, state.breakdown.kind);
        return true;
    }

    public bool NeedsSafeEmergencyRelief(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!IsEligibleHumanoid(actor)
            || actor.Stats == null
            || !actor.Stats.TryGetConditionValue(CharacterCondition.THIRST, out float thirst)
            || thirst >= SafeReliefNeedThreshold
            || HasActiveBreakdown(actor))
        {
            return false;
        }

        reason = $"갈증 {thirst:0}: 안전한 식수 필요";
        return true;
    }

    public bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (!NeedsSafeEmergencyRelief(actor, out _))
        {
            return false;
        }

        safeReliefRequests++;
        string id = GetPersistentId(actor);
        if (runningSafeReliefActions.Contains(id))
        {
            status = "식수를 찾는 중";
            return true;
        }

        float now = gameClock.Time;
        if (nextSafeReliefAttemptAt.TryGetValue(id, out float nextAttempt)
            && now < nextAttempt)
        {
            status = "물을 마실 자리를 기다리는 중";
            return true;
        }

        if (!CanStartSafeReliefThisFrame())
        {
            status = "급수 순서를 기다리는 중";
            return true;
        }

        if (!TryCreateSafeDrinkPlan(actor, id, out SafeDrinkPlan plan))
        {
            safeReliefPlanFailures++;
            nextSafeReliefAttemptAt[id] =
                now + GetSafeReliefRetryDelay(id);
            status = "물을 마실 자리를 기다리는 중";
            return true;
        }

        RecordSafeReliefStart();
        safeReliefActionsStarted++;
        int plannedPathSteps = plan.Path?.Count ?? 0;
        safeReliefPlannedPathSteps += plannedPathSteps;
        safeReliefMaximumPlannedPathSteps = Mathf.Max(
            safeReliefMaximumPlannedPathSteps,
            plannedPathSteps);
        if (actor.GetNowXY().y != plan.TargetPosition.y)
        {
            safeReliefCrossFloorTargetPlans++;
        }
        if (plan.Path != null)
        {
            int verticalSteps = 0;
            foreach (GridMoveStep step in plan.Path)
            {
                if (step.From.y != step.To.y)
                {
                    verticalSteps++;
                }
            }

            if (verticalSteps > 0)
            {
                safeReliefPathsWithVerticalTraversal++;
                safeReliefVerticalTraversalSteps += verticalSteps;
            }
        }
        runningSafeReliefActions.Add(id);
        if (actor.Brain != null)
        {
            actor.Brain.StopCurrentActionForReplan("갈증 비상 대응");
            actor.Brain.BeginExternallyDrivenAction(
                "식수 확보",
                "이동 중",
                $"목표 ({plan.TargetPosition.x}, {plan.TargetPosition.y})");
        }
        actor.StartCoroutine(RunSafeDrink(actor, id, plan));
        status = "갈증 때문에 식수를 찾음";
        return true;
    }

    public CharacterDeprivationDiagnosticsSnapshot GetDiagnostics()
    {
        return new CharacterDeprivationDiagnosticsSnapshot(
            safeReliefRequests,
            safeReliefPlanFailures,
            safeReliefActionsStarted,
            safeReliefStoredStackPlans,
            safeReliefMoveFailures,
            safeReliefBreakdownMoveFailures,
            safeReliefBlockedMoveFailures,
            safeReliefOtherMoveFailures,
            safeReliefStaleStartFailures,
            safeReliefWallBlockedFailures,
            safeReliefDoorDeniedFailures,
            safeReliefDefenseReservationFailures,
            safeReliefTraversalChangedFailures,
            safeReliefArrivals,
            safeReliefInteractionAttempts,
            safeReliefSuccesses,
            runningSafeReliefActions.Count,
            safeReliefActionsFinished,
            safeReliefPlannedPathSteps,
            safeReliefMaximumPlannedPathSteps,
            safeReliefCompletedDurationSeconds,
            safeReliefMaximumDurationSeconds,
            safeReliefCancelledMoveFailures,
            safeReliefMissingPathFailures,
            safeReliefMissingMovementHandlerFailures,
            safeReliefGridUnavailableFailures,
            safeReliefInvalidSpeedFailures,
            safeReliefNoFailureReasonFailures,
            safeReliefActorDeadMoveFailures,
            safeReliefActorMissingMoveFailures,
            safeReliefCrossFloorTargetPlans,
            safeReliefPathsWithVerticalTraversal,
            safeReliefVerticalTraversalSteps,
            desperateDrinkAttempts,
            desperateDrinkStackMoveFailures,
            desperateDrinkStackArrivals,
            desperateDrinkStackConsumptions);
    }

    public void ResetDiagnostics()
    {
        safeReliefRequests = 0;
        safeReliefPlanFailures = 0;
        safeReliefActionsStarted = 0;
        safeReliefStoredStackPlans = 0;
        safeReliefMoveFailures = 0;
        safeReliefBreakdownMoveFailures = 0;
        safeReliefBlockedMoveFailures = 0;
        safeReliefOtherMoveFailures = 0;
        safeReliefStaleStartFailures = 0;
        safeReliefWallBlockedFailures = 0;
        safeReliefDoorDeniedFailures = 0;
        safeReliefDefenseReservationFailures = 0;
        safeReliefTraversalChangedFailures = 0;
        safeReliefArrivals = 0;
        safeReliefInteractionAttempts = 0;
        safeReliefSuccesses = 0;
        safeReliefActionsFinished = 0;
        safeReliefPlannedPathSteps = 0L;
        safeReliefMaximumPlannedPathSteps = 0;
        safeReliefCompletedDurationSeconds = 0f;
        safeReliefMaximumDurationSeconds = 0f;
        safeReliefCancelledMoveFailures = 0;
        safeReliefMissingPathFailures = 0;
        safeReliefMissingMovementHandlerFailures = 0;
        safeReliefGridUnavailableFailures = 0;
        safeReliefInvalidSpeedFailures = 0;
        safeReliefNoFailureReasonFailures = 0;
        safeReliefActorDeadMoveFailures = 0;
        safeReliefActorMissingMoveFailures = 0;
        safeReliefCrossFloorTargetPlans = 0;
        safeReliefPathsWithVerticalTraversal = 0;
        safeReliefVerticalTraversalSteps = 0L;
        desperateDrinkAttempts = 0;
        desperateDrinkStackMoveFailures = 0;
        desperateDrinkStackArrivals = 0;
        desperateDrinkStackConsumptions = 0;
    }

    public void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind)
    {
        if (!HasBreakdownKind(actor, kind))
        {
            return;
        }

        string id = GetPersistentId(actor);
        if (runningBreakdownActions.Add(id))
        {
            actor.Brain?.StopCurrentActionForReplan("결핍 붕괴");
            actor.StartCoroutine(RunBreakdownAction(actor, id, kind));
        }
    }

    public bool IsSuppressible(CharacterActor actor)
    {
        return HasActiveBreakdown(actor);
    }

    public bool ApplySuppression(CharacterActor actor, float amount, out bool ended)
    {
        ended = false;
        if (!TryGetState(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        state.breakdown.suppressionResistance = Mathf.Max(
            0f,
            state.breakdown.suppressionResistance - Mathf.Max(0f, amount));
        actor.ApplyDamage(Mathf.Clamp(amount * 0.08f, 0.5f, 2.5f), "비살상 제압");
        if (state.breakdown.suppressionResistance <= 0f)
        {
            EndBreakdown(actor, state, "제압됨", reduceCauseTo: 55f);
            ended = true;
        }

        return true;
    }

    public bool DebugForceBreakdown(CharacterActor actor, CharacterBreakdownKind kind)
    {
        if (!IsEligibleHumanoid(actor) || kind == CharacterBreakdownKind.None)
        {
            return false;
        }

        CharacterDeprivationState state = EnsureState(actor);
        DeprivationKind cause = kind switch
        {
            CharacterBreakdownKind.DesperateRelief => DeprivationKind.Bladder,
            CharacterBreakdownKind.DesperateDrink => DeprivationKind.Thirst,
            CharacterBreakdownKind.DesperateEat => DeprivationKind.Hunger,
            CharacterBreakdownKind.Collapse => DeprivationKind.Exhaustion,
            _ => DeprivationKind.MentalInstability
        };
        GetBurden(state, cause).burden = 100f;
        state.breakdown.active = true;
        state.breakdown.kind = kind;
        state.breakdown.cause = cause;
        state.breakdown.startedAt = gameClock.Time;
        state.breakdown.suppressionResistance = 25f;
        state.breakdown.targetId = string.Empty;
        actor.Stats?.ApplyMoodFactor("survival:breakdown", "결핍으로 이성을 잃음", -12f, 180f, 1);
        actor.Brain?.StopCurrentActionForReplan("디버그 붕괴 발동");
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        return true;
    }

    public bool DebugClearBreakdown(CharacterActor actor)
    {
        if (!TryGetState(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        EndBreakdown(actor, state, "디버그 해제", reduceCauseTo: 0f);
        return true;
    }

    public float GetMoveSpeedMultiplier(CharacterActor actor)
    {
        if (!TryGetState(actor, out CharacterDeprivationState state))
        {
            return 1f;
        }

        float exhaustion = GetBurden(state, DeprivationKind.Exhaustion).burden;
        float dehydration = GetBurden(state, DeprivationKind.Thirst).burden;
        return Mathf.Clamp(1f - exhaustion * 0.004f - dehydration * 0.002f, 0.45f, 1f);
    }

    public float GetWorkSpeedMultiplier(CharacterActor actor)
    {
        if (!TryGetState(actor, out CharacterDeprivationState state))
        {
            return 1f;
        }

        float exhaustion = GetBurden(state, DeprivationKind.Exhaustion).burden;
        float hunger = GetBurden(state, DeprivationKind.Hunger).burden;
        float thirst = GetBurden(state, DeprivationKind.Thirst).burden;
        return Mathf.Clamp(1f - exhaustion * 0.004f - (hunger + thirst) * 0.0015f, 0.4f, 1f);
    }

    public void RecordTaboo(CharacterActor actor, string memory)
    {
        if (actor == null || string.IsNullOrWhiteSpace(memory))
        {
            return;
        }

        CharacterDeprivationState state = EnsureState(actor);
        state.tabooMemories ??= new List<string>();
        string normalized = memory.Trim();
        if (!state.tabooMemories.Contains(normalized))
        {
            state.tabooMemories.Add(normalized);
            while (state.tabooMemories.Count > 24)
            {
                state.tabooMemories.RemoveAt(0);
            }
        }

        actor.Progression?.RecordNarrative(
            CharacterNarrativeDomain.Survival,
            "survival/taboo",
            string.Empty,
            normalized,
            1f);
    }

    public void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string label,
        float mood)
    {
        ApplyWitnessMood(source, position, label, mood, permanentMemory: true);
    }

    public DungeonDarkSurvivalSaveData Capture()
    {
        return new DungeonDarkSurvivalSaveData
        {
            version = DungeonDarkSurvivalSaveData.CurrentVersion,
            nextFilthSequence = filthQuery.NextFilthSequence,
            nextWaterSequence = waterQuery.NextWaterSequence,
            characters = states.Values.Select(CloneState).ToList(),
            filth = filthQuery.CaptureFilth(),
            waterSources = waterQuery.CaptureWaterSources()
        };
    }

    public void Restore(DungeonDarkSurvivalSaveData saveData)
    {
        DungeonDarkSurvivalSaveData source = saveData ?? new DungeonDarkSurvivalSaveData();
        if (source.version != DungeonDarkSurvivalSaveData.CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported dark survival save version {source.version}.");
        }

        states.Clear();
        foreach (CharacterDeprivationState state in source.characters ?? new List<CharacterDeprivationState>())
        {
            if (state == null || string.IsNullOrWhiteSpace(state.persistentId))
            {
                continue;
            }

            CharacterDeprivationState copy = CloneState(state);
            if (copy.breakdown.active)
            {
                copy.breakdown.targetId = string.Empty;
                copy.breakdown.lastReplanReason = "불러오기 후 대상 재판정";
            }
            states[copy.persistentId] = copy;
        }

        filthQuery.RestoreFilth(source.filth, source.nextFilthSequence);
        waterQuery.RestoreWaterSources(source.waterSources, source.nextWaterSequence);
    }

    private void OnCharacterDeath(CharacterDeathEvent eventType)
    {
        CharacterActor actor = eventType.Actor;
        if (actor == null || itemStackRuntime == null)
        {
            return;
        }

        string sourceId = GetPersistentId(actor);
        if (safeReliefApproachByActor.TryGetValue(
                sourceId,
                out Vector2Int safeApproach))
        {
            ReleaseSafeReliefApproach(sourceId, safeApproach);
        }
        nextSafeReliefAttemptAt.Remove(sourceId);
        bool alreadyExists = itemStackRuntime.GetAllStacks().Any(stack => stack != null
            && stack.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
            && string.Equals(stack.SourceCharacterId, sourceId, StringComparison.Ordinal));
        if (!alreadyExists)
        {
            itemStackRuntime.SpawnHumanoidCorpse(actor, actor.GetNowXY(), eventType.Reason, out _);
        }

        filthQuery.AddFilth(
            WorldFilthType.Blood,
            actor.GetNowXY(),
            12f,
            sourceId,
            0.45f);
    }

    public static float GetBreakdownChance(float burden, float mood01)
    {
        float debtChance = Mathf.Lerp(0.05f, 0.35f, Mathf.InverseLerp(70f, 100f, burden));
        float moodMultiplier = Mathf.Lerp(1.35f, 0.8f, Mathf.Clamp01(mood01));
        return Mathf.Clamp01(debtChance * moodMultiplier);
    }

    public static float GetBreakdownChance(
        float burden,
        float mood01,
        CharacterAiPersonality personality)
    {
        float baseChance = GetBreakdownChance(burden, mood01);
        if (personality == null)
        {
            return baseChance;
        }

        float selfCare01 = Mathf.InverseLerp(0.25f, 2f, personality.selfCare);
        float patience01 = Mathf.InverseLerp(0.25f, 2f, personality.patience);
        float stability01 = (selfCare01 + patience01) * 0.5f;
        return Mathf.Clamp(baseChance * Mathf.Lerp(1.2f, 0.85f, stability01), 0.025f, 0.35f);
    }

    public static float GetBreakdownChance(CharacterActor actor, float burden, float mood01)
    {
        return GetBreakdownChance(burden, mood01, GetPersonality(actor));
    }

    public static float CalculateBurdenDelta(float needValue, float elapsed)
    {
        float safeElapsed = Mathf.Max(0f, elapsed);
        if (needValue < 20f)
        {
            float deficit = Mathf.Clamp01((20f - needValue) / 20f);
            return deficit * deficit * 4f * safeElapsed;
        }

        if (needValue >= 40f)
        {
            float recovery = Mathf.Lerp(0.35f, 1.6f, Mathf.InverseLerp(40f, 100f, needValue));
            return -recovery * safeElapsed;
        }

        return 0f;
    }

    public static bool IsForcedBreakdown(float burden, float maximumHeldSeconds)
    {
        return burden >= MaximumBurden && maximumHeldSeconds >= CertainBreakdownDelay;
    }

    private void TickActor(
        CharacterActor actor,
        CharacterDeprivationState state,
        float elapsed,
        float now)
    {
        UpdateBurden(actor, state, DeprivationKind.Hunger, GetNeed(actor, CharacterCondition.HUNGER), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Thirst, GetNeed(actor, CharacterCondition.THIRST), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Bladder, GetNeed(actor, CharacterCondition.EXCRETION), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Contamination, GetNeed(actor, CharacterCondition.HYGIENE), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Exhaustion, GetNeed(actor, CharacterCondition.SLEEP), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.MentalInstability, actor.Stats?.Mood ?? 50f, elapsed, now);
        state.lastUpdatedAt = now;

        float filthExposure = filthQuery.GetCleanlinessPenalty(actor.GetNowXY(), 1);
        if (filthExposure > 15f)
        {
            DeprivationBurdenSaveData contamination = GetBurden(state, DeprivationKind.Contamination);
            contamination.burden = Mathf.Clamp(contamination.burden + filthExposure * 0.0025f * elapsed, 0f, 100f);
            state.infectionBurden = Mathf.Clamp(state.infectionBurden + filthExposure * 0.0015f * elapsed, 0f, 100f);
        }

        ApplyDamageConsequences(actor, state, now);
        UpdateAlert(actor, state);
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.PreventBreakdowns))
        {
            if (state.breakdown.active)
            {
                EndBreakdown(actor, state, "개발자 붕괴 방지", reduceCauseTo: 55f);
            }
            return;
        }

        if (state.breakdown.active)
        {
            if (IsCauseRelieved(actor, state.breakdown.cause))
            {
                EndBreakdown(actor, state, "욕구가 충족됨", reduceCauseTo: 45f);
            }
            return;
        }

        if (runningSafeReliefActions.Contains(state.persistentId))
        {
            return;
        }

        DeprivationBurdenSaveData highest = null;
        List<DeprivationBurdenSaveData> burdens = state.burdens;
        for (int i = 0; i < burdens.Count; i++)
        {
            DeprivationBurdenSaveData candidate = burdens[i];
            if (candidate != null
                && (highest == null || candidate.burden > highest.burden))
            {
                highest = candidate;
            }
        }
        if (highest == null || highest.burden < BreakdownThreshold)
        {
            return;
        }

        if (highest.burden >= MaximumBurden)
        {
            highest.maximumHeldSeconds += elapsed;
        }
        else
        {
            highest.maximumHeldSeconds = 0f;
        }

        bool certain = highest.maximumHeldSeconds >= CertainBreakdownDelay;
        if (!certain && now < highest.nextBreakdownCheckAt)
        {
            return;
        }

        highest.nextBreakdownCheckAt = now + BreakdownCheckInterval;
        float mood01 = Mathf.Clamp01((actor.Stats?.Mood ?? 50f) / 100f);
        if (certain
            || breakdownRandom.NextFloat()
                <= GetBreakdownChance(actor, highest.burden, mood01))
        {
            StartBreakdown(actor, state, highest.kind, now);
        }
    }

    private static void UpdateBurden(
        CharacterActor actor,
        CharacterDeprivationState state,
        DeprivationKind kind,
        float needValue,
        float elapsed,
        float now)
    {
        DeprivationBurdenSaveData burden = GetBurden(state, kind);
        float delta = CalculateBurdenDelta(needValue, elapsed);
        if (delta > 0f)
        {
            burden.burden = Mathf.Min(MaximumBurden, burden.burden + delta);
        }
        else if (delta < 0f)
        {
            burden.burden = Mathf.Max(0f, burden.burden + delta);
            if (burden.burden < MaximumBurden)
            {
                burden.maximumHeldSeconds = 0f;
            }
        }

        if (burden.nextBreakdownCheckAt <= 0f)
        {
            burden.nextBreakdownCheckAt = now + BreakdownCheckInterval;
        }
        if (burden.nextDamageAt <= 0f)
        {
            burden.nextDamageAt = now + DamageInterval;
        }
    }

    private static void ApplyDamageConsequences(CharacterActor actor, CharacterDeprivationState state, float now)
    {
        ApplyDeprivationDamage(
            actor,
            GetBurden(state, DeprivationKind.Hunger),
            now,
            "심한 굶주림");
        ApplyDeprivationDamage(
            actor,
            GetBurden(state, DeprivationKind.Thirst),
            now,
            "심한 탈수");

        float infectionSource = Mathf.Max(
            GetBurden(state, DeprivationKind.Bladder).burden,
            GetBurden(state, DeprivationKind.Contamination).burden);
        if (infectionSource >= WarningThreshold)
        {
            state.infectionBurden = Mathf.Clamp(
                state.infectionBurden + Mathf.InverseLerp(40f, 100f, infectionSource) * 0.4f,
                0f,
                100f);
        }
    }

    private static void ApplyDeprivationDamage(
        CharacterActor actor,
        DeprivationBurdenSaveData burden,
        float now,
        string source)
    {
        if (burden.burden < BreakdownThreshold || now < burden.nextDamageAt)
        {
            return;
        }

        burden.nextDamageAt = now + DamageInterval;
        actor.ApplyDamage(actor.MaxHealth * 0.01f, source);
    }

    private void UpdateAlert(CharacterActor actor, CharacterDeprivationState state)
    {
        float highest = 0f;
        List<DeprivationBurdenSaveData> burdens = state.burdens;
        for (int i = 0; i < burdens.Count; i++)
        {
            DeprivationBurdenSaveData burden = burdens[i];
            if (burden != null && burden.burden > highest)
            {
                highest = burden.burden;
            }
        }

        int level = highest >= BreakdownThreshold ? 2 : highest >= WarningThreshold ? 1 : 0;
        alertLevels.TryGetValue(state.persistentId, out int previous);
        if (level <= previous)
        {
            alertLevels[state.persistentId] = level;
            return;
        }

        alertLevels[state.persistentId] = level;
        if (level >= 2)
        {
            pendingDangerAlerts++;
        }
        else
        {
            pendingWarningAlerts++;
        }
    }

    private void StartBreakdown(
        CharacterActor actor,
        CharacterDeprivationState state,
        DeprivationKind cause,
        float now)
    {
        state.breakdown = new CharacterBreakdownState
        {
            active = true,
            cause = cause,
            kind = ResolveBreakdownKind(cause),
            startedAt = now,
            suppressionResistance = DefaultSuppressionResistance,
            lastReplanReason = "결핍 임계값 초과"
        };
        actor.Brain?.StopCurrentActionForReplan("결핍 붕괴");
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        actor.ApplyMoodFactor("survival:breakdown", "통제력을 잃음", -8f, 180f, 1);
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Started,
            $"{GetBreakdownLabel(state.breakdown.kind)}",
            actionId: "survival/breakdown",
            reasonCode: cause.ToString(),
            sentiment: -1f,
            bubbleEligible: true));
        DispatchAutomaticSuppression(actor);
    }

    private IEnumerator RunBreakdownAction(
        CharacterActor actor,
        string actorId,
        CharacterBreakdownKind kind)
    {
        try
        {
            if (actionRoutines.TryGetValue(kind, out Func<CharacterActor, IEnumerator> routine))
            {
                yield return routine(actor);
            }
        }
        finally
        {
            runningBreakdownActions.Remove(actorId);
            actor?.Brain?.EndExternallyDrivenAction(clearFailures: true);
        }
    }

    private IEnumerator RunSafeDrink(
        CharacterActor actor,
        string actorId,
        SafeDrinkPlan plan)
    {
        float startedAt = gameClock.Time;
        try
        {
            yield return MoveNear(
                actor,
                plan.ApproachPosition,
                0,
                plan.Path);
            if (actor == null
                || actor.IsDead
                || actor.GetNowXY() != plan.ApproachPosition)
            {
                safeReliefMoveFailures++;
                if (actor == null)
                {
                    safeReliefActorMissingMoveFailures++;
                }
                else if (actor.IsDead)
                {
                    safeReliefActorDeadMoveFailures++;
                }
                else if (HasActiveBreakdown(actor))
                {
                    safeReliefBreakdownMoveFailures++;
                }
                else if (actor != null
                    && actor.TryGetAbility(out AbilityMove move)
                    && move.LastGridMoveWasBlocked)
                {
                    safeReliefBlockedMoveFailures++;
                    RecordSafeReliefBlockReason(
                        move.LastGridMoveFailureReason);
                }
                else
                {
                    safeReliefOtherMoveFailures++;
                    RecordSafeReliefOtherFailureReason(
                        actor != null
                            && actor.TryGetAbility(out AbilityMove currentMove)
                                ? currentMove.LastGridMoveFailureReason
                                : GridMoveFailureReason.None);
                }
                yield break;
            }

            safeReliefArrivals++;
            safeReliefInteractionAttempts++;
            bool succeeded = false;
            switch (plan.Kind)
            {
                case SafeDrinkTargetKind.ItemStack:
                    if (Manhattan(
                            actor.GetNowXY(),
                            plan.TargetPosition) <= 1
                        && itemStackRuntime.TryConsumeStackQuantity(
                            plan.TargetId,
                            1,
                            out _))
                    {
                        actor.ChangesStat(CharacterCondition.THIRST, 100f);
                        actor.ApplyMoodFactor(
                            "survival:clean-water",
                            "깨끗한 물을 마심",
                            2f,
                            90f,
                            1);
                        succeeded = true;
                    }
                    break;

                case SafeDrinkTargetKind.Facility:
                    BuildableObject facility = plan.Facility;
                    if (facility != null
                        && !facility.IsGridDestroyed
                        && Manhattan(
                            actor.GetNowXY(),
                            facility.centerPos) <= 1)
                    {
                        actor.ChangesStat(CharacterCondition.THIRST, 100f);
                        actor.ApplyMoodFactor(
                            "survival:well-water",
                            "우물에서 물을 마심",
                            1f,
                            90f,
                            1);
                        succeeded = true;
                    }
                    break;

                case SafeDrinkTargetKind.WorldSource:
                    if (Manhattan(
                            actor.GetNowXY(),
                            plan.TargetPosition) <= 1
                        && waterQuery.TryDrink(
                            plan.TargetId,
                            1f,
                            out WorldWaterQuality quality,
                            out float consumed)
                        && consumed > 0f
                        && quality == WorldWaterQuality.Clean)
                    {
                        actor.ChangesStat(CharacterCondition.THIRST, 100f);
                        succeeded = true;
                    }
                    break;
            }

            if (succeeded)
            {
                safeReliefSuccesses++;
            }
        }
        finally
        {
            float duration = Mathf.Max(0f, gameClock.Time - startedAt);
            safeReliefActionsFinished++;
            safeReliefCompletedDurationSeconds += duration;
            safeReliefMaximumDurationSeconds = Mathf.Max(
                safeReliefMaximumDurationSeconds,
                duration);
            runningSafeReliefActions.Remove(actorId);
            ReleaseSafeReliefApproach(
                actorId,
                plan.ApproachPosition);
            nextSafeReliefAttemptAt[actorId] =
                gameClock.Time + GetSafeReliefRetryDelay(actorId);
            actor?.Brain?.EndExternallyDrivenAction(clearFailures: true);
        }
    }

    private IEnumerator RunDesperateRelief(CharacterActor actor)
    {
        if (!TryChooseAccidentPosition(actor, out Vector2Int target))
        {
            yield break;
        }

        yield return MoveNear(actor, target, 0);
        if (actor == null || actor.IsDead)
        {
            yield break;
        }

        Vector2Int position = actor.GetNowXY();
        string id = GetPersistentId(actor);
        filthQuery.AddFilth(WorldFilthType.Waste, position, 22f, id, 0.8f);
        filthQuery.AddFilth(WorldFilthType.Stain, position, 8f, id, 0.55f, wallStain: true);
        actor.ChangesStat(CharacterCondition.EXCRETION, 90f);
        actor.ChangesStat(CharacterCondition.HYGIENE, -25f);
        actor.ApplyMoodFactor("survival:public-accident", "아무 데서나 사고를 냄", -10f, 360f, 1);
        ApplyWitnessMood(actor, position, "끔찍한 사고를 목격함", -4f);
        RecordTaboo(actor, "통제력을 잃고 던전을 오염시켰다");
    }

    private void RecordSafeReliefBlockReason(
        GridMoveFailureReason reason)
    {
        switch (reason)
        {
            case GridMoveFailureReason.StaleStepStart:
                safeReliefStaleStartFailures++;
                break;
            case GridMoveFailureReason.WallBlocked:
                safeReliefWallBlockedFailures++;
                break;
            case GridMoveFailureReason.DoorDenied:
                safeReliefDoorDeniedFailures++;
                break;
            case GridMoveFailureReason.DefenseReservation:
                safeReliefDefenseReservationFailures++;
                break;
            case GridMoveFailureReason.TraversalChanged:
                safeReliefTraversalChangedFailures++;
                break;
        }
    }

    private void RecordSafeReliefOtherFailureReason(
        GridMoveFailureReason reason)
    {
        switch (reason)
        {
            case GridMoveFailureReason.Cancelled:
                safeReliefCancelledMoveFailures++;
                break;
            case GridMoveFailureReason.MissingPath:
                safeReliefMissingPathFailures++;
                break;
            case GridMoveFailureReason.MissingMovementHandler:
                safeReliefMissingMovementHandlerFailures++;
                break;
            case GridMoveFailureReason.GridUnavailable:
                safeReliefGridUnavailableFailures++;
                break;
            case GridMoveFailureReason.InvalidSpeed:
                safeReliefInvalidSpeedFailures++;
                break;
            default:
                safeReliefNoFailureReasonFailures++;
                break;
        }
    }

    private IEnumerator RunDesperateDrink(CharacterActor actor, bool allowWaste, bool safeOnly = false)
    {
        desperateDrinkAttempts++;
        string actorId = GetPersistentId(actor);
        if (gridSystemProvider.TryGetGrid(out Grid grid)
            && TryFindReservableWaterStack(
                grid,
                actor,
                actorId,
                out WorldItemStockCandidate waterStack,
                out Vector2Int approach,
                out Queue<GridMoveStep> path,
                out _,
                countSafeReliefPlan: false))
        {
            try
            {
                yield return MoveNear(actor, approach, 0, path);
                if (actor != null
                    && !actor.IsDead
                    && actor.GetNowXY() == approach
                    && Manhattan(actor.GetNowXY(), waterStack.Position) <= 1)
                {
                    desperateDrinkStackArrivals++;
                    if (itemStackRuntime.TryConsumeStackQuantity(
                            waterStack.StackId,
                            1,
                            out _))
                    {
                        desperateDrinkStackConsumptions++;
                        actor.ChangesStat(CharacterCondition.THIRST, 75f);
                        actor.ApplyMoodFactor(
                            "survival:clean-water",
                            "물을 마심",
                            2f,
                            90f,
                            1);
                        EndActiveBreakdownIfRelieved(actor);
                        yield break;
                    }
                }
                else
                {
                    desperateDrinkStackMoveFailures++;
                }
            }
            finally
            {
                ReleaseSafeReliefApproach(actorId, approach);
            }
        }

        Vector2Int facilityApproach = default;
        if (gridSystemProvider.TryGetGrid(out grid)
            && TryFindReservableWaterFacility(
                grid,
                actor,
                actorId,
                out BuildableObject waterFacility,
                out facilityApproach,
                out Queue<GridMoveStep> facilityPath,
                out _))
        {
            yield return MoveNear(
                actor,
                facilityApproach,
                0,
                facilityPath);
            if (actor != null
                && !actor.IsDead
                && waterFacility != null
                && !waterFacility.IsGridDestroyed
                && actor.GetNowXY() == facilityApproach
                && Manhattan(actor.GetNowXY(), waterFacility.centerPos) <= 1)
            {
                actor.ChangesStat(CharacterCondition.THIRST, 70f);
                EndActiveBreakdownIfRelieved(actor);
                ReleaseSafeReliefApproach(actorId, facilityApproach);
                actor.ApplyMoodFactor("survival:well-water", "수원에서 물을 마심", 1f, 90f, 1);
                yield break;
            }
        }

        if (safeReliefApproachByActor.ContainsKey(actorId))
        {
            ReleaseSafeReliefApproach(actorId, facilityApproach);
        }

        if (waterQuery.TryFindDrinkSource(actor.GetNowXY(), allowFoul: !safeOnly, out WorldWaterSourceSnapshot source)
            && (!safeOnly || source.Quality == WorldWaterQuality.Clean))
        {
            int standDistance = source.TerrainType == GridCellTerrainType.DeepWater ? 1 : 0;
            yield return MoveNear(actor, source.Position, standDistance);
            if (actor != null
                && !actor.IsDead
                && Manhattan(actor.GetNowXY(), source.Position) <= standDistance
                && waterQuery.TryDrink(source.SourceId, 1f, out WorldWaterQuality quality, out float consumed)
                && consumed > 0f)
            {
                actor.ChangesStat(CharacterCondition.THIRST, quality == WorldWaterQuality.Foul ? 45f : 65f);
                EndActiveBreakdownIfRelieved(actor);
                if (quality != WorldWaterQuality.Clean)
                {
                    actor.ApplyDamage(quality == WorldWaterQuality.Foul ? 5f : 2f, "오염된 물");
                    actor.ChangesStat(CharacterCondition.HYGIENE, -12f);
                    AddInfection(actor, quality == WorldWaterQuality.Foul ? 22f : 10f);
                    actor.ApplyMoodFactor("survival:foul-water", "썩은 물을 삼킴", -7f, 240f, 1);
                }
                yield break;
            }
        }

        if (!allowWaste || GetNeed(actor, CharacterCondition.EXCRETION) > 25f)
        {
            yield break;
        }

        Vector2Int position = actor.GetNowXY();
        string id = GetPersistentId(actor);
        filthQuery.AddFilth(WorldFilthType.Waste, position, 12f, id, 0.95f);
        actor.ChangesStat(CharacterCondition.EXCRETION, 70f);
        actor.ChangesStat(CharacterCondition.THIRST, 25f);
        EndActiveBreakdownIfRelieved(actor);
        actor.ChangesStat(CharacterCondition.HYGIENE, -35f);
        actor.ApplyDamage(7f, "체액 오염 섭취");
        AddInfection(actor, 35f);
        actor.ApplyMoodFactor("survival:taboo-drink", "마셔서는 안 될 것을 마심", -14f, 600f, 1);
        RecordTaboo(actor, "갈증 끝에 자신의 오염물을 마셨다");
    }

    private IEnumerator RunDesperateEat(CharacterActor actor)
    {
        if (TryFindEmergencyFood(actor, out WorldItemStackSnapshot food))
        {
            yield return MoveNear(actor, food.Position, 0);
            if (actor != null
                && !actor.IsDead
                && Manhattan(actor.GetNowXY(), food.Position) == 0
                && itemStackRuntime.TryConsumeStackQuantity(food.StackId, 1, out WorldItemStackSnapshot consumed))
            {
                bool humanoid = consumed.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                    || consumed.ItemId == DarkSurvivalItemDefinitions.HumanoidMeatItemId;
                actor.ChangesStat(CharacterCondition.HUNGER, humanoid ? 75f : 55f);
                if (humanoid)
                {
                    ApplyCannibalismConsequences(actor, consumed);
                }
                else if (consumed.ItemId == SurvivalItemDefinitions.TaintedFoodItemId)
                {
                    actor.ApplyDamage(3f, "오염 음식");
                    AddInfection(actor, 12f);
                }
                yield break;
            }
        }

        CharacterActor victim = FindLivingVictim(actor);
        if (victim == null)
        {
            yield break;
        }

        if (TryGetState(actor, out CharacterDeprivationState state))
        {
            state.breakdown.targetId = GetPersistentId(victim);
            state.breakdown.targetGridX = victim.GetNowXY().x;
            state.breakdown.targetGridY = victim.GetNowXY().y;
        }

        while (actor != null && victim != null && !actor.IsDead && !victim.IsDead)
        {
            yield return MoveNear(actor, victim.GetNowXY(), 1);
            if (actor == null || victim == null || actor.IsDead || victim.IsDead)
            {
                break;
            }

            if (Manhattan(actor.GetNowXY(), victim.GetNowXY()) > 1)
            {
                break;
            }

            float damage = Mathf.Max(4f, actor.GetCharacterStat(CharacterStatType.Strength) * 1.2f);
            victim.ApplyDamage(damage, $"굶주린 {actor.Identity?.DisplayName ?? actor.name}의 습격");
            if (!victim.IsDead)
            {
                actor.ApplyDamage(Mathf.Max(1f, victim.GetCharacterStat(CharacterStatType.Strength) * 0.35f), "필사적인 반격");
            }
            yield return CannibalAttackDelay;
        }

        if (victim != null && victim.IsDead)
        {
            yield return CorpseSpawnDelay;
            WorldItemStackSnapshot corpse = FindHumanoidCorpse(victim);
            if (corpse != null
                && itemStackRuntime.TryConsumeStackQuantity(corpse.StackId, 1, out WorldItemStackSnapshot consumed))
            {
                actor.ChangesStat(CharacterCondition.HUNGER, 85f);
                ApplyCannibalismConsequences(actor, consumed);
            }
        }
    }

    private static IEnumerator RunCollapse(CharacterActor actor)
    {
        if (actor == null)
        {
            yield break;
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Started,
            "바닥에 쓰러져 잠듦",
            actionId: "survival/collapse",
            sentiment: -0.65f,
            bubbleEligible: true));
        yield return CollapseDelay;
        if (actor != null && !actor.IsDead)
        {
            actor.ChangesStat(CharacterCondition.SLEEP, 35f);
            actor.ApplyMoodFactor("survival:floor-collapse", "차가운 바닥에서 깨어남", -5f, 180f, 1);
        }
    }

    private IEnumerator RunViolentImpulse(CharacterActor actor)
    {
        if (actor == null)
        {
            yield break;
        }

        actor.ApplyMoodFactor("survival:violent-impulse", "분노에 휩쓸림", -6f, 180f, 1);
        CharacterAiPersonality personality = GetPersonality(actor);
        GetViolentImpulseThresholds(personality, out float vandalThreshold, out float assaultThreshold);
        float choice = breakdownRandom.NextFloat();
        if (choice < vandalThreshold && TryFindVandalismTarget(actor, out BuildableObject building))
        {
            yield return MoveNear(actor, building.centerPos, 1);
            if (actor != null
                && !actor.IsDead
                && building != null
                && !building.IsGridDestroyed
                && !building.IsDamaged
                && Manhattan(actor.GetNowXY(), building.centerPos) <= 1)
            {
                building.SetDamaged(true);
                actor.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Damaged,
                    $"{GetBuildingLabel(building)}을 파손함",
                    building,
                    actionId: "survival:violent-vandalism",
                    reasonCode: "mental-instability",
                    value: 1f,
                    bubbleEligible: true));
                ApplyWitnessMood(actor, actor.GetNowXY(), "붕괴자의 난동을 목격함", -5f);
                yield return ViolentActionDelay;
                yield break;
            }
        }

        if (choice < assaultThreshold)
        {
            CharacterActor victim = FindLivingVictim(actor);
            if (victim != null)
            {
                yield return MoveNear(actor, victim.GetNowXY(), 1);
                if (actor != null
                    && victim != null
                    && !actor.IsDead
                    && !victim.IsDead
                    && Manhattan(actor.GetNowXY(), victim.GetNowXY()) <= 1)
                {
                    float damage = Mathf.Clamp(
                        2f + actor.GetCharacterStat(CharacterStatType.Strength) * 0.45f,
                        3f,
                        10f);
                    victim.ApplyDamage(damage, $"붕괴한 {actor.Identity?.DisplayName ?? actor.name}의 폭행");
                    actor.AddActivity(CharacterActivityEvent.Create(
                        CharacterActivityKinds.Combat,
                        CharacterActivityOutcomes.Damaged,
                        $"{victim.Identity?.DisplayName ?? victim.name}에게 달려들었다",
                        actionId: "survival:violent-assault",
                        targetId: GetPersistentId(victim),
                        reasonCode: "mental-instability",
                        value: damage,
                        sentiment: -1f,
                        bubbleEligible: true));
                    ApplyWitnessMood(actor, victim.GetNowXY(), "이성을 잃은 폭행을 목격함", -7f);
                    yield return ViolentActionDelay;
                    yield break;
                }
            }
        }

        if (IdleBehaviorRunner.TryRunDefault(actor, 2.2f, true, out string behavior, out _))
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Health,
                CharacterActivityOutcomes.Started,
                $"불안정하게 {behavior}",
                actionId: "survival/mental-breakdown",
                sentiment: -0.75f,
                bubbleEligible: true));
        }
        yield return BreakdownIdleDelay;
        actor.ChangesStat(CharacterCondition.FUN, 8f);
    }

    private bool TryFindVandalismTarget(CharacterActor actor, out BuildableObject target)
    {
        target = null;
        if (actor == null)
        {
            return false;
        }

        Vector2Int origin = actor.GetNowXY();
        int bestDistance = int.MaxValue;
        IReadOnlyList<BuildableObject> buildings = worldRegistry.Buildings;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject candidate = buildings[index];
            if (candidate == null
                || candidate.IsGridDestroyed
                || candidate.IsDamaged
                || candidate.IsGridMovement)
            {
                continue;
            }

            int distance = Manhattan(origin, candidate.centerPos);
            if (distance >= bestDistance)
            {
                continue;
            }

            target = candidate;
            bestDistance = distance;
        }

        return target != null;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        return building?.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building != null ? building.name : "시설";
    }

    private IEnumerator MoveNear(
        CharacterActor actor,
        Vector2Int target,
        int distance,
        Queue<GridMoveStep> preparedPath = null)
    {
        if (actor == null
            || actor.IsDead
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !actor.TryGetAbility(out AbilityMove move))
        {
            yield break;
        }

        Vector2Int start = actor.GetNowXY();
        if (Manhattan(start, target) <= distance)
        {
            yield break;
        }

        IGridPathSearchBroker broker = actor.PathSearchBroker;
        if (broker == null)
        {
            move.MarkGridMoveFailure(GridMoveFailureReason.MissingPath);
            yield break;
        }

        Vector2Int preferredAdjacent = start.x <= target.x
            ? target + Vector2Int.left
            : target + Vector2Int.right;
        Vector2Int alternateAdjacent = start.x <= target.x
            ? target + Vector2Int.right
            : target + Vector2Int.left;
        int destinationCount = distance <= 0 ? 1 : 2;

        Queue<GridMoveStep> path = preparedPath;
        GridTraversalContext traversalContext =
            GridTraversalContext.ForCharacter(actor);
        for (int destinationIndex = 0;
             destinationIndex < destinationCount && path == null;
             destinationIndex++)
        {
            Vector2Int destination;
            if (distance <= 0)
            {
                destination = target;
            }
            else
            {
                destination = destinationIndex == 0
                    ? preferredAdjacent
                    : alternateAdjacent;
            }

            if (!grid.IsValidGridPos(destination)
                || !grid.IsWalkable(destination))
            {
                continue;
            }

            for (int attempt = 0; attempt < EmergencyPathRetryFrames; attempt++)
            {
                if (actor == null || actor.IsDead)
                {
                    yield break;
                }

                GridPathRequestStatus status = broker.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    destination,
                    out path,
                    GridPathSearchPriority.Urgent,
                    traversalContext);
                if (status == GridPathRequestStatus.Reachable)
                {
                    break;
                }

                if (status == GridPathRequestStatus.Unreachable)
                {
                    path = null;
                    break;
                }

                yield return null;
            }

            if (path != null && path.Count == 0)
            {
                path = null;
            }
        }
        if (path == null || path.Count == 0)
        {
            move.MarkGridMoveFailure(GridMoveFailureReason.MissingPath);
            if (TryGetState(actor, out CharacterDeprivationState state))
            {
                state.breakdown.targetId = string.Empty;
                state.breakdown.lastReplanReason = "경로가 막혀 다른 대상을 찾음";
            }
            yield break;
        }

        yield return move.MoveByPath(path);
    }

    private bool TryChooseAccidentPosition(CharacterActor actor, out Vector2Int position)
    {
        position = actor != null ? actor.GetNowXY() : default;
        if (actor == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int origin = actor.GetNowXY();
        GridCell best = null;
        int bestPriority = int.MaxValue;
        int bestDistance = int.MaxValue;
        int minX = Mathf.Max(0, origin.x - AccidentSearchRadius);
        int maxX = Mathf.Min(grid.width - 1, origin.x + AccidentSearchRadius);
        for (int x = minX; x <= maxX; x++)
        {
            Vector2Int candidate = new Vector2Int(x, origin.y);
            GridCell cell = grid.GetGridCell(candidate);
            if (cell == null || !grid.IsWalkable(candidate))
            {
                continue;
            }

            int priority = GetAccidentLocationPriority(grid, cell);
            int candidateDistance = Mathf.Abs(x - origin.x);
            if (best != null
                && (priority > bestPriority
                    || (priority == bestPriority
                        && candidateDistance >= bestDistance)))
            {
                continue;
            }

            best = cell;
            bestPriority = priority;
            bestDistance = candidateDistance;
        }
        if (best == null)
        {
            return false;
        }

        position = best.Position;
        return true;
    }

    private bool TryCreateSafeDrinkPlan(
        CharacterActor actor,
        string actorId,
        out SafeDrinkPlan plan)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefPlanProfilerMarker.Auto();
        plan = default;
        if (actor == null
            || string.IsNullOrWhiteSpace(actorId)
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        if (TryFindReservableWaterFacility(
                grid,
                actor,
                actorId,
                out BuildableObject facility,
                out Vector2Int facilityApproach,
                out Queue<GridMoveStep> facilityPath,
                out bool facilitySearchPending))
        {
            plan = new SafeDrinkPlan(
                SafeDrinkTargetKind.Facility,
                facility.centerPos,
                facilityApproach,
                facilityPath,
                facility: facility);
            return true;
        }
        if (facilitySearchPending)
        {
            return false;
        }

        if (TryFindReservableWaterStack(
                grid,
                actor,
                actorId,
                out WorldItemStockCandidate stack,
                out Vector2Int stackApproach,
                out Queue<GridMoveStep> stackPath,
                out bool stackSearchPending))
        {
            plan = new SafeDrinkPlan(
                SafeDrinkTargetKind.ItemStack,
                stack.Position,
                stackApproach,
                stackPath,
                stack.StackId);
            return true;
        }
        if (stackSearchPending)
        {
            return false;
        }

        if (waterQuery.TryFindDrinkSource(
                actor.GetNowXY(),
                allowFoul: false,
                out WorldWaterSourceSnapshot source)
            && source.Quality == WorldWaterQuality.Clean
            && TryFindReachableSafeReliefApproach(
                grid,
                actor,
                actorId,
                source.Position,
                includeTarget:
                    source.TerrainType != GridCellTerrainType.DeepWater,
                allowPathSearch: true,
                out Vector2Int sourceApproach,
                out Queue<GridMoveStep> sourcePath)
                == SafeReliefApproachSearchStatus.Reachable)
        {
            ReserveSafeReliefApproach(actorId, sourceApproach);
            plan = new SafeDrinkPlan(
                SafeDrinkTargetKind.WorldSource,
                source.Position,
                sourceApproach,
                sourcePath,
                source.SourceId);
            return true;
        }

        return false;
    }

    private bool TryFindReservableWaterStack(
        Grid grid,
        CharacterActor actor,
        string actorId,
        out WorldItemStockCandidate selected,
        out Vector2Int selectedApproach,
        out Queue<GridMoveStep> selectedPath,
        out bool searchPending,
        bool countSafeReliefPlan = true)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefStackProfilerMarker.Auto();
        selected = default;
        selectedApproach = default;
        selectedPath = null;
        searchPending = false;
        itemStackRuntime.CopyAvailableStockCandidates(
            StockCategory.Water,
            safeDrinkStockCandidates);

        Vector2Int origin = actor.GetNowXY();
        bool hasSameFloorCandidate = false;
        for (int index = 0; index < safeDrinkStockCandidates.Count; index++)
        {
            WorldItemStockCandidate candidate =
                safeDrinkStockCandidates[index];
            if (candidate.IsValid && candidate.Position.y == origin.y)
            {
                hasSameFloorCandidate = true;
                break;
            }
        }

        for (int searchPass = 0;
             searchPass < 2 && !selected.IsValid;
             searchPass++)
        {
            bool allowPathSearch = searchPass > 0;
            bool sameFloorHasAvailableApproach = false;
            int previousDistance = -1;
            int previousIndex = -1;
            for (int rank = 0; rank < safeDrinkStockCandidates.Count; rank++)
            {
                int nextIndex = -1;
                int nextDistance = int.MaxValue;
                for (int index = 0;
                     index < safeDrinkStockCandidates.Count;
                     index++)
                {
                    WorldItemStockCandidate candidateEntry =
                        safeDrinkStockCandidates[index];
                    if (!candidateEntry.IsValid)
                    {
                        continue;
                    }

                    int distance = GetSafeReliefDistance(
                        origin,
                        candidateEntry.Position,
                        grid.width);
                    if (distance < previousDistance
                        || distance == previousDistance
                            && index <= previousIndex
                        || distance > nextDistance
                        || distance == nextDistance
                            && nextIndex >= 0
                            && index >= nextIndex)
                    {
                        continue;
                    }

                    nextIndex = index;
                    nextDistance = distance;
                }

                if (nextIndex < 0)
                {
                    break;
                }

                previousDistance = nextDistance;
                previousIndex = nextIndex;
                WorldItemStockCandidate selectedCandidate =
                    safeDrinkStockCandidates[nextIndex];
                bool isSameFloor =
                    selectedCandidate.Position.y == origin.y;
                if (!isSameFloor
                    && hasSameFloorCandidate
                    && !sameFloorHasAvailableApproach)
                {
                    break;
                }

                if (isSameFloor
                    && HasAvailableSafeReliefApproach(
                        grid,
                        actorId,
                        origin,
                        selectedCandidate.Position,
                        includeTarget:
                            selectedCandidate.State !=
                                WorldItemStackState.Stored))
                {
                    sameFloorHasAvailableApproach = true;
                }

                SafeReliefApproachSearchStatus approachStatus =
                    TryFindReachableSafeReliefApproach(
                        grid,
                        actor,
                        actorId,
                        selectedCandidate.Position,
                        includeTarget:
                            selectedCandidate.State !=
                                WorldItemStackState.Stored,
                        allowPathSearch: allowPathSearch,
                        out Vector2Int approach,
                        out Queue<GridMoveStep> path);
                if (approachStatus ==
                    SafeReliefApproachSearchStatus.Pending)
                {
                    searchPending = true;
                    return false;
                }

                if (approachStatus !=
                    SafeReliefApproachSearchStatus.Reachable)
                {
                    continue;
                }

                selected = selectedCandidate;
                selectedApproach = approach;
                selectedPath = path;
                break;
            }
        }

        if (!selected.IsValid)
        {
            return false;
        }

        ReserveSafeReliefApproach(actorId, selectedApproach);
        if (countSafeReliefPlan
            && selected.State == WorldItemStackState.Stored)
        {
            safeReliefStoredStackPlans++;
        }
        return true;
    }

    private bool HasAvailableSafeReliefApproach(
        Grid grid,
        string actorId,
        Vector2Int origin,
        Vector2Int target,
        bool includeTarget)
    {
        for (int candidateIndex = 0; candidateIndex < 3; candidateIndex++)
        {
            if (candidateIndex == 0 && !includeTarget)
            {
                continue;
            }

            Vector2Int candidatePosition = candidateIndex switch
            {
                0 => target,
                1 => target + Vector2Int.left,
                _ => target + Vector2Int.right
            };
            if (!grid.IsValidGridPos(candidatePosition)
                || (candidatePosition != origin
                    && !grid.IsWalkable(candidatePosition))
                || (safeReliefApproachOwners.TryGetValue(
                        candidatePosition,
                        out string owner)
                    && !string.Equals(
                        owner,
                        actorId,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryFindReservableWaterFacility(
        Grid grid,
        CharacterActor actor,
        string actorId,
        out BuildableObject selected,
        out Vector2Int selectedApproach,
        out Queue<GridMoveStep> selectedPath,
        out bool searchPending)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefFacilityProfilerMarker.Auto();
        selected = null;
        selectedApproach = default;
        selectedPath = null;
        searchPending = false;
        Vector2Int origin = actor.GetNowXY();
        IReadOnlyList<BuildableObject> buildings =
            facilityCandidateCache.GetWorkCandidates(
                grid,
                FacilityWorkType.DrawWater);
        if (buildings.Count == 0
            && facilityCandidateCache.HasPendingIndexBuild)
        {
            searchPending = true;
            return false;
        }

        bool hasSameFloorCandidate = false;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject candidate = buildings[index];
            if (IsUsableWaterFacility(candidate)
                && candidate.centerPos.y == origin.y)
            {
                hasSameFloorCandidate = true;
                break;
            }
        }

        for (int searchPass = 0;
             searchPass < 2 && selected == null;
             searchPass++)
        {
            bool allowPathSearch = searchPass > 0;
            bool sameFloorHasAvailableApproach = false;
            int previousDistance = -1;
            int previousIndex = -1;
            for (int rank = 0; rank < buildings.Count; rank++)
            {
                int nextIndex = -1;
                int nextDistance = int.MaxValue;
                for (int index = 0; index < buildings.Count; index++)
                {
                    BuildableObject candidateEntry = buildings[index];
                    if (!IsUsableWaterFacility(candidateEntry))
                    {
                        continue;
                    }

                    int distance = GetSafeReliefDistance(
                        origin,
                        candidateEntry.centerPos,
                        grid.width);
                    if (distance < previousDistance
                        || distance == previousDistance
                            && index <= previousIndex
                        || distance > nextDistance
                        || distance == nextDistance
                            && nextIndex >= 0
                            && index >= nextIndex)
                    {
                        continue;
                    }

                    nextIndex = index;
                    nextDistance = distance;
                }

                if (nextIndex < 0)
                {
                    break;
                }

                previousDistance = nextDistance;
                previousIndex = nextIndex;
                BuildableObject selectedCandidate = buildings[nextIndex];
                bool isSameFloor =
                    selectedCandidate.centerPos.y == origin.y;
                if (!isSameFloor
                    && hasSameFloorCandidate
                    && !sameFloorHasAvailableApproach)
                {
                    break;
                }

                if (isSameFloor
                    && HasAvailableSafeReliefApproach(
                        grid,
                        actorId,
                        origin,
                        selectedCandidate.centerPos,
                        includeTarget: false))
                {
                    sameFloorHasAvailableApproach = true;
                }

                SafeReliefApproachSearchStatus approachStatus =
                    TryFindReachableSafeReliefApproach(
                        grid,
                        actor,
                        actorId,
                        selectedCandidate.centerPos,
                        includeTarget: false,
                        allowPathSearch: allowPathSearch,
                        out Vector2Int approach,
                        out Queue<GridMoveStep> path);
                if (approachStatus ==
                    SafeReliefApproachSearchStatus.Pending)
                {
                    searchPending = true;
                    return false;
                }

                if (approachStatus !=
                    SafeReliefApproachSearchStatus.Reachable)
                {
                    continue;
                }

                selected = selectedCandidate;
                selectedApproach = approach;
                selectedPath = path;
                break;
            }
        }

        if (selected == null)
        {
            return false;
        }

        ReserveSafeReliefApproach(actorId, selectedApproach);
        return true;
    }

    private bool IsUsableWaterFacility(BuildableObject candidate)
    {
        return candidate != null
            && candidate.gameObject.activeInHierarchy
            && !candidate.IsGridDestroyed
            && candidate.BuildingData?.GetAbility<BuildingWaterSourceAbility>() != null
            && survivalFoodRuntime.HasSurvivalWorkAvailable(
                candidate,
                BuiltInWorkTypeIds.DrawWater);
    }

    private SafeReliefApproachSearchStatus TryFindReachableSafeReliefApproach(
        Grid grid,
        CharacterActor actor,
        string actorId,
        Vector2Int target,
        bool includeTarget,
        bool allowPathSearch,
        out Vector2Int approach,
        out Queue<GridMoveStep> path)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefApproachProfilerMarker.Auto();
        approach = default;
        path = null;
        if (grid == null
            || actor == null
            || string.IsNullOrWhiteSpace(actorId)
            || actor.PathSearchBroker == null)
        {
            return SafeReliefApproachSearchStatus.Invalid;
        }

        Vector2Int origin = actor.GetNowXY();
        if (safeReliefApproachByActor.TryGetValue(
                actorId,
                out Vector2Int existing))
        {
            return TryPrepareSafeReliefApproach(
                grid,
                actor,
                existing,
                allowPathSearch,
                out approach,
                out path);
        }

        int previousDistance = -1;
        int previousIndex = -1;
        for (int rank = 0; rank < 3; rank++)
        {
            int nextIndex = -1;
            int nextDistance = int.MaxValue;
            for (int candidateIndex = 0; candidateIndex < 3; candidateIndex++)
            {
                if (candidateIndex == 0 && !includeTarget)
                {
                    continue;
                }

                Vector2Int candidatePosition = candidateIndex switch
                {
                    0 => target,
                    1 => target + Vector2Int.left,
                    _ => target + Vector2Int.right
                };
                if (!grid.IsValidGridPos(candidatePosition)
                    || (candidatePosition != origin && !grid.IsWalkable(candidatePosition))
                    || (safeReliefApproachOwners.TryGetValue(
                            candidatePosition,
                            out string owner)
                        && !string.Equals(
                            owner,
                            actorId,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                int distance = Manhattan(origin, candidatePosition);
                if (distance < previousDistance
                    || distance == previousDistance && candidateIndex <= previousIndex
                    || distance > nextDistance
                    || distance == nextDistance
                        && nextIndex >= 0
                        && candidateIndex >= nextIndex)
                {
                    continue;
                }

                nextIndex = candidateIndex;
                nextDistance = distance;
            }

            if (nextIndex < 0)
            {
                break;
            }

            previousDistance = nextDistance;
            previousIndex = nextIndex;
            Vector2Int selectedPosition = nextIndex switch
            {
                0 => target,
                1 => target + Vector2Int.left,
                _ => target + Vector2Int.right
            };
            SafeReliefApproachSearchStatus searchStatus =
                TryPrepareSafeReliefApproach(
                    grid,
                    actor,
                    selectedPosition,
                    allowPathSearch,
                    out Vector2Int preparedApproach,
                    out Queue<GridMoveStep> preparedPath);
            if (searchStatus == SafeReliefApproachSearchStatus.Reachable)
            {
                approach = preparedApproach;
                path = preparedPath;
                return SafeReliefApproachSearchStatus.Reachable;
            }

            if (searchStatus == SafeReliefApproachSearchStatus.Pending)
            {
                approach = preparedApproach;
                path = preparedPath;
                return SafeReliefApproachSearchStatus.Pending;
            }

        }

        return SafeReliefApproachSearchStatus.Invalid;
    }

    private SafeReliefApproachSearchStatus TryPrepareSafeReliefApproach(
        Grid grid,
        CharacterActor actor,
        Vector2Int destination,
        bool allowPathSearch,
        out Vector2Int approach,
        out Queue<GridMoveStep> path)
    {
        approach = destination;
        path = null;
        Vector2Int origin = actor.GetNowXY();
        if (origin == destination)
        {
            return SafeReliefApproachSearchStatus.Reachable;
        }

        if (!grid.IsValidGridPos(destination)
            || !grid.IsWalkable(destination)
            || actor.PathSearchBroker == null)
        {
            return SafeReliefApproachSearchStatus.Invalid;
        }

        if (TryBuildDirectHorizontalSafeReliefPath(
                grid,
                actor,
                origin,
                destination,
                out path))
        {
            return SafeReliefApproachSearchStatus.Reachable;
        }

        if (!allowPathSearch)
        {
            return SafeReliefApproachSearchStatus.Invalid;
        }

        GridPathRequestStatus requestStatus;
        using (SafeReliefExactPathProfilerMarker.Auto())
        {
            requestStatus = actor.PathSearchBroker.RequestMovePathTo(
                grid,
                origin,
                destination,
                out path,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(actor));
        }
        if (requestStatus == GridPathRequestStatus.Pending)
        {
            return SafeReliefApproachSearchStatus.Pending;
        }

        return requestStatus == GridPathRequestStatus.Reachable
            && path != null
            && path.Count > 0
                ? SafeReliefApproachSearchStatus.Reachable
                : SafeReliefApproachSearchStatus.Invalid;
    }

    private bool TryBuildDirectHorizontalSafeReliefPath(
        Grid grid,
        CharacterActor actor,
        Vector2Int origin,
        Vector2Int destination,
        out Queue<GridMoveStep> path)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefDirectPathProfilerMarker.Auto();
        path = null;
        if (grid == null
            || actor == null
            || origin.y != destination.y
            || origin.x == destination.x)
        {
            return false;
        }

        int direction = destination.x > origin.x ? 1 : -1;
        int stepCount = Mathf.Abs(destination.x - origin.x);
        GridTraversalContext traversalContext =
            GridTraversalContext.ForCharacter(actor);
        Vector2Int current = origin;
        for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            Vector2Int next = new Vector2Int(
                current.x + direction,
                current.y);
            if (!grid.IsValidGridPos(next)
                || !grid.IsWalkable(next)
                || grid.IsMovementBlockedByWall(next)
                || (doorAccessQuery != null
                    && !doorAccessQuery.CanTraverse(
                        grid,
                        next,
                        traversalContext,
                        out _)))
            {
                return false;
            }

            current = next;
        }

        Queue<GridMoveStep> directPath =
            GridSearchScratch.RentMovePath(stepCount);
        current = origin;
        for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            Vector2Int next = new Vector2Int(
                current.x + direction,
                current.y);
            directPath.Enqueue(new GridMoveStep(
                current,
                next,
                grid.GetGridCell(next)?.GetTopOccupant(),
                null,
                GridMoveType.Walk));
            current = next;
        }

        path = directPath;
        return path.Count > 0;
    }

    private void ReserveSafeReliefApproach(
        string actorId,
        Vector2Int approach)
    {
        safeReliefApproachOwners[approach] = actorId;
        safeReliefApproachByActor[actorId] = approach;
    }

    private void ReleaseSafeReliefApproach(
        string actorId,
        Vector2Int expectedApproach)
    {
        if (string.IsNullOrWhiteSpace(actorId)
            || !safeReliefApproachByActor.TryGetValue(
                actorId,
                out Vector2Int approach))
        {
            return;
        }

        safeReliefApproachByActor.Remove(actorId);
        if (approach == expectedApproach
            && safeReliefApproachOwners.TryGetValue(
                approach,
                out string owner)
            && string.Equals(owner, actorId, StringComparison.Ordinal))
        {
            safeReliefApproachOwners.Remove(approach);
        }
    }

    private static float GetSafeReliefRetryDelay(string actorId)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < actorId.Length; index++)
            {
                hash = (hash * 31) + actorId[index];
            }

            int stagger = (hash & int.MaxValue) % 76;
            return SafeReliefRetrySeconds + stagger * 0.01f;
        }
    }

    private static int GetSafeReliefDistance(
        Vector2Int origin,
        Vector2Int target,
        int gridWidth)
    {
        int horizontal = Mathf.Abs(origin.x - target.x);
        int floorDistance = Mathf.Abs(origin.y - target.y);
        return horizontal + floorDistance * Mathf.Max(1, gridWidth);
    }

    private bool CanStartSafeReliefThisFrame()
    {
        int frame = gameClock.FrameCount;
        if (safeReliefStartFrame != frame)
        {
            safeReliefStartFrame = frame;
            safeReliefStartsThisFrame = 0;
        }

        return safeReliefStartsThisFrame
            < MaximumSafeReliefStartsPerFrame;
    }

    private void RecordSafeReliefStart()
    {
        CanStartSafeReliefThisFrame();
        safeReliefStartsThisFrame++;
    }

    private bool TryFindEmergencyFood(CharacterActor actor, out WorldItemStackSnapshot food)
    {
        food = null;
        return actor != null
            && itemStackRuntime.TryFindBestAvailableStack(
                actor.GetNowXY(),
                EmergencyFoodRankSelector,
                out food);
    }

    private static int GetEmergencyFoodRank(string itemId)
    {
        if (itemId == SurvivalItemDefinitions.TaintedFoodItemId) return 0;
        if (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(itemId, out _)) return 1;
        if (itemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId) return 2;
        if (itemId == DarkSurvivalItemDefinitions.HumanoidMeatItemId) return 3;
        return DungeonItemCatalogSO.TryGetStockCategoryFromItemId(itemId, out StockCategory category)
            && category == StockCategory.Food ? 4 : int.MaxValue;
    }

    private CharacterActor FindLivingVictim(CharacterActor attacker)
    {
        if (attacker == null)
        {
            return null;
        }

        CharacterActor best = null;
        float bestHealthRatio = float.PositiveInfinity;
        int bestNearbyCount = int.MaxValue;
        float bestSentiment = float.PositiveInfinity;
        int bestDistance = int.MaxValue;
        Vector2Int origin = attacker.GetNowXY();
        IReadOnlyList<CharacterActor> characters = worldRegistry.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor candidate = characters[index];
            if (!IsEligibleHumanoid(candidate)
                || candidate == attacker
                || candidate.IsDead)
            {
                continue;
            }

            float healthRatio =
                candidate.CurrentHealth / Mathf.Max(1f, candidate.MaxHealth);
            int nearbyCount = CountNearbyHumanoids(candidate, 3);
            float sentiment =
                attacker.SocialMemory?.GetRelationshipSentiment(candidate) ?? 0f;
            int distance = Manhattan(origin, candidate.GetNowXY());
            if (best != null
                && (healthRatio > bestHealthRatio
                    || Mathf.Approximately(healthRatio, bestHealthRatio)
                    && (nearbyCount > bestNearbyCount
                        || nearbyCount == bestNearbyCount
                        && (sentiment > bestSentiment
                            || Mathf.Approximately(sentiment, bestSentiment)
                            && distance >= bestDistance))))
            {
                continue;
            }

            best = candidate;
            bestHealthRatio = healthRatio;
            bestNearbyCount = nearbyCount;
            bestSentiment = sentiment;
            bestDistance = distance;
        }

        return best;
    }

    private int CountNearbyHumanoids(CharacterActor center, int radius)
    {
        if (center == null)
        {
            return 0;
        }

        int count = 0;
        Vector2Int origin = center.GetNowXY();
        IReadOnlyList<CharacterActor> characters = worldRegistry.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor candidate = characters[index];
            if (IsEligibleHumanoid(candidate)
                && candidate != center
                && !candidate.IsDead
                && Manhattan(origin, candidate.GetNowXY()) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private WorldItemStackSnapshot FindHumanoidCorpse(CharacterActor victim)
    {
        if (victim == null)
        {
            return null;
        }

        string victimId = GetPersistentId(victim);
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            itemStackRuntime.GetStacksAt(victim.GetNowXY(), includeStored: true);
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack != null
                && stack.ItemId ==
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                && string.Equals(
                    stack.SourceCharacterId,
                    victimId,
                    StringComparison.Ordinal))
            {
                return stack;
            }
        }

        return null;
    }

    private void ApplyCannibalismConsequences(CharacterActor actor, WorldItemStackSnapshot consumed)
    {
        bool sameSpecies = !string.IsNullOrWhiteSpace(consumed.SourceSpeciesTag)
            && string.Equals(actor.Identity?.SpeciesTag, consumed.SourceSpeciesTag, StringComparison.OrdinalIgnoreCase);
        CharacterAiPersonality personality = GetPersonality(actor);
        float conscience01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.selfCare)
                + Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
                + Mathf.InverseLerp(0.25f, 2f, personality.routineAdherence)) / 3f
            : 0.5f;
        float appetite01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
                + Mathf.InverseLerp(0.25f, 2f, personality.noveltySeeking)) * 0.5f
            : 0.5f;
        float mood = (sameSpecies ? -18f : -11f) * Mathf.Lerp(0.45f, 1.25f, conscience01);
        string reaction = appetite01 > 0.72f && conscience01 < 0.45f
            ? "금기의 맛을 다시 떠올림"
            : conscience01 < 0.35f
                ? "금기에 무감각해짐"
                : sameSpecies ? "동족을 먹었다" : "인간형 사체를 먹었다";
        actor.ApplyMoodFactor(
            sameSpecies ? "survival:same-species-cannibalism" : "survival:cannibalism",
            reaction,
            mood,
            900f,
            1);
        actor.ChangesStat(CharacterCondition.HYGIENE, -20f);
        AddInfection(actor, sameSpecies ? 20f : 12f);
        string victim = string.IsNullOrWhiteSpace(consumed.SourceDisplayName) ? "이름 모를 사체" : consumed.SourceDisplayName;
        RecordTaboo(actor, $"극한의 굶주림 속에서 {victim}을 먹었다");
        ApplyWitnessMood(
            actor,
            actor.GetNowXY(),
            "금기의 포식을 목격함",
            sameSpecies ? -12f : -8f,
            permanentMemory: true);
    }

    private void ApplyWitnessMood(
        CharacterActor source,
        Vector2Int position,
        string label,
        float mood,
        bool permanentMemory = false)
    {
        foreach (CharacterActor witness in worldRegistry.Characters)
        {
            if (!IsEligibleHumanoid(witness)
                || witness == source
                || witness.IsDead
                || Manhattan(witness.GetNowXY(), position) > 4)
            {
                continue;
            }

            witness.ApplyMoodFactor($"survival:witness:{GetPersistentId(source)}", label, mood, 360f, 1);
            witness.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Relationship,
                "survival/taboo-witness",
                GetPersistentId(source),
                label,
                mood);
            if (permanentMemory)
            {
                witness.SocialMemory?.RememberCharacterExperience(
                    source,
                    Mathf.Clamp(mood / 12f, -1f, 1f),
                    label,
                    durationSeconds: 0f);
            }
        }
    }

    private int GetAccidentLocationPriority(Grid grid, GridCell cell)
    {
        if (cell.AreaType == GridCellAreaType.ExteriorPath)
        {
            return 0;
        }

        if (cell.HasOccupantInLayer(GridLayer.Hallway))
        {
            return 100;
        }

        return roomLayoutCache.TryGetRoom(grid, cell.Position, out RoomInstance room)
            ? 200 + Mathf.RoundToInt(room.GetQualityScore() * 100f)
            : 350;
    }

    private static void GetViolentImpulseThresholds(
        CharacterAiPersonality personality,
        out float vandalThreshold,
        out float assaultThreshold)
    {
        float risk01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
            : 0.5f;
        float order01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
            : 0.5f;
        float social01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.sociability)
            : 0.5f;
        float vandalWeight = 0.25f + (1f - order01) * 0.35f;
        float assaultWeight = 0.2f + risk01 * 0.4f + (1f - social01) * 0.1f;
        float restlessWeight = 0.2f + (1f - risk01) * 0.25f;
        float total = vandalWeight + assaultWeight + restlessWeight;
        vandalThreshold = vandalWeight / total;
        assaultThreshold = vandalThreshold + assaultWeight / total;
    }

    private static CharacterAiPersonality GetPersonality(CharacterActor actor)
    {
        return actor != null && actor.Identity != null && actor.Identity.Data != null
            ? actor.Identity.Data.aiPersonality
            : null;
    }

    public void AddInfectionBurden(CharacterActor actor, float amount)
    {
        AddInfection(actor, amount);
    }

    public void ReduceInfectionBurden(CharacterActor actor, float amount)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        CharacterDeprivationState state = EnsureState(actor);
        state.infectionBurden = Mathf.Max(0f, state.infectionBurden - amount);
        DeprivationBurdenSaveData contamination = GetBurden(
            state,
            DeprivationKind.Contamination);
        contamination.burden = Mathf.Max(
            0f,
            contamination.burden - amount * 0.25f);
    }

    private void AddMentalInstabilityBurden(
        CharacterActor actor,
        float amount)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        CharacterDeprivationState state = EnsureState(actor);
        DeprivationBurdenSaveData burden = GetBurden(
            state,
            DeprivationKind.MentalInstability);
        burden.burden = Mathf.Clamp(
            burden.burden + amount,
            0f,
            MaximumBurden);
    }

    private void AddInfection(CharacterActor actor, float amount)
    {
        CharacterDeprivationState state = EnsureState(actor);
        state.infectionBurden = Mathf.Clamp(state.infectionBurden + Mathf.Max(0f, amount), 0f, 100f);
        GetBurden(state, DeprivationKind.Contamination).burden = Mathf.Clamp(
            GetBurden(state, DeprivationKind.Contamination).burden + amount * 0.5f,
            0f,
            100f);
    }

    private void EndBreakdown(
        CharacterActor actor,
        CharacterDeprivationState state,
        string reason,
        float reduceCauseTo)
    {
        DeprivationBurdenSaveData cause = GetBurden(state, state.breakdown.cause);
        cause.burden = Mathf.Min(cause.burden, reduceCauseTo);
        state.breakdown.active = false;
        state.breakdown.targetId = string.Empty;
        state.breakdown.lastReplanReason = reason ?? string.Empty;
        actor?.Stats?.RemoveMoodFactor("survival:breakdown");
        actor?.Brain?.EndExternallyDrivenAction(clearFailures: true);
    }

    private void EndActiveBreakdownIfRelieved(CharacterActor actor)
    {
        if (actor == null
            || !TryGetState(actor, out CharacterDeprivationState state)
            || !state.breakdown.active
            || !IsCauseRelieved(actor, state.breakdown.cause))
        {
            return;
        }

        EndBreakdown(actor, state, "욕구가 충족됨", reduceCauseTo: 45f);
    }

    private void DispatchAutomaticSuppression(CharacterActor breakdownActor)
    {
        foreach (CharacterActor guard in worldRegistry.Characters)
        {
            if (!IsEligibleHumanoid(guard)
                || guard == breakdownActor
                || !guard.TryGetAbility(out AbilityWork work)
                || work.HasPrioritySuppressTarget
                || !work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard))
            {
                continue;
            }

            work.TrySetPrioritySuppressTarget(breakdownActor, null, out _);
        }
    }

    private CharacterDeprivationState EnsureState(CharacterActor actor)
    {
        string id = GetPersistentId(actor);
        if (!states.TryGetValue(id, out CharacterDeprivationState state))
        {
            state = new CharacterDeprivationState { persistentId = id };
            states[id] = state;
        }

        state.burdens ??= new List<DeprivationBurdenSaveData>();
        state.breakdown ??= new CharacterBreakdownState();
        state.tabooMemories ??= new List<string>();
        NormalizeBurdens(state.burdens);
        return state;
    }

    private bool TryGetState(CharacterActor actor, out CharacterDeprivationState state)
    {
        state = null;
        return actor != null && states.TryGetValue(GetPersistentId(actor), out state);
    }

    private static DeprivationBurdenSaveData GetBurden(CharacterDeprivationState state, DeprivationKind kind)
    {
        state.burdens ??= new List<DeprivationBurdenSaveData>();
        int index = (int)kind;
        if (index < 0 || index >= BurdenKindCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported deprivation kind.");
        }

        if (state.burdens.Count != BurdenKindCount
            || state.burdens[index] == null
            || state.burdens[index].kind != kind)
        {
            NormalizeBurdens(state.burdens);
        }

        return state.burdens[index];
    }

    private static void NormalizeBurdens(List<DeprivationBurdenSaveData> burdens)
    {
        bool isNormalized = burdens.Count == BurdenKindCount;
        if (isNormalized)
        {
            for (int i = 0; i < BurdenKindCount; i++)
            {
                DeprivationBurdenSaveData burden = burdens[i];
                if (burden == null || (int)burden.kind != i)
                {
                    isNormalized = false;
                    break;
                }
            }
        }

        if (isNormalized)
        {
            return;
        }

        var normalized = new DeprivationBurdenSaveData[BurdenKindCount];
        for (int i = 0; i < burdens.Count; i++)
        {
            DeprivationBurdenSaveData burden = burdens[i];
            int kindIndex = burden != null ? (int)burden.kind : -1;
            if (kindIndex >= 0 && kindIndex < BurdenKindCount)
            {
                normalized[kindIndex] = burden;
            }
        }

        burdens.Clear();
        for (int i = 0; i < BurdenKindCount; i++)
        {
            burdens.Add(
                normalized[i]
                ?? new DeprivationBurdenSaveData { kind = (DeprivationKind)i });
        }
    }

    private static CharacterBreakdownKind ResolveBreakdownKind(DeprivationKind kind)
    {
        return kind switch
        {
            DeprivationKind.Bladder => CharacterBreakdownKind.DesperateRelief,
            DeprivationKind.Thirst => CharacterBreakdownKind.DesperateDrink,
            DeprivationKind.Hunger => CharacterBreakdownKind.DesperateEat,
            DeprivationKind.Exhaustion => CharacterBreakdownKind.Collapse,
            _ => CharacterBreakdownKind.ViolentImpulse
        };
    }

    private static bool IsCauseRelieved(CharacterActor actor, DeprivationKind kind)
    {
        float value = kind switch
        {
            DeprivationKind.Hunger => GetNeed(actor, CharacterCondition.HUNGER),
            DeprivationKind.Thirst => GetNeed(actor, CharacterCondition.THIRST),
            DeprivationKind.Bladder => GetNeed(actor, CharacterCondition.EXCRETION),
            DeprivationKind.Contamination => GetNeed(actor, CharacterCondition.HYGIENE),
            DeprivationKind.Exhaustion => GetNeed(actor, CharacterCondition.SLEEP),
            _ => actor?.Stats?.Mood ?? 50f
        };
        return value >= 30f;
    }

    private static float GetNeed(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? Mathf.Clamp(value, 0f, 100f)
                : 100f;
    }

    private static bool IsEligibleHumanoid(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition;
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static string GetBreakdownLabel(CharacterBreakdownKind kind)
    {
        return kind switch
        {
            CharacterBreakdownKind.DesperateRelief => "배변 붕괴",
            CharacterBreakdownKind.DesperateDrink => "갈증 붕괴",
            CharacterBreakdownKind.DesperateEat => "굶주림 붕괴",
            CharacterBreakdownKind.Collapse => "탈진 실신",
            CharacterBreakdownKind.ViolentImpulse => "정신 붕괴",
            _ => "붕괴"
        };
    }

    private void CreateActionRoutines()
    {
        actionRoutines[CharacterBreakdownKind.DesperateRelief] = RunDesperateRelief;
        actionRoutines[CharacterBreakdownKind.DesperateDrink] =
            actor => RunDesperateDrink(actor, allowWaste: true);
        actionRoutines[CharacterBreakdownKind.DesperateEat] = RunDesperateEat;
        actionRoutines[CharacterBreakdownKind.Collapse] = RunCollapse;
        actionRoutines[CharacterBreakdownKind.ViolentImpulse] = RunViolentImpulse;
    }

    private static CharacterDeprivationState CloneState(CharacterDeprivationState state)
    {
        return new CharacterDeprivationState
        {
            persistentId = state.persistentId ?? string.Empty,
            burdens = (state.burdens ?? new List<DeprivationBurdenSaveData>())
                .Where(entry => entry != null)
                .Select(entry => new DeprivationBurdenSaveData
                {
                    kind = entry.kind,
                    burden = Mathf.Clamp(entry.burden, 0f, 100f),
                    maximumHeldSeconds = Mathf.Max(0f, entry.maximumHeldSeconds),
                    nextBreakdownCheckAt = entry.nextBreakdownCheckAt,
                    nextDamageAt = entry.nextDamageAt
                }).ToList(),
            breakdown = CloneBreakdown(state.breakdown),
            tabooMemories = new List<string>(state.tabooMemories ?? new List<string>()),
            infectionBurden = Mathf.Clamp(state.infectionBurden, 0f, 100f),
            lastUpdatedAt = state.lastUpdatedAt
        };
    }

    private static CharacterBreakdownState CloneBreakdown(CharacterBreakdownState state)
    {
        state ??= new CharacterBreakdownState();
        return new CharacterBreakdownState
        {
            active = state.active,
            kind = state.kind,
            cause = state.cause,
            targetId = state.targetId ?? string.Empty,
            targetGridX = state.targetGridX,
            targetGridY = state.targetGridY,
            startedAt = state.startedAt,
            suppressionResistance = state.suppressionResistance,
            lastReplanReason = state.lastReplanReason ?? string.Empty
        };
    }
}
