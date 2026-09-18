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
    public bool IsCurrentOperationBlock { get; set; }
    public string TargetStableId { get; set; } = string.Empty;
    public CharacterOperationBlockAxis BlockAxis { get; set; }
    public AIActionFailureKind FailureKind { get; set; }
    public FailureCode DomainFailureCode { get; set; }
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
    public bool PathSearchDeferred { get; set; }
    public CharacterOperationBlockSnapshot CurrentOperationBlock { get; set; }
    public CharacterAiDecisionScheduleObservation DecisionSchedule { get; set; }
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
    public bool HasRemainingMassCapacity { get; set; }
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
    private readonly ICharacterAiDiagnosticsQuery aiDiagnostics;

    public GameplayFlowDiagnosticsQuery(
        IWorkOrderRuntime workOrders,
        IWorldItemStackRuntime itemStacks,
        IStaffWorkforceQueryService workforce,
        IWarehouseWorldQuery warehouseWorld,
        ICharacterAiDiagnosticsQuery aiDiagnostics)
    {
        this.workOrders = workOrders ?? throw new ArgumentNullException(nameof(workOrders));
        this.itemStacks = itemStacks ?? throw new ArgumentNullException(nameof(itemStacks));
        this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        this.warehouseWorld = warehouseWorld ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.aiDiagnostics = aiDiagnostics
            ?? throw new ArgumentNullException(nameof(aiDiagnostics));
    }

    public GameplayFlowDiagnosticsSnapshot Capture()
    {
        WorldItemStackSnapshot[] stacks = itemStacks.GetAllStacks()
            .Where(stack => stack != null && stack.Quantity > 0)
            .ToArray();
        IReadOnlyList<GameplayFlowWorkerSnapshot> workers = workforce.FindActiveWorkers()
            .Where(actor => actor != null)
            .Select(CreateWorkerSnapshot)
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

    private GameplayFlowWorkerSnapshot CreateWorkerSnapshot(CharacterActor actor)
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
            PathSearchDeferred = actor.Brain?.IsPathSearchDeferred == true,
            CurrentOperationBlock = actor.Brain?
                .CaptureCurrentOperationBlock() ?? default,
            DecisionSchedule = aiDiagnostics.CaptureDecisionSchedule(actor),
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
            && inventory.Accepts(stack.StockCategory)
            && inventory.CanStoreItem(stack.ItemId, 1));
        return new GameplayFlowWarehouseSnapshot
        {
            Name = warehouse is BuildableObject building
                ? (!string.IsNullOrWhiteSpace(building.BuildingData?.objectName)
                    ? building.BuildingData.objectName
                    : building.name)
                : "창고",
            HasInventory = warehouse.HasWarehouseInventory && inventory != null,
            CanAcceptLooseStack = canAcceptLoose,
            HasRemainingMassCapacity = inventory?.RemainingMassGrams > 0L
        };
    }
}

public static class GameplayFlowDiagnosticsBuilder
{
    private const int MaxVisibleOrders = 7;
    private const int MaxVisibleOperationBlocks = 4;

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

        foreach (GameplayFlowWorkerSnapshot worker in activeWorkers
                     .Where(value =>
                         value.CurrentOperationBlock.HasObservation)
                     .OrderBy(value => value.Name, StringComparer.Ordinal)
                     .ThenBy(
                         value => value.CurrentOperationBlock.TargetStableId,
                         StringComparer.Ordinal)
                     .Take(MaxVisibleOperationBlocks))
        {
            items.Add(BuildOperationBlockDiagnostic(worker));
        }

