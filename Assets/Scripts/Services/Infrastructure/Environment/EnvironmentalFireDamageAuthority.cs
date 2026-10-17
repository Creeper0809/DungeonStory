using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public interface IEnvironmentalFireDamageAuthority
{
    bool TryBegin(
        EnvironmentalFireDamageCommand command,
        string targetDisplayName,
        int absoluteDay,
        out EnvironmentalFireDamageRecord record,
        out string failureReason);

    bool TryCommitApplied(
        string operationId,
        float appliedDamage,
        bool targetRemainsCombustible,
        out EnvironmentalFireDamageResult result,
        out string failureReason);

    bool TryStageAwaitingWorldRemoval(
        string operationId,
        float appliedDamage,
        out string failureReason);

    bool TryMarkDrainOwnerAcknowledged(
        string operationId,
        out string failureReason);

    bool TryFinalizeAfterWorldRemoval(
        string operationId,
        out string failureReason);

    bool TryGet(
        string operationId,
        out EnvironmentalFireDamageRecord record);

    void CancelUncommitted(string operationId);
}

public interface IEnvironmentalFireDamageQuery
{
    IReadOnlyList<EnvironmentalFireDamageRecord> Records { get; }
    bool TryGet(
        string operationId,
        out EnvironmentalFireDamageRecord record);
    string ProjectFacilityContribution(BuildingInstanceId facilityId);
}

public interface IEnvironmentalFireDamagePersistence
{
    IReadOnlyList<EnvironmentalFireDamageRecord> Capture();
    void Restore(
        IReadOnlyList<EnvironmentalFireDamageRecord> records);
}


