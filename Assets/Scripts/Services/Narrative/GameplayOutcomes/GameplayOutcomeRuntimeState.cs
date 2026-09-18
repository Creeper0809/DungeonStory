using System;
using System.Collections.Generic;

internal readonly struct CompactedMemoryKey : IEquatable<CompactedMemoryKey>
{
    public CompactedMemoryKey(GameplayEntityId subjectId, GameplayMemorySignature signature)
    {
        SubjectId = subjectId;
        Signature = signature;
    }
    public GameplayEntityId SubjectId { get; }
    public GameplayMemorySignature Signature { get; }
    public bool Equals(CompactedMemoryKey other) => SubjectId.Equals(other.SubjectId) && Signature.Equals(other.Signature);
    public override bool Equals(object obj) => obj is CompactedMemoryKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(SubjectId, Signature);
}

[Serializable]
public sealed class GameplayOutcomeTombstoneSnapshot
{
    public string runId = string.Empty;
    public long sequence;
    public string producerId = string.Empty;
    public string operationId = string.Empty;
    public long commitRevision;
    public int localResultIndex;
    public long ownerRevision;
    public NarrativeMemoryTier terminalTier;
    public string canonicalHash = string.Empty;
    public int influenceUseCount;
    public int influenceRevision;
}

[Serializable]
public sealed class GameplayOutcomeConsolidationJobSnapshot
{
    public int evaluationDay;
    public long cutoffSequence;
    public int policyVersion;
    public long worldEpoch;
    public int nextPublishedSlot;
    public long nextSequence;
}

internal sealed class GameplayOutcomeConsolidationJob
{
    public int EvaluationDay;
    public long CutoffSequence;
    public int PolicyVersion;
    public long WorldEpoch;
    public int NextDueIndex;
    public long NextSequence;
    public List<int> DuePageIndices;
}

internal readonly struct GameplayOutcomeDueEntry : IComparable<GameplayOutcomeDueEntry>, IEquatable<GameplayOutcomeDueEntry>
{
    public GameplayOutcomeDueEntry(int dueDay, long sequence, int pageIndex, int generation)
    { DueDay = dueDay; Sequence = sequence; PageIndex = pageIndex; Generation = generation; }
    public int DueDay { get; }
    public long Sequence { get; }
    public int PageIndex { get; }
    public int Generation { get; }
    public int CompareTo(GameplayOutcomeDueEntry other)
    {
        int value = DueDay.CompareTo(other.DueDay);
        if (value != 0) return value;
        value = Sequence.CompareTo(other.Sequence);
        if (value != 0) return value;
        value = PageIndex.CompareTo(other.PageIndex);
        return value != 0 ? value : Generation.CompareTo(other.Generation);
    }
    public bool Equals(GameplayOutcomeDueEntry other) => CompareTo(other) == 0;
    public override bool Equals(object obj) => obj is GameplayOutcomeDueEntry other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(DueDay, Sequence, PageIndex, Generation);
}

internal sealed class GameplayOutcomeConsolidationScratch
{
    public int PageIndex;
    public int PageGeneration;
    public int DueSlot;
    public long WorldEpoch;
    public long PublishedRevision;
    public long CompactedRevision;
    public int AnchorRevision;
    public int PolicyVersion;
    public int EvaluationDay;
    public long CutoffSequence;
    public GameplayOutcomeLedger.SubjectConsolidationDecision[] Decisions;
    public int SubjectIndex;
    public int MetricIndex;
    public List<GameplayOutcomeMetric> SelectedMetrics;
    public GameplayOutcomeLedger.SubjectConsolidationDecision PendingDecision;
    public bool SelectingMetrics;
    public int ParticipantValidationIndex;
    public int ProvenanceValidationIndex;
    public int CompactionDecisionIndex;
    public List<GameplayOutcomeCompactedDelta> PreparedDeltas;
    public int ModifiedHashIndex;
    public GameplayOutcomeCanonicalHash.CompactedHashAccumulator CurrentHash;
    public GameplayOutcomeCompactedMergeScratch CurrentMerge;
}

internal sealed class GameplayOutcomeCompactedMergeScratch
{
    public GameplayOutcomeLedger.SubjectConsolidationDecision Decision;
    public CompactedNarrativeMemorySnapshot Memory;
    public CompactedNarrativeMemorySnapshot SourceMemory;
    public int CopyIndex;
    public int Stage;
    public int TagIndex;
    public int MetricIndex;
    public int ParticipantIndex;
    public int ProvenanceIndex;
    public int FactIndex;
    public int RowState;
    public int SearchLow;
    public int SearchHigh;
    public int InsertIndex;
    public int ShiftIndex;
    public GameplayOutcomeMetricAggregateSnapshot PendingMetric;
    public GameplayOutcomeParticipantSnapshot PendingParticipant;
    public GameplayOutcomeProvenanceReferenceSnapshot PendingProvenance;
}

