using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// The sole mutable owner of per-character deprivation state. The dictionary is
/// keyed by the persistent <see cref="CharacterId"/> value type; serialized strings
/// are accepted only at the persistence boundary after strict validation.
/// </summary>
internal sealed class CharacterDeprivationAggregateState
{
    private readonly Dictionary<CharacterId, CharacterDeprivationState> states =
        new Dictionary<CharacterId, CharacterDeprivationState>(512);

    internal IReadOnlyDictionary<CharacterId, CharacterDeprivationState> States =>
        states;

    internal bool TryGet(
        CharacterId characterId,
        out CharacterDeprivationState state) =>
        states.TryGetValue(characterId, out state);

    internal void Set(
        CharacterId characterId,
        CharacterDeprivationState state)
    {
        CharacterDeprivationStateStore.RequireCharacterId(characterId);
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.characterId = characterId.Value;
        states[characterId] = state;
    }

    internal bool Remove(CharacterId characterId) => states.Remove(characterId);

    internal CharacterDeprivationAggregateState Clone()
    {
        CharacterDeprivationAggregateState clone =
            new CharacterDeprivationAggregateState();
        foreach (KeyValuePair<CharacterId, CharacterDeprivationState> pair in states)
        {
            clone.Set(
                pair.Key,
                CharacterDeprivationStateStore.CloneState(
                    pair.Value,
                    clearTransientTarget: false));
        }

        return clone;
    }
}

public sealed class CharacterDeprivationWorldDependencies
{
    public CharacterDeprivationWorldDependencies(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery,
        IRoomLayoutCache roomLayoutCache,
        ICharacterAiWorldRegistry worldRegistry,
        IFacilityCandidateCache facilityCandidateCache,
        ISurvivalFoodQuery survivalFoodRuntime)
    {
        GridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        ItemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        FilthQuery = filthQuery
            ?? throw new ArgumentNullException(nameof(filthQuery));
        WaterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        RoomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        FacilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        SurvivalFoodRuntime = survivalFoodRuntime
            ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
    }

    public IGridSystemProvider GridSystemProvider { get; }
    public IWorldItemStackRuntime ItemStackRuntime { get; }
    public IWorldFilthQuery FilthQuery { get; }
    public IWorldWaterQuery WaterQuery { get; }
    public IRoomLayoutCache RoomLayoutCache { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IFacilityCandidateCache FacilityCandidateCache { get; }
    public ISurvivalFoodQuery SurvivalFoodRuntime { get; }
}

public sealed class CharacterDeprivationSystemDependencies
{
    public CharacterDeprivationSystemDependencies(
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IDynamicFrameWorkBudget frameWorkBudget,
        IRandomStreamProvider randomStreamProvider,
        IUiClock uiClock,
        IDoorAccessQuery doorAccessQuery,
        ICharacterNeedBalanceRuntime needBalanceRuntime,
        IDungeonDebugRuleQuery debugRules,
        IHeritableTraitEffectQuery heritableTraits,
        IReproductionService reproduction)
    {
        GameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        GameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        FrameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        UiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
        DoorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        NeedBalanceRuntime = needBalanceRuntime
            ?? throw new ArgumentNullException(nameof(needBalanceRuntime));
        DebugRules = debugRules
            ?? throw new ArgumentNullException(nameof(debugRules));
        HeritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
        Reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
    }

    public IGameEventBus GameEventBus { get; }
    public IGameClock GameClock { get; }
    public IDynamicFrameWorkBudget FrameWorkBudget { get; }
    public IRandomStreamProvider RandomStreamProvider { get; }
    public IUiClock UiClock { get; }
    public IDoorAccessQuery DoorAccessQuery { get; }
    public ICharacterNeedBalanceRuntime NeedBalanceRuntime { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
    public IHeritableTraitEffectQuery HeritableTraits { get; }
    public IReproductionService Reproduction { get; }
}

public sealed class CharacterDeprivationAuthorityDependencies
{
    public CharacterDeprivationAuthorityDependencies(
        IItemDefinitionCatalog itemCatalog,
        CharacterDeprivationStateStore stateStore,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICharacterPerformanceQuery performance,
        CharacterPrimitiveSurvivalDependencies primitiveSurvival)
    {
        ItemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        StateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        Performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        PrimitiveSurvival = primitiveSurvival
            ?? throw new ArgumentNullException(nameof(primitiveSurvival));
    }

    public IItemDefinitionCatalog ItemCatalog { get; }
    public CharacterDeprivationStateStore StateStore { get; }
    public ICharacterBodyHealthCommand BodyHealthCommands { get; }
    public ICharacterPerformanceQuery Performance { get; }
    public CharacterPrimitiveSurvivalDependencies PrimitiveSurvival { get; }

