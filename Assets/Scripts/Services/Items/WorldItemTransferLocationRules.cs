using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal static class WorldItemTransferLocationRules
{

    internal static IEnumerable<IWarehouseFacility> GetWarehouses(
        IWarehouseWorldQuery worldRegistry)
    {
        foreach (IWarehouseFacility warehouse in worldRegistry.Warehouses)
        {
            if (warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            {
                yield return warehouse;
            }
        }
    }

    internal static string GetWarehouseStorageDestinationId(
        IWarehouseFacility warehouse)
    {
        return WarehouseStorageIdentity.RequireDestinationId(warehouse);
    }

    internal static Vector2Int ResolveWarehouseStoragePosition(
        IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
    }

    internal static bool IsExactLiveWarehouseOwner(
        IWarehouseFacility warehouse,
        string expectedDestinationId,
        Vector2Int expectedPosition)
    {
        if (warehouse == null
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null
            || warehouse is BuildableObject building
                && (building.isDestroy || building.IsGridDestroyed)
            || WorldItemTransferLocationRules.ResolveWarehouseStoragePosition(warehouse) != expectedPosition)
        {
            return false;
        }
        try
        {
            return string.Equals(
                WarehouseStorageIdentity.RequireDestinationId(warehouse),
                expectedDestinationId,
                StringComparison.Ordinal);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static Vector2Int ResolveActorGridPosition(CharacterActor actor)
    {
        return actor != null ? actor.GetNowXY() : Vector2Int.zero;
    }
}
