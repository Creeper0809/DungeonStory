using System;
using DungeonStory.Narrative.Korean;

public enum TradeInventoryOutcomeKind
{
    FacilityCrime = 1,
    FacilityRevenue = 2,
    FacilityShopPurchased = 3,
    FacilityStockConsumed = 4,
    FacilityVisit = 5,
    GrandProjectPhysicalInput = 6,
    GuestRequestDelivery = 7,
    ItemExtraction = 8,
    MetaUpgradePurchased = 9,
    PackagedLotTareOutput = 10,
    PhysicalItemBatchDisposition = 11,
    PhysicalItemDisposition = 12,
    PhysicalItemExactSourcePublication = 13,
    PhysicalItemRelocation = 14,
    PhysicalItemSourcePublication = 15,
    PhysicalItemTransform = 16,
    RegionalSupplyDeliveryTransfer = 17,
    ReservedRetailStockTransfer = 18,
    ResourceStockPolicySaleTransfer = 19,
    StockSupply = 20,
    WarehouseMassAdmission = 21,
    WorldResourceRenewableDebit = 22,
    WastePolicyCommand = 23
}

public static class TradeInventoryOutcomeIds
{
    public const string ProducerPrefix = "trade-inventory.";
    public static readonly GameplayOutcomeTypeId Result = new("trade-inventory.result");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId ItemDefinitionKind = new("item-definition");
    public static readonly GameplayEntityKindId ItemInstanceKind = new("item-instance");
    public static readonly GameplayEntityKindId ItemStackKind = new("item-stack");
    public static readonly GameplayEntityKindId ContractKind = new("contract");
    public static readonly GameplayEntityKindId ProjectKind = new("project");
    public static readonly GameplayEntityKindId WarehouseKind = new("warehouse");
    public static readonly GameplayEntityKindId ResourcePatchKind = new("resource-patch");
    public static readonly GameplayEntityKindId UpgradeKind = new("meta-upgrade");
    public static readonly GameplayEntityKindId WastePolicyDefinitionKind =
        new("waste-policy-definition");
    public static readonly GameplayEntityKindId WastePolicyInstanceKind =
        new("waste-policy-instance");
    public static readonly GameplayEntityKindId OutcomeKind = new("trade-inventory-kind");
    public static readonly GameplayEntityKindId OperationKind = new("operation");

    public static readonly GameplayRoleId ActorRole = new("actor");
    public static readonly GameplayRoleId CustomerRole = new("customer");
    public static readonly GameplayRoleId FacilityRole = new("facility");
    public static readonly GameplayRoleId ItemRole = new("item");
    public static readonly GameplayRoleId SourceRole = new("source");
    public static readonly GameplayRoleId DestinationRole = new("destination");
    public static readonly GameplayRoleId ContractRole = new("contract");
    public static readonly GameplayRoleId ProjectRole = new("project");
    public static readonly GameplayRoleId WarehouseRole = new("warehouse");
    public static readonly GameplayRoleId ResourceRole = new("resource");
    public static readonly GameplayRoleId UpgradeRole = new("upgrade");
    public static readonly GameplayRoleId BeneficiaryRole = new("beneficiary");
    public static readonly GameplayRoleId PolicyRole = new("policy");

    public static readonly GameplayMetricId KindMetric = new("result.kind");
    public static readonly GameplayMetricId QuantityMetric = new("item.quantity");
    public static readonly GameplayMetricId InputQuantityMetric = new("item.input-quantity");
    public static readonly GameplayMetricId OutputQuantityMetric = new("item.output-quantity");
    public static readonly GameplayMetricId MassMetric = new("item.mass-grams");
    public static readonly GameplayMetricId InputMassMetric = new("item.input-mass-grams");
    public static readonly GameplayMetricId OutputMassMetric = new("item.output-mass-grams");
    public static readonly GameplayMetricId LossMassMetric = new("item.loss-mass-grams");
    public static readonly GameplayMetricId TareDestroyedMassMetric = new("item.tare-destroyed-mass-grams");
    public static readonly GameplayMetricId GoldMetric = new("economy.gold");
    public static readonly GameplayMetricId CostMetric = new("economy.cost");
    public static readonly GameplayMetricId RevenueMetric = new("economy.revenue");
    public static readonly GameplayMetricId LossValueMetric = new("economy.loss-value");
    public static readonly GameplayMetricId BeforeValueMetric = new("state.before-value");
    public static readonly GameplayMetricId AfterValueMetric = new("state.after-value");
    public static readonly GameplayMetricId CategoryMetric = new("stock.category");
    public static readonly GameplayMetricId DispositionMetric = new("item.disposition");
    public static readonly GameplayMetricId ResultRevisionMetric = new("result.owner-revision");
    public static readonly GameplayMetricId SourceXMetric = new("location.source-x");
    public static readonly GameplayMetricId SourceYMetric = new("location.source-y");
    public static readonly GameplayMetricId DestinationXMetric =
        new("location.destination-x");
    public static readonly GameplayMetricId DestinationYMetric =
        new("location.destination-y");
    public static readonly GameplayMetricId BeforeDispositionMetric =
        new("policy.before-disposition");
    public static readonly GameplayMetricId AfterDispositionMetric =
        new("policy.after-disposition");
    public static readonly GameplayMetricId BeforeEnabledMetric =
        new("policy.before-enabled");
    public static readonly GameplayMetricId AfterEnabledMetric =
        new("policy.after-enabled");
    public static readonly GameplayMetricId BeforePercentMetric =
        new("policy.before-percent");
    public static readonly GameplayMetricId AfterPercentMetric =
        new("policy.after-percent");

