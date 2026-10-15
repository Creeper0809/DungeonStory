#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DungeonStory.Foundation.Facts;
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

    internal bool TryObserveDomainFactReservation(
        MigratedProducerOutcomeKind kind,
        out string failureReason)
    {
        Increment(reservations, kind);
        if (FaultMode == MigratedProducerOutcomeFaultMode.RejectReservation
            && kind == TargetKind)
        {
            failureReason =
                "migrated-producer-e2e-injected-reservation-rejection";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    internal bool ObserveDomainFactCommitAttempt(
        MigratedProducerOutcomeKind kind)
    {
        Increment(commitAttempts, kind);
        return FaultMode == MigratedProducerOutcomeFaultMode.RejectCommit
            && kind == TargetKind;
    }

    internal void ObserveDomainFactCommitted(
        MigratedProducerOutcomeKind kind,
        GameplayResultKey resultKey)
    {
        Increment(commits, kind);
        latestCommittedKeys[kind] = resultKey;
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
        out string failureReason,
        int additionalProvenanceCount = 0)
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
            out failureReason,
            additionalProvenanceCount);
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

    public bool CommitWithExternalPreparedBatch(
        PreparedOwnerOutcome[] externalPrepared,
        long[] expectedExternalOwnerRevisions,
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
            if (externalPrepared != null && recorder != null)
            {
                for (int index = 0; index < externalPrepared.Length; index++)
                {
                    if (externalPrepared[index].IsValid)
                        recorder.CancelPrepared(externalPrepared[index].Token);
                }
            }
            CancelAll(prepared);
            failureReason =
                "migrated-producer-e2e-injected-external-batch-commit-rejection";
            return false;
        }
        bool committed = external.CommitWithExternalPreparedBatch(
            externalPrepared,
            expectedExternalOwnerRevisions,
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

internal sealed class MigratedProducerDomainFactProbeObserver<TFact> :
    IDurableDomainFactObserver<TFact>,
    IDurableDomainFactRecoveryObserver<TFact>
{
    private readonly Func<TFact, MigratedProducerOutcomeKind> kind;
    private readonly IDurableDomainFactObserver<TFact> inner;
    private readonly IDurableDomainFactRecoveryObserver<TFact> recovery;
    private readonly MigratedProducerDomainFactProbeCommitGroup commits;
    private readonly MigratedProducerOutcomeProbeTransaction probe;

    internal MigratedProducerDomainFactProbeObserver(
        Func<TFact, MigratedDomainFactCapture> capture,
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        if (capture == null)
            throw new ArgumentNullException(nameof(capture));
        kind = fact => capture(fact).Kind;
        var observer = new MigratedDomainFactObserver<TFact>(
            capture,
            runtime ?? throw new ArgumentNullException(nameof(runtime)),
            commits ?? throw new ArgumentNullException(nameof(commits)));
        inner = observer;
        recovery = observer;
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.commits = MigratedProducerDomainFactProbeCommitGroupCache.Get(
            commits,
            probe);
    }

    internal MigratedProducerDomainFactProbeObserver(
        Func<TFact, MigratedProducerOutcomeKind> kind,
        IDurableDomainFactObserver<TFact> inner,
        IDurableDomainFactRecoveryObserver<TFact> recovery,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        this.kind = kind ?? throw new ArgumentNullException(nameof(kind));
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        this.commits = MigratedProducerDomainFactProbeCommitGroupCache.Get(
            commits ?? throw new ArgumentNullException(nameof(commits)),
            probe);
    }

    public bool TryReserve(
        in TFact proposed,
        out object reservation,
        out string failure)
    {
        reservation = null;
        MigratedProducerOutcomeKind value = kind(proposed);
        if (!probe.TryObserveDomainFactReservation(value, out failure))
            return false;
        if (!inner.TryReserve(proposed, out object innerReservation, out failure))
            return false;
        if (innerReservation is not IDomainFactReservedIdentity identity)
        {
            inner.Cancel(innerReservation);
            failure =
                "migrated-producer-e2e-domain-fact-reserved-identity-missing";
            return false;
        }
        reservation = new ProbeReservation(value, innerReservation, identity);
        return true;
    }

    public bool TryPrepare(
        in TFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (reservation is not ProbeReservation state)
        {
            failure = "migrated-producer-e2e-domain-fact-reservation-invalid";
            return false;
        }

        MigratedProducerOutcomeKind value = kind(actual);
        if (value != state.Kind)
        {
            failure = "migrated-producer-e2e-domain-fact-kind-changed";
            return false;
        }
        if (!inner.TryPrepare(
                actual,
                state.Inner,
                out IDomainFactPreparedPublication innerPrepared,
                out failure))
        {
            return false;
        }
        if (innerPrepared is not GameplayDomainFactPublication publication)
        {
            innerPrepared?.Cancel();
            failure =
                "migrated-producer-e2e-domain-fact-publication-invalid";
            return false;
        }

        prepared = new MigratedProducerDomainFactProbePublication(
            commits,
            publication,
            value);
        failure = string.Empty;
        return true;
    }

    public DomainFactDelivery Replay(in TFact fact) => inner.Replay(fact);

    public bool TryPrepareReplay(
        string publicationKey,
        in TFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        MigratedProducerOutcomeKind value = kind(fact);
        if (!recovery.TryPrepareReplay(
                publicationKey,
                fact,
                out IDomainFactPreparedPublication innerPrepared,
                out failure))
        {
            return false;
        }
        if (innerPrepared is not GameplayDomainFactPublication publication)
        {
            innerPrepared?.Cancel();
            failure =
                "migrated-producer-e2e-domain-fact-recovery-publication-invalid";
            return false;
        }

        prepared = new MigratedProducerDomainFactProbePublication(
            commits,
            publication,
            value);
        failure = string.Empty;
        return true;
    }

    public void Cancel(object reservation)
    {
        if (reservation is ProbeReservation state)
            inner.Cancel(state.Inner);
    }

    private sealed class ProbeReservation : IDomainFactReservedIdentity
    {
        internal ProbeReservation(
            MigratedProducerOutcomeKind kind,
            object inner,
            IDomainFactReservedIdentity identity)
        {
            Kind = kind;
            Inner = inner;
            Identity = identity;
        }

        internal MigratedProducerOutcomeKind Kind { get; }
        internal object Inner { get; }
        private IDomainFactReservedIdentity Identity { get; }
        public string PublicationKey => Identity.PublicationKey;
    }
}

internal sealed class MigratedProducerDomainFactProbePublication :
    IDomainFactPreparedPublication,
    IDomainFactPreparedIdentity
{
    internal MigratedProducerDomainFactProbePublication(
        MigratedProducerDomainFactProbeCommitGroup commits,
        GameplayDomainFactPublication inner,
        MigratedProducerOutcomeKind kind)
    {
        CommitGroup = commits ?? throw new ArgumentNullException(nameof(commits));
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Kind = kind;
    }

    public IDomainFactCommitGroup CommitGroup { get; }
    internal GameplayDomainFactPublication Inner { get; }
    internal MigratedProducerOutcomeKind Kind { get; }
    public string OperationKey => Inner.OperationKey;
    public long OwnerRevision => Inner.OwnerRevision;
    public bool IsReplay => Inner.IsReplay;
    public void Cancel() => Inner.Cancel();
}

internal sealed class MigratedProducerDomainFactProbeCommitGroup :
    IDomainFactCommitGroup
{
    private readonly GameplayDomainFactCommitGroup inner;
    private readonly MigratedProducerOutcomeProbeTransaction probe;

    internal MigratedProducerDomainFactProbeCommitGroup(
        GameplayDomainFactCommitGroup inner,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public bool Commit(
        IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results,
        out string failure)
    {
        if (publications == null
            || results == null
            || publications.Count == 0
            || publications.Count != results.Length)
        {
            throw new ArgumentException(
                "Invalid migrated-producer probe publication cohort.");
        }

        var values =
            new MigratedProducerDomainFactProbePublication[publications.Count];
        bool reject = false;
        for (int index = 0; index < publications.Count; index++)
        {
            if (publications[index]
                    is not MigratedProducerDomainFactProbePublication value
                || !ReferenceEquals(value.CommitGroup, this))
            {
                throw new ArgumentException(
                    "Mismatched migrated-producer probe commit group.");
            }
            values[index] = value;
            if (!value.IsReplay)
                reject |= probe.ObserveDomainFactCommitAttempt(value.Kind);
        }

        if (reject)
        {
            failure =
                "migrated-producer-e2e-injected-batch-commit-rejection";
            for (int index = 0; index < publications.Count; index++)
            {
                values[index].Cancel();
                results[index] = DomainFactDelivery.Reject(failure);
            }
            return false;
        }

        var innerPublications =
            new IDomainFactPreparedPublication[values.Length];
        for (int index = 0; index < values.Length; index++)
            innerPublications[index] = values[index].Inner;
        bool committed = inner.Commit(
            innerPublications,
            results,
            out failure);
        if (committed)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!values[index].IsReplay && results[index].MayBeCommitted)
                {
                    probe.ObserveDomainFactCommitted(
                        values[index].Kind,
                        values[index].Inner.Prepared.ResultKey);
                }
            }
        }
        return committed;
    }
}

