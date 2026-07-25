using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WorkOrderStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5
}

[Serializable]
public sealed class WorkOrderMaterialSaveData
{
    public StockCategory category = StockCategory.General;
    public int required;
    public int delivered;
}

[Serializable]
public sealed class WorkOrderSaveData
{
    public string workOrderId = string.Empty;
    public string workTypeId = string.Empty;
    public int targetBuildingId;
    public int gridX;
    public int gridY;
    public float requiredWork;
    public float completedWork;
    public string materialDestinationId = string.Empty;
    public string reservedWorkerPersistentId = string.Empty;
    public WorkOrderStatus status = WorkOrderStatus.WaitingForMaterials;
    public List<WorkOrderMaterialSaveData> materials = new List<WorkOrderMaterialSaveData>();
}

[Serializable]
public sealed class DungeonWorkOrderSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int nextOrderSequence = 1;
    public List<WorkOrderSaveData> orders = new List<WorkOrderSaveData>();
}

public sealed class WorkOrderProgressState
{
    public string WorkOrderId { get; set; }
    public WorkTypeId WorkTypeId { get; set; }
    public int TargetBuildingId { get; set; }
    public Vector2Int Position { get; set; }
    public float RequiredWork { get; set; }
    public float CompletedWork { get; set; }
    public string MaterialDestinationId { get; set; }
    public string ReservedWorkerPersistentId { get; set; }
    public WorkOrderStatus Status { get; set; }
    public IReadOnlyDictionary<StockCategory, int> MaterialRequirements { get; set; }
    public IReadOnlyDictionary<StockCategory, int> DeliveredMaterials { get; set; }
    public float ProgressRatio => RequiredWork <= 0f ? 1f : Mathf.Clamp01(CompletedWork / RequiredWork);
}

public interface IWorkOrderRuntime
{
    int WorkOrderCandidateVersion { get; }
    DungeonWorkOrderSaveData Capture();
    void Restore(DungeonWorkOrderSaveData snapshot, DungeonGameRestoreReport report);
    bool TryCreateConstructionOrder(ConstructionSite site, BuildingSO building, Vector2Int position, out string orderId, out string failureReason);
    bool TryGetOrderFor(BuildableObject target, WorkTypeId workTypeId, out WorkOrderProgressState order);
    bool ApplyWork(CharacterActor worker, BuildableObject target, WorkTypeId workTypeId, float amount, out bool completed, out bool appliedCompletionEffects, out string message);
    bool RefreshMaterialsReady(ConstructionSite site);
    bool CancelOrder(string orderId, bool refundDeliveredMaterials);
    bool DebugCompleteOrder(string orderId, out string message);
    int DebugCompleteAllOrders();
}

internal sealed class WorkOrderRecord
{
    public string workOrderId = string.Empty;
    public WorkTypeId workTypeId;
    public int targetBuildingId;
    public Vector2Int position;
    public float requiredWork;
    public float completedWork;
    public string materialDestinationId = string.Empty;
    public string reservedWorkerPersistentId = string.Empty;
    public WorkOrderStatus status = WorkOrderStatus.WaitingForMaterials;
    public readonly Dictionary<StockCategory, int> requiredMaterials = new Dictionary<StockCategory, int>();
    public readonly Dictionary<StockCategory, int> deliveredMaterials = new Dictionary<StockCategory, int>();
}

public sealed class WorkOrderRuntime : IWorkOrderRuntime
{
    public const string ConstructionDestinationPrefix = "construction:";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IBuildingDefinitionLookup buildingDefinitionLookup;
    private readonly Dictionary<string, WorkOrderRecord> ordersById =
        new Dictionary<string, WorkOrderRecord>(StringComparer.Ordinal);
    private readonly Dictionary<ConstructionSite, string> orderIdBySite =
        new Dictionary<ConstructionSite, string>();
    private int nextOrderSequence = 1;
    public int WorkOrderCandidateVersion { get; private set; }

