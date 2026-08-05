using System;
using System.Linq;

public interface IStockQuery : IWarehousePhysicalStockQueryPort
{
    System.Collections.Generic.IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    int GetGlobalQuantity(string itemDefinitionId);
    int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        string itemDefinitionId);
}

/// <summary>
/// Rebuildable read index over authoritative physical item stacks. It owns no stock and
/// deliberately has no save DTO.
/// </summary>
public sealed class PhysicalStockQuery : IStockQuery
{
    private readonly WorldItemRepository repository;
    private readonly IDungeonItemCatalogProvider catalog;

    public PhysicalStockQuery(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public System.Collections.Generic.IReadOnlyList<WorldItemStackSnapshot>
        GetAllStacks() => repository.Records
            .Where(record => record != null && record.quantity > 0)
            .Select(CreateSnapshot)
            .ToArray();

    public int GetGlobalQuantity(string itemDefinitionId)
    {
        string itemId = RequireItemId(itemDefinitionId);
        return SaturatingSum(repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
            .Select(record => record.quantity));
    }

    public int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        string itemDefinitionId)
    {
        string itemId = RequireItemId(itemDefinitionId);
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Where(record => string.Equals(
                record.itemId,
                itemId,
                StringComparison.Ordinal))
            .Select(record => record.quantity));
    }

    public int GetWarehouseQuantity(
        BuildingInstanceId warehouseId,
        StockCategory category)
    {
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Where(record => catalog.GetDefinition(record.itemId).StockCategory == category)
            .Select(record => record.quantity));
    }

    public int GetWarehouseTotal(BuildingInstanceId warehouseId)
    {
        string destinationId = RequireWarehouseDestinationId(warehouseId);
        return SaturatingSum(GetWarehouseRecords(destinationId)
            .Select(record => record.quantity));
    }

    private System.Collections.Generic.IEnumerable<WorldItemStackRecord>
        GetWarehouseRecords(string destinationId)
    {
        return repository.Records.Where(record => record != null
            && record.quantity > 0
            && record.state == WorldItemStackState.Stored
            && string.Equals(
                string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                    ? record.destinationId
                    : record.sourceStorageDestinationId,
                destinationId,
                StringComparison.Ordinal));
    }

    private WorldItemStackSnapshot CreateSnapshot(WorldItemStackRecord record)
    {
        DungeonItemDefinition definition = catalog.GetDefinition(record.itemId);
        return new WorldItemStackSnapshot
        {
            StackId = record.stackId,
            ItemInstanceId = record.itemInstanceId,
            ItemId = record.itemId,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            StockCategory = definition.StockCategory,
            Quantity = record.quantity,
            UnitPrice = definition.UnitPrice,
            UnitWeight = definition.UnitWeight,
            Sprite = definition.Sprite,
            State = record.state,
            Position = record.position,
            ReservedByPersistentId = record.reservedByPersistentId,
            DestinationId = record.destinationId,
            SourceStorageDestinationId = record.sourceStorageDestinationId,
            HasDestinationPosition = record.hasDestinationPosition,
            DestinationPosition = record.destinationPosition,
            Forbidden = record.forbidden,
            SourceCharacterId = record.sourceCharacterId,
            SourceDisplayName = record.sourceDisplayName,
            SourceSpeciesTag = record.sourceSpeciesTag,
            SourceDeathReason = record.sourceDeathReason,
            EmergencyButcheryAllowed = record.emergencyButcheryAllowed,
            WasteOrigin = record.wasteOrigin,
            Contamination = record.contamination,
            Components = record.components
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToArray()
        };
    }

    private string RequireItemId(string itemDefinitionId)
    {
        string itemId = itemDefinitionId?.Trim() ?? string.Empty;
        if (!catalog.TryGetDefinition(itemId, out _))
        {
            throw new System.Collections.Generic.KeyNotFoundException(
                $"Unknown physical item definition '{itemId}'.");
        }

        return itemId;
    }

    private static string RequireWarehouseDestinationId(
        BuildingInstanceId warehouseId)
    {
        if (!warehouseId.IsValid)
        {
            throw new ArgumentException(
                "A valid warehouse BuildingInstanceId is required.",
                nameof(warehouseId));
        }

        return WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouseId.Value;
    }

    private static int SaturatingSum(System.Collections.Generic.IEnumerable<int> values)
    {
        long total = 0L;
        foreach (int value in values)
        {
            total += Math.Max(0, value);
            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }
}
