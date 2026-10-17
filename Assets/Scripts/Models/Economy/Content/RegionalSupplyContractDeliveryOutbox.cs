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
            throw new ArgumentNullException(nameof(contract));
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
        IRegionalSupplyContractOutcomePort outcomePort,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (outcomePort == null)
            throw new ArgumentNullException(nameof(outcomePort));
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

        RegionalSupplyContractOutcomeReservation prepared = default;
        bool hasPrepared = false;
        if (contract.deliveryCommitPhase is
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted
            or RegionalSupplyDeliveryCommitPhase.RewardPublished)
        {
            if (!outcomePort.TryReserveCompletion(
                    contract,
                    Math.Max(0, absoluteDay),
                    out prepared,
                    out failureReason))
                return false;
            hasPrepared = true;
        }

        if (contract.deliveryCommitPhase ==
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted)
        {
            if (!commands.TryAddContractIncome(
                    contract.rewardGold,
                    contract.deliveryOperationId,
                    out RegionalSupplyRewardReceipt reward,
                    out failureReason))
            {
                outcomePort.Cancel(prepared);
                return false;
            }
            if (!RewardMatches(contract, reward))
            {
                outcomePort.Cancel(prepared);
                failureReason = "regional-supply-reward-receipt-mismatch";
                return false;
            }
            contract.rewardTransactionId = reward.transactionId;
            contract.rewardBalanceBefore = reward.balanceBefore;
            contract.rewardBalanceAfter = reward.balanceAfter;
            contract.deliveryCommitPhase =
                RegionalSupplyDeliveryCommitPhase.RewardPublished;
            contract.status = RegionalSupplyContractStatus.Completed;
            contract.lastStatus =
                $"납품 완료 · {contract.rewardGold} 골드 획득";
        }

        if (contract.deliveryCommitPhase ==
            RegionalSupplyDeliveryCommitPhase.RewardPublished)
        {
            if (!outcomePort.TryCommitCompletion(
                    prepared,
                    contract,
                    out string resultKey,
                    out failureReason))
                return false;
            contract.deliveryOutcomeResultKey = resultKey;
            contract.deliveryCommitPhase =
                RegionalSupplyDeliveryCommitPhase.OutcomeCommitted;
        }
        else if (hasPrepared)
        {
            outcomePort.Cancel(prepared);
        }

        if (contract.status != RegionalSupplyContractStatus.Completed
            || contract.deliveryCommitPhase !=
                RegionalSupplyDeliveryCommitPhase.OutcomeCommitted)
        {
            failureReason = "regional-supply-outbox-terminal-status-mismatch";
            return false;
        }

        if (!commands.AcknowledgeDeliveryTransfer(
                contract.deliveryCommitId,
                out failureReason))
            return false;

        Clear(contract);
        return true;
    }

    public static bool HasCanonicalPending(
        RegionalSupplyContractState contract) =>
        HasPending(contract)
        && contract.deliveryCommitPhase is
            RegionalSupplyDeliveryCommitPhase.PhysicalCommitted
            or RegionalSupplyDeliveryCommitPhase.RewardPublished
            or RegionalSupplyDeliveryCommitPhase.OutcomeCommitted
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
            ? (contract.status is RegionalSupplyContractStatus.Accepted
                or RegionalSupplyContractStatus.Delivering)
                && HasCanonicalRewardEmpty(contract)
                && string.IsNullOrEmpty(contract.deliveryOutcomeResultKey)
            : contract.status == RegionalSupplyContractStatus.Completed
                && HasCanonicalReward(contract)
                && (contract.deliveryCommitPhase ==
                        RegionalSupplyDeliveryCommitPhase.OutcomeCommitted
                    ? IsCanonicalRequired(contract.deliveryOutcomeResultKey)
                    : string.IsNullOrEmpty(
                        contract.deliveryOutcomeResultKey)));

    public static bool HasCanonicalEmpty(RegionalSupplyContractState contract) =>
        contract != null
        && contract.deliveryCommitPhase == RegionalSupplyDeliveryCommitPhase.None
        && string.IsNullOrEmpty(contract.deliveryOperationId)
        && string.IsNullOrEmpty(contract.deliveryCommitId)
        && (contract.deliverySourceStackIds?.Count ?? 0) == 0
        && contract.deliveryQuantity == 0
        && contract.deliveryMassGrams == 0L
        && HasCanonicalRewardEmpty(contract)
        && (contract.status == RegionalSupplyContractStatus.Completed
            ? IsCanonicalRequired(contract.deliveryOutcomeResultKey)
            : string.IsNullOrEmpty(contract.deliveryOutcomeResultKey));

    private static void Clear(RegionalSupplyContractState contract)
    {
        contract.deliveryCommitPhase = RegionalSupplyDeliveryCommitPhase.None;
        contract.deliveryOperationId = string.Empty;
        contract.deliveryCommitId = string.Empty;
        contract.deliverySourceStackIds.Clear();
        contract.deliveryQuantity = 0;
        contract.deliveryMassGrams = 0L;
        contract.rewardTransactionId = string.Empty;
        contract.rewardBalanceBefore = 0;
        contract.rewardBalanceAfter = 0;
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

    private static bool RewardMatches(
        RegionalSupplyContractState contract,
        RegionalSupplyRewardReceipt reward) =>
        reward != null
        && string.Equals(
            reward.operationId,
            contract.deliveryOperationId,
            StringComparison.Ordinal)
        && IsCanonicalRequired(reward.transactionId)
        && reward.amount == contract.rewardGold
        && reward.balanceBefore >= 0
        && reward.balanceAfter - reward.balanceBefore == reward.amount;

    private static bool HasCanonicalReward(
        RegionalSupplyContractState contract) =>
        IsCanonicalRequired(contract.rewardTransactionId)
        && contract.rewardBalanceBefore >= 0
        && contract.rewardBalanceAfter - contract.rewardBalanceBefore
            == contract.rewardGold;

    private static bool HasCanonicalRewardEmpty(
        RegionalSupplyContractState contract) =>
        string.IsNullOrEmpty(contract.rewardTransactionId)
        && contract.rewardBalanceBefore == 0
        && contract.rewardBalanceAfter == 0;

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
