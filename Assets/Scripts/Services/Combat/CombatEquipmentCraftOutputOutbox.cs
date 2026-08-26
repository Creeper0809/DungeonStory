using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Idempotently publishes generic combat-craft output into the facility output
/// buffer. The deterministic component commit prevents a retry or restore from
/// minting a second stack.
/// </summary>
public static class CombatEquipmentCraftOutputOutbox
{
    public const string ReasonCode = "combat-equipment-craft-output";
    public const string OperationPrefix = "combat-craft-output:";

    public static string FormatOperationId(string orderId, int attemptIndex) =>
        $"{OperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}";

    public static string FormatCommitId(
        string operationId,
        string itemId,
        int quantity) =>
        $"physical-source:{operationId}:{itemId}:{quantity}";

    public static bool TryEnsureGenericOutput(
        CombatEquipmentCraftOrderSaveData order,
        IEquipmentPhysicalItemGateway items,
        Vector2Int position,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || !IsCanonical(order.outputItemId)
            || order.outputQuantity <= 0
            || !IsCanonical(destinationId))
        {
            failureReason = "combat-craft-output-request-invalid";
            return false;
        }

        string operationId = FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        string commitId = FormatCommitId(operationId, order.outputItemId, order.outputQuantity);
        bool hasOperation = !string.IsNullOrEmpty(order.outputOperationId);
        bool hasCommit = !string.IsNullOrEmpty(order.outputCommitId);
        if (hasOperation
                && !string.Equals(
                    order.outputOperationId,
                    operationId,
                    StringComparison.Ordinal)
            || hasCommit
                && !string.Equals(
                    order.outputCommitId,
                    commitId,
                    StringComparison.Ordinal)
            || hasCommit && !hasOperation
            || order.outputPublished && (!hasOperation || !hasCommit))
        {
            failureReason = "combat-craft-output-owner-conflict";
            return false;
        }

        if (!TryEnsureGenericOutput(
                order.outputItemId,
                order.outputQuantity,
                operationId,
                items,
                position,
                destinationId,
                out string ensuredCommitId,
                out failureReason))
        {
            return false;
        }
        order.outputOperationId = operationId;
        order.outputCommitId = ensuredCommitId;
        order.outputPublished = true;
        return true;
    }

    public static bool TryEnsureGenericOutput(
        string itemId,
        int quantity,
        string operationId,
        IEquipmentPhysicalItemGateway items,
        Vector2Int position,
        string destinationId,
        out string commitId,
        out string failureReason)
    {
        commitId = string.Empty;
        failureReason = string.Empty;
        if (!IsCanonical(itemId)
            || quantity <= 0
            || !IsCanonical(operationId)
            || items == null
            || !IsCanonical(destinationId))
        {
            failureReason = "combat-craft-output-request-invalid";
            return false;
        }
        string expectedCommitId = FormatCommitId(operationId, itemId, quantity);
        WorldItemStackSnapshot[] existing = items.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    expectedCommitId))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (existing.Length > 0)
        {
            if (existing.Any(stack =>
                    !string.Equals(
                        stack.ItemId,
                        itemId,
                        StringComparison.Ordinal)
                    || stack.State != WorldItemStackState.FacilityOutputBuffer
                    || stack.Position != position
                    || !string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                || existing.Sum(stack => (long)stack.Quantity)
                    != quantity)
            {
                failureReason = "combat-craft-output-existing-conflict";
                return false;
            }
            commitId = expectedCommitId;
            return true;
        }

        if (!items.SpawnItemAtWithComponents(
                itemId,
                quantity,
                position,
                WorldItemStackState.FacilityOutputBuffer,
                destinationId,
                new[] { ProductionOutputCommitComponentCodec.Create(expectedCommitId) },
                out int spawned)
            || spawned != quantity)
        {
            failureReason = "combat-craft-output-space-unavailable";
            return false;
        }

        WorldItemStackSnapshot[] published = items.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    expectedCommitId))
            .ToArray();
        if (published.Length == 0
            || published.Any(stack =>
                !string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                || stack.State != WorldItemStackState.FacilityOutputBuffer
                || stack.Position != position
                || !string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            || published.Sum(stack => (long)stack.Quantity)
                != quantity)
        {
            failureReason = "combat-craft-output-postcondition-failed";
            return false;
        }

        commitId = expectedCommitId;
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
