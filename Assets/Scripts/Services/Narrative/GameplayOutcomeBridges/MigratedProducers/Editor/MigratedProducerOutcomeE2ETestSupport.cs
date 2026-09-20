#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;

internal enum MigratedProducerOutcomeFaultMode
{
    None = 0,
    RejectReservation = 1,
    RejectCommit = 2
}

internal sealed class MigratedProducerOutcomeDeliveryFault
{
    private readonly HashSet<GameplayOutcomeId> preparedValidations = new();

    internal MigratedProducerOutcomeKind TargetKind { get; set; }
    internal bool Enabled { get; set; }

    internal bool ShouldThrow(in GameplayOutcomeReadView outcome)
    {
        if (!Enabled
            || MigratedProducerOutcomeCatalog.Get(TargetKind).OutcomeTypeId
                != outcome.OutcomeTypeId)
        {
            return false;
        }

        // Descriptor validation runs once while the receipt is prepared and
        // again only after the outbox commit when delivery is attempted. Let
        // preparation succeed, then fail the delivery boundary itself.
        if (preparedValidations.Add(outcome.OutcomeId))
        {
            return false;
        }
        return true;
    }
}

internal sealed class FaultingMigratedProducerOutcomeDescriptorCatalog :
    IGameplayOutcomeDescriptorCatalog
{
    private readonly IGameplayOutcomeDescriptor[] descriptors;

    internal FaultingMigratedProducerOutcomeDescriptorCatalog(
        IKoreanJosaFormatter josa,
        MigratedProducerOutcomeDeliveryFault fault)
    {
        if (josa == null)
            throw new ArgumentNullException(nameof(josa));
        if (fault == null)
            throw new ArgumentNullException(nameof(fault));

        IReadOnlyList<MigratedProducerOutcomeDefinition> definitions =
            MigratedProducerOutcomeCatalog.Definitions;
        descriptors = new IGameplayOutcomeDescriptor[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
        {
            var inner = new MigratedProducerOutcomeDescriptor(
                definitions[index],
                josa);
            descriptors[index] = new FaultingMigratedProducerOutcomeDescriptor(
                inner,
                fault);
        }
    }

    public IReadOnlyList<IGameplayOutcomeDescriptor> Descriptors => descriptors;
}

internal sealed class FaultingMigratedProducerOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly IGameplayOutcomeDescriptor inner;
    private readonly MigratedProducerOutcomeDeliveryFault fault;

    internal FaultingMigratedProducerOutcomeDescriptor(
        IGameplayOutcomeDescriptor inner,
        MigratedProducerOutcomeDeliveryFault fault)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.fault = fault ?? throw new ArgumentNullException(nameof(fault));
    }

    public GameplayOutcomeTypeId OutcomeTypeId => inner.OutcomeTypeId;
    public INarrativePerspectiveProjector PerspectiveProjector =>
        inner.PerspectiveProjector;
    public IOutcomeMemoryPolicy MemoryPolicy => inner.MemoryPolicy;
    public IOutcomePerceptionPolicy PerceptionPolicy => inner.PerceptionPolicy;
    public IOutcomeMemoryConsolidator MemoryConsolidator =>
        inner.MemoryConsolidator;
    public bool IsKnownRole(GameplayRoleId roleId) => inner.IsKnownRole(roleId);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        inner.IsKnownMetric(metricId, unitId);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (fault.ShouldThrow(outcome))
        {
            throw new InvalidOperationException(
                "migrated-producer-e2e-injected-delivery-fault");
        }
        return inner.Validate(outcome);
    }
}

