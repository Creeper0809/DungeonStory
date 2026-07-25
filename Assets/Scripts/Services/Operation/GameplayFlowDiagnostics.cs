using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GameplayFlowDiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public sealed class GameplayFlowDiagnosticItem
{
    public GameplayFlowDiagnosticSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class GameplayFlowDiagnosticsSnapshot
{
    public string Summary { get; set; } = string.Empty;
    public int ActiveOrderCount { get; set; }
    public int BlockedOrderCount { get; set; }
    public int LooseStackCount { get; set; }
    public float LooseWeight { get; set; }
    public IReadOnlyList<GameplayFlowDiagnosticItem> Items { get; set; }
        = Array.Empty<GameplayFlowDiagnosticItem>();
}

public sealed class GameplayFlowWorkerSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool CanRunAi { get; set; }
    public bool IsOffDuty { get; set; }
    public bool HaulEnabled { get; set; }
    public bool HasHaulPlan { get; set; }
    public bool PathSearchDeferred { get; set; }
    public IReadOnlyCollection<string> EnabledWorkTypeIds { get; set; }
        = Array.Empty<string>();

    public bool CanPerform(string workTypeId)
    {
        return CanRunAi
            && !IsOffDuty
            && EnabledWorkTypeIds.Contains(workTypeId ?? string.Empty);
    }
}

public sealed class GameplayFlowWarehouseSnapshot
{
    public string Name { get; set; } = string.Empty;
    public bool HasInventory { get; set; }
    public bool CanAcceptLooseStack { get; set; }
    public int RemainingCapacity { get; set; }
}

public interface IGameplayFlowDiagnosticsQuery
{
    GameplayFlowDiagnosticsSnapshot Capture();
}

public sealed class GameplayFlowDiagnosticsQuery : IGameplayFlowDiagnosticsQuery
{
    private readonly IWorkOrderRuntime workOrders;
    private readonly IWorldItemStackRuntime itemStacks;
    private readonly IStaffWorkforceQueryService workforce;
    private readonly IWarehouseWorldQuery warehouseWorld;

    public GameplayFlowDiagnosticsQuery(
        IWorkOrderRuntime workOrders,
        IWorldItemStackRuntime itemStacks,
        IStaffWorkforceQueryService workforce,
        IWarehouseWorldQuery warehouseWorld)
    {
        this.workOrders = workOrders ?? throw new ArgumentNullException(nameof(workOrders));
        this.itemStacks = itemStacks ?? throw new ArgumentNullException(nameof(itemStacks));
        this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        this.warehouseWorld = warehouseWorld ?? throw new ArgumentNullException(nameof(warehouseWorld));
    }

    public GameplayFlowDiagnosticsSnapshot Capture()
    {
        WorldItemStackSnapshot[] stacks = itemStacks.GetAllStacks()
            .Where(stack => stack != null && stack.Quantity > 0)
            .ToArray();
        bool hasUnassignedLooseStacks = stacks.Any(stack =>
            stack.State == WorldItemStackState.Loose
            && string.IsNullOrWhiteSpace(stack.DestinationId));
        IReadOnlyList<GameplayFlowWorkerSnapshot> workers = workforce.FindActiveWorkers()
            .Where(actor => actor != null)
            .Select(actor => CreateWorkerSnapshot(actor, hasUnassignedLooseStacks))
            .ToArray();
        IReadOnlyList<GameplayFlowWarehouseSnapshot> warehouses = warehouseWorld.Warehouses
            .Where(warehouse => warehouse != null)
            .Select(warehouse => CreateWarehouseSnapshot(warehouse, stacks))
            .ToArray();
        return GameplayFlowDiagnosticsBuilder.Build(
            workOrders.Capture()?.orders,
            stacks,
            workers,
            warehouses);
    }

