using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public readonly struct SpeciesIncidentContext
{
    public SpeciesIncidentContext(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        Actor = actor;
        Species = species;
        State = state;
    }

    public CharacterActor Actor { get; }
    public CharacterSpeciesSO Species { get; }
    public CharacterSpeciesRuntimeState State { get; }
}

public interface ISpeciesIncidentHandler
{
    string IncidentId { get; }
    bool Execute(SpeciesIncidentContext context, out string summary);
}

public interface ISpeciesIncidentHandlerRegistry
{
    bool TryExecute(SpeciesIncidentContext context, out string summary);
}

public interface ICharacterSpeciesPersistence
{
    CharacterSpeciesRuntimeSaveData Capture();
    CharacterSpeciesRestoreCandidate BuildRestore(
        CharacterSpeciesRuntimeSaveData data);
    void Restore(CharacterSpeciesRestoreCandidate candidate);
}

public interface ICharacterSpeciesRechargeService
{
    bool IsRechargeAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out string reason);
    float GetRechargeUrgency(
        CharacterActor actor,
        BuildableObject facility);
    bool TryBeginRecharge(
        CharacterActor actor,
        BuildableObject facility,
        out float completedWork,
        out DomainFailure failure);
    bool TryApplyRechargeWork(
        CharacterActor actor,
        BuildableObject facility,
        float work,
        out bool completed,
        out DomainFailure failure);
    void CancelRecharge(CharacterId characterId);
}

internal sealed class CharacterSpeciesAggregateState
{
    internal Dictionary<CharacterId, CharacterSpeciesRuntimeState> Characters { get; } =
        new();
    internal float NextTickAt { get; set; }
    internal List<SpeciesActionObservationSaveData> PendingObservations { get; } = new();
}

public sealed class CharacterSpeciesRestoreCandidate
{
    internal CharacterSpeciesRestoreCandidate(CharacterSpeciesAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterSpeciesAggregateState State { get; }
}

internal static class CharacterSpeciesStateCodec
{
    internal static CharacterSpeciesRestoreCandidate BuildRestore(
        CharacterSpeciesRuntimeSaveData data,
        float nextTickAt,
        Func<CharacterSpeciesId, string> getIncidentId)
    {
        if (data == null
            || data.version != CharacterSpeciesRuntimeSaveData.CurrentVersion
            || data.characters == null || data.pendingObservations == null)
        {
            throw new InvalidOperationException(
                "Character-species payload is null, incomplete, or has an unsupported version.");
        }
        if (getIncidentId == null)
        {
            throw new ArgumentNullException(nameof(getIncidentId));
        }
        CharacterSpeciesAggregateState restored = new()
        {
            NextTickAt = nextTickAt
        };
        CharacterId previousCharacterId = default;
        foreach (CharacterSpeciesRuntimeRecordSaveData source in data.characters)
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Character-species payload contains a null record.");
            }
            CharacterId characterId = new(source.characterInstanceId);
            CharacterSpeciesId speciesId = new(source.speciesDefinitionId);
            if (!characterId.IsValid
                || !speciesId.IsValid
                || !string.Equals(characterId.Value, source.characterInstanceId, StringComparison.Ordinal)
                || !string.Equals(speciesId.Value, source.speciesDefinitionId, StringComparison.Ordinal)
                || previousCharacterId.IsValid
                    && string.CompareOrdinal(previousCharacterId.Value, characterId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    "Character-species records require unique, canonical, sorted IDs.");
            }
            string incidentId = getIncidentId(speciesId);
            if (incidentId == null)
            {
                throw new InvalidOperationException(
                    $"Unknown authored character species '{speciesId.Value}'.");
            }
            if (!IsFiniteRange(source.charge, 0f, 100f)
                || !IsFiniteRange(source.integrity, 0f, 100f)
                || !IsFiniteRange(source.wearWorkRemainder, 0f, 100f)
                || source.completedWorkIndex < 0
                || !IsFiniteRange(source.rechargeProgressWork, 0f, 100f)
                || source.rechargeWorkerId == null
                || source.rechargeFacilityId == null
                || source.rechargeMaterialStackId == null
                || source.rechargeOperationId == null
                || source.rechargePhysicalEvidence == null
                || source.rechargeEpisodeRevision < 0L
                || source.golemEpisodeRevision < 0L
                || !IsFiniteRange(source.rechargeChargeBefore, 0f, 100f)
                || !IsFiniteRange(source.rechargeRequestedCharge, 0f, 100f)
                || !IsValidRechargeOrder(source, characterId, speciesId)
                || !IsValidWorkContributions(source.appliedWorkContributions)
                || !IsFiniteRange(source.nextIncidentAt, 0f, float.MaxValue)
                || source.incidentCount < 0
                || source.lastIncidentId == null
                || source.lastIncidentId.Length > 0
                    && !string.Equals(source.lastIncidentId, incidentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Character-species record '{characterId.Value}' contains invalid state.");
            }
            CharacterSpeciesRuntimeState state = new()
            {
                CharacterId = characterId,
                SpeciesId = speciesId,
                Charge = source.charge,
                Integrity = source.integrity,
                NextIncidentAt = source.nextIncidentAt,
                LastIncidentId = source.lastIncidentId,
                IncidentCount = source.incidentCount,
                WearWorkRemainder = source.wearWorkRemainder,
                CompletedWorkIndex = source.completedWorkIndex,
                RechargeWorkerId = source.rechargeWorkerId,
                RechargeFacilityId = source.rechargeFacilityId,
                RechargeMaterialStackId = source.rechargeMaterialStackId,
                RechargeProgressWork = source.rechargeProgressWork,
                RechargeOperationId = source.rechargeOperationId,
                RechargeEpisodeRevision = source.rechargeEpisodeRevision,
                RechargeMaterialCommitted = source.rechargeMaterialCommitted,
                RechargeChargeApplied = source.rechargeChargeApplied,
                RechargeObservationCompleted = source.rechargeObservationCompleted,
                RechargePhysicalEvidence = source.rechargePhysicalEvidence,
                RechargeChargeBefore = source.rechargeChargeBefore,
                RechargeRequestedCharge = source.rechargeRequestedCharge,
                GolemEpisodeRevision = source.golemEpisodeRevision,
                DischargeEpisodeActive = source.dischargeEpisodeActive,
                AppliedWorkContributions = source.appliedWorkContributions
                    .Select(value => new CharacterSpeciesWorkContributionState
                    {
                        SourceOperationId = value.sourceOperationId,
                        WorkTypeId = value.workTypeId,
                        CompletedWork = value.completedWork
                    })
                    .ToList()
            };
            if (!restored.Characters.TryAdd(characterId, state))
            {
                throw new InvalidOperationException(
                    $"Duplicate character-species record '{characterId.Value}'.");
            }
            previousCharacterId = characterId;
        }

