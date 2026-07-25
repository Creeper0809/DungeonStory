using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffensePreparationSnapshot
{
    public OffensePreparationSnapshot(
        OffenseExpeditionPreparation preparation,
        IReadOnlyDictionary<OffenseSupplyType, int> availableSupplies)
    {
        Preparation = preparation ?? new OffenseExpeditionPreparation();
        AvailableSupplies = availableSupplies != null
            ? new Dictionary<OffenseSupplyType, int>(availableSupplies)
            : Enum.GetValues(typeof(OffenseSupplyType))
                .Cast<OffenseSupplyType>()
                .ToDictionary(type => type, _ => 0);
    }

    public OffenseExpeditionPreparation Preparation { get; }
    public IReadOnlyDictionary<OffenseSupplyType, int> AvailableSupplies { get; }

    public int GetAvailable(OffenseSupplyType type)
    {
        return AvailableSupplies.TryGetValue(type, out int amount) ? amount : 0;
    }
}

public interface IOffensePreparationService
{
    OffensePreparationSnapshot Evaluate();
    bool TryCommitLoadout(
        OffenseSupplyLoadout loadout,
        OffenseExpeditionPreparation preparation,
        string packageId,
        out string message);
    void ConsumePackedSupplies(string packageId);
    void ReturnSupplies(OffenseSupplyLoadout loadout, string packageId = "");
    void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot);
}

public sealed class DungeonOffensePreparationService : IOffensePreparationService
{
    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;
    private readonly IWorldItemStackRuntime itemStackRuntime;

    public DungeonOffensePreparationService(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery,
        IWorldItemStackRuntime itemStackRuntime)
    {
        this.inventoryQuery = inventoryQuery ?? throw new ArgumentNullException(nameof(inventoryQuery));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public OffensePreparationSnapshot Evaluate()
    {
        return new OffensePreparationSnapshot(
            new OffenseExpeditionPreparation(),
            CaptureAvailableSupplies());
    }

    public bool TryCommitLoadout(
        OffenseSupplyLoadout loadout,
        OffenseExpeditionPreparation preparation,
        string packageId,
        out string message)
    {
        loadout ??= new OffenseSupplyLoadout();
        preparation ??= new OffenseExpeditionPreparation();
        if (loadout.TotalCount > preparation.SupplyCapacity)
        {
            message = $"보급 한도를 초과했습니다. ({loadout.TotalCount}/{preparation.SupplyCapacity})";
            return false;
        }

        WarehouseInventory[] inventories = GetInventories();
        foreach (OffenseSupplyType type in Enum.GetValues(typeof(OffenseSupplyType)))
        {
            StockCategory category = OffenseSupplyCatalog.GetStockCategory(type);
            int required = loadout.Get(type);
            if (inventories.Sum(inventory => (long)inventory.GetStock(category)) < required)
            {
                message = $"{OffenseSupplyCatalog.GetDisplayName(type)} 재고가 부족합니다.";
                return false;
            }
        }

        List<Withdrawal> withdrawals = new List<Withdrawal>();
        foreach (OffenseSupplyType type in Enum.GetValues(typeof(OffenseSupplyType)))
        {
            StockCategory category = OffenseSupplyCatalog.GetStockCategory(type);
            int remaining = loadout.Get(type);
            foreach (WarehouseInventory inventory in inventories)
            {
                int amount = inventory.Withdraw(category, remaining);
                if (amount > 0)
                {
                    withdrawals.Add(new Withdrawal(inventory, category, amount));
                    remaining -= amount;
                }
                if (remaining <= 0) break;
            }

            if (remaining <= 0) continue;
            Rollback(withdrawals);
            message = "보급품을 인출하는 동안 재고가 변경되어 출발을 취소했습니다.";
            return false;
        }

        message = "던전 창고에서 원정 보급품을 실었습니다.";
        if (!TrySpawnPackedSupplies(loadout, packageId, withdrawals, out string packMessage))
        {
            message = packMessage;
            return false;
        }

        return true;
    }

    public void ConsumePackedSupplies(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return;
        }

        itemStackRuntime.RemoveStacksByStateAndDestination(
            WorldItemStackState.ExpeditionPacked,
            packageId);
    }