    private GameplayFlowWorkerSnapshot CreateWorkerSnapshot(
        CharacterActor actor,
        bool evaluateHaulPlan)
    {
        if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work))
        {
            return new GameplayFlowWorkerSnapshot
            {
                Name = workforce.GetDisplayName(actor),
                CanRunAi = false
            };
        }

        string[] enabledWorkTypes = WorkTypeCatalog.All
            .Where(definition => work.WorkPriorities.IsEnabled(definition.WorkTypeId))
            .Select(definition => definition.Id)
            .ToArray();
        bool haulEnabled = work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Haul);
        return new GameplayFlowWorkerSnapshot
        {
            Name = workforce.GetDisplayName(actor),
            CanRunAi = actor.CanRunAi,
            IsOffDuty = work.IsOffDuty,
            HaulEnabled = haulEnabled,
            HasHaulPlan = evaluateHaulPlan
                && actor.CanRunAi
                && !work.IsOffDuty
                && haulEnabled
                && itemStacks.HasAvailableHaulJob(actor),
            PathSearchDeferred = actor.Brain?.IsPathSearchDeferred == true,
            EnabledWorkTypeIds = enabledWorkTypes
        };
    }

    private static GameplayFlowWarehouseSnapshot CreateWarehouseSnapshot(
        IWarehouseFacility warehouse,
        IReadOnlyList<WorldItemStackSnapshot> stacks)
    {
        WarehouseInventory inventory = warehouse.Inventory;
        bool canAcceptLoose = inventory != null && stacks.Any(stack =>
            stack.State == WorldItemStackState.Loose
            && string.IsNullOrWhiteSpace(stack.DestinationId)
            && inventory.CanStore(stack.StockCategory, 1));
        return new GameplayFlowWarehouseSnapshot
        {
            Name = warehouse is BuildableObject building
                ? (!string.IsNullOrWhiteSpace(building.BuildingData?.objectName)
                    ? building.BuildingData.objectName
                    : building.name)
                : "창고",
            HasInventory = warehouse.HasWarehouseInventory && inventory != null,
            CanAcceptLooseStack = canAcceptLoose,
            RemainingCapacity = inventory?.RemainingCapacity ?? 0
        };
    }
}

public static class GameplayFlowDiagnosticsBuilder
{
    private const int MaxVisibleOrders = 7;

