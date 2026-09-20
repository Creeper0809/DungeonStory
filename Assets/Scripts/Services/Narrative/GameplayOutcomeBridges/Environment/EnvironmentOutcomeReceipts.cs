using System;
using DungeonStory.Narrative.Korean;

public static class EnvironmentOutcomeIds
{
    public static readonly GameplayOutcomeTypeId CertifiedSeedCompleted =
        new("environment.certified-seed-completed");
    public static readonly GameplayOutcomeTypeId CropPlanTerminal =
        new("environment.crop-plan-terminal");
    public static readonly GameplayOutcomeTypeId CropIrrigationSupplied =
        new("environment.crop-irrigation-supplied");
    public static readonly GameplayOutcomeTypeId FireIgnition =
        new("environment.fire-ignition");
    public static readonly GameplayOutcomeTypeId FireSuppression =
        new("environment.fire-suppression");
    public static readonly GameplayOutcomeTypeId FireDamage =
        new("environment.fire-damage");
    public static readonly GameplayOutcomeTypeId FireFuelLoss =
        new("environment.fire-fuel-loss");
    public static readonly GameplayOutcomeTypeId FireWaterConsumed =
        new("environment.fire-water-consumed");
    public static readonly GameplayOutcomeTypeId DiseaseRouteExposure =
        new("environment.disease-route-exposure");
    public static readonly GameplayOutcomeTypeId ProcessAccident =
        new("environment.process-accident");
    public static readonly GameplayOutcomeTypeId RoomConditionChanged =
        new("environment.room-condition-changed");
    public static readonly GameplayOutcomeTypeId RoomExperienceApplied =
        new("environment.room-experience-applied");
    public static readonly GameplayOutcomeTypeId SpeciesIncidentTriggered =
        new("environment.species-incident-triggered");
    public static readonly GameplayOutcomeTypeId HarpyGaleRelocation =
        new("environment.harpy-gale-relocation");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId ItemDefinitionKind = new("item-definition");
    public static readonly GameplayEntityKindId ItemInstanceKind = new("item-instance");
    public static readonly GameplayEntityKindId ItemStackKind = new("item-stack");
    public static readonly GameplayEntityKindId CropDefinitionKind = new("crop-definition");
    public static readonly GameplayEntityKindId DiseaseDefinitionKind = new("disease-definition");
    public static readonly GameplayEntityKindId SpeciesDefinitionKind = new("species-definition");
    public static readonly GameplayEntityKindId RoomKind = new("room");
    public static readonly GameplayEntityKindId FireKind = new("fire");
    public static readonly GameplayEntityKindId OperationKind = new("operation");

    public static readonly GameplayRoleId ActorRole = new("actor");
    public static readonly GameplayRoleId FacilityRole = new("facility");
    public static readonly GameplayRoleId CropRole = new("crop");
    public static readonly GameplayRoleId DiseaseRole = new("disease");
    public static readonly GameplayRoleId SpeciesRole = new("species");
    public static readonly GameplayRoleId RoomRole = new("room");
    public static readonly GameplayRoleId FireRole = new("fire");
    public static readonly GameplayRoleId TargetRole = new("target");
    public static readonly GameplayRoleId ItemRole = new("item");

    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId HourUnit = new("hour");
    public static readonly GameplayMetricUnitId SecondUnit = new("second");
    public static readonly GameplayMetricUnitId RatioUnit = new("ratio");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId CellUnit = new("cell");