internal static class MigratedProducerDomainFactProbeCommitGroupCache
{
    private static readonly ConditionalWeakTable<
        GameplayDomainFactCommitGroup,
        GroupsByProbe> Groups = new();

    internal static MigratedProducerDomainFactProbeCommitGroup Get(
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        if (commits == null) throw new ArgumentNullException(nameof(commits));
        if (probe == null) throw new ArgumentNullException(nameof(probe));
        return Groups.GetValue(
                commits,
                value => new GroupsByProbe(value))
            .Get(probe);
    }

    private sealed class GroupsByProbe
    {
        private readonly GameplayDomainFactCommitGroup commits;
        private readonly ConditionalWeakTable<
            MigratedProducerOutcomeProbeTransaction,
            MigratedProducerDomainFactProbeCommitGroup> groups = new();

        internal GroupsByProbe(GameplayDomainFactCommitGroup commits) =>
            this.commits = commits;

        internal MigratedProducerDomainFactProbeCommitGroup Get(
            MigratedProducerOutcomeProbeTransaction probe) =>
            groups.GetValue(
                probe,
                value => new MigratedProducerDomainFactProbeCommitGroup(
                    commits,
                    value));
    }
}

internal static class MigratedProducerOutcomeEvidenceCapture
{
    private static Action<
        MigratedProducerOutcomeKind,
        GameplayOutcomeReplayIdentity,
        GameplayOutcomeSnapshot> current;

