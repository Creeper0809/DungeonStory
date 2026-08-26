using System;
using System.Collections.Generic;
using System.Linq;

public interface IPhysicalFacilityItemSinkGateway
{
    bool TryCommitSinkPending(
        string destinationId,
        string itemId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);

    bool Acknowledge(string commitId, out string failureReason);
}

public interface IPhysicalFacilityItemBatchSinkGateway
{
    bool TryCommitSinkPending(
        string destinationId,
        IReadOnlyDictionary<string, int> itemQuantities,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);

    bool Acknowledge(string commitId, out string failureReason);
}

public interface IPhysicalFacilityItemBatchTransferGateway
{
    bool TryCommitTransferPending(
        string destinationId,
        IReadOnlyDictionary<string, int> itemQuantities,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);

    bool Acknowledge(string commitId, out string failureReason);
}

/// <summary>
/// Selects exact, unreserved facility-buffer lots in stable stack order and
/// delegates their terminal Sink or external custody Transfer to the physical
/// batch-disposition authority.
/// Domain services own intent/outcome persistence; this gateway owns neither.
/// </summary>
public sealed class PhysicalFacilityItemSinkGateway :
    IPhysicalFacilityItemSinkGateway,
    IPhysicalFacilityItemBatchSinkGateway,
    IPhysicalFacilityItemBatchTransferGateway
{
    private readonly IStockQuery stock;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;

    public PhysicalFacilityItemSinkGateway(
        IStockQuery stock,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    public bool TryCommitSinkPending(
        string destinationId,
        string itemId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitSinkPending(
            destinationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [itemId ?? string.Empty] = quantity
            },
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    public bool TryCommitSinkPending(
        string destinationId,
        IReadOnlyDictionary<string, int> itemQuantities,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitPending(
            destinationId,
            itemQuantities,
            PhysicalItemDispositionKind.Sink,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    public bool TryCommitTransferPending(
        string destinationId,
        IReadOnlyDictionary<string, int> itemQuantities,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        return TryCommitPending(
            destinationId,
            itemQuantities,
            PhysicalItemDispositionKind.Transfer,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    private bool TryCommitPending(
        string destinationId,
        IReadOnlyDictionary<string, int> itemQuantities,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        List<PhysicalItemTransformInput> combined = new();
        KeyValuePair<string, int>[] requested = (itemQuantities
                ?? new Dictionary<string, int>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (requested.Length == 0
            || requested.Any(pair => pair.Value <= 0
                || string.IsNullOrEmpty(pair.Key)
                || !string.Equals(
                    pair.Key,
                    pair.Key.Trim(),
                    StringComparison.Ordinal)))
        {
            failureReason = "facility batch sink request invalid";
            return false;
        }

        foreach (KeyValuePair<string, int> pair in requested)
        {
            if (!TrySelectFacilityInputs(
                    destinationId,
                    pair.Key,
                    pair.Value,
                    out IReadOnlyList<PhysicalItemTransformInput> inputs,
                    out failureReason))
            {
                return false;
            }
            combined.AddRange(inputs);
        }

        return batchDispositions.TryCommitPending(
            combined,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    public bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt) =>
        batchDispositions.TryGetPending(operationId, out receipt);

    public bool Acknowledge(string commitId, out string failureReason) =>
        batchDispositions.Acknowledge(commitId, out failureReason);

    private bool TrySelectFacilityInputs(
        string destinationId,
        string itemId,
        int quantity,
        out IReadOnlyList<PhysicalItemTransformInput> inputs,
        out string failureReason)
    {
        List<PhysicalItemTransformInput> selected = new();
        int remaining = quantity;
        foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                     .Where(value => value != null
                         && value.State == WorldItemStackState.FacilityBuffer
                         && value.ReservedQuantity == 0
                         && string.IsNullOrEmpty(value.ReservedByPersistentId)
                         && string.Equals(
                             value.DestinationId,
                             destinationId,
                             StringComparison.Ordinal)
                         && string.Equals(
                             value.ItemId,
                             itemId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
            {
                break;
            }
            int take = Math.Min(remaining, stack.AvailableQuantity);
            if (take <= 0)
            {
                continue;
            }
            selected.Add(new PhysicalItemTransformInput(stack.StackId, take));
            remaining -= take;
        }
        inputs = selected;
        failureReason = remaining == 0
            ? string.Empty
            : $"facility item missing: {itemId}";
        return remaining == 0;
    }
}
