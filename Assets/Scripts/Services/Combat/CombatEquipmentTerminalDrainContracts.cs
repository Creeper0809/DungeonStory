using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum CombatEquipmentTerminalSourceKind
{
    CraftOrder = 0,
    RepairOrder = 1
}

public enum CombatEquipmentTerminalDrainPhase
{
    PreparedAwaitingInputDestinationReceipt = 0,
    InputDestinationReceiptRecordedAwaitingAcknowledgement = 1,
    InputDestinationAcknowledgedAwaitingTerminalEffects = 2,
    TerminalEffectsCommittedAwaitingOwnerAcknowledgement = 3,
    OwnerAcknowledgedAwaitingCheckpointGc = 4
}

public enum CombatEquipmentTerminalDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public enum CombatEquipmentTerminalEffectStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public readonly struct CombatEquipmentTerminalMassAccounting
{
    public CombatEquipmentTerminalMassAccounting(
        int pendingInputQuantity,
        long pendingInputMassGrams,
        int wipInputQuantity,
        long wipInputMassGrams,
        long committedOutputMassGrams,
        long declaredLossMassGrams)
    {
        PendingInputQuantity = pendingInputQuantity;
        PendingInputMassGrams = pendingInputMassGrams;
        WipInputQuantity = wipInputQuantity;
        WipInputMassGrams = wipInputMassGrams;
        CommittedOutputMassGrams = committedOutputMassGrams;
        DeclaredLossMassGrams = declaredLossMassGrams;
    }

    public int PendingInputQuantity { get; }
    public long PendingInputMassGrams { get; }
    public int WipInputQuantity { get; }
    public long WipInputMassGrams { get; }
    public long CommittedOutputMassGrams { get; }
    public long DeclaredLossMassGrams { get; }
}

/// <summary>
/// Immutable gameplay subject captured from the live craft/repair authority.
/// The mutable combat save DTO is retained only as a canonical frozen payload;
/// destructive commands never accept that DTO directly.
/// </summary>
public sealed class CombatEquipmentTerminalFrozenSubject
{
    private readonly CombatEquipmentTerminalFrozenSourceSaveData frozen;

    private CombatEquipmentTerminalFrozenSubject(
        CombatEquipmentTerminalFrozenSourceSaveData frozen)
    {
        this.frozen = frozen?.Clone()
            ?? throw new ArgumentNullException(nameof(frozen));
        if (!CombatEquipmentTerminalDrainCanonical.IsValidFrozenSource(
                this.frozen))
        {
            throw new ArgumentException(
                "Combat equipment terminal source is not canonical.",
                nameof(frozen));
        }
    }

    public CombatEquipmentTerminalSourceKind SourceKind => frozen.sourceKind;
    public string OwnerStableId => frozen.ownerStableId;
    public string SourceId => frozen.sourceId;
    public string FacilityId => frozen.facilityId;
    public string InputDestinationId => frozen.inputDestinationId;
    public string SourcePayload => frozen.sourcePayload;
    public string SourceFingerprint => frozen.sourceFingerprint;
    public int PendingInputQuantity => frozen.pendingInputQuantity;
    public long PendingInputMassGrams => frozen.pendingInputMassGrams;
    public int WipInputQuantity => frozen.wipInputQuantity;
    public long WipInputMassGrams => frozen.wipInputMassGrams;
    public long CommittedOutputMassGrams => frozen.committedOutputMassGrams;
    public long DeclaredLossMassGrams => frozen.declaredLossMassGrams;

    public static bool TryCreateCraftOrder(
        CombatEquipmentCraftOrderSaveData source,
        CombatEquipmentTerminalMassAccounting mass,
        out CombatEquipmentTerminalFrozenSubject subject,
        out string failureReason) => TryCreate(
        CombatEquipmentTerminalSourceKind.CraftOrder,
        source?.orderId,
        source?.facilityPersistentId,
        source?.materialDestinationId,
        source == null ? string.Empty : JsonUtility.ToJson(source),
        mass,
        out subject,
        out failureReason);

    public static bool TryCreateRepairOrder(
        CombatEquipmentRepairOrder source,
        CombatEquipmentTerminalMassAccounting mass,
        out CombatEquipmentTerminalFrozenSubject subject,
        out string failureReason) => TryCreate(
        CombatEquipmentTerminalSourceKind.RepairOrder,
        source?.orderId,
        source?.facilityBuildingId,
        source?.FacilityDestinationId,
        source == null ? string.Empty : JsonUtility.ToJson(source),
        mass,
        out subject,
        out failureReason);

    internal static CombatEquipmentTerminalFrozenSubject FromSave(
        CombatEquipmentTerminalFrozenSourceSaveData source) => new(source);

    internal CombatEquipmentTerminalFrozenSourceSaveData CaptureFrozen() =>
        frozen.Clone();