    internal static IDisposable Begin(
        Action<
            MigratedProducerOutcomeKind,
            GameplayOutcomeReplayIdentity,
            GameplayOutcomeSnapshot> capture)
    {
        if (capture == null)
            throw new ArgumentNullException(nameof(capture));
        if (current != null)
        {
            throw new InvalidOperationException(
                "A migrated-producer evidence capture is already active.");
        }
        current = capture;
        return new Scope(capture);
    }

    internal static void Record(
        MigratedProducerOutcomeKind kind,
        in GameplayOutcomeReplayIdentity identity,
        GameplayOutcomeSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        current?.Invoke(kind, identity, snapshot);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action<
            MigratedProducerOutcomeKind,
            GameplayOutcomeReplayIdentity,
            GameplayOutcomeSnapshot> capture;
        private bool disposed;

        internal Scope(Action<
            MigratedProducerOutcomeKind,
            GameplayOutcomeReplayIdentity,
            GameplayOutcomeSnapshot> capture) => this.capture = capture;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (!ReferenceEquals(current, capture))
            {
                throw new InvalidOperationException(
                    "The migrated-producer evidence capture scope changed unexpectedly.");
            }
            current = null;
        }
    }
}

internal sealed class MigratedProducerProductionTransitionEvidence
{
    internal MigratedProducerProductionTransitionEvidence(
        MigratedProducerOutcomeKind kind,
        in GameplayOutcomeReplayIdentity identity,
        GameplayOutcomeSnapshot outcome,
        string beforeState,
        string afterState)
    {
        Kind = kind;
        Identity = identity;
        Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
        BeforeState = RequireState(beforeState, nameof(beforeState));
        AfterState = RequireState(afterState, nameof(afterState));
        if (string.Equals(BeforeState, AfterState, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A production transition cannot capture identical before/after state: "
                + kind);
        }
    }

    internal MigratedProducerOutcomeKind Kind { get; }
    internal GameplayOutcomeReplayIdentity Identity { get; }
    internal GameplayOutcomeSnapshot Outcome { get; }
    internal string BeforeState { get; }
    internal string AfterState { get; }

    private static string RequireState(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A captured owner state is required.", parameter);
        return value;
    }
}

internal static class MigratedProducerProductionTransitionEvidenceCapture
{
    private static Action<MigratedProducerProductionTransitionEvidence> current;

    internal static IDisposable Begin(
        Action<MigratedProducerProductionTransitionEvidence> capture)
    {
        if (capture == null)
            throw new ArgumentNullException(nameof(capture));
        if (current != null)
        {
            throw new InvalidOperationException(
                "A migrated-producer transition capture is already active.");
        }
        current = capture;
        return new Scope(capture);
    }

    internal static void Record(
        MigratedProducerOutcomeKind kind,
        in GameplayOutcomeReplayIdentity identity,
        GameplayOutcomeSnapshot outcome,
        string beforeState,
        string afterState)
    {
        if (current == null)
            return;
        current(new MigratedProducerProductionTransitionEvidence(
            kind,
            identity,
            outcome,
            beforeState,
            afterState));
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action<MigratedProducerProductionTransitionEvidence>
            capture;
        private bool disposed;

        internal Scope(
            Action<MigratedProducerProductionTransitionEvidence> capture) =>
            this.capture = capture;

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (!ReferenceEquals(current, capture))
            {
                throw new InvalidOperationException(
                    "The migrated-producer transition capture scope changed unexpectedly.");
            }
            current = null;
        }
    }
}

internal static class MigratedProducerOutcomeE2EAssertions
{
    internal static GameplayOutcomeReplayIdentity RequireExact(
        MigratedProducerOutcomeEditorFixture fixture,
        MigratedProducerOutcomeKind kind,
        MigratedProducerOutcomeProbeTransaction probe,
        string beforeState = null,
        string afterState = null)
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
            MigratedProducerOutcomeEvidenceCapture.Record(
                kind,
                identity,
                exact);
            if (beforeState != null || afterState != null)
            {
                MigratedProducerProductionTransitionEvidenceCapture.Record(
                    kind,
                    identity,
                    exact,
                    beforeState,
                    afterState);
            }
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
        MigratedProducerOutcomeDeliveryFault fault,
        string beforeState = null,
        string afterState = null)
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
        _ = RequireExact(fixture, kind, probe, beforeState, afterState);
    }
}
#endif
