#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class PhysicalStockQueryV18DebugScenarios
{
    private const string LumberItemId = "material:lumber";

    [MenuItem("DungeonStory/Debug/Items/Run V18 Physical Stock Query Contracts")]
    public static void RunAll()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        PhysicalStockQuery query = new(repository, catalog);
        BuildingInstanceId firstWarehouse =
            (BuildingInstanceId)"building:test-stock-query-a";
        BuildingInstanceId secondWarehouse =
            (BuildingInstanceId)"building:test-stock-query-b";
        string firstDestination =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + firstWarehouse.Value;
        string secondDestination =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + secondWarehouse.Value;
        string itemId = LumberItemId;

        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            7,
            WorldItemStackState.Stored,
            firstDestination);
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            5,
            WorldItemStackState.Stored,
            secondDestination);
        string outboundId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            3,
            WorldItemStackState.Stored,
            "facility-input:test",
            firstDestination);
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            11,
            WorldItemStackState.Loose);

        Require(query.GetWarehouseQuantity(firstWarehouse, itemId) == 10,
            "Warehouse query did not derive stored and outbound physical quantities.");
        Require(query.GetWarehouseQuantity(secondWarehouse, itemId) == 5,
            "Warehouse identities leaked stock across buildings.");
        Require(query.GetWarehouseTotal(firstWarehouse) == 10,
            "Warehouse total was not derived from physical stacks.");
        Require(query.GetGlobalQuantity(itemId) == 26,
            "Global physical stock quantity is inconsistent.");

        WorldItemRepositoryEditorAccess.RemoveStack(repository, outboundId);
        Require(query.GetWarehouseTotal(firstWarehouse) == 7,
            "The derived stock index retained removed physical state.");

        Debug.Log(
            "V18 PHYSICAL STOCK QUERY PASS: warehouse totals are rebuildable views "
            + "over physical stacks and have no independent save state.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