    internal CharacterBreakdownActionRunner CreateBreakdownActionRunner(
        CharacterBreakdownWorld world,
        IRandomStream breakdownRandom,
        ICharacterNeedBalanceRuntime needBalanceRuntime,
        CharacterSafeDrinkPlanner safeDrinkPlanner,
        CharacterEmergencyMovement emergencyMovement,
        CharacterDeprivationDiagnostics diagnostics,
        CharacterDeprivationConsequences consequences,
        IGameEventBus events)
    {
        return new CharacterBreakdownActionRunner(
            world,
            new CharacterBreakdownActionPolicyDependencies(
                breakdownRandom,
                needBalanceRuntime,
                ItemCatalog,
                BodyHealthCommands,
                Performance),
            new CharacterBreakdownActionExecutionDependencies(
                StateStore,
                safeDrinkPlanner,
                emergencyMovement,
                diagnostics,
                consequences,
                PrimitiveSurvival.FieldMeals,
                events));
    }
}

public sealed class CharacterPrimitiveSurvivalDependencies
{
    public CharacterPrimitiveSurvivalDependencies(
        IFieldMealConsumptionCommand fieldMeals,
        IItemQuantityReservationService quantityReservations,
        IReservedItemTransferService reservedTransfers)
    {
        FieldMeals = fieldMeals ?? throw new ArgumentNullException(nameof(fieldMeals));
        QuantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        ReservedTransfers = reservedTransfers
            ?? throw new ArgumentNullException(nameof(reservedTransfers));
    }

    public IFieldMealConsumptionCommand FieldMeals { get; }
    public IItemQuantityReservationService QuantityReservations { get; }
    public IReservedItemTransferService ReservedTransfers { get; }
}

public sealed class CharacterDeprivationStateStore
{
    internal const int BurdenKindCount = 6;

    private static readonly string[] ForeignPersistentIdPrefixes =
    {
        "item-instance:",
        "stack:",
        "building:",
        "wildlife-habitat:",
        "filth:",
        "water:"
    };

    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public CharacterDeprivationStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    internal int PublishedRestoreRevision =>
        aggregateRootStore.PublishedRestoreRevision;

    internal bool IsRestoreStaging => aggregateRootStore.IsRestoreStaging;

    private CharacterDeprivationAggregateState ReadState =>
        aggregateRootStore.GetOrCreate(
            () => new CharacterDeprivationAggregateState());

    private CharacterDeprivationAggregateState WriteState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterDeprivationAggregateState(),
            source => source.Clone());

    internal IEnumerable<KeyValuePair<CharacterId, CharacterDeprivationState>> Entries =>
        ReadState.States;

    internal CharacterDeprivationState Ensure(CharacterActor actor) =>
        Ensure(CharacterPersistentIdentity.Require(actor));

    internal CharacterDeprivationState Ensure(CharacterId characterId)
    {
        RequireCharacterId(characterId);
        CharacterDeprivationAggregateState aggregate = WriteState;
        if (!aggregate.TryGet(characterId, out CharacterDeprivationState state))
        {
            state = CreateState(characterId);
            aggregate.Set(characterId, state);
        }

        EnsureNormalized(state);
        return state;
    }

    internal bool TryGet(
        CharacterActor actor,
        out CharacterDeprivationState state)
    {
        state = null;
        return CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            && ReadState.TryGet(characterId, out state);
    }

    internal bool TryGet(
        CharacterId characterId,
        out CharacterDeprivationState state)
    {
        state = null;
        return characterId.IsValid && ReadState.TryGet(characterId, out state);
    }

    internal bool TryGetWritable(
        CharacterActor actor,
        out CharacterDeprivationState state)
    {
        state = null;
        return CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            && TryGetWritable(characterId, out state);
    }

    internal bool TryGetWritable(
        CharacterId characterId,
        out CharacterDeprivationState state)
    {
        state = null;
        return characterId.IsValid && WriteState.TryGet(characterId, out state);
    }

    internal void Remove(CharacterId characterId)
    {
        RequireCharacterId(characterId);
        WriteState.Remove(characterId);
    }

    internal List<CharacterDeprivationState> Capture()
    {
        return ReadState.States
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => CloneState(pair.Value, clearTransientTarget: true))
            .ToList();
    }

    internal void Restore(
        IEnumerable<CharacterDeprivationState> savedStates,
        IReadOnlyCollection<CharacterId> knownCharacterIds)
    {
        CharacterDeprivationAggregateState restored =
            BuildValidatedAggregate(savedStates, knownCharacterIds);
        aggregateRootStore.Replace(restored);
    }

    internal void ReplaceValidatedAggregate(
        CharacterDeprivationAggregateState restored)
    {
        aggregateRootStore.Replace(
            restored ?? throw new ArgumentNullException(nameof(restored)));
    }

