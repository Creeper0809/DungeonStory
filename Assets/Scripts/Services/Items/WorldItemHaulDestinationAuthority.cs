using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Single destination authority used both when a haul plan is authored and
/// when a pickup-committed delivery is rebound.  It resolves only live world
/// ownership; saved cells are evidence to compare with this result, never a
/// substitute for a missing facility/order.
/// </summary>
internal static class WorldItemHaulDestinationAuthority
{
    private const string ManualWaterDestinationPrefix =
        "plumbing:manual-water:";

    internal readonly struct Resolution
    {
        public Resolution(
            WorldItemHaulDestinationKind kind,
            string destinationId,
            IWarehouseFacility warehouse,
            Vector2Int deliveryPosition,
            Vector2Int dropPosition)
        {
            Kind = kind;
            DestinationId = destinationId;
            Warehouse = warehouse;
            DeliveryPosition = deliveryPosition;
            DropPosition = dropPosition;
        }

        public WorldItemHaulDestinationKind Kind { get; }
        public string DestinationId { get; }
        public IWarehouseFacility Warehouse { get; }
        public Vector2Int DeliveryPosition { get; }
        public Vector2Int DropPosition { get; }
    }

    public static bool TryResolve(
        Grid grid,
        ICharacterAiWorldRegistry world,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        WorldItemHaulDestinationKind kind,
        string destinationId,
        Vector2Int requestedDropPosition,
        out Resolution resolution,
        out string failureReason)
    {
        return TryResolve(
            grid,
            world,
            null,
            destinationClaims,
            kind,
            destinationId,
            requestedDropPosition,
            out resolution,
            out failureReason);
    }

