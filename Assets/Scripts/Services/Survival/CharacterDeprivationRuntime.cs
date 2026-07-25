using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterDeprivationRuntime :
    ICharacterDeprivationRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float TickInterval = 1f;
    private const float BreakdownCheckInterval = 5f;
    private const float CertainBreakdownDelay = 30f;
    private const float DamageInterval = 10f;
    private const float WarningThreshold = 40f;
    private const float BreakdownThreshold = 70f;
    private const float MaximumBurden = 100f;
    private const float DefaultSuppressionResistance = 35f;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ISurvivalFoodRuntime survivalFoodRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IRandomStream breakdownRandom;
    private readonly Dictionary<string, CharacterDeprivationState> states =
        new Dictionary<string, CharacterDeprivationState>(StringComparer.Ordinal);
    private readonly HashSet<string> runningBreakdownActions =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> runningSafeReliefActions =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> alertLevels =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>> actionRoutines =
        new Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>>();
    private IDisposable infectionSubscription;
    private IDisposable deathSubscription;
    private float nextTickAt;

    public CharacterDeprivationRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery,
        IRoomLayoutCache roomLayoutCache,
        ICharacterAiWorldRegistry worldRegistry,
        ISurvivalFoodRuntime survivalFoodRuntime,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.filthQuery = filthQuery ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery ?? throw new ArgumentNullException(nameof(waterQuery));
        this.roomLayoutCache = roomLayoutCache ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.survivalFoodRuntime = survivalFoodRuntime ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        breakdownRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("character-deprivation");
    }

    public void Initialize()
    {
        CreateActionRoutines();
        nextTickAt = gameClock.Time + TickInterval;
        infectionSubscription = gameEventBus.Subscribe<CharacterInfectionBurdenRequestedEvent>(
            gameEvent => AddInfectionBurden(gameEvent.Actor, gameEvent.Amount));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
    }

    public void Dispose()
    {
        infectionSubscription?.Dispose();
        deathSubscription?.Dispose();
        infectionSubscription = null;
        deathSubscription = null;
        actionRoutines.Clear();
    }

    public void Tick()
    {
        if (!Application.isPlaying || gameClock.Time < nextTickAt)
        {
            return;
        }

        float now = gameClock.Time;
        float elapsed = Mathf.Max(TickInterval, now - (nextTickAt - TickInterval));
        nextTickAt = now + TickInterval;
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        HashSet<string> liveIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (!IsEligibleHumanoid(actor))
            {
                continue;
            }

            string id = GetPersistentId(actor);
            liveIds.Add(id);
            CharacterDeprivationState state = EnsureState(actor);
            TickActor(actor, state, elapsed, now);
        }

        foreach (string stale in states.Keys.Where(id => !liveIds.Contains(id)
                     && !states[id].breakdown.active).ToArray())
        {
            states.Remove(stale);
            alertLevels.Remove(stale);
        }
    }

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

        BeginBreakdownAction(actor, state.breakdown.kind);
        status = GetBreakdownLabel(state.breakdown.kind);
        return true;
    }

    public bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (!IsEligibleHumanoid(actor)
            || actor.Stats == null
            || !actor.Stats.Stats.TryGetValue(CharacterCondition.THIRST, out float thirst)
            || thirst >= 25f
            || HasActiveBreakdown(actor))
        {
            return false;
        }

        string id = GetPersistentId(actor);
        if (runningSafeReliefActions.Contains(id))
        {
            status = "식수를 찾는 중";
            return true;
        }

        actor.StartCoroutine(RunSafeDrink(actor, id));
        status = "갈증 때문에 식수를 찾음";
        return true;
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

        DeprivationBurdenSaveData highest = state.burdens
            .Where(entry => entry != null)
            .OrderByDescending(entry => entry.burden)
            .FirstOrDefault();
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
        foreach (DeprivationKind kind in new[] { DeprivationKind.Hunger, DeprivationKind.Thirst })
        {
            DeprivationBurdenSaveData burden = GetBurden(state, kind);
            if (burden.burden < BreakdownThreshold || now < burden.nextDamageAt)
            {
                continue;
            }

            burden.nextDamageAt = now + DamageInterval;
            actor.ApplyDamage(actor.MaxHealth * 0.01f, kind == DeprivationKind.Thirst ? "심한 탈수" : "심한 굶주림");
        }

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

    private void UpdateAlert(CharacterActor actor, CharacterDeprivationState state)
    {
        float highest = state.burdens.Where(entry => entry != null).Select(entry => entry.burden).DefaultIfEmpty(0f).Max();
        int level = highest >= BreakdownThreshold ? 2 : highest >= WarningThreshold ? 1 : 0;
        alertLevels.TryGetValue(state.persistentId, out int previous);
        if (level <= previous)
        {
            alertLevels[state.persistentId] = level;
            return;
        }

        alertLevels[state.persistentId] = level;
        string name = actor.Identity?.DisplayName ?? actor.name;
        gameEventBus.RaiseAlert(
            level >= 2 ? $"{name}의 결핍이 위험합니다" : $"{name}의 건강 부담이 쌓입니다",
            level >= 2
                ? "곧 통제를 잃을 수 있습니다. 원인을 해결하거나 경비를 준비하세요."
                : "욕구가 바닥난 채 방치되어 건강 이상이 시작됐습니다.",
            level >= 2 ? EventAlertImportance.High : EventAlertImportance.Medium,
            "생존");
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
            if (actor != null && HasActiveBreakdown(actor))
            {
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }
    }

    private IEnumerator RunSafeDrink(CharacterActor actor, string actorId)
    {
        runningSafeReliefActions.Add(actorId);
        try
        {
            yield return RunDesperateDrink(actor, allowWaste: false, safeOnly: true);
        }
        finally
        {
            runningSafeReliefActions.Remove(actorId);
            actor?.Brain?.RequestImmediateReplan(clearFailures: true);
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

    private IEnumerator RunDesperateDrink(CharacterActor actor, bool allowWaste, bool safeOnly = false)
    {
        if (TryFindWaterStack(actor, safeOnly, out WorldItemStackSnapshot waterStack))
        {
            yield return MoveNear(actor, waterStack.Position, 0);
            if (actor != null
                && !actor.IsDead
                && Manhattan(actor.GetNowXY(), waterStack.Position) == 0
                && itemStackRuntime.TryConsumeStackQuantity(waterStack.StackId, 1, out _))
            {
                actor.ChangesStat(CharacterCondition.THIRST, 75f);
                actor.ApplyMoodFactor("survival:clean-water", "물을 마심", 2f, 90f, 1);
                yield break;
            }
        }

        if (TryFindWaterFacility(actor, out BuildableObject waterFacility))
        {
            yield return MoveNear(actor, waterFacility.centerPos, 1);
            if (actor != null
                && !actor.IsDead
                && waterFacility != null
                && !waterFacility.IsGridDestroyed
                && Manhattan(actor.GetNowXY(), waterFacility.centerPos) <= 1)
            {
                actor.ChangesStat(CharacterCondition.THIRST, 70f);
                actor.ApplyMoodFactor("survival:well-water", "수원에서 물을 마심", 1f, 90f, 1);
                yield break;
            }
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
            yield return new WaitForSeconds(0.75f);
        }

        if (victim != null && victim.IsDead)
        {
            yield return new WaitForSeconds(0.1f);
            WorldItemStackSnapshot corpse = itemStackRuntime.GetAllStacks().FirstOrDefault(stack => stack != null
                && stack.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                && string.Equals(stack.SourceCharacterId, GetPersistentId(victim), StringComparison.Ordinal));
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
        yield return new WaitForSeconds(5f);
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
                yield return new WaitForSeconds(0.8f);
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
                    yield return new WaitForSeconds(0.8f);
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
        yield return new WaitForSeconds(1.5f);
        actor.ChangesStat(CharacterCondition.FUN, 8f);
    }

    private bool TryFindVandalismTarget(CharacterActor actor, out BuildableObject target)
    {
        target = worldRegistry.Buildings
            .Where(building => building != null
                && !building.IsGridDestroyed
                && !building.IsDamaged
                && !building.IsGridMovement)
            .OrderBy(building => Manhattan(actor.GetNowXY(), building.centerPos))
            .FirstOrDefault();
        return target != null;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        return building?.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building != null ? building.name : "시설";
    }

    private IEnumerator MoveNear(CharacterActor actor, Vector2Int target, int distance)
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

        Queue<GridMoveStep> path = grid.GetMovePath(start, position => Manhattan(position, target) <= distance);
        if (path == null || path.Count == 0)
        {
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

        GridPathSearchResult reachable = actor.Brain?.GetPathSearch(actor);
        GridCell best = grid.GetCells()
            .Where(cell => cell != null
                && grid.IsWalkable(cell.Position)
                && (reachable == null || reachable.ContainsPosition(cell.Position)))
            .OrderBy(cell => GetAccidentLocationPriority(grid, cell))
            .ThenBy(cell => Manhattan(actor.GetNowXY(), cell.Position))
            .FirstOrDefault();
        if (best == null)
        {
            return false;
        }

        position = best.Position;
        return true;
    }

    private bool TryFindWaterStack(CharacterActor actor, bool safeOnly, out WorldItemStackSnapshot water)
    {
        water = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && !stack.IsReserved
                && DungeonItemCatalogSO.TryGetStockCategoryFromItemId(stack.ItemId, out StockCategory category)
                && category == StockCategory.Water)
            .OrderBy(stack => stack.State == WorldItemStackState.Stored ? 0 : 1)
            .ThenBy(stack => Manhattan(actor.GetNowXY(), stack.Position))
            .FirstOrDefault();
        return water != null;
    }

    private bool TryFindWaterFacility(CharacterActor actor, out BuildableObject facility)
    {
        facility = worldRegistry.Buildings
            .Where(building => building != null
                && building.gameObject.activeInHierarchy
                && !building.IsGridDestroyed
                && building.BuildingData?.GetAbility<BuildingWaterSourceAbility>() != null
                && survivalFoodRuntime.HasSurvivalWorkAvailable(building, BuiltInWorkTypeIds.DrawWater))
            .OrderBy(building => Manhattan(actor.GetNowXY(), building.centerPos))
            .FirstOrDefault();
        return facility != null;
    }

    private bool TryFindEmergencyFood(CharacterActor actor, out WorldItemStackSnapshot food)
    {
        food = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null && stack.Quantity > 0 && !stack.Forbidden && !stack.IsReserved)
            .Select(stack => new
            {
                Stack = stack,
                Rank = GetEmergencyFoodRank(stack.ItemId)
            })
            .Where(candidate => candidate.Rank < int.MaxValue)
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => Manhattan(actor.GetNowXY(), candidate.Stack.Position))
            .Select(candidate => candidate.Stack)
            .FirstOrDefault();
        return food != null;
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
        return worldRegistry.Characters
            .Where(candidate => IsEligibleHumanoid(candidate)
                && candidate != attacker
                && !candidate.IsDead)
            .OrderBy(candidate => candidate.CurrentHealth / Mathf.Max(1f, candidate.MaxHealth))
            .ThenBy(candidate => CountNearbyHumanoids(candidate, 3))
            .ThenBy(candidate => attacker.SocialMemory?.GetRelationshipSentiment(candidate) ?? 0f)
            .ThenBy(candidate => Manhattan(attacker.GetNowXY(), candidate.GetNowXY()))
            .FirstOrDefault();
    }

    private int CountNearbyHumanoids(CharacterActor center, int radius)
    {
        return worldRegistry.Characters.Count(candidate => IsEligibleHumanoid(candidate)
            && candidate != center
            && !candidate.IsDead
            && Manhattan(center.GetNowXY(), candidate.GetNowXY()) <= radius);
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
        actor?.Brain?.RequestImmediateReplan(clearFailures: true);
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

            GridPathSearchResult search = guard.Brain != null ? guard.Brain.GetPathSearch(guard) : null;
            work.TrySetPrioritySuppressTarget(breakdownActor, search, out _);
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
        foreach (DeprivationKind kind in Enum.GetValues(typeof(DeprivationKind)))
        {
            GetBurden(state, kind);
        }
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
        DeprivationBurdenSaveData burden = state.burdens.FirstOrDefault(entry => entry != null && entry.kind == kind);
        if (burden == null)
        {
            burden = new DeprivationBurdenSaveData { kind = kind };
            state.burdens.Add(burden);
        }
        return burden;
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
