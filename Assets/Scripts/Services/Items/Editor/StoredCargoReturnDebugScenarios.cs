#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using UnityEngine;

public static class StoredCargoReturnDebugScenarios
{
    private const string ActorId = "character:qa:stored-cargo-return";
    private const string OperationId = "memory-erasure:qa:stored-cargo-return";

    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "Stored cargo validation requires isolated batch execution.");
        VerifyReturn(sourceIdentityTransferred: true);
        VerifyReturn(sourceIdentityTransferred: false);
        Debug.Log("ARCHITECTURE_STORED_CARGO_RETURN=PASS; whole identity and split child; capacity rejection unchanged; post-mutation fault rollback; same-operation retry");
    }

    private static void VerifyReturn(bool sourceIdentityTransferred)
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore());
        PhysicalStockQuery stock = new(repository, catalog, mass);
        BuildingInstanceId warehouseId = (BuildingInstanceId)"building:qa:stored-cargo-return";
        long unitMass = mass.GetQuantityMass(
            (ItemDefinitionId)MemoryErasureSealItemRules.ItemId,
            PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)MemoryErasureSealItemRules.ItemId),
            1).Value;
        WarehouseInventory inventory = new(
            unitMass * (sourceIdentityTransferred ? 1 : 2),
            StockCategory.General, restrictCategory: false);
        inventory.BindPhysicalStock(stock, warehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        Warehouse warehouse = new(warehouseId, inventory);
        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        world.RegisterWarehouse(warehouse);
        GameObject actorObject = new("Stored cargo return fixture");
        actorObject.SetActive(false);
        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = actorObject.AddComponent<CharacterCarryInventory>();
            WarehouseMassAdmissionService admission = new(
                catalog, mass, stock, new WarehouseWorld(warehouse),
                new UnityGameClock(), repository);
            FaultingMarkers markers = new();
            ItemQuantityReservationService reservations = new(
                repository, markers, new UnityGameClock());
            StoredCargoReturnService returns = new(
                catalog, new CharacterIds(), markers, reservations,
                repository, admission, world);
            string sourceId = sourceIdentityTransferred ? string.Empty
                : repository.AddEditorTestStack(
                    MemoryErasureSealItemRules.ItemId, 1,
                    WorldItemStackState.Stored, destinationId);
            string carriedId = repository.AddEditorTestStack(
                MemoryErasureSealItemRules.ItemId, 1, WorldItemStackState.Carried,
                ActorId, sourceStorageDestinationId: destinationId);
            if (sourceIdentityTransferred)
                sourceId = carriedId;
            carry.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = carriedId,
                        sourceStackId = sourceId,
                        ownerOperationId = OperationId,
                        itemId = MemoryErasureSealItemRules.ItemId,
                        quantity = 1
                    }
                }
            });
            string blockerId = repository.AddEditorTestStack(
                MemoryErasureSealItemRules.ItemId, 1,
                WorldItemStackState.Stored, destinationId);
            string before = PhysicalSignature(repository);
            Debug.Log("ARCHITECTURE_PHYSICAL_RECORD_COMPARISON: legacyUnityFields="
                + JsonUtility.ToJson(repository.RecordsById[carriedId])
                    .Contains(carriedId, StringComparison.Ordinal));
            Require(before.Contains(carriedId, StringComparison.Ordinal),
                "Physical comparison did not capture the actual stack fields.");
            string carryBefore = JsonUtility.ToJson(carry.Capture());
            Require(!returns.TryReturnCarriedMemoryErasureSealToStoredSource(
                    actor, carry, OperationId, out string capacityFailure)
                && !string.IsNullOrEmpty(capacityFailure)
                && PhysicalSignature(repository) == before
                && JsonUtility.ToJson(carry.Capture()) == carryBefore
                && admission.GetReservedInboundMassGrams(warehouseId) == 0,
                "Full warehouse return changed cargo or physical stock: " + capacityFailure);

            repository.RemoveEditorTestStack(blockerId);
            before = PhysicalSignature(repository);
            markers.FailNextRefresh = true;
            Require(!returns.TryReturnCarriedMemoryErasureSealToStoredSource(
                    actor, carry, OperationId, out string faultFailure)
                && faultFailure.Contains("stored-return-marker-fault", StringComparison.Ordinal)
                && !markers.FailNextRefresh
                && PhysicalSignature(repository) == before
                && JsonUtility.ToJson(carry.Capture()) == carryBefore
                && admission.GetReservedInboundMassGrams(warehouseId) == 0,
                "Post-mutation return fault did not restore cargo and stock: " + faultFailure);
            Require(returns.TryReturnCarriedMemoryErasureSealToStoredSource(
                    actor, carry, OperationId, out string retryFailure),
                "Same-operation stored return retry failed: " + retryFailure);
            WorldItemStackRecord returned = repository.RecordsById[sourceId];
            Require(carry.Items.Count == 0
                && repository.RecordsById.Count == 1
                && returned.stackId == sourceId
                && returned.state == WorldItemStackState.Stored
                && returned.destinationId == destinationId
                && string.IsNullOrEmpty(returned.sourceStorageDestinationId)
                && returned.quantity == (sourceIdentityTransferred ? 1 : 2)
                && admission.GetReservedInboundMassGrams(warehouseId) == 0,
                "Stored return lost identity, quantity, or admission cleanup.");
        }
        finally
        {
            world.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static string PhysicalSignature(WorldItemRepository repository) =>
        string.Join("\n", repository.RecordsById.Values
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .Select(value => JsonConvert.SerializeObject(value)));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CharacterIds : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            persistentId = ActorId;
            return actor != null;
        }
        public string GetOrAssignPersistentId(CharacterActor actor) => ActorId;
    }

    private sealed class Warehouse : IWarehouseFacility
    {
        internal Warehouse(BuildingInstanceId id, WarehouseInventory inventory)
        {
            PersistentInstanceId = id;
            Inventory = inventory;
        }
        public BuildingInstanceId PersistentInstanceId { get; }
        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => true;
    }

    private sealed class WarehouseWorld : IWarehouseWorldQuery
    {
        internal WarehouseWorld(IWarehouseFacility warehouse) => Warehouses = new[] { warehouse };
        public int WarehouseVersion => 1;
        public IReadOnlyList<IWarehouseFacility> Warehouses { get; }
    }

    private sealed class FaultingMarkers : IItemMarkerPresenter
    {
        internal bool FailNextRefresh;
        public void RefreshAt(Vector2Int position)
        {
            if (!FailNextRefresh)
                return;
            FailNextRefresh = false;
            throw new InvalidOperationException("stored-return-marker-fault");
        }
        public void Initialize(IWorldItemMarkerDataSource source) { }
        public void RefreshAll(IEnumerable<Vector2Int> positions) { }
        public void Clear() { }
        public bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker)
        {
            marker = null;
            return false;
        }
    }
}
#endif
