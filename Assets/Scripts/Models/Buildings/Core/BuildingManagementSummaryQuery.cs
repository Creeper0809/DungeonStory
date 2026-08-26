using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct BuildingManagementSummary
{
    public BuildingManagementSummary(int totalBuildings, int visitorFacilities, int workableFacilities, int damagedBuildings)
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
        TotalShops = totalShops; StockedShops = stockedShops; EmptyShops = emptyShops;
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
        IReadOnlyDictionary<StockCategory, int> stockByCategory,
        int massWarehouseCount = 0,
        long totalStoredMassGrams = 0L,
        long totalMaxMassGrams = 0L)
    {
        WarehouseCount = warehouseCount; TotalStock = totalStock; TotalCapacity = totalCapacity;
        StockByCategory = stockByCategory != null ? new Dictionary<StockCategory, int>(stockByCategory) : new Dictionary<StockCategory, int>();
        MassWarehouseCount = massWarehouseCount;
        TotalStoredMassGrams = totalStoredMassGrams;
        TotalMaxMassGrams = totalMaxMassGrams;
    }
    public int WarehouseCount { get; }
    public int TotalStock { get; }
    public int TotalCapacity { get; }
    public int MassWarehouseCount { get; }
    public int LegacyCountWarehouseCount => WarehouseCount - MassWarehouseCount;
    public long TotalStoredMassGrams { get; }
    public long TotalMaxMassGrams { get; }
    public IReadOnlyDictionary<StockCategory, int> StockByCategory { get; }
    public bool HasCapacityLimit => TotalCapacity > 0;
    public bool HasMassCapacityAuthority => MassWarehouseCount > 0;
    public int GetStock(StockCategory category) => StockByCategory.TryGetValue(category, out int amount) ? amount : 0;
    public IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock() => StockByCategory.OrderBy(pair => (int)pair.Key).ToArray();
}

public readonly struct BuildingManagementSnapshot
{
    public BuildingManagementSnapshot(bool visitorFacility, bool workableFacility, bool damaged)
    { VisitorFacility = visitorFacility; WorkableFacility = workableFacility; Damaged = damaged; }
    public bool VisitorFacility { get; }
    public bool WorkableFacility { get; }
    public bool Damaged { get; }
}

public readonly struct ShopManagementSnapshot
{
    public ShopManagementSnapshot(bool stocked) { Stocked = stocked; }
    public bool Stocked { get; }
}

public readonly struct WarehouseManagementSnapshot
{
    public WarehouseManagementSnapshot(
        int totalStock,
        int capacity,
        bool hasCapacityLimit,
        IReadOnlyDictionary<StockCategory, int> stock,
        bool hasMassCapacityAuthority = false,
        long storedMassGrams = 0L,
        long maxMassGrams = 0L)
    {
        TotalStock = totalStock;
        Capacity = capacity;
        HasCapacityLimit = hasCapacityLimit;
        Stock = stock ?? throw new ArgumentNullException(nameof(stock));
        HasMassCapacityAuthority = hasMassCapacityAuthority;
        StoredMassGrams = storedMassGrams;
        MaxMassGrams = maxMassGrams;
    }
    public int TotalStock { get; }
    public int Capacity { get; }
    public bool HasCapacityLimit { get; }
    public bool HasMassCapacityAuthority { get; }
    public long StoredMassGrams { get; }
    public long MaxMassGrams { get; }
    public IReadOnlyDictionary<StockCategory, int> Stock { get; }
}

public interface IBuildingManagementWorldQuery
{
    IReadOnlyList<BuildingManagementSnapshot> CaptureBuildings();
    IReadOnlyList<ShopManagementSnapshot> CaptureShops();
    IReadOnlyList<WarehouseManagementSnapshot> CaptureWarehouses();
}

public interface IBuildingManagementSummaryService
{
    BuildingManagementSummary CaptureBuildings();
    ShopManagementSummary CaptureShops();
    WarehouseManagementSummary CaptureWarehouses();
}

public sealed class BuildingManagementSummaryService : IBuildingManagementSummaryService
{
    private readonly IBuildingManagementWorldQuery world;
    public BuildingManagementSummaryService(IBuildingManagementWorldQuery world) => this.world = world ?? throw new ArgumentNullException(nameof(world));
    public BuildingManagementSummary CaptureBuildings() => BuildingManagementSummaryQuery.FromBuildings(world.CaptureBuildings());
    public ShopManagementSummary CaptureShops() => BuildingManagementSummaryQuery.FromShops(world.CaptureShops());
    public WarehouseManagementSummary CaptureWarehouses() => BuildingManagementSummaryQuery.FromWarehouses(world.CaptureWarehouses());
}

public static class BuildingManagementSummaryQuery
{
    public static BuildingManagementSummary FromBuildings(IEnumerable<BuildingManagementSnapshot> source)
    {
        BuildingManagementSnapshot[] values = source?.ToArray() ?? Array.Empty<BuildingManagementSnapshot>();
        return new BuildingManagementSummary(values.Length, values.Count(v => v.VisitorFacility), values.Count(v => v.WorkableFacility), values.Count(v => v.Damaged));
    }
    public static ShopManagementSummary FromShops(IEnumerable<ShopManagementSnapshot> source)
    {
        ShopManagementSnapshot[] values = source?.ToArray() ?? Array.Empty<ShopManagementSnapshot>();
        int stocked = values.Count(v => v.Stocked);
        return new ShopManagementSummary(values.Length, stocked, values.Length - stocked);
    }
    public static WarehouseManagementSummary FromWarehouses(IEnumerable<WarehouseManagementSnapshot> source)
    {
        WarehouseManagementSnapshot[] values = source?.ToArray() ?? Array.Empty<WarehouseManagementSnapshot>();
        bool limited = values.Any(v => v.HasCapacityLimit);
        Dictionary<StockCategory, int> stock = values.SelectMany(v => v.Stock).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
        WarehouseManagementSnapshot[] massValues = values
            .Where(value => value.HasMassCapacityAuthority)
            .ToArray();
        return new WarehouseManagementSummary(
            values.Length,
            values.Sum(v => v.TotalStock),
            limited ? values.Where(v => v.HasCapacityLimit).Sum(v => v.Capacity) : 0,
            stock,
            massValues.Length,
            massValues.Aggregate(0L, (sum, value) => checked(sum + value.StoredMassGrams)),
            massValues.Aggregate(0L, (sum, value) => checked(sum + value.MaxMassGrams)));
    }
}
