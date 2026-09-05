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

public interface IWarehousePhysicalMassQueryPort
{
    int PhysicalItemStackVersion { get; }
    long PhysicalMassAuthorityRevision { get; }
    long GetWarehouseStoredMassGrams(BuildingInstanceId warehouseId);
    long GetWarehouseStoredMassRevision(BuildingInstanceId warehouseId);
    long GetDefinitionUnitMassGrams(string itemDefinitionId);
}

public interface IWarehouseMassCapacityQuery
{
    long StoredMassGrams { get; }
    long ReservedInboundMassGrams { get; }
    long MaxMassGrams { get; }
    long RemainingMassGrams { get; }
}

public interface IWarehouseMassAdmissionLedgerQuery
{
    long Revision { get; }
    long GetWarehouseCapacityRevision(BuildingInstanceId warehouseId);
    long GetReservedInboundMassGrams(BuildingInstanceId warehouseId);
}

public readonly struct WarehouseLifecycleOccupancySnapshot
{
    public WarehouseLifecycleOccupancySnapshot(
        long storedMassGrams,
        long reservedInboundMassGrams,
        int referencedPhysicalStackCount,
        int activeHaulIntentCount)
    {
        StoredMassGrams = Math.Max(0L, storedMassGrams);
        ReservedInboundMassGrams = Math.Max(0L, reservedInboundMassGrams);
        ReferencedPhysicalStackCount = Math.Max(0, referencedPhysicalStackCount);
        ActiveHaulIntentCount = Math.Max(0, activeHaulIntentCount);
    }

    public long StoredMassGrams { get; }
    public long ReservedInboundMassGrams { get; }
    public int ReferencedPhysicalStackCount { get; }
    public int ActiveHaulIntentCount { get; }
    public bool IsEmpty => StoredMassGrams == 0L
        && ReservedInboundMassGrams == 0L
        && ReferencedPhysicalStackCount == 0
        && ActiveHaulIntentCount == 0;
}

public interface IWarehouseLifecycleOccupancyQuery
{
    bool TryRequireEmpty(
        IWarehouseFacility warehouse,
        out WarehouseLifecycleOccupancySnapshot occupancy,
        out string failureReason);
}

public interface IWarehouseStockCategoryCatalogPort
{
    IReadOnlyList<StockCategoryDefinition> All { get; }
    bool TryGet(StockCategory category, out StockCategoryDefinition definition);
}