internal sealed class MigratedProducerOutcomeProbeTransaction :
    IMigratedProducerOutcomeTransaction,
    IMigratedProducerOutcomeExternalBatchTransaction
{
    private readonly IMigratedProducerOutcomeTransaction inner;
    private readonly IMigratedProducerOutcomeExternalBatchTransaction external;
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly Dictionary<GameplayResultKey, MigratedProducerOutcomeKind>
        reservedKinds = new();
    private readonly Dictionary<MigratedProducerOutcomeKind, int> reservations =
        new();
    private readonly Dictionary<MigratedProducerOutcomeKind, int> commitAttempts =
        new();
    private readonly Dictionary<MigratedProducerOutcomeKind, int> commits = new();
    private readonly Dictionary<MigratedProducerOutcomeKind, GameplayResultKey>
        latestCommittedKeys = new();

    internal MigratedProducerOutcomeProbeTransaction(
        IMigratedProducerOutcomeTransaction inner,
        IGameplayOutcomeRecorder recorder = null)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        external = inner as IMigratedProducerOutcomeExternalBatchTransaction;
        this.recorder = recorder;
    }

    internal MigratedProducerOutcomeKind TargetKind { get; set; }
    internal MigratedProducerOutcomeFaultMode FaultMode { get; set; }

    internal int ReservationCount(MigratedProducerOutcomeKind kind) =>
        reservations.TryGetValue(kind, out int count) ? count : 0;

    internal int CommitCount(MigratedProducerOutcomeKind kind) =>
        commits.TryGetValue(kind, out int count) ? count : 0;

    internal int CommitAttemptCount(MigratedProducerOutcomeKind kind) =>
        commitAttempts.TryGetValue(kind, out int count) ? count : 0;

    internal GameplayResultKey LatestCommittedKey(
        MigratedProducerOutcomeKind kind) =>
        latestCommittedKeys.TryGetValue(kind, out GameplayResultKey key)
            ? key
            : default;

    internal void ClearObservations()
    {
        reservations.Clear();
        commitAttempts.Clear();
        commits.Clear();
        latestCommittedKeys.Clear();
        reservedKinds.Clear();
    }

    public bool TryReserve(
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        GameplayOutcomeStatus status,
        int participantCount,
        int metricCount,
        int subjectCount,
        int additionalFactCount,
        out PreparedMigratedProducerOutcome prepared,
        out string failureReason)
    {
        Increment(reservations, kind);
        if (FaultMode == MigratedProducerOutcomeFaultMode.RejectReservation
            && kind == TargetKind)
        {
            prepared = default;
            failureReason = "migrated-producer-e2e-injected-reservation-rejection";
            return false;
        }

        bool reserved = inner.TryReserve(
            kind,
            ownerIdentity,
            absoluteDay,
            status,
            participantCount,
            metricCount,
            subjectCount,
            additionalFactCount,
            out prepared,
            out failureReason);
        if (reserved)
            reservedKinds[prepared.ResultKey] = kind;
        return reserved;
    }

    public MigratedProducerOutcomePayloadBuilder CreatePayloadBuilder(
        in PreparedMigratedProducerOutcome prepared) =>
        inner.CreatePayloadBuilder(prepared);

    public MigratedProducerOutcomeCommitResult Commit(
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeReceipt receipt)
    {
        MigratedProducerOutcomeKind kind = KindOf(prepared);
        Increment(commitAttempts, kind);
        if (FaultMode == MigratedProducerOutcomeFaultMode.RejectCommit
            && kind == TargetKind)
        {
            inner.Cancel(prepared);
            return Rejected(
                prepared.ResultKey,
                "migrated-producer-e2e-injected-commit-rejection");
        }

        MigratedProducerOutcomeCommitResult result = inner.Commit(
            prepared,
            receipt);
        if (result.DurablyCommitted)
        {
            Increment(commits, kind);
            latestCommittedKeys[kind] = result.ResultKey;
        }
        return result;
    }

    public bool CommitBatch(
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        CountAttempts(prepared);
        if (ShouldReject(prepared))
        {
            CancelAll(prepared);
            failureReason = "migrated-producer-e2e-injected-batch-commit-rejection";
            return false;
        }

        bool committed = inner.CommitBatch(
            prepared,
            receipts,
            results,
            out failureReason);
        if (committed)
            CountCommitted(prepared, results, offset: 0);
        return committed;
    }

    public bool CommitWithExternalPrepared(
        in PreparedOutcomeToken externalPrepared,
        long expectedExternalOwnerRevision,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        if (external == null)
        {
            failureReason = "migrated-producer-e2e-external-batch-unavailable";
            return false;
        }
        CountAttempts(prepared);
        if (ShouldReject(prepared))
        {
            if (externalPrepared.IsValid && recorder != null)
                recorder.CancelPrepared(externalPrepared);
            CancelAll(prepared);
            failureReason =
                "migrated-producer-e2e-injected-external-batch-commit-rejection";
            return false;
        }

        bool committed = external.CommitWithExternalPrepared(
            externalPrepared,
            expectedExternalOwnerRevision,
            prepared,
            receipts,
            results,
            out failureReason);
        if (committed)
            CountCommitted(prepared, results, offset: 0);
        return committed;
    }

    public void Cancel(in PreparedMigratedProducerOutcome prepared) =>
        inner.Cancel(prepared);

    private bool ShouldReject(PreparedMigratedProducerOutcome[] prepared)
    {
        if (FaultMode != MigratedProducerOutcomeFaultMode.RejectCommit
            || prepared == null)
        {
            return false;
        }
        for (int index = 0; index < prepared.Length; index++)
        {
            if (KindOf(prepared[index]) == TargetKind)
                return true;
        }
        return false;
    }

    private void CountAttempts(PreparedMigratedProducerOutcome[] prepared)
    {
        if (prepared == null)
            return;
        for (int index = 0; index < prepared.Length; index++)
            Increment(commitAttempts, KindOf(prepared[index]));
    }

    private void CancelAll(PreparedMigratedProducerOutcome[] prepared)
    {
        if (prepared == null)
            return;
        for (int index = 0; index < prepared.Length; index++)
            inner.Cancel(prepared[index]);
    }

    private void CountCommitted(
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeCommitResult[] results,
        int offset)
    {
        if (prepared == null || results == null)
            return;
        int count = Math.Min(prepared.Length, results.Length - offset);
        for (int index = 0; index < count; index++)
        {
            if (results[index + offset].DurablyCommitted)
            {
                MigratedProducerOutcomeKind kind = KindOf(prepared[index]);
                Increment(commits, kind);
                latestCommittedKeys[kind] = results[index + offset].ResultKey;
            }
        }
    }

    private MigratedProducerOutcomeKind KindOf(
        in PreparedMigratedProducerOutcome prepared) =>
        reservedKinds.TryGetValue(
            prepared.ResultKey,
            out MigratedProducerOutcomeKind kind)
                ? kind
                : throw new InvalidOperationException(
                    "The E2E probe did not observe this migrated producer reservation.");

    private static void Increment(
        Dictionary<MigratedProducerOutcomeKind, int> counts,
        MigratedProducerOutcomeKind kind) =>
        counts[kind] = counts.TryGetValue(kind, out int count)
            ? count + 1
            : 1;

    private static MigratedProducerOutcomeCommitResult Rejected(
        GameplayResultKey key,
        string detail) => new(
        false,
        key,
        default,
        string.Empty,
        detail);
}

