using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

public readonly struct OutcomeCursor
{
    public OutcomeCursor(long beforeSequenceExclusive, int limit)
        : this(beforeSequenceExclusive, 0, string.Empty, false, limit)
    {
    }

    internal OutcomeCursor(
        long sequence,
        int kind,
        string stableId,
        bool hasTieBreak,
        int limit)
    {
        BeforeSequenceExclusive = sequence <= 0L ? long.MaxValue : sequence;
        Kind = kind;
        StableId = stableId ?? string.Empty;
        HasTieBreak = hasTieBreak;
        Limit = limit <= 0 ? 50 : Math.Min(limit, 200);
    }
    public long BeforeSequenceExclusive { get; }
    internal int Kind { get; }
    internal string StableId { get; }
    internal bool HasTieBreak { get; }
    public int Limit { get; }
    public static OutcomeCursor FirstPage(int limit = 50) => new OutcomeCursor(long.MaxValue, limit);
}

public readonly struct OutcomeFilter
{
    public OutcomeFilter(
        GameplayOutcomeTypeId outcomeTypeId,
        GameplayOutcomeTagId tagId,
        GameplayOutcomeStatus? status,
        int minimumDay,
        int maximumDay,
        bool includeCompacted)
    {
        OutcomeTypeId = outcomeTypeId;
        TagId = tagId;
        Status = status;
        MinimumDay = minimumDay;
        MaximumDay = maximumDay <= 0 ? int.MaxValue : maximumDay;
        IncludeCompacted = includeCompacted;
    }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public GameplayOutcomeTagId TagId { get; }
    public GameplayOutcomeStatus? Status { get; }
    public int MinimumDay { get; }
    public int MaximumDay { get; }
    public bool IncludeCompacted { get; }
    public static OutcomeFilter All => new OutcomeFilter(default, default, null, int.MinValue, int.MaxValue, true);
}

