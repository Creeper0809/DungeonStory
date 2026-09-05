using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionFacilityDestructiveDrainCheckpointGcCoordinator :
    IProductionFacilityDestructiveDrainCheckpointGcCoordinator
{
    private readonly IProductionFacilityDestructiveDrainParticipantRegistry
        registry;
    private readonly IReadOnlyList<
        IProductionFacilityDestructiveDrainCheckpointGcParticipant>
        participants;
    private readonly IProductionFacilityDestructiveDrainJournalQuery journalQuery;
    private readonly IProductionFacilityDestructiveDrainCheckpointGcJournal
        journal;
    private readonly IWorkOrderDestructiveDrainRetentionQuery retention;

    public ProductionFacilityDestructiveDrainCheckpointGcCoordinator(
        IProductionFacilityDestructiveDrainParticipantRegistry registry,
        IEnumerable<IProductionFacilityDestructiveDrainCheckpointGcParticipant>
            participants,
        IProductionFacilityDestructiveDrainJournalQuery journalQuery,
        IProductionFacilityDestructiveDrainCheckpointGcJournal journal,
        IWorkOrderDestructiveDrainRetentionQuery retention)
    {
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
        this.journalQuery = journalQuery
            ?? throw new ArgumentNullException(nameof(journalQuery));
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.retention = retention
            ?? throw new ArgumentNullException(nameof(retention));

        Dictionary<string,
            IProductionFacilityDestructiveDrainCheckpointGcParticipant> byId =
            (participants ?? throw new ArgumentNullException(nameof(participants)))
            .Where(value => value != null)
            .ToDictionary(
                value => value.CheckpointGcParticipantId,
                StringComparer.Ordinal);
        this.participants = registry.ExecutionOrder
            .Reverse()
            .Select(value => byId.TryGetValue(value.ParticipantId, out var exact)
                ? exact
                : null)
            .ToArray();
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        OnDurableSaveCommitted(
            string slotId,
            string serializedByteDigest)
    {
        ProductionFacilityDestructiveDrainCheckpointGcResult topology =
            ValidateTopology();
        if (topology.Status !=
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied)
        {
            return topology;
        }

        if (journal.LastConfirmedCheckpointSequence > 0L
            && string.Equals(
                journal.LastConfirmedSerializedByteDigest,
                serializedByteDigest,
                StringComparison.Ordinal))
        {
            return Result(
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied,
                ProductionFacilityDestructiveDrainCheckpointGcReason.None,
                journal.LastConfirmedCheckpointSequence,
                "The destructive-drain durable callback was already applied.");
        }

        long nextSequence;
        try
        {
            nextSequence = checked(journal.LastConfirmedCheckpointSequence + 1L);
        }
        catch (OverflowException exception)
        {
            return Result(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .StaleCheckpoint,
                journal.LastConfirmedCheckpointSequence,
                exception.Message);
        }

        ProductionFacilityDestructiveDrainCheckpointGcContext context;
        try
        {
            context = new ProductionFacilityDestructiveDrainCheckpointGcContext(
                nextSequence,
                serializedByteDigest,
                slotId);
        }
        catch (Exception exception)
        {
            return Result(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ReplayDigestMismatch,
                nextSequence,
                exception.Message);
        }

        List<RetentionProof> absenceProofs = new();
        List<ProductionFacilityDestructiveDrainEntrySaveData> eligible = new();
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 journalQuery.CaptureOpen()
                     .Where(value => value.phase ==
                         ProductionFacilityDestructiveDrainPhase
                             .WorldRemovedAwaitingCheckpointGc)
                     .OrderBy(value => value.operationId, StringComparer.Ordinal))
        {
            string failureReason = string.Empty;
            if (!ProductionFacilityDestructiveDrainOperationId.TryParse(
                    entry.operationId,
                    out ProductionFacilityDestructiveDrainOperationId operation)
                || !retention.TryCaptureRetention(
                    operation,
                    out WorkOrderDestructiveDrainRetentionSnapshot proof,
                    out failureReason))
            {
                return Result(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .WorkOrderOwnerStillLive,
                    nextSequence,
                    string.IsNullOrEmpty(failureReason)
                        ? "Work-order retention proof is invalid."
                        : failureReason);
            }
            if (proof.HasOwner)
            {
                if (entry.cause !=
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition)
                {
                    return Result(
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Corruption,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .WorkOrderOwnerStillLive,
                        nextSequence,
                        "A non-demolition destructive operation is owned by a WorkOrder.");
                }
                continue;
            }
            absenceProofs.Add(new RetentionProof(operation, proof));
            eligible.Add(entry.Clone());
        }

        List<PreparedParticipant> prepared = new();
        IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
            journalCandidate = null;
        try
        {
            foreach (IProductionFacilityDestructiveDrainCheckpointGcParticipant
                     participant in participants)
            {
                ProductionFacilityDestructiveDrainCheckpointGcResult result =
                    participant.PrepareCheckpointGarbageCollection(
                        context,
                        eligible,
                        out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                            candidate);
                if (candidate != null)
                    prepared.Add(new PreparedParticipant(participant, candidate));
                if (result.Status !=
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Applied
                    || !ValidCandidate(
                        participant,
                        candidate,
                        context,
                        eligible))
                {
                    return FinishPrepared(
                        prepared,
                        result.Status ==
                            ProductionFacilityDestructiveDrainCheckpointGcStatus
                                .Applied
                            ? Result(
                                ProductionFacilityDestructiveDrainCheckpointGcStatus
                                    .Corruption,
                                ProductionFacilityDestructiveDrainCheckpointGcReason
                                    .ParticipantPrepareFailed,
                                nextSequence,
                                "A destructive-drain participant returned a conflicting candidate.")
                            : result);
                }
            }

            ProductionFacilityDestructiveDrainCheckpointGcResult journalPrepared =
                journal.PrepareCheckpointGarbageCollection(
                    context,
                    eligible.Select(value => value.operationId).ToArray(),
                    out journalCandidate);
            if (journalPrepared.Status !=
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                || journalCandidate == null)
            {
                return FinishPrepared(prepared, journalPrepared);
            }
        }
        catch (Exception exception)
        {
            return FinishJournalAndParticipants(
                journalCandidate,
                prepared,
                Result(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPrepareFailed,
                    nextSequence,
                    exception.Message));
        }

        foreach (RetentionProof absence in absenceProofs)
        {
            if (!retention.TryCaptureRetention(
                    absence.Operation,
                    out WorkOrderDestructiveDrainRetentionSnapshot current,
                    out string failureReason)
                || current.HasOwner
                || current.WorkOrderVersion != absence.Snapshot.WorkOrderVersion
                || !string.Equals(
                    current.SemanticFingerprint,
                    absence.Snapshot.SemanticFingerprint,
                    StringComparison.Ordinal))
            {
                return FinishJournalAndParticipants(
                    journalCandidate,
                    prepared,
                    Result(
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Deferred,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .WorkOrderOwnerStillLive,
                        nextSequence,
                        string.IsNullOrEmpty(failureReason)
                            ? "Work-order authority changed after GC preparation."
                            : failureReason));
            }
        }

        List<PreparedParticipant> published = new();
        bool journalPublishAttempted = false;
        try
        {
            foreach (PreparedParticipant entry in prepared)
            {
                published.Add(entry);
                ProductionFacilityDestructiveDrainCheckpointGcResult result =
                    entry.Participant.PublishCheckpointGarbageCollection(
                        entry.Candidate);
                if (result.Status !=
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied)
                {
                    return RollbackPublished(
                        journalCandidate,
                        journalPublishAttempted,
                        prepared,
                        published,
                        result);
                }
            }

            journalPublishAttempted = true;
            ProductionFacilityDestructiveDrainCheckpointGcResult journalResult =
                journal.PublishCheckpointGarbageCollection(journalCandidate);
            if (journalResult.Status !=
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied)
            {
                return RollbackPublished(
                    journalCandidate,
                    journalPublishAttempted,
                    prepared,
                    published,
                    journalResult);
            }
        }
        catch (Exception exception)
        {
            return RollbackPublished(
                journalCandidate,
                journalPublishAttempted,
                prepared,
                published,
                Result(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPublishFailed,
                    nextSequence,
                    exception.Message));
        }

        journal.CompleteCheckpointGarbageCollection(journalCandidate);
        foreach (PreparedParticipant entry in prepared)
            entry.Participant.CompleteCheckpointGarbageCollection(entry.Candidate);
        return Result(
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            eligible.Count == 0
                ? ProductionFacilityDestructiveDrainCheckpointGcReason
                    .NoEligibleOperation
                : ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            nextSequence,
            eligible.Count == 0
                ? "No destructive-drain operation was eligible; checkpoint marker advanced."
                : "Destructive-drain lower tombstones and journal were checkpoint-collected atomically.",
            eligible.Count);
    }

    private ProductionFacilityDestructiveDrainCheckpointGcResult
        ValidateTopology()
    {
        if (participants.Count != registry.ExecutionOrder.Count
            || participants.Any(value => value == null)
            || participants.Select(value => value.CheckpointGcParticipantId)
                .Distinct(StringComparer.Ordinal).Count() != participants.Count)
        {
            return Result(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantTopologyMismatch,
                journal.LastConfirmedCheckpointSequence,
                "Destructive-drain checkpoint participant topology is incomplete or duplicated.");
        }
        string[] expected = registry.ExecutionOrder
            .Select(value => value.ParticipantId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = participants
            .Select(value => value.CheckpointGcParticipantId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            return Result(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant,
                journal.LastConfirmedCheckpointSequence,
                "Destructive-drain checkpoint participant set does not match the execution registry.");
        }
        return Result(
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            journal.LastConfirmedCheckpointSequence,
            "Destructive-drain checkpoint participant topology is complete.");
    }

    private static bool ValidCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcParticipant participant,
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate,
        ProductionFacilityDestructiveDrainCheckpointGcContext context,
        IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> entries)
    {
        return candidate != null
            && string.Equals(
                candidate.ParticipantId,
                participant.CheckpointGcParticipantId,
                StringComparison.Ordinal)
            && candidate.CheckpointSequence == context.CheckpointSequence
            && string.Equals(
                candidate.SerializedByteDigest,
                context.SerializedByteDigest,
                StringComparison.Ordinal)
            && candidate.OperationIds.SequenceEqual(
                entries.Select(value => value.operationId),
                StringComparer.Ordinal);
    }

    private ProductionFacilityDestructiveDrainCheckpointGcResult
        RollbackPublished(
            IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                journalCandidate,
            bool journalPublishAttempted,
            IReadOnlyList<PreparedParticipant> prepared,
            IReadOnlyList<PreparedParticipant> published,
            ProductionFacilityDestructiveDrainCheckpointGcResult failure)
    {
        List<Exception> rollbackFailures = new();
        bool journalRollbackSucceeded = !journalPublishAttempted;
        HashSet<IProductionFacilityDestructiveDrainCheckpointGcCandidate>
            rollbackSucceeded = new();
        if (journalPublishAttempted)
        {
            try
            {
                journal.RollbackCheckpointGarbageCollection(journalCandidate);
                journalRollbackSucceeded = true;
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        for (int index = published.Count - 1; index >= 0; index--)
        {
            try
            {
                published[index].Participant
                    .RollbackCheckpointGarbageCollection(
                        published[index].Candidate);
                rollbackSucceeded.Add(published[index].Candidate);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        if (journalRollbackSucceeded)
        {
            try
            {
                journal.CompleteCheckpointGarbageCollection(journalCandidate);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        foreach (PreparedParticipant entry in prepared)
        {
            bool wasPublished = published.Any(value =>
                ReferenceEquals(value.Candidate, entry.Candidate));
            if (wasPublished && !rollbackSucceeded.Contains(entry.Candidate))
                continue;
            try
            {
                entry.Participant.CompleteCheckpointGarbageCollection(
                    entry.Candidate);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        if (rollbackFailures.Count == 0)
            return failure;
        return Result(
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
            ProductionFacilityDestructiveDrainCheckpointGcReason
                .ParticipantRollbackFailed,
            failure.CheckpointSequence,
            string.Join(" | ", rollbackFailures.Select(value => value.Message)));
    }

    private ProductionFacilityDestructiveDrainCheckpointGcResult
        FinishJournalAndParticipants(
            IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                journalCandidate,
            IReadOnlyList<PreparedParticipant> prepared,
            ProductionFacilityDestructiveDrainCheckpointGcResult result)
    {
        if (journalCandidate != null)
            journal.CompleteCheckpointGarbageCollection(journalCandidate);
        return FinishPrepared(prepared, result);
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult
        FinishPrepared(
            IReadOnlyList<PreparedParticipant> prepared,
            ProductionFacilityDestructiveDrainCheckpointGcResult result)
    {
        foreach (PreparedParticipant entry in prepared)
            entry.Participant.CompleteCheckpointGarbageCollection(entry.Candidate);
        return result;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult Result(
        ProductionFacilityDestructiveDrainCheckpointGcStatus status,
        ProductionFacilityDestructiveDrainCheckpointGcReason reason,
        long sequence,
        string message,
        int collectedOperationCount = 0) => new(
        status,
        reason,
        sequence,
        message,
        collectedOperationCount);

    private readonly struct RetentionProof
    {
        internal RetentionProof(
            ProductionFacilityDestructiveDrainOperationId operation,
            WorkOrderDestructiveDrainRetentionSnapshot snapshot)
        {
            Operation = operation;
            Snapshot = snapshot;
        }

        internal ProductionFacilityDestructiveDrainOperationId Operation
            { get; }
        internal WorkOrderDestructiveDrainRetentionSnapshot Snapshot { get; }
    }

    private readonly struct PreparedParticipant
    {
        internal PreparedParticipant(
            IProductionFacilityDestructiveDrainCheckpointGcParticipant
                participant,
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
        {
            Participant = participant;
            Candidate = candidate;
        }

        internal IProductionFacilityDestructiveDrainCheckpointGcParticipant
            Participant { get; }
        internal IProductionFacilityDestructiveDrainCheckpointGcCandidate
            Candidate { get; }
    }
}
