using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ConveyorItemGateway
{
    private readonly IItemTransferService transfers;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IWarehouseWorldQuery warehouses;
    private readonly IGridSystemProvider gridSystem;

    public ConveyorItemGateway(
        IItemTransferService transfers,
        IDungeonItemCatalogProvider catalog,
        IWarehouseWorldQuery warehouses,
        IGridSystemProvider gridSystem)
    {
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.warehouses = warehouses
            ?? throw new ArgumentNullException(nameof(warehouses));
        this.gridSystem = gridSystem
            ?? throw new ArgumentNullException(nameof(gridSystem));
    }

    public bool TryInspect(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack) =>
        transfers.TryInspectStackForTransit(stackId, out stack);

    public bool TryGetTransit(
        ItemStackId stackId,
        string payloadId,
        out ItemTransitStackSnapshot stack) =>
        transfers.TryGetTransitStack(stackId, payloadId, out stack);

    public bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int inputPosition,
        string payloadId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure) =>
        transfers.TryBeginTransit(
            stackId,
            inputPosition,
            payloadId,
            out stack,
            out failure);

    public void CopyLoadableStackIds(
        Vector2Int position,
        List<ItemStackId> destination) =>
        transfers.CopyLoadableTransitStackIds(position, destination);

    public bool TryCompleteToFacility(
        ItemStackId stackId,
        string payloadId,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure) =>
        transfers.TryCompleteTransitToFacilityBuffer(
            stackId,
            payloadId,
            position,
            destinationId,
            out _,
            out failure);

    public bool TryCompleteLoose(
        ItemStackId stackId,
        string payloadId,
        Vector2Int preferredPosition,
        out Vector2Int restoredPosition,
        out DomainFailure failure)
    {
        restoredPosition = preferredPosition;
        if (!TryResolveLoosePosition(preferredPosition, out restoredPosition))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                preferredPosition.x.ToString(),
                preferredPosition.y.ToString());
            return false;
        }

        return transfers.TryCompleteTransit(
            stackId,
            payloadId,
            WorldItemStackState.Loose,
            restoredPosition,
            string.Empty,
            out failure);
    }

    public bool TryCompleteToWarehouse(
        ItemStackId stackId,
        string payloadId,
        string preferredWarehouseId,
        bool allowAnyCompatible,
        out string warehouseId,
        out DomainFailure failure)
    {
        warehouseId = string.Empty;
        failure = DomainFailure.None;
        if (!TryGetTransit(stackId, payloadId, out ItemTransitStackSnapshot stack)
            || !catalog.TryGetDefinition(
                stack.ItemId,
                out DungeonItemDefinition definition))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        IWarehouseFacility[] candidates = warehouses.Warehouses
            .Where(candidate => candidate != null
                && candidate.HasWarehouseInventory
                && candidate.Inventory != null)
            .OrderBy(ResolveWarehouseId, StringComparer.Ordinal)
            .ToArray();
        IWarehouseFacility preferred = candidates.FirstOrDefault(candidate =>
            MatchesWarehouseId(candidate, preferredWarehouseId));
        IEnumerable<IWarehouseFacility> orderedCandidates = preferred != null
            ? new[] { preferred }.Concat(candidates.Where(candidate =>
                !ReferenceEquals(candidate, preferred)))
            : candidates;
        IWarehouseFacility[] compatible = orderedCandidates
            .Where(candidate => candidate.Inventory.HasMassCapacityAuthority
                && candidate.Inventory.Accepts(definition.StockCategory))
            .ToArray();
        if (!allowAnyCompatible)
        {
            compatible = preferred != null
                && preferred.Inventory.HasMassCapacityAuthority
                && preferred.Inventory.Accepts(definition.StockCategory)
                    ? new[] { preferred }
                    : Array.Empty<IWarehouseFacility>();
        }

        DomainFailure lastFailure = DomainFailure.None;
        foreach (IWarehouseFacility candidate in compatible)
        {
            if (!transfers.TryCompleteTransitToWarehouse(
                    stackId,
                    payloadId,
                    candidate,
                    out _,
                    out lastFailure))
            {
                if (!TryGetTransit(stackId, payloadId, out _))
                {
                    failure = lastFailure;
                    return false;
                }
                continue;
            }

            warehouseId = ResolveWarehouseId(candidate);
            return true;
        }

        if (compatible.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                preferredWarehouseId ?? string.Empty);
            return false;
        }

        failure = lastFailure.IsFailure
            ? lastFailure
            : new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                preferredWarehouseId ?? string.Empty);
        return false;
    }

    public Vector2Int ResolveNodeDropPosition(
        IndustrialNodeDescriptor node) =>
        node?.Cells != null && node.Cells.Count > 0
            ? node.Cells[0]
            : node?.Building != null
                ? node.Building.centerPos
                : Vector2Int.zero;

    private bool TryResolveLoosePosition(
        Vector2Int preferred,
        out Vector2Int resolved)
    {
        resolved = preferred;
        if (!gridSystem.TryGetGrid(out Grid grid))
        {
            return true;
        }

        Vector2Int[] offsets =
        {
            Vector2Int.zero,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up
        };
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int candidate = preferred + offset;
            if (grid.GetGridCell(candidate) != null
                && grid.IsWalkable(candidate))
            {
                resolved = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool MatchesWarehouseId(
        IWarehouseFacility warehouse,
        string requestedId)
    {
        string normalized = requestedId?.Trim() ?? string.Empty;
        string actual = ResolveWarehouseId(warehouse);
        return normalized.Length > 0
            && (string.Equals(actual, normalized, StringComparison.Ordinal)
                || string.Equals(
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    normalized,
                    StringComparison.Ordinal));
    }

    internal static string ResolveWarehouseId(
        IWarehouseFacility warehouse) =>
        WarehouseStorageIdentity.RequireDestinationId(warehouse);
}
