#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Editor-only physical-stock fixture. It binds a query source to a warehouse so
/// unrelated domain scenarios can seed physical quantities without restoring the
/// removed WarehouseInventory aggregate writer.
/// </summary>
public static class WarehousePhysicalStockEditorFixtureExtensions
{
    private static readonly ConditionalWeakTable<WarehouseInventory, FixturePhysicalStock>
        PhysicalStocks = new();

    public static int SeedPhysicalStockForTest(
        this WarehouseInventory inventory,
        StockCategory category,
        int amount)
    {
        if (inventory == null || !inventory.Accepts(category)) return 0;
        FixturePhysicalStock stock = RequirePhysicalStock(inventory);
        int accepted = (int)Math.Min(
            Math.Max(0, amount),
            Math.Min(int.MaxValue, inventory.RemainingMassGrams));
        stock.Add(category, accepted);
        return accepted;
    }

    public static int ConsumePhysicalStockForTest(
        this WarehouseInventory inventory,
        StockCategory category,
        int amount)
    {
        return inventory == null
            ? 0
            : RequirePhysicalStock(inventory).Remove(category, Mathf.Max(0, amount));
    }

    private static FixturePhysicalStock RequirePhysicalStock(
        WarehouseInventory inventory)
    {
        if (PhysicalStocks.TryGetValue(inventory, out FixturePhysicalStock existing))
        {
            return existing;
        }

        FixturePhysicalStock created = new();
        PhysicalStocks.Add(inventory, created);
        inventory.BindPhysicalStock(
            created,
            FixturePhysicalStock.WarehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        return created;
    }

    private sealed class FixturePhysicalStock :
        IStockQuery,
        IWarehousePhysicalMassQueryPort
    {
        public static readonly BuildingInstanceId WarehouseId =
            (BuildingInstanceId)"building:editor-physical-stock-fixture";
        private readonly Dictionary<StockCategory, int> quantities = new();
        private int revision;

        public int PhysicalItemStackVersion => revision;
        public long PhysicalMassAuthorityRevision => 0L;

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();

        public int GetGlobalQuantity(string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) =>
            quantities.TryGetValue(category, out int amount) ? amount : 0;

        public int GetWarehouseTotal(BuildingInstanceId warehouseId)
        {
            long total = 0;
            foreach (int amount in quantities.Values) total += amount;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public long GetWarehouseStoredMassGrams(
            BuildingInstanceId warehouseId) =>
            GetWarehouseTotal(warehouseId);

        public long GetWarehouseStoredMassRevision(
            BuildingInstanceId warehouseId) => revision;

        public long GetDefinitionUnitMassGrams(string itemDefinitionId) => 1L;

        public void Add(StockCategory category, int amount)
        {
            quantities[category] = GetWarehouseQuantity(WarehouseId, category) + amount;
            revision++;
        }

        public int Remove(StockCategory category, int amount)
        {
            int current = GetWarehouseQuantity(WarehouseId, category);
            int removed = Math.Min(current, amount);
            quantities[category] = current - removed;
            if (removed > 0)
            {
                revision++;
            }
            return removed;
        }
    }
}
#endif
