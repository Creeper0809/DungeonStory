using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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
    public const int CurrentVersion = 5;
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
    public const int CurrentVersion = 7;
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
}

public interface ISocietyEventCatalog
{
    IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IFactionStoryCatalog
{
    IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IWorldEventCatalog
{
    IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    SeasonalWorldEventDefinitionSO Require(string id);
}

public interface IEndingCatalog
{
    IReadOnlyList<EndingDefinitionSO> All { get; }
    bool TryGet(string id, out EndingDefinitionSO definition);
    EndingDefinitionSO Require(string id);
}

public sealed class V20StoryContentCatalog :
    ISocietyEventCatalog,
    IFactionStoryCatalog,
    IWorldEventCatalog,
    IEndingCatalog
{
    private readonly Dictionary<string, V20AuthoredContentSO> byId;
    public V20StoryContentCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        LifeEvents = Exact(content.GetAll<LifeEventDefinitionSO>(), 32, "life events");
        GuestRequests = Exact(content.GetAll<GuestRequestDefinitionSO>(), 13, "guest requests");
        ServiceIncidents = Exact(content.GetAll<ServiceIncidentDefinitionSO>(), 8, "service incidents");
        Arcs = Exact(content.GetAll<FactionArcDefinitionSO>(), 6, "faction arcs");
        Chapters = Exact(content.GetAll<FactionChapterDefinitionSO>(), 36, "faction chapters");
        Contracts = Exact(content.GetAll<FactionContractDefinitionSO>(), 24, "faction contracts");
        SeasonalEvents = Exact(content.GetAll<SeasonalWorldEventDefinitionSO>(), 28, "seasonal events");
        EndlessCrisisPolicyDefinitionSO[] endlessPolicies = (content
                .GetAll<EndlessCrisisPolicyDefinitionSO>()
                ?? Array.Empty<EndlessCrisisPolicyDefinitionSO>())
            .Where(value => value != null)
            .ToArray();
        if (endlessPolicies.Length != 1)
            throw new InvalidOperationException(
                $"Expected one endless crisis policy, found {endlessPolicies.Length}.");
        IReadOnlyList<string> endlessErrors = endlessPolicies[0]
            .ValidateDefinition();
        if (endlessErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" | ", endlessErrors));
        EndlessCrisisPolicy = endlessPolicies[0];
        HashSet<string> weatherFrontIds = (content
                .GetAll<WeatherFrontDefinitionSO>()
                ?? Array.Empty<WeatherFrontDefinitionSO>())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.stableId))
            .Select(value => value.stableId)
            .ToHashSet(StringComparer.Ordinal);
        All = Exact(content.GetAll<EndingDefinitionSO>(), 9, "milestones");
        Encounters = content.GetAll<OffenseEncounterSO>()
            .Where(value => value != null)
            .OrderBy(value => value.encounterId, StringComparer.Ordinal)
            .ToArray();
        Diseases = content.GetAll<DiseaseDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        Wildlife = content.GetAll<WildlifeSpeciesSO>()
            .Where(value => value != null)
            .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        ValidateSeasonalWildlifeArrivals(SeasonalEvents, Wildlife);
        BattlefieldModifiers = content.GetAll<BattlefieldModifierDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        V20AuthoredContentSO[] definitions = LifeEvents.Cast<V20AuthoredContentSO>()
            .Concat(GuestRequests).Concat(ServiceIncidents).Concat(Arcs).Concat(Chapters)
            .Concat(Contracts).Concat(SeasonalEvents).Concat(All).ToArray();
        byId = definitions.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        HashSet<string> chapterIds = Chapters.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contractIds = Contracts.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FactionArcDefinitionSO arc in Arcs)
        {
            if (arc.chapterIds.Any(id => !chapterIds.Contains(id))
                || arc.contractIds.Any(id => !contractIds.Contains(id)))
                throw new InvalidOperationException(
                    $"Faction arc '{arc.StableId}' has a broken chapter or contract reference.");
        }
        ValidateSeasonalContracts(Contracts, SeasonalEvents, Arcs);
        ValidateSeasonalWeatherFrontRequirements(
            SeasonalEvents,
            weatherFrontIds);
        ValidateFactionEffectTargets(definitions, Arcs);
        ValidateFactionChapterSignatures(Chapters);
    }

    public IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    public IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    public IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    public IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    public IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    public IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    public IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    public EndlessCrisisPolicyDefinitionSO EndlessCrisisPolicy { get; }
    public IReadOnlyList<EndingDefinitionSO> All { get; }
    public IReadOnlyList<OffenseEncounterSO> Encounters { get; }
    public IReadOnlyList<DiseaseDefinitionSO> Diseases { get; }
    public IReadOnlyList<WildlifeSpeciesSO> Wildlife { get; }
    public IReadOnlyList<BattlefieldModifierDefinitionSO> BattlefieldModifiers { get; }

    V20AuthoredContentSO ISocietyEventCatalog.Require(string id) => RequireAny(id);
    V20AuthoredContentSO IFactionStoryCatalog.Require(string id) => RequireAny(id);
    SeasonalWorldEventDefinitionSO IWorldEventCatalog.Require(string id) =>
        RequireAny(id) as SeasonalWorldEventDefinitionSO
        ?? throw new KeyNotFoundException($"Unknown seasonal event '{id}'.");
    public bool TryGet(string id, out EndingDefinitionSO definition)
    {
        definition = All.FirstOrDefault(value => string.Equals(value.StableId, Normalize(id), StringComparison.Ordinal));
        return definition != null;
    }
    public EndingDefinitionSO Require(string id) => TryGet(id, out EndingDefinitionSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown milestone '{id}'.");

    private V20AuthoredContentSO RequireAny(string id) => byId.TryGetValue(Normalize(id), out V20AuthoredContentSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown V20 story definition '{id}'.");
    private static IReadOnlyList<T> Exact<T>(IEnumerable<T> source, int count, string label) where T : V20AuthoredContentSO
    {
        T[] values = (source ?? Array.Empty<T>()).Where(value => value != null).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        List<string> errors = values.SelectMany(value => value.ValidateDefinition()).ToList();
        if (values.Length != count) errors.Add($"Expected {count} {label}, found {values.Length}.");
        if (values.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count() != values.Length) errors.Add($"Duplicate {label} ids.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
        return values;
    }

    private static void ValidateFactionEffectTargets(
        IEnumerable<V20AuthoredContentSO> definitions,
        IEnumerable<FactionArcDefinitionSO> arcs)
    {
        HashSet<string> factionIds = arcs.Select(value => value.factionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contextualTargets = new(StringComparer.Ordinal)
        {
            "requesting-faction",
            "affected-faction"
        };
        foreach ((string ownerId, V20ContentEffect effect) in EnumerateEffects(definitions))
        {
            if (effect == null || effect.kind is not (
                    V20ContentEffectKind.FactionRapport
                    or V20ContentEffectKind.FactionGrievance
                    or V20ContentEffectKind.FactionObligation))
                continue;
            if (!factionIds.Contains(effect.targetId)
                && !contextualTargets.Contains(effect.targetId))
                throw new InvalidOperationException(
                    $"V20 definition '{ownerId}' references unknown faction effect target '{effect.targetId}'.");
        }
    }

    private static void ValidateSeasonalContracts(
        IEnumerable<FactionContractDefinitionSO> contracts,
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        IEnumerable<FactionArcDefinitionSO> arcs)
    {
        HashSet<string> eventIds = seasonalEvents
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> factionIds = arcs
            .Select(value => value.factionId)
            .ToHashSet(StringComparer.Ordinal);
        FactionContractDefinitionSO[] seasonal = contracts
            .Where(value => !string.IsNullOrEmpty(value.seasonalEventId))
            .ToArray();
        if (seasonal.Any(value => !eventIds.Contains(value.seasonalEventId)
                || !factionIds.Contains(value.factionId)))
            throw new InvalidOperationException(
                "Seasonal faction contract references an unknown event or faction.");
        if (seasonal.GroupBy(
                    value => $"{value.seasonalEventId}|{value.factionId}",
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
            throw new InvalidOperationException(
                "Seasonal faction contracts require one authored contract per event/faction pair.");
    }

    private static void ValidateSeasonalWeatherFrontRequirements(
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        HashSet<string> weatherFrontIds)
    {
        foreach (SeasonalWorldEventDefinitionSO definition in seasonalEvents)
        {
            if (definition.requiredWeatherFrontId.Length > 0
                && !weatherFrontIds.Contains(definition.requiredWeatherFrontId))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' references unknown weather front "
                    + $"'{definition.requiredWeatherFrontId}'.");
        }
    }

    private static void ValidateSeasonalWildlifeArrivals(
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonalEvents,
        IEnumerable<WildlifeSpeciesSO> wildlife)
    {
        Dictionary<string, WildlifeSpeciesSO> speciesById =
            (wildlife ?? Array.Empty<WildlifeSpeciesSO>())
            .Where(value => value != null)
            .ToDictionary(value => value.SpeciesId, StringComparer.Ordinal);
        foreach (SeasonalWorldEventDefinitionSO definition in
                 seasonalEvents.Where(value =>
                     value?.wildlifeArrivalProfile?.IsConfigured == true))
        {
            speciesById.TryGetValue(
                definition.wildlifeArrivalProfile.speciesId,
                out WildlifeSpeciesSO speciesAsset);
            SeasonalWildlifeArrivalRules.RequireValidAuthoredProfile(
                definition,
                speciesAsset?.ToDefinition());
        }
    }

    private static void ValidateFactionChapterSignatures(
        IEnumerable<FactionChapterDefinitionSO> chapters)
    {
        IGrouping<string, FactionChapterDefinitionSO> duplicate = chapters
            .GroupBy(FactionChapterMechanicalSignature, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException(
                "Faction chapters share a mechanical choice signature: "
                + string.Join(", ", duplicate.Select(value => value.StableId)));
    }

    public static string FactionChapterMechanicalSignature(
        FactionChapterDefinitionSO chapter) => string.Join(
        "||",
        (chapter?.choices ?? new List<V20ChoiceDefinition>())
        .OrderBy(value => value.choiceId, StringComparer.Ordinal)
        .Select(value =>
        {
            string items = string.Join(",", (value.requirements?.items ?? new())
                .Where(item => item != null)
                .OrderBy(item => item.itemDefinitionId, StringComparer.Ordinal)
                .Select(item => $"{item.itemDefinitionId}:{item.amount}:{item.consume}"));
            string facilities = string.Join(",", (value.requirements?.facilities ?? new())
                .Where(facility => facility != null)
                .OrderBy(facility => facility.buildingDefinitionId, StringComparer.Ordinal)
                .ThenBy(facility => facility.capabilityId, StringComparer.Ordinal)
                .Select(facility => $"{facility.buildingDefinitionId}:{facility.capabilityId}:{facility.minimumCount}:{facility.mustBeOperational}"));
            string effects = string.Join(",", (value.effects ?? new())
                .Where(effect => effect != null)
                .OrderBy(effect => effect.kind)
                .ThenBy(effect => effect.targetId, StringComparer.Ordinal)
                .Select(effect => $"{effect.kind}:{effect.targetId}:{effect.amount}:{effect.durationDays}"));
            return $"{value.choiceId}[{items}][{facilities}][{effects}]";
        }));

    private static IEnumerable<(string OwnerId, V20ContentEffect Effect)> EnumerateEffects(
        IEnumerable<V20AuthoredContentSO> definitions)
    {
        foreach (V20AuthoredContentSO definition in definitions)
        {
            IEnumerable<V20ContentEffect> effects = definition switch
            {
                LifeEventDefinitionSO value => (value.automaticEffects ?? new())
                    .Concat((value.choices ?? new()).SelectMany(choice => choice.effects ?? new())),
                GuestRequestDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                ServiceIncidentDefinitionSO value => (value.responses ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionChapterDefinitionSO value => (value.choices ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionContractDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                SeasonalWorldEventDefinitionSO value => (value.startEffects ?? new())
                    .Concat(value.dailyEffects ?? new())
                    .Concat(value.endEffects ?? new()),
                EndingDefinitionSO value => (value.permanentRewards ?? new())
                    .Concat(value.counterPressures ?? new()),
                _ => Array.Empty<V20ContentEffect>()
            };
            foreach (V20ContentEffect effect in effects)
                yield return (definition.StableId, effect);
        }
    }
    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
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
        string occurrenceInstanceId = "")
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
    public bool IsSocietyTerminal => InstanceId.Length > 0;
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
    private V20CampaignRuntime restoreOwner;

    internal void AttachRestoreOwner(V20CampaignRuntime owner)
    {
        if (restoreOwner != null)
            throw new InvalidOperationException(
                "Seasonal restore candidate already has an owner.");
        restoreOwner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    internal void ReleaseRestoreOwner(V20CampaignRuntime owner)
    {
        if (!ReferenceEquals(restoreOwner, owner))
            throw new InvalidOperationException(
                "Seasonal restore candidate owner mismatch.");
        restoreOwner = null;
    }

    public void Discard()
    {
        V20CampaignRuntime owner = restoreOwner;
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

public sealed class V20CampaignRuntime :
    IV20CampaignPersistence,
    IRunMilestoneQuery,
    ICommittedRunResultQuery,
    IMilestoneGameplayModifierQuery,
    IRunMilestoneCommand,
    ISeasonalEventQuery,
    ISeasonalWildlifeArrivalCommand,
    ISocietyEventQuery,
    IContentWorkDelayQuery,
    IContentWorkDelayCommand,
    ISocietyEventCommand,
    ISocietyObservedMealIncidentCommand,
    ISocietyObservedIncidentResponseCommand,
    ISocietyIncidentOwnedCharacterIdQuery,
    IGuestRequestDeliveryStateCommand,
    IFactionCampaignQuery,
    IFactionCampaignCommand,
    IFactionCampaignDeliveryCommand,
    IEndlessCrisisCommand,
    IEndlessCrisisQuery
{
    public const string SocietyChoiceOwnerId = "society.events";
    private const string GraveVisitLifeEventId = "life-event:grave-visit";
    private const string StoryCompressedLifeEventId =
        "life-event:story-compressed";
    private const string ApprenticeMistakeLifeEventId =
        "life-event:apprentice-mistake";
    private const string LastLessonLifeEventId = "life-event:last-lesson";
    private const string QuietPromotionLifeEventId =
        "life-event:quiet-promotion";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20StoryContentCatalog catalog;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly ISeasonalFeedSelfHeatingTargetQuery
        seasonalFeedSelfHeatingTargets;
    private SeasonalEventAggregateState preparedSeasonalRestore;
    public V20CampaignRuntime(DungeonRuntimeAggregateRootStore rootStore, V20StoryContentCatalog catalog)
        : this(rootStore, catalog, null, null) { }
    private int evaluationAbsoluteDay = -1;
    public V20CampaignRuntime(DungeonRuntimeAggregateRootStore rootStore, V20StoryContentCatalog catalog, IPhysicalItemBatchDispositionService physicalDispositions)
        : this(rootStore, catalog, physicalDispositions, null) { }
    public V20CampaignRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        V20StoryContentCatalog catalog,
        IPhysicalItemBatchDispositionService physicalDispositions,
        ISeasonalFeedSelfHeatingTargetQuery seasonalFeedSelfHeatingTargets)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalDispositions = physicalDispositions;
        this.seasonalFeedSelfHeatingTargets =
            seasonalFeedSelfHeatingTargets;
        EnsureFactionStates();
    }

    internal ISeasonalFeedSelfHeatingTargetQuery
        SeasonalFeedSelfHeatingTargets => seasonalFeedSelfHeatingTargets
        ?? throw new InvalidOperationException(
            "Seasonal feed self-heating evaluation requires its typed physical-target query.");

    public RunProgressionPhase Phase => Milestones.Data.phase;
    public int EndlessCycle => Milestones.Data.endlessCycle;
    public IReadOnlyCollection<string> CompletedMilestoneIds => Milestones.Data.completedMilestoneIds.AsReadOnly();
    public IReadOnlyCollection<string> GrantedRewardIds =>
        Milestones.Data.grantedRewardIds.AsReadOnly();
    public IReadOnlyCollection<string> ActivePressureIds =>
        Milestones.Data.activePressureIds.AsReadOnly();
    public IReadOnlyCollection<string> WorldFlags =>
        Milestones.Data.worldFlags.AsReadOnly();
    public EndlessCrisisSnapshot CurrentEndlessCrisis =>
        ProjectEndlessCrisis(Milestones.Data);
    public float GetEndlessCrisisMultiplier(EndlessCrisisAxis axis)
    {
        if (!Enum.IsDefined(typeof(EndlessCrisisAxis), axis)
            || axis == EndlessCrisisAxis.None)
            throw new ArgumentOutOfRangeException(nameof(axis));
        EndlessCrisisAxisSaveData modifier = Milestones.Data.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            ? Milestones.Data.endlessCrisisAxes.FirstOrDefault(value =>
                value != null && value.axis == axis)
            : null;
        return modifier?.multiplier ?? 1f;
    }
    public bool TryGetActiveEndlessCrisisEffectOwner(
        EndlessCrisisAxis axis,
        out string effectOwnerId)
    {
        effectOwnerId = string.Empty;
        if (!Enum.IsDefined(typeof(EndlessCrisisAxis), axis)
            || axis == EndlessCrisisAxis.None)
            return false;
        EndlessCrisisAxisSaveData modifier = Milestones.Data.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            ? Milestones.Data.endlessCrisisAxes.FirstOrDefault(value =>
                value != null && value.axis == axis)
            : null;
        effectOwnerId = modifier?.effectOwnerId ?? string.Empty;
        return effectOwnerId.Length > 0;
    }
    public CommittedRunResultSnapshot CaptureCommittedRunResult()
    {
        RunMilestoneWorldSaveData state = Milestones.Data;
        return new CommittedRunResultSnapshot(
            state.completedMilestoneIds,
            state.committedChoices
                .OrderBy(value => value.ordinal)
                .Select(ToCommittedChoiceSnapshot));
    }

    internal void RecordCommittedChoice(
        CommittedRunChoiceKind kind,
        string ownerId,
        string definitionId,
        string instanceId,
        string choiceId,
        string operationId)
    {
        V20CommittedRunChoiceSaveData incoming = new()
        {
            kind = kind,
            ownerId = ownerId,
            definitionId = definitionId,
            instanceId = instanceId,
            choiceId = choiceId,
            operationId = operationId
        };
        ValidateCommittedChoice(incoming, validateOrdinal: false);

        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        V20CommittedRunChoiceSaveData operationMatch = state.committedChoices
            .FirstOrDefault(value => string.Equals(
                value.operationId,
                incoming.operationId,
                StringComparison.Ordinal));
        if (operationMatch != null)
        {
            if (SameCommittedDecision(operationMatch, incoming)) return;
            throw new InvalidOperationException(
                $"Committed choice operation '{incoming.operationId}' conflicts with its existing decision.");
        }

        V20CommittedRunChoiceSaveData instanceMatch = state.committedChoices
            .FirstOrDefault(value => value.kind == incoming.kind
                && string.Equals(value.ownerId, incoming.ownerId, StringComparison.Ordinal)
                && string.Equals(value.instanceId, incoming.instanceId, StringComparison.Ordinal));
        if (instanceMatch != null)
        {
            if (SameCommittedDecision(instanceMatch, incoming)) return;
            throw new InvalidOperationException(
                $"Committed choice instance '{incoming.instanceId}' already has a different decision.");
        }

        incoming.ordinal = checked(state.nextCommittedChoiceOrdinal + 1L);
        state.nextCommittedChoiceOrdinal = incoming.ordinal;
        state.committedChoices.Add(incoming);
    }
    public bool IsLandmarkBuilding(string id)
    {
        string normalized = Normalize(id);
        return catalog.All.Any(value => string.Equals(
            Normalize(value.landmarkBuildingId),
            normalized,
            StringComparison.Ordinal));
    }

    public bool IsLandmarkUnlocked(string id) => Milestones.Data.unlockedLandmarkIds.Contains(Normalize(id), StringComparer.Ordinal);
    public bool EnemyCounterIntelVisible => HasReward("ending:truth-revealed");
    public float ExpeditionTravelTimeMultiplier =>
        HasReward("ending:surface-hegemony") ? 0.9f : 1f;
    public float FacilityMaintenanceGoldMultiplier =>
        HasReward("ending:dungeon-sovereignty") ? 0.9f : 1f;
    public float WaterAndFertilizerConsumptionMultiplier =>
        HasReward("ending:sealed-paradise") ? 0.9f : 1f;
    public int MentorshipDailyXpCap =>
        HasReward("ending:eternal-lineage")
            ? 15
            : CareerRules.MaximumDailyMentoringXp;
    public int TemporalStasisWarningDays =>
        HasReward("ending:timeless-sanctuary") ? 3 : 0;
    public float ManaTransferLossMultiplier =>
        HasReward("ending:arcane-ascension") ? 0.9f : 1f;
    public float AutomaticMaintenanceWorkMultiplier =>
        HasReward("ending:steel-apotheosis") ? 0.85f : 1f;
    public bool IsAccordSignalSupportDay(int absoluteDay) =>
        absoluteDay > 0
        && absoluteDay % GameCalendarRules.DaysPerSeason == 0
        && HasReward("ending:monster-accord");
    public bool IsAccordSignalSupportActive(int absoluteDay) =>
        IsAccordSignalSupportDay(absoluteDay)
        && Milestones.Data.lastAccordSignalSupportAbsoluteDay == absoluteDay;
    public bool TryActivateAccordSignalSupport(int absoluteDay, string sourceStackId)
    {
        if (!IsAccordSignalSupportDay(absoluteDay))
        {
            return false;
        }

        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        string operationId = $"accord-signal-support:{absoluteDay:D8}";
        if (state.pendingAccordSignalOperationId.Length > 0)
        {
            if (physicalDispositions == null) return false;
            if (!string.Equals(state.pendingAccordSignalOperationId, operationId, StringComparison.Ordinal)) return false;
            if (physicalDispositions.TryGetPending(operationId, out _)
                && !physicalDispositions.Acknowledge(state.pendingAccordSignalCommitId, out _)) return false;
            state.pendingAccordSignalOperationId = state.pendingAccordSignalCommitId = state.pendingAccordSignalSourceStackId = string.Empty;
            state.pendingAccordSignalMassGrams = 0;
            return state.lastAccordSignalSupportAbsoluteDay == absoluteDay;
        }
        if (state.lastAccordSignalSupportAbsoluteDay == absoluteDay)
        {
            return true;
        }
        if (physicalDispositions == null || string.IsNullOrWhiteSpace(sourceStackId)
            || !physicalDispositions.TryCommitPending(new[] { new PhysicalItemTransformInput(sourceStackId, 1) }, PhysicalItemDispositionKind.Sink,
                operationId, "alliance-signal-kit-consumed", out PhysicalItemBatchDispositionReceipt receipt, out _)) return false;
        state.pendingAccordSignalOperationId = operationId;
        state.pendingAccordSignalCommitId = receipt.CommitId;
        state.pendingAccordSignalSourceStackId = sourceStackId;
        state.pendingAccordSignalMassGrams = receipt.InputMassGrams;
        state.lastAccordSignalSupportAbsoluteDay = absoluteDay;
        return TryActivateAccordSignalSupport(absoluteDay, sourceStackId);
    }
    public bool HasReward(string milestoneId) =>
        Milestones.Data.grantedRewardIds.Contains(
            RewardId(milestoneId),
            StringComparer.Ordinal);
    public bool HasPressure(string milestoneId) =>
        Milestones.Data.activePressureIds.Contains(
            PressureId(milestoneId),
            StringComparer.Ordinal);
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents =>
        Seasonal.Data.activeEvents.AsReadOnly();

    [GameplayInternalOnly(
        "Commits one immutable exact-count seasonal wildlife arrival plan.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalWildlifeArrivalPlan(
        string occurrenceInstanceId,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> members,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        if (occurrence?.seasonalWildlifeArrival?.configured != true)
        {
            failureReason = "활성 계절 방문 occurrence를 찾지 못했습니다.";
            return false;
        }

        SeasonalWildlifeArrivalSaveData arrival =
            occurrence.seasonalWildlifeArrival;
        SeasonalWildlifeArrivalMemberSaveData[] proposed =
            (members ?? Array.Empty<SeasonalWildlifeArrivalMemberSaveData>())
            .Select(value => value == null
                ? null
                : SeasonalWildlifeArrivalRules.CloneMember(value))
            .ToArray();
        if (proposed.Any(value => value?.spawned == true))
        {
            failureReason = "새 계절 방문 계획은 생성 완료 상태를 포함할 수 없습니다.";
            return false;
        }
        if (arrival.members.Count > 0)
        {
            if (SeasonalWildlifeArrivalRules.SamePlan(
                    arrival.members,
                    proposed))
            {
                return true;
            }
            failureReason = "계절 방문 계획이 이미 다른 값으로 확정되었습니다.";
            return false;
        }

        SeasonalWildlifeArrivalSaveData candidate = new()
        {
            configured = arrival.configured,
            speciesId = arrival.speciesId,
            exactCount = arrival.exactCount,
            requiredHabitatId = arrival.requiredHabitatId,
            qualification = arrival.qualification,
            members = proposed.ToList(),
            lastFailureReason = string.Empty
        };
        SeasonalWorldEventDefinitionSO authored =
            ((IWorldEventCatalog)catalog).Require(occurrence.definitionId);
        try
        {
            SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                candidate,
                authored.wildlifeArrivalProfile,
                occurrence.instanceId);
        }
        catch (InvalidOperationException exception)
        {
            failureReason = exception.Message;
            return false;
        }

        arrival.members = proposed.ToList();
        arrival.lastFailureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Persists retry-visible failure state without changing a frozen seasonal arrival plan.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalWildlifeArrivalFailure(
        string occurrenceInstanceId,
        string failureReason)
    {
        string normalized = failureReason?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > 256)
        {
            return false;
        }
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        if (occurrence?.seasonalWildlifeArrival?.configured != true)
        {
            return false;
        }
        occurrence.seasonalWildlifeArrival.lastFailureReason = normalized;
        return true;
    }

    [GameplayInternalOnly(
        "Marks one exact planned visitor as materialized after wildlife authority confirms it.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryMarkSeasonalWildlifeArrivalSpawned(
        string occurrenceInstanceId,
        string wildlifeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence?.seasonalWildlifeArrival;
        SeasonalWildlifeArrivalMemberSaveData member = arrival?.members
            ?.SingleOrDefault(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal));
        if (member == null)
        {
            failureReason = "계절 방문 계획에 예약 개체가 없습니다.";
            return false;
        }
        if (member.spawned)
        {
            return true;
        }
        member.spawned = true;
        if (arrival.members.All(value => value?.spawned == true))
        {
            arrival.lastFailureReason = string.Empty;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Freezes one legal exterior source cell before seasonal drift cargo is materialized.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoPlan(
        string occurrenceInstanceId,
        int positionX,
        int positionY,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null)
        {
            failureReason = "활성 표류 화물 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (cargo.positionFrozen)
        {
            if (cargo.positionX == positionX && cargo.positionY == positionY)
                return true;
            failureReason = "표류 화물 위치가 이미 다른 값으로 확정되었습니다.";
            return false;
        }
        cargo.positionFrozen = true;
        cargo.positionX = positionX;
        cargo.positionY = positionY;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Records the exact persistent source stack IDs after physical loose publication succeeds.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryMarkSeasonalDriftCargoSpawned(
        string occurrenceInstanceId,
        IReadOnlyList<string> sourceStackIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        string[] ids = (sourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (cargo == null || !cargo.positionFrozen
            || ids.Length == 0
            || ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            failureReason = "표류 화물 생성 receipt가 유효하지 않습니다.";
            return false;
        }
        if (cargo.spawned)
        {
            if (cargo.sourceStackIds.SequenceEqual(ids, StringComparer.Ordinal))
                return true;
            failureReason = "표류 화물 원본 스택이 이미 다른 값으로 확정되었습니다.";
            return false;
        }
        cargo.spawned = true;
        cargo.sourceStackIds = ids.ToList();
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Persists monotonic actual-pickup progress derived from event-tagged physical lots.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoSecured(
        string occurrenceInstanceId,
        int securedQuantity,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null
            || securedQuantity < cargo.securedQuantity
            || securedQuantity + cargo.expiredQuantity > cargo.exactQuantity)
        {
            failureReason = "표류 화물 확보 수량이 기존 물리 진행과 충돌합니다.";
            return false;
        }
        cargo.securedQuantity = securedQuantity;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Commits only the supplied unpicked event source lots to the typed world-exit Sink outbox.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryExpireSeasonalDriftCargo(
        string occurrenceInstanceId,
        IReadOnlyList<PhysicalItemTransformInput> unpickedSourceLots,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null)
        {
            failureReason = "활성 표류 화물 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (cargo.expirationCompleted)
            return true;

        PhysicalItemTransformInput[] inputs = (unpickedSourceLots
                ?? Array.Empty<PhysicalItemTransformInput>())
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (inputs.Length == 0
            && string.IsNullOrEmpty(cargo.dispositionOperationId))
        {
            if (cargo.spawned)
            {
                cargo.securedQuantity = Math.Max(
                    cargo.securedQuantity,
                    cargo.exactQuantity - cargo.expiredQuantity);
            }
            cargo.expirationCompleted = true;
            cargo.lastFailureReason = string.Empty;
            return true;
        }
        if (physicalDispositions == null)
        {
            failureReason = "표류 화물 물리 손실 권위를 찾지 못했습니다.";
            return false;
        }

        string operationId = SeasonalDriftCargoRules.ExpirationOperationId(
            occurrenceInstanceId);
        PhysicalItemBatchDispositionReceipt receipt;
        if (!string.IsNullOrEmpty(cargo.dispositionOperationId))
        {
            if (!physicalDispositions.TryGetPending(operationId, out receipt))
            {
                failureReason = "표류 화물 손실 receipt를 복원하지 못했습니다.";
                return false;
            }
        }
        else if (!physicalDispositions.TryCommitPending(
                     inputs,
                     PhysicalItemDispositionKind.Sink,
                     operationId,
                     SeasonalDriftCargoRules.ExpirationReason,
                     out receipt,
                     out failureReason))
        {
            return false;
        }

        if (receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(receipt.OperationId, operationId, StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                SeasonalDriftCargoRules.ExpirationReason,
                StringComparison.Ordinal)
            || receipt.Quantity <= 0
            || cargo.securedQuantity + receipt.Quantity > cargo.exactQuantity)
        {
            failureReason = "표류 화물 손실 receipt가 occurrence와 일치하지 않습니다.";
            return false;
        }
        cargo.dispositionOperationId = operationId;
        cargo.dispositionCommitId = receipt.CommitId;
        cargo.dispositionSourceStackIds = receipt.SourceStackIds.ToList();
        cargo.expiredQuantity = receipt.Quantity;
        cargo.expiredMassGrams = receipt.InputMassGrams;
        cargo.securedQuantity = cargo.exactQuantity - cargo.expiredQuantity;
        if (!physicalDispositions.Acknowledge(
                receipt.CommitId,
                out failureReason))
        {
            cargo.lastFailureReason = failureReason;
            return false;
        }
        cargo.expirationCompleted = true;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Persists retry-visible drift-cargo failure without changing frozen source facts.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoFailure(
        string occurrenceInstanceId,
        string failureReason)
    {
        string normalized = failureReason?.Trim() ?? string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null || normalized.Length is < 1 or > 256)
            return false;
        cargo.lastFailureReason = normalized;
        return true;
    }

    private SeasonalDriftCargoSaveData FindSeasonalDriftCargo(
        string occurrenceInstanceId) => WritableSeasonal.Data.activeEvents
        .SingleOrDefault(value => value != null
            && string.Equals(
                value.instanceId,
                occurrenceInstanceId,
                StringComparison.Ordinal))
        ?.seasonalDriftCargo is { configured: true } cargo
            ? cargo
            : null;

    public float GetWorldWaterRegenerationMultiplier()
    {
        float multiplier = GetEndlessCrisisMultiplier(
            EndlessCrisisAxis.Climate);
        List<V20ActiveEventSaveData> activeEvents = Seasonal.Data.activeEvents;
        for (int index = 0; index < activeEvents.Count; index++)
        {
            V20ActiveEventSaveData occurrence = activeEvents[index]
                ?? throw new InvalidOperationException(
                    "Seasonal active-event state contains a null occurrence.");
            if (occurrence.resolved)
            {
                continue;
            }

            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(occurrence.definitionId);
            float authored = definition.worldWaterRegenerationMultiplier;
            if (float.IsNaN(authored)
                || float.IsInfinity(authored)
                || authored <= 0f
                || authored > 1f)
            {
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid "
                    + "world-water regeneration multiplier.");
            }
            multiplier = Math.Min(multiplier, authored);
        }
        return multiplier;
    }
    public SeasonalPowerCapacityContribution GetPowerCapacityContribution()
    {
        SeasonalPowerCapacityContribution selected =
            SeasonalPowerCapacityContribution.None;
        foreach ((V20ActiveEventSaveData occurrence,
                     SeasonalWorldEventDefinitionSO definition) in
                 ActiveSeasonalDefinitions())
        {
            float multiplier = definition
                .powerAvailableSupplyCapacityMultiplier;
            if (!ValidSeasonalMultiplier(multiplier))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid power available-supply multiplier.");
            if (multiplier >= 1f
                || selected.IsActive
                && (multiplier > selected.AvailableSupplyMultiplier
                    || multiplier == selected.AvailableSupplyMultiplier
                    && string.CompareOrdinal(
                        occurrence.instanceId,
                        selected.OccurrenceInstanceId) >= 0))
            {
                continue;
            }
            selected = new SeasonalPowerCapacityContribution(
                occurrence.instanceId,
                definition.StableId,
                definition.DisplayName,
                RemainingSeasonalDays(occurrence),
                multiplier);
        }
        return selected;
    }

    public SeasonalPipedWaterContribution GetPipedWaterContribution()
    {
        SeasonalPipedWaterContribution selected =
            SeasonalPipedWaterContribution.None;
        foreach ((V20ActiveEventSaveData occurrence,
                     SeasonalWorldEventDefinitionSO definition) in
                 ActiveSeasonalDefinitions())
        {
            float multiplier = definition.pipedWaterThroughputMultiplier;
            if (!ValidSeasonalMultiplier(multiplier))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid piped-water throughput multiplier.");
            if (multiplier >= 1f
                || selected.IsActive
                && (multiplier > selected.ThroughputMultiplier
                    || multiplier == selected.ThroughputMultiplier
                    && string.CompareOrdinal(
                        occurrence.instanceId,
                        selected.OccurrenceInstanceId) >= 0))
            {
                continue;
            }
            selected = new SeasonalPipedWaterContribution(
                occurrence.instanceId,
                definition.StableId,
                definition.DisplayName,
                RemainingSeasonalDays(occurrence),
                multiplier,
                definition.pipedWaterFreezeThresholdC,
                definition.pipedWaterRecoveryThresholdC);
        }
        return selected;
    }

    private IEnumerable<(V20ActiveEventSaveData Occurrence,
        SeasonalWorldEventDefinitionSO Definition)> ActiveSeasonalDefinitions()
    {
        foreach (V20ActiveEventSaveData occurrence in Seasonal.Data.activeEvents)
        {
            if (occurrence == null)
                throw new InvalidOperationException(
                    "Seasonal active-event state contains a null occurrence.");
            if (occurrence.resolved)
                continue;
            if (string.IsNullOrWhiteSpace(occurrence.instanceId)
                || !string.Equals(
                    occurrence.instanceId,
                    occurrence.instanceId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Seasonal active-event state has a non-canonical occurrence ID.");
            yield return (
                occurrence,
                ((IWorldEventCatalog)catalog).Require(occurrence.definitionId));
        }
    }

    private int RemainingSeasonalDays(V20ActiveEventSaveData occurrence) =>
        Math.Max(
            0,
            occurrence.deadlineAbsoluteDay
            - Math.Max(0, Seasonal.Data.lastEvaluationAbsoluteDay));
    private static bool ValidSeasonalMultiplier(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value > 0f
        && value <= 1f;
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents =>
        Society.Data.activeEvents.AsReadOnly();
    public IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents =>
        Society.Data.recentResolvedEvents.AsReadOnly();

    internal ObservedLifeEventCommitResult RecordObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt) => RecordObservedLifeEvent(
        ObservedLifeEventSourceKind.Funeral,
        receipt.SourceOperationId,
        receipt.CanonicalPayload,
        receipt.AbsoluteDay,
        receipt.Generation,
        receipt.ParticipantCharacterIds.Select(value => value.Value),
        GraveVisitLifeEventId);

    internal ObservedLifeEventCommitResult
        RecordObservedLineageCompressionLifeEvent(
            ObservedLineageCompressionLifeEventReceipt receipt) =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.LineageCompression,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.ArchiveAbsoluteDay,
            receipt.Generation,
            new[] { receipt.CharacterId.Value },
            StoryCompressedLifeEventId);

    internal ObservedLifeEventCommitResult
        RecordObservedProductionLossLifeEvent(
            ObservedProductionLossLifeEventReceipt receipt) =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.ProductionDeclaredLoss,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.AbsoluteDay,
            receipt.Generation,
            new[] { receipt.StudentCharacterId.Value },
            ApprenticeMistakeLifeEventId,
            resolveAutomatically: false);

    internal ObservedLifeEventCommitResult RecordObservedLastLessonLifeEvent(
        ObservedLastLessonLifeEventReceipt receipt) => RecordObservedLifeEvent(
        ObservedLifeEventSourceKind.LastMentorshipLesson,
        receipt.SourceOperationId,
        receipt.CanonicalPayload,
        receipt.AbsoluteDay,
        receipt.Generation,
        new[] { receipt.MentorCharacterId.Value },
        LastLessonLifeEventId,
        resolveAutomatically: false);

    internal ObservedLifeEventCommitResult
        RecordObservedQuietPromotionLifeEvent(
            ObservedQuietPromotionLifeEventReceipt receipt) =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.ProficiencyPromotion,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.AbsoluteDay,
            receipt.Generation,
            new[] { receipt.CharacterId.Value },
            QuietPromotionLifeEventId);

    internal void RequireCanRecordObservedLineageCompressionLifeEvent(
        ObservedLineageCompressionLifeEventReceipt receipt)
    {
        LifeEventDefinitionSO definition = RequireObservedLifeEventDefinition(
            StoryCompressedLifeEventId);
        ObservedLifeEventReceiptSaveData[] matches =
            Society.Data.successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind
                        == ObservedLifeEventSourceKind.LineageCompression
                    && string.Equals(
                        value.sourceOperationId,
                        receipt.SourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException(
                $"Observed lineage source '{receipt.SourceOperationId}' has duplicate receipts.");
        if (matches.Length == 1
            && !string.Equals(
                matches[0].canonicalPayload,
                receipt.CanonicalPayload,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Observed lineage source '{receipt.SourceOperationId}' conflicts with its committed receipt.");
        if (matches.Length == 1) return;

        V20DailyEventContext context = new()
        {
            AbsoluteDay = receipt.ArchiveAbsoluteDay,
            Generation = receipt.Generation
        };
        if (CanAssignLifeEvent(
                definition,
                receipt.CharacterId.Value,
                context,
                Society.Data))
        {
            CreateObservedLifeEvent(
                ObservedLifeEventSourceKind.LineageCompression,
                receipt.SourceOperationId,
                receipt.CanonicalPayload,
                receipt.ArchiveAbsoluteDay,
                receipt.Generation,
                receipt.CharacterId.Value,
                definition);
        }
    }
    public int GetRemainingDays(string scopeId = "")
    {
        int currentDay = Math.Max(0, Society.Data.lastEvaluationAbsoluteDay);
        bool queryAll = string.IsNullOrWhiteSpace(scopeId);
        string scope = queryAll ? string.Empty : NormalizeWorkDelayScope(scopeId);
        return Society.Data.workDelays
            .Where(value => value != null
                && (queryAll || string.Equals(
                    value.scopeId,
                    scope,
                    StringComparison.Ordinal)))
            .Select(value => Math.Max(0, value.untilAbsoluteDayExclusive - currentDay))
            .DefaultIfEmpty(0)
            .Max();
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        int currentDay = Math.Max(0, Society.Data.lastEvaluationAbsoluteDay);
        string workId = workTypeId.Value?.Trim() ?? string.Empty;
        int active = Society.Data.workDelays.Count(value => value != null
            && value.untilAbsoluteDayExclusive > currentDay
            && WorkDelayAffects(value.scopeId, workId));
        float result = ContentWorkDelaySpeedAuthority.Resolve(active);
        if (workTypeId == BuiltInWorkTypeIds.Haul)
        {
            result = Math.Max(
                ContentWorkDelaySpeedAuthority.MinimumMultiplier,
                result * GetEndlessCrisisMultiplier(
                    EndlessCrisisAxis.Logistics));
        }
        return result;
    }

    public void ApplyWorkDelay(
        string scopeId,
        int days,
        int absoluteDay) =>
        ApplyWorkDelayAt(scopeId, days, Math.Max(0, absoluteDay));
    IReadOnlyList<FactionCampaignStateSaveData> IFactionCampaignQuery.Factions =>
        Factions.Data.factions.AsReadOnly();

    public IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        foreach (string flag in state.worldFlags)
            snapshot.WorldFlags.Add(flag);
        UpdateSelfSufficiency(state, snapshot);
        List<string> completed = new();
        foreach (EndingDefinitionSO definition in catalog.All)
        {
            if (state.completedMilestoneIds.Contains(definition.StableId, StringComparer.Ordinal)
                || !RequirementsSatisfied(definition.completionRequirements, snapshot)) continue;
            state.completedMilestoneIds.Add(definition.StableId);
            state.unlockedLandmarkIds.Add(definition.landmarkBuildingId);
            state.grantedRewardIds.Add(definition.permanentRewards[0].targetId);
            state.activePressureIds.Add(definition.counterPressures[0].targetId);
            completed.Add(definition.StableId);
            if (definition.tier == RunMilestoneTier.Legacy && state.phase == RunProgressionPhase.Founding)
                state.phase = RunProgressionPhase.LegacyAge;
            if (definition.tier == RunMilestoneTier.Grand)
                state.phase = RunProgressionPhase.EndlessAge;
        }
        return completed;
    }

    public int AdvanceEndlessCycle()
    {
        if (WritableMilestones.Data.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless cycles require EndlessAge.");
        return ++WritableMilestones.Data.endlessCycle;
    }

    public IReadOnlyList<V20ResolvedEventResult> EvaluateDaily(
        V20DailyEventContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.AbsoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(context.AbsoluteDay));
        if (context.WeatherFrontId == null
            || context.WeatherFrontId.Length > 0
            && (string.IsNullOrWhiteSpace(context.WeatherFrontId)
                || !string.Equals(
                    context.WeatherFrontId,
                    context.WeatherFrontId.Trim(),
                    StringComparison.Ordinal)))
            throw new ArgumentException(
                "Daily event context weather-front ID must be optional and canonical.",
                nameof(context));

        List<V20ResolvedEventResult> resolved = new();
        evaluationAbsoluteDay = context.AbsoluteDay;
        try
        {
            EvaluateSeasonal(context, resolved);
            EvaluateSociety(context, resolved);
            return resolved;
        }
        finally
        {
            evaluationAbsoluteDay = -1;
        }
    }

    public ObservedMealIncidentCaptureResult CaptureObservedMealIncident(
        ObservedMealIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedMealEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out _,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-meal-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult CaptureObservedShopliftingIncident(
        ObservedShopliftingIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedShopliftingEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-shoplifting-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult CaptureObservedBrawlIncident(
        ObservedBrawlIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedBrawlEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-brawl-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult
        CaptureObservedCulturalConflictIncident(
            ObservedCulturalConflictIncidentSnapshot evidence,
            V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedCulturalConflictEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-cultural-conflict-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.CustomerCharacterId,
            context);
    }

    private ObservedMealIncidentCaptureResult CaptureObservedIncident(
        ObservedMealIncidentEvidenceSaveData proposed,
        CharacterId targetCharacterId,
        V20DailyEventContext context)
    {
        ServiceIncidentKind incidentKind = proposed.incidentKind;
        SocietyEventAggregateState candidate = PrepareSociety(CaptureSociety());
        SocietyEventWorldSaveData state = candidate.Data;
        ObservedMealIncidentEvidenceSaveData existing =
            state.successfulIncidentOperations.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.operationId,
                    proposed.operationId,
                    StringComparison.Ordinal));
        if (existing != null)
        {
            if (!SameObservedMealPayload(existing, proposed))
            {
                return new ObservedMealIncidentCaptureResult(
                    ObservedMealIncidentCaptureDisposition
                        .RejectedPayloadConflict,
                    existing.incidentKind,
                    existing.occurrenceInstanceId,
                    new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        proposed.operationId,
                        "observed-service-incident-payload-conflict"));
            }
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.ExactReplay,
                existing.incidentKind,
                existing.occurrenceInstanceId,
                DomainFailure.None);
        }
        ServiceIncidentDefinitionSO[] definitions = catalog.ServiceIncidents
            .Where(value => value != null && value.kind == incidentKind)
            .ToArray();
        if (definitions.Length != 1)
        {
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.NotEligible,
                incidentKind,
                string.Empty,
                new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    incidentKind.ToString(),
                    "observed-service-incident-definition-unavailable"));
        }

        ServiceIncidentDefinitionSO definition = definitions[0];
        bool participantOccupied = state.activeEvents.Any(value =>
            value?.participantCharacterIds?.Contains(
                targetCharacterId.Value,
                StringComparer.Ordinal) == true);
        bool emergencyOccupied = state.activeEvents.Any(value => value != null
            && IsEmergency(((ISocietyEventCatalog)catalog).Require(
                value.definitionId)));
        if (participantOccupied
            || emergencyOccupied
            || !CanStartSocietyDefinition(definition, context, state))
        {
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.NotEligible,
                incidentKind,
                string.Empty,
                new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    definition.StableId,
                    "observed-service-incident-society-policy-blocked"));
        }

        V20ActiveEventSaveData occurrence = CreateEvent(
            definition.StableId,
            context,
            DeadlineDays(definition),
            new[] { targetCharacterId.Value },
            definition);
        ObservedMealIncidentEvidenceSaveData frozen = JsonClone(proposed);
        frozen.definitionId = definition.StableId;
        frozen.occurrenceInstanceId = occurrence.instanceId;

        occurrence.hasObservedMealIncident = true;
        occurrence.observedMealIncident = frozen;
        state.activeEvents.Add(occurrence);
        state.successfulIncidentOperations.Add(JsonClone(frozen));
        candidate.Data = ValidateSociety(Clone(state));
        PublishSociety(candidate);
        return new ObservedMealIncidentCaptureResult(
            ObservedMealIncidentCaptureDisposition.Created,
            incidentKind,
            occurrence.instanceId,
            DomainFailure.None);
    }

    private static ObservedMealIncidentCaptureResult NotEligibleObservedIncident(
        string reason) => new(
        ObservedMealIncidentCaptureDisposition.NotEligible,
        default,
        string.Empty,
        new DomainFailure(FailureCode.ExternalInfluenceUnavailable, reason));

    public bool TryResolveObservedMealIncidentTargetDeath(
        CharacterId targetCharacterId,
        int absoluteDay,
        out V20ResolvedEventResult result,
        out DomainFailure failure)
    {
        result = default;
        failure = DomainFailure.None;
        if (!targetCharacterId.IsValid || absoluteDay < 1)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                targetCharacterId.Value,
                "observed-meal-incident-target-death-invalid");
            return false;
        }

        SocietyEventAggregateState candidate = PrepareSociety(CaptureSociety());
        SocietyEventWorldSaveData state = candidate.Data;
        V20ActiveEventSaveData occurrence = state.activeEvents
            .SingleOrDefault(value => value?.hasObservedMealIncident == true
                && string.Equals(
                    value.observedMealIncident.targetCharacterId,
                    targetCharacterId.Value,
                    StringComparison.Ordinal));
        if (occurrence == null)
        {
            return false;
        }

        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(occurrence.definitionId);
        occurrence.resolved = true;
        bool cancelledResponse = false;
        if (occurrence.observedIncidentResponse?.phase is
            ObservedIncidentResponsePhase.Accepted
            or ObservedIncidentResponsePhase.ExternalOperationStarted
            or ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            occurrence.observedIncidentResponse.phase =
                ObservedIncidentResponsePhase.Cancelled;
            occurrence.observedIncidentResponse.receiptId = string.Empty;
            occurrence.observedIncidentResponse.failureReason =
                "target-died-before-response-effects";
            cancelledResponse = true;
        }
        occurrence.resolutionId = cancelledResponse
            ? ObservedIncidentResponseFailureResolutionId(
                ObservedIncidentResponsePhase.Cancelled)
            : "target-death";
        FreezeSocietyTerminalOutcome(
            occurrence,
            definition,
            cancelledResponse ? "현장 대응 취소" : "대상 사망으로 종료",
            cancelledResponse
                ? FormatObservedIncidentResponseFailureNarrative(
                    occurrence.observedIncidentResponse)
                : "관측된 사건의 대상이 사망하여 현장 대응을 종료했습니다.",
            Array.Empty<V20ContentEffect>());
        state.activeEvents.Remove(occurrence);
        AddResolvedSociety(state, occurrence);
        MarkEventRecurrence(state, definition, occurrence, absoluteDay);
        candidate.Data = ValidateSociety(Clone(state));
        PublishSociety(candidate);
        result = CreateSocietyResolvedResult(
            occurrence,
            definition,
            Array.Empty<V20ContentEffect>());
        return true;
    }

    public IReadOnlyCollection<CharacterId> GetOwnedCustomerIds()
    {
        List<CharacterId> result = new();
        foreach (V20ActiveEventSaveData occurrence in Society.Data.activeEvents
                     .Where(value => value?.hasObservedMealIncident == true)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            ObservedMealIncidentEvidenceSaveData evidence =
                occurrence.observedMealIncident;
            RequireValidObservedMealEvidence(evidence);
            CharacterId target = new(evidence.targetCharacterId);
            if (occurrence.resolved
                || occurrence.participantCharacterIds == null
                || occurrence.participantCharacterIds.Count != 1
                || !string.Equals(
                    occurrence.participantCharacterIds[0],
                    target.Value,
                    StringComparison.Ordinal)
                || result.Contains(target))
            {
                throw new InvalidOperationException(
                    $"Society incident owner '{occurrence.instanceId}' is invalid or duplicated.");
            }
            result.Add(target);
        }
        return result.AsReadOnly();
    }

    public bool TryResolveSocietyEvent(
        string instanceId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            string.Equals(value.instanceId, Normalize(instanceId), StringComparison.Ordinal));
        if (active == null)
        {
            failure = "활성 사건을 찾을 수 없습니다.";
            return false;
        }

        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        if (definition is GuestRequestDefinitionSO guest)
        {
            string guestChoice = Normalize(choiceId);
            bool fulfilled = string.Equals(
                guestChoice,
                "fulfill",
                StringComparison.Ordinal);
            if (!fulfilled && !string.Equals(
                    guestChoice,
                    "decline",
                    StringComparison.Ordinal))
            {
                failure = "손님 요청은 fulfill 또는 decline으로 해결해야 합니다.";
                return false;
            }
            if (GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(guest))
            {
                if (fulfilled && !RequirementsSatisfied(
                        GuestRequestDeliveryMaterialRules
                            .BuildNonDeliveryRequirements(guest),
                        requirements))
                {
                    failure = "손님 요청 수락 조건을 충족하지 못했습니다.";
                    return false;
                }
                GuestRequestDeliverySaveData delivery =
                    active.guestDelivery ??= new GuestRequestDeliverySaveData();
                if (delivery.phase != GuestRequestDeliveryPhase.None)
                {
                    if (fulfilled
                        && delivery.phase is
                            GuestRequestDeliveryPhase.AwaitingVenue
                            or GuestRequestDeliveryPhase.Delivering)
                    {
                        return true;
                    }
                    if (!fulfilled)
                    {
                        return TryRequestGuestRequestDeliveryRelease(
                            active.instanceId,
                            GuestRequestDeliveryOutbox.DeclineDisposition,
                            "guest-request-declined",
                            out failure);
                    }
                    failure = "이미 인도된 손님 요청은 취소하거나 다시 수락할 수 없습니다.";
                    return false;
                }
                if (fulfilled)
                {
                    return TryAcceptGuestRequestDelivery(
                        active.instanceId,
                        V21ContentAlertActionIds.Society(
                            active.instanceId,
                            guestChoice),
                        out failure);
                }
            }
            if (fulfilled && !RequirementsSatisfied(
                    guest.serviceRequirements,
                    requirements))
            {
                failure = "손님 요청의 물리 조건을 충족하지 못했습니다.";
                return false;
            }
            IReadOnlyList<V20ContentEffect> guestEffects = fulfilled
                ? guest.successEffects
                : guest.failureEffects;
            if (fulfilled)
            {
                guestEffects = WithConsumedRequirements(
                    guestEffects,
                    guest.serviceRequirements);
            }
            active.selectedChoiceId = guestChoice;
            active.resolutionId = guestChoice;
            active.resolved = true;
            FreezeSocietyTerminalOutcome(
                active,
                definition,
                GuestResolutionLabel(guestChoice),
                RequireAuthoredNarrative(definition, null),
                guestEffects);
            state.activeEvents.Remove(active);
            AddResolvedSociety(state, active);
            MarkEventRecurrence(
                state,
                definition,
                active,
                active.startedAbsoluteDay);
            ApplyInternalEffects(guestEffects, active.contextFactionId);
            result = CreateSocietyResolvedResult(
                active,
                definition,
                guestEffects);
            return true;
        }
        V20ChoiceDefinition choice = GetChoices(definition).FirstOrDefault(value =>
            string.Equals(value.choiceId, Normalize(choiceId), StringComparison.Ordinal));
        if (choice == null)
        {
            failure = "선택지가 이 사건에 속하지 않습니다.";
            return false;
        }
        if (!RequirementsSatisfied(choice.requirements, requirements))
        {
            failure = "선택 조건을 충족하지 못했습니다.";
            return false;
        }
        if (active.hasObservedMealIncident
            && RequiresObservedIncidentOperation(
                active.observedMealIncident?.incidentKind ?? default,
                choice.choiceId))
        {
            ObservedIncidentResponseSaveData response =
                active.observedIncidentResponse ??=
                    new ObservedIncidentResponseSaveData();
            if (response.phase == ObservedIncidentResponsePhase.None)
            {
                if (string.Equals(
                        choice.choiceId,
                        "replace",
                        StringComparison.Ordinal)
                    && (active.observedMealIncident.fieldMeal
                        || !new BuildingInstanceId(
                                active.observedMealIncident.facilityInstanceId)
                            .IsValid))
                {
                    failure = "BLOCKED_CONTRACT:wim040-replacement-meal-facility:"
                        + active.instanceId;
                    return false;
                }
                response.phase = ObservedIncidentResponsePhase.Accepted;
                response.choiceId = choice.choiceId;
                response.operationId = FormatObservedIncidentResponseOperationId(
                    active.instanceId,
                    choice.choiceId);
                active.selectedChoiceId = choice.choiceId;
                return true;
            }
            if (!string.Equals(
                    response.choiceId,
                    choice.choiceId,
                    StringComparison.Ordinal))
            {
                failure = "BLOCKED_CONTRACT:wim040-incident-response-choice-owned:"
                    + $"{active.instanceId}:{response.choiceId}";
                return false;
            }
            if (response.phase is ObservedIncidentResponsePhase.Accepted
                or ObservedIncidentResponsePhase.ExternalOperationStarted)
            {
                return true;
            }
            if (response.phase
                != ObservedIncidentResponsePhase.ReceiptCommitted)
            {
                failure = "BLOCKED_CONTRACT:wim040-incident-response-state:"
                    + $"{active.instanceId}:{response.phase}";
                return false;
            }
        }

        active.selectedChoiceId = choice.choiceId;
        active.resolutionId = choice.choiceId;
        active.resolved = true;
        if (active.observedIncidentResponse?.phase
            == ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            active.observedIncidentResponse.phase =
                ObservedIncidentResponsePhase.EffectsPublished;
        }
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            choice.title,
            RequireAuthoredNarrative(definition, choice),
            choice.effects);
        state.activeEvents.Remove(active);
        AddResolvedSociety(state, active);
        MarkEventRecurrence(
            state,
            definition,
            active,
            active.startedAbsoluteDay);
        ApplyInternalEffects(choice.effects, active.contextFactionId);
        result = CreateSocietyResolvedResult(
            active,
            definition,
            choice.effects);
        return true;
    }

    public bool TryGetFaction(
        string factionId,
        out FactionCampaignStateSaveData state)
    {
        state = Factions.Data.factions.FirstOrDefault(value => string.Equals(
            value.factionId,
            Normalize(factionId),
            StringComparison.Ordinal));
        return state != null;
    }

    public bool TryResolveChapter(
        string factionId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (state.currentChapter < 1 || state.currentChapter > 6)
        {
            failure = "이 세력의 장기 서사는 이미 끝났습니다.";
            return false;
        }
        FactionChapterDefinitionSO chapter = catalog.Chapters.Single(value =>
            string.Equals(value.factionId, state.factionId, StringComparison.Ordinal)
            && value.chapterNumber == state.currentChapter);
        if (!RequirementsSatisfied(chapter.triggerRequirements, requirements))
        {
            failure = "서사 장의 시작 조건을 충족하지 못했습니다.";
            return false;
        }
        V20ChoiceDefinition choice = chapter.choices.FirstOrDefault(value =>
            string.Equals(value.choiceId, Normalize(choiceId), StringComparison.Ordinal));
        if (choice == null || !RequirementsSatisfied(choice.requirements, requirements))
        {
            failure = "유효하지 않거나 조건을 충족하지 못한 선택입니다.";
            return false;
        }

        IReadOnlyList<V20ContentEffect> resolvedEffects = WithConsumedRequirements(
            WithConsumedRequirements(choice.effects, chapter.triggerRequirements),
            choice.requirements);
        state.majorChoiceFlags.Add($"{chapter.StableId}:{choice.choiceId}");
        state.currentChapter++;
        ApplyInternalEffects(resolvedEffects);
        result = new V20ResolvedEventResult(
            chapter.StableId,
            choice.choiceId,
            resolvedEffects,
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        out string failure)
    {
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, Normalize(factionId), StringComparison.Ordinal));
        if (contract == null || !string.IsNullOrEmpty(contract.seasonalEventId))
        {
            failure = "활성 계절 사건에 속한 계약은 사건 제안으로만 수락할 수 있습니다.";
            return false;
        }
        if (FactionContractMaterialRules.HasMaterialRequirements(contract))
        {
            failure = "물자 계약은 유효한 물리 납품 목적지로만 수락할 수 있습니다.";
            return false;
        }
        return TryAcceptContractCore(
            factionId,
            contractId,
            absoluteDay,
            default,
            material: false,
            occurrenceId: string.Empty,
            deadlineAbsoluteDay: Math.Max(0, absoluteDay) + contract.deadlineDays,
            out failure);
    }

    public bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        out string failure)
    {
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, Normalize(factionId), StringComparison.Ordinal));
        if (contract == null || !string.IsNullOrEmpty(contract.seasonalEventId))
        {
            failure = "활성 계절 사건에 속한 계약은 사건 제안으로만 수락할 수 있습니다.";
            return false;
        }
        bool material = FactionContractMaterialRules
            .HasMaterialRequirements(contract);
        if (material
            && (!target.IsValid
                || !string.Equals(
                    target.DestinationId,
                    FactionContractDeliveryOutbox.FormatDestinationId(
                        contract.StableId),
                    StringComparison.Ordinal)))
        {
            failure = "물자 계약 납품 목적지가 유효하지 않습니다.";
            return false;
        }
        return TryAcceptContractCore(
            factionId,
            contractId,
            absoluteDay,
            target,
            material,
            occurrenceId: string.Empty,
            deadlineAbsoluteDay: Math.Max(0, absoluteDay) + contract.deadlineDays,
            out failure);
    }

    public bool TryAcceptSeasonalContract(
        string occurrenceId,
        string contractId,
        FactionContractDeliveryTarget target,
        out string failure)
    {
        failure = string.Empty;
        string normalizedOccurrenceId = Normalize(occurrenceId);
        V20ActiveEventSaveData occurrence = Seasonal.Data.activeEvents
            .FirstOrDefault(value => value != null
                && !value.resolved
                && value.deadlineAbsoluteDay >=
                    Seasonal.Data.lastEvaluationAbsoluteDay
                && string.Equals(
                    value.instanceId,
                    normalizedOccurrenceId,
                    StringComparison.Ordinal));
        FactionContractDefinitionSO contract = catalog.Contracts
            .FirstOrDefault(value => occurrence != null
                && string.Equals(
                    value.StableId,
                    Normalize(contractId),
                    StringComparison.Ordinal)
                && string.Equals(
                    value.factionId,
                    occurrence.contextFactionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.seasonalEventId,
                    occurrence.definitionId,
                    StringComparison.Ordinal));
        if (contract == null
            || !FactionContractMaterialRules.HasMaterialRequirements(contract)
            || !target.IsValid
            || !string.Equals(
                target.DestinationId,
                FactionContractDeliveryOutbox.FormatDestinationId(
                    contract.StableId,
                    normalizedOccurrenceId),
                StringComparison.Ordinal))
        {
            failure = "활성 계절 사건과 세력에 맞는 물자 계약이 아닙니다.";
            return false;
        }
        return TryAcceptContractCore(
            contract.factionId,
            contract.StableId,
            Math.Max(
                occurrence.startedAbsoluteDay,
                Seasonal.Data.lastEvaluationAbsoluteDay),
            target,
            material: true,
            occurrenceId: normalizedOccurrenceId,
            deadlineAbsoluteDay: occurrence.deadlineAbsoluteDay,
            out failure);
    }

    private bool TryAcceptContractCore(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        bool material,
        string occurrenceId,
        int deadlineAbsoluteDay,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (!string.IsNullOrWhiteSpace(state.activeContractId)
            || FactionContractDeliveryOutbox.HasPending(state)
            || state.activeContractInputOwnerActive
            || state.deliveryTerminalCleanupPending)
        {
            failure = "이미 진행 중인 세력 계약이 있습니다.";
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, state.factionId, StringComparison.Ordinal));
        bool seasonal = !string.IsNullOrEmpty(occurrenceId);
        bool alreadyTerminal = seasonal
            ? string.Equals(
                    state.lastSeasonalContractId,
                    contract?.StableId,
                    StringComparison.Ordinal)
                && string.Equals(
                    state.lastSeasonalOccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal)
            : contract != null
                && (state.completedContractIds.Contains(
                        contract.StableId,
                        StringComparer.Ordinal)
                    || state.failedContractIds.Contains(
                        contract.StableId,
                        StringComparer.Ordinal));
        if (contract == null
            || seasonal != !string.IsNullOrEmpty(contract.seasonalEventId)
            || alreadyTerminal
            || deadlineAbsoluteDay < Math.Max(0, absoluteDay)
            || material
                && (!target.IsValid
                    || !string.Equals(
                        target.DestinationId,
                        FactionContractDeliveryOutbox.FormatDestinationId(
                            contract.StableId,
                            occurrenceId),
                        StringComparison.Ordinal)))
        {
            failure = "이 세력에 속한 수락 가능한 계약이 아닙니다.";
            return false;
        }
        if (seasonal)
        {
            state.completedContractIds.RemoveAll(value => string.Equals(
                value,
                contract.StableId,
                StringComparison.Ordinal));
            state.failedContractIds.RemoveAll(value => string.Equals(
                value,
                contract.StableId,
                StringComparison.Ordinal));
            state.lastSeasonalContractId = contract.StableId;
            state.lastSeasonalOccurrenceId = occurrenceId;
        }
        state.activeContractId = contract.StableId;
        state.activeContractOccurrenceId = occurrenceId;
        state.activeContractDeadlineAbsoluteDay = deadlineAbsoluteDay;
        state.deliveryFailureReason = string.Empty;
        if (material)
        {
            state.activeContractDestinationOwnerId =
                FactionContractDeliveryOutbox.FormatOwnerId(
                    contract.StableId,
                    occurrenceId);
            state.activeContractDestinationId = target.DestinationId;
            state.activeContractDestinationX = target.Position.x;
            state.activeContractDestinationY = target.Position.y;
            state.activeContractInputOwnerActive = true;
        }
        return true;
    }

    private static string FormatObservedIncidentResponseOperationId(
        string instanceId,
        string choiceId) => string.Equals(
            choiceId,
            "replace",
            StringComparison.Ordinal)
        ? $"consumable-operation:wim040-response:{instanceId}"
        : $"society-incident-response:{instanceId}:{choiceId}";

    private static bool IsExpectedObservedIncidentResponseReceipt(
        V20ActiveEventSaveData occurrence,
        string externalOperationId,
        string receiptId)
    {
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null
            || !IsCanonicalRequiredId(externalOperationId)
            || !IsCanonicalRequiredId(receiptId))
        {
            return false;
        }
        if (string.Equals(response.choiceId, "replace", StringComparison.Ordinal))
        {
            string prefix = $"physical-meal-consumed:{response.operationId}:";
            return string.Equals(
                    externalOperationId,
                    response.operationId,
                    StringComparison.Ordinal)
                && receiptId.StartsWith(prefix, StringComparison.Ordinal)
                && new ItemStackId(receiptId.Substring(prefix.Length)).IsValid;
        }
        if (string.Equals(
                response.choiceId,
                "emergency-care",
                StringComparison.Ordinal))
        {
            return string.Equals(
                receiptId,
                $"medical-stabilized:{externalOperationId}",
                StringComparison.Ordinal);
        }
        return string.Equals(response.choiceId, "transfer", StringComparison.Ordinal)
            && TryClassifyObservedIncidentTransferExternalOperationId(
                occurrence,
                externalOperationId,
                out _)
            && new CharacterId(
                    occurrence.observedMealIncident?.targetCharacterId)
                is { IsValid: true } customerId
            && string.Equals(
                receiptId,
                FormatObservedIncidentTransferReceiptId(
                    externalOperationId,
                    customerId),
                StringComparison.Ordinal);
    }

    internal static bool
        TryClassifyObservedIncidentTransferExternalOperationId(
            V20ActiveEventSaveData occurrence,
            string externalOperationId,
            out bool medicalOrder)
    {
        medicalOrder = false;
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null
            || !string.Equals(
                response.choiceId,
                "transfer",
                StringComparison.Ordinal)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            return false;
        }
        if (TryParseObservedIncidentTransferOperationId(
                response.operationId,
                externalOperationId,
                out _,
                out _))
        {
            return true;
        }
        if (occurrence.hasObservedMealIncident != true
            || occurrence.observedMealIncident?.incidentKind
                != ServiceIncidentKind.MedicalCollapse)
        {
            return false;
        }

        const string medicalOrderPrefix = "medical:";
        if (!externalOperationId.StartsWith(
                medicalOrderPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = externalOperationId.Substring(
            medicalOrderPrefix.Length);
        medicalOrder = int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        return medicalOrder;
    }

    internal static string FormatObservedIncidentTransferOperationId(
        string responseOperationId,
        BuildingInstanceId facilityId,
        CoreGridCell destination)
    {
        if (!IsCanonicalRequiredId(responseOperationId)
            || !facilityId.IsValid)
        {
            throw new ArgumentException(
                "A canonical response operation and isolation facility are required.");
        }
        return responseOperationId
            + ":destination:"
            + destination.X.ToString(CultureInfo.InvariantCulture)
            + ":"
            + destination.Y.ToString(CultureInfo.InvariantCulture)
            + ":"
            + facilityId.Value;
    }

    internal static bool TryParseObservedIncidentTransferOperationId(
        string responseOperationId,
        string externalOperationId,
        out BuildingInstanceId facilityId,
        out CoreGridCell destination)
    {
        facilityId = default;
        destination = default;
        if (!IsCanonicalRequiredId(responseOperationId)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            return false;
        }

        string prefix = responseOperationId + ":destination:";
        if (!externalOperationId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string payload = externalOperationId.Substring(prefix.Length);
        int firstSeparator = payload.IndexOf(':');
        int secondSeparator = firstSeparator < 0
            ? -1
            : payload.IndexOf(':', firstSeparator + 1);
        if (firstSeparator <= 0
            || secondSeparator <= firstSeparator + 1
            || secondSeparator >= payload.Length - 1
            || !int.TryParse(
                payload.Substring(0, firstSeparator),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int x)
            || !int.TryParse(
                payload.Substring(
                    firstSeparator + 1,
                    secondSeparator - firstSeparator - 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int y))
        {
            return false;
        }

        BuildingInstanceId parsedFacility = new(
            payload.Substring(secondSeparator + 1));
        CoreGridCell parsedDestination = new(x, y);
        if (!parsedFacility.IsValid
            || !string.Equals(
                externalOperationId,
                FormatObservedIncidentTransferOperationId(
                    responseOperationId,
                    parsedFacility,
                    parsedDestination),
                StringComparison.Ordinal))
        {
            return false;
        }

        facilityId = parsedFacility;
        destination = parsedDestination;
        return true;
    }

    internal static string FormatObservedIncidentTransferReceiptId(
        string externalOperationId,
        CharacterId customerId)
    {
        if (!IsCanonicalRequiredId(externalOperationId)
            || !customerId.IsValid)
        {
            throw new ArgumentException(
                "A canonical transfer operation and customer are required.");
        }
        return $"character-arrived:{externalOperationId}:{customerId.Value}";
    }

    public bool TryRecordObservedIncidentResponseStarted(
        string instanceId,
        string operationId,
        string externalOperationId,
        out string failure)
    {
        failure = string.Empty;
        if (!IsCanonicalRequiredId(operationId)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            failure = "wim040-incident-response-start-identity-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = PrepareSociety(CaptureSociety());
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        bool externalIdentityConflict = string.Equals(
                response.choiceId,
                "replace",
                StringComparison.Ordinal)
            && !string.Equals(
                externalOperationId,
                response.operationId,
                StringComparison.Ordinal)
            || string.Equals(
                response.choiceId,
                "transfer",
                StringComparison.Ordinal)
            && !TryClassifyObservedIncidentTransferExternalOperationId(
                active,
                externalOperationId,
                out _);
        if (externalIdentityConflict)
        {
            failure = "wim040-incident-response-external-operation-conflict";
            return false;
        }
        if (response.phase
            == ObservedIncidentResponsePhase.ExternalOperationStarted)
        {
            if (string.Equals(
                    response.externalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-external-operation-conflict";
            return false;
        }
        if (response.phase != ObservedIncidentResponsePhase.Accepted)
        {
            failure = "wim040-incident-response-start-phase-invalid:"
                + response.phase;
            return false;
        }
        response.phase =
            ObservedIncidentResponsePhase.ExternalOperationStarted;
        response.externalOperationId = externalOperationId;
        candidate.Data = ValidateSociety(Clone(candidate.Data));
        PublishSociety(candidate);
        return true;
    }

    public bool TryRecordObservedIncidentResponseReceipt(
        string instanceId,
        string operationId,
        string externalOperationId,
        string receiptId,
        out string failure)
    {
        failure = string.Empty;
        if (!IsCanonicalRequiredId(operationId)
            || !IsCanonicalRequiredId(externalOperationId)
            || !IsCanonicalRequiredId(receiptId))
        {
            failure = "wim040-incident-response-receipt-identity-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = PrepareSociety(CaptureSociety());
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        if (!IsExpectedObservedIncidentResponseReceipt(
                active,
                externalOperationId,
                receiptId))
        {
            failure = "wim040-incident-response-receipt-payload-invalid";
            return false;
        }
        if (response.phase == ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            if (string.Equals(
                    response.externalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    response.receiptId,
                    receiptId,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-receipt-conflict";
            return false;
        }
        if (response.phase
                != ObservedIncidentResponsePhase.ExternalOperationStarted
            || !string.Equals(
                response.externalOperationId,
                externalOperationId,
                StringComparison.Ordinal))
        {
            failure = "wim040-incident-response-receipt-phase-invalid";
            return false;
        }
        response.phase = ObservedIncidentResponsePhase.ReceiptCommitted;
        response.receiptId = receiptId;
        candidate.Data = ValidateSociety(Clone(candidate.Data));
        PublishSociety(candidate);
        return true;
    }

    public bool TryRecordObservedIncidentResponseFailure(
        string instanceId,
        string operationId,
        string failureReason,
        bool cancelled,
        out string failure)
    {
        failure = string.Empty;
        string normalizedReason = Normalize(failureReason);
        if (!IsCanonicalRequiredId(operationId)
            || string.IsNullOrEmpty(normalizedReason))
        {
            failure = "wim040-incident-response-failure-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = PrepareSociety(CaptureSociety());
        V20ActiveEventSaveData resolved = candidate.Data.recentResolvedEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    Normalize(instanceId),
                    StringComparison.Ordinal));
        if (resolved != null)
        {
            ObservedIncidentResponseSaveData resolvedResponse =
                resolved.observedIncidentResponse;
            ObservedIncidentResponsePhase expectedTerminal = cancelled
                ? ObservedIncidentResponsePhase.Cancelled
                : ObservedIncidentResponsePhase.Failed;
            if (resolved.hasObservedMealIncident
                && resolvedResponse?.phase == expectedTerminal
                && string.Equals(
                    resolvedResponse.operationId,
                    Normalize(operationId),
                    StringComparison.Ordinal)
                && string.Equals(
                    resolvedResponse.failureReason,
                    normalizedReason,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-terminal-conflict";
            return false;
        }
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        ObservedIncidentResponsePhase terminal = cancelled
            ? ObservedIncidentResponsePhase.Cancelled
            : ObservedIncidentResponsePhase.Failed;
        if (response.phase is ObservedIncidentResponsePhase.Failed
            or ObservedIncidentResponsePhase.Cancelled)
        {
            if (response.phase == terminal
                && string.Equals(
                    response.failureReason,
                    normalizedReason,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-terminal-conflict";
            return false;
        }
        if (response.phase is not (
            ObservedIncidentResponsePhase.Accepted
            or ObservedIncidentResponsePhase.ExternalOperationStarted))
        {
            failure = "wim040-incident-response-failure-phase-invalid";
            return false;
        }
        response.phase = terminal;
        response.failureReason = normalizedReason;
        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        active.resolved = true;
        active.resolutionId = ObservedIncidentResponseFailureResolutionId(
            terminal);
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            terminal == ObservedIncidentResponsePhase.Cancelled
                ? "현장 대응 취소"
                : "현장 대응 실패",
            FormatObservedIncidentResponseFailureNarrative(response),
            Array.Empty<V20ContentEffect>());
        candidate.Data.activeEvents.Remove(active);
        AddResolvedSociety(candidate.Data, active);
        MarkEventRecurrence(
            candidate.Data,
            definition,
            active,
            active.startedAbsoluteDay);
        candidate.Data = ValidateSociety(Clone(candidate.Data));
        PublishSociety(candidate);
        return true;
    }

    private static string ObservedIncidentResponseFailureResolutionId(
        ObservedIncidentResponsePhase phase) => phase switch
        {
            ObservedIncidentResponsePhase.Failed => "response-failed",
            ObservedIncidentResponsePhase.Cancelled => "response-cancelled",
            _ => throw new ArgumentOutOfRangeException(
                nameof(phase),
                phase,
                "Only failed or cancelled incident responses are terminal failures.")
        };

    private static string FormatObservedIncidentResponseFailureNarrative(
        ObservedIncidentResponseSaveData response)
    {
        if (response == null
            || response.phase is not (
                ObservedIncidentResponsePhase.Failed
                or ObservedIncidentResponsePhase.Cancelled)
            || !IsCanonicalRequiredId(response.choiceId)
            || !IsCanonicalRequiredId(response.operationId)
            || string.IsNullOrWhiteSpace(response.failureReason))
        {
            throw new InvalidOperationException(
                "A terminal incident-response failure requires exact provenance.");
        }
        string externalOperationId = string.IsNullOrEmpty(
                response.externalOperationId)
            ? "없음"
            : response.externalOperationId;
        return $"선택={response.choiceId}; operation={response.operationId}; "
            + $"external-operation={externalOperationId}; "
            + $"failure={response.failureReason}";
    }

    private static V20ActiveEventSaveData FindPendingIncidentResponse(
        SocietyEventWorldSaveData state,
        string instanceId,
        string operationId,
        out string failure)
    {
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.instanceId,
                Normalize(instanceId),
                StringComparison.Ordinal));
        ObservedIncidentResponseSaveData response =
            active?.observedIncidentResponse;
        if (active == null
            || active.resolved
            || !active.hasObservedMealIncident
            || response == null
            || response.phase == ObservedIncidentResponsePhase.None
            || !string.Equals(
                response.operationId,
                Normalize(operationId),
                StringComparison.Ordinal))
        {
            failure = "wim040-incident-response-owner-missing";
            return null;
        }
        failure = string.Empty;
        return active;
    }

    public bool TryAcceptGuestRequestDelivery(
        string instanceId,
        string actionId,
        out string failure)
    {
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        if (definition == null
            || !GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(definition)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(actionId)
            || !string.Equals(
                actionId,
                V21ContentAlertActionIds.Society(active.instanceId, "fulfill"),
                StringComparison.Ordinal))
        {
            failure = "물리 납품 손님 요청 수락 정보가 유효하지 않습니다.";
            return false;
        }
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        if (delivery.phase != GuestRequestDeliveryPhase.None)
        {
            if (string.Equals(
                    delivery.acceptedActionId,
                    actionId,
                    StringComparison.Ordinal)
                && delivery.phase is GuestRequestDeliveryPhase.AwaitingVenue
                    or GuestRequestDeliveryPhase.Delivering)
            {
                return true;
            }
            failure = "손님 요청 납품이 이미 다른 단계에서 진행 중입니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.AwaitingVenue;
        delivery.acceptedActionId = actionId;
        delivery.failureReason = "적격 시설을 찾는 중입니다.";
        active.selectedChoiceId = "fulfill";
        return true;
    }

    public bool TryAssignGuestRequestDeliveryVenue(
        string instanceId,
        string facilityInstanceId,
        string destinationId,
        UnityEngine.Vector2Int destination,
        EconomyProjectInputOwnerProjection projection,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase != GuestRequestDeliveryPhase.AwaitingVenue
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                facilityInstanceId)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(destinationId)
            || projection.CapacityGrams <= 0L
            || projection.MassAuthorityRevision <= 0L
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                projection.Fingerprint))
        {
            failure = "손님 요청 납품 시설 배정 정보가 유효하지 않습니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.Delivering;
        delivery.venueFacilityInstanceId = facilityInstanceId;
        delivery.destinationId = destinationId;
        delivery.destinationX = destination.x;
        delivery.destinationY = destination.y;
        delivery.inputOwnerActive = true;
        delivery.inputCapacityGrams = projection.CapacityGrams;
        delivery.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        delivery.inputCapacityFingerprint = projection.Fingerprint;
        delivery.failureReason = string.Empty;
        return true;
    }

    public bool TrySetGuestRequestDeliveryFailure(
        string instanceId,
        string failureReason,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase == GuestRequestDeliveryPhase.None)
        {
            failure = "진행 중인 손님 요청 납품이 없습니다.";
            return false;
        }
        delivery.failureReason = failureReason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryRequestGuestRequestDeliveryRelease(
        string instanceId,
        string disposition,
        string reason,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        bool terminal = string.Equals(
                disposition,
                GuestRequestDeliveryOutbox.DeclineDisposition,
                StringComparison.Ordinal)
            || string.Equals(
                disposition,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal);
        bool replan = string.Equals(
            disposition,
            GuestRequestDeliveryOutbox.ReplanDisposition,
            StringComparison.Ordinal);
        if (delivery.phase == GuestRequestDeliveryPhase.ReleasePending
            && string.Equals(
                delivery.terminalResolutionId,
                disposition,
                StringComparison.Ordinal))
        {
            return true;
        }
        if ((!terminal && !replan)
            || delivery.phase is not (
                GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering)
            || replan && delivery.phase != GuestRequestDeliveryPhase.Delivering)
        {
            failure = "현재 손님 요청 납품은 회수 단계로 전환할 수 없습니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.ReleasePending;
        delivery.terminalResolutionId = disposition;
        delivery.failureReason = reason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryMarkGuestRequestInputOwnerRetired(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (!delivery.inputOwnerActive)
            return true;
        if (delivery.phase is not (
                GuestRequestDeliveryPhase.ReleasePending
                or GuestRequestDeliveryPhase.RewardPublished))
        {
            failure = "진행 중인 손님 요청 입력 목적지는 종료할 수 없습니다.";
            return false;
        }
        delivery.inputOwnerActive = false;
        delivery.inputCapacityGrams = 0L;
        delivery.inputMassAuthorityRevision = 0L;
        delivery.inputCapacityFingerprint = string.Empty;
        return true;
    }

    public bool TryResumeGuestRequestVenueSelection(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase != GuestRequestDeliveryPhase.ReleasePending
            || delivery.inputOwnerActive
            || !string.Equals(
                delivery.terminalResolutionId,
                GuestRequestDeliveryOutbox.ReplanDisposition,
                StringComparison.Ordinal)
            || GuestRequestDeliveryOutbox.IsCanonicalReceipt(delivery))
        {
            failure = "손님 요청 납품 목적지를 다시 찾을 수 없는 상태입니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.AwaitingVenue;
        delivery.venueFacilityInstanceId = string.Empty;
        delivery.destinationId = string.Empty;
        delivery.destinationX = 0;
        delivery.destinationY = 0;
        delivery.terminalResolutionId = string.Empty;
        delivery.failureReason = "적격 시설을 다시 찾는 중입니다.";
        return true;
    }

    public bool TryRecordGuestRequestDeliveryReceipt(
        string instanceId,
        GuestRequestDeliveryReceipt receipt,
        out string failure)
    {
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        IReadOnlyDictionary<string, int> material =
            GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                definition);
        if (definition == null
            || !delivery.inputOwnerActive
            || delivery.inputCapacityGrams <= 0L
            || receipt.Quantity != material.Values.Sum()
            || receipt.InputMassGrams != delivery.inputCapacityGrams)
        {
            failure = "손님 요청의 물리 인도 receipt를 기록할 수 없습니다.";
            return false;
        }
        try
        {
            GuestRequestDeliveryOutbox.Record(
                delivery,
                active.instanceId,
                receipt);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return false;
        }
        return true;
    }

    public bool TryPublishGuestRequestDeliveryOutcome(
        string instanceId,
        string resolutionId,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        bool fulfilled = string.Equals(
            resolutionId,
            "fulfill",
            StringComparison.Ordinal);
        bool failed = string.Equals(
                resolutionId,
                GuestRequestDeliveryOutbox.DeclineDisposition,
                StringComparison.Ordinal)
            || string.Equals(
                resolutionId,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal);
        if (definition == null
            || fulfilled && (delivery.phase !=
                    GuestRequestDeliveryPhase.PhysicalCommitted
                || !GuestRequestDeliveryOutbox.IsCanonicalReceipt(delivery))
            || failed && (delivery.phase !=
                    GuestRequestDeliveryPhase.ReleasePending
                || delivery.inputOwnerActive
                || !string.Equals(
                    delivery.terminalResolutionId,
                    resolutionId,
                    StringComparison.Ordinal))
            || !fulfilled && !failed)
        {
            failure = "손님 요청 납품 결과를 확정할 수 없는 상태입니다.";
            return false;
        }
        IReadOnlyList<V20ContentEffect> effects = fulfilled
            ? definition.successEffects
            : definition.failureEffects;
        delivery.phase = GuestRequestDeliveryPhase.RewardPublished;
        delivery.terminalResolutionId = resolutionId;
        delivery.failureReason = string.Empty;
        active.selectedChoiceId = fulfilled ? "fulfill" : resolutionId;
        active.resolutionId = resolutionId;
        active.resolved = true;
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            GuestResolutionLabel(resolutionId),
            RequireAuthoredNarrative(definition, null),
            effects);
        ApplyInternalEffects(effects, active.contextFactionId);
        result = CreateSocietyResolvedResult(
            active,
            definition,
            effects);
        return true;
    }

    public bool TryFinalizeGuestRequestDelivery(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            string.Equals(
                value.instanceId,
                Normalize(instanceId),
                StringComparison.Ordinal));
        GuestRequestDeliverySaveData delivery = active?.guestDelivery;
        if (active == null
            || !active.resolved
            || delivery?.phase != GuestRequestDeliveryPhase.RewardPublished
            || delivery.inputOwnerActive
            || string.IsNullOrEmpty(delivery.terminalResolutionId))
        {
            failure = "손님 요청 납품 결과의 ACK 정리를 완료할 수 없습니다.";
            return false;
        }
        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        ResetGuestDelivery(delivery);
        state.activeEvents.Remove(active);
        AddResolvedSociety(state, active);
        MarkEventRecurrence(
            state,
            definition,
            active,
            active.startedAbsoluteDay);
        return true;
    }

    public bool TrySetInputOwnerProjection(
        string factionId,
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (string.IsNullOrWhiteSpace(state.activeContractId)
            || !state.activeContractInputOwnerActive
            || capacityGrams <= 0L
            || massAuthorityRevision <= 0L
            || string.IsNullOrWhiteSpace(capacityFingerprint))
        {
            failure = "세력 계약 물리 목적지 projection이 유효하지 않습니다.";
            return false;
        }
        state.activeContractInputCapacityGrams = capacityGrams;
        state.activeContractInputMassAuthorityRevision =
            massAuthorityRevision;
        state.activeContractInputCapacityFingerprint =
            capacityFingerprint.Trim();
        return true;
    }

    public bool TryRecordDeliveryReceipt(
        string factionId,
        FactionContractDeliveryReceipt receipt,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (contract == null
            || material.Count == 0
            || !state.activeContractInputOwnerActive
            || state.activeContractInputCapacityGrams <= 0L
            || receipt.Quantity != material.Values.Sum()
            || receipt.InputMassGrams !=
                state.activeContractInputCapacityGrams
            || FactionContractDeliveryOutbox.HasPending(state))
        {
            failure = "세력 계약 인도 receipt를 기록할 수 없는 상태입니다.";
            return false;
        }
        try
        {
            FactionContractDeliveryOutbox.RecordPending(
                state,
                contract.StableId,
                state.activeContractOccurrenceId,
                receipt);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return false;
        }
        return true;
    }

    public bool TryResolveDeliveredContract(
        string factionId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId,
                StringComparison.Ordinal));
        if (contract == null
            || !FactionContractMaterialRules.HasMaterialRequirements(contract)
            || state.deliveryCommitPhase !=
                FactionContractDeliveryCommitPhase.PhysicalCommitted
            || !string.Equals(
                state.deliveryContractId,
                contract.StableId,
                StringComparison.Ordinal)
            || !FactionContractDeliveryOutbox.HasCanonicalPending(state))
        {
            failure = "실제 인도가 확정된 진행 중 계약이 없습니다.";
            return false;
        }
        state.completedContractIds.Add(contract.StableId);
        state.activeContractId = string.Empty;
        state.activeContractDeadlineAbsoluteDay = 0;
        state.deliveryTerminalCleanupPending =
            state.activeContractInputOwnerActive;
        FactionContractDeliveryOutbox.MarkRewardPublished(state);
        ApplyInternalEffects(contract.successEffects, state.factionId);
        result = new V20ResolvedEventResult(
            contract.StableId,
            "success",
            contract.successEffects.AsReadOnly(),
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public bool TrySetDeliveryFailure(
        string factionId,
        string failureReason,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (string.IsNullOrWhiteSpace(state.activeContractId)
            && !FactionContractDeliveryOutbox.HasPending(state)
            && !state.deliveryTerminalCleanupPending)
        {
            failure = "진행 중인 세력 계약 납품이 없습니다.";
            return false;
        }
        state.deliveryFailureReason = failureReason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryMarkInputOwnerRetired(
        string factionId,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (!state.activeContractInputOwnerActive)
            return true;
        if (!state.deliveryTerminalCleanupPending)
        {
            failure = "진행 중인 세력 계약의 입력 목적지는 종료할 수 없습니다.";
            return false;
        }
        state.activeContractInputOwnerActive = false;
        state.activeContractInputCapacityGrams = 0L;
        state.activeContractInputMassAuthorityRevision = 0L;
        state.activeContractInputCapacityFingerprint = string.Empty;
        state.deliveryTerminalCleanupPending = false;
        if (!FactionContractDeliveryOutbox.HasPending(state))
            ClearContractDestination(state);
        return true;
    }

    public bool TryClearDeliveryOutbox(
        string factionId,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (state.deliveryCommitPhase !=
                FactionContractDeliveryCommitPhase.RewardPublished
            || state.activeContractInputOwnerActive
            || state.deliveryTerminalCleanupPending)
        {
            failure = "세력 계약 인도 outbox를 종료할 수 없는 상태입니다.";
            return false;
        }
        FactionContractDeliveryOutbox.Clear(state);
        ClearContractDestination(state);
        return true;
    }

    public bool TryResolveContract(
        string factionId,
        bool success,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId, StringComparison.Ordinal));
        if (contract == null)
        {
            failure = "진행 중인 계약이 없습니다.";
            return false;
        }
        if (FactionContractMaterialRules.HasMaterialRequirements(contract))
        {
            failure = success
                ? "물자 계약은 실제 인도 receipt 이후에만 완료할 수 있습니다."
                : "물자 계약은 공개 포기 명령으로 실패시킬 수 없습니다.";
            return false;
        }
        if (success && !RequirementsSatisfied(contract.completionRequirements, requirements))
        {
            failure = "계약 완료 조건을 충족하지 못했습니다.";
            return false;
        }

        IReadOnlyList<V20ContentEffect> effects = success
            ? contract.successEffects
            : contract.failureEffects;
        if (success)
        {
            effects = WithConsumedRequirements(
                effects,
                contract.completionRequirements);
        }
        (success ? state.completedContractIds : state.failedContractIds)
            .Add(contract.StableId);
        state.activeContractId = string.Empty;
        state.activeContractDeadlineAbsoluteDay = 0;
        ApplyInternalEffects(effects, state.factionId);
        result = new V20ResolvedEventResult(
            contract.StableId,
            success ? "success" : "failure",
            effects,
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta)
    {
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (rapportDelta > 0)
        {
            rapportDelta = Math.Max(
                1,
                (int)Math.Round(
                    rapportDelta * GetEndlessCrisisMultiplier(
                        EndlessCrisisAxis.Faction),
                    MidpointRounding.AwayFromZero));
        }
        state.rapport = Math.Clamp(state.rapport + rapportDelta, -100, 100);
        state.grievance = Math.Clamp(state.grievance + grievanceDelta, 0, 100);
        state.obligationTokens = Math.Clamp(
            state.obligationTokens + obligationDelta,
            0,
            5);
    }

    public void ApplyResolvedEffects(
        IEnumerable<V20ContentEffect> effects,
        string contextFactionId = "") =>
        ApplyInternalEffects(effects, contextFactionId);

    public IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (state.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless crisis composition requires EndlessAge.");
        if (absoluteDay <= state.lastEndlessCrisisEvaluationAbsoluteDay)
            return state.endlessCrisisAxes
                .Select(value => value.effectOwnerId)
                .ToArray();

        state.lastEndlessCrisisEvaluationAbsoluteDay = absoluteDay;
        if (state.endlessCrisisPhase != EndlessCrisisLifecyclePhase.None)
            return state.endlessCrisisAxes
                .Select(value => value.effectOwnerId)
                .ToArray();

        EndlessCrisisPolicyDefinitionSO policy = catalog.EndlessCrisisPolicy;
        int cycle = state.endlessCycle + 1;
        string key = $"{runSeed}:endless:{absoluteDay}:{cycle}";
        bool single = PersistentEntityId.GetStableHash32(key + ":count")
            % 100u < (uint)policy.singleAxisProbabilityPercent;
        int axisCount = single ? 1 : 2;
        List<EndlessCrisisAxisPolicy> available = policy.axes
            .OrderBy(value => value.axis)
            .ToList();
        List<EndlessCrisisAxisPolicy> selected = new();
        for (int index = 0; index < axisCount; index++)
        {
            int selectedIndex = (int)(PersistentEntityId.GetStableHash32(
                    key + ":axis:" + index)
                % (uint)available.Count);
            selected.Add(available[selectedIndex]);
            available.RemoveAt(selectedIndex);
        }

        state.endlessCycle = cycle;
        state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.Active;
        state.endlessCrisisInstanceId =
            $"endless-crisis:{cycle}:{absoluteDay}";
        state.endlessCrisisAxes = selected
            .OrderBy(value => value.axis)
            .Select(value => new EndlessCrisisAxisSaveData
            {
                axis = value.axis,
                effectOwnerId = EffectOwnerId(
                    state.endlessCrisisInstanceId,
                    value.axis),
                multiplier = single
                    ? value.singleAxisMultiplier
                    : value.compoundAxisMultiplier
            })
            .ToList();
        state.endlessCrisisStartedAbsoluteDay = absoluteDay;
        state.endlessCrisisPressureEndAbsoluteDay = checked(
            absoluteDay + policy.pressureDurationDays);
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisRecoveryEndAbsoluteDay = -1;
        state.endlessCrisisAuthoredRecoveryDays = single
            ? policy.singleAxisRecoveryDays
            : policy.compoundAxisRecoveryDays;
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
        return state.endlessCrisisAxes
            .Select(value => value.effectOwnerId)
            .ToArray();
    }

    public EndlessCrisisSnapshot AdvanceEndlessCrisis(int absoluteDay)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (state.endlessCrisisPhase == EndlessCrisisLifecyclePhase.Active
            && absoluteDay >= state.endlessCrisisPressureEndAbsoluteDay)
        {
            state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
            if (state.endlessCrisisDirectResponseOwnerId.Length > 0)
            {
                state.endlessCrisisPhase =
                    EndlessCrisisLifecyclePhase.AwaitingDirectResponse;
            }
            else
            {
                BeginEndlessCrisisRecovery(
                    state,
                    state.endlessCrisisPressureEndAbsoluteDay);
            }
        }
        else if (state.endlessCrisisPhase
                    == EndlessCrisisLifecyclePhase.Recovery
            && absoluteDay >= state.endlessCrisisRecoveryEndAbsoluteDay)
        {
            state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.None;
        }
        return ProjectEndlessCrisis(state);
    }

    public bool TryOwnInvasionCandidate(string effectOwnerId)
    {
        if (!IsCanonicalRequiredId(effectOwnerId))
            return false;
        RunMilestoneWorldSaveData current = Milestones.Data;
        bool ownsCombat = current.endlessCrisisPhase
                == EndlessCrisisLifecyclePhase.Active
            && current.endlessCrisisAxes.Any(value =>
                value != null
                && value.axis == EndlessCrisisAxis.Combat
                && string.Equals(
                    value.effectOwnerId,
                    effectOwnerId,
                    StringComparison.Ordinal));
        if (!ownsCombat
            || current.endlessCrisisDirectResponseOwnerId.Length > 0)
            return false;
        if (current.endlessCrisisPendingInvasionSourceOwnerId.Length > 0)
            return string.Equals(
                current.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisPendingInvasionSourceOwnerId = effectOwnerId;
        return true;
    }

    public bool TryBeginOwnedInvasion(
        string effectOwnerId,
        int absoluteDay,
        out string directResponseOwnerId)
    {
        directResponseOwnerId = string.Empty;
        if (absoluteDay < 1 || !IsCanonicalRequiredId(effectOwnerId))
            return false;
        RunMilestoneWorldSaveData current = Milestones.Data;
        bool ownsCombat = current.endlessCrisisAxes.Any(value =>
            value != null
            && value.axis == EndlessCrisisAxis.Combat
            && string.Equals(
                value.effectOwnerId,
                effectOwnerId,
                StringComparison.Ordinal));
        if (current.endlessCrisisPhase
                != EndlessCrisisLifecyclePhase.Active
            || !ownsCombat
            || !string.Equals(
                current.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal)
            || current.endlessCrisisDirectResponseOwnerId.Length > 0)
            return false;
        AdvanceEndlessCrisis(absoluteDay);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        ownsCombat = state.endlessCrisisAxes.Any(value =>
            value != null
            && value.axis == EndlessCrisisAxis.Combat
            && string.Equals(
                value.effectOwnerId,
                effectOwnerId,
                StringComparison.Ordinal));
        if (state.endlessCrisisPhase != EndlessCrisisLifecyclePhase.Active
            || !ownsCombat
            || !string.Equals(
                state.endlessCrisisPendingInvasionSourceOwnerId,
                effectOwnerId,
                StringComparison.Ordinal))
            return false;
        string expected = effectOwnerId + ":invasion";
        if (state.endlessCrisisDirectResponseOwnerId.Length > 0
            && !string.Equals(
                state.endlessCrisisDirectResponseOwnerId,
                expected,
                StringComparison.Ordinal))
            return false;
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisDirectResponseOwnerId = expected;
        directResponseOwnerId = expected;
        return true;
    }

    public bool TryResolveOwnedInvasion(
        string directResponseOwnerId,
        int absoluteDay)
    {
        if (absoluteDay < 1 || string.IsNullOrWhiteSpace(directResponseOwnerId))
            return false;
        AdvanceEndlessCrisis(absoluteDay);
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (!string.Equals(
                state.endlessCrisisDirectResponseOwnerId,
                directResponseOwnerId,
                StringComparison.Ordinal))
            return false;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
        if (state.endlessCrisisPhase
            == EndlessCrisisLifecyclePhase.AwaitingDirectResponse)
        {
            BeginEndlessCrisisRecovery(state, absoluteDay);
        }
        else if (state.endlessCrisisPhase
            == EndlessCrisisLifecyclePhase.Active)
        {
            state.endlessCrisisLastActualEndAbsoluteDay = absoluteDay;
        }
        return true;
    }

    private static void BeginEndlessCrisisRecovery(
        RunMilestoneWorldSaveData state,
        int absoluteDay)
    {
        state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.Recovery;
        state.endlessCrisisLastActualEndAbsoluteDay = absoluteDay;
        state.endlessCrisisRecoveryEndAbsoluteDay = checked(
            absoluteDay + state.endlessCrisisAuthoredRecoveryDays);
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
    }

    private EndlessCrisisSnapshot ProjectEndlessCrisis(
        RunMilestoneWorldSaveData state)
    {
        EndlessCrisisAxisModifierSnapshot[] axes =
            (state.endlessCrisisAxes ?? new List<EndlessCrisisAxisSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.axis)
            .Select(value =>
            {
                EndlessCrisisAxisPolicy authored =
                    catalog.EndlessCrisisPolicy.Require(value.axis);
                return new EndlessCrisisAxisModifierSnapshot(
                    value.axis,
                    authored.displayName,
                    value.effectOwnerId,
                    value.multiplier);
            })
            .ToArray();
        return new EndlessCrisisSnapshot(
            state.endlessCrisisPhase,
            state.endlessCrisisInstanceId,
            Array.AsReadOnly(axes),
            state.endlessCrisisStartedAbsoluteDay,
            state.endlessCrisisPressureEndAbsoluteDay,
            state.endlessCrisisLastActualEndAbsoluteDay,
            state.endlessCrisisRecoveryEndAbsoluteDay,
            state.lastEndlessCrisisEvaluationAbsoluteDay,
            state.endlessCrisisAuthoredRecoveryDays,
            state.endlessCrisisPendingInvasionSourceOwnerId,
            state.endlessCrisisDirectResponseOwnerId);
    }

    private static string EffectOwnerId(
        string instanceId,
        EndlessCrisisAxis axis) =>
        $"{instanceId}:axis:{(int)axis}";

    public SeasonalEventWorldSaveData CaptureSeasonal() => Clone(Seasonal.Data);
    public SocietyEventWorldSaveData CaptureSociety() => Clone(Society.Data);
    public FactionCampaignWorldSaveData CaptureFactions() => Clone(Factions.Data);
    public RunMilestoneWorldSaveData CaptureMilestones() => Clone(Milestones.Data);
    public SeasonalEventAggregateState PrepareSeasonal(SeasonalEventWorldSaveData data) => new() { Data = ValidateSeasonal(Clone(data)) };
    public SeasonalEventAggregateState PrepareSeasonalRestore(
        SeasonalEventWorldSaveData data)
    {
        if (preparedSeasonalRestore != null)
            throw new InvalidOperationException(
                "A seasonal restore candidate is already staged.");
        SeasonalEventAggregateState candidate = PrepareSeasonal(data);
        candidate.AttachRestoreOwner(this);
        preparedSeasonalRestore = candidate;
        return candidate;
    }
    public SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data) => new() { Data = ValidateSociety(Clone(data)) };
    public FactionCampaignAggregateState PrepareFactions(FactionCampaignWorldSaveData data) => new() { Data = ValidateFactions(Clone(data), Seasonal.Data) };
    public FactionCampaignAggregateState PrepareFactionsPreflight(
        FactionCampaignWorldSaveData data) => new()
    {
        Data = ValidateFactions(Clone(data), seasonalData: null)
    };
    public FactionCampaignAggregateState PrepareFactionsRestore(
        FactionCampaignWorldSaveData data)
    {
        if (preparedSeasonalRestore == null)
            throw new InvalidOperationException(
                "Faction campaign restore requires the staged seasonal candidate.");
        return new FactionCampaignAggregateState
        {
            Data = ValidateFactions(
                Clone(data),
                preparedSeasonalRestore.Data)
        };
    }
    public RunMilestoneAggregateState PrepareMilestones(RunMilestoneWorldSaveData data) => new() { Data = ValidateMilestones(Clone(data)) };
    public void PublishSeasonal(SeasonalEventAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishSeasonalRestore(SeasonalEventAggregateState state)
    {
        if (!ReferenceEquals(preparedSeasonalRestore, state))
            throw new InvalidOperationException(
                "Seasonal restore candidate does not match the staged candidate.");
        state.ReleaseRestoreOwner(this);
        preparedSeasonalRestore = null;
        rootStore.Replace(state);
    }

    internal void DiscardSeasonalRestore(SeasonalEventAggregateState state)
    {
        if (ReferenceEquals(preparedSeasonalRestore, state))
            preparedSeasonalRestore = null;
    }
    public void PublishSociety(SocietyEventAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishFactions(FactionCampaignAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishMilestones(RunMilestoneAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishContentResolution(
        SeasonalEventAggregateState seasonal,
        SocietyEventAggregateState society,
        FactionCampaignAggregateState factions,
        RunMilestoneAggregateState milestones) =>
        rootStore.PublishPreparedReplacements(
            seasonal ?? throw new ArgumentNullException(nameof(seasonal)),
            society ?? throw new ArgumentNullException(nameof(society)),
            factions ?? throw new ArgumentNullException(nameof(factions)),
            milestones ?? throw new ArgumentNullException(nameof(milestones)));

    public void PublishContentResolution(
        SeasonalEventAggregateState seasonal,
        SocietyEventAggregateState society,
        FactionCampaignAggregateState factions,
        RunMilestoneAggregateState milestones,
        CharacterCareerAggregate careers) =>
        rootStore.PublishPreparedReplacements(
            seasonal ?? throw new ArgumentNullException(nameof(seasonal)),
            society ?? throw new ArgumentNullException(nameof(society)),
            factions ?? throw new ArgumentNullException(nameof(factions)),
            milestones ?? throw new ArgumentNullException(nameof(milestones)),
            careers ?? throw new ArgumentNullException(nameof(careers)));

    private SeasonalEventAggregateState Seasonal => rootStore.GetOrCreate(() => new SeasonalEventAggregateState());
    private SocietyEventAggregateState Society => rootStore.GetOrCreate(() => new SocietyEventAggregateState());
    private FactionCampaignAggregateState Factions => rootStore.GetOrCreate(() => new FactionCampaignAggregateState());
    private RunMilestoneAggregateState Milestones => rootStore.GetOrCreate(() => new RunMilestoneAggregateState());
    private RunMilestoneAggregateState WritableMilestones => rootStore.GetOrCreateWritable(
        () => new RunMilestoneAggregateState(), value => new RunMilestoneAggregateState { Data = Clone(value.Data) });
    private SeasonalEventAggregateState WritableSeasonal => rootStore.GetOrCreateWritable(
        () => new SeasonalEventAggregateState(),
        value => new SeasonalEventAggregateState { Data = Clone(value.Data) });
    private SocietyEventAggregateState WritableSociety => rootStore.GetOrCreateWritable(
        () => new SocietyEventAggregateState(),
        value => new SocietyEventAggregateState { Data = Clone(value.Data) });
    private FactionCampaignAggregateState WritableFactions => rootStore.GetOrCreateWritable(
        () => new FactionCampaignAggregateState(),
        value => new FactionCampaignAggregateState { Data = Clone(value.Data) });

    private static bool RequirementsSatisfied(V20ContentRequirementSet requirements, RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) return false;
        requirements ??= new V20ContentRequirementSet();
        return (requirements.research ?? new()).All(value => snapshot.CompletedResearchIds.Contains(value.researchNumericId))
            && (requirements.requiredFlags ?? new()).All(snapshot.WorldFlags.Contains)
            && (requirements.excludedFlags ?? new()).All(value => !snapshot.WorldFlags.Contains(value))
            && (requirements.worldMetrics ?? new()).All(value => snapshot.WorldMetrics.TryGetValue(value.kind, out float actual) && actual >= value.minimumValue)
            && (requirements.items ?? new()).All(value => snapshot.ItemQuantities.TryGetValue(value.itemDefinitionId, out int quantity) && quantity >= value.amount)
            && (requirements.facilities ?? new()).All(value => snapshot.FacilityCounts.TryGetValue(FacilityRequirementKey(value), out int count) && count >= value.minimumCount)
            && (requirements.factions ?? new()).All(value => snapshot.Factions.TryGetValue(value.factionId, out FactionCampaignStateSaveData faction)
                && faction.rapport >= value.minimumRapport
                && faction.grievance <= value.maximumGrievance
                && faction.obligationTokens >= value.minimumObligationTokens)
            && ((requirements.characters?.Count ?? 0) == 0
                || snapshot.EligibleCharacterCount >= requirements.characters.Count);
    }

    private static IReadOnlyList<V20ContentEffect> WithConsumedRequirements(
        IEnumerable<V20ContentEffect> effects,
        V20ContentRequirementSet requirements)
    {
        List<V20ContentEffect> result = (effects ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .ToList();
        foreach (IGrouping<string, V20ItemAmountRequirement> group in
                 (requirements?.items ?? new List<V20ItemAmountRequirement>())
                 .Where(value => value != null
                     && value.consume
                     && !string.IsNullOrWhiteSpace(value.itemDefinitionId))
                 .GroupBy(value => value.itemDefinitionId.Trim(), StringComparer.Ordinal))
        {
            result.Add(new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = group.Key,
                amount = group.Sum(value => Math.Max(0, value.amount))
            });
        }
        return result.AsReadOnly();
    }

    private void EvaluateSeasonal(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved)
    {
        SeasonalEventWorldSaveData state = WritableSeasonal.Data;
        if (state.lastEvaluationAbsoluteDay >= context.AbsoluteDay) return;
        state.lastEvaluationAbsoluteDay = context.AbsoluteDay;
        int cycle = context.AbsoluteDay / GameCalendarRules.DaysPerYear;
        if (state.cycle != cycle)
        {
            state.cycle = cycle;
            state.completedEventIds.Clear();
        }

        foreach (V20ActiveEventSaveData active in state.activeEvents
                     .Where(value => value.deadlineAbsoluteDay < context.AbsoluteDay)
                     .ToArray())
        {
            if (active.seasonalDriftCargo?.configured == true
                && !active.seasonalDriftCargo.expirationCompleted)
            {
                continue;
            }
            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(active.definitionId);
            active.resolved = true;
            active.resolutionId = "completed";
            state.activeEvents.Remove(active);
            state.completedEventIds.Add(definition.StableId);
            ApplyInternalEffects(definition.endEffects, active.contextFactionId);
            resolved.Add(new V20ResolvedEventResult(
                definition.StableId,
                "completed",
                definition.endEffects.AsReadOnly(),
                context.ParticipantCharacterIds.AsReadOnly(),
                active.contextFactionId,
                occurrenceInstanceId: active.instanceId));
        }

        foreach (V20ActiveEventSaveData active in state.activeEvents.ToArray())
        {
            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(active.definitionId);
            ApplyInternalEffects(
                definition.dailyEffects,
                active.contextFactionId);
            if ((definition.dailyEffects?.Count ?? 0) > 0)
            {
                resolved.Add(new V20ResolvedEventResult(
                    definition.StableId,
                    "daily",
                    definition.dailyEffects.AsReadOnly(),
                    context.ParticipantCharacterIds.AsReadOnly(),
                    active.contextFactionId));
            }
        }

        if (state.activeEvents.Count > 0) return;
        SeasonalWorldEventDefinitionSO[] eligible = catalog.SeasonalEvents
            .Where(value => value.season == context.Season
                && !state.completedEventIds.Contains(value.StableId, StringComparer.Ordinal)
                && (value.requiredWeatherFrontId.Length == 0
                    || string.Equals(
                        value.requiredWeatherFrontId,
                        context.WeatherFrontId,
                        StringComparison.Ordinal))
                && HasSeasonalFeedSelfHeatingTarget(value)
                && RequirementsSatisfied(value.triggerRequirements, context.Requirements))
            .ToArray();
        if (eligible.Length == 0) return;
        SeasonalWorldEventDefinitionSO selected = SelectDeterministic(
            eligible,
            value => value.StableId,
            context,
            "seasonal");
        uint roll = EventRoll(context, selected.StableId);
        int duration = selected.minimumDurationDays
            + (int)(roll % (uint)(selected.maximumDurationDays
                - selected.minimumDurationDays + 1));
        V20ActiveEventSaveData created = CreateEvent(
            selected.StableId,
            context,
            duration,
            Array.Empty<string>());
        created.seasonalWildlifeArrival =
            SeasonalWildlifeArrivalSaveData.FromProfile(
                selected.wildlifeArrivalProfile);
        created.seasonalDriftCargo = SeasonalDriftCargoSaveData.FromProfile(
            selected.driftCargoProfile);
        created.seasonalManaLightning =
            SeasonalManaLightningSaveData.FromProfile(
                selected.manaLightningProfile);
        created.seasonalSpecialExpedition =
            SeasonalSpecialExpeditionSaveData.FromProfile(
                selected.specialExpeditionProfile);
        state.activeEvents.Add(created);
        ApplyInternalEffects(selected.startEffects, created.contextFactionId);
        resolved.Add(new V20ResolvedEventResult(
            selected.StableId,
            "started",
            selected.startEffects.AsReadOnly(),
            context.ParticipantCharacterIds.AsReadOnly(),
            created.contextFactionId,
            occurrenceInstanceId: created.instanceId));
    }

    private bool HasSeasonalFeedSelfHeatingTarget(
        SeasonalWorldEventDefinitionSO definition)
    {
        SeasonalFeedSelfHeatingFireProfile profile =
            definition.feedSelfHeatingFireProfile;
        if (profile?.IsConfigured != true)
        {
            return true;
        }

        if (SeasonalFeedSelfHeatingTargets.TrySelect(
                profile,
                out _,
                out SeasonalFeedSelfHeatingTargetFailure reason))
        {
            return true;
        }
        if (reason.Code == SeasonalFeedSelfHeatingTargetFailureCode
                .NoEligiblePhysicalStock)
        {
            return false;
        }

        throw new InvalidOperationException(
            $"Seasonal event '{definition.StableId}' target query "
            + $"failed with {reason.Code}: {reason.Reason}");
    }

    private void EvaluateSociety(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved)
    {
        SocietyEventWorldSaveData state = WritableSociety.Data;
        if (state.lastEvaluationAbsoluteDay >= context.AbsoluteDay) return;
        state.lastEvaluationAbsoluteDay = context.AbsoluteDay;
        state.cooldowns.RemoveAll(value =>
            value == null || value.availableAbsoluteDay <= context.AbsoluteDay);

        foreach (V20ActiveEventSaveData active in state.activeEvents
                     .Where(value => value.deadlineAbsoluteDay < context.AbsoluteDay
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             == GuestRequestDeliveryPhase.None
                         && (value.observedIncidentResponse?.phase
                                 ?? ObservedIncidentResponsePhase.None)
                             == ObservedIncidentResponsePhase.None)
                     .ToArray())
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(active.definitionId);
            IReadOnlyList<V20ContentEffect> effects = ExpirationEffects(definition);
            active.resolved = true;
            active.resolutionId = "expired";
            FreezeSocietyTerminalOutcome(
                active,
                definition,
                "기한 만료",
                RequireAuthoredNarrative(definition, null),
                effects);
            state.activeEvents.Remove(active);
            AddResolvedSociety(state, active);
            MarkEventRecurrence(
                state,
                definition,
                active,
                context.AbsoluteDay);
            ApplyInternalEffects(effects, active.contextFactionId);
            resolved.Add(CreateSocietyResolvedResult(
                active,
                definition,
                effects));
        }

        foreach (FactionCampaignStateSaveData faction in WritableFactions.Data.factions)
        {
            if (string.IsNullOrWhiteSpace(faction.activeContractId)
                || faction.activeContractDeadlineAbsoluteDay >= context.AbsoluteDay
                || FactionContractDeliveryOutbox.HasPending(faction))
                continue;
            FactionContractDefinitionSO contract = catalog.Contracts.Single(value =>
                string.Equals(value.StableId, faction.activeContractId, StringComparison.Ordinal));
            faction.failedContractIds.Add(contract.StableId);
            faction.activeContractId = string.Empty;
            faction.activeContractDeadlineAbsoluteDay = 0;
            faction.deliveryTerminalCleanupPending =
                faction.activeContractInputOwnerActive;
            faction.deliveryFailureReason = "faction-contract-deadline-expired";
            ApplyInternalEffects(contract.failureEffects, faction.factionId);
            resolved.Add(new V20ResolvedEventResult(
                contract.StableId,
                "expired",
                contract.failureEffects.AsReadOnly(),
                Array.Empty<string>(),
                faction.factionId));
        }

        string[] participants = context.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> occupied = state.activeEvents
            .SelectMany(value => value.participantCharacterIds)
            .ToHashSet(StringComparer.Ordinal);

        LifeEventDefinitionSO[] automatic = catalog.LifeEvents
            .Where(value => value.automatic
                && CanStartSocietyDefinition(
                    value,
                    context,
                    state,
                    dailyCadenceCandidate: true))
            .OrderBy(value => WeightedDefinitionRoll(context, value))
            .ToArray();
        int automaticCount = 0;
        foreach (LifeEventDefinitionSO definition in automatic)
        {
            int requiredParticipantCount = definition.automaticEffects.Any(
                value => value != null
                    && value.kind == V20ContentEffectKind.Relationship)
                ? 2
                : 1;
            string[] selectedParticipants = participants
                .Where(value => !occupied.Contains(value)
                    && CanAssignLifeEvent(
                        definition,
                        value,
                        context,
                        state))
                .OrderBy(value => WeightedParticipantRoll(
                    context,
                    definition,
                    value))
                .Take(requiredParticipantCount)
                .ToArray();
            if (selectedParticipants.Length != requiredParticipantCount)
                continue;
            foreach (string participant in selectedParticipants)
                occupied.Add(participant);
            V20ActiveEventSaveData completed = CreateEvent(
                definition.StableId,
                context,
                0,
                selectedParticipants,
                definition);
            completed.resolved = true;
            completed.resolutionId = "automatic";
            FreezeSocietyTerminalOutcome(
                completed,
                definition,
                "자동 처리",
                RequireAuthoredNarrative(definition, null),
                definition.automaticEffects);
            AddResolvedSociety(state, completed);
            MarkEventRecurrence(
                state,
                definition,
                completed,
                context.AbsoluteDay);
            ApplyInternalEffects(
                definition.automaticEffects,
                completed.contextFactionId);
            resolved.Add(CreateSocietyResolvedResult(
                completed,
                definition,
                definition.automaticEffects));
            automaticCount++;
            if (automaticCount >= 6) break;
        }

        int ordinaryCap = context.AbsoluteDay <= 30 ? 1 : 2;
        int currentEmergency = state.activeEvents.Count(value =>
            IsEmergency(((ISocietyEventCatalog)catalog).Require(value.definitionId)));
        int currentOrdinary = state.activeEvents.Count - currentEmergency;
        int capacity = Math.Max(0, ordinaryCap - currentOrdinary);
        IEnumerable<V20AuthoredContentSO> majorCandidates = catalog.LifeEvents
            .Where(value => !value.automatic)
            .Cast<V20AuthoredContentSO>()
            .Concat(catalog.GuestRequests)
            .Where(value => CanStartSocietyDefinition(
                value,
                context,
                state,
                dailyCadenceCandidate: true))
            .OrderBy(value => WeightedDefinitionRoll(context, value));
        foreach (V20AuthoredContentSO definition in majorCandidates)
        {
            bool emergency = IsEmergency(definition);
            if ((emergency && currentEmergency > 0)
                || (!emergency && capacity <= 0))
                continue;
            string participant = participants
                .Where(value => !occupied.Contains(value)
                    && (definition is not LifeEventDefinitionSO life
                        || CanAssignLifeEvent(
                            life,
                            value,
                            context,
                            state)))
                .OrderBy(value => WeightedParticipantRoll(
                    context,
                    definition,
                    value))
                .FirstOrDefault();
            if (participant == null && definition is LifeEventDefinitionSO)
                continue;
            string[] selectedParticipants = participant == null
                ? Array.Empty<string>()
                : new[] { participant };
            state.activeEvents.Add(CreateEvent(
                definition.StableId,
                context,
                DeadlineDays(definition),
                selectedParticipants,
                definition));
            if (participant != null) occupied.Add(participant);
            if (emergency) currentEmergency++;
            else capacity--;
            if (capacity <= 0 && currentEmergency > 0) break;
        }
    }

    private static double WeightedDefinitionRoll(
        V20DailyEventContext context,
        V20AuthoredContentSO definition)
    {
        float weight = context.ParticipantCharacterIds
            .Select(characterId => ContentWeight(context, characterId, definition))
            .DefaultIfEmpty(1f)
            .Max();
        return EventRoll(context, definition.StableId) / Math.Max(0.1f, weight);
    }

    private static double WeightedParticipantRoll(
        V20DailyEventContext context,
        V20AuthoredContentSO definition,
        string characterId) =>
        PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{context.AbsoluteDay}:{definition.StableId}:{characterId}")
        / Math.Max(0.1f, ContentWeight(context, characterId, definition));

    private static float ContentWeight(
        V20DailyEventContext context,
        string characterId,
        V20AuthoredContentSO definition)
    {
        if (!context.ParticipantContentWeights.TryGetValue(
                characterId,
                out Dictionary<string, float> weights))
            return 1f;
        float result = 1f;
        foreach (string key in ContentWeightKeys(definition))
            if (weights.TryGetValue(key, out float value)) result *= value;
        return Math.Clamp(result, 0.1f, 10f);
    }

    private static IEnumerable<string> ContentWeightKeys(
        V20AuthoredContentSO definition)
    {
        yield return definition.StableId;
        switch (definition)
        {
            case LifeEventDefinitionSO life:
                yield return "life-event";
                yield return KebabCase(life.category.ToString());
                break;
            case GuestRequestDefinitionSO:
                yield return "guest-request";
                break;
            case ServiceIncidentDefinitionSO:
                yield return "service-incident";
                break;
        }
    }

    private static string KebabCase(string value) => string.Concat(
        (value ?? string.Empty).Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));

    private bool CanStartSocietyDefinition(
        V20AuthoredContentSO definition,
        V20DailyEventContext context,
        SocietyEventWorldSaveData state,
        bool dailyCadenceCandidate = false)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (definition is LifeEventDefinitionSO lifeEvent
            && RequireOccurrencePolicy(lifeEvent) != LifeEventOccurrencePolicy.DailyCadence
            && dailyCadenceCandidate)
            return false;
        if (state.activeEvents.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.activeEvents.Any(value => string.Equals(
                CategoryKey(((ISocietyEventCatalog)catalog).Require(
                    value.definitionId)),
                CategoryKey(definition),
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                CategoryKey(definition),
                StringComparison.Ordinal)))
            return false;
        return RequirementsSatisfied(TriggerRequirements(definition), context.Requirements);
    }

    private static LifeEventOccurrencePolicy RequireOccurrencePolicy(
        LifeEventDefinitionSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (!Enum.IsDefined(
                typeof(LifeEventOccurrencePolicy),
                definition.occurrencePolicy))
            throw new InvalidOperationException(
                $"Life event '{definition.StableId}' has invalid occurrence policy "
                + $"'{definition.occurrencePolicy}'.");
        return definition.occurrencePolicy;
    }

    private static bool CanAssignLifeEvent(
        LifeEventDefinitionSO definition,
        string participantCharacterId,
        V20DailyEventContext context,
        SocietyEventWorldSaveData state)
    {
        if (definition == null
            || context == null
            || string.IsNullOrWhiteSpace(participantCharacterId)
            || RequiresRetirementSchedule(definition)
                && !context.RetirementEligibleParticipantIds.Contains(
                    participantCharacterId))
            return false;
        return definition.frequencyRule switch
        {
            LifeEventFrequencyRule.Repeatable => true,
            LifeEventFrequencyRule.OncePerRun =>
                !state.completedOnceEventIds.Contains(
                    definition.StableId,
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerGeneration =>
                !state.recurrenceKeys.Contains(
                    GenerationRecurrenceKey(
                        definition.StableId,
                        context.Generation),
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerCharacter =>
                !state.recurrenceKeys.Contains(
                    CharacterRecurrenceKey(
                        definition.StableId,
                        participantCharacterId),
                    StringComparer.Ordinal),
            _ => false
        };
    }

    private static bool RequiresRetirementSchedule(
        LifeEventDefinitionSO definition) =>
        (definition?.choices ?? new List<V20ChoiceDefinition>())
            .Where(value => value != null)
            .SelectMany(value => value.effects ?? new List<V20ContentEffect>())
            .Any(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);

    private void ApplyInternalEffects(
        IEnumerable<V20ContentEffect> source,
        string contextFactionId = "")
    {
        foreach (V20ContentEffect effect in source ?? Array.Empty<V20ContentEffect>())
        {
            if (effect == null || !effect.IsValid) continue;
            int amount = (int)Math.Round(effect.amount);
            switch (effect.kind)
            {
                case V20ContentEffectKind.FactionRapport:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        amount,
                        0,
                        0);
                    break;
                case V20ContentEffectKind.FactionGrievance:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        0,
                        amount,
                        0);
                    break;
                case V20ContentEffectKind.FactionObligation:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        0,
                        0,
                        amount);
                    break;
                case V20ContentEffectKind.WorldFlag:
                    if (!WritableMilestones.Data.worldFlags.Contains(
                            effect.targetId,
                            StringComparer.Ordinal))
                        WritableMilestones.Data.worldFlags.Add(effect.targetId);
                    break;
                case V20ContentEffectKind.MilestonePressure:
                case V20ContentEffectKind.Threat:
                    if (!WritableMilestones.Data.activePressureIds.Contains(
                            effect.targetId,
                            StringComparer.Ordinal))
                        WritableMilestones.Data.activePressureIds.Add(effect.targetId);
                    break;
                case V20ContentEffectKind.WorkDelayDays:
                    ApplyWorkDelayAt(
                        effect.targetId,
                        amount,
                        evaluationAbsoluteDay >= 0
                            ? evaluationAbsoluteDay
                            : Math.Max(
                                0,
                                Society.Data.lastEvaluationAbsoluteDay));
                    break;
            }
        }
    }

    private void ApplyWorkDelayAt(
        string authoredScopeId,
        int days,
        int currentDay)
    {
        if (days == 0) return;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        state.workDelays.RemoveAll(value => value == null
            || value.untilAbsoluteDayExclusive <= currentDay);
        string scope = NormalizeWorkDelayScope(authoredScopeId);
        V20WorkDelaySaveData existing = state.workDelays.FirstOrDefault(value =>
            string.Equals(value.scopeId, scope, StringComparison.Ordinal));
        if (days > 0)
        {
            existing ??= new V20WorkDelaySaveData { scopeId = scope };
            if (!state.workDelays.Contains(existing)) state.workDelays.Add(existing);
            existing.untilAbsoluteDayExclusive = Math.Max(
                currentDay,
                existing.untilAbsoluteDayExclusive) + days;
            return;
        }

        if (existing != null)
            existing.untilAbsoluteDayExclusive = Math.Max(
                currentDay,
                existing.untilAbsoluteDayExclusive + days);
        else if (scope == "global")
        {
            foreach (V20WorkDelaySaveData delay in state.workDelays)
                delay.untilAbsoluteDayExclusive = Math.Max(
                    currentDay,
                    delay.untilAbsoluteDayExclusive + days);
        }
        state.workDelays.RemoveAll(value =>
            value.untilAbsoluteDayExclusive <= currentDay);
    }

    private static string NormalizeWorkDelayScope(string value) =>
        string.IsNullOrWhiteSpace(value) ? "global" : value.Trim();

    private static bool WorkDelayAffects(string scopeId, string workTypeId)
    {
        string scope = NormalizeWorkDelayScope(scopeId);
        if (scope == "global") return true;
        if (scope == "flood")
            return WorkTypeCatalog.TryGet(
                    workTypeId,
                    out WorkTypeDefinition definition)
                && definition.CapabilityId is "crop:sow"
                    or "crop:harvest"
                    or "item:haul"
                    or "building:stock";
        if (scope is "road" or "whiteout")
            return ContainsAny(workTypeId, "expedition", "haul", "logistic", "carry", "trade");
        if (workTypeId.IndexOf(scope, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Authored service, life-event and faction-work delays describe the
        // operational cost of resolving the choice, not a WorkTypeId. They
        // must therefore slow the whole settlement unless the author used one
        // of the explicit environmental scopes handled above.
        return scope.StartsWith("service-incident:", StringComparison.Ordinal)
            || scope.StartsWith("life-event:", StringComparison.Ordinal)
            || scope.StartsWith("faction-work:", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

    private string ResolveFactionTarget(
        string authoredTargetId,
        string contextFactionId)
    {
        string target = Normalize(authoredTargetId);
        if (target is "requesting-faction" or "affected-faction")
            target = Normalize(contextFactionId);
        if (WritableFactions.Data.factions.Any(value => string.Equals(
                value.factionId,
                target,
                StringComparison.Ordinal)))
            return target;
        throw new InvalidOperationException(
            $"Faction effect target '{authoredTargetId}' has no valid campaign faction context.");
    }

    private void EnsureFactionStates()
    {
        FactionCampaignWorldSaveData state = WritableFactions.Data;
        foreach (FactionArcDefinitionSO arc in catalog.Arcs)
        {
            if (state.factions.All(value => !string.Equals(
                    value.factionId,
                    arc.factionId,
                    StringComparison.Ordinal)))
            {
                state.factions.Add(new FactionCampaignStateSaveData
                {
                    factionId = arc.factionId,
                    currentChapter = 1
                });
            }
        }
        state.factions = state.factions
            .OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ToList();
    }

    private FactionCampaignStateSaveData RequireWritableFaction(string factionId)
    {
        string normalized = Normalize(factionId);
        FactionCampaignStateSaveData state = WritableFactions.Data.factions
            .FirstOrDefault(value => string.Equals(
                value.factionId,
                normalized,
                StringComparison.Ordinal));
        return state ?? throw new KeyNotFoundException(
            $"Unknown V20 faction campaign '{normalized}'.");
    }

    private V20ActiveEventSaveData RequireWritableSocietyEvent(
        string instanceId)
    {
        string normalized = Normalize(instanceId);
        V20ActiveEventSaveData active = WritableSociety.Data.activeEvents
            .FirstOrDefault(value => value != null && string.Equals(
                value.instanceId,
                normalized,
                StringComparison.Ordinal));
        return active ?? throw new KeyNotFoundException(
            $"Unknown active society event '{normalized}'.");
    }

    private GuestRequestDeliverySaveData RequireWritableGuestDelivery(
        string instanceId)
    {
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        return active.guestDelivery ??= new GuestRequestDeliverySaveData();
    }

    private static void ResetGuestDelivery(
        GuestRequestDeliverySaveData delivery)
    {
        if (delivery == null)
            throw new ArgumentNullException(nameof(delivery));
        delivery.phase = GuestRequestDeliveryPhase.None;
        delivery.acceptedActionId = string.Empty;
        delivery.venueFacilityInstanceId = string.Empty;
        delivery.destinationId = string.Empty;
        delivery.destinationX = 0;
        delivery.destinationY = 0;
        delivery.inputOwnerActive = false;
        delivery.inputCapacityGrams = 0L;
        delivery.inputMassAuthorityRevision = 0L;
        delivery.inputCapacityFingerprint = string.Empty;
        GuestRequestDeliveryOutbox.ClearReceipt(delivery);
        delivery.terminalResolutionId = string.Empty;
        delivery.failureReason = string.Empty;
    }

    private static void ClearContractDestination(
        FactionCampaignStateSaveData state)
    {
        state.activeContractOccurrenceId = string.Empty;
        state.activeContractDestinationId = string.Empty;
        state.activeContractDestinationOwnerId = string.Empty;
        state.activeContractDestinationX = 0;
        state.activeContractDestinationY = 0;
        state.activeContractInputCapacityGrams = 0L;
        state.activeContractInputMassAuthorityRevision = 0L;
        state.activeContractInputCapacityFingerprint = string.Empty;
    }

    private ObservedLifeEventCommitResult RecordObservedLifeEvent(
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId,
        string canonicalPayload,
        int absoluteDay,
        int generation,
        IEnumerable<string> sourceParticipants,
        string expectedDefinitionId,
        bool resolveAutomatically = true)
    {
        if (!Enum.IsDefined(typeof(ObservedLifeEventSourceKind), sourceKind)
            || string.IsNullOrWhiteSpace(sourceOperationId)
            || !string.Equals(
                sourceOperationId,
                sourceOperationId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(canonicalPayload)
            || absoluteDay < 1
            || generation < 0)
            throw new ArgumentException("Observed life-event receipt is invalid.");

        SocietyEventWorldSaveData state = WritableSociety.Data;
        ObservedLifeEventReceiptSaveData[] operationMatches =
            state.successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind == sourceKind
                    && string.Equals(
                        value.sourceOperationId,
                        sourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (operationMatches.Length > 1)
            throw new InvalidOperationException(
                $"Observed life-event source '{sourceKind}:{sourceOperationId}' has duplicate receipts.");
        if (operationMatches.Length == 1)
        {
            if (string.Equals(
                    operationMatches[0].canonicalPayload,
                    canonicalPayload,
                    StringComparison.Ordinal))
                return new ObservedLifeEventCommitResult(false, null);
            throw new InvalidOperationException(
                $"Observed life-event source '{sourceKind}:{sourceOperationId}' conflicts with its committed receipt.");
        }

        LifeEventDefinitionSO definition = RequireObservedLifeEventDefinition(
            expectedDefinitionId,
            resolveAutomatically);
        V20DailyEventContext assignmentContext = new()
        {
            AbsoluteDay = absoluteDay,
            Generation = generation
        };
        // The irreversible source receipt, not daily event selection, owns
        // creation. Authored requirements were already validated by the
        // observed-definition contract; only authored character recurrence
        // may make this source ineligible.
        string participant = (sourceParticipants ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .Where(value => CanAssignLifeEvent(
                definition,
                value,
                assignmentContext,
                state)
                && !state.activeEvents.Any(active => active != null
                    && string.Equals(
                        active.definitionId,
                        definition.StableId,
                        StringComparison.Ordinal)
                    && active.participantCharacterIds?.Contains(
                        value,
                        StringComparer.Ordinal) == true))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (participant == null)
            return new ObservedLifeEventCommitResult(false, null);

        V20ActiveEventSaveData occurrence = CreateObservedLifeEvent(
            sourceKind,
            sourceOperationId,
            canonicalPayload,
            absoluteDay,
            generation,
            participant,
            definition,
            resolveAutomatically);
        ObservedLifeEventReceiptSaveData durableReceipt = new()
        {
            sourceKind = sourceKind,
            sourceOperationId = sourceOperationId,
            canonicalPayload = canonicalPayload,
            occurrenceInstanceId = occurrence.instanceId
        };
        state.successfulLifeEventOperations.Add(durableReceipt);
        if (!resolveAutomatically)
        {
            state.activeEvents.Add(occurrence);
            return new ObservedLifeEventCommitResult(true, null);
        }

        AddResolvedSociety(state, occurrence);
        MarkEventRecurrence(state, definition, occurrence, absoluteDay);
        ApplyInternalEffects(
            definition.automaticEffects,
            occurrence.contextFactionId);
        return new ObservedLifeEventCommitResult(
            true,
            CreateSocietyResolvedResult(
                occurrence,
                definition,
                definition.automaticEffects));
    }

    private LifeEventDefinitionSO RequireObservedLifeEventDefinition(
        string expectedDefinitionId,
        bool resolveAutomatically = true)
    {
        LifeEventDefinitionSO definition = catalog.LifeEvents.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.StableId,
                expectedDefinitionId,
                StringComparison.Ordinal));
        if (definition == null
            || definition.automatic != resolveAutomatically
            || RequireOccurrencePolicy(definition)
                != LifeEventOccurrencePolicy.ExternalObservedOnly
            || definition.frequencyRule != LifeEventFrequencyRule.OncePerCharacter
            || (resolveAutomatically
                ? definition.automaticEffects == null
                    || definition.automaticEffects.Count == 0
                : definition.choices == null
                    || definition.choices.Count < 2
                    || definition.choices.Count > 4
                    || definition.automaticEffects == null
                    || definition.automaticEffects.Count != 0)
            || !RequirementsSatisfied(
                definition.triggerRequirements,
                new RunMilestoneEvaluationSnapshot()))
        {
            throw new InvalidOperationException(
                $"Observed life-event definition '{expectedDefinitionId}' no longer matches its source receipt contract.");
        }
        return definition;
    }

    private V20ActiveEventSaveData CreateObservedLifeEvent(
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId,
        string canonicalPayload,
        int absoluteDay,
        int generation,
        string participantCharacterId,
        LifeEventDefinitionSO definition,
        bool resolveAutomatically = true)
    {
        string[] factionIds = catalog.Arcs.Select(value => value.factionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (factionIds.Length == 0)
            throw new InvalidOperationException(
                "Observed life-event creation requires at least one faction campaign.");
        (ExperienceEventRiskTier riskTier, string riskReason) =
            SocietyRisk(definition);
        SocietyEventRiskContract.RequireValid(
            riskTier,
            riskReason,
            definition.StableId);
        V20ActiveEventSaveData occurrence = new()
        {
            instanceId = ObservedLifeOccurrenceInstanceId(
                sourceKind,
                sourceOperationId),
            definitionId = definition.StableId,
            startedAbsoluteDay = absoluteDay,
            deadlineAbsoluteDay = resolveAutomatically
                ? absoluteDay
                : absoluteDay + Math.Max(1, definition.responseDeadlineDays),
            generation = generation,
            deterministicRoll = PersistentEntityId.GetStableHash32(canonicalPayload),
            contextFactionId = factionIds[0],
            participantCharacterIds = new List<string>
            {
                participantCharacterId
            },
            riskTier = riskTier,
            riskReason = riskReason,
            resolved = resolveAutomatically,
            resolutionId = resolveAutomatically ? "automatic" : string.Empty
        };
        if (resolveAutomatically)
        {
            FreezeSocietyTerminalOutcome(
                occurrence,
                definition,
                "자동 처리",
                RequireAuthoredNarrative(definition, null),
                definition.automaticEffects);
        }
        return occurrence;
    }

    private static string ObservedLifeOccurrenceInstanceId(
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId) =>
        $"observed-life:{(int)sourceKind}:"
        + Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceOperationId));

    private V20ActiveEventSaveData CreateEvent(
        string definitionId,
        V20DailyEventContext context,
        int durationDays,
        IEnumerable<string> participants,
        V20AuthoredContentSO societyDefinition = null)
    {
        string[] sorted = (participants ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        uint roll = EventRoll(context, definitionId, sorted);
        string[] factionIds = catalog.Arcs.Select(value => value.factionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (factionIds.Length == 0)
            throw new InvalidOperationException(
                "V20 event creation requires at least one faction campaign.");
        ExperienceEventRiskTier riskTier = ExperienceEventRiskTier.None;
        string riskReason = string.Empty;
        if (societyDefinition != null)
        {
            (riskTier, riskReason) = SocietyRisk(societyDefinition);
            SocietyEventRiskContract.RequireValid(
                riskTier,
                riskReason,
                societyDefinition.StableId);
        }
        return new V20ActiveEventSaveData
        {
            instanceId = $"event:{context.AbsoluteDay}:{definitionId}:{roll:X8}",
            definitionId = definitionId,
            startedAbsoluteDay = context.AbsoluteDay,
            deadlineAbsoluteDay = context.AbsoluteDay + Math.Max(0, durationDays),
            generation = Math.Max(0, context.Generation),
            deterministicRoll = roll,
            contextFactionId = factionIds[(int)(roll % (uint)factionIds.Length)],
            participantCharacterIds = sorted.ToList(),
            riskTier = riskTier,
            riskReason = riskReason
        };
    }

    private static bool TryBuildObservedMealEvidence(
        ObservedMealIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ServiceIncidentKind incidentKind,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        incidentKind = default;
        evidence = null;
        bool facilityValid = source.FacilityInstanceId.IsValid;
        if (!source.OperationId.IsValid
            || !source.TargetCharacterId.IsValid
            || !source.ItemDefinitionId.IsValid
            || !source.ItemStackId.IsValid
            || source.AbsoluteDay < 1
            || source.ObservedDead
            || !source.ObservedDowned
                && !source.ObservedContaminated
                && !source.ObservedPolicyViolation
            || source.FieldMeal == facilityValid)
        {
            return false;
        }

        incidentKind = source.ObservedDowned
            ? ServiceIncidentKind.MedicalCollapse
            : source.ObservedContaminated
                ? ServiceIncidentKind.Contamination
                : ServiceIncidentKind.ForbiddenMeal;
        string location = source.FieldMeal
            ? $"야전 식사 ({source.Location.X}, {source.Location.Y})"
            : $"시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})";
        string observedCause = incidentKind switch
        {
            ServiceIncidentKind.MedicalCollapse => "식사 직후 확인된 쓰러짐",
            ServiceIncidentKind.Contamination => "실제로 섭취한 식사의 오염 확인",
            _ => "실제 식사 정책 위반 확인"
        };
        observedCause += $" · 대상 {source.TargetCharacterId.Value}"
            + $" · 식사 {source.ItemDefinitionId.Value}"
            + $" · 스택 {source.ItemStackId.Value}"
            + $" · 장소 {location}";
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Meal,
            operationId = source.OperationId.Value,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = incidentKind,
            targetCharacterId = source.TargetCharacterId.Value,
            itemDefinitionId = source.ItemDefinitionId.Value,
            itemStackId = source.ItemStackId.Value,
            facilityInstanceId = source.FieldMeal
                ? string.Empty
                : source.FacilityInstanceId.Value,
            fieldMeal = source.FieldMeal,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = source.ObservedPolicyViolation,
            observedDowned = source.ObservedDowned,
            observedDead = source.ObservedDead,
            observedCause = observedCause
        };
        return true;
    }

    private static bool TryBuildObservedShopliftingEvidence(
        ObservedShopliftingIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        bool itemInstanceValid = string.IsNullOrEmpty(source.ItemInstanceId)
            || IsCanonicalRequiredId(source.ItemInstanceId);
        bool sourceStackValid = string.IsNullOrEmpty(source.SourceStackId)
            || IsCanonicalRequiredId(source.SourceStackId);
        bool componentFingerprintValid = string.IsNullOrEmpty(
                source.ComponentFingerprint)
            || string.Equals(
                source.ComponentFingerprint,
                source.ComponentFingerprint.Trim(),
                StringComparison.Ordinal);
        if (!IsCanonicalRequiredId(source.OperationId)
            || !IsCanonicalRequiredId(source.SourceOperationId)
            || !source.TargetCharacterId.IsValid
            || !source.ItemDefinitionId.IsValid
            || source.SaleItemId < 0
            || !itemInstanceValid
            || !sourceStackValid
            || source.Quantity != 1
            || source.UnitMassGrams <= 0L
            || !componentFingerprintValid
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1
            || source.LossValue < 0)
        {
            return false;
        }

        string lotIdentity = !string.IsNullOrEmpty(source.ItemInstanceId)
            ? source.ItemInstanceId
            : !string.IsNullOrEmpty(source.SourceStackId)
                ? source.SourceStackId
                : source.SourceOperationId;
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Shoplifting,
            operationId = source.OperationId,
            sourceOperationId = source.SourceOperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.Theft,
            targetCharacterId = source.TargetCharacterId.Value,
            itemDefinitionId = source.ItemDefinitionId.Value,
            itemStackId = string.Empty,
            saleItemId = source.SaleItemId,
            itemInstanceId = source.ItemInstanceId,
            sourceStackId = source.SourceStackId,
            quantity = source.Quantity,
            unitMassGrams = source.UnitMassGrams,
            componentFingerprint = source.ComponentFingerprint,
            lossValue = source.LossValue,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = false,
            observedDowned = false,
            observedDead = false,
            observedCause = "던전 상점 물품의 실제 절도 커밋"
                + $" · 대상 {source.TargetCharacterId.Value}"
                + $" · 품목 {source.ItemDefinitionId.Value}"
                + $" · lot {lotIdentity}"
                + $" · 수량 {source.Quantity}"
                + $" · 질량 {source.UnitMassGrams}g"
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    private static bool TryBuildObservedBrawlEvidence(
        ObservedBrawlIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        if (!IsCanonicalRequiredId(source.AttackOperationId)
            || !source.TargetCharacterId.IsValid
            || !source.OtherParticipantCharacterId.IsValid
            || source.TargetCharacterId.Equals(
                source.OtherParticipantCharacterId)
            || !float.IsFinite(source.ActualDamage)
            || source.ActualDamage <= 0f
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1)
        {
            return false;
        }

        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.Combat,
            operationId = source.AttackOperationId,
            sourceOperationId = source.AttackOperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.Brawl,
            targetCharacterId = source.TargetCharacterId.Value,
            otherParticipantCharacterId =
                source.OtherParticipantCharacterId.Value,
            customerWasAttacker = source.CustomerWasAttacker,
            actualDamage = source.ActualDamage,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedPolicyViolation = false,
            observedDowned = false,
            observedDead = false,
            observedCause = "방문 서비스 시설에서 확인된 실제 전투 피해"
                + " · 공격자 "
                + (source.CustomerWasAttacker
                    ? source.TargetCharacterId.Value
                    : source.OtherParticipantCharacterId.Value)
                + " · 피해자 "
                + (source.CustomerWasAttacker
                    ? source.OtherParticipantCharacterId.Value
                    : source.TargetCharacterId.Value)
                + " · 피해 "
                + source.ActualDamage.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    private static bool TryBuildObservedCulturalConflictEvidence(
        ObservedCulturalConflictIncidentSnapshot source,
        string definitionId,
        string occurrenceInstanceId,
        out ObservedMealIncidentEvidenceSaveData evidence)
    {
        evidence = null;
        bool customerIsParticipant = source.CustomerCharacterId.Equals(
                source.InstigatorCharacterId)
            || source.CustomerCharacterId.Equals(source.TargetCharacterId);
        if (!IsCanonicalRequiredId(source.OperationId)
            || !source.InstigatorCharacterId.IsValid
            || !source.TargetCharacterId.IsValid
            || source.InstigatorCharacterId.Equals(source.TargetCharacterId)
            || !source.CustomerCharacterId.IsValid
            || !customerIsParticipant
            || !source.InstigatorCultureId.IsValid
            || !source.TargetCultureId.IsValid
            || source.InstigatorCultureId.Equals(source.TargetCultureId)
            || !source.FacilityInstanceId.IsValid
            || source.AbsoluteDay < 1)
        {
            return false;
        }

        CharacterId otherParticipant = source.CustomerCharacterId.Equals(
                source.InstigatorCharacterId)
            ? source.TargetCharacterId
            : source.InstigatorCharacterId;
        evidence = new ObservedMealIncidentEvidenceSaveData
        {
            sourceKind = ObservedIncidentSourceKind.SocialConflict,
            operationId = source.OperationId,
            sourceOperationId = source.OperationId,
            occurrenceInstanceId = occurrenceInstanceId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            incidentKind = ServiceIncidentKind.CulturalInsult,
            targetCharacterId = source.CustomerCharacterId.Value,
            otherParticipantCharacterId = otherParticipant.Value,
            instigatorCharacterId = source.InstigatorCharacterId.Value,
            instigatorCultureId = source.InstigatorCultureId.Value,
            targetCultureId = source.TargetCultureId.Value,
            facilityInstanceId = source.FacilityInstanceId.Value,
            fieldMeal = false,
            locationX = source.Location.X,
            locationY = source.Location.Y,
            absoluteDay = source.AbsoluteDay,
            observedCause = "실제 사회 갈등 효과 커밋"
                + $" · 행위자 {source.InstigatorCharacterId.Value}"
                + $" ({source.InstigatorCultureId.Value})"
                + $" · 상대 {source.TargetCharacterId.Value}"
                + $" ({source.TargetCultureId.Value})"
                + $" · 장소 시설 {source.FacilityInstanceId.Value} "
                + $"({source.Location.X}, {source.Location.Y})"
        };
        return true;
    }

    private static bool RequiresObservedIncidentOperation(
        ServiceIncidentKind incidentKind,
        string choiceId) =>
        incidentKind == ServiceIncidentKind.ForbiddenMeal
            && string.Equals(choiceId, "replace", StringComparison.Ordinal)
        || incidentKind == ServiceIncidentKind.MedicalCollapse
            && (string.Equals(
                    choiceId,
                    "emergency-care",
                    StringComparison.Ordinal)
                || string.Equals(
                    choiceId,
                    "transfer",
                    StringComparison.Ordinal));

    private static bool SameObservedMealPayload(
        ObservedMealIncidentEvidenceSaveData left,
        ObservedMealIncidentEvidenceSaveData right) =>
        left != null
        && right != null
        && string.Equals(left.operationId, right.operationId, StringComparison.Ordinal)
        && string.Equals(left.sourceOperationId, right.sourceOperationId, StringComparison.Ordinal)
        && left.incidentKind == right.incidentKind
        && string.Equals(left.targetCharacterId, right.targetCharacterId, StringComparison.Ordinal)
        && string.Equals(left.itemDefinitionId, right.itemDefinitionId, StringComparison.Ordinal)
        && string.Equals(left.itemStackId, right.itemStackId, StringComparison.Ordinal)
        && left.sourceKind == right.sourceKind
        && left.saleItemId == right.saleItemId
        && string.Equals(left.itemInstanceId, right.itemInstanceId, StringComparison.Ordinal)
        && string.Equals(left.sourceStackId, right.sourceStackId, StringComparison.Ordinal)
        && left.quantity == right.quantity
        && left.unitMassGrams == right.unitMassGrams
        && string.Equals(left.componentFingerprint, right.componentFingerprint, StringComparison.Ordinal)
        && left.lossValue == right.lossValue
        && string.Equals(
            left.otherParticipantCharacterId,
            right.otherParticipantCharacterId,
            StringComparison.Ordinal)
        && string.Equals(
            left.instigatorCharacterId,
            right.instigatorCharacterId,
            StringComparison.Ordinal)
        && string.Equals(
            left.instigatorCultureId,
            right.instigatorCultureId,
            StringComparison.Ordinal)
        && string.Equals(
            left.targetCultureId,
            right.targetCultureId,
            StringComparison.Ordinal)
        && left.customerWasAttacker == right.customerWasAttacker
        && left.actualDamage.Equals(right.actualDamage)
        && string.Equals(left.facilityInstanceId, right.facilityInstanceId, StringComparison.Ordinal)
        && left.fieldMeal == right.fieldMeal
        && left.locationX == right.locationX
        && left.locationY == right.locationY
        && left.absoluteDay == right.absoluteDay
        && left.observedPolicyViolation == right.observedPolicyViolation
        && left.observedDowned == right.observedDowned
        && left.observedDead == right.observedDead;

    private static bool SameObservedMealEvidence(
        ObservedMealIncidentEvidenceSaveData left,
        ObservedMealIncidentEvidenceSaveData right) =>
        SameObservedMealPayload(left, right)
        && string.Equals(
            left.occurrenceInstanceId,
            right.occurrenceInstanceId,
            StringComparison.Ordinal)
        && string.Equals(
            left.definitionId,
            right.definitionId,
            StringComparison.Ordinal)
        && string.Equals(
            left.observedCause,
            right.observedCause,
            StringComparison.Ordinal);

    private static (ExperienceEventRiskTier Tier, string Reason) SocietyRisk(
        V20AuthoredContentSO definition) => definition switch
        {
            GuestRequestDefinitionSO value =>
                (value.riskTier, value.riskReason),
            LifeEventDefinitionSO value =>
                (value.riskTier, value.riskReason),
            ServiceIncidentDefinitionSO value =>
                (value.riskTier, value.riskReason),
            _ => throw new InvalidOperationException(
                $"'{definition?.StableId}' is not a society-event definition.")
        };

    private static void FreezeSocietyTerminalOutcome(
        V20ActiveEventSaveData occurrence,
        V20AuthoredContentSO definition,
        string actionLabel,
        string authoredNarrative,
        IEnumerable<V20ContentEffect> effects)
    {
        if (occurrence == null
            || definition == null
            || string.IsNullOrWhiteSpace(occurrence.resolutionId)
            || string.IsNullOrWhiteSpace(actionLabel)
            || !string.Equals(
                actionLabel,
                actionLabel.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(authoredNarrative)
            || !string.Equals(
                authoredNarrative,
                authoredNarrative.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Society event '{occurrence?.instanceId}' has no canonical terminal narrative.");
        }
        SocietyEventRiskContract.RequireValid(
            occurrence.riskTier,
            occurrence.riskReason,
            occurrence.instanceId);
        occurrence.terminalActionLabel = actionLabel;
        occurrence.terminalOutcomeNarrative = authoredNarrative;
        occurrence.terminalEffectKinds = (effects
                ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .Select(value => value.kind)
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        if (occurrence.terminalEffectKinds.Any(value =>
                value == V20ContentEffectKind.None
                || !Enum.IsDefined(typeof(V20ContentEffectKind), value)))
        {
            throw new InvalidOperationException(
                $"Society event '{occurrence.instanceId}' has an invalid terminal effect type.");
        }
    }

    private static string RequireAuthoredNarrative(
        V20AuthoredContentSO definition,
        V20ChoiceDefinition choice)
    {
        string narrative = choice == null
            ? definition?.Description ?? string.Empty
            : choice.outcomeText?.Trim() ?? string.Empty;
        if (narrative.Length == 0)
        {
            throw new InvalidOperationException(
                $"Society event '{definition?.StableId}' has no authored terminal narrative.");
        }
        return narrative;
    }

    private static string GuestResolutionLabel(string resolutionId) =>
        resolutionId switch
        {
            "fulfill" => "요청 이행",
            "decline" => "요청 거절",
            "expired" => "기한 만료",
            _ => throw new InvalidOperationException(
                $"Unknown guest-request terminal resolution '{resolutionId}'.")
        };

    private static V20ResolvedEventResult CreateSocietyResolvedResult(
        V20ActiveEventSaveData occurrence,
        V20AuthoredContentSO definition,
        IReadOnlyList<V20ContentEffect> effects) =>
        new(
            definition.StableId,
            occurrence.resolutionId,
            effects,
            occurrence.participantCharacterIds.AsReadOnly(),
            occurrence.contextFactionId,
            occurrence.instanceId,
            definition.DisplayName,
            occurrence.riskTier,
            occurrence.riskReason,
            occurrence.terminalActionLabel,
            occurrence.terminalOutcomeNarrative,
            occurrence.terminalEffectKinds.AsReadOnly(),
            occurrence.hasObservedMealIncident
                ? occurrence.observedMealIncident?.observedCause
                    ?? string.Empty
                : string.Empty);

    private static uint EventRoll(
        V20DailyEventContext context,
        string definitionId,
        IEnumerable<string> participants = null) =>
        PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{definitionId}:{context.AbsoluteDay}:"
            + string.Join(",", (participants ?? context.ParticipantCharacterIds)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)));

    private static T SelectDeterministic<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        V20DailyEventContext context,
        string salt) => values
        .OrderBy(value => PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{salt}:{id(value)}:{context.AbsoluteDay}"))
        .ThenBy(id, StringComparer.Ordinal)
        .First();

    private static string Pick<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        string key)
    {
        if (values == null || values.Count == 0)
            throw new InvalidOperationException("Endless crisis content pool is empty.");
        int index = (int)(PersistentEntityId.GetStableHash32(key)
            % (uint)values.Count);
        return id(values[index]);
    }

    private static V20ContentRequirementSet TriggerRequirements(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.triggerRequirements,
            GuestRequestDefinitionSO value => value.serviceRequirements,
            ServiceIncidentDefinitionSO value => value.triggerRequirements,
            _ => new V20ContentRequirementSet()
        };

    private static IReadOnlyList<V20ChoiceDefinition> GetChoices(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.choices,
            ServiceIncidentDefinitionSO value => value.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };

    private static IReadOnlyList<V20ContentEffect> ExpirationEffects(
        V20AuthoredContentSO definition) => definition switch
        {
            GuestRequestDefinitionSO value => value.failureEffects,
            _ => Array.Empty<V20ContentEffect>()
        };

    private static int DeadlineDays(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value => value.responseDeadlineDays,
            GuestRequestDefinitionSO value => value.deadlineDays,
            _ => 3
        };

    private static bool IsEmergency(V20AuthoredContentSO definition) =>
        definition is LifeEventDefinitionSO { emergency: true }
        || definition is ServiceIncidentDefinitionSO;

    private static void AddResolvedSociety(
        SocietyEventWorldSaveData state,
        V20ActiveEventSaveData value)
    {
        state.recentResolvedEvents.Add(value);
        HashSet<string> receiptOwnedOccurrences =
            (state.successfulLifeEventOperations
                ?? new List<ObservedLifeEventReceiptSaveData>())
            .Where(receipt => receipt != null)
            .Select(receipt => receipt.occurrenceInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        int excess = state.recentResolvedEvents.Count(occurrence =>
            occurrence != null
            && !receiptOwnedOccurrences.Contains(occurrence.instanceId)) - 256;
        while (excess-- > 0)
        {
            int removableIndex = state.recentResolvedEvents.FindIndex(occurrence =>
                occurrence != null
                && !receiptOwnedOccurrences.Contains(occurrence.instanceId));
            if (removableIndex < 0) break;
            state.recentResolvedEvents.RemoveAt(removableIndex);
        }
    }

    private static void MarkEventRecurrence(
        SocietyEventWorldSaveData state,
        V20AuthoredContentSO definition,
        V20ActiveEventSaveData resolvedEvent,
        int absoluteDay)
    {
        int cooldown = definition is LifeEventDefinitionSO lifeDefinition
            ? lifeDefinition.cooldownDays
            : 30;
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            definition.StableId,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = definition.StableId,
            availableAbsoluteDay = absoluteDay + Math.Max(3, cooldown)
        });
        string categoryKey = CategoryKey(definition);
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            categoryKey,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = categoryKey,
            availableAbsoluteDay = absoluteDay + 3
        });
        if (definition is not LifeEventDefinitionSO lifeEvent) return;
        switch (lifeEvent.frequencyRule)
        {
            case LifeEventFrequencyRule.OncePerRun:
                if (!state.completedOnceEventIds.Contains(
                        lifeEvent.StableId,
                        StringComparer.Ordinal))
                    state.completedOnceEventIds.Add(lifeEvent.StableId);
                break;
            case LifeEventFrequencyRule.OncePerGeneration:
                AddUnique(
                    state.recurrenceKeys,
                    GenerationRecurrenceKey(
                        lifeEvent.StableId,
                        resolvedEvent?.generation ?? 0));
                break;
            case LifeEventFrequencyRule.OncePerCharacter:
                foreach (string participant in
                         resolvedEvent?.participantCharacterIds
                         ?? new List<string>())
                    AddUnique(
                        state.recurrenceKeys,
                        CharacterRecurrenceKey(lifeEvent.StableId, participant));
                break;
        }
    }

    private static void AddUnique(ICollection<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.Ordinal)) target.Add(value);
    }

    private static string CategoryKey(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value =>
                $"event-category:life:{value.category}",
            GuestRequestDefinitionSO value =>
                $"event-category:guest:{value.kind}",
            ServiceIncidentDefinitionSO value =>
                $"event-category:incident:{value.kind}",
            _ => $"event-category:definition:{definition?.StableId}"
        };

    private static string GenerationRecurrenceKey(
        string definitionId,
        int generation) =>
        $"{definitionId}:generation:{Math.Max(0, generation)}";

    private static string CharacterRecurrenceKey(
        string definitionId,
        string characterId) =>
        $"{definitionId}:character:{Normalize(characterId)}";

    public static string FacilityRequirementKey(V20FacilityRequirement value) =>
        !string.IsNullOrWhiteSpace(value.buildingDefinitionId)
            ? value.buildingDefinitionId
            : $"capability:{value.capabilityId}";

    private SeasonalEventWorldSaveData ValidateSeasonal(SeasonalEventWorldSaveData data)
    {
        if (data == null || data.version != SeasonalEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.completedEventIds == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Seasonal-event save payload is invalid.");
        RequireValidEvents(data.activeEvents, seasonal: true);
        foreach (string id in data.completedEventIds) ((IWorldEventCatalog)catalog).Require(id);
        RequireUniqueIds(data.completedEventIds, "seasonal completion");
        return data;
    }
    private SocietyEventWorldSaveData ValidateSociety(SocietyEventWorldSaveData data)
    {
        if (data == null || data.version != SocietyEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.recentResolvedEvents == null || data.completedOnceEventIds == null || data.recurrenceKeys == null || data.cooldowns == null || data.workDelays == null || data.successfulIncidentOperations == null || data.successfulLifeEventOperations == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Society-event save payload is invalid.");
        if (data.workDelays.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.scopeId)
                || value.untilAbsoluteDayExclusive < 0)
            || data.workDelays.GroupBy(value => value.scopeId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            throw new InvalidOperationException("Society-event work-delay state is invalid.");
        RequireValidEvents(data.activeEvents.Concat(data.recentResolvedEvents), seasonal: false);
        HashSet<string> incidentOperations = new(StringComparer.Ordinal);
        HashSet<string> incidentOccurrences = new(StringComparer.Ordinal);
        foreach (ObservedMealIncidentEvidenceSaveData evidence in
                 data.successfulIncidentOperations)
        {
            RequireValidObservedMealEvidence(evidence);
            if (!incidentOperations.Add(evidence.operationId)
                || !incidentOccurrences.Add(evidence.occurrenceInstanceId))
            {
                throw new InvalidOperationException(
                    "Observed meal-incident history contains duplicate identities.");
            }
        }
        foreach (V20ActiveEventSaveData occurrence in data.activeEvents
                     .Concat(data.recentResolvedEvents)
                     .Where(value => value?.hasObservedMealIncident == true))
        {
            ObservedMealIncidentEvidenceSaveData recorded =
                data.successfulIncidentOperations.SingleOrDefault(value =>
                    string.Equals(
                        value.operationId,
                        occurrence.observedMealIncident.operationId,
                        StringComparison.Ordinal));
            if (!SameObservedMealEvidence(
                    occurrence.observedMealIncident,
                    recorded))
            {
                throw new InvalidOperationException(
                    $"Observed meal incident '{occurrence.instanceId}' does not match its immutable operation history.");
            }
        }
        RequireValidObservedLifeEventReceipts(data);
        foreach (V20ActiveEventSaveData value in data.activeEvents)
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            GuestRequestDeliveryOutbox.RequireValidState(
                value,
                definition as GuestRequestDefinitionSO,
                activeCollection: true);
        }
        foreach (V20ActiveEventSaveData value in data.recentResolvedEvents)
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            GuestRequestDeliveryOutbox.RequireValidState(
                value,
                definition as GuestRequestDefinitionSO,
                activeCollection: false);
        }
        HashSet<string> receiptOwnedOccurrenceIds =
            data.successfulLifeEventOperations
                .Where(value => value != null)
                .Select(value => value.occurrenceInstanceId)
                .ToHashSet(StringComparer.Ordinal);
        V20ActiveEventSaveData[] unownedActive = data.activeEvents
            .Where(value => value != null
                && !receiptOwnedOccurrenceIds.Contains(value.instanceId))
            .ToArray();
        int activeEmergency = unownedActive.Count(value =>
            IsEmergency(((ISocietyEventCatalog)catalog).Require(
                value.definitionId)));
        int activeOrdinary = unownedActive.Length - activeEmergency;
        int ordinaryCap = data.lastEvaluationAbsoluteDay is >= 0 and <= 30
            ? 1
            : 2;
        int receiptOwnedResolvedCount = data.recentResolvedEvents.Count(value =>
            value != null
            && receiptOwnedOccurrenceIds.Contains(value.instanceId));
        if (activeEmergency > 1
            || activeOrdinary > ordinaryCap
            || data.recentResolvedEvents.Count - receiptOwnedResolvedCount > 256)
            throw new InvalidOperationException(
                "Society-event history bounds are invalid: "
                + $"ordinary={activeOrdinary}/{ordinaryCap}, "
                + $"emergency={activeEmergency}/1, "
                + $"unowned-resolved={data.recentResolvedEvents.Count - receiptOwnedResolvedCount}/256.");
        foreach (string id in data.completedOnceEventIds) ((ISocietyEventCatalog)catalog).Require(id);
        foreach (string key in data.recurrenceKeys)
            RequireValidRecurrenceKey(key);
        HashSet<string> categoryKeys = catalog.LifeEvents
            .Cast<V20AuthoredContentSO>()
            .Concat(catalog.GuestRequests)
            .Concat(catalog.ServiceIncidents)
            .Select(CategoryKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (V20EventCooldownSaveData cooldown in data.cooldowns)
        {
            if (cooldown == null || cooldown.availableAbsoluteDay < 0)
                throw new InvalidOperationException("Society-event cooldown is invalid.");
            if (!categoryKeys.Contains(cooldown.definitionId))
                ((ISocietyEventCatalog)catalog).Require(cooldown.definitionId);
        }
        RequireUniqueIds(data.completedOnceEventIds, "society once-only completion");
        RequireUniqueIds(data.recurrenceKeys, "society scoped recurrence");
        RequireUniqueIds(data.cooldowns.Select(value => value.definitionId), "society cooldown");
        return data;
    }

    private void RequireValidObservedLifeEventReceipts(
        SocietyEventWorldSaveData data)
    {
        HashSet<string> sourceOperations = new(StringComparer.Ordinal);
        HashSet<string> occurrences = new(StringComparer.Ordinal);
        foreach (ObservedLifeEventReceiptSaveData receipt in
                 data.successfulLifeEventOperations)
        {
            if (receipt == null
                || receipt.version != ObservedLifeEventReceiptSaveData.CurrentVersion
                || !Enum.IsDefined(
                    typeof(ObservedLifeEventSourceKind),
                    receipt.sourceKind)
                || string.IsNullOrWhiteSpace(receipt.sourceOperationId)
                || !string.Equals(
                    receipt.sourceOperationId,
                    receipt.sourceOperationId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(receipt.canonicalPayload)
                || !string.Equals(
                    receipt.occurrenceInstanceId,
                    ObservedLifeOccurrenceInstanceId(
                        receipt.sourceKind,
                        receipt.sourceOperationId),
                    StringComparison.Ordinal)
                || !sourceOperations.Add(
                    $"{(int)receipt.sourceKind}:{receipt.sourceOperationId}")
                || !occurrences.Add(receipt.occurrenceInstanceId))
            {
                throw new InvalidOperationException(
                    "Observed life-event receipt identity is invalid or duplicated.");
            }

            string definitionId;
            int absoluteDay;
            int generation;
            bool resolvedAutomatically;
            string[] eligibleParticipants;
            switch (receipt.sourceKind)
            {
                case ObservedLifeEventSourceKind.Funeral:
                    if (!ObservedFuneralLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedFuneralLifeEventReceipt funeral)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            funeral.SourceOperationId,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Observed funeral life-event receipt payload is invalid.");
                    definitionId = GraveVisitLifeEventId;
                    absoluteDay = funeral.AbsoluteDay;
                    generation = funeral.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = funeral.ParticipantCharacterIds
                        .Select(value => value.Value)
                        .ToArray();
                    break;
                case ObservedLifeEventSourceKind.LineageCompression:
                    if (!ObservedLineageCompressionLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedLineageCompressionLifeEventReceipt lineage)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            lineage.SourceOperationId,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Observed lineage-compression life-event receipt payload is invalid.");
                    definitionId = StoryCompressedLifeEventId;
                    absoluteDay = lineage.ArchiveAbsoluteDay;
                    generation = lineage.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = new[] { lineage.CharacterId.Value };
                    break;
                case ObservedLifeEventSourceKind.ProductionDeclaredLoss:
                    if (!ObservedProductionLossLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedProductionLossLifeEventReceipt loss)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            loss.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed production-loss life-event receipt payload is invalid.");
                    }
                    definitionId = ApprenticeMistakeLifeEventId;
                    absoluteDay = loss.AbsoluteDay;
                    generation = loss.Generation;
                    resolvedAutomatically = false;
                    eligibleParticipants = new[]
                    {
                        loss.StudentCharacterId.Value
                    };
                    break;
                case ObservedLifeEventSourceKind.LastMentorshipLesson:
                    if (!ObservedLastLessonLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedLastLessonLifeEventReceipt lesson)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            lesson.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed last-lesson life-event receipt payload is invalid.");
                    }
                    definitionId = LastLessonLifeEventId;
                    absoluteDay = lesson.AbsoluteDay;
                    generation = lesson.Generation;
                    resolvedAutomatically = false;
                    eligibleParticipants = new[]
                    {
                        lesson.MentorCharacterId.Value
                    };
                    break;
                case ObservedLifeEventSourceKind.ProficiencyPromotion:
                    if (!ObservedQuietPromotionLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedQuietPromotionLifeEventReceipt promotion)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            promotion.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed quiet-promotion life-event receipt payload is invalid.");
                    }
                    definitionId = QuietPromotionLifeEventId;
                    absoluteDay = promotion.AbsoluteDay;
                    generation = promotion.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = new[]
                    {
                        promotion.CharacterId.Value
                    };
                    break;
                default:
                    throw new InvalidOperationException(
                        "Observed life-event receipt source kind is unsupported.");
            }

            LifeEventDefinitionSO definition =
                RequireObservedLifeEventDefinition(
                    definitionId,
                    resolvedAutomatically);
            V20ActiveEventSaveData[] joined = data.activeEvents
                .Concat(data.recentResolvedEvents)
                .Where(value => value != null
                    && string.Equals(
                        value.instanceId,
                        receipt.occurrenceInstanceId,
                        StringComparison.Ordinal))
                .ToArray();
            if (joined.Length != 1)
                throw new InvalidOperationException(
                    $"Observed life-event receipt '{receipt.sourceOperationId}' has no exact resolved occurrence.");
            V20ActiveEventSaveData occurrence = joined[0];
            bool recurrenceCommitted = data.recurrenceKeys.Contains(
                CharacterRecurrenceKey(
                    definitionId,
                    occurrence.participantCharacterIds?.SingleOrDefault()),
                StringComparer.Ordinal);
            bool expectedResolutionState = resolvedAutomatically
                ? occurrence.resolved
                    && string.Equals(
                        occurrence.resolutionId,
                        "automatic",
                        StringComparison.Ordinal)
                    && data.recentResolvedEvents.Contains(occurrence)
                : occurrence.resolved
                    ? !string.IsNullOrWhiteSpace(occurrence.resolutionId)
                        && data.recentResolvedEvents.Contains(occurrence)
                    : string.IsNullOrEmpty(occurrence.resolutionId)
                        && data.activeEvents.Contains(occurrence);
            if (!expectedResolutionState
                || !string.Equals(
                    occurrence.definitionId,
                    definitionId,
                    StringComparison.Ordinal)
                || occurrence.startedAbsoluteDay != absoluteDay
                || occurrence.deadlineAbsoluteDay != (resolvedAutomatically
                    ? absoluteDay
                    : absoluteDay + Math.Max(
                        1,
                        definition.responseDeadlineDays))
                || occurrence.generation != generation
                || occurrence.deterministicRoll
                    != PersistentEntityId.GetStableHash32(
                        receipt.canonicalPayload)
                || occurrence.participantCharacterIds.Count != 1
                || !eligibleParticipants.Contains(
                    occurrence.participantCharacterIds[0],
                    StringComparer.Ordinal)
                || recurrenceCommitted != occurrence.resolved)
            {
                throw new InvalidOperationException(
                    $"Observed life-event receipt '{receipt.sourceOperationId}' conflicts with its resolved occurrence.");
            }
        }

        HashSet<string> owned = data.successfulLifeEventOperations
            .Select(value => value.occurrenceInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (data.activeEvents.Any(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId)
                && !owned.Contains(value.instanceId))
            || data.recentResolvedEvents.Any(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId)
                && !owned.Contains(value.instanceId)))
        {
            throw new InvalidOperationException(
                "Observed life-event occurrence has no exact durable source receipt.");
        }
        bool duplicatePendingCharacterRecurrence = data.activeEvents
            .Where(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId))
            .SelectMany(value => value.participantCharacterIds.Select(
                participant => (value.definitionId, participant)))
            .GroupBy(value => value)
            .Any(group => group.Count() > 1);
        if (duplicatePendingCharacterRecurrence)
        {
            throw new InvalidOperationException(
                "Observed life-event state contains duplicate pending character recurrence.");
        }
    }

    private static bool IsObservedLifeEventDefinition(string definitionId) =>
        string.Equals(
            definitionId,
            GraveVisitLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            StoryCompressedLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            ApprenticeMistakeLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            LastLessonLifeEventId,
            StringComparison.Ordinal)
        || string.Equals(
            definitionId,
            QuietPromotionLifeEventId,
            StringComparison.Ordinal);

    private void RequireValidRecurrenceKey(string key)
    {
        string normalized = Normalize(key);
        int marker = normalized.IndexOf(":generation:", StringComparison.Ordinal);
        if (marker > 0)
        {
            ((ISocietyEventCatalog)catalog).Require(normalized.Substring(0, marker));
            if (!int.TryParse(
                    normalized.Substring(marker + ":generation:".Length),
                    out int generation)
                || generation < 0)
                throw new InvalidOperationException(
                    $"Society generation recurrence key '{key}' is invalid.");
            return;
        }
        marker = normalized.IndexOf(":character:", StringComparison.Ordinal);
        if (marker > 0)
        {
            ((ISocietyEventCatalog)catalog).Require(normalized.Substring(0, marker));
            if (!new CharacterId(normalized.Substring(
                    marker + ":character:".Length)).IsValid)
                throw new InvalidOperationException(
                    $"Society character recurrence key '{key}' is invalid.");
            return;
        }
        throw new InvalidOperationException(
            $"Society recurrence key '{key}' is invalid.");
    }
    private FactionCampaignWorldSaveData ValidateFactions(
        FactionCampaignWorldSaveData data,
        SeasonalEventWorldSaveData seasonalData)
    {
        if (data == null
            || data.version != FactionCampaignWorldSaveData.CurrentVersion
            || data.factions == null
            || seasonalData != null && seasonalData.activeEvents == null)
            throw new InvalidOperationException("Faction-campaign save payload is invalid.");
        if (data.factions.Count != catalog.Arcs.Count)
            throw new InvalidOperationException("Faction-campaign save must contain all six authored factions.");
        foreach (FactionCampaignStateSaveData value in data.factions)
        {
            FactionArcDefinitionSO arc = catalog.Arcs.FirstOrDefault(candidate =>
                value != null && string.Equals(
                    candidate.factionId,
                    value.factionId,
                    StringComparison.Ordinal));
            if (value != null)
            {
                value.activeContractOccurrenceId ??= string.Empty;
                value.lastSeasonalContractId ??= string.Empty;
                value.lastSeasonalOccurrenceId ??= string.Empty;
            }
            if (value == null || arc == null || value.rapport < -100 || value.rapport > 100 || value.grievance < 0 || value.grievance > 100 || value.obligationTokens < 0 || value.obligationTokens > 5 || value.currentChapter < 1 || value.currentChapter > 7
                || value.completedContractIds == null
                || value.failedContractIds == null
                || value.majorChoiceFlags == null
                || value.deliverySourceStackIds == null
                || value.activeContractDestinationOwnerId == null
                || value.activeContractDestinationId == null
                || value.activeContractInputCapacityFingerprint == null
                || value.deliveryContractId == null
                || value.deliveryOperationId == null
                || value.deliveryCommitId == null
                || value.deliveryFailureReason == null)
                throw new InvalidOperationException("Faction-campaign state is invalid.");
            bool hasLastSeasonal = !string.IsNullOrEmpty(
                value.lastSeasonalOccurrenceId);
            if (hasLastSeasonal != !string.IsNullOrEmpty(
                    value.lastSeasonalContractId)
                || hasLastSeasonal
                    && !catalog.Contracts.Any(contract => contract != null
                        && !string.IsNullOrEmpty(contract.seasonalEventId)
                        && string.Equals(
                            contract.StableId,
                            value.lastSeasonalContractId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            contract.factionId,
                            value.factionId,
                            StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Faction seasonal contract history is invalid.");
            foreach (string id in (value.completedContractIds ?? new()).Concat(value.failedContractIds ?? new())) ((IFactionStoryCatalog)catalog).Require(id);
            if (!string.IsNullOrWhiteSpace(value.activeContractId))
            {
                FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(candidate =>
                    string.Equals(candidate.StableId, value.activeContractId, StringComparison.Ordinal)
                    && string.Equals(candidate.factionId, value.factionId, StringComparison.Ordinal));
                bool seasonal = contract != null
                    && !string.IsNullOrEmpty(contract.seasonalEventId);
                if (contract == null
                    || value.activeContractDeadlineAbsoluteDay < 0
                    || seasonal != !string.IsNullOrEmpty(
                        value.activeContractOccurrenceId)
                    || seasonal
                        && (!string.Equals(
                                value.lastSeasonalContractId,
                                contract.StableId,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                value.lastSeasonalOccurrenceId,
                                value.activeContractOccurrenceId,
                                StringComparison.Ordinal)
                            || seasonalData != null
                                && !seasonalData.activeEvents.Any(occurrence =>
                                occurrence != null
                                && !occurrence.resolved
                                && string.Equals(
                                    occurrence.instanceId,
                                    value.activeContractOccurrenceId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    occurrence.definitionId,
                                    contract.seasonalEventId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    occurrence.contextFactionId,
                                    value.factionId,
                                    StringComparison.Ordinal)
                                && occurrence.deadlineAbsoluteDay ==
                                    value.activeContractDeadlineAbsoluteDay)))
                    throw new InvalidOperationException("Faction active contract is invalid.");
                bool material = FactionContractMaterialRules
                    .HasMaterialRequirements(contract);
                if (material
                    && (!value.activeContractInputOwnerActive
                        || value.deliveryTerminalCleanupPending
                        || !string.Equals(
                            value.activeContractDestinationOwnerId,
                            FactionContractDeliveryOutbox.FormatOwnerId(
                                contract.StableId,
                                value.activeContractOccurrenceId),
                            StringComparison.Ordinal)
                        || !string.Equals(
                            value.activeContractDestinationId,
                            FactionContractDeliveryOutbox.FormatDestinationId(
                                contract.StableId,
                                value.activeContractOccurrenceId),
                            StringComparison.Ordinal)
                        || value.activeContractInputCapacityGrams <= 0L
                        || value.activeContractInputMassAuthorityRevision <= 0L
                        || string.IsNullOrWhiteSpace(
                            value.activeContractInputCapacityFingerprint)
                        || value.deliveryCommitPhase ==
                            FactionContractDeliveryCommitPhase.RewardPublished))
                    throw new InvalidOperationException(
                        "Faction material contract destination is invalid.");
                if (!material
                    && (!FactionContractDeliveryOutbox.HasCanonicalEmpty(value)
                        || value.activeContractInputOwnerActive
                        || value.deliveryTerminalCleanupPending
                        || !string.IsNullOrEmpty(
                            value.activeContractDestinationOwnerId)
                        || !string.IsNullOrEmpty(
                            value.activeContractDestinationId)))
                    throw new InvalidOperationException(
                        "Faction non-material contract owns a delivery target.");
            }
            else if (value.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.PhysicalCommitted
                || value.activeContractInputOwnerActive
                    != value.deliveryTerminalCleanupPending)
                throw new InvalidOperationException(
                    "Faction terminal delivery state is invalid.");

            bool pending = FactionContractDeliveryOutbox.HasPending(value);
            if (pending
                ? !FactionContractDeliveryOutbox.HasCanonicalPending(value)
                : !FactionContractDeliveryOutbox.HasCanonicalEmpty(value))
                throw new InvalidOperationException(
                    "Faction contract delivery outbox is invalid.");
            if (pending)
            {
                FactionContractDefinitionSO deliveryContract = catalog.Contracts
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.StableId,
                        value.deliveryContractId,
                        StringComparison.Ordinal)
                        && string.Equals(
                            candidate.factionId,
                            value.factionId,
                            StringComparison.Ordinal));
                int requiredQuantity = FactionContractMaterialRules
                    .BuildMaterialRequirements(deliveryContract)
                    .Values.Sum();
                if (deliveryContract == null
                    || value.deliveryQuantity != requiredQuantity
                    || !string.Equals(
                        value.activeContractDestinationOwnerId,
                        FactionContractDeliveryOutbox.FormatOwnerId(
                            deliveryContract.StableId,
                            value.activeContractOccurrenceId),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.activeContractDestinationId,
                        FactionContractDeliveryOutbox.FormatDestinationId(
                            deliveryContract.StableId,
                            value.activeContractOccurrenceId),
                        StringComparison.Ordinal)
                    || value.deliveryCommitPhase ==
                            FactionContractDeliveryCommitPhase.RewardPublished
                        && !value.completedContractIds.Contains(
                            deliveryContract.StableId,
                            StringComparer.Ordinal))
                    throw new InvalidOperationException(
                        "Faction contract delivery receipt identity is invalid.");
            }
            if (!value.activeContractInputOwnerActive
                && !pending
                && (!string.IsNullOrEmpty(value.activeContractDestinationId)
                    || !string.IsNullOrEmpty(
                        value.activeContractDestinationOwnerId)))
                throw new InvalidOperationException(
                    "Faction contract retained an ownerless destination.");
            if (value.activeContractInputOwnerActive
                && (string.IsNullOrWhiteSpace(
                        value.activeContractDestinationOwnerId)
                    || !string.Equals(
                        value.activeContractDestinationId,
                        EconomyProjectInputOwnerAuthority
                            .BuildFactionContractDestinationId(
                                value.activeContractDestinationOwnerId),
                        StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Faction contract input-owner identity is invalid.");
            bool ownsLifecycle = !string.IsNullOrEmpty(value.activeContractId)
                || value.activeContractInputOwnerActive
                || value.deliveryTerminalCleanupPending
                || pending;
            if (!ownsLifecycle
                && !string.IsNullOrEmpty(value.activeContractOccurrenceId))
                throw new InvalidOperationException(
                    "Faction contract retained ownerless occurrence provenance.");
            if (!string.IsNullOrEmpty(value.activeContractOccurrenceId))
            {
                string lifecycleContractId = !string.IsNullOrEmpty(
                        value.activeContractId)
                    ? value.activeContractId
                    : !string.IsNullOrEmpty(value.deliveryContractId)
                        ? value.deliveryContractId
                        : FactionContractDeliveryOutbox.TryGetContractIdFromOwnerId(
                            value.activeContractDestinationOwnerId,
                            out string ownerContractId)
                                ? ownerContractId
                                : string.Empty;
                FactionContractDefinitionSO lifecycleContract = catalog.Contracts
                    .FirstOrDefault(contract => contract != null
                        && string.Equals(
                            contract.StableId,
                            lifecycleContractId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            contract.factionId,
                            value.factionId,
                            StringComparison.Ordinal));
                if (lifecycleContract == null
                    || string.IsNullOrEmpty(lifecycleContract.seasonalEventId)
                    || !string.Equals(
                        value.lastSeasonalContractId,
                        lifecycleContract.StableId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.lastSeasonalOccurrenceId,
                        value.activeContractOccurrenceId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Faction seasonal delivery provenance is invalid.");
            }
            if (value.completedContractIds.Intersect(
                    value.failedContractIds,
                    StringComparer.Ordinal).Any())
                throw new InvalidOperationException(
                    "Faction contract is both completed and failed.");
            RequireUniqueIds(value.completedContractIds, "completed faction contract");
            RequireUniqueIds(value.failedContractIds, "failed faction contract");
            RequireUniqueIds(value.majorChoiceFlags, "faction choice flag");
        }
        RequireUniqueIds(data.factions.Select(value => value.factionId), "faction campaign");
        return data;
    }
    private RunMilestoneWorldSaveData ValidateMilestones(RunMilestoneWorldSaveData data)
    {
        if (data == null || data.version != RunMilestoneWorldSaveData.CurrentVersion || data.completedMilestoneIds == null || data.grantedRewardIds == null || data.unlockedLandmarkIds == null || data.activePressureIds == null || data.endlessCrisisAxes == null || data.worldFlags == null || data.committedChoices == null || data.nextCommittedChoiceOrdinal < 0 || data.endlessCycle < 0 || data.selfSufficiencyStreakDays < 0 || data.productivityCoverageStreakDays < 0 || data.lastMilestoneEvaluationAbsoluteDay < -1 || data.lastAccordSignalSupportAbsoluteDay < -1 || data.lastEndlessCrisisEvaluationAbsoluteDay < -1 || data.endlessCrisisInstanceId == null || data.endlessCrisisPendingInvasionSourceOwnerId == null || data.endlessCrisisDirectResponseOwnerId == null
            || (data.pendingAccordSignalOperationId.Length == 0) != (data.pendingAccordSignalCommitId.Length == 0)
            || data.pendingAccordSignalOperationId.Length > 0 && (data.pendingAccordSignalSourceStackId.Length == 0 || data.pendingAccordSignalMassGrams <= 0 || data.lastAccordSignalSupportAbsoluteDay < 1))
            throw new InvalidOperationException("Milestone save payload is invalid.");
        foreach (string id in data.completedMilestoneIds) catalog.Require(id);
        if (data.completedMilestoneIds.Distinct(StringComparer.Ordinal).Count() != data.completedMilestoneIds.Count)
            throw new InvalidOperationException("Milestone completion ids are duplicated.");
        ValidateEndlessCrisis(data);
        RequireUniqueIds(data.grantedRewardIds, "milestone reward");
        RequireUniqueIds(data.unlockedLandmarkIds, "milestone landmark");
        RequireUniqueIds(data.activePressureIds, "milestone pressure");
        RequireUniqueIds(data.worldFlags, "milestone world flag");
        long expectedOrdinal = 0;
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> decisions = new(StringComparer.Ordinal);
        foreach (V20CommittedRunChoiceSaveData choice in data.committedChoices)
        {
            ValidateCommittedChoice(choice, validateOrdinal: true);
            expectedOrdinal++;
            if (choice.ordinal != expectedOrdinal
                || !operations.Add(choice.operationId)
                || !decisions.Add(CommittedDecisionKey(choice)))
            {
                throw new InvalidOperationException(
                    "Committed run choices contain an invalid order or duplicate identity.");
            }
        }
        if (data.nextCommittedChoiceOrdinal != expectedOrdinal)
        {
            throw new InvalidOperationException(
                "Committed run choice sequence does not match its immutable history.");
        }
        return data;
    }

    private void ValidateEndlessCrisis(RunMilestoneWorldSaveData data)
    {
        if (!Enum.IsDefined(
                typeof(EndlessCrisisLifecyclePhase),
                data.endlessCrisisPhase))
            throw new InvalidOperationException(
                "Endless crisis has an invalid lifecycle phase.");

        bool hasHistory = data.endlessCrisisInstanceId.Length > 0;
        if (!hasHistory)
        {
            if (data.endlessCrisisPhase != EndlessCrisisLifecyclePhase.None
                || data.endlessCrisisAxes.Count != 0
                || data.endlessCrisisStartedAbsoluteDay != -1
                || data.endlessCrisisPressureEndAbsoluteDay != -1
                || data.endlessCrisisLastActualEndAbsoluteDay != -1
                || data.endlessCrisisRecoveryEndAbsoluteDay != -1
                || data.endlessCrisisAuthoredRecoveryDays != 0
                || data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                || data.endlessCrisisDirectResponseOwnerId.Length > 0)
                throw new InvalidOperationException(
                    "Empty endless crisis state contains lifecycle data.");
            return;
        }

        if (!IsCanonicalRequiredId(data.endlessCrisisInstanceId)
            || data.endlessCycle < 1
            || data.endlessCrisisStartedAbsoluteDay < 1
            || data.lastEndlessCrisisEvaluationAbsoluteDay
                < data.endlessCrisisStartedAbsoluteDay
            || !string.Equals(
                data.endlessCrisisInstanceId,
                $"endless-crisis:{data.endlessCycle}:{data.endlessCrisisStartedAbsoluteDay}",
                StringComparison.Ordinal)
            || data.endlessCrisisAxes.Count is not (1 or 2))
            throw new InvalidOperationException(
                "Endless crisis identity, dates, or axis count are invalid.");

        bool compound = data.endlessCrisisAxes.Count == 2;
        int expectedRecoveryDays = compound
            ? catalog.EndlessCrisisPolicy.compoundAxisRecoveryDays
            : catalog.EndlessCrisisPolicy.singleAxisRecoveryDays;
        if (data.endlessCrisisPressureEndAbsoluteDay != checked(
                data.endlessCrisisStartedAbsoluteDay
                + catalog.EndlessCrisisPolicy.pressureDurationDays)
            || data.endlessCrisisAuthoredRecoveryDays
                != expectedRecoveryDays
            || data.endlessCrisisAxes.Any(value => value == null)
            || data.endlessCrisisAxes.Select(value => value.axis)
                .Distinct().Count() != data.endlessCrisisAxes.Count)
            throw new InvalidOperationException(
                "Endless crisis authored lifetime or axes are invalid.");

        foreach (EndlessCrisisAxisSaveData modifier in
                 data.endlessCrisisAxes)
        {
            EndlessCrisisAxisPolicy authored =
                catalog.EndlessCrisisPolicy.Require(modifier.axis);
            float expectedMultiplier = compound
                ? authored.compoundAxisMultiplier
                : authored.singleAxisMultiplier;
            if (!string.Equals(
                    modifier.effectOwnerId,
                    EffectOwnerId(
                        data.endlessCrisisInstanceId,
                        modifier.axis),
                    StringComparison.Ordinal)
                || Math.Abs(modifier.multiplier - expectedMultiplier) > 0.000001f)
                throw new InvalidOperationException(
                    "Endless crisis modifier owner or authored strength is invalid.");
        }

        EndlessCrisisAxisSaveData combat = data.endlessCrisisAxes
            .SingleOrDefault(value => value.axis == EndlessCrisisAxis.Combat);
        string combatOwner = combat?.effectOwnerId ?? string.Empty;
        bool validPending = data.endlessCrisisPendingInvasionSourceOwnerId
                .Length == 0
            || (combatOwner.Length > 0
            && string.Equals(
                data.endlessCrisisPendingInvasionSourceOwnerId,
                combatOwner,
                StringComparison.Ordinal));
        bool validResponse = data.endlessCrisisDirectResponseOwnerId.Length == 0
            || (combatOwner.Length > 0
            && string.Equals(
                data.endlessCrisisDirectResponseOwnerId,
                combatOwner + ":invasion",
                StringComparison.Ordinal));
        if (!validPending
            || !validResponse
            || data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                && data.endlessCrisisDirectResponseOwnerId.Length > 0)
            throw new InvalidOperationException(
                "Endless crisis invasion ownership is invalid.");

        switch (data.endlessCrisisPhase)
        {
            case EndlessCrisisLifecyclePhase.Active:
                bool earlyDirectResponseEnded =
                    data.endlessCrisisLastActualEndAbsoluteDay >=
                        data.endlessCrisisStartedAbsoluteDay
                    && data.endlessCrisisLastActualEndAbsoluteDay <
                        data.endlessCrisisPressureEndAbsoluteDay
                    && data.endlessCrisisPendingInvasionSourceOwnerId.Length == 0
                    && data.endlessCrisisDirectResponseOwnerId.Length == 0;
                if (data.endlessCrisisRecoveryEndAbsoluteDay != -1
                    || data.endlessCrisisLastActualEndAbsoluteDay != -1
                        && !earlyDirectResponseEnded)
                    throw new InvalidOperationException(
                        "Active endless crisis contains terminal dates.");
                break;
            case EndlessCrisisLifecyclePhase.AwaitingDirectResponse:
                if (data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                    || data.endlessCrisisDirectResponseOwnerId.Length == 0
                    || data.endlessCrisisLastActualEndAbsoluteDay != -1
                    || data.endlessCrisisRecoveryEndAbsoluteDay != -1)
                    throw new InvalidOperationException(
                        "Awaiting endless crisis has invalid response ownership.");
                break;
            case EndlessCrisisLifecyclePhase.Recovery:
            case EndlessCrisisLifecyclePhase.None:
                if (data.endlessCrisisPendingInvasionSourceOwnerId.Length > 0
                    || data.endlessCrisisDirectResponseOwnerId.Length > 0
                    || data.endlessCrisisLastActualEndAbsoluteDay
                        < data.endlessCrisisPressureEndAbsoluteDay
                    || data.endlessCrisisRecoveryEndAbsoluteDay != checked(
                        data.endlessCrisisLastActualEndAbsoluteDay
                        + data.endlessCrisisAuthoredRecoveryDays))
                    throw new InvalidOperationException(
                        "Completed endless crisis recovery lifetime is invalid.");
                break;
            default:
                throw new InvalidOperationException(
                    "Endless crisis lifecycle phase is unsupported.");
        }
    }

    private void RequireValidObservedMealEvidence(
        ObservedMealIncidentEvidenceSaveData evidence)
    {
        if (evidence == null
            || evidence.version
                != ObservedMealIncidentEvidenceSaveData.CurrentVersion
            || !Enum.IsDefined(
                typeof(ObservedIncidentSourceKind),
                evidence.sourceKind)
            || !new CharacterId(evidence.targetCharacterId).IsValid
            || !string.Equals(
                new CharacterId(evidence.targetCharacterId).Value,
                evidence.targetCharacterId,
                StringComparison.Ordinal)
            || !IsCanonicalRequiredId(evidence.definitionId)
            || !IsCanonicalRequiredId(evidence.occurrenceInstanceId)
            || evidence.absoluteDay < 1
            || evidence.observedDead)
        {
            throw new InvalidOperationException(
                "Observed service-incident evidence has an invalid identity or shape.");
        }

        ServiceIncidentDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(evidence.definitionId)
                as ServiceIncidentDefinitionSO;
        if (definition == null
            || definition.kind != evidence.incidentKind)
        {
            throw new InvalidOperationException(
                $"Observed service-incident evidence '{evidence.operationId}' is inconsistent with its definition.");
        }

        ObservedMealIncidentEvidenceSaveData expected = null;
        bool valid;
        bool culturalFieldsEmpty = string.IsNullOrEmpty(
                evidence.instigatorCharacterId)
            && string.IsNullOrEmpty(evidence.instigatorCultureId)
            && string.IsNullOrEmpty(evidence.targetCultureId);
        if (evidence.sourceKind == ObservedIncidentSourceKind.Meal)
        {
            ConsumableOperationId operationId = new(evidence.operationId);
            ItemDefinitionId itemDefinitionId = new(evidence.itemDefinitionId);
            ItemStackId itemStackId = new(evidence.itemStackId);
            valid = operationId.IsValid
                && string.Equals(
                    operationId.Value,
                    evidence.operationId,
                    StringComparison.Ordinal)
                && itemDefinitionId.IsValid
                && string.Equals(
                    itemDefinitionId.Value,
                    evidence.itemDefinitionId,
                    StringComparison.Ordinal)
                && itemStackId.IsValid
                && string.Equals(
                    itemStackId.Value,
                    evidence.itemStackId,
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(evidence.sourceOperationId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && string.IsNullOrEmpty(
                    evidence.otherParticipantCharacterId)
                && culturalFieldsEmpty
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && evidence.fieldMeal != new BuildingInstanceId(
                    evidence.facilityInstanceId).IsValid
                && TryBuildObservedMealEvidence(
                    new ObservedMealIncidentSnapshot(
                        operationId,
                        new CharacterId(evidence.targetCharacterId),
                        itemDefinitionId,
                        itemStackId,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        evidence.fieldMeal,
                        new CoreGridCell(evidence.locationX, evidence.locationY),
                        evidence.absoluteDay,
                        evidence.observedPolicyViolation,
                        evidence.observedDowned,
                        evidence.observedDead,
                        observedContaminated:
                            evidence.incidentKind
                                == ServiceIncidentKind.Contamination),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out ServiceIncidentKind expectedKind,
                    out expected)
                && expectedKind == evidence.incidentKind;
        }
        else if (evidence.sourceKind == ObservedIncidentSourceKind.Shoplifting)
        {
            valid = string.IsNullOrEmpty(evidence.itemStackId)
                && string.IsNullOrEmpty(
                    evidence.otherParticipantCharacterId)
                && culturalFieldsEmpty
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedShopliftingEvidence(
                    new ObservedShopliftingIncidentSnapshot(
                        evidence.operationId,
                        evidence.sourceOperationId,
                        new CharacterId(evidence.targetCharacterId),
                        new ItemDefinitionId(evidence.itemDefinitionId),
                        evidence.saleItemId,
                        evidence.itemInstanceId,
                        evidence.sourceStackId,
                        evidence.quantity,
                        evidence.unitMassGrams,
                        evidence.componentFingerprint,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(evidence.locationX, evidence.locationY),
                        evidence.absoluteDay,
                        evidence.lossValue),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind == ServiceIncidentKind.Theft;
        }
        else if (evidence.sourceKind == ObservedIncidentSourceKind.Combat)
        {
            valid = string.IsNullOrEmpty(evidence.itemDefinitionId)
                && string.IsNullOrEmpty(evidence.itemStackId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && culturalFieldsEmpty
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedBrawlEvidence(
                    new ObservedBrawlIncidentSnapshot(
                        evidence.operationId,
                        new CharacterId(evidence.targetCharacterId),
                        new CharacterId(
                            evidence.otherParticipantCharacterId),
                        evidence.customerWasAttacker,
                        evidence.actualDamage,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(
                            evidence.locationX,
                            evidence.locationY),
                        evidence.absoluteDay),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind == ServiceIncidentKind.Brawl;
        }
        else
        {
            CharacterId instigator = new(evidence.instigatorCharacterId);
            CharacterId customer = new(evidence.targetCharacterId);
            CharacterId other = new(evidence.otherParticipantCharacterId);
            CharacterId target = instigator.Equals(customer)
                ? other
                : customer;
            valid = evidence.sourceKind
                    == ObservedIncidentSourceKind.SocialConflict
                && string.IsNullOrEmpty(evidence.itemDefinitionId)
                && string.IsNullOrEmpty(evidence.itemStackId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedCulturalConflictEvidence(
                    new ObservedCulturalConflictIncidentSnapshot(
                        evidence.operationId,
                        instigator,
                        target,
                        customer,
                        new SpeciesCultureId(evidence.instigatorCultureId),
                        new SpeciesCultureId(evidence.targetCultureId),
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(
                            evidence.locationX,
                            evidence.locationY),
                        evidence.absoluteDay),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind
                    == ServiceIncidentKind.CulturalInsult;
        }
        if (!valid || !SameObservedMealEvidence(evidence, expected))
        {
            throw new InvalidOperationException(
                $"Observed service-incident evidence '{evidence.operationId}' is inconsistent with its committed facts.");
        }
    }

    private void ValidateCommittedChoice(
        V20CommittedRunChoiceSaveData choice,
        bool validateOrdinal)
    {
        if (choice == null
            || !Enum.IsDefined(typeof(CommittedRunChoiceKind), choice.kind)
            || choice.kind == CommittedRunChoiceKind.None
            || !IsCanonicalRequiredId(choice.ownerId)
            || !IsCanonicalRequiredId(choice.definitionId)
            || !IsCanonicalRequiredId(choice.instanceId)
            || !IsCanonicalRequiredId(choice.choiceId)
            || !IsCanonicalRequiredId(choice.operationId)
            || validateOrdinal && choice.ordinal <= 0)
        {
            throw new InvalidOperationException(
                "Committed run choice contains an invalid kind, identifier, or ordinal.");
        }

        V20AuthoredContentSO definition = choice.kind switch
        {
            CommittedRunChoiceKind.SocietyEventChoice =>
                ((ISocietyEventCatalog)catalog).Require(choice.definitionId),
            CommittedRunChoiceKind.FactionChapterChoice =>
                ((IFactionStoryCatalog)catalog).Require(choice.definitionId),
            _ => throw new InvalidOperationException(
                "Committed run choice kind is unsupported.")
        };
        switch (choice.kind)
        {
            case CommittedRunChoiceKind.SocietyEventChoice:
                bool validSocietyDefinition = definition is LifeEventDefinitionSO
                    or GuestRequestDefinitionSO
                    or ServiceIncidentDefinitionSO;
                bool validSocietyChoice = definition is GuestRequestDefinitionSO
                    ? choice.choiceId is "fulfill" or "decline"
                    : GetChoices(definition).Any(value => value != null
                        && string.Equals(
                            value.choiceId,
                            choice.choiceId,
                            StringComparison.Ordinal));
                if (!validSocietyDefinition
                    || !validSocietyChoice
                    || !string.Equals(
                        choice.ownerId,
                        SocietyChoiceOwnerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Committed society choice does not match authored content or its owner.");
                }
                break;
            case CommittedRunChoiceKind.FactionChapterChoice:
                if (definition is not FactionChapterDefinitionSO chapter
                    || !string.Equals(
                        choice.ownerId,
                        chapter.factionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        choice.instanceId,
                        chapter.StableId,
                        StringComparison.Ordinal)
                    || !chapter.choices.Any(value => value != null
                        && string.Equals(
                            value.choiceId,
                            choice.choiceId,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "Committed faction choice does not match authored content or its owner.");
                }
                break;
            default:
                throw new InvalidOperationException(
                    "Committed run choice kind is unsupported.");
        }
    }

    private static CommittedRunChoiceSnapshot ToCommittedChoiceSnapshot(
        V20CommittedRunChoiceSaveData choice) =>
        new(
            choice.kind,
            choice.ownerId,
            choice.definitionId,
            choice.instanceId,
            choice.choiceId,
            choice.operationId,
            choice.ordinal);

    private static bool SameCommittedDecision(
        V20CommittedRunChoiceSaveData left,
        V20CommittedRunChoiceSaveData right) =>
        left.kind == right.kind
        && string.Equals(left.ownerId, right.ownerId, StringComparison.Ordinal)
        && string.Equals(left.definitionId, right.definitionId, StringComparison.Ordinal)
        && string.Equals(left.instanceId, right.instanceId, StringComparison.Ordinal)
        && string.Equals(left.choiceId, right.choiceId, StringComparison.Ordinal);

    private static string CommittedDecisionKey(
        V20CommittedRunChoiceSaveData choice) =>
        $"{(int)choice.kind}:{choice.ownerId}:{choice.instanceId}";

    private static bool IsCanonicalRequiredId(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static void UpdateSelfSufficiency(
        RunMilestoneWorldSaveData state,
        RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot.AbsoluteDay < 0
            || state.lastMilestoneEvaluationAbsoluteDay >= snapshot.AbsoluteDay)
        {
            snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
                state.selfSufficiencyStreakDays;
            snapshot.WorldMetrics[V20WorldMetricKind.ProductivityCoverageDays] =
                state.productivityCoverageStreakDays;
            if (state.selfSufficiencyStreakDays >= 120)
                snapshot.WorldFlags.Add("ecology:closed-cycle");
            if (state.productivityCoverageStreakDays >= 120)
                snapshot.WorldFlags.Add("temporal:productivity-sustained");
            return;
        }

        bool consecutive = state.lastMilestoneEvaluationAbsoluteDay < 0
            || snapshot.AbsoluteDay
                == state.lastMilestoneEvaluationAbsoluteDay + 1;
        bool sufficientToday = snapshot.WorldFlags.Contains(
            "ecology:self-sufficient-today");
        state.selfSufficiencyStreakDays = sufficientToday
            ? (consecutive ? state.selfSufficiencyStreakDays + 1 : 1)
            : 0;
        bool productivityQualifiedToday = snapshot.WorldFlags.Contains(
            "temporal:productivity-qualified-today");
        state.productivityCoverageStreakDays = productivityQualifiedToday
            ? (consecutive ? state.productivityCoverageStreakDays + 1 : 1)
            : 0;
        state.lastMilestoneEvaluationAbsoluteDay = snapshot.AbsoluteDay;
        snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
            state.selfSufficiencyStreakDays;
        if (state.selfSufficiencyStreakDays >= 120)
            snapshot.WorldFlags.Add("ecology:closed-cycle");
        snapshot.WorldMetrics[V20WorldMetricKind.ProductivityCoverageDays] =
            state.productivityCoverageStreakDays;
        if (state.productivityCoverageStreakDays >= 120)
            snapshot.WorldFlags.Add("temporal:productivity-sustained");
    }

    private void RequireValidEvents(
        IEnumerable<V20ActiveEventSaveData> source,
        bool seasonal)
    {
        HashSet<string> instances = new(StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData value in source ?? Array.Empty<V20ActiveEventSaveData>())
        {
            if (value == null || string.IsNullOrWhiteSpace(value.instanceId)
                || !instances.Add(value.instanceId)
                || value.startedAbsoluteDay < 0
                || value.deadlineAbsoluteDay < value.startedAbsoluteDay
                || value.generation < 0
                || !catalog.Arcs.Any(arc => string.Equals(
                    arc.factionId,
                    value.contextFactionId,
                    StringComparison.Ordinal))
                || value.participantCharacterIds == null
                || value.participantCharacterIds.Any(string.IsNullOrWhiteSpace)
                || value.participantCharacterIds.Distinct(StringComparer.Ordinal).Count()
                    != value.participantCharacterIds.Count)
                throw new InvalidOperationException("V20 event instance is invalid.");
            if (seasonal)
            {
                if (value.hasObservedMealIncident
                    || HasObservedMealIncidentPayload(
                        value.observedMealIncident)
                    || HasObservedIncidentResponsePayload(
                        value.observedIncidentResponse))
                {
                    throw new InvalidOperationException(
                        $"Seasonal event '{value.instanceId}' contains society incident evidence.");
                }
                SeasonalWorldEventDefinitionSO seasonalDefinition =
                    ((IWorldEventCatalog)catalog).Require(
                        value.definitionId);
                SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                    value.seasonalWildlifeArrival,
                    seasonalDefinition.wildlifeArrivalProfile,
                    value.instanceId);
                SeasonalDriftCargoRules.RequireValidFrozenState(
                    value.seasonalDriftCargo,
                    seasonalDefinition.driftCargoProfile,
                    value.instanceId);
                SeasonalArcaneEventRules.RequireValidFrozenState(
                    value.seasonalManaLightning,
                    seasonalDefinition.manaLightningProfile,
                    value.instanceId);
                SeasonalArcaneEventRules.RequireValidFrozenState(
                    value.seasonalSpecialExpedition,
                    seasonalDefinition.specialExpeditionProfile,
                    value.instanceId);
                continue;
            }

            SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                value.seasonalWildlifeArrival,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalDriftCargoRules.RequireValidFrozenState(
                value.seasonalDriftCargo,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalArcaneEventRules.RequireValidFrozenState(
                value.seasonalManaLightning,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalArcaneEventRules.RequireValidFrozenState(
                value.seasonalSpecialExpedition,
                authored: null,
                occurrenceInstanceId: value.instanceId);

            V20AuthoredContentSO societyDefinition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            if (societyDefinition is ServiceIncidentDefinitionSO)
            {
                if (!value.hasObservedMealIncident
                    || value.observedMealIncident == null
                    || value.participantCharacterIds.Count != 1
                    || !string.Equals(
                        value.participantCharacterIds[0],
                        value.observedMealIncident.targetCharacterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.instanceId,
                        value.observedMealIncident.occurrenceInstanceId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.definitionId,
                        value.observedMealIncident.definitionId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Service incident '{value.instanceId}' has no exact observed-meal evidence owner.");
                }
                RequireValidObservedMealEvidence(value.observedMealIncident);
                RequireValidObservedIncidentResponse(value);
            }
            else if (value.hasObservedMealIncident
                || HasObservedMealIncidentPayload(
                    value.observedMealIncident)
                || HasObservedIncidentResponsePayload(
                    value.observedIncidentResponse))
            {
                throw new InvalidOperationException(
                    $"Non-incident society event '{value.instanceId}' contains observed-meal evidence.");
            }
            SocietyEventRiskContract.RequireValid(
                value.riskTier,
                value.riskReason,
                value.instanceId);
            if (value.terminalEffectKinds == null
                || value.terminalEffectKinds.Any(kind =>
                    kind == V20ContentEffectKind.None
                    || !Enum.IsDefined(typeof(V20ContentEffectKind), kind))
                || value.terminalEffectKinds.Distinct().Count()
                    != value.terminalEffectKinds.Count
                || !value.terminalEffectKinds.SequenceEqual(
                    value.terminalEffectKinds.OrderBy(kind => kind)))
            {
                throw new InvalidOperationException(
                    $"Society event '{value.instanceId}' has invalid terminal effect types.");
            }

            bool hasTerminalText = !string.IsNullOrEmpty(
                    value.terminalActionLabel)
                || !string.IsNullOrEmpty(value.terminalOutcomeNarrative)
                || value.terminalEffectKinds.Count > 0;
            if (value.resolved)
            {
                if (string.IsNullOrWhiteSpace(value.resolutionId)
                    || !IsCanonicalRequiredId(value.resolutionId)
                    || string.IsNullOrWhiteSpace(value.terminalActionLabel)
                    || !string.Equals(
                        value.terminalActionLabel,
                        value.terminalActionLabel.Trim(),
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(
                        value.terminalOutcomeNarrative)
                    || !string.Equals(
                        value.terminalOutcomeNarrative,
                        value.terminalOutcomeNarrative.Trim(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Resolved society event '{value.instanceId}' has no canonical terminal result.");
                }
            }
            else if (!string.IsNullOrEmpty(value.resolutionId)
                || hasTerminalText)
            {
                throw new InvalidOperationException(
                    $"Pending society event '{value.instanceId}' contains a terminal result.");
            }
        }
    }

    private static void RequireValidObservedIncidentResponse(
        V20ActiveEventSaveData occurrence)
    {
        ObservedIncidentResponseSaveData response =
            occurrence?.observedIncidentResponse;
        if (response == null)
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence?.instanceId}' has no response owner state.");
        }
        if (!Enum.IsDefined(
                typeof(ObservedIncidentResponsePhase),
                response.phase))
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence.instanceId}' has an unknown response phase.");
        }

        bool emptyChoice = string.IsNullOrEmpty(response.choiceId);
        bool emptyOperation = string.IsNullOrEmpty(response.operationId);
        bool emptyExternal = string.IsNullOrEmpty(
            response.externalOperationId);
        bool emptyReceipt = string.IsNullOrEmpty(response.receiptId);
        bool emptyFailure = string.IsNullOrEmpty(response.failureReason);
        if (response.phase == ObservedIncidentResponsePhase.None)
        {
            if (!emptyChoice || !emptyOperation || !emptyExternal
                || !emptyReceipt || !emptyFailure)
            {
                throw new InvalidOperationException(
                    $"Service incident '{occurrence.instanceId}' has a non-empty idle response owner.");
            }
            return;
        }

        ServiceIncidentKind kind = occurrence.observedMealIncident.incidentKind;
        bool supportedChoice = kind == ServiceIncidentKind.ForbiddenMeal
                && string.Equals(
                    response.choiceId,
                    "replace",
                    StringComparison.Ordinal)
            || kind == ServiceIncidentKind.MedicalCollapse
                && (string.Equals(
                        response.choiceId,
                        "emergency-care",
                        StringComparison.Ordinal)
                    || string.Equals(
                        response.choiceId,
                        "transfer",
                        StringComparison.Ordinal));
        string expectedOperation = supportedChoice
            ? FormatObservedIncidentResponseOperationId(
                occurrence.instanceId,
                response.choiceId)
            : string.Empty;
        bool identityValid = supportedChoice
            && !emptyChoice
            && !emptyOperation
            && string.Equals(
                response.operationId,
                expectedOperation,
                StringComparison.Ordinal)
            && string.Equals(
                occurrence.selectedChoiceId,
                response.choiceId,
                StringComparison.Ordinal);
        bool externalIdentityValid = emptyExternal
            || string.Equals(response.choiceId, "replace", StringComparison.Ordinal)
                && string.Equals(
                    response.externalOperationId,
                    response.operationId,
                    StringComparison.Ordinal)
            || string.Equals(
                    response.choiceId,
                    "emergency-care",
                    StringComparison.Ordinal)
                && IsCanonicalRequiredId(response.externalOperationId)
            || string.Equals(response.choiceId, "transfer", StringComparison.Ordinal)
                && TryClassifyObservedIncidentTransferExternalOperationId(
                    occurrence,
                    response.externalOperationId,
                    out _);
        bool receiptIdentityValid = emptyReceipt
            || IsExpectedObservedIncidentResponseReceipt(
                occurrence,
                response.externalOperationId,
                response.receiptId);
        bool shapeValid = response.phase switch
        {
            ObservedIncidentResponsePhase.Accepted =>
                !occurrence.resolved
                && emptyExternal && emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.ExternalOperationStarted =>
                !occurrence.resolved
                && !emptyExternal && emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.ReceiptCommitted =>
                !occurrence.resolved
                && !emptyExternal && !emptyReceipt && emptyFailure,
            ObservedIncidentResponsePhase.Failed =>
                occurrence.resolved
                && emptyReceipt && !emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    ObservedIncidentResponseFailureResolutionId(
                        response.phase),
                    StringComparison.Ordinal)
                && occurrence.terminalEffectKinds.Count == 0
                && string.Equals(
                    occurrence.terminalActionLabel,
                    "현장 대응 실패",
                    StringComparison.Ordinal)
                && string.Equals(
                    occurrence.terminalOutcomeNarrative,
                    FormatObservedIncidentResponseFailureNarrative(response),
                    StringComparison.Ordinal),
            ObservedIncidentResponsePhase.Cancelled =>
                occurrence.resolved
                && emptyReceipt && !emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    ObservedIncidentResponseFailureResolutionId(
                        response.phase),
                    StringComparison.Ordinal)
                && occurrence.terminalEffectKinds.Count == 0
                && string.Equals(
                    occurrence.terminalActionLabel,
                    "현장 대응 취소",
                    StringComparison.Ordinal)
                && string.Equals(
                    occurrence.terminalOutcomeNarrative,
                    FormatObservedIncidentResponseFailureNarrative(response),
                    StringComparison.Ordinal),
            ObservedIncidentResponsePhase.EffectsPublished =>
                occurrence.resolved
                && !emptyExternal && !emptyReceipt && emptyFailure
                && string.Equals(
                    occurrence.resolutionId,
                    response.choiceId,
                    StringComparison.Ordinal),
            _ => false
        };
        if (!identityValid
            || !externalIdentityValid
            || !receiptIdentityValid
            || !shapeValid
            || !emptyExternal
                && !IsCanonicalRequiredId(response.externalOperationId)
            || !emptyReceipt && !IsCanonicalRequiredId(response.receiptId)
            || !emptyFailure
                && !string.Equals(
                    response.failureReason,
                    response.failureReason.Trim(),
                    StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Service incident '{occurrence.instanceId}' has an invalid response owner state.");
        }
    }

    private static bool HasObservedIncidentResponsePayload(
        ObservedIncidentResponseSaveData response) =>
        response != null
        && (response.phase != ObservedIncidentResponsePhase.None
            || !string.IsNullOrEmpty(response.choiceId)
            || !string.IsNullOrEmpty(response.operationId)
            || !string.IsNullOrEmpty(response.externalOperationId)
            || !string.IsNullOrEmpty(response.receiptId)
            || !string.IsNullOrEmpty(response.failureReason));

    private static bool HasObservedMealIncidentPayload(
        ObservedMealIncidentEvidenceSaveData evidence) =>
        evidence != null
        && (!string.IsNullOrEmpty(evidence.operationId)
            || !string.IsNullOrEmpty(evidence.sourceOperationId)
            || !string.IsNullOrEmpty(evidence.occurrenceInstanceId)
            || !string.IsNullOrEmpty(evidence.definitionId)
            || evidence.incidentKind != default
            || !string.IsNullOrEmpty(evidence.targetCharacterId)
            || !string.IsNullOrEmpty(evidence.itemDefinitionId)
            || !string.IsNullOrEmpty(evidence.itemStackId)
            || evidence.sourceKind != ObservedIncidentSourceKind.Meal
            || evidence.saleItemId != 0
            || !string.IsNullOrEmpty(evidence.itemInstanceId)
            || !string.IsNullOrEmpty(evidence.sourceStackId)
            || evidence.quantity != 0
            || evidence.unitMassGrams != 0L
            || !string.IsNullOrEmpty(evidence.componentFingerprint)
            || evidence.lossValue != 0
            || !string.IsNullOrEmpty(
                evidence.otherParticipantCharacterId)
            || !string.IsNullOrEmpty(evidence.instigatorCharacterId)
            || !string.IsNullOrEmpty(evidence.instigatorCultureId)
            || !string.IsNullOrEmpty(evidence.targetCultureId)
            || evidence.customerWasAttacker
            || evidence.actualDamage != 0f
            || !string.IsNullOrEmpty(evidence.facilityInstanceId)
            || evidence.fieldMeal
            || evidence.locationX != 0
            || evidence.locationY != 0
            || evidence.absoluteDay != 0
            || evidence.observedPolicyViolation
            || evidence.observedDowned
            || evidence.observedDead
            || !string.IsNullOrEmpty(evidence.observedCause));

    private static void RequireUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException($"{label} ids are invalid.");
    }

    private static SeasonalEventWorldSaveData Clone(SeasonalEventWorldSaveData value) => JsonClone(value);
    private static SocietyEventWorldSaveData Clone(SocietyEventWorldSaveData value) => JsonClone(value);
    private static FactionCampaignWorldSaveData Clone(FactionCampaignWorldSaveData value) => JsonClone(value);
    private static RunMilestoneWorldSaveData Clone(RunMilestoneWorldSaveData value) => JsonClone(value);
    private static T JsonClone<T>(T value) where T : class =>
        UnityEngine.JsonUtility.FromJson<T>(UnityEngine.JsonUtility.ToJson(value ?? throw new ArgumentNullException(nameof(value))));
    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    private static string RewardId(string milestoneId)
    {
        string normalized = Normalize(milestoneId);
        return normalized.StartsWith("reward:", StringComparison.Ordinal)
            ? normalized
            : $"reward:{normalized}";
    }

    private static string PressureId(string milestoneId)
    {
        string normalized = Normalize(milestoneId);
        return normalized.StartsWith("pressure:", StringComparison.Ordinal)
            ? normalized
            : $"pressure:{normalized}";
    }
}