public readonly struct NarrativeEvidenceQuery
{
    public NarrativeEvidenceQuery(
        GameplayEntityId subjectId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted,
        GameplayOutcomeTypeId outcomeTypeId)
    {
        SubjectId = subjectId;
        MaximumCount = maximumCount <= 0 ? 20 : Math.Min(maximumCount, 100);
        MinimumSalience = minimumSalience < 0f ? 0f : minimumSalience;
        IncludeCompacted = includeCompacted;
        OutcomeTypeId = outcomeTypeId;
    }
    public GameplayEntityId SubjectId { get; }
    public int MaximumCount { get; }
    public float MinimumSalience { get; }
    public bool IncludeCompacted { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
}

[Serializable]
public sealed class GameplayOutcomeParticipantSnapshot
{
    public string entityKindId = string.Empty;
    public string entityId = string.Empty;
    public string roleId = string.Empty;
    public GameplayParticipationKind participationKind;
    public bool hasPerceptionEvidence;
    public string displayText = string.Empty;
    public string displayRevision = string.Empty;
    public string locale = string.Empty;
    public KoreanPronunciationMode pronunciationMode;
    public string pronunciationValue = string.Empty;
    public KoreanFinalConsonantKind finalConsonant;
    public string pronunciationRevision = string.Empty;
}

[Serializable]
public sealed class GameplayOutcomeMetricSnapshot
{
    public string metricId = string.Empty;
    public double value;
    public string unitId = string.Empty;
    public string referenceKindId = string.Empty;
    public string referenceId = string.Empty;
}

[Serializable]
public sealed class GameplayOutcomeFactSnapshot
{
    public string factId = string.Empty;
    public string value = string.Empty;
}

[Serializable]
public sealed class GameplayOutcomeSubjectSnapshot
{
    public string entityKindId = string.Empty;
    public string entityId = string.Empty;
    public float salience;
    public NarrativeMemoryTier tier;
    public bool isPinned;
    public bool isOptionalWitness;
    public int anchorRevision;
    public int nextEvaluationDay;
    public int influenceUseCount;
    public int influenceRevision;
}

[Serializable]
public sealed class NarrativeEvidenceReferenceSnapshot
{
    public string subjectKindId = string.Empty;
    public string subjectId = string.Empty;
    public string anchorTypeId = string.Empty;
    public string anchorId = string.Empty;
}

[Serializable]
public sealed class GameplayOutcomeProvenanceReferenceSnapshot
{
    public string kindId = string.Empty;
    public string value = string.Empty;
}

[Serializable]
public sealed class GameplayOutcomeSnapshot
{
    public string runId = string.Empty;
    public long sequence;
    public string producerId = string.Empty;
    public string operationId = string.Empty;
    public long commitRevision;
    public int localResultIndex;
    public string outcomeTypeId = string.Empty;
    public int absoluteDay;
    public GameplayOutcomeStatus status;
    public long ownerRevision;
    public long commitGeneration;
    public GameplayOutcomeRecordLifecycle lifecycle;
    public NarrativeMemoryTier storageTier;
    public string locationId = string.Empty;
    public string roomId = string.Empty;
    public int locationX;
    public int locationY;
    public string parentRunId = string.Empty;
    public long parentSequence;
    public string rootOperationId = string.Empty;
    public string causationRelationId = string.Empty;
    public int anchorRevision;
    public int deliveryFaultCount;
    public string lastDeliveryFaultCode = string.Empty;
    public string immutablePayloadHash = string.Empty;
    public List<GameplayOutcomeParticipantSnapshot> participants = new List<GameplayOutcomeParticipantSnapshot>();
    public List<GameplayOutcomeMetricSnapshot> metrics = new List<GameplayOutcomeMetricSnapshot>();
    public List<GameplayOutcomeSubjectSnapshot> subjects = new List<GameplayOutcomeSubjectSnapshot>();
    public List<GameplayOutcomeSubjectSnapshot> initialSubjects = new List<GameplayOutcomeSubjectSnapshot>();
    public List<string> tags = new List<string>();
    public List<NarrativeEvidenceReferenceSnapshot> anchors = new List<NarrativeEvidenceReferenceSnapshot>();
    public List<NarrativeEvidenceReferenceSnapshot> initialAnchors = new List<NarrativeEvidenceReferenceSnapshot>();
    public List<GameplayOutcomeProvenanceReferenceSnapshot> provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>();
    public List<GameplayOutcomeFactSnapshot> facts = new List<GameplayOutcomeFactSnapshot>();
}

[Serializable]
public sealed class GameplayOutcomeMetricAggregateSnapshot
{
    public string metricId = string.Empty;
    public string unitId = string.Empty;
    public string referenceKindId = string.Empty;
    public string referenceId = string.Empty;
    public double sum;
    public double minimum;
    public double maximum;
    public int sampleCount;
}

[Serializable]
public sealed class CompactedNarrativeMemorySnapshot
{
    public string memoryId = string.Empty;
    public string sharedAggregateId = string.Empty;
    public string signature = string.Empty;
    public string outcomeTypeId = string.Empty;
    public GameplayOutcomeStatus status;
    public string subjectKindId = string.Empty;
    public string subjectId = string.Empty;
    public string locationId = string.Empty;
    public string roomId = string.Empty;
    public int locationX;
    public int locationY;
    public long firstSequence;
    public long lastSequence;
    public int firstDay;
    public int lastDay;
    public int occurrenceCount;
    public float salience;
    public int influenceUseCount;
    public int influenceRevision;
    public string sourceSegmentHash = string.Empty;
    public string compactedPayloadHash = string.Empty;
    public List<string> tags = new List<string>();
    public List<GameplayOutcomeMetricAggregateSnapshot> metrics = new List<GameplayOutcomeMetricAggregateSnapshot>();
    public List<GameplayOutcomeParticipantSnapshot> participants = new List<GameplayOutcomeParticipantSnapshot>();
    public List<GameplayOutcomeProvenanceReferenceSnapshot> provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>();
    public List<GameplayOutcomeFactSnapshot> facts = new List<GameplayOutcomeFactSnapshot>();
}

public readonly struct CompactedNarrativeMetric
{
    internal CompactedNarrativeMetric(GameplayOutcomeMetricAggregateSnapshot source)
    {
        MetricId = new GameplayMetricId(source.metricId);
        UnitId = new GameplayMetricUnitId(source.unitId);
        DefinitionOrInstanceId = string.IsNullOrEmpty(source.referenceKindId)
            ? default
            : new GameplayEntityId(new GameplayEntityKindId(source.referenceKindId), source.referenceId);
        Sum = source.sum;
        Minimum = source.minimum;
        Maximum = source.maximum;
        SampleCount = source.sampleCount;
    }
    public GameplayMetricId MetricId { get; }
    public GameplayMetricUnitId UnitId { get; }
    public GameplayEntityId DefinitionOrInstanceId { get; }
    public double Sum { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public int SampleCount { get; }
}

public readonly struct CompactedNarrativeParticipant
{
    internal CompactedNarrativeParticipant(GameplayOutcomeParticipantSnapshot source)
    {
        EntityId = new GameplayEntityId(new GameplayEntityKindId(source.entityKindId), source.entityId);
        RoleId = new GameplayRoleId(source.roleId);
        ParticipationKind = source.participationKind;
        HasPerceptionEvidence = source.hasPerceptionEvidence;
        DisplayName = new KoreanNameSnapshot(
            source.displayText,
            source.displayRevision,
            new KoreanPronunciationHint(
                source.pronunciationMode,
                source.pronunciationValue,
                source.finalConsonant,
                source.pronunciationRevision),
            source.locale);
    }
    public GameplayEntityId EntityId { get; }
    public GameplayRoleId RoleId { get; }
    public GameplayParticipationKind ParticipationKind { get; }
    public bool HasPerceptionEvidence { get; }
    public KoreanNameSnapshot DisplayName { get; }
}

public readonly struct CompactedNarrativeMemoryReadView
{
    private readonly CompactedNarrativeMemorySnapshot memory;

    internal CompactedNarrativeMemoryReadView(CompactedNarrativeMemorySnapshot memory) =>
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));

    public NarrativeMemoryId MemoryId => new NarrativeMemoryId(memory.memoryId);
    public GameplayMemorySignature Signature => new GameplayMemorySignature(memory.signature);
    public GameplayOutcomeTypeId OutcomeTypeId => new GameplayOutcomeTypeId(memory.outcomeTypeId);
    public GameplayOutcomeStatus Status => memory.status;
    public GameplayEntityId SubjectId => new GameplayEntityId(
        new GameplayEntityKindId(memory.subjectKindId),
        memory.subjectId);
    public GameplayLocationReference Location => new GameplayLocationReference(
        memory.locationId, memory.roomId, memory.locationX, memory.locationY);
    public long FirstSequence => memory.firstSequence;
    public long LastSequence => memory.lastSequence;
    public int FirstDay => memory.firstDay;
    public int LastDay => memory.lastDay;
    public int OccurrenceCount => memory.occurrenceCount;
    public float Salience => memory.salience;
    public int TagCount => memory.tags?.Count ?? 0;
    public int MetricCount => memory.metrics?.Count ?? 0;
    public int ParticipantCount => memory.participants?.Count ?? 0;
    public int ProvenanceCount => memory.provenance?.Count ?? 0;
    public int FactCount => memory.facts?.Count ?? 0;
    public GameplayOutcomeTagId GetTag(int index) =>
        new GameplayOutcomeTagId(memory.tags[RequireIndex(index, TagCount)]);
    public CompactedNarrativeMetric GetMetric(int index) =>
        new CompactedNarrativeMetric(memory.metrics[RequireIndex(index, MetricCount)]);
    public CompactedNarrativeParticipant GetParticipant(int index) =>
        new CompactedNarrativeParticipant(
            memory.participants[RequireIndex(index, ParticipantCount)]);
    public GameplayOutcomeProvenanceReference GetProvenance(int index)
    {
        GameplayOutcomeProvenanceReferenceSnapshot source =
            memory.provenance[RequireIndex(index, ProvenanceCount)];
        return new GameplayOutcomeProvenanceReference(source.kindId, source.value);
    }
    public GameplayOutcomeFact GetFact(int index)
    {
        GameplayOutcomeFactSnapshot source = memory.facts[RequireIndex(index, FactCount)];
        return new GameplayOutcomeFact(new GameplayOutcomeFactId(source.factId), source.value);
    }

    private static int RequireIndex(int index, int count)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }
}

