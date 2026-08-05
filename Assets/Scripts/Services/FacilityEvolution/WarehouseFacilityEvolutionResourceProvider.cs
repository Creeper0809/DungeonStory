using System;
using System.Collections.Generic;
using System.Linq;

public interface IFacilityEvolutionWarehouseInventoryQuery
{
    IReadOnlyList<IWarehouseFacility> GetWarehouses();
    int Consume(StockCategory category, int amount);
}

public sealed class RegistryFacilityEvolutionWarehouseInventoryQuery :
    IFacilityEvolutionWarehouseInventoryQuery
{
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IWorldItemStackRuntime items;

    public RegistryFacilityEvolutionWarehouseInventoryQuery(
        IWarehouseWorldQuery warehouseWorld,
        IWorldItemStackRuntime items)
    {
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public int Consume(StockCategory category, int amount) =>
        items.ConsumeAcross(GetWarehouses(), category, amount);

    public IReadOnlyList<IWarehouseFacility> GetWarehouses()
    {
        return warehouseWorld.Warehouses
            .Where(warehouse => warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            .OrderBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.y : int.MaxValue)
            .ThenBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.x : int.MaxValue)
            .ThenBy(RequirePersistentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequirePersistentId(IWarehouseFacility warehouse)
    {
        BuildingInstanceId id = warehouse.PersistentInstanceId;
        if (!id.IsValid)
        {
            throw new InvalidOperationException(
                "Facility-evolution warehouse has no persistent building ID.");
        }
        return id.Value;
    }
}

public sealed class WarehouseFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider
{
    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;

    public WarehouseFacilityEvolutionResourceProvider(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery)
    {
        this.inventoryQuery = inventoryQuery
            ?? throw new ArgumentNullException(nameof(inventoryQuery));
    }

    public bool HasMaterial(string materialId, int amount)
    {
        if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
        {
            return true;
        }

        if (!StockCategoryPersistenceId.TryParse(materialId, out StockCategory category))
        {
            return false;
        }

        long available = 0;
        foreach (IWarehouseFacility warehouse in GetWarehouses())
        {
            available += warehouse.Inventory.GetStock(category);
            if (available >= amount)
            {
                return true;
            }
        }

        return false;
    }

    public bool ConsumeMaterial(string materialId, int amount)
    {
        if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
        {
            return true;
        }

        if (!StockCategoryPersistenceId.TryParse(materialId, out StockCategory category))
        {
            return false;
        }

        IWarehouseFacility[] warehouses = GetWarehouses();
        if (warehouses.Sum(warehouse => (long)warehouse.Inventory.GetStock(category)) < amount)
        {
            return false;
        }

        return inventoryQuery.Consume(category, amount) == amount;
    }

    private IWarehouseFacility[] GetWarehouses()
    {
        return inventoryQuery.GetWarehouses()
            .Where(warehouse => warehouse?.Inventory != null)
            .ToArray();
    }
}
