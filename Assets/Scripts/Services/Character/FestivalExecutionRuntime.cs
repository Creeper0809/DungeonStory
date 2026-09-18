using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public enum FestivalExecutionPhase
{
    None = 0,
    Preparing = 1,
    Running = 2,
    Cancelled = 3,
    Stopped = 4,
    Resolved = 5,
    Declined = 6
}

[Serializable]
public sealed class FestivalExecutionItemSaveData
{
    public string itemId = string.Empty;
    public int quantity;

    public FestivalExecutionItemSaveData Clone() => new()
    {
        itemId = itemId ?? string.Empty,
        quantity = quantity
    };
}

[Serializable]
public sealed class FestivalExecutionCellSaveData
{
    public int x;
    public int y;

    public Vector2Int Position => new(x, y);

    public FestivalExecutionCellSaveData Clone() => new() { x = x, y = y };
}

[Serializable]
public sealed class FestivalAttendanceProgressSaveData
{
    public string characterId = string.Empty;
    public int assignedCellX;
    public int assignedCellY;
    public float attendedSeconds;

    public Vector2Int AssignedCell => new(assignedCellX, assignedCellY);

    public FestivalAttendanceProgressSaveData Clone() => new()
    {
        characterId = characterId ?? string.Empty,
        assignedCellX = assignedCellX,
        assignedCellY = assignedCellY,
        attendedSeconds = attendedSeconds
    };
}

[Serializable]
public sealed class FestivalExecutionOccurrenceSaveData
{
    public string occurrenceId = string.Empty;
    public string actionId = string.Empty;
    public string festivalId = string.Empty;
    public int occurrenceYear;
    public FestivalExecutionPhase phase;
    public int festivalAbsoluteDay;
    public int startHour;
    public int deadlineAbsoluteDay;
    public int deadlineHour;
    public float requiredPreparationWork;
    public float completedPreparationWork;
    public float plannedDurationSeconds;
    public float elapsedFestivalSeconds;
    public string venueFacilityInstanceId = string.Empty;
    public int venueAnchorX;
    public int venueAnchorY;
    public int venueAccessX;
    public int venueAccessY;
    public int venueCapacity;
    public List<FestivalExecutionCellSaveData> venueCells = new();
    public List<FestivalExecutionItemSaveData> itemCosts = new();
    public List<FestivalAttendanceProgressSaveData> attendance = new();
    public string materialDestinationId = string.Empty;
    public bool inputOwnerActive;
    public long inputCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public bool materialReceiptPending;
    public string materialOperationId = string.Empty;
    public string materialReasonCode = string.Empty;
    public string materialRequestFingerprint = string.Empty;
    public string materialCommitId = string.Empty;
    public int materialQuantity;
    public long materialMassGrams;
    public List<string> materialSourceStackIds = new();
    public bool effectsApplied;
    public FestivalResolutionGrade resultGrade;
    public string resultReason = string.Empty;
    public string resultSummary = string.Empty;
    public string lastStatus = string.Empty;
    public bool cleanupComplete;

    public FestivalExecutionOccurrenceSaveData Clone() => new()
    {
        occurrenceId = occurrenceId ?? string.Empty,
        actionId = actionId ?? string.Empty,
        festivalId = festivalId ?? string.Empty,
        occurrenceYear = occurrenceYear,
        phase = phase,
        festivalAbsoluteDay = festivalAbsoluteDay,
        startHour = startHour,
        deadlineAbsoluteDay = deadlineAbsoluteDay,
        deadlineHour = deadlineHour,
        requiredPreparationWork = requiredPreparationWork,
        completedPreparationWork = completedPreparationWork,
        plannedDurationSeconds = plannedDurationSeconds,
        elapsedFestivalSeconds = elapsedFestivalSeconds,
        venueFacilityInstanceId = venueFacilityInstanceId ?? string.Empty,
        venueAnchorX = venueAnchorX,
        venueAnchorY = venueAnchorY,
        venueAccessX = venueAccessX,
        venueAccessY = venueAccessY,
        venueCapacity = venueCapacity,
        venueCells = (venueCells ?? new List<FestivalExecutionCellSaveData>())
            .Select(value => value?.Clone())
            .Where(value => value != null)
            .ToList(),
        itemCosts = (itemCosts ?? new List<FestivalExecutionItemSaveData>())
            .Select(value => value?.Clone())
            .Where(value => value != null)
            .ToList(),
        attendance = (attendance ?? new List<FestivalAttendanceProgressSaveData>())
            .Select(value => value?.Clone())
            .Where(value => value != null)
            .ToList(),
        materialDestinationId = materialDestinationId ?? string.Empty,
        inputOwnerActive = inputOwnerActive,
        inputCapacityGrams = inputCapacityGrams,
        inputMassAuthorityRevision = inputMassAuthorityRevision,
        inputCapacityFingerprint = inputCapacityFingerprint ?? string.Empty,
        materialReceiptPending = materialReceiptPending,
        materialOperationId = materialOperationId ?? string.Empty,
        materialReasonCode = materialReasonCode ?? string.Empty,
        materialRequestFingerprint = materialRequestFingerprint ?? string.Empty,
        materialCommitId = materialCommitId ?? string.Empty,
        materialQuantity = materialQuantity,
        materialMassGrams = materialMassGrams,
        materialSourceStackIds = new List<string>(
            materialSourceStackIds ?? new List<string>()),
        effectsApplied = effectsApplied,
        resultGrade = resultGrade,
        resultReason = resultReason ?? string.Empty,
        resultSummary = resultSummary ?? string.Empty,
        lastStatus = lastStatus ?? string.Empty,
        cleanupComplete = cleanupComplete
    };
}

[Serializable]
public sealed class FestivalExecutionWorldSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<FestivalExecutionOccurrenceSaveData> occurrences = new();
}

public sealed class FestivalExecutionAggregateState
{
    public IReadOnlyList<FestivalExecutionOccurrenceSaveData> Occurrences
        { get; internal set; } = Array.Empty<FestivalExecutionOccurrenceSaveData>();
}

public sealed class FestivalMaterialPreview
{
    public string ItemId { get; internal set; } = string.Empty;
    public int Required { get; internal set; }
    public int Available { get; internal set; }
}

public sealed class FestivalExecutionPreview
{
    public string OccurrenceId { get; internal set; } = string.Empty;
    public string FestivalId { get; internal set; } = string.Empty;
    public int OccurrenceYear { get; internal set; }
    public int FestivalAbsoluteDay { get; internal set; }
    public int AnnouncementAbsoluteDay { get; internal set; }
    public int StartHour { get; internal set; }
    public int DeadlineHour { get; internal set; }
    public float RequiredPreparationWork { get; internal set; }
    public int PreparationLeadDays { get; internal set; }
    public int PlannedDurationHours { get; internal set; }
    public IReadOnlyList<CharacterId> ParticipantIds { get; internal set; } =
        Array.Empty<CharacterId>();
    public SocietyVenueCandidateSnapshot Venue { get; internal set; }
    public IReadOnlyList<FestivalMaterialPreview> Materials { get; internal set; } =
        Array.Empty<FestivalMaterialPreview>();
    public bool CanHost { get; internal set; }
    public string FailureReason { get; internal set; } = string.Empty;
}

public interface IFestivalExecutionQuery
{
    IReadOnlyList<FestivalExecutionOccurrenceSaveData> Occurrences { get; }
    bool HasOccurrence(string occurrenceId);
    FestivalExecutionPreview BuildPreview(string festivalId, int occurrenceYear);
}

public interface IFestivalExecutionDriver
{
    void Advance();
}

public interface IFestivalExecutionPersistence
{
    FestivalExecutionWorldSaveData CaptureFestivalExecutions();
    FestivalExecutionAggregateState PrepareFestivalExecutionRestore(
        FestivalExecutionWorldSaveData payload);
    void PublishFestivalExecutionRestore(FestivalExecutionAggregateState candidate);
}

public static class FestivalExecutionRules
{
    public const float MinimumValidAttendanceRatio = 0.25f;
    public const int StartHour = 18;
    public const int MaterialUnitsPerPreparationWu = 4;
    public const int PreparationWuPerLeadDay = 4;
    public const string MaterialReasonCode = "festival-material-consumed";
    public const string MaterialOperationPrefix = "festival-material:";

    public static int RequiredPreparationWork(FestivalDefinitionSO festival)
    {
        int physicalUnits = (festival?.requiredItems ?? new List<FestivalItemRequirement>())
            .Where(value => value != null && value.amount > 0)
            .Sum(value => value.amount);
        return Math.Max(
            1,
            (physicalUnits + MaterialUnitsPerPreparationWu - 1)
                / MaterialUnitsPerPreparationWu);
    }

    public static int PreparationLeadDays(FestivalDefinitionSO festival)
    {
        int work = RequiredPreparationWork(festival);
        return Math.Max(1, (work + PreparationWuPerLeadDay - 1)
            / PreparationWuPerLeadDay);
    }

    public static int PlannedDurationHours(FestivalDefinitionSO festival) =>
        Math.Clamp(
            (Math.Max(1, festival?.minimumParticipants ?? 1) + 3) / 4,
            2,
            5);

