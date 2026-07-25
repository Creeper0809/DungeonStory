using System;
using System.Collections.Generic;
using System.Linq;

public interface IFacilityEvolutionWarehouseInventoryQuery
{
    IReadOnlyList<WarehouseInventory> GetInventories();
}

public sealed class RegistryFacilityEvolutionWarehouseInventoryQuery :
    IFacilityEvolutionWarehouseInventoryQuery
{
    private readonly IWarehouseWorldQuery warehouseWorld;

    public RegistryFacilityEvolutionWarehouseInventoryQuery(IWarehouseWorldQuery warehouseWorld)
    {
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
    }

    public IReadOnlyList<WarehouseInventory> GetInventories()
    {
        return warehouseWorld.Warehouses
            .Where(warehouse => warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            .OrderBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.y : int.MaxValue)
            .ThenBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.x : int.MaxValue)
            .ThenBy(warehouse =>
                warehouse is BuildableObject building ? building.GetInstanceID() : 0)
            .Select(warehouse => warehouse.Inventory)
            .ToArray();
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
        foreach (WarehouseInventory inventory in GetInventories())
        {
            available += inventory.GetStock(category);
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

        WarehouseInventory[] inventories = GetInventories();
        if (inventories.Sum(inventory => (long)inventory.GetStock(category)) < amount)
        {
            return false;
        }

        int remaining = amount;
        List<Withdrawal> withdrawals = new List<Withdrawal>();
        foreach (WarehouseInventory inventory in inventories)
        {
            int withdrawn = inventory.Withdraw(category, remaining);
            if (withdrawn > 0)
            {
                withdrawals.Add(new Withdrawal(inventory, withdrawn));
                remaining -= withdrawn;
            }

            if (remaining == 0)
            {
                return true;
            }
        }

        foreach (Withdrawal withdrawal in withdrawals)
        {
            withdrawal.Inventory.AddStock(category, withdrawal.Amount);
        }

        return false;
    }

    private WarehouseInventory[] GetInventories()
    {
        return inventoryQuery.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray();
    }

    private readonly struct Withdrawal
    {
        public Withdrawal(WarehouseInventory inventory, int amount)
        {
            Inventory = inventory;
            Amount = amount;
        }

        public WarehouseInventory Inventory { get; }
        public int Amount { get; }
    }
}