    internal static CharacterDeprivationAggregateState BuildValidatedAggregate(
        IEnumerable<CharacterDeprivationState> savedStates,
        IReadOnlyCollection<CharacterId> knownCharacterIds)
    {
        if (savedStates == null)
        {
            throw new InvalidOperationException(
                "Deprivation payload has no character collection.");
        }

        if (knownCharacterIds == null)
        {
            throw new ArgumentNullException(nameof(knownCharacterIds));
        }

        HashSet<CharacterId> known = new HashSet<CharacterId>();
        foreach (CharacterId characterId in knownCharacterIds)
        {
            RequireCharacterId(characterId);
            if (!known.Add(characterId))
            {
                throw new InvalidOperationException(
                    $"World state contains duplicate CharacterId '{characterId.Value}'.");
            }
        }

        CharacterDeprivationAggregateState restored =
            new CharacterDeprivationAggregateState();
        foreach (CharacterDeprivationState source in savedStates)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Deprivation payload contains a null character state.");
            }

            CharacterId characterId = ParsePayloadCharacterId(source.characterId);
            if (!known.Contains(characterId))
            {
                throw new InvalidOperationException(
                    $"Deprivation payload references unknown CharacterId '{characterId.Value}'.");
            }

            if (restored.TryGet(characterId, out _))
            {
                throw new InvalidOperationException(
                    $"Deprivation payload contains duplicate CharacterId '{characterId.Value}'.");
            }

