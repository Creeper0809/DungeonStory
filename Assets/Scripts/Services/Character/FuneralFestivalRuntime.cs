using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public readonly struct FestivalCelebratedEvent
{
    public FestivalCelebratedEvent(
        string festivalId,
        int absoluteDay,
        IReadOnlyList<CharacterId> participantIds)
    {
        FestivalId = festivalId ?? string.Empty;
        AbsoluteDay = absoluteDay;
        ParticipantIds = participantIds ?? Array.Empty<CharacterId>();
    }

    public string FestivalId { get; }
    public int AbsoluteDay { get; }
    public IReadOnlyList<CharacterId> ParticipantIds { get; }
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

public sealed class FuneralFestivalRuntime : IFuneralFestivalService
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

    public FuneralFestivalRuntime(
        IKinshipQuery kinship,
        ICharacterSpeciesDefinitionCatalog species,
        IFestivalDefinitionCatalog festivals,
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings,
        IGriefTraumaService grief,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.species = species ?? throw new ArgumentNullException(nameof(species));
        this.festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void HoldFuneral(
        BuildingInstanceId facilityId,
        CharacterId deceasedId,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        CharacterTombstoneSaveData tombstone = RequireTombstone(deceasedId);
        BuildableObject facility = RequireMemorialFacility(facilityId);
        FuneralCultureSO culture = RequireCulture(tombstone);
        if (!facility.HasSemanticTag(culture.requiredFacilityTag))
            throw new InvalidOperationException(
                $"Facility '{facilityId.Value}' does not support funeral culture '{culture.cultureId}'.");

        CharacterId[] participants = RequireLivingParticipants(participantIds);
        foreach (CharacterId participant in participants)
            grief.CompleteFuneral(
                participant,
                deceasedId,
                calendar.Day,
                matchingRitual: true);
    }

    public void HoldJointMemorial(
        BuildingInstanceId facilityId,
        IReadOnlyCollection<CharacterId> deceasedIds,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        CharacterId[] deceased = (deceasedIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (deceased.Length < 3)
            throw new InvalidOperationException(
                "A joint memorial requires at least three different deceased characters.");

        BuildableObject facility = RequireMemorialFacility(facilityId);
        CharacterTombstoneSaveData[] tombstones = deceased
            .Select(RequireTombstone)
            .ToArray();
        if (tombstones.Max(value => value.deathAbsoluteDay)
            - tombstones.Min(value => value.deathAbsoluteDay) > 10)
        {
            throw new InvalidOperationException(
                "Joint memorial deaths must fall within ten days.");
        }
        foreach (CharacterTombstoneSaveData tombstone in tombstones)
        {
            FuneralCultureSO culture = RequireCulture(tombstone);
            if (!facility.HasSemanticTag(culture.requiredFacilityTag))
                throw new InvalidOperationException(
                    $"Facility '{facilityId.Value}' does not support funeral culture '{culture.cultureId}'.");
        }

        CharacterId[] participants = RequireLivingParticipants(participantIds);
        foreach (CharacterId participant in participants)
            grief.CompleteJointMemorial(
                participant,
                deceased,
                calendar.Day,
                matchingRitual: true);
    }

    public void AttendFestival(
        string festivalId,
        IReadOnlyCollection<CharacterId> participantIds)
    {
        FestivalDefinitionSO festival = festivals.Require(festivalId);
        if (calendar.Season != festival.season
            || calendar.DayOfSeason != festival.dayOfSeason)
        {
            throw new InvalidOperationException(
                $"Festival '{festival.StableId}' is not scheduled for the current date.");
        }

        CharacterId[] participants = RequireLivingParticipants(participantIds);
        foreach (CharacterId participant in participants)
        {
            CharacterGriefAggregate state = grief.Require(participant);
            state.RecordFestivalAttendance(festival.StableId, calendar.Year);
            if (festival.convertsActiveGrief)
                state.ApplyLongNightMemorial(calendar.Day);
        }
        events.Publish(new FestivalCelebratedEvent(
            festival.StableId,
            calendar.Day,
            participants));
    }

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
