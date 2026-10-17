using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Commits an already-reserved set of physical item costs as one repository
/// mutation. Every stack and quantity is validated before the first item is
/// removed, so a stale or stolen final stack cannot leave a partial payment.
/// </summary>
public interface IAtomicItemConsumptionService
{
    bool TryConsumeReserved(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out DomainFailure failure);
}

public readonly struct AtomicItemConsumptionLine
{
    public AtomicItemConsumptionLine(
        string stackId,
        string itemId,
        int requestedQuantity,
        int quantityBefore,
        int quantityAfter)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        RequestedQuantity = Mathf.Max(0, requestedQuantity);
        QuantityBefore = Mathf.Max(0, quantityBefore);
        QuantityAfter = Mathf.Max(0, quantityAfter);
    }

    public string StackId { get; }
    public string ItemId { get; }
    public int RequestedQuantity { get; }
    public int QuantityBefore { get; }
    public int QuantityAfter { get; }
}

public sealed class AtomicItemConsumptionReceipt
{
    public AtomicItemConsumptionReceipt(
        string reservationOwnerId,
        IReadOnlyList<AtomicItemConsumptionLine> lines)
    {
        ReservationOwnerId = reservationOwnerId?.Trim() ?? string.Empty;
        Lines = (lines ?? Array.Empty<AtomicItemConsumptionLine>()).ToArray();
    }

    public string ReservationOwnerId { get; }
    public IReadOnlyList<AtomicItemConsumptionLine> Lines { get; }

    public string ToCanonicalEvidence() => string.Join(
        ";",
        Lines
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value =>
                $"owner={ReservationOwnerId}|stack={value.StackId}|item={value.ItemId}" +
                $"|requested={value.RequestedQuantity}|before={value.QuantityBefore}" +
                $"|after={value.QuantityAfter}"));
}

public interface IAtomicItemConsumptionEvidenceService
{
    bool TryConsumeReservedWithEvidence(
        IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId,
        out AtomicItemConsumptionReceipt receipt,
        out DomainFailure failure);
}

public interface IIdempotentAtomicItemConsumptionService
{
    bool TryConsumeReservedOnce(IReadOnlyList<ReservedItemConsumption> consumptions,
        string reservationOwnerId, out PhysicalEffectReceiptSaveData receipt, out DomainFailure failure);
}
