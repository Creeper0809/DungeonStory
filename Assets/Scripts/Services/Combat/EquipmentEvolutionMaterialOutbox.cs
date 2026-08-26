using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Moves authored reforge/reattunement inputs from the facility buffer into a
/// durable WIP owner. Equipment custody remains represented by its exact
/// FacilityBuffer stack and is never part of this disposition.
/// </summary>
public static class EquipmentEvolutionMaterialOutbox
{
    public const string ReforgeReasonCode =
        "equipment-reforge-materials-to-wip";
    public const string ReattunementReasonCode =
        "equipment-reattunement-catalyst-to-wip";
    private const string ReforgeOperationPrefix =
        "equipment-reforge-material:";
    private const string ReattunementOperationPrefix =
        "equipment-reattunement-material:";

    public static string FormatReforgeOperationId(string orderId) =>
        ReforgeOperationPrefix + orderId;

    public static string FormatReattunementOperationId(string orderId) =>
        ReattunementOperationPrefix + orderId;

    public static bool TryCommitOrFinalize(
        EvolutionReforgeOrder order,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions,
        string equipmentSourceStackId,
        out string failureReason)
    {
        if (items == null)
        {
            failureReason = "equipment-reforge-material-outbox-invalid";
            return false;
        }

        return TryCommitOrFinalize(
            order,
            items.GetAllStacks(),
            dispositions,
            equipmentSourceStackId,
            out failureReason);
    }