    public static readonly GameplayMetricId QuantityMetric = new("quantity");
    public static readonly GameplayMetricId MassMetric = new("mass");
    public static readonly GameplayMetricId InputQuantityMetric = new("input-quantity");
    public static readonly GameplayMetricId InputMassMetric = new("input-mass");
    public static readonly GameplayMetricId OutputQuantityMetric = new("output-quantity");
    public static readonly GameplayMetricId OutputMassMetric = new("output-mass");
    public static readonly GameplayMetricId ExposureHoursMetric = new("exposure-hours");
    public static readonly GameplayMetricId EnvironmentCoefficientMetric = new("environment-coefficient");
    public static readonly GameplayMetricId DamageMetric = new("damage");
    public static readonly GameplayMetricId PreviousCleanlinessMetric = new("previous-cleanliness");
    public static readonly GameplayMetricId CurrentCleanlinessMetric = new("current-cleanliness");
    public static readonly GameplayMetricId ImpressionMoodMetric = new("impression-mood");
    public static readonly GameplayMetricId CleanlinessMoodMetric = new("cleanliness-mood");
    public static readonly GameplayMetricId DurationMetric = new("duration");
    public static readonly GameplayMetricId SequenceMetric = new("sequence");
    public static readonly GameplayMetricId StatusMetric = new("status");
    public static readonly GameplayMetricId IntensityBeforeMetric = new("intensity-before");
    public static readonly GameplayMetricId IntensityAfterMetric = new("intensity-after");
    public static readonly GameplayMetricId WaterUnitsMetric = new("water-units");
    public static readonly GameplayMetricId WaterQualityMetric = new("water-quality");
    public static readonly GameplayMetricId SourceXMetric = new("source-x");
    public static readonly GameplayMetricId SourceYMetric = new("source-y");
    public static readonly GameplayMetricId DestinationXMetric = new("destination-x");
    public static readonly GameplayMetricId DestinationYMetric = new("destination-y");

    public static readonly GameplayOutcomeTagId EnvironmentTag = new("domain:environment");
    public static readonly GameplayOutcomeTagId AgricultureTag = new("domain:agriculture");
    public static readonly GameplayOutcomeTagId DiseaseTag = new("domain:disease");
    public static readonly GameplayOutcomeTagId DisasterTag = new("domain:disaster");
    public static readonly GameplayOutcomeTagId WildlifeTag = new("domain:wildlife");

    public static readonly GameplayOutcomeFactId ReceiptKindFact = new("receipt-kind");
    public static readonly GameplayOutcomeFactId SourceDigestFact = new("source-digest");
    public static readonly GameplayOutcomeFactId CorrelationFact = new("correlation-id");
    public static readonly GameplayOutcomeFactId ReasonFact = new("reason-code");
    public static readonly GameplayOutcomeFactId DetailFact = new("detail");
    public static readonly GameplayOutcomeFactId VectorFact = new("exact-vector");
    public static readonly GameplayOutcomeFactId ActivityFact = new("activity-id");
    public static readonly GameplayOutcomeFactId WorkTypeFact = new("work-type-id");
    public static readonly GameplayOutcomeFactId AnatomyNodeFact = new("anatomy-node-id");
    public static readonly GameplayOutcomeFactId IncidentFact = new("incident-id");
    public static readonly GameplayOutcomeFactId SummaryFact = new("summary");
    public static readonly GameplayOutcomeFactId RouteFact = new("route");
    public static readonly GameplayOutcomeFactId InputDigestFact = new("input-vector-digest");
    public static readonly GameplayOutcomeFactId OutputDigestFact = new("output-vector-digest");
    public static readonly GameplayOutcomeFactId CommitFact = new("commit-id");
    public static readonly GameplayOutcomeFactId FingerprintFact = new("outcome-fingerprint");
    public static readonly GameplayOutcomeFactId IndoorFact = new("indoor");
    public static readonly GameplayOutcomeFactId SourceStackFact = new("source-stack-id");
    public static readonly GameplayOutcomeFactId DestinationStackFact = new("destination-stack-id");
    public static readonly GameplayOutcomeFactId ItemDefinitionFact = new("item-definition-id");
    public static readonly GameplayOutcomeFactId ItemInstanceFact = new("item-instance-id");

    public const string CertifiedSeedProducer = "environment.certified-seed";
    public const string CropPlanProducer = "environment.crop-plan";
    public const string CropIrrigationProducer = "environment.crop-irrigation";
    public const string FireIgnitionProducer = "environment.fire-ignition";
    public const string FireSuppressionProducer = "environment.fire-suppression";
    public const string FireDamageProducer = "environment.fire-damage";
    public const string FireFuelProducer = "environment.fire-fuel";
    public const string FireWaterProducer = "environment.fire-water";
    public const string DiseaseExposureProducer = "environment.disease-exposure";
    public const string ProcessAccidentProducer = "environment.process-accident";
    public const string RoomConditionProducer = "environment.room-condition";
    public const string RoomExperienceProducer = "environment.room-experience";
    public const string SpeciesIncidentProducer = "environment.species-incident";
}

