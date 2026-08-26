using System;
using System.Linq;

/// <summary>
/// Transfers one rejected unique equipment stack into dismantle WIP. Recovery
/// outputs are published separately with deterministic Source commits; the
/// input receipt is acknowledged only after every recovery output exists.
/// </summary>
public static class CombatEquipmentRejectedDismantleOutbox
{
    public const string ReasonCode =
        "combat-equipment-rejected-output-to-dismantle-wip";
    public const string OperationPrefix = "combat-craft-rejected-dismantle:";
    public const string RecoveryOperationPrefix = "combat-craft-rejected-recovery:";

    public static string FormatOperationId(string orderId, int attemptIndex) =>
        $"{OperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}";

    public static string FormatRecoveryOperationId(
        string orderId,
        int attemptIndex,
        int outputIndex) =>
        $"{RecoveryOperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}:"
        + $"{Math.Max(0, outputIndex):D4}";

    public static bool TryCommitOrResume(
        CombatEquipmentCraftOrderSaveData order,
        IEquipmentPhysicalItemGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || items == null)
        {
            failureReason = "combat-craft-rejected-dismantle-invalid";
            return false;
        }
        if (order.rejectedDismantleAcknowledged)
        {
            return ValidateProvenance(order, out failureReason);
        }

        string operationId = FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        bool starting = string.IsNullOrEmpty(order.rejectedDismantleOperationId);
        if (starting)
        {
            WorldItemStackSnapshot source = items.GetAllStacks()
                .SingleOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        order.rejectedStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemInstanceId,
                        order.rejectedInstanceId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.FacilityOutputBuffer
                    && stack.Quantity == 1
                    && stack.ReservedQuantity == 0
                    && string.IsNullOrEmpty(stack.ReservedByPersistentId));
            if (source == null)
            {
                failureReason = "combat-craft-rejected-dismantle-source-missing";
                return false;
            }
        }
        else if (!ValidateProvenance(order, out failureReason)
                 || !items.TryGetPendingBatchPhysicalDisposition(operationId, out _))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "combat-craft-rejected-dismantle-receipt-missing"
                : failureReason;
            return false;
        }

        PhysicalItemTransformInput[] inputs =
        {
            new(order.rejectedStackId, 1)
        };
        if (!items.TryCommitPendingBatchPhysicalDisposition(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        if (starting)
        {
            order.rejectedDismantleOperationId = receipt.OperationId;
            order.rejectedDismantleCommitId = receipt.CommitId;
            order.rejectedDismantleRequestFingerprint =
                CreateRequestFingerprint(order.rejectedStackId);
            order.rejectedDismantleInputMassGrams = receipt.InputMassGrams;
            order.rejectedOutputConsumed = true;
        }
        if (!ValidateReceipt(order, receipt, out failureReason))
        {
            return false;
        }
        return true;
    }

    public static bool TryAcknowledgeRecovery(
        CombatEquipmentCraftOrderSaveData order,
        IEquipmentPhysicalItemGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || !ValidateProvenance(order, out failureReason))
        {
            return false;
        }
        if (order.rejectedDismantleAcknowledged)
        {
            return true;
        }
        if (!order.rejectedRecoveryPublished)
        {
            failureReason = "combat-craft-rejected-recovery-not-published";
            return false;
        }
        if (!items.AcknowledgeBatchPhysicalDisposition(
                order.rejectedDismantleCommitId,
                out failureReason))
        {
            return false;
        }
        order.rejectedDismantleAcknowledged = true;
        return true;
    }

    public static bool ValidateProvenance(
        CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        bool hasOperation = !string.IsNullOrEmpty(
            order?.rejectedDismantleOperationId);
        if (!hasOperation)
        {
            bool empty = order != null
                && string.IsNullOrEmpty(order.rejectedDismantleCommitId)
                && string.IsNullOrEmpty(
                    order.rejectedDismantleRequestFingerprint)
                && order.rejectedDismantleInputMassGrams == 0L
                && !order.rejectedRecoveryPublished
                && !order.rejectedDismantleAcknowledged;
            if (!empty)
            {
                failureReason =
                    "combat-craft-rejected-dismantle-owner-partial";
            }
            return empty;
        }
        bool valid = order.dismantlingRejectedOutput
            && order.rejectedOutputConsumed
            && !string.IsNullOrWhiteSpace(order.rejectedStackId)
            && !string.IsNullOrWhiteSpace(order.rejectedInstanceId)
            && string.Equals(
                order.rejectedDismantleOperationId,
                FormatOperationId(order.orderId, order.qualityAttemptIndex),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.rejectedDismantleCommitId)
            && string.Equals(
                order.rejectedDismantleRequestFingerprint,
                CreateRequestFingerprint(order.rejectedStackId),
                StringComparison.Ordinal)
            && order.rejectedDismantleInputMassGrams > 0L
            && (!order.rejectedDismantleAcknowledged
                || order.rejectedRecoveryPublished);
        if (!valid)
        {
            failureReason = "combat-craft-rejected-dismantle-owner-invalid";
        }
        return valid;
    }

    public static string CreateRequestFingerprint(string sourceStackId) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{ReasonCode}:"
        + $"{sourceStackId}=1";

    public static void Clear(CombatEquipmentCraftOrderSaveData order)
    {
        order.rejectedDismantleOperationId = string.Empty;
        order.rejectedDismantleCommitId = string.Empty;
        order.rejectedDismantleRequestFingerprint = string.Empty;
        order.rejectedDismantleInputMassGrams = 0L;
        order.rejectedRecoveryPublished = false;
        order.rejectedDismantleAcknowledged = false;
    }

    private static bool ValidateReceipt(
        CombatEquipmentCraftOrderSaveData order,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        bool valid = receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(
                receipt.OperationId,
                order.rejectedDismantleOperationId,
                StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, ReasonCode, StringComparison.Ordinal)
            && string.Equals(
                receipt.CommitId,
                order.rejectedDismantleCommitId,
                StringComparison.Ordinal)
            && receipt.Quantity == 1
            && receipt.InputMassGrams == order.rejectedDismantleInputMassGrams
            && receipt.SourceStackIds.SequenceEqual(
                new[] { order.rejectedStackId },
                StringComparer.Ordinal);
        failureReason = valid
            ? string.Empty
            : "combat-craft-rejected-dismantle-receipt-mismatch";
        return valid;
    }
}
