using System;

/// <summary>
/// Shared producer-side transaction boundary.  It deliberately keeps the
/// gameplay ledger token opaque: an owner may mutate only after Prepare has
/// reserved and fully serialized the immutable receipt, and treats every
/// state at or after Committed as durable even when delivery is deferred.
/// </summary>
public readonly struct PreparedOwnerOutcome
{
    internal PreparedOwnerOutcome(PreparedOutcomeToken token) => Token = token;
    internal PreparedOutcomeToken Token { get; }
    public GameplayResultKey ResultKey => Token.ResultKey;
    public bool IsValid => Token.IsValid;
}

public enum OwnerOutcomeCommitPhase
{
    Rejected = 0,
    CommittedPendingDelivery = 1,
    PublishedAwaitingAcknowledgement = 2,
    PublishedAcknowledged = 3
}

public readonly struct OwnerOutcomeCommitResult
{
    public OwnerOutcomeCommitResult(
        OwnerOutcomeCommitPhase phase,
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        string canonicalPayloadHash,
        string detailCode)
    {
        Phase = phase;
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        CanonicalPayloadHash = canonicalPayloadHash ?? string.Empty;
        DetailCode = detailCode ?? string.Empty;
    }

    public OwnerOutcomeCommitPhase Phase { get; }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public string CanonicalPayloadHash { get; }
    public string DetailCode { get; }
    public bool DurablyCommitted => Phase != OwnerOutcomeCommitPhase.Rejected;
    public bool Acknowledged =>
        Phase == OwnerOutcomeCommitPhase.PublishedAcknowledged;
}

public sealed class PreparedOutcomeOwnerTransaction
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public PreparedOutcomeOwnerTransaction(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedOwnerOutcome prepared,
        out OwnerOutcomeCommitResult replay,
        out bool capacityDeferred,
        out string failureReason)
    {
        prepared = default;
        replay = default;
        capacityDeferred = false;
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (result.Success)
        {
            prepared = new PreparedOwnerOutcome(token);
            failureReason = string.Empty;
            return true;
        }
        capacityDeferred = result.Code == OutcomePrepareCode.CapacityDeferred;
        if (result.Code is OutcomePrepareCode.AlreadyCommitted
            or OutcomePrepareCode.AlreadyPublished
            or OutcomePrepareCode.AlreadyTerminal)
        {
            replay = Reconcile(result.Existing.ResultKey);
            if (replay.DurablyCommitted)
            {
                failureReason = string.Empty;
                return true;
            }
        }
        failureReason = "owner-outcome-prepare-" + result.Code
            + ":" + result.DetailCode;
        return false;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedOwnerOutcome prepared,
        long expectedOwnerRevision)
    {
        if (!prepared.IsValid)
            return Rejected(default, "owner-outcome-token-invalid");

        OutcomeCommitResult commit;
        CommittedOutcomeToken committed;
        try
        {
            commit = recorder.CommitPrepared(
                prepared.Token,
                expectedOwnerRevision,
                out committed);
        }
        catch (Exception exception)
        {
            OwnerOutcomeCommitResult ambiguous = Reconcile(prepared.ResultKey);
            return ambiguous.DurablyCommitted
                ? ambiguous
                : new OwnerOutcomeCommitResult(
                    OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                    prepared.ResultKey,
                    default,
                    string.Empty,
                    "owner-outcome-commit-ambiguous:"
                    + exception.GetType().Name);
        }
        if (!commit.Success)
        {
            recorder.CancelPrepared(prepared.Token);
            return Rejected(
                prepared.ResultKey,
                "owner-outcome-commit-" + commit.Code + ":" + commit.DetailCode);
        }

        OutcomeDeliveryResult delivery;
        try
        {
            delivery = recorder.TryDeliver(committed);
        }
        catch (Exception exception)
        {
            return Snapshot(
                OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                committed.ResultKey,
                committed.OutcomeId,
                "owner-outcome-delivery-exception:" + exception.GetType().Name);
        }
        if (!delivery.Published)
        {
            return Snapshot(
                OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                committed.ResultKey,
                committed.OutcomeId,
                "owner-outcome-delivery-" + delivery.Code + ":"
                + delivery.DetailCode);
        }

        OutcomeAcknowledgeResult acknowledgement;
        try
        {
            acknowledgement = recorder.Acknowledge(committed);
        }
        catch (Exception exception)
        {
            return Snapshot(
                OwnerOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
                committed.ResultKey,
                committed.OutcomeId,
                "owner-outcome-acknowledge-exception:"
                + exception.GetType().Name);
        }
        return Snapshot(
            acknowledgement.Success
                ? OwnerOutcomeCommitPhase.PublishedAcknowledged
                : OwnerOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
            committed.ResultKey,
            committed.OutcomeId,
            acknowledgement.Success
                ? string.Empty
                : "owner-outcome-acknowledge-" + acknowledgement.Code);
    }

    public OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey)
    {
        if (!resultKey.IsValid)
            return Rejected(resultKey, "owner-outcome-result-key-invalid");
        try
        {
            recorder.RetryPendingDeliveries(1);
            if (!diagnostics.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity identity)
                || identity.ResultKey != resultKey)
            {
                return Rejected(resultKey, "owner-outcome-result-missing");
            }
            return FromIdentity(identity, string.Empty);
        }
        catch (Exception exception)
        {
            return new OwnerOutcomeCommitResult(
                OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                resultKey,
                default,
                string.Empty,
                "owner-outcome-reconcile-ambiguous:"
                + exception.GetType().Name);
        }
    }

    public void Cancel(in PreparedOwnerOutcome prepared)
    {
        if (prepared.IsValid)
            recorder.CancelPrepared(prepared.Token);
    }

    private OwnerOutcomeCommitResult Snapshot(
        OwnerOutcomeCommitPhase fallback,
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        string detail)
    {
        if (diagnostics.TryGetResultIdentity(
                resultKey,
                out GameplayOutcomeReplayIdentity identity)
            && identity.ResultKey == resultKey)
        {
            return FromIdentity(identity, detail);
        }
        return new OwnerOutcomeCommitResult(
            fallback,
            resultKey,
            outcomeId,
            string.Empty,
            detail);
    }

    private static OwnerOutcomeCommitResult FromIdentity(
        GameplayOutcomeReplayIdentity identity,
        string detail)
    {
        OwnerOutcomeCommitPhase phase = identity.State switch
        {
            GameplayOutcomeReplayState.PublishedAcknowledged
                or GameplayOutcomeReplayState.Compacted
                or GameplayOutcomeReplayState.Forgotten =>
                OwnerOutcomeCommitPhase.PublishedAcknowledged,
            GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement =>
                OwnerOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
            _ => OwnerOutcomeCommitPhase.CommittedPendingDelivery
        };
        return new OwnerOutcomeCommitResult(
            phase,
            identity.ResultKey,
            identity.OutcomeId,
            identity.CanonicalPayloadHash,
            detail);
    }

    private static OwnerOutcomeCommitResult Rejected(
        GameplayResultKey key,
        string detail) => new(
        OwnerOutcomeCommitPhase.Rejected,
        key,
        default,
        string.Empty,
        detail);
}
