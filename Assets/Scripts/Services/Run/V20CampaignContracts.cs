using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;


public enum RunProgressionPhase
{
    Founding,
    LegacyAge,
    EndlessAge
}

public enum GuestRequestDeliveryPhase
{
    None = 0,
    AwaitingVenue = 1,
    Delivering = 2,
    ReleasePending = 3,
    PhysicalCommitted = 4,
    RewardPublished = 5
}

public enum ObservedMealIncidentCaptureDisposition
{
    NotEligible = 0,
    Created = 1,
    ExactReplay = 2,
    RejectedPayloadConflict = 3
}

public enum ObservedIncidentSourceKind
{
    Meal = 0,
    Shoplifting = 1,
    Combat = 2,
    SocialConflict = 3
}

public enum ObservedLifeEventSourceKind
{
    Funeral = 1,
    LineageCompression = 2,
    ProductionDeclaredLoss = 3,
    LastMentorshipLesson = 4,
    ProficiencyPromotion = 5
}

internal readonly struct ObservedFuneralLifeEventReceipt
{
    private const string PayloadVersion = "funeral@1";

    internal ObservedFuneralLifeEventReceipt(
        string sourceOperationId,
        CharacterId deceasedCharacterId,
        string facilityInstanceId,
        int absoluteDay,
        int generation,
        IEnumerable<CharacterId> participantCharacterIds)
    {
        SourceOperationId = RequireCanonical(sourceOperationId, "funeral source operation");
        if (!deceasedCharacterId.IsValid)
            throw new ArgumentException("Funeral receipt requires a valid deceased character.");
        DeceasedCharacterId = deceasedCharacterId;
        FacilityInstanceId = RequireCanonical(facilityInstanceId, "funeral facility");
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (generation < 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
        AbsoluteDay = absoluteDay;
        Generation = generation;
        ParticipantCharacterIds = (participantCharacterIds
                ?? throw new ArgumentNullException(nameof(participantCharacterIds)))
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (ParticipantCharacterIds.Count == 0)
            throw new ArgumentException("Funeral receipt requires an actual living participant.");
    }

    internal string SourceOperationId { get; }
    internal CharacterId DeceasedCharacterId { get; }
    internal string FacilityInstanceId { get; }
    internal int AbsoluteDay { get; }
    internal int Generation { get; }
    internal IReadOnlyList<CharacterId> ParticipantCharacterIds { get; }
    internal string CanonicalPayload => string.Join("|", new[]
    {
        PayloadVersion,
        Encode(SourceOperationId),
        Encode(DeceasedCharacterId.Value),
        Encode(FacilityInstanceId),
        AbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Generation.ToString(CultureInfo.InvariantCulture),
        string.Join(",", ParticipantCharacterIds.Select(value => Encode(value.Value)))
    });

    internal static bool TryParse(
        string payload,
        out ObservedFuneralLifeEventReceipt receipt)
    {
        receipt = default;
        string[] fields = (payload ?? string.Empty).Split('|');
        if (fields.Length != 7
            || !string.Equals(fields[0], PayloadVersion, StringComparison.Ordinal)
            || !TryDecode(fields[1], out string operationId)
            || !TryDecode(fields[2], out string deceasedId)
            || !TryDecode(fields[3], out string facilityId)
            || !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out int day)
            || !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out int generation))
            return false;
        try
        {
            CharacterId[] participants = fields[6]
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => TryDecode(value, out string decoded)
                    ? new CharacterId(decoded)
                    : default)
                .ToArray();
            if (participants.Any(value => !value.IsValid)) return false;
            receipt = new ObservedFuneralLifeEventReceipt(
                operationId,
                new CharacterId(deceasedId),
                facilityId,
                day,
                generation,
                participants);
            return string.Equals(
                receipt.CanonicalPayload,
                payload,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string RequireCanonical(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{label} ID must be canonical.");
        return value;
    }
    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));
    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return string.Equals(Encode(decoded), value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}