    public static GameplayFlowDiagnosticsSnapshot Build(
        IEnumerable<WorkOrderSaveData> orders,
        IEnumerable<WorldItemStackSnapshot> stacks,
        IEnumerable<GameplayFlowWorkerSnapshot> workers,
        IEnumerable<GameplayFlowWarehouseSnapshot> warehouses = null)
    {
        WorkOrderSaveData[] activeOrders = (orders ?? Array.Empty<WorkOrderSaveData>())
            .Where(IsActive)
            .OrderByDescending(GetSeverity)
            .ThenBy(order => order.workOrderId, StringComparer.Ordinal)
            .ToArray();
        WorldItemStackSnapshot[] allStacks = (stacks ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(stack => stack != null && stack.Quantity > 0)
            .ToArray();
        GameplayFlowWorkerSnapshot[] activeWorkers =
            (workers ?? Array.Empty<GameplayFlowWorkerSnapshot>())
            .Where(worker => worker != null)
            .ToArray();
        GameplayFlowWarehouseSnapshot[] activeWarehouses = warehouses?
            .Where(warehouse => warehouse != null)
            .ToArray();

        List<GameplayFlowDiagnosticItem> items = new List<GameplayFlowDiagnosticItem>();
        foreach (WorkOrderSaveData order in activeOrders.Take(MaxVisibleOrders))
        {
            items.Add(BuildOrderDiagnostic(order, allStacks, activeWorkers));
        }

        WorldItemStackSnapshot[] looseStacks = allStacks
            .Where(stack => stack.State == WorldItemStackState.Loose)
            .ToArray();
        int reservedLooseCount = looseStacks.Count(stack => stack.IsReserved);
        GameplayFlowDiagnosticItem looseDiagnostic = BuildLooseStackDiagnostic(
            looseStacks,
            reservedLooseCount,
            activeWorkers,
            activeWarehouses);
        items.Add(looseDiagnostic);

        int blockedCount = activeOrders.Count(order =>
            GetSeverity(order) == GameplayFlowDiagnosticSeverity.Critical);
        int criticalCount = items.Count(item =>
            item.Severity == GameplayFlowDiagnosticSeverity.Critical);
        int warningCount = items.Count(item =>
            item.Severity == GameplayFlowDiagnosticSeverity.Warning);
        string summary = criticalCount > 0
            ? $"작업 {activeOrders.Length}건 · 막힘 {criticalCount}건 · 즉시 확인 필요"
            : warningCount > 0
                ? $"작업 {activeOrders.Length}건 · 대기 {warningCount}건 · 복구 가능"
                : $"작업 {activeOrders.Length}건 · 물류 정상";

        return new GameplayFlowDiagnosticsSnapshot
        {
            Summary = summary,
            ActiveOrderCount = activeOrders.Length,
            BlockedOrderCount = blockedCount,
            LooseStackCount = looseStacks.Length,
            LooseWeight = looseStacks.Sum(stack => stack.TotalWeight),
            Items = items
        };
    }

    private static GameplayFlowDiagnosticItem BuildLooseStackDiagnostic(
        IReadOnlyList<WorldItemStackSnapshot> looseStacks,
        int reservedLooseCount,
        IReadOnlyList<GameplayFlowWorkerSnapshot> workers,
        IReadOnlyList<GameplayFlowWarehouseSnapshot> warehouses)
    {
        if (looseStacks.Count == 0)
        {
            return new GameplayFlowDiagnosticItem
            {
                Severity = GameplayFlowDiagnosticSeverity.Info,
                Title = "바닥 물류 정상",
                Detail = "운반되지 않은 loose 스택이 없습니다."
            };
        }

        string totals = $"스택 {looseStacks.Count}개 · 수량 {looseStacks.Sum(stack => stack.Quantity)}"
            + $" · {looseStacks.Sum(stack => stack.TotalWeight):0.#}kg"
            + $" · 예약 {reservedLooseCount}개";
        WorldItemStackSnapshot[] unassigned = looseStacks
            .Where(stack =>
                string.IsNullOrWhiteSpace(stack.DestinationId)
                && !stack.IsReserved)
            .ToArray();
        if (unassigned.Length == 0)
        {
            return Warning(
                "바닥 물류 운반 중",
                $"{totals} · 예약된 물품을 직원이 옮기고 있습니다.");
        }

        if (warehouses == null)
        {
            return Warning("바닥 물류 대기", totals);
        }

        int availableHaulers = workers.Count(worker =>
            worker.CanRunAi && !worker.IsOffDuty && worker.HaulEnabled);
        if (availableHaulers == 0)
        {
            return Critical(
                "바닥 물류",
                $"{totals} · 운반 가능한 직원이 없습니다. 작업 우선순위에서 운반을 활성화하세요.");
        }

        if (!warehouses.Any(warehouse => warehouse.HasInventory))
        {
            return Critical(
                "바닥 물류",
                $"{totals} · 사용 가능한 창고가 없습니다. 창고를 건설하거나 수리하세요.");
        }

        if (!warehouses.Any(warehouse =>
                warehouse.HasInventory
                && warehouse.RemainingCapacity > 0
                && warehouse.CanAcceptLooseStack))
        {
            return Critical(
                "바닥 물류",
                $"{totals} · 받아들일 빈 창고 공간이 없습니다. 창고 용량과 허용 품목을 확인하세요.");
        }

        if (!workers.Any(worker => worker.HasHaulPlan))
        {
            if (workers.Any(worker => worker.PathSearchDeferred))
            {
                return Warning(
                    "바닥 물류 경로 계산 중",
                    $"{totals} · 다음 AI 판단에서 운반 경로를 다시 확인합니다.");
            }

            return Critical(
                "바닥 물류",
                $"{totals} · 하차장과 창고 사이의 이동 경로가 막혔습니다. 입구·문·저장 위치를 확인하세요.");
        }

        return Warning(
            "바닥 물류 대기",
            $"{totals} · 운반 가능 직원 {availableHaulers}명");
    }

    private static GameplayFlowDiagnosticItem BuildOrderDiagnostic(
        WorkOrderSaveData order,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IReadOnlyList<GameplayFlowWorkerSnapshot> workers)
    {
        string workName = FormatWorkName(order.workTypeId);
        string target = $"{workName} ({order.gridX}, {order.gridY})";
        if (order.status == WorkOrderStatus.Blocked)
        {
            return Critical(
                target,
                "대상이나 동선이 막혔습니다. 현장 정보를 열어 퇴로·예약을 확인하고, 필요하면 취소 후 다시 배치하세요.");
        }

        if (order.status == WorkOrderStatus.WaitingForMaterials)
        {
            return BuildMaterialDiagnostic(order, target, stacks, workers);
        }

        int capableWorkers = workers.Count(worker => worker.CanPerform(order.workTypeId));
        float progress = order.requiredWork <= 0f
            ? 1f
            : Mathf.Clamp01(order.completedWork / order.requiredWork);
        if (capableWorkers <= 0)
        {
            return Critical(
                target,
                $"{workName} 담당자가 없습니다. 직원 작업 우선순위에서 {workName}을 활성화하세요."
                + $" · 진행 {FormatPercent(progress)}");
        }

        string reservedWorker = string.IsNullOrWhiteSpace(order.reservedWorkerPersistentId)
            ? "미배정"
            : order.reservedWorkerPersistentId;
        return new GameplayFlowDiagnosticItem
        {
            Severity = order.status == WorkOrderStatus.InProgress
                ? GameplayFlowDiagnosticSeverity.Info
                : GameplayFlowDiagnosticSeverity.Warning,
            Title = order.status == WorkOrderStatus.InProgress
                ? $"[진행] {target}"
                : $"[작업 대기] {target}",
            Detail = $"진행 {FormatPercent(progress)} · 담당 {reservedWorker} · 가능한 직원 {capableWorkers}명"
        };
    }

    private static GameplayFlowDiagnosticItem BuildMaterialDiagnostic(
        WorkOrderSaveData order,
        string target,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IReadOnlyList<GameplayFlowWorkerSnapshot> workers)
    {
        List<string> shortages = new List<string>();
        List<string> deliveries = new List<string>();
        foreach (WorkOrderMaterialSaveData material in order.materials
                     ?? new List<WorkOrderMaterialSaveData>())
        {
            if (material == null)
            {
                continue;
            }

            int missing = Mathf.Max(0, material.required - material.delivered);
            if (missing <= 0)
            {
                continue;
            }

            int pending = stacks
                .Where(stack => IsPendingFor(order, material.category, stack))
                .Sum(stack => stack.Quantity);
            int available = stacks
                .Where(stack => IsAvailableSource(material.category, stack))
                .Sum(stack => stack.Quantity);
            string materialName = StockCategoryCatalog.GetDisplayName(material.category);
            if (pending + available < missing)
            {
                shortages.Add($"{materialName} {missing - pending - available}");
            }
            else
            {
                deliveries.Add($"{materialName} {missing} (예약·이동 {Mathf.Min(missing, pending)})");
            }
        }

        if (shortages.Count > 0)
        {
            return Critical(
                target,
                $"재료 부족: {string.Join(", ", shortages)}"
                + " · 상점·배송·생산으로 확보하면 운반 주문이 자동으로 이어집니다.");
        }

        int availableHaulers = workers.Count(worker =>
            worker.CanRunAi && !worker.IsOffDuty && worker.HaulEnabled);
        if (availableHaulers <= 0)
        {
            return Critical(
                target,
                $"재료는 있지만 운반 담당자가 없습니다. 직원 작업 우선순위에서 운반을 활성화하세요."
                + FormatMaterialSuffix(deliveries));
        }

        return new GameplayFlowDiagnosticItem
        {
            Severity = GameplayFlowDiagnosticSeverity.Warning,
            Title = $"[운반 대기] {target}",
            Detail = $"재료 조달 중 · 운반 가능 직원 {availableHaulers}명"
                + FormatMaterialSuffix(deliveries)
        };
    }

    private static bool IsPendingFor(
        WorkOrderSaveData order,
        StockCategory category,
        WorldItemStackSnapshot stack)
    {
        return stack != null
            && stack.StockCategory == category
            && string.Equals(
                stack.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal)
            && (stack.State == WorldItemStackState.Loose
                || stack.State == WorldItemStackState.FacilityBuffer
                || stack.State == WorldItemStackState.Carried
                || (stack.State == WorldItemStackState.Stored
                    && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)));
    }

