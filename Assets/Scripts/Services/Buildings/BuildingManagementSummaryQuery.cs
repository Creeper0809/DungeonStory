using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct BuildingManagementSummary
{
    public BuildingManagementSummary(
        int totalBuildings,
        int visitorFacilities,
        int workableFacilities,
        int damagedBuildings)
    {
        TotalBuildings = totalBuildings;
        VisitorFacilities = visitorFacilities;
        WorkableFacilities = workableFacilities;
        DamagedBuildings = damagedBuildings;
    }

    public int TotalBuildings { get; }
    public int VisitorFacilities { get; }
    public int WorkableFacilities { get; }
    public int DamagedBuildings { get; }
}

public readonly struct ShopManagementSummary
{
    public ShopManagementSummary(int totalShops, int stockedShops, int emptyShops)
    {
        TotalShops = totalShops;
        StockedShops = stockedShops;
        EmptyShops = emptyShops;
    }

    public int TotalShops { get; }
    public int StockedShops { get; }
    public int EmptyShops { get; }
}

public readonly struct WarehouseManagementSummary
{
    public WarehouseManagementSummary(
        int warehouseCount,
        int totalStock,
        int totalCapacity,
        IReadOnlyDictionary<StockCategory, int> stockByCategory)
    {
        WarehouseCount = warehouseCount;
        TotalStock = totalStock;
        TotalCapacity = totalCapacity;
        StockByCategory = stockByCategory != null
            ? new Dictionary<StockCategory, int>(stockByCategory)
            : new Dictionary<StockCategory, int>();
    }

    public int WarehouseCount { get; }
    public int TotalStock { get; }
    public int TotalCapacity { get; }
    public IReadOnlyDictionary<StockCategory, int> StockByCategory { get; }
    public bool HasCapacityLimit => TotalCapacity > 0;

    public int GetStock(StockCategory category)
    {
        return StockByCategory.TryGetValue(category, out int amount) ? amount : 0;
    }

    public IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock()
    {
        return StockByCategory
            .OrderBy((pair) => StockCategoryCatalog.TryGet(pair.Key, out StockCategoryDefinition definition)
                ? definition.SortOrder
                : int.MaxValue)
            .ThenBy((pair) => (int)pair.Key)
            .ToArray();
    }
}

public interface IBuildingManagementSummaryService
{
    BuildingManagementSummary CaptureBuildings();
    ShopManagementSummary CaptureShops();
    WarehouseManagementSummary CaptureWarehouses();
}

public sealed class BuildingManagementSummaryService : IBuildingManagementSummaryService
{
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IRetailWorldQuery retailWorld;
    private readonly IWarehouseWorldQuery warehouseWorld;

    public BuildingManagementSummaryService(
        IBuildingWorldQuery buildingWorld,
        IRetailWorldQuery retailWorld,
        IWarehouseWorldQuery warehouseWorld)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.retailWorld = retailWorld
            ?? throw new ArgumentNullException(nameof(retailWorld));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
    }

    public BuildingManagementSummary CaptureBuildings()
    {
        return BuildingManagementSummaryQuery.FromBuildings(buildingWorld.Buildings);
    }

    public ShopManagementSummary CaptureShops()
    {
        return BuildingManagementSummaryQuery.FromShops(retailWorld.RetailFacilities);
    }

    public WarehouseManagementSummary CaptureWarehouses()
    {
        IWarehouseFacility[] warehouses = warehouseWorld.Warehouses
            .Where(IsValidWarehouse)
            .ToArray();

        return BuildingManagementSummaryQuery.FromWarehouses(warehouses);
    }

    private static bool IsValidWarehouse(IWarehouseFacility warehouse)
    {
        return warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null;
    }
}

public static class BuildingManagementSummaryQuery
{
    public static BuildingManagementSummary FromBuildings(IEnumerable<BuildableObject> buildings)
    {
        BuildableObject[] snapshot = buildings?
            .Where((building) => building != null)
            .ToArray()
            ?? Array.Empty<BuildableObject>();

        int visitorFacilities = snapshot.Count((building) =>
            building.Facility != null && building.Facility.IsVisitorFacility);
        int workableFacilities = snapshot.Count((building) =>
            building.Facility != null && building.Facility.HasSupportedWorkTypes);
        int damagedBuildings = snapshot.Count((building) => building.IsDamaged);

        return new BuildingManagementSummary(
            snapshot.Length,
            visitorFacilities,
            workableFacilities,
            damagedBuildings);
    }

    public static ShopManagementSummary FromShops(IEnumerable<IRetailFacility> shops)
    {
        IRetailFacility[] snapshot = shops?
            .Where((shop) => shop != null)
            .ToArray()
            ?? Array.Empty<IRetailFacility>();

        int stockedShops = snapshot.Count((shop) => shop.HasAvailableStock);
        return new ShopManagementSummary(snapshot.Length, stockedShops, snapshot.Length - stockedShops);
    }

    public static WarehouseManagementSummary FromWarehouses(IEnumerable<IWarehouseFacility> warehouses)
    {
        IWarehouseFacility[] snapshot = warehouses?
            .Where(IsValidWarehouse)
            .ToArray()
            ?? Array.Empty<IWarehouseFacility>();

        bool hasCapacityLimit = snapshot.Any((warehouse) => warehouse.Inventory.HasCapacityLimit);
        int totalCapacity = hasCapacityLimit
            ? snapshot.Sum((warehouse) => warehouse.Inventory.HasCapacityLimit ? warehouse.Inventory.MaxCapacity : 0)
            : 0;

        Dictionary<StockCategory, int> stockByCategory = snapshot
            .SelectMany((warehouse) => warehouse.Inventory.EnumerateStock())
            .GroupBy((pair) => pair.Key)
            .ToDictionary(
                (group) => group.Key,
                (group) => group.Sum((pair) => pair.Value));

        return new WarehouseManagementSummary(
            snapshot.Length,
            snapshot.Sum((warehouse) => warehouse.Inventory.TotalStock),
            totalCapacity,
            stockByCategory);
    }

    private static bool IsValidWarehouse(IWarehouseFacility warehouse)
    {
        return warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null;
    }
}
