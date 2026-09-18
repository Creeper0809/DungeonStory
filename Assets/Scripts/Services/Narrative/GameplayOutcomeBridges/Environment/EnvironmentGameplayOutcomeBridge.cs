using System;

public readonly struct PreparedEnvironmentOutcome
{
    internal PreparedEnvironmentOutcome(PreparedOutcomeToken token) => Token = token;
    internal PreparedOutcomeToken Token { get; }
    public GameplayResultKey ResultKey => Token.ResultKey;
    public bool IsValid => Token.IsValid;
}

public readonly struct ReservedEnvironmentOutcome
{
    internal ReservedEnvironmentOutcome(PreparedOutcomeReservation reservation) =>
        Reservation = reservation;
    internal PreparedOutcomeReservation Reservation { get; }
    public GameplayResultKey ResultKey => Reservation.ResultKey;
    public bool IsValid => Reservation.IsValid;
}

public readonly struct EnvironmentOutcomeReservationSpec
{
    public EnvironmentOutcomeReservationSpec(
        GameplayResultKey resultKey,
        GameplayOutcomeTypeId outcomeTypeId,
        int absoluteDay,
        GameplayOutcomeStatus status,
        long ownerRevision,
        int participantCount,
        int metricCount,
        int subjectCount,
        int tagCount,
        int provenanceCount,
        int factCount)
    {
        ResultKey = resultKey;
        OutcomeTypeId = outcomeTypeId;
        AbsoluteDay = absoluteDay;
        Status = status;
        OwnerRevision = ownerRevision;
        ParticipantCount = participantCount;
        MetricCount = metricCount;
        SubjectCount = subjectCount;
        TagCount = tagCount;
        ProvenanceCount = provenanceCount;
        FactCount = factCount;
    }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public long OwnerRevision { get; }
    public int ParticipantCount { get; }
    public int MetricCount { get; }
    public int SubjectCount { get; }
    public int TagCount { get; }
    public int ProvenanceCount { get; }
    public int FactCount { get; }
}

public enum EnvironmentOutcomeCommitPhase
{
    Rejected = 0,
    CommittedPendingDelivery = 1,
    PublishedAwaitingAcknowledgement = 2,
    PublishedAcknowledged = 3
}

public readonly struct EnvironmentOutcomeCommitResult
{
    public EnvironmentOutcomeCommitResult(
        EnvironmentOutcomeCommitPhase phase,
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        string canonicalDigest,
        string detailCode)
    {
        Phase = phase;
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        CanonicalDigest = canonicalDigest ?? string.Empty;
        DetailCode = detailCode ?? string.Empty;
    }
    public EnvironmentOutcomeCommitPhase Phase { get; }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public string CanonicalDigest { get; }
    public string DetailCode { get; }
    public bool DurablyCommitted => Phase != EnvironmentOutcomeCommitPhase.Rejected;
    public bool CanClearOwnerPending =>
        Phase == EnvironmentOutcomeCommitPhase.PublishedAcknowledged
        && ResultKey.IsValid
        && OutcomeId.IsValid
        && CanonicalDigest.Length == 64;
}

public interface IEnvironmentGameplayOutcomeCommitter
{
    bool TryReserve(
        in EnvironmentOutcomeReservationSpec spec,
        out ReservedEnvironmentOutcome reserved,
        out string failureReason);
    bool TryWriteReserved<TReceipt>(
        in TReceipt receipt,
        in ReservedEnvironmentOutcome reserved,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt;
    bool TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt;
    EnvironmentOutcomeCommitResult Commit(
        in PreparedEnvironmentOutcome prepared,
        long expectedOwnerRevision);
    EnvironmentOutcomeCommitResult Reconcile(GameplayResultKey resultKey);
    bool IsCanonicalAcknowledgedReplay<TReceipt>(
        in TReceipt receipt,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt;
    void Cancel(in PreparedEnvironmentOutcome prepared);
    void Cancel(in ReservedEnvironmentOutcome reserved);
}

public sealed class EnvironmentGameplayOutcomeBridge : IEnvironmentGameplayOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly GameplayOutcomeLedger ledger;

    public EnvironmentGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        GameplayOutcomeLedger ledger)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public bool TryReserve(
        in EnvironmentOutcomeReservationSpec spec,
        out ReservedEnvironmentOutcome reserved,
        out string failureReason)
    {
        var requirements = new OutcomeWriteRequirements(
            spec.ResultKey,
            spec.OutcomeTypeId,
            spec.AbsoluteDay,
            spec.Status,
            ledger.CurrentWorldEpoch,
            spec.OwnerRevision,
            spec.ParticipantCount,
            spec.MetricCount,
            spec.SubjectCount,
            spec.TagCount,
            anchorCount: 0,
            provenanceCount: spec.ProvenanceCount,
            factCount: spec.FactCount);
        OutcomePrepareResult result = recorder.TryReserve(
            requirements,
            out PreparedOutcomeReservation reservation);
        if (result.Success)
        {
            reserved = new ReservedEnvironmentOutcome(reservation);
            failureReason = string.Empty;
            return true;
        }
        reserved = default;
        failureReason = "environment-outcome-reserve-" + result.Code
            + ":" + result.DetailCode;
        return false;
    }

