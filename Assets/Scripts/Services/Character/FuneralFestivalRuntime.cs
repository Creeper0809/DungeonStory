using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct FestivalCelebratedEvent
{
    public FestivalCelebratedEvent(
        string festivalId,
        int absoluteDay,
        IReadOnlyList<CharacterId> participantIds,
        FestivalResolutionGrade grade,
        int assignedParticipantCount = 0,
        int venueCapacity = 0,
        float plannedDurationSeconds = 0f,
        float elapsedFestivalSeconds = 0f,
        bool stoppedByVenueHazard = false)
    {
        FestivalId = festivalId ?? string.Empty;
        AbsoluteDay = absoluteDay;
        ParticipantIds = participantIds ?? Array.Empty<CharacterId>();
        Grade = grade;
        AssignedParticipantCount = Math.Max(0, assignedParticipantCount);
        VenueCapacity = Math.Max(0, venueCapacity);
        PlannedDurationSeconds = Math.Max(0f, plannedDurationSeconds);
        ElapsedFestivalSeconds = Math.Max(0f, elapsedFestivalSeconds);
        StoppedByVenueHazard = stoppedByVenueHazard;
    }

    public string FestivalId { get; }
    public int AbsoluteDay { get; }
    public IReadOnlyList<CharacterId> ParticipantIds { get; }
    public FestivalResolutionGrade Grade { get; }
    public int AssignedParticipantCount { get; }
    public int VenueCapacity { get; }
    public float PlannedDurationSeconds { get; }
    public float ElapsedFestivalSeconds { get; }
    public bool StoppedByVenueHazard { get; }
}

public interface IFuneralFestivalService
{
    void HoldFuneral(
        BuildingInstanceId facilityId,
        CharacterId deceasedId,
        IReadOnlyCollection<CharacterId> participantIds);
    void HoldJointMemorial(
        BuildingInstanceId facilityId,
        IReadOnlyCollection<CharacterId> deceasedIds,
        IReadOnlyCollection<CharacterId> participantIds);
    void AttendFestival(
        string festivalId,
        IReadOnlyCollection<CharacterId> participantIds);
}

public enum FestivalResolutionGrade
{
    Failure,
    Partial,
    Success
}

public sealed class FestivalScheduleRequest
{
    public string ActionId { get; set; } = string.Empty;
    public string FestivalId { get; set; } = string.Empty;
    public int OccurrenceYear { get; set; }
    public bool Decline { get; set; }
    public IReadOnlyCollection<CharacterId> ParticipantIds { get; set; } =
        Array.Empty<CharacterId>();
}

public sealed class FestivalPreparedOrder
{
    public string ActionId { get; internal set; } = string.Empty;
    public string OccurrenceId { get; internal set; } = string.Empty;
    public string FestivalId { get; internal set; } = string.Empty;
    public string FacilityInstanceId { get; internal set; } = string.Empty;
    public int AbsoluteDay { get; internal set; }
    public int DeadlineAbsoluteDay { get; internal set; }
    public int DeadlineHour { get; internal set; }
    public float RequiredPreparationWork { get; internal set; }
    public float PlannedDurationSeconds { get; internal set; }
    public int VenueCapacity { get; internal set; }
    public FestivalResolutionGrade Grade { get; internal set; }
    public IReadOnlyList<CharacterId> ParticipantIds { get; internal set; } =
        Array.Empty<CharacterId>();
    public IReadOnlyDictionary<string, int> ItemCosts { get; internal set; } =
        new Dictionary<string, int>();
}

public interface IFestivalCommand
{
    bool Schedule(
        FestivalScheduleRequest request,
        out FestivalPreparedOrder order,
        out DomainFailure failure);
    bool Resolve(
        FestivalPreparedOrder order,
        out DomainFailure failure);
}

public interface ISocialCareCommand
{
    bool TryHoldFuneral(
        string actionId,
        CharacterId deceasedId,
        IReadOnlyCollection<CharacterId> participantIds,
        string facilityInstanceId,
        out DomainFailure failure);
    bool TryHoldJointMemorial(
        string actionId,
        IReadOnlyCollection<CharacterId> deceasedIds,
        IReadOnlyCollection<CharacterId> participantIds,
        string facilityInstanceId,
        out DomainFailure failure);
    bool TryCounsel(
        string actionId,
        CharacterId patientId,
        out DomainFailure failure);
}

