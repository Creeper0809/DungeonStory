using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            VerifyUnobservedHaulIsUnconfirmed();
            VerifyGenuinelyBlockedLooseStack();
            VerifyObservedBlockProjectionAndNormalization();
            VerifyNormalScheduleIsNotRetry();
            VerifyPassiveQuerySource();
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
        stack.ReservedQuantity = stack.Quantity;

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
        GameplayFlowWorkerSnapshot worker = CreateWorker(
            haul: true,
            construct: true);
        worker.CurrentOperationBlock = CreateOperationBlock(
            BuiltInWorkTypeIds.Haul,
            CharacterOperationBlockAxis.Access,
            AIActionFailureKind.NoPath,
            DomainFailure.None,
            retryKind: CharacterAiRetryKind.FailureCooldown);
        GameplayFlowDiagnosticItem diagnostic = BuildLooseFlow(
            CreateGeneralStack(WorldItemStackState.Loose, string.Empty).Single(),
            worker,
            CreateWarehouse(canAcceptLooseStack: true)).Items
            .First(item => item.Title.Contains("바닥 물류"));
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Critical
            && diagnostic.Detail.Contains("실제 운반 시도 차단")
            && diagnostic.Detail.Contains("NoPath"),
            "genuinely unreachable loose stack did not expose the blocked route");
    }

    private static void VerifyUnobservedHaulIsUnconfirmed()
    {
        GameplayFlowDiagnosticItem diagnostic = BuildLooseFlow(
            CreateGeneralStack(WorldItemStackState.Loose, string.Empty).Single(),
            CreateWorker(haul: true, construct: true),
            CreateWarehouse(canAcceptLooseStack: true)).Items.Single();
        Require(
            diagnostic.Severity == GameplayFlowDiagnosticSeverity.Warning
            && diagnostic.Title.Contains("경로 미확인")
            && diagnostic.Detail.Contains("실제 운반 시도"),
            "missing haul observation was presented as a confirmed block");
    }

    private static void VerifyObservedBlockProjectionAndNormalization()
    {
        GameObject brainRoot = new GameObject("WIM046_Brain");
        GameObject targetRoot = new GameObject("WIM046_Target");
        AIWork actionSet = ScriptableObject.CreateInstance<AIWork>();
        try
        {
            AIBrain brain = brainRoot.AddComponent<AIBrain>();
            BuildableObject target = targetRoot.AddComponent<BuildableObject>();
            SetPrivateField(target, "persistentInstanceId", "building:test");
            SetPrivateField(actionSet, "workType", FacilityWorkType.Operate);
            actionSet.actionName = "테스트 운용";
            brain.bestAction = new AIAction(
                actionSet,
                AIActionPlan.AtDestination(target));
            SetPrivateField(brain, "currentActionEpoch", 7L);
            SetPrivateField(brain, "actionEpochLive", true);
            InvokePrivate(
                brain,
                "RecordCurrentOperationBlock",
                actionSet,
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "typed-test-failure",
                    target),
                new DomainFailure(FailureCode.AutomationUnpowered),
                CharacterOperationBlockAxis.Power,
                CharacterAiRetryKind.FailureCooldown,
                false,
                true);

            GameplayFlowWorkerSnapshot worker = CreateWorker(
                haul: false,
                construct: true);
            worker.CurrentOperationBlock = brain.CaptureCurrentOperationBlock();
            worker.DecisionSchedule = new CharacterAiDecisionScheduleObservation(
                isScheduled: true,
                dueTime: 14f,
                observedAt: 13.5f,
                retryKind: CharacterAiRetryKind.FailureCooldown);

            CharacterAiRuntimeGateSnapshot queryStart =
                brain.CaptureRuntimeDiagnostics().Gate;
            GameplayFlowDiagnosticsSnapshot blocked =
                GameplayFlowDiagnosticsBuilder.Build(
                    Array.Empty<WorkOrderSaveData>(),
                    Array.Empty<WorldItemStackSnapshot>(),
                    new[] { worker });
            GameplayFlowDiagnosticsBuilder.Build(
                Array.Empty<WorkOrderSaveData>(),
                Array.Empty<WorldItemStackSnapshot>(),
                new[] { worker });
            CharacterAiRuntimeGateSnapshot queryEnd =
                brain.CaptureRuntimeDiagnostics().Gate;
            GameplayFlowDiagnosticItem item = blocked.Items.Single(
                value => value.IsCurrentOperationBlock);
            Require(
                item.BlockAxis == CharacterOperationBlockAxis.Power
                && item.DomainFailureCode == FailureCode.AutomationUnpowered
                && item.TargetStableId == "building:test"
                && item.Detail.Contains("실패 쿨다운")
                && item.Detail.Contains("0.5초 후"),
                "typed execution block was not projected to the flow surface");
            Require(
                queryEnd.PathRequests == queryStart.PathRequests
                && queryEnd.PathResults == queryStart.PathResults
                && queryEnd.ReservationAcquires == queryStart.ReservationAcquires
                && queryEnd.ReservationReleases == queryStart.ReservationReleases
                && queryEnd.RetrySchedules == queryStart.RetrySchedules
                && queryEnd.RetryAttempts == queryStart.RetryAttempts,
                "repeated flow projection mutated AI path/reservation/retry state");

            brain.InvalidateQueuedActionForNextDecision();
            Require(
                brain.CaptureCurrentOperationBlock().HasCurrentBlock,
                "queue-only invalidation cleared the active failure observation");

            brain.ClearSelectedActionForIdle("test-normalized");
            worker.CurrentOperationBlock = brain.CaptureCurrentOperationBlock();
            GameplayFlowDiagnosticsSnapshot normalized =
                GameplayFlowDiagnosticsBuilder.Build(
                    Array.Empty<WorkOrderSaveData>(),
                    Array.Empty<WorldItemStackSnapshot>(),
                    new[] { worker });
            Require(
                normalized.Items.All(value => !value.IsCurrentOperationBlock),
                "normalized current block remained on the flow surface");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(brainRoot);
            UnityEngine.Object.DestroyImmediate(targetRoot);
            UnityEngine.Object.DestroyImmediate(actionSet);
        }
    }

    private static void VerifyNormalScheduleIsNotRetry()
    {
        CharacterAiDecisionScheduleObservation normal =
            new CharacterAiDecisionScheduleObservation(
                isScheduled: true,
                dueTime: 22f,
                observedAt: 20f,
                retryKind: CharacterAiRetryKind.None);
        CharacterAiDecisionScheduleObservation retry =
            new CharacterAiDecisionScheduleObservation(
                isScheduled: true,
                dueTime: 20.25f,
                observedAt: 20f,
                retryKind: CharacterAiRetryKind.PathSearch);
        Require(
            normal.IsScheduled && !normal.IsRetry
            && retry.IsRetry
            && retry.RetryKind == CharacterAiRetryKind.PathSearch,
            "normal cadence and failure/pending retry were conflated");
    }

    private static void VerifyPassiveQuerySource()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "Scripts/Services/Operation/GameplayFlowDiagnostics.cs");
        string source = File.ReadAllText(sourcePath);
        Require(
            !source.Contains(".HasAvailableHaulJob(", StringComparison.Ordinal)
            && !source.Contains(".AssessStart(", StringComparison.Ordinal)
            && !source.Contains(".CanConsume(", StringComparison.Ordinal)
            && !source.Contains(".NextRandom", StringComparison.Ordinal)
            && source.Contains(
                ".CaptureCurrentOperationBlock()",
                StringComparison.Ordinal),
            "flow query contains an active gameplay probe instead of snapshot reads");
    }

    private static CharacterOperationBlockSnapshot CreateOperationBlock(
        WorkTypeId workTypeId,
        CharacterOperationBlockAxis axis,
        AIActionFailureKind failureKind,
        DomainFailure domainFailure,
        CharacterAiRetryKind retryKind)
    {
        return new CharacterOperationBlockSnapshot(
            workTypeId.Value,
            workTypeId.Value,
            "building:test",
            "테스트 시설",
            actionEpoch: 7L,
            failure: AIActionFailure.Create(failureKind, "typed-test-failure"),
            domainFailure: domainFailure,
            axis: axis,
            retryKind: retryKind,
            isCurrent: true,
            hasKnownExpiry: retryKind == CharacterAiRetryKind.FailureCooldown,
            expiresAt: 14f);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }
        field.SetValue(target, value);
    }

    private static void InvokePrivate(
        object target,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new MissingMethodException(target.GetType().FullName, methodName);
        }
        method.Invoke(target, arguments);
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
            HasRemainingMassCapacity = canAcceptLooseStack
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
