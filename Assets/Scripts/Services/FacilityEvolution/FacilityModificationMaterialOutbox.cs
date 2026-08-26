using System;
using System.Collections.Generic;
using System.Linq;

public static class FacilityModificationMaterialOutbox
{
    public const string ReasonCode =
        "facility-modification-materials-to-wip";
    private const string OperationPrefix = "facility-modification-material:";

    public static string FormatOperationId(string orderId) =>
        OperationPrefix + orderId;

    public static bool TryCommitOrFinalize(
        FacilityModificationOrder order,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService service,
        out string failureReason)
    {
        if (items == null)
        {
            failureReason = "facility-modification-outbox-invalid";
            return false;
        }

        return TryCommitOrFinalize(
            order,
            items.GetAllStacks(),
            service,
            out failureReason);
    }

    public static bool TryCommitOrFinalize(
        FacilityModificationOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IPhysicalItemBatchDispositionService service,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || stacks == null || service == null)
        {
            failureReason = "facility-modification-outbox-invalid";
            return false;
        }
        if (order.materialsConsumed
            && string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            return true;
        }

        string operationId = FormatOperationId(order.orderId);
        IReadOnlyList<FacilityModificationMaterialTransferInput> slices;
        bool startingNewTransfer = string.IsNullOrEmpty(
            order.materialTransferOperationId);
        if (startingNewTransfer)
        {
            if (!TrySelectInputs(order, stacks, out slices, out failureReason))
            {
                return false;
            }
        }
        else
        {
            slices = order.materialTransferInputs;
            if (!TryValidateInputs(order, slices, out failureReason))
            {
                return false;
            }
            if (!service.TryGetPending(operationId, out _))
            {
                failureReason =
                    "facility-modification-material-receipt-missing";
                return false;
            }
        }