internal sealed class GameplayOutcomeCompactedDelta
{
    public CompactedMemoryKey Key;
    public int ExistingIndex;
    public CompactedNarrativeMemorySnapshot Memory;
}

internal sealed class GameplayOutcomeLedgerRuntimeState
{
    public GameplayOutcomeLedgerRuntimeState(
        GameplayOutcomeRunId runId,
        long worldEpoch,
        GameplayOutcomeBufferLimits limits)
    {
        if (!runId.IsValid)
            throw new ArgumentException("A valid gameplay outcome run ID is required.", nameof(runId));
        if (worldEpoch <= 0L)
            throw new ArgumentOutOfRangeException(nameof(worldEpoch));
        RunId = runId;
        WorldEpoch = worldEpoch;
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        Pool = new GameplayOutcomeValuePagePool(limits);
        int addressableCapacity = Pool.AddressableCount;
        ActiveByResultKey = new Dictionary<GameplayResultKey, int>(addressableCapacity);
        KnownResultKeys = new HashSet<GameplayResultKey>(limits.KnownResultKeyCapacity);
        ActiveByOutcomeId = new Dictionary<GameplayOutcomeId, int>(addressableCapacity);
        PublishedPageIndices = new List<int>(addressableCapacity);
        CompactedMemories = new List<CompactedNarrativeMemorySnapshot>(addressableCapacity / 4 + 4);
        CompactedByKey = new Dictionary<CompactedMemoryKey, int>(addressableCapacity / 4 + 4);
        Tombstones = new List<GameplayOutcomeTombstoneSnapshot>(addressableCapacity);
        TombstoneByResultKey = new Dictionary<GameplayResultKey, int>(addressableCapacity);
        ConsolidationJobs = new List<GameplayOutcomeConsolidationJob>(16);
        // A sorted flat buffer avoids one tree-node allocation for every
        // acknowledged outcome. Its bounded hot capacity is reserved with the
        // page pool and insertion/removal is deterministic.
        DueEntries = new List<GameplayOutcomeDueEntry>(addressableCapacity);
        DueByPageIndex = new Dictionary<int, GameplayOutcomeDueEntry>(addressableCapacity);
        DirtyDuePageIndices = new List<int>(addressableCapacity);
        NextSequence = 1L;
    }

    public GameplayOutcomeRunId RunId;
    public long WorldEpoch;
    public long Revision;
    public long NextSequence;
    public GameplayOutcomeBufferLimits Limits { get; }
    public GameplayOutcomeValuePagePool Pool { get; }
    public Dictionary<GameplayResultKey, int> ActiveByResultKey { get; }
    public HashSet<GameplayResultKey> KnownResultKeys { get; }
    public Dictionary<GameplayOutcomeId, int> ActiveByOutcomeId { get; }
    public List<int> PublishedPageIndices { get; }
    public List<CompactedNarrativeMemorySnapshot> CompactedMemories { get; set; }
    public Dictionary<CompactedMemoryKey, int> CompactedByKey { get; set; }
    public List<GameplayOutcomeTombstoneSnapshot> Tombstones { get; }
    public Dictionary<GameplayResultKey, int> TombstoneByResultKey { get; }
    public List<GameplayOutcomeConsolidationJob> ConsolidationJobs { get; }
    public List<GameplayOutcomeDueEntry> DueEntries { get; }
    public Dictionary<int, GameplayOutcomeDueEntry> DueByPageIndex { get; }
    public List<int> DirtyDuePageIndices { get; }
    public GameplayOutcomeConsolidationScratch ConsolidationScratch;
    public int NotificationFaultCount;
    public int ReservedCount;
    public int ActiveAnchorReservationCount;
    public int ActiveInfluenceReservationCount;
    public int PendingDeliveryCount;
    public int PublishedCount;
    public int ForgottenCount;
    public int KnownResultKeyHighWater;
    public long CompactedRevision;
    public long CapacityDeferredCount;
    public int ConsecutiveCapacityDeferredCount;
    public int CapacityDeferredStreakHighWater;
    public int ExactPageUseHighWater;
    public int RetentionPromotionFaultCount;
    public int LastScheduledEvaluationDay = -1;
    public long LastScheduledCutoffSequence;
}
