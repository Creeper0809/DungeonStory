using System;

public interface ISocialLifeOutcomeCommitter
{
    bool TryReserveConflict(
        string operationId,
        int absoluteDay,
        bool hasVisitorFacility,
        out ReservedSocialLifeOutcome reserved,
        out string failureReason);
    bool TryWriteConflict(
        in SocialConflictOutcomeReceipt receipt,
        in ReservedSocialLifeOutcome reserved,
        out PreparedSocialLifeOutcome prepared,
        out string failureReason);
    bool TryReserveApology(
        string operationId,
        int absoluteDay,
        out ReservedSocialLifeOutcome reserved,
        out string failureReason);
    bool TryWriteApology(
        in ApologyOutcomeReceipt receipt,
        in ReservedSocialLifeOutcome reserved,
        out PreparedSocialLifeOutcome prepared,
        out string failureReason);
    bool IsAcknowledgedConflictReplay(
        in SocialConflictOutcomeReceipt receipt,
        out string failureReason);
    bool IsAcknowledgedApologyReplay(
        in ApologyOutcomeReceipt receipt,
        out string failureReason);
    bool TryCommit(
        in PreparedSocialLifeOutcome prepared,
        out string failureReason);
    void Cancel(in ReservedSocialLifeOutcome reserved);
    void Cancel(in PreparedSocialLifeOutcome prepared);
}

