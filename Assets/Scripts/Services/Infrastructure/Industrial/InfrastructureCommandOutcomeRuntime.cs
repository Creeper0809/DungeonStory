using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

internal sealed class InfrastructureCommandOutcomeRuntime :
    IInfrastructureCommandOutcomeTransaction,
    IInfrastructureCommandOutcomePersistence,
    ITickable
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IInfrastructureCommandOutcomeCommitter committer;

    public InfrastructureCommandOutcomeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        IInfrastructureCommandOutcomeCommitter committer)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.committer = committer
            ?? throw new ArgumentNullException(nameof(committer));
        _ = Current;
    }

    private InfrastructureCommandOutcomeAggregateState Current =>
        rootStore.GetOrCreate(
            () => new InfrastructureCommandOutcomeAggregateState());

    private InfrastructureCommandOutcomeAggregateState Writable =>
        rootStore.GetOrCreateWritable(
            () => new InfrastructureCommandOutcomeAggregateState(),
            state => state.DeepClone());

    public void Tick() => FlushPending();

    public bool TryPrepare(
        in InfrastructureCommandOutcomeSource source,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        long revision = Current.NextOwnerRevision;
        if (revision <= 0L || revision == long.MaxValue)
        {
            failureReason = "infrastructure-command-owner-revision-exhausted";
            return false;
        }

        try
        {
            return committer.TryPrepare(
                source,
                revision,
                out prepared,
                out failureReason);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "infrastructure-command-prepare-exception:"
                + exception.GetType().Name;
            return false;
        }
    }

    public InfrastructureCommandOutcomeCommitResult CommitReversible(
        IPreparedInfrastructureCommandOutcome prepared)
    {
        if (!IsCurrent(prepared))
        {
            return new InfrastructureCommandOutcomeCommitResult(
                false,
                "infrastructure-command-owner-revision-stale");
        }

        long revision = prepared.OwnerRevision;
        Writable.NextOwnerRevision = checked(revision + 1L);
        try
        {
            InfrastructureCommandOutcomeCommitResult result =
                committer.Commit(prepared);
            if (result.DurablyCommitted)
                return result;
            Writable.NextOwnerRevision = revision;
            return result;
        }
        catch (Exception exception)
        {
            Writable.NextOwnerRevision = revision;
            committer.Cancel(prepared);
            return new InfrastructureCommandOutcomeCommitResult(
                false,
                "infrastructure-command-commit-exception:"
                + exception.GetType().Name);
        }
    }

    public void QueueCommitted(IPreparedInfrastructureCommandOutcome prepared)
    {
        if (!IsCurrent(prepared)
            || Current.Pending.ContainsKey(prepared.OwnerRevision))
        {
            throw new InvalidOperationException(
                "Infrastructure command outcome revision is stale or conflicting.");
        }

        InfrastructureCommandOutcomeOutboxSaveData pending =
            InfrastructureCommandOutcomeOutboxSaveData.Create(
                prepared.Source,
                prepared.OwnerRevision,
                prepared.FrozenContext);
        Writable.Pending.Add(prepared.OwnerRevision, pending);
        Writable.NextOwnerRevision = checked(prepared.OwnerRevision + 1L);
    }

    public void DeliverQueued(
        long ownerRevision,
        IPreparedInfrastructureCommandOutcome prepared = null)
    {
        if (!Current.Pending.TryGetValue(
                ownerRevision,
                out InfrastructureCommandOutcomeOutboxSaveData pending))
        {
            return;
        }

        try
        {
            IPreparedInfrastructureCommandOutcome token = prepared;
            if (token == null
                && !committer.TryPreparePending(
                    pending.Clone(),
                    out token,
                    out string prepareFailure))
            {
                Debug.LogError(
                    "Infrastructure command outcome remains pending: "
                    + ownerRevision + ":" + prepareFailure);
                return;
            }
            if (token == null || token.OwnerRevision != ownerRevision)
            {
                Debug.LogError(
                    "Infrastructure command outcome remains pending: "
                    + ownerRevision + ":token-revision-mismatch");
                return;
            }

            InfrastructureCommandOutcomeCommitResult result =
                committer.Commit(token);
            if (result.DurablyCommitted)
            {
                Writable.Pending.Remove(ownerRevision);
                return;
            }
            Debug.LogError(
                "Infrastructure command outcome remains pending after commit rejection: "
                + ownerRevision + ":" + result.DetailCode);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Infrastructure command outcome delivery threw and remains pending: "
                + ownerRevision + ":" + exception.GetType().Name);
        }
    }

    public void Cancel(IPreparedInfrastructureCommandOutcome prepared)
    {
        if (prepared != null)
            committer.Cancel(prepared);
    }

    public DungeonInfrastructureCommandOutcomeSaveData Capture() => new()
    {
        nextOwnerRevision = Current.NextOwnerRevision,
        pendingOutcomes = Current.Pending.Values
            .OrderBy(value => value.ownerRevision)
            .Select(value => value.Clone())
            .ToList()
    };

    public InfrastructureCommandOutcomeRestoreCandidate PrepareRestore(
        DungeonInfrastructureCommandOutcomeSaveData snapshot)
    {
        InfrastructureCommandOutcomeSaveValidation.RequireValid(snapshot);
        return new InfrastructureCommandOutcomeRestoreCandidate(
            snapshot.nextOwnerRevision,
            snapshot.pendingOutcomes
                .OrderBy(value => value.ownerRevision)
                .Select(value => value.Clone())
                .ToArray());
    }

    public void Restore(InfrastructureCommandOutcomeRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        InfrastructureCommandOutcomeAggregateState replacement = new()
        {
            NextOwnerRevision = candidate.NextOwnerRevision
        };
        foreach (InfrastructureCommandOutcomeOutboxSaveData pending in
                 candidate.Pending)
        {
            replacement.Pending.Add(pending.ownerRevision, pending.Clone());
        }
        rootStore.Replace(replacement);
    }

    private bool IsCurrent(IPreparedInfrastructureCommandOutcome prepared) =>
        prepared != null
        && prepared.OwnerRevision > 0L
        && prepared.OwnerRevision == Current.NextOwnerRevision;

    private void FlushPending()
    {
        foreach (long revision in Current.Pending.Keys.ToArray())
            DeliverQueued(revision);
    }
}