public sealed class FuneralFestivalRuntime :
    IFuneralFestivalService,
    ISocialCareCommand
{
    private const string MemorialWorkstationTag = "workstation:v19:memorial";
    private readonly IKinshipQuery kinship;
    private readonly ICharacterSpeciesDefinitionCatalog species;
    private readonly IFestivalDefinitionCatalog festivals;
    private readonly ICharacterWorldQuery characters;
    private readonly IBuildingWorldQuery buildings;
    private readonly IGriefTraumaService grief;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IPsychosocialPersistence psychosocial;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;
    private readonly IFactionCampaignQuery factions;
    private readonly V20ContentResolutionService contentResolution;
    private readonly ICharacterRitualFastingCommand ritualFasting;
    private readonly IFestivalCommand festivalCommands;

    public FuneralFestivalRuntime(
        IKinshipQuery kinship,
        ICharacterSpeciesDefinitionCatalog species,
        IFestivalDefinitionCatalog festivals,
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings,
        IGriefTraumaService grief,
        IGameCalendar calendar,
        IGameEventBus events,
        IPsychosocialPersistence psychosocial,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems,
        IFactionCampaignQuery factions,
        V20ContentResolutionService contentResolution,
        IFestivalCommand festivalCommands,
        ICharacterRitualFastingCommand ritualFasting = null)
    {
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.species = species ?? throw new ArgumentNullException(nameof(species));
        this.festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.psychosocial = psychosocial
            ?? throw new ArgumentNullException(nameof(psychosocial));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
        this.contentResolution = contentResolution
            ?? throw new ArgumentNullException(nameof(contentResolution));
        this.festivalCommands = festivalCommands
            ?? throw new ArgumentNullException(nameof(festivalCommands));
        this.ritualFasting = ritualFasting;
    }

    public void HoldFuneral(
        BuildingInstanceId facilityId,
        CharacterId deceasedId,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        if (!TryHoldFuneral(
                $"legacy-funeral:{deceasedId.Value}:{calendar.Day}",
                deceasedId,
                participantIds,
                facilityId.Value,
                out DomainFailure failure))
            throw new InvalidOperationException(
                $"Funeral for '{deceasedId.Value}' failed: {failure.Code}.");
    }

    public void HoldJointMemorial(
        BuildingInstanceId facilityId,
        IReadOnlyCollection<CharacterId> deceasedIds,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        if (!TryHoldJointMemorial(
                $"legacy-joint-memorial:{calendar.Day}",
                deceasedIds,
                participantIds,
                facilityId.Value,
                out DomainFailure failure))
            throw new InvalidOperationException(
                $"Joint memorial failed: {failure.Code}.");
    }

    public void AttendFestival(
        string festivalId,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        FestivalScheduleRequest request = new()
        {
            ActionId = $"legacy:{festivalId}:{calendar.Day}",
            FestivalId = festivalId,
            ParticipantIds = participantIds
        };
        if (!festivalCommands.Schedule(
                request,
                out _,
                out DomainFailure failure))
        {
            throw new InvalidOperationException(
                $"Festival '{festivalId}' could not be resolved: {failure.Code}.");
        }
    }

    public bool Schedule(
        FestivalScheduleRequest request,
        out FestivalPreparedOrder order,
        out DomainFailure failure)
    {
        return festivalCommands.Schedule(request, out order, out failure);
    }

    public bool Resolve(
        FestivalPreparedOrder order,
        out DomainFailure failure)
    {
        return festivalCommands.Resolve(order, out failure);
    }

    public bool TryHoldFuneral(
        string actionId,
        CharacterId deceasedId,
        IReadOnlyCollection<CharacterId> participantIds,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(actionId) || !deceasedId.IsValid)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        CharacterTombstoneSaveData tombstone;
        CharacterId[] living;
        try
        {
            tombstone = RequireTombstone(deceasedId);
            living = RequireLivingParticipants(participantIds);
        }
        catch (InvalidOperationException)
        {
            failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        FuneralCultureSO culture = RequireCulture(tombstone);
        BuildableObject facility = buildings.Buildings
            .Where(value => value != null && !value.isDestroy
                && (string.IsNullOrWhiteSpace(facilityInstanceId)
                    || string.Equals(
                        value.PersistentInstanceId.Value,
                        facilityInstanceId.Trim(),
                        StringComparison.Ordinal)))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault(value => value.HasSemanticTag(MemorialWorkstationTag)
                && value.HasSemanticTag(culture.requiredFacilityTag));
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        string owner = $"funeral:{actionId.Trim()}";
        if (!TryReserve(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["supply:funeral-preparation-kit"] = 1
                },
                owner,
                out IReadOnlyList<ReservedItemConsumption> reserved,
                out failure))
            return false;
        PsychosocialAggregateState candidate = psychosocial.PrepareRestore(
            psychosocial.Capture());
        CharacterId[] participants = living.Where(value =>
                candidate.TryGet(value, out CharacterGriefAggregate state)
                && state.NeedsFuneral(deceasedId))
            .ToArray();
        if (participants.Length == 0)
        {
            Release(reserved, owner);
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        try
        {
            foreach (CharacterId participant in participants)
                candidate.Require(participant).CompleteFuneral(
                    deceasedId,
                    calendar.Day,
                    matchingSpeciesRitual: true);
        }
        catch (InvalidOperationException)
        {
            Release(reserved, owner);
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        ObservedFuneralLifeEventReceipt observedReceipt;
        try
        {
            observedReceipt = new ObservedFuneralLifeEventReceipt(
                actionId.Trim(),
                deceasedId,
                facility.PersistentInstanceId.Value,
                calendar.Day,
                Math.Max(0, tombstone.generation),
                participants);
            contentResolution.RequireCanCommitObservedFuneralLifeEvent(
                observedReceipt);
        }
        catch
        {
            Release(reserved, owner);
            throw;
        }
        if (!atomicItems.TryConsumeReserved(reserved, owner, out failure))
        {
            Release(reserved, owner);
            return false;
        }
        psychosocial.PublishRestore(candidate);
        CompleteParticipantFasts(participants);
        foreach (V20ResolvedEventResult resolved in
                 contentResolution.CommitObservedFuneralLifeEvent(
                     observedReceipt))
        {
            V20SocietyEventAlertProjection.PublishResolved(
                events,
                resolved,
                physicalEffectsApplied: true);
        }
        return true;
    }

    public bool TryHoldJointMemorial(
        string actionId,
        IReadOnlyCollection<CharacterId> deceasedIds,
        IReadOnlyCollection<CharacterId> participantIds,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterId[] deceased = (deceasedIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (string.IsNullOrWhiteSpace(actionId) || deceased.Length < 3)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        BuildableObject facility = buildings.Buildings
            .Where(value => value != null && !value.isDestroy
                && (string.IsNullOrWhiteSpace(facilityInstanceId)
                    || string.Equals(
                        value.PersistentInstanceId.Value,
                        facilityInstanceId.Trim(),
                        StringComparison.Ordinal)))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault(value => value.HasSemanticTag(MemorialWorkstationTag));
        CharacterTombstoneSaveData[] tombstones;
        CharacterId[] living;
        try
        {
            tombstones = deceased.Select(RequireTombstone).ToArray();
            living = RequireLivingParticipants(participantIds);
        }
        catch (InvalidOperationException)
        {
            failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        if (facility == null
            || tombstones.Max(value => value.deathAbsoluteDay)
                - tombstones.Min(value => value.deathAbsoluteDay) > 10
            || tombstones.Any(value =>
                !facility.HasSemanticTag(RequireCulture(value).requiredFacilityTag)))
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        string owner = $"joint-memorial:{actionId.Trim()}";
        if (!TryReserve(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["supply:funeral-preparation-kit"] = deceased.Length
                },
                owner,
                out IReadOnlyList<ReservedItemConsumption> reserved,
                out failure))
            return false;

        PsychosocialAggregateState candidate = psychosocial.PrepareRestore(
            psychosocial.Capture());
        CharacterId[] participants = living.Where(value =>
                candidate.TryGet(value, out CharacterGriefAggregate state)
                && deceased.Any(state.NeedsFuneral))
            .ToArray();
        if (participants.Length == 0)
        {
            Release(reserved, owner);
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        try
        {
            foreach (CharacterId participant in participants)
                candidate.Require(participant).CompleteJointMemorial(
                    deceased,
                    calendar.Day,
                    matchingSpeciesRitual: true);
        }
        catch (InvalidOperationException)
        {
            Release(reserved, owner);
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (!atomicItems.TryConsumeReserved(reserved, owner, out failure))
        {
            Release(reserved, owner);
            return false;
        }
        psychosocial.PublishRestore(candidate);
        CompleteParticipantFasts(participants);
        return true;
    }

    public bool TryCounsel(
        string actionId,
        CharacterId patientId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(actionId)
            || !patientId.IsValid
            || !grief.TryGet(patientId, out CharacterGriefAggregate current)
            || current.Trauma <= 0f)
        {
            failure = new DomainFailure(FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        BuildableObject facility = buildings.Buildings
            .Where(value => value != null && !value.isDestroy)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault(value => string.Equals(
                DefinitionId(value.BuildingData),
                "building:8885",
                StringComparison.Ordinal));
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }
        string owner = $"counsel:{actionId.Trim()}";
        if (!TryReserve(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["medical:trauma-care-kit"] = 1
                },
                owner,
                out IReadOnlyList<ReservedItemConsumption> reserved,
                out failure))
            return false;
        PsychosocialAggregateState candidate = psychosocial.PrepareRestore(
            psychosocial.Capture());
        candidate.Require(patientId).ApplyCounseling();
        if (!atomicItems.TryConsumeReserved(reserved, owner, out failure))
        {
            Release(reserved, owner);
            return false;
        }
        psychosocial.PublishRestore(candidate);
        return true;
    }

    private void CompleteParticipantFasts(
        IEnumerable<CharacterId> participantIds)
    {
        if (ritualFasting == null)
            return;
        HashSet<CharacterId> participants = new(
            participantIds ?? Array.Empty<CharacterId>());
        foreach (CharacterActor actor in characters.Characters
                     .Where(value => value != null
                         && CharacterPersistentIdentity.TryGet(
                             value,
                             out CharacterId id)
                         && participants.Contains(id))
                     .OrderBy(value => value.Identity?.PersistentId,
                         StringComparer.Ordinal))
        {
            ritualFasting.TryComplete(actor, out _);
        }
    }

    private bool TryReserve(
        IReadOnlyDictionary<string, int> costs,
        string owner,
        out IReadOnlyList<ReservedItemConsumption> selected,
        out DomainFailure failure)
    {
        List<ReservedItemConsumption> result = new();
        foreach (KeyValuePair<string, int> cost in costs
                     .Where(value => value.Value > 0)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int needed = cost.Value;
            foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                         .Where(value => value != null
                             && value.Quantity > 0
                             && !value.Forbidden
                             && value.AvailableQuantity > 0
                             && string.Equals(value.ItemId, cost.Key, StringComparison.Ordinal))
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
            && !reservations.TryReserveQuantities(
                result,
                owner,
                ItemReservationPurpose.FacilityBuffer,
                $"festival:{owner}:materials"))
        {
            selected = Array.Empty<ReservedItemConsumption>();
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        selected = result;
        failure = DomainFailure.None;
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

    private static string DefinitionId(BuildingSO building) =>
        !string.IsNullOrWhiteSpace(building?.ContentDefinitionId)
            ? building.ContentDefinitionId
            : building == null ? string.Empty : $"building:{building.id}";

    private CharacterTombstoneSaveData RequireTombstone(CharacterId deceasedId) =>
        kinship.TryGetTombstone(deceasedId, out CharacterTombstoneSaveData tombstone)
            ? tombstone
            : throw new InvalidOperationException(
                $"No active tombstone exists for '{deceasedId.Value}'.");

    private FuneralCultureSO RequireCulture(CharacterTombstoneSaveData tombstone)
    {
        CharacterSpeciesId speciesId = new(tombstone.phenotypeSpeciesId);
        if (!species.TryGetDefinition(speciesId, out CharacterSpeciesDefinitionSO definition)
            || definition.funeralCulture == null)
        {
            throw new InvalidOperationException(
                $"Species '{speciesId.Value}' has no authored funeral culture.");
        }
        return definition.funeralCulture;
    }

    private BuildableObject RequireMemorialFacility(BuildingInstanceId facilityId)
    {
        BuildableObject facility = buildings.Buildings.FirstOrDefault(value =>
            value != null && !value.isDestroy
            && value.PersistentInstanceId.Equals(facilityId));
        if (facility == null || !facility.HasSemanticTag(MemorialWorkstationTag))
            throw new InvalidOperationException(
                $"A built memorial facility is required: '{facilityId.Value}'.");
        return facility;
    }

    private CharacterId[] RequireLivingParticipants(
        IReadOnlyCollection<CharacterId> participantIds)
    {
        CharacterId[] participants = (participantIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (participants.Length == 0)
            throw new InvalidOperationException("At least one living participant is required.");
        HashSet<CharacterId> living = characters.Characters
            .Where(value => value != null && !value.IsDead)
            .Select(CharacterPersistentIdentity.Require)
            .ToHashSet();
        if (participants.Any(value => !living.Contains(value)))
            throw new InvalidOperationException(
                "Funeral and festival participants must be living world characters.");
        return participants;
    }
}
