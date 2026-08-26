using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class RegionalSupplyContractDeliveryOutbox
{
    public const string TransferReason = "regional-supply-export";

    public static string FormatOperationId(string contractId) =>
        $"regional-supply-transfer:{contractId}";

    public static bool HasPending(RegionalSupplyContractState contract) =>
        contract != null
        && contract.deliveryCommitPhase != RegionalSupplyDeliveryCommitPhase.None;

    public static void RecordPending(
        RegionalSupplyContractState contract,
        RegionalSupplyDeliveryTransferReceipt receipt)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }
        if (!ReceiptMatchesIdentity(contract, receipt)
            || receipt.sourceStackIds == null
            || receipt.sourceStackIds.Count == 0
            || receipt.sourceStackIds.Any(value => !IsCanonicalRequired(value))
            || receipt.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != receipt.sourceStackIds.Count
            || receipt.quantity <= 0
            || receipt.inputMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Regional-supply delivery transfer receipt is not canonical.");
        }

        contract.deliveryCommitPhase =
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted;
        contract.deliveryOperationId = receipt.operationId;
        contract.deliveryCommitId = receipt.commitId;
        contract.deliverySourceStackIds = receipt.sourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        contract.deliveryQuantity = receipt.quantity;
        contract.deliveryMassGrams = receipt.inputMassGrams;
    }

    public static bool TryFinalizePending(
        RegionalSupplyContractState contract,
        IRegionalSupplyContractCommandPort commands,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasCanonicalPending(contract))
        {
            failureReason = "regional-supply-outbox-invalid";
            return false;
        }
        if (!commands.TryGetPendingDeliveryTransfer(
                contract.deliveryOperationId,
                out RegionalSupplyDeliveryTransferReceipt receipt)
            || !ReceiptMatchesSaved(contract, receipt))
        {
            failureReason = "regional-supply-outbox-receipt-missing-or-mismatch";
            return false;
        }

        if (contract.deliveryCommitPhase ==
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted)
        {
            if (!commands.TryAddContractIncome(
                    contract.rewardGold,
                    contract.deliveryOperationId,
                    out failureReason))
            {
                return false;
            }
            contract.deliveryCommitPhase =
                RegionalSupplyDeliveryCommitPhase.RewardPublished;
            contract.status = RegionalSupplyContractStatus.Completed;
            contract.lastStatus =
                $"납품 완료 · {contract.rewardGold} 골드 획득";
        }
        else if (contract.status != RegionalSupplyContractStatus.Completed)
        {
            failureReason = "regional-supply-outbox-terminal-status-mismatch";
            return false;
        }

        if (!commands.AcknowledgeDeliveryTransfer(
                contract.deliveryCommitId,
                out failureReason))
        {
            return false;
        }

        Clear(contract);
        return true;
    }

    public static bool HasCanonicalPending(
        RegionalSupplyContractState contract) =>
        HasPending(contract)
        && contract.deliveryCommitPhase is
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted
            or RegionalSupplyDeliveryCommitPhase.RewardPublished
        && IsCanonicalRequired(contract.deliveryOperationId)
        && string.Equals(
            contract.deliveryOperationId,
            FormatOperationId(contract.contractId),
            StringComparison.Ordinal)
        && IsCanonicalRequired(contract.deliveryCommitId)
        && contract.deliverySourceStackIds != null
        && contract.deliverySourceStackIds.Count > 0
        && contract.deliverySourceStackIds.All(IsCanonicalRequired)
        && contract.deliverySourceStackIds
            .SequenceEqual(
                contract.deliverySourceStackIds
                    .OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
        && contract.deliverySourceStackIds
            .Distinct(StringComparer.Ordinal).Count()
            == contract.deliverySourceStackIds.Count
        && contract.deliveryQuantity > 0
        && contract.deliveryMassGrams > 0L
        && string.Equals(
            contract.deliveryCommitId,
            FormatCommitId(contract),
            StringComparison.Ordinal)
        && (contract.deliveryCommitPhase ==
                RegionalSupplyDeliveryCommitPhase.PhysicalCommitted
            ? contract.status is RegionalSupplyContractStatus.Accepted
                or RegionalSupplyContractStatus.Delivering
            : contract.status == RegionalSupplyContractStatus.Completed);

    public static bool HasCanonicalEmpty(RegionalSupplyContractState contract) =>
        contract != null
        && contract.deliveryCommitPhase == RegionalSupplyDeliveryCommitPhase.None
        && string.IsNullOrEmpty(contract.deliveryOperationId)
        && string.IsNullOrEmpty(contract.deliveryCommitId)
        && (contract.deliverySourceStackIds?.Count ?? 0) == 0
        && contract.deliveryQuantity == 0
        && contract.deliveryMassGrams == 0L;

    private static void Clear(RegionalSupplyContractState contract)
    {
        contract.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
        contract.deliveryOperationId = string.Empty;
        contract.deliveryCommitId = string.Empty;
        contract.deliverySourceStackIds.Clear();
        contract.deliveryQuantity = 0;
        contract.deliveryMassGrams = 0L;
    }

    private static bool ReceiptMatchesSaved(
        RegionalSupplyContractState contract,
        RegionalSupplyDeliveryTransferReceipt receipt) =>
        ReceiptMatchesIdentity(contract, receipt)
        && string.Equals(
            receipt.commitId,
            contract.deliveryCommitId,
            StringComparison.Ordinal)
        && receipt.quantity == contract.deliveryQuantity
        && receipt.inputMassGrams == contract.deliveryMassGrams
        && (receipt.sourceStackIds ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                contract.deliverySourceStackIds,
                StringComparer.Ordinal);

    private static bool ReceiptMatchesIdentity(
        RegionalSupplyContractState contract,
        RegionalSupplyDeliveryTransferReceipt receipt) =>
        receipt != null
        && string.Equals(
            receipt.operationId,
            FormatOperationId(contract.contractId),
            StringComparison.Ordinal)
        && string.Equals(
            receipt.reasonCode,
            TransferReason,
            StringComparison.Ordinal);

    private static string FormatCommitId(
        RegionalSupplyContractState contract) =>
        $"physical-batch-disposition:1:{contract.deliveryOperationId}:"
        + $"{contract.deliveryQuantity}:{contract.deliveryMassGrams}";

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
