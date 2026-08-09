using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class ReproductionPlanRequest
{
    public string ActionId { get; set; } = string.Empty;
    public string FirstParentId { get; set; } = string.Empty;
    public string SecondParentId { get; set; } = string.Empty;
    public string CarrierId { get; set; } = string.Empty;
    public string PhenotypeSpeciesId { get; set; } = string.Empty;
    public int AbsoluteDay { get; set; }
    public bool UseFertilityTreatment { get; set; }
}

public interface IReproductionCommand
{
    bool TryPlan(
        ReproductionPlanRequest request,
        out string processId,
        out DomainFailure failure);

    bool TryStart(
        string processId,
        out DomainFailure failure);
    bool TryStart(
        string processId,
        bool useFertilityTreatment,
        out DomainFailure failure);
}

/// <summary>
/// Creates and starts persistent reproduction processes. Planning validates the
/// people, lineage and operational facility without changing inventory. Start
/// revalidates the facility, reserves exact physical inputs, applies the change
/// to a detached reproduction aggregate, consumes the batch atomically, and
/// publishes only the prepared aggregate.
/// </summary>
public sealed class ReproductionCommandRuntime : IReproductionCommand
{
    private const string CrossLineageMediumId =
        "medical:cross-lineage-medium";
    private const string GolemCoreCaseId = "component:golem-core-case";
    private const string FertilityTreatmentId = "medical:fertility-treatment";

    private readonly IReproductionService reproduction;
    private readonly IReproductionPersistence persistence;
    private readonly IReproductionDefinitionCatalog definitions;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly IKinshipQuery kinship;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;
    private readonly IWorldItemStackRuntime items;

