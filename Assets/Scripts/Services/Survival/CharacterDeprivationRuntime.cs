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
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterDeprivationRuntime.Tick");

    private const float TickInterval = 1f;
    private const float BreakdownCheckInterval = 5f;
    private const float DefaultCertainBreakdownDelay = 30f;
    private const float WarningThreshold = 40f;
    private const float BreakdownThreshold = 70f;
    private const float MaximumBurden = 100f;
    private const float DefaultSuppressionResistance = 35f;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly IRandomStream breakdownRandom;
    private readonly ICharacterNeedBalanceRuntime needBalanceRuntime;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private readonly IReproductionService reproduction;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly CharacterSafeDrinkPlanner safeDrinkPlanner;
    private readonly CharacterEmergencyMovement emergencyMovement;
    private readonly CharacterSafeReliefRunner safeReliefRunner;
    private readonly CharacterPrimitiveSurvivalRunner primitiveSurvivalRunner;
    private readonly CharacterDeprivationConsequences consequences;
    private readonly CharacterBreakdownActionRunner breakdownActionRunner;
    private readonly CharacterDeprivationPersistenceCoordinator persistence;
    private readonly Dictionary<CharacterId, int> alertLevels =
        new Dictionary<CharacterId, int>();
    private readonly List<CharacterActor> tickActors =
        new List<CharacterActor>(512);
    private readonly HashSet<CharacterId> liveTickIds =
        new HashSet<CharacterId>();
    private readonly List<CharacterId> staleStateIds =
        new List<CharacterId>(128);
    private IDisposable infectionSubscription;
    private IDisposable infectionReductionSubscription;
    private IDisposable mentalInstabilitySubscription;
    private IDisposable deathSubscription;
    private IDisposable tabooIncidentSubscription;
    private float nextTickAt;
    private float lastSimulationTickAt;
    private float tickPassStartedAt;
    private int tickActorIndex;
    private float tickElapsed;
    private float tickNow;
    private bool tickPassActive;
    private int pendingWarningAlerts;
    private int pendingDangerAlerts;
    private int observedRestoreRevision;
    private readonly CharacterDeprivationDiagnostics diagnostics = new();

    public CharacterDeprivationRuntime(
        CharacterDeprivationWorldDependencies world,
        CharacterDeprivationSystemDependencies system,
        CharacterDeprivationAuthorityDependencies authority)
    {
        _ = world ?? throw new ArgumentNullException(nameof(world));
        _ = system ?? throw new ArgumentNullException(nameof(system));
        _ = authority ?? throw new ArgumentNullException(nameof(authority));
        IGridSystemProvider gridSystemProvider = world.GridSystemProvider;
        IWorldItemStackRuntime itemStackRuntime = world.ItemStackRuntime;
        IWorldFilthQuery filthQuery = world.FilthQuery;
        IWorldWaterQuery waterQuery = world.WaterQuery;
        IRoomLayoutCache roomLayoutCache = world.RoomLayoutCache;
        ICharacterAiWorldRegistry worldRegistry = world.WorldRegistry;
        ICharacterLifetimeQuery characterLifetime = world.CharacterLifetime;
        IGameEventBus gameEventBus = system.GameEventBus;
        IGameClock gameClock = system.GameClock;
        IDynamicFrameWorkBudget frameWorkBudget = system.FrameWorkBudget;
        IRandomStreamProvider randomStreamProvider = system.RandomStreamProvider;
        IUiClock uiClock = system.UiClock;
        IDoorAccessQuery doorAccessQuery = system.DoorAccessQuery;
        ICharacterNeedBalanceRuntime needBalanceRuntime =
            system.NeedBalanceRuntime;
        CharacterDeprivationStateStore stateStore = authority.StateStore;
        IDungeonDebugRuleQuery debugRules = system.DebugRules;
        IHeritableTraitEffectQuery heritableTraits = system.HeritableTraits;
        IReproductionService reproduction = system.Reproduction;
        ICharacterBodyHealthCommand bodyHealthCommands = authority.BodyHealthCommands;

        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.filthQuery = filthQuery ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery ?? throw new ArgumentNullException(nameof(waterQuery));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.needBalanceRuntime = needBalanceRuntime
            ?? throw new ArgumentNullException(nameof(needBalanceRuntime));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        this.heritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        breakdownRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-deprivation");
        safeDrinkPlanner = new CharacterSafeDrinkPlanner(
            gridSystemProvider,
            itemStackRuntime,
            waterQuery,
            authority.PrimitiveSurvival.QuantityReservations,
            doorAccessQuery,
            world.EnvironmentWorkPolicy,
            diagnostics);
        emergencyMovement = new CharacterEmergencyMovement(
            gridSystemProvider,
            stateStore);
        safeReliefRunner = new CharacterSafeReliefRunner(
            itemStackRuntime,
            authority.PrimitiveSurvival.ReservedTransfers,
            waterQuery,
            gameClock,
            needBalanceRuntime,
            gameEventBus,
            stateStore,
            safeDrinkPlanner,
            emergencyMovement,
            diagnostics);
        consequences = new CharacterDeprivationConsequences(
            stateStore,
            worldRegistry);
        CharacterBreakdownWorld breakdownWorld = new CharacterBreakdownWorld(
            gridSystemProvider,
            itemStackRuntime,
            filthQuery,
            waterQuery,
            roomLayoutCache,
            worldRegistry);
        primitiveSurvivalRunner = new CharacterPrimitiveSurvivalRunner(
            breakdownWorld,
            emergencyMovement,
            gameClock,
            gameEventBus,
            authority.PrimitiveSurvival);
        breakdownActionRunner = authority.CreateBreakdownActionRunner(
            breakdownWorld,
            breakdownRandom,
            needBalanceRuntime,
            safeDrinkPlanner,
            emergencyMovement,
            diagnostics,
            consequences,
            gameEventBus);
        persistence = new CharacterDeprivationPersistenceCoordinator(
            stateStore,
            worldRegistry,
            characterLifetime,
            filthQuery,
            waterQuery);
    }

    public void Initialize()
    {
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
        tabooIncidentSubscription =
            gameEventBus.Subscribe<CharacterTabooIncidentEvent<CharacterActor>>(
                OnTabooIncident);
    }

    public void Dispose()
    {
        infectionSubscription?.Dispose();
        infectionReductionSubscription?.Dispose();
        mentalInstabilitySubscription?.Dispose();
        deathSubscription?.Dispose();
        tabooIncidentSubscription?.Dispose();
        infectionSubscription = null;
        infectionReductionSubscription = null;
        mentalInstabilitySubscription = null;
        deathSubscription = null;
        tabooIncidentSubscription = null;
        breakdownActionRunner.Reset();
        tickActors.Clear();
        liveTickIds.Clear();
        staleStateIds.Clear();
        safeReliefRunner.Reset();
        safeDrinkPlanner.Reset();
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
        EnsureDerivedCachesCurrent();
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
                CharacterId id = CharacterPersistentIdentity.Require(actor);
                liveTickIds.Add(id);
                CharacterDeprivationState state = stateStore.Ensure(actor);
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
        foreach (KeyValuePair<CharacterId, CharacterDeprivationState> pair in stateStore.Entries)
        {
            if (!liveTickIds.Contains(pair.Key))
            {
                staleStateIds.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleStateIds.Count; i++)
        {
            CharacterId stale = staleStateIds[i];
            // A lifecycle transition can remove an actor from the live registry
            // while a breakdown coroutine is between yields. Active breakdown
            // state is not a persistence root: release every transient runner
            // and reservation before removing the orphan aggregate entry.
            breakdownActionRunner.ReleaseActor(stale);
            safeReliefRunner.ReleaseActor(stale);
            primitiveSurvivalRunner.ReleaseActor(stale);
            safeDrinkPlanner.ReleaseForActor(stale.Value);
            stateStore.Remove(stale);
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

    private float CadenceTime => uiClock.Time;

    public bool HasActiveBreakdown(CharacterActor actor)
    {
        return stateStore.TryGet(actor, out CharacterDeprivationState state)
            && state.breakdown != null
            && state.breakdown.active;
    }

    public bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind)
    {
        return stateStore.TryGet(actor, out CharacterDeprivationState state)
            && state.breakdown != null
            && state.breakdown.active
            && state.breakdown.kind == kind;
    }

    public bool TryGetDisplayState(
        CharacterActor actor,
        out CharacterDeprivationDisplayState displayState)
    {
        if (!stateStore.TryGet(actor, out CharacterDeprivationState state))
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
        if (!stateStore.TryGet(actor, out CharacterDeprivationState state))
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
            CharacterDeprivationStateStore.CloneBreakdown(state.breakdown),
            state.infectionBurden,
            state.tabooMemories?.ToArray() ?? Array.Empty<string>());
        return true;
    }

    public bool TryRunActiveBreakdown(CharacterActor actor, out string status)
    {
        EnsureDerivedCachesCurrent();
        return breakdownActionRunner.TryRunActive(actor, out status);
    }

    public bool NeedsSafeEmergencyRelief(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!IsEligibleHumanoid(actor)
            || !UsesBiologicalFoodAndWater(actor)
            || actor.Stats == null
            || !actor.Stats.TryGetConditionValue(CharacterCondition.THIRST, out float thirst)
            || !CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.THIRST)
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

        return TryStartSafeDrink(actor, true, out status);
    }

    public bool TryRunMostUrgentEmergencySelfCare(
        CharacterActor actor,
        out string status)
    {
        status = string.Empty;
        if (actor?.Stats == null || actor.Brain == null)
        {
            return false;
        }

        CharacterCondition[] orderedConditions =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        CharacterCondition selected = default;
        CharacterActionIntentKind selectedIntent = CharacterActionIntentKind.None;
        float selectedUrgency = float.NegativeInfinity;
        bool hasSelected = false;
        for (int index = 0; index < orderedConditions.Length; index++)
        {
            CharacterCondition condition = orderedConditions[index];
            if (!IsEmergencyCareDue(actor, condition)
                || !actor.Stats.TryGetConditionValue(condition, out float value))
            {
                continue;
            }

            CharacterNeedResponseProfile response = GetResponse(condition);
            CharacterActionIntentKind intent =
                CharacterNeedAiThresholds.GetEmergencyIntentKind(actor, condition);
            float denominator = Mathf.Max(1f, response.emergencyStart);
            float urgency = (response.emergencyStart - value) / denominator;
            if (hasSelected
                && (intent < selectedIntent
                    || intent == selectedIntent && urgency <= selectedUrgency))
            {
                continue;
            }

            selected = condition;
            selectedIntent = intent;
            selectedUrgency = urgency;
            hasSelected = true;
        }

        if (!hasSelected)
        {
            return false;
        }

        DeprivationKind selectedCause = selected switch
        {
            CharacterCondition.HUNGER => DeprivationKind.Hunger,
            CharacterCondition.THIRST => DeprivationKind.Thirst,
            CharacterCondition.SLEEP => DeprivationKind.Exhaustion,
            CharacterCondition.EXCRETION => DeprivationKind.Bladder,
            CharacterCondition.HYGIENE => DeprivationKind.Contamination,
            _ => DeprivationKind.MentalInstability
        };
        if (IsCurrentActionAddressingDeprivation(actor, selectedCause))
        {
            status = $"진행 중인 {selected} 자기관리 행동 유지";
            return true;
        }

        return selected switch
        {
            CharacterCondition.HUNGER =>
                NeedsPrimitiveMeal(actor, out _)
                && AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                    actor,
                    FacilityRole.Meal,
                    CharacterCondition.HUNGER)
                && primitiveSurvivalRunner.TryStart(
                    actor,
                    CharacterPrimitiveSurvivalActionKind.FieldMeal,
                    out status),
            CharacterCondition.THIRST =>
                NeedsSafeEmergencyRelief(actor, out _)
                && TryStartSafeDrink(actor, emergency: true, out status),
            CharacterCondition.SLEEP =>
                NeedsPrimitiveRest(actor, out _)
                && AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                    actor,
                    FacilityRole.Rest,
                    CharacterCondition.SLEEP)
                && primitiveSurvivalRunner.TryStart(
                    actor,
                    CharacterPrimitiveSurvivalActionKind.FloorRest,
                    out status),
            CharacterCondition.EXCRETION =>
                NeedsPrimitiveRelief(actor, out _)
                && AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                    actor,
                    FacilityRole.Toilet,
                    CharacterCondition.EXCRETION)
                && primitiveSurvivalRunner.TryStart(
                    actor,
                    CharacterPrimitiveSurvivalActionKind.Latrine,
                    out status),
            CharacterCondition.HYGIENE =>
                NeedsPrimitiveWash(actor, out _)
                && AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                    actor,
                    FacilityRole.Hygiene,
                    CharacterCondition.HYGIENE)
                && primitiveSurvivalRunner.TryStart(
                    actor,
                    CharacterPrimitiveSurvivalActionKind.BucketWash,
                    out status),
            _ => false
        };
    }

    public bool NeedsRoutineDrink(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!IsEligibleHumanoid(actor)
            || !UsesBiologicalFoodAndWater(actor)
            || actor.Stats == null
            || !actor.Stats.TryGetConditionValue(
                CharacterCondition.THIRST,
                out float thirst)
            || thirst > GetResponse(
                CharacterCondition.THIRST).routineStart
            || HasActiveBreakdown(actor))
        {
            return false;
        }

        reason = $"갈증 {thirst:0}: 마실 물이 필요함";
        return true;
    }

    public bool TryRunRoutineDrink(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (!NeedsRoutineDrink(actor, out _))
        {
            return false;
        }

        return TryStartSafeDrink(actor, false, out status);
    }

    public bool IsRoutineDrinkActionActive(CharacterActor actor)
    {
        return actor != null
            && safeReliefRunner.IsActive(
                CharacterPersistentIdentity.Require(actor));
    }

    public bool NeedsPrimitiveMeal(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!NeedsRoutineAction(actor, CharacterCondition.HUNGER, biologicalOnly: true)
            || !primitiveSurvivalRunner.HasFieldMeal(actor, out reason))
        {
            return false;
        }
        reason = $"허기 {GetNeed(actor, CharacterCondition.HUNGER):0}: {reason}";
        return true;
    }

    public bool NeedsPrimitiveRest(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!NeedsRoutineAction(actor, CharacterCondition.SLEEP, biologicalOnly: false))
        {
            return false;
        }
        reason = $"수면 {GetNeed(actor, CharacterCondition.SLEEP):0}: 침대 없는 바닥 취침 필요";
        return true;
    }

    public bool NeedsPrimitiveRelief(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!NeedsRoutineAction(actor, CharacterCondition.EXCRETION, biologicalOnly: true))
        {
            return false;
        }
        reason = $"배변 {GetNeed(actor, CharacterCondition.EXCRETION):0}: 임시 변소 필요";
        return true;
    }

    public bool NeedsPrimitiveWash(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!NeedsRoutineAction(actor, CharacterCondition.HYGIENE, biologicalOnly: false)
            || !primitiveSurvivalRunner.HasWashWater(actor, out reason))
        {
            return false;
        }
        reason = $"위생 {GetNeed(actor, CharacterCondition.HYGIENE):0}: {reason}";
        return true;
    }

    public bool TryRunPrimitiveMeal(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return NeedsPrimitiveMeal(actor, out _)
            && primitiveSurvivalRunner.TryStart(
                actor,
                CharacterPrimitiveSurvivalActionKind.FieldMeal,
                out status);
    }

    public bool TryRunPrimitiveRest(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return NeedsPrimitiveRest(actor, out _)
            && primitiveSurvivalRunner.TryStart(
                actor,
                CharacterPrimitiveSurvivalActionKind.FloorRest,
                out status);
    }

    public bool TryRunPrimitiveRelief(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return NeedsPrimitiveRelief(actor, out _)
            && primitiveSurvivalRunner.TryStart(
                actor,
                CharacterPrimitiveSurvivalActionKind.Latrine,
                out status);
    }

    public bool TryRunPrimitiveWash(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return NeedsPrimitiveWash(actor, out _)
            && primitiveSurvivalRunner.TryStart(
                actor,
                CharacterPrimitiveSurvivalActionKind.BucketWash,
                out status);
    }

    private bool NeedsRoutineAction(
        CharacterActor actor,
        CharacterCondition condition,
        bool biologicalOnly)
    {
        if (!IsEligibleHumanoid(actor)
            || biologicalOnly && !UsesBiologicalFoodAndWater(actor)
            || actor.Stats == null
            || GetNeed(actor, condition) > GetResponse(condition).routineStart
            || HasActiveBreakdown(actor))
        {
            return false;
        }

        return true;
    }

    private bool TryStartSafeDrink(
        CharacterActor actor,
        bool emergency,
        out string status)
    {
        EnsureDerivedCachesCurrent();
        return safeReliefRunner.TryStart(actor, emergency, out status);
    }

    public CharacterDeprivationDiagnosticsSnapshot GetDiagnostics()
    {
        EnsureDerivedCachesCurrent();
        return diagnostics.Capture(safeReliefRunner.ActiveCount);
    }

    public void ResetDiagnostics()
    {
        diagnostics.Reset();
    }

    public void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind)
    {
        EnsureDerivedCachesCurrent();
        breakdownActionRunner.TryBegin(actor, kind);
    }

    public bool IsSuppressible(CharacterActor actor)
    {
        return HasActiveBreakdown(actor);
    }

    public bool ApplySuppression(CharacterActor actor, float amount, out bool ended)
    {
        ended = false;
        if (!stateStore.TryGetWritable(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        state.breakdown.suppressionResistance = Mathf.Max(
            0f,
            state.breakdown.suppressionResistance - Mathf.Max(0f, amount));
        bodyHealthCommands.ApplyLegacyDamage(
            actor,
            Mathf.Clamp(amount * 0.08f, 0.5f, 2.5f),
            "비살상 제압",
            allowDeath: false);
        if (state.breakdown.suppressionResistance <= 0f)
        {
            consequences.EndBreakdown(actor, state, "제압됨", reduceCauseTo: 55f);
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

        DeprivationKind cause = kind switch
        {
            CharacterBreakdownKind.DesperateRelief => DeprivationKind.Bladder,
            CharacterBreakdownKind.DesperateDrink => DeprivationKind.Thirst,
            CharacterBreakdownKind.DesperateEat => DeprivationKind.Hunger,
            CharacterBreakdownKind.Collapse => DeprivationKind.Exhaustion,
            _ => DeprivationKind.MentalInstability
        };
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        CharacterDeprivationState state = stateStore.Ensure(characterId);
        CharacterDeprivationStateStore.GetBurden(state, cause).burden = 100f;
        if (!stateStore.TryBeginBreakdown(
                characterId,
                cause,
                kind,
                gameClock.Time,
                25f,
                "디버그 명령",
                out state,
                out int generation))
        {
            return state.breakdown.kind == kind;
        }
        if (!stateStore.TryClaimBreakdownSideEffects(characterId, generation))
        {
            return true;
        }
        actor.Stats?.ApplyMoodFactor("survival:breakdown", "결핍으로 이성을 잃음", -12f, 180f, 1);
        actor.Brain?.StopCurrentActionForReplan("디버그 붕괴 발동");
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        return true;
    }

    public bool DebugClearBreakdown(CharacterActor actor)
    {
        if (!stateStore.TryGetWritable(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        consequences.EndBreakdown(actor, state, "디버그 해제", reduceCauseTo: 0f);
        return true;
    }

    public bool DebugResetForDeterministicScenario(CharacterActor actor)
    {
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return false;
        }

        // A deterministic scenario boundary must clear both the persisted
        // burden authority and every transient runner which can republish it.
        // Resetting visible needs alone leaves warm-up contamination and can
        // trigger a breakdown during an otherwise neutral measurement window.
        actor.Brain?.StopCurrentActionForReplan(
            "deterministic scenario state reset");
        safeReliefRunner.ReleaseActor(characterId);
        primitiveSurvivalRunner.ReleaseActor(characterId);
        breakdownActionRunner.ReleaseActor(characterId);
        safeDrinkPlanner.ReleaseForActor(characterId.Value);
        stateStore.Remove(characterId);
        alertLevels.Remove(characterId);
        return true;
    }

    public float GetMoveSpeedMultiplier(CharacterActor actor)
    {
        float traitMultiplier = CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId characterId)
            ? heritableTraits.GetMultiplier(
                characterId,
                HeritableTraitConsequenceKind.Movement,
                "move-speed")
            : 1f;
        if (!stateStore.TryGet(actor, out CharacterDeprivationState state))
        {
            return Mathf.Clamp(traitMultiplier, 0.35f, 1.25f);
        }

        float exhaustion = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Exhaustion).burden;
        float dehydration = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Thirst).burden;
        return Mathf.Clamp(
            (1f - exhaustion * 0.004f - dehydration * 0.002f)
            * traitMultiplier,
            0.35f,
            1.25f);
    }

    public float GetWorkSpeedMultiplier(CharacterActor actor)
    {
        if (!stateStore.TryGet(actor, out CharacterDeprivationState state))
        {
            return 1f;
        }

        float exhaustion = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Exhaustion).burden;
        float hunger = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Hunger).burden;
        float thirst = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Thirst).burden;
        return Mathf.Clamp(1f - exhaustion * 0.004f - (hunger + thirst) * 0.0015f, 0.4f, 1f);
    }

    public void RecordTaboo(CharacterActor actor, string memory)
    {
        consequences.RecordTaboo(actor, memory);
    }

    public void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string label,
        float mood)
    {
        consequences.ApplyWitnessMood(
            source,
            position,
            label,
            mood,
            permanentMemory: true);
    }

    private void OnTabooIncident(
        CharacterTabooIncidentEvent<CharacterActor> gameEvent)
    {
        RecordTaboo(gameEvent.Source, gameEvent.Memory);
        RecordTabooWitnesses(
            gameEvent.Source,
            gameEvent.Position,
            gameEvent.WitnessLabel,
            gameEvent.WitnessMood);
    }

    public DungeonDarkSurvivalSaveData Capture() => persistence.Capture();

    public DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData saveData) =>
        persistence.BuildRestoreCandidate(saveData);

    public void PublishRestoreCandidate(DarkSurvivalRestoreCandidate candidate)
    {
        persistence.PublishRestoreCandidate(candidate);
        if (!stateStore.IsRestoreStaging)
        {
            InvalidateDerivedCaches();
        }
    }

    private void OnCharacterDeath(CharacterDeathEvent eventType)
    {
        CharacterActor actor = worldRegistry.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(eventType.CharacterId));
        if (actor == null)
        {
            return;
        }

        CharacterId sourceId = eventType.CharacterId;
        safeReliefRunner.ReleaseActor(sourceId);
        primitiveSurvivalRunner.ReleaseActor(sourceId);
        breakdownActionRunner.ReleaseActor(sourceId);
        bool alreadyExists = itemStackRuntime.GetAllStacks().Any(stack => stack != null
            && stack.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
            && string.Equals(
                stack.SourceCharacterId,
                sourceId.Value,
                StringComparison.Ordinal));
        if (!alreadyExists)
        {
            itemStackRuntime.SpawnHumanoidCorpse(
                actor,
                actor.GetNowXY(),
                eventType.Cause.ToString(),
                out _);
        }

        filthQuery.AddFilth(
            WorldFilthType.Blood,
            actor.GetNowXY(),
            12f,
            sourceId.Value,
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
        return GetBreakdownChance(
            burden,
            mood01,
            CharacterBreakdownActionRunner.GetPersonality(actor));
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
        return burden >= MaximumBurden
            && maximumHeldSeconds >= DefaultCertainBreakdownDelay;
    }

    private void TickActor(
        CharacterActor actor,
        CharacterDeprivationState state,
        float elapsed,
        float now)
    {
        bool biologicalFoodAndWater =
            UsesBiologicalFoodAndWater(actor);
        UpdateBurden(
            actor,
            state,
            DeprivationKind.Hunger,
            biologicalFoodAndWater
                ? GetNeed(actor, CharacterCondition.HUNGER)
                : 100f,
            elapsed,
            now);
        UpdateBurden(
            actor,
            state,
            DeprivationKind.Thirst,
            biologicalFoodAndWater
                ? GetNeed(actor, CharacterCondition.THIRST)
                : 100f,
            elapsed,
            now);
        UpdateBurden(actor, state, DeprivationKind.Bladder, GetNeed(actor, CharacterCondition.EXCRETION), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Contamination, GetNeed(actor, CharacterCondition.HYGIENE), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.Exhaustion, GetNeed(actor, CharacterCondition.SLEEP), elapsed, now);
        UpdateBurden(actor, state, DeprivationKind.MentalInstability, actor.Stats?.Mood ?? 50f, elapsed, now);
        state.lastUpdatedAt = now;

        float filthExposure = filthQuery.GetCleanlinessPenalty(actor.GetNowXY(), 1);
        if (filthExposure > 15f)
        {
            DeprivationBurdenSaveData contamination = CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Contamination);
            contamination.burden = Mathf.Clamp(contamination.burden + filthExposure * 0.0025f * elapsed, 0f, 100f);
            state.infectionBurden = Mathf.Clamp(state.infectionBurden + filthExposure * 0.0015f * elapsed, 0f, 100f);
        }

        ApplyDamageConsequences(actor, state, now);
        UpdateAlert(actor, state);
        if (debugRules.IsEnabled(DungeonDebugCheat.PreventBreakdowns))
        {
            if (state.breakdown.active)
            {
                consequences.EndBreakdown(
                    actor,
                    state,
                    "개발자 붕괴 방지",
                    reduceCauseTo: 55f);
            }
            return;
        }

        if (state.breakdown.active)
        {
            if (IsCauseRelieved(actor, state.breakdown.cause))
            {
                consequences.EndBreakdown(
                    actor,
                    state,
                    "욕구가 충족됨",
                    reduceCauseTo: 45f);
            }
            return;
        }

        // At high simulation speeds a need can cross the breakdown threshold
        // between two scheduled AI decisions. Give an available safe emergency
        // self-care action one immediate start opportunity before converting the
        // same need into a destructive breakdown. The action still acquires the
        // brain's authoritative external intent, so this is not a parallel AI.
        if (TryStartEmergencySelfCare(actor))
        {
            return;
        }

        // A running lower-priority primitive or a deferred drink retry must not
        // suppress evaluation of a newly lethal hunger/thirst need. The intent
        // lease above arbitrates preemption first; only then may an existing
        // self-care action suppress breakdown selection.
        if (safeReliefRunner.IsRunning((CharacterId)state.characterId)
            || primitiveSurvivalRunner.IsRunning((CharacterId)state.characterId))
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

        // A high historical burden may remain after the actor has already
        // committed to the authored service that relieves the same cause.
        // Starting a breakdown here used to cancel the toilet/wash action in
        // its short service phase, after which the still-active burden routed
        // the actor into the primitive fallback despite a usable facility.
        // Only defer the matching need; a different lethal need must still be
        // allowed to pre-empt this self-care action.
        if (IsCurrentActionAddressingDeprivation(actor, highest.kind))
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

        bool certain = highest.maximumHeldSeconds
            >= GetForcedBreakdownDelay();
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

    private bool TryStartEmergencySelfCare(CharacterActor actor)
    {
        // This high-speed safety path bypasses the regular scheduler, so it
        // must apply the scheduler's admission authority explicitly. Debug/
        // command pauses and non-Active lifecycle states may still be ticked
        // for need accumulation, but they must not acquire an external action.
        if (actor == null || !actor.CanRunAi)
        {
            return false;
        }

        return TryRunMostUrgentEmergencySelfCare(actor, out _);
    }

    private static bool IsCurrentActionAddressingDeprivation(
        CharacterActor actor,
        DeprivationKind cause)
    {
        AIAction action = actor?.Brain?.bestAction;
        AIActionSet actionSet = action?.actionset;
        if (actionSet == null
            || action == null
            || !action.HasStarted
            || actor.Brain.isBestActionEnd
            || !actionSet.HasSemanticTag(CharacterAiActionTags.SelfCare))
        {
            return false;
        }

        CharacterAiBranch expectedBranch = cause switch
        {
            DeprivationKind.Hunger => CharacterAiBranch.Eat,
            DeprivationKind.Thirst => CharacterAiBranch.Drink,
            DeprivationKind.Bladder => CharacterAiBranch.Toilet,
            DeprivationKind.Contamination => CharacterAiBranch.Hygiene,
            DeprivationKind.Exhaustion => CharacterAiBranch.Rest,
            _ => CharacterAiBranch.None
        };
        return expectedBranch != CharacterAiBranch.None
            && actionSet.Branch == expectedBranch;
    }

    private static bool IsEmergencyCareDue(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return condition is CharacterCondition.HUNGER or CharacterCondition.THIRST
            ? CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
                actor,
                condition)
            : CharacterNeedAiThresholds.IsEmergency(actor, condition);
    }

    private void UpdateBurden(
        CharacterActor actor,
        CharacterDeprivationState state,
        DeprivationKind kind,
        float needValue,
        float elapsed,
        float now)
    {
        DeprivationBurdenSaveData burden = CharacterDeprivationStateStore.GetBurden(state, kind);
        float delta = CalculateBurdenDelta(needValue, elapsed);
        delta *= GetBurdenMultiplier(recovering: delta < 0f);
        if (delta > 0f
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            if (kind == DeprivationKind.Hunger)
            {
                delta *= heritableTraits.GetMultiplier(
                    characterId,
                    HeritableTraitConsequenceKind.NeedRate,
                    "hunger");
                bool reproducing = reproduction.Processes.Any(process =>
                    (process.Status is ReproductionProcessStatus.Active
                        or ReproductionProcessStatus.WaitingForEnvironment
                        or ReproductionProcessStatus.WaitingForEmergencyExtraction)
                    && (process.CarrierId.Equals(characterId)
                        || process.FirstParentId.Equals(characterId)));
                if (reproducing)
                {
                    delta *= heritableTraits.GetMultiplier(
                        characterId,
                        HeritableTraitConsequenceKind.NeedRate,
                        "reproduction-hunger");
                }
            }
            else if (kind == DeprivationKind.Exhaustion)
            {
                delta *= heritableTraits.GetMultiplier(
                    characterId,
                    HeritableTraitConsequenceKind.NeedRate,
                    "sleep");
            }
        }
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
            burden.nextDamageAt = now + GetDamageInterval();
        }
    }

    private void ApplyDamageConsequences(CharacterActor actor, CharacterDeprivationState state, float now)
    {
        ApplyDeprivationDamage(
            actor,
            CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Hunger),
            GetNeed(actor, CharacterCondition.HUNGER),
            now,
            CharacterDeathCauseCode.Starvation,
            "심한 굶주림");
        ApplyDeprivationDamage(
            actor,
            CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Thirst),
            GetNeed(actor, CharacterCondition.THIRST),
            now,
            CharacterDeathCauseCode.Dehydration,
            "심한 탈수");

        float infectionSource = Mathf.Max(
            CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Bladder).burden,
            CharacterDeprivationStateStore.GetBurden(state, DeprivationKind.Contamination).burden);
        if (infectionSource >= WarningThreshold)
        {
            state.infectionBurden = Mathf.Clamp(
                state.infectionBurden + Mathf.InverseLerp(40f, 100f, infectionSource) * 0.4f,
                0f,
                100f);
        }
    }

    private void ApplyDeprivationDamage(
        CharacterActor actor,
        DeprivationBurdenSaveData burden,
        float currentNeed,
        float now,
        CharacterDeathCauseCode deathCause,
        string source)
    {
        // Burden is long-lived history used for breakdown risk. Physical
        // starvation/dehydration damage may only tick while the authoritative
        // need remains in the actual deprivation band. Otherwise a successful
        // low-nutrition meal at hunger 0 -> 35 can keep dealing starvation
        // damage solely because its historical burden has not decayed yet.
        if (currentNeed >= 20f)
        {
            // Keep a full grace interval after the need next crosses back into
            // deprivation instead of applying an overdue historical tick on
            // the first frame below 20.
            burden.nextDamageAt = now + GetDamageInterval();
            return;
        }

        if (!ShouldApplyDeprivationDamage(
                currentNeed,
                burden.burden,
                now,
                burden.nextDamageAt))
        {
            return;
        }

        burden.nextDamageAt = now + GetDamageInterval();
        bodyHealthCommands.ApplyLegacyDamageWithCause(
            actor,
            actor.MaxHealth * 0.01f,
            deathCause,
            source,
            allowDeath: true);
    }

    private static bool ShouldApplyDeprivationDamage(
        float currentNeed,
        float burden,
        float now,
        float nextDamageAt)
    {
        return currentNeed < 20f
            && burden >= BreakdownThreshold
            && now >= nextDamageAt;
    }

    private CharacterNeedResponseProfile GetResponse(
        CharacterCondition condition)
    {
        return needBalanceRuntime.GetResponse(condition);
    }

    private float GetBurdenMultiplier(bool recovering)
    {
        return needBalanceRuntime.GetDeprivationBurdenMultiplier(recovering);
    }

    private float GetForcedBreakdownDelay()
    {
        return needBalanceRuntime.ForcedBreakdownDelaySeconds;
    }

    private float GetDamageInterval()
    {
        return needBalanceRuntime.HighBurdenDamageIntervalSeconds;
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
        CharacterId characterId = (CharacterId)state.characterId;
        alertLevels.TryGetValue(characterId, out int previous);
        if (level <= previous)
        {
            alertLevels[characterId] = level;
            return;
        }

        alertLevels[characterId] = level;
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
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        CharacterBreakdownKind kind = ResolveBreakdownKind(cause);
        if (!stateStore.TryBeginBreakdown(
                characterId,
                cause,
                kind,
                now,
                DefaultSuppressionResistance,
                "결핍 임계값 초과",
                out state,
                out int generation)
            || !stateStore.TryClaimBreakdownSideEffects(
                characterId,
                generation))
        {
            return;
        }

        actor.Brain?.StopCurrentActionForReplan("결핍 붕괴");
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        actor.ApplyMoodFactor("survival:breakdown", "통제력을 잃음", -8f, 180f, 1);
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Started,
            GetBreakdownLabel(kind),
            actionId: "survival/breakdown",
            reasonCode: cause.ToString(),
            sentiment: -1f,
            bubbleEligible: true));
        DispatchAutomaticSuppression(actor);
    }

    public void AddInfectionBurden(CharacterActor actor, float amount)
    {
        consequences.AddInfection(actor, amount);
    }

    public void ReduceInfectionBurden(CharacterActor actor, float amount)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        CharacterDeprivationState state = stateStore.Ensure(actor);
        state.infectionBurden = Mathf.Max(0f, state.infectionBurden - amount);
        DeprivationBurdenSaveData contamination = CharacterDeprivationStateStore.GetBurden(
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

        CharacterDeprivationState state = stateStore.Ensure(actor);
        DeprivationBurdenSaveData burden = CharacterDeprivationStateStore.GetBurden(
            state,
            DeprivationKind.MentalInstability);
        burden.burden = Mathf.Clamp(
            burden.burden + amount,
            0f,
            MaximumBurden);
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
            // Transient Customers use the visitor satisfaction/patience,
            // complaint, vandalism and exit authorities. Feeding them into
            // the persistent staff deprivation aggregate duplicates that
            // control plane and lets an ordinary visitor become a violent
            // breakdown actor before its authored exit completes. Promoted
            // population staff are projected to NPC before this query.
            && actor.Identity?.CharacterType != CharacterType.Customer
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition;
    }

    private static bool UsesBiologicalFoodAndWater(CharacterActor actor)
    {
        return actor != null
            && !string.Equals(
                actor.SpeciesTag,
                "Golem",
                StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureDerivedCachesCurrent()
    {
        int publishedRevision = stateStore.PublishedRestoreRevision;
        if (observedRestoreRevision == publishedRevision)
        {
            return;
        }

        InvalidateDerivedCaches();
        observedRestoreRevision = publishedRevision;
    }

    private void InvalidateDerivedCaches()
    {
        alertLevels.Clear();
        safeReliefRunner.Reset();
        primitiveSurvivalRunner.Reset();
        safeDrinkPlanner.Reset();
        breakdownActionRunner.Reset();
        tickActors.Clear();
        liveTickIds.Clear();
        staleStateIds.Clear();
        tickActorIndex = 0;
        tickPassActive = false;
        pendingWarningAlerts = 0;
        pendingDangerAlerts = 0;
        diagnostics.Reset();
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
}