    private static bool TryCreate(
        CombatEquipmentTerminalSourceKind sourceKind,
        string sourceId,
        string facilityId,
        string inputDestinationId,
        string sourcePayload,
        CombatEquipmentTerminalMassAccounting mass,
        out CombatEquipmentTerminalFrozenSubject subject,
        out string failureReason)
    {
        subject = null;
        failureReason = string.Empty;
        if (string.IsNullOrEmpty(sourceId)
            || !string.Equals(sourceId, sourceId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrEmpty(facilityId)
            || !string.Equals(facilityId, facilityId.Trim(),
                StringComparison.Ordinal)
            || (inputDestinationId != null
                && !string.Equals(inputDestinationId,
                    inputDestinationId.Trim(), StringComparison.Ordinal))
            || !CombatEquipmentTerminalDrainCanonical.TryOwnerStableId(
                sourceKind,
                sourceId,
                out string ownerStableId))
        {
            failureReason = "combat-equipment-terminal-source-identity-invalid";
            return false;
        }
        CombatEquipmentTerminalFrozenSourceSaveData candidate = new()
        {
            sourceKind = sourceKind,
            ownerStableId = ownerStableId,
            sourceId = sourceId ?? string.Empty,
            facilityId = facilityId ?? string.Empty,
            inputDestinationId = inputDestinationId ?? string.Empty,
            sourcePayload = sourcePayload ?? string.Empty,
            pendingInputQuantity = mass.PendingInputQuantity,
            pendingInputMassGrams = mass.PendingInputMassGrams,
            wipInputQuantity = mass.WipInputQuantity,
            wipInputMassGrams = mass.WipInputMassGrams,
            committedOutputMassGrams = mass.CommittedOutputMassGrams,
            declaredLossMassGrams = mass.DeclaredLossMassGrams
        };
        candidate.sourceFingerprint = CombatEquipmentTerminalDrainCanonical
            .CreateSourceFingerprint(candidate);
        if (!CombatEquipmentTerminalDrainCanonical.IsValidFrozenSource(candidate))
        {
            failureReason = "combat-equipment-terminal-source-invalid";
            return false;
        }
        subject = new CombatEquipmentTerminalFrozenSubject(candidate);
        return true;
    }
}

/// <summary>
/// One prepare-time capture that binds the immutable combat owner projection to
/// the exact Items custody closure used to create the child drain. A destination
/// is captured even when its current quantity is zero, so a concurrent arrival
/// cannot be omitted merely because an earlier owner read observed no input.
/// </summary>
public sealed class CombatEquipmentTerminalPreparedSource
{
    private readonly CombatEquipmentTerminalFrozenSubject source;
    private readonly ProductionInputDestinationCustodySourceSnapshot custody;

    private CombatEquipmentTerminalPreparedSource(
        CombatEquipmentTerminalFrozenSubject source,
        ProductionInputDestinationCustodySourceSnapshot custody)
    {
        this.source = CombatEquipmentTerminalFrozenSubject.FromSave(
            source.CaptureFrozen());
        this.custody = custody == null
            ? null
            : new ProductionInputDestinationCustodySourceSnapshot(
                custody.SourceDestinationId,
                custody.MassAuthorityRevision,
                custody.SourceOwnershipFingerprint,
                custody.SourceStacks,
                custody.SourceOperations,
                custody.SourceActors,
                custody.InputQuantity,
                custody.InputMassGrams);
    }

    public CombatEquipmentTerminalFrozenSubject Source =>
        CombatEquipmentTerminalFrozenSubject.FromSave(source.CaptureFrozen());

    public ProductionInputDestinationCustodySourceSnapshot Custody =>
        custody == null
            ? null
            : new ProductionInputDestinationCustodySourceSnapshot(
                custody.SourceDestinationId,
                custody.MassAuthorityRevision,
                custody.SourceOwnershipFingerprint,
                custody.SourceStacks,
                custody.SourceOperations,
                custody.SourceActors,
                custody.InputQuantity,
                custody.InputMassGrams);

    public static bool TryCreate(
        CombatEquipmentTerminalFrozenSubject source,
        ProductionInputDestinationCustodySourceSnapshot custody,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        if (source == null)
        {
            failureReason = "combat-equipment-terminal-prepared-source-missing";
            return false;
        }
        bool hasDestination = !string.IsNullOrEmpty(source.InputDestinationId);
        if (hasDestination != (custody != null)
            || custody != null
                && !ProductionInputDestinationCustodyDrainContract
                    .IsValidSourceSnapshot(custody)
            || custody != null
                && (!string.Equals(
                        source.InputDestinationId,
                        custody.SourceDestinationId,
                        StringComparison.Ordinal)
                    || source.PendingInputQuantity != custody.InputQuantity
                    || source.PendingInputMassGrams != custody.InputMassGrams)
            || !hasDestination
                && (source.PendingInputQuantity != 0
                    || source.PendingInputMassGrams != 0L))
        {
            failureReason =
                "combat-equipment-terminal-prepared-custody-drift";
            return false;
        }
        prepared = new CombatEquipmentTerminalPreparedSource(source, custody);
        return true;
    }
}

public sealed class CombatEquipmentTerminalDrainRequest
{
    public CombatEquipmentTerminalDrainRequest(
        string parentOperationId,
        string stepOperationId,
        CombatEquipmentTerminalFrozenSubject source,
        ProductionInputDestinationCustodyDrainRequest childRequest,
        string requestFingerprint)
        : this(
            parentOperationId,
            stepOperationId,
            source,
            childRequest?.StepOperationId,
            childRequest?.RequestFingerprint,
            requestFingerprint)
    {
        ChildRequest = CloneChild(childRequest);
    }