    private static bool IsAvailableSource(
        StockCategory category,
        WorldItemStackSnapshot stack)
    {
        return stack != null
            && stack.StockCategory == category
            && !stack.Forbidden
            && string.IsNullOrWhiteSpace(stack.DestinationId)
            && (stack.State == WorldItemStackState.Stored
                || stack.State == WorldItemStackState.Loose);
    }

    private static bool IsActive(WorkOrderSaveData order)
    {
        return order != null
            && order.status != WorkOrderStatus.Completed
            && order.status != WorkOrderStatus.Cancelled;
    }

    private static GameplayFlowDiagnosticSeverity GetSeverity(WorkOrderSaveData order)
    {
        return order?.status switch
        {
            WorkOrderStatus.Blocked => GameplayFlowDiagnosticSeverity.Critical,
            WorkOrderStatus.WaitingForMaterials => GameplayFlowDiagnosticSeverity.Warning,
            WorkOrderStatus.Ready => GameplayFlowDiagnosticSeverity.Warning,
            _ => GameplayFlowDiagnosticSeverity.Info
        };
    }

    private static GameplayFlowDiagnosticItem Critical(string title, string detail)
    {
        return new GameplayFlowDiagnosticItem
        {
            Severity = GameplayFlowDiagnosticSeverity.Critical,
            Title = $"[막힘] {title}",
            Detail = detail
        };
    }

    private static GameplayFlowDiagnosticItem Warning(string title, string detail)
    {
        return new GameplayFlowDiagnosticItem
        {
            Severity = GameplayFlowDiagnosticSeverity.Warning,
            Title = title,
            Detail = detail
        };
    }

    private static string FormatWorkName(string workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? definition.DisplayName
            : "작업";
    }

    private static string FormatMaterialSuffix(IReadOnlyCollection<string> materials)
    {
        return materials != null && materials.Count > 0
            ? $" · {string.Join(", ", materials)}"
            : string.Empty;
    }

    private static string FormatPercent(float ratio)
    {
        return $"{Mathf.RoundToInt(Mathf.Clamp01(ratio) * 100f)}%";
    }
}
