using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameplayFlowDiagnosticsDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Operation/Run Gameplay Flow Diagnostics")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            throw new InvalidOperationException("Gameplay flow diagnostics scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        try
        {
            VerifyMissingMaterial();
            VerifyMissingHauler();
            VerifyRecoverableDeliveryWait();
            VerifyMissingWorkRole();
            VerifyInProgressOrder();
            VerifyReservedLooseStackIsInTransit();
            VerifyDeferredPathIsNotBlocked();
            VerifyGenuinelyBlockedLooseStack();
            if (logSuccess)
            {
                Debug.Log("[GameplayFlowDiagnostics] PASS");
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameplayFlowDiagnostics] FAIL: {exception.Message}");
            return false;
        }
    }

    private static void VerifyMissingMaterial()
    {
        GameplayFlowDiagnosticItem diagnostic = Build(
            CreateOrder(WorkOrderStatus.WaitingForMaterials),
            Array.Empty<WorldItemStackSnapshot>(),
            CreateWorker(haul: true, construct: true)).Items.First();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Critical
            && diagnostic.Detail.Contains("재료 부족")
            && diagnostic.Detail.Contains("상점·배송·생산"),
            "missing material did not expose a recovery route");
    }

    private static void VerifyMissingHauler()
    {
        GameplayFlowDiagnosticItem diagnostic = Build(
            CreateOrder(WorkOrderStatus.WaitingForMaterials),
            CreateGeneralStack(WorldItemStackState.Stored, string.Empty),
            CreateWorker(haul: false, construct: true)).Items.First();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Critical
            && diagnostic.Detail.Contains("운반 담당자")
            && diagnostic.Detail.Contains("운반을 활성화"),
            "missing hauler did not expose work-priority recovery");
    }

    private static void VerifyRecoverableDeliveryWait()
    {
        WorkOrderSaveData order = CreateOrder(WorkOrderStatus.WaitingForMaterials);
        GameplayFlowDiagnosticItem diagnostic = Build(
            order,
            CreateGeneralStack(WorldItemStackState.Loose, order.materialDestinationId),
            CreateWorker(haul: true, construct: true)).Items.First();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Warning
            && diagnostic.Title.Contains("운반 대기")
            && diagnostic.Detail.Contains("운반 가능 직원 1명"),
            "recoverable delivery wait was classified incorrectly");
    }

    private static void VerifyMissingWorkRole()
    {
        GameplayFlowDiagnosticItem diagnostic = Build(
            CreateOrder(WorkOrderStatus.Ready),
            Array.Empty<WorldItemStackSnapshot>(),
            CreateWorker(haul: true, construct: false)).Items.First();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Critical
            && diagnostic.Detail.Contains("건설 담당자")
            && diagnostic.Detail.Contains("건설을 활성화"),
            "missing work role did not expose work-priority recovery");
    }

    private static void VerifyInProgressOrder()
    {
        WorkOrderSaveData order = CreateOrder(WorkOrderStatus.InProgress);
        order.completedWork = 25f;
        order.reservedWorkerPersistentId = "worker:one";
        GameplayFlowDiagnosticsSnapshot snapshot = Build(
            order,
            Array.Empty<WorldItemStackSnapshot>(),
            CreateWorker(haul: true, construct: true));
        GameplayFlowDiagnosticItem diagnostic = snapshot.Items.First();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Info
            && diagnostic.Detail.Contains("진행 25%")
            && diagnostic.Detail.Contains("가능한 직원 1명")
            && snapshot.BlockedOrderCount == 0,
            "in-progress order summary was incorrect");
    }

    private static void VerifyReservedLooseStackIsInTransit()
    {
        WorldItemStackSnapshot stack = CreateGeneralStack(
                WorldItemStackState.Loose,
                string.Empty)
            .Single();
        stack.ReservedByPersistentId = "worker:one";

        GameplayFlowDiagnosticItem diagnostic = BuildLooseFlow(
            stack,
            CreateWorker(haul: true, construct: true),
            CreateWarehouse(canAcceptLooseStack: true)).Items.Single();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Warning
            && diagnostic.Title.Contains("운반 중")
            && !diagnostic.Detail.Contains("막혔"),
            "reserved loose stack was incorrectly reported as a blocked route");
    }

    private static void VerifyDeferredPathIsNotBlocked()
    {
        GameplayFlowWorkerSnapshot worker = CreateWorker(haul: true, construct: true);
        worker.PathSearchDeferred = true;

        GameplayFlowDiagnosticItem diagnostic = BuildLooseFlow(
            CreateGeneralStack(WorldItemStackState.Loose, string.Empty).Single(),
            worker,
            CreateWarehouse(canAcceptLooseStack: true)).Items.Single();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Warning
            && diagnostic.Title.Contains("경로 계산 중")
            && !diagnostic.Detail.Contains("막혔"),
            "deferred path search was incorrectly reported as a blocked route");
    }

    private static void VerifyGenuinelyBlockedLooseStack()
    {
        GameplayFlowDiagnosticItem diagnostic = BuildLooseFlow(
            CreateGeneralStack(WorldItemStackState.Loose, string.Empty).Single(),
            CreateWorker(haul: true, construct: true),
            CreateWarehouse(canAcceptLooseStack: true)).Items.Single();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Critical
            && diagnostic.Detail.Contains("이동 경로가 막혔"),
            "genuinely unreachable loose stack did not expose the blocked route");
    }

    private static GameplayFlowDiagnosticsSnapshot Build(
        WorkOrderSaveData order,
        IEnumerable<WorldItemStackSnapshot> stacks,
        GameplayFlowWorkerSnapshot worker)
    {
        return GameplayFlowDiagnosticsBuilder.Build(
            new[] { order },
            stacks,
            new[] { worker });
    }

    private static GameplayFlowDiagnosticsSnapshot BuildLooseFlow(
        WorldItemStackSnapshot stack,
        GameplayFlowWorkerSnapshot worker,
        GameplayFlowWarehouseSnapshot warehouse)
    {
        return GameplayFlowDiagnosticsBuilder.Build(
            Array.Empty<WorkOrderSaveData>(),
            new[] { stack },
            new[] { worker },
            new[] { warehouse });
    }

    private static WorkOrderSaveData CreateOrder(WorkOrderStatus status)
    {
        return new WorkOrderSaveData
        {
            workOrderId = "order:test",
            workTypeId = BuiltInWorkTypeIds.Construct.Value,
            gridX = 4,
            gridY = 2,
            requiredWork = 100f,
            materialDestinationId = "construction:test",
            status = status,
            itemMaterials = new List<WorkOrderItemMaterialSaveData>
            {
                new WorkOrderItemMaterialSaveData
                {
                    itemId = "material:lumber",
                    required = 5
                }
            }
        };
    }

    private static IEnumerable<WorldItemStackSnapshot> CreateGeneralStack(
        WorldItemStackState state,
        string destinationId)
    {
        return new[]
        {
            new WorldItemStackSnapshot
            {
                StackId = "stack:test",
                ItemId = "material:lumber",
                DisplayName = "일반 재료",
                StockCategory = StockCategory.General,
                Quantity = 5,
                UnitWeight = 1f,
                State = state,
                DestinationId = destinationId
            }
        };
    }

    private static GameplayFlowWorkerSnapshot CreateWorker(bool haul, bool construct)
    {
        List<string> enabled = new List<string>();
        if (haul)
        {
            enabled.Add(BuiltInWorkTypeIds.Haul.Value);
        }

        if (construct)
        {
            enabled.Add(BuiltInWorkTypeIds.Construct.Value);
        }

        return new GameplayFlowWorkerSnapshot
        {
            Name = "테스트 직원",
            CanRunAi = true,
            HaulEnabled = haul,
            EnabledWorkTypeIds = enabled
        };
    }

    private static GameplayFlowWarehouseSnapshot CreateWarehouse(bool canAcceptLooseStack)
    {
        return new GameplayFlowWarehouseSnapshot
        {
            Name = "테스트 창고",
            HasInventory = true,
            CanAcceptLooseStack = canAcceptLooseStack,
            RemainingCapacity = canAcceptLooseStack ? 20 : 0
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