            ValidateState(source, characterId);
            restored.Set(
                characterId,
                CloneState(source, clearTransientTarget: true));
        }

        return restored;
    }

    internal bool TryBeginBreakdown(
        CharacterId characterId,
        DeprivationKind cause,
        CharacterBreakdownKind kind,
        float startedAt,
        float suppressionResistance,
        string reason,
        out CharacterDeprivationState state,
        out int generation)
    {
        state = Ensure(characterId);
        generation = state.breakdownGeneration;
        if (state.breakdown.active)
        {
            return false;
        }

        checked
        {
            state.breakdownGeneration++;
        }
        generation = state.breakdownGeneration;
        state.breakdown = new CharacterBreakdownState
        {
            active = true,
            cause = cause,
            kind = kind,
            startedAt = Mathf.Max(0f, startedAt),
            suppressionResistance = Mathf.Max(0f, suppressionResistance),
            lastReplanReason = reason ?? string.Empty
        };
        return true;
    }

    internal bool TryClaimBreakdownSideEffects(
        CharacterId characterId,
        int generation)
    {
        if (generation <= 0
            || !TryGetWritable(characterId, out CharacterDeprivationState state)
            || state.breakdownGeneration != generation
            || state.dispatchedBreakdownGeneration >= generation)
        {
            return false;
        }

        state.dispatchedBreakdownGeneration = generation;
        return true;
    }

    internal static DeprivationBurdenSaveData GetBurden(
        CharacterDeprivationState state,
        DeprivationKind kind)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        int index = (int)kind;
        if (index < 0 || index >= BurdenKindCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported deprivation kind.");
        }

        EnsureNormalized(state);
        return state.burdens[index];
    }

    internal static CharacterBreakdownState CloneBreakdown(
        CharacterBreakdownState state,
        bool clearTransientTarget = false)
    {
        state ??= new CharacterBreakdownState();
        return new CharacterBreakdownState
        {
            active = state.active,
            kind = state.kind,
            cause = state.cause,
            targetId = clearTransientTarget
                ? string.Empty
                : state.targetId ?? string.Empty,
            targetGridX = clearTransientTarget ? 0 : state.targetGridX,
            targetGridY = clearTransientTarget ? 0 : state.targetGridY,
            startedAt = state.startedAt,
            suppressionResistance = state.suppressionResistance,
            lastReplanReason = state.lastReplanReason ?? string.Empty
        };
    }

    internal static CharacterDeprivationState CloneState(
        CharacterDeprivationState state,
        bool clearTransientTarget)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        CharacterDeprivationState clone = new CharacterDeprivationState
        {
            characterId = state.characterId ?? string.Empty,
            burdens = (state.burdens ?? new List<DeprivationBurdenSaveData>())
                .Where(entry => entry != null)
                .Select(entry => new DeprivationBurdenSaveData
                {
                    kind = entry.kind,
                    burden = entry.burden,
                    maximumHeldSeconds = entry.maximumHeldSeconds,
                    nextBreakdownCheckAt = entry.nextBreakdownCheckAt,
                    nextDamageAt = entry.nextDamageAt
                }).ToList(),
            breakdown = CloneBreakdown(state.breakdown, clearTransientTarget),
            tabooMemories = new List<string>(
                state.tabooMemories ?? new List<string>()),
            infectionBurden = state.infectionBurden,
            lastUpdatedAt = state.lastUpdatedAt,
            nextSafeReliefAttemptAt = state.nextSafeReliefAttemptAt,
            breakdownGeneration = state.breakdownGeneration,
            dispatchedBreakdownGeneration = state.dispatchedBreakdownGeneration
        };
        EnsureNormalized(clone);
        return clone;
    }

    internal static void RequireCharacterId(CharacterId characterId)
    {
        if (!characterId.IsValid || IsForeignPersistentId(characterId.Value))
        {
            throw new ArgumentException(
                $"'{characterId.Value}' is not a valid CharacterId for deprivation state.",
                nameof(characterId));
        }
    }

    private static CharacterId ParsePayloadCharacterId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !string.Equals(raw, raw.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Deprivation payload contains an empty or non-canonical CharacterId.");
        }

        CharacterId characterId = (CharacterId)raw;
        try
        {
            RequireCharacterId(characterId);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Deprivation payload contains invalid CharacterId '{raw}'.",
                exception);
        }
        return characterId;
    }

    private static bool IsForeignPersistentId(string value)
    {
        for (int i = 0; i < ForeignPersistentIdPrefixes.Length; i++)
        {
            if (value.StartsWith(
                    ForeignPersistentIdPrefixes[i],
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static CharacterDeprivationState CreateState(CharacterId characterId)
    {
        CharacterDeprivationState state = new CharacterDeprivationState
        {
            characterId = characterId.Value
        };
        EnsureNormalized(state);
        return state;
    }

    private static void EnsureNormalized(CharacterDeprivationState state)
    {
        state.burdens ??= new List<DeprivationBurdenSaveData>();
        state.breakdown ??= new CharacterBreakdownState();
        state.tabooMemories ??= new List<string>();
        NormalizeBurdens(state.burdens);
    }

    private static void NormalizeBurdens(
        List<DeprivationBurdenSaveData> burdens)
    {
        bool normalized = burdens.Count == BurdenKindCount;
        if (normalized)
        {
            for (int index = 0; index < BurdenKindCount; index++)
            {
                DeprivationBurdenSaveData burden = burdens[index];
                if (burden == null || (int)burden.kind != index)
                {
                    normalized = false;
                    break;
                }
            }
        }
        if (normalized)
        {
            return;
        }

        DeprivationBurdenSaveData[] byKind =
            new DeprivationBurdenSaveData[BurdenKindCount];
        foreach (DeprivationBurdenSaveData burden in burdens)
        {
            int index = burden != null ? (int)burden.kind : -1;
            if (index >= 0 && index < BurdenKindCount && byKind[index] == null)
            {
                byKind[index] = burden;
            }
        }

        burdens.Clear();
        for (int index = 0; index < BurdenKindCount; index++)
        {
            burdens.Add(
                byKind[index]
                ?? new DeprivationBurdenSaveData
                {
                    kind = (DeprivationKind)index
                });
        }
    }

    private static void ValidateState(
        CharacterDeprivationState state,
        CharacterId characterId)
    {
        if (state.burdens == null
            || state.burdens.Count != BurdenKindCount)
        {
            throw Invalid(characterId, "must contain exactly six burdens");
        }

        bool[] seen = new bool[BurdenKindCount];
        for (int index = 0; index < state.burdens.Count; index++)
        {
            DeprivationBurdenSaveData burden = state.burdens[index];
            int kindIndex = burden != null ? (int)burden.kind : -1;
            if (kindIndex < 0
                || kindIndex >= BurdenKindCount
                || seen[kindIndex]
                || kindIndex != index)
            {
                throw Invalid(characterId, "contains missing, duplicate, or unordered burdens");
            }

            seen[kindIndex] = true;
            RequireFiniteRange(burden.burden, 0f, 100f, characterId, "burden");
            RequireFiniteRange(
                burden.maximumHeldSeconds,
                0f,
                float.MaxValue,
                characterId,
                "maximum-held time");
            RequireFiniteRange(
                burden.nextBreakdownCheckAt,
                0f,
                float.MaxValue,
                characterId,
                "breakdown cooldown");
            RequireFiniteRange(
                burden.nextDamageAt,
                0f,
                float.MaxValue,
                characterId,
                "damage cooldown");
        }

        if (state.breakdown == null)
        {
            throw Invalid(characterId, "has no breakdown state");
        }
        if (!Enum.IsDefined(typeof(DeprivationKind), state.breakdown.cause)
            || !Enum.IsDefined(typeof(CharacterBreakdownKind), state.breakdown.kind)
            || (state.breakdown.active
                && state.breakdown.kind == CharacterBreakdownKind.None))
        {
            throw Invalid(characterId, "contains an invalid breakdown kind or cause");
        }
        if (!string.IsNullOrEmpty(state.breakdown.targetId)
            || state.breakdown.targetGridX != 0
            || state.breakdown.targetGridY != 0)
        {
            throw Invalid(
                characterId,
                "contains a transient breakdown target; routing caches are not persistent");
        }

        RequireFiniteRange(
            state.breakdown.startedAt,
            0f,
            float.MaxValue,
            characterId,
            "breakdown start time");
        RequireFiniteRange(
            state.breakdown.suppressionResistance,
            0f,
            float.MaxValue,
            characterId,
            "suppression resistance");
        RequireFiniteRange(
            state.infectionBurden,
            0f,
            100f,
            characterId,
            "infection burden");
        RequireFiniteRange(
            state.lastUpdatedAt,
            0f,
            float.MaxValue,
            characterId,
            "last update time");
        RequireFiniteRange(
            state.nextSafeReliefAttemptAt,
            0f,
            float.MaxValue,
            characterId,
            "safe-relief cooldown");
        if (state.breakdownGeneration < 0
            || state.dispatchedBreakdownGeneration < 0
            || state.dispatchedBreakdownGeneration > state.breakdownGeneration
            || (state.breakdown.active
                && (state.breakdownGeneration <= 0
                    || state.dispatchedBreakdownGeneration
                        != state.breakdownGeneration)))
        {
            throw Invalid(characterId, "contains an invalid breakdown generation");
        }
        if (state.tabooMemories == null
            || state.tabooMemories.Count > 24
            || state.tabooMemories.Any(memory => string.IsNullOrWhiteSpace(memory)))
        {
            throw Invalid(characterId, "contains invalid taboo memories");
        }
    }

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        CharacterId characterId,
        string field)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw Invalid(characterId, $"contains invalid {field}");
        }
    }

    private static InvalidOperationException Invalid(
        CharacterId characterId,
        string detail) =>
        new InvalidOperationException(
            $"Deprivation state '{characterId.Value}' {detail}.");
}

