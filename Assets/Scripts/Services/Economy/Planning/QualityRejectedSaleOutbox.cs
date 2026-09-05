using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IQualityRejectedSaleCommandPort
{
    bool TryGetPendingRejectedSaleTransfer(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);

    bool TryPublishRejectedSaleIncome(
        QualityRejectedSalePending pending,
        out string failureReason);

    bool TryReleaseRejectedSaleUniqueAuthority(
        QualityRejectedSalePending pending,
        out string failureReason);

    bool AcknowledgeRejectedSaleTransfer(
        string commitId,
        out string failureReason);
}

public static class QualityRejectedSaleOutbox
{
    public const string OperationPrefix =
        QualityRejectedSaleContract.OperationPrefix;
    public const string TransferReason =
        QualityRejectedSaleContract.TransferReason;

    public static string FormatOperationId(int sequence, string sourceStackId) =>
        QualityRejectedSaleContract.FormatOperationId(sequence, sourceStackId);

    public static QualityRejectedSalePending CreatePrepared(
        int sequence,
        WorldItemStackSnapshot source,
        int proceeds,
        FacilityBufferAcknowledgedOutputReleaseTarget target,
        bool requiresCombatAuthority)
    {
        if (source == null
            || source.Quantity != 1
            || proceeds <= 0
            || !target.IsValid)
        {
            throw new ArgumentException(
                "A canonical rejected-sale source, proceeds and target are required.");
        }

        QualityRejectedSalePending pending = new()
        {
            sequence = sequence,
            operationId = FormatOperationId(sequence, source.StackId),
            reasonCode = TransferReason,
            sourceStackId = source.StackId,
            itemId = source.ItemId,
            itemInstanceId = source.ItemInstanceId,
            componentFingerprint = ComputeComponentFingerprint(
                source.ItemId,
                source.ItemInstanceId,
                source.Components),
            destinationId = target.DestinationId,
            destinationX = target.DestinationPosition.x,
            destinationY = target.DestinationPosition.y,
            quantity = 1,
            proceeds = proceeds,
            requiresCombatAuthority = requiresCombatAuthority,
            phase = QualityRejectedSaleCommitPhase.Prepared
        };
        if (!HasCanonicalPrepared(pending))
        {
            throw new InvalidOperationException(
                "Rejected-sale prepared owner is not canonical.");
        }
        return pending;
    }

    public static bool TryApplyPhysicalReceipt(
        QualityRejectedSalePending pending,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasCanonicalPrepared(pending)
            || !ReceiptMatchesPrepared(pending, receipt))
        {
            failureReason = "quality-rejected-sale-physical-receipt-mismatch";
            return false;
        }
        pending.commitId = receipt.CommitId;
        pending.inputMassGrams = receipt.InputMassGrams;
        pending.phase = QualityRejectedSaleCommitPhase.PhysicalCommitted;
        return HasCanonicalPending(pending);
    }

    public static bool TryFinalizePending(
        QualityRejectedSalePending pending,
        IQualityRejectedSaleCommandPort commands,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasCanonicalPending(pending))
        {
            failureReason = "quality-rejected-sale-owner-invalid";
            return false;
        }
        if (commands == null)
        {
            failureReason = "quality-rejected-sale-command-port-missing";
            return false;
        }

        if (pending.phase == QualityRejectedSaleCommitPhase.Prepared)
        {
            if (!commands.TryGetPendingRejectedSaleTransfer(
                    pending.operationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !TryApplyPhysicalReceipt(
                    pending,
                    receipt,
                    out failureReason))
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? "quality-rejected-sale-physical-receipt-missing"
                    : failureReason;
                return false;
            }
        }

        if (pending.phase == QualityRejectedSaleCommitPhase.PhysicalCommitted)
        {
            if (!commands.TryPublishRejectedSaleIncome(
                    pending,
                    out failureReason))
            {
                return false;
            }
            pending.phase = QualityRejectedSaleCommitPhase.IncomePublished;
        }

        if (pending.phase == QualityRejectedSaleCommitPhase.IncomePublished)
        {
            if (!commands.TryReleaseRejectedSaleUniqueAuthority(
                    pending,
                    out failureReason))
            {
                return false;
            }
            pending.phase =
                QualityRejectedSaleCommitPhase.UniqueAuthorityReleased;
        }

        return commands.AcknowledgeRejectedSaleTransfer(
            pending.commitId,
            out failureReason);
    }

    public static bool HasCanonicalPending(QualityRejectedSalePending pending) =>
        QualityRejectedSaleContract.HasCanonicalPending(pending);

    public static bool HasCanonicalPrepared(QualityRejectedSalePending pending) =>
        QualityRejectedSaleContract.HasCanonicalPrepared(pending);

    public static bool ReceiptMatchesPrepared(
        QualityRejectedSalePending pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        pending != null
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(receipt.OperationId, pending.operationId, StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, pending.reasonCode, StringComparison.Ordinal)
        && receipt.Quantity == 1
        && receipt.InputMassGrams > 0L
        && (receipt.SourceStackIds ?? Array.Empty<string>()).Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            pending.sourceStackId,
            StringComparison.Ordinal);

    public static bool ReceiptMatchesSaved(
        QualityRejectedSalePending pending,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        HasCanonicalPending(pending)
        && pending.phase != QualityRejectedSaleCommitPhase.Prepared
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(receipt.OperationId, pending.operationId, StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, pending.reasonCode, StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, pending.commitId, StringComparison.Ordinal)
        && receipt.Quantity == 1
        && receipt.InputMassGrams == pending.inputMassGrams
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            pending.sourceStackId,
            StringComparison.Ordinal);

    public static string ComputeComponentFingerprint(
        string itemId,
        string itemInstanceId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        List<ItemInstanceComponentSaveData> normalized = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList();
        int equipmentIndex = normalized.FindIndex(component => string.Equals(
            component.componentTypeId,
            ItemInstanceComponentIds.Equipment,
            StringComparison.Ordinal));
        if (equipmentIndex >= 0
            && EquipmentItemStateCodec.TryDecodeFull(
                normalized[equipmentIndex],
                out EquipmentPhysicalStatePayload payload,
                out _))
        {
            CombatEquipmentInstance equipment = payload.equipment.Clone();
            equipment.worldState = CombatEquipmentWorldState.Stored;
            equipment.ownerCharacterId = string.Empty;
            equipment.sourceStackId = string.Empty;
            normalized[equipmentIndex] = EquipmentItemStateCodec.Encode(
                equipment,
                payload.attachedModules);
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(itemId);
        digest.Append(itemInstanceId);
        digest.Append(ItemStackSignature.Create(itemId, normalized));
        return digest.ComputeSha256();
    }

}