        PhysicalItemTransformInput[] physicalInputs = slices
            .OrderBy(slice => slice.sourceStackId, StringComparer.Ordinal)
            .Select(slice => new PhysicalItemTransformInput(
                slice.sourceStackId,
                slice.quantity))
            .ToArray();
        if (!service.TryCommitPending(
                physicalInputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }

        string requestFingerprint = CreateRequestFingerprint(slices);
        string[] expectedSourceIds = slices
            .Select(slice => slice.sourceStackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        int expectedQuantity = checked(slices.Sum(slice => slice.quantity));
        if (startingNewTransfer)
        {
            // Publish the owner provenance immediately after the physical
            // commit. Even an impossible downstream contract mismatch must
            // leave a durable owner for the pending debit rather than an
            // orphaned Physical Items receipt.
            order.materialTransferOperationId = receipt.OperationId;
            order.materialTransferCommitId = receipt.CommitId;
            order.materialTransferRequestFingerprint = requestFingerprint;
            order.materialTransferMassGrams = receipt.InputMassGrams;
            order.materialTransferInputs = slices
                .Select(slice => slice.Clone())
                .ToList();
        }
        if (receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            || receipt.Quantity != expectedQuantity
            || receipt.InputMassGrams <= 0
            || !receipt.SourceStackIds.SequenceEqual(
                expectedSourceIds,
                StringComparer.Ordinal))
        {
            failureReason =
                "facility-modification-material-receipt-mismatch";
            return false;
        }

        if (!startingNewTransfer && (!string.Equals(
                order.materialTransferCommitId,
                receipt.CommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                order.materialTransferRequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal)
            || order.materialTransferMassGrams != receipt.InputMassGrams))
        {
            failureReason =
                "facility-modification-material-provenance-mismatch";
            return false;
        }

        if (!order.materialTransferOutcomePublished)
        {
            order.materialsConsumed = true;
            order.state = EvolutionReforgeOrderState.Ready;
            order.materialTransferOutcomePublished = true;
        }
        if (!service.Acknowledge(
                order.materialTransferCommitId,
                out failureReason))
        {
            return false;
        }

        ClearPending(order);
        return true;
    }

    public static bool TryValidateInputs(
        FacilityModificationOrder order,
        IReadOnlyList<FacilityModificationMaterialTransferInput> slices,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || slices == null || slices.Count == 0)
        {
            failureReason = "facility-modification-material-inputs-missing";
            return false;
        }

        FacilityModificationMaterialTransferInput[] canonical = slices
            .Where(slice => slice != null)
            .ToArray();
        if (canonical.Length != slices.Count
            || canonical.Any(slice =>
                string.IsNullOrWhiteSpace(slice.itemId)
                || !string.Equals(
                    slice.itemId,
                    slice.itemId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(slice.sourceStackId)
                || !string.Equals(
                    slice.sourceStackId,
                    slice.sourceStackId.Trim(),
                    StringComparison.Ordinal)
                || slice.quantity <= 0)
            || canonical.Select(slice => slice.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() != canonical.Length
            || !canonical.Select(slice => slice.sourceStackId)
                .SequenceEqual(
                    canonical.Select(slice => slice.sourceStackId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            failureReason = "facility-modification-material-inputs-invalid";
            return false;
        }

        Dictionary<string, int> requirements =
            FacilityEvolutionRules.BuildRequirements(order);
        Dictionary<string, int> actual = canonical
            .GroupBy(slice => slice.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(slice => slice.quantity)),
                StringComparer.Ordinal);
        if (requirements.Count != actual.Count
            || requirements.Any(requirement =>
                !actual.TryGetValue(requirement.Key, out int quantity)
                || quantity != requirement.Value))
        {
            failureReason =
                "facility-modification-material-requirements-mismatch";
            return false;
        }

        return true;
    }

    public static string CreateRequestFingerprint(
        IReadOnlyList<FacilityModificationMaterialTransferInput> slices) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{ReasonCode}:"
        + string.Join(",", (slices
                ?? Array.Empty<FacilityModificationMaterialTransferInput>())
            .Where(slice => slice != null)
            .OrderBy(slice => slice.sourceStackId, StringComparer.Ordinal)
            .Select(slice => $"{slice.sourceStackId}={slice.quantity}"));

    private static bool TrySelectInputs(
        FacilityModificationOrder order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out IReadOnlyList<FacilityModificationMaterialTransferInput> slices,
        out string failureReason)
    {
        List<FacilityModificationMaterialTransferInput> selected = new();
        foreach (KeyValuePair<string, int> requirement in
                 FacilityEvolutionRules.BuildRequirements(order)
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int remaining = requirement.Value;
            foreach (WorldItemStackSnapshot stack in stacks
                         .Where(stack => stack != null
                             && string.Equals(
                                 stack.ItemId,
                                 requirement.Key,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 stack.DestinationId,
                                 order.destinationId,
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
                selected.Add(new FacilityModificationMaterialTransferInput
                {
                    itemId = requirement.Key,
                    sourceStackId = stack.StackId,
                    quantity = quantity
                });
                remaining -= quantity;
            }

            if (remaining > 0)
            {
                slices = Array.Empty<
                    FacilityModificationMaterialTransferInput>();
                failureReason =
                    "facility-modification-material-missing:"
                    + requirement.Key;
                return false;
            }
        }

        selected = selected
            .OrderBy(slice => slice.sourceStackId, StringComparer.Ordinal)
            .ToList();
        if (!TryValidateInputs(order, selected, out failureReason))
        {
            slices = Array.Empty<
                FacilityModificationMaterialTransferInput>();
            return false;
        }

        slices = selected;
        return true;
    }

    private static void ClearPending(FacilityModificationOrder order)
    {
        order.materialTransferOperationId = string.Empty;
        order.materialTransferCommitId = string.Empty;
        order.materialTransferRequestFingerprint = string.Empty;
        order.materialTransferMassGrams = 0L;
        order.materialTransferOutcomePublished = false;
        order.materialTransferInputs = new List<
            FacilityModificationMaterialTransferInput>();
    }
}