        var pendingCharacters = new HashSet<string>(StringComparer.Ordinal);
        var pendingKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in data.pendingObservations)
        {
            if (row == null) throw new InvalidOperationException("species-observation-null");
            row.Validate();
            if (!pendingCharacters.Add(row.CharacterId) || !pendingKeys.Add(row.publicationKey)
                || !restored.Characters.TryGetValue(new CharacterId(row.CharacterId), out var state)
                || !row.rollbackBlocked && (row.actionType == 0 && row.golem.kind == "recharge"
                    ? state.GolemEpisodeRevision < row.Revision
                    : (row.actionType == 0 ? state.GolemEpisodeRevision : state.IncidentCount) != row.Revision)
                || row.actionType == 0 && state.SpeciesId.Value != row.golem.speciesId
                || row.actionType == 1 && state.SpeciesId.Value != row.incident.speciesId
                || row.actionType == 2 && state.SpeciesId.Value != row.harpy.speciesId)
                throw new InvalidOperationException("species-observation-owner-mismatch");
            if (row.actionType == 0 && row.golem.kind == "recharge" && state.RechargeOperationId.Length > 0
                && (state.RechargeOperationId != row.OperationId || state.RechargeEpisodeRevision != row.Revision || !state.RechargeMaterialCommitted
                    || !state.RechargeChargeApplied || state.RechargePhysicalEvidence != row.golem.physicalEvidence))
                throw new InvalidOperationException("species-recharge-observation-mismatch");
            restored.PendingObservations.Add(row.Clone());
        }
        foreach (var state in restored.Characters.Values)
            if (state.RechargeObservationCompleted && !restored.PendingObservations.Any(row =>
                row.actionType == 0 && row.golem.kind == "recharge"
                && row.CharacterId == state.CharacterId.Value && row.OperationId == state.RechargeOperationId))
                throw new InvalidOperationException("species-recharge-completion-fact-missing");
        return new CharacterSpeciesRestoreCandidate(restored);
    }

    private static bool IsFiniteRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static bool IsValidRechargeOrder(
        CharacterSpeciesRuntimeRecordSaveData source,
        CharacterId characterId,
        CharacterSpeciesId speciesId)
    {
        bool hasAny = source.rechargeProgressWork > 0f
            || source.rechargeWorkerId.Length > 0
            || source.rechargeFacilityId.Length > 0
            || source.rechargeMaterialStackId.Length > 0
            || source.rechargeOperationId.Length > 0
            || source.rechargeEpisodeRevision > 0L
            || source.rechargeMaterialCommitted
            || source.rechargeChargeApplied
            || source.rechargeObservationCompleted
            || source.rechargePhysicalEvidence.Length > 0
            || source.rechargeChargeBefore > 0f
            || source.rechargeRequestedCharge > 0f;
        if (!hasAny)
            return true;
        return speciesId.Equals(new CharacterSpeciesId("Golem"))
            && source.rechargeProgressWork > 0f
            && source.rechargeProgressWork <= 100f
            && string.Equals(
                source.rechargeWorkerId,
                characterId.Value,
                StringComparison.Ordinal)
            && new BuildingInstanceId(source.rechargeFacilityId).IsValid
            && new ItemStackId(source.rechargeMaterialStackId).IsValid
            && DomainFactValueSyntax.IsIdentity(source.rechargeOperationId)
            && source.rechargeEpisodeRevision > 0L
            && source.rechargeEpisodeRevision <= source.golemEpisodeRevision
            && source.rechargeRequestedCharge > 0f
            && (!source.rechargeMaterialCommitted
                || source.rechargeProgressWork >= 100f
                    && source.rechargePhysicalEvidence.Length > 0)
            && (!source.rechargeChargeApplied || source.rechargeMaterialCommitted)
            && (!source.rechargeObservationCompleted || source.rechargeChargeApplied);
    }

    private static bool IsValidWorkContributions(
        IReadOnlyList<CharacterSpeciesWorkContributionSaveData> contributions)
    {
        if (contributions == null)
            return false;
        string previous = string.Empty;
        foreach (CharacterSpeciesWorkContributionSaveData value in contributions)
        {
            if (value == null
                || !DomainFactValueSyntax.IsIdentity(value.sourceOperationId)
                || string.IsNullOrWhiteSpace(value.workTypeId)
                || !IsFiniteRange(value.completedWork, float.Epsilon, float.MaxValue)
                || previous.Length > 0
                    && string.CompareOrdinal(previous, value.sourceOperationId) >= 0)
                return false;
            previous = value.sourceOperationId;
        }
        return true;
    }
}

public sealed class SpeciesIncidentHandlerRegistry :
    ISpeciesIncidentHandlerRegistry
{
    private readonly Dictionary<string, ISpeciesIncidentHandler> handlers;

    public SpeciesIncidentHandlerRegistry(
        IWorldItemStackRuntime items,
        IPhysicalItemRelocationService relocations,
        IWorldFilthQuery filth,
        IWorldWaterContaminationCommand water,
        ICharacterAiWorldRegistry world)
    {
        ISpeciesIncidentHandler[] values =
        {
            new SlimeContaminationHandler(filth, water),
            new BeastkinCommotionHandler(world),
            new DemonContractCurseHandler(world),
            new MyconidSporeBloomHandler(filth),
            new HarpyGaleCommotionHandler(items, relocations),
            new GolemCoreOverloadHandler(world)
        };
        handlers = values.ToDictionary(
            value => value.IncidentId,
            StringComparer.Ordinal);
    }

    public bool TryExecute(
        SpeciesIncidentContext context,
        out string summary)
    {
        summary = string.Empty;
        string incidentId = context.Species?.IncidentId ?? string.Empty;
        return handlers.TryGetValue(
                incidentId,
                out ISpeciesIncidentHandler handler)
            && handler.Execute(context, out summary);
    }
}

