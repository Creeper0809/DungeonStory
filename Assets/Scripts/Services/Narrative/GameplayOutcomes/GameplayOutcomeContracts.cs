using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

public enum GameplayOutcomeStatus
{
    Succeeded = 1,
    PartiallySucceeded = 2,
    Failed = 3,
    Blocked = 4,
    Cancelled = 5
}

public enum GameplayParticipationKind
{
    Direct = 1,
    OptionalWitness = 2,
    GroupObservation = 3
}

public enum NarrativeMemoryTier
{
    Recent = 1,
    Core = 2,
    Episodic = 3,
    Compacted = 4,
    Forgotten = 5
}

public enum GameplayOutcomeRecordLifecycle
{
    Reserved = 1,
    Prepared = 2,
    Committed = 3,
    DeliveryFaultPending = 4,
    PublishedAwaitingAcknowledgement = 5,
    PublishedAcknowledged = 6
}

public readonly struct GameplayOutcomeParticipant
{
    public GameplayOutcomeParticipant(
        GameplayEntityId entityId,
        GameplayRoleId roleId,
        GameplayParticipationKind participationKind,
        bool hasPerceptionEvidence,
        KoreanNameSnapshot displayName)
    {
        EntityId = entityId;
        RoleId = roleId;
        ParticipationKind = participationKind;
        HasPerceptionEvidence = hasPerceptionEvidence;
        DisplayName = displayName;
    }
    [Obsolete("Historical outcomes require an immutable KoreanNameSnapshot.")]
    public GameplayOutcomeParticipant(
        GameplayEntityId entityId,
        GameplayRoleId roleId,
        GameplayParticipationKind participationKind,
        bool hasPerceptionEvidence)
        : this(entityId, roleId, participationKind, hasPerceptionEvidence, default)
    {
    }
    public GameplayEntityId EntityId { get; }
    public GameplayRoleId RoleId { get; }
    public GameplayParticipationKind ParticipationKind { get; }
    public bool HasPerceptionEvidence { get; }
    public KoreanNameSnapshot DisplayName { get; }
}

public readonly struct GameplayOutcomeMetric
{
    public GameplayOutcomeMetric(
        GameplayMetricId metricId,
        double value,
        GameplayMetricUnitId unitId,
        GameplayEntityId definitionOrInstanceId)
    {
        MetricId = metricId;
        Value = value;
        UnitId = unitId;
        DefinitionOrInstanceId = definitionOrInstanceId;
    }
    public GameplayMetricId MetricId { get; }
    public double Value { get; }
    public GameplayMetricUnitId UnitId { get; }
    public GameplayEntityId DefinitionOrInstanceId { get; }
}

/// <summary>
/// Immutable, non-numeric receipt truth such as the exact title/text that was
/// authoritative at commit time. Values are bounded UTF-16 snapshots; they
/// are not model-authored narrative prose.
/// </summary>
public readonly struct GameplayOutcomeFact
{
    public GameplayOutcomeFact(GameplayOutcomeFactId factId, string value)
    {
        FactId = factId;
        Value = value ?? string.Empty;
    }
    public GameplayOutcomeFactId FactId { get; }
    public string Value { get; }
}

public readonly struct GameplayLocationReference
{
    public GameplayLocationReference(string locationId, string roomId, int x, int y)
    {
        LocationId = string.IsNullOrEmpty(locationId)
            ? string.Empty
            : GameplayOutcomeStableIdSyntax.Require(locationId, nameof(locationId));
        RoomId = string.IsNullOrEmpty(roomId)
            ? string.Empty
            : GameplayOutcomeStableIdSyntax.Require(roomId, nameof(roomId));
        X = x;
        Y = y;
    }
    public string LocationId { get; }
    public string RoomId { get; }
    public int X { get; }
    public int Y { get; }
    public bool HasLocation => !string.IsNullOrEmpty(LocationId) || !string.IsNullOrEmpty(RoomId);
}

