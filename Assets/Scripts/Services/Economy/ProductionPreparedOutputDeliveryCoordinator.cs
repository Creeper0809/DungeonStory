using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Commits one prepared-output delivery revision across Economy ownership,
/// Items custody/outbox state, and destination gram admission.  Every
/// participant prepares a detached candidate before the first live mutation.
/// </summary>
public sealed class ProductionPreparedOutputDeliveryCoordinator :
    IProductionPreparedOutputDeliveryCoordinator
{
    private readonly IProductionPreparedOutputDeliveryRerouteParticipant economy;
    private readonly IFacilityOutputExactRouteDeliveryOverlayParticipant items;
    private readonly IPreparedOutputExactDestinationAdmissionParticipant admission;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IGridSystemProvider gridSystemProvider;

    public ProductionPreparedOutputDeliveryCoordinator(
        IProductionPreparedOutputDeliveryRerouteParticipant economy,
        IFacilityOutputExactRouteDeliveryOverlayParticipant items,
        IPreparedOutputExactDestinationAdmissionParticipant admission,
        IDungeonItemCatalogProvider catalog,
        IWarehouseWorldQuery warehouseWorld,
        IGridSystemProvider gridSystemProvider)
    {
        this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.admission = admission
            ?? throw new ArgumentNullException(nameof(admission));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
    }

    public ProductionPreparedOutputDeliveryCoordinationResult TryApplyExactTarget(
        string routeOperationId,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY)
    {
        string route = RequireCanonical(routeOperationId, nameof(routeOperationId));
        string target = RequireCanonical(
            targetDestinationId,
            nameof(targetDestinationId));
        if (!Enum.IsDefined(
                typeof(ProductionPreparedOutputDeliveryRerouteReason),
                reason)
            || reason == ProductionPreparedOutputDeliveryRerouteReason.InitialRoute)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        PreparedOutputExactDestinationTargetKind kind = target.StartsWith(
                WarehouseStorageIdentity.DestinationPrefix,
                StringComparison.Ordinal)
            ? PreparedOutputExactDestinationTargetKind.Warehouse
            : PreparedOutputExactDestinationTargetKind.FacilityBuffer;
        Vector2Int position = new(targetPositionX, targetPositionY);
        if (!admission.TryCaptureTargetAuthority(
                kind,
                target,
                position,
                out PreparedOutputExactDestinationAuthoritySnapshot authority,
                out PreparedOutputExactDestinationAdmissionFailureCode failure,
                out string failureReason))
        {
            return Rejected(
                route,
                target,
                position,
                MapAdmissionReason(failure),
                failureReason);
        }

        return TryApplyCandidate(route, reason, authority);
    }

    public ProductionPreparedOutputDeliveryCoordinationResult
        TryApplyCompatibleWarehouse(
            string routeOperationId,
            string itemId,
            int originPositionX,
            int originPositionY)
    {
        string route = RequireCanonical(routeOperationId, nameof(routeOperationId));
        string item = RequireCanonical(itemId, nameof(itemId));
        if (!catalog.TryGetDefinition(item, out DungeonItemDefinition definition)
            || definition == null)
        {
            return Rejected(
                route,
                string.Empty,
                new Vector2Int(originPositionX, originPositionY),
                ProductionPreparedOutputDeliveryCoordinationReason
                    .NoCompatibleWarehouse,
                $"Prepared-output item '{item}' is not in the physical catalog.");
        }

        Vector2Int origin = new(originPositionX, originPositionY);
        if (!gridSystemProvider.TryGetGrid(out Grid grid) || grid == null)
        {
            return Rejected(
                route,
                string.Empty,
                origin,
                ProductionPreparedOutputDeliveryCoordinationReason
                    .NoCompatibleWarehouse,
                "Prepared-output warehouse reachability requires the live grid.");
        }

        List<WarehouseRouteCandidate> routeCandidates = new();
        foreach (IWarehouseFacility value in warehouseWorld.Warehouses
                     ?? Array.Empty<IWarehouseFacility>())
        {
            if (value?.Inventory == null
                || !value.HasWarehouseInventory
                || !value.Inventory.HasMassCapacityAuthority
                || !value.Inventory.Accepts(definition.StockCategory)
                || !value.Inventory.CanStoreItem(item, 1)
                || !value.PersistentInstanceId.IsValid
                || value is not BuildableObject building
                || !TryGetWarehouseRouteCost(
                    grid,
                    origin,
                    building,
                    out int routeCost))
            {
                continue;
            }

            routeCandidates.Add(new WarehouseRouteCandidate(value, routeCost));
        }
        IWarehouseFacility[] candidates = routeCandidates
            .OrderBy(value => value.RouteCost)
            .ThenBy(value => value.Warehouse.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .Select(value => value.Warehouse)
            .ToArray();

        ProductionPreparedOutputDeliveryCoordinationResult lastCapacityFailure =
            default;
        foreach (IWarehouseFacility candidate in candidates)
        {
            string destination = WarehouseStorageIdentity.RequireDestinationId(
                candidate);
            Vector2Int position = candidate is BuildableObject building
                ? building.centerPos
                : default;
            if (!admission.TryCaptureTargetAuthority(
                    PreparedOutputExactDestinationTargetKind.Warehouse,
                    destination,
                    position,
                    out PreparedOutputExactDestinationAuthoritySnapshot authority,
                    out PreparedOutputExactDestinationAdmissionFailureCode failure,
                    out string failureReason))
            {
                lastCapacityFailure = Rejected(
                    route,
                    destination,
                    position,
                    MapAdmissionReason(failure),
                    failureReason);
                continue;
            }

            ProductionPreparedOutputDeliveryCoordinationResult result =
                TryApplyCandidate(
                    route,
                    ProductionPreparedOutputDeliveryRerouteReason.WarehouseRetarget,
                    authority);
            if (result.Succeeded
                || result.Status ==
                    ProductionPreparedOutputDeliveryCoordinationStatus.Deferred)
            {
                return result;
            }
            if (result.Reason is not
                ProductionPreparedOutputDeliveryCoordinationReason
                    .TargetCapacityUnavailable and not
                ProductionPreparedOutputDeliveryCoordinationReason
                    .TargetAuthorityUnavailable)
            {
                return result;
            }
            lastCapacityFailure = result;
        }

        if (!string.IsNullOrEmpty(lastCapacityFailure.RouteOperationId))
            return lastCapacityFailure;
        return Rejected(
            route,
            string.Empty,
            origin,
            ProductionPreparedOutputDeliveryCoordinationReason
                .NoCompatibleWarehouse,
            $"No exact gram warehouse target can accept '{item}'.");
    }

    private ProductionPreparedOutputDeliveryCoordinationResult TryApplyCandidate(
        string routeOperationId,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        PreparedOutputExactDestinationAuthoritySnapshot targetAuthority)
    {
        ProductionPreparedOutputDeliveryRevisionSnapshot economyCurrent =
            economy.CaptureCurrentDelivery(routeOperationId);
        FacilityOutputExactRouteDeliveryRevisionSnapshot itemsCurrent =
            items.CaptureCurrentDelivery(routeOperationId);
        RequireCurrentAuthoritiesMatch(economyCurrent, itemsCurrent);

        if (economyCurrent.Revision > 0L
            && string.Equals(
                economyCurrent.TargetDestinationId,
                targetAuthority.DestinationId,
                StringComparison.Ordinal)
            && economyCurrent.TargetPositionX == targetAuthority.Position.x
            && economyCurrent.TargetPositionY == targetAuthority.Position.y
            && string.Equals(
                economyCurrent.TargetAuthorityFingerprint,
                targetAuthority.Fingerprint,
                StringComparison.Ordinal))
        {
            return Result(
                ProductionPreparedOutputDeliveryCoordinationStatus.Replay,
                ProductionPreparedOutputDeliveryCoordinationReason.None,
                economyCurrent,
                "Prepared-output delivery target is already exact and current.");
        }
        if (economyCurrent.Revision > 0L
            && targetAuthority.Kind ==
                PreparedOutputExactDestinationTargetKind.FacilityBuffer
            && string.Equals(
                economyCurrent.TargetDestinationId,
                targetAuthority.DestinationId,
                StringComparison.Ordinal)
            && economyCurrent.TargetPositionX == targetAuthority.Position.x
            && economyCurrent.TargetPositionY == targetAuthority.Position.y
            && !string.Equals(
                economyCurrent.TargetAuthorityFingerprint,
                targetAuthority.Fingerprint,
                StringComparison.Ordinal))
        {
            return Rejected(
                routeOperationId,
                targetAuthority.DestinationId,
                targetAuthority.Position,
                ProductionPreparedOutputDeliveryCoordinationReason
                    .AuthorityConflict,
                "A same-target FacilityBuffer authority refresh cannot prove "
                + "exact-lot occupancy exclusion and is blocked to prevent "
                + "double-counted mass.");
        }

        IProductionPreparedOutputDeliveryRerouteCandidate economyCandidate = null;
        IFacilityOutputExactRouteDeliveryOverlayCandidate itemsCandidate = null;
        PreparedOutputExactDestinationAdmissionCandidate admissionCandidate = null;
        bool economyPublished = false;
        try
        {
            economyCandidate = economy.PrepareDeliveryReroute(
                routeOperationId,
                economyCurrent.Revision,
                economyCurrent.RevisionFingerprint,
                economyCurrent.OriginalPhysicalReceiptFingerprint,
                reason,
                targetAuthority.DestinationId,
                targetAuthority.Position.x,
                targetAuthority.Position.y,
                targetAuthority.Fingerprint);
            itemsCandidate = items.PrepareDeliveryOverlay(
                routeOperationId,
                economyCandidate.ExpectedCurrentRevision,
                economyCandidate.ExpectedCurrentRevisionFingerprint,
                economyCandidate.OriginalPhysicalReceiptFingerprint,
                economyCandidate.NextRevision,
                economyCandidate.NextRevisionFingerprint,
                economyCandidate.RerouteOperationId,
                economyCandidate.TargetDestinationId,
                economyCandidate.TargetPositionX,
                economyCandidate.TargetPositionY,
                economyCandidate.TargetAuthorityFingerprint);
            if (itemsCandidate.Status ==
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred)
            {
                return Deferred(
                    economyCurrent,
                    itemsCandidate.Reason ==
                        FacilityOutputExactRouteDeliveryOverlayReason
                            .PhysicalStateNotStable
                        ? ProductionPreparedOutputDeliveryCoordinationReason
                            .PhysicalStateNotStable
                        : ProductionPreparedOutputDeliveryCoordinationReason
                            .AuthorityBusy,
                    itemsCandidate.Message);
            }

            PreparedOutputExactDestinationLotSlice[] exactSlices =
                itemsCandidate.DeliverySubjects
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => new PreparedOutputExactDestinationLotSlice(
                        value.StackId,
                        value.Quantity,
                        value.ReservationRevision,
                        value.ComponentFingerprint,
                        value.ExactMassGrams))
                    .ToArray();
            string admissionOperationId =
                $"prepared-output-delivery-admission:{routeOperationId}:"
                + $"{economyCandidate.NextRevision:D12}";
            PreparedOutputExactDestinationAdmissionRequest request = new(
                admissionOperationId,
                routeOperationId,
                economyCandidate.OriginalPhysicalReceiptFingerprint,
                economyCandidate.NextRevisionFingerprint,
                exactSlices,
                targetAuthority);
            if (!admission.TryPrepare(
                    request,
                    out admissionCandidate,
                    out PreparedOutputExactDestinationAdmissionFailureCode failure,
                    out string failureReason))
            {
                items.RollbackDeliveryOverlay(itemsCandidate);
                return Rejected(
                    routeOperationId,
                    targetAuthority.DestinationId,
                    targetAuthority.Position,
                    MapAdmissionReason(failure),
                    failureReason);
            }

            items.PublishDeliveryOverlay(itemsCandidate);
            if (!admission.TryPublish(
                    admissionCandidate,
                    out PreparedOutputExactDestinationAdmissionFailureCode
                        publishFailure,
                    out string publishFailureReason))
            {
                throw new InvalidOperationException(
                    "Prepared-output destination admission publish failed: "
                    + $"{publishFailure}:{publishFailureReason}");
            }
            economy.PublishDeliveryReroute(economyCandidate);
            economyPublished = true;

            ProductionPreparedOutputDeliveryRevisionSnapshot committed =
                economy.CaptureCurrentDelivery(routeOperationId);
            FacilityOutputExactRouteDeliveryRevisionSnapshot committedItems =
                items.CaptureCurrentDelivery(routeOperationId);
            RequireCurrentAuthoritiesMatch(committed, committedItems);

            if (!admission.TryComplete(
                    admissionCandidate,
                    out PreparedOutputExactDestinationAdmissionFailureCode
                        completeFailure,
                    out string completeFailureReason))
            {
                throw new InvalidOperationException(
                    "Prepared-output destination admission completion failed: "
                    + $"{completeFailure}:{completeFailureReason}");
            }
            items.CompleteDeliveryOverlay(itemsCandidate);
            economy.CompleteDeliveryReroute(economyCandidate);

            return Result(
                itemsCandidate.Status ==
                    FacilityOutputExactRouteDeliveryOverlayStatus.Replay
                    ? ProductionPreparedOutputDeliveryCoordinationStatus.Replay
                    : ProductionPreparedOutputDeliveryCoordinationStatus.Applied,
                ProductionPreparedOutputDeliveryCoordinationReason.None,
                committed,
                "Prepared-output delivery revision committed atomically.");
        }
        catch (Exception commitFailure)
        {
            List<Exception> rollbackFailures = new();
            if (economyPublished && economyCandidate != null)
            {
                try
                {
                    economy.RollbackDeliveryReroute(economyCandidate);
                }
                catch (Exception rollbackFailure)
                {
                    rollbackFailures.Add(rollbackFailure);
                }
            }
            if (admissionCandidate != null)
            {
                try
                {
                    if (!admission.TryRollback(
                            admissionCandidate,
                            out PreparedOutputExactDestinationAdmissionFailureCode
                                rollbackFailure,
                            out string rollbackReason))
                    {
                        rollbackFailures.Add(new InvalidOperationException(
                            "Prepared-output admission rollback failed: "
                            + $"{rollbackFailure}:{rollbackReason}"));
                    }
                }
                catch (Exception rollbackFailure)
                {
                    rollbackFailures.Add(rollbackFailure);
                }
            }
            if (itemsCandidate != null)
            {
                try
                {
                    items.RollbackDeliveryOverlay(itemsCandidate);
                }
                catch (Exception rollbackFailure)
                {
                    rollbackFailures.Add(rollbackFailure);
                }
            }
            if (rollbackFailures.Count == 0)
                throw;

            rollbackFailures.Insert(0, commitFailure);
            throw new AggregateException(
                "Prepared-output delivery commit failed and one or more "
                + "participant rollbacks also failed.",
                rollbackFailures);
        }
    }

    private static void RequireCurrentAuthoritiesMatch(
        ProductionPreparedOutputDeliveryRevisionSnapshot economyCurrent,
        FacilityOutputExactRouteDeliveryRevisionSnapshot itemsCurrent)
    {
        if (!string.Equals(economyCurrent.RouteOperationId,
                itemsCurrent.RouteOperationId, StringComparison.Ordinal)
            || economyCurrent.Revision != itemsCurrent.Revision
            || !string.Equals(economyCurrent.RevisionFingerprint,
                itemsCurrent.RevisionFingerprint, StringComparison.Ordinal)
            || !string.Equals(economyCurrent.OriginalPhysicalReceiptFingerprint,
                itemsCurrent.OriginalPhysicalReceiptFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(economyCurrent.TargetDestinationId,
                itemsCurrent.TargetDestinationId, StringComparison.Ordinal)
            || economyCurrent.TargetPositionX != itemsCurrent.TargetPositionX
            || economyCurrent.TargetPositionY != itemsCurrent.TargetPositionY
            || !string.Equals(economyCurrent.TargetAuthorityFingerprint,
                itemsCurrent.TargetAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared-output Economy and Items delivery authorities diverged.");
        }
    }

    private static ProductionPreparedOutputDeliveryCoordinationResult Result(
        ProductionPreparedOutputDeliveryCoordinationStatus status,
        ProductionPreparedOutputDeliveryCoordinationReason reason,
        ProductionPreparedOutputDeliveryRevisionSnapshot revision,
        string message) => new(
        status,
        reason,
        revision.RouteOperationId,
        revision.RerouteOperationId,
        revision.Revision,
        revision.RevisionFingerprint,
        revision.TargetDestinationId,
        revision.TargetPositionX,
        revision.TargetPositionY,
        message);

    private static ProductionPreparedOutputDeliveryCoordinationResult Deferred(
        ProductionPreparedOutputDeliveryRevisionSnapshot current,
        ProductionPreparedOutputDeliveryCoordinationReason reason,
        string message) => Result(
        ProductionPreparedOutputDeliveryCoordinationStatus.Deferred,
        reason,
        current,
        message);

    private static ProductionPreparedOutputDeliveryCoordinationResult Rejected(
        string routeOperationId,
        string targetDestinationId,
        Vector2Int targetPosition,
        ProductionPreparedOutputDeliveryCoordinationReason reason,
        string message) => new(
        ProductionPreparedOutputDeliveryCoordinationStatus.Rejected,
        reason,
        routeOperationId,
        string.Empty,
        0L,
        string.Empty,
        targetDestinationId,
        targetPosition.x,
        targetPosition.y,
        message);

    private static ProductionPreparedOutputDeliveryCoordinationReason
        MapAdmissionReason(
            PreparedOutputExactDestinationAdmissionFailureCode failure) =>
        failure switch
        {
            PreparedOutputExactDestinationAdmissionFailureCode
                .CapacityUnavailable =>
                ProductionPreparedOutputDeliveryCoordinationReason
                    .TargetCapacityUnavailable,
            PreparedOutputExactDestinationAdmissionFailureCode.AuthorityMissing
                or PreparedOutputExactDestinationAdmissionFailureCode
                    .AuthorityStale =>
                ProductionPreparedOutputDeliveryCoordinationReason
                    .TargetAuthorityUnavailable,
            PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged =>
                ProductionPreparedOutputDeliveryCoordinationReason
                    .PhysicalStateNotStable,
            _ => ProductionPreparedOutputDeliveryCoordinationReason
                .AdmissionUnavailable
        };

    private static string RequireCanonical(string value, string parameter)
    {
        string exact = value ?? string.Empty;
        if (exact.Length == 0
            || !string.Equals(exact, exact.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Prepared-output delivery identity must be canonical.",
                parameter);
        }
        return exact;
    }

    internal static bool TryGetWarehouseRouteCost(
        Grid grid,
        Vector2Int origin,
        BuildableObject warehouse,
        out int routeCost)
    {
        routeCost = int.MaxValue;
        if (grid == null
            || warehouse == null
            || warehouse.isDestroy
            || !TryResolveWarehouseDeliveryCell(
                grid,
                warehouse,
                out Vector2Int delivery))
        {
            return false;
        }

        Vector2Int[] pickupStands =
        {
            origin,
            origin + Vector2Int.left,
            origin + Vector2Int.right
        };
        foreach (Vector2Int pickupStand in pickupStands.Distinct())
        {
            if (!grid.IsValidGridPos(pickupStand)
                || !grid.IsWalkable(pickupStand))
            {
                continue;
            }

            int candidateCost = grid.SearchPath(pickupStand)
                .GetMoveCostTo(delivery);
            if (candidateCost < routeCost)
                routeCost = candidateCost;
        }

        return routeCost != int.MaxValue;
    }

    private static bool TryResolveWarehouseDeliveryCell(
        Grid grid,
        BuildableObject warehouse,
        out Vector2Int delivery)
    {
        delivery = default;
        foreach (Vector2Int position in
                 warehouse.buildPoses ?? Array.Empty<Vector2Int>())
        {
            if (grid.IsValidGridPos(position) && grid.IsWalkable(position))
            {
                delivery = position;
                return true;
            }
        }

        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            warehouse.centerPos,
            out delivery,
            maxDistance: 2);
    }

    private readonly struct WarehouseRouteCandidate
    {
        internal WarehouseRouteCandidate(
            IWarehouseFacility warehouse,
            int routeCost)
        {
            Warehouse = warehouse;
            RouteCost = routeCost;
        }

        internal IWarehouseFacility Warehouse { get; }
        internal int RouteCost { get; }
    }
}