public sealed class CharacterSpeciesRuntime :
    ICharacterSpeciesQuery,
    ICharacterSpeciesCommand,
    ICharacterSpeciesContributionCommand,
    ICharacterSpeciesRechargeService,
    ICharacterSpeciesPersistence,
    IDungeonSaveRestoreCompletedHook,
    ITickable
{
    private const float TickInterval = 1f;
    private const float IncidentCooldown = 300f;
    private const float IncidentMoodThreshold = 30f;

    private readonly ICharacterAiWorldRegistry world;
    private readonly ICharacterSpeciesCatalog speciesCatalog;
    private readonly ISpeciesIncidentHandlerRegistry incidents;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly CharacterPerformanceFormulaCatalog performanceFormulas;
    private readonly IRunSeedProvider runSeed;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;
    private readonly IWorldFilthQuery filth;
    private readonly IWorldWaterQuery water;
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private float nextObservationRetryAt;
    private bool publishingObservation;
    private readonly IGameCalendar calendar;
    private readonly IPreparedPhysicalItemRelocationService preparedRelocations;

    private CharacterSpeciesAggregateState aggregateState
    {
        get => aggregateRootStore.GetOrCreate(
            () => new CharacterSpeciesAggregateState());
        set => aggregateRootStore.Replace(value);
    }

    private Dictionary<CharacterId, CharacterSpeciesRuntimeState> states =>
        aggregateState.Characters;

#if UNITY_EDITOR
    public bool DebugRejectNextRechargeProjection { get; set; }
#endif

    public CharacterSpeciesRuntime(
        ICharacterAiWorldRegistry world,
        ICharacterSpeciesCatalog speciesCatalog,
        ISpeciesIncidentHandlerRegistry incidents,
        IGameClock clock,
        IGameEventBus events,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog anatomyProfiles,
        CharacterPerformanceFormulaCatalog performanceFormulas,
        IRunSeedProvider runSeed,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems,
        IWorldFilthQuery filth,
        IWorldWaterQuery water,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        IGameCalendar calendar,
        IPreparedPhysicalItemRelocationService preparedRelocations)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.incidents = incidents
            ?? throw new ArgumentNullException(nameof(incidents));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.performanceFormulas = performanceFormulas
            ?? throw new ArgumentNullException(nameof(performanceFormulas));
        this.runSeed = runSeed ?? throw new ArgumentNullException(nameof(runSeed));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.preparedRelocations = preparedRelocations
            ?? throw new ArgumentNullException(nameof(preparedRelocations));
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.Time < aggregateState.NextTickAt)
        {
            return;
        }

        float elapsed = aggregateState.NextTickAt <= 0f
            ? TickInterval
            : Mathf.Max(
                TickInterval,
                clock.Time - aggregateState.NextTickAt + TickInterval);
        aggregateState.NextTickAt = clock.Time + TickInterval;
        if (clock.Time >= nextObservationRetryAt && aggregateState.PendingObservations.Count > 0)
        {
            nextObservationRetryAt = clock.Time + 5f;
            TryResumeObservations();
        }
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            CharacterSpeciesId speciesId = new(actor.SpeciesTag);
            if (!speciesCatalog.TryGet(speciesId, out CharacterSpeciesSO species))
            {
                continue;
            }

            CharacterSpeciesRuntimeState state = GetOrCreate(actor, species);
            if (HasPendingObservation(state.CharacterId)) continue;
            TickPhysiology(actor, species, state, elapsed);
            TryTriggerIncident(actor, species, state);
        }
    }

    public bool TryGet(
        CharacterId characterId,
        out CharacterSpeciesRuntimeState state)
    {
        if (characterId.IsValid
            && states.TryGetValue(characterId, out CharacterSpeciesRuntimeState found))
        {
            state = found.Clone();
            return true;
        }

        state = null;
        return false;
    }

    private bool ApplyRechargeAmount(
        CharacterId characterId,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!characterId.IsValid
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesStateUnavailable,
                characterId.Value);
            return false;
        }
        if (!state.SpeciesId.Equals(new CharacterSpeciesId("Golem")))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesRechargeUnsupported,
                state.SpeciesId.Value);
            return false;
        }

        state.Charge = Mathf.Clamp(state.Charge + amount, 0f, 100f);
        return true;
    }

    public bool IsRechargeAvailable(
        CharacterActor actor,
        BuildableObject facility,
        out string reason)
    {
        reason = string.Empty;
        if (!TryResolveGolemRecharge(
                actor,
                facility,
                out CharacterSpeciesRuntimeState state,
                out BuildingGolemRechargeAbility ability,
                out reason))
            return false;
        if (state.RechargeProgressWork > 0f)
            return true;
        if (HasPendingObservation(state.CharacterId))
        {
            reason = "species-observation-pending";
            return false;
        }
        if (state.Charge > 35f)
        {
            reason = "charge-above-recharge-threshold";
            return false;
        }
        bool materialAvailable = stock.GetAllStacks().Any(value => value != null
            && value.AvailableQuantity >= ability.materialQuantity
            && !value.Forbidden
            && string.Equals(
                value.ItemId,
                ability.materialItemId,
                StringComparison.Ordinal));
        if (!materialAvailable)
            reason = "golem-recharge-material-missing";
        return materialAvailable;
    }

    public float GetRechargeUrgency(
        CharacterActor actor,
        BuildableObject facility)
    {
        if (!IsRechargeAvailable(actor, facility, out _)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !states.TryGetValue(id, out CharacterSpeciesRuntimeState state))
            return 0f;
        return state.RechargeProgressWork > 0f
            ? 95f
            : Mathf.Clamp(100f - state.Charge, 65f, 95f);
    }

    public bool TryBeginRecharge(
        CharacterActor actor,
        BuildableObject facility,
        out float completedWork,
        out DomainFailure failure)
    {
        completedWork = 0f;
        failure = DomainFailure.None;
        if (!IsRechargeAvailable(actor, facility, out string reason)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesRechargeUnsupported,
                reason);
            return false;
        }
        if (state.RechargeProgressWork > 0f)
        {
            completedWork = state.RechargeProgressWork;
            return true;
        }
        BuildingGolemRechargeAbility ability = facility.BuildingData
            .GetAbility<BuildingGolemRechargeAbility>();
        WorldItemStackSnapshot material = stock.GetAllStacks()
            .Where(value => value != null
                && value.AvailableQuantity >= ability.materialQuantity
                && !value.Forbidden
                && string.Equals(
                    value.ItemId,
                    ability.materialItemId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        long episodeRevision = checked(state.GolemEpisodeRevision + 1L);
        string owner = $"golem-recharge:{characterId.Value}:{episodeRevision}";
        if (material == null
            || !reservations.TryReserveQuantities(
                new[]
                {
                    new ReservedItemConsumption(
                        material.StackId,
                        ability.materialQuantity)
                },
                owner,
                ItemReservationPurpose.FacilityBuffer,
                $"golem-recharge:{facility.PersistentInstanceId.Value}:material"))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        state.RechargeWorkerId = characterId.Value;
        state.RechargeFacilityId = facility.PersistentInstanceId.Value;
        state.RechargeMaterialStackId = material.StackId;
        state.RechargeProgressWork = 0.0001f;
        state.RechargeOperationId = owner;
        state.RechargeEpisodeRevision = episodeRevision;
        state.RechargeMaterialCommitted = false;
        state.RechargeChargeApplied = false;
        state.RechargeObservationCompleted = false;
        state.RechargePhysicalEvidence = string.Empty;
        state.RechargeChargeBefore = state.Charge;
        state.RechargeRequestedCharge = ability.restoredCharge;
        state.GolemEpisodeRevision = episodeRevision;
        completedWork = 0f;
        return true;
    }

    public bool TryApplyRechargeWork(
        CharacterActor actor,
        BuildableObject facility,
        float work,
        out bool completed,
        out DomainFailure failure)
    {
        completed = false;
        failure = DomainFailure.None;
        if (work <= 0f || float.IsNaN(work) || float.IsInfinity(work)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state)
            || !TryResolveGolemRecharge(actor, facility, out _, out BuildingGolemRechargeAbility ability, out _)
            || !string.Equals(state.RechargeWorkerId, characterId.Value, StringComparison.Ordinal)
            || !string.Equals(
                state.RechargeFacilityId,
                facility.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(state.RechargeMaterialStackId))
        {
            failure = new DomainFailure(FailureCode.CharacterSpeciesRechargeUnsupported);
            return false;
        }
        if (state.RechargeObservationCompleted)
        {
            aggregateState.PendingObservations.RemoveAll(value => value.OperationId == state.RechargeOperationId);
            ClearRechargeOrder(state);
            state.DischargeEpisodeActive = false;
            completed = true;
            return true;
        }
        var pending = aggregateState.PendingObservations.FirstOrDefault(value =>
            value.CharacterId == characterId.Value);
        if (pending != null)
        {
            if (pending.OperationId != state.RechargeOperationId)
            {
                failure = new DomainFailure(FailureCode.CharacterSpeciesStateUnavailable,
                    "species-observation-pending");
                return false;
            }
            TryResumeObservations();
            completed = state.RechargeObservationCompleted;
            if (completed)
            {
                aggregateState.PendingObservations.RemoveAll(value => value.OperationId == state.RechargeOperationId);
                ClearRechargeOrder(state);
                state.DischargeEpisodeActive = false;
            }
            if (!completed) failure = new DomainFailure(FailureCode.EnvironmentOutcomeUnavailable,
                "golem-recharge-observation-pending");
            return completed;
        }
        state.RechargeProgressWork = Mathf.Min(
            ability.requiredWork,
            state.RechargeProgressWork + work);
        if (state.RechargeProgressWork + .0001f < ability.requiredWork)
            return true;
        GolemUpkeepFactSaveData observed = CreateGolemFactData(actor, state,
            state.RechargeOperationId, state.RechargeEpisodeRevision, "recharge");
        observed.chargeBefore = state.RechargeChargeBefore;
        observed.chargeAfter = observed.chargeBefore;
        observed.completedWork = ability.requiredWork;
        if (!TryReserveGolemFact(observed,
                out DomainFactLease reserved, out failure)) return false;

        if (!state.RechargeMaterialCommitted)
        {
            if (atomicItems is not IAtomicItemConsumptionEvidenceService evidenceService)
            {
                facts.Cancel(reserved);
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    state.RechargeOperationId);
                return false;
            }
            ReservedItemConsumption[] cost =
            {
                new(state.RechargeMaterialStackId, ability.materialQuantity)
            };
            if (!evidenceService.TryConsumeReservedWithEvidence(
                    cost,
                    state.RechargeOperationId,
                    out AtomicItemConsumptionReceipt physicalReceipt,
                    out failure))
            {
                facts.Cancel(reserved);
                state.RechargeProgressWork = Mathf.Max(
                    0.0001f,
                    ability.requiredWork - 0.0001f);
                return false;
            }
            state.RechargeMaterialCommitted = true;
            state.RechargePhysicalEvidence = physicalReceipt.ToCanonicalEvidence();
        }

#if UNITY_EDITOR
        if (DebugRejectNextRechargeProjection)
        {
            DebugRejectNextRechargeProjection = false;
            facts.Cancel(reserved);
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesStateUnavailable,
                "golem-recharge-projection-rejected-after-consumption");
            return false;
        }