    public WorkOrderRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IBuildingDefinitionLookup buildingDefinitionLookup)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.buildingDefinitionLookup = buildingDefinitionLookup ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
    }

    public DungeonWorkOrderSaveData Capture()
    {
        return new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = Mathf.Max(1, nextOrderSequence),
            orders = ordersById.Values
                .Where(order => order.status != WorkOrderStatus.Completed && order.status != WorkOrderStatus.Cancelled)
                .OrderBy(order => order.workOrderId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList()
        };
    }

    public void Restore(DungeonWorkOrderSaveData snapshot, DungeonGameRestoreReport report)
    {
        ClearRuntimeSites();
        ordersById.Clear();
        orderIdBySite.Clear();
        BumpWorkOrderCandidates();

        snapshot ??= new DungeonWorkOrderSaveData();
        if (snapshot.version != DungeonWorkOrderSaveData.CurrentVersion)
        {
            report?.AddWarning($"Unsupported work order snapshot version {snapshot.version}; work orders were discarded.");
            nextOrderSequence = 1;
            return;
        }

        nextOrderSequence = Mathf.Max(1, snapshot.nextOrderSequence);
        foreach (WorkOrderSaveData source in snapshot.orders ?? new List<WorkOrderSaveData>())
        {
            if (source == null || string.IsNullOrWhiteSpace(source.workOrderId))
            {
                continue;
            }

            if (ordersById.ContainsKey(source.workOrderId))
            {
                report?.AddError($"Duplicate work order id {source.workOrderId}.");
                continue;
            }

            WorkOrderRecord order = FromSaveData(source, report);
            if (order == null)
            {
                continue;
            }

            ordersById[order.workOrderId] = order;
            if (order.workTypeId == BuiltInWorkTypeIds.Construct)
            {
                RestoreConstructionSite(order, report);
            }
        }
    }

    public bool TryCreateConstructionOrder(
        ConstructionSite site,
        BuildingSO building,
        Vector2Int position,
        out string orderId,
        out string failureReason)
    {
        orderId = string.Empty;
        failureReason = string.Empty;
        if (site == null || building == null)
        {
            failureReason = "construction target missing";
            return false;
        }

        WorkOrderRecord order = new WorkOrderRecord
        {
            workOrderId = NextOrderId(),
            workTypeId = BuiltInWorkTypeIds.Construct,
            targetBuildingId = building.id,
            position = position,
            requiredWork = Mathf.Max(0.1f, building.GetRequiredWork(BuiltInWorkTypeIds.Construct)),
            completedWork = 0f,
            materialDestinationId = BuildConstructionDestinationId(building, position),
            status = WorkOrderStatus.WaitingForMaterials
        };

        foreach (KeyValuePair<StockCategory, int> pair in building.GetConstructionMaterials())
        {
            if (pair.Value > 0)
            {
                order.requiredMaterials[pair.Key] = pair.Value;
                order.deliveredMaterials[pair.Key] = 0;
            }
        }

        if (order.requiredMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
        }
        else
        {
            RequestMissingMaterials(order);
        }

        ordersById[order.workOrderId] = order;
        orderIdBySite[site] = order.workOrderId;
        site.ConfigureWorkOrderRuntime(this);
        orderId = order.workOrderId;
        BumpWorkOrderCandidates();
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantConstruction))
        {
            foreach (StockCategory category in order.requiredMaterials.Keys.ToArray())
            {
                order.deliveredMaterials[category] = order.requiredMaterials[category];
            }
            order.status = WorkOrderStatus.Ready;
            CompleteOrder(order, site, out _, out failureReason);
        }
        return true;
    }

    public bool TryGetOrderFor(
        BuildableObject target,
        WorkTypeId workTypeId,
        out WorkOrderProgressState order)
    {
        order = null;
        WorkOrderRecord record = FindOrder(target, workTypeId);
        if (record == null)
        {
            return false;
        }

        order = ToProgressState(record);
        return true;
    }

    public bool ApplyWork(
        CharacterActor worker,
        BuildableObject target,
        WorkTypeId workTypeId,
        float amount,
        out bool completed,
        out bool appliedCompletionEffects,
        out string message)
    {
        completed = false;
        appliedCompletionEffects = false;
        message = string.Empty;
        WorkOrderRecord order = FindOrder(target, workTypeId);
        if (order == null)
        {
            message = "work order missing";
            return false;
        }

        WorkTypeDefinition definition = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition resolvedDefinition)
            ? resolvedDefinition
            : null;
        string displayName = definition?.DisplayName ?? workTypeId.ToString();
        if (order.status == WorkOrderStatus.WaitingForMaterials && !EnsureMaterialsReady(order, out message))
        {
            RequestMissingMaterials(order);
            return false;
        }

        if (order.status == WorkOrderStatus.Blocked
            || order.status == WorkOrderStatus.Cancelled
            || order.status == WorkOrderStatus.Completed)
        {
            message = order.status.ToString();
            return false;
        }

        order.status = WorkOrderStatus.InProgress;
        order.reservedWorkerPersistentId = worker?.Identity?.PersistentId ?? string.Empty;
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork)
            || (workTypeId == BuiltInWorkTypeIds.Construct
                && DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantConstruction)))
        {
            amount = order.requiredWork;
        }
        order.completedWork = Mathf.Clamp(
            order.completedWork + Mathf.Max(0f, amount),
            0f,
            Mathf.Max(0.1f, order.requiredWork));

        if (order.completedWork + 0.001f < order.requiredWork)
        {
            message = $"{displayName} {Mathf.RoundToInt(order.completedWork / Mathf.Max(0.1f, order.requiredWork) * 100f)}%";
            return true;
        }

        completed = CompleteOrder(order, target, out appliedCompletionEffects, out message);
        return completed;
    }

    public bool RefreshMaterialsReady(ConstructionSite site)
    {
        WorkOrderRecord order = FindOrder(site, BuiltInWorkTypeIds.Construct);
        if (order == null)
        {
            return false;
        }

        if (order.status == WorkOrderStatus.Ready || order.status == WorkOrderStatus.InProgress)
        {
            return true;
        }

        bool ready = EnsureMaterialsReady(order, out _);
        if (!ready)
        {
            RequestMissingMaterials(order);
        }
        else
        {
            BumpWorkOrderCandidates();
        }

        return ready;
    }

    public bool CancelOrder(string orderId, bool refundDeliveredMaterials)
    {
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId, out WorkOrderRecord order))
        {
            return false;
        }

        order.status = WorkOrderStatus.Cancelled;
        itemStackRuntime.RemoveStacksByStateAndDestination(
            WorldItemStackState.Loose,
            order.materialDestinationId);
        itemStackRuntime.RemoveStacksByStateAndDestination(
            WorldItemStackState.FacilityBuffer,
            order.materialDestinationId);
        itemStackRuntime.RemoveStacksByStateAndDestination(
            WorldItemStackState.Stored,
            order.materialDestinationId);
        ordersById.Remove(orderId);
        foreach (KeyValuePair<ConstructionSite, string> pair in orderIdBySite.ToArray())
        {
            if (string.Equals(pair.Value, orderId, StringComparison.Ordinal))
            {
                orderIdBySite.Remove(pair.Key);
            }
        }

        BumpWorkOrderCandidates();
        return true;
    }

    public bool DebugCompleteOrder(string orderId, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(orderId)
            || !ordersById.TryGetValue(orderId, out WorkOrderRecord order))
        {
            message = "작업 주문을 찾을 수 없습니다.";
            return false;
        }

        BuildableObject target = ResolveTarget(order);
        if (target == null)
        {
            message = "작업 대상을 찾을 수 없습니다.";
            return false;
        }

        order.completedWork = order.requiredWork;
        return CompleteOrder(order, target, out _, out message);
    }

    public int DebugCompleteAllOrders()
    {
        int completed = 0;
        foreach (string orderId in ordersById.Keys.ToArray())
        {
            if (DebugCompleteOrder(orderId, out _))
            {
                completed++;
            }
        }

        return completed;
    }

    private BuildableObject ResolveTarget(WorkOrderRecord order)
    {
        if (order == null)
        {
            return null;
        }

        if (order.workTypeId == BuiltInWorkTypeIds.Construct)
        {
            return orderIdBySite.FirstOrDefault(pair =>
                string.Equals(pair.Value, order.workOrderId, StringComparison.Ordinal)).Key;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return null;
        }

        return grid.GetGridCell(order.position)?
            .GetAllOccupants()
            .OfType<BuildableObject>()
            .FirstOrDefault(building => building != null && building.id == order.targetBuildingId);
    }

    private bool CompleteOrder(
        WorkOrderRecord order,
        BuildableObject target,
        out bool appliedCompletionEffects,
        out string message)
    {
        appliedCompletionEffects = false;
        message = string.Empty;
        order.status = WorkOrderStatus.Completed;
        order.completedWork = order.requiredWork;
        ordersById.Remove(order.workOrderId);
        BumpWorkOrderCandidates();

        if (target is ConstructionSite site)
        {
            orderIdBySite.Remove(site);
            appliedCompletionEffects = true;
            bool placed = site.CompleteConstruction();
            message = placed ? "construction completed" : "construction completion failed";
            return placed;
        }

        message = "work completed";
        return true;
    }

    private WorkOrderRecord FindOrder(BuildableObject target, WorkTypeId workTypeId)
    {
        if (target is ConstructionSite site
            && orderIdBySite.TryGetValue(site, out string orderId)
            && ordersById.TryGetValue(orderId, out WorkOrderRecord bySite)
            && IsOrderWorkType(bySite, workTypeId))
        {
            return bySite;
        }

        if (target == null)
        {
            return null;
        }

        return ordersById.Values.FirstOrDefault(order =>
            IsOrderWorkType(order, workTypeId)
            && order.targetBuildingId == target.id
            && order.position == target.centerPos);
    }

    private static bool IsOrderWorkType(WorkOrderRecord order, WorkTypeId workTypeId)
    {
        return order != null
            && workTypeId.IsValid
            && order.workTypeId == workTypeId;
    }

    private bool EnsureMaterialsReady(WorkOrderRecord order, out string failureReason)
    {
        failureReason = string.Empty;
        if (order.requiredMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
            return true;
        }

        Dictionary<StockCategory, int> missing = new Dictionary<StockCategory, int>();
        foreach (KeyValuePair<StockCategory, int> pair in order.requiredMaterials)
        {
            order.deliveredMaterials.TryGetValue(pair.Key, out int delivered);
            int remaining = pair.Value - delivered;
            if (remaining > 0)
            {
                missing[pair.Key] = remaining;
            }
        }

        if (missing.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
            return true;
        }

        if (!itemStackRuntime.TryConsumeFacilityBuffer(
                order.materialDestinationId,
                missing,
                out failureReason))
        {
            order.status = WorkOrderStatus.WaitingForMaterials;
            return false;
        }

        foreach (KeyValuePair<StockCategory, int> pair in missing)
        {
            order.deliveredMaterials[pair.Key] = order.deliveredMaterials.TryGetValue(pair.Key, out int current)
                ? current + pair.Value
                : pair.Value;
        }

        order.status = WorkOrderStatus.Ready;
        BumpWorkOrderCandidates();
        return true;
    }

    private void RequestMissingMaterials(WorkOrderRecord order)
    {
        if (order == null || order.requiredMaterials.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<StockCategory, int> pair in order.requiredMaterials)
        {
            int delivered = order.deliveredMaterials.TryGetValue(pair.Key, out int currentDelivered)
                ? currentDelivered
                : 0;
            int pending = CountPendingDestinationStock(order, pair.Key);
            int remaining = Mathf.Max(0, pair.Value - delivered - pending);
            if (remaining <= 0)
            {
                continue;
            }

            itemStackRuntime.TryRequestFacilityDelivery(
                pair.Key,
                remaining,
                order.position,
                order.materialDestinationId,
                out _,
                out _);
        }
    }

    private int CountPendingDestinationStock(WorkOrderRecord order, StockCategory category)
    {
        return itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal)
                && stack.StockCategory == category
                && (stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer
                    || (stack.State == WorldItemStackState.Stored
                        && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId))))
            .Sum(stack => stack.Quantity);
    }

    private void RestoreConstructionSite(WorkOrderRecord order, DungeonGameRestoreReport report)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            report?.AddWarning($"Cannot restore construction site {order.workOrderId}: grid missing.");
            return;
        }

        BuildingSO building = TryGetBuilding(order.targetBuildingId);
        if (building == null)
        {
            report?.AddWarning($"Cannot restore construction site {order.workOrderId}: building {order.targetBuildingId} missing.");
            return;
        }

        GameObject siteObject = new GameObject($"ConstructionSite_{building.objectName}_{order.position.x}_{order.position.y}");
        DungeonRuntimeHierarchy.Parent(siteObject, DungeonRuntimeHierarchy.Construction);
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.transform.position = grid.GetWorldPos(order.position);
        site.SetGrid(grid);
        site.Initialization(building, order.position);
        site.ConfigureWorkOrderRuntime(this);
        site.ConfigureSite(
            order.workOrderId,
            () => TryPlaceFinalBuildingOnRestore(grid, building, order.position),
            () => RemoveSiteFromGrid(grid, site));

        if (!grid.RegisterOccupant(
                site,
                GridLayer.Construction,
                building.GetGridPosList(order.position),
                false))
        {
            UnityEngine.Object.Destroy(siteObject);
            report?.AddWarning($"Cannot restore construction site {order.workOrderId}: grid occupied.");
            return;
        }

        orderIdBySite[site] = order.workOrderId;
    }

    private bool TryPlaceFinalBuildingOnRestore(Grid grid, BuildingSO building, Vector2Int position)
    {
        GridBuildingPlacementService service = new GridBuildingPlacementService(
            grid,
            null,
            TryGetBuilding);
        return service.TryPlaceBuildingImmediateUnchecked(
            building,
            position,
            chargeCost: false,
            out _);
    }

    private void ClearRuntimeSites()
    {
        foreach (ConstructionSite site in orderIdBySite.Keys.ToArray())
        {
            if (site == null)
            {
                continue;
            }

            RemoveSiteFromGrid(site.Grid, site);
            UnityEngine.Object.Destroy(site.gameObject);
        }
    }

    private static void RemoveSiteFromGrid(Grid grid, ConstructionSite site)
    {
        if (grid == null || site == null)
        {
            return;
        }

        grid.RemoveOccupant(
            site,
            GridLayer.Construction,
            site.buildPoses,
            false);
        if (site != null)
        {
            UnityEngine.Object.Destroy(site.gameObject);
        }
    }

    private BuildingSO TryGetBuilding(int id)
    {
        try
        {
            return buildingDefinitionLookup.GetBuilding(id);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string NextOrderId()
    {
        return $"work:{nextOrderSequence++:D6}";
    }

    private void BumpWorkOrderCandidates()
    {
        unchecked
        {
            WorkOrderCandidateVersion++;
        }
    }

    private static string BuildConstructionDestinationId(BuildingSO building, Vector2Int position)
    {
        return $"{ConstructionDestinationPrefix}{building.id}:{position.x}:{position.y}";
    }

    private static WorkOrderSaveData ToSaveData(WorkOrderRecord order)
    {
        return new WorkOrderSaveData
        {
            workOrderId = order.workOrderId,
            workTypeId = order.workTypeId.Value,
            targetBuildingId = order.targetBuildingId,
            gridX = order.position.x,
            gridY = order.position.y,
            requiredWork = order.requiredWork,
            completedWork = order.completedWork,
            materialDestinationId = order.materialDestinationId,
            reservedWorkerPersistentId = order.reservedWorkerPersistentId,
            status = order.status,
            materials = order.requiredMaterials
                .OrderBy(pair => (int)pair.Key)
                .Select(pair => new WorkOrderMaterialSaveData
                {
                    category = pair.Key,
                    required = pair.Value,
                    delivered = order.deliveredMaterials.TryGetValue(pair.Key, out int delivered) ? delivered : 0
                })
                .ToList()
        };
    }

    private static WorkOrderRecord FromSaveData(
        WorkOrderSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!WorkTypeCatalog.TryGet(source.workTypeId, out WorkTypeDefinition definition))
        {
            report?.AddWarning(
                $"Cannot restore work order {source.workOrderId}: work type '{source.workTypeId}' is not registered.");
            return null;
        }

        WorkOrderRecord order = new WorkOrderRecord
        {
            workOrderId = source.workOrderId ?? string.Empty,
            workTypeId = definition.WorkTypeId,
            targetBuildingId = source.targetBuildingId,
            position = new Vector2Int(source.gridX, source.gridY),
            requiredWork = Mathf.Max(0.1f, source.requiredWork),
            completedWork = Mathf.Clamp(source.completedWork, 0f, Mathf.Max(0.1f, source.requiredWork)),
            materialDestinationId = source.materialDestinationId ?? string.Empty,
            reservedWorkerPersistentId = source.reservedWorkerPersistentId ?? string.Empty,
            status = source.status
        };

        foreach (WorkOrderMaterialSaveData material in source.materials ?? new List<WorkOrderMaterialSaveData>())
        {
            if (material == null || material.required <= 0)
            {
                continue;
            }

            order.requiredMaterials[material.category] = material.required;
            order.deliveredMaterials[material.category] = Mathf.Clamp(material.delivered, 0, material.required);
        }

        return order;
    }

    private static WorkOrderProgressState ToProgressState(WorkOrderRecord order)
    {
        return new WorkOrderProgressState
        {
            WorkOrderId = order.workOrderId,
            WorkTypeId = order.workTypeId,
            TargetBuildingId = order.targetBuildingId,
            Position = order.position,
            RequiredWork = order.requiredWork,
            CompletedWork = order.completedWork,
            MaterialDestinationId = order.materialDestinationId,
            ReservedWorkerPersistentId = order.reservedWorkerPersistentId,
            Status = order.status,
            MaterialRequirements = new Dictionary<StockCategory, int>(order.requiredMaterials),
            DeliveredMaterials = new Dictionary<StockCategory, int>(order.deliveredMaterials)
        };
    }
}
