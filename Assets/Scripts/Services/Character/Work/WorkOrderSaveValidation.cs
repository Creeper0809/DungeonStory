using System;
using System.Collections.Generic;

internal static class WorkOrderSaveValidation
{
    internal const int MaxSavedOrders = 4096;

    public static void Validate(
        DungeonWorkOrderSaveData snapshot,
        DungeonGameRestoreReport report,
        Func<int, BuildingSO> findBuilding,
        Func<string, bool> itemDefinitionExists)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (snapshot == null)
        {
            report.AddError("Work-order payload is null.");
            return;
        }

        if (snapshot.version != DungeonWorkOrderSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported work-order payload version {snapshot.version}; expected {DungeonWorkOrderSaveData.CurrentVersion}.");
        }

        if (snapshot.nextOrderSequence < 1)
        {
            report.AddError("Work-order next sequence must be positive.");
        }

        if (snapshot.orders == null)
        {
            report.AddError("Work-order payload has no order list.");
            return;
        }

        if (snapshot.orders.Count > MaxSavedOrders)
        {
            report.AddError(
                $"Work-order payload exceeds the {MaxSavedOrders}-order limit.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> constructionTargets =
            new HashSet<string>(StringComparer.Ordinal);
        int highestSequence = 0;
        string previousOrderId = string.Empty;
        for (int index = 0; index < snapshot.orders.Count; index++)
        {
            WorkOrderSaveData order = snapshot.orders[index];
            if (order == null)
            {
                report.AddError($"Work-order payload order {index} is null.");
                continue;
            }

            string orderId = order.workOrderId?.Trim() ?? string.Empty;
            if (!TryParseOrderSequence(orderId, out int sequence)
                || !string.Equals(
                    order.workOrderId,
                    orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    orderId,
                    $"work:{sequence:D6}",
                    StringComparison.Ordinal)
                || !ids.Add(orderId))
            {
                report.AddError(
                    $"Work-order payload contains invalid or duplicate ID '{orderId}'.");
            }
            else
            {
                highestSequence = Math.Max(highestSequence, sequence);
                if (index > 0
                    && string.CompareOrdinal(previousOrderId, orderId) >= 0)
                {
                    report.AddError(
                        "Work-order payload orders must use canonical ascending ID order.");
                }

                previousOrderId = orderId;
            }

            if (!WorkTypeCatalog.TryGet(
                    order.workTypeId,
                    out WorkTypeDefinition definition))
            {
                report.AddError(
                    $"Work order '{orderId}' references unknown work type '{order.workTypeId}'.");
                continue;
            }
            if (!string.Equals(
                    order.workTypeId,
                    definition.WorkTypeId.Value,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Work order '{orderId}' has non-canonical work type '{order.workTypeId}'.");
            }

            if (order.targetBuildingId <= 0)
            {
                report.AddError(
                    $"Work order '{orderId}' has invalid target building ID {order.targetBuildingId}.");
            }

            if (!IsFinitePositive(order.requiredWork)
                || float.IsNaN(order.completedWork)
                || float.IsInfinity(order.completedWork)
                || order.completedWork < 0f
                || order.completedWork > order.requiredWork)
            {
                report.AddError(
                    $"Work order '{orderId}' has invalid work progress {order.completedWork}/{order.requiredWork}.");
            }

            if (!Enum.IsDefined(typeof(WorkOrderStatus), order.status)
                || order.status == WorkOrderStatus.InProgress
                || order.status == WorkOrderStatus.Completed
                || order.status == WorkOrderStatus.Cancelled)
            {
                report.AddError(
                    $"Work order '{orderId}' has non-restorable status {order.status}.");
            }

            if (order.materialDestinationId == null
                || !string.Equals(
                    order.materialDestinationId,
                    order.materialDestinationId.Trim(),
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(order.reservedWorkerPersistentId))
            {
                report.AddError(
                    $"Work order '{orderId}' has non-canonical destination or transient worker reservation state.");
            }

            ValidateMaterials(
                order,
                orderId,
                report,
                itemDefinitionExists);
            if (definition.WorkTypeId != BuiltInWorkTypeIds.Construct)
            {
                continue;
            }

            BuildingSO building = findBuilding?.Invoke(order.targetBuildingId);
            if (building == null)
            {
                report.AddError(
                    $"Construction order '{orderId}' references missing building {order.targetBuildingId}.");
                continue;
            }

            string expectedDestination =
                $"{WorkOrderRuntime.ConstructionDestinationPrefix}{building.id}:{order.gridX}:{order.gridY}";
            if (!string.Equals(
                    order.materialDestinationId,
                    expectedDestination,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Construction order '{orderId}' has destination '{order.materialDestinationId}', expected '{expectedDestination}'.");
            }

            string targetKey =
                $"{order.targetBuildingId}:{order.gridX}:{order.gridY}";
            if (!constructionTargets.Add(targetKey))
            {
                report.AddError(
                    $"Construction order '{orderId}' duplicates target {targetKey}.");
            }
        }

        if (snapshot.nextOrderSequence <= highestSequence)
        {
            report.AddError(
                $"Work-order next sequence {snapshot.nextOrderSequence} does not exceed existing sequence {highestSequence}.");
        }
    }

    private static void ValidateMaterials(
        WorkOrderSaveData order,
        string orderId,
        DungeonGameRestoreReport report,
        Func<string, bool> itemDefinitionExists)
    {
        if (order.itemMaterials == null)
        {
            report.AddError(
                $"Work order '{orderId}' has a missing material list.");
            return;
        }

        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        string previousItemId = string.Empty;
        foreach (WorkOrderItemMaterialSaveData material in order.itemMaterials)
        {
            string itemId = material?.itemId?.Trim() ?? string.Empty;
            if (material == null
                || itemId.Length == 0
                || !string.Equals(
                    material.itemId,
                    itemId,
                    StringComparison.Ordinal)
                || itemId.StartsWith("stock-item:", StringComparison.Ordinal)
                || itemDefinitionExists == null
                || !itemDefinitionExists(itemId)
                || material.required <= 0
                || material.delivered < 0
                || material.delivered > material.required
                || !itemIds.Add(itemId))
            {
                report.AddError(
                    $"Work order '{orderId}' contains an invalid or duplicate item material '{itemId}'.");
            }
            else if (previousItemId.Length > 0
                && string.CompareOrdinal(previousItemId, itemId) >= 0)
            {
                report.AddError(
                    $"Work order '{orderId}' item materials are not in canonical order.");
            }
            else
            {
                previousItemId = itemId;
            }
        }
    }

    private static bool TryParseOrderSequence(
        string orderId,
        out int sequence)
    {
        const string prefix = "work:";
        sequence = 0;
        return orderId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                orderId.Substring(prefix.Length),
                out sequence)
            && sequence > 0;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value > 0f;
    }
}
