using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum ProductionApparelOrderTerminalDrainPhase
{
    PreparedAwaitingLeaseAuthorityRelease = 0,
    LeaseAuthorityReleasedAwaitingTerminalEffect = 1,
    TerminalEffectCommittedAwaitingSourceOrderTerminal = 2,
    SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement = 3,
    OwnerAcknowledgedAwaitingCheckpointGc = 4
}

public enum ProductionApparelOrderTerminalDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public enum ProductionApparelOrderPendingEffectKind
{
    None = 0,
    RepairDisposition = 1,
    RejectedOutputDismantle = 2
}

[Serializable]
public sealed class ProductionApparelOrderPendingEffectIdentity
{
    public ProductionApparelOrderPendingEffectKind kind;
    public string operationId = string.Empty;
    public string priorCommitId = string.Empty;
    public string reasonCode = string.Empty;
    public int phase;
    public int quantity;
    public long massGrams;
    public int completedQuantity;
    public bool sourceAlreadyConsumed;
    public List<string> sourceStackIds = new();
    public string targetStackId = string.Empty;
    public string originalStateFingerprint = string.Empty;
    public string resolvedStateFingerprint = string.Empty;
    public string identityFingerprint = string.Empty;

    public ProductionApparelOrderPendingEffectIdentity Clone() => new()
    {
        kind = kind,
        operationId = operationId,
        priorCommitId = priorCommitId,
        reasonCode = reasonCode,
        phase = phase,
        quantity = quantity,
        massGrams = massGrams,
        completedQuantity = completedQuantity,
        sourceAlreadyConsumed = sourceAlreadyConsumed,
        sourceStackIds = sourceStackIds?.ToList() ?? new List<string>(),
        targetStackId = targetStackId,
        originalStateFingerprint = originalStateFingerprint,
        resolvedStateFingerprint = resolvedStateFingerprint,
        identityFingerprint = identityFingerprint
    };
}

[Serializable]
public sealed class ProductionApparelOrderTerminalEffectReceipt
{
    public string stepOperationId = string.Empty;
    public string orderId = string.Empty;
    public string sourceOrderFingerprint = string.Empty;
    public string pendingEffectIdentityFingerprint = string.Empty;
    public int abandonedRequiredWorkBits;
    public int abandonedCompletedWorkBits;
    public int historicalConsumedWorkBits;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionApparelOrderTerminalEffectReceipt Clone() => new()
    {
        stepOperationId = stepOperationId,
        orderId = orderId,
        sourceOrderFingerprint = sourceOrderFingerprint,
        pendingEffectIdentityFingerprint = pendingEffectIdentityFingerprint,
        abandonedRequiredWorkBits = abandonedRequiredWorkBits,
        abandonedCompletedWorkBits = abandonedCompletedWorkBits,
        historicalConsumedWorkBits = historicalConsumedWorkBits,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

[Serializable]
public sealed class ProductionApparelOrderSourceTerminalReceipt
{
    public string stepOperationId = string.Empty;
    public string orderId = string.Empty;
    public string sourceOrderFingerprint = string.Empty;
    public string terminalEffectReceiptFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionApparelOrderSourceTerminalReceipt Clone() => new()
    {
        stepOperationId = stepOperationId,
        orderId = orderId,
        sourceOrderFingerprint = sourceOrderFingerprint,
        terminalEffectReceiptFingerprint = terminalEffectReceiptFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

public readonly struct ProductionApparelOrderTerminalEffectApplyResult
{
    public ProductionApparelOrderTerminalEffectApplyResult(
        ProductionApparelOrderTerminalDrainStatus status,
        ProductionApparelOrderTerminalEffectReceipt receipt,
        string failureReason)
    {
        Status = status;
        Receipt = receipt?.Clone();
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionApparelOrderTerminalDrainStatus Status { get; }
    public ProductionApparelOrderTerminalEffectReceipt Receipt { get; }
    public string FailureReason { get; }
}

public readonly struct ProductionApparelOrderSourceTerminalApplyResult
{
    public ProductionApparelOrderSourceTerminalApplyResult(
        ProductionApparelOrderTerminalDrainStatus status,
        ProductionApparelOrderSourceTerminalReceipt receipt,
        string failureReason)
    {
        Status = status;
        Receipt = receipt?.Clone();
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionApparelOrderTerminalDrainStatus Status { get; }
    public ProductionApparelOrderSourceTerminalReceipt Receipt { get; }
    public string FailureReason { get; }
}

public interface IProductionApparelOrderTerminalEffectPort
{
    IReadOnlyList<ProductionApparelOrderTerminalEffectReceipt>
        CaptureTerminalEffectReceipts();

    bool TryCaptureTerminalEffectReceipt(
        string commitId,
        out ProductionApparelOrderTerminalEffectReceipt receipt);

    [GameplayInternalOnly(
        "Commits only the exact frozen apparel work-loss and pending-effect receipt owned by one durable apparel terminal producer.",
        "Apparel destructive terminal drain producer only")]
    ProductionApparelOrderTerminalEffectApplyResult TryCommitTerminalEffect(
        ProductionApparelOrderTerminalEffectReceipt expectedReceipt,
        ProductionApparelOrderPendingEffectIdentity pendingEffect);
}

public interface IProductionApparelOrderSourceTerminalPort
{
    bool TryCaptureLiveOrder(
        string orderId,
        out ApparelWorkOrderSaveData sourceOrder,
        out string failureReason);

    IReadOnlyList<ProductionApparelOrderSourceTerminalReceipt>
        CaptureSourceTerminalReceipts();

    bool TryCaptureSourceTerminalReceipt(
        string commitId,
        out ProductionApparelOrderSourceTerminalReceipt receipt);

    [GameplayInternalOnly(
        "Removes only one exact frozen apparel order after its durable terminal-effect receipt exists.",
        "Apparel destructive terminal drain producer only")]
    ProductionApparelOrderSourceTerminalApplyResult TryCommitSourceTerminal(
        ProductionApparelOrderSourceTerminalReceipt expectedReceipt);
}

public interface IProductionApparelTerminalStateCheckpointGcCandidate
{
}

/// <summary>
/// Row-scoped checkpoint collector for the paired terminal-effect and
/// source-terminal receipts stored by the apparel work-order authority.
/// </summary>
public interface IProductionApparelTerminalStateCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        out IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate);
}

/// <summary>
/// Checkpoint-GC facade owned by the apparel terminal producer. It removes
/// lower terminal-state receipts before producer tombstones and can restore
/// the exact rows if a later upper participant fails.
/// </summary>
public interface IProductionApparelOrderTerminalDrainCheckpointGcPort
{
    ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate);

    ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);

    void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate);
}

public sealed class ProductionApparelOrderTerminalDrainRequest
{
    public ProductionApparelOrderTerminalDrainRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        ApparelWorkOrderSaveData sourceOrder,
        bool hasLeaseAuthority,
        string leaseAuthorityFingerprint,
        ProductionApparelOrderPendingEffectIdentity pendingEffect,
        string requestFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        SourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
            sourceOrder);
        HasLeaseAuthority = hasLeaseAuthority;
        LeaseAuthorityFingerprint = leaseAuthorityFingerprint ?? string.Empty;
        PendingEffect = pendingEffect?.Clone();
        RequestFingerprint = requestFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public ApparelWorkOrderSaveData SourceOrder { get; }
    public bool HasLeaseAuthority { get; }
    public string LeaseAuthorityFingerprint { get; }
    public ProductionApparelOrderPendingEffectIdentity PendingEffect { get; }
    public string RequestFingerprint { get; }
}

