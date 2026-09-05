using System;
using System.Linq;

public static class CircusShowSupplyOutbox
{
    public const string OperationPrefix = "circus-show-supplies:";
    public const string ReasonCode = "circus-performance-prop-consumed";

    public static string FormatOperationId(string orderId, int sequence) =>
        $"{OperationPrefix}{orderId}:{sequence:D8}";

    public static bool HasPending(CircusShowOrder order) =>
        order != null && order.pendingSupplyPhase != CircusShowSupplyCommitPhase.None;

    public static void Record(
        CircusShowOrder order,
        int sequence,
        PhysicalItemBatchDispositionReceipt receipt,
        string cartStackId,
        float before,
        float after)
    {
        if (order == null || sequence <= 0
            || sequence != order.nextSupplyOperationSequence
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || receipt.Quantity != 1
            || receipt.InputMassGrams !=
                CircusPerformanceSupplyContracts
                    .PerformancePropBoxMassGrams
            || !string.Equals(receipt.OperationId, FormatOperationId(order.orderId, sequence), StringComparison.Ordinal)
            || !string.Equals(receipt.ReasonCode, ReasonCode, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cartStackId)
            || float.IsNaN(before) || float.IsInfinity(before)
            || float.IsNaN(after) || float.IsInfinity(after)
            || before <= after || after < 0f)
        {
            throw new InvalidOperationException("Circus show supply receipt is not canonical.");
        }
        order.pendingSupplyOperationSequence = sequence;
        order.pendingSupplyPhase = CircusShowSupplyCommitPhase.ItemCommitted;
        order.pendingSupplyOperationId = receipt.OperationId;
        order.pendingSupplyReasonCode = receipt.ReasonCode;
        order.pendingSupplyCommitId = receipt.CommitId;
        order.pendingSupplySourceStackIds = receipt.SourceStackIds.OrderBy(x => x, StringComparer.Ordinal).ToList();
        order.pendingSupplyQuantity = receipt.Quantity;
        order.pendingSupplyMassGrams = receipt.InputMassGrams;
        order.pendingSupplyCartStackId = cartStackId;
        order.pendingSupplyCartDurabilityBefore = before;
        order.pendingSupplyCartDurabilityAfter = after;
    }

    public static bool TryFinalize(
        CircusShowOrder order,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsValid(order) || items == null || dispositions == null)
        {
            failureReason = "circus-show-supply-outbox-invalid";
            return false;
        }
        bool pending = dispositions.TryGetPending(order.pendingSupplyOperationId, out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !string.Equals(receipt.CommitId, order.pendingSupplyCommitId, StringComparison.Ordinal))
        {
            failureReason = "circus-show-supply-receipt-mismatch";
            return false;
        }
        if (order.pendingSupplyPhase == CircusShowSupplyCommitPhase.ItemCommitted)
        {
            if (!pending)
            {
                failureReason = "circus-show-supply-receipt-missing";
                return false;
            }
            WorldItemStackSnapshot cart = items.GetAllStacks().FirstOrDefault(x => x != null && x.StackId == order.pendingSupplyCartStackId);
            if (cart == null
                || !string.Equals(
                    cart.ItemId,
                    DurableToolItemRules.BanquetCart,
                    StringComparison.Ordinal))
            {
                failureReason = "circus-show-supply-cart-missing";
                return false;
            }
            float current = DurableToolItemRules.ReadCurrentDurability(cart.ItemId, cart.Components);
            if (Math.Abs(current - order.pendingSupplyCartDurabilityAfter)
                >= 0.001f)
            {
                failureReason = "circus-show-supply-cart-conflict";
                return false;
            }
            order.preparationSuppliesCommitted = true;
            order.preparationSupplyCommitId = order.pendingSupplyCommitId;
            order.pendingSupplyPhase = CircusShowSupplyCommitPhase.OutcomesPublished;
        }
        if (!order.preparationSuppliesCommitted
            || !string.Equals(order.preparationSupplyCommitId, order.pendingSupplyCommitId, StringComparison.Ordinal))
        {
            failureReason = "circus-show-supply-terminal-mismatch";
            return false;
        }
        if (pending && !dispositions.Acknowledge(order.pendingSupplyCommitId, out failureReason))
        {
            return false;
        }
        order.nextSupplyOperationSequence = checked(order.pendingSupplyOperationSequence + 1);
        Clear(order);
        return true;
    }

    public static bool IsValid(CircusShowOrder order) =>
        order != null
        && order.pendingSupplyPhase is CircusShowSupplyCommitPhase.ItemCommitted or CircusShowSupplyCommitPhase.OutcomesPublished
        && order.pendingSupplyOperationSequence > 0
        && order.pendingSupplyOperationSequence == order.nextSupplyOperationSequence
        && string.Equals(order.pendingSupplyOperationId, FormatOperationId(order.orderId, order.pendingSupplyOperationSequence), StringComparison.Ordinal)
        && string.Equals(order.pendingSupplyReasonCode, ReasonCode, StringComparison.Ordinal)
        && order.pendingSupplyQuantity == 1
        && order.pendingSupplyMassGrams ==
            CircusPerformanceSupplyContracts.PerformancePropBoxMassGrams
        && order.pendingSupplySourceStackIds != null
        && order.pendingSupplySourceStackIds.Count > 0
        && !string.IsNullOrWhiteSpace(order.pendingSupplyCartStackId)
        && order.pendingSupplyCartDurabilityBefore > order.pendingSupplyCartDurabilityAfter
        && Math.Abs(
            order.pendingSupplyCartDurabilityBefore
            - order.pendingSupplyCartDurabilityAfter
            - CircusPerformanceSupplyContracts.BanquetCartWearPerShow) < 0.001d
        && order.pendingSupplyCartDurabilityAfter >= 0f;

    private static void Clear(CircusShowOrder order)
    {
        order.pendingSupplyPhase = CircusShowSupplyCommitPhase.None;
        order.pendingSupplyOperationSequence = 0;
        order.pendingSupplyOperationId = string.Empty;
        order.pendingSupplyReasonCode = string.Empty;
        order.pendingSupplyCommitId = string.Empty;
        order.pendingSupplySourceStackIds.Clear();
        order.pendingSupplyQuantity = 0;
        order.pendingSupplyMassGrams = 0;
        order.pendingSupplyCartStackId = string.Empty;
        order.pendingSupplyCartDurabilityBefore = 0f;
        order.pendingSupplyCartDurabilityAfter = 0f;
    }
}