#endif

        if (!state.RechargeChargeApplied)
        {
            // Work may span physiology ticks; the applied recharge begins at
            // the actual current charge, not the earlier order-start value.
            state.RechargeChargeBefore = state.Charge;
            if (!ApplyRechargeAmount(
                    characterId,
                    state.RechargeRequestedCharge,
                    out failure))
            {
                facts.Cancel(reserved);
                return false;
            }
            state.RechargeChargeApplied = true;
        }

        observed.chargeBefore = state.RechargeChargeBefore;
        observed.chargeAfter = state.Charge;
        observed.healthAfter = actor.Stats?.CurrentHealth ?? 0f;
        observed.physicalEvidence = state.RechargePhysicalEvidence;
        if (!PublishAppliedGolemFact(reserved, observed, reversible: false, out failure))
        {
            return false;
        }
        ClearRechargeOrder(state);
        state.DischargeEpisodeActive = false;
        completed = true;
        return true;
    }

    public void CancelRecharge(CharacterId characterId)
    {
        if (!characterId.IsValid
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state)
            || string.IsNullOrWhiteSpace(state.RechargeMaterialStackId))
            return;
        if (state.RechargeMaterialCommitted)
            return;
        reservations.Release(
            state.RechargeMaterialStackId,
            state.RechargeOperationId);
        ClearRechargeOrder(state);
    }

    public bool RepairIntegrity(
        CharacterId characterId,
        float amount,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!characterId.IsValid
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesStateUnavailable,
                characterId.Value);
            return false;
        }
        if (!state.SpeciesId.Equals(new CharacterSpeciesId("Golem")))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesRepairUnsupported,
                state.SpeciesId.Value);
            return false;
        }

        state.Integrity = Mathf.Clamp(state.Integrity + amount, 0f, 100f);
        return true;
    }

    public bool RecordCompletedWork(
        CharacterId characterId,
        string workTypeId,
        float completedWork,
        out DomainFailure failure)
    {
        int nextLegacyIndex = states.TryGetValue(
                characterId,
                out CharacterSpeciesRuntimeState current)
            ? (current.AppliedWorkContributions?.Count ?? 0) + 1
            : 1;
        return RecordCompletedWork(
            characterId,
            $"legacy-work:{characterId.Value}:{nextLegacyIndex}",
            workTypeId,
            completedWork,
            out failure);
    }

    public bool RecordCompletedWork(
        CharacterId characterId,
        string sourceOperationId,
        string workTypeId,
        float completedWork,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        WorkTypeId typedWorkTypeId = new(workTypeId);
        string sourceId = sourceOperationId?.Trim() ?? string.Empty;
        CharacterActor actor = world.Characters.FirstOrDefault(value =>
            value != null
            && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
            && id.Equals(characterId));
        if (actor == null
            || !DomainFactValueSyntax.IsIdentity(sourceId)
            || !typedWorkTypeId.IsValid
            || completedWork <= 0f
            || float.IsNaN(completedWork)
            || float.IsInfinity(completedWork))
        {
            failure = new DomainFailure(FailureCode.CharacterSpeciesStateUnavailable);
            return false;
        }
        if (!speciesCatalog.TryGet(
                new CharacterSpeciesId(actor.SpeciesTag),
                out CharacterSpeciesSO species))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesStateUnavailable,
                actor.SpeciesTag);
            return false;
        }
        if (!string.Equals(species.speciesTag, "Golem", StringComparison.Ordinal))
            return true;

        CharacterSpeciesRuntimeState state = GetOrCreate(actor, species);
        CharacterSpeciesWorkContributionState existing =
            (state.AppliedWorkContributions
                ??= new List<CharacterSpeciesWorkContributionState>())
            .FirstOrDefault(value => string.Equals(
                value.SourceOperationId,
                sourceId,
                StringComparison.Ordinal));
        if (existing != null)
        {
            if (string.Equals(existing.WorkTypeId, typedWorkTypeId.Value,
                    StringComparison.Ordinal)
                && Mathf.Approximately(existing.CompletedWork, completedWork))
                return true;
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesStateUnavailable,
                "golem-work-contribution-identity-conflict");
            return false;
        }
        if (HasPendingObservation(characterId))
        {
            failure = new DomainFailure(FailureCode.CharacterSpeciesStateUnavailable,
                "species-observation-pending");
            return false;
        }

        float wearMultiplier = Mathf.Max(
            0f,
            species.needs?.integrityWearMultiplier ?? 1f);
        float combinedWork = state.WearWorkRemainder + completedWork;
        int wearCount = Mathf.FloorToInt((combinedWork + .0001f) / 100f);
        float nextRemainder = combinedWork - wearCount * 100f;
        List<string> wearNodes = new(wearCount);
        for (int index = 0; index < wearCount; index++)
        {
            if (!TrySelectWearNode(
                    actor,
                    typedWorkTypeId,
                    checked(state.CompletedWorkIndex + index + 1),
                    out string nodeId))
            {
                failure = new DomainFailure(
                    FailureCode.CharacterSpeciesRepairUnsupported,
                    typedWorkTypeId.Value);
                return false;
            }
            wearNodes.Add(nodeId);
        }

        if (wearCount == 0)
        {
            state.WearWorkRemainder = nextRemainder;
            state.AppliedWorkContributions.Add(new CharacterSpeciesWorkContributionState
            {
                SourceOperationId = sourceId,
                WorkTypeId = typedWorkTypeId.Value,
                CompletedWork = completedWork
            });
            return true;
        }

        long ownerRevision = checked(state.GolemEpisodeRevision + 1L);
        GolemUpkeepFactSaveData observed = CreateGolemFactData(actor, state,
            $"golem-wear:{characterId.Value}:{ownerRevision}", ownerRevision, "wear");
        observed.workSourceId = sourceId;
        observed.workTypeId = typedWorkTypeId.Value;
        observed.completedWork = completedWork;
        observed.wornNodes = wearNodes.ToList();
        if (!TryReserveGolemFact(observed,
                out DomainFactLease reserved, out failure)) return false;

        CharacterSpeciesRuntimeState before = state.Clone();
        CharacterBodyHealthMutationSnapshot bodyBefore =
            bodyHealthMutation.CaptureCombatMutation(actor);
        float burden = 2.5f * wearMultiplier;
        foreach (string nodeId in wearNodes)
        {
            if (!anatomy.TryAddNodeBurden(
                    actor,
                    nodeId,
                    burden,
                    0f,
                    0f,
                    out failure))
            {
                CopySpeciesState(before, state);
                bodyHealthMutation.RestoreCombatMutation(
                    actor,
                    bodyBefore,
                    "golem-wear-anatomy-rejected");
                facts.Cancel(reserved);
                return false;
            }
            state.Integrity = Mathf.Clamp(state.Integrity - burden, 0f, 100f);
        }
        state.WearWorkRemainder = nextRemainder;
        state.CompletedWorkIndex = checked(state.CompletedWorkIndex + wearCount);
        state.GolemEpisodeRevision = ownerRevision;
        state.AppliedWorkContributions.Add(new CharacterSpeciesWorkContributionState
        {
            SourceOperationId = sourceId,
            WorkTypeId = typedWorkTypeId.Value,
            CompletedWork = completedWork
        });
        observed.integrityAfter = state.Integrity;
        observed.healthAfter = actor.Stats?.CurrentHealth ?? 0f;
        if (!PublishAppliedGolemFact(reserved, observed, reversible: true, out failure))
        {
            RollbackObservation(observed.operationId, () =>
            {
                bodyHealthMutation.RestoreCombatMutation(actor, bodyBefore, "golem-wear-observation-rejected");
                CopySpeciesState(before, state);
            });
            return false;
        }
        return true;
    }

    public CharacterSpeciesRuntimeSaveData Capture()
    {
        return new CharacterSpeciesRuntimeSaveData
        {
            pendingObservations = aggregateState.PendingObservations
                .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                .Select(value => value.Clone()).ToList(),
            characters = states.Values
                .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
                .Select(value => new CharacterSpeciesRuntimeRecordSaveData
                {
                    characterInstanceId = value.CharacterId.Value,
                    speciesDefinitionId = value.SpeciesId.Value,
                    charge = value.Charge,
                    integrity = value.Integrity,
                    nextIncidentAt = value.NextIncidentAt,
                    lastIncidentId = value.LastIncidentId ?? string.Empty,
                    incidentCount = value.IncidentCount,
                    wearWorkRemainder = value.WearWorkRemainder,
                    completedWorkIndex = value.CompletedWorkIndex,
                    rechargeWorkerId = value.RechargeWorkerId ?? string.Empty,
                    rechargeFacilityId = value.RechargeFacilityId ?? string.Empty,
                    rechargeMaterialStackId = value.RechargeMaterialStackId ?? string.Empty,
                    rechargeProgressWork = value.RechargeProgressWork,
                    rechargeOperationId = value.RechargeOperationId ?? string.Empty,
                    rechargeEpisodeRevision = value.RechargeEpisodeRevision,
                    rechargeMaterialCommitted = value.RechargeMaterialCommitted,
                    rechargeChargeApplied = value.RechargeChargeApplied,
                    rechargeObservationCompleted = value.RechargeObservationCompleted,
                    rechargePhysicalEvidence = value.RechargePhysicalEvidence ?? string.Empty,
                    rechargeChargeBefore = value.RechargeChargeBefore,
                    rechargeRequestedCharge = value.RechargeRequestedCharge,
                    golemEpisodeRevision = value.GolemEpisodeRevision,
                    dischargeEpisodeActive = value.DischargeEpisodeActive,
                    appliedWorkContributions = (value.AppliedWorkContributions
                            ?? new List<CharacterSpeciesWorkContributionState>())
                        .OrderBy(entry => entry.SourceOperationId, StringComparer.Ordinal)
                        .Select(entry => new CharacterSpeciesWorkContributionSaveData
                        {
                            sourceOperationId = entry.SourceOperationId ?? string.Empty,
                            workTypeId = entry.WorkTypeId ?? string.Empty,
                            completedWork = entry.CompletedWork
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private bool TryResolveGolemRecharge(
        CharacterActor actor,
        BuildableObject facility,
        out CharacterSpeciesRuntimeState state,
        out BuildingGolemRechargeAbility ability,
        out string reason)
    {
        state = null;
        ability = null;
        reason = string.Empty;
        if (actor == null
            || facility?.BuildingData == null
            || facility.IsBuildingDestroyed
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !speciesCatalog.TryGet(
                new CharacterSpeciesId(actor.SpeciesTag),
                out CharacterSpeciesSO species)
            || !string.Equals(species.speciesTag, "Golem", StringComparison.Ordinal))
        {
            reason = "golem-recharge-subject-or-facility-invalid";
            return false;
        }
        ability = facility.BuildingData.GetAbility<BuildingGolemRechargeAbility>();
        if (ability == null)
        {
            reason = "facility-lacks-golem-recharge-capability";
            return false;
        }
        state = GetOrCreate(actor, species);
        return true;
    }

    private static void ClearRechargeOrder(CharacterSpeciesRuntimeState state)
    {
        state.RechargeWorkerId = string.Empty;
        state.RechargeFacilityId = string.Empty;
        state.RechargeMaterialStackId = string.Empty;
        state.RechargeProgressWork = 0f;
        state.RechargeOperationId = string.Empty;
        state.RechargeEpisodeRevision = 0L;
        state.RechargeMaterialCommitted = false;
        state.RechargeChargeApplied = false;
        state.RechargeObservationCompleted = false;
        state.RechargePhysicalEvidence = string.Empty;
        state.RechargeChargeBefore = 0f;
        state.RechargeRequestedCharge = 0f;
    }

    private GolemUpkeepFactSaveData CreateGolemFactData(
        CharacterActor actor, CharacterSpeciesRuntimeState state,
        string operationId, long revision, string kind)
    {
        if (!speciesCatalog.TryGet(state.SpeciesId, out var species))
            throw new InvalidOperationException("species-definition-unavailable");
        Vector2Int position = actor.GetNowXY();
        return new GolemUpkeepFactSaveData
        {
            operationId = operationId, revision = revision, kind = kind,
            characterId = state.CharacterId.Value,
            characterDisplayName = actor.Identity?.DisplayName?.Trim() ?? string.Empty,
            speciesId = state.SpeciesId.Value,
            speciesDisplayName = species.displayName?.Trim() ?? string.Empty,
            absoluteDay = calendar.Day, x = position.x, y = position.y,
            chargeBefore = state.Charge, chargeAfter = state.Charge,
            integrityBefore = state.Integrity, integrityAfter = state.Integrity,
            healthBefore = actor.Stats?.CurrentHealth ?? 0f,
            healthAfter = actor.Stats?.CurrentHealth ?? 0f
        };
    }

    private bool HasPendingObservation(CharacterId characterId) =>
        aggregateState.PendingObservations.Any(value => value.CharacterId == characterId.Value);

    private bool TryReserveGolemFact(GolemUpkeepFactSaveData proposed,
        out DomainFactLease lease, out DomainFailure failure)
    {
        GolemUpkeepFact fact;
        try { fact = new GolemUpkeepFact(proposed, proposed: true); }
        catch (ArgumentException exception)
        {
            lease = default;
            failure = new DomainFailure(FailureCode.CharacterSpeciesStateUnavailable, proposed.operationId, exception.Message);
            return false;
        }
        return TryReserveFact(fact, out lease, out failure);
    }

    private bool PublishAppliedGolemFact(DomainFactLease lease, GolemUpkeepFactSaveData observed,
        bool reversible, out DomainFailure failure)
    {
        GolemUpkeepFact actual;
        try { actual = new GolemUpkeepFact(observed); }
        catch (ArgumentException exception)
        {
            facts.Cancel(lease);
            failure = new DomainFailure(FailureCode.CharacterSpeciesStateUnavailable, observed.operationId, exception.Message);
            return false;
        }
        return PublishAppliedFact(lease, actual,
            new SpeciesActionObservationSaveData { golem = actual.Snapshot() }, reversible, out failure);
    }

    private void RollbackObservation(string operationId, Action rollback)
    {
        // The fault row survives a partial rollback and is never replayed as
        // an applied action. Only a fully successful rollback discards it.
        try { rollback(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            var row = aggregateState.PendingObservations.FirstOrDefault(value => value.OperationId == operationId);
            if (row != null) row.rollbackFailure = "species-rollback-failed:" + exception.GetType().Name;
            throw;
        }
        aggregateState.PendingObservations.RemoveAll(value => value.OperationId == operationId);
    }

    private bool TryReserveFact<TFact>(TFact proposed,
        out DomainFactLease lease, out DomainFailure failure)
    {
        lease = default;
        string detail;
        try
        {
            if (facts.TryReserve(proposed, out lease, out detail))
            {
                if (!lease.IsValid || !DomainFactValueSyntax.IsText(lease.PublicationKey, 1024, false))
                {
                    facts.Cancel(lease);
                    failure = new DomainFailure(FailureCode.EnvironmentOutcomeUnavailable, "species-observation-reservation-invalid");
                    return false;
                }
                failure = DomainFailure.None;
                return true;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { detail = exception.Message; }
        failure = new DomainFailure(FailureCode.EnvironmentOutcomeUnavailable,
            "species-observation-reserve", detail);
        return false;
    }

    private bool PublishAppliedFact<TFact>(DomainFactLease lease, TFact actual,
        SpeciesActionObservationSaveData observation, bool reversible, out DomainFailure failure)
    {
        observation.publicationKey = lease.PublicationKey;
        observation.Validate();
        if (HasPendingObservation(new CharacterId(observation.CharacterId)))
            throw new InvalidOperationException("species-observation-already-pending");
        // Persist the exact after-image before crossing any observer commit boundary.
        aggregateState.PendingObservations.Add(observation);
        DomainFactDelivery delivered;
        publishingObservation = true;
        try { delivered = facts.Publish(lease, actual); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { delivered = DomainFactDelivery.Reject("species-observation-prepare-exception:" + exception.Message); }
        finally
        {
            publishingObservation = false;
            try { facts.Cancel(lease); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { Debug.LogWarning("Species observation lease cleanup failed: " + exception.Message); }
        }
        failure = DomainFailure.None;
        if (!delivered.MayBeCommitted)
        {
            if (reversible)
            {
                observation.rollbackBlocked = true;
                observation.rollbackFailure = "species-observation-rejected-rollback-incomplete";
            }
            failure = new DomainFailure(FailureCode.EnvironmentOutcomeUnavailable,
                observation.OperationId, delivered.Detail);
            return false;
        }
        if (IsExactDelivery(delivered, observation.publicationKey))
            aggregateState.PendingObservations.Remove(observation);
        // InDoubt is an applied action, never permission to apply its effects again.
        return true;
    }

    private static bool IsExactDelivery(DomainFactDelivery value, string key) =>
        value.Completed && value.OperationKey == key
        && !string.IsNullOrWhiteSpace(value.PublicationId)
        && value.Fingerprint.Length == 64 && value.Fingerprint.All(Uri.IsHexDigit);

    public void ResumeObservations()
    {
        if (publishingObservation) throw new InvalidOperationException("species-observation-publication-in-progress");
        var failures = new List<string>();
        foreach (var row in aggregateState.PendingObservations.ToArray())
        {
            try
            {
                row.Validate();
                if (row.rollbackBlocked)
                    throw new InvalidOperationException("species-observation-rollback-blocked:" + row.OperationId + ":" + row.rollbackFailure);
                states.TryGetValue(new CharacterId(row.CharacterId), out var owner);
                if (row.actionType == 0 && row.golem.kind == "recharge"
                    && owner?.RechargeOperationId == row.OperationId && owner.RechargeObservationCompleted)
                    continue;
                DomainFactReplay replay = row.actionType == 0
                    ? DomainFactReplay.Create(row.publicationKey, new GolemUpkeepFact(row.golem))
                    : row.actionType == 1
                        ? DomainFactReplay.Create(row.publicationKey, new SpeciesIncidentFact(row.incident))
                        : DomainFactReplay.Create(row.publicationKey, new HarpyGaleFact(row.harpy));
                var deliveries = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { replay }, deliveries, out string failure)
                    || !IsExactDelivery(deliveries[0], row.publicationKey))
                    throw new InvalidOperationException("species-observation-recovery-pending:" + failure);
                if (row.actionType == 0 && row.golem.kind == "recharge"
                    && states.TryGetValue(new CharacterId(row.CharacterId), out var state)
                    && state.RechargeOperationId == row.OperationId)
                {
                    // The worker still needs the completion ACK. Clearing the
                    // order here would make its next real work call look invalid.
                    state.RechargeObservationCompleted = true;
                    continue;
                }
                aggregateState.PendingObservations.Remove(row);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { failures.Add(exception.Message); }
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(";", failures));
    }

    private void TryResumeObservations()
    {
        try { ResumeObservations(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { Debug.LogWarning("Species observation retained for retry: " + exception.Message); }
    }

    public void OnRestoreCompleted() => TryResumeObservations();

    private bool TrySelectWearNode(
        CharacterActor actor,
        WorkTypeId workTypeId,
        int completedWorkIndex,
        out string nodeId)
    {
        nodeId = string.Empty;
        CharacterPerformanceFormulaDefinitionSO formula = performanceFormulas
            .RequireWork(workTypeId, CharacterPerformanceResultChannel.Speed);
        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        if (!anatomyProfiles.TryGet(
                snapshot.ProfileId,
                out AnatomyProfileDefinition profile))
            return false;
        float maximumWeight = formula.CapacityInputs
            .Where(value => value.Weight > 0f)
            .Select(value => value.Weight)
            .DefaultIfEmpty(0f)
            .Max();
        AnatomyFunction functions = formula.CapacityInputs
            .Where(value => Mathf.Approximately(value.Weight, maximumWeight))
            .Aggregate(
                AnatomyFunction.None,
                (current, value) => current | ToAnatomyFunction(value.CapacityId));
        string[] candidates = profile.Nodes
            .Where(value => (value.ExpandedFunctions & functions) != 0)
            .Select(value => value.NodeId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            return false;
        uint hash = PersistentEntityId.GetStableHash32(
            $"{runSeed.RunSeed}:{actor.Identity.PersistentId}:"
            + $"{workTypeId.Value}:{completedWorkIndex}");
        nodeId = candidates[hash % (uint)candidates.Length];
        return true;
    }

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
        {
            CharacterFunctionalCapacityId.MentalMaintenance => AnatomyFunction.MentalMaintenance,
            CharacterFunctionalCapacityId.VisualDiscernment => AnatomyFunction.VisualDiscernment,
            CharacterFunctionalCapacityId.AuditorySensing => AnatomyFunction.AuditorySensing,
            CharacterFunctionalCapacityId.RespiratoryExchange => AnatomyFunction.RespiratoryExchange,
            CharacterFunctionalCapacityId.PowerCirculation => AnatomyFunction.PowerCirculation,
            CharacterFunctionalCapacityId.IntakeProcessing => AnatomyFunction.IntakeProcessing,
            CharacterFunctionalCapacityId.PurificationProcessing => AnatomyFunction.PurificationProcessing,
            CharacterFunctionalCapacityId.VitalityResponse => AnatomyFunction.VitalityResponse,
            CharacterFunctionalCapacityId.PhysicalPower => AnatomyFunction.PhysicalPower,
            CharacterFunctionalCapacityId.PrecisionManipulation => AnatomyFunction.PrecisionManipulation,
            CharacterFunctionalCapacityId.PhysicalMobility => AnatomyFunction.PhysicalMobility,
            CharacterFunctionalCapacityId.Communication => AnatomyFunction.Communication,
            CharacterFunctionalCapacityId.ArcaneConduction => AnatomyFunction.ArcaneConduction,
            CharacterFunctionalCapacityId.ImmuneDefense => AnatomyFunction.ImmuneDefense,
            _ => AnatomyFunction.None
        };

    public CharacterSpeciesRestoreCandidate BuildRestore(
        CharacterSpeciesRuntimeSaveData data)
    {
        return CharacterSpeciesStateCodec.BuildRestore(
            data,
            clock.Time + TickInterval,
            speciesId => speciesCatalog.TryGet(
                speciesId,
                out CharacterSpeciesSO species)
                    ? species.IncidentId
                    : null);
    }

    public void Restore(CharacterSpeciesRestoreCandidate candidate)
    {
        aggregateState = (candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State;
        nextObservationRetryAt = 0f;
    }

    private void TickPhysiology(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state,
        float elapsed)
    {
        if (species.needs?.UsesChargeInsteadOfFood != true)
        {
            return;
        }

        float chargeBefore = state.Charge;
        state.Charge = Mathf.Clamp(
            chargeBefore
            - 0.035f
            * Mathf.Max(0f, species.needs.chargeRateMultiplier)
            * elapsed,
            0f,
            100f);
        state.Integrity = actor.Stats != null
            ? Mathf.Min(
                state.Integrity,
                actor.Stats.CurrentHealth
                / Mathf.Max(1f, actor.Stats.MaxHealth)
                * 100f)
            : state.Integrity;
        if (state.Charge < 25f)
        {
            actor.ApplyMoodFactor(
                "species:golem-low-charge",
                "동력핵 충전 부족",
                -10f,
                5f,
                1);
        }

        if (state.Charge > 0f)
        {
            state.DischargeEpisodeActive = false;
        }
        else if (chargeBefore > 0f && !state.DischargeEpisodeActive)
        {
            ApplyGolemDischargeEpisode(actor, state, chargeBefore, elapsed);
        }
    }

#if UNITY_EDITOR
    public void DebugRunPhysiologyTick(CharacterActor actor, float elapsed)
    {
        if (actor == null
            || elapsed <= 0f
            || !speciesCatalog.TryGet(
                new CharacterSpeciesId(actor.SpeciesTag),
                out CharacterSpeciesSO species))
            throw new InvalidOperationException(
                "A valid authored species actor and positive elapsed time are required.");
        TickPhysiology(actor, species, GetOrCreate(actor, species), elapsed);
    }
#endif

    private void ApplyGolemDischargeEpisode(
        CharacterActor actor, CharacterSpeciesRuntimeState state, float chargeBefore, float elapsed)
    {
        long revision = checked(state.GolemEpisodeRevision + 1L);
        var observed = CreateGolemFactData(actor, state,
            $"golem-discharge:{state.CharacterId.Value}:{revision}", revision, "discharge");
        observed.chargeBefore = chargeBefore;
        observed.requestedDamage = Mathf.Max(0.1f,
            (actor.Stats?.MaxHealth ?? 100f) * 0.0025f * elapsed);
        if (!TryReserveGolemFact(observed,
                out DomainFactLease lease, out var reserveFailure))
        {
            state.Charge = chargeBefore;
            throw new InvalidOperationException("Golem discharge observation unavailable: " + reserveFailure.Code);
        }
        var before = state.Clone();
        before.Charge = chargeBefore;
        var bodyBefore = bodyHealthMutation.CaptureCombatMutation(actor);
        var bodyReceipt = bodyHealthMutation.ApplyPreparedAggregateDamage(actor,
            observed.requestedDamage, CharacterDeathCauseCode.ConstructFailure,
            "species:golem-discharge", allowDeath: false);
        if (!bodyReceipt.IsValid || bodyReceipt.AppliedDamage <= 0f)
        {
            CopySpeciesState(before, state);
            bodyHealthMutation.RestoreCombatMutation(actor, bodyBefore, "golem-discharge-body-rejected");
            facts.Cancel(lease);
            throw new InvalidOperationException("Golem discharge body mutation was rejected.");
        }
        state.GolemEpisodeRevision = revision;
        state.DischargeEpisodeActive = true;
        observed.healthBefore = bodyReceipt.PreviousHealth;
        observed.healthAfter = bodyReceipt.ResultingHealth;
        observed.appliedDamage = bodyReceipt.AppliedDamage;
        if (!PublishAppliedGolemFact(lease, observed, reversible: true, out var commitFailure))
        {
            RollbackObservation(observed.operationId, () =>
            {
                bodyHealthMutation.RestoreCombatMutation(actor, bodyBefore, "golem-discharge-observation-rejected");
                CopySpeciesState(before, state);
            });
            throw new InvalidOperationException("Golem discharge observation rejected: " + commitFailure.Code);
        }
        bodyHealthMutation.CompletePreparedAggregateDamage(actor, bodyReceipt);
    }

    private void TryTriggerIncident(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        if (HasPendingObservation(state.CharacterId)) return;
        string incidentId = species.IncidentId;
        if (string.IsNullOrWhiteSpace(incidentId)
            || incidentId is CharacterSpeciesIncidentIds.OrcRampage
                or CharacterSpeciesIncidentIds.VampireFear
                or CharacterSpeciesIncidentIds.KoboldPartsHoarding
            || state.NextIncidentAt > clock.Time)
        {
            return;
        }

        bool forcedGolemOverload =
            incidentId == CharacterSpeciesIncidentIds.GolemCoreOverload
            && state.Charge <= 5f;
        if (!forcedGolemOverload && actor.Mood.Value > IncidentMoodThreshold)
        {
            return;
        }

        int sample = CharacterGrowthRules.StableHash(
            state.CharacterId.Value
            + "|"
            + incidentId
            + "|"
            + state.IncidentCount);
        if (!forcedGolemOverload
            && (sample & 0x7fffffff) / (float)int.MaxValue > 0.25f)
        {
            state.NextIncidentAt = clock.Time + 30f;
            return;
        }

        if (incidentId == CharacterSpeciesIncidentIds.HarpyGaleCommotion)
        {
            TryTriggerHarpyGaleIncident(actor, species, state);
            return;
        }

        long revision = checked((long)state.IncidentCount + 1L);
        Vector2Int position = actor.GetNowXY();
        var observed = new SpeciesIncidentFactSaveData
        {
            operationId = $"species-incident:{state.CharacterId.Value}:{state.IncidentCount}",
            revision = revision, characterId = state.CharacterId.Value,
            characterDisplayName = actor.Identity?.DisplayName?.Trim() ?? string.Empty,
            speciesId = species.DefinitionId.Value,
            speciesDisplayName = species.displayName?.Trim() ?? string.Empty,
            incidentId = incidentId, absoluteDay = calendar.Day, x = position.x, y = position.y
        };
        if (!TryReserveFact(new SpeciesIncidentFact(observed, proposed: true),
                out DomainFactLease lease, out var reserveFailure))
            throw new InvalidOperationException("Species incident observation unavailable: " + reserveFailure.Code);
        var before = CaptureIncidentRollback(actor, state);
        SpeciesIncidentFact actual;
        try
        {
            if (!incidents.TryExecute(new SpeciesIncidentContext(actor, species, state), out string summary))
            {
                RestoreIncidentRollback(actor, state, before);
                facts.Cancel(lease);
                return;
            }
            observed.summary = summary;
            position = actor.GetNowXY();
            observed.x = position.x; observed.y = position.y;
            actual = new SpeciesIncidentFact(observed);
        }
        catch
        {
            RestoreIncidentRollback(actor, state, before);
            facts.Cancel(lease);
            throw;
        }
        state.LastIncidentId = incidentId;
        state.IncidentCount = checked(state.IncidentCount + 1);
        state.NextIncidentAt = clock.Time + IncidentCooldown;
        if (!PublishAppliedFact(lease, actual,
                new SpeciesActionObservationSaveData { actionType = 1, incident = actual.Snapshot() },
                reversible: true, out var commitFailure))
        {
            RollbackObservation(observed.operationId, () => RestoreIncidentRollback(actor, state, before));
            throw new InvalidOperationException("Species incident observation rejected: " + commitFailure.Code);
        }
        PublishIncidentNotification(state.CharacterId, species.DefinitionId,
            incidentId, position, observed.summary);
    }

    private void TryTriggerHarpyGaleIncident(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        Vector2Int origin = actor.GetNowXY();
        WorldItemStackSnapshot source = (stock.GetAllStacks()
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Quantity > 0
                && !stack.HasUniqueMetadata
                && Mathf.Abs(stack.Position.x - origin.x)
                    + Mathf.Abs(stack.Position.y - origin.y) <= 3)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (source == null)
            return;

        Vector2Int destination = source.Position
            + ((CharacterGrowthRules.StableHash(source.StackId) & 1) == 0
                ? Vector2Int.left
                : Vector2Int.up);
        string operationId =
            $"species-harpy-gale:{state.CharacterId.Value}:{state.IncidentCount}";
        if (!preparedRelocations.TryPrepare(
                source.StackId,
                1,
                destination,
                WorldItemStackState.Loose,
                string.Empty,
                operationId,
                "species-harpy-gale-relocation",
                out IPreparedPhysicalItemRelocation preparedPhysical,
                out _))
        {
            return;
        }

        HarpyGaleFact actual;
        try
        {
            var preview = preparedPhysical.Preview;
            var physical = preview.Receipt;
            actual = new HarpyGaleFact(new HarpyGaleFactSaveData
            {
                operationId = operationId, revision = checked((long)state.IncidentCount + 1L),
                characterId = state.CharacterId.Value,
                characterDisplayName = actor.Identity?.DisplayName?.Trim() ?? string.Empty,
                speciesId = species.DefinitionId.Value,
                speciesDisplayName = species.displayName?.Trim() ?? string.Empty,
                absoluteDay = calendar.Day, x = destination.x, y = destination.y,
                sourceStackId = physical.SourceStackId, destinationStackId = physical.DestinationStackId,
                itemId = physical.ItemId, itemInstanceId = preview.ItemInstanceId,
                itemDisplayName = preview.DisplayName, reasonCode = physical.ReasonCode,
                quantity = physical.Quantity, massGrams = physical.MassGrams,
                sourceX = physical.SourcePosition.x, sourceY = physical.SourcePosition.y,
                destinationX = physical.DestinationPosition.x, destinationY = physical.DestinationPosition.y,
                physicalRevision = preview.OwnerRevision
            });
        }
        catch
        {
            preparedPhysical.Cancel();
            throw;
        }
        if (!TryReserveFact(actual, out DomainFactLease lease, out var reserveFailure))
        {
            preparedPhysical.Cancel();
            throw new InvalidOperationException("Harpy incident observation unavailable: " + reserveFailure.Code);
        }
        CharacterSpeciesRuntimeState before = state.Clone();
        if (!preparedPhysical.TryApply(out IReversiblePhysicalItemRelocation relocation, out string applyFailure))
        {
            facts.Cancel(lease);
            preparedPhysical.Cancel();
            throw new InvalidOperationException("Harpy relocation apply failed: " + applyFailure);
        }
        state.LastIncidentId = CharacterSpeciesIncidentIds.HarpyGaleCommotion;
        state.IncidentCount = checked(state.IncidentCount + 1);
        state.NextIncidentAt = clock.Time + IncidentCooldown;
        if (!PublishAppliedFact(lease, actual,
                new SpeciesActionObservationSaveData { actionType = 2, harpy = actual.Snapshot() },
                reversible: true, out _))
        {
            RollbackObservation(operationId, () =>
            {
                if (!relocation.TryRollback(out string rollbackFailure))
                    throw new InvalidOperationException("Harpy relocation rollback failed: " + rollbackFailure);
                CopySpeciesState(before, state);
            });
            return;
        }

        // An ambiguous observation also retains the applied movement. Only the
        // frozen fact is replayed; the stack is never moved twice.
        if (!relocation.TryAcknowledge(out string acknowledgeFailure))
            Debug.LogWarning("Harpy relocation acknowledgement failed: " + acknowledgeFailure);
        PublishIncidentNotification(state.CharacterId, species.DefinitionId,
            CharacterSpeciesIncidentIds.HarpyGaleCommotion, destination,
            $"{preparedPhysical.Preview.DisplayName} 1개가 돌풍에 인접 칸으로 흩어졌습니다.");
    }

    private void PublishIncidentNotification(CharacterId characterId, CharacterSpeciesId speciesId,
        string incidentId, Vector2Int position, string summary)
    {
        try { events.Publish(new SpeciesIncidentTriggeredEvent(characterId, speciesId, incidentId, position, summary)); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { Debug.LogException(exception); }
    }

    private SpeciesIncidentRollbackSnapshot CaptureIncidentRollback(
        CharacterActor actor,
        CharacterSpeciesRuntimeState state)
    {
        var moods = new Dictionary<CharacterActor, CharacterMoodDeliveryTransactionSnapshot>();
        foreach (CharacterActor candidate in world.Characters)
        {
            if (candidate?.Stats != null)
                moods[candidate] = candidate.Stats.CaptureMoodDeliveryTransactionState();
        }
        var damaged = new Dictionary<BuildableObject, bool>();
        foreach (BuildableObject building in world.Buildings)
        {
            if (building != null)
                damaged[building] = building.IsDamaged;
        }
        return new SpeciesIncidentRollbackSnapshot(
            state.Clone(),
            filth.CaptureFilth(),
            filth.NextFilthSequence,
            water.CaptureWaterSources(),
            water.NextWaterSequence,
            moods,
            damaged,
            bodyHealthMutation.CaptureCombatMutation(actor));
    }

    private void RestoreIncidentRollback(
        CharacterActor actor,
        CharacterSpeciesRuntimeState state,
        SpeciesIncidentRollbackSnapshot snapshot)
    {
        filth.RestoreFilth(snapshot.Filth, snapshot.NextFilthSequence);
        water.RestoreWaterSources(snapshot.Water, snapshot.NextWaterSequence);
        foreach (KeyValuePair<CharacterActor, CharacterMoodDeliveryTransactionSnapshot> row
                 in snapshot.Moods)
            row.Key.Stats?.RestoreMoodDeliveryTransactionState(row.Value);
        foreach (KeyValuePair<BuildableObject, bool> row in snapshot.DamagedBuildings)
            row.Key.SetDamaged(row.Value);
        bodyHealthMutation.RestoreCombatMutation(
            actor,
            snapshot.Body,
            "species-incident-outcome-rollback");
        CopySpeciesState(snapshot.SpeciesState, state);
    }

    private static void CopySpeciesState(
        CharacterSpeciesRuntimeState source,
        CharacterSpeciesRuntimeState target)
    {
        target.CharacterId = source.CharacterId;
        target.SpeciesId = source.SpeciesId;
        target.Charge = source.Charge;
        target.Integrity = source.Integrity;
        target.NextIncidentAt = source.NextIncidentAt;
        target.LastIncidentId = source.LastIncidentId;
        target.IncidentCount = source.IncidentCount;
        target.WearWorkRemainder = source.WearWorkRemainder;
        target.CompletedWorkIndex = source.CompletedWorkIndex;
        target.RechargeWorkerId = source.RechargeWorkerId;
        target.RechargeFacilityId = source.RechargeFacilityId;
        target.RechargeMaterialStackId = source.RechargeMaterialStackId;
        target.RechargeProgressWork = source.RechargeProgressWork;
        target.RechargeOperationId = source.RechargeOperationId;
        target.RechargeEpisodeRevision = source.RechargeEpisodeRevision;
        target.RechargeMaterialCommitted = source.RechargeMaterialCommitted;
        target.RechargeChargeApplied = source.RechargeChargeApplied;
        target.RechargeObservationCompleted = source.RechargeObservationCompleted;
        target.RechargePhysicalEvidence = source.RechargePhysicalEvidence;
        target.RechargeChargeBefore = source.RechargeChargeBefore;
        target.RechargeRequestedCharge = source.RechargeRequestedCharge;
        target.GolemEpisodeRevision = source.GolemEpisodeRevision;
        target.DischargeEpisodeActive = source.DischargeEpisodeActive;
        target.AppliedWorkContributions = (source.AppliedWorkContributions
                ?? new List<CharacterSpeciesWorkContributionState>())
            .Select(value => value.Clone())
            .ToList();
    }

    private sealed class SpeciesIncidentRollbackSnapshot
    {
        public SpeciesIncidentRollbackSnapshot(
            CharacterSpeciesRuntimeState speciesState,
            List<WorldFilthSaveData> filth,
            int nextFilthSequence,
            List<WorldWaterSourceSaveData> water,
            int nextWaterSequence,
            Dictionary<CharacterActor, CharacterMoodDeliveryTransactionSnapshot> moods,
            Dictionary<BuildableObject, bool> damagedBuildings,
            CharacterBodyHealthMutationSnapshot body)
        {
            SpeciesState = speciesState;
            Filth = filth;
            NextFilthSequence = nextFilthSequence;
            Water = water;
            NextWaterSequence = nextWaterSequence;
            Moods = moods;
            DamagedBuildings = damagedBuildings;
            Body = body;
        }
        public CharacterSpeciesRuntimeState SpeciesState { get; }
        public List<WorldFilthSaveData> Filth { get; }
        public int NextFilthSequence { get; }
        public List<WorldWaterSourceSaveData> Water { get; }
        public int NextWaterSequence { get; }
        public Dictionary<CharacterActor, CharacterMoodDeliveryTransactionSnapshot> Moods { get; }
        public Dictionary<BuildableObject, bool> DamagedBuildings { get; }
        public CharacterBodyHealthMutationSnapshot Body { get; }
    }

    private CharacterSpeciesRuntimeState GetOrCreate(
        CharacterActor actor,
        CharacterSpeciesSO species)
    {
        CharacterId id = CharacterPersistentIdentity.Require(actor);

        if (states.TryGetValue(id, out CharacterSpeciesRuntimeState state))
        {
            state.SpeciesId = species.DefinitionId;
            return state;
        }

        state = new CharacterSpeciesRuntimeState
        {
            CharacterId = id,
            SpeciesId = species.DefinitionId,
            Charge = 100f,
            Integrity = 100f,
            NextIncidentAt = clock.Time + 30f
        };
        states.Add(id, state);
        return state;
    }

}

internal abstract class SpeciesIncidentHandlerBase : ISpeciesIncidentHandler
{
    public abstract string IncidentId { get; }
    public abstract bool Execute(
        SpeciesIncidentContext context,
        out string summary);

    protected static IEnumerable<CharacterActor> Nearby(
        ICharacterAiWorldRegistry world,
        CharacterActor source,
        int radius)
    {
        Vector2Int origin = source.GetNowXY();
        return world.Characters.Where(actor => actor != null
            && actor != source
            && !actor.IsDead
            && Mathf.Abs(actor.GetNowXY().x - origin.x)
                + Mathf.Abs(actor.GetNowXY().y - origin.y)
                <= radius);
    }
}

internal sealed class SlimeContaminationHandler : SpeciesIncidentHandlerBase
{
    private const string SlimeBlightDiseaseId = "disease:slime-blight";
    private readonly IWorldFilthQuery filth;
    private readonly IWorldWaterContaminationCommand water;

    public SlimeContaminationHandler(
        IWorldFilthQuery filth,
        IWorldWaterContaminationCommand water)
    {
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
    }

    public override string IncidentId => CharacterSpeciesIncidentIds.SlimeContamination;

    public override bool Execute(SpeciesIncidentContext context, out string summary)
    {
        Vector2Int origin = context.Actor.GetNowXY();
        string characterId = CharacterPersistentIdentity.Require(context.Actor).Value;
        filth.AddFilth(
            WorldFilthType.Stain,
            origin,
            20f,
            characterId,
            0.65f);
        bool contaminated = water.TryContaminateNearest(
            origin,
            4,
            SlimeBlightDiseaseId,
            WorldWaterQuality.Unsafe,
            out string sourceId);
        summary = contaminated
            ? $"점액 오염이 수원 '{sourceId}'에 번져 점액역병 위험이 생겼습니다."
            : "점액 오염이 바닥에 남았지만 반경 안에 오염될 수원은 없습니다.";
        return true;
    }
}

internal sealed class BeastkinCommotionHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public BeastkinCommotionHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.BeastkinCommotion;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        foreach (CharacterActor actor in Nearby(world, context.Actor, 3))
        {
            actor.ApplyMoodFactor(
                IncidentId,
                "수인 소동의 소음",
                -4f,
                90f,
                2);
        }

        summary = "무리 불만이 수인 소동으로 번져 주변의 휴식과 작업을 방해했습니다.";
        return true;
    }
}

internal sealed class DemonContractCurseHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public DemonContractCurseHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.DemonContractCurse;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        foreach (CharacterActor actor in Nearby(world, context.Actor, 4))
        {
            actor.ApplyMoodFactor(
                IncidentId,
                "계약 저주의 압박",
                -6f,
                120f,
                1);
        }

        summary = "불이행된 대가를 요구하는 계약 저주가 주변 인원에게 남았습니다.";
        return true;
    }
}

internal sealed class MyconidSporeBloomHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldFilthQuery filth;
    public MyconidSporeBloomHandler(IWorldFilthQuery filth) =>
        this.filth = filth;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.MyconidSporeBloom;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        string sourceId = context.Actor.Identity?.PersistentId ?? string.Empty;
        filth.AddFilth(
            WorldFilthType.Stain,
            context.Actor.GetNowXY(),
            18f,
            sourceId,
            0.35f);
        summary = "건조 스트레스로 포자 개화가 발생해 실제 오염이 남았습니다.";
        return true;
    }
}

internal sealed class HarpyGaleCommotionHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemRelocationService relocations;
    public HarpyGaleCommotionHandler(
        IWorldItemStackRuntime items,
        IPhysicalItemRelocationService relocations)
    {
        this.items = items;
        this.relocations = relocations;
    }
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.HarpyGaleCommotion;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        Vector2Int origin = context.Actor.GetNowXY();
        WorldItemStackSnapshot source = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Quantity > 0
                && !stack.HasUniqueMetadata
                && Mathf.Abs(stack.Position.x - origin.x)
                    + Mathf.Abs(stack.Position.y - origin.y)
                    <= 3)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Vector2Int destination = source?.Position
            + ((CharacterGrowthRules.StableHash(source?.StackId ?? string.Empty) & 1) == 0
                ? Vector2Int.left
                : Vector2Int.up) ?? default;
        if (source == null
            || !relocations.TryRelocateQuantity(
                source.StackId,
                1,
                destination,
                WorldItemStackState.Loose,
                string.Empty,
                $"species-harpy-gale:{context.State.CharacterId.Value}:{context.State.IncidentCount}",
                "species-harpy-gale-relocation",
                out _,
                out _))
        {
            summary = "돌풍이 불었지만 흩어질 loose stack이 없었습니다.";
            return true;
        }

        summary = $"{source.DisplayName} 1개가 돌풍에 인접 칸으로 흩어졌습니다.";
        return true;
    }
}

internal sealed class GolemCoreOverloadHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public GolemCoreOverloadHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.GolemCoreOverload;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        context.Actor.Stats?.ApplyNonLethalDamage(
            context.Actor.Stats.MaxHealth * 0.08f,
            "핵 과부하");
        Vector2Int origin = context.Actor.GetNowXY();
        foreach (BuildableObject building in world.Buildings.Where(
                     building => building != null
                         && Mathf.Abs(building.centerPos.x - origin.x)
                             + Mathf.Abs(building.centerPos.y - origin.y)
                             <= 1))
        {
            building.SetDamaged(true);
        }

        summary = "방전된 동력핵이 과부하되어 골렘과 인접 시설이 실제 피해를 입었습니다.";
        return true;
    }
}