    public CombatEquipmentTerminalDrainRequest(
        string parentOperationId,
        string stepOperationId,
        CombatEquipmentTerminalFrozenSubject source,
        string inputDestinationDrainStepOperationId,
        string inputDestinationDrainRequestFingerprint,
        string requestFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        Source = source == null
            ? null
            : CombatEquipmentTerminalFrozenSubject.FromSave(
                source.CaptureFrozen());
        InputDestinationDrainStepOperationId =
            inputDestinationDrainStepOperationId ?? string.Empty;
        InputDestinationDrainRequestFingerprint =
            inputDestinationDrainRequestFingerprint ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public CombatEquipmentTerminalFrozenSubject Source { get; }
    public string InputDestinationDrainStepOperationId { get; }
    public string InputDestinationDrainRequestFingerprint { get; }
    public ProductionInputDestinationCustodyDrainRequest ChildRequest { get; }
    public string RequestFingerprint { get; }

    private static ProductionInputDestinationCustodyDrainRequest CloneChild(
        ProductionInputDestinationCustodyDrainRequest child) => child == null
        ? null
        : new ProductionInputDestinationCustodyDrainRequest(
            child.ParentOperationId,
            child.StepOperationId,
            child.OwnerStableId,
            child.BillId,
            child.FacilityId,
            child.SourceDestinationId,
            child.OwnerGridX,
            child.OwnerGridY,
            child.SourceClaimFingerprint,
            child.SourceOwnershipFingerprint,
            child.SourceStacks,
            child.SourceOperations,
            child.SourceActors,
            child.InputQuantity,
            child.InputMassGrams,
            child.RequestFingerprint);
}

public readonly struct CombatEquipmentTerminalDrainResult
{
    public CombatEquipmentTerminalDrainResult(
        CombatEquipmentTerminalDrainStatus status,
        CombatEquipmentTerminalDrainPhase phase,
        string commitId,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        Phase = phase;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public CombatEquipmentTerminalDrainStatus Status { get; }
    public CombatEquipmentTerminalDrainPhase Phase { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public readonly struct CombatEquipmentTerminalEffectResult
{
    public CombatEquipmentTerminalEffectResult(
        CombatEquipmentTerminalEffectStatus status,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public CombatEquipmentTerminalEffectStatus Status { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public sealed class CombatEquipmentTerminalInputDispositionEvidence
{
    public CombatEquipmentTerminalInputDispositionEvidence(
        string stepOperationId,
        string requestFingerprint,
        string commitId,
        string receiptFingerprint,
        int releasedQuantity,
        long releasedMassGrams)
    {
        StepOperationId = stepOperationId ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        ReleasedQuantity = releasedQuantity;
        ReleasedMassGrams = releasedMassGrams;
    }

    public string StepOperationId { get; }
    public string RequestFingerprint { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public int ReleasedQuantity { get; }
    public long ReleasedMassGrams { get; }

    public bool IsValidFor(CombatEquipmentTerminalFrozenSubject source)
    {
        if (source == null
            || ReleasedQuantity != source.PendingInputQuantity
            || ReleasedMassGrams != source.PendingInputMassGrams)
        {
            return false;
        }
        bool hasInput = ReleasedQuantity > 0;
        return hasInput
            ? CombatEquipmentTerminalDrainCanonical.IsToken(StepOperationId)
                && CombatEquipmentTerminalDrainCanonical.IsDigest(
                    RequestFingerprint)
                && CombatEquipmentTerminalDrainCanonical.IsToken(CommitId)
                && CombatEquipmentTerminalDrainCanonical.IsDigest(
                    ReceiptFingerprint)
            : string.IsNullOrEmpty(StepOperationId)
                && string.IsNullOrEmpty(RequestFingerprint)
                && string.IsNullOrEmpty(CommitId)
                && string.IsNullOrEmpty(ReceiptFingerprint);
    }
}

public interface ICombatEquipmentTerminalSourceAuthority
{
    bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason);

    bool TryCaptureLiveSource(
        string ownerStableId,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason);

    bool TryCaptureWipLossReceipt(
        string commitId,
        out CombatEquipmentTerminalWipLossReceiptSaveData receipt);

    bool TryCaptureSourceRemovalReceipt(
        string commitId,
        out CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt);

    [GameplayInternalOnly(
        "Publishes one deterministic combat WIP/loss terminal receipt before exact source removal.",
        "Combat equipment terminal drain outbox only")]
    CombatEquipmentTerminalEffectResult TryPublishWipLossReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence);

    [GameplayInternalOnly(
        "Removes one exact frozen craft/repair source after any required WIP receipt exists.",
        "Combat equipment terminal drain outbox only")]
    CombatEquipmentTerminalEffectResult TryRemoveExactSource(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence);

    [GameplayInternalOnly(
        "Garbage-collects the exact combat owner receipt pair after the durable checkpoint commits.",
        "Combat equipment terminal drain outbox only")]
    CombatEquipmentTerminalEffectResult TryGarbageCollectReceipts(
        CombatEquipmentTerminalFrozenSubject source,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint);
}

internal sealed class CombatEquipmentTerminalReceiptGcRow
{
    internal CombatEquipmentTerminalReceiptGcRow(
        CombatEquipmentTerminalFrozenSubject source,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        WipReceiptFingerprint = wipReceiptFingerprint ?? string.Empty;
        RemovalReceiptFingerprint = removalReceiptFingerprint ?? string.Empty;
    }

    internal CombatEquipmentTerminalFrozenSubject Source { get; }
    internal string WipReceiptFingerprint { get; }
    internal string RemovalReceiptFingerprint { get; }
}

internal interface ICombatEquipmentTerminalSourceCheckpointGcCandidate
{
}

internal interface ICombatEquipmentTerminalSourceCheckpointGcAuthority
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<CombatEquipmentTerminalReceiptGcRow> rows,
        out ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate,
        out string failureReason);

    CombatEquipmentTerminalEffectResult PublishCheckpointGarbageCollection(
        ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate);
    void RollbackCheckpointGarbageCollection(
        ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate);
    void CompleteCheckpointGarbageCollection(
        ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate);
}

public interface ICombatEquipmentTerminalDrainQuery
{
    bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason);

    bool TryCaptureLiveSource(
        string ownerStableId,
        out CombatEquipmentTerminalFrozenSubject source,
        out string sourceFingerprint,
        out string failureReason);

    bool TryCapture(
        string stepOperationId,
        out CombatEquipmentTerminalDrainSaveData record);

    IReadOnlyList<CombatEquipmentTerminalDrainSaveData> CaptureCurrentFormat();
}

public interface ICombatEquipmentTerminalDrainCommand
{
    CombatEquipmentTerminalDrainResult TryPrepare(
        CombatEquipmentTerminalDrainRequest request);
    CombatEquipmentTerminalDrainResult TryProgress(string stepOperationId);
    CombatEquipmentTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);
    CombatEquipmentTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);
    CombatEquipmentTerminalDrainResult TryRecover(string stepOperationId);
    bool TryRestoreCurrentFormat(
        IEnumerable<CombatEquipmentTerminalDrainSaveData> records,
        IEnumerable<ProductionInputDestinationCustodyDrainSaveData> childRecords,
        out string failureReason);
}

internal interface ICombatEquipmentTerminalDrainCheckpointGcCandidate
{
}

internal interface ICombatEquipmentTerminalDrainCheckpointGcAuthority
{
    ICombatEquipmentTerminalSourceCheckpointGcAuthority
        SourceCheckpointGcAuthority { get; }

    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<CombatEquipmentTerminalDrainSaveData> rows,
        out ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate,
        out string failureReason);

