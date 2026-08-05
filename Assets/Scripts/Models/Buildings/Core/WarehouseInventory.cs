using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Buildings;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IWarehousePhysicalStockQueryPort
{
    int GetWarehouseQuantity(BuildingInstanceId warehouseId, StockCategory category);
    int GetWarehouseTotal(BuildingInstanceId warehouseId);
}

public interface IWarehouseStockCategoryCatalogPort
{
    IReadOnlyList<StockCategoryDefinition> All { get; }
    bool TryGet(StockCategory category, out StockCategoryDefinition definition);
}

public interface IWarehouseInventoryPort
{
    int TotalStock { get; }
    int MaxCapacity { get; }
    int RemainingCapacity { get; }
    bool HasCapacityLimit { get; }
    bool RestrictsCategory { get; }
    StockCategory AcceptedCategory { get; }
    IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock();
    int GetStock(StockCategory category);
    bool HasStock(StockCategory category);
    bool CanStore(int amount);
    bool CanStore(StockCategory category, int amount);
    bool Accepts(StockCategory category);
    WarehouseInventorySnapshot CreateSnapshot();
    void ApplySnapshot(WarehouseInventorySnapshot snapshot);
    bool TryApplySnapshot(WarehouseInventorySnapshot snapshot, out string error);
}

