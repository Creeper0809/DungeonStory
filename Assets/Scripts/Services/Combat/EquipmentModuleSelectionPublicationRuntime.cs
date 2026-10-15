using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class EquipmentModuleSelectionPublicationData
{
    public EquipmentModuleSelectedFactData fact;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string fingerprint = string.Empty;
    public bool pending;
    public EquipmentModuleSelectionPublicationData Clone() => new()
    { fact = fact.Clone(), publicationKey = publicationKey, publicationId = publicationId,
        fingerprint = fingerprint, pending = pending };
}

[Serializable]
public sealed class EquipmentModuleSelectionPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<EquipmentModuleSelectionPublicationData> observations = new();
}

/// <summary>Lifetime-independent applied facts; recovery never selects modules or consumes evidence.</summary>
public sealed class EquipmentModuleSelectionPublicationRuntime : IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook, VContainer.Unity.ITickable
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private readonly System.Diagnostics.Stopwatch retryClock = System.Diagnostics.Stopwatch.StartNew();
    private Dictionary<(string, long), EquipmentModuleSelectionPublicationData> rows = new();
    private readonly HashSet<string> active = new(StringComparer.Ordinal);
    // Failed reversible transactions are not saveable. Keep their exact rollback,
    // including evidence tokens, until it succeeds; equipment lifetime cannot discard it.
    private readonly Dictionary<string, Func<bool>> rollbacks = new(StringComparer.Ordinal);
    private bool recovering;
    public void RequireIdleMutation() => RequireIdle();
    public bool TryReserve(EquipmentModuleSelectedFact fact, out DomainFactLease lease, out string failure) =>
        publisher.TryReserve(fact, out lease, out failure);
    public void Cancel(DomainFactLease lease) => publisher.Cancel(lease);
    public bool IsRestoring { get; private set; }
    public bool IsBusy => active.Count != 0 || rollbacks.Count != 0 || recovering;
    public string LastFailure { get; private set; } = string.Empty;

    public EquipmentModuleSelectionPublicationRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public EquipmentModuleSelectionPublicationData PendingForEquipment(string equipmentInstanceId) => rows.Values
        .SingleOrDefault(row => row.pending && row.fact.equipmentInstanceId == equipmentInstanceId)?.Clone();

    public bool TryBegin(string equipmentInstanceId, out string failure)
    {
        failure = string.Empty;
        if (!DomainFactValueSyntax.IsIdentity(equipmentInstanceId) || IsRestoring || IsBusy
            || PendingForEquipment(equipmentInstanceId) != null)
        { failure = "equipment-module-selection-publication-busy"; return false; }
        active.Add(equipmentInstanceId); return true;
    }
    public void End(string equipmentInstanceId)
    { if (!active.Remove(equipmentInstanceId)) throw new InvalidOperationException("equipment-module-selection-source-not-active"); }

    public void RetainRollback(string equipmentInstanceId, Func<bool> retry)
    {
        if (!active.Contains(equipmentInstanceId) || retry == null || rollbacks.ContainsKey(equipmentInstanceId))
            throw new InvalidOperationException("equipment-module-selection-rollback-contract-invalid");
        rollbacks.Add(equipmentInstanceId, retry);
        LastFailure = "equipment-module-selection-evidence-rollback-pending";
    }

    public DomainFactDelivery PublishApplied(EquipmentModuleSelectedFact fact, DomainFactLease lease)
    {
        var data = fact.Snapshot();
        var key = (data.equipmentInstanceId, data.afterRevision);
        if (!active.Contains(data.equipmentInstanceId) || IsRestoring || recovering || !lease.IsValid || rows.ContainsKey(key))
            throw new InvalidOperationException("equipment-module-selection-publication-contract-invalid");
        var row = new EquipmentModuleSelectionPublicationData
        { fact = data, publicationKey = lease.PublicationKey, pending = true };
        rows.Add(key, row);
        DomainFactDelivery delivery;
        try { delivery = publisher.Publish(lease, fact); }
        catch
        {
            // The neutral port contract defines an escaped exception as pre-commit.
            rows.Remove(key); throw;
        }
        if (!delivery.MayBeCommitted)
        { rows.Remove(key); LastFailure = delivery.Detail; return delivery; }
        if (Accept(row, delivery)) { LastFailure = string.Empty; return delivery; }
        LastFailure = "equipment-module-selection-observation-pending:" + delivery.Detail;
        return DomainFactDelivery.Uncertain(LastFailure);
    }

    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (IsRestoring || recovering || active.Count != 0)
        { failure = "equipment-module-selection-recovery-busy"; return false; }
        recovering = true;
        try
        {
            foreach (var rollback in rollbacks.ToArray())
            {
                if (!rollback.Value())
                { failure = "equipment-module-selection-evidence-rollback-pending"; LastFailure = failure; return false; }
                rollbacks.Remove(rollback.Key);
            }
            foreach (var row in rows.Values.Where(row => row.pending)
                .OrderBy(row => row.fact.equipmentInstanceId, StringComparer.Ordinal).ThenBy(row => row.fact.afterRevision))
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                    new EquipmentModuleSelectedFact(row.fact)) }, results, out failure) || !Accept(row, results[0]))
                { failure = "equipment-module-selection-recovery-pending:" + failure; LastFailure = failure; return false; }
            }
            LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "equipment-module-selection-recovery:" + exception.Message; LastFailure = failure; return false; }
        finally { recovering = false; }
    }
    public void Tick()
    {
        if (IsRestoring || active.Count != 0 || recovering || retryClock.ElapsedMilliseconds < 1000
            || rollbacks.Count == 0 && !rows.Values.Any(row => row.pending)) return;
        retryClock.Restart(); RecoverPending(out _);
    }
    public void OnRestoreCompleted() => RecoverPending(out _);

    private static bool Accept(EquipmentModuleSelectionPublicationData row, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != row.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint)) return false;
        row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint; row.pending = false; return true;
    }
    public EquipmentModuleSelectionPublicationSaveData Capture()
    {
        RequireIdle();
        return new() { observations = rows.Values.OrderBy(row => row.fact.equipmentInstanceId, StringComparer.Ordinal)
            .ThenBy(row => row.fact.afterRevision).Select(row => row.Clone()).ToList() };
    }
    public static EquipmentModuleSelectionPublicationSaveData ValidateAndClone(EquipmentModuleSelectionPublicationSaveData source)
    {
        if (source == null || source.version != EquipmentModuleSelectionPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("equipment-module-selection-publication-save-invalid");
        var result = new EquipmentModuleSelectionPublicationSaveData();
        var identities = new HashSet<(string, long)>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        var pendingEquipments = new HashSet<string>(StringComparer.Ordinal);
        var lastRevisions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var row in source.observations.OrderBy(row => row?.fact?.equipmentInstanceId, StringComparer.Ordinal)
            .ThenBy(row => row?.fact?.afterRevision))
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false)
                || !keys.Add(row.publicationKey)
                || (row.pending ? row.publicationId != string.Empty || row.fingerprint != string.Empty
                    : !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)))
                throw new ArgumentException("equipment-module-selection-publication-proof-invalid");
            row.fact.Validate();
            if (!identities.Add((row.fact.equipmentInstanceId, row.fact.afterRevision)) || pendingEquipments.Contains(row.fact.equipmentInstanceId)
                || row.fact.beforeRevision != (lastRevisions.TryGetValue(row.fact.equipmentInstanceId, out long previous) ? previous : 0L))
                throw new ArgumentException("equipment-module-selection-publication-history-invalid");
            lastRevisions[row.fact.equipmentInstanceId] = row.fact.afterRevision;
            if (row.pending) pendingEquipments.Add(row.fact.equipmentInstanceId);
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(EquipmentModuleSelectionPublicationSaveData source)
    {
        if (IsBusy) throw new InvalidOperationException("equipment-module-selection-publication-active");
        var candidate = ValidateAndClone(source).observations.ToDictionary(row => (row.fact.equipmentInstanceId, (long)row.fact.afterRevision));
        if (!restore.TryStage(() => rows = candidate, () => { var previous = rows; return () => rows = previous; })) rows = candidate;
    }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireIdle()
    { if (IsBusy || IsRestoring) throw new InvalidOperationException("equipment-module-selection-publication-active"); }
    public string ParticipantId => "equipment.module-selections";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