public readonly struct GameplayOutcomeCausation
{
    public GameplayOutcomeCausation(
        GameplayOutcomeId parentOutcomeId,
        GameplayOperationId rootOperationId,
        string relationId)
    {
        ParentOutcomeId = parentOutcomeId;
        RootOperationId = rootOperationId;
        RelationId = string.IsNullOrEmpty(relationId)
            ? string.Empty
            : GameplayOutcomeStableIdSyntax.Require(relationId, nameof(relationId));
    }
    public GameplayOutcomeId ParentOutcomeId { get; }
    public GameplayOperationId RootOperationId { get; }
    public string RelationId { get; }
    public bool HasParent => ParentOutcomeId.IsValid;
}

public readonly struct NarrativeEvidenceReference : IEquatable<NarrativeEvidenceReference>
{
    public NarrativeEvidenceReference(string anchorTypeId, string anchorId)
    {
        AnchorTypeId = GameplayOutcomeStableIdSyntax.Require(anchorTypeId, nameof(anchorTypeId));
        AnchorId = GameplayOutcomeStableIdSyntax.Require(anchorId, nameof(anchorId));
    }
    public string AnchorTypeId { get; }
    public string AnchorId { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(AnchorTypeId)
        && GameplayOutcomeStableIdSyntax.IsValid(AnchorId);
    public bool Equals(NarrativeEvidenceReference other) =>
        string.Equals(AnchorTypeId, other.AnchorTypeId, StringComparison.Ordinal)
        && string.Equals(AnchorId, other.AnchorId, StringComparison.Ordinal);
    public override bool Equals(object obj) => obj is NarrativeEvidenceReference other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        AnchorTypeId == null ? 0 : StringComparer.Ordinal.GetHashCode(AnchorTypeId),
        AnchorId == null ? 0 : StringComparer.Ordinal.GetHashCode(AnchorId));
}

/// <summary>
/// Immutable source/audit identity captured from a committed receipt. Unlike an
/// evidence anchor this row never changes retention or memory tier.
/// </summary>
public readonly struct GameplayOutcomeProvenanceReference : IEquatable<GameplayOutcomeProvenanceReference>
{
    public GameplayOutcomeProvenanceReference(string kindId, string value)
    {
        KindId = GameplayOutcomeStableIdSyntax.Require(kindId, nameof(kindId));
        Value = GameplayOutcomeStableIdSyntax.Require(value, nameof(value));
    }
    public string KindId { get; }
    public string Value { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(KindId)
        && GameplayOutcomeStableIdSyntax.IsValid(Value);
    public bool Equals(GameplayOutcomeProvenanceReference other) =>
        string.Equals(KindId, other.KindId, StringComparison.Ordinal)
        && string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is GameplayOutcomeProvenanceReference other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        KindId == null ? 0 : StringComparer.Ordinal.GetHashCode(KindId),
        Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value));
}

public readonly struct GameplayOutcomeSubjectLink
{
    public GameplayOutcomeSubjectLink(
        GameplayEntityId subjectId,
        float salience,
        NarrativeMemoryTier tier,
        bool isPinned,
        bool isOptionalWitness,
        int anchorRevision,
        int nextEvaluationDay = 0,
        int influenceUseCount = 0,
        int influenceRevision = 0)
    {
        SubjectId = subjectId;
        Salience = salience;
        Tier = tier;
        IsPinned = isPinned;
        IsOptionalWitness = isOptionalWitness;
        AnchorRevision = anchorRevision;
        NextEvaluationDay = nextEvaluationDay;
        InfluenceUseCount = influenceUseCount;
        InfluenceRevision = influenceRevision;
    }
    public GameplayEntityId SubjectId { get; }
    public float Salience { get; }
    public NarrativeMemoryTier Tier { get; }
    public bool IsPinned { get; }
    public bool IsOptionalWitness { get; }
    public int AnchorRevision { get; }
    public int NextEvaluationDay { get; }
    public int InfluenceUseCount { get; }
    public int InfluenceRevision { get; }
}

