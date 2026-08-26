using System;
using System.Collections.Generic;

public enum PreparedOutputCheckpointGcParticipantKind
{
    EconomyRoutingAuthority = 1,
    ItemsExactRouteAuthority = 2
}

public enum PreparedOutputCheckpointGcStatus
{
    Applied = 1,
    Deferred = 2,
    AlreadyApplied = 3,
    Corruption = 4
}

public enum PreparedOutputCheckpointGcReason
{
    None = 0,
    NoEligibleWholeBatch = 1,
    MissingParticipant = 2,
    ParticipantSequenceMismatch = 3,
    StaleCheckpoint = 4,
    ReplayDigestMismatch = 5,
    LiveAuthorityChanged = 6,
    PhysicalStateNotStable = 7,
    PartialAuthorityCoverage = 8,
    ParticipantPublishFailed = 9,
    ParticipantRollbackFailed = 10
}

public readonly struct PreparedOutputCheckpointGcContext
{
    public PreparedOutputCheckpointGcContext(
        long checkpointSequence,
        string serializedByteDigest,
        string slotId)
    {
        if (checkpointSequence <= 0L)
            throw new ArgumentOutOfRangeException(nameof(checkpointSequence));
        CheckpointSequence = checkpointSequence;
        SerializedByteDigest = RequireDigest(
            serializedByteDigest,
            nameof(serializedByteDigest));
        SlotId = RequireCanonical(slotId, nameof(slotId));
    }

    public long CheckpointSequence { get; }
    public string SerializedByteDigest { get; }
    public string SlotId { get; }

    private static string RequireDigest(string value, string parameterName)
    {
        if (value == null || value.Length != 64)
            throw new ArgumentException("A lowercase SHA-256 digest is required.", parameterName);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')))
            {
                throw new ArgumentException(
                    "A lowercase SHA-256 digest is required.",
                    parameterName);
            }
        }
        return value;
    }

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A canonical non-empty value is required.", parameterName);
        }
        return value;
    }
}

public readonly struct PreparedOutputCheckpointGcResult
{
    public PreparedOutputCheckpointGcResult(
        PreparedOutputCheckpointGcStatus status,
        PreparedOutputCheckpointGcReason reason,
        long checkpointSequence,
        string message,
        int collectedBatchCount = 0)
    {
        Status = status;
        Reason = reason;
        CheckpointSequence = checkpointSequence;
        Message = message ?? string.Empty;
        CollectedBatchCount = collectedBatchCount;
    }

    public PreparedOutputCheckpointGcStatus Status { get; }
    public PreparedOutputCheckpointGcReason Reason { get; }
    public long CheckpointSequence { get; }
    public string Message { get; }
    public int CollectedBatchCount { get; }
}

public interface IPreparedOutputCheckpointGcCandidate
{
    string ParticipantId { get; }
    PreparedOutputCheckpointGcParticipantKind ParticipantKind { get; }
    long CheckpointSequence { get; }
    string SerializedByteDigest { get; }
    IReadOnlyList<string> BatchCommitIds { get; }
    IReadOnlyList<string> RouteOperationIds { get; }
}

public interface IPreparedOutputCheckpointGcParticipant
{
    string CheckpointGcParticipantId { get; }
    PreparedOutputCheckpointGcParticipantKind CheckpointGcParticipantKind { get; }
    long LastConfirmedCheckpointSequence { get; }
    string LastConfirmedSerializedByteDigest { get; }

    PreparedOutputCheckpointGcResult PrepareCheckpointGarbageCollection(
        PreparedOutputCheckpointGcContext context,
        out IPreparedOutputCheckpointGcCandidate candidate);

    PreparedOutputCheckpointGcResult PublishCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate);

    void RollbackCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate);
}

public interface IPreparedOutputCheckpointGcCoordinator
{
    PreparedOutputCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest);
}