    CombatEquipmentTerminalDrainResult PublishCheckpointGarbageCollection(
        ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate);
    void RollbackCheckpointGarbageCollection(
        ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate);
    void CompleteCheckpointGarbageCollection(
        ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate);
}

[Serializable]
public sealed class CombatEquipmentTerminalFrozenSourceSaveData
{
    public CombatEquipmentTerminalSourceKind sourceKind;
    public string ownerStableId = string.Empty;
    public string sourceId = string.Empty;
    public string facilityId = string.Empty;
    public string inputDestinationId = string.Empty;
    public string sourcePayload = string.Empty;
    public string sourceFingerprint = string.Empty;
    public int pendingInputQuantity;
    public long pendingInputMassGrams;
    public int wipInputQuantity;
    public long wipInputMassGrams;
    public long committedOutputMassGrams;
    public long declaredLossMassGrams;

    public CombatEquipmentTerminalFrozenSourceSaveData Clone() => new()
    {
        sourceKind = sourceKind,
        ownerStableId = ownerStableId,
        sourceId = sourceId,
        facilityId = facilityId,
        inputDestinationId = inputDestinationId,
        sourcePayload = sourcePayload,
        sourceFingerprint = sourceFingerprint,
        pendingInputQuantity = pendingInputQuantity,
        pendingInputMassGrams = pendingInputMassGrams,
        wipInputQuantity = wipInputQuantity,
        wipInputMassGrams = wipInputMassGrams,
        committedOutputMassGrams = committedOutputMassGrams,
        declaredLossMassGrams = declaredLossMassGrams
    };
}

[Serializable]
public sealed class CombatEquipmentTerminalWipLossReceiptSaveData
{
    public string commitId = string.Empty;
    public CombatEquipmentTerminalSourceKind sourceKind;
    public string ownerStableId = string.Empty;
    public string sourceId = string.Empty;
    public string facilityId = string.Empty;
    public string sourceFingerprint = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public long committedOutputMassGrams;
    public long declaredLossMassGrams;
    public ProductionWipTerminalReason reason =
        ProductionWipTerminalReason.FacilityDestroyed;
    public ProductionWipTerminalLossKind lossKind =
        ProductionWipTerminalLossKind.ExplicitIrrecoverableProcessLoss;
    public string receiptFingerprint = string.Empty;

    public CombatEquipmentTerminalWipLossReceiptSaveData Clone() => new()
    {
        commitId = commitId,
        sourceKind = sourceKind,
        ownerStableId = ownerStableId,
        sourceId = sourceId,
        facilityId = facilityId,
        sourceFingerprint = sourceFingerprint,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        committedOutputMassGrams = committedOutputMassGrams,
        declaredLossMassGrams = declaredLossMassGrams,
        reason = reason,
        lossKind = lossKind,
        receiptFingerprint = receiptFingerprint
    };
}

[Serializable]
public sealed class CombatEquipmentTerminalSourceRemovalReceiptSaveData
{
    public string commitId = string.Empty;
    public CombatEquipmentTerminalSourceKind sourceKind;
    public string ownerStableId = string.Empty;
    public string sourceId = string.Empty;
    public string facilityId = string.Empty;
    public string sourceFingerprint = string.Empty;
    public string receiptFingerprint = string.Empty;