public readonly struct NarrativeMemoryView
{
    public NarrativeMemoryView(
        NarrativeMemoryId memoryId,
        NarrativePerspectiveKind perspectiveKind,
        string text,
        string rendererVersion,
        bool neutralFrameUsed)
    {
        MemoryId = memoryId;
        PerspectiveKind = perspectiveKind;
        Text = text ?? string.Empty;
        RendererVersion = rendererVersion ?? string.Empty;
        NeutralFrameUsed = neutralFrameUsed;
    }
    public NarrativeMemoryId MemoryId { get; }
    public NarrativePerspectiveKind PerspectiveKind { get; }
    public string Text { get; }
    public string RendererVersion { get; }
    public bool NeutralFrameUsed { get; }
}

public readonly struct GameplayOutcomeQueryItem
{
    public GameplayOutcomeQueryItem(GameplayOutcomeSnapshot exact)
    {
        Exact = exact;
        Compacted = null;
    }
    public GameplayOutcomeQueryItem(CompactedNarrativeMemorySnapshot compacted)
    {
        Exact = null;
        Compacted = compacted;
    }
    public GameplayOutcomeSnapshot Exact { get; }
    public CompactedNarrativeMemorySnapshot Compacted { get; }
    public bool IsCompacted => Compacted != null;
    public long SortSequence => Exact != null ? Exact.sequence : Compacted?.lastSequence ?? 0L;
    internal int SortKind => Exact != null ? 1 : 2;
    internal string StableId => Exact != null
        ? Exact.runId + "/" + Exact.sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : Compacted?.memoryId ?? string.Empty;
}

