using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

[Serializable]
public sealed class OffenseNodeObservationData
{
    public OffenseNodeResolutionFactData fact = new();
    public string publicationKey = string.Empty;
    public bool pending, notificationPending;
    public List<OffenseNodeExperienceData> experience = new();
    public OffenseNodeObservationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, pending = pending, notificationPending = notificationPending,
        experience = experience.Select(value => value.Clone()).ToList() };
}
[Serializable]
public sealed class OffenseNodePublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<OffenseNodeObservationData> observations = new();
}

/// <summary>Owns applied node facts and delivery, never supplies, health, loot or experience.</summary>
public sealed class OffenseNodePublicationRuntime : ITickable, IDungeonRestoreTransactionParticipant
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus events;
    private readonly DungeonRestorePublication restorePublication = new();
    private Dictionary<(string, long), OffenseNodeObservationData> observations = new();
    private readonly HashSet<(string, long)> work = new();
    private OffenseNodeResolutionFact proposal;
    private DomainFactLease lease;
    private OffenseNodeObservationData applied;
    private bool restoring, recovering;
    private long nextRetry;
    public string LastFailure { get; private set; } = string.Empty;
    public OffenseNodePublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery, IGameEventBus events)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher)); this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); this.events = events ?? throw new ArgumentNullException(nameof(events)); }
    public long NextSequence(string expeditionId)
    {
        RequireIdle();
        if (!DomainFactValueSyntax.IsIdentity(expeditionId)) throw new ArgumentException("offense-node-expedition-id-invalid");
        return checked(observations.Keys.Where(value => value.Item1 == expeditionId).Select(value => value.Item2).DefaultIfEmpty(0L).Max() + 1L);
    }
    public bool TryBegin(OffenseNodeResolutionFact proposed, out string failure)
    {
        RequireIdle();
        var data = (proposed ?? throw new ArgumentNullException(nameof(proposed))).Snapshot();
        failure = string.Empty;
        if (data.resolved || data.sequence != NextSequence(data.expeditionId)
            || observations.Values.Any(value => value.fact.expeditionId == data.expeditionId
                && (value.fact.nodeId == data.nodeId || value.pending)))
        { failure = "offense-node-identity-or-pending-conflict"; return false; }
        proposal = proposed;
        try
        {
            if (publisher.TryReserve(proposed, out lease, out failure) && lease.IsValid && !string.IsNullOrWhiteSpace(lease.PublicationKey)) return true;
            failure = "offense-node-reservation:" + failure;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "offense-node-reservation:" + exception.GetType().Name + ":" + exception.Message; }
        LastFailure = failure;
        End();
        return false;
    }
    public DomainFactDelivery PublishApplied(OffenseNodeResolutionFact actual)
    {
        if (proposal == null || applied != null || actual == null || !actual.MatchesProposal(proposal) || !actual.Snapshot().resolved)
            throw new InvalidOperationException("offense-node-applied-fact-conflict");
        var data = actual.Snapshot();
        var key = (data.expeditionId, data.sequence);
        applied = new() { fact = data, publicationKey = lease.PublicationKey, pending = true, notificationPending = true };
        observations.Add(key, applied); work.Add(key);
        DomainFactDelivery result;
        try { result = publisher.Publish(lease, actual); }
        catch (Exception exception) when (Recoverable(exception))
        { result = DomainFactDelivery.Uncertain("offense-node-publish:" + exception.GetType().Name + ":" + exception.Message); }
        if (Delivered(applied.publicationKey, result)) applied.pending = false;
        else if (result.Completed) result = DomainFactDelivery.Uncertain("offense-node-proof-invalid");
        if (!result.MayBeCommitted) { observations.Remove(key); work.Remove(key); applied = null; }
        LastFailure = result.Completed ? string.Empty : result.Detail;
        return result;
    }
    public void RecordExperience(OffenseNodeExperienceData experience)
    {
        if (proposal == null || applied == null) throw new InvalidOperationException("offense-node-experience-without-applied-node");
        var observed = new OffenseNodeExperienceObservedFact(new OffenseNodeResolutionFact(applied.fact), experience);
        if (applied.experience.Any(value => value.characterId == experience.characterId))
            throw new InvalidOperationException("offense-node-experience-duplicate");
        applied.experience.Add(observed.Experience);
    }
    public void RecordFollowupFailure(Exception exception) => LastFailure = "offense-node-followup:" + exception.GetType().Name + ":" + exception.Message;
    public void End()
    {
        if (proposal == null) throw new InvalidOperationException("offense-node-command-missing");
        try { publisher.Cancel(lease); }
        catch (Exception exception) when (Recoverable(exception)) { LastFailure = "offense-node-cancel:" + exception.GetType().Name; }
        finally { lease = default; proposal = null; applied = null; }
    }
    public bool RecoverPending(out string failure)
    {
        RequireIdle(); failure = string.Empty; recovering = true;
        try
        {
            foreach (var key in work.OrderBy(value => value.Item1, StringComparer.Ordinal).ThenBy(value => value.Item2).ToArray())
            {
                var row = observations[key]; var fact = new OffenseNodeResolutionFact(row.fact);
                if (row.pending)
                {
                    var replies = new DomainFactDelivery[1];
                    if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, fact) }, replies, out failure) || !Delivered(row.publicationKey, replies[0]))
                    { failure = "offense-node-recovery-pending:" + failure + ":" + replies[0].Detail; LastFailure = failure; return false; }
                    row.pending = false;
                }
                if (row.notificationPending)
                {
                    row.notificationPending = false;
                    foreach (var experience in row.experience)
                    {
                        try { events.Publish(new OffenseNodeExperienceObservedFact(fact, experience)); }
                        catch (Exception exception) when (Recoverable(exception)) { RecordFollowupFailure(exception); }
                    }
                }
                work.Remove(key);
            }
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "offense-node-recovery:" + exception.GetType().Name + ":" + exception.Message; LastFailure = failure; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (work.Count == 0 || proposal != null || recovering || restoring || Stopwatch.GetTimestamp() < nextRetry) return;
        nextRetry = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
        RecoverPending(out _);
    }
    public OffenseNodePublicationSaveData Capture()
    {
        RequireNotBusy();
        return new() { observations = observations.OrderBy(value => value.Key.Item1, StringComparer.Ordinal).ThenBy(value => value.Key.Item2).Select(value => value.Value.Clone()).ToList() };
    }
    public static OffenseNodePublicationSaveData ValidateAndClone(OffenseNodePublicationSaveData source)
    {
        if (source == null || source.version != OffenseNodePublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("offense-node-save-invalid");
        var keys = new HashSet<(string, string)>(); var sequences = new Dictionary<string, long>(StringComparer.Ordinal);
        var pendingExpeditions = new HashSet<string>(StringComparer.Ordinal);
        var result = new OffenseNodePublicationSaveData();
        foreach (var row in source.observations.OrderBy(value => value?.fact?.expeditionId, StringComparer.Ordinal).ThenBy(value => value?.fact?.sequence))
        {
            if (row?.fact == null || row.experience == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false) || row.pending && !row.notificationPending)
                throw new ArgumentException("offense-node-observation-invalid");
            var fact = new OffenseNodeResolutionFact(row.fact); var data = fact.Snapshot();
            sequences.TryGetValue(data.expeditionId, out long previous);
            if (!data.resolved || data.sequence != previous + 1L || pendingExpeditions.Contains(data.expeditionId)
                || !keys.Add((data.expeditionId, data.nodeId)))
                throw new ArgumentException("offense-node-observation-identity-conflict");
            sequences[data.expeditionId] = data.sequence;
            if (row.pending) pendingExpeditions.Add(data.expeditionId);
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in row.experience)
            {
                _ = new OffenseNodeExperienceObservedFact(fact, item);
                if (!members.Add(item.characterId)) throw new ArgumentException("offense-node-experience-duplicate");
            }
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(OffenseNodePublicationSaveData source)
    {
        RequireNotBusy();
        var candidate = ValidateAndClone(source).observations.ToDictionary(value => (value.fact.expeditionId, value.fact.sequence));
        if (!restorePublication.TryStage(() => Apply(candidate), () => { var previous = observations; return () => Apply(previous); })) Apply(candidate);
    }
    private void Apply(Dictionary<(string, long), OffenseNodeObservationData> value)
    { observations = value; work.Clear(); foreach (var pair in value) if (pair.Value.pending || pair.Value.notificationPending) work.Add(pair.Key); }
    public string ParticipantId => "offense.node-publications";
    public void BeginRestoreCandidate() { RequireIdle(); restorePublication.Begin(); restoring = true; }
    public void PublishRestoreCandidate() => restorePublication.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restorePublication.Discard(); } finally { restoring = false; } }
    public void CompleteRestoreCandidate() { restorePublication.Complete(); restoring = false; }
    private void RequireNotBusy() { if (proposal != null || recovering) throw new InvalidOperationException("offense-node-command-active"); }
    private void RequireIdle() { RequireNotBusy(); if (restoring) throw new InvalidOperationException("offense-node-restore-active"); }
    private static bool Delivered(string key, DomainFactDelivery reply) => reply.Completed && reply.OperationKey == key
        && !string.IsNullOrWhiteSpace(reply.PublicationId) && reply.Fingerprint?.Length == 64
        && reply.Fingerprint.All(value => value >= '0' && value <= '9' || value >= 'a' && value <= 'f' || value >= 'A' && value <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;
}