/// <summary>Domain damage idempotency and frozen delivery state. No receipt or ledger contracts.</summary>
public sealed class EnvironmentalFireDamageAuthority :
    IEnvironmentalFireDamageAuthority, IEnvironmentalFireDamageQuery, IEnvironmentalFireDamagePersistence,
    IDungeonRestoreTransactionParticipant, IDungeonSaveRestoreCompletedHook, IStartable, ITickable
{
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private Dictionary<string, EnvironmentalFireDamageRecord> records = new(StringComparer.Ordinal);
    private Dictionary<string, DomainFactLease> reservations = new(StringComparer.Ordinal);
    private Dictionary<string, EnvironmentalFireDamageRecord> stagedRecords, previousRecords;
    private Dictionary<string, DomainFactLease> previousReservations;
    private bool restoreTransactionActive;
    private float nextRecovery;
    private int recoveryCursor;
    private string lastFailure = string.Empty;
    private readonly HashSet<string> pendingDelivery = new(StringComparer.Ordinal);

    public EnvironmentalFireDamageAuthority(IDomainFactPublisher facts, IDomainFactRecoveryPublisher recovery)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }
    public IReadOnlyList<EnvironmentalFireDamageRecord> Records => Capture();

    public bool TryBegin(EnvironmentalFireDamageCommand command, string targetDisplayName, int absoluteDay,
        out EnvironmentalFireDamageRecord record, out string failureReason)
    {
        record = null;
        if (restoreTransactionActive) { failureReason = "environmental-fire-damage-restore-active"; return false; }
        string operationId = command.OperationId?.Trim() ?? string.Empty;
        string fingerprint = EnvironmentalFireDamageIdentity.BuildRequestFingerprint(command, targetDisplayName, absoluteDay);
        if (records.TryGetValue(operationId, out var known))
        {
            if (known.requestFingerprint != fingerprint)
            { failureReason = "environmental-fire-damage-operation-conflict"; return false; }
            record = known.Clone(); failureReason = string.Empty; return true;
        }
        var created = new EnvironmentalFireDamageRecord
        {
            operationId = operationId, requestFingerprint = fingerprint, fireId = command.FireId,
            targetKind = (int)command.Target.Kind, targetId = command.Target.TargetId,
            targetDisplayName = targetDisplayName?.Trim(), positionX = command.Position.x, positionY = command.Position.y,
            intensity = command.Intensity, requestedDamage = command.RequestedDamage,
            targetRemainsCombustible = true, absoluteDay = absoluteDay,
            ownerRevision = EnvironmentalFireDamageIdentity.BuildOwnerRevision(operationId),
            phase = EnvironmentalFireDamagePhase.Prepared
        };
        DomainFactLease lease = default;
        try
        {
            if (!facts.TryReserve(new EnvironmentalFireDamageFact(created), out lease, out failureReason)) return false;
            created.publicationKey = lease.PublicationKey;
            EnvironmentalFireDamageFact.ValidateRecord(created);
            records.Add(operationId, created); reservations.Add(operationId, lease);
            record = created.Clone(); failureReason = string.Empty; return true;
        }
        catch (Exception exception)
        {
            if (lease.IsValid) facts.Cancel(lease);
            failureReason = "environmental-fire-damage-prepare-failed:" + exception.Message; return false;
        }
    }

    public bool TryCommitApplied(string operationId, float appliedDamage, bool targetRemainsCombustible,
        out EnvironmentalFireDamageResult result, out string failureReason)
    {
        result = default;
        bool alreadyApplied = records.TryGetValue(operationId ?? string.Empty, out var previous)
            && previous.phase == EnvironmentalFireDamagePhase.Applied;
        if (!TrySetApplied(operationId, appliedDamage, targetRemainsCombustible, false, out var record, out failureReason)) return false;
        if (record.phase == EnvironmentalFireDamagePhase.Delivered)
        { result = new EnvironmentalFireDamageResult(true, record.appliedDamage, record.targetRemainsCombustible); return true; }
        record.phase = EnvironmentalFireDamagePhase.Applied;
        pendingDelivery.Add(record.operationId);
        var delivery = Publish(record);
        if (!delivery.MayBeCommitted && !alreadyApplied)
        {
            // This branch precedes the caller's existing reversible damage rollback.
            record.phase = EnvironmentalFireDamagePhase.Prepared; record.appliedDamage = 0; record.targetRemainsCombustible = true;
            pendingDelivery.Remove(record.operationId);
            failureReason = "environmental-fire-damage-publication-rejected:" + delivery.Detail; return false;
        }
        result = new EnvironmentalFireDamageResult(true, record.appliedDamage, record.targetRemainsCombustible);
        failureReason = string.Empty; return true;
    }

    public bool TryStageAwaitingWorldRemoval(string operationId, float appliedDamage, out string failureReason)
    {
        if (!TrySetApplied(operationId, appliedDamage, false, true, out var record, out failureReason)) return false;
        if (record.phase == EnvironmentalFireDamagePhase.Prepared) record.phase = EnvironmentalFireDamagePhase.AwaitingWorldRemoval;
        return true;
    }

    public bool TryMarkDrainOwnerAcknowledged(string operationId, out string failureReason)
    {
        bool valid = records.TryGetValue(operationId ?? string.Empty, out var record)
            && record.phase is EnvironmentalFireDamagePhase.AwaitingWorldRemoval or EnvironmentalFireDamagePhase.Applied or EnvironmentalFireDamagePhase.Delivered;
        failureReason = valid ? string.Empty : "environmental-fire-damage-drain-owner-state-invalid";
        return valid;
    }

    public bool TryFinalizeAfterWorldRemoval(string operationId, out string failureReason)
    {
        if (!TryMarkDrainOwnerAcknowledged(operationId, out failureReason)) return false;
        var record = records[operationId];
        if (record.phase == EnvironmentalFireDamagePhase.Delivered) return true;
        // Only the destructive-drain finalizer crosses this boundary, never the timer.
        record.phase = EnvironmentalFireDamagePhase.Applied;
        pendingDelivery.Add(record.operationId);
        var delivery = Publish(record);
        failureReason = delivery.MayBeCommitted ? string.Empty : "environmental-fire-damage-post-removal-pending:" + delivery.Detail;
        return delivery.MayBeCommitted;
    }

    public bool TryGet(string operationId, out EnvironmentalFireDamageRecord record)
    {
        if (records.TryGetValue(operationId?.Trim() ?? string.Empty, out var found)) { record = found.Clone(); return true; }
        record = null; return false;
    }
    public void CancelUncommitted(string operationId)
    {
        string key = operationId?.Trim() ?? string.Empty;
        if (!records.TryGetValue(key, out var record) || record.phase != EnvironmentalFireDamagePhase.Prepared) return;
        if (reservations.Remove(key, out var lease) && lease.IsValid) facts.Cancel(lease);
        records.Remove(key);
    }
    public IReadOnlyList<EnvironmentalFireDamageRecord> Capture() => records.Values
        .OrderBy(value => value.operationId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
    public void Restore(IReadOnlyList<EnvironmentalFireDamageRecord> source)
    {
        if (source == null) throw new InvalidOperationException("Environmental fire damage records are missing.");
        var restored = new Dictionary<string, EnvironmentalFireDamageRecord>(StringComparer.Ordinal);
        foreach (var value in source)
        {
            EnvironmentalFireDamageFact.ValidateRecord(value);
            if (!restored.TryAdd(value.operationId, value.Clone())) throw new InvalidOperationException("Duplicate fire damage operation.");
        }
        if (restoreTransactionActive) { stagedRecords = restored; return; }
        foreach (var lease in reservations.Values) if (lease.IsValid) facts.Cancel(lease);
        reservations.Clear(); records = restored; RebuildPendingIndex();
    }
    public string ParticipantId => "environment.fire-damage-outcomes";
    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive) throw new InvalidOperationException("Environmental fire damage restore is already active.");
        restoreTransactionActive = true;
    }
    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || stagedRecords == null) throw new InvalidOperationException("Fire damage candidate is missing.");
        previousRecords = records; previousReservations = reservations;
        records = stagedRecords; stagedRecords = null; reservations = new(StringComparer.Ordinal);
        RebuildPendingIndex();
    }
    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();
    public void DiscardRestoreCandidate()
    {
        if (previousRecords != null) { records = previousRecords; reservations = previousReservations; RebuildPendingIndex(); }
        CompleteRestoreCandidate();
    }
    public void CompleteRestoreCandidate()
    {
        // Retired leases belong to the old dispatcher/ledger image; do not cancel against the incoming image.
        stagedRecords = previousRecords = null; previousReservations = null; restoreTransactionActive = false;
    }

    public string ProjectFacilityContribution(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A facility ID is required.", nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-owner@1");
        digest.Append(facilityId.Value);
        EnvironmentalFireDamageRecord[] owned = records.Values
            .Where(value => value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(
                    value.targetId,
                    facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamagePhase.Delivered)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(owned.Length);
        foreach (EnvironmentalFireDamageRecord value in owned)
        {
            digest.Append(value.operationId);
            digest.Append(value.requestFingerprint);
            digest.Append((int)value.phase);
            digest.Append(value.appliedDamage.ToString("R", CultureInfo.InvariantCulture));
        }
        return digest.ComputeSha256();
    }

    public static string ProjectFacilityContribution(
        BuildingInstanceId facilityId,
        IEnumerable<EnvironmentalFireDamageRecord> source)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A facility ID is required.", nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-owner@1");
        digest.Append(facilityId.Value);
        EnvironmentalFireDamageRecord[] owned = (source
                ?? Array.Empty<EnvironmentalFireDamageRecord>())
            .Where(value => value != null
                && value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(value.targetId, facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamagePhase.Delivered)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(owned.Length);
        foreach (EnvironmentalFireDamageRecord value in owned)
        {
            digest.Append(value.operationId);
            digest.Append(value.requestFingerprint);
            digest.Append((int)value.phase);
            digest.Append(value.appliedDamage.ToString("R", CultureInfo.InvariantCulture));
        }
        return digest.ComputeSha256();
    }


    private bool TrySetApplied(string operationId, float appliedDamage, bool targetRemainsCombustible,
        bool requireBuildingDestruction, out EnvironmentalFireDamageRecord record, out string failureReason)
    {
        failureReason = "environmental-fire-damage-result-invalid";
        if (restoreTransactionActive || !records.TryGetValue(operationId?.Trim() ?? string.Empty, out record))
        { record = null; return false; }
        if (!float.IsFinite(appliedDamage) || appliedDamage <= 0 || appliedDamage > record.requestedDamage * 2f + 0.001f
            || requireBuildingDestruction && (record.targetKind != (int)EnvironmentalFireTargetKind.Building || targetRemainsCombustible)) return false;
        if (record.appliedDamage > 0 && (record.appliedDamage != appliedDamage || record.targetRemainsCombustible != targetRemainsCombustible))
        { failureReason = "environmental-fire-damage-result-conflict"; return false; }
        record.appliedDamage = appliedDamage; record.targetRemainsCombustible = targetRemainsCombustible;
        failureReason = string.Empty; return true;
    }
    private DomainFactDelivery Publish(EnvironmentalFireDamageRecord record)
    {
        DomainFactDelivery result;
        bool hasLease = reservations.Remove(record.operationId, out var lease);
        try
        {
            var fact = new EnvironmentalFireDamageFact(record);
            if (hasLease && lease.IsValid) result = facts.Publish(lease, fact);
            else
            {
                var replies = new DomainFactDelivery[1];
                recovery.ReplayBatch(new[] { DomainFactReplay.Create(record.publicationKey, fact) }, replies, out string detail);
                result = replies[0];
                if (!result.MayBeCommitted && string.IsNullOrEmpty(result.Detail)) result = DomainFactDelivery.Reject(detail);
            }
        }
        catch (Exception exception) { result = DomainFactDelivery.Uncertain("environmental-fire-damage-reply-lost:" + exception.Message); }
        finally { if (hasLease && lease.IsValid) facts.Cancel(lease); }
        if (result.Completed && result.OperationKey == record.publicationKey
            && !string.IsNullOrWhiteSpace(result.PublicationId) && !string.IsNullOrWhiteSpace(result.Fingerprint))
        {
            record.phase = EnvironmentalFireDamagePhase.Delivered;
            record.publicationId = result.PublicationId; record.publicationFingerprint = result.Fingerprint;
            pendingDelivery.Remove(record.operationId);
        }
        return result;
    }
    public void Start() => Recover();
    public void OnRestoreCompleted() { recoveryCursor = 0; nextRecovery = 0; lastFailure = string.Empty; Recover(); }
    public void Tick()
    {
        if (restoreTransactionActive || pendingDelivery.Count == 0 || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 0.5f; Recover();
    }
    public bool Recover()
    {
        if (restoreTransactionActive) return false;
        var pending = pendingDelivery.ToArray();
        if (pending.Length == 0) { recoveryCursor = 0; lastFailure = string.Empty; return true; }
        int start = recoveryCursor % pending.Length, count = Math.Min(8, pending.Length);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            var record = records[pending[(start + index) % pending.Length]];
            var delivered = Publish(record);
            if (record.phase != EnvironmentalFireDamagePhase.Delivered) failure = "environmental-fire-damage-delivery-pending:" + delivered.Detail;
        }
        recoveryCursor = (start + count) % pending.Length;
        if (failure.Length > 0 && failure != lastFailure) Debug.LogWarning(failure);
        lastFailure = failure; return failure.Length == 0;
    }
    private void RebuildPendingIndex()
    {
        pendingDelivery.Clear();
        foreach (var value in records.Values.Where(value => value.phase == EnvironmentalFireDamagePhase.Applied))
            pendingDelivery.Add(value.operationId);
    }
}

public sealed class EnvironmentalFireDamageLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    private readonly IEnvironmentalFireDamageQuery outcomes;

    public EnvironmentalFireDamageLifecycleContributor(
        IEnvironmentalFireDamageQuery outcomes)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public string ContributorId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .EnvironmentalFireDamageOutcome;

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        EnvironmentalFireDamageRecord[] owned = outcomes.Records
            .Where(value => value != null
                && value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(value.targetId, facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamagePhase.Delivered)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        bool blocks = owned.Any(value => value.phase ==
            EnvironmentalFireDamagePhase.Prepared);
        string fingerprint = outcomes.ProjectFacilityContribution(facilityId);
        ProductionOutputLifecycleBlock[] lifecycleBlocks = blocks
            ? new[]
            {
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode
                        .EnvironmentalFireDamageOutcomePending,
                    owned.Count(value => value.phase ==
                        EnvironmentalFireDamagePhase.Prepared),
                    0L)
            }
            : Array.Empty<ProductionOutputLifecycleBlock>();
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            blocks,
            authorityRevision: owned.Length,
            activeRecordCount: owned.Length,
            ownedMassGrams: 0L,
            lifecycleBlocks,
            fingerprint,
            fingerprint);
    }
}

public sealed class EnvironmentalFireDamageDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainPostWorldRemovalFinalizer,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage
        });

    private readonly IEnvironmentalFireDamageAuthority authority;
    private readonly IEnvironmentalFireDamageQuery query;
    private CheckpointCandidate activeCheckpointCandidate;

    public EnvironmentalFireDamageDestructiveDrainParticipant(
        IEnvironmentalFireDamageAuthority authority,
        IEnvironmentalFireDamageQuery query)
    {
        this.authority = authority
            ?? throw new ArgumentNullException(nameof(authority));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .EnvironmentalFireDamageOutcome;
    public int ContractVersion => CurrentContractVersion;
    public string CheckpointGcParticipantId => ParticipantId;
    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        string contribution = query.ProjectFacilityContribution(
            context.FacilityId);
        EnvironmentalFireDamageRecord[] owners = PendingOwners(
            context.FacilityId);
        if (context.Cause !=
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity
            && owners.Length > 0)
        {
            throw new InvalidOperationException(
                "environmental-fire-damage-destructive-cause-conflict");
        }
        ProductionFacilityDestructiveDrainOwnerPlan[] plans = owners.Select(
                value => new ProductionFacilityDestructiveDrainOwnerPlan(
                    ProductionFacilityDestructiveDrainOwnerStableIds
                        .EnvironmentalFireDamage(value.operationId),
                    ProductionFacilityDestructiveDrainDisposition.Terminalize,
                    string.Empty,
                    value.requestFingerprint))
            .ToArray();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-destructive-plan@1");
        digest.Append(context.FacilityId.Value);
        digest.Append(contribution);
        digest.Append(plans.Length);
        foreach (ProductionFacilityDestructiveDrainOwnerPlan plan in plans)
        {
            digest.Append(plan.OwnerStableId);
            digest.Append(plan.RequestFingerprint);
        }
        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId,
            ContractVersion,
            contribution,
            digest.ComputeSha256(),
            plans);
    }

    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        if (!TryResolve(context, out _, out failureReason))
            return false;
        failureReason = string.Empty;
        return true;
    }

    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = query.ProjectFacilityContribution(
            context.FacilityId);
        if (!TryResolve(context, out EnvironmentalFireDamageRecord owner,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || owner.phase is not
                EnvironmentalFireDamagePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamagePhase.Delivered)
        {
            return Conflict(contribution);
        }
        return Applied(context, contribution);
    }

    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = query.ProjectFacilityContribution(
            context.FacilityId);
        if (!TryResolve(context, out EnvironmentalFireDamageRecord owner,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck
            || !MatchesJournalReceipt(context)
            || owner.phase is not
                EnvironmentalFireDamagePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamagePhase.Delivered
            || !authority.TryMarkDrainOwnerAcknowledged(
                owner.operationId,
                out _))
        {
            return Conflict(contribution);
        }
        return Applied(context, contribution);
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = query.ProjectFacilityContribution(
            context.FacilityId);
        if (!TryResolve(context, out _, out _)
            || context.Owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.Planned
                && !MatchesJournalReceipt(context))
        {
            return new ProductionFacilityDestructiveDrainRecoveryResult(
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
                Conflict(contribution));
        }
        ProductionFacilityDestructiveDrainRecoveryAction action =
            context.Owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    ProductionFacilityDestructiveDrainRecoveryAction
                        .ResumeCommit,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    ProductionFacilityDestructiveDrainRecoveryAction
                        .ResumeAcknowledge,
                _ => ProductionFacilityDestructiveDrainRecoveryAction
                    .AlreadyAcknowledged
            };
        return new ProductionFacilityDestructiveDrainRecoveryResult(
            action,
            action == ProductionFacilityDestructiveDrainRecoveryAction
                    .AlreadyAcknowledged
                ? Applied(context, contribution)
                : Deferred(contribution));
    }

    public bool TryFinalizeAfterWorldRemoval(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData row = entry?
            .participants?
            .SingleOrDefault(value => value != null
                && string.Equals(value.participantId, ParticipantId,
                    StringComparison.Ordinal));
        if (row == null)
        {
            failureReason =
                "environmental-fire-damage-destructive-row-missing";
            return false;
        }
        failureReason = string.Empty;
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                 row.owners ?? new List<ProductionFacilityDestructiveDrainOwnerSaveData>())
        {
            if (owner == null
                || owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
                || !TryParseOwnerOperationId(
                    owner.ownerStableId,
                    out string operationId)
                || !authority.TryFinalizeAfterWorldRemoval(
                    operationId,
                    out failureReason))
            {
                if (string.IsNullOrEmpty(failureReason))
                {
                    failureReason =
                        "environmental-fire-damage-post-removal-finalize-failed";
                }
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate)
    {
        if (activeCheckpointCandidate != null)
        {
            candidate = null;
            return CheckpointResult(
                context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .LiveAuthorityChanged,
                "A fire-damage checkpoint candidate is already active.");
        }
        string[] operations = (entries
                ?? Array.Empty<
                    ProductionFacilityDestructiveDrainEntrySaveData>())
            .Select(value => value?.operationId ?? string.Empty)
            .ToArray();
        if (entries == null
            || entries.Any(value => value == null
                || value.phase !=
                    ProductionFacilityDestructiveDrainPhase
                        .WorldRemovedAwaitingCheckpointGc))
        {
            candidate = null;
            return CheckpointResult(
                context,
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                "Fire-damage checkpoint input is invalid.");
        }
        activeCheckpointCandidate = new CheckpointCandidate(
            context,
            operations);
        candidate = activeCheckpointCandidate;
        return CheckpointResult(
            context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            operations.Length == 0
                ? ProductionFacilityDestructiveDrainCheckpointGcReason
                    .NoEligibleOperation
                : ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            "Fire-damage terminal outcome owners require no lower tombstone GC.",
            operations.Length);
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointCandidate exact = RequireCheckpointCandidate(candidate);
        exact.Published = true;
        return CheckpointResult(
            exact.Context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            "Fire-damage outcome history was retained for canonical replay.",
            exact.OperationIds.Count);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointCandidate exact = RequireCheckpointCandidate(candidate);
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        RequireCheckpointCandidate(candidate);
        activeCheckpointCandidate = null;
    }

    private EnvironmentalFireDamageRecord[] PendingOwners(
        BuildingInstanceId facilityId) => query.Records
        .Where(value => value != null
            && value.targetKind ==
                (int)EnvironmentalFireTargetKind.Building
            && string.Equals(value.targetId, facilityId.Value,
                StringComparison.Ordinal)
            && value.phase !=
                EnvironmentalFireDamagePhase.Delivered)
        .OrderBy(value => value.operationId, StringComparer.Ordinal)
        .ToArray();

    private bool TryResolve(
        ProductionFacilityDestructiveDrainStepContext context,
        out EnvironmentalFireDamageRecord record,
        out string failureReason)
    {
        record = null;
        if (!string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            || context.Owner == null
            || context.Owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(context.Owner.targetDestinationId)
            || !TryParseOwnerOperationId(
                context.Owner.ownerStableId,
                out string operationId)
            || !query.TryGet(operationId, out record)
            || record.targetKind !=
                (int)EnvironmentalFireTargetKind.Building
            || !string.Equals(record.targetId, context.FacilityId.Value,
                StringComparison.Ordinal)
            || !string.Equals(record.requestFingerprint,
                context.Owner.requestFingerprint, StringComparison.Ordinal)
            || !string.Equals(
                context.Owner.stepOperationId,
                ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                    context.OperationId,
                    ParticipantId,
                    context.Owner.ownerStableId),
                StringComparison.Ordinal))
        {
            failureReason =
                "environmental-fire-damage-destructive-owner-conflict";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool TryParseOwnerOperationId(
        string ownerStableId,
        out string operationId)
    {
        const string Prefix = "fire-damage:";
        operationId = string.Empty;
        if (string.IsNullOrEmpty(ownerStableId)
            || !ownerStableId.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        operationId = ownerStableId.Substring(Prefix.Length);
        return !string.IsNullOrEmpty(operationId);
    }

    private static ProductionFacilityDestructiveDrainStepResult Applied(
        ProductionFacilityDestructiveDrainStepContext context,
        string contribution)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-destructive-receipt@1");
        digest.Append(context.Owner.requestFingerprint);
        digest.Append(context.Owner.stepOperationId);
        string receipt = digest.ComputeSha256();
        return new ProductionFacilityDestructiveDrainStepResult(
            ProductionFacilityDestructiveDrainStepStatus.Applied,
            "environmental-fire-damage-stage:"
                + receipt.Substring(0, 24),
            receipt,
            contribution);
    }

    private static bool MatchesJournalReceipt(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-destructive-receipt@1");
        digest.Append(context.Owner.requestFingerprint);
        digest.Append(context.Owner.stepOperationId);
        string receipt = digest.ComputeSha256();
        return string.Equals(
                context.Owner.commitId,
                "environmental-fire-damage-stage:"
                    + receipt.Substring(0, 24),
                StringComparison.Ordinal)
            && string.Equals(
                context.Owner.receiptFingerprint,
                receipt,
                StringComparison.Ordinal);
    }

    private static ProductionFacilityDestructiveDrainStepResult Deferred(
        string contribution) => new(
        ProductionFacilityDestructiveDrainStepStatus.Deferred,
        string.Empty,
        string.Empty,
        contribution);

    private static ProductionFacilityDestructiveDrainStepResult Conflict(
        string contribution) => new(
        ProductionFacilityDestructiveDrainStepStatus.Conflict,
        string.Empty,
        string.Empty,
        contribution);

    private CheckpointCandidate RequireCheckpointCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointCandidate exact
            || !ReferenceEquals(exact, activeCheckpointCandidate))
        {
            throw new InvalidOperationException(
                "Fire-damage checkpoint candidate is stale or foreign.");
        }
        return exact;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult
        CheckpointResult(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            ProductionFacilityDestructiveDrainCheckpointGcStatus status,
            ProductionFacilityDestructiveDrainCheckpointGcReason reason,
            string message,
            int count = 0) => new(
            status,
            reason,
            context.CheckpointSequence,
            message,
            count);

    private sealed class CheckpointCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal CheckpointCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds)
        {
            Context = context;
            OperationIds = Array.AsReadOnly((operationIds
                    ?? Array.Empty<string>())
                .ToArray());
        }

        public string ParticipantId =>
            ProductionFacilityDestructiveDrainParticipantIds
                .EnvironmentalFireDamageOutcome;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
        internal bool Published { get; set; }
    }
}