public readonly struct ProductionApparelOrderTerminalDrainResult
{
    public ProductionApparelOrderTerminalDrainResult(
        ProductionApparelOrderTerminalDrainStatus status,
        ProductionApparelOrderTerminalDrainPhase phase,
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

    public ProductionApparelOrderTerminalDrainStatus Status { get; }
    public ProductionApparelOrderTerminalDrainPhase Phase { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public interface IProductionApparelOrderTerminalDrainQuery
{
    bool TryCaptureLiveOrder(
        string orderId,
        out ApparelWorkOrderSaveData sourceOrder,
        out string sourceOrderFingerprint,
        out string failureReason);

    bool TryCapture(
        string stepOperationId,
        out ProductionApparelOrderTerminalDrainSaveData record);

    IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData>
        CaptureCurrentFormat();
}

public interface IProductionApparelOrderTerminalDrainCommand
{
    ProductionApparelOrderTerminalDrainResult TryPrepare(
        ProductionApparelOrderTerminalDrainRequest request);

    ProductionApparelOrderTerminalDrainResult TryProgress(
        string stepOperationId);

    ProductionApparelOrderTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);

    ProductionApparelOrderTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);

    ProductionApparelOrderTerminalDrainResult TryRecover(
        string stepOperationId);

    bool TryRestoreCurrentFormat(
        IEnumerable<ProductionApparelOrderTerminalDrainSaveData> records,
        out string failureReason);
}

[Serializable]
public sealed class ProductionApparelOrderTerminalDrainSaveData
{
    public const int CurrentSchemaVersion = 3;

    public int schemaVersion = CurrentSchemaVersion;
    public string parentOperationId = string.Empty;
    public string stepOperationId = string.Empty;
    public string ownerStableId = string.Empty;
    public string orderId = string.Empty;
    public string facilityId = string.Empty;
    public ApparelWorkOrderKind orderKind;
    public ApparelWorkOrderSaveData sourceOrder = new();
    public string sourceOrderFingerprint = string.Empty;
    public bool hasLeaseAuthority;
    public string leaseAuthorityFingerprint = string.Empty;
    public ProductionApparelOrderPendingEffectIdentity pendingEffect;
    public string requestFingerprint = string.Empty;
    public ProductionApparelOrderTerminalDrainPhase phase;

    public string leaseReleaseCommitId = string.Empty;
    public string leaseReleaseReceiptFingerprint = string.Empty;
    public ProductionApparelOrderTerminalEffectReceipt terminalEffectReceipt;
    public ProductionApparelOrderSourceTerminalReceipt sourceTerminalReceipt;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionApparelOrderTerminalDrainSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        parentOperationId = parentOperationId,
        stepOperationId = stepOperationId,
        ownerStableId = ownerStableId,
        orderId = orderId,
        facilityId = facilityId,
        orderKind = orderKind,
        sourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
            sourceOrder),
        sourceOrderFingerprint = sourceOrderFingerprint,
        hasLeaseAuthority = hasLeaseAuthority,
        leaseAuthorityFingerprint = leaseAuthorityFingerprint,
        pendingEffect = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalPendingEffect(pendingEffect),
        requestFingerprint = requestFingerprint,
        phase = phase,
        leaseReleaseCommitId = leaseReleaseCommitId,
        leaseReleaseReceiptFingerprint = leaseReleaseReceiptFingerprint,
        terminalEffectReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalTerminalEffectReceipt(terminalEffectReceipt),
        sourceTerminalReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalSourceTerminalReceipt(sourceTerminalReceipt),
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

public static class ProductionApparelOrderTerminalDrainCanonical
{
    public const string CommitPrefix =
        "production-apparel-order-terminal-drain-commit:";

    public static ApparelWorkOrderSaveData CloneOrder(
        ApparelWorkOrderSaveData source)
    {
        if (source == null)
            return null;
        return JsonUtility.FromJson<ApparelWorkOrderSaveData>(
            JsonUtility.ToJson(source));
    }

    public static ProductionApparelOrderPendingEffectIdentity
        CloneOptionalPendingEffect(
            ProductionApparelOrderPendingEffectIdentity value) =>
        IsEmpty(value) ? null : value.Clone();

    public static ProductionApparelOrderTerminalEffectReceipt
        CloneOptionalTerminalEffectReceipt(
            ProductionApparelOrderTerminalEffectReceipt value) =>
        IsEmpty(value) ? null : value.Clone();

    public static ProductionApparelOrderSourceTerminalReceipt
        CloneOptionalSourceTerminalReceipt(
            ProductionApparelOrderSourceTerminalReceipt value) =>
        IsEmpty(value) ? null : value.Clone();

    private static bool IsEmpty(
        ProductionApparelOrderPendingEffectIdentity value) => value == null
        || value.kind == ProductionApparelOrderPendingEffectKind.None
        && string.IsNullOrEmpty(value.operationId)
        && string.IsNullOrEmpty(value.priorCommitId)
        && string.IsNullOrEmpty(value.reasonCode)
        && value.phase == 0
        && value.quantity == 0
        && value.massGrams == 0L
        && value.completedQuantity == 0
        && !value.sourceAlreadyConsumed
        && (value.sourceStackIds == null || value.sourceStackIds.Count == 0)
        && string.IsNullOrEmpty(value.targetStackId)
        && string.IsNullOrEmpty(value.originalStateFingerprint)
        && string.IsNullOrEmpty(value.resolvedStateFingerprint)
        && string.IsNullOrEmpty(value.identityFingerprint);

    private static bool IsEmpty(
        ProductionApparelOrderTerminalEffectReceipt value) => value == null
        || string.IsNullOrEmpty(value.stepOperationId)
        && string.IsNullOrEmpty(value.orderId)
        && string.IsNullOrEmpty(value.sourceOrderFingerprint)
        && string.IsNullOrEmpty(value.pendingEffectIdentityFingerprint)
        && value.abandonedRequiredWorkBits == 0
        && value.abandonedCompletedWorkBits == 0
        && value.historicalConsumedWorkBits == 0
        && string.IsNullOrEmpty(value.commitId)
        && string.IsNullOrEmpty(value.receiptFingerprint);

    private static bool IsEmpty(
        ProductionApparelOrderSourceTerminalReceipt value) => value == null
        || string.IsNullOrEmpty(value.stepOperationId)
        && string.IsNullOrEmpty(value.orderId)
        && string.IsNullOrEmpty(value.sourceOrderFingerprint)
        && string.IsNullOrEmpty(value.terminalEffectReceiptFingerprint)
        && string.IsNullOrEmpty(value.commitId)
        && string.IsNullOrEmpty(value.receiptFingerprint);

    public static string CreateSourceOrderFingerprint(
        ApparelWorkOrderSaveData sourceOrder) => Hash(
        "production-apparel-order-terminal-source@3|"
        + (sourceOrder == null ? string.Empty : JsonUtility.ToJson(sourceOrder)));

    public static string CreateNoLeaseAuthorityFingerprint(string orderId) =>
        Hash("production-apparel-order-no-lease@1|" + (orderId ?? string.Empty));

    public static bool TryCreatePendingEffectIdentity(
        ApparelWorkOrderSaveData sourceOrder,
        out ProductionApparelOrderPendingEffectIdentity identity,
        out string failureReason)
    {
        identity = null;
        failureReason = string.Empty;
        if (!IsValidSourceOrder(sourceOrder))
        {
            failureReason = "production-apparel-terminal-source-invalid";
            return false;
        }

        bool repairPending = sourceOrder.repairCommitPhase !=
            ApparelRepairCommitPhase.None;
        bool rejectedPending = sourceOrder.dismantlingRejectedOutput;
        if (repairPending && rejectedPending)
        {
            failureReason = "production-apparel-terminal-pending-effect-ambiguous";
            return false;
        }
        if (!repairPending && !rejectedPending)
            return true;

        if (repairPending)
        {
            if (sourceOrder.kind != ApparelWorkOrderKind.Repair
                || sourceOrder.state !=
                    ApparelWorkOrderState.WaitingForDispositionFinalization
                || sourceOrder.repairCommitPhase is not (
                    ApparelRepairCommitPhase.MaterialCommitted or
                    ApparelRepairCommitPhase.RepairApplied)
                || !Token(sourceOrder.repairOperationId)
                || !Token(sourceOrder.repairReasonCode)
                || !Token(sourceOrder.repairCommitId)
                || sourceOrder.repairSourceStackIds == null
                || sourceOrder.repairSourceStackIds.Count == 0
                || !CanonicalUnique(sourceOrder.repairSourceStackIds)
                || sourceOrder.repairInputQuantity <= 0
                || sourceOrder.repairInputMassGrams <= 0L
                || !Token(sourceOrder.repairTargetStackId)
                || Empty(sourceOrder.repairOriginalStatePayload)
                || Empty(sourceOrder.repairResolvedStatePayload))
            {
                failureReason =
                    "production-apparel-terminal-repair-effect-invalid";
                return false;
            }
            identity = new ProductionApparelOrderPendingEffectIdentity
            {
                kind = ProductionApparelOrderPendingEffectKind
                    .RepairDisposition,
                operationId = sourceOrder.repairOperationId,
                priorCommitId = sourceOrder.repairCommitId,
                reasonCode = sourceOrder.repairReasonCode,
                phase = (int)sourceOrder.repairCommitPhase,
                quantity = sourceOrder.repairInputQuantity,
                massGrams = sourceOrder.repairInputMassGrams,
                completedQuantity = 0,
                sourceAlreadyConsumed = true,
                sourceStackIds = sourceOrder.repairSourceStackIds.ToList(),
                targetStackId = sourceOrder.repairTargetStackId,
                originalStateFingerprint = Hash(
                    "production-apparel-repair-original@1|"
                    + sourceOrder.repairOriginalStatePayload),
                resolvedStateFingerprint = Hash(
                    "production-apparel-repair-resolved@1|"
                    + sourceOrder.repairResolvedStatePayload)
            };
        }
        else
        {
            if (sourceOrder.kind != ApparelWorkOrderKind.Craft
                || !Token(sourceOrder.rejectedOutputStackId)
                || !Token(sourceOrder.rejectedOutputInstanceId)
                || sourceOrder.rejectedMaterialAmount < 0
                || sourceOrder.rejectedMaterialSpawned < 0
                || sourceOrder.rejectedMaterialSpawned >
                    sourceOrder.rejectedMaterialAmount
                || !Token(sourceOrder.rejectedRecoveryItemId)
                || !ApparelRejectedDismantleOutbox.ValidateOwnerShape(
                    sourceOrder,
                    out _))
            {
                failureReason =
                    "production-apparel-terminal-rejected-effect-invalid";
                return false;
            }
            identity = new ProductionApparelOrderPendingEffectIdentity
            {
                kind = ProductionApparelOrderPendingEffectKind
                    .RejectedOutputDismantle,
                operationId = ApparelRejectedDismantleOutbox
                    .FormatOperationId(
                        sourceOrder.orderId,
                        sourceOrder.qualityAttemptIndex),
                priorCommitId =
                    sourceOrder.rejectedDismantleCommitId,
                reasonCode = ApparelRejectedDismantleOutbox.ReasonCode,
                phase = sourceOrder.rejectedDismantleAcknowledged
                    ? 3
                    : sourceOrder.rejectedRecoveryPublished
                        ? 2
                        : sourceOrder.rejectedOutputConsumed ? 1 : 0,
                quantity = sourceOrder.rejectedMaterialAmount,
                massGrams = sourceOrder.rejectedDismantleInputMassGrams,
                completedQuantity = sourceOrder.rejectedMaterialSpawned,
                sourceAlreadyConsumed = sourceOrder.rejectedOutputConsumed,
                sourceStackIds = new List<string>
                {
                    sourceOrder.rejectedOutputStackId
                },
                targetStackId = sourceOrder.rejectedRecoveryItemId,
                originalStateFingerprint = Hash(
                    "production-apparel-rejected-source@1|"
                    + sourceOrder.rejectedOutputStackId
                    + "|"
                    + sourceOrder.rejectedOutputInstanceId),
                resolvedStateFingerprint =
                    sourceOrder.rejectedRecoveryPublished
                        ? Hash(
                            "production-apparel-rejected-recovery@1|"
                            + sourceOrder.rejectedRecoveryOperationId
                            + "|"
                            + sourceOrder.rejectedRecoveryCommitId
                            + "|"
                            + sourceOrder.rejectedRecoveryOutputMassGrams
                                .ToString(CultureInfo.InvariantCulture))
                        : string.Empty
            };
        }

        identity.identityFingerprint = CreatePendingEffectFingerprint(identity);
        return IsValidPendingEffect(identity);
    }

    public static string CreatePendingEffectFingerprint(
        ProductionApparelOrderPendingEffectIdentity identity)
    {
        if (identity == null)
            return string.Empty;
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-apparel-pending-effect@1|")
            .Append(((int)identity.kind).ToString(CultureInfo.InvariantCulture))
            .Append('|');
        AppendToken(canonical, identity.operationId);
        AppendToken(canonical, identity.priorCommitId);
        AppendToken(canonical, identity.reasonCode);
        canonical.Append(identity.phase.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(identity.quantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(identity.massGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(identity.completedQuantity.ToString(
                CultureInfo.InvariantCulture))
            .Append('|')
            .Append(identity.sourceAlreadyConsumed ? "1|" : "0|");
        foreach (string stackId in identity.sourceStackIds ?? new List<string>())
            AppendToken(canonical, stackId);
        AppendToken(canonical, identity.targetStackId);
        AppendToken(canonical, identity.originalStateFingerprint);
        AppendToken(canonical, identity.resolvedStateFingerprint);
        return Hash(canonical.ToString());
    }

    public static string CreateRequestFingerprint(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        ApparelWorkOrderSaveData sourceOrder,
        bool hasLeaseAuthority,
        string leaseAuthorityFingerprint,
        ProductionApparelOrderPendingEffectIdentity pendingEffect)
    {
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-apparel-order-terminal-request@1|");
        AppendToken(canonical, parentOperationId);
        AppendToken(canonical, stepOperationId);
        AppendToken(canonical, ownerStableId);
        AppendToken(canonical, CreateSourceOrderFingerprint(sourceOrder));
        canonical.Append(hasLeaseAuthority ? "1|" : "0|");
        AppendToken(canonical, leaseAuthorityFingerprint);
        AppendToken(canonical, pendingEffect?.identityFingerprint ?? string.Empty);
        return Hash(canonical.ToString());
    }

    public static string CreateLeaseReleaseCommitId(
        string stepOperationId,
        string requestFingerprint) =>
        "production-apparel-lease-release-commit:" + Hash(
            (stepOperationId ?? string.Empty) + "\n"
            + (requestFingerprint ?? string.Empty));

    public static string CreateLeaseReleaseReceiptFingerprint(
        string requestFingerprint,
        string leaseAuthorityFingerprint,
        string leaseReleaseCommitId) => Hash(
            "production-apparel-lease-release-receipt@1|"
            + (requestFingerprint ?? string.Empty) + "|"
            + (leaseAuthorityFingerprint ?? string.Empty) + "|"
            + (leaseReleaseCommitId ?? string.Empty));

    public static ProductionApparelOrderTerminalEffectReceipt
        CreateTerminalEffectReceipt(
            string stepOperationId,
            ApparelWorkOrderSaveData sourceOrder,
            string sourceOrderFingerprint,
            ProductionApparelOrderPendingEffectIdentity pendingEffect)
    {
        ProductionApparelOrderTerminalEffectReceipt receipt = new()
        {
            stepOperationId = stepOperationId ?? string.Empty,
            orderId = sourceOrder?.orderId ?? string.Empty,
            sourceOrderFingerprint = sourceOrderFingerprint ?? string.Empty,
            pendingEffectIdentityFingerprint =
                pendingEffect?.identityFingerprint ?? string.Empty,
            abandonedRequiredWorkBits = FloatBits(sourceOrder?.requiredWork ?? 0f),
            abandonedCompletedWorkBits = FloatBits(
                sourceOrder?.completedWork ?? 0f),
            historicalConsumedWorkBits = FloatBits(
                sourceOrder?.consumedWork ?? 0f)
        };
        receipt.commitId = "production-apparel-terminal-effect-commit:" + Hash(
            receipt.stepOperationId + "\n" + receipt.sourceOrderFingerprint);
        receipt.receiptFingerprint = CreateTerminalEffectReceiptFingerprint(
            receipt);
        return receipt;
    }

    public static string CreateTerminalEffectReceiptFingerprint(
        ProductionApparelOrderTerminalEffectReceipt receipt)
    {
        if (receipt == null)
            return string.Empty;
        StringBuilder canonical = new StringBuilder(384)
            .Append("production-apparel-terminal-effect-receipt@1|");
        AppendToken(canonical, receipt.stepOperationId);
        AppendToken(canonical, receipt.orderId);
        AppendToken(canonical, receipt.sourceOrderFingerprint);
        AppendToken(canonical, receipt.pendingEffectIdentityFingerprint);
        canonical.Append(receipt.abandonedRequiredWorkBits.ToString(
                CultureInfo.InvariantCulture)).Append('|')
            .Append(receipt.abandonedCompletedWorkBits.ToString(
                CultureInfo.InvariantCulture)).Append('|')
            .Append(receipt.historicalConsumedWorkBits.ToString(
                CultureInfo.InvariantCulture)).Append('|');
        AppendToken(canonical, receipt.commitId);
        return Hash(canonical.ToString());
    }

    public static ProductionApparelOrderSourceTerminalReceipt
        CreateSourceTerminalReceipt(
            string stepOperationId,
            ApparelWorkOrderSaveData sourceOrder,
            string sourceOrderFingerprint,
            string terminalEffectReceiptFingerprint)
    {
        ProductionApparelOrderSourceTerminalReceipt receipt = new()
        {
            stepOperationId = stepOperationId ?? string.Empty,
            orderId = sourceOrder?.orderId ?? string.Empty,
            sourceOrderFingerprint = sourceOrderFingerprint ?? string.Empty,
            terminalEffectReceiptFingerprint =
                terminalEffectReceiptFingerprint ?? string.Empty
        };
        receipt.commitId = "production-apparel-source-terminal-commit:" + Hash(
            receipt.stepOperationId + "\n" + receipt.sourceOrderFingerprint);
        receipt.receiptFingerprint = CreateSourceTerminalReceiptFingerprint(
            receipt);
        return receipt;
    }

    public static string CreateSourceTerminalReceiptFingerprint(
        ProductionApparelOrderSourceTerminalReceipt receipt)
    {
        if (receipt == null)
            return string.Empty;
        StringBuilder canonical = new StringBuilder(320)
            .Append("production-apparel-source-terminal-receipt@1|");
        AppendToken(canonical, receipt.stepOperationId);
        AppendToken(canonical, receipt.orderId);
        AppendToken(canonical, receipt.sourceOrderFingerprint);
        AppendToken(canonical, receipt.terminalEffectReceiptFingerprint);
        AppendToken(canonical, receipt.commitId);
        return Hash(canonical.ToString());
    }

    public static string CreateCommitId(
        string stepOperationId,
        string requestFingerprint) => CommitPrefix + Hash(
            (stepOperationId ?? string.Empty) + "\n"
            + (requestFingerprint ?? string.Empty));

    public static string CreateReceiptFingerprint(
        string requestFingerprint,
        string leaseReleaseReceiptFingerprint,
        string terminalEffectReceiptFingerprint,
        string sourceTerminalReceiptFingerprint,
        string commitId)
    {
        StringBuilder canonical = new StringBuilder(384)
            .Append("production-apparel-order-terminal-receipt@1|");
        AppendToken(canonical, requestFingerprint);
        AppendToken(canonical, leaseReleaseReceiptFingerprint);
        AppendToken(canonical, terminalEffectReceiptFingerprint);
        AppendToken(canonical, sourceTerminalReceiptFingerprint);
        AppendToken(canonical, commitId);
        return Hash(canonical.ToString());
    }

    public static bool IsValidSave(
        ProductionApparelOrderTerminalDrainSaveData value)
    {
        if (value == null
            || value.schemaVersion !=
                ProductionApparelOrderTerminalDrainSaveData.CurrentSchemaVersion
            || !Token(value.parentOperationId)
            || !Token(value.stepOperationId)
            || !Token(value.ownerStableId)
            || !Token(value.orderId)
            || !Token(value.facilityId)
            || !Enum.IsDefined(typeof(ApparelWorkOrderKind), value.orderKind)
            || !Enum.IsDefined(
                typeof(ProductionApparelOrderTerminalDrainPhase), value.phase)
            || !IsValidSourceOrder(value.sourceOrder)
            || !string.Equals(value.orderId, value.sourceOrder.orderId,
                StringComparison.Ordinal)
            || !string.Equals(value.facilityId,
                value.sourceOrder.facilityInstanceId, StringComparison.Ordinal)
            || value.orderKind != value.sourceOrder.kind
            || !Digest(value.sourceOrderFingerprint)
            || !string.Equals(value.sourceOrderFingerprint,
                CreateSourceOrderFingerprint(value.sourceOrder),
                StringComparison.Ordinal)
            || !Digest(value.leaseAuthorityFingerprint)
            || !IsPendingEffectMatch(value.sourceOrder, value.pendingEffect)
            || !Digest(value.requestFingerprint)
            || !string.Equals(value.requestFingerprint,
                CreateRequestFingerprint(
                    value.parentOperationId,
                    value.stepOperationId,
                    value.ownerStableId,
                    value.sourceOrder,
                    value.hasLeaseAuthority,
                    value.leaseAuthorityFingerprint,
                    value.pendingEffect),
                StringComparison.Ordinal))
        {
            return false;
        }

        bool leaseReleased = value.phase >=
            ProductionApparelOrderTerminalDrainPhase
                .LeaseAuthorityReleasedAwaitingTerminalEffect;
        bool effectCommitted = value.phase >=
            ProductionApparelOrderTerminalDrainPhase
                .TerminalEffectCommittedAwaitingSourceOrderTerminal;
        bool sourceTerminal = value.phase >=
            ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement;

        if (!leaseReleased)
        {
            return Empty(value.leaseReleaseCommitId)
                && Empty(value.leaseReleaseReceiptFingerprint)
                && value.terminalEffectReceipt == null
                && value.sourceTerminalReceipt == null
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint);
        }
        string expectedLeaseCommit = CreateLeaseReleaseCommitId(
            value.stepOperationId,
            value.requestFingerprint);
        if (!string.Equals(value.leaseReleaseCommitId, expectedLeaseCommit,
                StringComparison.Ordinal)
            || !string.Equals(value.leaseReleaseReceiptFingerprint,
                CreateLeaseReleaseReceiptFingerprint(
                    value.requestFingerprint,
                    value.leaseAuthorityFingerprint,
                    expectedLeaseCommit),
                StringComparison.Ordinal))
        {
            return false;
        }
        if (!effectCommitted)
        {
            return value.terminalEffectReceipt == null
                && value.sourceTerminalReceipt == null
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint);
        }
        ProductionApparelOrderTerminalEffectReceipt expectedEffect =
            CreateTerminalEffectReceipt(
                value.stepOperationId,
                value.sourceOrder,
                value.sourceOrderFingerprint,
                value.pendingEffect);
        if (!EffectReceiptEquals(value.terminalEffectReceipt, expectedEffect))
            return false;
        if (!sourceTerminal)
        {
            return value.sourceTerminalReceipt == null
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint);
        }
        ProductionApparelOrderSourceTerminalReceipt expectedSource =
            CreateSourceTerminalReceipt(
                value.stepOperationId,
                value.sourceOrder,
                value.sourceOrderFingerprint,
                expectedEffect.receiptFingerprint);
        string expectedCommit = CreateCommitId(
            value.stepOperationId,
            value.requestFingerprint);
        return SourceReceiptEquals(value.sourceTerminalReceipt, expectedSource)
            && string.Equals(value.commitId, expectedCommit,
                StringComparison.Ordinal)
            && string.Equals(value.receiptFingerprint,
                CreateReceiptFingerprint(
                    value.requestFingerprint,
                    value.leaseReleaseReceiptFingerprint,
                    expectedEffect.receiptFingerprint,
                    expectedSource.receiptFingerprint,
                    expectedCommit),
                StringComparison.Ordinal);
    }

    public static bool IsValidSourceOrder(ApparelWorkOrderSaveData value)
    {
        if (value == null
            || !Token(value.orderId)
            || !Token(value.facilityInstanceId)
            || !Enum.IsDefined(typeof(ApparelWorkOrderKind), value.kind)
            || !Enum.IsDefined(typeof(ApparelWorkOrderState), value.state)
            || !Enum.IsDefined(
                typeof(ApparelRepairCommitPhase), value.repairCommitPhase)
            || !FiniteNonNegative(value.requiredWork)
            || !FiniteNonNegative(value.completedWork)
            || value.completedWork > value.requiredWork
            || !FiniteNonNegative(value.consumedWork)
            || !FiniteNonNegative(value.workBudget)
            || !FiniteNonNegative(value.craftWorkPerAttempt)
            || !FiniteNonNegative(value.nextRetryGameHour)
            || value.targetItemInstanceIds == null
            || value.materialStackIds == null
            || value.materialStackAmounts == null
            || value.repairSourceStackIds == null
            || value.contributions == null
            || value.materialStackIds.Count != value.materialStackAmounts.Count
            || value.materialStackAmounts.Any(amount => amount <= 0)
            || !CanonicalUnique(value.materialStackIds)
            || !CanonicalUnique(value.targetItemInstanceIds)
            || !CanonicalUnique(value.repairSourceStackIds)
            || !ApparelPhysicalTransaction.ValidateCraftOwnerShape(
                value,
                out _)
            || !ApparelRejectedDismantleOutbox.ValidateOwnerShape(
                value,
                out _))
        {
            return false;
        }

        return TryCreatePendingEffectIdentityUnchecked(value);
    }

    public static bool IsValidPendingEffect(
        ProductionApparelOrderPendingEffectIdentity value)
    {
        if (value == null
            || value.kind == ProductionApparelOrderPendingEffectKind.None
            || !Enum.IsDefined(typeof(ProductionApparelOrderPendingEffectKind),
                value.kind)
            || !Token(value.operationId)
            || !Token(value.reasonCode)
            || value.quantity < 0
            || value.massGrams < 0L
            || value.completedQuantity < 0
            || value.completedQuantity > value.quantity
            || value.sourceStackIds == null
            || value.sourceStackIds.Count == 0
            || !CanonicalUnique(value.sourceStackIds)
            || !Token(value.targetStackId)
            || !Digest(value.identityFingerprint)
            || !string.Equals(value.identityFingerprint,
                CreatePendingEffectFingerprint(value),
                StringComparison.Ordinal))
        {
            return false;
        }
        if (value.kind == ProductionApparelOrderPendingEffectKind
                .RepairDisposition)
        {
            return Token(value.priorCommitId)
                && value.quantity > 0
                && value.massGrams > 0L
                && Digest(value.originalStateFingerprint)
                && Digest(value.resolvedStateFingerprint);
        }
        if (value.kind != ProductionApparelOrderPendingEffectKind
                .RejectedOutputDismantle
            || value.phase < 0
            || value.phase > 3
            || !Digest(value.originalStateFingerprint))
        {
            return false;
        }
        return value.phase switch
        {
            0 => Empty(value.priorCommitId)
                && value.massGrams == 0L
                && value.completedQuantity == 0
                && !value.sourceAlreadyConsumed
                && Empty(value.resolvedStateFingerprint),
            1 => Token(value.priorCommitId)
                && value.massGrams > 0L
                && value.completedQuantity == 0
                && value.sourceAlreadyConsumed
                && Empty(value.resolvedStateFingerprint),
            2 or 3 => Token(value.priorCommitId)
                && value.massGrams > 0L
                && value.completedQuantity == value.quantity
                && value.sourceAlreadyConsumed
                && Digest(value.resolvedStateFingerprint),
            _ => false
        };
    }

    public static bool EffectReceiptEquals(
        ProductionApparelOrderTerminalEffectReceipt left,
        ProductionApparelOrderTerminalEffectReceipt right) =>
        left != null && right != null
        && string.Equals(left.stepOperationId, right.stepOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.orderId, right.orderId, StringComparison.Ordinal)
        && string.Equals(left.sourceOrderFingerprint,
            right.sourceOrderFingerprint, StringComparison.Ordinal)
        && string.Equals(left.pendingEffectIdentityFingerprint,
            right.pendingEffectIdentityFingerprint, StringComparison.Ordinal)
        && left.abandonedRequiredWorkBits == right.abandonedRequiredWorkBits
        && left.abandonedCompletedWorkBits == right.abandonedCompletedWorkBits
        && left.historicalConsumedWorkBits == right.historicalConsumedWorkBits
        && string.Equals(left.commitId, right.commitId, StringComparison.Ordinal)
        && string.Equals(left.receiptFingerprint, right.receiptFingerprint,
            StringComparison.Ordinal);

    public static bool SourceReceiptEquals(
        ProductionApparelOrderSourceTerminalReceipt left,
        ProductionApparelOrderSourceTerminalReceipt right) =>
        left != null && right != null
        && string.Equals(left.stepOperationId, right.stepOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.orderId, right.orderId, StringComparison.Ordinal)
        && string.Equals(left.sourceOrderFingerprint,
            right.sourceOrderFingerprint, StringComparison.Ordinal)
        && string.Equals(left.terminalEffectReceiptFingerprint,
            right.terminalEffectReceiptFingerprint, StringComparison.Ordinal)
        && string.Equals(left.commitId, right.commitId, StringComparison.Ordinal)
        && string.Equals(left.receiptFingerprint, right.receiptFingerprint,
            StringComparison.Ordinal);

    public static bool IsDigest(string value) => Digest(value);

    private static bool IsPendingEffectMatch(
        ApparelWorkOrderSaveData sourceOrder,
        ProductionApparelOrderPendingEffectIdentity actual)
    {
        if (!TryCreatePendingEffectIdentity(
                sourceOrder,
                out ProductionApparelOrderPendingEffectIdentity expected,
                out _))
        {
            return false;
        }
        if (expected == null || actual == null)
            return expected == null && actual == null;
        return string.Equals(
            expected.identityFingerprint,
            actual.identityFingerprint,
            StringComparison.Ordinal)
            && JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual);
    }

    private static bool TryCreatePendingEffectIdentityUnchecked(
        ApparelWorkOrderSaveData sourceOrder)
    {
        bool repairAny = sourceOrder.repairCommitPhase !=
                ApparelRepairCommitPhase.None
            || !Empty(sourceOrder.repairOperationId)
            || !Empty(sourceOrder.repairReasonCode)
            || !Empty(sourceOrder.repairCommitId)
            || sourceOrder.repairSourceStackIds.Count > 0
            || sourceOrder.repairInputQuantity != 0
            || sourceOrder.repairInputMassGrams != 0L
            || !Empty(sourceOrder.repairTargetStackId)
            || !Empty(sourceOrder.repairOriginalStatePayload)
            || !Empty(sourceOrder.repairResolvedStatePayload);
        if (!repairAny)
            return true;
        return sourceOrder.repairCommitPhase != ApparelRepairCommitPhase.None;
    }

    private static bool CanonicalUnique(IEnumerable<string> values)
    {
        if (values == null)
            return false;
        string[] array = values.ToArray();
        return array.All(Token)
            && array.Distinct(StringComparer.Ordinal).Count() == array.Length;
    }

    private static int FloatBits(float value) =>
        BitConverter.SingleToInt32Bits(value);

    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }

    private static bool Empty(string value) => string.IsNullOrEmpty(value);
    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
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
