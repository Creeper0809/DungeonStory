using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Executes one restock plan from source selection through exact physical transfer.
/// Cargo/lease ownership and shared work-run cleanup remain single authorities.
/// </summary>
internal sealed class RestockWorkExecution
{
    private const float RestockPickupWaitSeconds = 0.35f;
    private const double RestockLeaseHeartbeatIntervalSeconds = 10d;
    private readonly AbilityWork work;
    private WorkTargetSelector targetSelector;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly IEnvironmentWorkPolicy environmentWorkPolicy;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IReservedItemTransferService reservedItemTransfers;
    private readonly RestockCargoOperation restockCargo;
    private readonly WorkProficiencyProgress proficiencyProgress;
    private readonly WorkRunRuntime run;

    internal RestockWorkExecution(WorkTaskCoreDependencies core, WorkTaskExecutionDependencies execution,
        IEnvironmentWorkPolicy environmentWorkPolicy, IReservedItemTransferService reservedItemTransfers,
        CharacterIdentityEventPublisher identityEvents, RestockCargoOperation restockCargo,
        WorkProficiencyProgress proficiencyProgress, WorkRunRuntime run)
    {
        work = core.Work;
        targetSelector = core.TargetSelector;
        gameClock = core.GameClock;
        workAmountCalculator = execution.WorkAmountCalculator;
        this.environmentWorkPolicy = environmentWorkPolicy;
        this.reservedItemTransfers = reservedItemTransfers;
        this.identityEvents = identityEvents;
        this.restockCargo = restockCargo;
        this.proficiencyProgress = proficiencyProgress;
        this.run = run;
    }

    internal void RebindTargetSelector(WorkTargetSelector selector) =>
        targetSelector = selector ?? throw new ArgumentNullException(nameof(selector));