    public static readonly GameplayOutcomeFactId ReasonCodeFact =
        new("physical.reason-code");
    public static readonly GameplayOutcomeFactId RequestFingerprintFact =
        new("physical.request-fingerprint");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId GoldUnit = new("gold");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId CellUnit = new("cell");
    public static readonly GameplayMetricUnitId PercentUnit = new("percent");

    public static readonly GameplayOutcomeTagId TradeInventoryTag = new("trade-inventory");
    public static readonly GameplayOutcomeTagId FacilityTag = new("facility-service");
    public static readonly GameplayOutcomeTagId PhysicalTag = new("physical-item");
    public static readonly GameplayOutcomeTagId EconomyTag = new("economy");
    public static readonly GameplayOutcomeTagId ProgressionTag = new("progression");
    public static readonly GameplayOutcomeTagId WorldResourceTag = new("world-resource");

    public static readonly GameplayOutcomeProvenanceReference RuntimeReceiptProvenance =
        new("source-contract", "authoritative-runtime-receipt");

    public static string Slug(TradeInventoryOutcomeKind kind) =>
        TradeInventoryOutcomeDefinitions.Get(kind).Slug;

    public static string KoreanLabel(TradeInventoryOutcomeKind kind) =>
        TradeInventoryOutcomeDefinitions.Get(kind).KoreanLabel;
}

