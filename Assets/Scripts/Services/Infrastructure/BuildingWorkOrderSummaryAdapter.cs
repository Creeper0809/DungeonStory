using System;
using System.Collections.Generic;

public sealed class BuildingWorkOrderSummaryAdapter :
    IBuildingWorkOrderSummaryQuery
{
    private readonly IWorkOrderRuntime workOrders;

    public BuildingWorkOrderSummaryAdapter(IWorkOrderRuntime workOrders)
    {
        this.workOrders = workOrders
            ?? throw new ArgumentNullException(nameof(workOrders));
    }

    public bool TryGetOrder(
        IBuildingWorldEntryPort building,
        WorkTypeId workTypeId,
        out BuildingWorkOrderSummarySnapshot snapshot)
    {
        if (building is not BuildableObject buildable
            || !workOrders.TryGetOrderFor(
                buildable,
                workTypeId,
                out WorkOrderProgressState order))
        {
            snapshot = default;
            return false;
        }

        snapshot = new BuildingWorkOrderSummarySnapshot(
            MapStatus(order.Status),
            order.RequiredWork,
            order.CompletedWork,
            order.ReservedWorkerPersistentId,
            Copy(order.ItemMaterialRequirements),
            Copy(order.DeliveredItemMaterials));
        return true;
    }

    private static BuildingWorkOrderSummaryStatus MapStatus(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.WaitingForMaterials => BuildingWorkOrderSummaryStatus.WaitingForMaterials,
            WorkOrderStatus.Ready => BuildingWorkOrderSummaryStatus.Ready,
            WorkOrderStatus.InProgress => BuildingWorkOrderSummaryStatus.InProgress,
            WorkOrderStatus.Blocked => BuildingWorkOrderSummaryStatus.Blocked,
            WorkOrderStatus.Completed => BuildingWorkOrderSummaryStatus.Completed,
            WorkOrderStatus.Cancelled => BuildingWorkOrderSummaryStatus.Cancelled,
            WorkOrderStatus.WaitingForEligibleWorker =>
                BuildingWorkOrderSummaryStatus.Blocked,
            WorkOrderStatus.TargetCurrentlyUnreachable =>
                BuildingWorkOrderSummaryStatus.Blocked,
            WorkOrderStatus.WaitingForOutputSpace =>
                BuildingWorkOrderSummaryStatus.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    private static IReadOnlyDictionary<string, int> Copy(
        IReadOnlyDictionary<string, int> source)
    {
        Dictionary<string, int> copy = new Dictionary<string, int>(StringComparer.Ordinal);
        if (source == null)
        {
            return copy;
        }

        foreach (KeyValuePair<string, int> pair in source)
        {
            copy.Add(pair.Key, pair.Value);
        }

        return copy;
    }
}