public sealed class DarkSurvivalRestoreCandidate
{
    internal DarkSurvivalRestoreCandidate(
        CharacterDeprivationAggregateState characters,
        WorldFilthRestoreCandidate filth,
        WorldWaterRestoreCandidate water)
    {
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Filth = filth ?? throw new ArgumentNullException(nameof(filth));
        Water = water ?? throw new ArgumentNullException(nameof(water));
    }

    internal CharacterDeprivationAggregateState Characters { get; }
    internal WorldFilthRestoreCandidate Filth { get; }
    internal WorldWaterRestoreCandidate Water { get; }
}

internal sealed class CharacterDeprivationPersistenceCoordinator
{
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldFilthQuery filthQuery;
    private readonly IWorldWaterQuery waterQuery;

    internal CharacterDeprivationPersistenceCoordinator(
        CharacterDeprivationStateStore stateStore,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldFilthQuery filthQuery,
        IWorldWaterQuery waterQuery)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.filthQuery = filthQuery
            ?? throw new ArgumentNullException(nameof(filthQuery));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
    }

    internal DungeonDarkSurvivalSaveData Capture()
    {
        return new DungeonDarkSurvivalSaveData
        {
            version = DungeonDarkSurvivalSaveData.CurrentVersion,
            nextFilthSequence = filthQuery.NextFilthSequence,
            nextWaterSequence = waterQuery.NextWaterSequence,
            characters = stateStore.Capture(),
            filth = filthQuery.CaptureFilth(),
            waterSources = waterQuery.CaptureWaterSources()
        };
    }

    internal DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData saveData)
    {
        DungeonDarkSurvivalSaveData source = RequireCurrent(saveData);
        IReadOnlyCollection<CharacterId> knownCharacterIds =
            CaptureKnownCharacterIds();
        CharacterDeprivationAggregateState restoredCharacters =
            CharacterDeprivationStateStore.BuildValidatedAggregate(
                source.characters,
                knownCharacterIds);
        ValidateWorldPayload(
            source,
            knownCharacterIds,
            requireKnownFilthSources: true);

        IWorldFilthRestoreCandidatePort filthRestore = filthQuery
            as IWorldFilthRestoreCandidatePort
            ?? throw new InvalidOperationException(
                "World filth runtime has no strict restore candidate port.");
        IWorldWaterRestoreCandidatePort waterRestore = waterQuery
            as IWorldWaterRestoreCandidatePort
            ?? throw new InvalidOperationException(
                "World water runtime has no strict restore candidate port.");

        return new DarkSurvivalRestoreCandidate(
            restoredCharacters,
            filthRestore.BuildRestoreCandidate(
                source.filth.Select(CloneFilth).ToArray(),
                source.nextFilthSequence),
            waterRestore.BuildRestoreCandidate(
                source.waterSources.Select(CloneWater).ToArray(),
                source.nextWaterSequence));
    }

    internal void PublishRestoreCandidate(DarkSurvivalRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        stateStore.ReplaceValidatedAggregate(candidate.Characters);
        ((IWorldFilthRestoreCandidatePort)filthQuery)
            .PublishRestoreCandidate(candidate.Filth);
        ((IWorldWaterRestoreCandidatePort)waterQuery)
            .PublishRestoreCandidate(candidate.Water);
    }

    private IReadOnlyCollection<CharacterId> CaptureKnownCharacterIds()
    {
        HashSet<CharacterId> known = new HashSet<CharacterId>();
        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor == null)
            {
                continue;
            }

            CharacterId characterId = CharacterPersistentIdentity.Require(actor);
            CharacterDeprivationStateStore.RequireCharacterId(characterId);
            if (!known.Add(characterId))
            {
                throw new InvalidOperationException(
                    $"World state contains duplicate CharacterId '{characterId.Value}'.");
            }
        }

        return known;
    }

    private static DungeonDarkSurvivalSaveData RequireCurrent(
        DungeonDarkSurvivalSaveData saveData)
    {
        DungeonDarkSurvivalSaveData source = saveData
            ?? throw new ArgumentNullException(nameof(saveData));
        if (source.version != DungeonDarkSurvivalSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported dark survival save version {source.version}.");
        }
        return source;
    }

    internal static void ValidateWorldPayload(
        DungeonDarkSurvivalSaveData source,
        IReadOnlyCollection<CharacterId> knownCharacterIds,
        bool requireKnownFilthSources)
    {
        if (source.nextFilthSequence < 1 || source.nextWaterSequence < 1)
        {
            throw new InvalidOperationException(
                "Dark-survival payload contains an invalid world sequence.");
        }
        if (source.filth == null || source.waterSources == null)
        {
            throw new InvalidOperationException(
                "Dark-survival payload is missing world-state collections.");
        }

        HashSet<CharacterId> known = new HashSet<CharacterId>(knownCharacterIds);
        HashSet<string> filthIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldFilthSaveData filth in source.filth)
        {
            if (filth == null
                || !IsCanonicalKindId(filth.filthId, "filth")
                || !filthIds.Add(filth.filthId)
                || !Enum.IsDefined(typeof(WorldFilthType), filth.type)
                || !IsFiniteInRange(filth.amount, float.Epsilon, float.MaxValue)
                || !IsFiniteInRange(filth.infectionRisk, 0f, 1f))
            {
                throw new InvalidOperationException(
                    "Dark-survival payload contains invalid or duplicate filth state.");
            }

            if (!string.IsNullOrEmpty(filth.sourceCharacterId))
            {
                if (!IsCanonicalId(filth.sourceCharacterId))
                {
                    throw new InvalidOperationException(
                        "Filth state contains a non-canonical source CharacterId.");
                }

                CharacterId sourceId = (CharacterId)filth.sourceCharacterId;
                CharacterDeprivationStateStore.RequireCharacterId(sourceId);
                if (requireKnownFilthSources && !known.Contains(sourceId))
                {
                    throw new InvalidOperationException(
                        $"Filth state references unknown CharacterId '{sourceId.Value}'.");
                }
            }
        }

        HashSet<string> waterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldWaterSourceSaveData water in source.waterSources)
        {
            if (water == null
                || !IsCanonicalKindId(water.sourceId, "water")
                || !waterIds.Add(water.sourceId)
                || !Enum.IsDefined(typeof(GridCellTerrainType), water.terrainType)
                || !Enum.IsDefined(typeof(WorldWaterQuality), water.quality)
                || !IsFiniteInRange(water.capacity, float.Epsilon, float.MaxValue)
                || !IsFiniteInRange(water.remaining, 0f, water.capacity)
                || !IsFiniteInRange(
                    water.regenerationPerSecond,
                    0f,
                    float.MaxValue)
                || water.pathogenDiseaseId == null)
            {
                throw new InvalidOperationException(
                    "Dark-survival payload contains invalid or duplicate water-source state.");
            }
        }
    }

    private static bool IsCanonicalKindId(string value, string kind) =>
        IsCanonicalId(value)
        && value.StartsWith(kind + ":", StringComparison.Ordinal)
        && value.Length > kind.Length + 1;

    private static bool IsCanonicalId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static WorldFilthSaveData CloneFilth(WorldFilthSaveData source) =>
        new WorldFilthSaveData
        {
            filthId = source.filthId,
            type = source.type,
            gridX = source.gridX,
            gridY = source.gridY,
            amount = source.amount,
            infectionRisk = source.infectionRisk,
            sourceCharacterId = source.sourceCharacterId,
            wallStain = source.wallStain
        };

    private static WorldWaterSourceSaveData CloneWater(
        WorldWaterSourceSaveData source) =>
        new WorldWaterSourceSaveData
        {
            sourceId = source.sourceId,
            gridX = source.gridX,
            gridY = source.gridY,
            terrainType = source.terrainType,
            quality = source.quality,
            capacity = source.capacity,
            remaining = source.remaining,
            regenerationPerSecond = source.regenerationPerSecond,
            pathogenDiseaseId = source.pathogenDiseaseId ?? string.Empty
        };
}