    public static float PlannedDurationSeconds(FestivalDefinitionSO festival) =>
        PlannedDurationHours(festival) * GameCalendarRules.SecondsPerGameHour;

    public static int FestivalAbsoluteDay(
        FestivalDefinitionSO festival,
        int occurrenceYear)
    {
        if (festival == null)
            throw new ArgumentNullException(nameof(festival));
        int year = Math.Max(1, occurrenceYear);
        return checked(
            (year - 1) * GameCalendarRules.DaysPerYear
            + (int)festival.season * GameCalendarRules.DaysPerSeason
            + Math.Clamp(
                festival.dayOfSeason,
                1,
                GameCalendarRules.DaysPerSeason));
    }

    public static int AnnouncementAbsoluteDay(
        FestivalDefinitionSO festival,
        int occurrenceYear) => Math.Max(
        1,
        FestivalAbsoluteDay(festival, occurrenceYear)
            - PreparationLeadDays(festival));

    public static string OccurrenceId(string festivalId, int occurrenceYear) =>
        $"festival:{festivalId?.Trim() ?? string.Empty}:year:{Math.Max(1, occurrenceYear)}";

    public static string MaterialOperationId(string occurrenceId) =>
        MaterialOperationPrefix + Uri.EscapeDataString(
            occurrenceId?.Trim() ?? string.Empty);

    public static float AttendanceRatio(float attendedSeconds, float plannedSeconds) =>
        plannedSeconds <= 0f
            ? 0f
            : Mathf.Clamp01(attendedSeconds / plannedSeconds);

    public static bool IsValidAttendance(float attendedSeconds, float plannedSeconds) =>
        AttendanceRatio(attendedSeconds, plannedSeconds)
            + 0.0001f >= MinimumValidAttendanceRatio;

    public static float ScalePersonalNumericBenefit(float value, float ratio) =>
        value * Mathf.Clamp01(ratio);
}

