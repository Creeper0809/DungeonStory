using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class FacilityShopPurchasePendingData
{
    public FacilityShopPurchaseFactData fact;
    public string publicationKey = string.Empty;
    public bool observationCommitted;
    public FacilityShopPurchasePendingData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, observationCommitted = observationCommitted };
}
[Serializable]
public sealed class FacilityShopPurchaseJournalData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<FacilityShopPurchasePendingData> pending = new();
}

// Retries frozen purchase observations and delivery enqueue. It never debits money or re-applies an offer.
public sealed class FacilityShopPurchaseJournal : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IFacilityShopPurchaseDelivery delivery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private FacilityShopPurchaseJournalData state = new();
    private Preparation active;
    private bool preparing, recovering, restoring;
    public string LastFailure { get; private set; } = string.Empty;
    public FacilityShopPurchaseJournal(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery,
        IFacilityShopPurchaseDelivery delivery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery)); }

    public bool TryBegin(FacilityShopPurchaseFact proposed, out Preparation prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        if (Busy || restoring) { failure = "facility-shop-purchase-publication-busy"; return false; }
        RecoverPending(out _);
        if (state.pending.Any(row => !row.observationCommitted))
        { failure = LastFailure; return false; }
        preparing = true; DomainFactLease lease = default;
        try
        {
            if (!proposed.IsProposal) throw new ArgumentException("facility-shop-purchase-proposal-required");
            if (!publisher.TryReserve(proposed, out lease, out failure)) return false;
            if (!lease.IsValid || !Text(lease.PublicationKey))
            { failure = "facility-shop-purchase-reservation-invalid"; publisher.Cancel(lease); return false; }
            active = prepared = new Preparation(this, proposed, lease); return true;
        }
        catch (Exception error) when (Recoverable(error))
        { publisher.Cancel(lease); failure = "facility-shop-purchase-prepare:" + error.Message; return false; }
        finally { preparing = false; if (prepared == null) LastFailure = failure; }
    }
    public sealed class Preparation : IDisposable
    {
        private readonly FacilityShopPurchaseJournal owner;
        private readonly FacilityShopPurchaseFact proposal;
        private readonly DomainFactLease lease;
        private bool attempted, ended;
        internal Preparation(FacilityShopPurchaseJournal owner, FacilityShopPurchaseFact proposal, DomainFactLease lease)
        { this.owner = owner; this.proposal = proposal; this.lease = lease; PublicationKey = lease.PublicationKey; }
        public string PublicationKey { get; }
        public bool MayBeApplied { get; private set; }
        public DomainFactDelivery PublishApplied(FacilityShopPurchaseFact actual)
        {
            if (ended || attempted || !ReferenceEquals(owner.active, this) || !actual.MatchesProposal(proposal))
                throw new InvalidOperationException("facility-shop-purchase-actual-fact-conflict");
            attempted = true;
            var row = new FacilityShopPurchasePendingData { fact = actual.Snapshot(), publicationKey = PublicationKey };
            owner.state.pending.Add(row);
            DomainFactDelivery result;
            try { result = owner.publisher.Publish(lease, actual); }
            catch (Exception error) when (Recoverable(error))
            { result = DomainFactDelivery.Uncertain("facility-shop-purchase-response-unknown:" + error.Message); }
            if (!result.MayBeCommitted)
            { owner.state.pending.Remove(row); owner.LastFailure = result.Detail; return result; }
            MayBeApplied = true; row.observationCommitted = Committed(row, result);
            owner.LastFailure = row.observationCommitted ? string.Empty : "facility-shop-purchase-observation-pending:" + result.Detail;
            return row.observationCommitted ? result : DomainFactDelivery.Uncertain(owner.LastFailure);
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
        if (Busy || restoring) { failure = "facility-shop-purchase-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var row in state.pending.ToArray())
            {
                var fact = new FacilityShopPurchaseFact(row.fact);
                if (!row.observationCommitted)
                {
                    var results = new DomainFactDelivery[1];
                    if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, fact) }, results, out failure)
                        || !Committed(row, results[0]))
                    { LastFailure = failure = "facility-shop-purchase-observation-pending:" + failure + ":" + results[0].Detail; return false; }
                    row.observationCommitted = true;
                }
                if (!delivery.TryAcceptPurchase(fact, row.publicationKey, out failure))
                { LastFailure = failure = "facility-shop-purchase-enqueue-pending:" + failure; return false; }
                state.pending.Remove(row);
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "facility-shop-purchase-recovery:" + error.Message; return false; }
        finally { recovering = false; }
    }
    private static bool Committed(FacilityShopPurchasePendingData row, DomainFactDelivery value) =>
        value.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && value.OperationKey == row.publicationKey && Text(value.PublicationId)
        && value.Fingerprint?.Length == 64 && value.Fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 2048, false);
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
    private bool Busy => active != null || preparing || recovering;
    public void Tick()
    {
        if (Busy || restoring || state.pending.Count == 0 || retryClock.ElapsedMilliseconds < 1000) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public FacilityShopPurchaseJournalData Capture() { RequireIdle(); return ValidateAndClone(state); }
    public static FacilityShopPurchaseJournalData ValidateAndClone(FacilityShopPurchaseJournalData source)
    {
        if (source?.version != FacilityShopPurchaseJournalData.CurrentVersion || source.pending == null)
            throw new ArgumentException("facility-shop-purchase-journal-invalid");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var sequences = new HashSet<int>();
        var result = new FacilityShopPurchaseJournalData();
        foreach (var row in source.pending)
        {
            if (row?.fact == null || !Text(row.publicationKey) || !keys.Add(row.publicationKey)
                || !operations.Add(row.fact.operationId) || !sequences.Add(row.fact.sequence))
                throw new ArgumentException("facility-shop-purchase-pending-identity-invalid");
            row.fact.Validate(); result.pending.Add(row.Clone());
        }
        return result;
    }
    private void RequireIdle() { if (Busy) throw new InvalidOperationException("facility-shop-purchase-publication-active"); }
    public void Restore(FacilityShopPurchaseJournalData source)
    {
        RequireIdle(); var candidate = ValidateAndClone(source);
        if (!restore.TryStage(() => state = candidate, () => { var before = state; return () => state = before; })) state = candidate;
    }
    public string ParticipantId => FacilityShopPurchaseFactSaveSection.Id;
    public void BeginRestoreCandidate()
    { RequireIdle(); if (restoring) throw new InvalidOperationException("facility-shop-purchase-already-restoring"); restore.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { try { restore.Complete(); } finally { restoring = false; } }
}