public static class CharacterDeprivationAuthorityDebugScenarios
{
    public static List<string> RunAll()
    {
        List<string> errors = new List<string>();
        Run("typed_v2_round_trip", VerifyTypedRoundTrip, errors);
        Run("invalid_payload_no_mutation", VerifyInvalidPayloadNoMutation, errors);
        Run("typed_id_collision_rejected", VerifyTypedIdCollisionRejected, errors);
        Run("unknown_world_reference_rejected", VerifyUnknownWorldReference, errors);
        Run("breakdown_side_effect_once", VerifyBreakdownSideEffectOnce, errors);
        Run("consecutive_run_scope_isolation", VerifyConsecutiveRunScopeIsolation, errors);
        Run("rollback_free_contract", VerifyRollbackFreeContract, errors);
        return errors;
    }

    private static void VerifyTypedRoundTrip()
    {
        CharacterId owner = CharacterId.Owner;
        CharacterDeprivationStateStore source = CreateStore();
        CharacterDeprivationState state = source.Ensure(owner);
        Get(state, DeprivationKind.Thirst).burden = 74f;
        Get(state, DeprivationKind.Thirst).nextDamageAt = 91f;
        state.infectionBurden = 27f;
        state.nextSafeReliefAttemptAt = 14f;
        state.breakdown.targetId = "water:transient";

        DungeonDarkSurvivalSaveData envelope = new DungeonDarkSurvivalSaveData
        {
            characters = source.Capture()
        };
        string json = JsonUtility.ToJson(envelope);
        DungeonDarkSurvivalSaveData roundTrip =
            JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(json);
        CharacterDeprivationStateStore restored = CreateStore();
        restored.Restore(roundTrip.characters, new[] { owner });

        Require(restored.TryGet(owner, out CharacterDeprivationState result),
            "typed CharacterId did not restore");
        Require(Mathf.Approximately(Get(result, DeprivationKind.Thirst).burden, 74f),
            "burden did not round-trip");
        Require(Mathf.Approximately(result.nextSafeReliefAttemptAt, 14f),
            "safe-relief cooldown did not round-trip");
        Require(string.IsNullOrEmpty(result.breakdown.targetId),
            "transient breakdown routing target was persisted");
    }

