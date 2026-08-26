using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IResourceStockPolicySaleCommandPort
{
    bool TryGetPendingSaleTransfer(
        string operationId,
        out ResourceStockPolicySaleTransferReceipt receipt);

    bool TryPublishSaleIncome(
        int amount,
        string operationId,
        string itemId,
        out string failureReason);

    bool AcknowledgeSaleTransfer(
        string commitId,
        out string failureReason);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ResourceStockPolicySaleOutbox
{
    public const string OperationPrefix = "stock-policy-sale:";
    public const string TransferReason = "stock-policy-market-export";
    public const string DestinationPrefix = "stock-policy:sell:";

    public static string FormatOperationId(string itemId, int sequence) =>
        $"{OperationPrefix}{sequence:D8}:{itemId}";

    public static string FormatDestinationId(string itemId) =>
        DestinationPrefix + itemId;

    public static ResourceStockPolicyPendingSale CreatePending(
        int sequence,
        string itemId,
        int proceeds,
        ResourceStockPolicySaleTransferReceipt receipt)
    {
        ResourceStockPolicyPendingSale pending = new()
        {
            sequence = sequence,
            itemId = itemId,
            destinationId = FormatDestinationId(itemId),
            quantity = receipt?.quantity ?? 0,
            proceeds = proceeds,
            phase = ResourceStockPolicySaleCommitPhase.PhysicalCommitted,
            operationId = receipt?.operationId ?? string.Empty,
            reasonCode = receipt?.reasonCode ?? string.Empty,
            commitId = receipt?.commitId ?? string.Empty,
            sourceStackIds = (receipt?.sourceStackIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            inputMassGrams = receipt?.inputMassGrams ?? 0L
        };
        if (!HasCanonicalPending(pending))
        {
            throw new InvalidOperationException(
                "Stock-policy sale Transfer receipt is not canonical.");
        }
        return pending;
    }

    public static bool TryFinalizePending(
        ResourceStockPolicyPendingSale pending,
        IResourceStockPolicySaleCommandPort commands,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasCanonicalPending(pending))
        {
            failureReason = "stock-policy-sale-outbox-invalid";
            return false;
        }
        if (commands == null)
        {
            failureReason = "stock-policy-sale-command-port-missing";
            return false;
        }
        if (!commands.TryGetPendingSaleTransfer(
                pending.operationId,
                out ResourceStockPolicySaleTransferReceipt receipt)
            || !ReceiptMatchesSaved(pending, receipt))
        {
            failureReason =
                "stock-policy-sale-receipt-missing-or-mismatch";
            return false;
        }

        if (pending.phase ==
            ResourceStockPolicySaleCommitPhase.PhysicalCommitted)
        {
            if (!commands.TryPublishSaleIncome(
                    pending.proceeds,
                    pending.operationId,
                    pending.itemId,
                    out failureReason))
            {
                return false;
            }
            pending.phase = ResourceStockPolicySaleCommitPhase.IncomePublished;
        }

        return commands.AcknowledgeSaleTransfer(
            pending.commitId,
            out failureReason);
    }

    public static bool HasCanonicalPending(
        ResourceStockPolicyPendingSale pending) =>
        pending != null
        && pending.sequence > 0
        && IsCanonicalRequired(pending.itemId)
        && string.Equals(
            pending.destinationId,
            FormatDestinationId(pending.itemId),
            StringComparison.Ordinal)
        && pending.quantity > 0
        && pending.proceeds > 0
        && pending.phase is
            ResourceStockPolicySaleCommitPhase.PhysicalCommitted
            or ResourceStockPolicySaleCommitPhase.IncomePublished
        && string.Equals(
            pending.operationId,
            FormatOperationId(pending.itemId, pending.sequence),
            StringComparison.Ordinal)
        && string.Equals(
            pending.reasonCode,
            TransferReason,
            StringComparison.Ordinal)
        && IsCanonicalRequired(pending.commitId)
        && pending.sourceStackIds != null
        && pending.sourceStackIds.Count > 0
        && pending.sourceStackIds.All(IsCanonicalRequired)
        && pending.sourceStackIds.SequenceEqual(
            pending.sourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal),
            StringComparer.Ordinal)
        && pending.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == pending.sourceStackIds.Count
        && pending.inputMassGrams > 0L
        && string.Equals(
            pending.commitId,
            FormatCommitId(pending),
            StringComparison.Ordinal);

    public static bool ReceiptMatchesSaved(
        ResourceStockPolicyPendingSale pending,
        ResourceStockPolicySaleTransferReceipt receipt) =>
        pending != null
        && receipt != null
        && string.Equals(
            receipt.operationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.reasonCode,
            pending.reasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.commitId,
            pending.commitId,
            StringComparison.Ordinal)
        && receipt.quantity == pending.quantity
        && receipt.inputMassGrams == pending.inputMassGrams
        && (receipt.sourceStackIds ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(pending.sourceStackIds, StringComparer.Ordinal);

    private static string FormatCommitId(
        ResourceStockPolicyPendingSale pending) =>
        $"physical-batch-disposition:1:{pending.operationId}:"
        + $"{pending.quantity}:{pending.inputMassGrams}";

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
