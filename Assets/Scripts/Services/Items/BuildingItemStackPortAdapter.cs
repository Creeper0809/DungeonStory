using System;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class BuildingItemStackPortAdapter : IBuildingItemStackPort
{
    private readonly IWorldItemStackRuntime items;

    public BuildingItemStackPortAdapter(IWorldItemStackRuntime items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool SpawnStockInWarehouse(
        IBuildingWorldEntryPort warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        return items.SpawnStockInWarehouse(
            RequireWarehouse(warehouse),
            category,
            amount,
            out spawned);
    }

    private static IWarehouseFacility RequireWarehouse(
        IBuildingWorldEntryPort warehouse)
    {
        return warehouse as IWarehouseFacility
            ?? throw new ArgumentException(
                $"{nameof(IBuildingItemStackPort)} requires an {nameof(IWarehouseFacility)} target.",
                nameof(warehouse));
    }
}
