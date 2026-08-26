#if UNITY_EDITOR
using System;
using UnityEngine;

public static class ProductionPreparedOutputDeliveryCallerDebugScenarios
{
    public static void RunAll()
    {
        VerifyWarehouseAndExactDispatch();
        VerifyDeferredRetryAndCompletedGate();
    }

    private static void VerifyWarehouseAndExactDispatch()
    {
        FakeCoordinator coordinator = new();
        ProductionPreparedOutputRouteRequestSnapshot warehouse = Route(
            string.Empty,
            ProductionPreparedOutputDeliveryTargetKind
                .WarehouseSelectionPending,
            string.Empty);
        Require(ProductionPreparedOutputDeliveryDispatch.RequiresCompletedAuthority(
                warehouse)
            && ProductionPreparedOutputDeliveryDispatch.TryApply(
                coordinator,
                warehouse)
            && coordinator.WarehouseCalls == 1
            && coordinator.ExactCalls == 0
            && coordinator.LastItemId == "item:qa:meal"
            && coordinator.LastPosition == new Vector2Int(13, 7),
            "Warehouse-pending prepared output did not use the compatible-warehouse coordinator path.");

        ProductionPreparedOutputRouteRequestSnapshot exact = Route(
            "consumer:qa:kitchen",
            ProductionPreparedOutputDeliveryTargetKind.InitialExactTarget,
            string.Empty);
        Require(ProductionPreparedOutputDeliveryDispatch.TryApply(
                coordinator,
                exact)
            && coordinator.ExactCalls == 1
            && coordinator.LastReason ==
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed
            && coordinator.LastDestinationId == "consumer:qa:kitchen",
            "Concrete prepared-output target did not confirm its initial target authority.");
    }

    private static void VerifyDeferredRetryAndCompletedGate()
    {
        FakeCoordinator coordinator = new() { Succeeds = false };
        ProductionPreparedOutputRouteRequestSnapshot pending = Route(
            string.Empty,
            ProductionPreparedOutputDeliveryTargetKind
                .WarehouseSelectionPending,
            string.Empty);
        Require(!ProductionPreparedOutputDeliveryDispatch.TryApply(
                coordinator,
                pending)
            && !ProductionPreparedOutputDeliveryDispatch.TryApply(
                coordinator,
                pending)
            && coordinator.WarehouseCalls == 2,
            "Deferred delivery authority was not safely retryable on the same route.");

        ProductionPreparedOutputRouteRequestSnapshot completed = Route(
            "warehouse:qa:selected",
            ProductionPreparedOutputDeliveryTargetKind.ExactRerouteTarget,
            new string('c', 64),
            currentRevision: 1L);
        Require(!ProductionPreparedOutputDeliveryDispatch
                .RequiresCompletedAuthority(completed),
            "Completed exact delivery authority remained in the retry gate.");
    }

    private static ProductionPreparedOutputRouteRequestSnapshot Route(
        string targetDestinationId,
        ProductionPreparedOutputDeliveryTargetKind targetKind,
        string targetAuthorityFingerprint,
        long currentRevision = 0L) => new(
        "route:qa:delivery-caller",
        new string('a', 64),
        "batch:qa:delivery-caller",
        "line:qa:delivery-caller",
        "output:main",
        "item:qa:meal",
        new string('b', 64),
        "production:qa:buffer",
        targetDestinationId,
        13,
        7,
        0,
        0L,
        2,
        2_000L,
        ProductionPreparedOutputRoutePhase
            .ItemsAcknowledgedAwaitingCheckpointGc,
        new string('d', 64),
        currentRevision,
        targetKind,
        new string('e', 64),
        targetDestinationId,
        13,
        7,
        targetAuthorityFingerprint);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeCoordinator :
        IProductionPreparedOutputDeliveryCoordinator
    {
        internal bool Succeeds { get; set; } = true;
        internal int ExactCalls { get; private set; }
        internal int WarehouseCalls { get; private set; }
        internal string LastItemId { get; private set; } = string.Empty;
        internal string LastDestinationId { get; private set; } = string.Empty;
        internal Vector2Int LastPosition { get; private set; }
        internal ProductionPreparedOutputDeliveryRerouteReason LastReason
            { get; private set; }

        public ProductionPreparedOutputDeliveryCoordinationResult
            TryApplyExactTarget(
                string routeOperationId,
                ProductionPreparedOutputDeliveryRerouteReason reason,
                string targetDestinationId,
                int targetPositionX,
                int targetPositionY)
        {
            ExactCalls++;
            LastReason = reason;
            LastDestinationId = targetDestinationId;
            LastPosition = new Vector2Int(targetPositionX, targetPositionY);
            return Result(routeOperationId);
        }

        public ProductionPreparedOutputDeliveryCoordinationResult
            TryApplyCompatibleWarehouse(
                string routeOperationId,
                string itemId,
                int originPositionX,
                int originPositionY)
        {
            WarehouseCalls++;
            LastItemId = itemId;
            LastPosition = new Vector2Int(originPositionX, originPositionY);
            return Result(routeOperationId);
        }

        private ProductionPreparedOutputDeliveryCoordinationResult Result(
            string routeOperationId) => new(
            Succeeds
                ? ProductionPreparedOutputDeliveryCoordinationStatus.Applied
                : ProductionPreparedOutputDeliveryCoordinationStatus.Deferred,
            Succeeds
                ? ProductionPreparedOutputDeliveryCoordinationReason.None
                : ProductionPreparedOutputDeliveryCoordinationReason
                    .AuthorityBusy,
            routeOperationId,
            Succeeds ? "reroute:qa:delivery-caller" : string.Empty,
            Succeeds ? 1L : 0L,
            Succeeds ? new string('f', 64) : string.Empty,
            LastDestinationId,
            LastPosition.x,
            LastPosition.y,
            Succeeds ? "applied" : "deferred");
    }
}
#endif