        WorldItemStackSnapshot[] looseStacks = allStacks
            .Where(stack => stack.State == WorldItemStackState.Loose)
            .ToArray();
        int reservedLooseCount = looseStacks.Count(stack => stack.HasReservations);
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
                && stack.AvailableQuantity > 0)
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
                && warehouse.HasRemainingMassCapacity
                && warehouse.CanAcceptLooseStack))
        {
            return Critical(
                "바닥 물류",
                $"{totals} · 받아들일 빈 창고 공간이 없습니다. 창고 용량과 허용 품목을 확인하세요.");
        }

        GameplayFlowWorkerSnapshot observedHaulBlock = workers
            .Where(worker =>
                IsObservedHaulAction(worker.CurrentOperationBlock)
                && worker.CurrentOperationBlock.HasCurrentBlock)
            .OrderBy(worker => worker.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (observedHaulBlock != null)
        {
            CharacterOperationBlockSnapshot block =
                observedHaulBlock.CurrentOperationBlock;
            if (block.Axis == CharacterOperationBlockAxis.Access
                || block.Failure.Kind is AIActionFailureKind.NoPath
                    or AIActionFailureKind.NoGrid
                    or AIActionFailureKind.DestinationOccupied)
            {
                return Critical(
                    "바닥 물류",
                    $"{totals} · 실제 운반 시도 차단: "
                    + FormatOperationBlock(block, observedHaulBlock.DecisionSchedule));
            }

            return Warning(
                "바닥 물류 실행 대기",
                $"{totals} · 실제 운반 관측: "
                + FormatOperationBlock(block, observedHaulBlock.DecisionSchedule));
        }

        GameplayFlowWorkerSnapshot unconfirmedHaulObservation = workers
            .Where(worker =>
                IsObservedHaulAction(worker.CurrentOperationBlock)
                && !worker.CurrentOperationBlock.HasCurrentBlock)
            .OrderBy(worker => worker.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (unconfirmedHaulObservation != null)
        {
            return Warning(
                "바닥 물류 경로 현재성 미확인",
                $"{totals} · 마지막 실제 운반 실패는 관측 이후 원본 상태가 "
                + "바뀌었거나 아직 재평가되지 않았습니다: "
                + FormatOperationBlock(
                    unconfirmedHaulObservation.CurrentOperationBlock,
                    unconfirmedHaulObservation.DecisionSchedule));
        }

        if (workers.Any(worker =>
                worker.PathSearchDeferred
                || worker.DecisionSchedule.IsRetry
                    && worker.DecisionSchedule.RetryKind
                        == CharacterAiRetryKind.PathSearch))
        {
            return Warning(
                "바닥 물류 경로 계산 중",
                $"{totals} · 실제 경로 계산 재개를 기다리고 있습니다.");
        }

        return Warning(
            "바닥 물류 경로 미확인",
            $"{totals} · 운반 가능 직원 {availableHaulers}명"
            + " · 아직 실제 운반 시도의 경로 결과가 없습니다.");
    }

    private static GameplayFlowDiagnosticItem BuildOperationBlockDiagnostic(
        GameplayFlowWorkerSnapshot worker)
    {
        CharacterOperationBlockSnapshot block = worker.CurrentOperationBlock;
        if (!block.HasCurrentBlock)
        {
            return new GameplayFlowDiagnosticItem
            {
                Severity = GameplayFlowDiagnosticSeverity.Warning,
                Title = $"[현재성 미확인] {worker.Name} · {block.TargetLabel}",
                Detail = FormatOperationBlock(block, worker.DecisionSchedule),
                IsCurrentOperationBlock = false,
                TargetStableId = block.TargetStableId,
                BlockAxis = block.Axis,
                FailureKind = block.Failure.Kind,
                DomainFailureCode = block.DomainFailure.Code
            };
        }

        bool retryPending = worker.DecisionSchedule.IsRetry
            || block.RetryKind == CharacterAiRetryKind.ExecutorDeferred;
        return new GameplayFlowDiagnosticItem
        {
            Severity = retryPending
                ? GameplayFlowDiagnosticSeverity.Warning
                : GameplayFlowDiagnosticSeverity.Critical,
            Title = retryPending
                ? $"[재시도 대기] {worker.Name} · {block.TargetLabel}"
                : $"[막힘] {worker.Name} · {block.TargetLabel}",
            Detail = FormatOperationBlock(block, worker.DecisionSchedule),
            IsCurrentOperationBlock = true,
            TargetStableId = block.TargetStableId,
            BlockAxis = block.Axis,
            FailureKind = block.Failure.Kind,
            DomainFailureCode = block.DomainFailure.Code
        };
    }

    public static string FormatOperationBlock(
        CharacterOperationBlockSnapshot block,
        CharacterAiDecisionScheduleObservation schedule)
    {
        if (!block.HasObservation)
        {
            return "실행 차단 상태 미확인";
        }

        string failure = block.DomainFailure.IsFailure
            ? block.DomainFailure.Code.ToString()
            : block.Failure.Kind.ToString();
        string retry = schedule.IsRetry
            ? $"재시도 {FormatRetryKind(schedule.RetryKind)} "
                + $"{schedule.RemainingSeconds:0.0}초 후"
            : block.RetryKind == CharacterAiRetryKind.ExecutorDeferred
                ? "실행기 재시도 대기 · 시각 미확인"
                : "재시도 일정 없음";
        string currentness = block.IsCurrentnessConfirmed
            ? string.Empty
            : $"관측 {block.ObservedAt:0.0} · 현재성 미확인 · ";
        return currentness + $"{FormatBlockAxis(block.Axis)} · {failure}"
            + $" · 작업 {block.ActionStableId} · 실행 #{block.ActionEpoch}"
            + $" · {retry}";
    }

    public static string FormatBlockAxis(CharacterOperationBlockAxis axis)
    {
        return axis switch
        {
            CharacterOperationBlockAxis.Power => "전력",
            CharacterOperationBlockAxis.Water => "물",
            CharacterOperationBlockAxis.Fuel => "연료",
            CharacterOperationBlockAxis.Cleanliness => "청결",
            CharacterOperationBlockAxis.Environment => "환경",
            CharacterOperationBlockAxis.Tool => "도구",
            CharacterOperationBlockAxis.Access => "접근",
            CharacterOperationBlockAxis.Identity => "작업자 정체성",
            CharacterOperationBlockAxis.Risk => "위험",
            CharacterOperationBlockAxis.Materials => "재료",
            CharacterOperationBlockAxis.OutputSpace => "출력 공간",
            CharacterOperationBlockAxis.Facility => "시설",
            _ => "원인 미확인"
        };
    }

    public static string FormatRetryKind(CharacterAiRetryKind retryKind)
    {
        return retryKind switch
        {
            CharacterAiRetryKind.CandidateEvaluation => "후보 평가",
            CharacterAiRetryKind.PathSearch => "경로 계산",
            CharacterAiRetryKind.FailureCooldown => "실패 쿨다운",
            CharacterAiRetryKind.DecisionRetry => "판단",
            CharacterAiRetryKind.ExecutorDeferred => "실행기",
            _ => "없음"
        };
    }

    private static bool IsObservedHaulAction(
        CharacterOperationBlockSnapshot block)
    {
        return block.HasObservation
            && (string.Equals(
                    block.ActionStableId,
                    BuiltInWorkTypeIds.Haul.Value,
                    StringComparison.Ordinal)
                || string.Equals(
                    block.ActionStableId,
                    BuiltInWorkTypeIds.Restock.Value,
                    StringComparison.Ordinal));
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

        if (order.status == WorkOrderStatus.WaitingForEligibleWorker)
        {
            return Critical(
                target,
                "작업자 지정 조건을 만족하는 주민이 없습니다. 주문의 능력치·숙련·제외 조건을 확인하세요.");
        }

        if (order.status == WorkOrderStatus.TargetCurrentlyUnreachable)
        {
            return Critical(
                target,
                "현재 작업자·시설·도구 조합으로 목표 품질에 도달할 수 없습니다.");
        }

        if (order.status == WorkOrderStatus.WaitingForOutputSpace)
        {
            return Warning(
                target,
                "완성품 또는 회수 재료를 놓을 출력 공간이 없습니다.");
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
        foreach (WorkOrderItemMaterialSaveData material in order.itemMaterials
                     ?? new List<WorkOrderItemMaterialSaveData>())
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
                .Where(stack => IsPendingFor(order, material.itemId, stack))
                .Sum(stack => stack.Quantity);
            int available = stacks
                .Where(stack => IsAvailableSource(material.itemId, stack))
                .Sum(stack => stack.Quantity);
            string materialName = material.itemId;
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
        string itemId,
        WorldItemStackSnapshot stack)
    {
        return stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
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
        string itemId,
        WorldItemStackSnapshot stack)
    {
        return stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
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
            WorkOrderStatus.WaitingForEligibleWorker =>
                GameplayFlowDiagnosticSeverity.Critical,
            WorkOrderStatus.TargetCurrentlyUnreachable =>
                GameplayFlowDiagnosticSeverity.Critical,
            WorkOrderStatus.WaitingForOutputSpace =>
                GameplayFlowDiagnosticSeverity.Warning,
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