    public CombatEquipmentTerminalSourceRemovalReceiptSaveData Clone() => new()
    {
        commitId = commitId,
        sourceKind = sourceKind,
        ownerStableId = ownerStableId,
        sourceId = sourceId,
        facilityId = facilityId,
        sourceFingerprint = sourceFingerprint,
        receiptFingerprint = receiptFingerprint
    };
}

[Serializable]
public sealed class CombatEquipmentTerminalDrainSaveData
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public string parentOperationId = string.Empty;
    public string stepOperationId = string.Empty;
    public CombatEquipmentTerminalFrozenSourceSaveData source = new();
    public string inputDestinationDrainStepOperationId = string.Empty;
    public string inputDestinationDrainRequestFingerprint = string.Empty;
    public string requestFingerprint = string.Empty;
    public CombatEquipmentTerminalDrainPhase phase;
    public string inputDestinationDrainCommitId = string.Empty;
    public string inputDestinationDrainReceiptFingerprint = string.Empty;
    public int releasedInputQuantity;
    public long releasedInputMassGrams;
    public string wipLossCommitId = string.Empty;
    public string wipLossReceiptFingerprint = string.Empty;
    public string sourceRemovalCommitId = string.Empty;
    public string sourceRemovalReceiptFingerprint = string.Empty;
    public string terminalEffectFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public CombatEquipmentTerminalDrainSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        parentOperationId = parentOperationId,
        stepOperationId = stepOperationId,
        source = source?.Clone(),
        inputDestinationDrainStepOperationId =
            inputDestinationDrainStepOperationId,
        inputDestinationDrainRequestFingerprint =
            inputDestinationDrainRequestFingerprint,
        requestFingerprint = requestFingerprint,
        phase = phase,
        inputDestinationDrainCommitId = inputDestinationDrainCommitId,
        inputDestinationDrainReceiptFingerprint =
            inputDestinationDrainReceiptFingerprint,
        releasedInputQuantity = releasedInputQuantity,
        releasedInputMassGrams = releasedInputMassGrams,
        wipLossCommitId = wipLossCommitId,
        wipLossReceiptFingerprint = wipLossReceiptFingerprint,
        sourceRemovalCommitId = sourceRemovalCommitId,
        sourceRemovalReceiptFingerprint = sourceRemovalReceiptFingerprint,
        terminalEffectFingerprint = terminalEffectFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

public static class CombatEquipmentTerminalDrainCanonical
{
    public const string ParticipantId =
        ProductionFacilityDestructiveDrainParticipantIds
            .CombatEquipmentCrafting;
    public const string CommitPrefix =
        "combat-equipment-terminal-drain-commit:";
    public const string WipCommitPrefix =
        "combat-equipment-terminal-wip-loss:";
    public const string RemovalCommitPrefix =
        "combat-equipment-terminal-source-removal:";

