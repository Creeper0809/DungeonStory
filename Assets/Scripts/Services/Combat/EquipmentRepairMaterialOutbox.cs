using System;
using System.Collections.Generic;
using System.Linq;

public static class EquipmentRepairMaterialOutbox
{
    public const string ReasonCode = "equipment-repair-materials-to-wip";
    private const string OperationPrefix = "equipment-repair-material:";

    public static string FormatOperationId(string orderId) =>
        OperationPrefix + orderId;

    public static bool TryCommitOrResume(
        CombatEquipmentRepairOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IPhysicalItemBatchDispositionService dispositions,
        string equipmentSourceStackId,
        float durabilityBefore,
        float durabilityAfter,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || stacks == null || dispositions == null)
        {
            failureReason = "equipment-repair-material-outbox-invalid";
            return false;
        }
        if (order.materialTransferAcknowledged)
        {
            return ValidateProvenance(order, out failureReason);
        }

        bool starting = string.IsNullOrEmpty(
            order.materialTransferOperationId);
        List<EquipmentRepairMaterialTransferInput> inputs;
        string operationId = FormatOperationId(order.orderId);
        if (starting)
        {
            if (string.IsNullOrWhiteSpace(equipmentSourceStackId)
                || !float.IsFinite(durabilityBefore)
                || !float.IsFinite(durabilityAfter)
                || durabilityBefore < 0f
                || durabilityAfter < durabilityBefore
                || durabilityAfter > 1f
                || !TrySelectInputs(
                    order,
                    stacks,
                    equipmentSourceStackId,
                    out inputs,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "equipment-repair-outcome-envelope-invalid"
                    : failureReason;
                return false;
            }
        }
        else
        {
            if (!ValidateProvenance(order, out failureReason)
                || order.materialTransferAcknowledged)
            {
                return order.materialTransferAcknowledged;
            }
            inputs = order.materialTransferInputs
                .Select(input => input.Clone())
                .ToList();
            if (!dispositions.TryGetPending(operationId, out _))
            {
                failureReason =
                    "equipment-repair-material-receipt-missing";
                return false;
            }
        }

        PhysicalItemTransformInput[] physicalInputs = inputs
            .Select(input => new PhysicalItemTransformInput(
                input.sourceStackId,
                input.quantity))
            .ToArray();
        if (!dispositions.TryCommitPending(
                physicalInputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }

        string fingerprint = CreateRequestFingerprint(inputs);
        if (starting)
        {
            // Persist the WIP owner immediately after the world debit. Any
            // later contract or outcome failure therefore remains recoverable.
            order.materialsConsumed = true;
            order.materialTransferOperationId = receipt.OperationId;
            order.materialTransferCommitId = receipt.CommitId;
            order.materialTransferRequestFingerprint = fingerprint;
            order.materialTransferMassGrams = receipt.InputMassGrams;
            order.materialTransferInputs = inputs
                .Select(input => input.Clone())
                .ToList();
            order.repairEquipmentSourceStackId = equipmentSourceStackId;
            order.repairDurabilityBefore = durabilityBefore;
            order.repairDurabilityAfter = durabilityAfter;
            order.state = CombatEquipmentRepairOrderState.InProgress;
        }

        if (!ValidateReceipt(order, receipt, out failureReason)
            || (!starting
                && (!string.Equals(
                        order.materialTransferRequestFingerprint,
                        fingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        order.materialTransferCommitId,
                        receipt.CommitId,
                        StringComparison.Ordinal)
                    || order.materialTransferMassGrams
                        != receipt.InputMassGrams)))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "equipment-repair-material-provenance-mismatch"
                : failureReason;
            return false;
        }

        return true;
    }

    public static bool TryAcknowledgeOutcome(
        CombatEquipmentRepairOrder order,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || dispositions == null
            || !ValidateProvenance(order, out failureReason))
        {
            return false;
        }
        if (order.materialTransferAcknowledged)
        {
            return true;
        }
        if (!order.repairOutcomePublished)
        {
            failureReason = "equipment-repair-outcome-not-published";
            return false;
        }
        if (!dispositions.Acknowledge(
                order.materialTransferCommitId,
                out failureReason))
        {
            return false;
        }