    public bool TryWriteReserved<TReceipt>(
        in TReceipt receipt,
        in ReservedEnvironmentOutcome reserved,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        prepared = default;
        if (!reserved.IsValid || receipt.Payload.ResultKey != reserved.ResultKey)
        {
            failureReason = "environment-outcome-reservation-mismatch";
            return false;
        }
        OutcomePrepareResult result = recorder.TryWriteReserved(
            receipt,
            reserved.Reservation,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            recorder.CancelReservation(reserved.Reservation);
            failureReason = "environment-outcome-write-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        prepared = new PreparedEnvironmentOutcome(token);
        failureReason = string.Empty;
        return true;
    }

    public bool TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (result.Success)
        {
            prepared = new PreparedEnvironmentOutcome(token);
            failureReason = string.Empty;
            return true;
        }
        prepared = default;
        failureReason = "environment-outcome-prepare-" + result.Code
            + ":" + result.DetailCode;
        return false;
    }

    public EnvironmentOutcomeCommitResult Commit(
        in PreparedEnvironmentOutcome prepared,
        long expectedOwnerRevision)
    {
        if (!prepared.IsValid)
            return Rejected(default, "environment-outcome-prepared-token-invalid");

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
            // CommitPrepared is specified as a no-throw boundary.  If a host
            // violation nevertheless occurs, the commit point is ambiguous:
            // reconciliation must win over domain rollback.
            EnvironmentOutcomeCommitResult reconciled = Reconcile(
                prepared.ResultKey);
            return reconciled.DurablyCommitted
                ? reconciled
                : new EnvironmentOutcomeCommitResult(
                    EnvironmentOutcomeCommitPhase.CommittedPendingDelivery,
                    prepared.ResultKey,
                    default,
                    string.Empty,
                    "environment-outcome-commit-exception-ambiguous:"
                    + exception.GetType().Name);
        }
        if (!commit.Success)
        {
            recorder.CancelPrepared(prepared.Token);
            return Rejected(
                prepared.ResultKey,
                "environment-outcome-commit-" + commit.Code + ":" + commit.DetailCode);
        }

