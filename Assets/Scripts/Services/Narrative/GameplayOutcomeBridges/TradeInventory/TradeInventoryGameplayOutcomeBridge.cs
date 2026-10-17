using System;

public interface ITradeInventoryOutcomeCommitter
{
    bool TryPrepare(
        in TradeInventoryOutcomeReceipt receipt,
        out PreparedTradeInventoryOutcome prepared,
        out string failureReason);
    bool TryCommit(
        in PreparedTradeInventoryOutcome prepared,
        long expectedOwnerRevision,
        out bool canonicalCommitted,
        out string failureReason);
    void Cancel(in PreparedTradeInventoryOutcome prepared);
}

public sealed class TradeInventoryGameplayOutcomeBridge :
    ITradeInventoryOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public TradeInventoryGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool TryPrepare(
        in TradeInventoryOutcomeReceipt receipt,
        out PreparedTradeInventoryOutcome prepared,
        out string failureReason)
    {
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (result.Success)
        {
            prepared = new PreparedTradeInventoryOutcome(
                token,
                receipt.ResultKey,
                false);
            failureReason = string.Empty;
            return true;
        }
        if (result.Code is OutcomePrepareCode.AlreadyCommitted
                or OutcomePrepareCode.AlreadyPublished
                or OutcomePrepareCode.AlreadyTerminal
            && result.Existing.ResultKey.Equals(receipt.ResultKey)
            && result.Existing.HasCanonicalPayloadHash
            && result.Existing.State is >= GameplayOutcomeReplayState.Committed
                and <= GameplayOutcomeReplayState.Forgotten)
        {
            prepared = new PreparedTradeInventoryOutcome(
                default,
                receipt.ResultKey,
                true);
            failureReason = string.Empty;
            return true;
        }
        prepared = default;
        failureReason = "trade-inventory-outcome-prepare-" + result.Code
            + ":" + result.DetailCode;
        return false;
    }

    public bool TryCommit(
        in PreparedTradeInventoryOutcome prepared,
        long expectedOwnerRevision,
        out bool canonicalCommitted,
        out string failureReason)
    {
        canonicalCommitted = false;
        failureReason = string.Empty;
        if (!prepared.IsValid || expectedOwnerRevision < 0L)
        {
            failureReason = "trade-inventory-prepared-invalid";
            return false;
        }
        if (prepared.IsCanonicalReplay)
        {
            canonicalCommitted = IsCanonicalCommitted(
                prepared.ResultKey,
                out failureReason);
            return canonicalCommitted;
        }

        OutcomeCommitResult commit;
        CommittedOutcomeToken committed;
        try
        {
            commit = recorder.CommitPrepared(
                prepared.Token,
                expectedOwnerRevision,
                out committed);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // CommitPrepared is the authority boundary. A dependency may throw
            // after that boundary while returning its token. Resolve the
            // canonical key before the owner is allowed to roll back.
            canonicalCommitted = IsCanonicalCommitted(
                prepared.ResultKey,
                out _);
            failureReason = canonicalCommitted
                ? "trade-inventory-outcome-post-commit-reconciliation-pending:"
                    + exception.Message
                : "trade-inventory-outcome-commit-exception:"
                    + exception.Message;
            return canonicalCommitted;
        }
        if (!commit.Success)
        {
            recorder.CancelPrepared(prepared.Token);
            failureReason = "trade-inventory-outcome-commit-" + commit.Code
                + ":" + commit.DetailCode;
            return false;
        }
        canonicalCommitted = true;
        try
        {
            OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
            if (!delivery.Published)
            {
                failureReason = "trade-inventory-outcome-delivery-" + delivery.Code
                    + ":" + delivery.DetailCode;
                return true;
            }
            OutcomeAcknowledgeResult acknowledged = recorder.Acknowledge(committed);
            if (!acknowledged.Success)
            {
                failureReason = "trade-inventory-outcome-acknowledge-"
                    + acknowledged.Code;
                return true;
            }
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // The ledger commit is already canonical. Delivery and
            // acknowledgement are retryable phases and must never make the
            // domain owner roll back its matching mutation.
            failureReason =
                "trade-inventory-outcome-post-commit-reconciliation-pending:"
                + exception.Message;
            return true;
        }
    }

    public void Cancel(in PreparedTradeInventoryOutcome prepared)
    {
        if (!prepared.IsCanonicalReplay && prepared.Token.IsValid)
            recorder.CancelPrepared(prepared.Token);
    }

    private bool IsCanonicalCommitted(
        GameplayResultKey key,
        out string failureReason)
    {
        try
        {
            if (diagnostics.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.ResultKey.Equals(key)
                && identity.HasCanonicalPayloadHash
                && identity.State is >= GameplayOutcomeReplayState.Committed
                    and <= GameplayOutcomeReplayState.Forgotten)
            {
                failureReason = string.Empty;
                return true;
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failureReason =
                "trade-inventory-canonical-replay-diagnostics-failed:"
                + exception.Message;
            return false;
        }
        failureReason = "trade-inventory-canonical-replay-unresolved";
        return false;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
