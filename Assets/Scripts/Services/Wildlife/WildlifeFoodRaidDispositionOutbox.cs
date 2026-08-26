using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WildlifeFoodRaidDispositionOutbox
{
    private const string ReasonCode = "wildlife-food-raid-consumed";
    private readonly IPhysicalItemBatchDispositionService dispositions;

    public WildlifeFoodRaidDispositionOutbox(
        IPhysicalItemBatchDispositionService dispositions) =>
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));

    public bool TryCommit(
        WildlifeFoodRaidOrderSaveData order,
        WorldItemStackSnapshot target,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || target == null || target.AvailableQuantity < 1)
        {
            failureReason = "wildlife-food-raid-target-unavailable";
            return false;
        }
        if (order.commitPhase != WildlifeFoodRaidCommitPhase.None)
        {
            return TryResume(order, out failureReason);
        }

        string operationId = OperationId(order);
        if (!dispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(target.StackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }

        order.commitPhase = WildlifeFoodRaidCommitPhase.ItemCommitted;
        order.state = WildlifeFoodRaidOrderState.WaitingForDispositionFinalization;
        order.dispositionOperationId = operationId;
        order.dispositionReasonCode = ReasonCode;
        order.dispositionCommitId = receipt.CommitId;
        order.dispositionSourceStackIds = receipt.SourceStackIds.ToList();
        order.dispositionQuantity = receipt.Quantity;
        order.dispositionInputMassGrams = receipt.InputMassGrams;
        order.dispositionItemId = target.ItemId;
        return TryResume(order, out failureReason);
    }

    public bool TryResume(
        WildlifeFoodRaidOrderSaveData order,
        out string failureReason)
    {
        if (!TryValidatePending(order, out _, out failureReason))
        {
            return false;
        }
        if (order.commitPhase == WildlifeFoodRaidCommitPhase.ItemCommitted)
        {
            order.stolenQuantity = order.dispositionQuantity;
            order.outcomeReason = order.stolenQuantity > 0
                ? "늑대가 식량에 도달해 1개를 훔쳤습니다."
                : "식량이 먼저 사라져 아무것도 훔치지 못했습니다.";
            order.commitPhase = WildlifeFoodRaidCommitPhase.RaidPublished;
        }
        if (!dispositions.Acknowledge(
                order.dispositionCommitId,
                out failureReason))
        {
            return false;
        }

        ClearPending(order);
        order.state = WildlifeFoodRaidOrderState.Leaving;
        return true;
    }

    public bool TryValidatePending(
        WildlifeFoodRaidOrderSaveData order,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!HasValidPendingShape(order))
        {
            failureReason = "wildlife-food-raid-pending-shape-invalid";
            return false;
        }
        if (!dispositions.TryGetPending(
                order.dispositionOperationId,
                out receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                order.dispositionOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                order.dispositionReasonCode,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.CommitId,
                order.dispositionCommitId,
                StringComparison.Ordinal)
            || receipt.Quantity != order.dispositionQuantity
            || receipt.InputMassGrams != order.dispositionInputMassGrams
            || !receipt.SourceStackIds.SequenceEqual(
                order.dispositionSourceStackIds,
                StringComparer.Ordinal))
        {
            failureReason = "wildlife-food-raid-pending-receipt-mismatch";
            return false;
        }
        return true;
    }

    public static bool HasValidShape(WildlifeFoodRaidOrderSaveData order)
    {
        if (order == null
            || !Enum.IsDefined(typeof(WildlifeFoodRaidCommitPhase), order.commitPhase))
        {
            return false;
        }
        if (order.commitPhase == WildlifeFoodRaidCommitPhase.None)
        {
            return string.IsNullOrEmpty(order.dispositionOperationId)
                && string.IsNullOrEmpty(order.dispositionReasonCode)
                && string.IsNullOrEmpty(order.dispositionCommitId)
                && (order.dispositionSourceStackIds?.Count ?? 0) == 0
                && order.dispositionQuantity == 0
                && order.dispositionInputMassGrams == 0L
                && string.IsNullOrEmpty(order.dispositionItemId);
        }
        return HasValidPendingShape(order);
    }

    private static bool HasValidPendingShape(WildlifeFoodRaidOrderSaveData order) =>
        order != null
        && order.commitPhase is WildlifeFoodRaidCommitPhase.ItemCommitted
            or WildlifeFoodRaidCommitPhase.RaidPublished
        && order.state == WildlifeFoodRaidOrderState.WaitingForDispositionFinalization
        && string.Equals(order.dispositionOperationId, OperationId(order), StringComparison.Ordinal)
        && string.Equals(order.dispositionReasonCode, ReasonCode, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(order.dispositionCommitId)
        && order.dispositionSourceStackIds?.Count == 1
        && order.dispositionSourceStackIds.All(value => !string.IsNullOrWhiteSpace(value))
        && order.dispositionQuantity == 1
        && order.dispositionInputMassGrams > 0L
        && !string.IsNullOrWhiteSpace(order.dispositionItemId)
        && (order.commitPhase != WildlifeFoodRaidCommitPhase.ItemCommitted
            || order.stolenQuantity == 0)
        && (order.commitPhase != WildlifeFoodRaidCommitPhase.RaidPublished
            || order.stolenQuantity == order.dispositionQuantity);

    private static string OperationId(WildlifeFoodRaidOrderSaveData order) =>
        $"wildlife-food-raid:{order?.raidId}:{order?.wildlifeId}";

    private static void ClearPending(WildlifeFoodRaidOrderSaveData order)
    {
        order.commitPhase = WildlifeFoodRaidCommitPhase.None;
        order.dispositionOperationId = string.Empty;
        order.dispositionReasonCode = string.Empty;
        order.dispositionCommitId = string.Empty;
        order.dispositionSourceStackIds = new List<string>();
        order.dispositionQuantity = 0;
        order.dispositionInputMassGrams = 0L;
        order.dispositionItemId = string.Empty;
    }
}
