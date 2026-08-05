using System;
using System.Collections.Generic;

public enum BuildingWorkOrderSummaryStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5
}

public readonly struct BuildingWorkOrderSummarySnapshot
{
    public BuildingWorkOrderSummarySnapshot(
        BuildingWorkOrderSummaryStatus status,
        float requiredWork,
        float completedWork,
        string reservedWorkerPersistentId,
        IReadOnlyDictionary<string, int> itemMaterialRequirements,
        IReadOnlyDictionary<string, int> deliveredItemMaterials)
    {
        Status = status;
        RequiredWork = requiredWork;
        CompletedWork = completedWork;
        ReservedWorkerPersistentId = reservedWorkerPersistentId ?? string.Empty;
        ItemMaterialRequirements = itemMaterialRequirements
            ?? throw new ArgumentNullException(nameof(itemMaterialRequirements));
        DeliveredItemMaterials = deliveredItemMaterials
            ?? throw new ArgumentNullException(nameof(deliveredItemMaterials));
    }

    public BuildingWorkOrderSummaryStatus Status { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public float ProgressRatio => RequiredWork <= 0f
        ? 1f
        : Math.Min(1f, Math.Max(0f, CompletedWork / RequiredWork));
    public string ReservedWorkerPersistentId { get; }
    public IReadOnlyDictionary<string, int> ItemMaterialRequirements { get; }
    public IReadOnlyDictionary<string, int> DeliveredItemMaterials { get; }
}

public interface IBuildingWorkOrderSummaryQuery
{
    bool TryGetOrder(
        IBuildingWorldEntryPort building,
        WorkTypeId workTypeId,
        out BuildingWorkOrderSummarySnapshot snapshot);
}
