using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PreparedOutputCheckpointGcCoordinator :
    IPreparedOutputCheckpointGcCoordinator
{
    private static readonly PreparedOutputCheckpointGcParticipantKind[]
        RequiredKinds =
        {
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority
        };

    private readonly IReadOnlyList<IPreparedOutputCheckpointGcParticipant>
        participants;

    public PreparedOutputCheckpointGcCoordinator(
        IEnumerable<IPreparedOutputCheckpointGcParticipant> participants)
    {
        this.participants = (participants
                ?? throw new ArgumentNullException(nameof(participants)))
            .Where(value => value != null)
            .OrderBy(value => value.CheckpointGcParticipantKind)
            .ThenBy(value => value.CheckpointGcParticipantId,
                StringComparer.Ordinal)
            .ToArray();
    }

    public PreparedOutputCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest)
    {
        PreparedOutputCheckpointGcResult topology = ValidateTopology();
        if (topology.Status != PreparedOutputCheckpointGcStatus.Applied)
            return topology;

        long[] sequences = participants
            .Select(value => value.LastConfirmedCheckpointSequence)
            .Distinct()
            .ToArray();
        if (sequences.Length != 1)
        {
            return Result(
                PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.ParticipantSequenceMismatch,
                0L,
                "Prepared-output checkpoint participants disagree on sequence.");
        }
        string[] confirmedDigests = participants
            .Select(value => value.LastConfirmedSerializedByteDigest
                ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (confirmedDigests.Length != 1
            || sequences[0] == 0L && confirmedDigests[0].Length != 0
            || sequences[0] > 0L && !IsDigest(confirmedDigests[0]))
        {
            return Result(
                PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.ParticipantSequenceMismatch,
                sequences[0],
                "Prepared-output checkpoint participants disagree on durable digest.");
        }
        if (sequences[0] > 0L
            && string.Equals(
                confirmedDigests[0],
                serializedByteDigest,
                StringComparison.Ordinal))
        {
            return Result(
                PreparedOutputCheckpointGcStatus.AlreadyApplied,
                PreparedOutputCheckpointGcReason.None,
                sequences[0],
                "Durable save callback was already checkpointed.");
        }

        long nextSequence;
        try
        {
            nextSequence = checked(sequences[0] + 1L);
        }
        catch (OverflowException)
        {
            return Result(
                PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.StaleCheckpoint,
                sequences[0],
                "Prepared-output checkpoint sequence overflowed.");
        }

        PreparedOutputCheckpointGcContext context;
        try
        {
            context = new PreparedOutputCheckpointGcContext(
                nextSequence,
                serializedByteDigest,
                slotId);
        }
        catch (Exception exception)
        {
            return Result(
                PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.ReplayDigestMismatch,
                nextSequence,
                exception.Message);
        }

        List<PreparedParticipant> prepared = new();
        int alreadyAppliedCount = 0;
        PreparedOutputCheckpointGcResult alreadyAppliedResult = default;
        try
        {
            foreach (IPreparedOutputCheckpointGcParticipant participant in
                     participants)
            {
                PreparedOutputCheckpointGcResult result = participant
                    .PrepareCheckpointGarbageCollection(
                        context,
                        out IPreparedOutputCheckpointGcCandidate candidate);
                if (candidate != null)
                    prepared.Add(new PreparedParticipant(participant, candidate));
                if (result.CheckpointSequence != nextSequence)
                {
                    return FinishPrepared(
                        prepared,
                        Result(
                            PreparedOutputCheckpointGcStatus.Corruption,
                            PreparedOutputCheckpointGcReason
                                .PartialAuthorityCoverage,
                            nextSequence,
                            $"Prepared-output participant '{participant.CheckpointGcParticipantId}' returned a conflicting result sequence."));
                }
                if (result.Status == PreparedOutputCheckpointGcStatus.Applied)
                {
                    string batchIdError = string.Empty;
                    string operationIdError = string.Empty;
                    bool batchIdsValid = candidate != null
                        && TryValidateOrderedCanonicalIds(
                            candidate.BatchCommitIds,
                            out batchIdError);
                    bool operationIdsValid = candidate != null
                        && TryValidateOrderedCanonicalIds(
                            candidate.RouteOperationIds,
                            out operationIdError);
                    if (candidate == null
                        || candidate.ParticipantKind
                            != participant.CheckpointGcParticipantKind
                        || !string.Equals(candidate.ParticipantId,
                            participant.CheckpointGcParticipantId,
                            StringComparison.Ordinal)
                        || candidate.CheckpointSequence != nextSequence
                        || !string.Equals(candidate.SerializedByteDigest,
                            serializedByteDigest,
                            StringComparison.Ordinal)
                        || !batchIdsValid
                        || !operationIdsValid)
                    {
                        return FinishPrepared(
                            prepared,
                            Result(
                                PreparedOutputCheckpointGcStatus.Corruption,
                                PreparedOutputCheckpointGcReason
                                    .PartialAuthorityCoverage,
                                nextSequence,
                                "Prepared-output participant returned a conflicting candidate. "
                                + batchIdError + operationIdError));
                    }
                    continue;
                }

                if (result.Status ==
                    PreparedOutputCheckpointGcStatus.AlreadyApplied)
                {
                    if (prepared.Count != 0)
                    {
                        return FinishPrepared(
                            prepared,
                            Result(
                                PreparedOutputCheckpointGcStatus.Corruption,
                                PreparedOutputCheckpointGcReason
                                    .PartialAuthorityCoverage,
                                nextSequence,
                                "Prepared-output checkpoint is partially applied."));
                    }
                    if (participant.LastConfirmedCheckpointSequence
                            != nextSequence
                        || !string.Equals(
                            participant.LastConfirmedSerializedByteDigest,
                            serializedByteDigest,
                            StringComparison.Ordinal))
                    {
                        return FinishPrepared(
                            prepared,
                            Result(
                                PreparedOutputCheckpointGcStatus.Corruption,
                                PreparedOutputCheckpointGcReason
                                    .PartialAuthorityCoverage,
                                nextSequence,
                                "Prepared-output AlreadyApplied state does not match the durable checkpoint."));
                    }
                    alreadyAppliedCount++;
                    alreadyAppliedResult = result;
                    continue;
                }
                return FinishPrepared(prepared, result);
            }
        }
        catch (Exception exception)
        {
            return FinishPrepared(
                prepared,
                Result(
                    PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                    nextSequence,
                    exception.Message));
        }

        if (alreadyAppliedCount != 0)
        {
            bool stateAgrees = participants.All(participant =>
                participant.LastConfirmedCheckpointSequence == nextSequence
                && string.Equals(
                    participant.LastConfirmedSerializedByteDigest,
                    serializedByteDigest,
                    StringComparison.Ordinal));
            if (alreadyAppliedCount == participants.Count
                && prepared.Count == 0
                && stateAgrees)
                return alreadyAppliedResult;
            return FinishPrepared(
                prepared,
                Result(
                    PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                    nextSequence,
                    "Prepared-output checkpoint is partially applied."));
        }

        PreparedParticipant reference = prepared[0];
        for (int index = 1; index < prepared.Count; index++)
        {
            PreparedParticipant candidate = prepared[index];
            if (!SameOrderedIds(
                    reference.Candidate.BatchCommitIds,
                    candidate.Candidate.BatchCommitIds)
                || !SameOrderedIds(
                    reference.Candidate.RouteOperationIds,
                    candidate.Candidate.RouteOperationIds))
            {
                return FinishPrepared(
                    prepared,
                    Result(
                        PreparedOutputCheckpointGcStatus.Corruption,
                        PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                        nextSequence,
                        "Prepared-output participants selected different batch or route-operation authority."));
            }
        }

        List<PreparedParticipant> published = new();
        int collectedBatchCount = 0;
        try
        {
            foreach (PreparedParticipant entry in prepared)
            {
                // Publish may throw after an internal pointer swap. Include the
                // attempted participant in reverse rollback before invoking it.
                published.Add(entry);
                PreparedOutputCheckpointGcResult result = entry.Participant
                    .PublishCheckpointGarbageCollection(entry.Candidate);
                if (result.Status != PreparedOutputCheckpointGcStatus.Applied)
                {
                    return RollbackPublished(
                        prepared,
                        published,
                        result.Status == PreparedOutputCheckpointGcStatus.Deferred
                            ? result
                            : Result(
                                PreparedOutputCheckpointGcStatus.Corruption,
                                PreparedOutputCheckpointGcReason
                                    .ParticipantPublishFailed,
                                nextSequence,
                                result.Message));
                }
                if (result.CheckpointSequence != nextSequence
                    || result.CollectedBatchCount
                        != entry.Candidate.BatchCommitIds.Count
                    || entry.Participant.LastConfirmedCheckpointSequence
                        != nextSequence
                    || !string.Equals(
                        entry.Participant.LastConfirmedSerializedByteDigest,
                        serializedByteDigest,
                        StringComparison.Ordinal))
                {
                    return RollbackPublished(
                        prepared,
                        published,
                        Result(
                            PreparedOutputCheckpointGcStatus.Corruption,
                            PreparedOutputCheckpointGcReason
                                .ParticipantPublishFailed,
                            nextSequence,
                            $"Prepared-output participant '{entry.Participant.CheckpointGcParticipantId}' published a conflicting checkpoint result or authority state."));
                }
                collectedBatchCount = result.CollectedBatchCount;
            }
        }
        catch (Exception exception)
        {
            return RollbackPublished(
                prepared,
                published,
                Result(
                    PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.ParticipantPublishFailed,
                    nextSequence,
                    exception.Message));
        }

        foreach (PreparedParticipant entry in prepared)
            entry.Participant.CompleteCheckpointGarbageCollection(entry.Candidate);
        return Result(
            PreparedOutputCheckpointGcStatus.Applied,
            PreparedOutputCheckpointGcReason.None,
            nextSequence,
            "Prepared-output checkpoint GC committed after durable save replacement.",
            collectedBatchCount);
    }

    private PreparedOutputCheckpointGcResult ValidateTopology()
    {
        foreach (PreparedOutputCheckpointGcParticipantKind required in RequiredKinds)
        {
            int count = participants.Count(value =>
                value.CheckpointGcParticipantKind == required);
            if (count == 0)
            {
                return Result(
                    PreparedOutputCheckpointGcStatus.Deferred,
                    PreparedOutputCheckpointGcReason.MissingParticipant,
                    0L,
                    $"Prepared-output checkpoint participant '{required}' is not registered.");
            }
            if (count != 1)
            {
                return Result(
                    PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                    0L,
                    $"Prepared-output checkpoint participant '{required}' is duplicated.");
            }
        }
        if (participants.Count != RequiredKinds.Length)
        {
            return Result(
                PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                0L,
                "Prepared-output checkpoint has an unknown participant.");
        }
        return Result(
            PreparedOutputCheckpointGcStatus.Applied,
            PreparedOutputCheckpointGcReason.None,
            participants[0].LastConfirmedCheckpointSequence,
            "Prepared-output checkpoint participant topology is complete.");
    }

    private static PreparedOutputCheckpointGcResult RollbackPublished(
        IReadOnlyList<PreparedParticipant> prepared,
        IReadOnlyList<PreparedParticipant> published,
        PreparedOutputCheckpointGcResult failure)
    {
        List<Exception> rollbackFailures = new();
        for (int index = published.Count - 1; index >= 0; index--)
        {
            try
            {
                published[index].Participant.RollbackCheckpointGarbageCollection(
                    published[index].Candidate);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        for (int index = 0; index < prepared.Count; index++)
        {
            try
            {
                prepared[index].Participant.CompleteCheckpointGarbageCollection(
                    prepared[index].Candidate);
            }
            catch (Exception exception)
            {
                rollbackFailures.Add(exception);
            }
        }
        if (rollbackFailures.Count == 0)
            return failure;
        return Result(
            PreparedOutputCheckpointGcStatus.Corruption,
            PreparedOutputCheckpointGcReason.ParticipantRollbackFailed,
            failure.CheckpointSequence,
            string.Join(" | ", rollbackFailures.Select(value => value.Message)));
    }

    private static PreparedOutputCheckpointGcResult FinishPrepared(
        IReadOnlyList<PreparedParticipant> prepared,
        PreparedOutputCheckpointGcResult result)
    {
        foreach (PreparedParticipant entry in prepared)
            entry.Participant.CompleteCheckpointGarbageCollection(entry.Candidate);
        return result;
    }

    private static PreparedOutputCheckpointGcResult Result(
        PreparedOutputCheckpointGcStatus status,
        PreparedOutputCheckpointGcReason reason,
        long sequence,
        string message,
        int collectedBatchCount = 0) => new(
        status,
        reason,
        sequence,
        message,
        collectedBatchCount);

    private static bool IsDigest(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')))
                return false;
        }
        return true;
    }

    private static bool TryValidateOrderedCanonicalIds(
        IReadOnlyList<string> values,
        out string error)
    {
        if (values == null)
        {
            error = " Candidate ID collection is null.";
            return false;
        }
        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (string.IsNullOrEmpty(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                error = " Candidate ID is empty or noncanonical.";
                return false;
            }
            if (previous != null && string.CompareOrdinal(previous, value) >= 0)
            {
                error = " Candidate IDs are duplicated or not ordinal-sorted.";
                return false;
            }
            previous = value;
        }
        error = string.Empty;
        return true;
    }

    private static bool SameOrderedIds(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private readonly struct PreparedParticipant
    {
        internal PreparedParticipant(
            IPreparedOutputCheckpointGcParticipant participant,
            IPreparedOutputCheckpointGcCandidate candidate)
        {
            Participant = participant;
            Candidate = candidate;
        }

        internal IPreparedOutputCheckpointGcParticipant Participant { get; }
        internal IPreparedOutputCheckpointGcCandidate Candidate { get; }
    }
}