public sealed class FestivalExecutionRuntime :
    IFestivalCommand,
    IFestivalExecutionQuery,
    IFestivalExecutionDriver,
    IFestivalExecutionPersistence,
    IDungeonRestoreTransactionParticipant
{
    private sealed class ParticipantRuntime
    {
        internal CharacterActor Actor;
        internal CharacterActionIntentLease Lease;
        internal Coroutine Movement;
        internal long MovementEpoch;
    }

    private enum FestivalMovementStatus
    {
        Reached,
        InProgress,
        Deferred,
        Unavailable,
    }

    private readonly IFestivalDefinitionCatalog festivals;
    private readonly ICharacterWorldQuery characters;
    private readonly ISocietyVenueQuery venues;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterSettlementStandingQuery standings;
    private readonly ICharacterCombatStanceQuery combat;
    private readonly IWorldHazardZoneQuery hazards;
    private readonly IGridSystemProvider grids;
    private readonly IGameCalendar calendar;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly IPsychosocialPersistence psychosocial;
    private readonly IFactionCampaignQuery factions;
    private readonly V20CampaignRuntime campaign;
    private readonly ICharacterRitualFastingCommand ritualFasting;
    private readonly IEconomyProjectInputOwnerPort inputOwners;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalFacilityItemBatchSinkGateway materialSink;
    private readonly IWorkAmountCalculator workAmounts;
    private readonly Dictionary<string, FestivalExecutionOccurrenceSaveData>
        byOccurrence = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParticipantRuntime> participantRuntime =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> alertFingerprints =
        new(StringComparer.Ordinal);
    private FestivalExecutionAggregateState stagedRestore;
    private FestivalExecutionAggregateState rollbackRestore;
    private bool restoreTransactionOpen;
    private bool restorePublished;

    public FestivalExecutionRuntime(
        IFestivalDefinitionCatalog festivals,
        ICharacterWorldQuery characters,
        ISocietyVenueQuery venues,
        ICharacterNarrativeQuery narratives,
        ICharacterSettlementStandingQuery standings,
        ICharacterCombatStanceQuery combat,
        IWorldHazardZoneQuery hazards,
        IGridSystemProvider grids,
        IGameCalendar calendar,
        IGameClock clock,
        IGameEventBus events,
        IPsychosocialPersistence psychosocial,
        IFactionCampaignQuery factions,
        V20CampaignRuntime campaign,
        IEconomyProjectInputOwnerPort inputOwners,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemBatchSinkGateway materialSink,
        IWorkAmountCalculator workAmounts,
        ICharacterRitualFastingCommand ritualFasting = null)
    {
        this.festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.venues = venues ?? throw new ArgumentNullException(nameof(venues));
        this.narratives = narratives ?? throw new ArgumentNullException(nameof(narratives));
        this.standings = standings ?? throw new ArgumentNullException(nameof(standings));
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
        this.grids = grids ?? throw new ArgumentNullException(nameof(grids));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.psychosocial = psychosocial
            ?? throw new ArgumentNullException(nameof(psychosocial));
        this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
        this.campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.materialSink = materialSink
            ?? throw new ArgumentNullException(nameof(materialSink));
        this.workAmounts = workAmounts
            ?? throw new ArgumentNullException(nameof(workAmounts));
        this.ritualFasting = ritualFasting;
    }

    public IReadOnlyList<FestivalExecutionOccurrenceSaveData> Occurrences =>
        byOccurrence.Values
            .OrderBy(value => value.occurrenceId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

    public string ParticipantId => "festival-execution-runtime";

    public bool HasOccurrence(string occurrenceId) =>
        !string.IsNullOrWhiteSpace(occurrenceId)
        && byOccurrence.ContainsKey(occurrenceId.Trim());

    public FestivalExecutionPreview BuildPreview(
        string festivalId,
        int occurrenceYear)
    {
        FestivalDefinitionSO festival = festivals.Require(festivalId);
        int year = Math.Max(1, occurrenceYear);
        CharacterActor[] eligible = GetEligibleActors(festival)
            .Where(actor => !IsCriticalDuty(actor))
            .OrderBy(actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToArray();
        SocietyVenueCandidateSnapshot selectedVenue = null;
        CharacterId[] selectedParticipants = Array.Empty<CharacterId>();
        string venueFailure = eligible.Length < festival.minimumParticipants
            ? "문화·신분·필수 업무 조건을 충족하는 참가자가 부족합니다."
            : string.Empty;
        // Preview is an availability hint, not an implicit full-roster decision.
        // Keep it at the smallest viable roster; Schedule owns the submitted roster.
        for (int count = festival.minimumParticipants;
             count <= eligible.Length;
             count++)
        {
            CharacterId[] candidateParticipants = eligible
                .Take(count)
                .Select(CharacterPersistentIdentity.Require)
                .ToArray();
            if (!venues.TryFindFestivalVenues(
                    festival.venueRequirements,
                    candidateParticipants,
                    count,
                    out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
                    out venueFailure))
            {
                continue;
            }
            selectedVenue = candidates[0];
            selectedParticipants = candidateParticipants;
            break;
        }

        IReadOnlyDictionary<string, int> requirements = MaterialRequirements(festival);
        Dictionary<string, int> available = items.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && value.AvailableQuantity > 0
                && !value.Forbidden)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.AvailableQuantity),
                StringComparer.Ordinal);
        int festivalDay = FestivalExecutionRules.FestivalAbsoluteDay(festival, year);
        return new FestivalExecutionPreview
        {
            OccurrenceId = FestivalExecutionRules.OccurrenceId(
                festival.StableId,
                year),
            FestivalId = festival.StableId,
            OccurrenceYear = year,
            FestivalAbsoluteDay = festivalDay,
            AnnouncementAbsoluteDay = FestivalExecutionRules
                .AnnouncementAbsoluteDay(festival, year),
            StartHour = FestivalExecutionRules.StartHour,
            DeadlineHour = FestivalExecutionRules.StartHour,
            RequiredPreparationWork = FestivalExecutionRules
                .RequiredPreparationWork(festival),
            PreparationLeadDays = FestivalExecutionRules
                .PreparationLeadDays(festival),
            PlannedDurationHours = FestivalExecutionRules
                .PlannedDurationHours(festival),
            ParticipantIds = Array.AsReadOnly(selectedParticipants),
            Venue = selectedVenue,
            Materials = requirements.Select(pair => new FestivalMaterialPreview
                {
                    ItemId = pair.Key,
                    Required = pair.Value,
                    Available = available.TryGetValue(pair.Key, out int count)
                        ? count
                        : 0
                })
                .ToArray(),
            CanHost = selectedVenue != null
                && selectedParticipants.Length >= festival.minimumParticipants,
            FailureReason = selectedVenue != null
                ? string.Empty
                : string.IsNullOrWhiteSpace(venueFailure)
                    ? "실제 접근·수용 가능한 축제 장소가 없습니다."
                    : venueFailure
        };
    }

    public bool Schedule(
        FestivalScheduleRequest request,
        out FestivalPreparedOrder order,
        out DomainFailure failure)
    {
        order = null;
        failure = DomainFailure.None;
        if (request == null
            || string.IsNullOrWhiteSpace(request.ActionId)
            || string.IsNullOrWhiteSpace(request.FestivalId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        int requestedYear = request.OccurrenceYear > 0
            ? request.OccurrenceYear
            : calendar.Year;
        if (request.Decline)
            return Decline(request.FestivalId, requestedYear, out failure);

        FestivalExecutionPreview preview;
        FestivalDefinitionSO festival;
        try
        {
            festival = festivals.Require(request.FestivalId);
            preview = BuildPreview(
                festival.StableId,
                requestedYear);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or KeyNotFoundException)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        long deadlineAbsoluteHour = ((long)preview.FestivalAbsoluteDay - 1L)
            * GameCalendarRules.HoursPerDay + preview.DeadlineHour;
        if (calendar.Day < preview.AnnouncementAbsoluteDay
            || calendar.AbsoluteHour >= deadlineAbsoluteHour)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        if (byOccurrence.TryGetValue(
                preview.OccurrenceId,
                out FestivalExecutionOccurrenceSaveData existing))
        {
            if (string.Equals(
                    existing.actionId,
                    request.ActionId.Trim(),
                    StringComparison.Ordinal)
                && existing.phase is FestivalExecutionPhase.Preparing
                    or FestivalExecutionPhase.Running)
            {
                order = BuildPreparedOrder(existing);
                return true;
            }
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        if (!TryResolveScheduledRoster(
                festival,
                request.ParticipantIds,
                preview,
                out CharacterId[] participants,
                out SocietyVenueCandidateSnapshot venue,
                out string rosterFailure))
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                rosterFailure);
            return false;
        }

        IReadOnlyDictionary<string, int> material = MaterialRequirements(festival);
        string destinationId = EconomyProjectInputOwnerAuthority
            .BuildFestivalDestinationId(preview.OccurrenceId);
        if (!inputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.FestivalDomain,
                preview.OccurrenceId,
                destinationId,
                venue.AnchorCenter,
                EconomyProjectInputOwnerAnchorKind.LiveFacility,
                venue.FacilityInstanceId,
                material,
                0L,
                0L,
                string.Empty,
                out EconomyProjectInputOwnerProjection projection,
                out string ownerFailure))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferDestinationMissing,
                ownerFailure);
            return false;
        }

        Vector2Int[] eventCells = venue.EventCells
            .OrderBy(value => value.y)
            .ThenBy(value => value.x)
            .ToArray();
        FestivalExecutionOccurrenceSaveData state = new()
        {
            occurrenceId = preview.OccurrenceId,
            actionId = request.ActionId.Trim(),
            festivalId = festival.StableId,
            occurrenceYear = preview.OccurrenceYear,
            phase = FestivalExecutionPhase.Preparing,
            festivalAbsoluteDay = preview.FestivalAbsoluteDay,
            startHour = preview.StartHour,
            deadlineAbsoluteDay = preview.FestivalAbsoluteDay,
            deadlineHour = preview.DeadlineHour,
            requiredPreparationWork = preview.RequiredPreparationWork,
            plannedDurationSeconds = FestivalExecutionRules
                .PlannedDurationSeconds(festival),
            venueFacilityInstanceId = venue.FacilityInstanceId,
            venueAnchorX = venue.AnchorCenter.x,
            venueAnchorY = venue.AnchorCenter.y,
            venueAccessX = venue.AccessCell.x,
            venueAccessY = venue.AccessCell.y,
            venueCapacity = venue.EventCells.Count,
            venueCells = eventCells.Select(value => new FestivalExecutionCellSaveData
                { x = value.x, y = value.y }).ToList(),
            itemCosts = material.Select(pair => new FestivalExecutionItemSaveData
                { itemId = pair.Key, quantity = pair.Value }).ToList(),
            attendance = participants.Select((id, index) =>
                new FestivalAttendanceProgressSaveData
                {
                    characterId = id.Value,
                    assignedCellX = eventCells[index].x,
                    assignedCellY = eventCells[index].y
                }).ToList(),
            materialDestinationId = destinationId,
            inputOwnerActive = true,
            inputCapacityGrams = projection.CapacityGrams,
            inputMassAuthorityRevision = projection.MassAuthorityRevision,
            inputCapacityFingerprint = projection.Fingerprint,
            materialOperationId = FestivalExecutionRules.MaterialOperationId(
                preview.OccurrenceId),
            materialReasonCode = FestivalExecutionRules.MaterialReasonCode,
            lastStatus = "물자 운반과 현장 준비를 시작했습니다."
        };
        byOccurrence.Add(state.occurrenceId, state);
        order = BuildPreparedOrder(state);
        PublishAlertIfChanged(state, festival, force: true);
        return true;
    }

    public bool Decline(
        string festivalId,
        int occurrenceYear,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        FestivalDefinitionSO festival;
        try
        {
            festival = festivals.Require(festivalId);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or KeyNotFoundException)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        string occurrenceId = FestivalExecutionRules.OccurrenceId(
            festival.StableId,
            occurrenceYear);
        if (byOccurrence.TryGetValue(occurrenceId, out FestivalExecutionOccurrenceSaveData existing))
        {
            if (existing.phase == FestivalExecutionPhase.Declined)
                return true;
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        int festivalDay = FestivalExecutionRules.FestivalAbsoluteDay(
            festival,
            occurrenceYear);
        long deadlineAbsoluteHour = ((long)festivalDay - 1L)
            * GameCalendarRules.HoursPerDay + FestivalExecutionRules.StartHour;
        if (calendar.Day < FestivalExecutionRules.AnnouncementAbsoluteDay(
                festival,
                occurrenceYear)
            || calendar.AbsoluteHour >= deadlineAbsoluteHour)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        byOccurrence.Add(occurrenceId, new FestivalExecutionOccurrenceSaveData
        {
            occurrenceId = occurrenceId,
            festivalId = festival.StableId,
            occurrenceYear = Math.Max(1, occurrenceYear),
            phase = FestivalExecutionPhase.Declined,
            festivalAbsoluteDay = festivalDay,
            startHour = FestivalExecutionRules.StartHour,
            deadlineAbsoluteDay = festivalDay,
            deadlineHour = FestivalExecutionRules.StartHour,
            resultReason = "player-declined",
            resultSummary = "이번 회차는 개최하지 않았습니다. 물자·작업·평판 비용이 없습니다.",
            lastStatus = "미개최 선택",
            cleanupComplete = true
        });
        PublishAlertIfChanged(byOccurrence[occurrenceId], festival, force: true);
        return true;
    }

    public bool Resolve(FestivalPreparedOrder order, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string occurrenceId = order?.OccurrenceId ?? string.Empty;
        if (occurrenceId.Length == 0
            || !byOccurrence.TryGetValue(
                occurrenceId,
                out FestivalExecutionOccurrenceSaveData state))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (state.phase is FestivalExecutionPhase.Resolved
            or FestivalExecutionPhase.Stopped)
            return true;
        if (state.phase != FestivalExecutionPhase.Running
            || state.elapsedFestivalSeconds + 0.0001f
                < state.plannedDurationSeconds)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "축제가 아직 실제 참석 시간을 완료하지 않았습니다.");
            return false;
        }
        ResolveOccurrence(state, stopped: false, "planned-duration-complete");
        return true;
    }

    public void Advance()
    {
        float deltaSeconds = clock.IsPaused
            ? 0f
            : Mathf.Max(0f, clock.DeltaTime);
        foreach (FestivalExecutionOccurrenceSaveData state in byOccurrence.Values
                     .OrderBy(value => value.occurrenceId, StringComparer.Ordinal)
                     .ToArray())
        {
            FestivalDefinitionSO festival = festivals.Require(state.festivalId);
            switch (state.phase)
            {
                case FestivalExecutionPhase.Preparing:
                    AdvancePreparation(state, festival, deltaSeconds);
                    break;
                case FestivalExecutionPhase.Running:
                    AdvanceAttendance(state, festival, deltaSeconds);
                    break;
                case FestivalExecutionPhase.Cancelled:
                case FestivalExecutionPhase.Stopped:
                case FestivalExecutionPhase.Resolved:
                    AdvanceTerminalCleanup(state);
                    break;
            }
            PublishAlertIfChanged(state, festival);
        }
    }

    public FestivalExecutionWorldSaveData CaptureFestivalExecutions()
    {
        FestivalExecutionWorldSaveData payload = new()
        {
            occurrences = byOccurrence.Values
                .OrderBy(value => value.occurrenceId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList()
        };
        _ = PrepareFestivalExecutionRestore(payload);
        return payload;
    }

    public FestivalExecutionAggregateState PrepareFestivalExecutionRestore(
        FestivalExecutionWorldSaveData payload)
    {
        if (payload == null
            || payload.version != FestivalExecutionWorldSaveData.CurrentVersion
            || payload.occurrences == null)
            throw new InvalidOperationException(
                "Festival execution save payload is invalid or obsolete.");
        FestivalExecutionOccurrenceSaveData[] occurrences = payload.occurrences
            .Select(value => value?.Clone()
                ?? throw new InvalidOperationException(
                    "Festival execution save contains a null occurrence."))
            .OrderBy(value => value.occurrenceId, StringComparer.Ordinal)
            .ToArray();
        if (occurrences.Any(value => !ValidateOccurrence(value))
            || occurrences.Select(value => value.occurrenceId)
                .Distinct(StringComparer.Ordinal).Count() != occurrences.Length)
            throw new InvalidOperationException(
                "Festival execution save contains invalid or duplicate authority.");
        return new FestivalExecutionAggregateState
        {
            Occurrences = Array.AsReadOnly(occurrences)
        };
    }

    public void PublishFestivalExecutionRestore(
        FestivalExecutionAggregateState candidate)
    {
        if (candidate?.Occurrences == null)
            throw new ArgumentNullException(nameof(candidate));
        FestivalExecutionAggregateState detached = CloneAggregate(candidate);
        if (restoreTransactionOpen)
        {
            if (stagedRestore != null)
                throw new InvalidOperationException(
                    "Festival execution restore candidate is already staged.");
            stagedRestore = detached;
            return;
        }
        ApplyAggregate(detached);
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionOpen)
            throw new InvalidOperationException(
                "Festival execution restore transaction is already open.");
        restoreTransactionOpen = true;
        restorePublished = false;
        stagedRestore = null;
        rollbackRestore = null;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionOpen || stagedRestore == null)
            throw new InvalidOperationException(
                "Festival execution restore candidate was not staged.");
        rollbackRestore = CaptureAggregate();
        ApplyAggregate(stagedRestore);
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (restorePublished && rollbackRestore != null)
            ApplyAggregate(rollbackRestore);
        ClearRestoreTransaction();
    }

    public void CompleteRestoreCandidate() => ClearRestoreTransaction();

    public void DiscardRestoreCandidate() => ClearRestoreTransaction();

    private FestivalExecutionAggregateState CaptureAggregate() => new()
    {
        Occurrences = Array.AsReadOnly(byOccurrence.Values
            .OrderBy(value => value.occurrenceId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray())
    };

    private static FestivalExecutionAggregateState CloneAggregate(
        FestivalExecutionAggregateState candidate) => new()
    {
        Occurrences = Array.AsReadOnly(candidate.Occurrences
            .Select(value => value.Clone())
            .ToArray())
    };

    private void ApplyAggregate(FestivalExecutionAggregateState candidate)
    {
        foreach (string occurrenceId in byOccurrence.Keys.ToArray())
            ReleaseOccurrenceRuntime(occurrenceId);
        byOccurrence.Clear();
        foreach (FestivalExecutionOccurrenceSaveData value in candidate.Occurrences)
            byOccurrence.Add(value.occurrenceId, value.Clone());
        alertFingerprints.Clear();
    }

    private void ClearRestoreTransaction()
    {
        restoreTransactionOpen = false;
        restorePublished = false;
        stagedRestore = null;
        rollbackRestore = null;
    }

    private void AdvancePreparation(
        FestivalExecutionOccurrenceSaveData state,
        FestivalDefinitionSO festival,
        float deltaSeconds)
    {
        long deadline = ((long)state.deadlineAbsoluteDay - 1L)
            * GameCalendarRules.HoursPerDay + state.deadlineHour;
        bool deadlineReached = calendar.AbsoluteHour >= deadline;
        if (deadlineReached
            && !TryRefreshStartingRoster(
                state,
                festival,
                out _,
                out string rosterFailure))
        {
            CancelOccurrence(state, rosterFailure);
            return;
        }

        CharacterId[] participants = ParticipantIds(state);
        bool venueValid = venues.TryValidateFestivalVenue(
            festival.venueRequirements,
            participants,
            participants.Length,
            state.venueFacilityInstanceId,
            out SocietyVenueCandidateSnapshot venue,
            out string venueFailure);
        IReadOnlyDictionary<string, int> material = MaterialRequirements(state);
        string ownerFailure = string.Empty;
        bool ownerValid = venueValid && state.inputOwnerActive
            && inputOwners.TryValidate(
                EconomyProjectInputOwnerAuthority.FestivalDomain,
                state.occurrenceId,
                state.materialDestinationId,
                new Vector2Int(state.venueAnchorX, state.venueAnchorY),
                EconomyProjectInputOwnerAnchorKind.LiveFacility,
                state.venueFacilityInstanceId,
                material,
                state.inputCapacityGrams,
                state.inputMassAuthorityRevision,
                state.inputCapacityFingerprint,
                out ownerFailure);
        bool allArrived = ownerValid && material.All(pair =>
            CountArrived(state.materialDestinationId, pair.Key) >= pair.Value);
        if (deadlineReached)
        {
            if (!venueValid
                || !ownerValid
                || !allArrived
                || state.completedPreparationWork + 0.0001f
                    < state.requiredPreparationWork)
            {
                string reason = !venueValid
                    ? venueFailure
                    : !ownerValid
                        ? ownerFailure
                        : !allArrived
                            ? "필수 물자가 마감까지 현장에 도착하지 않았습니다."
                            : "필수 준비 작업이 마감까지 완료되지 않았습니다.";
                CancelOccurrence(state, reason);
                return;
            }
            if (!TryStartFestival(state, festival))
                CancelOccurrence(
                    state,
                    "마감 시점에 도착 물자의 소비 receipt를 확정하지 못했습니다.");
            return;
        }

        if (!venueValid || !ownerValid)
        {
            state.lastStatus = !venueValid ? venueFailure : ownerFailure;
            ReleaseOccurrenceRuntime(state.occurrenceId);
            return;
        }
        RequestMissingMaterials(state, material);
        if (state.completedPreparationWork + 0.0001f
            >= state.requiredPreparationWork)
        {
            state.completedPreparationWork = state.requiredPreparationWork;
            state.lastStatus = allArrived
                ? "준비 작업과 물자 도착 완료 · 개최 시각 대기"
                : "준비 작업 완료 · 물자 운반 대기";
            ReleaseOccurrenceRuntime(state.occurrenceId);
            return;
        }

        CharacterActor worker = participants
            .Select(FindActor)
            .Where(value => IsActive(value) && !IsCriticalDuty(value))
            .OrderBy(value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (worker == null)
        {
            state.lastStatus = "필수 업무를 제외한 준비 작업자가 없습니다.";
            ReleaseOccurrenceRuntime(state.occurrenceId);
            return;
        }
        string runtimeKey = RuntimeKey(state.occurrenceId,
            CharacterPersistentIdentity.Require(worker));
        ReleaseOtherOccurrenceRuntime(state.occurrenceId, runtimeKey);
        Vector2Int preparationCell = new(state.venueAccessX, state.venueAccessY);
        FestivalMovementStatus movementStatus = EnsureParticipantAt(
                runtimeKey,
                state.occurrenceId,
                worker,
                preparationCell,
                "축제 준비",
                "준비 장소로 이동",
                out string movementFailure);
        if (movementStatus != FestivalMovementStatus.Reached)
        {
            state.lastStatus = movementStatus == FestivalMovementStatus.Unavailable
                ? $"준비 이동 불가: {movementFailure}"
                : movementFailure;
            return;
        }
        float rate = workAmounts.CalculateWorkPerSecond(
            worker,
            venue.Facility,
            BuiltInWorkTypeIds.Perform,
            1f);
        state.completedPreparationWork = Mathf.Min(
            state.requiredPreparationWork,
            state.completedPreparationWork + Mathf.Max(0f, rate) * deltaSeconds);
        ParticipantRuntime runtime = participantRuntime[runtimeKey];
        worker.Brain?.UpdateExternallyDrivenAction(
            runtime.Lease,
            "축제 준비",
            $"준비 {Mathf.RoundToInt(state.completedPreparationWork / state.requiredPreparationWork * 100f)}%",
            festival.displayName);
        state.lastStatus = $"현장 준비 {state.completedPreparationWork:0.0}/{state.requiredPreparationWork:0.0} WU"
            + (allArrived ? " · 물자 도착 완료" : " · 물자 운반 중");
    }

    private bool TryRefreshStartingRoster(
        FestivalExecutionOccurrenceSaveData state,
        FestivalDefinitionSO festival,
        out SocietyVenueCandidateSnapshot venue,
        out string failureReason)
    {
        venue = null;
        failureReason = string.Empty;
        CharacterId[] requested = ParticipantIds(state);
        CharacterId[] selected = EligibleSubmittedParticipants(festival, requested);
        if (selected.Length < festival.minimumParticipants)
        {
            failureReason = "개최 시각에 제출 참가자 중 문화·신분·필수 업무·생존 조건을 충족하는 인원이 부족합니다.";
            return false;
        }
        if (!venues.TryValidateFestivalVenue(
                festival.venueRequirements,
                selected,
                selected.Length,
                state.venueFacilityInstanceId,
                out venue,
                out failureReason)
            || venue.AnchorCenter != new Vector2Int(
                state.venueAnchorX,
                state.venueAnchorY))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "개최 시각에 확정 장소와 참가자 접근 경로를 함께 검증할 수 없습니다."
                : failureReason;
            return false;
        }

        int capacity = Math.Min(
            state.venueCapacity,
            venue.EventCells.Count);
        if (capacity < selected.Length)
        {
            failureReason = "개최 시각에 확정 장소의 실제 행사 칸 정원이 제출 참가 인원보다 작습니다.";
            return false;
        }

        Vector2Int[] eventCells = venue.EventCells
            .OrderBy(value => value.y)
            .ThenBy(value => value.x)
            .Take(selected.Length)
            .ToArray();
        if (eventCells.Length != selected.Length)
        {
            failureReason = "개최 시각에 참가자별 실제 행사 칸을 직접 배정할 수 없습니다.";
            return false;
        }

        Dictionary<string, float> priorAttendance = state.attendance
            .ToDictionary(
                value => value.characterId,
                value => value.attendedSeconds,
                StringComparer.Ordinal);
        state.venueAccessX = venue.AccessCell.x;
        state.venueAccessY = venue.AccessCell.y;
        state.venueCapacity = capacity;
        state.venueCells = eventCells
            .Select(value => new FestivalExecutionCellSaveData
                { x = value.x, y = value.y })
            .ToList();
        state.attendance = selected.Select((id, index) =>
            new FestivalAttendanceProgressSaveData
            {
                characterId = id.Value,
                assignedCellX = eventCells[index].x,
                assignedCellY = eventCells[index].y,
                attendedSeconds = priorAttendance.TryGetValue(
                    id.Value,
                    out float attendedSeconds)
                    ? attendedSeconds
                    : 0f
            }).ToList();
        return true;
    }

    private void AdvanceAttendance(
        FestivalExecutionOccurrenceSaveData state,
        FestivalDefinitionSO festival,
        float deltaSeconds)
    {
        CharacterActor[] activeParticipants = state.attendance
            .Select(value => new CharacterId(value.characterId))
            .Select(FindActor)
            .Where(IsActive)
            .OrderBy(
                value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .ToArray();
        if (activeParticipants.Length == 0)
        {
            ResolveOccurrence(
                state,
                stopped: true,
                "참가 가능한 생존 인원이 남지 않았습니다.");
            return;
        }

        HashSet<CharacterId> criticalDutyParticipants = activeParticipants
            .Where(IsCriticalDuty)
            .Select(CharacterPersistentIdentity.Require)
            .ToHashSet();
        foreach (CharacterId participantId in criticalDutyParticipants
                     .OrderBy(value => value.Value, StringComparer.Ordinal))
            ReleaseParticipantRuntime(RuntimeKey(
                state.occurrenceId,
                participantId));

        CharacterId[] validationSubjects = activeParticipants
            .Where(actor => IsCultureEligible(actor, festival)
                && !criticalDutyParticipants.Contains(
                    CharacterPersistentIdentity.Require(actor)))
            .Select(CharacterPersistentIdentity.Require)
            .ToArray();
        if (validationSubjects.Length > 0
            && !venues.TryValidateFestivalVenue(
                festival.venueRequirements,
                validationSubjects,
                state.attendance.Count,
                state.venueFacilityInstanceId,
                out _,
                out string venueFailure))
        {
            ResolveOccurrence(
                state,
                stopped: true,
                venueFailure);
            return;
        }
        CharacterId hazardSubject = validationSubjects.Length > 0
            ? validationSubjects[0]
            : CharacterPersistentIdentity.Require(activeParticipants[0]);
        WorldHazardFlags siteFlags = WorldHazardFlags.Combat
            | WorldHazardFlags.Fire;
        bool siteUnsafe = state.venueCells.Any(cell =>
        {
            WorldHazardSnapshot hazard = hazards.GetHazard(
                hazardSubject,
                cell.Position);
            return hazard.Level == WorldHazardLevel.Forbidden
                || (hazard.Flags & siteFlags) != 0;
        });
        if (siteUnsafe)
        {
            ResolveOccurrence(
                state,
                stopped: true,
                "축제 현장에 화재·전투 또는 통행 금지 위험이 발생해 전원 대피했습니다.");
            return;
        }

        float elapsedDelta = Mathf.Min(
            deltaSeconds,
            Mathf.Max(0f, state.plannedDurationSeconds
                - state.elapsedFestivalSeconds));
        List<string> movementFailures = new();
        foreach (FestivalAttendanceProgressSaveData attendance in state.attendance
                     .OrderBy(value => value.characterId, StringComparer.Ordinal))
        {
            CharacterId characterId = new(attendance.characterId);
            CharacterActor actor = FindActor(characterId);
            string runtimeKey = RuntimeKey(state.occurrenceId, characterId);
            if (!IsActive(actor)
                || !IsCultureEligible(actor, festival))
            {
                ReleaseParticipantRuntime(runtimeKey);
                continue;
            }
            if (criticalDutyParticipants.Contains(characterId))
                continue;
            FestivalMovementStatus movementStatus = EnsureParticipantAt(
                    runtimeKey,
                    state.occurrenceId,
                    actor,
                    attendance.AssignedCell,
                    "축제 참석",
                    "배정된 행사 칸으로 이동",
                    out string movementFailure);
            if (movementStatus == FestivalMovementStatus.Unavailable)
            {
                movementFailures.Add(
                    $"{characterId.Value}: {movementFailure}");
                continue;
            }
            if (movementStatus != FestivalMovementStatus.Reached)
                continue;
            attendance.attendedSeconds = Mathf.Min(
                state.plannedDurationSeconds,
                attendance.attendedSeconds + elapsedDelta);
            ParticipantRuntime runtime = participantRuntime[runtimeKey];
            actor.Brain?.UpdateExternallyDrivenAction(
                runtime.Lease,
                "축제 참석",
                $"참석 {FestivalExecutionRules.AttendanceRatio(attendance.attendedSeconds, state.plannedDurationSeconds) * 100f:0}%",
                festival.displayName);
        }
        state.elapsedFestivalSeconds = Mathf.Min(
            state.plannedDurationSeconds,
            state.elapsedFestivalSeconds + elapsedDelta);
        int present = state.attendance.Count(value =>
        {
            CharacterId characterId = new(value.characterId);
            CharacterActor actor = FindActor(characterId);
            return IsActive(actor)
                && IsCultureEligible(actor, festival)
                && !criticalDutyParticipants.Contains(characterId)
                && actor.GetNowXY() == value.AssignedCell;
        });
        state.lastStatus = $"행사 진행 {state.elapsedFestivalSeconds:0.0}/{state.plannedDurationSeconds:0.0}초"
            + $" · 현장 {present}/{state.attendance.Count}명"
            + (criticalDutyParticipants.Count == 0
                ? string.Empty
                : $" · 필수 업무 이탈 {criticalDutyParticipants.Count}명")
            + (movementFailures.Count == 0
                ? string.Empty
                : " · 이동 불가 " + string.Join(", ", movementFailures));
        if (state.elapsedFestivalSeconds + 0.0001f
            >= state.plannedDurationSeconds)
        {
            string completionReason = movementFailures.Count == 0
                ? "planned-duration-complete"
                : "planned-duration-complete; movement-unavailable: "
                    + string.Join(", ", movementFailures);
            ResolveOccurrence(state, stopped: false, completionReason);
        }
    }

    private bool TryStartFestival(
        FestivalExecutionOccurrenceSaveData state,
        FestivalDefinitionSO festival)
    {
        IReadOnlyDictionary<string, int> material = MaterialRequirements(state);
        PhysicalItemBatchDispositionReceipt receipt;
        if (!materialSink.TryGetPending(state.materialOperationId, out receipt)
            && !materialSink.TryCommitSinkPending(
                state.materialDestinationId,
                material,
                state.materialOperationId,
                FestivalExecutionRules.MaterialReasonCode,
                out receipt,
                out string sinkFailure))
        {
            state.lastStatus = sinkFailure;
            return false;
        }
        if (!ReceiptMatches(state, receipt, requireSavedReceipt: false))
        {
            state.lastStatus = "축제 물자 소비 receipt가 요청과 일치하지 않습니다.";
            return false;
        }
        RecordReceipt(state, receipt);
        state.phase = FestivalExecutionPhase.Running;
        state.elapsedFestivalSeconds = 0f;
        state.lastStatus = "물자 소비 확정 · 참가자 이동 시작";
        ReleaseOccurrenceRuntime(state.occurrenceId);
        return true;
    }

    private void ResolveOccurrence(
        FestivalExecutionOccurrenceSaveData state,
        bool stopped,
        string reason)
    {
        if (state.effectsApplied)
            return;
        FestivalDefinitionSO festival = festivals.Require(state.festivalId);
        FestivalAttendanceProgressSaveData[] valid = state.attendance
            .Where(value => FestivalExecutionRules.IsValidAttendance(
                value.attendedSeconds,
                state.plannedDurationSeconds))
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        int partialMinimum = Math.Max(1, (festival.minimumParticipants + 1) / 2);
        FestivalResolutionGrade grade = valid.Length >= festival.minimumParticipants
            ? FestivalResolutionGrade.Success
            : valid.Length >= partialMinimum
                ? FestivalResolutionGrade.Partial
                : FestivalResolutionGrade.Failure;
        FestivalOutcomeDefinition outcome = grade switch
        {
            FestivalResolutionGrade.Success => festival.successOutcome,
            FestivalResolutionGrade.Partial => festival.partialOutcome,
            _ => festival.failureOutcome
        };
        PsychosocialAggregateState candidate = psychosocial.PrepareRestore(
            psychosocial.Capture());
        foreach (FestivalAttendanceProgressSaveData attendance in valid)
        {
            CharacterId participant = new(attendance.characterId);
            CharacterGriefAggregate griefState = candidate.Require(participant);
            griefState.RecordFestivalAttendance(festival.StableId, state.occurrenceYear);
            float ratio = FestivalExecutionRules.AttendanceRatio(
                attendance.attendedSeconds,
                state.plannedDurationSeconds);
            float griefDelta = FestivalExecutionRules.ScalePersonalNumericBenefit(
                outcome.griefConversionPercent,
                ratio);
            if (griefDelta > 0f)
                griefState.ApplyGriefConversion(griefDelta);
        }
        psychosocial.PublishRestore(candidate);
        foreach (FestivalAttendanceProgressSaveData attendance in valid)
        {
            CharacterActor actor = FindActor(new CharacterId(attendance.characterId));
            if (actor == null)
                continue;
            float ratio = FestivalExecutionRules.AttendanceRatio(
                attendance.attendedSeconds,
                state.plannedDurationSeconds);
            float moodDelta = FestivalExecutionRules.ScalePersonalNumericBenefit(
                outcome.moodDelta,
                ratio);
            if (Mathf.Abs(moodDelta) > 0.0001f)
            {
                actor.ApplyMoodFactor(
                    $"festival:{festival.StableId}:{state.occurrenceYear}",
                    festival.displayName,
                    moodDelta,
                    Math.Max(1, outcome.moodDurationDays)
                        * GameCalendarRules.SecondsPerDay,
                    1);
            }
        }
        if (outcome.factionRapportDelta != 0)
        {
            foreach (FactionCampaignStateSaveData faction in factions.Factions)
                campaign.ApplyFactionChange(
                    faction.factionId,
                    outcome.factionRapportDelta,
                    0,
                    0);
        }

        CharacterId[] validIds = valid
            .Select(value => new CharacterId(value.characterId))
            .ToArray();
        events.Publish(new FestivalCelebratedEvent(
            festival.StableId,
            calendar.Day,
            validIds,
            grade,
            state.attendance.Count,
            state.venueCapacity,
            state.plannedDurationSeconds,
            state.elapsedFestivalSeconds,
            stopped));
        CompleteParticipantFasts(validIds);
        state.effectsApplied = true;
        state.phase = stopped
            ? FestivalExecutionPhase.Stopped
            : FestivalExecutionPhase.Resolved;
        state.resultGrade = grade;
        state.resultReason = reason ?? string.Empty;
        float attendanceRate = state.attendance.Count == 0
            ? 0f
            : state.attendance.Sum(value => FestivalExecutionRules.AttendanceRatio(
                    value.attendedSeconds,
                    state.plannedDurationSeconds))
                / state.attendance.Count;
        state.resultSummary = $"{DescribeGrade(grade)} · 유효 참석 {valid.Length}/{state.attendance.Count}명"
            + $" · 평균 참석률 {attendanceRate * 100f:0}%"
            + $" · 장소 정원 {state.venueCapacity}명"
            + (stopped ? " · 현장 위험으로 중단" : string.Empty);
        state.lastStatus = state.resultSummary;
        ReleaseOccurrenceRuntime(state.occurrenceId);
        AdvanceTerminalCleanup(state);
    }

    private void CancelOccurrence(
        FestivalExecutionOccurrenceSaveData state,
        string reason)
    {
        if (state.phase != FestivalExecutionPhase.Preparing)
            return;
        state.phase = FestivalExecutionPhase.Cancelled;
        state.resultReason = reason?.Trim() ?? string.Empty;
        state.resultSummary = "준비 마감 미충족으로 이번 회차 취소 · 미사용·운반 중 물자 회수"
            + $" · 소비 물자 {state.materialQuantity}개 · 준비 작업 {state.completedPreparationWork:0.0} WU 유지";
        state.lastStatus = state.resultSummary
            + (state.resultReason.Length == 0
                ? string.Empty
                : " · " + state.resultReason);
        ReleaseOccurrenceRuntime(state.occurrenceId);
        AdvanceTerminalCleanup(state);
    }

    private void AdvanceTerminalCleanup(FestivalExecutionOccurrenceSaveData state)
    {
        if (state.inputOwnerActive)
        {
            if (!inputOwners.TryRetireDestination(
                    EconomyProjectInputOwnerAuthority.FestivalDomain,
                    state.materialDestinationId,
                    EconomyProjectInputOwnerAuthority.FestivalTerminalReason,
                    out string retireFailure))
            {
                state.lastStatus = state.resultSummary + " · 물자 권위 회수 재시도: "
                    + retireFailure;
                return;
            }
            state.inputOwnerActive = false;
        }
        if (state.materialReceiptPending)
        {
            if (!materialSink.TryGetPending(
                    state.materialOperationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !ReceiptMatches(state, receipt, requireSavedReceipt: true))
            {
                state.lastStatus = state.resultSummary
                    + " · 소비 receipt 확인 재시도";
                return;
            }
            if (!materialSink.Acknowledge(
                    state.materialCommitId,
                    out string acknowledgeFailure))
            {
                state.lastStatus = state.resultSummary
                    + " · 소비 receipt 승인 재시도: " + acknowledgeFailure;
                return;
            }
            ClearReceipt(state);
        }
        state.cleanupComplete = true;
        state.lastStatus = state.resultSummary;
    }

    private void RequestMissingMaterials(
        FestivalExecutionOccurrenceSaveData state,
        IReadOnlyDictionary<string, int> material)
    {
        Vector2Int destination = new(state.venueAnchorX, state.venueAnchorY);
        foreach (KeyValuePair<string, int> required in material)
        {
            int assigned = CountAssigned(
                state.materialDestinationId,
                required.Key);
            int missing = Math.Max(0, required.Value - assigned);
            if (missing <= 0)
                continue;
            if (!items.TryRequestItemDelivery(
                    required.Key,
                    missing,
                    destination,
                    state.materialDestinationId,
                    out int requested,
                    out string failure)
                || requested != missing)
            {
                state.lastStatus = string.IsNullOrWhiteSpace(failure)
                    ? $"{required.Key} 운반 배정 {requested}/{missing}"
                    : failure;
                return;
            }
        }
    }

    private FestivalMovementStatus EnsureParticipantAt(
        string runtimeKey,
        string occurrenceId,
        CharacterActor actor,
        Vector2Int target,
        string label,
        string phase,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsActive(actor) || actor.Brain == null)
        {
            failureReason = "활성 참가자 또는 AI 행동 권위를 확인할 수 없습니다.";
            return FestivalMovementStatus.Unavailable;
        }
        if (!participantRuntime.TryGetValue(runtimeKey, out ParticipantRuntime runtime))
        {
            runtime = new ParticipantRuntime { Actor = actor };
            participantRuntime.Add(runtimeKey, runtime);
        }
        if (!runtime.Lease.IsValid
            || !actor.Brain.IsExternalIntentCurrent(runtime.Lease))
        {
            runtime.Movement = null;
            runtime.Lease = default;
            if (!actor.Brain.TryBeginExternallyDrivenAction(
                    "festival:" + occurrenceId,
                    CharacterActionIntentKind.Festival,
                    label,
                    phase,
                    $"목표 ({target.x}, {target.y})",
                    out runtime.Lease))
            {
                failureReason = "더 높은 우선순위 행동으로 행사 이동이 보류되었습니다.";
                return FestivalMovementStatus.Deferred;
            }
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }
        if (actor.GetNowXY() == target)
        {
            runtime.Movement = null;
            return FestivalMovementStatus.Reached;
        }
        if (runtime.Movement != null)
        {
            failureReason = "행사 위치로 이동 중입니다.";
            return FestivalMovementStatus.InProgress;
        }
        if (!grids.TryGetGrid(out Grid grid))
        {
            failureReason = "행사 이동에 필요한 실제 격자를 확인할 수 없습니다.";
            ReleaseParticipantRuntime(runtimeKey);
            return FestivalMovementStatus.Unavailable;
        }
        if (actor.PathSearchBroker == null)
        {
            failureReason = "참가자의 경로 탐색 권위를 확인할 수 없습니다.";
            ReleaseParticipantRuntime(runtimeKey);
            return FestivalMovementStatus.Unavailable;
        }
        if (!actor.TryGetAbility(out AbilityMove move))
        {
            failureReason = "참가자의 실제 이동 능력을 확인할 수 없습니다.";
            ReleaseParticipantRuntime(runtimeKey);
            return FestivalMovementStatus.Unavailable;
        }
        GridPathRequestStatus status = actor.PathSearchBroker.RequestMovePathTo(
            grid,
            actor.GetNowXY(),
            target,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(actor)));
        if (status == GridPathRequestStatus.Pending)
        {
            failureReason = "행사 위치로 이동 경로를 계산하는 중입니다.";
            return FestivalMovementStatus.InProgress;
        }
        if (status != GridPathRequestStatus.Reachable
            || path == null
            || path.Count == 0)
        {
            failureReason = "현재 위치에서 행사 목표 칸까지 실제 이동 경로가 없습니다.";
            ReleaseParticipantRuntime(runtimeKey);
            return FestivalMovementStatus.Unavailable;
        }
        runtime.MovementEpoch = checked(runtime.MovementEpoch + 1L);
        long epoch = runtime.MovementEpoch;
        runtime.Movement = actor.StartCoroutine(RunMovement(
            runtimeKey,
            actor,
            move,
            path,
            epoch));
        actor.Brain.UpdateExternallyDrivenAction(
            runtime.Lease,
            label,
            phase,
            $"목표 ({target.x}, {target.y})");
        failureReason = "행사 위치로 이동 중입니다.";
        return FestivalMovementStatus.InProgress;
    }

    private IEnumerator RunMovement(
        string runtimeKey,
        CharacterActor actor,
        AbilityMove move,
        Queue<GridMoveStep> path,
        long epoch)
    {
        yield return move.MoveByPath(path);
        if (participantRuntime.TryGetValue(runtimeKey, out ParticipantRuntime runtime)
            && ReferenceEquals(runtime.Actor, actor)
            && runtime.MovementEpoch == epoch)
            runtime.Movement = null;
    }

    private void ReleaseOtherOccurrenceRuntime(
        string occurrenceId,
        string preservedRuntimeKey)
    {
        string prefix = occurrenceId + "|";
        foreach (string key in participantRuntime.Keys
                     .Where(value => value.StartsWith(prefix, StringComparison.Ordinal)
                         && !string.Equals(value, preservedRuntimeKey,
                            StringComparison.Ordinal))
                     .ToArray())
            ReleaseParticipantRuntime(key);
    }

    private void ReleaseOccurrenceRuntime(string occurrenceId)
    {
        string prefix = occurrenceId + "|";
        foreach (string key in participantRuntime.Keys
                     .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
                     .ToArray())
            ReleaseParticipantRuntime(key);
    }

    private void ReleaseParticipantRuntime(string runtimeKey)
    {
        if (!participantRuntime.TryGetValue(runtimeKey, out ParticipantRuntime runtime))
            return;
        if (runtime.Actor?.Brain?.IsExternalIntentCurrent(runtime.Lease) == true)
        {
            runtime.Actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            runtime.Actor.Brain.CancelExternallyDrivenAction(runtime.Lease);
        }
        participantRuntime.Remove(runtimeKey);
    }

    private void PublishAlertIfChanged(
        FestivalExecutionOccurrenceSaveData state,
        FestivalDefinitionSO festival,
        bool force = false)
    {
        string fingerprint = string.Join(
            "|",
            (int)state.phase,
            state.completedPreparationWork.ToString("0.0"),
            state.elapsedFestivalSeconds.ToString("0.0"),
            state.attendance.Sum(value => value.attendedSeconds).ToString("0.0"),
            state.inputOwnerActive,
            state.materialReceiptPending,
            state.lastStatus ?? string.Empty);
        if (!force
            && alertFingerprints.TryGetValue(state.occurrenceId, out string current)
            && string.Equals(current, fingerprint, StringComparison.Ordinal))
            return;
        alertFingerprints[state.occurrenceId] = fingerprint;
        bool terminal = state.phase is FestivalExecutionPhase.Cancelled
            or FestivalExecutionPhase.Stopped
            or FestivalExecutionPhase.Resolved
            or FestivalExecutionPhase.Declined;
        string attendance = state.attendance.Count == 0
            ? "배정 참가자 없음"
            : string.Join(
                ", ",
                state.attendance.Select(value =>
                    $"{value.characterId} {FestivalExecutionRules.AttendanceRatio(value.attendedSeconds, state.plannedDurationSeconds) * 100f:0}%"));
        string detail = $"장소: {state.venueFacilityInstanceId} · 정원 {state.venueCapacity}명 · 배정 {state.attendance.Count}명\n"
            + $"준비: {state.completedPreparationWork:0.0}/{state.requiredPreparationWork:0.0} WU\n"
            + $"개최/마감: {state.deadlineAbsoluteDay}일 {state.deadlineHour}:00 · 예정 {state.plannedDurationSeconds / GameCalendarRules.SecondsPerGameHour:0}시간\n"
            + $"실제 참석: {attendance}\n상태: {state.lastStatus}";
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            festival.displayName,
            detail,
            state.phase == FestivalExecutionPhase.Running
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            "V21 축제",
            Array.Empty<EventAlertChoice>(),
            state.occurrenceId,
            isResolved: terminal,
            resultSummary: terminal
                ? string.IsNullOrWhiteSpace(state.resultSummary)
                    ? state.lastStatus
                    : state.resultSummary
                : string.Empty)));
    }

    private bool ValidateOccurrence(FestivalExecutionOccurrenceSaveData state)
    {
        if (state == null
            || !EconomyProjectInputOwnerAuthority.IsCanonical(state.occurrenceId)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(state.festivalId)
            || state.occurrenceYear <= 0
            || !Enum.IsDefined(typeof(FestivalExecutionPhase), state.phase)
            || state.itemCosts == null
            || state.attendance == null
            || state.venueCells == null
            || state.materialSourceStackIds == null)
            return false;
        FestivalDefinitionSO festival;
        try
        {
            festival = festivals.Require(state.festivalId);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or KeyNotFoundException)
        {
            return false;
        }
        if (!string.Equals(
                state.occurrenceId,
                FestivalExecutionRules.OccurrenceId(
                    festival.StableId,
                    state.occurrenceYear),
                StringComparison.Ordinal)
            || state.festivalAbsoluteDay != FestivalExecutionRules
                .FestivalAbsoluteDay(festival, state.occurrenceYear)
            || state.deadlineAbsoluteDay != state.festivalAbsoluteDay
            || state.deadlineHour != FestivalExecutionRules.StartHour
            || state.startHour != FestivalExecutionRules.StartHour)
            return false;
        bool declined = state.phase == FestivalExecutionPhase.Declined;
        if (declined)
            return !state.inputOwnerActive
                && !state.materialReceiptPending
                && state.cleanupComplete;
        if (state.phase == FestivalExecutionPhase.None
            || state.requiredPreparationWork <= 0f
            || state.plannedDurationSeconds <= 0f
            || state.completedPreparationWork < 0f
            || state.elapsedFestivalSeconds < 0f
            || state.elapsedFestivalSeconds > state.plannedDurationSeconds + 0.01f
            || state.venueCapacity < state.attendance.Count
            || state.attendance.Count == 0
            || state.venueCells.Count < state.attendance.Count
            || state.attendance.Select(value => value.characterId)
                .Distinct(StringComparer.Ordinal).Count() != state.attendance.Count
            || state.itemCosts.Any(value => value == null
                || !EconomyProjectInputOwnerAuthority.IsCanonical(value.itemId)
                || value.quantity <= 0)
            || state.attendance.Any(value => value == null
                || !new CharacterId(value.characterId).IsValid
                || value.attendedSeconds < 0f
                || value.attendedSeconds > state.plannedDurationSeconds + 0.01f))
            return false;
        IReadOnlyDictionary<string, int> authored = MaterialRequirements(festival);
        IReadOnlyDictionary<string, int> saved = MaterialRequirements(state);
        if (authored.Count != saved.Count
            || authored.Any(pair => !saved.TryGetValue(pair.Key, out int quantity)
                || quantity != pair.Value))
            return false;
        if (state.inputOwnerActive
            && (!EconomyProjectInputOwnerAuthority.IsCanonical(
                    state.materialDestinationId)
                || state.inputCapacityGrams <= 0L
                || state.inputMassAuthorityRevision <= 0L
                || string.IsNullOrWhiteSpace(state.inputCapacityFingerprint)))
            return false;
        return !state.materialReceiptPending
            || (state.materialQuantity > 0
                && state.materialMassGrams > 0L
                && EconomyProjectInputOwnerAuthority.IsCanonical(
                    state.materialOperationId)
                && EconomyProjectInputOwnerAuthority.IsCanonical(
                    state.materialCommitId));
    }

    private bool TryResolveScheduledRoster(
        FestivalDefinitionSO festival,
        IReadOnlyCollection<CharacterId> submittedParticipantIds,
        FestivalExecutionPreview preview,
        out CharacterId[] participants,
        out SocietyVenueCandidateSnapshot venue,
        out string failureReason)
    {
        participants = Array.Empty<CharacterId>();
        venue = null;
        failureReason = string.Empty;
        CharacterId[] rawSubmission = (submittedParticipantIds
            ?? Array.Empty<CharacterId>())
            .ToArray();
        bool hasExplicitSubmission = rawSubmission.Length > 0;
        CharacterId[] submitted = rawSubmission
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

        // The current general alert has no participant picker. An empty request
        // therefore retains its legacy meaning: use the preview's minimum roster.
        CharacterId[] candidates = !hasExplicitSubmission
            ? preview.ParticipantIds.ToArray()
            : submitted;
        participants = EligibleSubmittedParticipants(festival, candidates);
        if (participants.Length < festival.minimumParticipants)
        {
            failureReason = !hasExplicitSubmission
                ? string.IsNullOrWhiteSpace(preview.FailureReason)
                    ? "문화·신분·필수 업무·생존 조건을 충족하는 최소 축제 참가자가 부족합니다."
                    : preview.FailureReason
                : "제출 참가자 중 문화·신분·필수 업무·생존 조건을 충족하는 인원이 부족합니다.";
            return false;
        }

        if (!venues.TryFindFestivalVenues(
                festival.venueRequirements,
                participants,
                participants.Length,
                out IReadOnlyList<SocietyVenueCandidateSnapshot> candidatesByVenue,
                out failureReason))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "제출 참가자 전원이 실제 접근·수용 가능한 축제 장소가 없습니다."
                : failureReason;
            return false;
        }
        venue = candidatesByVenue.FirstOrDefault();
        if (venue == null || venue.EventCells.Count < participants.Length)
        {
            failureReason = "제출 참가자에게 실제 행사 칸을 배정할 수 있는 축제 장소가 없습니다.";
            return false;
        }
        return true;
    }

    private CharacterId[] EligibleSubmittedParticipants(
        FestivalDefinitionSO festival,
        IEnumerable<CharacterId> participantIds) => (participantIds
            ?? Array.Empty<CharacterId>())
        .Where(value => value.IsValid)
        .Distinct()
        .OrderBy(value => value.Value, StringComparer.Ordinal)
        .Select(FindActor)
        .Where(actor => IsFestivalEligible(actor, festival))
        .Where(actor => !IsCriticalDuty(actor))
        .Select(CharacterPersistentIdentity.Require)
        .ToArray();

    private IEnumerable<CharacterActor> GetEligibleActors(FestivalDefinitionSO festival) =>
        characters.Characters
            .Where(actor => IsFestivalEligible(actor, festival));

    private bool IsFestivalEligible(
        CharacterActor actor,
        FestivalDefinitionSO festival)
    {
        if (!IsActive(actor) || festival == null)
            return false;
        CharacterSettlementStanding standing = standings.GetStanding(actor);
        bool roleEligible = actor.IsOwner
            || standing is CharacterSettlementStanding.Resident
                or CharacterSettlementStanding.Minion
            || festival.allowsVisitors
                && standing == CharacterSettlementStanding.Visitor;
        return roleEligible && IsCultureEligible(actor, festival);
    }

    private bool IsCultureEligible(
        CharacterActor actor,
        FestivalDefinitionSO festival)
    {
        if (actor == null || festival == null)
            return false;
        string requiredCulture = festival.cultureId?.Trim() ?? string.Empty;
        if (requiredCulture.Length == 0)
            return true;
        return CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && narratives.TryGet(id, out CharacterNarrativeSnapshot narrative)
            && string.Equals(
                narrative.CultureId.Value,
                requiredCulture,
                StringComparison.Ordinal);
    }

    private bool IsCriticalDuty(CharacterActor actor)
    {
        if (!IsActive(actor) || combat.IsInCombatStance(actor))
            return true;
        if (actor.Brain?.ExternalIntentKind
            >= CharacterActionIntentKind.EmergencyNeed)
            return true;
        if (!actor.TryGetAbility(out AbilityWork work))
            return false;
        if (work.HasEmergencyResponseWorkGateForDiagnostics)
            return true;
        WorkTypeId type = work.AssignedWorkTypeId;
        return work.isWorking && (type == BuiltInWorkTypeIds.Guard
            || type == BuiltInWorkTypeIds.Rescue
            || type == BuiltInWorkTypeIds.Treat
            || type == BuiltInWorkTypeIds.Surgery
            || type == BuiltInWorkTypeIds.ThreatMitigation);
    }

    private static bool IsActive(CharacterActor actor) => actor != null
        && !actor.IsDead
        && !actor.IsOnExpedition
        && actor.CurrentLifecycleState == CharacterLifecycleState.Active;

    private CharacterActor FindActor(CharacterId id) => characters.Characters
        .FirstOrDefault(actor => actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId candidate)
            && candidate.Equals(id));

    private int CountAssigned(string destinationId, string itemId)
    {
        int stacks = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.DestinationId, destinationId,
                    StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer)
            .Sum(stack => stack.TotalQuantity);
        return checked(stacks + items.GetCommittedHaulDeliveryQuantity(
            destinationId,
            itemId));
    }

    private int CountArrived(string destinationId, string itemId) =>
        items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.DestinationId, destinationId,
                    StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityBuffer)
            .Sum(stack => stack.TotalQuantity);

    private static IReadOnlyDictionary<string, int> MaterialRequirements(
        FestivalDefinitionSO festival) => (festival.requiredItems
            ?? new List<FestivalItemRequirement>())
        .Where(value => value != null
            && !string.IsNullOrWhiteSpace(value.itemDefinitionId)
            && value.amount > 0)
        .GroupBy(value => value.itemDefinitionId.Trim(), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Sum(value => value.amount),
            StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, int> MaterialRequirements(
        FestivalExecutionOccurrenceSaveData state) => (state?.itemCosts
            ?? new List<FestivalExecutionItemSaveData>())
        .Where(value => value != null
            && !string.IsNullOrWhiteSpace(value.itemId)
            && value.quantity > 0)
        .GroupBy(value => value.itemId.Trim(), StringComparer.Ordinal)
        .OrderBy(group => group.Key, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Sum(value => value.quantity),
            StringComparer.Ordinal);

    private static CharacterId[] ParticipantIds(
        FestivalExecutionOccurrenceSaveData state) => state.attendance
        .Select(value => new CharacterId(value.characterId))
        .OrderBy(value => value.Value, StringComparer.Ordinal)
        .ToArray();

    private static FestivalPreparedOrder BuildPreparedOrder(
        FestivalExecutionOccurrenceSaveData state) => new()
    {
        ActionId = state.actionId,
        OccurrenceId = state.occurrenceId,
        FestivalId = state.festivalId,
        FacilityInstanceId = state.venueFacilityInstanceId,
        AbsoluteDay = state.festivalAbsoluteDay,
        DeadlineAbsoluteDay = state.deadlineAbsoluteDay,
        DeadlineHour = state.deadlineHour,
        RequiredPreparationWork = state.requiredPreparationWork,
        PlannedDurationSeconds = state.plannedDurationSeconds,
        VenueCapacity = state.venueCapacity,
        ParticipantIds = Array.AsReadOnly(ParticipantIds(state)),
        ItemCosts = MaterialRequirements(state)
    };

    private static void RecordReceipt(
        FestivalExecutionOccurrenceSaveData state,
        PhysicalItemBatchDispositionReceipt receipt)
    {
        state.materialReceiptPending = true;
        state.materialOperationId = receipt.OperationId;
        state.materialReasonCode = receipt.ReasonCode;
        state.materialRequestFingerprint = receipt.RequestFingerprint;
        state.materialCommitId = receipt.CommitId;
        state.materialQuantity = receipt.Quantity;
        state.materialMassGrams = receipt.InputMassGrams;
        state.materialSourceStackIds = receipt.SourceStackIds.ToList();
    }

    private static void ClearReceipt(FestivalExecutionOccurrenceSaveData state)
    {
        state.materialReceiptPending = false;
        state.materialOperationId = string.Empty;
        state.materialReasonCode = string.Empty;
        state.materialRequestFingerprint = string.Empty;
        state.materialCommitId = string.Empty;
        state.materialSourceStackIds.Clear();
    }

    public static bool ReceiptMatches(
        FestivalExecutionOccurrenceSaveData state,
        PhysicalItemBatchDispositionReceipt receipt,
        bool requireSavedReceipt)
    {
        if (receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                FestivalExecutionRules.MaterialOperationId(state.occurrenceId),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                FestivalExecutionRules.MaterialReasonCode,
                StringComparison.Ordinal)
            || receipt.Quantity != MaterialRequirements(state).Values.Sum())
            return false;
        return !requireSavedReceipt
            || state.materialReceiptPending
                && string.Equals(receipt.OperationId, state.materialOperationId,
                    StringComparison.Ordinal)
                && string.Equals(receipt.ReasonCode, state.materialReasonCode,
                    StringComparison.Ordinal)
                && string.Equals(receipt.RequestFingerprint,
                    state.materialRequestFingerprint, StringComparison.Ordinal)
                && string.Equals(receipt.CommitId, state.materialCommitId,
                    StringComparison.Ordinal)
                && receipt.Quantity == state.materialQuantity
                && receipt.InputMassGrams == state.materialMassGrams
                && receipt.SourceStackIds.SequenceEqual(
                    state.materialSourceStackIds,
                    StringComparer.Ordinal);
    }

    private void CompleteParticipantFasts(IEnumerable<CharacterId> participantIds)
    {
        if (ritualFasting == null)
            return;
        HashSet<CharacterId> participantSet = new(
            participantIds ?? Array.Empty<CharacterId>());
        foreach (CharacterActor actor in characters.Characters
                     .Where(value => value != null
                         && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
                         && participantSet.Contains(id))
                     .OrderBy(value => value.Identity?.PersistentId,
                         StringComparer.Ordinal))
            ritualFasting.TryComplete(actor, out _);
    }

    private static string RuntimeKey(string occurrenceId, CharacterId characterId) =>
        occurrenceId + "|" + characterId.Value;

    private static string DescribeGrade(FestivalResolutionGrade grade) => grade switch
    {
        FestivalResolutionGrade.Success => "성공",
        FestivalResolutionGrade.Partial => "부분 성공",
        _ => "실패"
    };
}