internal readonly struct ObservedLineageCompressionLifeEventReceipt
{
    private const string PayloadVersion = "lineage@1";

    internal ObservedLineageCompressionLifeEventReceipt(
        CharacterTombstoneSaveData tombstone,
        int archiveAbsoluteDay,
        LineageSummarySaveData committedSummary)
    {
        if (tombstone == null || committedSummary == null)
            throw new ArgumentNullException(tombstone == null
                ? nameof(tombstone)
                : nameof(committedSummary));
        CharacterId characterId = new(tombstone.characterId);
        CharacterSpeciesId speciesId = new(tombstone.phenotypeSpeciesId);
        if (!characterId.IsValid || !speciesId.IsValid
            || tombstone.birthAbsoluteDay < 0
            || tombstone.deathAbsoluteDay < 1
            || tombstone.deathAbsoluteDay < tombstone.birthAbsoluteDay
            || tombstone.famous
            || tombstone.generation < 0
            || archiveAbsoluteDay < 1
            || archiveAbsoluteDay - tombstone.deathAbsoluteDay
                <= CharacterKinshipAggregate.RecentDeathDays
            || committedSummary.archivedCharacterCount < 1
            || committedSummary.generation != tombstone.generation
            || committedSummary.earliestBirthDay < 0
            || committedSummary.latestDeathDay
                < committedSummary.earliestBirthDay
            || committedSummary.earliestBirthDay > tombstone.birthAbsoluteDay
            || committedSummary.latestDeathDay < tombstone.deathAbsoluteDay)
            throw new ArgumentException("Lineage compression receipt is inconsistent with its committed tombstone.");
        string expectedHousehold = string.IsNullOrWhiteSpace(tombstone.householdId)
            ? "household:unassigned"
            : tombstone.householdId;
        if (!string.Equals(
                expectedHousehold,
                committedSummary.householdId,
                StringComparison.Ordinal))
            throw new ArgumentException("Lineage compression receipt has a mismatched summary owner.");
        CharacterId = characterId;
        PhenotypeSpeciesId = speciesId;
        HouseholdId = tombstone.householdId ?? string.Empty;
        BirthAbsoluteDay = tombstone.birthAbsoluteDay;
        DeathAbsoluteDay = tombstone.deathAbsoluteDay;
        Famous = tombstone.famous;
        Generation = tombstone.generation;
        ArchiveAbsoluteDay = archiveAbsoluteDay;
        SummaryHouseholdId = committedSummary.householdId;
        SummaryArchivedCharacterCount = committedSummary.archivedCharacterCount;
        SummaryEarliestBirthDay = committedSummary.earliestBirthDay;
        SummaryLatestDeathDay = committedSummary.latestDeathDay;
    }

    internal CharacterId CharacterId { get; }
    internal CharacterSpeciesId PhenotypeSpeciesId { get; }
    internal string HouseholdId { get; }
    internal int BirthAbsoluteDay { get; }
    internal int DeathAbsoluteDay { get; }
    internal bool Famous { get; }
    internal int Generation { get; }
    internal int ArchiveAbsoluteDay { get; }
    internal string SummaryHouseholdId { get; }
    internal int SummaryArchivedCharacterCount { get; }
    internal int SummaryEarliestBirthDay { get; }
    internal int SummaryLatestDeathDay { get; }
    internal string SourceOperationId => $"kinship-lineage:{CharacterId.Value}";
    internal string CanonicalPayload => string.Join("|", new[]
    {
        PayloadVersion,
        Encode(CharacterId.Value),
        Encode(PhenotypeSpeciesId.Value),
        Encode(HouseholdId),
        BirthAbsoluteDay.ToString(CultureInfo.InvariantCulture),
        DeathAbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Famous ? "1" : "0",
        Generation.ToString(CultureInfo.InvariantCulture),
        ArchiveAbsoluteDay.ToString(CultureInfo.InvariantCulture),
        Encode(SummaryHouseholdId),
        SummaryArchivedCharacterCount.ToString(CultureInfo.InvariantCulture),
        SummaryEarliestBirthDay.ToString(CultureInfo.InvariantCulture),
        SummaryLatestDeathDay.ToString(CultureInfo.InvariantCulture)
    });

    internal static bool TryParse(
        string payload,
        out ObservedLineageCompressionLifeEventReceipt receipt)
    {
        receipt = default;
        string[] fields = (payload ?? string.Empty).Split('|');
        if (fields.Length != 13
            || !string.Equals(fields[0], PayloadVersion, StringComparison.Ordinal)
            || !TryDecode(fields[1], out string characterId)
            || !TryDecode(fields[2], out string speciesId)
            || !TryDecode(fields[3], out string householdId)
            || !int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out int birthDay)
            || !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out int deathDay)
            || fields[6] != "0" && fields[6] != "1"
            || !int.TryParse(fields[7], NumberStyles.None, CultureInfo.InvariantCulture, out int generation)
            || !int.TryParse(fields[8], NumberStyles.None, CultureInfo.InvariantCulture, out int archiveDay)
            || !TryDecode(fields[9], out string summaryHouseholdId)
            || !int.TryParse(fields[10], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            || !int.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out int earliest)
            || !int.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out int latest))
            return false;
        try
        {
            receipt = new ObservedLineageCompressionLifeEventReceipt(
                new CharacterTombstoneSaveData
                {
                    characterId = characterId,
                    phenotypeSpeciesId = speciesId,
                    householdId = householdId,
                    birthAbsoluteDay = birthDay,
                    deathAbsoluteDay = deathDay,
                    famous = fields[6] == "1",
                    generation = generation
                },
                archiveDay,
                new LineageSummarySaveData
                {
                    householdId = summaryHouseholdId,
                    generation = generation,
                    archivedCharacterCount = count,
                    earliestBirthDay = earliest,
                    latestDeathDay = latest
                });
            return string.Equals(
                receipt.CanonicalPayload,
                payload,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static string Encode(string value) => Convert.ToBase64String(
        Encoding.UTF8.GetBytes(value ?? string.Empty));
    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            return string.Equals(Encode(decoded), value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}

public enum ObservedIncidentResponsePhase
{
    None = 0,
    Accepted = 1,
    ExternalOperationStarted = 2,
    ReceiptCommitted = 3,
    Failed = 4,
    Cancelled = 5,
    EffectsPublished = 6
}

public readonly struct ObservedMealIncidentSnapshot
{
    public ObservedMealIncidentSnapshot(
        ConsumableOperationId operationId,
        CharacterId targetCharacterId,
        ItemDefinitionId itemDefinitionId,
        ItemStackId itemStackId,
        BuildingInstanceId facilityInstanceId,
        bool fieldMeal,
        CoreGridCell location,
        int absoluteDay,
        bool observedPolicyViolation,
        bool observedDowned,
        bool observedDead,
        bool observedContaminated = false)
    {
        OperationId = operationId;
        TargetCharacterId = targetCharacterId;
        ItemDefinitionId = itemDefinitionId;
        ItemStackId = itemStackId;
        FacilityInstanceId = facilityInstanceId;
        FieldMeal = fieldMeal;
        Location = location;
        AbsoluteDay = absoluteDay;
        ObservedPolicyViolation = observedPolicyViolation;
        ObservedDowned = observedDowned;
        ObservedDead = observedDead;
        ObservedContaminated = observedContaminated;
    }

    public ConsumableOperationId OperationId { get; }
    public CharacterId TargetCharacterId { get; }
    public ItemDefinitionId ItemDefinitionId { get; }
    public ItemStackId ItemStackId { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public bool FieldMeal { get; }
    public CoreGridCell Location { get; }
    public int AbsoluteDay { get; }
    public bool ObservedPolicyViolation { get; }
    public bool ObservedDowned { get; }
    public bool ObservedDead { get; }
    public bool ObservedContaminated { get; }
}

public readonly struct ObservedShopliftingIncidentSnapshot
{
    public ObservedShopliftingIncidentSnapshot(
        string operationId,
        string sourceOperationId,
        CharacterId targetCharacterId,
        ItemDefinitionId itemDefinitionId,
        int saleItemId,
        string itemInstanceId,
        string sourceStackId,
        int quantity,
        long unitMassGrams,
        string componentFingerprint,
        BuildingInstanceId facilityInstanceId,
        CoreGridCell location,
        int absoluteDay,
        int lossValue)
    {
        OperationId = operationId ?? string.Empty;
        SourceOperationId = sourceOperationId ?? string.Empty;
        TargetCharacterId = targetCharacterId;
        ItemDefinitionId = itemDefinitionId;
        SaleItemId = saleItemId;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        SourceStackId = sourceStackId ?? string.Empty;
        Quantity = quantity;
        UnitMassGrams = unitMassGrams;
        ComponentFingerprint = componentFingerprint ?? string.Empty;
        FacilityInstanceId = facilityInstanceId;
        Location = location;
        AbsoluteDay = absoluteDay;
        LossValue = lossValue;
    }

    public string OperationId { get; }
    public string SourceOperationId { get; }
    public CharacterId TargetCharacterId { get; }
    public ItemDefinitionId ItemDefinitionId { get; }
    public int SaleItemId { get; }
    public string ItemInstanceId { get; }
    public string SourceStackId { get; }
    public int Quantity { get; }
    public long UnitMassGrams { get; }
    public string ComponentFingerprint { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public CoreGridCell Location { get; }
    public int AbsoluteDay { get; }
    public int LossValue { get; }
}

public readonly struct ObservedBrawlIncidentSnapshot
{
    public ObservedBrawlIncidentSnapshot(
        string attackOperationId,
        CharacterId targetCharacterId,
        CharacterId otherParticipantCharacterId,
        bool customerWasAttacker,
        float actualDamage,
        BuildingInstanceId facilityInstanceId,
        CoreGridCell location,
        int absoluteDay)
    {
        AttackOperationId = attackOperationId ?? string.Empty;
        TargetCharacterId = targetCharacterId;
        OtherParticipantCharacterId = otherParticipantCharacterId;
        CustomerWasAttacker = customerWasAttacker;
        ActualDamage = actualDamage;
        FacilityInstanceId = facilityInstanceId;
        Location = location;
        AbsoluteDay = absoluteDay;
    }

    public string AttackOperationId { get; }
    public CharacterId TargetCharacterId { get; }
    public CharacterId OtherParticipantCharacterId { get; }
    public bool CustomerWasAttacker { get; }
    public float ActualDamage { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public CoreGridCell Location { get; }
    public int AbsoluteDay { get; }
}

public readonly struct ObservedCulturalConflictIncidentSnapshot
{
    public ObservedCulturalConflictIncidentSnapshot(
        string operationId,
        CharacterId instigatorCharacterId,
        CharacterId targetCharacterId,
        CharacterId customerCharacterId,
        SpeciesCultureId instigatorCultureId,
        SpeciesCultureId targetCultureId,
        BuildingInstanceId facilityInstanceId,
        CoreGridCell location,
        int absoluteDay)
    {
        OperationId = operationId ?? string.Empty;
        InstigatorCharacterId = instigatorCharacterId;
        TargetCharacterId = targetCharacterId;
        CustomerCharacterId = customerCharacterId;
        InstigatorCultureId = instigatorCultureId;
        TargetCultureId = targetCultureId;
        FacilityInstanceId = facilityInstanceId;
        Location = location;
        AbsoluteDay = absoluteDay;
    }

    public string OperationId { get; }
    public CharacterId InstigatorCharacterId { get; }
    public CharacterId TargetCharacterId { get; }
    public CharacterId CustomerCharacterId { get; }
    public SpeciesCultureId InstigatorCultureId { get; }
    public SpeciesCultureId TargetCultureId { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public CoreGridCell Location { get; }
    public int AbsoluteDay { get; }
}

public readonly struct ObservedMealIncidentCaptureResult
{
    public ObservedMealIncidentCaptureResult(
        ObservedMealIncidentCaptureDisposition disposition,
        ServiceIncidentKind incidentKind,
        string occurrenceInstanceId,
        DomainFailure failure)
    {
        Disposition = disposition;
        IncidentKind = incidentKind;
        OccurrenceInstanceId = occurrenceInstanceId ?? string.Empty;
        Failure = failure;
    }

    public ObservedMealIncidentCaptureDisposition Disposition { get; }
    public ServiceIncidentKind IncidentKind { get; }
    public string OccurrenceInstanceId { get; }
    public DomainFailure Failure { get; }
    public bool Created => Disposition == ObservedMealIncidentCaptureDisposition.Created;
}

[Serializable]
public sealed class ObservedMealIncidentEvidenceSaveData
{
    public const int CurrentVersion = 4;
    public int version = CurrentVersion;
    public ObservedIncidentSourceKind sourceKind;
    public string operationId = string.Empty;
    public string sourceOperationId = string.Empty;
    public string occurrenceInstanceId = string.Empty;
    public string definitionId = string.Empty;
    public ServiceIncidentKind incidentKind;
    public string targetCharacterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string itemStackId = string.Empty;
    public int saleItemId;
    public string itemInstanceId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;
    public long unitMassGrams;
    public string componentFingerprint = string.Empty;
    public int lossValue;
    public string otherParticipantCharacterId = string.Empty;
    public string instigatorCharacterId = string.Empty;
    public string instigatorCultureId = string.Empty;
    public string targetCultureId = string.Empty;
    public bool customerWasAttacker;
    public float actualDamage;
    public string facilityInstanceId = string.Empty;
    public bool fieldMeal;
    public int locationX;
    public int locationY;
    public int absoluteDay;
    public bool observedPolicyViolation;
    public bool observedDowned;
    public bool observedDead;
    public string observedCause = string.Empty;
}

[Serializable]
public sealed class ObservedIncidentResponseSaveData
{
    public ObservedIncidentResponsePhase phase;
    public string choiceId = string.Empty;
    public string operationId = string.Empty;
    public string externalOperationId = string.Empty;
    public string receiptId = string.Empty;
    public string failureReason = string.Empty;
}

[Serializable]
public sealed class GuestRequestDeliverySaveData
{
    public GuestRequestDeliveryPhase phase;
    public string acceptedActionId = string.Empty;
    public string venueFacilityInstanceId = string.Empty;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public bool inputOwnerActive;
    public long inputCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public string operationId = string.Empty;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public List<string> sourceStackIds = new();
    public int committedQuantity;
    public long committedMassGrams;
    public string terminalResolutionId = string.Empty;
    public string failureReason = string.Empty;
}

[Serializable]
public sealed class V20ActiveEventSaveData
{
    public string instanceId = string.Empty;
    public string definitionId = string.Empty;
    public int startedAbsoluteDay;
    public int deadlineAbsoluteDay;
    public int generation;
    public string selectedChoiceId = string.Empty;
    public bool resolved;
    public uint deterministicRoll;
    public string resolutionId = string.Empty;
    public string contextFactionId = string.Empty;
    public List<string> participantCharacterIds = new();
    public ExperienceEventRiskTier riskTier;
    public string riskReason = string.Empty;
    public string terminalActionLabel = string.Empty;
    public string terminalOutcomeNarrative = string.Empty;
    public List<V20ContentEffectKind> terminalEffectKinds = new();
    public bool hasObservedMealIncident;
    public ObservedMealIncidentEvidenceSaveData observedMealIncident;
    public ObservedIncidentResponseSaveData observedIncidentResponse = new();
    public GuestRequestDeliverySaveData guestDelivery = new();
    public SeasonalWildlifeArrivalSaveData seasonalWildlifeArrival = new();
    public SeasonalDriftCargoSaveData seasonalDriftCargo = new();
    public SeasonalManaLightningSaveData seasonalManaLightning = new();
    public SeasonalSpecialExpeditionSaveData seasonalSpecialExpedition = new();
}

[Serializable]
public sealed class V20EventCooldownSaveData
{
    public string definitionId = string.Empty;
    public int availableAbsoluteDay;
}

[Serializable]
public sealed class SeasonalEventWorldSaveData
{
    public const int CurrentVersion = 6;
    public int version = CurrentVersion;
    public List<V20ActiveEventSaveData> activeEvents = new();
    public List<string> completedEventIds = new();
    public int cycle;
    public int lastEvaluationAbsoluteDay = -1;
}

[Serializable]
public sealed class V20WorkDelaySaveData
{
    public string scopeId = string.Empty;
    public int untilAbsoluteDayExclusive;
}

[Serializable]
public sealed class ObservedLifeEventReceiptSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public ObservedLifeEventSourceKind sourceKind;
    public string sourceOperationId = string.Empty;
    public string canonicalPayload = string.Empty;
    public string occurrenceInstanceId = string.Empty;
}

[Serializable]
public sealed class SocietyEventWorldSaveData
{
    public const int CurrentVersion = 9;
    public int version = CurrentVersion;
    public List<V20ActiveEventSaveData> activeEvents = new();
    public List<V20ActiveEventSaveData> recentResolvedEvents = new();
    public List<string> completedOnceEventIds = new();
    public List<string> recurrenceKeys = new();
    public List<V20EventCooldownSaveData> cooldowns = new();
    public List<V20WorkDelaySaveData> workDelays = new();
    public List<ObservedMealIncidentEvidenceSaveData>
        successfulIncidentOperations = new();
    public List<ObservedLifeEventReceiptSaveData>
        successfulLifeEventOperations = new();
    public int lastEvaluationAbsoluteDay = -1;
}

[Serializable]
public sealed class FactionCampaignStateSaveData
{
    public string factionId = string.Empty;
    public int rapport;
    public int grievance;
    public int obligationTokens;
    public int currentChapter;
    public string activeContractId = string.Empty;
    public string activeContractOccurrenceId = string.Empty;
    public string lastSeasonalContractId = string.Empty;
    public string lastSeasonalOccurrenceId = string.Empty;
    public int activeContractDeadlineAbsoluteDay;
    public string activeContractDestinationOwnerId = string.Empty;
    public string activeContractDestinationId = string.Empty;
    public int activeContractDestinationX;
    public int activeContractDestinationY;
    public bool activeContractInputOwnerActive;
    public long activeContractInputCapacityGrams;
    public long activeContractInputMassAuthorityRevision;
    public string activeContractInputCapacityFingerprint = string.Empty;
    public string deliveryContractId = string.Empty;
    public FactionContractDeliveryCommitPhase deliveryCommitPhase;
    public string deliveryOperationId = string.Empty;
    public string deliveryCommitId = string.Empty;
    public List<string> deliverySourceStackIds = new();
    public int deliveryQuantity;
    public long deliveryMassGrams;
    public bool deliveryTerminalCleanupPending;
    public string deliveryFailureReason = string.Empty;
    public List<string> completedContractIds = new();
    public List<string> failedContractIds = new();
    public List<string> majorChoiceFlags = new();
}

[Serializable]
public sealed class FactionCampaignWorldSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<FactionCampaignStateSaveData> factions = new();
}

[Serializable]
public sealed class V20CommittedRunChoiceSaveData
{
    public CommittedRunChoiceKind kind;
    public string ownerId = string.Empty;
    public string definitionId = string.Empty;
    public string instanceId = string.Empty;
    public string choiceId = string.Empty;
    public string operationId = string.Empty;
    public long ordinal;
}

public enum EndlessCrisisLifecyclePhase
{
    None = 0,
    Active = 1,
    AwaitingDirectResponse = 2,
    Recovery = 3
}

[Serializable]
public sealed class EndlessCrisisAxisSaveData
{
    public EndlessCrisisAxis axis;
    public string effectOwnerId = string.Empty;
    public float multiplier = 1f;
}

[Serializable]
public sealed class RunMilestoneWorldSaveData
{
    public const int CurrentVersion = 10;
    public int version = CurrentVersion;
    public RunProgressionPhase phase;
    public int endlessCycle;
    public List<string> completedMilestoneIds = new();
    public List<string> grantedRewardIds = new();
    public List<string> unlockedLandmarkIds = new();
    public List<string> activePressureIds = new();
    public List<string> worldFlags = new();
    public EndlessCrisisLifecyclePhase endlessCrisisPhase;
    public string endlessCrisisInstanceId = string.Empty;
    public List<EndlessCrisisAxisSaveData> endlessCrisisAxes = new();
    public int endlessCrisisStartedAbsoluteDay = -1;
    public int endlessCrisisPressureEndAbsoluteDay = -1;
    public int endlessCrisisLastActualEndAbsoluteDay = -1;
    public int endlessCrisisRecoveryEndAbsoluteDay = -1;
    public int lastEndlessCrisisEvaluationAbsoluteDay = -1;
    public int endlessCrisisAuthoredRecoveryDays;
    public string endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
    public string endlessCrisisDirectResponseOwnerId = string.Empty;
    public int selfSufficiencyStreakDays;
    public int productivityCoverageStreakDays;
    public int lastMilestoneEvaluationAbsoluteDay = -1;
    public int lastAccordSignalSupportAbsoluteDay = -1;
    public string pendingAccordSignalOperationId = string.Empty;
    public string pendingAccordSignalCommitId = string.Empty;
    public string pendingAccordSignalSourceStackId = string.Empty;
    public long pendingAccordSignalMassGrams;
    public List<V20CommittedRunChoiceSaveData> committedChoices = new();
    public long nextCommittedChoiceOrdinal;
    public List<V20ContentResolutionRecoverySaveData>
        pendingContentResolutions = new();
}

public interface IV20CampaignPersistence
{
    SeasonalEventWorldSaveData CaptureSeasonal();
    SocietyEventWorldSaveData CaptureSociety();
    FactionCampaignWorldSaveData CaptureFactions();
    RunMilestoneWorldSaveData CaptureMilestones();
    SeasonalEventAggregateState PrepareSeasonal(SeasonalEventWorldSaveData data);
    SeasonalEventAggregateState PrepareSeasonalRestore(
        SeasonalEventWorldSaveData data);
    SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data);
    FactionCampaignAggregateState PrepareFactions(FactionCampaignWorldSaveData data);
    FactionCampaignAggregateState PrepareFactionsPreflight(
        FactionCampaignWorldSaveData data);
    FactionCampaignAggregateState PrepareFactionsRestore(
        FactionCampaignWorldSaveData data);
    RunMilestoneAggregateState PrepareMilestones(RunMilestoneWorldSaveData data);
    void PublishSeasonal(SeasonalEventAggregateState state);
    void PublishSeasonalRestore(SeasonalEventAggregateState state);
    void PublishSociety(SocietyEventAggregateState state);
    void PublishFactions(FactionCampaignAggregateState state);
    void PublishMilestones(RunMilestoneAggregateState state);
}

public sealed class EndlessCrisisAxisModifierSnapshot
{
    public EndlessCrisisAxisModifierSnapshot(
        EndlessCrisisAxis axis,
        string displayName,
        string effectOwnerId,
        float multiplier)
    {
        Axis = axis;
        DisplayName = displayName ?? string.Empty;
        EffectOwnerId = effectOwnerId ?? string.Empty;
        Multiplier = multiplier;
    }

    public EndlessCrisisAxis Axis { get; }
    public string DisplayName { get; }
    public string EffectOwnerId { get; }
    public float Multiplier { get; }
}

public sealed class EndlessCrisisSnapshot
{
    public EndlessCrisisSnapshot(
        EndlessCrisisLifecyclePhase phase,
        string instanceId,
        IReadOnlyList<EndlessCrisisAxisModifierSnapshot> axes,
        int startedAbsoluteDay,
        int pressureEndAbsoluteDay,
        int lastActualEndAbsoluteDay,
        int recoveryEndAbsoluteDay,
        int lastEvaluationAbsoluteDay,
        int authoredRecoveryDays,
        string pendingInvasionSourceOwnerId,
        string directResponseOwnerId)
    {
        Phase = phase;
        InstanceId = instanceId ?? string.Empty;
        Axes = axes ?? Array.Empty<EndlessCrisisAxisModifierSnapshot>();
        StartedAbsoluteDay = startedAbsoluteDay;
        PressureEndAbsoluteDay = pressureEndAbsoluteDay;
        LastActualEndAbsoluteDay = lastActualEndAbsoluteDay;
        RecoveryEndAbsoluteDay = recoveryEndAbsoluteDay;
        LastEvaluationAbsoluteDay = lastEvaluationAbsoluteDay;
        AuthoredRecoveryDays = authoredRecoveryDays;
        PendingInvasionSourceOwnerId =
            pendingInvasionSourceOwnerId ?? string.Empty;
        DirectResponseOwnerId = directResponseOwnerId ?? string.Empty;
    }

    public EndlessCrisisLifecyclePhase Phase { get; }
    public string InstanceId { get; }
    public IReadOnlyList<EndlessCrisisAxisModifierSnapshot> Axes { get; }
    public int StartedAbsoluteDay { get; }
    public int PressureEndAbsoluteDay { get; }
    public int LastActualEndAbsoluteDay { get; }
    public int RecoveryEndAbsoluteDay { get; }
    public int LastEvaluationAbsoluteDay { get; }
    public int AuthoredRecoveryDays { get; }
    public string PendingInvasionSourceOwnerId { get; }
    public string DirectResponseOwnerId { get; }
    public bool IsPressureActive => Phase == EndlessCrisisLifecyclePhase.Active;
    public bool IsCompound => Axes.Count == 2;
}

public interface IEndlessCrisisQuery
{
    EndlessCrisisSnapshot CurrentEndlessCrisis { get; }
    float GetEndlessCrisisMultiplier(EndlessCrisisAxis axis);
    bool TryGetActiveEndlessCrisisEffectOwner(
        EndlessCrisisAxis axis,
        out string effectOwnerId);
}

public sealed class NeutralEndlessCrisisQuery :
    IEndlessCrisisQuery,
    IEndlessCrisisCommand
{
    public static NeutralEndlessCrisisQuery Instance { get; } = new();
    private NeutralEndlessCrisisQuery() { }
    public EndlessCrisisSnapshot CurrentEndlessCrisis { get; } = new(
        EndlessCrisisLifecyclePhase.None,
        string.Empty,
        Array.Empty<EndlessCrisisAxisModifierSnapshot>(),
        -1,
        -1,
        -1,
        -1,
        -1,
        0,
        string.Empty,
        string.Empty);
    public float GetEndlessCrisisMultiplier(EndlessCrisisAxis axis) => 1f;
    public bool TryGetActiveEndlessCrisisEffectOwner(
        EndlessCrisisAxis axis,
        out string effectOwnerId)
    {
        effectOwnerId = string.Empty;
        return false;
    }
    public IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed) => Array.Empty<string>();
    public EndlessCrisisSnapshot AdvanceEndlessCrisis(int absoluteDay) =>
        CurrentEndlessCrisis;
    public bool TryOwnInvasionCandidate(string effectOwnerId) => false;
    public bool TryBeginOwnedInvasion(
        string effectOwnerId,
        int absoluteDay,
        out string directResponseOwnerId)
    {
        directResponseOwnerId = string.Empty;
        return false;
    }
    public bool TryResolveOwnedInvasion(
        string directResponseOwnerId,
        int absoluteDay) => false;
}

public interface IRunMilestoneQuery
{
    RunProgressionPhase Phase { get; }
    int EndlessCycle { get; }
    IReadOnlyCollection<string> CompletedMilestoneIds { get; }
    IReadOnlyCollection<string> GrantedRewardIds { get; }
    IReadOnlyCollection<string> ActivePressureIds { get; }
    IReadOnlyCollection<string> WorldFlags { get; }
    bool IsLandmarkBuilding(string buildingDefinitionId);
    bool IsLandmarkUnlocked(string buildingDefinitionId);
}

/// <summary>
/// Typed, read-only projection of the gameplay rules granted by completed
/// milestones. Runtime systems consume this contract instead of inspecting
/// authored reward strings themselves.
/// </summary>
public interface IMilestoneGameplayModifierQuery
{
    bool EnemyCounterIntelVisible { get; }
    float ExpeditionTravelTimeMultiplier { get; }
    float FacilityMaintenanceGoldMultiplier { get; }
    float WaterAndFertilizerConsumptionMultiplier { get; }
    int MentorshipDailyXpCap { get; }
    int TemporalStasisWarningDays { get; }
    float ManaTransferLossMultiplier { get; }
    float AutomaticMaintenanceWorkMultiplier { get; }
    bool IsAccordSignalSupportDay(int absoluteDay);
    bool IsAccordSignalSupportActive(int absoluteDay);
    bool HasReward(string milestoneId);
    bool HasPressure(string milestoneId);
}

public sealed class NeutralMilestoneGameplayModifierQuery :
    IMilestoneGameplayModifierQuery
{
    public static readonly NeutralMilestoneGameplayModifierQuery Instance = new();
    private NeutralMilestoneGameplayModifierQuery() { }
    public bool EnemyCounterIntelVisible => false;
    public float ExpeditionTravelTimeMultiplier => 1f;
    public float FacilityMaintenanceGoldMultiplier => 1f;
    public float WaterAndFertilizerConsumptionMultiplier => 1f;
    public int MentorshipDailyXpCap => CareerRules.MaximumDailyMentoringXp;
    public int TemporalStasisWarningDays => 0;
    public float ManaTransferLossMultiplier => 1f;
    public float AutomaticMaintenanceWorkMultiplier => 1f;
    public bool IsAccordSignalSupportDay(int absoluteDay) => false;
    public bool IsAccordSignalSupportActive(int absoluteDay) => false;
    public bool HasReward(string milestoneId) => false;
    public bool HasPressure(string milestoneId) => false;
}

public sealed class RunMilestoneEvaluationSnapshot
{
    public int AbsoluteDay { get; set; }
    public ISet<int> CompletedResearchIds { get; } = new HashSet<int>();
    public ISet<string> WorldFlags { get; } = new HashSet<string>(StringComparer.Ordinal);
    public IDictionary<V20WorldMetricKind, float> WorldMetrics { get; } = new Dictionary<V20WorldMetricKind, float>();
    public IDictionary<string, int> ItemQuantities { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IDictionary<string, int> FacilityCounts { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IDictionary<string, FactionCampaignStateSaveData> Factions { get; } =
        new Dictionary<string, FactionCampaignStateSaveData>(StringComparer.Ordinal);
    public int EligibleCharacterCount { get; set; }
}

public interface IRunMilestoneCommand
{
    IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot);
    int AdvanceEndlessCycle();
    bool TryActivateAccordSignalSupport(int absoluteDay, string sourceStackId);
}

public sealed class V20DailyEventContext
{
    public int AbsoluteDay { get; set; }
    public int RunSeed { get; set; }
    public Season Season { get; set; }
    public string WeatherFrontId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public List<string> ParticipantCharacterIds { get; } = new();
    public HashSet<string> RetirementEligibleParticipantIds { get; } =
        new(StringComparer.Ordinal);
    // Active Exterior incidents retain their own actors. Observing the action
    // must not create a second actionable Society owner for that same actor.
    public HashSet<string> ActiveExteriorIncidentCharacterIds { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, float>> ParticipantContentWeights
    {
        get;
    } = new(StringComparer.Ordinal);
    public RunMilestoneEvaluationSnapshot Requirements { get; } = new();
}

public readonly struct V20ResolvedEventResult
{
    public V20ResolvedEventResult(
        string definitionId,
        string resolutionId,
        IReadOnlyList<V20ContentEffect> effects,
        IReadOnlyList<string> participantCharacterIds = null,
        string contextFactionId = "",
        string instanceId = "",
        string displayName = "",
        ExperienceEventRiskTier riskTier = ExperienceEventRiskTier.None,
        string riskReason = "",
        string terminalActionLabel = "",
        string terminalOutcomeNarrative = "",
        IReadOnlyList<V20ContentEffectKind> terminalEffectKinds = null,
        string observedCause = "",
        string occurrenceInstanceId = "",
        IReadOnlyList<V20EffectApplicationEvidence> applications = null)
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        Effects = effects ?? Array.Empty<V20ContentEffect>();
        ParticipantCharacterIds = participantCharacterIds
            ?? Array.Empty<string>();
        ContextFactionId = contextFactionId?.Trim() ?? string.Empty;
        InstanceId = instanceId?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        RiskTier = riskTier;
        RiskReason = riskReason?.Trim() ?? string.Empty;
        TerminalActionLabel = terminalActionLabel?.Trim() ?? string.Empty;
        TerminalOutcomeNarrative =
            terminalOutcomeNarrative?.Trim() ?? string.Empty;
        ObservedCause = observedCause?.Trim() ?? string.Empty;
        OccurrenceInstanceId = occurrenceInstanceId?.Trim()
            ?? string.Empty;
        Applications = applications == null
            ? Array.Empty<V20EffectApplicationEvidence>()
            : Array.AsReadOnly(applications
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray());
        TerminalEffectKinds = terminalEffectKinds == null
            ? Array.Empty<V20ContentEffectKind>()
            : Array.AsReadOnly(terminalEffectKinds.ToArray());
        if (InstanceId.Length > 0)
        {
            SocietyEventRiskContract.RequireValid(
                RiskTier,
                RiskReason,
                InstanceId);
            V20ContentEffectKind[] expectedKinds = Effects
                .Where(value => value != null)
                .Select(value => value.kind)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            if (DisplayName.Length == 0
                || ResolutionId.Length == 0
                || TerminalActionLabel.Length == 0
                || TerminalOutcomeNarrative.Length == 0
                || !TerminalEffectKinds.SequenceEqual(expectedKinds))
            {
                throw new InvalidOperationException(
                    $"Society result '{InstanceId}' has inconsistent terminal metadata.");
            }
        }
        else if (RiskTier != ExperienceEventRiskTier.None
            || RiskReason.Length > 0
            || TerminalActionLabel.Length > 0
            || TerminalOutcomeNarrative.Length > 0
            || TerminalEffectKinds.Count > 0
            || ObservedCause.Length > 0)
        {
            throw new InvalidOperationException(
                "Non-society content result contains society terminal metadata.");
        }
        if (OccurrenceInstanceId.Length > 0
            && (!string.Equals(
                    OccurrenceInstanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal)
                || OccurrenceInstanceId.Any(char.IsWhiteSpace)))
            throw new InvalidOperationException(
                "Content result occurrence ID must be canonical.");
    }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects { get; }
    public IReadOnlyList<string> ParticipantCharacterIds { get; }
    public string ContextFactionId { get; }
    public string InstanceId { get; }
    public string DisplayName { get; }
    public ExperienceEventRiskTier RiskTier { get; }
    public string RiskReason { get; }
    public string TerminalActionLabel { get; }
    public string TerminalOutcomeNarrative { get; }
    public IReadOnlyList<V20ContentEffectKind> TerminalEffectKinds { get; }
    public string ObservedCause { get; }
    public string OccurrenceInstanceId { get; }
    public IReadOnlyList<V20EffectApplicationEvidence> Applications { get; }
    public bool IsSocietyTerminal => InstanceId.Length > 0;

    public V20ResolvedEventResult WithApplications(
        IReadOnlyList<V20EffectApplicationEvidence> applications) => new(
        DefinitionId,
        ResolutionId,
        Effects,
        ParticipantCharacterIds,
        ContextFactionId,
        InstanceId,
        DisplayName,
        RiskTier,
        RiskReason,
        TerminalActionLabel,
        TerminalOutcomeNarrative,
        TerminalEffectKinds,
        ObservedCause,
        OccurrenceInstanceId,
        applications);
}

public interface ISeasonalEventQuery
{
    IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents { get; }
    float GetWorldWaterRegenerationMultiplier();
    SeasonalPowerCapacityContribution GetPowerCapacityContribution();
    SeasonalPipedWaterContribution GetPipedWaterContribution();
}

public readonly struct SeasonalPowerCapacityContribution
{
    private SeasonalPowerCapacityContribution(bool neutral)
    {
        OccurrenceInstanceId = string.Empty;
        DefinitionId = string.Empty;
        DisplayName = string.Empty;
        RemainingDays = 0;
        AvailableSupplyMultiplier = 1f;
    }

    public SeasonalPowerCapacityContribution(
        string occurrenceInstanceId,
        string definitionId,
        string displayName,
        int remainingDays,
        float availableSupplyMultiplier)
    {
        if (!Canonical(occurrenceInstanceId)
            || !Canonical(definitionId)
            || string.IsNullOrWhiteSpace(displayName)
            || remainingDays < 0
            || !ValidMultiplier(availableSupplyMultiplier)
            || availableSupplyMultiplier >= 1f)
            throw new ArgumentException("Active seasonal power contribution is invalid.");
        OccurrenceInstanceId = occurrenceInstanceId;
        DefinitionId = definitionId;
        DisplayName = displayName.Trim();
        RemainingDays = remainingDays;
        AvailableSupplyMultiplier = availableSupplyMultiplier;
    }

    public bool IsActive => OccurrenceInstanceId?.Length > 0;
    public string OccurrenceInstanceId { get; }
    public string DefinitionId { get; }
    public string DisplayName { get; }
    public int RemainingDays { get; }
    public float AvailableSupplyMultiplier { get; }
    public static SeasonalPowerCapacityContribution None => new(true);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    private static bool ValidMultiplier(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f && value <= 1f;
}

public readonly struct SeasonalPipedWaterContribution
{
    private SeasonalPipedWaterContribution(bool neutral)
    {
        OccurrenceInstanceId = string.Empty;
        DefinitionId = string.Empty;
        DisplayName = string.Empty;
        RemainingDays = 0;
        ThroughputMultiplier = 1f;
        FreezeThresholdC = 0f;
        RecoveryThresholdC = 2f;
    }

    public SeasonalPipedWaterContribution(
        string occurrenceInstanceId,
        string definitionId,
        string displayName,
        int remainingDays,
        float throughputMultiplier,
        float freezeThresholdC,
        float recoveryThresholdC)
    {
        if (!Canonical(occurrenceInstanceId)
            || !Canonical(definitionId)
            || string.IsNullOrWhiteSpace(displayName)
            || remainingDays < 0
            || !ValidMultiplier(throughputMultiplier)
            || throughputMultiplier >= 1f
            || !Finite(freezeThresholdC)
            || !Finite(recoveryThresholdC)
            || recoveryThresholdC <= freezeThresholdC)
            throw new ArgumentException("Active seasonal piped-water contribution is invalid.");
        OccurrenceInstanceId = occurrenceInstanceId;
        DefinitionId = definitionId;
        DisplayName = displayName.Trim();
        RemainingDays = remainingDays;
        ThroughputMultiplier = throughputMultiplier;
        FreezeThresholdC = freezeThresholdC;
        RecoveryThresholdC = recoveryThresholdC;
    }

    public bool IsActive => OccurrenceInstanceId?.Length > 0;
    public string OccurrenceInstanceId { get; }
    public string DefinitionId { get; }
    public string DisplayName { get; }
    public int RemainingDays { get; }
    public float ThroughputMultiplier { get; }
    public float FreezeThresholdC { get; }
    public float RecoveryThresholdC { get; }
    public static SeasonalPipedWaterContribution None => new(true);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    private static bool Finite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool ValidMultiplier(float value) =>
        Finite(value) && value > 0f && value <= 1f;
}

public sealed class NeutralSeasonalEventQuery : ISeasonalEventQuery
{
    public static NeutralSeasonalEventQuery Instance { get; } = new();
    private NeutralSeasonalEventQuery() { }
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents =>
        Array.Empty<V20ActiveEventSaveData>();
    public float GetWorldWaterRegenerationMultiplier() => 1f;
    public SeasonalPowerCapacityContribution GetPowerCapacityContribution() =>
        SeasonalPowerCapacityContribution.None;
    public SeasonalPipedWaterContribution GetPipedWaterContribution() =>
        SeasonalPipedWaterContribution.None;
}

public interface ISocietyEventQuery
{
    IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents { get; }
    IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents { get; }
}

public interface IContentWorkDelayQuery
{
    int GetRemainingDays(string scopeId = "");
    float GetWorkSpeedMultiplier(WorkTypeId workTypeId);
}

public static class ContentWorkDelaySpeedAuthority
{
    public const string Schema = "content-work-delay-speed-authority@1";
    public const float PerActiveDelayMultiplier = 0.8f;
    public const float MinimumMultiplier = 0.5f;
    public const float MaximumMultiplier = 1f;

    public static float Resolve(int activeDelayCount)
    {
        if (activeDelayCount < 0)
            throw new ArgumentOutOfRangeException(nameof(activeDelayCount));
        return activeDelayCount == 0
            ? MaximumMultiplier
            : Math.Max(
                MinimumMultiplier,
                (float)Math.Pow(PerActiveDelayMultiplier, activeDelayCount));
    }
}

public interface IContentWorkDelayCommand
{
    void ApplyWorkDelay(string scopeId, int days, int absoluteDay);
}

public sealed class NeutralContentWorkDelayQuery : IContentWorkDelayQuery
{
    public static readonly NeutralContentWorkDelayQuery Instance = new();
    private NeutralContentWorkDelayQuery() { }
    public int GetRemainingDays(string scopeId = "") => 0;
    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId) => 1f;
}

public interface ISocietyEventCommand
{
    IReadOnlyList<V20ResolvedEventResult> EvaluateDaily(V20DailyEventContext context);
    bool TryResolveSocietyEvent(
        string instanceId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
}

public interface IFactionCampaignQuery
{
    IReadOnlyList<FactionCampaignStateSaveData> Factions { get; }
    bool TryGetFaction(string factionId, out FactionCampaignStateSaveData state);
}

public interface IFactionCampaignCommand
{
    bool TryResolveChapter(
        string factionId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        out string failure);
    bool TryResolveContract(
        string factionId,
        bool success,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta);
}

public interface IEndlessCrisisCommand
{
    IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed);
    EndlessCrisisSnapshot AdvanceEndlessCrisis(int absoluteDay);
    bool TryOwnInvasionCandidate(string effectOwnerId);
    bool TryBeginOwnedInvasion(
        string effectOwnerId,
        int absoluteDay,
        out string directResponseOwnerId);
    bool TryResolveOwnedInvasion(
        string directResponseOwnerId,
        int absoluteDay);
}

public sealed class SeasonalEventAggregateState :
    IDungeonDiscardableRestoreCandidate
{
    internal SeasonalEventWorldSaveData Data = new();
    private ISeasonalRestoreCandidateOwner restoreOwner;

    internal void AttachRestoreOwner(ISeasonalRestoreCandidateOwner owner)
    {
        if (restoreOwner != null)
            throw new InvalidOperationException(
                "Seasonal restore candidate already has an owner.");
        restoreOwner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal void ReleaseRestoreOwner(ISeasonalRestoreCandidateOwner owner)
    {
        if (!ReferenceEquals(restoreOwner, owner))
            throw new InvalidOperationException(
                "Seasonal restore candidate owner mismatch.");
        restoreOwner = null;
    }

    public void Discard()
    {
        ISeasonalRestoreCandidateOwner owner = restoreOwner;
        restoreOwner = null;
        owner?.DiscardSeasonalRestore(this);
    }
}

public interface IGuestRequestDeliveryStateCommand
{
    bool TryAssignGuestRequestDeliveryVenue(
        string instanceId,
        string facilityInstanceId,
        string destinationId,
        UnityEngine.Vector2Int destination,
        EconomyProjectInputOwnerProjection projection,
        out string failure);
    bool TrySetGuestRequestDeliveryFailure(
        string instanceId,
        string failureReason,
        out string failure);
    bool TryRequestGuestRequestDeliveryRelease(
        string instanceId,
        string disposition,
        string reason,
        out string failure);
    bool TryMarkGuestRequestInputOwnerRetired(
        string instanceId,
        out string failure);
    bool TryResumeGuestRequestVenueSelection(
        string instanceId,
        out string failure);
    bool TryRecordGuestRequestDeliveryReceipt(
        string instanceId,
        GuestRequestDeliveryReceipt receipt,
        out string failure);
    bool TryFinalizeGuestRequestDelivery(
        string instanceId,
        out string failure);
}

public interface ISocietyObservedMealIncidentCommand
{
    ObservedMealIncidentCaptureResult CaptureObservedMealIncident(
        ObservedMealIncidentSnapshot evidence,
        V20DailyEventContext context);

    ObservedMealIncidentCaptureResult CaptureObservedShopliftingIncident(
        ObservedShopliftingIncidentSnapshot evidence,
        V20DailyEventContext context);

    ObservedMealIncidentCaptureResult CaptureObservedBrawlIncident(
        ObservedBrawlIncidentSnapshot evidence,
        V20DailyEventContext context);

    ObservedMealIncidentCaptureResult CaptureObservedCulturalConflictIncident(
        ObservedCulturalConflictIncidentSnapshot evidence,
        V20DailyEventContext context);

    bool TryResolveObservedMealIncidentTargetDeath(
        CharacterId targetCharacterId,
        int absoluteDay,
        out V20ResolvedEventResult result,
        out DomainFailure failure);
}

public interface ISocietyObservedIncidentResponseCommand
{
    bool TryRecordObservedIncidentResponseStarted(
        string instanceId,
        string operationId,
        string externalOperationId,
        out string failure);
    bool TryRecordObservedIncidentResponseReceipt(
        string instanceId,
        string operationId,
        string externalOperationId,
        string receiptId,
        out string failure);
    bool TryRecordObservedIncidentResponseFailure(
        string instanceId,
        string operationId,
        string failureReason,
        bool cancelled,
        out string failure);
}

public interface ISocietyIncidentOwnedCharacterIdQuery
{
    IReadOnlyCollection<CharacterId> GetOwnedCustomerIds();
}

internal readonly struct ObservedLifeEventCommitResult
{
    internal ObservedLifeEventCommitResult(
        bool stateChanged,
        V20ResolvedEventResult? resolution)
    {
        StateChanged = stateChanged;
        Resolution = resolution;
    }

    internal bool StateChanged { get; }
    internal V20ResolvedEventResult? Resolution { get; }
}

public sealed class SocietyEventAggregateState
{
    internal SocietyEventWorldSaveData Data = new();
}
public sealed class FactionCampaignAggregateState
{
    internal FactionCampaignWorldSaveData Data = new();
}
public sealed class RunMilestoneAggregateState
{
    internal RunMilestoneWorldSaveData Data = new();
}

internal interface ISeasonalRestoreCandidateOwner
{
    void DiscardSeasonalRestore(SeasonalEventAggregateState candidate);
}
