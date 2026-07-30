using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WorkTaskExecutor
{
    private const float RestockPickupWaitSeconds = 0.35f;
    private const float MinimumNaturalExteriorWorkSeconds = 3.2f;

    private readonly AbilityWork work;
    private readonly WorkTargetSelector targetSelector;
    private readonly IWorkExecutionHandlerRegistry executionHandlers;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private readonly IPaidFacilityContractRuntime paidFacilityContracts;

    public WorkTaskExecutor(
        AbilityWork work,
        WorkTargetSelector targetSelector,
        IWorkExecutionHandlerRegistry executionHandlers = null,
        IWorkOrderRuntime workOrderRuntime = null,
        IWorkAmountCalculator workAmountCalculator = null,
        IGameClock gameClock = null,
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService = null,
        IPaidFacilityContractRuntime paidFacilityContracts = null)
    {
        this.work = work;
        this.targetSelector = targetSelector;
        this.executionHandlers = executionHandlers;
        this.workOrderRuntime = workOrderRuntime;
        this.workAmountCalculator = workAmountCalculator;
        this.gameClock = gameClock ?? new UnityGameClock();
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.paidFacilityContracts = paidFacilityContracts;
    }

    public IEnumerator Work(int runId)
    {
        CharacterActor actor = work.WorkerActor;
        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;

        work.EnsureWorkReferences();
        AbilityMove move = work.WorkerMove;
        Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (move == null || grid == null)
        {
            WorkDebugLog.LogEnd(actor, "이동 정보 없음");
            actor?.AddActivity(CharacterActivityEvent.Work(
                work.AssignedWorkType,
                CharacterActivityOutcomes.Failed,
                "작업 실패: 이동 정보 없음",
                work.assignedShop,
                reasonCode: "missing-movement",
                bubbleEligible: true));
            work.isWorking = false;
            EndAiAction(actor, currentAction);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        work.isWorking = true;
        if (work.AssignedWorkType == FacilityWorkType.Restock)
        {
            yield return ExecuteRestockHaulWork(runId, currentAction, move, grid);
            FinishWorkRun(actor, currentAction);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        yield return move.MoveByCurrentBestActionPath();
        if (ShouldAbortWorkRun(runId, actor) || !work.isWorking)
        {
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        BuildableObject assignedTarget = work.assignedShop;
        if (HasReachedAssignedWorkTarget(actor, grid)
            && assignedTarget is IWorkableFacility facility)
        {
            yield return facility.AllocateWorker(actor);
            if (ShouldAbortWorkRun(runId, actor)
                || !work.isWorking
                || work.assignedShop != assignedTarget)
            {
                facility.DeallocateWorker(actor);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            currentAction?.ReleaseReservation(actor);
            FacilityWorkType workType = work.AssignedWorkType;
            WorkTypeDefinition workDefinition = WorkTypeCatalog.TryGet(
                    workType,
                    out WorkTypeDefinition resolvedWorkDefinition)
                ? resolvedWorkDefinition
                : null;
            WorkTypeId workTypeId = workDefinition?.WorkTypeId ?? default;
            string paidOrderKey =
                $"work:{workTypeId.Value}:{actor?.Identity?.PersistentId}:{runId}";
            if (workOrderRuntime != null
                && workTypeId.IsValid
                && workOrderRuntime.TryGetOrderFor(
                    assignedTarget,
                    workTypeId,
                    out WorkOrderProgressState paidOrder))
            {
                paidOrderKey = paidOrder.WorkOrderId;
            }

            if (paidFacilityContracts != null
                && !paidFacilityContracts.TryChargeOrder(
                    assignedTarget,
                    paidOrderKey,
                    out string paidFailureReason))
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{workDefinition?.DisplayName ?? "작업"} 중단: {paidFailureReason}",
                    assignedTarget,
                    reasonCode: "paid-facility-order",
                    bubbleEligible: true));
                facility.DeallocateWorker(actor);
                work.isWorking = false;
                EndAiAction(actor, currentAction);
                work.ClearActiveWorkRoutine(runId);
                yield break;
            }

            CharacterSkillRuntimeEffects.BeginWork(
                actor,
                assignedTarget,
                workTypeId,
                $"work:{runId}:{assignedTarget.GetInstanceID()}:started");
            WorkDebugLog.LogStarted(actor);
            bool completedImmediately = false;
            bool completedSuccessfully = true;
            bool completionEffectsAlreadyApplied = false;
            if (workOrderRuntime != null
                && workTypeId.IsValid
                && workOrderRuntime.TryGetOrderFor(assignedTarget, workTypeId, out _))
            {
                yield return ExecuteWorkOrderRoutine(
                    runId,
                    actor,
                    assignedTarget,
                    workType,
                    workDefinition,
                    (success, appliedEffects) =>
                    {
                        completedSuccessfully = success;
                        completionEffectsAlreadyApplied = appliedEffects;
                    });
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(actor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }
            else if (TryGetExteriorWorkSeconds(assignedTarget, actor, workTypeId, out float exteriorWorkSeconds))
            {
                yield return ExecuteWorkAmountLoop(
                    runId,
                    actor,
                    assignedTarget,
                    workType,
                    exteriorWorkSeconds,
                    WorkTaskCatalog.GetLegacyDisplayName(workType));
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(actor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }
            else if (executionHandlers != null
                && workDefinition != null
                && executionHandlers.TryGet(
                    workTypeId,
                    out IWorkExecutionHandler executionHandler))
            {
                WorkExecutionResult executionResult = new WorkExecutionResult();
                WorkExecutionContext executionContext = new WorkExecutionContext(
                    runId,
                    work,
                    actor,
                    assignedTarget,
                    workTypeId,
                    (requiredWork, label, extraMultiplier) => ExecuteWorkAmountLoop(
                        runId,
                        actor,
                        assignedTarget,
                        workType,
                        requiredWork,
                        label,
                        extraMultiplier),
                    () => CanContinueTimedWork(runId, actor) && work.isWorking,
                    (
                        requiredWork,
                        completedWork,
                        label,
                        extraMultiplier,
                        applyDelta) => ExecutePersistentWorkAmountLoop(
                            runId,
                            actor,
                            assignedTarget,
                            workType,
                            requiredWork,
                            completedWork,
                            label,
                            applyDelta,
                            extraMultiplier));
                yield return executionHandler.Execute(executionContext, executionResult);
                completedSuccessfully = executionResult.CompletedSuccessfully;
                completionEffectsAlreadyApplied =
                    executionResult.CompletionEffectsAlreadyApplied;
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(actor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }

            if (!completedImmediately)
            {
                work.StartCheckActionWork(runId);
                yield return new WaitUntil(() => !work.IsActiveWorkRun(runId) || !work.isWorking);
                if (!work.IsActiveWorkRun(runId))
                {
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                work.ClearActiveWorkCheckRoutine(runId);
                completedSuccessfully = work.LastWorkRunCompleted;
            }
            else
            {
                work.isWorking = false;
                WorkDebugLog.LogEnd(actor, "작업량 완료");
            }

            if (completedSuccessfully)
            {
                actor.Progression?.AddExperience(5);
                CharacterSkillRuntimeEffects.TriggerWorkCompleted(
                    actor,
                    assignedTarget,
                    workTypeId,
                    $"work:{runId}:{assignedTarget.GetInstanceID()}:completed");
                if (!completionEffectsAlreadyApplied)
                {
                    ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                        actor,
                        assignedTarget,
                        workTypeId);
                    roomEnvironmentExperienceService?.Apply(new RoomEnvironmentExperienceEvent(
                        actor,
                        assignedTarget,
                        RoomExperienceActivity.Work,
                        workTypeId));
                }
            }

            actor?.AiMemory?.RecordWork(
                workTypeId,
                assignedTarget,
                completedSuccessfully,
                $"{WorkTaskCatalog.GetLegacyDisplayName(workType)} {(completedSuccessfully ? "완료" : "실패")}: {assignedTarget.name}");
            CharacterSkillRuntimeEffects.EndWork(actor);
            bool wasPriorityTarget = work.assignedShop == work.PriorityWorkTarget;
            facility.DeallocateWorker(actor);
            currentAction?.ReleaseReservation(actor);
            work.AssignWork(null, FacilityWorkType.None);
            if (wasPriorityTarget)
            {
                work.ClearPriorityWorkTarget();
            }
        }
        else
        {
            work.isWorking = false;
            WorkDebugLog.LogEnd(actor, "작업 도달 실패");
            actor?.AddActivity(CharacterActivityEvent.Work(
                work.AssignedWorkType,
                CharacterActivityOutcomes.Failed,
                "작업 실패: 작업 도달 실패",
                assignedTarget,
                reasonCode: "target-unreachable",
                bubbleEligible: true));
            actor?.AiMemory?.RecordWork(
                work.AssignedWorkTypeId,
                assignedTarget,
                false,
                $"작업 도달 실패: {(assignedTarget != null ? assignedTarget.name : "대상 없음")}");
            currentAction?.ReleaseReservation(actor);
        }

        EndAiAction(actor, currentAction);
        work.ClearActiveWorkRoutine(runId);
    }

    private bool HasReachedAssignedWorkTarget(CharacterActor actor, Grid grid)
    {
        if (actor == null || grid == null || work.assignedShop == null)
        {
            return false;
        }

        GridCell currentCell = grid.GetGridCell(grid.GetXY(work.transform.position));
        return currentCell != null && currentCell.ContainsOccupant(work.assignedShop);
    }

    private IEnumerator ExecuteRestockHaulWork(
        int runId,
        AIAction currentAction,
        AbilityMove move,
        Grid grid)
    {
        CharacterActor actor = work.WorkerActor;
        BuildableObject restockTarget = work.assignedShop;
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            restockTarget,
            BuiltInWorkTypeIds.Restock,
            $"work:{runId}:{(restockTarget != null ? restockTarget.GetInstanceID() : 0)}:restock-started");
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(BuiltInWorkTypeIds.Restock)
            / Mathf.Max(0.1f, CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor));
        if (restockTarget is not IRestockableFacility restockable)
        {
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                "보충 실패: 재고를 받을 수 없는 시설",
                restockTarget,
                reasonCode: "target-not-restockable",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        if (!TryCreateRestockHaulPlan(
            actor,
            grid,
            restockTarget,
            restockable,
            out BuildableObject warehouseBuilding,
            out IWarehouseFacility warehouse,
            out SaleItem saleItem,
            out int loadAmount,
            out Queue<GridMoveStep> pathToWarehouse,
            out string failureReason))
        {
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                $"보충 실패: {failureReason}",
                restockTarget,
                reasonCode: failureReason,
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Restock,
            CharacterActivityOutcomes.Progress,
            $"보충 이동: {warehouseBuilding.name} -> {restockTarget.name}",
            restockTarget,
            reasonCode: "moving-to-stock"));
        yield return move.MoveByPath(pathToWarehouse, currentAction);
        if (ShouldAbortWorkRun(runId, actor))
        {
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        int carriedAmount = 0;
        for (int i = 0; i < loadAmount; i++)
        {
            Vector3 pickupPosition = GetWarehousePickupWorldPosition(grid, warehouseBuilding, i, loadAmount);
            yield return move.Move2PosBySpeed(pickupPosition, 0.8f, currentAction);
            if (ShouldAbortWorkRun(runId, actor))
            {
                ReturnCarriedStock(warehouse, saleItem, carriedAmount);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            int withdrawn = warehouse.Inventory.Withdraw(saleItem.category, 1);
            if (withdrawn <= 0)
            {
                break;
            }

            carriedAmount += withdrawn;
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Progress,
                $"보충 적재: {saleItem.itemName} {carriedAmount}/{loadAmount}",
                warehouseBuilding,
                reasonCode: "loading-stock",
                quantity: carriedAmount));
            work.FloatingIconFeedbackService.Show(actor, saleItem.itemSprite, FloatingIconFeedbackDefaults.DefaultMaxWorldSize);
            yield return new WaitForSeconds(RestockPickupWaitSeconds * durationMultiplier);
            if (ShouldAbortWorkRun(runId, actor))
            {
                ReturnCarriedStock(warehouse, saleItem, carriedAmount);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
        }

        if (carriedAmount <= 0)
        {
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                "보충 실패: 창고 재고 부족",
                warehouseBuilding,
                reasonCode: "warehouse-stock-shortage",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        if (!TryGetPathToBuilding(grid, actor, restockTarget, out Queue<GridMoveStep> pathToShop))
        {
            ReturnCarriedStock(warehouse, saleItem, carriedAmount);
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Blocked,
                "보충 실패: 상점 경로 없음",
                restockTarget,
                reasonCode: "shop-path-missing",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        yield return move.MoveByPath(pathToShop, currentAction);
        if (ShouldAbortWorkRun(runId, actor))
        {
            ReturnCarriedStock(warehouse, saleItem, carriedAmount);
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        int restocked = restockable.ReceiveRestock(
            saleItem,
            carriedAmount,
            carriedAmount,
            out string resultMessage);
        int leftover = carriedAmount - restocked;
        if (leftover > 0)
        {
            ReturnCarriedStock(warehouse, saleItem, leftover);
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Restock,
            restocked > 0 ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Failed,
            restocked > 0
                ? $"보충 완료: {restockTarget.name} {resultMessage}"
                : $"보충 실패: {resultMessage}",
            restockTarget,
            reasonCode: resultMessage,
            quantity: restocked,
            bubbleEligible: restocked <= 0));
        if (restocked > 0)
        {
            CharacterSkillRuntimeEffects.TriggerWorkCompleted(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock,
                $"work:{runId}:{restockTarget.GetInstanceID()}:restock-completed");
        }

        actor?.AiMemory?.RecordWork(
            BuiltInWorkTypeIds.Restock,
            restockTarget,
            restocked > 0,
            restocked > 0
                ? $"보충 완료: {restockTarget.name}"
                : $"보충 실패: {restockTarget.name}");

        yield return new WaitForSeconds(0.5f * durationMultiplier);
        work.isWorking = false;
        WorkDebugLog.LogEnd(actor, "보충 완료");
    }

    private bool TryCreateRestockHaulPlan(
        CharacterActor actor,
        Grid grid,
        BuildableObject restockTarget,
        IRestockableFacility restockable,
        out BuildableObject warehouseBuilding,
        out IWarehouseFacility warehouse,
        out SaleItem saleItem,
        out int loadAmount,
        out Queue<GridMoveStep> pathToWarehouse,
        out string failureReason)
    {
        warehouseBuilding = null;
        warehouse = null;
        saleItem = null;
        loadAmount = 0;
        pathToWarehouse = null;
        failureReason = string.Empty;

        if (actor == null || grid == null || restockTarget == null || restockable == null)
        {
            failureReason = "보충 경로 정보 없음";
            return false;
        }

        Vector2Int startPos = work.WorkGridResolver.GetGridPosition(grid, actor);
        List<IWarehouseFacility> reachableWarehouses = targetSelector
            .FindReachableWarehouses(null)
            .Where((candidate) => candidate.HasWarehouseInventory && candidate.Inventory != null)
            .ToList();

        if (!restockable.TryFindRestockSource(
            reachableWarehouses,
            restockable.MissingStock,
            out warehouse,
            out saleItem,
            out loadAmount,
            out failureReason))
        {
            return false;
        }

        warehouseBuilding = warehouse as BuildableObject;
        if (warehouseBuilding == null)
        {
            failureReason = "창고 건물 정보 없음";
            return false;
        }

        pathToWarehouse = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            startPos,
            warehouseBuilding.centerPos,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(actor));
        if (pathToWarehouse == null)
        {
            failureReason = "창고 경로 없음";
            return false;
        }

        return true;
    }

    private static Vector3 GetWarehousePickupWorldPosition(
        Grid grid,
        BuildableObject warehouseBuilding,
        int pickupIndex,
        int pickupCount)
    {
        if (grid == null
            || warehouseBuilding == null
            || warehouseBuilding.buildPoses == null
            || warehouseBuilding.buildPoses.Count == 0)
        {
            return warehouseBuilding != null ? warehouseBuilding.transform.position : Vector3.zero;
        }

        int minX = warehouseBuilding.buildPoses.Min((pos) => pos.x);
        int maxX = warehouseBuilding.buildPoses.Max((pos) => pos.x);
        int slotCount = Mathf.Clamp(pickupCount, 1, Mathf.Max(1, maxX - minX + 1));
        int slot = pickupIndex % slotCount;
        if ((pickupIndex / slotCount) % 2 == 1)
        {
            slot = slotCount - 1 - slot;
        }

        Vector2 minWorld = grid.GetWorldPos(new Vector2Int(minX, warehouseBuilding.centerPos.y));
        Vector2 maxWorld = grid.GetWorldPos(new Vector2Int(maxX, warehouseBuilding.centerPos.y));
        float minWorldX = Mathf.Min(minWorld.x, maxWorld.x) + 0.15f;
        float maxWorldX = Mathf.Max(minWorld.x, maxWorld.x) - 0.15f;
        float t = slotCount <= 1 ? 0.5f : (slot + 0.5f) / slotCount;
        float x = minWorldX <= maxWorldX
            ? Mathf.Lerp(minWorldX, maxWorldX, t)
            : (minWorld.x + maxWorld.x) * 0.5f;

        return new Vector3(x, minWorld.y, warehouseBuilding.transform.position.z);
    }

    private bool TryGetPathToBuilding(
        Grid grid,
        CharacterActor actor,
        BuildableObject target,
        out Queue<GridMoveStep> path)
    {
        path = null;
        if (grid == null || actor == null || target == null)
        {
            return false;
        }

        Vector2Int startPos = work.WorkGridResolver.GetGridPosition(grid, actor);
        if (actor.PathSearchBroker == null)
        {
            return false;
        }

        path = actor.PathSearchBroker.GetMovePathTo(
            grid,
            startPos,
            target.centerPos,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(actor));
        return path != null;
    }

    private void ReturnCarriedStock(
        IWarehouseFacility warehouse,
        SaleItem saleItem,
        int amount)
    {
        if (warehouse == null
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null
            || saleItem == null
            || amount <= 0)
        {
            return;
        }

        warehouse.Inventory.Deposit(saleItem.category, amount);
        work.MarkFacilityDynamicStateDirty();
    }

    private IEnumerator ExecuteWorkOrderRoutine(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        WorkTypeDefinition workDefinition,
        Action<bool, bool> onCompleted)
    {
        bool completed = false;
        bool appliedCompletionEffects = false;
        if (target == null || workOrderRuntime == null)
        {
            onCompleted?.Invoke(false, false);
            yield break;
        }

        string label = workDefinition?.DisplayName ?? WorkTaskCatalog.GetLegacyDisplayName(workType);
        WorkTypeId workTypeId = workDefinition?.WorkTypeId ?? default;
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        while (CanContinueTimedWork(runId, actor)
            && work.isWorking
            && workOrderRuntime.TryGetOrderFor(target, workTypeId, out WorkOrderProgressState order)
            && order.Status != WorkOrderStatus.Completed
            && order.Status != WorkOrderStatus.Cancelled)
        {
            if (order.Status == WorkOrderStatus.WaitingForMaterials)
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{label} 대기: 재료가 아직 도착하지 않음",
                    target,
                    reasonCode: "waiting-for-materials",
                    value: order.ProgressRatio));
                yield return new WaitForSeconds(0.35f);
                if (!workOrderRuntime.RefreshMaterialsReady(target as ConstructionSite))
                {
                    continue;
                }
            }

            float deltaWork = CalculateWorkPerSecond(
                    actor,
                    target,
                    workTypeId,
                    durationMultiplier)
                * gameClock.DeltaTime;
            if (!workOrderRuntime.ApplyWork(
                    actor,
                    target,
                    workTypeId,
                    deltaWork,
                    out completed,
                    out appliedCompletionEffects,
                    out string message))
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{label} 중단: {message}",
                    target,
                    reasonCode: "work-order-blocked",
                    bubbleEligible: true));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            if (gameClock.Time - lastReportTime >= 0.75f
                && workOrderRuntime.TryGetOrderFor(target, workTypeId, out order))
            {
                lastReportTime = gameClock.Time;
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(order.ProgressRatio * 100f)}%", target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(order.ProgressRatio * 100f)}%",
                    target,
                    reasonCode: "work-progress",
                    value: order.ProgressRatio));
            }

            if (completed)
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Completed,
                    $"{label} 완료",
                    target,
                    reasonCode: "work-order-completed",
                    value: 1f));
                onCompleted?.Invoke(true, appliedCompletionEffects);
                yield break;
            }

            yield return null;
        }

        onCompleted?.Invoke(false, appliedCompletionEffects);
    }

    private IEnumerator ExecuteWorkAmountLoop(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        float requiredWork,
        string label,
        float extraMultiplier = 1f)
    {
        requiredWork = Mathf.Max(0.1f, requiredWork);
        label = string.IsNullOrWhiteSpace(label) ? WorkTaskCatalog.GetLegacyDisplayName(workType) : label;
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork))
        {
            actor?.Brain?.SetActionPhase($"{label} 100%", target);
            yield return null;
            yield break;
        }

        float completedWork = 0f;
        WorkTypeId workTypeId = WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : default;
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        while (completedWork + 0.001f < requiredWork
            && CanContinueTimedWork(runId, actor)
            && work.isWorking)
        {
            float tickDeltaTime = gameClock.DeltaTime > 0f
                ? gameClock.DeltaTime
                : 1f / 60f;
            float deltaWork = CalculateWorkPerSecond(
                    actor,
                    target,
                    workTypeId,
                    durationMultiplier)
                * Mathf.Max(0.05f, extraMultiplier)
                * tickDeltaTime;
            completedWork = Mathf.Min(requiredWork, completedWork + deltaWork);
            if (gameClock.Time - lastReportTime >= 0.75f)
            {
                lastReportTime = gameClock.Time;
                float ratio = Mathf.Clamp01(completedWork / requiredWork);
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(ratio * 100f)}%", target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(ratio * 100f)}%",
                    target,
                    reasonCode: "work-progress",
                    value: ratio));
            }

            yield return null;
        }
    }

    private IEnumerator ExecutePersistentWorkAmountLoop(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        float requiredWork,
        float completedWork,
        string label,
        Func<float, bool> applyDelta,
        float extraMultiplier = 1f)
    {
        requiredWork = Mathf.Max(0.1f, requiredWork);
        completedWork = Mathf.Clamp(completedWork, 0f, requiredWork);
        label = string.IsNullOrWhiteSpace(label)
            ? WorkTaskCatalog.GetLegacyDisplayName(workType)
            : label;

        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork))
        {
            float remainingWork = Mathf.Max(0f, requiredWork - completedWork);
            if (remainingWork > 0f)
            {
                applyDelta(remainingWork);
            }

            actor?.Brain?.SetActionPhase($"{label} 100%", target);
            yield return null;
            yield break;
        }

        WorkTypeId workTypeId = WorkTypeCatalog.TryGet(
                workType,
                out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : default;
        float durationMultiplier =
            work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;

        while (completedWork + 0.001f < requiredWork
            && CanContinueTimedWork(runId, actor)
            && work.isWorking)
        {
            float tickDeltaTime = gameClock.DeltaTime > 0f
                ? gameClock.DeltaTime
                : 1f / 60f;
            float deltaWork = Mathf.Min(
                requiredWork - completedWork,
                CalculateWorkPerSecond(
                        actor,
                        target,
                        workTypeId,
                        durationMultiplier)
                    * Mathf.Max(0.05f, extraMultiplier)
                    * tickDeltaTime);
            if (deltaWork <= 0f || !applyDelta(deltaWork))
            {
                yield break;
            }

            completedWork = Mathf.Min(requiredWork, completedWork + deltaWork);
            if (gameClock.Time - lastReportTime >= 0.75f)
            {
                lastReportTime = gameClock.Time;
                float ratio = Mathf.Clamp01(completedWork / requiredWork);
                actor?.Brain?.SetActionPhase(
                    $"{label} {Mathf.RoundToInt(ratio * 100f)}%",
                    target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(ratio * 100f)}%",
                    target,
                    reasonCode: "persistent-work-progress",
                    value: ratio));
            }

            yield return null;
        }
    }

    private float CalculateWorkPerSecond(
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        float environmentDurationMultiplier)
    {
        WorkTypeId workTypeId = WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : default;
        return CalculateWorkPerSecond(actor, target, workTypeId, environmentDurationMultiplier);
    }

    private float CalculateWorkPerSecond(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier)
    {
        if (workAmountCalculator != null)
        {
            if (workTypeId.IsValid)
            {
                return workAmountCalculator.CalculateWorkPerSecond(
                    actor,
                    target,
                    workTypeId,
                    environmentDurationMultiplier);
            }
        }

        float workSpeed = actor != null
            ? Mathf.Max(
                0.1f,
                workTypeId.IsValid
                    ? actor.GetWorkSpeedMultiplier(workTypeId)
                    : 1f)
            : 1f;
        float environment = 1f / Mathf.Max(0.1f, environmentDurationMultiplier);
        return Mathf.Clamp(workSpeed * environment, 0.05f, 8f);
    }

    private static void EndAiAction(CharacterActor actor, AIAction currentAction)
    {
        currentAction?.ReleaseReservation(actor);
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    private void FinishWorkRun(CharacterActor actor, AIAction currentAction)
    {
        CharacterSkillRuntimeEffects.EndWork(actor);
        bool wasPriorityTarget = work.assignedShop == work.PriorityWorkTarget;
        currentAction?.ReleaseReservation(actor);
        work.AssignWork(null, FacilityWorkType.None);
        if (wasPriorityTarget)
        {
            work.ClearPriorityWorkTarget();
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }
    }

    private bool ShouldAbortWorkRun(int runId, CharacterActor actor)
    {
        return !work.IsActiveWorkRun(runId)
            || actor == null
            || actor.Brain == null
            || actor.Brain.isBestActionEnd;
    }

    private bool CanContinueTimedWork(int runId)
    {
        return runId <= 0 || work.IsActiveWorkRun(runId);
    }

    private bool CanContinueTimedWork(int runId, CharacterActor actor)
    {
        if (!CanContinueTimedWork(runId))
        {
            return false;
        }

        if (runId <= 0)
        {
            return true;
        }

        return actor != null
            && actor.Brain != null
            && !actor.Brain.isBestActionEnd;
    }

    private void AbortWorkRun(int runId, CharacterActor actor, AIAction currentAction)
    {
        CharacterSkillRuntimeEffects.EndWork(actor);
        currentAction?.ReleaseReservation(actor);
        work.isWorking = false;
        if (work.IsActiveWorkRun(runId))
        {
            work.AssignWork(null, FacilityWorkType.None);
        }

        work.ClearActiveWorkRoutine(runId);
    }

    private static bool TryGetExteriorWorkSeconds(
        BuildableObject target,
        CharacterActor actor,
        WorkTypeId workTypeId,
        out float seconds)
    {
        seconds = 0f;
        if (target?.BuildingData == null)
        {
            return false;
        }

        foreach (IBuildingExteriorWorkRuntimeAbility ability in target.BuildingData.Abilities
                     .OfType<IBuildingExteriorWorkRuntimeAbility>())
        {
            if (!ability.SupportsExteriorWork(workTypeId)
                || !ability.IsExteriorWorkAvailable(actor, target, workTypeId))
            {
                continue;
            }

            seconds = Mathf.Max(
                MinimumNaturalExteriorWorkSeconds,
                ability.GetExteriorWorkSeconds(actor, target, workTypeId));
            return true;
        }

        return false;
    }

}
