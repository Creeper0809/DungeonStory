using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
            || data.characters == null)
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
                || !IsValidRechargeOrder(source, characterId, speciesId)
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
                RechargeProgressWork = source.rechargeProgressWork
            };
            if (!restored.Characters.TryAdd(characterId, state))
            {
                throw new InvalidOperationException(
                    $"Duplicate character-species record '{characterId.Value}'.");
            }
            previousCharacterId = characterId;
        }

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
            || source.rechargeMaterialStackId.Length > 0;
        if (!hasAny)
            return true;
        return speciesId.Equals(new CharacterSpeciesId("Golem"))
            && source.rechargeProgressWork > 0f
            && source.rechargeProgressWork < 100f
            && string.Equals(
                source.rechargeWorkerId,
                characterId.Value,
                StringComparison.Ordinal)
            && new BuildingInstanceId(source.rechargeFacilityId).IsValid
            && new ItemStackId(source.rechargeMaterialStackId).IsValid;
    }
}

public sealed class SpeciesIncidentHandlerRegistry :
    ISpeciesIncidentHandlerRegistry
{
    private readonly Dictionary<string, ISpeciesIncidentHandler> handlers;

    public SpeciesIncidentHandlerRegistry(
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        IWorldWaterContaminationCommand water,
        ICharacterAiWorldRegistry world)
    {
        ISpeciesIncidentHandler[] values =
        {
            new SlimeContaminationHandler(filth, water),
            new BeastkinCommotionHandler(world),
            new DemonContractCurseHandler(world),
            new KoboldPartsHoardingHandler(items),
            new MyconidSporeBloomHandler(filth),
            new HarpyGaleCommotionHandler(items),
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
    ICharacterSpeciesRechargeService,
    ICharacterSpeciesPersistence,
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

    private CharacterSpeciesAggregateState aggregateState
    {
        get => aggregateRootStore.GetOrCreate(
            () => new CharacterSpeciesAggregateState());
        set => aggregateRootStore.Replace(value);
    }

    private Dictionary<CharacterId, CharacterSpeciesRuntimeState> states =>
        aggregateState.Characters;

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
        IAtomicItemConsumptionService atomicItems)
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
        string owner = RechargeReservationOwner(characterId);
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
        state.RechargeProgressWork = Mathf.Min(
            ability.requiredWork,
            state.RechargeProgressWork + work);
        if (state.RechargeProgressWork + .0001f < ability.requiredWork)
            return true;
        ReservedItemConsumption[] cost =
        {
            new(state.RechargeMaterialStackId, ability.materialQuantity)
        };
        if (!atomicItems.TryConsumeReserved(
                cost,
                RechargeReservationOwner(characterId),
                out failure))
        {
            state.RechargeProgressWork = Mathf.Max(
                0.0001f,
                ability.requiredWork - 0.0001f);
            return false;
        }
        if (!ApplyRechargeAmount(
                characterId,
                ability.restoredCharge,
                out failure))
            throw new InvalidOperationException(
                "Committed Golem recharge material but charge projection failed.");
        ClearRechargeOrder(state);
        completed = true;
        return true;
    }

    public void CancelRecharge(CharacterId characterId)
    {
        if (!characterId.IsValid
            || !states.TryGetValue(characterId, out CharacterSpeciesRuntimeState state)
            || string.IsNullOrWhiteSpace(state.RechargeMaterialStackId))
            return;
        reservations.Release(
            state.RechargeMaterialStackId,
            RechargeReservationOwner(characterId));
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
        failure = DomainFailure.None;
        WorkTypeId typedWorkTypeId = new(workTypeId);
        CharacterActor actor = world.Characters.FirstOrDefault(value =>
            value != null
            && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
            && id.Equals(characterId));
        if (actor == null
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
        float wearMultiplier = Mathf.Max(
            0f,
            species.needs?.integrityWearMultiplier ?? 1f);
        state.WearWorkRemainder += completedWork;
        while (state.WearWorkRemainder + .0001f >= 100f)
        {
            state.WearWorkRemainder -= 100f;
            state.CompletedWorkIndex++;
            float burden = 2.5f * wearMultiplier;
            if (!TrySelectWearNode(
                    actor,
                    typedWorkTypeId,
                    state.CompletedWorkIndex,
                    out string nodeId))
            {
                failure = new DomainFailure(
                    FailureCode.CharacterSpeciesRepairUnsupported,
                    typedWorkTypeId.Value);
                return false;
            }
            if (!anatomy.TryAddNodeBurden(
                    actor,
                    nodeId,
                    burden,
                    0f,
                    0f,
                    out failure))
                return false;
            state.Integrity = Mathf.Clamp(state.Integrity - burden, 0f, 100f);
        }
        return true;
    }

    public CharacterSpeciesRuntimeSaveData Capture()
    {
        return new CharacterSpeciesRuntimeSaveData
        {
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
                    rechargeProgressWork = value.RechargeProgressWork
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

    private static string RechargeReservationOwner(CharacterId characterId) =>
        $"golem-recharge:{characterId.Value}";

    private static void ClearRechargeOrder(CharacterSpeciesRuntimeState state)
    {
        state.RechargeWorkerId = string.Empty;
        state.RechargeFacilityId = string.Empty;
        state.RechargeMaterialStackId = string.Empty;
        state.RechargeProgressWork = 0f;
    }

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

        state.Charge = Mathf.Clamp(
            state.Charge
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

        if (state.Charge <= 0f)
        {
            actor.Stats?.ApplyNonLethalDamage(
                Mathf.Max(0.1f, actor.Stats.MaxHealth * 0.0025f * elapsed),
                "동력핵 방전");
        }
    }

    private void TryTriggerIncident(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        string incidentId = species.IncidentId;
        if (string.IsNullOrWhiteSpace(incidentId)
            || incidentId is CharacterSpeciesIncidentIds.OrcRampage
                or CharacterSpeciesIncidentIds.VampireFear
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

        SpeciesIncidentContext context =
            new SpeciesIncidentContext(actor, species, state);
        if (!incidents.TryExecute(context, out string summary))
        {
            return;
        }

        state.LastIncidentId = incidentId;
        state.IncidentCount++;
        state.NextIncidentAt = clock.Time + IncidentCooldown;
        actor.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Failed,
            summary,
            null,
            actionId: incidentId,
            reasonCode: "species-discontent",
            bubbleEligible: true));
        events.Publish(new SpeciesIncidentTriggeredEvent(
            state.CharacterId,
            species.DefinitionId,
            incidentId,
            actor.GetNowXY(),
            summary));
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

internal sealed class KoboldPartsHoardingHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;
    public KoboldPartsHoardingHandler(IWorldItemStackRuntime items) =>
        this.items = items;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.KoboldPartsHoarding;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        Vector2Int origin = context.Actor.GetNowXY();
        WorldItemStackSnapshot source = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.HasUniqueMetadata
                && stack.StockCategory == StockCategory.General
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored)
            .OrderBy(stack =>
                Mathf.Abs(stack.Position.x - origin.x)
                + Mathf.Abs(stack.Position.y - origin.y))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (source == null
            || !items.TryConsumeStackQuantity(
                source.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            summary = "숨길 부품을 찾지 못해 코볼트의 사재기가 미수에 그쳤습니다.";
            return true;
        }

        Vector2Int hidePosition = origin + Vector2Int.right;
        items.SpawnItemAt(
            consumed.ItemId,
            1,
            hidePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        WorldItemStackSnapshot hidden = items.GetStacksAt(hidePosition)
            .Where(stack => stack.ItemId == consumed.ItemId)
            .OrderByDescending(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (hidden != null)
        {
            items.SetForbidden(hidden.StackId, true);
        }

        summary = $"{consumed.DisplayName} 1개를 인접 칸에 숨기고 금지 표시했습니다.";
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
    public HarpyGaleCommotionHandler(IWorldItemStackRuntime items) =>
        this.items = items;
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
        if (source == null
            || !items.TryConsumeStackQuantity(
                source.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            summary = "돌풍이 불었지만 흩어질 loose stack이 없었습니다.";
            return true;
        }

        Vector2Int destination = source.Position
            + ((CharacterGrowthRules.StableHash(source.StackId) & 1) == 0
                ? Vector2Int.left
                : Vector2Int.up);
        items.SpawnItemAt(
            consumed.ItemId,
            1,
            destination,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        summary = $"{consumed.DisplayName} 1개가 돌풍에 인접 칸으로 흩어졌습니다.";
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