    public static string OwnerStableId(
        CombatEquipmentTerminalSourceKind kind,
        string sourceId) => kind switch
    {
        CombatEquipmentTerminalSourceKind.CraftOrder =>
            ProductionFacilityDestructiveDrainOwnerStableIds
                .CombatCraftOrder(sourceId),
        CombatEquipmentTerminalSourceKind.RepairOrder =>
            ProductionFacilityDestructiveDrainOwnerStableIds
                .EquipmentRepairOrder(sourceId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static string CreateSourceFingerprint(
        CombatEquipmentTerminalFrozenSourceSaveData source)
    {
        StringBuilder canonical = new StringBuilder(1024)
            .Append("combat-equipment-terminal-source@2|");
        Append(canonical, ((int)(source?.sourceKind ?? default)).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, source?.ownerStableId);
        Append(canonical, source?.sourceId);
        Append(canonical, source?.facilityId);
        Append(canonical, source?.inputDestinationId);
        Append(canonical, source?.sourcePayload);
        // Pending destination custody is intentionally excluded. The exact
        // quantity, grams and physical closure are bound by the child request
        // fingerprint and its restore join. The child legitimately drains those
        // values before source removal, so including them here would make the
        // stable owner identity drift after a successful child commit.
        Append(canonical, (source?.wipInputQuantity ?? 0).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, (source?.wipInputMassGrams ?? 0L).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, (source?.committedOutputMassGrams ?? 0L).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, (source?.declaredLossMassGrams ?? 0L).ToString(
            CultureInfo.InvariantCulture));
        return Hash(canonical.ToString());
    }

    public static string CreateRequestFingerprint(
        string parentOperationId,
        string stepOperationId,
        CombatEquipmentTerminalFrozenSubject source,
        string childStepOperationId,
        string childRequestFingerprint)
    {
        StringBuilder canonical = new StringBuilder(384)
            .Append("combat-equipment-terminal-request@1|");
        Append(canonical, parentOperationId);
        Append(canonical, stepOperationId);
        Append(canonical, source?.SourceFingerprint);
        Append(canonical, childStepOperationId);
        Append(canonical, childRequestFingerprint);
        return Hash(canonical.ToString());
    }

    public static CombatEquipmentTerminalWipLossReceiptSaveData
        CreateWipLossReceipt(CombatEquipmentTerminalFrozenSubject source)
    {
        if (source == null || source.WipInputMassGrams <= 0L)
            return null;
        CombatEquipmentTerminalWipLossReceiptSaveData receipt = new()
        {
            commitId = WipCommitPrefix + Hash(source.SourceFingerprint),
            sourceKind = source.SourceKind,
            ownerStableId = source.OwnerStableId,
            sourceId = source.SourceId,
            facilityId = source.FacilityId,
            sourceFingerprint = source.SourceFingerprint,
            inputQuantity = source.WipInputQuantity,
            inputMassGrams = source.WipInputMassGrams,
            committedOutputMassGrams = source.CommittedOutputMassGrams,
            declaredLossMassGrams = source.DeclaredLossMassGrams,
            reason = ProductionWipTerminalReason.FacilityDestroyed,
            lossKind = ProductionWipTerminalLossKind
                .ExplicitIrrecoverableProcessLoss
        };
        receipt.receiptFingerprint = CreateWipLossReceiptFingerprint(receipt);
        return receipt;
    }

    public static CombatEquipmentTerminalSourceRemovalReceiptSaveData
        CreateSourceRemovalReceipt(CombatEquipmentTerminalFrozenSubject source)
    {
        if (source == null)
            return null;
        CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt = new()
        {
            commitId = RemovalCommitPrefix + Hash(source.SourceFingerprint),
            sourceKind = source.SourceKind,
            ownerStableId = source.OwnerStableId,
            sourceId = source.SourceId,
            facilityId = source.FacilityId,
            sourceFingerprint = source.SourceFingerprint
        };
        receipt.receiptFingerprint = CreateRemovalReceiptFingerprint(receipt);
        return receipt;
    }

    public static string CreateTerminalEffectFingerprint(
        string requestFingerprint,
        string childReceiptFingerprint,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint)
    {
        StringBuilder canonical = new StringBuilder(384)
            .Append("combat-equipment-terminal-effect@1|");
        Append(canonical, requestFingerprint);
        Append(canonical, childReceiptFingerprint);
        Append(canonical, wipReceiptFingerprint);
        Append(canonical, removalReceiptFingerprint);
        return Hash(canonical.ToString());
    }

    public static string CreateCommitId(
        string stepOperationId,
        string requestFingerprint) => CommitPrefix + Hash(
        (stepOperationId ?? string.Empty) + "\n"
        + (requestFingerprint ?? string.Empty));

    public static string CreateReceiptFingerprint(
        string requestFingerprint,
        string terminalEffectFingerprint,
        string commitId)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("combat-equipment-terminal-receipt@1|");
        Append(canonical, requestFingerprint);
        Append(canonical, terminalEffectFingerprint);
        Append(canonical, commitId);
        return Hash(canonical.ToString());
    }

    public static bool IsValidFrozenSource(
        CombatEquipmentTerminalFrozenSourceSaveData source)
    {
        if (source == null
            || !Enum.IsDefined(typeof(CombatEquipmentTerminalSourceKind),
                source.sourceKind)
            || !Token(source.ownerStableId)
            || !Token(source.sourceId)
            || !Token(source.facilityId)
            || !OptionalToken(source.inputDestinationId)
            || string.IsNullOrEmpty(source.sourcePayload)
            || !Digest(source.sourceFingerprint)
            || source.pendingInputQuantity < 0
            || source.pendingInputMassGrams < 0L
            || source.wipInputQuantity < 0
            || source.wipInputMassGrams < 0L
            || source.committedOutputMassGrams < 0L
            || source.declaredLossMassGrams < 0L
            || (source.pendingInputQuantity == 0)
                != (source.pendingInputMassGrams == 0L)
            || (source.wipInputQuantity == 0)
                != (source.wipInputMassGrams == 0L)
            || !TryAdd(
                source.committedOutputMassGrams,
                source.declaredLossMassGrams,
                out long accountedWip)
            || accountedWip != source.wipInputMassGrams
            || !TryOwnerStableId(
                source.sourceKind,
                source.sourceId,
                out string expectedOwnerStableId)
            || !string.Equals(
                source.ownerStableId,
                expectedOwnerStableId,
                StringComparison.Ordinal)
            || !string.Equals(
                source.sourceFingerprint,
                CreateSourceFingerprint(source),
                StringComparison.Ordinal))
        {
            return false;
        }

        return source.sourceKind switch
        {
            CombatEquipmentTerminalSourceKind.CraftOrder =>
                TryValidateCraftPayload(source),
            CombatEquipmentTerminalSourceKind.RepairOrder =>
                TryValidateRepairPayload(source),
            _ => false
        };
    }

    public static bool IsValidWipLossReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData value)
    {
        if (value == null
            || !Token(value.commitId)
            || !Enum.IsDefined(typeof(CombatEquipmentTerminalSourceKind),
                value.sourceKind)
            || !Token(value.ownerStableId)
            || !Token(value.sourceId)
            || !Token(value.facilityId)
            || !Digest(value.sourceFingerprint)
            || value.inputQuantity <= 0
            || value.inputMassGrams <= 0L
            || value.committedOutputMassGrams < 0L
            || value.declaredLossMassGrams < 0L
            || !TryAdd(
                value.committedOutputMassGrams,
                value.declaredLossMassGrams,
                out long accounted)
            || accounted != value.inputMassGrams
            || value.reason != ProductionWipTerminalReason.FacilityDestroyed
            || value.lossKind != ProductionWipTerminalLossKind
                .ExplicitIrrecoverableProcessLoss
            || !string.Equals(
                value.commitId,
                WipCommitPrefix + Hash(value.sourceFingerprint),
                StringComparison.Ordinal))
        {
            return false;
        }
        return string.Equals(
            value.receiptFingerprint,
            CreateWipLossReceiptFingerprint(value),
            StringComparison.Ordinal);
    }