    private static void VerifyInvalidPayloadNoMutation()
    {
        CharacterId owner = CharacterId.Owner;
        CharacterDeprivationStateStore store = CreateStore();
        CharacterDeprivationState live = store.Ensure(owner);
        Get(live, DeprivationKind.Hunger).burden = 33f;
        string before = JsonUtility.ToJson(new DungeonDarkSurvivalSaveData
        {
            characters = store.Capture()
        });

        CharacterDeprivationState unknown = CreatePayloadState("unknown-character");
        RequireThrows(() => store.Restore(
            new[] { unknown },
            new[] { owner }));
        string after = JsonUtility.ToJson(new DungeonDarkSurvivalSaveData
        {
            characters = store.Capture()
        });
        Require(string.Equals(before, after, StringComparison.Ordinal),
            "invalid restore mutated the live deprivation aggregate");
    }

    private static void VerifyTypedIdCollisionRejected()
    {
        CharacterDeprivationStateStore store = CreateStore();
        RequireThrows(() => store.Restore(
            new[] { CreatePayloadState("stack:owner") },
            new[] { (CharacterId)"stack:owner" }));
        RequireThrows(() => store.Restore(
            new[] { CreatePayloadState(" owner ") },
            new[] { CharacterId.Owner }));
    }

    private static void VerifyUnknownWorldReference()
    {
        DungeonDarkSurvivalSaveData payload = new DungeonDarkSurvivalSaveData();
        payload.filth.Add(new WorldFilthSaveData
        {
            filthId = "filth:00000001",
            type = WorldFilthType.Waste,
            amount = 1f,
            infectionRisk = 0.2f,
            sourceCharacterId = "unknown-character"
        });
        RequireThrows(() =>
            CharacterDeprivationPersistenceCoordinator.ValidateWorldPayload(
                payload,
                new[] { CharacterId.Owner },
                requireKnownFilthSources: true));

        payload.filth.Clear();
        payload.waterSources.Add(new WorldWaterSourceSaveData
        {
            sourceId = "not-a-water-id",
            capacity = 1f,
            remaining = 1f
        });
        RequireThrows(() =>
            CharacterDeprivationPersistenceCoordinator.ValidateWorldPayload(
                payload,
                new[] { CharacterId.Owner },
                requireKnownFilthSources: true));
    }