        order.materialTransferAcknowledged = true;
        return true;
    }

    internal static bool TryAcknowledgeTerminalLoss(
        CombatEquipmentRepairOrder frozenOrder,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (frozenOrder == null
            || dispositions == null
            || !ValidateProvenance(frozenOrder, out failureReason))
        {
            return false;
        }
        if (!frozenOrder.materialsConsumed)
            return true;

        bool hasPending = dispositions.TryGetPending(
            frozenOrder.materialTransferOperationId,
            out PhysicalItemBatchDispositionReceipt pending);
        if (frozenOrder.materialTransferAcknowledged)
        {
            if (hasPending)
            {
                failureReason =
                    "equipment-repair-terminal-acknowledged-material-still-pending";
                return false;
            }
            return true;
        }
        if (!hasPending)
        {
            // The maintenance aggregate publishes the terminal WIP row before
            // this acknowledgement. A missing receipt is therefore the single
            // legal crash-ahead window between the physical acknowledgement
            // and the terminal-row phase advance.
            return true;
        }
        if (!ValidateReceipt(frozenOrder, pending, out failureReason))
        {
            failureReason =
                "equipment-repair-terminal-material-receipt-conflict:"
                + failureReason;
            return false;
        }
        return dispositions.Acknowledge(
            frozenOrder.materialTransferCommitId,
            out failureReason);
    }

    public static bool ValidateProvenance(
        CombatEquipmentRepairOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || order.materialTransferInputs == null)
        {
            failureReason = "equipment-repair-material-owner-invalid";
            return false;
        }

        bool hasOperation = !string.IsNullOrEmpty(
            order.materialTransferOperationId);
        if (!hasOperation)
        {
            bool empty = !order.materialsConsumed
                && string.IsNullOrEmpty(order.materialTransferCommitId)
                && string.IsNullOrEmpty(
                    order.materialTransferRequestFingerprint)
                && order.materialTransferMassGrams == 0L
                && order.materialTransferInputs.Count == 0
                && string.IsNullOrEmpty(order.repairEquipmentSourceStackId)
                && order.repairDurabilityBefore == 0f
                && order.repairDurabilityAfter == 0f
                && !order.repairOutcomePublished
                && !order.materialTransferAcknowledged
                && !order.repairOutputReleased;
            if (!empty)
            {
                failureReason =
                    "equipment-repair-material-owner-partial";
            }
            return empty;
        }

        EquipmentRepairMaterialTransferInput[] inputs =
            order.materialTransferInputs
                .Where(input => input != null)
                .ToArray();
        bool validInputs = inputs.Length ==
                order.materialTransferInputs.Count
            && inputs.Length > 0
            && inputs.All(input =>
                string.Equals(
                    input.itemId,
                    order.materialItemId,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(input.sourceStackId)
                && string.Equals(
                    input.sourceStackId,
                    input.sourceStackId.Trim(),
                    StringComparison.Ordinal)
                && input.quantity > 0)
            && inputs.Sum(input => input.quantity)
                == order.requiredMaterialAmount
            && inputs.Select(input => input.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() == inputs.Length
            && inputs.Select(input => input.sourceStackId)
                .SequenceEqual(
                    inputs.Select(input => input.sourceStackId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal);
        bool valid = order.materialsConsumed
            && string.Equals(
                order.materialTransferOperationId,
                FormatOperationId(order.orderId),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.materialTransferCommitId)
            && string.Equals(
                order.materialTransferCommitId,
                order.materialTransferCommitId.Trim(),
                StringComparison.Ordinal)
            && order.materialTransferMassGrams > 0L
            && !string.IsNullOrWhiteSpace(
                order.repairEquipmentSourceStackId)
            && string.Equals(
                order.repairEquipmentSourceStackId,
                order.repairEquipmentSourceStackId.Trim(),
                StringComparison.Ordinal)
            && !float.IsNaN(order.repairDurabilityBefore)
            && !float.IsInfinity(order.repairDurabilityBefore)
            && !float.IsNaN(order.repairDurabilityAfter)
            && !float.IsInfinity(order.repairDurabilityAfter)
            && order.repairDurabilityBefore >= 0f
            && order.repairDurabilityAfter
                >= order.repairDurabilityBefore
            && order.repairDurabilityAfter <= 1f
            && validInputs
            && string.Equals(
                order.materialTransferRequestFingerprint,
                CreateRequestFingerprint(inputs),
                StringComparison.Ordinal)
            && (!order.materialTransferAcknowledged
                || order.repairOutcomePublished)
            && (!order.repairOutputReleased
                || (order.repairOutcomePublished
                    && order.materialTransferAcknowledged));
        if (!valid)
        {
            failureReason = "equipment-repair-material-owner-invalid";
        }
        return valid;
    }

    public static string CreateRequestFingerprint(
        IReadOnlyList<EquipmentRepairMaterialTransferInput> inputs) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{ReasonCode}:"
        + string.Join(",", (inputs
                ?? Array.Empty<EquipmentRepairMaterialTransferInput>())
            .Where(input => input != null)
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => $"{input.sourceStackId}={input.quantity}"));

    private static bool TrySelectInputs(
        CombatEquipmentRepairOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        string equipmentSourceStackId,
        out List<EquipmentRepairMaterialTransferInput> inputs,
        out string failureReason)
    {
        inputs = new List<EquipmentRepairMaterialTransferInput>();
        int remaining = order.requiredMaterialAmount;
        foreach (WorldItemStackSnapshot stack in stacks
                     .Where(stack => stack != null
                         && !string.Equals(
                             stack.StackId,
                             equipmentSourceStackId,
                             StringComparison.Ordinal)
                         && stack.State ==
                             WorldItemStackState.FacilityBuffer
                         && string.Equals(
                             stack.DestinationId,
                             order.FacilityDestinationId,
                             StringComparison.Ordinal)
                         && string.Equals(
                             stack.ItemId,
                             order.materialItemId,
                             StringComparison.Ordinal)
                         && stack.AvailableQuantity > 0
                         && stack.ReservedQuantity == 0
                         && string.IsNullOrEmpty(
                             stack.ReservedByPersistentId))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
            {
                break;
            }
            int quantity = Math.Min(remaining, stack.AvailableQuantity);
            inputs.Add(new EquipmentRepairMaterialTransferInput
            {
                itemId = order.materialItemId,
                sourceStackId = stack.StackId,
                quantity = quantity
            });
            remaining -= quantity;
        }

        if (remaining > 0)
        {
            inputs.Clear();
            failureReason = "equipment-repair-material-missing";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool ValidateReceipt(
        CombatEquipmentRepairOrder order,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        string[] sourceIds = order.materialTransferInputs
            .Select(input => input.sourceStackId)
            .ToArray();
        bool valid = receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(
                receipt.OperationId,
                order.materialTransferOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.ReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            && receipt.Quantity == order.requiredMaterialAmount
            && receipt.InputMassGrams == order.materialTransferMassGrams
            && receipt.SourceStackIds.SequenceEqual(
                sourceIds,
                StringComparer.Ordinal);
        if (!valid)
        {
            failureReason = "equipment-repair-material-receipt-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }
}