        OutcomeDeliveryResult delivery;
        try
        {
            delivery = recorder.TryDeliver(committed);
        }
        catch (Exception exception)
        {
            return new EnvironmentOutcomeCommitResult(
                EnvironmentOutcomeCommitPhase.CommittedPendingDelivery,
                committed.ResultKey,
                committed.OutcomeId,
                string.Empty,
                "environment-outcome-delivery-exception:"
                + exception.GetType().Name);
        }
        if (!delivery.Published)
        {
            // Delivery is durable in the gameplay outbox. The domain transaction
            // remains committed and the retry pump will finish publication.
            return Snapshot(
                EnvironmentOutcomeCommitPhase.CommittedPendingDelivery,
                committed.ResultKey,
                committed.OutcomeId,
                "environment-outcome-delivery-" + delivery.Code + ":" + delivery.DetailCode);
        }
        OutcomeAcknowledgeResult acknowledge;
        try
        {
            acknowledge = recorder.Acknowledge(committed);
        }
        catch (Exception exception)
        {
            return Snapshot(
                EnvironmentOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
                committed.ResultKey,
                committed.OutcomeId,
                "environment-outcome-acknowledge-exception:"
                + exception.GetType().Name);
        }
        return Snapshot(
            acknowledge.Success
                ? EnvironmentOutcomeCommitPhase.PublishedAcknowledged
                : EnvironmentOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
            committed.ResultKey,
            committed.OutcomeId,
            acknowledge.Success
                ? string.Empty
                : "environment-outcome-acknowledge-" + acknowledge.Code);
    }

    public EnvironmentOutcomeCommitResult Reconcile(GameplayResultKey resultKey)
    {
        if (!resultKey.IsValid)
            return Rejected(resultKey, "environment-reconcile-result-key-invalid");
        try
        {
            recorder.RetryPendingDeliveries(1);
            if (!diagnostics.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity identity)
                || identity.ResultKey != resultKey)
                return Rejected(resultKey, "environment-reconcile-result-missing");
            EnvironmentOutcomeCommitPhase phase = identity.State switch
            {
                GameplayOutcomeReplayState.PublishedAcknowledged =>
                    EnvironmentOutcomeCommitPhase.PublishedAcknowledged,
                GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement =>
                    EnvironmentOutcomeCommitPhase.PublishedAwaitingAcknowledgement,
                _ => EnvironmentOutcomeCommitPhase.CommittedPendingDelivery
            };
            return new EnvironmentOutcomeCommitResult(
                phase,
                resultKey,
                identity.OutcomeId,
                identity.CanonicalPayloadHash,
                string.Empty);
        }
        catch (Exception exception)
        {
            // Callers reach reconciliation after a potentially successful
            // commit.  Treat diagnostic failure as durable-unknown and retain
            // their save-owned pending row; rolling back would create a split
            // brain if the core commit already crossed its point of no return.
            return new EnvironmentOutcomeCommitResult(
                EnvironmentOutcomeCommitPhase.CommittedPendingDelivery,
                resultKey,
                default,
                string.Empty,
                "environment-reconcile-exception:"
                + exception.GetType().Name);
        }
    }

    public bool IsCanonicalAcknowledgedReplay<TReceipt>(
        in TReceipt receipt,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        OutcomePrepareResult replay = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken unexpected);
        if (unexpected.IsValid)
        {
            recorder.CancelPrepared(unexpected);
            failureReason = "environment-replay-result-was-not-previously-committed";
            return false;
        }
        if (replay.Code is not OutcomePrepareCode.AlreadyCommitted
            and not OutcomePrepareCode.AlreadyPublished
            and not OutcomePrepareCode.AlreadyTerminal)
        {
            failureReason = "environment-replay-reconciliation-" + replay.Code
                + ":" + replay.DetailCode;
            return false;
        }

        recorder.RetryPendingDeliveries(1);
        GameplayResultKey key = receipt.Payload.ResultKey;
        if (diagnostics.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity identity)
            && identity.ResultKey == key
            && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash)
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = "environment-replay-not-canonical-acknowledged";
        return false;
    }

    public void Cancel(in PreparedEnvironmentOutcome prepared)
    {
        if (prepared.IsValid)
            recorder.CancelPrepared(prepared.Token);
    }

    public void Cancel(in ReservedEnvironmentOutcome reserved)
    {
        if (reserved.IsValid)
            recorder.CancelReservation(reserved.Reservation);
    }

    private EnvironmentOutcomeCommitResult Snapshot(
        EnvironmentOutcomeCommitPhase fallbackPhase,
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        string detailCode)
    {
        try
        {
            if (diagnostics.TryGetResultIdentity(resultKey, out GameplayOutcomeReplayIdentity identity)
                && identity.ResultKey == resultKey)
            {
                EnvironmentOutcomeCommitPhase phase = identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                    ? EnvironmentOutcomeCommitPhase.PublishedAcknowledged
                    : identity.State == GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
                        ? EnvironmentOutcomeCommitPhase.PublishedAwaitingAcknowledgement
                        : fallbackPhase;
                return new EnvironmentOutcomeCommitResult(
                    phase,
                    resultKey,
                    identity.OutcomeId,
                    identity.CanonicalPayloadHash,
                    detailCode);
            }
        }
        catch (Exception)
        {
            // The authoritative commit has already succeeded. Diagnostics are
            // observational and must never trigger a domain rollback.
        }
        return new EnvironmentOutcomeCommitResult(
            fallbackPhase,
            resultKey,
            outcomeId,
            string.Empty,
            detailCode);
    }

    private static EnvironmentOutcomeCommitResult Rejected(
        GameplayResultKey resultKey,
        string detailCode) => new(
        EnvironmentOutcomeCommitPhase.Rejected,
        resultKey,
        default,
        string.Empty,
        detailCode);
}