    private static void VerifyBreakdownSideEffectOnce()
    {
        CharacterId owner = CharacterId.Owner;
        CharacterDeprivationStateStore store = CreateStore();
        bool began = store.TryBeginBreakdown(
            owner,
            DeprivationKind.Thirst,
            CharacterBreakdownKind.DesperateDrink,
            10f,
            25f,
            "fixture",
            out _,
            out int generation);
        int effects = store.TryClaimBreakdownSideEffects(owner, generation) ? 1 : 0;
        bool duplicate = store.TryBeginBreakdown(
            owner,
            DeprivationKind.Thirst,
            CharacterBreakdownKind.DesperateDrink,
            11f,
            25f,
            "duplicate",
            out _,
            out int duplicateGeneration);
        if (store.TryClaimBreakdownSideEffects(owner, duplicateGeneration))
        {
            effects++;
        }

        CharacterDeprivationStateStore restored = CreateStore();
        restored.Restore(store.Capture(), new[] { owner });
        if (restored.TryClaimBreakdownSideEffects(owner, generation))
        {
            effects++;
        }

        Require(began && !duplicate, "breakdown transition was not idempotent");
        Require(effects == 1, "breakdown side effects were not claimed exactly once");
    }

    private static void VerifyConsecutiveRunScopeIsolation()
    {
        CharacterId shared = (CharacterId)"character:shared-fixture";
        CharacterId firstOnly = (CharacterId)"character:first-run-only";
        CharacterId secondOnly = (CharacterId)"character:second-run-only";
        DungeonRuntimeAggregateRootStore firstRoot = new();
        CharacterDeprivationStateStore first = new(firstRoot);
        Get(first.Ensure(shared), DeprivationKind.Hunger).burden = 63f;
        first.Ensure(firstOnly);
        Require(first.TryBeginBreakdown(shared, DeprivationKind.Hunger, CharacterBreakdownKind.DesperateEat, 12f, 30f, "first-run", out _, out int firstGeneration), "first run did not begin its breakdown");
        Require(first.TryClaimBreakdownSideEffects(shared, firstGeneration) && !first.TryClaimBreakdownSideEffects(shared, firstGeneration), "first-run exactly-once claim failed");
        DungeonRuntimeAggregateRootStore secondRoot = new();
        CharacterDeprivationStateStore second = new(secondRoot);
        Require(!second.TryGet(shared, out _) && !second.TryGet(firstOnly, out _) && secondRoot.PublishedRestoreRevision == 0, "state or restore revision leaked into the next run scope");
        CharacterDeprivationState secondState = second.Ensure(shared);
        Require(Mathf.Approximately(Get(secondState, DeprivationKind.Hunger).burden, 0f) && secondState.breakdownGeneration == 0 && secondState.dispatchedBreakdownGeneration == 0, "shared state or exactly-once ledger leaked across runs");
        second.Ensure(secondOnly);
        Require(second.TryBeginBreakdown(shared, DeprivationKind.Hunger, CharacterBreakdownKind.DesperateEat, 4f, 20f, "second-run", out _, out int secondGeneration) && secondGeneration == 1, "second run did not begin with generation one");
        Require(second.TryClaimBreakdownSideEffects(shared, secondGeneration) && !second.TryClaimBreakdownSideEffects(shared, secondGeneration), "second-run exactly-once claim failed");
        Require(!first.TryGet(secondOnly, out _) && first.TryGet(shared, out CharacterDeprivationState retained) && Mathf.Approximately(Get(retained, DeprivationKind.Hunger).burden, 63f), "the second run mutated the first run aggregate");
    }

    private static void VerifyRollbackFreeContract()
    {
        Require(DungeonDarkSurvivalSaveData.CurrentVersion == 3,
            "dark-survival payload is not exact V3");
        Require(typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(
                typeof(DarkSurvivalSaveSection)),
            "dark-survival save section is not rollback-free");
        Require(typeof(IDungeonStagedSaveSection).IsAssignableFrom(
                typeof(DarkSurvivalSaveSection))
            && typeof(ICharacterDeprivationPersistence).GetMethod(
                    nameof(ICharacterDeprivationPersistence.BuildRestoreCandidate))
                ?.ReturnType == typeof(DarkSurvivalRestoreCandidate),
            "deprivation runtime has no detached strict restore candidate");
    }

    private static CharacterDeprivationStateStore CreateStore() =>
        new CharacterDeprivationStateStore(
            new DungeonRuntimeAggregateRootStore());

    private static CharacterDeprivationState CreatePayloadState(string characterId)
    {
        CharacterDeprivationState state = new CharacterDeprivationState
        {
            characterId = characterId,
            burdens = new List<DeprivationBurdenSaveData>()
        };
        for (int index = 0;
             index < CharacterDeprivationStateStore.BurdenKindCount;
             index++)
        {
            state.burdens.Add(new DeprivationBurdenSaveData
            {
                kind = (DeprivationKind)index
            });
        }
        return state;
    }

    private static DeprivationBurdenSaveData Get(
        CharacterDeprivationState state,
        DeprivationKind kind) =>
        CharacterDeprivationStateStore.GetBurden(state, kind);

    private static void Run(
        string name,
        Action scenario,
        ICollection<string> errors)
    {
        try
        {
            scenario();
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static void RequireThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException("expected validation failure was not raised");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