public struct EnvironmentFixedBuffer16<T>
{
    private T item0; private T item1; private T item2; private T item3;
    private T item4; private T item5; private T item6; private T item7;
    private T item8; private T item9; private T item10; private T item11;
    private T item12; private T item13; private T item14; private T item15;

    public int Count { get; private set; }

    public bool TryAdd(in T value)
    {
        switch (Count)
        {
            case 0: item0 = value; break; case 1: item1 = value; break;
            case 2: item2 = value; break; case 3: item3 = value; break;
            case 4: item4 = value; break; case 5: item5 = value; break;
            case 6: item6 = value; break; case 7: item7 = value; break;
            case 8: item8 = value; break; case 9: item9 = value; break;
            case 10: item10 = value; break; case 11: item11 = value; break;
            case 12: item12 = value; break; case 13: item13 = value; break;
            case 14: item14 = value; break; case 15: item15 = value; break;
            default: return false;
        }
        Count++;
        return true;
    }

    public T Get(int index) => index switch
    {
        0 when index < Count => item0, 1 when index < Count => item1,
        2 when index < Count => item2, 3 when index < Count => item3,
        4 when index < Count => item4, 5 when index < Count => item5,
        6 when index < Count => item6, 7 when index < Count => item7,
        8 when index < Count => item8, 9 when index < Count => item9,
        10 when index < Count => item10, 11 when index < Count => item11,
        12 when index < Count => item12, 13 when index < Count => item13,
        14 when index < Count => item14, 15 when index < Count => item15,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

public readonly struct EnvironmentOutcomePayload
{
    internal EnvironmentOutcomePayload(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long ownerRevision,
        in EnvironmentFixedBuffer16<GameplayOutcomeParticipant> participants,
        in EnvironmentFixedBuffer16<GameplayOutcomeMetric> metrics,
        in EnvironmentFixedBuffer16<GameplayOutcomeSubjectLink> subjects,
        in EnvironmentFixedBuffer16<GameplayOutcomeTagId> tags,
        in EnvironmentFixedBuffer16<GameplayOutcomeProvenanceReference> provenance,
        in EnvironmentFixedBuffer16<GameplayOutcomeFact> facts,
        in GameplayLocationReference location,
        in GameplayOutcomeCausation causation)
    {
        ResultKey = resultKey;
        OutcomeTypeId = outcomeTypeId;
        AbsoluteDay = absoluteDay;
        Status = status;
        OwnerRevision = ownerRevision;
        Participants = participants;
        Metrics = metrics;
        Subjects = subjects;
        Tags = tags;
        Provenance = provenance;
        Facts = facts;
        Location = location;
        Causation = causation;
    }

    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public long OwnerRevision { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeParticipant> Participants { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeMetric> Metrics { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeSubjectLink> Subjects { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeTagId> Tags { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeProvenanceReference> Provenance { get; }
    public EnvironmentFixedBuffer16<GameplayOutcomeFact> Facts { get; }
    public GameplayLocationReference Location { get; }
    public GameplayOutcomeCausation Causation { get; }
}

public struct EnvironmentOutcomePayloadBuilder
{
    private readonly GameplayResultKey resultKey;
    private readonly GameplayOutcomeTypeId outcomeTypeId;
    private readonly int absoluteDay;
    private readonly GameplayOutcomeStatus status;
    private readonly long ownerRevision;
    private EnvironmentFixedBuffer16<GameplayOutcomeParticipant> participants;
    private EnvironmentFixedBuffer16<GameplayOutcomeMetric> metrics;
    private EnvironmentFixedBuffer16<GameplayOutcomeSubjectLink> subjects;
    private EnvironmentFixedBuffer16<GameplayOutcomeTagId> tags;
    private EnvironmentFixedBuffer16<GameplayOutcomeProvenanceReference> provenance;
    private EnvironmentFixedBuffer16<GameplayOutcomeFact> facts;
    private GameplayLocationReference location;
    private GameplayOutcomeCausation causation;

    public EnvironmentOutcomePayloadBuilder(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long ownerRevision)
    {
        this.resultKey = resultKey;
        this.outcomeTypeId = outcomeTypeId;
        this.absoluteDay = absoluteDay;
        this.status = status;
        this.ownerRevision = ownerRevision;
        participants = default;
        metrics = default;
        subjects = default;
        tags = default;
        provenance = default;
        facts = default;
        location = default;
        causation = default;
    }

    public bool AddParticipant(in GameplayOutcomeParticipant value) => participants.TryAdd(value);
    public bool AddMetric(in GameplayOutcomeMetric value) => metrics.TryAdd(value);
    public bool AddSubject(in GameplayOutcomeSubjectLink value) => subjects.TryAdd(value);
    public bool AddTag(GameplayOutcomeTagId value) => tags.TryAdd(value);
    public bool AddProvenance(in GameplayOutcomeProvenanceReference value) => provenance.TryAdd(value);
    public bool AddFact(in GameplayOutcomeFact value) => facts.TryAdd(value);
    public void SetLocation(in GameplayLocationReference value) => location = value;
    public void SetCausation(in GameplayOutcomeCausation value) => causation = value;

    public EnvironmentOutcomePayload Build() => new(
        resultKey,
        outcomeTypeId,
        absoluteDay,
        status,
        ownerRevision,
        participants,
        metrics,
        subjects,
        tags,
        provenance,
        facts,
        location,
        causation);
}

public interface IEnvironmentOutcomeReceipt
{
    EnvironmentOutcomePayload Payload { get; }
}

public readonly struct CertifiedSeedCompletionOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public CertifiedSeedCompletionOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct CropPlanGameplayOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public CropPlanGameplayOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct CropIrrigationSupplyOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public CropIrrigationSupplyOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct EnvironmentalFireIgnitionOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public EnvironmentalFireIgnitionOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct EnvironmentalFireSuppressionOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public EnvironmentalFireSuppressionOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct EnvironmentalFireDamageOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public EnvironmentalFireDamageOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct EnvironmentalFireFuelOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public EnvironmentalFireFuelOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct EnvironmentalFireWaterOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public EnvironmentalFireWaterOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct PopulationDiseaseExposureOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public PopulationDiseaseExposureOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct ProcessAccidentOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public ProcessAccidentOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct RoomConditionOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public RoomConditionOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct RoomEnvironmentExperienceOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public RoomEnvironmentExperienceOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct SpeciesIncidentOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public SpeciesIncidentOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public readonly struct HarpyGaleRelocationOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public HarpyGaleRelocationOutcomeReceipt(EnvironmentOutcomePayload payload) => Payload = payload;
    public EnvironmentOutcomePayload Payload { get; }
}

public static class EnvironmentOutcomeSnapshots
{
    public static KoreanNameSnapshot Name(string stableId, string displayText)
    {
        string canonicalId = string.IsNullOrWhiteSpace(stableId)
            ? throw new ArgumentException("A stable identity is required.", nameof(stableId))
            : stableId.Trim();
        string display = string.IsNullOrWhiteSpace(displayText)
            ? throw new ArgumentException(
                "An immutable display snapshot is required; persistent IDs are not display names.",
                nameof(displayText))
            : displayText.Trim();
        string revision = "environment-name-v1:" + canonicalId;
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }

    public static GameplayOutcomeParticipant Participant(
        GameplayEntityKindId kind,
        string id,
        GameplayRoleId role,
        string displayText) => new(
            new GameplayEntityId(kind, id),
            role,
            GameplayParticipationKind.Direct,
            true,
            Name(id, displayText));

    public static GameplayOutcomeSubjectLink Subject(
        GameplayEntityKindId kind,
        string id,
        float salience = 0.7f,
        bool pinned = false) => new(
            new GameplayEntityId(kind, id),
            Math.Clamp(salience, 0f, 1f),
            NarrativeMemoryTier.Episodic,
            pinned,
            false,
            0);
}