    public static bool IsValidSourceRemovalReceipt(
        CombatEquipmentTerminalSourceRemovalReceiptSaveData value)
    {
        if (value == null
            || !Token(value.commitId)
            || !Enum.IsDefined(typeof(CombatEquipmentTerminalSourceKind),
                value.sourceKind)
            || !Token(value.ownerStableId)
            || !Token(value.sourceId)
            || !Token(value.facilityId)
            || !Digest(value.sourceFingerprint)
            || !string.Equals(
                value.commitId,
                RemovalCommitPrefix + Hash(value.sourceFingerprint),
                StringComparison.Ordinal))
        {
            return false;
        }
        return string.Equals(
            value.receiptFingerprint,
            CreateRemovalReceiptFingerprint(value),
            StringComparison.Ordinal);
    }

    public static bool IsValidSave(CombatEquipmentTerminalDrainSaveData value)
    {
        if (value == null
            || value.schemaVersion !=
                CombatEquipmentTerminalDrainSaveData.CurrentSchemaVersion
            || !Token(value.parentOperationId)
            || !Token(value.stepOperationId)
            || !IsValidFrozenSource(value.source)
            || !Enum.IsDefined(typeof(CombatEquipmentTerminalDrainPhase),
                value.phase)
            || !Digest(value.requestFingerprint)
            || !OptionalPair(
                value.inputDestinationDrainStepOperationId,
                value.inputDestinationDrainRequestFingerprint)
            || !string.Equals(
                value.requestFingerprint,
                CreateRequestFingerprint(
                    value.parentOperationId,
                    value.stepOperationId,
                    CombatEquipmentTerminalFrozenSubject.FromSave(value.source),
                    value.inputDestinationDrainStepOperationId,
                    value.inputDestinationDrainRequestFingerprint),
                StringComparison.Ordinal)
            || value.releasedInputQuantity < 0
            || value.releasedInputMassGrams < 0L)
        {
            return false;
        }

        bool hasChild = !string.IsNullOrEmpty(
            value.inputDestinationDrainStepOperationId);
        if (hasChild != (value.source.pendingInputQuantity > 0)
            || hasChild != (value.source.pendingInputMassGrams > 0L))
        {
            return false;
        }

        bool childRecorded = value.phase >=
            CombatEquipmentTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        bool childAcknowledged = value.phase >=
            CombatEquipmentTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingTerminalEffects;
        bool terminal = value.phase >=
            CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement;

        if (!hasChild)
        {
            if (value.phase == CombatEquipmentTerminalDrainPhase
                    .InputDestinationReceiptRecordedAwaitingAcknowledgement
                || !Empty(value.inputDestinationDrainCommitId)
                || !Empty(value.inputDestinationDrainReceiptFingerprint)
                || value.releasedInputQuantity != 0
                || value.releasedInputMassGrams != 0L)
            {
                return false;
            }
        }
        else if (childRecorded)
        {
            if (!Token(value.inputDestinationDrainCommitId)
                || !Digest(value.inputDestinationDrainReceiptFingerprint)
                || value.releasedInputQuantity !=
                    value.source.pendingInputQuantity
                || value.releasedInputMassGrams !=
                    value.source.pendingInputMassGrams)
            {
                return false;
            }
        }
        else if (!Empty(value.inputDestinationDrainCommitId)
            || !Empty(value.inputDestinationDrainReceiptFingerprint)
            || value.releasedInputQuantity != 0
            || value.releasedInputMassGrams != 0L)
        {
            return false;
        }

        if (!terminal)
        {
            return Empty(value.wipLossCommitId)
                && Empty(value.wipLossReceiptFingerprint)
                && Empty(value.sourceRemovalCommitId)
                && Empty(value.sourceRemovalReceiptFingerprint)
                && Empty(value.terminalEffectFingerprint)
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint)
                && (hasChild || childAcknowledged
                    || value.phase == CombatEquipmentTerminalDrainPhase
                        .PreparedAwaitingInputDestinationReceipt);
        }

