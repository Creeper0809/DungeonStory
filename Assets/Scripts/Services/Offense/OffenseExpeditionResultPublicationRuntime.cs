using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

[Serializable]
public sealed class OffenseExpeditionResultObservationData
{
    public OffenseExpeditionResultFactData fact = new();
    public string publicationKey = string.Empty;
    public List<string> stockPublicationKeys = new();
    public bool pending, notificationPending;
    public OffenseExpeditionResultObservationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, stockPublicationKeys = new(stockPublicationKeys), pending = pending, notificationPending = notificationPending };
}

[Serializable]
public sealed class OffenseExpeditionResultPublicationSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<OffenseExpeditionResultObservationData> observations = new();
}

/// <summary>Owns applied result identities and their exact undelivered facts; never applies rewards or return effects.</summary>
public sealed class OffenseExpeditionResultPublicationRuntime : ITickable, IDungeonRestoreTransactionParticipant
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus events;
    private readonly DungeonRestorePublication restorePublication = new();
    private Dictionary<string, OffenseExpeditionResultObservationData> observations = new(StringComparer.Ordinal);
    private readonly HashSet<string> work = new(StringComparer.Ordinal);
    private string activeId;
    private bool restoring, recovering;
    private long nextRetry;
    public string LastFailure { get; private set; } = string.Empty;
    public void RecordFollowupFailure(string failure) => LastFailure = failure ?? throw new ArgumentNullException(nameof(failure));
    public OffenseExpeditionResultPublicationRuntime(IDomainFactPublisher publisher,
        IDomainFactRecoveryPublisher recovery, IGameEventBus events)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool TryGetApplied(string expeditionId, out OffenseExpeditionResolvedFact fact)
    {
        RequireIdle();
        fact = observations.TryGetValue(expeditionId, out var found) ? new OffenseExpeditionResolvedFact(found.fact) : null;
        return fact != null;
    }
    public void Begin(string expeditionId)
    {
        RequireIdle();
        if (!DomainFactValueSyntax.IsIdentity(expeditionId) || observations.ContainsKey(expeditionId))
            throw new InvalidOperationException("offense-result-command-identity-conflict");
        activeId = expeditionId;
    }
    public void End()
    {
        if (activeId == null) throw new InvalidOperationException("offense-result-command-missing");
        activeId = null;
    }

    public DomainFactDelivery PublishApplied(OffenseExpeditionResolvedFact fact)
    {
        if (activeId == null || fact == null || activeId != fact.Result.expeditionId || observations.ContainsKey(activeId))
            throw new InvalidOperationException("offense-result-applied-identity-conflict");
        var leases = new List<DomainFactLease>();
        OffenseExpeditionResultObservationData record = null;
        bool publishAttempted = false;
        DomainFactDelivery delivery = DomainFactDelivery.Reject("offense-result-not-published");
        try
        {
            if (!publisher.TryReserve(fact, out var lease, out string failure))
                return DomainFactDelivery.Reject("offense-result-reserve:" + failure);
            leases.Add(lease);
            if (!lease.IsValid || string.IsNullOrWhiteSpace(lease.PublicationKey))
                return DomainFactDelivery.Reject("offense-result-reservation-invalid");
            record = new OffenseExpeditionResultObservationData
            { fact = fact.Snapshot(), publicationKey = lease.PublicationKey, pending = true, notificationPending = true };
            var emissions = new List<DomainFactEmission> { DomainFactEmission.Create(lease, fact) };
            foreach (var stock in OffenseStockRewardCompletedFact.From(fact))
            {
                if (!publisher.TryReserve(stock, out var stockLease, out failure))
                    return DomainFactDelivery.Reject("offense-result-stock-reserve:" + failure);
                leases.Add(stockLease);
                if (!stockLease.IsValid || string.IsNullOrWhiteSpace(stockLease.PublicationKey))
                    return DomainFactDelivery.Reject("offense-result-stock-reservation-invalid");
                record.stockPublicationKeys.Add(stockLease.PublicationKey);
                emissions.Add(DomainFactEmission.Create(stockLease, stock));
            }
            observations.Add(activeId, record);
            work.Add(activeId);
            publishAttempted = true;
            try
            {
                if (emissions.Count == 1) delivery = publisher.Publish(lease, fact);
                else
                {
                    var results = new DomainFactDelivery[emissions.Count];
                    bool accepted = publisher.PublishBatch(emissions, results, out failure);
                    delivery = !accepted && results.All(value => !value.MayBeCommitted)
                        ? DomainFactDelivery.Reject("offense-result-cohort-rejected:" + failure)
                        : AllDelivered(record, results) ? results[0]
                        : DomainFactDelivery.Uncertain("offense-result-cohort-pending:" + failure);
                }
            }
            catch (Exception exception) when (Recoverable(exception))
            { delivery = DomainFactDelivery.Uncertain("offense-result-publish:" + exception.GetType().Name); }
            if (Delivered(record.publicationKey, delivery)) record.pending = false;
            else if (delivery.Completed) delivery = DomainFactDelivery.Uncertain("offense-result-delivery-proof-invalid");
            if (!delivery.MayBeCommitted) { observations.Remove(activeId); work.Remove(activeId); }
            LastFailure = delivery.Completed ? string.Empty : delivery.Detail;
            return delivery;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (!publishAttempted) { observations.Remove(activeId); work.Remove(activeId); }
            LastFailure = "offense-result-publication:" + exception.GetType().Name + ":" + exception.Message;
            return publishAttempted ? DomainFactDelivery.Uncertain(LastFailure) : DomainFactDelivery.Reject(LastFailure);
        }
        finally
        {
            foreach (var reserved in leases)
            {
                try { publisher.Cancel(reserved); }
                catch (Exception exception) when (Recoverable(exception))
                { LastFailure = "offense-result-cancel:" + exception.GetType().Name; }
            }
        }
    }

    public bool RecoverPending(out string failure)
    {
        RequireIdle();
        failure = string.Empty;
        recovering = true;
        try
        {
            foreach (string id in work.ToArray())
            {
                var record = observations[id];
                var fact = new OffenseExpeditionResolvedFact(record.fact);
                if (record.pending)
                {
                    var replay = new List<DomainFactReplay> { DomainFactReplay.Create(record.publicationKey, fact) };
                    var stocks = OffenseStockRewardCompletedFact.From(fact);
                    for (int index = 0; index < stocks.Count; index++)
                        replay.Add(DomainFactReplay.Create(record.stockPublicationKeys[index], stocks[index]));
                    var results = new DomainFactDelivery[replay.Count];
                    if (!recovery.ReplayBatch(replay, results, out failure) || !AllDelivered(record, results))
                    { failure = "offense-result-recovery-pending:" + failure + ":" + results[0].Detail; LastFailure = failure; return false; }
                    record.pending = false;
                }
                if (record.notificationPending)
                {
                    record.notificationPending = false;
                    // The canonical observer was already delivered; this is only the UI/identity event.
                    try { events.Publish(fact); }
                    catch (Exception exception) when (Recoverable(exception))
                    { LastFailure = "offense-result-notification:" + exception.GetType().Name + ":" + exception.Message; }
                }
                work.Remove(id);
            }
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "offense-result-recovery:" + exception.GetType().Name + ":" + exception.Message; LastFailure = failure; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (work.Count == 0 || activeId != null || restoring || recovering || Stopwatch.GetTimestamp() < nextRetry) return;
        nextRetry = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        RecoverPending(out _);
    }

    public OffenseExpeditionResultPublicationSaveData Capture()
    {
        RequireNotBusy();
        return new() { observations = observations.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.Clone()).ToList() };
    }
    public static OffenseExpeditionResultPublicationSaveData ValidateAndClone(OffenseExpeditionResultPublicationSaveData source)
    {
        if (source == null || source.version != OffenseExpeditionResultPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("offense-result-publications-save-invalid");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new OffenseExpeditionResultPublicationSaveData();
        foreach (var row in source.observations)
        {
            if (row == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false) || row.pending && !row.notificationPending
                || row.stockPublicationKeys == null || row.stockPublicationKeys.Any(key => !DomainFactValueSyntax.IsText(key, 4096, false))
                || row.stockPublicationKeys.Distinct(StringComparer.Ordinal).Count() != row.stockPublicationKeys.Count)
                throw new ArgumentException("offense-result-observation-invalid");
            var fact = new OffenseExpeditionResolvedFact(row.fact);
            if (row.stockPublicationKeys.Count != OffenseStockRewardCompletedFact.From(fact).Count)
                throw new ArgumentException("offense-result-stock-observations-missing");
            if (!ids.Add(fact.Result.expeditionId)) throw new ArgumentException("offense-result-observation-duplicate");
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(OffenseExpeditionResultPublicationSaveData source)
    {
        RequireNotBusy();
        var candidate = ValidateAndClone(source).observations.ToDictionary(value => value.fact.result.expeditionId, StringComparer.Ordinal);
        if (!restorePublication.TryStage(() => Apply(candidate), () => { var previous = observations; return () => Apply(previous); })) Apply(candidate);
    }
    private void Apply(Dictionary<string, OffenseExpeditionResultObservationData> value)
    { observations = value; work.Clear(); foreach (var pair in value) if (pair.Value.pending || pair.Value.notificationPending) work.Add(pair.Key); }
    public string ParticipantId => "offense.result-publications";
    public void BeginRestoreCandidate() { RequireIdle(); restorePublication.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restorePublication.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restorePublication.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { restorePublication.Complete(); restoring = false; }
    private void RequireNotBusy()
    { if (activeId != null || recovering) throw new InvalidOperationException("offense-result-command-active"); }
    private void RequireIdle() { RequireNotBusy(); if (restoring) throw new InvalidOperationException("offense-result-restore-active"); }
    private static bool Delivered(string key, DomainFactDelivery result) => result.Completed && result.OperationKey == key
        && !string.IsNullOrWhiteSpace(result.PublicationId) && result.Fingerprint?.Length == 64
        && result.Fingerprint.All(value => value >= '0' && value <= '9' || value >= 'a' && value <= 'f' || value >= 'A' && value <= 'F');
    private static bool AllDelivered(OffenseExpeditionResultObservationData record, IReadOnlyList<DomainFactDelivery> results)
    {
        if (results.Count != 1 + record.stockPublicationKeys.Count || !Delivered(record.publicationKey, results[0])) return false;
        for (int index = 0; index < record.stockPublicationKeys.Count; index++)
            if (!Delivered(record.stockPublicationKeys[index], results[index + 1])) return false;
        return true;
    }
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;
}