public readonly struct WarehouseRestockItem
{
    public WarehouseRestockItem(ShopSaleItemDefinition definition, Sprite sprite)
    {
        Definition = definition;
        Sprite = sprite;
    }

    public ShopSaleItemDefinition Definition { get; }
    public Sprite Sprite { get; }
    public int Id => Definition.Id;
    public string Name => Definition.Name;
    public StockCategory Category => Definition.Category;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IRestockableFacility : IStockedFacility
{
    int MaxStock { get; }
    int MissingStock { get; }
    bool NeedsRestock { get; }
    int RestockFrom(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out string resultMessage);
    bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int availableAmount,
        out string failureReason);
    int ReceiveRestock(
        WarehouseRestockItem saleItem,
        int amount,
        int requestedAmount,
        out string resultMessage);
    bool HasRestockSupply(
        IEnumerable<IWarehouseFacility> warehouses,
        out string failureReason);
    bool HasRestockSupply(
        IReadOnlyList<IWarehouseFacility> warehouses,
        out string failureReason);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWarehouseFacility
{
    BuildingInstanceId PersistentInstanceId { get; }
    WarehouseInventory Inventory { get; }
    bool HasWarehouseInventory { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class WarehouseInventory : IWarehouseInventoryPort
{
    [NonSerialized] private IWarehousePhysicalStockQueryPort physicalStockQuery;
    [NonSerialized] private IWarehouseStockCategoryCatalogPort categoryCatalog;
    [NonSerialized] private BuildingInstanceId warehouseId;
    [SerializeField] private int maxCapacity;
    [SerializeField] private bool restrictCategory;
    [SerializeField] private StockCategory acceptedCategory;

    public int TotalStock => physicalStockQuery != null
        ? physicalStockQuery.GetWarehouseTotal(warehouseId)
        : 0;
    public int MaxCapacity => maxCapacity > 0 ? maxCapacity : int.MaxValue;
    public int RemainingCapacity => Mathf.Max(0, MaxCapacity - TotalStock);
    public bool HasCapacityLimit => maxCapacity > 0;
    public bool RestrictsCategory => restrictCategory;
    public StockCategory AcceptedCategory => acceptedCategory;

    public WarehouseInventory()
    {
    }

    public WarehouseInventory(int maxCapacity)
    {
        this.maxCapacity = Mathf.Max(0, maxCapacity);
    }

    public WarehouseInventory(
        int maxCapacity,
        StockCategory acceptedCategory,
        bool restrictCategory)
    {
        this.maxCapacity = Mathf.Max(0, maxCapacity);
        this.acceptedCategory = acceptedCategory;
        this.restrictCategory = restrictCategory;
    }

    public IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock()
    {
        IWarehouseStockCategoryCatalogPort catalog = categoryCatalog
            ?? throw new InvalidOperationException(
                $"{nameof(WarehouseInventory)} requires the authored stock-category catalog.");
        return catalog.All
            .Select(definition => new KeyValuePair<StockCategory, int>(
                definition.Category,
                GetStock(definition.Category)))
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => catalog.TryGet(
                pair.Key,
                out StockCategoryDefinition definition)
                    ? definition.SortOrder
                    : int.MaxValue)
            .ThenBy(pair => Convert.ToInt32(pair.Key, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public int GetStock(StockCategory category) => physicalStockQuery != null
        ? physicalStockQuery.GetWarehouseQuantity(warehouseId, category)
        : 0;

    public bool HasStock(StockCategory category) => GetStock(category) > 0;
    public bool CanStore(int amount) => RemainingCapacity >= Mathf.Max(0, amount);
    public bool CanStore(StockCategory category, int amount) =>
        Accepts(category) && CanStore(amount);
    public bool Accepts(StockCategory category) =>
        !restrictCategory || category == acceptedCategory;

    public WarehouseInventorySnapshot CreateSnapshot()
    {
        return new WarehouseInventorySnapshot
        {
            version = WarehouseInventorySnapshot.CurrentVersion,
            maxCapacity = maxCapacity,
            restrictCategory = restrictCategory,
            acceptedCategoryId = StockCategoryPersistenceId.ToId(acceptedCategory)
        };
    }

    public void ApplySnapshot(WarehouseInventorySnapshot snapshot)
    {
        if (!TryApplySnapshot(snapshot, out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryApplySnapshot(WarehouseInventorySnapshot snapshot, out string error)
    {
        if (snapshot == null)
        {
            error = "Warehouse inventory snapshot is null.";
            return false;
        }
        if (snapshot.version != WarehouseInventorySnapshot.CurrentVersion)
        {
            error = $"Unsupported warehouse inventory snapshot version {snapshot.version}.";
            return false;
        }
        if (!StockCategoryPersistenceId.TryParse(
                snapshot.acceptedCategoryId,
                out StockCategory restoredAcceptedCategory))
        {
            error = $"Unknown accepted stock category id '{snapshot.acceptedCategoryId}'.";
            return false;
        }

        maxCapacity = Mathf.Max(0, snapshot.maxCapacity);
        restrictCategory = snapshot.restrictCategory;
        acceptedCategory = restoredAcceptedCategory;
        error = string.Empty;
        return true;
    }

    public void BindPhysicalStock(
        IWarehousePhysicalStockQueryPort stockQuery,
        BuildingInstanceId persistentWarehouseId,
        IWarehouseStockCategoryCatalogPort stockCategoryCatalog)
    {
        physicalStockQuery = stockQuery
            ?? throw new ArgumentNullException(nameof(stockQuery));
        warehouseId = persistentWarehouseId.IsValid
            ? persistentWarehouseId
            : throw new ArgumentException(
                "A persistent warehouse BuildingInstanceId is required.",
                nameof(persistentWarehouseId));
        categoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WarehouseInventorySnapshot
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public int maxCapacity;
    public bool restrictCategory;
    public string acceptedCategoryId =
        StockCategoryPersistenceId.ToId(StockCategory.General);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class StockCategoryPersistenceId
{
    public static string ToId(StockCategory category)
    {
        return category switch
        {
            StockCategory.Food => "stock:food",
            StockCategory.General => "stock:general",
            StockCategory.Weapon => "stock:weapon",
            StockCategory.Mana => "stock:mana",
            StockCategory.Water => "stock:water",
            StockCategory.Medicine => "stock:medicine",
            StockCategory.Fuel => "stock:fuel",
            StockCategory.Ammunition => "stock:ammunition",
            StockCategory.Biological => "stock:biological",
            StockCategory.Knowledge => "stock:knowledge",
            StockCategory.Blueprint => "stock:blueprint",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Unknown stock-category protocol value.")
        };
    }

    public static bool TryParse(string categoryId, out StockCategory category)
    {
        switch (categoryId?.Trim())
        {
            case "stock:food": category = StockCategory.Food; return true;
            case "stock:general": category = StockCategory.General; return true;
            case "stock:weapon": category = StockCategory.Weapon; return true;
            case "stock:mana": category = StockCategory.Mana; return true;
            case "stock:water": category = StockCategory.Water; return true;
            case "stock:medicine": category = StockCategory.Medicine; return true;
            case "stock:fuel": category = StockCategory.Fuel; return true;
            case "stock:ammunition": category = StockCategory.Ammunition; return true;
            case "stock:biological": category = StockCategory.Biological; return true;
            case "stock:knowledge": category = StockCategory.Knowledge; return true;
            case "stock:blueprint": category = StockCategory.Blueprint; return true;
            default: category = default; return false;
        }
    }
}