    public static bool TryResolve(
        Grid grid,
        ICharacterAiWorldRegistry world,
        IWorkOrderQuery workOrders,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        WorldItemHaulDestinationKind kind,
        string destinationId,
        Vector2Int requestedDropPosition,
        out Resolution resolution,
        out string failureReason)
    {
        resolution = default;
        failureReason = string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (grid == null
            || world == null
            || destinationClaims == null
            || !Enum.IsDefined(typeof(WorldItemHaulDestinationKind), kind)
            || destination.Length == 0)
        {
            failureReason = "haul-destination-authority-invalid";
            return false;
        }

        if (kind == WorldItemHaulDestinationKind.Warehouse)
        {
            IWarehouseFacility[] matches = (world.Warehouses
                    ?? Array.Empty<IWarehouseFacility>())
                .Where(candidate => candidate != null
                    && candidate.PersistentInstanceId.IsValid
                    && candidate.HasWarehouseInventory
                    && candidate.Inventory != null
                    && candidate is BuildableObject building
                    && !building.isDestroy
                    && string.Equals(
                        WarehouseStorageIdentity.RequireDestinationId(candidate),
                        destination,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1
                || matches[0] is not BuildableObject warehouseBuilding
                || !TryResolveDeliveryCell(grid, warehouseBuilding, out Vector2Int delivery))
            {
                failureReason = "haul-destination-warehouse-missing-or-ambiguous:" + destination;
                return false;
            }

            resolution = new Resolution(kind, destination, matches[0], delivery, delivery);
            return true;
        }

        if (kind != WorldItemHaulDestinationKind.FacilityBuffer)
        {
            failureReason = "haul-destination-kind-unsupported:" + kind;
            return false;
        }

        BuildableObject owner;
        if (destination.StartsWith(
                WorkOrderRuntime.ConstructionDestinationPrefix,
                StringComparison.Ordinal))
        {
            ConstructionSite[] sites = (world.Buildings
                    ?? Array.Empty<BuildableObject>())
                .OfType<ConstructionSite>()
                .Where(site => site != null
                    && !site.isDestroy
                    && site.TargetBuilding != null
                    && site.GridPosition == requestedDropPosition
                    && !string.IsNullOrWhiteSpace(site.WorkOrderId)
                    && string.Equals(
                        BuildConstructionDestinationId(site),
                        destination,
                        StringComparison.Ordinal))
                .ToArray();
            WorkOrderProgressState[] matchingOrders = (workOrders?.ActiveOrders
                    ?? Array.Empty<WorkOrderProgressState>())
                .Where(order => order != null
                    && order.WorkTypeId == BuiltInWorkTypeIds.Construct
                    && order.Position == requestedDropPosition
                    && string.Equals(
                        order.MaterialDestinationId?.Trim(),
                        destination,
                        StringComparison.Ordinal))
                .ToArray();
            if (sites.Length > 1
                || (sites.Length == 0 && matchingOrders.Length != 1)
                || (sites.Length == 1
                    && matchingOrders.Length > 0
                    && !matchingOrders.Any(order => string.Equals(
                        order.WorkOrderId,
                        sites[0].WorkOrderId,
                        StringComparison.Ordinal))))
            {
                failureReason = "haul-destination-construction-order-missing-or-ambiguous:"
                    + destination;
                return false;
            }
            owner = sites.SingleOrDefault();
        }
        else
        {
            if (destination.StartsWith(
                    ManualWaterDestinationPrefix,
                    StringComparison.Ordinal))
            {
                // Manual-water destinations encode the fixture's persistent
                // building identity. Resolve that owner directly instead of
                // inferring ownership from every FacilityData object sharing
                // the drop cell (floors and fixtures can lawfully overlap).
                // This identity also survives the normal save/restore path
                // without a transient claim projection.
                string ownerBuildingId = destination.Substring(
                    ManualWaterDestinationPrefix.Length);
                BuildableObject[] matchingFixtures = (world.Buildings
                        ?? Array.Empty<BuildableObject>())
                    .Where(candidate => candidate != null
                        && !candidate.isDestroy
                        && candidate.BuildingData != null
                        && candidate.Facility != null
                        && candidate.PersistentInstanceId.IsValid
                        && candidate.centerPos == requestedDropPosition
                        && string.Equals(
                            candidate.PersistentInstanceId.Value,
                            ownerBuildingId,
                            StringComparison.Ordinal))
                    .ToArray();
                if (matchingFixtures.Length != 1)
                {
                    failureReason =
                        "haul-destination-manual-water-owner-missing-or-ambiguous:"
                        + destination;
                    return false;
                }

                owner = matchingFixtures[0];
            }
            else if (destinationClaims.TryGetClaim(
                    destination,
                    requestedDropPosition,
                    out FacilityBufferDestinationClaim claim))
            {
                if (claim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.ReservedTarget)
                {
                    owner = null;
                }
                else
                {
                    BuildableObject[] claimedFacilities = (world.Buildings
                             ?? Array.Empty<BuildableObject>())
                        .Where(candidate => candidate != null
                            && !candidate.isDestroy
                            && candidate.BuildingData != null
                            && (claim.AnchorKind
                                    == FacilityBufferDestinationAnchorKind.LiveBuilding
                                || candidate.Facility != null)
                            && candidate.PersistentInstanceId.IsValid
                            && candidate.centerPos == requestedDropPosition
                            && string.Equals(
                                candidate.PersistentInstanceId.Value,
                                claim.OwnerFacilityId,
                                StringComparison.Ordinal))
                        .ToArray();
                    if (claimedFacilities.Length != 1)
                    {
                        failureReason =
                            "haul-destination-claimed-facility-missing-or-ambiguous:"
                            + destination;
                        return false;
                    }
                    owner = claimedFacilities[0];
                }
            }
            else
            {
                if (ReservedTargetDestinationIdentity.RequiresExactClaim(
                        destination))
                {
                    failureReason =
                        "haul-destination-reserved-target-claim-missing:"
                        + destination;
                    return false;
                }

                BuildableObject[] facilities = (world.Buildings
                        ?? Array.Empty<BuildableObject>())
                    .Where(candidate => candidate != null
                        && !candidate.isDestroy
                        && candidate.BuildingData != null
                        && candidate.Facility != null
                        && candidate.centerPos == requestedDropPosition)
                    .ToArray();
                if (facilities.Length != 1)
                {
                    failureReason =
                        "haul-destination-facility-buffer-missing-or-ambiguous:"
                        + destination;
                    return false;
                }
                owner = facilities[0];
            }
        }

        if (!TryResolveFacilityDeliveryCell(
                grid,
                requestedDropPosition,
                out Vector2Int facilityDelivery))
        {
            failureReason = "haul-destination-delivery-cell-missing:" + destination;
            return false;
        }

        resolution = new Resolution(
            kind,
            destination,
            warehouse: null,
            facilityDelivery,
            requestedDropPosition);
        return true;
    }

    private static string BuildConstructionDestinationId(ConstructionSite site) =>
        $"{WorkOrderRuntime.ConstructionDestinationPrefix}"
        + $"{site.TargetBuilding.id}:{site.GridPosition.x}:{site.GridPosition.y}";

    internal static bool TryResolveDeliveryCell(
        Grid grid,
        BuildableObject building,
        out Vector2Int deliveryCell)
    {
        deliveryCell = default;
        foreach (Vector2Int position in building.buildPoses ?? Array.Empty<Vector2Int>())
        {
            if (grid.IsValidGridPos(position) && grid.IsWalkable(position))
            {
                deliveryCell = position;
                return true;
            }
        }
        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            building.centerPos,
            out deliveryCell,
            maxDistance: 2);
    }

    private static bool TryResolveFacilityDeliveryCell(
        Grid grid,
        Vector2Int destinationPosition,
        out Vector2Int deliveryCell)
    {
        if (grid.IsValidGridPos(destinationPosition)
            && grid.IsWalkable(destinationPosition))
        {
            deliveryCell = destinationPosition;
            return true;
        }
        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            destinationPosition,
            out deliveryCell,
            maxDistance: 2);
    }
}
