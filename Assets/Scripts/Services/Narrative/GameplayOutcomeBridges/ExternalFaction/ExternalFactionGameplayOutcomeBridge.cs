using System;

public interface IOffenseTruthRevealOutcomeCommitter
{
    bool TryPrepare(
        in OffenseTruthRevealOutcomeReceipt receipt,
        out PreparedExternalFactionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedExternalFactionOutcome prepared);
    bool TryCommit(
        in PreparedExternalFactionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public sealed class ExternalFactionGameplayOutcomeBridge :
    IOffenseTruthRevealOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;

    public ExternalFactionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder)
    {
        this.recorder = recorder
            ?? throw new ArgumentNullException(nameof(recorder));
    }

    public bool TryPrepare(
        in OffenseTruthRevealOutcomeReceipt receipt,
        out PreparedExternalFactionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            if (IsCanonicalReplay(result, receipt.ResultKey))
            {
                prepared = new PreparedExternalFactionOutcome(
                    default,
                    receipt.ResultKey,
                    alreadyCommitted: true);
                return true;
            }
            failureReason = "external-faction-outcome-prepare-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }

        prepared = new PreparedExternalFactionOutcome(
            token,
            receipt.ResultKey,
            alreadyCommitted: false);
        return true;
    }

    public void Cancel(in PreparedExternalFactionOutcome prepared)
    {
        if (prepared.Token.IsValid)
            recorder.CancelPrepared(prepared.Token);
    }

    public bool TryCommit(
        in PreparedExternalFactionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!prepared.IsValid)
        {
            failureReason = "external-faction-outcome-prepared-token-invalid";
            return false;
        }
        if (prepared.AlreadyCommitted)
        {
            recorder.RetryPendingDeliveries(1);
            return true;
        }

        OutcomeCommitResult result = recorder.CommitPrepared(
            prepared.Token,
            expectedOwnerRevision,
            out CommittedOutcomeToken committed);
        if (!result.Success)
        {
            recorder.CancelPrepared(prepared.Token);
            failureReason = "external-faction-outcome-commit-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }

        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        if (delivery.Published)
            recorder.Acknowledge(committed);
        return true;
    }

    private static bool IsCanonicalReplay(
        in OutcomePrepareResult result,
        GameplayResultKey resultKey) =>
        result.Existing.ResultKey == resultKey
        && (int)result.Existing.State
            >= (int)GameplayOutcomeReplayState.Committed
        && (result.Code == OutcomePrepareCode.AlreadyCommitted
            || result.Code == OutcomePrepareCode.AlreadyPublished
            || result.Code == OutcomePrepareCode.AlreadyTerminal);
}