public sealed class GameplayOutcomeQueryPage
{
    public GameplayOutcomeQueryPage(IReadOnlyList<GameplayOutcomeQueryItem> items, OutcomeCursor nextCursor)
    {
        Items = items ?? Array.Empty<GameplayOutcomeQueryItem>();
        NextCursor = nextCursor;
    }
    public IReadOnlyList<GameplayOutcomeQueryItem> Items { get; }
    public OutcomeCursor NextCursor { get; }
}

public interface IGameplayOutcomeQuery
{
    GameplayOutcomeQueryPage GetGlobal(OutcomeCursor cursor, OutcomeFilter filter);
    GameplayOutcomeQueryPage GetForEntity(GameplayEntityId id, OutcomeCursor cursor, OutcomeFilter filter);
    GameplayOutcomeQueryPage GetForOperation(GameplayOperationId id);
    GameplayOutcomeQueryPage GetNarrativeEvidence(NarrativeEvidenceQuery query);
    bool TryGetExact(GameplayOutcomeId id, out GameplayOutcomeSnapshot outcome);
    bool TryProject(GameplayOutcomeId id, NarrativePerspectiveContext perspective, out NarrativeView view);
    bool TryProjectMemory(
        NarrativeMemoryId id,
        NarrativePerspectiveContext perspective,
        out NarrativeMemoryView view);
}

public interface IGameplayOutcomeMemoryCommands
{
    bool TrySetPlayerPin(GameplayOutcomeId outcomeId, GameplayEntityId subjectId, bool pinned, out string failureCode);
    bool TryAddEvidenceAnchor(GameplayOutcomeId outcomeId, GameplayEntityId subjectId, NarrativeEvidenceReference anchor, out string failureCode);
    bool TryRemoveEvidenceAnchor(GameplayOutcomeId outcomeId, GameplayEntityId subjectId, NarrativeEvidenceReference anchor, out string failureCode);
    EvidenceAnchorPrepareResult TryPrepareEvidenceAnchorMutation(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor,
        EvidenceAnchorMutationKind mutation,
        int expectedAnchorRevision,
        out PreparedEvidenceAnchorToken prepared);
    EvidenceAnchorCommitResult CommitEvidenceAnchorMutation(
        in PreparedEvidenceAnchorToken prepared,
        out EvidenceAnchorRollbackToken rollback);
    void CancelEvidenceAnchorMutation(in PreparedEvidenceAnchorToken prepared);
    EvidenceAnchorRollbackResult RollbackEvidenceAnchorMutation(
        in EvidenceAnchorRollbackToken rollback);
    InfluenceUsePrepareResult TryPrepareInfluenceUse(
        GameplayOutcomeId outcomeId,
        GameplayEntityId subjectId,
        int expectedRevision,
        int expectedUseCount,
        out PreparedInfluenceUseToken prepared);
    InfluenceUseCommitResult CommitInfluenceUse(
        in PreparedInfluenceUseToken prepared,
        out InfluenceUseRollbackToken rollback);
    InfluenceUseCommitResult CompleteInfluenceUses(
        IReadOnlyList<InfluenceUseRollbackToken> committed);
    void CancelInfluenceUse(in PreparedInfluenceUseToken prepared);
    InfluenceUseRollbackResult RollbackInfluenceUse(
        in InfluenceUseRollbackToken rollback);
    InfluenceUseRollbackResult RollbackCompletedInfluenceUse(
        in InfluenceUseRollbackToken rollback);
}

