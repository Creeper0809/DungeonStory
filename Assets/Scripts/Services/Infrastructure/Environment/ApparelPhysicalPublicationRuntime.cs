using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ApparelPhysicalPublicationData
{
    public ApparelPhysicalCompletedFactData fact;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool pending;
    public ApparelPhysicalPublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}
[Serializable]
public sealed class ApparelPhysicalPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<ApparelPhysicalPublicationData> observations = new();
}

/// <summary>Retains completed physical facts beyond work-order lifetime. Recovery never consumes or creates items.</summary>
public sealed class ApparelPhysicalPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private ApparelPhysicalPublicationSaveData state = new();
    private Preparation active;
    private bool preparing, recovering;
    public bool IsRestoring { get; private set; }
    public bool IsBusy => preparing || recovering || active != null;
    public string LastFailure { get; private set; } = string.Empty;

    public ApparelPhysicalPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryPrepare(ApparelPhysicalCompletedFact proposal, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (IsBusy || IsRestoring) { failure = "apparel-physical-publication-busy"; return false; }
        preparing = true;
        DomainFactLease lease = default;
        try
        {
            var data = proposal.Snapshot();
            if (data.completed) throw new ArgumentException("apparel-physical-prepare-requires-proposal");
            var known = state.observations.SingleOrDefault(row => row.fact.operationId == data.operationId);
            if (known != null && !SameSource(known.fact, data))
                throw new ArgumentException("apparel-physical-frozen-source-conflict");
            if (known == null && !publisher.TryReserve(proposal, out lease, out failure)) return false;
            if (known == null && (!lease.IsValid
                || !DomainFactValueSyntax.IsText(lease.PublicationKey, 1024, false)))
                throw new InvalidOperationException("apparel-physical-reserved-key-missing");
            prepared = new Preparation(this, data, known, lease);
            active = prepared; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (lease.IsValid) publisher.Cancel(lease);
            LastFailure = failure = "apparel-physical-prepare:" + exception.Message;
            return false;
        }
        finally { preparing = false; }
    }

    public sealed class Preparation : IDisposable
    {
        private readonly ApparelPhysicalPublicationRuntime owner;
        internal readonly ApparelPhysicalCompletedFactData Proposed;
        internal readonly DomainFactLease Lease;
        internal readonly string PublicationKey;
        internal ApparelPhysicalPublicationData Row;
        internal bool Completed, Disposed;
        internal Preparation(ApparelPhysicalPublicationRuntime owner, ApparelPhysicalCompletedFactData proposal,
            ApparelPhysicalPublicationData known, DomainFactLease lease)
        { this.owner = owner; Proposed = proposal; Row = known; Lease = lease;
            PublicationKey = known?.publicationKey ?? lease.PublicationKey; }
        public bool HasCompletedFact => Row != null;
        public DomainFactDelivery Complete(ApparelPhysicalTransactionResult actual) => owner.Complete(this, actual);
        public void Dispose() => owner.End(this);
    }

    private DomainFactDelivery Complete(Preparation prepared, ApparelPhysicalTransactionResult actual)
    {
        if (!ReferenceEquals(active, prepared) || prepared.Disposed || prepared.Completed || !actual.IsCompleted)
            throw new InvalidOperationException("apparel-physical-completion-contract-invalid");
        prepared.Completed = true;
        var known = prepared.Row;
        if (known == null)
        {
            var fact = prepared.Proposed.Clone();
            fact.completed = true;
            fact.outputStackId = actual.OutputStackId; fact.outputInstanceId = actual.OutputInstanceId;
            fact.inputMassGrams = actual.InputMassGrams; fact.outputMassGrams = actual.OutputMassGrams;
            fact.Validate(requireCompleted: true);
            known = prepared.Row = new ApparelPhysicalPublicationData
            { fact = fact, publicationKey = prepared.PublicationKey, pending = true };
            // The irreversible physical result is already real. Freeze it BEFORE calling any observer.
            state.observations.Add(known);
            try
            {
                var result = publisher.Publish(prepared.Lease, new ApparelPhysicalCompletedFact(fact));
                if (Accept(known, result)) { LastFailure = string.Empty; return result; }
                return Pending(result.Detail);
            }
            catch (Exception exception) when (Recoverable(exception)) { return Pending(exception.Message); }
        }
        if (known.fact.outputStackId != actual.OutputStackId || known.fact.outputInstanceId != actual.OutputInstanceId
            || known.fact.inputMassGrams != actual.InputMassGrams || known.fact.outputMassGrams != actual.OutputMassGrams)
            return DomainFactDelivery.Reject("apparel-physical-frozen-result-conflict");
        if (!known.pending) return Delivered(known);
        return Replay(known);
    }

    private void End(Preparation prepared)
    {
        if (prepared.Disposed) return;
        if (!ReferenceEquals(active, prepared)) throw new InvalidOperationException("apparel-physical-source-not-active");
        prepared.Disposed = true;
        try { if (prepared.Lease.IsValid) publisher.Cancel(prepared.Lease); }
        finally { active = null; }
    }

    private DomainFactDelivery Replay(ApparelPhysicalPublicationData row)
    {
        try
        {
            var result = new DomainFactDelivery[1];
            if (recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                new ApparelPhysicalCompletedFact(row.fact)) }, result, out string failure) && Accept(row, result[0]))
            { LastFailure = string.Empty; return result[0]; }
            return Pending(failure);
        }
        catch (Exception exception) when (Recoverable(exception)) { return Pending(exception.Message); }
    }
    private DomainFactDelivery Pending(string detail)
    {
        LastFailure = "apparel-physical-observation-pending:" + detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }
    private static DomainFactDelivery Delivered(ApparelPhysicalPublicationData row) => new(
        DomainFactDeliveryPhase.Delivered, row.publicationKey, row.publicationId, row.fingerprint, string.Empty);
    private static bool Accept(ApparelPhysicalPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false;
        return true;
    }
    // Display names and observation day can change while an owner waits. Replay retains the original values.
    private static bool SameSource(ApparelPhysicalCompletedFactData a, ApparelPhysicalCompletedFactData b) =>
        a.operationId == b.operationId && a.orderId == b.orderId && a.facilityId == b.facilityId
        && a.outputItemId == b.outputItemId && a.kind == b.kind && a.attemptIndex == b.attemptIndex
        && a.hasProductQuality == b.hasProductQuality && a.makerId == b.makerId && a.quality == b.quality
        && a.rejectedBelowMinimum == b.rejectedBelowMinimum;

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || IsBusy) { failure = "apparel-physical-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var row in state.observations.Where(row => row.pending))
                if (!Replay(row).Completed) { failure = LastFailure; return false; }
            LastFailure = string.Empty; return true;
        }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || IsBusy || retryClock.ElapsedMilliseconds < 1000
            || !state.observations.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);

    public ApparelPhysicalPublicationSaveData Capture()
    { RequireIdleMutation(); return ValidateAndClone(state); }
    public static ApparelPhysicalPublicationSaveData ValidateAndClone(ApparelPhysicalPublicationSaveData source)
    {
        if (source == null || source.version != ApparelPhysicalPublicationSaveData.CurrentVersion
            || source.observations == null || source.observations.Any(row => row?.fact == null))
            throw new ArgumentException("apparel-physical-publication-save-invalid");
        var result = new ApparelPhysicalPublicationSaveData();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            row.fact.Validate(requireCompleted: true);
            if (!operations.Add(row.fact.operationId) || !keys.Add(row.publicationKey)
                || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("apparel-physical-publication-proof-invalid");
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(ApparelPhysicalPublicationSaveData source)
    {
        RequireIdlePersistence();
        var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var previous = state; return () => state = previous; }))
            state = candidate;
    }
    public void RequireIdlePersistence()
    { if (IsBusy) throw new InvalidOperationException("apparel-physical-publication-active"); }
    public void RequireIdleMutation()
    { RequireIdlePersistence(); if (IsRestoring) throw new InvalidOperationException("apparel-physical-publication-restoring"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => ApparelPhysicalPublicationSaveSection.Id;
    public void BeginRestoreCandidate() { RequireIdleMutation(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