public interface IWarehouseInventoryPort
{
    int TotalStock { get; }
    bool RestrictsCategory { get; }
    StockCategory AcceptedCategory { get; }
    IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock();
    int GetStock(StockCategory category);
    bool HasStock(StockCategory category);
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
    bool TryRequestRestock(out string resultMessage);
    bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int availableAmount,
        out string failureReason);
    bool TryReceiveExactRetailLots(
        IReadOnlyList<RetailStockLotSnapshot> incoming,
        int requestedAmount,
        out int received,
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
public class WarehouseInventory : IWarehouseInventoryPort, IWarehouseMassCapacityQuery
{
    [NonSerialized] private IWarehousePhysicalStockQueryPort physicalStockQuery;
    [NonSerialized] private IWarehousePhysicalMassQueryPort physicalMassQuery;
    [NonSerialized] private IWarehouseStockCategoryCatalogPort categoryCatalog;
    [NonSerialized] private IWarehouseMassAdmissionLedgerQuery massAdmissionLedger;
    [NonSerialized] private BuildingInstanceId warehouseId;
    [NonSerialized] private int cachedStoredMassItemStackVersion = int.MinValue;
    [NonSerialized] private long cachedStoredMassAuthorityRevision = long.MinValue;
    [NonSerialized] private long cachedStoredMassGrams;
    [NonSerialized] private long maxStoredMassGrams;
    [SerializeField] private bool restrictCategory;
    [SerializeField] private StockCategory acceptedCategory;

    public int TotalStock => physicalStockQuery != null
        ? physicalStockQuery.GetWarehouseTotal(warehouseId)
        : 0;
    public bool HasMassCapacityAuthority => maxStoredMassGrams > 0L;
    public long StoredMassGrams
    {
        get
        {
            if (!HasMassCapacityAuthority)
            {
                return 0L;
            }

            IWarehousePhysicalMassQueryPort query = RequirePhysicalMassQuery();
            int itemStackVersion = query.PhysicalItemStackVersion;
            long massAuthorityRevision = query.PhysicalMassAuthorityRevision;
            if (cachedStoredMassItemStackVersion == itemStackVersion
                && cachedStoredMassAuthorityRevision == massAuthorityRevision)
            {
                return cachedStoredMassGrams;
            }

            long storedMassGrams = query.GetWarehouseStoredMassGrams(warehouseId);
            cachedStoredMassGrams = storedMassGrams;
            cachedStoredMassItemStackVersion = itemStackVersion;
            cachedStoredMassAuthorityRevision = massAuthorityRevision;
            return storedMassGrams;
        }
    }
    public long ReservedInboundMassGrams => HasMassCapacityAuthority
        && massAdmissionLedger != null
            ? massAdmissionLedger.GetReservedInboundMassGrams(warehouseId)
            : 0L;
    public long MaxMassGrams => HasMassCapacityAuthority
        ? maxStoredMassGrams
        : 0L;
    public long RemainingMassGrams => HasMassCapacityAuthority
        ? Math.Max(0L, checked(
            maxStoredMassGrams - StoredMassGrams - ReservedInboundMassGrams))
        : 0L;
    public bool RestrictsCategory => restrictCategory;
    public StockCategory AcceptedCategory => acceptedCategory;

    public WarehouseInventory(
        long maxStoredMassGrams,
        StockCategory acceptedCategory,
        bool restrictCategory)
    {
        if (maxStoredMassGrams <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStoredMassGrams));
        }
        this.maxStoredMassGrams = maxStoredMassGrams;
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
    public int GetAcceptableQuantity(string itemDefinitionId, int requestedQuantity)
    {
        if (requestedQuantity <= 0)
        {
            return 0;
        }
        if (!HasMassCapacityAuthority)
            throw new InvalidOperationException(
                "Warehouse admission requires a positive gram-capacity authority.");

        long unitMassGrams = RequirePhysicalMassQuery()
            .GetDefinitionUnitMassGrams(itemDefinitionId);
        if (unitMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                $"Warehouse item '{itemDefinitionId}' has nonpositive canonical mass.");
        }

        long byMass = RemainingMassGrams / unitMassGrams;
        return (int)Math.Min(requestedQuantity, Math.Min(int.MaxValue, byMass));
    }

    public bool CanStoreItem(string itemDefinitionId, int quantity) =>
        quantity > 0 && GetAcceptableQuantity(itemDefinitionId, quantity) >= quantity;
    public bool Accepts(StockCategory category) =>
        !restrictCategory || category == acceptedCategory;

    public WarehouseInventorySnapshot CreateSnapshot()
    {
        return new WarehouseInventorySnapshot
        {
            version = WarehouseInventorySnapshot.CurrentVersion,
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
        physicalMassQuery = stockQuery as IWarehousePhysicalMassQueryPort;
        cachedStoredMassItemStackVersion = int.MinValue;
        cachedStoredMassAuthorityRevision = long.MinValue;
        cachedStoredMassGrams = 0L;
        if (physicalMassQuery == null)
        {
            throw new InvalidOperationException(
                "A mass-authoritative warehouse requires IWarehousePhysicalMassQueryPort.");
        }
    }

    public void BindMassAdmissionLedger(
        IWarehouseMassAdmissionLedgerQuery admissionLedger)
    {
        if (!HasMassCapacityAuthority)
            throw new InvalidOperationException(
                "Warehouse gram admission requires a positive mass capacity.");
        if (admissionLedger == null)
        {
            throw new ArgumentNullException(nameof(admissionLedger));
        }
        if (massAdmissionLedger != null
            && !ReferenceEquals(massAdmissionLedger, admissionLedger))
        {
            throw new InvalidOperationException(
                "Warehouse gram admission ledger is already bound to another authority.");
        }
        massAdmissionLedger = admissionLedger;
    }

    private IWarehousePhysicalMassQueryPort RequirePhysicalMassQuery() =>
        physicalMassQuery
        ?? throw new InvalidOperationException(
            "Warehouse canonical mass query is not bound.");
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WarehouseInventorySnapshot
{
    public const int CurrentVersion = 4;
    public int version = CurrentVersion;
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
