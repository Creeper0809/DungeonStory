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
        ResourceStockPolicyPendingSale pending,
        out string failureReason);

    bool TryPublishSaleFact(
        ResourceStockPolicyPendingSale pending,
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
    public const string DestinationPrefix =
        "facility-input:exact:economy.stock-policy:";

    public static string FormatOperationId(string itemId, int sequence) =>
        $"{OperationPrefix}{sequence:D8}:{itemId}";

    public static string FormatDestinationId(string itemId) =>
        EconomyProjectInputOwnerAuthority.BuildStockPolicyDestinationId(itemId);

    public static ResourceStockPolicyPendingSale CreatePending(
        int sequence,
        string itemId,
        int proceeds,
        int unitPrice,
        int saleRateBasisPoints,
        int absoluteDay,
        string outcomeResultKey,
        ResourceStockPolicySaleTransferReceipt receipt)
    {
        ResourceStockPolicyPendingSale pending = new()
        {
            sequence = sequence,
            itemId = itemId,
            destinationId = FormatDestinationId(itemId),
            quantity = receipt?.quantity ?? 0,
            proceeds = proceeds,
            unitPrice = unitPrice,
            saleRateBasisPoints = saleRateBasisPoints,
            absoluteDay = absoluteDay,
            phase = ResourceStockPolicySaleCommitPhase.PhysicalCommitted,
            operationId = receipt?.operationId ?? string.Empty,
            reasonCode = receipt?.reasonCode ?? string.Empty,
            commitId = receipt?.commitId ?? string.Empty,
            sourceStackIds = (receipt?.sourceStackIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            inputMassGrams = receipt?.inputMassGrams ?? 0L,
            physicalOutcomeOwnerRevision =
                receipt?.physicalOutcomeOwnerRevision ?? 0L,
            outcomeResultKey = outcomeResultKey?.Trim() ?? string.Empty
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
                    pending,
                    out failureReason))
            {
                return false;
            }
            pending.phase = ResourceStockPolicySaleCommitPhase.IncomePublished;
        }

        if (pending.phase == ResourceStockPolicySaleCommitPhase.IncomePublished)
        {
            if (!commands.TryPublishSaleFact(pending, out failureReason))
                return false;
            pending.phase = ResourceStockPolicySaleCommitPhase.LedgerCommitted;
        }

        if (pending.phase == ResourceStockPolicySaleCommitPhase.LedgerCommitted)
        {
            if (!commands.AcknowledgeSaleTransfer(
                    pending.commitId,
                    out failureReason))
            {
                return false;
            }
            pending.phase = ResourceStockPolicySaleCommitPhase.Acknowledged;
        }
        return pending.phase == ResourceStockPolicySaleCommitPhase.Acknowledged;
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
        && pending.unitPrice > 0
        && pending.saleRateBasisPoints > 0
        && pending.saleRateBasisPoints <= 10000
        && pending.absoluteDay >= 0
        && pending.phase is
            ResourceStockPolicySaleCommitPhase.PhysicalCommitted
            or ResourceStockPolicySaleCommitPhase.IncomePublished
            or ResourceStockPolicySaleCommitPhase.LedgerCommitted
            or ResourceStockPolicySaleCommitPhase.Acknowledged
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
        && pending.physicalOutcomeOwnerRevision > 0L
        && IsCanonicalRequired(pending.outcomeResultKey)
        && string.Equals(
            pending.commitId,
            FormatCommitId(pending),
            StringComparison.Ordinal)
        && HasCanonicalPhaseState(pending);

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
        && receipt.physicalOutcomeOwnerRevision
            == pending.physicalOutcomeOwnerRevision
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

    private static bool HasCanonicalPhaseState(
        ResourceStockPolicyPendingSale pending)
    {
        bool hasPayment = IsCanonicalRequired(pending.economyTransactionId)
            && pending.balanceBefore >= 0
            && pending.balanceAfter == pending.balanceBefore + pending.proceeds;
        bool hasOutcome = IsCanonicalRequired(pending.outcomeId)
            && IsLowerSha256(pending.outcomeDigest);
        return pending.phase switch
        {
            ResourceStockPolicySaleCommitPhase.PhysicalCommitted =>
                !hasPayment
                && string.IsNullOrEmpty(pending.economyTransactionId)
                && pending.balanceBefore == 0
                && pending.balanceAfter == 0
                && !hasOutcome
                && string.IsNullOrEmpty(pending.outcomeId)
                && string.IsNullOrEmpty(pending.outcomeDigest),
            ResourceStockPolicySaleCommitPhase.IncomePublished =>
                hasPayment
                && !hasOutcome
                && string.IsNullOrEmpty(pending.outcomeId)
                && string.IsNullOrEmpty(pending.outcomeDigest),
            ResourceStockPolicySaleCommitPhase.LedgerCommitted
                or ResourceStockPolicySaleCommitPhase.Acknowledged =>
                hasPayment && hasOutcome,
            _ => false
        };
    }

    private static bool IsLowerSha256(string value)
    {
        if (value?.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9')
                && character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