public enum InfluenceUsePrepareCode
{
    Prepared = 1,
    Invalid = 2,
    Stale = 3,
    ReservationBusy = 4,
    Exhausted = 5
}
public readonly struct InfluenceUsePrepareResult
{
    public InfluenceUsePrepareResult(InfluenceUsePrepareCode code, string detailCode)
    { Code = code; DetailCode = detailCode ?? string.Empty; }
    public InfluenceUsePrepareCode Code { get; }
    public string DetailCode { get; }
    public bool Success => Code == InfluenceUsePrepareCode.Prepared;
}
public enum InfluenceUseCommitCode
{ Committed = 1, InvalidToken = 2, Stale = 3, WorldMismatch = 4 }
public readonly struct InfluenceUseCommitResult
{
    public InfluenceUseCommitResult(InfluenceUseCommitCode code) => Code = code;
    public InfluenceUseCommitCode Code { get; }
    public bool Success => Code == InfluenceUseCommitCode.Committed;
}
public enum InfluenceUseRollbackCode
{ RolledBack = 1, InvalidToken = 2, Stale = 3, WorldMismatch = 4 }
public readonly struct InfluenceUseRollbackResult
{
    public InfluenceUseRollbackResult(InfluenceUseRollbackCode code) => Code = code;
    public InfluenceUseRollbackCode Code { get; }
    public bool Success => Code == InfluenceUseRollbackCode.RolledBack;
}
public readonly struct PreparedInfluenceUseToken
{
    internal PreparedInfluenceUseToken(
        int pageIndex, int pageGeneration, long worldEpoch, long nonce,
        int subjectIndex, GameplayEntityId subjectId,
        int expectedRevision, int expectedUseCount)
    {
        PageIndex = pageIndex; PageGeneration = pageGeneration;
        WorldEpoch = worldEpoch; Nonce = nonce; SubjectIndex = subjectIndex;
        SubjectId = subjectId; ExpectedRevision = expectedRevision;
        ExpectedUseCount = expectedUseCount;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    internal long Nonce { get; }
    internal int SubjectIndex { get; }
    public long WorldEpoch { get; }
    public GameplayEntityId SubjectId { get; }
    public int ExpectedRevision { get; }
    public int ExpectedUseCount { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0
        && Nonce > 0L && SubjectId.IsValid;
}
public readonly struct InfluenceUseRollbackToken
{
    internal InfluenceUseRollbackToken(
        int pageIndex, int pageGeneration, long worldEpoch,
        int subjectIndex, GameplayEntityId subjectId,
        long reservationNonce,
        int committedRevision, int committedUseCount,
        GameplayOutcomeSubjectLink previousSubject)
    {
        PageIndex = pageIndex; PageGeneration = pageGeneration;
        WorldEpoch = worldEpoch; SubjectIndex = subjectIndex;
        SubjectId = subjectId; ReservationNonce = reservationNonce;
        CommittedRevision = committedRevision;
        CommittedUseCount = committedUseCount;
        PreviousSubject = previousSubject;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    internal int SubjectIndex { get; }
    internal GameplayOutcomeSubjectLink PreviousSubject { get; }
    internal long ReservationNonce { get; }
    public long WorldEpoch { get; }
    public GameplayEntityId SubjectId { get; }
    public int CommittedRevision { get; }
    public int CommittedUseCount { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0
        && SubjectId.IsValid && ReservationNonce > 0L
        && CommittedRevision > 0 && CommittedUseCount > 0;
}

public enum EvidenceAnchorMutationKind { Add = 1, Remove = 2 }
public enum EvidenceAnchorPrepareCode
{
    Prepared = 1,
    AlreadyApplied = 2,
    Invalid = 3,
    Stale = 4,
    CapacityDeferred = 5,
    ReservationBusy = 6
}
public readonly struct EvidenceAnchorPrepareResult
{
    public EvidenceAnchorPrepareResult(EvidenceAnchorPrepareCode code, string detailCode)
    {
        Code = code;
        DetailCode = detailCode ?? string.Empty;
    }
    public EvidenceAnchorPrepareCode Code { get; }
    public string DetailCode { get; }
    public bool Success => Code == EvidenceAnchorPrepareCode.Prepared
        || Code == EvidenceAnchorPrepareCode.AlreadyApplied;
}
public enum EvidenceAnchorCommitCode { Committed = 1, InvalidToken = 2, Stale = 3, WorldMismatch = 4 }
public readonly struct EvidenceAnchorCommitResult
{
    public EvidenceAnchorCommitResult(EvidenceAnchorCommitCode code) => Code = code;
    public EvidenceAnchorCommitCode Code { get; }
    public bool Success => Code == EvidenceAnchorCommitCode.Committed;
}
public enum EvidenceAnchorRollbackCode { RolledBack = 1, InvalidToken = 2, Stale = 3, WorldMismatch = 4 }
public readonly struct EvidenceAnchorRollbackResult
{
    public EvidenceAnchorRollbackResult(EvidenceAnchorRollbackCode code) => Code = code;
    public EvidenceAnchorRollbackCode Code { get; }
    public bool Success => Code == EvidenceAnchorRollbackCode.RolledBack;
}
public readonly struct PreparedEvidenceAnchorToken
{
    internal PreparedEvidenceAnchorToken(
        int pageIndex, int pageGeneration, long worldEpoch, long nonce,
        int expectedRevision, int subjectIndex, int anchorIndex,
        EvidenceAnchorMutationKind mutation, GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor)
    {
        PageIndex = pageIndex; PageGeneration = pageGeneration; WorldEpoch = worldEpoch;
        Nonce = nonce; ExpectedRevision = expectedRevision; SubjectIndex = subjectIndex;
        AnchorIndex = anchorIndex; Mutation = mutation; SubjectId = subjectId; Anchor = anchor;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    internal long Nonce { get; }
    internal int SubjectIndex { get; }
    internal int AnchorIndex { get; }
    public long WorldEpoch { get; }
    public int ExpectedRevision { get; }
    public EvidenceAnchorMutationKind Mutation { get; }
    public GameplayEntityId SubjectId { get; }
    public NarrativeEvidenceReference Anchor { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0 && Nonce > 0L && Anchor.IsValid;
}
public readonly struct EvidenceAnchorRollbackToken
{
    internal EvidenceAnchorRollbackToken(
        int pageIndex, int pageGeneration, long worldEpoch, int priorRevision, int committedRevision,
        int anchorIndex, EvidenceAnchorMutationKind mutation, GameplayEntityId subjectId,
        NarrativeEvidenceReference anchor, GameplayOutcomeSubjectLink previousSubject)
    {
        PageIndex = pageIndex; PageGeneration = pageGeneration; WorldEpoch = worldEpoch;
        PriorRevision = priorRevision; CommittedRevision = committedRevision; AnchorIndex = anchorIndex; Mutation = mutation;
        SubjectId = subjectId; Anchor = anchor; PreviousSubject = previousSubject;
    }
    internal int PageIndex { get; }
    internal int PageGeneration { get; }
    internal int AnchorIndex { get; }
    internal GameplayOutcomeSubjectLink PreviousSubject { get; }
    public long WorldEpoch { get; }
    public int CommittedRevision { get; }
    internal int PriorRevision { get; }
    public EvidenceAnchorMutationKind Mutation { get; }
    public GameplayEntityId SubjectId { get; }
    public NarrativeEvidenceReference Anchor { get; }
    public bool IsValid => PageIndex >= 0 && PageGeneration > 0 && CommittedRevision > 0 && Anchor.IsValid;
}

public readonly struct GameplayOutcomeConsolidationRequest
{
    public GameplayOutcomeConsolidationRequest(int evaluationDay, long cutoffSequence, int policyVersion, long worldEpoch)
    {
        EvaluationDay = evaluationDay;
        CutoffSequence = cutoffSequence;
        PolicyVersion = policyVersion;
        WorldEpoch = worldEpoch;
    }
    public int EvaluationDay { get; }
    public long CutoffSequence { get; }
    public int PolicyVersion { get; }
    public long WorldEpoch { get; }
}

public readonly struct GameplayOutcomeConsolidationSliceResult
{
    public GameplayOutcomeConsolidationSliceResult(int examined, int published, bool hasBacklog, string statusCode)
    {
        Examined = examined;
        Published = published;
        HasBacklog = hasBacklog;
        StatusCode = statusCode ?? string.Empty;
    }
    public int Examined { get; }
    public int Published { get; }
    public bool HasBacklog { get; }
    public string StatusCode { get; }
}

public interface IGameplayOutcomeConsolidationService
{
    bool TrySchedule(in GameplayOutcomeConsolidationRequest request, out string failureCode);
    GameplayOutcomeConsolidationSliceResult ProcessSlice(int maximumRecords, long maximumElapsedTicks);
    int PendingJobCount { get; }
}