internal static class InfrastructureCommandOutcomeSaveValidation
{
    public static void RequireValid(
        DungeonInfrastructureCommandOutcomeSaveData data)
    {
        if (data == null
            || data.version != DungeonInfrastructureCommandOutcomeSaveData
                .CurrentVersion
            || data.nextOwnerRevision <= 0L
            || data.pendingOutcomes == null)
        {
            throw new InvalidOperationException(
                "Infrastructure command outcome save header is invalid.");
        }

        long previous = 0L;
        HashSet<long> revisions = new();
        foreach (InfrastructureCommandOutcomeOutboxSaveData pending in
                 data.pendingOutcomes)
        {
            if (pending == null
                || pending.ownerRevision <= previous
                || pending.ownerRevision >= data.nextOwnerRevision
                || !revisions.Add(pending.ownerRevision))
            {
                throw new InvalidOperationException(
                    "Infrastructure command outcome outbox ordering is invalid.");
            }
            _ = pending.ToSource();
            _ = pending.ToFrozenContext();
            previous = pending.ownerRevision;
        }
    }
}

internal static class InfrastructureCommandOutcomeExecution
{
    public static bool TryPrepare(
        IInfrastructureCommandOutcomeTransaction transaction,
        InfrastructureCommandOutcomeKind kind,
        string targetId,
        BuildableObject facility,
        string beforeValue,
        string afterValue,
        out IPreparedInfrastructureCommandOutcome prepared,
        out InfrastructureCommandResult failure)
    {
        prepared = null;
        failure = default;
        if (transaction == null)
        {
            failure = InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid,
                "infrastructure-command-outcome-unavailable");
            return false;
        }

        try
        {
            string facilityId =
                IndustrialInfrastructureIdentity.GetNodeId(facility);
            InfrastructureCommandOutcomeSource source = new(
                kind,
                targetId,
                facilityId,
                facility.centerPos.x,
                facility.centerPos.y,
                beforeValue,
                afterValue);
            if (transaction.TryPrepare(
                    source,
                    out prepared,
                    out string prepareFailure))
            {
                return true;
            }
            failure = InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid,
                string.IsNullOrWhiteSpace(prepareFailure)
                    ? "infrastructure-command-prepare-failed"
                    : prepareFailure);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException
                                           or NullReferenceException)
        {
            failure = InfrastructureCommandResult.Failed(
                FailureCode.IndustrialCommandInvalid,
                "infrastructure-command-source-invalid",
                exception.GetType().Name);
            return false;
        }
    }

    public static InfrastructureCommandResult CommitFailure(
        in InfrastructureCommandOutcomeCommitResult result) =>
        InfrastructureCommandResult.Failed(
            FailureCode.IndustrialCommandInvalid,
            string.IsNullOrWhiteSpace(result.DetailCode)
                ? "infrastructure-command-commit-rejected"
                : result.DetailCode);

    public static string Bool(bool value) => value ? "true" : "false";

    public static string Float(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