public sealed class SocialLifeGameplayOutcomeBridge : ISocialLifeOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public SocialLifeGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool TryReserveConflict(
        string operationId,
        int absoluteDay,
        bool hasVisitorFacility,
        out ReservedSocialLifeOutcome reserved,
        out string failureReason)
    {
        GameplayResultKey key;
        try
        {
            key = new GameplayResultKey(
                SocialLifeOutcomeIds.ConflictProducerId,
                new GameplayOperationId(operationId),
                0L,
                0);
        }
        catch (Exception exception) when (IsCaptureException(exception))
        {
            reserved = default;
            failureReason = "social-conflict-result-key-invalid:" + exception.Message;
            return false;
        }
        return Reserve(
            SocialConflictOutcomeAdapter.CreateRequirements(
                key,
                absoluteDay,
                hasVisitorFacility,
                ledger.CurrentWorldEpoch),
            out reserved,
            out failureReason);
    }

    public bool TryWriteConflict(
        in SocialConflictOutcomeReceipt receipt,
        in ReservedSocialLifeOutcome reserved,
        out PreparedSocialLifeOutcome prepared,
        out string failureReason) => Write(
        receipt,
        receipt.ResultKey,
        reserved,
        out prepared,
        out failureReason);

    public bool TryReserveApology(
        string operationId,
        int absoluteDay,
        out ReservedSocialLifeOutcome reserved,
        out string failureReason)
    {
        GameplayResultKey key;
        try
        {
            key = new GameplayResultKey(
                SocialLifeOutcomeIds.ApologyProducerId,
                new GameplayOperationId(operationId),
                0L,
                0);
        }
        catch (Exception exception) when (IsCaptureException(exception))
        {
            reserved = default;
            failureReason = "apology-result-key-invalid:" + exception.Message;
            return false;
        }
        return Reserve(
            ApologyOutcomeAdapter.CreateRequirements(
                key,
                absoluteDay,
                ledger.CurrentWorldEpoch),
            out reserved,
            out failureReason);
    }

    public bool TryWriteApology(
        in ApologyOutcomeReceipt receipt,
        in ReservedSocialLifeOutcome reserved,
        out PreparedSocialLifeOutcome prepared,
        out string failureReason) => Write(
        receipt,
        receipt.ResultKey,
        reserved,
        out prepared,
        out failureReason);

    public bool IsAcknowledgedConflictReplay(
        in SocialConflictOutcomeReceipt receipt,
        out string failureReason) => IsAcknowledgedReplay(
        receipt,
        receipt.ResultKey,
        out failureReason);

    public bool IsAcknowledgedApologyReplay(
        in ApologyOutcomeReceipt receipt,
        out string failureReason) => IsAcknowledgedReplay(
        receipt,
        receipt.ResultKey,
        out failureReason);

    public bool TryCommit(
        in PreparedSocialLifeOutcome prepared,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!prepared.IsValid)
        {
            failureReason = "social-life-prepared-token-invalid";
            return false;
        }
        OutcomeCommitResult commit = recorder.CommitPrepared(
            prepared.Token,
            0L,
            out CommittedOutcomeToken committed);
        if (!commit.Success)
        {
            recorder.CancelPrepared(prepared.Token);
            failureReason = "social-life-outcome-commit-" + commit.Code
                + ":" + commit.DetailCode;
            return false;
        }
        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        if (!delivery.Published)
        {
            failureReason = "social-life-outcome-delivery-" + delivery.Code
                + ":" + delivery.DetailCode;
            return true;
        }
        OutcomeAcknowledgeResult acknowledged = recorder.Acknowledge(committed);
        if (!acknowledged.Success)
        {
            failureReason = "social-life-outcome-acknowledge-" + acknowledged.Code;
            return true;
        }
        failureReason = string.Empty;
        return true;
    }

    public void Cancel(in ReservedSocialLifeOutcome reserved)
    {
        if (reserved.Reservation.IsValid)
            recorder.CancelReservation(reserved.Reservation);
    }

    public void Cancel(in PreparedSocialLifeOutcome prepared)
    {
        if (prepared.Token.IsValid)
            recorder.CancelPrepared(prepared.Token);
    }

    private bool Reserve(
        in OutcomeWriteRequirements requirements,
        out ReservedSocialLifeOutcome reserved,
        out string failureReason)
    {
        OutcomePrepareResult result = recorder.TryReserve(
            requirements,
            out PreparedOutcomeReservation reservation);
        if (result.Success)
        {
            reserved = new ReservedSocialLifeOutcome(
                reservation,
                requirements.ResultKey,
                false);
            failureReason = string.Empty;
            return true;
        }
        reserved = default;
        failureReason = result.Code == OutcomePrepareCode.DuplicateResult
            ? "social-life-duplicate-requires-exact-final-receipt-reconciliation"
            : "social-life-outcome-reserve-" + result.Code
                + ":" + result.DetailCode;
        return false;
    }

    private bool Write<TReceipt>(
        in TReceipt receipt,
        GameplayResultKey resultKey,
        in ReservedSocialLifeOutcome reserved,
        out PreparedSocialLifeOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        if (!reserved.IsValid || reserved.ResultKey != resultKey)
        {
            failureReason = "social-life-outcome-reservation-mismatch";
            return false;
        }
        OutcomePrepareResult result = recorder.TryWriteReserved(
            receipt,
            reserved.Reservation,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = "social-life-outcome-write-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        prepared = new PreparedSocialLifeOutcome(token, resultKey, false);
        failureReason = string.Empty;
        return true;
    }

    private bool IsCanonicalAcknowledged(
        GameplayResultKey resultKey,
        out string failureReason)
    {
        if (diagnostics.TryGetResultIdentity(resultKey, out var identity)
            && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash)
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = "social-life-duplicate-not-canonical-acknowledged";
        return false;
    }

    private bool IsAcknowledgedReplay<TReceipt>(
        in TReceipt receipt,
        GameplayResultKey resultKey,
        out string failureReason)
    {
        OutcomePrepareResult replay = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken unexpectedPrepared);
        if (unexpectedPrepared.IsValid)
        {
            recorder.CancelPrepared(unexpectedPrepared);
            failureReason = "social-life-replay-result-was-not-previously-committed";
            return false;
        }
        if (replay.Code is not OutcomePrepareCode.AlreadyCommitted
            and not OutcomePrepareCode.AlreadyPublished)
        {
            failureReason = "social-life-replay-reconciliation-" + replay.Code
                + ":" + replay.DetailCode;
            return false;
        }
        recorder.RetryPendingDeliveries(1);
        return IsCanonicalAcknowledged(resultKey, out failureReason);
    }

    private static bool IsCaptureException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
