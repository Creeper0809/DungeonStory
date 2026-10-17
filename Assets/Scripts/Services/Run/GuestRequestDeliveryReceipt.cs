using System;
using System.Collections.Generic;
using System.Linq;


public readonly struct GuestRequestDeliveryReceipt
{
    public GuestRequestDeliveryReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = $"physical-batch-disposition:{(int)kind}:{OperationId}:"
            + $"{quantity}:{inputMassGrams}";
    }

    public static GuestRequestDeliveryReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) =>
        new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams);

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind == PhysicalItemDispositionKind.Transfer
        && EconomyProjectInputOwnerAuthority.IsCanonical(OperationId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(ReasonCode)
        && EconomyProjectInputOwnerAuthority.IsCanonical(RequestFingerprint)
        && SourceStackIds.Count > 0
        && SourceStackIds.All(EconomyProjectInputOwnerAuthority.IsCanonical)
        && SourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == SourceStackIds.Count
        && Quantity > 0
        && InputMassGrams > 0L;
}
