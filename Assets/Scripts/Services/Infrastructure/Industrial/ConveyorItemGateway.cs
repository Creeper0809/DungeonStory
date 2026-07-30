using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

internal sealed class ConveyorItemGateway
{
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markers;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IWarehouseWorldQuery warehouses;
    private readonly IGridSystemProvider gridSystem;

    public ConveyorItemGateway(
        WorldItemRepository repository,
        IItemMarkerPresenter markers,
        IDungeonItemCatalogProvider catalog,
        IWarehouseWorldQuery warehouses,
        IGridSystemProvider gridSystem)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.warehouses = warehouses
            ?? throw new ArgumentNullException(nameof(warehouses));
        this.gridSystem = gridSystem
            ?? throw new ArgumentNullException(nameof(gridSystem));
    }

    public bool TryExtract(
        string stackId,
        Vector2Int inputPosition,
        out WorldItemStackSaveData stack,
        out string failureReason)
    {
        stack = null;
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(stackId)
            || !repository.RecordsById.TryGetValue(
                stackId.Trim(),
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            failureReason = "컨베이어에 올릴 아이템을 찾을 수 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(record.reservedByPersistentId))
        {
            failureReason = "다른 작업자가 예약한 아이템입니다.";
            return false;
        }

        if (Mathf.Abs(record.position.x - inputPosition.x)
                + Mathf.Abs(record.position.y - inputPosition.y)
            > 1)
        {
            failureReason = "아이템이 입력 포트에 닿아 있지 않습니다.";
            return false;
        }

        stack = ToSaveData(record);
        Vector2Int oldPosition = record.position;
        repository.Remove(record);
        markers.RefreshAt(oldPosition);
        return true;
    }

    public bool TryPeek(
        string stackId,
        out WorldItemStackSaveData stack)
    {
        stack = null;
        if (string.IsNullOrWhiteSpace(stackId)
            || !repository.RecordsById.TryGetValue(
                stackId.Trim(),
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            return false;
        }

        stack = ToSaveData(record);
        return true;
    }

    public void CopyLoadableStackIds(
        Vector2Int position,
        List<string> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> records))
        {
            return;
        }

        foreach (WorldItemStackRecord record in records
                     .Where(record => record != null
                         && record.quantity > 0
                         && record.state == WorldItemStackState.Loose
                         && string.IsNullOrWhiteSpace(
                             record.reservedByPersistentId))
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            destination.Add(record.stackId);
        }
    }

    public bool TryRestoreToFacility(
        WorldItemStackSaveData stack,
        Vector2Int position,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRestore(stack, out failureReason))
        {
            return false;
        }

        WorldItemStackRecord record = ToRecord(stack);
        record.position = position;
        record.state = WorldItemStackState.FacilityBuffer;
        record.destinationId = destinationId?.Trim() ?? string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.Add(record);
        markers.RefreshAt(position);
        return true;
    }

    public bool TryRestoreLoose(
        WorldItemStackSaveData stack,
        Vector2Int preferredPosition,
        out Vector2Int restoredPosition,
        out string failureReason)
    {
        restoredPosition = preferredPosition;
        failureReason = string.Empty;
        if (!CanRestore(stack, out failureReason))
        {
            return false;
        }

        if (!TryResolveLoosePosition(
                preferredPosition,
                out restoredPosition))
        {
            failureReason = "오버플로 아이템을 놓을 수 있는 칸이 없습니다.";
            return false;
        }

        WorldItemStackRecord record = ToRecord(stack);
        record.position = restoredPosition;
        record.state = WorldItemStackState.Loose;
        record.destinationId = string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.Add(record);
        markers.RefreshAt(restoredPosition);
        return true;
    }

    public bool TryRestoreToWarehouse(
        WorldItemStackSaveData stack,
        string preferredWarehouseId,
        bool allowAnyCompatible,
        out string warehouseId,
        out string failureReason)
    {
        warehouseId = string.Empty;
        failureReason = string.Empty;
        if (!CanRestore(stack, out failureReason)
            || !catalog.TryGetDefinition(
                stack.itemId,
                out DungeonItemDefinition definition))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "아이템의 재고 분류를 확인할 수 없습니다."
                : failureReason;
            return false;
        }

        IWarehouseFacility[] candidates = warehouses.Warehouses
            .Where(warehouse => warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            .OrderBy(ResolveWarehouseId, StringComparer.Ordinal)
            .ToArray();
        IWarehouseFacility preferred = candidates.FirstOrDefault(warehouse =>
            MatchesWarehouseId(warehouse, preferredWarehouseId));
        IWarehouseFacility selected = preferred != null
            && preferred.Inventory.CanStore(
                definition.StockCategory,
                stack.quantity)
                ? preferred
                : allowAnyCompatible
                    ? candidates.FirstOrDefault(warehouse =>
                        warehouse.Inventory.CanStore(
                            definition.StockCategory,
                            stack.quantity))
                    : null;
        if (selected == null)
        {
            failureReason = "배출할 수 있는 예비 창고가 없습니다.";
            return false;
        }

        int deposited = selected.Inventory.Deposit(
            definition.StockCategory,
            stack.quantity);
        if (deposited != stack.quantity)
        {
            if (deposited > 0)
            {
                selected.Inventory.Withdraw(
                    definition.StockCategory,
                    deposited);
            }

            failureReason = "예비 창고의 남은 공간이 부족합니다.";
            return false;
        }

        Vector2Int position = selected is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
        WorldItemStackRecord record = ToRecord(stack);
        record.position = position;
        record.state = WorldItemStackState.Stored;
        record.destinationId = ResolveStorageDestinationId(selected);
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.Add(record);
        markers.RefreshAt(position);
        warehouseId = ResolveWarehouseId(selected);
        return true;
    }

    public Vector2Int ResolveNodeDropPosition(
        IndustrialNodeDescriptor node)
    {
        return node?.Cells != null && node.Cells.Count > 0
            ? node.Cells[0]
            : node?.Building != null
                ? node.Building.centerPos
                : Vector2Int.zero;
    }

    private bool CanRestore(
        WorldItemStackSaveData stack,
        out string failureReason)
    {
        if (stack == null
            || stack.quantity <= 0
            || string.IsNullOrWhiteSpace(stack.itemId))
        {
            failureReason = "컨베이어 화물 데이터가 손상되었습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(stack.stackId)
            && repository.RecordsById.ContainsKey(stack.stackId.Trim()))
        {
            failureReason = "같은 ID의 아이템이 이미 월드에 있습니다.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

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
        return !string.IsNullOrWhiteSpace(normalized)
            && (string.Equals(
                ResolveWarehouseId(warehouse),
                normalized,
                StringComparison.Ordinal)
            || string.Equals(
                ResolveStorageDestinationId(warehouse),
                normalized,
                StringComparison.Ordinal));
    }

    internal static string ResolveWarehouseId(IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building
            ? IndustrialInfrastructureIdentity.GetNodeId(building)
            : $"warehouse:{warehouse?.GetHashCode() ?? 0}";
    }

    private static string ResolveStorageDestinationId(
        IWarehouseFacility warehouse)
    {
        if (warehouse is BuildableObject building)
        {
            return string.Concat(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                building.GridId.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.x.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.y.ToString(CultureInfo.InvariantCulture));
        }

        return WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + (warehouse?.GetHashCode() ?? 0)
                .ToString(CultureInfo.InvariantCulture);
    }

    private static WorldItemStackSaveData ToSaveData(
        WorldItemStackRecord record)
    {
        return new WorldItemStackSaveData
        {
            stackId = record.stackId,
            itemId = record.itemId,
            quantity = record.quantity,
            state = record.state,
            gridX = record.position.x,
            gridY = record.position.y,
            reservedByPersistentId =
                record.reservedByPersistentId ?? string.Empty,
            destinationId = record.destinationId ?? string.Empty,
            sourceStorageDestinationId =
                record.sourceStorageDestinationId ?? string.Empty,
            hasDestinationPosition = record.hasDestinationPosition,
            destinationGridX = record.destinationPosition.x,
            destinationGridY = record.destinationPosition.y,
            forbidden = record.forbidden,
            sourceCharacterId = record.sourceCharacterId ?? string.Empty,
            sourceDisplayName = record.sourceDisplayName ?? string.Empty,
            sourceSpeciesTag = record.sourceSpeciesTag ?? string.Empty,
            sourceDeathReason = record.sourceDeathReason ?? string.Empty,
            emergencyButcheryAllowed = record.emergencyButcheryAllowed,
            wasteOrigin = record.wasteOrigin,
            contamination = record.contamination
        };
    }

    private static WorldItemStackRecord ToRecord(
        WorldItemStackSaveData stack)
    {
        return new WorldItemStackRecord
        {
            stackId = stack.stackId?.Trim() ?? string.Empty,
            itemId = stack.itemId?.Trim() ?? string.Empty,
            quantity = Mathf.Max(0, stack.quantity),
            state = stack.state,
            position = new Vector2Int(stack.gridX, stack.gridY),
            reservedByPersistentId = string.Empty,
            destinationId = stack.destinationId?.Trim() ?? string.Empty,
            sourceStorageDestinationId =
                stack.sourceStorageDestinationId?.Trim() ?? string.Empty,
            hasDestinationPosition = stack.hasDestinationPosition,
            destinationPosition = new Vector2Int(
                stack.destinationGridX,
                stack.destinationGridY),
            forbidden = stack.forbidden,
            sourceCharacterId = stack.sourceCharacterId?.Trim()
                ?? string.Empty,
            sourceDisplayName = stack.sourceDisplayName?.Trim()
                ?? string.Empty,
            sourceSpeciesTag = stack.sourceSpeciesTag?.Trim()
                ?? string.Empty,
            sourceDeathReason = stack.sourceDeathReason?.Trim()
                ?? string.Empty,
            emergencyButcheryAllowed = stack.emergencyButcheryAllowed,
            wasteOrigin = stack.wasteOrigin,
            contamination = Mathf.Clamp(stack.contamination, 0f, 100f)
        };
    }
}