    public void ReturnSupplies(OffenseSupplyLoadout loadout, string packageId = "")
    {
        if (loadout == null) return;
        ConsumePackedSupplies(packageId);
        foreach (KeyValuePair<OffenseSupplyType, int> pair in loadout.Amounts)
        {
            Deposit(OffenseSupplyCatalog.GetStockCategory(pair.Key), pair.Value);
        }
    }

    public void DepositLoot(IReadOnlyDictionary<StockCategory, int> loot)
    {
        if (loot == null) return;
        foreach (KeyValuePair<StockCategory, int> pair in loot)
        {
            Deposit(pair.Key, pair.Value);
        }
    }

    private bool TrySpawnPackedSupplies(
        OffenseSupplyLoadout loadout,
        string packageId,
        IReadOnlyList<Withdrawal> withdrawals,
        out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(packageId)
            || loadout == null)
        {
            return true;
        }

        foreach (KeyValuePair<OffenseSupplyType, int> pair in loadout.Amounts)
        {
            int amount = Mathf.Max(0, pair.Value);
            if (amount <= 0)
            {
                continue;
            }

            StockCategory category = OffenseSupplyCatalog.GetStockCategory(pair.Key);
            bool spawnedAny = itemStackRuntime.SpawnStockAtDropoff(
                category,
                amount,
                $"expedition:{packageId}",
                WorldItemStackState.ExpeditionPacked,
                packageId,
                out int spawned);
            if (!spawnedAny || spawned < amount)
            {
                itemStackRuntime.RemoveStacksByStateAndDestination(
                    WorldItemStackState.ExpeditionPacked,
                    packageId);
                Rollback(withdrawals);
                message = "Expedition supplies could not be packed at the entrance.";
                return false;
            }
        }

        return true;
    }

    private IReadOnlyDictionary<OffenseSupplyType, int> CaptureAvailableSupplies()
    {
        WarehouseInventory[] inventories = GetInventories();
        return Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(
                type => type,
                type => inventories.Sum(inventory => inventory.GetStock(
                    OffenseSupplyCatalog.GetStockCategory(type))));
    }

    private void Deposit(StockCategory category, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
        {
            return;
        }

        if (itemStackRuntime.SpawnStockAtDropoff(
                category,
                remaining,
                "expedition-return",
                out int spawned))
        {
            remaining -= spawned;
        }

        if (remaining <= 0)
        {
            return;
        }

        foreach (WarehouseInventory inventory in GetInventories()
            .Where(value => value.Accepts(category))
            .OrderByDescending(value => value.RemainingCapacity))
        {
            remaining -= inventory.Deposit(category, remaining);
            if (remaining <= 0) break;
        }
    }

    private WarehouseInventory[] GetInventories()
    {
        return inventoryQuery.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray();
    }

    private static bool IsOperational(BuildableObject building)
    {
        return building != null
            && building.BuildingData != null
            && !building.isDestroy
            && !building.IsDamaged
            && building.gameObject.activeInHierarchy;
    }

    private static void Rollback(IEnumerable<Withdrawal> withdrawals)
    {
        foreach (Withdrawal withdrawal in withdrawals)
        {
            withdrawal.Inventory.AddStock(withdrawal.Category, withdrawal.Amount);
        }
    }

    private readonly struct Withdrawal
    {
        public Withdrawal(WarehouseInventory inventory, StockCategory category, int amount)
        {
            Inventory = inventory;
            Category = category;
            Amount = amount;
        }

        public WarehouseInventory Inventory { get; }
        public StockCategory Category { get; }
        public int Amount { get; }
    }
}