    public ReproductionCommandRuntime(
        IReproductionService reproduction,
        IReproductionPersistence persistence,
        IReproductionDefinitionCatalog definitions,
        ICharacterLifeQuery life,
        ICharacterWorldQuery world,
        ICharacterNarrativeQuery narratives,
        IKinshipQuery kinship,
        IFacilityCapabilityQuery facilities,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems,
        IWorldItemStackRuntime items)
    {
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.definitions = definitions
            ?? throw new ArgumentNullException(nameof(definitions));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool TryPlan(
        ReproductionPlanRequest request,
        out string processId,
        out DomainFailure failure)
    {
        processId = string.Empty;
        failure = DomainFailure.None;
        if (request == null
            || string.IsNullOrWhiteSpace(request.ActionId)
            || request.AbsoluteDay < 1
            || reproduction.FamilyPlanningPolicy == FamilyPlanningPolicy.Off)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        CharacterId firstId = (CharacterId)request.FirstParentId;
        CharacterId secondId = (CharacterId)request.SecondParentId;
        if (!TryResolveAdult(firstId, out CharacterLifeRecord firstLife,
                out CharacterActor firstActor))
        {
            failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }

        CharacterSpeciesId requestedPhenotype =
            (CharacterSpeciesId)request.PhenotypeSpeciesId;
        ReproductionDefinition firstDefinition =
            definitions.RequireReproduction(firstLife.PhenotypeSpeciesId);
        bool golemAssembly = requestedPhenotype.IsValid
            && definitions.RequireReproduction(requestedPhenotype).Mode
                == ReproductionMode.GolemAssembly;

        CharacterLifeRecord secondLife = null;
        CharacterActor secondActor = null;
        ReproductionDefinition secondDefinition = default;
        if (!golemAssembly)
        {
            if (!TryResolveAdult(secondId, out secondLife, out secondActor)
                || firstId.Equals(secondId))
            {
                failure = new DomainFailure(
                    FailureCode.CharacterMedicalPatientUnavailable);
                return false;
            }
            secondDefinition = definitions.RequireReproduction(
                secondLife.PhenotypeSpeciesId);
        }

        bool crossLineage = !golemAssembly
            && firstDefinition.Mode != secondDefinition.Mode;
        ReproductionFailureCode pairFailure = golemAssembly
            ? ReproductionFailureCode.None
            : ReproductionRules.ValidatePair(
                firstId,
                secondId,
                firstDefinition,
                secondDefinition,
                kinship,
                crossLineageIncubatorAvailable: crossLineage);
        if (pairFailure != ReproductionFailureCode.None)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        processId = $"reproduction:{request.ActionId.Trim()}";
        CharacterSpeciesId phenotype = requestedPhenotype.IsValid
            ? requestedPhenotype
            : ReproductionRules.SelectPhenotype(
                firstLife.PhenotypeSpeciesId,
                secondLife.PhenotypeSpeciesId,
                processId);
        ReproductionDefinition processDefinition =
            definitions.RequireReproduction(phenotype);
        if (golemAssembly !=
            (processDefinition.Mode == ReproductionMode.GolemAssembly))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (golemAssembly && request.UseFertilityTreatment)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        CharacterId carrierId = ResolveCarrier(
            request.CarrierId,
            processDefinition.Mode,
            firstId,
            firstActor,
            secondId,
            secondActor,
            golemAssembly);
        if (!carrierId.IsValid)
        {
            failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }

        string facilityDefinitionId = RequiredFacilityDefinitionId(
            processDefinition.Mode,
            crossLineage);
        BuildableObject facility = facilities.FindOperational(
                FacilityCapabilityKind.None,
                facilityDefinitionId)
            .FirstOrDefault();
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        CharacterRuntimeProfile firstProfile = firstActor.Identity?.Profile;
        CharacterRuntimeProfile secondProfile = secondActor?.Identity?.Profile;
        CharacterNarrativeSnapshot secondNarrative = null;
        if (!narratives.TryGet(firstId, out CharacterNarrativeSnapshot firstNarrative)
            || (!golemAssembly
                && !narratives.TryGet(secondId, out secondNarrative)))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        ReproductionRules.SelectInheritedTraits(
            TraitCandidates(firstNarrative),
            TraitCandidates(golemAssembly ? null : secondNarrative),
            processId,
            out IReadOnlyList<string> expressed,
            out IReadOnlyList<string> latent);
        IReadOnlyList<InnateAptitudeSaveData> aptitudes =
            BuildAptitudes(firstProfile, secondProfile, processId);
        ReproductionProcess process = new(
            processId,
            firstId,
            golemAssembly ? default : secondId,
            carrierId,
            phenotype,
            processDefinition,
            request.AbsoluteDay,
            crossLineage,
            facility.PersistentInstanceId.Value,
            expressed,
            latent,
            aptitudes,
            startActive: false,
            fertilityTreatmentUsed: request.UseFertilityTreatment);
        string plannedProcessId = processId;
        if (reproduction.Processes.Any(value => string.Equals(
                value.ProcessId,
                plannedProcessId,
                StringComparison.Ordinal))
            || !TryUseBreedingLedger(facility, out failure))
        {
            processId = string.Empty;
            return false;
        }
        try
        {
            reproduction.AddProcess(process);
            return true;
        }
        catch (InvalidOperationException)
        {
            processId = string.Empty;
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
    }

    private bool TryUseBreedingLedger(
        BuildableObject facility,
        out DomainFailure failure)
    {
        string destinationId = facility.PersistentInstanceId.Value;
        WorldItemStackSnapshot ledger = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.BreedingLedger,
                    StringComparison.Ordinal)
                && DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components) > 0f)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ledger == null)
        {
            bool pending = items.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.BreedingLedger,
                    StringComparison.Ordinal)
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            if (!pending)
            {
                items.TryRequestItemDelivery(
                    DurableToolItemRules.BreedingLedger,
                    1,
                    facility.centerPos,
                    destinationId,
                    out _,
                    out _);
            }
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                DurableToolItemRules.BreedingLedger);
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            ledger.ItemId,
            ledger.Components);
        if (!items.TrySetInstanceComponent(
                ledger.StackId,
                DurableToolItemRules.CreateDurability(ledger.ItemId, current - 1f)))
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryStart(
        string processId,
        out DomainFailure failure)
    {
        ReproductionProcess live = reproduction.Processes.FirstOrDefault(value =>
            string.Equals(
                value.ProcessId,
                processId?.Trim(),
                StringComparison.Ordinal));
        return TryStart(
            processId,
            live != null && live.FertilityTreatmentUsed,
            out failure);
    }

    public bool TryStart(
        string processId,
        bool useFertilityTreatment,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string id = processId?.Trim() ?? string.Empty;
        ReproductionProcess live = reproduction.Processes.FirstOrDefault(value =>
            string.Equals(value.ProcessId, id, StringComparison.Ordinal));
        if (live == null || live.Status != ReproductionProcessStatus.Planned)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (live.Mode == ReproductionMode.GolemAssembly
            && useFertilityTreatment)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        string requiredFacility = RequiredFacilityDefinitionId(
            live.Mode,
            live.CrossLineageIncubatorUsed);
        bool operational = facilities.FindOperational(
                FacilityCapabilityKind.None,
                requiredFacility)
            .Any(value => string.Equals(
                value.PersistentInstanceId.Value,
                live.SupportFacilityInstanceId,
                StringComparison.Ordinal));
        if (!operational)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        List<(string itemId, int quantity)> costs = new();
        if (live.CrossLineageIncubatorUsed)
            costs.Add((CrossLineageMediumId, 1));
        if (live.Mode == ReproductionMode.GolemAssembly)
            costs.Add((GolemCoreCaseId, 1));
        if (useFertilityTreatment)
            costs.Add((FertilityTreatmentId, 1));
        if (!TryReserveCosts(
                costs,
                $"reproduction-start:{id}",
                out IReadOnlyList<ReservedItemConsumption> reserved,
                out failure))
            return false;

        ReproductionWorldAggregate candidate;
        try
        {
            candidate = persistence.PrepareRestore(persistence.Capture());
            if (!candidate.TryGet(id, out ReproductionProcess planned))
                throw new InvalidOperationException();
            planned.SelectFertilityTreatment(useFertilityTreatment);
            planned.Start();
        }
        catch (InvalidOperationException)
        {
            Release(reserved, $"reproduction-start:{id}");
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        if (!atomicItems.TryConsumeReserved(
                reserved,
                $"reproduction-start:{id}",
                out failure))
        {
            Release(reserved, $"reproduction-start:{id}");
            return false;
        }
        persistence.PublishRestore(candidate);
        return true;
    }

    private bool TryResolveAdult(
        CharacterId id,
        out CharacterLifeRecord record,
        out CharacterActor actor)
    {
        record = null;
        actor = world.Characters.FirstOrDefault(value => value != null
            && !value.IsDead
            && CharacterPersistentIdentity.TryGet(value, out CharacterId candidate)
            && candidate.Equals(id));
        return actor != null
            && life.TryGet(id, out record)
            && record.LifeStage == CharacterLifeStage.Adult;
    }

    private static CharacterId ResolveCarrier(
        string requestedCarrierId,
        ReproductionMode mode,
        CharacterId firstId,
        CharacterActor first,
        CharacterId secondId,
        CharacterActor second,
        bool golemAssembly)
    {
        if (golemAssembly) return firstId;
        CharacterId requested = (CharacterId)requestedCarrierId;
        if (requested.IsValid
            && (requested.Equals(firstId) || requested.Equals(secondId)))
            return requested;
        ReproductiveRole desired = mode switch
        {
            ReproductionMode.Pregnancy => ReproductiveRole.Carrier,
            ReproductionMode.Egg => ReproductiveRole.Layer,
            _ => ReproductiveRole.None
        };
        if (desired == ReproductiveRole.None) return firstId;
        if (first.Identity?.Profile?.ReproductiveRole == desired) return firstId;
        if (second?.Identity?.Profile?.ReproductiveRole == desired) return secondId;
        return default;
    }

    private static IEnumerable<string> TraitCandidates(
        CharacterNarrativeSnapshot narrative) =>
        narrative == null
            ? Array.Empty<string>()
            : narrative.ExpressedHeritableTraitIds.Concat(
                narrative.LatentHeritableTraitIds);

    private static IReadOnlyList<InnateAptitudeSaveData> BuildAptitudes(
        CharacterRuntimeProfile first,
        CharacterRuntimeProfile second,
        string seed)
    {
        string[] skillIds = (first?.InnateAptitudes.Keys
                ?? Array.Empty<string>())
            .Concat(second?.InnateAptitudes.Keys ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return skillIds.Select(skillId => new InnateAptitudeSaveData
        {
            skillId = skillId,
            value = ReproductionRules.InheritAptitude(
                Value(first, skillId),
                Value(second, skillId),
                seed,
                skillId)
        }).ToArray();
    }

    private static int Value(CharacterRuntimeProfile profile, string skillId) =>
        profile != null
            && profile.InnateAptitudes.TryGetValue(skillId, out int value)
                ? value
                : 0;

    private static string RequiredFacilityDefinitionId(
        ReproductionMode mode,
        bool crossLineage) =>
        crossLineage
            ? "building:8881"
            : mode switch
            {
                ReproductionMode.Pregnancy => "building:8859",
                ReproductionMode.Egg => "building:8859",
                ReproductionMode.Spore => "building:8813",
                ReproductionMode.CoreDivision => "building:8881",
                ReproductionMode.GolemAssembly => "building:8847",
                _ => string.Empty
            };

    private bool TryReserveCosts(
        IReadOnlyList<(string itemId, int quantity)> costs,
        string owner,
        out IReadOnlyList<ReservedItemConsumption> selected,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        List<ReservedItemConsumption> result = new();
        foreach ((string itemId, int quantity) in costs)
        {
            int needed = quantity;
            foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                         .Where(value => value != null
                             && value.Quantity > 0
                             && !value.Forbidden
                             && !value.IsReserved
                             && string.Equals(
                                 value.ItemId,
                                 itemId,
                                 StringComparison.Ordinal))
                         .OrderBy(value => value.StackId, StringComparer.Ordinal))
            {
                int take = Math.Min(needed, stack.Quantity);
                result.Add(new ReservedItemConsumption(stack.StackId, take));
                needed -= take;
                if (needed == 0) break;
            }
            if (needed > 0)
            {
                selected = Array.Empty<ReservedItemConsumption>();
                failure = new DomainFailure(FailureCode.ProductionMaterialsMissing);
                return false;
            }
        }

        if (result.Count > 0
            && !reservations.TryReserve(
                result.Select(value => value.StackId),
                owner))
        {
            selected = Array.Empty<ReservedItemConsumption>();
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        selected = result;
        return true;
    }

    private void Release(
        IEnumerable<ReservedItemConsumption> costs,
        string owner)
    {
        foreach (ReservedItemConsumption cost in costs
                     ?? Array.Empty<ReservedItemConsumption>())
            reservations.Release(cost.StackId, owner);
    }
}

/// <summary>
/// The Allowed policy evaluates at most once every ten operating days and
/// creates one persistent planned proposal. It never starts or pays for the
/// process; that remains an explicit player action through TryStart.
/// </summary>
public sealed class ReproductionProposalAdapter : IStartable, IDisposable
{
    private readonly IReproductionService reproduction;
    private readonly IReproductionCommand commands;
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifeQuery life;
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public ReproductionProposalAdapter(
        IReproductionService reproduction,
        IReproductionCommand commands,
        ICharacterWorldQuery world,
        ICharacterLifeQuery life,
        IGameEventBus events)
    {
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => subscription ??=
        events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int absoluteDay = ended.day + 1;
        if (!ReproductionRules.ShouldEvaluateAllowedPolicy(
                reproduction.FamilyPlanningPolicy,
                absoluteDay - reproduction.LastAllowedPolicyEvaluationDay))
            return;

        reproduction.MarkAllowedPolicyEvaluation(absoluteDay);
        CharacterActor[] adults = world.Characters
            .Where(value => value != null
                && !value.IsDead
                && CharacterPersistentIdentity.TryGet(
                    value,
                    out CharacterId id)
                && life.TryGet(id, out CharacterLifeRecord record)
                && record.LifeStage == CharacterLifeStage.Adult
                && value.Identity?.Profile?.ReproductiveRole
                    != ReproductiveRole.None
                && value.Identity.Profile.ReproductiveRole
                    != ReproductiveRole.Assembler)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        for (int left = 0; left < adults.Length; left++)
        {
            for (int right = left + 1; right < adults.Length; right++)
            {
                string firstId = adults[left].Identity.PersistentId;
                string secondId = adults[right].Identity.PersistentId;
                if (commands.TryPlan(
                    new ReproductionPlanRequest
                    {
                        ActionId = $"allowed:{absoluteDay}:{firstId}:{secondId}",
                        FirstParentId = firstId,
                        SecondParentId = secondId,
                        AbsoluteDay = absoluteDay
                    },
                    out _,
                    out _))
                    return;
            }
        }
    }
}