    public static bool TryCommitOrFinalize(
        EvolutionReforgeOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IPhysicalItemBatchDispositionService dispositions,
        string equipmentSourceStackId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || stacks == null || dispositions == null)
        {
            failureReason = "equipment-reforge-material-outbox-invalid";
            return false;
        }
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            return true;
        }

        Dictionary<string, int> requirements =
            EquipmentEvolutionRules.BuildRequirements(order);
        if (!TryCommitCore(
                order.orderId,
                order.destinationId,
                order.equipmentInstanceId,
                equipmentSourceStackId,
                requirements,
                FormatReforgeOperationId(order.orderId),
                ReforgeReasonCode,
                order.materialTransferOperationId,
                order.materialTransferCommitId,
                order.materialTransferRequestFingerprint,
                order.materialTransferMassGrams,
                order.materialTransferInputs,
                stacks,
                dispositions,
                out PhysicalItemBatchDispositionReceipt receipt,
                out List<EquipmentEvolutionMaterialTransferInput> inputs,
                out string requestFingerprint,
                out bool startingNewTransfer,
                out failureReason))
        {
            return false;
        }

        if (startingNewTransfer)
        {
            PublishOwner(order, receipt, inputs, requestFingerprint);
        }
        if (!ValidateReceipt(
                receipt,
                FormatReforgeOperationId(order.orderId),
                ReforgeReasonCode,
                inputs,
                out failureReason))
        {
            return false;
        }
        if (!order.materialTransferOutcomePublished)
        {
            order.materialsConsumed = true;
            order.equipmentDelivered = true;
            order.state = EvolutionReforgeOrderState.Ready;
            order.materialTransferOutcomePublished = true;
        }
        if (!dispositions.Acknowledge(
                order.materialTransferCommitId,
                out failureReason))
        {
            return false;
        }

        ClearPending(order);
        return true;
    }

    public static bool TryCommitOrFinalize(
        EquipmentReattunementOrder order,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions,
        string equipmentSourceStackId,
        out string failureReason)
    {
        if (items == null)
        {
            failureReason =
                "equipment-reattunement-material-outbox-invalid";
            return false;
        }

        return TryCommitOrFinalize(
            order,
            items.GetAllStacks(),
            dispositions,
            equipmentSourceStackId,
            out failureReason);
    }

    public static bool TryCommitOrFinalize(
        EquipmentReattunementOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IPhysicalItemBatchDispositionService dispositions,
        string equipmentSourceStackId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || stacks == null || dispositions == null)
        {
            failureReason =
                "equipment-reattunement-material-outbox-invalid";
            return false;
        }
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            return true;
        }

        Dictionary<string, int> requirements =
            new(StringComparer.Ordinal)
            {
                [order.catalystItemId] = 1
            };
        if (!TryCommitCore(
                order.orderId,
                order.destinationId,
                order.equipmentInstanceId,
                equipmentSourceStackId,
                requirements,
                FormatReattunementOperationId(order.orderId),
                ReattunementReasonCode,
                order.materialTransferOperationId,
                order.materialTransferCommitId,
                order.materialTransferRequestFingerprint,
                order.materialTransferMassGrams,
                order.materialTransferInputs,
                stacks,
                dispositions,
                out PhysicalItemBatchDispositionReceipt receipt,
                out List<EquipmentEvolutionMaterialTransferInput> inputs,
                out string requestFingerprint,
                out bool startingNewTransfer,
                out failureReason))
        {
            return false;
        }

        if (startingNewTransfer)
        {
            PublishOwner(order, receipt, inputs, requestFingerprint);
        }
        if (!ValidateReceipt(
                receipt,
                FormatReattunementOperationId(order.orderId),
                ReattunementReasonCode,
                inputs,
                out failureReason))
        {
            return false;
        }
        if (!order.materialTransferOutcomePublished)
        {
            order.materialsConsumed = true;
            order.equipmentDelivered = true;
            order.state = EvolutionReforgeOrderState.Ready;
            order.materialTransferOutcomePublished = true;
        }
        if (!dispositions.Acknowledge(
                order.materialTransferCommitId,
                out failureReason))
        {
            return false;
        }

        ClearPending(order);
        return true;
    }

    public static string CreateRequestFingerprint(
        string reasonCode,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{reasonCode}:"
        + string.Join(",", (inputs
                ?? Array.Empty<EquipmentEvolutionMaterialTransferInput>())
            .Where(input => input != null)
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => $"{input.sourceStackId}={input.quantity}"));

    public static bool TryValidateInputs(
        IReadOnlyDictionary<string, int> requirements,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (requirements == null
            || requirements.Count == 0
            || inputs == null
            || inputs.Count == 0)
        {
            failureReason = "equipment-evolution-material-inputs-missing";
            return false;
        }

        EquipmentEvolutionMaterialTransferInput[] canonical = inputs
            .Where(input => input != null)
            .ToArray();
        if (canonical.Length != inputs.Count
            || canonical.Any(input =>
                string.IsNullOrWhiteSpace(input.itemId)
                || !string.Equals(
                    input.itemId,
                    input.itemId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(input.sourceStackId)
                || !string.Equals(
                    input.sourceStackId,
                    input.sourceStackId.Trim(),
                    StringComparison.Ordinal)
                || input.quantity <= 0)
            || canonical.Select(input => input.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() != canonical.Length
            || !canonical.Select(input => input.sourceStackId)
                .SequenceEqual(
                    canonical.Select(input => input.sourceStackId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            failureReason = "equipment-evolution-material-inputs-invalid";
            return false;
        }

        Dictionary<string, int> actual = canonical
            .GroupBy(input => input.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(input => input.quantity)),
                StringComparer.Ordinal);
        if (requirements.Count != actual.Count
            || requirements.Any(requirement =>
                !actual.TryGetValue(requirement.Key, out int quantity)
                || quantity != requirement.Value))
        {
            failureReason =
                "equipment-evolution-material-requirements-mismatch";
            return false;
        }

        return true;
    }

    private static bool TryCommitCore(
        string orderId,
        string destinationId,
        string equipmentInstanceId,
        string equipmentSourceStackId,
        IReadOnlyDictionary<string, int> requirements,
        string operationId,
        string reasonCode,
        string storedOperationId,
        string storedCommitId,
        string storedRequestFingerprint,
        long storedMassGrams,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> storedInputs,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IPhysicalItemBatchDispositionService dispositions,
        out PhysicalItemBatchDispositionReceipt receipt,
        out List<EquipmentEvolutionMaterialTransferInput> inputs,
        out string requestFingerprint,
        out bool startingNewTransfer,
        out string failureReason)
    {
        receipt = default;
        inputs = null;
        requestFingerprint = string.Empty;
        failureReason = string.Empty;
        startingNewTransfer = string.IsNullOrEmpty(storedOperationId);
        if (string.IsNullOrWhiteSpace(orderId)
            || string.IsNullOrWhiteSpace(destinationId)
            || string.IsNullOrWhiteSpace(equipmentInstanceId))
        {
            failureReason = "equipment-evolution-material-owner-invalid";
            return false;
        }

        if (startingNewTransfer)
        {
            if (!TrySelectInputs(
                    destinationId,
                    equipmentSourceStackId,
                    requirements,
                    stacks,
                    out inputs,
                    out failureReason))
            {
                return false;
            }
        }
        else
        {
            if (!string.Equals(
                    storedOperationId,
                    operationId,
                    StringComparison.Ordinal)
                || !TryValidateInputs(
                    requirements,
                    storedInputs,
                    out failureReason))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "equipment-evolution-material-owner-mismatch"
                    : failureReason;
                return false;
            }
            inputs = storedInputs.Select(input => input.Clone()).ToList();
            if (!dispositions.TryGetPending(operationId, out _))
            {
                failureReason =
                    "equipment-evolution-material-receipt-missing";
                return false;
            }
        }

        requestFingerprint = CreateRequestFingerprint(reasonCode, inputs);
        PhysicalItemTransformInput[] physicalInputs = inputs
            .Select(input => new PhysicalItemTransformInput(
                input.sourceStackId,
                input.quantity))
            .ToArray();
        if (!dispositions.TryCommitPending(
                physicalInputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                reasonCode,
                out receipt,
                out failureReason))
        {
            return false;
        }

        if (!startingNewTransfer
            && (!string.Equals(
                    storedCommitId,
                    receipt.CommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    storedRequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal)
                || storedMassGrams != receipt.InputMassGrams))
        {
            failureReason =
                "equipment-evolution-material-provenance-mismatch";
            return false;
        }

        return true;
    }

    private static bool ValidateReceipt(
        PhysicalItemBatchDispositionReceipt receipt,
        string operationId,
        string reasonCode,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        out string failureReason)
    {
        string[] sourceStackIds = inputs
            .Select(input => input.sourceStackId)
            .ToArray();
        int quantity = checked(inputs.Sum(input => input.quantity));
        if (receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                reasonCode,
                StringComparison.Ordinal)
            || receipt.Quantity != quantity
            || receipt.InputMassGrams <= 0
            || !receipt.SourceStackIds.SequenceEqual(
                sourceStackIds,
                StringComparer.Ordinal))
        {
            failureReason =
                "equipment-evolution-material-receipt-mismatch";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static bool TrySelectInputs(
        string destinationId,
        string equipmentSourceStackId,
        IReadOnlyDictionary<string, int> requirements,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out List<EquipmentEvolutionMaterialTransferInput> inputs,
        out string failureReason)
    {
        List<EquipmentEvolutionMaterialTransferInput> selected = new();
        foreach (KeyValuePair<string, int> requirement in requirements
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int remaining = requirement.Value;
            foreach (WorldItemStackSnapshot stack in stacks
                         .Where(stack => stack != null
                             && !string.Equals(
                                 stack.StackId,
                                 equipmentSourceStackId,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 stack.ItemId,
                                 requirement.Key,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 stack.DestinationId,
                                 destinationId,
                                 StringComparison.Ordinal)
                             && stack.State ==
                                 WorldItemStackState.FacilityBuffer
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
                selected.Add(new EquipmentEvolutionMaterialTransferInput
                {
                    itemId = requirement.Key,
                    sourceStackId = stack.StackId,
                    quantity = quantity
                });
                remaining -= quantity;
            }

            if (remaining > 0)
            {
                inputs = null;
                failureReason =
                    "equipment-evolution-material-missing:"
                    + requirement.Key;
                return false;
            }
        }

        inputs = selected
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .ToList();
        return TryValidateInputs(requirements, inputs, out failureReason);
    }

    private static void PublishOwner(
        EvolutionReforgeOrder order,
        PhysicalItemBatchDispositionReceipt receipt,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        string requestFingerprint)
    {
        order.materialTransferOperationId = receipt.OperationId;
        order.materialTransferCommitId = receipt.CommitId;
        order.materialTransferRequestFingerprint = requestFingerprint;
        order.materialTransferMassGrams = receipt.InputMassGrams;
        order.materialTransferInputs = inputs
            .Select(input => input.Clone())
            .ToList();
    }

    private static void PublishOwner(
        EquipmentReattunementOrder order,
        PhysicalItemBatchDispositionReceipt receipt,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        string requestFingerprint)
    {
        order.materialTransferOperationId = receipt.OperationId;
        order.materialTransferCommitId = receipt.CommitId;
        order.materialTransferRequestFingerprint = requestFingerprint;
        order.materialTransferMassGrams = receipt.InputMassGrams;
        order.materialTransferInputs = inputs
            .Select(input => input.Clone())
            .ToList();
    }

    private static void ClearPending(EvolutionReforgeOrder order)
    {
        order.materialTransferOperationId = string.Empty;
        order.materialTransferCommitId = string.Empty;
        order.materialTransferRequestFingerprint = string.Empty;
        order.materialTransferMassGrams = 0L;
        order.materialTransferOutcomePublished = false;
        order.materialTransferInputs =
            new List<EquipmentEvolutionMaterialTransferInput>();
    }

    private static void ClearPending(EquipmentReattunementOrder order)
    {
        order.materialTransferOperationId = string.Empty;
        order.materialTransferCommitId = string.Empty;
        order.materialTransferRequestFingerprint = string.Empty;
        order.materialTransferMassGrams = 0L;
        order.materialTransferOutcomePublished = false;
        order.materialTransferInputs =
            new List<EquipmentEvolutionMaterialTransferInput>();
    }
}