internal static class MigratedProducerOutcomeE2EAssertions
{
    internal static GameplayOutcomeReplayIdentity RequireExact(
        MigratedProducerOutcomeEditorFixture fixture,
        MigratedProducerOutcomeKind kind,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        if (fixture == null)
            throw new ArgumentNullException(nameof(fixture));
        if (probe == null)
            throw new ArgumentNullException(nameof(probe));
        if (probe.ReservationCount(kind) <= 0 || probe.CommitCount(kind) <= 0)
        {
            throw new InvalidOperationException(
                "The production entry did not reserve and durably commit the expected migrated outcome: "
                + kind);
        }

        MigratedProducerOutcomeDefinition definition =
            MigratedProducerOutcomeCatalog.Get(kind);
        GameplayResultKey resultKey = probe.LatestCommittedKey(kind);
        if (resultKey.IsValid
            && fixture.Ledger.TryGetResultIdentity(
                resultKey,
                out GameplayOutcomeReplayIdentity identity)
            && fixture.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact.outcomeTypeId == definition.OutcomeTypeId.Value)
        {
            return identity;
        }

        throw new InvalidOperationException(
            "The production entry did not leave an exact migrated outcome in the ledger: "
            + kind);
    }

    internal static void RequirePendingThenRetry(
        MigratedProducerOutcomeEditorFixture fixture,
        MigratedProducerOutcomeKind kind,
        MigratedProducerOutcomeProbeTransaction probe,
        MigratedProducerOutcomeDeliveryFault fault)
    {
        GameplayResultKey resultKey = probe.LatestCommittedKey(kind);
        GameplayOutcomeReplayIdentity pending = default;
        if (probe.ReservationCount(kind) <= 0
            || probe.CommitCount(kind) <= 0
            || !resultKey.IsValid
            || !fixture.Ledger.TryGetResultIdentity(
                resultKey,
                out pending)
            || pending.State != GameplayOutcomeReplayState.DeliveryFaultPending)
        {
            throw new InvalidOperationException(
                "Injected delivery failure did not remain durably pending: "
                + kind + "; state=" + pending.State);
        }
        fault.Enabled = false;
        if (fixture.Recorder.RetryPendingDeliveries(16) <= 0
            || !fixture.Ledger.TryGetResultIdentity(
                pending.ResultKey,
                out GameplayOutcomeReplayIdentity replayed)
            || replayed.State
                != GameplayOutcomeReplayState.PublishedAcknowledged)
        {
            throw new InvalidOperationException(
                "The durable migrated outcome did not publish exactly on retry: "
                + kind);
        }
        _ = RequireExact(fixture, kind, probe);
    }
}
#endif
