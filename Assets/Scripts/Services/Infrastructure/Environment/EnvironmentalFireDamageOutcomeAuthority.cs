using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;

public interface IEnvironmentalFireDamageOutcomeAuthority
{
    bool TryBegin(
        EnvironmentalFireDamageCommand command,
        string targetDisplayName,
        int absoluteDay,
        out EnvironmentalFireDamageOutcomeSaveRecord record,
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
        out EnvironmentalFireDamageOutcomeSaveRecord record);

    void CancelUncommitted(string operationId);
}

public interface IEnvironmentalFireDamageOutcomeQuery
{
    IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> Records { get; }
    bool TryGet(
        string operationId,
        out EnvironmentalFireDamageOutcomeSaveRecord record);
    string ProjectFacilityContribution(BuildingInstanceId facilityId);
}

public interface IEnvironmentalFireDamageOutcomePersistence
{
    IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> Capture();
    void Restore(
        IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> records);
}

/// <summary>
/// Save-owned fire damage/outcome join. Reservations are deliberately
/// process-local; the complete immutable receipt input is durable, so restore
/// recreates the same prepare request and never invents a different result.
/// </summary>
public sealed class EnvironmentalFireDamageOutcomeAuthority :
    IEnvironmentalFireDamageOutcomeAuthority,
    IEnvironmentalFireDamageOutcomeQuery,
    IEnvironmentalFireDamageOutcomePersistence
{
    private readonly IEnvironmentGameplayOutcomeCommitter outcomes;
    private readonly Dictionary<string, EnvironmentalFireDamageOutcomeSaveRecord>
        records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReservedEnvironmentOutcome>
        reservations = new(StringComparer.Ordinal);

    public EnvironmentalFireDamageOutcomeAuthority(
        IEnvironmentGameplayOutcomeCommitter outcomes)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> Records =>
        Capture();

    public bool TryBegin(
        EnvironmentalFireDamageCommand command,
        string targetDisplayName,
        int absoluteDay,
        out EnvironmentalFireDamageOutcomeSaveRecord record,
        out string failureReason)
    {
        string operationId = command.OperationId?.Trim() ?? string.Empty;
        string fingerprint = EnvironmentalFireDamageOutcomeIdentity
            .BuildRequestFingerprint(
                command,
                targetDisplayName,
                absoluteDay);
        if (records.TryGetValue(operationId, out EnvironmentalFireDamageOutcomeSaveRecord known))
        {
            if (!string.Equals(
                    known.requestFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                record = null;
                failureReason =
                    "environmental-fire-damage-operation-conflict";
                return false;
            }
            record = known.Clone();
            failureReason = string.Empty;
            return true;
        }

        if (operationId.Length == 0
            || !command.Target.IsValid
            || string.IsNullOrWhiteSpace(command.FireId)
            || string.IsNullOrWhiteSpace(targetDisplayName)
            || absoluteDay < 0
            || !float.IsFinite(command.Intensity)
            || command.Intensity <= 0f
            || command.Intensity > 1f
            || !float.IsFinite(command.RequestedDamage)
            || command.RequestedDamage <= 0f)
        {
            record = null;
            failureReason = "environmental-fire-damage-owner-invalid";
            return false;
        }

        long ownerRevision = EnvironmentalFireDamageOutcomeIdentity
            .BuildOwnerRevision(operationId);
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.FireDamageProducer,
            new GameplayOperationId(operationId),
            ownerRevision,
            0);
        var reservation = new EnvironmentOutcomeReservationSpec(
            resultKey,
            EnvironmentOutcomeIds.FireDamage,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision,
            participantCount: 2,
            metricCount: 3,
            subjectCount: 2,
            tagCount: 1,
            provenanceCount: 1,
            factCount: 3);
        if (!outcomes.TryReserve(
                reservation,
                out ReservedEnvironmentOutcome reserved,
                out failureReason))
        {
            record = null;
            return false;
        }

        var created = new EnvironmentalFireDamageOutcomeSaveRecord
        {
            operationId = operationId,
            requestFingerprint = fingerprint,
            fireId = command.FireId,
            targetKind = (int)command.Target.Kind,
            targetId = command.Target.TargetId,
            targetDisplayName = targetDisplayName.Trim(),
            positionX = command.Position.x,
            positionY = command.Position.y,
            intensity = command.Intensity,
            requestedDamage = command.RequestedDamage,
            appliedDamage = 0f,
            targetRemainsCombustible = true,
            absoluteDay = absoluteDay,
            ownerRevision = ownerRevision,
            phase = EnvironmentalFireDamageOutcomePhase.Prepared,
            outcomeDigest = string.Empty
        };
        records.Add(operationId, created);
        reservations.Add(operationId, reserved);
        record = created.Clone();
        failureReason = string.Empty;
        return true;
    }

    public bool TryCommitApplied(
        string operationId,
        float appliedDamage,
        bool targetRemainsCombustible,
        out EnvironmentalFireDamageResult result,
        out string failureReason)
    {
        result = default;
        if (!TrySetApplied(
                operationId,
                appliedDamage,
                targetRemainsCombustible,
                requireBuildingDestruction: false,
                out EnvironmentalFireDamageOutcomeSaveRecord record,
                out failureReason))
        {
            return false;
        }
        if (!TryCommitOutcome(record, out failureReason))
            return false;
        result = new EnvironmentalFireDamageResult(
            true,
            record.appliedDamage,
            record.targetRemainsCombustible);
        return true;
    }

    public bool TryStageAwaitingWorldRemoval(
        string operationId,
        float appliedDamage,
        out string failureReason)
    {
        if (!TrySetApplied(
                operationId,
                appliedDamage,
                targetRemainsCombustible: false,
                requireBuildingDestruction: true,
                out EnvironmentalFireDamageOutcomeSaveRecord record,
                out failureReason))
        {
            return false;
        }
        if (record.phase == EnvironmentalFireDamageOutcomePhase.OutcomeCommitted
            || record.phase ==
                EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval)
        {
            failureReason = string.Empty;
            return true;
        }
        record.phase =
            EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval;
        failureReason = string.Empty;
        return true;
    }

    public bool TryMarkDrainOwnerAcknowledged(
        string operationId,
        out string failureReason)
    {
        if (!records.TryGetValue(
                operationId ?? string.Empty,
                out EnvironmentalFireDamageOutcomeSaveRecord record)
            || record.phase is not
                EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
        {
            failureReason =
                "environmental-fire-damage-drain-owner-state-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryFinalizeAfterWorldRemoval(
        string operationId,
        out string failureReason)
    {
        if (!records.TryGetValue(
                operationId ?? string.Empty,
                out EnvironmentalFireDamageOutcomeSaveRecord record)
            || record.phase is not
                EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
        {
            failureReason =
                "environmental-fire-damage-post-removal-owner-missing";
            return false;
        }
        return TryCommitOutcome(record, out failureReason);
    }

    public bool TryGet(
        string operationId,
        out EnvironmentalFireDamageOutcomeSaveRecord record)
    {
        if (records.TryGetValue(
                operationId?.Trim() ?? string.Empty,
                out EnvironmentalFireDamageOutcomeSaveRecord found))
        {
            record = found.Clone();
            return true;
        }
        record = null;
        return false;
    }

    public void CancelUncommitted(string operationId)
    {
        string key = operationId?.Trim() ?? string.Empty;
        if (!records.TryGetValue(key, out EnvironmentalFireDamageOutcomeSaveRecord record)
            || record.phase != EnvironmentalFireDamageOutcomePhase.Prepared)
        {
            return;
        }
        if (reservations.Remove(key, out ReservedEnvironmentOutcome reserved))
            outcomes.Cancel(reserved);
        records.Remove(key);
    }

    public IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> Capture() =>
        records.Values
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

    public void Restore(
        IReadOnlyList<EnvironmentalFireDamageOutcomeSaveRecord> source)
    {
        var restored = new Dictionary<string,
            EnvironmentalFireDamageOutcomeSaveRecord>(StringComparer.Ordinal);
        foreach (EnvironmentalFireDamageOutcomeSaveRecord value in
                 source ?? Array.Empty<EnvironmentalFireDamageOutcomeSaveRecord>())
        {
            if (value == null || !restored.TryAdd(
                    value.operationId,
                    value.Clone()))
            {
                throw new InvalidOperationException(
                    "Environmental fire damage outcome restore is invalid.");
            }
        }
        foreach (ReservedEnvironmentOutcome reservation in reservations.Values)
            outcomes.Cancel(reservation);
        reservations.Clear();
        records.Clear();
        foreach (KeyValuePair<string, EnvironmentalFireDamageOutcomeSaveRecord> pair
                 in restored)
            records.Add(pair.Key, pair.Value);
    }

    public string ProjectFacilityContribution(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A facility ID is required.", nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-owner@1");
        digest.Append(facilityId.Value);
        EnvironmentalFireDamageOutcomeSaveRecord[] owned = records.Values
            .Where(value => value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(
                    value.targetId,
                    facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(owned.Length);
        foreach (EnvironmentalFireDamageOutcomeSaveRecord value in owned)
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
        IEnumerable<EnvironmentalFireDamageOutcomeSaveRecord> source)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A facility ID is required.", nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("environmental-fire-damage-owner@1");
        digest.Append(facilityId.Value);
        EnvironmentalFireDamageOutcomeSaveRecord[] owned = (source
                ?? Array.Empty<EnvironmentalFireDamageOutcomeSaveRecord>())
            .Where(value => value != null
                && value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(value.targetId, facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(owned.Length);
        foreach (EnvironmentalFireDamageOutcomeSaveRecord value in owned)
        {
            digest.Append(value.operationId);
            digest.Append(value.requestFingerprint);
            digest.Append((int)value.phase);
            digest.Append(value.appliedDamage.ToString("R", CultureInfo.InvariantCulture));
        }
        return digest.ComputeSha256();
    }

    private bool TrySetApplied(
        string operationId,
        float appliedDamage,
        bool targetRemainsCombustible,
        bool requireBuildingDestruction,
        out EnvironmentalFireDamageOutcomeSaveRecord record,
        out string failureReason)
    {
        if (!records.TryGetValue(
                operationId?.Trim() ?? string.Empty,
                out record)
            || !float.IsFinite(appliedDamage)
            || appliedDamage <= 0f
            || appliedDamage > record.requestedDamage * 2f + 0.001f
            || (requireBuildingDestruction
                && (record.targetKind !=
                        (int)EnvironmentalFireTargetKind.Building
                    || targetRemainsCombustible)))
        {
            failureReason = "environmental-fire-damage-result-invalid";
            return false;
        }
        if (record.appliedDamage > 0f
            && (Math.Abs(record.appliedDamage - appliedDamage) > 0.001f
                || record.targetRemainsCombustible !=
                    targetRemainsCombustible))
        {
            failureReason = "environmental-fire-damage-result-conflict";
            return false;
        }
        record.appliedDamage = appliedDamage;
        record.targetRemainsCombustible = targetRemainsCombustible;
        failureReason = string.Empty;
        return true;
    }

    private bool TryCommitOutcome(
        EnvironmentalFireDamageOutcomeSaveRecord record,
        out string failureReason)
    {
        EnvironmentalFireDamageOutcomeReceipt receipt = CreateReceipt(record);
        if (record.phase == EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
        {
            failureReason = string.Empty;
            return true;
        }

        PreparedEnvironmentOutcome prepared = default;
        bool hasReserved = reservations.Remove(
            record.operationId,
            out ReservedEnvironmentOutcome reserved);
        try
        {
            bool preparedSuccessfully = hasReserved
                ? outcomes.TryWriteReserved(
                    receipt,
                    reserved,
                    out prepared,
                    out failureReason)
                : outcomes.TryPrepare(
                    receipt,
                    out prepared,
                    out failureReason);
            if (!preparedSuccessfully)
            {
                if (!TryReconcileCommitted(record, receipt, out _))
                    return false;
                failureReason = string.Empty;
                return true;
            }

            EnvironmentOutcomeCommitResult commit = outcomes.Commit(
                prepared,
                record.ownerRevision);
            if (!commit.DurablyCommitted)
            {
                if (TryReconcileCommitted(record, receipt, out _))
                {
                    failureReason = string.Empty;
                    return true;
                }
                outcomes.Cancel(prepared);
                failureReason = commit.DetailCode;
                return false;
            }
            string canonicalDigest = commit.CanonicalDigest;
            if (canonicalDigest.Length != 64)
            {
                EnvironmentOutcomeCommitResult reconciled = outcomes.Reconcile(
                    receipt.Payload.ResultKey);
                if (reconciled.DurablyCommitted)
                    canonicalDigest = reconciled.CanonicalDigest;
            }
            record.phase = EnvironmentalFireDamageOutcomePhase.OutcomeCommitted;
            record.outcomeDigest = canonicalDigest;
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            if (TryReconcileCommitted(record, receipt, out _))
            {
                failureReason = string.Empty;
                return true;
            }
            if (prepared.IsValid)
                outcomes.Cancel(prepared);
            else if (hasReserved && reserved.IsValid)
                outcomes.Cancel(reserved);
            failureReason =
                "environmental-fire-damage-outcome-threw:"
                + exception.GetType().Name;
            return false;
        }
    }

    private bool TryReconcileCommitted(
        EnvironmentalFireDamageOutcomeSaveRecord record,
        in EnvironmentalFireDamageOutcomeReceipt receipt,
        out EnvironmentOutcomeCommitResult reconciled)
    {
        reconciled = outcomes.Reconcile(receipt.Payload.ResultKey);
        if (!reconciled.DurablyCommitted)
            return false;
        record.phase = EnvironmentalFireDamageOutcomePhase.OutcomeCommitted;
        record.outcomeDigest = reconciled.CanonicalDigest;
        return true;
    }

    private static EnvironmentalFireDamageOutcomeReceipt CreateReceipt(
        EnvironmentalFireDamageOutcomeSaveRecord record)
    {
        var command = new EnvironmentalFireDamageCommand(
            record.operationId,
            record.fireId,
            new EnvironmentalFireTargetRef(
                (EnvironmentalFireTargetKind)record.targetKind,
                record.targetId),
            new UnityEngine.Vector2Int(record.positionX, record.positionY),
            record.intensity,
            record.requestedDamage);
        var result = new EnvironmentalFireDamageResult(
            true,
            record.appliedDamage,
            record.targetRemainsCombustible);
        return EnvironmentOutcomeReceiptFactory.CreateFireDamage(
            command,
            result,
            record.targetDisplayName,
            record.absoluteDay,
            record.ownerRevision);
    }

}

public sealed class EnvironmentalFireDamageLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    private readonly IEnvironmentalFireDamageOutcomeQuery outcomes;

    public EnvironmentalFireDamageLifecycleContributor(
        IEnvironmentalFireDamageOutcomeQuery outcomes)
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
        EnvironmentalFireDamageOutcomeSaveRecord[] owned = outcomes.Records
            .Where(value => value != null
                && value.targetKind ==
                    (int)EnvironmentalFireTargetKind.Building
                && string.Equals(value.targetId, facilityId.Value,
                    StringComparison.Ordinal)
                && value.phase !=
                    EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();
        bool blocks = owned.Any(value => value.phase ==
            EnvironmentalFireDamageOutcomePhase.Prepared);
        string fingerprint = outcomes.ProjectFacilityContribution(facilityId);
        ProductionOutputLifecycleBlock[] lifecycleBlocks = blocks
            ? new[]
            {
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode
                        .EnvironmentalFireDamageOutcomePending,
                    owned.Count(value => value.phase ==
                        EnvironmentalFireDamageOutcomePhase.Prepared),
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

    private readonly IEnvironmentalFireDamageOutcomeAuthority authority;
    private readonly IEnvironmentalFireDamageOutcomeQuery query;
    private CheckpointCandidate activeCheckpointCandidate;

    public EnvironmentalFireDamageDestructiveDrainParticipant(
        IEnvironmentalFireDamageOutcomeAuthority authority,
        IEnvironmentalFireDamageOutcomeQuery query)
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
        EnvironmentalFireDamageOutcomeSaveRecord[] owners = PendingOwners(
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
        if (!TryResolve(context, out EnvironmentalFireDamageOutcomeSaveRecord owner,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || owner.phase is not
                EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
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
        if (!TryResolve(context, out EnvironmentalFireDamageOutcomeSaveRecord owner,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck
            || !MatchesJournalReceipt(context)
            || owner.phase is not
                EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval
                and not EnvironmentalFireDamageOutcomePhase.OutcomeCommitted
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

    private EnvironmentalFireDamageOutcomeSaveRecord[] PendingOwners(
        BuildingInstanceId facilityId) => query.Records
        .Where(value => value != null
            && value.targetKind ==
                (int)EnvironmentalFireTargetKind.Building
            && string.Equals(value.targetId, facilityId.Value,
                StringComparison.Ordinal)
            && value.phase !=
                EnvironmentalFireDamageOutcomePhase.OutcomeCommitted)
        .OrderBy(value => value.operationId, StringComparer.Ordinal)
        .ToArray();

    private bool TryResolve(
        ProductionFacilityDestructiveDrainStepContext context,
        out EnvironmentalFireDamageOutcomeSaveRecord record,
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