        CombatEquipmentTerminalFrozenSubject source =
            CombatEquipmentTerminalFrozenSubject.FromSave(value.source);
        CombatEquipmentTerminalWipLossReceiptSaveData wip =
            CreateWipLossReceipt(source);
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CreateSourceRemovalReceipt(source);
        string expectedWipCommit = wip?.commitId ?? string.Empty;
        string expectedWipReceipt = wip?.receiptFingerprint ?? string.Empty;
        string expectedEffect = CreateTerminalEffectFingerprint(
            value.requestFingerprint,
            value.inputDestinationDrainReceiptFingerprint,
            expectedWipReceipt,
            removal.receiptFingerprint);
        string expectedCommit = CreateCommitId(
            value.stepOperationId,
            value.requestFingerprint);
        return string.Equals(
                value.wipLossCommitId,
                expectedWipCommit,
                StringComparison.Ordinal)
            && string.Equals(
                value.wipLossReceiptFingerprint,
                expectedWipReceipt,
                StringComparison.Ordinal)
            && string.Equals(
                value.sourceRemovalCommitId,
                removal.commitId,
                StringComparison.Ordinal)
            && string.Equals(
                value.sourceRemovalReceiptFingerprint,
                removal.receiptFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                value.terminalEffectFingerprint,
                expectedEffect,
                StringComparison.Ordinal)
            && string.Equals(value.commitId, expectedCommit,
                StringComparison.Ordinal)
            && string.Equals(
                value.receiptFingerprint,
                CreateReceiptFingerprint(
                    value.requestFingerprint,
                    expectedEffect,
                    expectedCommit),
                StringComparison.Ordinal);
    }

    public static bool IsDigest(string value) => Digest(value);
    public static bool IsToken(string value) => Token(value);

    public static bool WipReceiptEquals(
        CombatEquipmentTerminalWipLossReceiptSaveData left,
        CombatEquipmentTerminalWipLossReceiptSaveData right) =>
        left != null && right != null
        && string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    public static bool RemovalReceiptEquals(
        CombatEquipmentTerminalSourceRemovalReceiptSaveData left,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData right) =>
        left != null && right != null
        && string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static bool TryValidateCraftPayload(
        CombatEquipmentTerminalFrozenSourceSaveData source)
    {
        try
        {
            CombatEquipmentCraftOrderSaveData value =
                JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                    source.sourcePayload);
            return value != null
                && Token(value.orderId)
                && Token(value.facilityPersistentId)
                && OptionalToken(value.materialDestinationId)
                && string.Equals(value.orderId, source.sourceId,
                    StringComparison.Ordinal)
                && string.Equals(value.facilityPersistentId, source.facilityId,
                    StringComparison.Ordinal)
                && string.Equals(value.materialDestinationId,
                    source.inputDestinationId, StringComparison.Ordinal)
                && string.Equals(
                    JsonUtility.ToJson(value),
                    source.sourcePayload,
                    StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryValidateRepairPayload(
        CombatEquipmentTerminalFrozenSourceSaveData source)
    {
        try
        {
            CombatEquipmentRepairOrder value =
                JsonUtility.FromJson<CombatEquipmentRepairOrder>(
                    source.sourcePayload);
            return value != null
                && Token(value.orderId)
                && Token(value.facilityBuildingId)
                && value.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled
                && string.Equals(value.orderId, source.sourceId,
                    StringComparison.Ordinal)
                && string.Equals(value.facilityBuildingId, source.facilityId,
                    StringComparison.Ordinal)
                && string.Equals(value.FacilityDestinationId,
                    source.inputDestinationId, StringComparison.Ordinal)
                && string.Equals(
                    JsonUtility.ToJson(value),
                    source.sourcePayload,
                    StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string CreateWipLossReceiptFingerprint(
        CombatEquipmentTerminalWipLossReceiptSaveData value)
    {
        string fingerprint = value.receiptFingerprint;
        value.receiptFingerprint = string.Empty;
        string result = Hash(
            "combat-equipment-terminal-wip-receipt@1|"
            + JsonUtility.ToJson(value));
        value.receiptFingerprint = fingerprint;
        return result;
    }

    private static string CreateRemovalReceiptFingerprint(
        CombatEquipmentTerminalSourceRemovalReceiptSaveData value)
    {
        string fingerprint = value.receiptFingerprint;
        value.receiptFingerprint = string.Empty;
        string result = Hash(
            "combat-equipment-terminal-removal-receipt@1|"
            + JsonUtility.ToJson(value));
        value.receiptFingerprint = fingerprint;
        return result;
    }

    private static bool OptionalPair(string token, string digest) =>
        (Empty(token) && Empty(digest)) || (Token(token) && Digest(digest));

    internal static bool TryOwnerStableId(
        CombatEquipmentTerminalSourceKind kind,
        string sourceId,
        out string ownerStableId)
    {
        ownerStableId = string.Empty;
        try
        {
            ownerStableId = OwnerStableId(kind, sourceId);
            return Token(ownerStableId);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or ArgumentOutOfRangeException)
        {
            ownerStableId = string.Empty;
            return false;
        }
    }

    private static bool TryAdd(long left, long right, out long result)
    {
        try
        {
            result = checked(left + right);
            return true;
        }
        catch (OverflowException)
        {
            result = 0L;
            return false;
        }
    }

    private static void Append(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }

    private static bool Empty(string value) => string.IsNullOrEmpty(value);
    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    private static bool OptionalToken(string value) => Empty(value) || Token(value);
    private static bool Digest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte current in digest)
            result.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