public readonly struct TradeInventoryOutcomeReceipt
{
    private readonly GameplayOutcomeParticipant[] participants;
    private readonly GameplayOutcomeMetric[] metrics;
    private readonly GameplayOutcomeSubjectLink[] subjects;
    private readonly GameplayOutcomeTagId[] tags;
    private readonly GameplayOutcomeProvenanceReference[] provenance;
    private readonly GameplayOutcomeFact[] facts;
    private readonly int participantCount;
    private readonly int metricCount;
    private readonly int subjectCount;
    private readonly int tagCount;
    private readonly int provenanceCount;
    private readonly int factCount;

    public TradeInventoryOutcomeReceipt(
        TradeInventoryOutcomeKind kind,
        string operationId,
        long resultSequence,
        long ownerRevision,
        int absoluteDay,
        GameplayOutcomeStatus status,
        GameplayOutcomeParticipant[] participants,
        GameplayOutcomeMetric[] metrics,
        GameplayOutcomeSubjectLink[] subjects,
        GameplayOutcomeTagId[] tags,
        GameplayOutcomeProvenanceReference[] provenance,
        GameplayOutcomeFact[] facts,
        GameplayLocationReference location = default)
        : this(
            kind,
            operationId,
            resultSequence,
            ownerRevision,
            absoluteDay,
            status,
            Clone(participants),
            participants?.Length ?? 0,
            Clone(metrics),
            metrics?.Length ?? 0,
            Clone(subjects),
            subjects?.Length ?? 0,
            Clone(tags),
            tags?.Length ?? 0,
            Clone(provenance),
            provenance?.Length ?? 0,
            Clone(facts),
            facts?.Length ?? 0,
            location)
    {
    }

    internal TradeInventoryOutcomeReceipt(
        TradeInventoryOutcomeKind kind,
        string operationId,
        long resultSequence,
        long ownerRevision,
        int absoluteDay,
        GameplayOutcomeStatus status,
        GameplayOutcomeParticipant[] participants,
        int participantCount,
        GameplayOutcomeMetric[] metrics,
        int metricCount,
        GameplayOutcomeSubjectLink[] subjects,
        int subjectCount,
        GameplayOutcomeTagId[] tags,
        int tagCount,
        GameplayOutcomeProvenanceReference[] provenance,
        int provenanceCount,
        GameplayOutcomeFact[] facts,
        int factCount,
        GameplayLocationReference location = default)
    {
        if ((int)kind < 1
            || (int)kind > TradeInventoryOutcomeDefinitions.All.Count)
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        OperationId = new GameplayOperationId(operationId);
        if (resultSequence < 0L || ownerRevision < 0L || absoluteDay < 0
            || (int)status < (int)GameplayOutcomeStatus.Succeeded
            || (int)status > (int)GameplayOutcomeStatus.Cancelled)
            throw new ArgumentException("Trade/inventory outcome scalar values are invalid.");
        ResultSequence = resultSequence;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
        Status = status;
        this.participants = participants ?? Array.Empty<GameplayOutcomeParticipant>();
        this.metrics = metrics ?? Array.Empty<GameplayOutcomeMetric>();
        this.subjects = subjects ?? Array.Empty<GameplayOutcomeSubjectLink>();
        this.tags = tags ?? Array.Empty<GameplayOutcomeTagId>();
        this.provenance = provenance
            ?? Array.Empty<GameplayOutcomeProvenanceReference>();
        this.facts = facts ?? Array.Empty<GameplayOutcomeFact>();
        this.participantCount = RequireCount(
            participantCount,
            this.participants.Length,
            nameof(participantCount));
        this.metricCount = RequireCount(
            metricCount,
            this.metrics.Length,
            nameof(metricCount));
        this.subjectCount = RequireCount(
            subjectCount,
            this.subjects.Length,
            nameof(subjectCount));
        this.tagCount = RequireCount(tagCount, this.tags.Length, nameof(tagCount));
        this.provenanceCount = RequireCount(
            provenanceCount,
            this.provenance.Length,
            nameof(provenanceCount));
        this.factCount = RequireCount(factCount, this.facts.Length, nameof(factCount));
        Location = location;
    }

    public TradeInventoryOutcomeKind Kind { get; }
    public GameplayOperationId OperationId { get; }
    public long ResultSequence { get; }
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public GameplayOutcomeParticipant[] Participants => participants
        ?? Array.Empty<GameplayOutcomeParticipant>();
    public GameplayOutcomeMetric[] Metrics => metrics
        ?? Array.Empty<GameplayOutcomeMetric>();
    public GameplayOutcomeSubjectLink[] Subjects => subjects
        ?? Array.Empty<GameplayOutcomeSubjectLink>();
    public GameplayOutcomeTagId[] Tags => tags
        ?? Array.Empty<GameplayOutcomeTagId>();
    public GameplayOutcomeProvenanceReference[] Provenance => provenance
        ?? Array.Empty<GameplayOutcomeProvenanceReference>();
    public GameplayOutcomeFact[] Facts => facts
        ?? Array.Empty<GameplayOutcomeFact>();
    public int ParticipantCount => participantCount;
    public int MetricCount => metricCount;
    public int SubjectCount => subjectCount;
    public int TagCount => tagCount;
    public int ProvenanceCount => provenanceCount;
    public int FactCount => factCount;
    public GameplayOutcomeParticipant GetParticipant(int index) =>
        Participants[RequireIndex(index, ParticipantCount)];
    public GameplayOutcomeMetric GetMetric(int index) =>
        Metrics[RequireIndex(index, MetricCount)];
    public GameplayOutcomeSubjectLink GetSubject(int index) =>
        Subjects[RequireIndex(index, SubjectCount)];
    public GameplayOutcomeTagId GetTag(int index) =>
        Tags[RequireIndex(index, TagCount)];
    public GameplayOutcomeProvenanceReference GetProvenance(int index) =>
        Provenance[RequireIndex(index, ProvenanceCount)];
    public GameplayOutcomeFact GetFact(int index) =>
        Facts[RequireIndex(index, FactCount)];
    public GameplayLocationReference Location { get; }
    public GameplayResultKey ResultKey => new(
        TradeInventoryOutcomeDefinitions.Get(Kind).ProducerId,
        OperationId,
        ResultSequence,
        0);

    private static int RequireCount(int count, int capacity, string name)
    {
        if (count < 0 || count > capacity)
            throw new ArgumentOutOfRangeException(name);
        return count;
    }

    private static int RequireIndex(int index, int count)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }

    private static T[] Clone<T>(T[] values) => values == null
        ? Array.Empty<T>()
        : (T[])values.Clone();
}

public readonly struct PreparedTradeInventoryOutcome
{
    internal PreparedTradeInventoryOutcome(
        PreparedOutcomeToken token,
        GameplayResultKey resultKey,
        bool canonicalReplay)
    {
        Token = token;
        ResultKey = resultKey;
        IsCanonicalReplay = canonicalReplay;
    }

    internal PreparedOutcomeToken Token { get; }
    public GameplayResultKey ResultKey { get; }
    public bool IsCanonicalReplay { get; }
    public bool IsValid => IsCanonicalReplay ? ResultKey.IsValid : Token.IsValid;
}

public static class TradeInventoryOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(
        string displayText,
        string revision)
    {
        string display = displayText?.Trim() ?? string.Empty;
        string snapshotRevision = revision?.Trim() ?? string.Empty;
        if (display.Length == 0 || !GameplayOutcomeStableIdSyntax.IsValid(snapshotRevision))
            throw new ArgumentException("An immutable display text and stable revision are required.");
        return new KoreanNameSnapshot(
            display,
            snapshotRevision,
            KoreanPronunciationHint.AutoHangulDisplay(),
            "ko-KR");
    }
}
