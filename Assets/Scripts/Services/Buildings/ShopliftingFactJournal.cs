using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ShopliftingPendingFactData
{
    public ShopliftingFactData fact;
    public string publicationKey = string.Empty;
    public ShopliftingPendingFactData Clone() => new() { fact = fact?.Clone(), publicationKey = publicationKey };
}
[Serializable]
public sealed class ShopliftingFactJournalData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<ShopliftingPendingFactData> pending = new();
}

// Owns only delivery of frozen facts. It never takes stock, removes equipment,
// or re-emits transient crime/activity events during saved replay.
public sealed class ShopliftingFactJournal : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private ShopliftingFactJournalData state = new();
    private Preparation active;
    private bool preparing, recovering, restoring;
    public string LastFailure { get; private set; } = string.Empty;
    public ShopliftingFactJournal(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryBegin(ShopliftingFact fact, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (Busy || restoring) { failure = "shoplifting-publication-busy"; return false; }
        if (!RecoverPending(out failure)) return false;
        preparing = true; DomainFactLease lease = default;
        try
        {
            fact.Snapshot().Validate();
            if (!publisher.TryReserve(fact, out lease, out failure)) return false;
            if (!lease.IsValid || !Key(lease.PublicationKey))
            { failure = "shoplifting-reservation-invalid"; publisher.Cancel(lease); return false; }
            active = prepared = new Preparation(this, fact, lease); return true;
        }
        catch (Exception error) when (Recoverable(error))
        { publisher.Cancel(lease); failure = "shoplifting-prepare:" + error.Message; return false; }
        finally { preparing = false; if (prepared == null) LastFailure = failure; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly ShopliftingFactJournal owner;
        private readonly ShopliftingFact fact;
        private readonly DomainFactLease lease;
        private readonly string publicationKey;
        private bool attempted, ended;
        internal Preparation(ShopliftingFactJournal owner, ShopliftingFact fact, DomainFactLease lease)
        { this.owner = owner; this.fact = fact; this.lease = lease; publicationKey = lease.PublicationKey; }
        public bool MayBeApplied { get; private set; }
        public DomainFactDelivery PublishApplied()
        {
            if (ended || attempted || !ReferenceEquals(owner.active, this))
                throw new InvalidOperationException("shoplifting-publication-not-active");
            attempted = true;
            var row = new ShopliftingPendingFactData { fact = fact.Snapshot(), publicationKey = publicationKey };
            owner.state.pending.Add(row);
            DomainFactDelivery result;
            try { result = owner.publisher.Publish(lease, fact); }
            catch (Exception error) when (Recoverable(error))
            { result = DomainFactDelivery.Uncertain("shoplifting-publication-response-unknown:" + error.Message); }
            MayBeApplied = result.MayBeCommitted;
            if (!MayBeApplied || Accepted(row, result)) owner.state.pending.Remove(row);
            owner.LastFailure = owner.state.pending.Contains(row)
                ? "shoplifting-observation-pending:" + result.Detail : result.Detail;
            return MayBeApplied && !Accepted(row, result)
                ? DomainFactDelivery.Uncertain(owner.LastFailure) : result;
        }
        public void Dispose()
        {
            if (ended) return; ended = true;
            try { owner.publisher.Cancel(lease); }
            finally { if (ReferenceEquals(owner.active, this)) owner.active = null; }
        }
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (Busy || restoring) { failure = "shoplifting-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var row in state.pending.ToArray())
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, new ShopliftingFact(row.fact)) },
                        results, out failure) || !Accepted(row, results[0]))
                { LastFailure = failure = "shoplifting-recovery-pending:" + failure + ":" + results[0].Detail; return false; }
                state.pending.Remove(row);
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "shoplifting-recovery:" + error.Message; return false; }
        finally { recovering = false; }
    }
    private static bool Accepted(ShopliftingPendingFactData row, DomainFactDelivery value) =>
        value.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && value.OperationKey == row.publicationKey && Key(value.PublicationId)
        && value.Fingerprint?.Length == 64 && value.Fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Key(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
    private bool Busy => active != null || preparing || recovering;
    public void Tick()
    { if (Busy || restoring || state.pending.Count == 0 || clock.ElapsedMilliseconds < 1000) return;
        clock.Restart(); RecoverPending(out _); }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public ShopliftingFactJournalData Capture() { RequireIdle(); return ValidateAndClone(state); }
    public static ShopliftingFactJournalData ValidateAndClone(ShopliftingFactJournalData source)
    {
        if (source?.version != ShopliftingFactJournalData.CurrentVersion || source.pending == null || source.pending.Count > 1)
            throw new ArgumentException("shoplifting-journal-invalid");
        var result = new ShopliftingFactJournalData();
        foreach (var row in source.pending)
        {
            if (row?.fact == null || !Key(row.publicationKey)) throw new ArgumentException("shoplifting-pending-invalid");
            row.fact.Validate(); result.pending.Add(row.Clone());
        }
        return result;
    }
    private void RequireIdle() { if (Busy) throw new InvalidOperationException("shoplifting-local-transaction-active"); }
    public void Restore(ShopliftingFactJournalData source)
    {
        RequireIdle(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public string ParticipantId => ShopliftingFactSaveSection.Id;
    public void BeginRestoreCandidate()
    { RequireIdle(); if (restoring) throw new InvalidOperationException("shoplifting-already-restoring"); restore.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { try { restore.Complete(); } finally { restoring = false; } }
}
