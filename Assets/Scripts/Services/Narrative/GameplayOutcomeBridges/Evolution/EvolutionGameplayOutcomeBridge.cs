using System;

public interface IApparelChangeOutcomeCommitter
{
    bool TryPrepare(
        in ApparelChangeOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface IAcquiredTraitReactionOutcomeCommitter
{
    bool TryPrepare(
        in AcquiredTraitReactionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface IFacilityEvolutionOutcomeCommitter
{
    bool TryPrepare(
        in FacilityEvolutionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface IMemoryErasureOutcomeCommitter
{
    bool TryReserveSuccess(
        string operationId,
        int absoluteDay,
        out ReservedMemoryErasureOutcome reserved,
        out string failureReason);
    bool TryWriteReservedSuccess(
        in MemoryErasureOutcomeReceipt receipt,
        in ReservedMemoryErasureOutcome reserved,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    bool TryPrepareTerminal(
        in MemoryErasureOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in ReservedMemoryErasureOutcome reserved);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        out string failureReason);
}

public sealed class EvolutionGameplayOutcomeBridge :
    IApparelChangeOutcomeCommitter,
    IAcquiredTraitReactionOutcomeCommitter,
    IFacilityEvolutionOutcomeCommitter,
    IMemoryErasureOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public EvolutionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    bool IApparelChangeOutcomeCommitter.TryPrepare(
        in ApparelChangeOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(receipt, receipt.ResultKey, out prepared, out failureReason);

    void IApparelChangeOutcomeCommitter.Cancel(in PreparedEvolutionOutcome prepared) =>
        Cancel(prepared);

    bool IApparelChangeOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(prepared, expectedOwnerRevision, out failureReason);

    bool IAcquiredTraitReactionOutcomeCommitter.TryPrepare(
        in AcquiredTraitReactionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(receipt, receipt.ResultKey, out prepared, out failureReason);

    void IAcquiredTraitReactionOutcomeCommitter.Cancel(in PreparedEvolutionOutcome prepared) =>
        Cancel(prepared);

    bool IAcquiredTraitReactionOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(prepared, expectedOwnerRevision, out failureReason);

    bool IFacilityEvolutionOutcomeCommitter.TryPrepare(
        in FacilityEvolutionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(receipt, receipt.ResultKey, out prepared, out failureReason);

    void IFacilityEvolutionOutcomeCommitter.Cancel(in PreparedEvolutionOutcome prepared) =>
        Cancel(prepared);

    bool IFacilityEvolutionOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(prepared, expectedOwnerRevision, out failureReason);

    public bool TryReserveSuccess(
        string operationId,
        int absoluteDay,
        out ReservedMemoryErasureOutcome reserved,
        out string failureReason)
    {
        reserved = default;
        failureReason = string.Empty;
        GameplayOperationId operation;
        GameplayResultKey resultKey;
        try
        {
            operation = new GameplayOperationId(operationId);
            resultKey = new GameplayResultKey(
                EvolutionOutcomeIds.MemoryErasureProducerId,
                operation,
                0L,
                0);
        }
        catch (Exception exception) when (IsCaptureException(exception))
        {
            failureReason = "memory-erasure-outcome-key-invalid:" + exception.Message;
            return false;
        }

        OutcomeWriteRequirements requirements =
            MemoryErasureOutcomeAdapter.CreateRequirements(
                resultKey,
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                hasPhysicalCommit: true,
                ledger.CurrentWorldEpoch);
        OutcomePrepareResult result = recorder.TryReserve(
            requirements,
            out PreparedOutcomeReservation reservation);
        if (!result.Success)
        {
            if (result.Code == OutcomePrepareCode.DuplicateResult
                && IsCommittedOrPublished(resultKey))
            {
                reserved = default;
                return true;
            }
            failureReason = Format("memory-erasure-outcome-reserve", result);
            return false;
        }
        reserved = new ReservedMemoryErasureOutcome(reservation, operation);
        return true;
    }

    public bool TryWriteReservedSuccess(
        in MemoryErasureOutcomeReceipt receipt,
        in ReservedMemoryErasureOutcome reserved,
        out PreparedEvolutionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        if (!reserved.IsValid)
        {
            if (IsCommittedOrPublished(receipt.ResultKey))
            {
                prepared = new PreparedEvolutionOutcome(
                    default,
                    receipt.ResultKey,
                    alreadyCommitted: true);
                return true;
            }
            failureReason = "memory-erasure-outcome-reservation-invalid";
            return false;
        }
        if (reserved.OperationId != receipt.OperationId
            || receipt.Result.Status != MemoryErasureSealUseStatus.Succeeded)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = "memory-erasure-outcome-reservation-owner-mismatch";
            return false;
        }
        OutcomePrepareResult result = recorder.TryWriteReserved(
            receipt,
            reserved.Reservation,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = Format("memory-erasure-outcome-write", result);
            return false;
        }
        prepared = new PreparedEvolutionOutcome(
            token,
            receipt.ResultKey,
            alreadyCommitted: false);
        return true;
    }

    public bool TryPrepareTerminal(
        in MemoryErasureOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) =>
        Prepare(receipt, receipt.ResultKey, out prepared, out failureReason);

    public void Cancel(in ReservedMemoryErasureOutcome reserved)
    {
        if (reserved.IsValid)
            recorder.CancelReservation(reserved.Reservation);
    }

    public void Cancel(in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    public bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        out string failureReason) => Commit(prepared, 0L, out failureReason);

    private bool Prepare<TReceipt>(
        in TReceipt receipt,
        GameplayResultKey resultKey,
        out PreparedEvolutionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            if (result.Code == OutcomePrepareCode.DuplicateResult
                && IsCommittedOrPublished(resultKey))
            {
                prepared = new PreparedEvolutionOutcome(
                    default,
                    resultKey,
                    alreadyCommitted: true);
                return true;
            }
            failureReason = Format("evolution-outcome-prepare", result);
            return false;
        }
        prepared = new PreparedEvolutionOutcome(token, resultKey, false);
        return true;
    }

    private bool Commit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!prepared.IsValid)
        {
            failureReason = "evolution-outcome-prepared-token-invalid";
            return false;
        }
        if (prepared.AlreadyCommitted)
        {
            recorder.RetryPendingDeliveries(1);
            return true;
        }
        OutcomeCommitResult result = recorder.CommitPrepared(
            prepared.Prepared,
            expectedOwnerRevision,
            out CommittedOutcomeToken committed);
        if (!result.Success)
        {
            recorder.CancelPrepared(prepared.Prepared);
            failureReason = "evolution-outcome-commit-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        if (delivery.Published)
            recorder.Acknowledge(committed);
        return true;
    }

    private void CancelPrepared(in PreparedEvolutionOutcome prepared)
    {
        if (prepared.Prepared.IsValid)
            recorder.CancelPrepared(prepared.Prepared);
    }

    private bool IsCommittedOrPublished(GameplayResultKey resultKey)
    {
        var rows = diagnostics.GetOutboxSnapshot();
        for (int index = 0; index < rows.Count; index++)
        {
            GameplayOutcomeOutboxSnapshot row = rows[index];
            if (row.ResultKey == resultKey
                && row.Lifecycle >= GameplayOutcomeRecordLifecycle.Committed)
            {
                return true;
            }
        }
        return false;
    }

    private static string Format(string prefix, OutcomePrepareResult result) =>
        prefix + "-" + result.Code + ":" + result.DetailCode;

    private static bool IsCaptureException(Exception exception) =>
        exception is ArgumentException
            or InvalidOperationException
            or OverflowException;
}
