using System;
using DungeonStory.Foundation;

public sealed class GameplayOutcomeRecorder : IGameplayOutcomeRecorder
{
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeRegistry registry;
    private readonly IGameEventBus eventBus;

    public GameplayOutcomeRecorder(
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRegistry registry,
        IGameEventBus eventBus)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    [GameplayInternalOnly(
        "Reserves narrative-ledger capacity before an authoritative domain commit.",
        "Registered gameplay outcome producer transaction adapters")]
    public OutcomePrepareResult TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedOutcomeToken prepared)
    {
        prepared = default;
        if (!registry.TryGetAdapter<TReceipt>(out IGameplayOutcomeAdapter<TReceipt> adapter))
            return new OutcomePrepareResult(OutcomePrepareCode.AdapterMissing, "receipt-adapter-missing");
        OutcomePrepareResult measured;
        OutcomeWriteRequirements requirements;
        try
        {
            measured = adapter.TryGetRequirements(
                receipt,
                ledger.CurrentWorldEpoch,
                out requirements);
        }
        catch (Exception exception) when (IsRecoverableCaptureException(exception))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "receipt-requirements-fault");
        }
        if (!measured.Success)
            return measured;
        if (!SupportsOutcomeType(adapter, requirements.OutcomeTypeId))
            return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "adapter-outcome-type-mismatch");
        OutcomePrepareResult reserved = TryReserve(requirements, out PreparedOutcomeReservation reservation);
        if (!reserved.Success)
        {
            if (reserved.Code == OutcomePrepareCode.DuplicateResult
                && reserved.Existing.HasCanonicalPayloadHash)
            {
                return TryReconcileDuplicate(
                    receipt,
                    adapter,
                    requirements,
                    reserved.Existing);
            }
            return reserved;
        }
        OutcomePrepareResult written = TryWriteReserved(receipt, reservation, out prepared);
        if (!written.Success)
            CancelReservation(reservation);
        return written;
    }

    [GameplayInternalOnly(
        "Capacity-only reservation for a detached authoritative receipt preview.",
        "Registered gameplay outcome producer transaction adapters")]
    public OutcomePrepareResult TryReserve(
        in OutcomeWriteRequirements requirements,
        out PreparedOutcomeReservation reservation) =>
        ledger.TryReserveCore(requirements, out reservation);

    [GameplayInternalOnly(
        "Writes a detached receipt into its already-owned bounded reservation.",
        "Registered gameplay outcome producer transaction adapters")]
    public OutcomePrepareResult TryWriteReserved<TReceipt>(
        in TReceipt receipt,
        in PreparedOutcomeReservation reservation,
        out PreparedOutcomeToken prepared)
    {
        prepared = default;
        if (!registry.TryGetAdapter<TReceipt>(out IGameplayOutcomeAdapter<TReceipt> adapter))
            return new OutcomePrepareResult(OutcomePrepareCode.AdapterMissing, "receipt-adapter-missing");
        OutcomePrepareResult measured;
        OutcomeWriteRequirements requirements;
        try
        {
            measured = adapter.TryGetRequirements(
                receipt,
                ledger.CurrentWorldEpoch,
                out requirements);
        }
        catch (Exception exception) when (IsRecoverableCaptureException(exception))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "receipt-requirements-fault");
        }
        if (!measured.Success)
            return measured;
        if (requirements.ResultKey != reservation.ResultKey
            || requirements.WorldEpoch != reservation.WorldEpoch
            || !SupportsOutcomeType(adapter, requirements.OutcomeTypeId))
        {
            return new OutcomePrepareResult(OutcomePrepareCode.ReservationInvalid, "reserved-receipt-identity-mismatch");
        }
        if (!ledger.TryGetReservedPage(reservation, out GameplayOutcomeValuePage page))
            return new OutcomePrepareResult(OutcomePrepareCode.ReservationInvalid, "reservation-not-active");

        OutcomeWriteBuilder builder = new OutcomeWriteBuilder(page);
        OutcomePrepareResult write;
        try
        {
            write = adapter.TryWrite(receipt, ref builder);
        }
        catch (Exception exception) when (IsRecoverableCaptureException(exception))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "receipt-write-fault");
        }
        if (!write.Success || !builder.CountsMatch(requirements))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                write.Success ? "adapter-count-contract-mismatch" : write.DetailCode);
        }
        try
        {
            return ledger.CompletePreparation(reservation, requirements, out prepared);
        }
        catch (Exception exception) when (IsRecoverableCaptureException(exception))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "receipt-validation-fault");
        }
    }

    [GameplayInternalOnly(
        "Releases an unpublished capacity reservation after producer cancellation.",
        "Registered gameplay outcome producer transaction adapters")]
    public void CancelReservation(in PreparedOutcomeReservation reservation) =>
        ledger.CancelReservationCore(reservation);

    [GameplayInternalOnly(
        "Releases an unpublished prepared result after its domain transaction cancels.",
        "Registered gameplay outcome producer transaction adapters")]
    public void CancelPrepared(in PreparedOutcomeToken prepared) =>
        ledger.CancelPreparedCore(prepared);

    [GameplayInternalOnly(
        "Transfers a prepared result into the durable in-memory outbox inside a domain commit boundary.",
        "Registered gameplay outcome producer transaction adapters")]
    public OutcomeCommitResult CommitPrepared(
        in PreparedOutcomeToken prepared,
        long expectedOwnerRevision,
        out CommittedOutcomeToken committed) =>
        ledger.CommitPreparedCore(prepared, expectedOwnerRevision, out committed);

    [GameplayInternalOnly(
        "Atomically transfers a bounded prepared-result batch into the durable in-memory outbox inside one domain commit boundary.",
        "Registered gameplay outcome producer transaction adapters")]
    public OutcomeCommitResult CommitPreparedBatch(
        PreparedOutcomeToken[] prepared,
        long[] expectedOwnerRevisions,
        CommittedOutcomeToken[] committed) =>
        ledger.CommitPreparedBatchCore(
            prepared,
            expectedOwnerRevisions,
            committed);

    [GameplayInternalOnly(
        "Publishes only an already-committed outbox result and never repeats gameplay effects.",
        "Registered gameplay outcome producer transaction adapters and delivery pump")]
    public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed)
    {
        OutcomeDeliveryResult result = ledger.TryPublishCore(committed);
        if (result.Code == OutcomeDeliveryCode.Published)
            NotifyPublished(result.OutcomeId.Sequence);
        return result;
    }

    [GameplayInternalOnly(
        "Acknowledges an exact result identity after ledger publication.",
        "Gameplay outcome delivery pump")]
    public OutcomeAcknowledgeResult Acknowledge(in CommittedOutcomeToken committed) =>
        ledger.AcknowledgeCore(committed);

    [GameplayInternalOnly(
        "Retries committed outbox delivery without re-running its domain command.",
        "Gameplay outcome delivery pump and save-restore completion hook")]
    public int RetryPendingDeliveries(int maximumCount)
    {
        if (maximumCount <= 0)
            return 0;
        int delivered = 0;
        int nextPage = 0;
        while (delivered < maximumCount
            && ledger.TryGetPendingCommittedToken(nextPage, out int pageIndex, out CommittedOutcomeToken token))
        {
            nextPage = pageIndex + 1;
            OutcomeDeliveryResult result = TryDeliver(token);
            if (!result.Published)
                continue;
            OutcomeAcknowledgeResult acknowledged = Acknowledge(token);
            if (acknowledged.Success)
                delivered++;
        }
        return delivered;
    }

    private void NotifyPublished(long sequence)
    {
        try
        {
            GameplayOutcomeLedgerDiagnostics diagnostics = ledger.GetDiagnostics();
            eventBus.Publish(new GameplayOutcomePublishedRangeEvent(
                sequence,
                sequence,
                diagnostics.Revision));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            ledger.RecordNotificationFault();
        }
    }

    private OutcomePrepareResult TryReconcileDuplicate<TReceipt>(
        in TReceipt receipt,
        IGameplayOutcomeAdapter<TReceipt> adapter,
        in OutcomeWriteRequirements requirements,
        in GameplayOutcomeReplayIdentity baseline)
    {
        OutcomePrepareResult started = ledger.TryCreateReplayComparisonCore(
            requirements,
            out GameplayOutcomeReplayIdentity current,
            out GameplayOutcomeValuePage comparison);
        if (!started.Success)
            return started;
        OutcomeWriteBuilder builder = new OutcomeWriteBuilder(comparison);
        try
        {
            OutcomePrepareResult written = adapter.TryWrite(receipt, ref builder);
            if (!written.Success || !builder.CountsMatch(requirements))
            {
                return new OutcomePrepareResult(
                    OutcomePrepareCode.AdapterWriteFailed,
                    written.Success ? "adapter-count-contract-mismatch" : written.DetailCode,
                    current);
            }
            return ledger.CompleteReplayComparisonCore(comparison, baseline);
        }
        catch (Exception exception) when (IsRecoverableCaptureException(exception))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "replay-comparison-fault",
                current);
        }
    }

    private static bool IsRecoverableCaptureException(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;

    private static bool SupportsOutcomeType<TReceipt>(
        IGameplayOutcomeAdapter<TReceipt> adapter,
        GameplayOutcomeTypeId outcomeTypeId) =>
        adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter
            ? dynamicAdapter.SupportsOutcomeType(outcomeTypeId)
            : adapter.OutcomeTypeId == outcomeTypeId;
}