    internal IEnumerator ExecuteRestockHaulWork(
        int runId,
        AIAction currentAction,
        AbilityMove move,
        Grid grid,
        Action<bool> onCompleted)
    {
        CharacterActor actor = work.WorkerActor;
        BuildableObject restockTarget = work.assignedShop;
        if (restockTarget == null || restockTarget.isDestroy)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Destroyed,
                    "restock-target-invalid-before-start"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        if (!CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId restockActorId)
            || !restockTarget.PersistentInstanceId.IsValid)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "restock-plan-identity-invalid",
                    restockTarget),
                DomainFailure.None,
                CharacterOperationBlockAxis.Identity,
                requestImmediateReplan: false);
            restockCargo.Release(
                ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        BuildingInstanceId restockTargetId =
            restockTarget.PersistentInstanceId;
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            restockTarget,
            BuiltInWorkTypeIds.Restock,
            $"work:{runId}:{restockTargetId.Value}:restock-started");
        actor?.Brain?.SetActionPhase(
            "\uBCF4\uCDA9 \uACC4\uD68D",
            restockTarget,
            "restock:planning");
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(BuiltInWorkTypeIds.Restock)
            / Mathf.Max(0.1f, CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor));
        if (restockTarget is not IRestockableFacility restockable)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "restock-target-not-restockable"),
                requestImmediateReplan: false);
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RestockTargetUnsupported,
                FacilityWorkType.Restock, restockTarget);
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
            out WarehouseRestockItem saleItem,
            out int loadAmount,
            out string failureReason))
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "restock-haul-plan-unavailable"
                        : failureReason),
                requestImmediateReplan: false);
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RestockPlanRejected,
                FacilityWorkType.Restock, restockTarget, detail: failureReason);
            work.isWorking = false;
            yield break;
        }

        if (restockTarget is not Shop shop
            || !shop.TryGetSaleItem(saleItem.Id, out SaleItem authoredSaleItem)
            || authoredSaleItem == null
            || !authoredSaleItem.ItemDefinitionId.IsValid
            || actor.WorldItemStackRuntime is not IWorldItemQuantityLeaseRuntime leaseRuntime
            || reservedItemTransfers == null)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "restock-quantity-lease-runtime-unavailable"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }

        string restockOperationId = RestockCargoOperation.CreateOperationId(
            restockActorId.Value,
            restockTargetId.Value,
            saleItem.Id,
            runId);
        WorldItemReservedStackQuantity stockReservation = default;
        Vector2Int pickupStandPosition = default;
        bool reserved = false;
        for (int requested = Mathf.Max(1, loadAmount); requested >= 1; requested--)
        {
            if (!leaseRuntime.TryReserveAvailableItemForDirectPickup(
                    actor,
                    authoredSaleItem.ItemDefinitionId.Value,
                    requested,
                    ItemReservationPurpose.FacilityBuffer,
                    restockOperationId,
                    out stockReservation,
                    out pickupStandPosition,
                    out failureReason))
            {
                continue;
            }
            loadAmount = requested;
            reserved = true;
            break;
        }
        if (!reserved || !stockReservation.IsValid)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "restock-quantity-unavailable"
                        : failureReason),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        if (!restockCargo.TryActivate(actor, leaseRuntime, restockOperationId,
                stockReservation.LeaseId, restockTarget as IRetailRestockOperationOwner))
        {
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-operation-owner-rejected"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        actor?.Brain?.SetActionPhase(
            "\uC774\uB3D9",
            warehouseBuilding,
            "restock:pickup-path-request");
        Vector2Int pickupStart = work.WorkGridResolver.GetGridPosition(grid, actor);
        IGridPathSearchBroker pathSearchBroker = actor.PathSearchBroker;
        if (pathSearchBroker == null)
        {
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "restock-pickup-path-broker-unavailable",
                    warehouseBuilding),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }

        double nextRestockLeaseHeartbeatAt = gameClock.Time;
        bool restockLeaseHeartbeatFailed = false;
        string restockLeaseHeartbeatFailure = string.Empty;
        Action heartbeatRestockLease = () =>
        {
            if (restockLeaseHeartbeatFailed
                || gameClock.Time < nextRestockLeaseHeartbeatAt)
            {
                return;
            }
            if (!restockCargo.TryRenew(
                    out restockLeaseHeartbeatFailure))
            {
                restockLeaseHeartbeatFailed = true;
                return;
            }
            nextRestockLeaseHeartbeatAt = gameClock.Time
                + RestockLeaseHeartbeatIntervalSeconds;
        };

        Queue<GridMoveStep> pathToWarehouse = null;
        GridPathRequestStatus pickupPathStatus = GridPathRequestStatus.Pending;
        GridTraversalContext pickupTraversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor));
        while (pickupPathStatus == GridPathRequestStatus.Pending)
        {
            if (run.ShouldAbortWorkRun(runId, actor, restockTarget))
            {
                run.AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
            heartbeatRestockLease();
            if (restockLeaseHeartbeatFailed)
            {
                actor.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        "restock-pickup-lease-heartbeat-failed-during-path-search:"
                        + restockLeaseHeartbeatFailure,
                        warehouseBuilding),
                    requestImmediateReplan: false);
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                work.isWorking = false;
                yield break;
            }
            if (!leaseRuntime.TryRevalidateQuantityLease(
                    restockCargo.LeaseId,
                    out string pendingLeaseFailure))
            {
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                actor.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        "restock-pickup-lease-invalid-during-path-search:"
                        + pendingLeaseFailure,
                        warehouseBuilding),
                    requestImmediateReplan: false);
                work.isWorking = false;
                yield break;
            }

            pickupPathStatus = pathSearchBroker.RequestMovePathTo(
                grid,
                pickupStart,
                pickupStandPosition,
                out pathToWarehouse,
                GridPathSearchPriority.Normal,
                pickupTraversal);
            if (pickupPathStatus == GridPathRequestStatus.Pending)
            {
                actor.Brain?.SetActionPhase(
                    "\uBCF4\uCDA9 \uACBD\uB85C \uACC4\uC0B0 \uC911",
                    warehouseBuilding,
                    "restock:pickup-path-pending");
                yield return null;
            }
        }

        if (run.ShouldAbortWorkRun(runId, actor, restockTarget))
        {
            run.AbortWorkRun(runId, actor, currentAction);
            yield break;
        }
        if (pickupPathStatus == GridPathRequestStatus.Unreachable
            || (pickupPathStatus == GridPathRequestStatus.Reachable
                && pathToWarehouse != null
                && pathToWarehouse.Count == 0
                && pickupStart != pickupStandPosition))
        {
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.NoPath,
                    "restock-pickup-path-unreachable",
                    warehouseBuilding),
                DomainFailure.None,
                CharacterOperationBlockAxis.Access,
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        if (pickupPathStatus != GridPathRequestStatus.Reachable
            || pathToWarehouse == null)
        {
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unknown,
                    "restock-pickup-path-terminal-contract-invalid:"
                    + pickupPathStatus,
                    warehouseBuilding),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        WorkEnvironmentAssessment pickupPreparation =
            environmentWorkPolicy.PrepareActiveWork(
                actor,
                pickupStandPosition,
                pathToWarehouse.ToArray(),
                expectedSeconds: 0f,
                EnvironmentalWorkKind.General,
                forced: ReferenceEquals(
                    work.PriorityWorkTarget,
                    restockTarget));
        if (!pickupPreparation.CanStart)
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "environment-apparel-restock-pickup-blocked",
                    warehouseBuilding),
                pickupPreparation.Failure,
                CharacterOperationBlockAxis.Environment,
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            work.isWorking = false;
            yield break;
        }

        actor?.Brain?.SetActionPhase(
            "\uC774\uB3D9",
            warehouseBuilding,
            "restock:move-to-stock");
        WorkExecutionChangedEvent.Publish(identityEvents, actor,
            WorkExecutionTransition.RestockSourceApproach,
            FacilityWorkType.Restock, restockTarget,
            sourceName: warehouseBuilding.name, destinationName: restockTarget.name);
        yield return move.MoveByPath(
            pathToWarehouse,
            currentAction,
            heartbeatRestockLease);
        if (run.ShouldAbortWorkRun(runId, actor, restockTarget))
        {
            run.AbortWorkRun(runId, actor, currentAction);
            yield break;
        }
        heartbeatRestockLease();
        if (restockLeaseHeartbeatFailed)
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-pickup-lease-heartbeat-failed-during-movement:"
                    + restockLeaseHeartbeatFailure,
                    warehouseBuilding),
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        Vector2Int pickupArrival = work.WorkGridResolver.GetGridPosition(
            grid,
            actor);
        if (move.LastGridMoveFailureReason != GridMoveFailureReason.None
            || pickupArrival != pickupStandPosition)
        {
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "restock-pickup-movement-failed:reason="
                    + move.LastGridMoveFailureReason
                    + ";actual=" + pickupArrival
                    + ";expected=" + pickupStandPosition,
                    warehouseBuilding),
                DomainFailure.None,
                CharacterOperationBlockAxis.Access,
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        if (run.TrySuspendAtSafeCheckpoint(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock))
        {
            run.AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        int carriedAmount = 0;
        for (int i = 0; i < loadAmount; i++)
        {
            actor?.Brain?.SetActionPhase(
                "\uBCF4\uCDA9 \uC801\uC7AC",
                warehouseBuilding,
                $"restock:loading:{i}/{loadAmount}");
            Vector2 pickupWorld = grid.GetWorldPos(pickupStandPosition);
            Vector3 pickupPosition = new Vector3(
                pickupWorld.x,
                pickupWorld.y,
                actor.transform.position.z);
            yield return move.Move2PosBySpeed(pickupPosition, 0.8f, currentAction);
            if (move.LastGridMoveFailureReason != GridMoveFailureReason.None
                || work.WorkGridResolver.GetGridPosition(grid, actor)
                    != pickupStandPosition
                || run.ShouldAbortWorkRun(runId, actor, restockTarget))
            {
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                run.AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            carriedAmount++;
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RestockLoadProgress,
                FacilityWorkType.Restock, warehouseBuilding,
                itemName: saleItem.Name, quantity: carriedAmount, requestedQuantity: loadAmount);
            work.FloatingIconFeedbackService.Show(
                actor,
                saleItem.Sprite,
                FloatingIconFeedbackDefaults.DefaultMaxWorldSize);
            float loadingStartedAt = gameClock.Time;
            yield return new WaitForSeconds(RestockPickupWaitSeconds * durationMultiplier);
            float loadingElapsed = Mathf.Max(
                1f / 60f,
                gameClock.Time - loadingStartedAt);
            heartbeatRestockLease();
            if (restockLeaseHeartbeatFailed)
            {
                actor.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        "restock-pickup-lease-heartbeat-failed-during-loading:"
                        + restockLeaseHeartbeatFailure,
                        warehouseBuilding),
                    requestImmediateReplan: false);
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                work.isWorking = false;
                yield break;
            }
            float approvedLoadingWork =
                WorkExecutionRules.CalculateWorkPerSecond(
                    workAmountCalculator,
                    actor,
                    restockTarget,
                    BuiltInWorkTypeIds.Restock,
                    durationMultiplier)
                * loadingElapsed;
            run.RecordApprovedWork(
                approvedLoadingWork,
                actor,
                remainingWork: Mathf.Max(
                    0f,
                    loadAmount - carriedAmount)
                    * RestockPickupWaitSeconds);
            if (run.ShouldAbortWorkRun(runId, actor, restockTarget))
            {
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                run.AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
            if (run.TrySuspendAtSafeCheckpoint(
                    actor,
                    restockTarget,
                    BuiltInWorkTypeIds.Restock))
            {
                restockCargo.Release(ItemReservationReleaseReason.Cancelled);
                run.AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
        }

        if (carriedAmount <= 0)
        {
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RestockStockUnavailable,
                FacilityWorkType.Restock, warehouseBuilding);
            work.isWorking = false;
            yield break;
        }

        CharacterCarryInventory restockCarry = CharacterCarryInventory.Ensure(actor);
        int physicallyPickedUp = 0;
        string pickupFailure = "restock-carry-runtime-unavailable";
        bool pickupReady = true;
        if (work.WorkGridResolver.GetGridPosition(grid, actor)
                != pickupStandPosition)
        {
            pickupFailure = "restock-pickup-position-changed-before-transfer";
            pickupReady = false;
        }
        else
        {
            heartbeatRestockLease();
            if (restockLeaseHeartbeatFailed)
            {
                pickupFailure = "restock-pickup-lease-heartbeat-failed-before-transfer:"
                    + restockLeaseHeartbeatFailure;
                pickupReady = false;
            }
            else if (!leaseRuntime.TryRevalidateQuantityLease(
                         restockCargo.LeaseId,
                         out pickupFailure))
            {
                pickupFailure = "restock-pickup-lease-invalid-before-transfer:"
                    + pickupFailure;
                pickupReady = false;
            }
        }
        if (!pickupReady
            || restockCarry == null
            || actor.WorldItemStackRuntime == null
            || !actor.WorldItemStackRuntime.TryPickupReservedStackQuantity(
                actor,
                restockCarry,
                stockReservation,
                out physicallyPickedUp,
                out pickupFailure)
            || physicallyPickedUp <= 0)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(pickupFailure)
                        ? "restock-physical-pickup-failed"
                        : pickupFailure),
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            work.isWorking = false;
            yield break;
        }
        carriedAmount = physicallyPickedUp;

        heartbeatRestockLease();
        if (restockLeaseHeartbeatFailed)
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-delivery-lease-heartbeat-failed-before-path:"
                    + restockLeaseHeartbeatFailure,
                    restockTarget),
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        if (!TryGetPathToBuilding(grid, actor, restockTarget, out Queue<GridMoveStep> pathToShop))
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.NoPath,
                    "restock-delivery-path-unreachable",
                    restockTarget),
                DomainFailure.None,
                CharacterOperationBlockAxis.Access,
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RestockDeliveryPathUnavailable,
                FacilityWorkType.Restock, restockTarget);
            work.isWorking = false;
            yield break;
        }
        WorkEnvironmentAssessment deliveryPreparation =
            environmentWorkPolicy.PrepareActiveWork(
                actor,
                restockTarget.centerPos,
                pathToShop.ToArray(),
                expectedSeconds: 0f,
                EnvironmentalWorkKind.General,
                forced: ReferenceEquals(
                    work.PriorityWorkTarget,
                    restockTarget));
        if (!deliveryPreparation.CanStart)
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "environment-apparel-restock-delivery-blocked",
                    restockTarget),
                deliveryPreparation.Failure,
                CharacterOperationBlockAxis.Environment,
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }

        actor?.Brain?.SetActionPhase(
            "\uC774\uB3D9",
            restockTarget,
            "restock:return-to-target");
        yield return move.MoveByPath(
            pathToShop,
            currentAction,
            heartbeatRestockLease);
        if (run.ShouldAbortWorkRun(runId, actor, restockTarget))
        {
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            run.AbortWorkRun(runId, actor, currentAction);
            yield break;
        }
        Vector2Int deliveryArrival = work.WorkGridResolver.GetGridPosition(
            grid,
            actor);
        if (move.LastGridMoveFailureReason != GridMoveFailureReason.None
            || !restockTarget.IsWorkAccessGridPosition(grid, deliveryArrival))
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "restock-delivery-movement-failed:reason="
                    + move.LastGridMoveFailureReason
                    + ";actual=" + deliveryArrival
                    + ";target=" + restockTarget.centerPos,
                    restockTarget),
                DomainFailure.None,
                CharacterOperationBlockAxis.Access,
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        heartbeatRestockLease();
        if (restockLeaseHeartbeatFailed)
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-delivery-lease-heartbeat-failed-during-movement:"
                    + restockLeaseHeartbeatFailure,
                    restockTarget),
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        if (run.TrySuspendAtSafeCheckpoint(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock))
        {
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            run.AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        if (!leaseRuntime.TryRevalidateQuantityLease(
                restockCargo.LeaseId,
                out string commitLeaseFailure))
        {
            actor.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-delivery-lease-invalid-before-commit:"
                    + commitLeaseFailure,
                    restockTarget),
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        actor?.Brain?.SetActionPhase(
            "\uBCF4\uCDA9 \uBC18\uC601",
            restockTarget,
            "restock:commit");
        ReservedRetailStockTransferReceipt transferReceipt = null;
        DomainFailure consumeFailure = new DomainFailure(
            FailureCode.ItemTransferRequestFailed,
            "retail-transfer-service-unavailable");
        if (reservedItemTransfers is not IReservedRetailStockTransferService retailTransfers
            || !retailTransfers.TryTakeReservedRetailLots(
                restockCargo.LeaseId,
                carriedAmount,
                saleItem.Id,
                authoredSaleItem.ItemDefinitionId.Value,
                restockCargo.OperationId,
                restockCarry,
                out transferReceipt,
                out consumeFailure))
        {
            string consumeFailureDetail =
                "restock-retail-take-failed:"
                + FormatDomainFailure(consumeFailure);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    consumeFailureDetail,
                    restockTarget),
                consumeFailure,
                CharacterOperationBlockAxis.Unknown,
                requestImmediateReplan: false);
            restockCargo.Release(ItemReservationReleaseReason.Replanned);
            work.isWorking = false;
            yield break;
        }
        if (!restockable.TryReceiveExactRetailLots(
                transferReceipt.Lots,
                carriedAmount,
                out int restocked,
                out string resultMessage))
        {
            string receiveFailureDetail =
                "restock-retail-receive-failed:"
                + (string.IsNullOrWhiteSpace(resultMessage)
                    ? "unspecified"
                    : resultMessage);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    receiveFailureDetail),
                requestImmediateReplan: false);
            if (!retailTransfers.TryRollbackRetailTransfer(
                    transferReceipt,
                    out DomainFailure rollbackFailure))
            {
                throw new InvalidOperationException(
                    $"Retail transfer '{transferReceipt.OperationId}' failed to rollback: "
                    + FormatDomainFailure(rollbackFailure));
            }
            // The exact physical cargo is Carried again after rollback. End the
            // operation only after that ownership is restored so the normal
            // cancellation path drops it at the actor's current cell instead
            // of leaving an ownerless carried slice behind.
            restockCargo.Release(ItemReservationReleaseReason.Cancelled);
            restocked = 0;
        }
        else
        {
            // Shop ownership is now authoritative. Only now may the source
            // quantity lease and the restock operation be completed.
            restockCargo.Release(ItemReservationReleaseReason.Completed);
        }

        WorkExecutionChangedEvent.Publish(identityEvents, actor,
            WorkExecutionTransition.RestockFinished,
            FacilityWorkType.Restock, restockTarget,
            detail: resultMessage, destinationName: restockTarget.name, quantity: restocked);
        if (restocked > 0)
        {
            run.RecordApprovedWork(restocked, actor, remainingWork: 0f);
            proficiencyProgress.RecordSpeciesCompletedWork(
                actor,
                BuiltInWorkTypeIds.Restock,
                restocked,
                "work-run:" + runId);
            run.PublishWorkCompleted(
                actor,
                BuiltInWorkTypeIds.Restock,
                saleItem.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                runId,
                WorkCompletionIdentityReferenceDefaults.ExecutorTerminalOwner,
                string.Empty,
                restockTarget.RequirePersistentInstanceId().Value,
                restocked);
            CharacterSkillRuntimeEffects.TriggerWorkCompleted(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock,
                $"work:{runId}:{restockTarget.RequirePersistentInstanceId().Value}:restock-completed");
        }

        actor?.AiMemory?.RecordWork(
            BuiltInWorkTypeIds.Restock,
            restockTarget,
            restocked > 0,
            restocked > 0
                ? $"보충 완료: {restockTarget.name}"
                : $"보충 실패: {restockTarget.name}");

        onCompleted?.Invoke(restocked > 0);
        yield return new WaitForSeconds(0.5f * durationMultiplier);
        work.isWorking = false;
        WorkControlChangedEvent.PublishAssigned(actor,
            WorkControlTransition.RestockRunFinished);
    }

    internal bool TryCreateRestockHaulPlan(
        CharacterActor actor,
        Grid grid,
        BuildableObject restockTarget,
        IRestockableFacility restockable,
        out BuildableObject warehouseBuilding,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int loadAmount,
        out string failureReason)
    {
        warehouseBuilding = null;
        warehouse = null;
        saleItem = default;
        loadAmount = 0;
        failureReason = string.Empty;

        if (actor == null || grid == null || restockTarget == null || restockable == null)
        {
            failureReason = "보충 경로 정보 없음";
            return false;
        }

        List<IWarehouseFacility> reachableWarehouses = targetSelector
            .FindReachableWarehouses(null)
            .Where(candidate => candidate.HasWarehouseInventory
                && candidate.Inventory != null
                && !ReferenceEquals(candidate, restockTarget))
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

        return true;
    }

    internal bool TryGetPathToBuilding(
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

        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor));
        if (!actor.PathSearchBroker.TryGetSearch(
                grid,
                startPos,
                out GridPathSearchResult search,
                GridPathSearchPriority.Normal,
                traversal)
            || !WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                target,
                search,
                out Vector2Int workAccess))
        {
            return false;
        }

        path = search.GetMovePathTo(workAccess);
        return path != null;
    }

    internal static string FormatDomainFailure(DomainFailure failure)
    {
        string[] parameters = failure.Parameters.ToArray();
        return parameters.Length == 0
            ? failure.Code.ToString()
            : failure.Code + ":" + string.Join(",", parameters);
    }
}
