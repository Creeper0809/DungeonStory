using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class MemoryErasureBossAwardPublicationData
{
    public MemoryErasureBossAwardFactData fact;
    public int dropoffX, dropoffY;
    public string publicationKey = string.Empty, publicationId = string.Empty, fingerprint = string.Empty;
    public bool delivered;
    public MemoryErasureBossAwardPublicationData Clone() => new()
    { fact = fact.Clone(), dropoffX = dropoffX, dropoffY = dropoffY,
        publicationKey = publicationKey, publicationId = publicationId, fingerprint = fingerprint, delivered = delivered };
}

[Serializable]
public sealed class MemoryErasureBossAwardPublicationSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<MemoryErasureBossAwardPublicationData> observations = new();
}

/// <summary>
/// Saved observation progress, not reward eligibility or item quantity authority.
/// A confirmed physical receipt permanently separates publication recovery from item creation.
/// </summary>
public sealed class MemoryErasureBossAwardPublicationRuntime : IDungeonRestoreTransactionParticipant
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRestorePublication restore = new();
    private Dictionary<string, MemoryErasureBossAwardPublicationData> rows = new(StringComparer.Ordinal);
    private string activeRegion;
    public bool IsBusy => activeRegion != null;
    public bool IsRestoring { get; private set; }
    public string LastFailure { get; private set; } = string.Empty;

    public MemoryErasureBossAwardPublicationRuntime(IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery)
    { this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public MemoryErasureBossAwardPublicationData CaptureForRegion(string regionId) =>
        rows.TryGetValue(regionId, out var row) ? row.Clone() : null;

    public bool TryBegin(string regionId, out string failure)
    {
        failure = string.Empty;
        if (!DomainFactValueSyntax.IsIdentity(regionId) || IsBusy || IsRestoring)
        { failure = "boss-award-publication-busy"; return false; }
        activeRegion = regionId; return true;
    }
    public void End(string regionId)
    {
        RequireActive(regionId); activeRegion = null;
    }
    public MemoryErasureBossAwardPublicationData Prepare(string regionId, string operationId,
        string regionName, int day, int dropoffX, int dropoffY)
    {
        RequireActive(regionId);
        if (rows.TryGetValue(regionId, out var existing))
        {
            if (existing.fact.operationId != operationId)
                throw new InvalidOperationException("boss-award-operation-conflict");
            return existing.Clone();
        }
        var row = new MemoryErasureBossAwardPublicationData
        {
            fact = new MemoryErasureBossAwardFactData { regionId = regionId, regionDisplayName = regionName,
                operationId = operationId, absoluteDay = day },
            dropoffX = dropoffX, dropoffY = dropoffY
        };
        row.fact.Validate(requirePhysical: false);
        rows.Add(regionId, row); return row.Clone();
    }
    public void RecordPhysical(string regionId, PhysicalItemSourcePublicationReceipt receipt,
        string regionName, int appliedDay)
    {
        RequireActive(regionId);
        var row = rows[regionId];
        if (!receipt.IsCommitted || receipt.OperationId != row.fact.operationId
            || receipt.ReasonCode != MemoryErasureSealBossAwardRules.PublicationReason
            || receipt.OutputQuantity != MemoryErasureSealItemRules.UseQuantity || receipt.OutputCommitIds.Count != 1)
            throw new ArgumentException("boss-award-physical-receipt-conflict");
        if (row.fact.physicalCommitId.Length != 0)
        {
            if (row.fact.physicalCommitId != receipt.OutputCommitIds[0])
                throw new InvalidOperationException("boss-award-physical-commit-conflict");
            return;
        }
        // A failed physical attempt is only intent. Freeze the occurrence name/day when the physical owner confirms it.
        var candidate = row.fact.Clone(); candidate.physicalCommitId = receipt.OutputCommitIds[0];
        candidate.regionDisplayName = regionName; candidate.absoluteDay = appliedDay; candidate.Validate();
        row.fact = candidate;
    }
    public bool TryDeliver(string regionId, out string failure)
    {
        RequireActive(regionId);
        var row = rows[regionId];
        failure = string.Empty;
        if (row.delivered) return true;
        IDomainFactStage stage = null;
        try
        {
            var fact = new MemoryErasureBossAwardFact(row.fact);
            DomainFactDelivery delivery;
            if (row.publicationKey.Length == 0)
            {
                if (!publisher.TryStage(fact, out stage, out failure))
                { LastFailure = failure; return false; }
                if (stage == null || !stage.IsActive || !DomainFactValueSyntax.IsText(stage.OperationKey, 1024, false))
                { failure = LastFailure = "boss-award-publication-stage-invalid"; return false; }
                row.publicationKey = stage.OperationKey;
                delivery = stage.Commit();
            }
            else
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, fact) }, results, out failure))
                { LastFailure = failure; return false; }
                delivery = results[0];
            }
            if (!delivery.Completed || delivery.OperationKey != row.publicationKey
                || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false) || !Hash(delivery.Fingerprint))
            { failure = LastFailure = "boss-award-observation-pending:" + delivery.Detail; return false; }
            row.publicationId = delivery.PublicationId; row.fingerprint = delivery.Fingerprint;
            row.delivered = true; LastFailure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = LastFailure = "boss-award-observation-pending:" + exception.Message; return false; }
        finally
        {
            try { stage?.Cancel(); }
            catch (Exception exception) when (Recoverable(exception))
            { LastFailure = "boss-award-stage-cancel-failed:" + exception.Message; }
        }
    }
    public MemoryErasureBossAwardPublicationSaveData Capture()
    {
        RequireIdle(); return new() { observations = rows.Values.OrderBy(row => row.fact.regionId, StringComparer.Ordinal)
            .Select(row => row.Clone()).ToList() };
    }
    public static MemoryErasureBossAwardPublicationSaveData ValidateAndClone(MemoryErasureBossAwardPublicationSaveData source)
    {
        if (source == null || source.version != MemoryErasureBossAwardPublicationSaveData.CurrentVersion || source.observations == null)
            throw new ArgumentException("boss-award-publication-save-invalid");
        var result = new MemoryErasureBossAwardPublicationSaveData();
        var regions = new HashSet<string>(StringComparer.Ordinal); var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in source.observations)
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, !row.delivered)
                || row.publicationKey.Length > 0 && !keys.Add(row.publicationKey)
                || (row.delivered ? !DomainFactValueSyntax.IsText(row.publicationId, 1024, false) || !Hash(row.fingerprint)
                    : row.publicationId != string.Empty || row.fingerprint != string.Empty))
                throw new ArgumentException("boss-award-publication-proof-invalid");
            row.fact.Validate(requirePhysical: false);
            if (!regions.Add(row.fact.regionId) || row.fact.physicalCommitId.Length == 0
                && (row.publicationKey.Length != 0 || row.delivered))
                throw new ArgumentException("boss-award-publication-phase-invalid");
            result.observations.Add(row.Clone());
        }
        return result;
    }
    public void Restore(MemoryErasureBossAwardPublicationSaveData source)
    {
        if (IsBusy) throw new InvalidOperationException("boss-award-publication-active");
        var candidate = ValidateAndClone(source).observations.ToDictionary(row => row.fact.regionId, StringComparer.Ordinal);
        if (!restore.TryStage(() => rows = candidate, () => { var previous = rows; return () => rows = previous; })) rows = candidate;
    }
    private void RequireActive(string regionId)
    { if (activeRegion != regionId || IsRestoring) throw new InvalidOperationException("boss-award-publication-not-active"); }
    private void RequireIdle()
    { if (IsBusy || IsRestoring) throw new InvalidOperationException("boss-award-publication-active"); }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    public string ParticipantId => "offense.memory-erasure-awards";
    public void BeginRestoreCandidate() { RequireIdle(); restore.Begin(); IsRestoring = true; }
    public void PublishRestoreCandidate() => restore.Publish();
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate() { try { restore.Discard(); } finally { IsRestoring = false; } }
    public void CompleteRestoreCandidate() { restore.Complete(); IsRestoring = false; }
}