public readonly struct OutcomeWriteRequirements
{
    public OutcomeWriteRequirements(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long worldEpoch,
        long ownerRevision,
        int participantCount,
        int metricCount,
        int subjectCount,
        int tagCount,
        int anchorCount,
        int provenanceCount = 0,
        int factCount = 0)
    {
        ResultKey = resultKey;
        OutcomeTypeId = outcomeTypeId;
        AbsoluteDay = absoluteDay;
        Status = status;
        WorldEpoch = worldEpoch;
        OwnerRevision = ownerRevision;
        ParticipantCount = participantCount;
        MetricCount = metricCount;
        SubjectCount = subjectCount;
        TagCount = tagCount;
        AnchorCount = anchorCount;
        ProvenanceCount = provenanceCount;
        FactCount = factCount;
    }
    public GameplayResultKey ResultKey { get; }
    public GameplayOperationId OperationId => ResultKey.OperationId;
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public long WorldEpoch { get; }
    public long OwnerRevision { get; }
    public int ParticipantCount { get; }
    public int MetricCount { get; }
    public int SubjectCount { get; }
    public int TagCount { get; }
    public int AnchorCount { get; }
    public int ProvenanceCount { get; }
    public int FactCount { get; }
    public bool HasNonNegativeCounts => ParticipantCount >= 0
        && MetricCount >= 0
        && SubjectCount >= 0
        && TagCount >= 0
        && AnchorCount >= 0
        && ProvenanceCount >= 0
        && FactCount >= 0;
}

public enum OutcomePrepareCode
{
    Prepared = 1,
    CapacityDeferred = 2,
    InvalidReceipt = 3,
    AdapterMissing = 4,
    DescriptorMissing = 5,
    DuplicateResult = 6,
    ConflictingResult = 7,
    ReservationInvalid = 8,
    AdapterWriteFailed = 9,
    AlreadyCommitted = 10,
    AlreadyPublished = 11,
    AlreadyTerminal = 12
}

public enum GameplayOutcomeReplayState
{
    Reserved = 1,
    Prepared = 2,
    Committed = 3,
    DeliveryFaultPending = 4,
    PublishedAwaitingAcknowledgement = 5,
    PublishedAcknowledged = 6,
    Compacted = 7,
    Forgotten = 8
}

public readonly struct GameplayOutcomeReplayIdentity
{
    public GameplayOutcomeReplayIdentity(
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        GameplayOutcomeReplayState state,
        string canonicalPayloadHash)
    {
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        State = state;
        CanonicalPayloadHash = canonicalPayloadHash ?? string.Empty;
    }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public GameplayOutcomeReplayState State { get; }
    public string CanonicalPayloadHash { get; }
    public bool HasCanonicalPayloadHash => CanonicalPayloadHash.Length == 64;
}

public readonly struct OutcomePrepareResult
{
    public OutcomePrepareResult(
        OutcomePrepareCode code,
        string detailCode,
        GameplayOutcomeReplayIdentity existing = default)
    {
        Code = code;
        DetailCode = detailCode ?? string.Empty;
        Existing = existing;
    }
    public OutcomePrepareCode Code { get; }
    public string DetailCode { get; }
    public GameplayOutcomeReplayIdentity Existing { get; }
    public bool Success => Code == OutcomePrepareCode.Prepared;
    public static OutcomePrepareResult Prepared() => new OutcomePrepareResult(OutcomePrepareCode.Prepared, string.Empty);
}

public enum OutcomeCommitCode
{
    Committed = 1,
    AlreadyCommitted = 2,
    InvalidPreparedToken = 3,
    OwnerRevisionMismatch = 4,
    WorldEpochMismatch = 5
}

public readonly struct OutcomeCommitResult
{
    public OutcomeCommitResult(OutcomeCommitCode code, string detailCode)
    {
        Code = code;
        DetailCode = detailCode ?? string.Empty;
    }
    public OutcomeCommitCode Code { get; }
    public string DetailCode { get; }
    public bool Success => Code == OutcomeCommitCode.Committed || Code == OutcomeCommitCode.AlreadyCommitted;
}

public enum OutcomeDeliveryCode
{
    Published = 1,
    AlreadyPublished = 2,
    PendingDeliveryFault = 3,
    InvalidCommittedToken = 4,
    WorldEpochMismatch = 5,
    DescriptorRejected = 6
}

