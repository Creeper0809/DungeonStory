using System;
using System.Collections.Generic;

public enum ProductionFacilityDestructiveDrainCheckpointGcStatus
{
    Applied = 1,
    Deferred = 2,
    AlreadyApplied = 3,
    Corruption = 4
}

public enum ProductionFacilityDestructiveDrainCheckpointGcReason
{
    None = 0,
    NoEligibleOperation = 1,
    MissingParticipant = 2,
    ParticipantTopologyMismatch = 3,
    StaleCheckpoint = 4,
    ReplayDigestMismatch = 5,
    WorkOrderOwnerStillLive = 6,
    LiveAuthorityChanged = 7,
    ParticipantPrepareFailed = 8,
    ParticipantPublishFailed = 9,
    ParticipantRollbackFailed = 10,
    JournalPublishFailed = 11
}

public readonly struct ProductionFacilityDestructiveDrainCheckpointGcContext
{
    public ProductionFacilityDestructiveDrainCheckpointGcContext(
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
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameterName);
        }
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
            throw new ArgumentException(
                "A canonical non-empty value is required.",
                parameterName);
        }
        return value;
    }
}

public readonly struct ProductionFacilityDestructiveDrainCheckpointGcResult
{
    public ProductionFacilityDestructiveDrainCheckpointGcResult(
        ProductionFacilityDestructiveDrainCheckpointGcStatus status,
        ProductionFacilityDestructiveDrainCheckpointGcReason reason,
        long checkpointSequence,
        string message,
        int collectedOperationCount = 0)
    {
        Status = status;
        Reason = reason;
        CheckpointSequence = checkpointSequence;
        Message = message ?? string.Empty;
        CollectedOperationCount = collectedOperationCount;
    }

    public ProductionFacilityDestructiveDrainCheckpointGcStatus Status { get; }
    public ProductionFacilityDestructiveDrainCheckpointGcReason Reason { get; }
    public long CheckpointSequence { get; }
    public string Message { get; }
    public int CollectedOperationCount { get; }
}

public interface IProductionFacilityDestructiveDrainCheckpointGcCandidate
{
    string ParticipantId { get; }
    long CheckpointSequence { get; }
    string SerializedByteDigest { get; }
    IReadOnlyList<string> OperationIds { get; }
}

/// <summary>
/// Optional capability implemented by every current destructive-drain
/// participant. Candidate preparation is read-only; publish removes exact
/// terminal rows; rollback restores only those exact rows.
/// </summary>
public interface IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    string CheckpointGcParticipantId { get; }

    ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate);

    ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);

    void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);
}

public readonly struct WorkOrderDestructiveDrainRetentionSnapshot
{
    public WorkOrderDestructiveDrainRetentionSnapshot(
        int workOrderVersion,
        string operationId,
        IReadOnlyList<string> ownerIds,
        string semanticFingerprint)
    {
        if (workOrderVersion < 0
            || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                operationId,
                out _)
            || ownerIds == null
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                semanticFingerprint))
        {
            throw new ArgumentException(
                "Work-order destructive-drain retention snapshot is invalid.");
        }
        string[] frozenOwners = new string[ownerIds.Count];
        string previous = null;
        for (int index = 0; index < ownerIds.Count; index++)
        {
            string owner = ownerIds[index];
            if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                    owner)
                || previous != null
                && string.CompareOrdinal(previous, owner) >= 0)
            {
                throw new ArgumentException(
                    "Work-order destructive-drain owners are invalid or unsorted.");
            }
            frozenOwners[index] = owner;
            previous = owner;
        }
        WorkOrderVersion = workOrderVersion;
        OperationId = operationId;
        OwnerIds = Array.AsReadOnly(frozenOwners);
        SemanticFingerprint = semanticFingerprint;
    }

    public int WorkOrderVersion { get; }
    public string OperationId { get; }
    public IReadOnlyList<string> OwnerIds { get; }
    public string SemanticFingerprint { get; }
    public bool HasOwner => OwnerIds.Count > 0;
}

/// <summary>
/// Read-only lifetime fence. A terminal journal row remains durable while a
/// dismantle WorkOrder still owns salvage/rebuild continuation for it.
/// </summary>
public interface IWorkOrderDestructiveDrainRetentionQuery
{
    bool TryCaptureRetention(
        ProductionFacilityDestructiveDrainOperationId operationId,
        out WorkOrderDestructiveDrainRetentionSnapshot snapshot,
        out string failureReason);
}

public interface IProductionFacilityDestructiveDrainCheckpointGcCoordinator
{
    ProductionFacilityDestructiveDrainCheckpointGcResult
        OnDurableSaveCommitted(
            string slotId,
            string serializedByteDigest);
}

public interface IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
{
    long CheckpointSequence { get; }
    string SerializedByteDigest { get; }
    IReadOnlyList<string> OperationIds { get; }
}

public interface IProductionFacilityDestructiveDrainCheckpointGcJournal
{
    long LastConfirmedCheckpointSequence { get; }
    string LastConfirmedSerializedByteDigest { get; }

    ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds,
            out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                candidate);

    ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                candidate);

    void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
            candidate);
    void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
            candidate);
}
