using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

/// <summary>Enlists frozen fire facts in the physical owner's debit. Recovery never consumes resources.</summary>
public sealed class EnvironmentalFireResourceTransaction : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, IStartable, ITickable
{
    private readonly IUiClock uiClock;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IPhysicalItemBatchDispositionService physical;
    private Dictionary<string, EnvironmentalFireResourceRecord> records = new(StringComparer.Ordinal);
    private Dictionary<string, EnvironmentalFireResourceRecord> staged, previous;
    private bool restoring;
    private float nextRecovery;
    private int cursor;
    private string lastFailure = string.Empty;

    public EnvironmentalFireResourceTransaction(IUiClock uiClock,
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery, IPhysicalItemBatchDispositionService physical)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.physical = physical ?? throw new ArgumentNullException(nameof(physical));
    }

    public IPhysicalItemDispositionEffect PrepareFuel(EnvironmentalFireFuelLossRequest request, Vector2Int position, int day)
    {
        if (request == null || !request.IsValid) throw new ArgumentException("fire-resource-fuel-request-invalid");
        return new Effect(this, new EnvironmentalFireResourceRecord
        {
            use = EnvironmentalFireResourceUse.FuelLoss, targetId = request.Target.TargetId,
            positionX = position.x, positionY = position.y, absoluteDay = day
        }, request.Quantity);
    }

    public IPhysicalItemDispositionEffect PrepareWater(string lease, string worker, string workerName,
        string fire, Vector2Int position, int quantity, int day) => new Effect(this, new EnvironmentalFireResourceRecord
        {
            use = EnvironmentalFireResourceUse.SuppressionWater, leaseId = lease, workerId = worker,
            workerName = workerName, fireId = fire, positionX = position.x, positionY = position.y, absoluteDay = day
        }, quantity);

    public bool TryGet(string operationId, out EnvironmentalFireResourceRecord record)
    {
        record = null;
        if (restoring || !records.TryGetValue(operationId ?? string.Empty, out var value)) return false;
        record = value.Clone(); return true;
    }

    public bool TryAcknowledge(string commitId, out string failure)
    {
        failure = "fire-resource-owner-missing";
        if (restoring) return false;
        var owner = records.Values.SingleOrDefault(value => value.physical.commitId == commitId);
        if (owner == null || !Replay(owner, out failure)) return false;
        if (!physical.Acknowledge(commitId, out failure)) return false;
        records.Remove(owner.physical.operationId);
        return true;
    }

    private bool Replay(EnvironmentalFireResourceRecord record, out string failure)
    {
        try
        {
            var result = new DomainFactDelivery[1];
            var replay = record.use == EnvironmentalFireResourceUse.FuelLoss
                ? DomainFactReplay.Create(record.publicationKey, new EnvironmentalFireFuelLostFact(record))
                : DomainFactReplay.Create(record.publicationKey, new EnvironmentalFireWaterConsumedFact(record));
            if (!recovery.ReplayBatch(new[] { replay }, result, out failure)) return false;
            var delivered = result[0];
            if (delivered.Completed && delivered.OperationKey == record.publicationKey
                && !string.IsNullOrEmpty(delivered.PublicationId) && !string.IsNullOrEmpty(delivered.Fingerprint)) return true;
            failure = "fire-resource-delivery-pending:" + delivered.Detail;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "fire-resource-recovery-failed:" + exception.Message; }
        return false;
    }

    public IReadOnlyList<EnvironmentalFireResourceRecord> Capture() => records.Values
        .OrderBy(value => value.physical.operationId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();

    public static void Validate(IReadOnlyList<EnvironmentalFireResourceRecord> source)
    {
        if (source == null) throw new InvalidOperationException("fire-resource-records-missing");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in source)
        {
            if (value == null || !DomainFactValueSyntax.IsText(value.publicationKey, 2048, false))
                throw new InvalidOperationException("fire-resource-publication-key-missing");
            if (value.use == EnvironmentalFireResourceUse.FuelLoss) _ = new EnvironmentalFireFuelLostFact(value);
            else _ = new EnvironmentalFireWaterConsumedFact(value);
            if (!keys.Add(value.physical.operationId)) throw new InvalidOperationException("fire-resource-operation-duplicate");
        }
    }
    public static bool MatchesPhysical(EnvironmentalFireResourceRecord value, in PhysicalItemBatchDispositionReceipt input)
    {
        if (value?.physical == null) return false;
        var expected = PhysicalItemDisposedFact.FromSave(value.physical).Receipt;
        return expected.Kind == input.Kind && expected.OperationId == input.OperationId
            && expected.ReasonCode == input.ReasonCode && expected.RequestFingerprint == input.RequestFingerprint
            && expected.Quantity == input.Quantity && expected.InputMassGrams == input.InputMassGrams
            && expected.OwnerRevision == input.OwnerRevision && expected.CommitId == input.CommitId
            && expected.SourceStackIds.SequenceEqual(input.SourceStackIds)
            && expected.SourceFacts.SequenceEqual(input.SourceFacts);
    }
    public void Restore(IReadOnlyList<EnvironmentalFireResourceRecord> source)
    {
        Validate(source);
        var next = source.ToDictionary(value => value.physical.operationId, value => value.Clone(), StringComparer.Ordinal);
        if (restoring) staged = next;
        else records = next;
    }
    public string ParticipantId => "environment.fire-resource-facts";
    public void BeginRestoreCandidate()
    {
        if (restoring) throw new InvalidOperationException("Fire resource restore is already active.");
        restoring = true;
    }
    public void PublishRestoreCandidate()
    {
        if (!restoring || staged == null) throw new InvalidOperationException("Fire resource candidate is missing.");
        previous = records; records = staged; staged = null;
    }
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate()
    {
        if (previous != null) records = previous;
        CompleteRestoreCandidate();
    }
    public void CompleteRestoreCandidate() { staged = previous = null; restoring = false; }
    public void Start() => Recover();
    public void OnRestoreCompleted() { cursor = 0; nextRecovery = 0; lastFailure = string.Empty; Recover(); }
    public void Tick()
    {
        if (restoring || records.Count == 0 || uiClock.Time < nextRecovery) return;
        nextRecovery = uiClock.Time + 0.5f; Recover();
    }
    public void Recover()
    {
        if (restoring || records.Count == 0) return;
        var pending = records.Values.ToArray();
        int start = cursor % pending.Length, count = Math.Min(8, pending.Length);
        string failure = string.Empty;
        for (int i = 0; i < count; i++)
            if (!Replay(pending[(start + i) % pending.Length], out string detail)) failure = detail;
        cursor = (start + count) % pending.Length;
        if (failure.Length > 0 && failure != lastFailure) Debug.LogWarning(failure);
        lastFailure = failure;
    }
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        and not StackOverflowException and not AccessViolationException;

    private sealed class Effect : IPhysicalItemDispositionEffect, IDomainFactStage
    {
        private readonly EnvironmentalFireResourceTransaction owner;
        private readonly EnvironmentalFireResourceRecord record;
        private readonly int quantity;
        private IDomainFactStage observation;
        private bool closed, attempted;
        public Effect(EnvironmentalFireResourceTransaction owner, EnvironmentalFireResourceRecord record, int quantity)
        { this.owner = owner; this.record = record; this.quantity = quantity; }
        public string OperationKey => observation?.OperationKey ?? string.Empty;
        public long OwnerRevision => observation?.OwnerRevision ?? 0;
        public bool IsReplay => observation?.IsReplay == true;
        public bool IsActive => !closed && observation?.IsActive == true;
        public bool TryStage(in PhysicalItemBatchDispositionReceipt input, long revision, out IDomainFactStage stage, out string failure)
        {
            stage = null; failure = "fire-resource-preparation-invalid";
            if (closed || observation != null || owner.restoring || input.Quantity != quantity || input.OwnerRevision != revision
                || owner.records.ContainsKey(input.OperationId)) return false;
            record.physical = new PhysicalItemBatchDispositionSaveData
            {
                kind = (int)input.Kind, operationId = input.OperationId, reasonCode = input.ReasonCode,
                requestFingerprint = input.RequestFingerprint, sourceStackIds = input.SourceStackIds.ToList(),
                quantity = input.Quantity, inputMassGrams = input.InputMassGrams, commitId = input.CommitId,
                ownerRevision = revision, defaultObservationDay = record.absoluteDay,
                sourceFacts = input.SourceFacts.Select(value => PhysicalItemSourceFactSaveCodec.ToSave(value)).ToList()
            };
            bool ready = record.use == EnvironmentalFireResourceUse.FuelLoss
                ? owner.facts.TryStage(new EnvironmentalFireFuelLostFact(record), out observation, out failure)
                : owner.facts.TryStage(new EnvironmentalFireWaterConsumedFact(record), out observation, out failure);
            if (!ready || observation == null || observation.IsReplay || observation.OwnerRevision != revision
                || !DomainFactValueSyntax.IsText(observation.OperationKey, 2048, false))
            { Cancel(); failure = "fire-resource-observation-rejected:" + failure; return false; }
            record.publicationKey = observation.OperationKey;
            stage = this; return true;
        }
        public DomainFactDelivery Commit()
        {
            if (attempted) return DomainFactDelivery.Uncertain("fire-resource-commit-already-attempted");
            if (!IsActive || owner.restoring || owner.records.ContainsKey(record.physical.operationId))
            { Cancel(); return DomainFactDelivery.Reject("fire-resource-stage-stale"); }
            // The physical owner has already applied its debit. Save the exact fact before crossing the observer boundary.
            owner.records.Add(record.physical.operationId, record.Clone());
            attempted = true; closed = true;
            DomainFactDelivery delivered = observation.Commit();
            if (!delivered.MayBeCommitted) owner.records.Remove(record.physical.operationId);
            return delivered;
        }
        public void Cancel()
        {
            if (attempted) return; // Unknown commit must retain the debit and fact.
            closed = true; observation?.Cancel();
        }
    }
}