public readonly struct OutcomeDeliveryResult
{
    public OutcomeDeliveryResult(OutcomeDeliveryCode code, GameplayOutcomeId outcomeId, string detailCode)
    {
        Code = code;
        OutcomeId = outcomeId;
        DetailCode = detailCode ?? string.Empty;
    }
    public OutcomeDeliveryCode Code { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public string DetailCode { get; }
    public bool Published => Code == OutcomeDeliveryCode.Published || Code == OutcomeDeliveryCode.AlreadyPublished;
}

public enum OutcomeAcknowledgeCode
{
    Acknowledged = 1,
    AlreadyAcknowledged = 2,
    NotPublished = 3,
    InvalidCommittedToken = 4,
    RetentionPromotionDeferred = 5
}

public readonly struct OutcomeAcknowledgeResult
{
    public OutcomeAcknowledgeResult(OutcomeAcknowledgeCode code) => Code = code;
    public OutcomeAcknowledgeCode Code { get; }
    public bool Success => Code == OutcomeAcknowledgeCode.Acknowledged
        || Code == OutcomeAcknowledgeCode.AlreadyAcknowledged;
}

public readonly struct PreparedOutcomeReservation
{
    internal PreparedOutcomeReservation(int pageIndex, int pageGeneration, long worldEpoch, GameplayResultKey resultKey)
    {
        PageIndex = pageIndex;
        PageGeneration = pageGeneration;
        WorldEpoch = worldEpoch;
        ResultKey = resultKey;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    public long WorldEpoch { get; }
    public GameplayResultKey ResultKey { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0 && ResultKey.IsValid;
}

public readonly struct PreparedOutcomeToken
{
    internal PreparedOutcomeToken(int pageIndex, int pageGeneration, long worldEpoch, long ownerRevision, GameplayResultKey resultKey, GameplayOutcomeId outcomeId)
    {
        PageIndex = pageIndex;
        PageGeneration = pageGeneration;
        WorldEpoch = worldEpoch;
        OwnerRevision = ownerRevision;
        ResultKey = resultKey;
        OutcomeId = outcomeId;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    public long WorldEpoch { get; }
    public long OwnerRevision { get; }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0 && ResultKey.IsValid && OutcomeId.IsValid;
}

public readonly struct CommittedOutcomeToken
{
    internal CommittedOutcomeToken(int pageIndex, int pageGeneration, long worldEpoch, GameplayResultKey resultKey, GameplayOutcomeId outcomeId)
    {
        PageIndex = pageIndex;
        PageGeneration = pageGeneration;
        WorldEpoch = worldEpoch;
        ResultKey = resultKey;
        OutcomeId = outcomeId;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    public long WorldEpoch { get; }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0 && ResultKey.IsValid && OutcomeId.IsValid;
}

public interface IGameplayOutcomeRecorder
{
    OutcomePrepareResult TryPrepare<TReceipt>(in TReceipt receipt, out PreparedOutcomeToken prepared);
    OutcomePrepareResult TryReserve(in OutcomeWriteRequirements requirements, out PreparedOutcomeReservation reservation);
    OutcomePrepareResult TryWriteReserved<TReceipt>(in TReceipt receipt, in PreparedOutcomeReservation reservation, out PreparedOutcomeToken prepared);
    void CancelReservation(in PreparedOutcomeReservation reservation);
    void CancelPrepared(in PreparedOutcomeToken prepared);
    OutcomeCommitResult CommitPrepared(in PreparedOutcomeToken prepared, long expectedOwnerRevision, out CommittedOutcomeToken committed);
    OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed);
    OutcomeAcknowledgeResult Acknowledge(in CommittedOutcomeToken committed);
    int RetryPendingDeliveries(int maximumCount);
}

public readonly struct GameplayOutcomePublishedRangeEvent
{
    public GameplayOutcomePublishedRangeEvent(long firstSequence, long lastSequence, long ledgerRevision)
    {
        FirstSequence = firstSequence;
        LastSequence = lastSequence;
        LedgerRevision = ledgerRevision;
    }
    public long FirstSequence { get; }
    public long LastSequence { get; }
    public long LedgerRevision { get; }
}

public readonly struct GameplayOutcomeOutboxSnapshot
{
    public GameplayOutcomeOutboxSnapshot(
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        GameplayOutcomeRecordLifecycle lifecycle,
        int deliveryFaultCount,
        string lastFaultCode)
    {
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        Lifecycle = lifecycle;
        DeliveryFaultCount = deliveryFaultCount;
        LastFaultCode = lastFaultCode ?? string.Empty;
    }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public GameplayOutcomeRecordLifecycle Lifecycle { get; }
    public int DeliveryFaultCount { get; }
    public string LastFaultCode { get; }
}

public readonly struct GameplayOutcomeLedgerDiagnostics
{
    public GameplayOutcomeLedgerDiagnostics(
        long worldEpoch,
        long revision,
        long nextSequence,
        int freeSmallPages,
        int freeLargePages,
        int reservedCount,
        int pendingDeliveryCount,
        int publishedCount,
        int compactedCount,
        int forgottenCount,
        int notificationFaultCount,
        int knownResultKeyCount,
        int knownResultKeyMaximum,
        int knownResultKeyHighWater,
        int activeAnchorReservationCount,
        long capacityDeferredCount,
        int consecutiveCapacityDeferredCount,
        int capacityDeferredStreakHighWater,
        int exactPageUseHighWater,
        int detachedExactCount,
        int tombstoneCount,
        int activeInfluenceReservationCount,
        int freeRetainedSmallPages,
        int freeRetainedLargePages,
        int retentionPromotionFaultCount)
    {
        WorldEpoch = worldEpoch;
        Revision = revision;
        NextSequence = nextSequence;
        FreeSmallPages = freeSmallPages;
        FreeLargePages = freeLargePages;
        ReservedCount = reservedCount;
        PendingDeliveryCount = pendingDeliveryCount;
        PublishedCount = publishedCount;
        CompactedCount = compactedCount;
        ForgottenCount = forgottenCount;
        NotificationFaultCount = notificationFaultCount;
        KnownResultKeyCount = knownResultKeyCount;
        KnownResultKeyMaximum = knownResultKeyMaximum;
        KnownResultKeyHighWater = knownResultKeyHighWater;
        ActiveAnchorReservationCount = activeAnchorReservationCount;
        CapacityDeferredCount = capacityDeferredCount;
        ConsecutiveCapacityDeferredCount = consecutiveCapacityDeferredCount;
        CapacityDeferredStreakHighWater = capacityDeferredStreakHighWater;
        ExactPageUseHighWater = exactPageUseHighWater;
        DetachedExactCount = detachedExactCount;
        TombstoneCount = tombstoneCount;
        ActiveInfluenceReservationCount = activeInfluenceReservationCount;
        FreeRetainedSmallPages = freeRetainedSmallPages;
        FreeRetainedLargePages = freeRetainedLargePages;
        RetentionPromotionFaultCount = retentionPromotionFaultCount;
    }
    public long WorldEpoch { get; }
    public long Revision { get; }
    public long NextSequence { get; }
    public int FreeSmallPages { get; }
    public int FreeLargePages { get; }
    public int ReservedCount { get; }
    public int PendingDeliveryCount { get; }
    public int PublishedCount { get; }
    public int CompactedCount { get; }
    public int ForgottenCount { get; }
    public int NotificationFaultCount { get; }
    /// <summary>Current exact duplicate-prevention identities retained for this run.</summary>
    public int KnownResultKeyCount { get; }
    /// <summary>Configured hard bound; saturation fails capacity-first and keys are never evicted silently.</summary>
    public int KnownResultKeyMaximum { get; }
    public int KnownResultKeyHighWater { get; }
    public int ActiveAnchorReservationCount { get; }
    public long CapacityDeferredCount { get; }
    public int ConsecutiveCapacityDeferredCount { get; }
    public int CapacityDeferredStreakHighWater { get; }
    public int ExactPageUseHighWater { get; }
    /// <summary>Live cold exact rows. Terminal compaction/forgetting returns these slots.</summary>
    public int DetachedExactCount { get; }
    /// <summary>Terminal replay identities retained for this run; they are never silently evicted.</summary>
    public int TombstoneCount { get; }
    public int ActiveInfluenceReservationCount { get; }
    public int FreeRetainedSmallPages { get; }
    public int FreeRetainedLargePages { get; }
    public int RetentionPromotionFaultCount { get; }
}

public interface IGameplayOutcomeDiagnosticsQuery
{
    GameplayOutcomeLedgerDiagnostics GetDiagnostics();
    IReadOnlyList<GameplayOutcomeOutboxSnapshot> GetOutboxSnapshot();
    bool TryGetResultIdentity(
        GameplayResultKey resultKey,
        out GameplayOutcomeReplayIdentity identity);
}
