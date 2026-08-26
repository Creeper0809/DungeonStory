using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// The single replay-safe prepared-output route executor. Ordinary production
/// and destructive capacity drains use this service so neither path can treat
/// an Items acknowledgement as completion before destination gram authority is
/// durably installed.
/// </summary>
public sealed class ProductionPreparedOutputExactRouteLifecycle :
    IProductionPreparedOutputExactRouteLifecycle
{
    private const int MaximumTransitionsPerCall = 4;

    private readonly IProductionPreparedOutputRoutingAuthority routing;
    private readonly IFacilityOutputExactRoutePort exactRoutes;
    private readonly IProductionPreparedOutputDeliveryCoordinator delivery;

    public ProductionPreparedOutputExactRouteLifecycle(
        IProductionPreparedOutputRoutingAuthority routing,
        IFacilityOutputExactRoutePort exactRoutes,
        IProductionPreparedOutputDeliveryCoordinator delivery)
    {
        this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
        this.exactRoutes = exactRoutes
            ?? throw new ArgumentNullException(nameof(exactRoutes));
        this.delivery = delivery
            ?? throw new ArgumentNullException(nameof(delivery));
    }

    [GameplayInternalOnly(
        "Prepared-output routing must commit Items, Economy and destination gram authority through one lifecycle.",
        "Production distribution and destructive capacity-drain coordinators only")]
    public ProductionPreparedOutputExactRouteLifecycleResult TryProgress(
        ProductionPreparedOutputRouteRequestSnapshot operation)
    {
        if (string.IsNullOrEmpty(operation.RouteOperationId))
        {
            return Conflict(
                operation,
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteSnapshotConflict,
                "prepared-output-route-operation-invalid");
        }

        bool mutated = false;
        for (int transition = 0;
             transition < MaximumTransitionsPerCall;
             transition++)
        {
            if (!TryCaptureCurrent(operation.RouteOperationId, out operation))
            {
                return Conflict(
                    operation,
                    ProductionPreparedOutputExactRouteLifecycleReason
                        .RouteSnapshotMissing,
                    "prepared-output-route-operation-missing");
            }

            switch (operation.Phase)
            {
                case ProductionPreparedOutputRoutePhase.PhysicalPending:
                {
                    FacilityOutputExactRouteRequest request = BuildRequest(operation);
                    if (!string.Equals(
                            request.RequestFingerprint,
                            operation.RequestFingerprint,
                            StringComparison.Ordinal))
                    {
                        return Conflict(
                            operation,
                            ProductionPreparedOutputExactRouteLifecycleReason
                                .RouteSnapshotConflict,
                            "prepared-output-route-request-fingerprint-drift");
                    }

                    if (!exactRoutes.TryRoute(
                            request,
                            out FacilityOutputExactRouteReceipt receipt,
                            out FacilityOutputExactRouteFailure failure))
                    {
                        return FromPhysicalFailure(operation, failure);
                    }

                    routing.CommitPhysicalRoute(ToEconomyReceipt(receipt));
                    mutated = true;
                    continue;
                }

                case ProductionPreparedOutputRoutePhase
                    .PhysicalAppliedAwaitingItemsAck:
                {
                    if (!exactRoutes.TryAcknowledge(
                            operation.RouteOperationId,
                            operation.PhysicalReceiptFingerprint,
                            out FacilityOutputExactRouteReceipt acknowledged,
                            out FacilityOutputExactRouteFailure failure))
                    {
                        return Deferred(
                            operation,
                            ProductionPreparedOutputExactRouteLifecycleReason
                                .PhysicalAcknowledgeUnavailable,
                            failure.Reason);
                    }

                    routing.AcknowledgePhysicalRoute(
                        acknowledged.RouteOperationId,
                        acknowledged.PhysicalReceiptFingerprint);
                    mutated = true;
                    continue;
                }

                case ProductionPreparedOutputRoutePhase
                    .ItemsAcknowledgedAwaitingCheckpointGc:
                {
                    if (HasCompletedDeliveryAuthority(operation))
                    {
                        return Completed(operation, mutated);
                    }

                    ProductionPreparedOutputDeliveryCoordinationResult result =
                        ApplyDeliveryAuthority(operation);
                    if (!result.Succeeded)
                    {
                        return FromDeliveryFailure(operation, result);
                    }

                    mutated = true;
                    if (!TryCaptureCurrent(
                            operation.RouteOperationId,
                            out ProductionPreparedOutputRouteRequestSnapshot
                                completed)
                        || !HasCompletedDeliveryAuthority(completed))
                    {
                        return Conflict(
                            operation,
                            ProductionPreparedOutputExactRouteLifecycleReason
                                .RouteSnapshotConflict,
                            "prepared-output-delivery-authority-not-published");
                    }

                    return Completed(completed, mutated);
                }

                default:
                    return Conflict(
                        operation,
                        ProductionPreparedOutputExactRouteLifecycleReason
                            .RouteSnapshotConflict,
                        "prepared-output-route-phase-invalid");
            }
        }

        return Deferred(
            operation,
            ProductionPreparedOutputExactRouteLifecycleReason.RouteUnavailable,
            "prepared-output-route-transition-budget-exhausted");
    }

    public int ResolveExactQuantity(
        ProductionPreparedOutputRoutingLineSnapshot line,
        int requestedQuantity)
    {
        int bounded = Math.Max(
            0,
            Math.Min(requestedQuantity, line.RemainingQuantity));
        if (bounded <= 0)
            return 0;

        long divisor = GreatestCommonDivisor(
            line.OriginalQuantity,
            line.OriginalMassGrams);
        int quantum = checked((int)(line.OriginalQuantity / divisor));
        return bounded - bounded % quantum;
    }

    private bool TryCaptureCurrent(
        string routeOperationId,
        out ProductionPreparedOutputRouteRequestSnapshot operation)
    {
        operation = routing.CaptureRouteOperations()
            .FirstOrDefault(value => string.Equals(
                value.RouteOperationId,
                routeOperationId,
                StringComparison.Ordinal));
        return !string.IsNullOrEmpty(operation.RouteOperationId);
    }

    private ProductionPreparedOutputDeliveryCoordinationResult
        ApplyDeliveryAuthority(
            ProductionPreparedOutputRouteRequestSnapshot operation) =>
        string.IsNullOrEmpty(operation.CurrentTargetDestinationId)
            ? delivery.TryApplyCompatibleWarehouse(
                operation.RouteOperationId,
                operation.ItemId,
                operation.CurrentTargetPositionX,
                operation.CurrentTargetPositionY)
            : delivery.TryApplyExactTarget(
                operation.RouteOperationId,
                ProductionPreparedOutputDeliveryRerouteReason
                    .InitialTargetAuthorityConfirmed,
                operation.CurrentTargetDestinationId,
                operation.CurrentTargetPositionX,
                operation.CurrentTargetPositionY);

    private static bool HasCompletedDeliveryAuthority(
        ProductionPreparedOutputRouteRequestSnapshot operation) =>
        operation.CurrentDeliveryTargetKind !=
            ProductionPreparedOutputDeliveryTargetKind.WarehouseSelectionPending
        && !string.IsNullOrEmpty(operation.CurrentTargetDestinationId)
        && !string.IsNullOrEmpty(operation.CurrentTargetAuthorityFingerprint);

    private static FacilityOutputExactRouteRequest BuildRequest(
        ProductionPreparedOutputRouteRequestSnapshot operation) => new(
        operation.RouteOperationId,
        operation.BatchCommitId,
        operation.SourceDestinationId,
        operation.TargetDestinationId,
        new Vector2Int(operation.TargetPositionX, operation.TargetPositionY),
        new[]
        {
            new FacilityOutputExactRouteSliceRequest(
                operation.OutputLineId,
                operation.LineCommitId,
                operation.ItemId,
                operation.SourceOffsetQuantity,
                operation.RoutedQuantity,
                operation.RoutedMassGrams,
                operation.ComponentFingerprint)
        });

    private static ProductionPreparedOutputPhysicalRouteReceipt ToEconomyReceipt(
        FacilityOutputExactRouteReceipt receipt) => new(
        receipt.RouteOperationId,
        receipt.RequestFingerprint,
        receipt.PhysicalReceiptFingerprint,
        receipt.BatchCommitId,
        receipt.SourceDestinationId,
        receipt.TargetDestinationId,
        receipt.TargetPosition.x,
        receipt.TargetPosition.y,
        receipt.TotalQuantity,
        receipt.TotalMassGrams,
        receipt.Slices.Select(value =>
            new ProductionPreparedOutputPhysicalRouteSliceReceipt(
                value.SourceStackId,
                value.RoutedStackId,
                value.OutputLineId,
                value.LineCommitId,
                value.ItemId,
                value.SourceOffsetQuantity,
                value.RoutedOffsetQuantity,
                value.RoutedQuantity,
                value.RoutedMassGrams,
                value.ComponentFingerprint)).ToArray());

    private static ProductionPreparedOutputExactRouteLifecycleResult
        FromPhysicalFailure(
            ProductionPreparedOutputRouteRequestSnapshot operation,
            FacilityOutputExactRouteFailure failure)
    {
        bool conflict = failure.Code is
            FacilityOutputExactRouteFailureCode.InvalidRequest or
            FacilityOutputExactRouteFailureCode.OperationConflict or
            FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid or
            FacilityOutputExactRouteFailureCode.ItemMismatch or
            FacilityOutputExactRouteFailureCode.ComponentMismatch or
            FacilityOutputExactRouteFailureCode.MassMismatch or
            FacilityOutputExactRouteFailureCode.UniquePartialForbidden or
            FacilityOutputExactRouteFailureCode.ReceiptMismatch or
            FacilityOutputExactRouteFailureCode.ProtectedRouteBypass or
            FacilityOutputExactRouteFailureCode.RestoreCandidateInvalid;
        return conflict
            ? Conflict(
                operation,
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteSnapshotConflict,
                failure.Reason)
            : Deferred(
                operation,
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteUnavailable,
                failure.Reason);
    }

    private static ProductionPreparedOutputExactRouteLifecycleResult
        FromDeliveryFailure(
            ProductionPreparedOutputRouteRequestSnapshot operation,
            ProductionPreparedOutputDeliveryCoordinationResult result) =>
        result.Status == ProductionPreparedOutputDeliveryCoordinationStatus
            .Rejected
        && result.Reason == ProductionPreparedOutputDeliveryCoordinationReason
            .AuthorityConflict
            ? Conflict(
                operation,
                ProductionPreparedOutputExactRouteLifecycleReason
                    .RouteSnapshotConflict,
                result.Message)
            : Deferred(
                operation,
                ProductionPreparedOutputExactRouteLifecycleReason
                    .DeliveryAuthorityUnavailable,
                result.Message);

    private static ProductionPreparedOutputExactRouteLifecycleResult Completed(
        ProductionPreparedOutputRouteRequestSnapshot operation,
        bool mutated) => new(
        mutated
            ? ProductionPreparedOutputExactRouteLifecycleStatus.Applied
            : ProductionPreparedOutputExactRouteLifecycleStatus.Replay,
        ProductionPreparedOutputExactRouteLifecycleReason.None,
        operation.RouteOperationId,
        operation.PhysicalReceiptFingerprint,
        operation.CurrentTargetAuthorityFingerprint,
        string.Empty);

    private static ProductionPreparedOutputExactRouteLifecycleResult Deferred(
        ProductionPreparedOutputRouteRequestSnapshot operation,
        ProductionPreparedOutputExactRouteLifecycleReason reason,
        string message) => new(
        ProductionPreparedOutputExactRouteLifecycleStatus.Deferred,
        reason,
        operation.RouteOperationId,
        operation.PhysicalReceiptFingerprint,
        operation.CurrentTargetAuthorityFingerprint,
        message);

    private static ProductionPreparedOutputExactRouteLifecycleResult Conflict(
        ProductionPreparedOutputRouteRequestSnapshot operation,
        ProductionPreparedOutputExactRouteLifecycleReason reason,
        string message) => new(
        ProductionPreparedOutputExactRouteLifecycleStatus.Conflict,
        reason,
        operation.RouteOperationId,
        operation.PhysicalReceiptFingerprint,
        operation.CurrentTargetAuthorityFingerprint,
        message);

    private static long GreatestCommonDivisor(long left, long right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0L)
        {
            long remainder = left % right;
            left = right;
            right = remainder;
        }
        return Math.Max(1L, left);
    }
}
