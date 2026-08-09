#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class PhysicalItemDebugScenarios
{
    private const string ReportPath = "Temp/physical-item-contracts.tsv";
    private const string LumberItemId = "material:lumber";
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string AppraisalFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF44_정밀_장착대.asset";
    private const string LineageArchiveFacilityPath =
        "Assets/Resources/SO/Building/Industrial/I18_계보_기록실.asset";

    [MenuItem("DungeonStory/Debug/Items/Run Physical Item Contracts")]
    public static void RunAll()
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("catalog_authored_stock_definition", VerifyCatalogAuthoredStockDefinition, lines, errors);
        Run("catalog_equipment_fallback", VerifyCatalogEquipmentFallback, lines, errors);
        Run("carry_weight_penalty", VerifyCarryWeightPenalty, lines, errors);
        Run("pile_sort_and_detail", VerifyPileSortAndDetail, lines, errors);
        Run("facility_delivery_buffer", VerifyFacilityDeliveryBuffer, lines, errors);
        Run("loose_material_delivery_request", VerifyLooseMaterialDeliveryRequest, lines, errors);
        Run("physical_craft_material_gate", VerifyPhysicalCraftMaterialGate, lines, errors);
        Run("customer_floor_theft", VerifyCustomerFloorTheft, lines, errors);
        Run("stack_delete_fallback", VerifyStackDeleteFallback, lines, errors);
        Run("warehouse_aggregate_view", VerifyWarehouseAggregateView, lines, errors);
        Run("warehouse_stored_physical_stack", VerifyWarehouseStoredPhysicalStack, lines, errors);
        Run("warehouse_stored_stack_consumption", VerifyWarehouseStoredStackConsumption, lines, errors);
        Run("transient_reservation_persistence", VerifyTransientReservationPersistence, lines, errors);
        Run("cancelled_destination_releases_materials", VerifyCancelledDestinationReleasesMaterials, lines, errors);
        Run("typed_persistent_item_ids", VerifyTypedPersistentItemIds, lines, errors);
        Run("equipment_instance_physical_authority",
            VerifyEquipmentInstancePhysicalAuthority,
            lines,
            errors);
        Run("equipment_module_physical_authority",
            VerifyEquipmentModulePhysicalAuthority,
            lines,
            errors);
        Run("equipment_lineage_transfer_physical_authority",
            VerifyEquipmentLineageTransferPhysicalAuthority,
            lines,
            errors);
        Run("equipment_identity_across_carry_and_storage",
            VerifyEquipmentIdentityAcrossCarryAndStorage,
            lines,
            errors);
        Run("save_v19_contract", VerifySaveV19Contract, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count == 0)
        {
            Debug.Log($"Physical item contracts PASS. Report: {ReportPath}");
        }
        else
        {
            Debug.LogError(
                $"Physical item contracts FAIL ({errors.Count}): {string.Join(" | ", errors)}. "
                + $"Report: {ReportPath}");
            throw new InvalidOperationException(
                $"Physical item contracts failed ({errors.Count}). See {ReportPath}.");
        }
    }

    private static string VerifyCatalogAuthoredStockDefinition()
    {
        ResourceDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        DungeonItemDefinition food = catalog.GetDefinition(PreservedRationItemId);
        Require(food.StockCategory == StockCategory.Food,
            $"authored item category was {food.StockCategory}");
        Require(food.UnitWeight > 0f && food.MaxStack > 0,
            "authored stock item had invalid physical data");
        return $"itemId={food.ItemId}; weight={food.UnitWeight:0.##}; maxStack={food.MaxStack}";
    }

    private static string VerifyCatalogEquipmentFallback()
    {
        string itemId = PhysicalItemIds.ForEquipment("weapon:dagger");
        Require(PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out string equipmentId),
            "equipment item id did not parse");
        Require(equipmentId == "weapon:dagger", $"parsed equipment id was {equipmentId}");
        DungeonItemDefinition definition =
            EditorItemCatalogFactory.Create().GetDefinition(itemId);
        Require(definition.ItemId == itemId, "equipment definition used the wrong item id");
        Require(definition.MaxStack == 1 && definition.UnitWeight > 0f, "equipment fallback physical data invalid");
        return $"itemId={itemId}; equipmentId={equipmentId}; maxStack={definition.MaxStack}";
    }

    private static string VerifyCarryWeightPenalty()
    {
        GameObject carrier = new GameObject("PhysicalItemCarryTest");
        try
        {
            CharacterCarryInventory inventory = carrier.AddComponent<CharacterCarryInventory>();
            TestCatalogProvider catalog = new TestCatalogProvider();
            TestHaulingSettings settings = new TestHaulingSettings(1.5f);
            bool added = inventory.TryAdd("test:heavy", "item:heavy", 10, catalog, settings, out string failure);
            Require(added, $"expected full carry add, failure={failure}");

            float baseLimit = inventory.GetBaseCarryLimit();
            float current = inventory.GetCurrentWeight(catalog);
            float max = inventory.GetMaxAllowedWeight(settings);
            float speed = inventory.GetMoveSpeedMultiplier(catalog, settings);
            Require(current > baseLimit, $"expected over base limit: current={current} base={baseLimit}");
            Require(current <= max, $"expected below max allowed: current={current} max={max}");
            Require(speed < 1f && speed >= 0.45f, $"unexpected speed penalty {speed}");

            CharacterCarryInventorySaveData snapshot = inventory.Capture();
            inventory.RemoveAllItems();
            inventory.Restore(snapshot);
            Require(inventory.GetCurrentWeight(catalog) == current, "carry inventory did not round-trip");
            return $"weight={current:0.##}/{baseLimit:0.##}/{max:0.##}; speed={speed:0.##}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carrier);
        }
    }

    private static string VerifyPileSortAndDetail()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            runtime.Restore(CreatePileSnapshot());
            Require(runtime.TryGetPileAt(new Vector2Int(2, 1), out WorldItemPileSnapshot pile),
                "expected visible pile");
            Require(pile.Stacks.Count == 3, $"stored stack leaked into default pile view: {pile.Stacks.Count}");
            Require(pile.Representative != null && pile.Representative.ItemId == "item:rich",
                $"wrong representative {pile.Representative?.ItemId}");

            IReadOnlyList<WorldItemStackSnapshot> withStored =
                runtime.GetStacksAt(new Vector2Int(2, 1), includeStored: true);
            Require(withStored.Count == 4, $"expected stored stack in detail query, got {withStored.Count}");
            Require(withStored[0].State == WorldItemStackState.Loose, "loose stack should sort first");
            Require(withStored[0].ItemId == "item:rich", "highest value loose item should sort first");
            Require(runtime.TryGetPileTargetAt(new Vector2Int(2, 1), out ItemPileInfoTarget target, out UnityEngine.Object marker)
                && target != null
                && marker == null, "pile target should resolve without a scene marker in editor contract");
            return $"visible={pile.Stacks.Count}; storedDetail={withStored.Count}; representative={pile.Representative.DisplayName}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyFacilityDeliveryBuffer()
    {
        GameObject warehouseObject = new GameObject("PhysicalItemDeliveryWarehouse");
        GameObject carrierObject = new GameObject("PhysicalItemDeliveryCarrier");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(repository, runtime.CatalogProvider));
            runtime.Start();
            Require(runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.General,
                    8,
                    out int seeded)
                    && seeded == 8,
                "warehouse physical stock seed failed");

            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "delivery-test";
            bool requested = runtime.TryRequestFacilityDelivery(
                StockCategory.General,
                3,
                new Vector2Int(4, 1),
                destinationId,
                out int requestedAmount,
                out string requestReason);
            Require(requested && requestedAmount == 3, $"delivery request failed: {requestReason}; amount={requestedAmount}");
            Require(warehouse.Inventory.GetStock(StockCategory.General) == 8,
                "warehouse stock changed before worker pickup");
            Require(!runtime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "warehouse material was dropped as a loose pile at request time");
            WorldItemStackSnapshot outboundStored = runtime.GetAllStacks().SingleOrDefault(stack =>
                stack.State == WorldItemStackState.Stored
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            Require(outboundStored != null
                    && outboundStored.Quantity == 3
                    && !string.IsNullOrWhiteSpace(outboundStored.SourceStorageDestinationId)
                    && outboundStored.HasDestinationPosition
                    && outboundStored.DestinationPosition == new Vector2Int(4, 1),
                "stored delivery reservation did not preserve source and destination");

            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                carrierObject);
            CharacterActor actor = carrierObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            Require(carry.TryAdd(
                    "test:delivery",
                    LumberItemId,
                    3,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    out string carryReason),
                $"could not seed carry inventory: {carryReason}");
            Require(runtime.TryDepositCarriedItemsToFacility(
                    actor,
                    carry,
                    new Vector2Int(4, 1),
                    destinationId,
                    out string depositReason),
                $"facility deposit failed: {depositReason}");
            Require(runtime.TryConsumeFacilityBuffer(
                    destinationId,
                    new Dictionary<StockCategory, int> { [StockCategory.General] = 3 },
                    out string consumeReason),
                $"facility buffer consume failed: {consumeReason}");
            Require(!runtime.GetAllStacks().Any(stack => stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == destinationId),
                "facility input buffer was not consumed");
            return $"requested={requestedAmount}; warehouseHeld=8; outboundStored={outboundStored.Quantity}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(carrierObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyPhysicalCraftMaterialGate()
    {
        GameObject warehouseObject = new GameObject("PhysicalCraftWarehouse");
        GameObject facilityObject = new GameObject("PhysicalCraftFacility");
        WorldItemStackRuntime itemRuntime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            itemRuntime = CreateRuntime(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipmentRuntime);
            warehouse.BindPhysicalStock(
                new PhysicalStockQuery(repository, itemRuntime.CatalogProvider));
            itemRuntime.Start();
            string materialStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                "material:iron-ingot",
                20,
                WorldItemStackState.Stored,
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                    + warehouse.PersistentInstanceId.Value);
            Require(!string.IsNullOrWhiteSpace(materialStackId),
                "physical craft material seed failed");
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());

            Require(equipmentRuntime.TryQueueCraft("weapon:dagger", facility, out string queueReason),
                $"physical craft queue failed: {queueReason}");
            Require(equipmentRuntime.CraftQueue.Count == 1, "craft order missing");
            CombatEquipmentCraftOrderSaveData order = equipmentRuntime.CraftQueue[0];
            Require(!order.materialsReady
                    && order.materialDestinationId.StartsWith(WorldItemStackRuntime.FacilityInputDestinationPrefix, StringComparison.Ordinal),
                "physical craft order did not wait for materials");
            Require(!equipmentRuntime.HasPendingCraftWork(new[] { "weapon:dagger" }),
                "craft work became available before materials arrived");

            foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks().ToArray())
            {
                itemRuntime.SpawnItemAt(
                    stack.ItemId,
                    stack.Quantity,
                    new Vector2Int(order.destinationX, order.destinationY),
                    WorldItemStackState.FacilityBuffer,
                    order.materialDestinationId,
                    out _);
                itemRuntime.DeleteStack(stack.StackId);
            }

            Require(equipmentRuntime.HasPendingCraftWork(new[] { "weapon:dagger" }),
                "craft work did not become available after materials arrived");
            int completed = equipmentRuntime.ApplyCraftWork(
                new[] { "weapon:dagger" },
                999f,
                out string completedEquipmentId);
            Require(completed == 1
                    && completedEquipmentId == "weapon:dagger"
                    && equipmentRuntime.CraftQueue.Count == 0,
                "physical craft order did not complete through the common work queue");
            return $"order={order.orderId}; completed={completedEquipmentId}";
        }
        finally
        {
            itemRuntime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyCustomerFloorTheft()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        GameObject customerObject = new GameObject("PhysicalItemTheftCustomer");
        try
        {
            runtime.Restore(new DungeonPhysicalItemSaveData
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                haulingSettings = new ItemHaulingSettingsSnapshot { maxCarryMultiplier = 1.5f },
                stacks = new List<WorldItemStackSaveData>
                {
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:stealable",
                        itemId = "item:rich",
                        quantity = 2,
                        state = WorldItemStackState.Loose,
                        gridX = 0,
                        gridY = 0
                    }
                }
            });
            runtime.Start();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                customerObject);
            CharacterActor customer = customerObject.AddComponent<CharacterActor>();
            customer.characterType = CharacterType.Customer;
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(customer)
                ?? customerObject.AddComponent<CharacterCarryInventory>();
            Require(runtime.TryStealLooseItem(customer, 0, out WorldItemStackSnapshot stolen, out string reason),
                $"floor theft failed: {reason}");
            carry = customer.GetComponent<CharacterCarryInventory>();
            Require(stolen != null && stolen.ItemId == "item:rich", "wrong stolen item");
            Require(carry.Items.Sum(item => item.quantity) == 1, "stolen item did not enter carry inventory");
            Require(runtime.GetAllStacks().Single(stack => stack.StackId == "stack:stealable").Quantity == 1,
                "world stack quantity was not reduced");
            return $"stolen={stolen.DisplayName}; carried={carry.Items.Sum(item => item.quantity)}";
        }
        finally
        {
            runtime.Dispose();
            UnityEngine.Object.DestroyImmediate(customerObject);
        }
    }

    private static string VerifyStackDeleteFallback()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            runtime.Restore(CreatePileSnapshot());
            Require(runtime.DeleteStack("stack:rich"), "failed to delete selected stack");
            Require(runtime.TryGetPileAt(new Vector2Int(2, 1), out WorldItemPileSnapshot pile),
                "pile disappeared after deleting one stack");
            Require(pile.Representative.ItemId == "item:cheap",
                $"panel should fall back to list representative, got {pile.Representative.ItemId}");
            return $"remaining={pile.Stacks.Count}; representative={pile.Representative.ItemId}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyWarehouseAggregateView()
    {
        GameObject warehouseObject = new GameObject("PhysicalStockQueryWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(repository, runtime.CatalogProvider));
            Require(runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.Food,
                    6,
                    out int spawned)
                    && spawned == 6,
                "physical warehouse stock seed failed");
            Require(warehouse.Inventory.TotalStock == 6,
                "warehouse query did not derive physical stock");

            WarehouseInventorySnapshot snapshot = warehouse.Inventory.CreateSnapshot();
            Require(snapshot.version == WarehouseInventorySnapshot.CurrentVersion
                    && snapshot.maxCapacity == warehouse.Inventory.MaxCapacity,
                "warehouse policy snapshot was not captured");
            Require(typeof(WarehouseInventorySnapshot).GetField("stocks") == null,
                "warehouse policy snapshot still owns stock quantities");
            return $"derivedStock={warehouse.Inventory.TotalStock}; capacity={warehouse.Inventory.MaxCapacity}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyWarehouseStoredPhysicalStack()
    {
        GameObject warehouseObject = new GameObject("PhysicalItemStoredMirrorWarehouse");
        GameObject carrierObject = new GameObject("PhysicalItemStoredMirrorCarrier");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(repository, runtime.CatalogProvider));
            runtime.Start();

            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                carrierObject);
            CharacterActor actor = carrierObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            string foodItemId = PreservedRationItemId;
            Require(carry.TryAdd(
                    "mirror:food",
                    foodItemId,
                    5,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    out string carryReason),
                $"could not seed carried stock: {carryReason}");
            Require(runtime.TryDepositCarriedItems(actor, carry, warehouse, out string depositReason),
                $"warehouse deposit failed: {depositReason}");
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                "warehouse aggregate did not receive carried stock");
            Require(!runtime.TryGetPileAt(Vector2Int.zero, out _),
                "stored warehouse stack leaked into default world marker view");

            IReadOnlyList<WorldItemStackSnapshot> storedHidden =
                runtime.GetStacksAt(Vector2Int.zero, includeStored: true);
            Require(storedHidden.Count == 1
                    && storedHidden[0].State == WorldItemStackState.Stored
                    && storedHidden[0].Quantity == 5,
                $"stored physical stack was not created correctly: {storedHidden.Count}");

            runtime.SetStoredItemMarkersVisible(true);
            Require(runtime.TryGetPileAt(Vector2Int.zero, out WorldItemPileSnapshot visiblePile)
                    && visiblePile.Stacks.Any(stack => stack.State == WorldItemStackState.Stored),
                "stored stack did not appear when item view was enabled");

            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "stored-mirror";
            Require(runtime.TryRequestFacilityDelivery(
                    StockCategory.Food,
                    3,
                    new Vector2Int(2, 0),
                    destinationId,
                    out int requested,
                    out string requestReason)
                    && requested == 3,
                $"delivery request failed: {requestReason}; requested={requested}");
            WorldItemStackSnapshot storedAfter = runtime
                .GetStacksAt(Vector2Int.zero, includeStored: true)
                .FirstOrDefault(stack => stack.State == WorldItemStackState.Stored
                    && string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId));
            WorldItemStackSnapshot outboundStored = runtime
                .GetStacksAt(Vector2Int.zero, includeStored: true)
                .SingleOrDefault(stack => stack.State == WorldItemStackState.Stored
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                "warehouse aggregate changed before physical pickup");
            Require(storedAfter != null && storedAfter.Quantity == 2,
                $"unassigned stored remainder was wrong: {storedAfter?.Quantity}");
            Require(outboundStored != null
                    && outboundStored.Quantity == 3
                    && !string.IsNullOrWhiteSpace(outboundStored.SourceStorageDestinationId),
                "outbound stored reservation was not created");
            Require(!runtime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "stored material became a visible loose pile");

            DungeonPhysicalItemSaveData captured = runtime.Capture();
            runtime.Restore(captured);
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                $"restore did not make warehouse aggregate follow stored stacks: {warehouse.Inventory.GetStock(StockCategory.Food)}");

            return $"stored=5; reserved=3; available=2; warehouse={warehouse.Inventory.GetStock(StockCategory.Food)}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(carrierObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyWarehouseStoredStackConsumption()
    {
        GameObject warehouseObject =
            new GameObject("PhysicalItemStoredConsumptionWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(
                warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(repository, runtime.CatalogProvider));
            runtime.Start();

            Require(
                runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.Water,
                    10,
                    out int spawned)
                && spawned == 10,
                $"warehouse water seed failed: spawned={spawned}");
            WorldItemStackSnapshot stored = runtime
                .GetAllStacks()
                .SingleOrDefault(stack =>
                    stack.State == WorldItemStackState.Stored
                    && string.Equals(
                        stack.ItemId,
                        "resource:clean-water",
                        StringComparison.Ordinal));
            Require(
                stored != null && stored.Quantity == 10,
                "stored water mirror was missing");
            Require(
                warehouse.Inventory.GetStock(StockCategory.Water) == 10,
                "warehouse water aggregate was not seeded");

            Require(
                runtime.TryConsumeStackQuantity(
                    stored.StackId,
                    1,
                    out WorldItemStackSnapshot consumed)
                && consumed != null
                && consumed.Quantity == 1,
                "stored water consumption failed");
            WorldItemStackSnapshot remaining = runtime
                .GetAllStacks()
                .SingleOrDefault(stack =>
                    string.Equals(
                        stack.StackId,
                        stored.StackId,
                        StringComparison.Ordinal));
            Require(
                remaining != null && remaining.Quantity == 9,
                $"stored water mirror did not decrement: {remaining?.Quantity}");
            Require(
                warehouse.Inventory.GetStock(StockCategory.Water) == 9,
                "warehouse aggregate did not decrement with stored water");
            return "seeded=10; consumed=1; remaining=9";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(
                warehouse);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifySaveV19Contract()
    {
        Require(DungeonGameSaveData.CurrentVersion == 23, $"save version is {DungeonGameSaveData.CurrentVersion}");
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonPhysicalItemSaveData physicalItems = CreatePileSnapshot();
        DungeonCharacterWorldSaveData characters = new DungeonCharacterWorldSaveData();
        characters.actors.Add(new DungeonCharacterSaveData
        {
            persistentId = "carry-test",
            dataId = 1,
            displayName = "Carry Test",
            carryInventory = new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new CharacterCarriedItemSaveData
                    {
                        sourceStackId = "stack:carried",
                        itemId = "item:food",
                        quantity = 3
                    }
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            save,
            PhysicalItemsSaveSection.Id,
            DungeonPhysicalItemSaveData.CurrentVersion,
            DungeonSaveRestorePhase.Items,
            physicalItems);
        DungeonSaveSectionPayload.Write(
            save,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            characters);
        DungeonSaveSectionPayload.Write(
            save,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonExteriorActivitySaveData());

        string json = JsonUtility.ToJson(save);
        DungeonGameSaveData restored = JsonUtility.FromJson<DungeonGameSaveData>(json);
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                PhysicalItemsSaveSection.Id,
                out DungeonPhysicalItemSaveData restoredItems),
            "physical item save section failed json round-trip");
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                CharacterWorldSaveSection.Id,
                out DungeonCharacterWorldSaveData restoredCharacters),
            "character save section failed json round-trip");
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData restoredExterior),
            "exterior activity save section failed json round-trip");
        Require(restoredItems.stacks.Count == 4, $"expected 4 physical stacks, got {restoredItems.stacks.Count}");
        Require(restoredCharacters.actors?.Count == 1, "character save section failed json round-trip");
        Require(restoredCharacters.actors[0].carryInventory?.items?.Count == 1,
            "carried item save section failed json round-trip");
        Require(restoredCharacters.actors[0].carryInventory.items[0].quantity == 3,
            "carried item quantity changed during json round-trip");
        Require(
            restoredExterior.version == DungeonExteriorActivitySaveData.CurrentVersion,
            "exterior activity save version changed during json round-trip");
        return $"version={DungeonGameSaveData.CurrentVersion}; stacks={restoredItems.stacks.Count}; carried=3";
    }

    private static string VerifyLooseMaterialDeliveryRequest()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        runtime.Start();
        try
        {
            string itemId = LumberItemId;
            Require(runtime.SpawnItemAt(
                    itemId,
                    5,
                    new Vector2Int(2, 0),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 5,
                $"spawned={spawned}");

            string destinationId = WorkOrderRuntime.ConstructionDestinationPrefix + "test";
            bool requested = runtime.TryRequestFacilityDelivery(
                StockCategory.General,
                3,
                new Vector2Int(6, 0),
                destinationId,
                out int requestedAmount,
                out string failure);
            Require(requested, $"request failed: {failure}");
            Require(requestedAmount == 3, $"requested={requestedAmount}");

            IReadOnlyList<WorldItemStackSnapshot> stacks = runtime.GetAllStacks();
            WorldItemStackSnapshot delivery = stacks.SingleOrDefault(stack =>
                stack.State == WorldItemStackState.Loose
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            Require(delivery != null, "destination loose stack missing");
            Require(delivery.Quantity == 3, $"delivery quantity={delivery.Quantity}");
            Require(delivery.HasDestinationPosition && delivery.DestinationPosition == new Vector2Int(6, 0),
                $"destination position={delivery.DestinationPosition}");

            WorldItemStackSnapshot remainder = stacks.SingleOrDefault(stack =>
                stack.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(stack.DestinationId));
            Require(remainder != null && remainder.Quantity == 2,
                remainder != null ? $"remainder={remainder.Quantity}" : "remainder missing");
            return $"requested={requestedAmount}; delivery={delivery.Quantity}; remainder={remainder.Quantity}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static DungeonPhysicalItemSaveData CreatePileSnapshot()
    {
        return new DungeonPhysicalItemSaveData
        {
            version = DungeonPhysicalItemSaveData.CurrentVersion,
            haulingSettings = new ItemHaulingSettingsSnapshot { maxCarryMultiplier = 1.5f },
            stacks = new List<WorldItemStackSaveData>
            {
                new WorldItemStackSaveData
                {
                    stackId = "stack:buffer",
                    itemId = "item:buffer",
                    quantity = 5,
                    state = WorldItemStackState.FacilityBuffer,
                    destinationId = "shop:1",
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:cheap",
                    itemId = "item:cheap",
                    quantity = 18,
                    state = WorldItemStackState.Loose,
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:rich",
                    itemId = "item:rich",
                    quantity = 2,
                    state = WorldItemStackState.Loose,
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:stored",
                    itemId = "item:stored",
                    quantity = 9,
                    state = WorldItemStackState.Stored,
                    destinationId = "warehouse:building:test-pile",
                    gridX = 2,
                    gridY = 1
                }
            }
        };
    }

    private static string VerifyTransientReservationPersistence()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _);
        try
        {
            runtime.Restore(CreatePileSnapshot());
            ItemReservationService reservations = new ItemReservationService(
                repository,
                EditorNullItemMarkerPresenter.Instance);
            Require(reservations.TryReserve(
                    new[] { "stack:buffer" },
                    CharacterId.Owner.Value),
                "production reservation service did not reserve the fixture stack");
            Require(runtime.GetAllStacks()
                    .Single(stack => stack.StackId == "stack:buffer")
                    .ReservedByPersistentId == CharacterId.Owner.Value,
                "live stack did not contain the transient reservation");

            DungeonPhysicalItemSaveData captured = runtime.Capture();
            WorldItemStackSaveData capturedBuffer = captured.stacks
                .Single(stack => stack.stackId == "stack:buffer");
            Require(capturedBuffer.reservedByPersistentId == string.Empty,
                "capture persisted a transient reservation");
            Require(capturedBuffer.destinationId == "shop:1",
                $"capture changed durable destination '{capturedBuffer.destinationId}'");

            string beforeInvalidRestore = JsonUtility.ToJson(captured);
            DungeonPhysicalItemSaveData invalid =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                    beforeInvalidRestore);
            invalid.stacks[0].reservedByPersistentId = "worker:missing";
            bool rejected = false;
            try
            {
                runtime.Restore(invalid);
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex.Message.Contains(
                    "transient reservation",
                    StringComparison.OrdinalIgnoreCase);
            }

            Require(rejected,
                "non-canonical saved reservation was not rejected");
            Require(JsonUtility.ToJson(runtime.Capture()) == beforeInvalidRestore,
                "invalid reservation preflight changed live physical items");
            return "live reservation omitted; invalid saved reservation rejected without mutation";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyCancelledDestinationReleasesMaterials()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            const string destinationId = "construction:test";
            DungeonPhysicalItemSaveData snapshot = new DungeonPhysicalItemSaveData
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                haulingSettings = new ItemHaulingSettingsSnapshot
                {
                    maxCarryMultiplier = 1.5f
                },
                stacks = new List<WorldItemStackSaveData>
                {
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:reserved-source",
                        itemId = LumberItemId,
                        quantity = 3,
                        state = WorldItemStackState.Loose,
                        destinationId = destinationId,
                        gridX = 2,
                        gridY = 0
                    },
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:delivered-buffer",
                        itemId = LumberItemId,
                        quantity = 2,
                        state = WorldItemStackState.FacilityBuffer,
                        destinationId = destinationId,
                        gridX = 7,
                        gridY = 0
                    }
                }
            };

            runtime.Restore(snapshot);
            Vector2Int releasePosition = new Vector2Int(7, 0);
            int released = runtime.ReleaseStacksByDestination(
                destinationId,
                releasePosition);
            WorldItemStackSnapshot[] stacks = runtime.GetAllStacks().ToArray();
            Require(released == 5, $"released={released}");
            Require(stacks.Sum(stack => stack.Quantity) == 5,
                $"quantity after release={stacks.Sum(stack => stack.Quantity)}");
            Require(stacks.All(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(stack.DestinationId)),
                "released materials were not loose and unassigned");
            Require(stacks.Any(stack =>
                    stack.Position == new Vector2Int(2, 0)
                    && stack.Quantity == 3),
                "source reservation did not return to its original cell");
            Require(stacks.Any(stack =>
                    stack.Position == releasePosition
                    && stack.Quantity == 2),
                "delivered buffer did not return at the construction cell");
            return "released=5; conserved=5; source=3; site=2";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static WorldItemStackRuntime CreateRuntime()
    {
        return CreateRuntime(out _, out _);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture()
    {
        return CreateRuntime(out _, out _);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime)
    {
        return CreateRuntime(out repository, out equipmentRuntime);
    }

    private static WorldItemStackRuntime CreateRuntime(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime)
    {
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ICombatEquipmentCatalog combatCatalog =
            new ResourceCombatEquipmentCatalog(gameContent);
        IGridSystemProvider gridProvider = new NoGridProvider();
        IDungeonItemCatalogProvider itemCatalog = new TestCatalogProvider();
        IItemHaulingSettingsProvider haulingSettings =
            new TestHaulingSettings(1.5f);
        ICharacterIdRegistry idRegistry = new TestIdRegistry();
        IGridPathSearchBroker pathBroker =
            new GridPathSearchBroker(new UnityGameClock(), doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
        ICharacterAiWorldRegistry worldRegistry =
            CharacterAiEditorTestDependencies.WorldRegistry;
        repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        IItemReservationService reservations = new ItemReservationService(
            repository,
            EditorNullItemMarkerPresenter.Instance);
        IWorldItemSpawner spawner = new WorldItemSpawner(
            itemCatalog,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        WorldItemQueryService query = new WorldItemQueryService(
            itemCatalog,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        IWorldItemHaulPlanningService haulPlanning =
            new WorldItemHaulPlanningService(
                gridProvider,
                itemCatalog,
                haulingSettings,
                idRegistry,
                pathBroker,
                worldRegistry,
                repository,
                reservations);
        EditorEquipmentPhysicalItemGatewayProxy equipmentItemGateway =
            new EditorEquipmentPhysicalItemGatewayProxy();
        equipmentRuntime = CombatEquipmentEditorTestFactory.Create(
            combatCatalog,
            repository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: new ResourceEquipmentModuleCatalog(gameContent),
            materialCatalog: new ResourceEconomyContentCatalog(gameContent),
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            itemStackRuntime: equipmentItemGateway);
        WorldItemReadServices readServices = new WorldItemReadServices(
            itemCatalog,
            haulingSettings,
            query,
            EditorNullItemMarkerPresenter.Instance,
            new EditorCharacterAiPerformanceRecorder(),
            DisabledDungeonDebugRuleQuery.Instance);
        IItemTransferService itemTransferService = new ItemTransferService(
            readServices,
            idRegistry,
            worldRegistry,
            combatCatalog,
            new GameEventBus(),
            repository,
            spawner,
            warehouseService: new WorldItemWarehouseService(
                itemCatalog,
                repository,
                worldRegistry,
                spawner,
                EditorNullItemMarkerPresenter.Instance,
                gridProvider,
                idRegistry,
                reservations));
        WorldItemStackRuntime runtime = WorldItemEditorTestFactory.Create(
            gridProvider,
            itemCatalog,
            haulingSettings,
            idRegistry,
            new NoDropZoneQuery(),
            new NoSpawnerProvider(),
            pathBroker,
            worldRegistry,
            new UnityGameClock(),
            repository,
            reservations,
            spawner,
            query,
            haulPlanning,
            itemMarkerPresenter: EditorNullItemMarkerPresenter.Instance,
            itemTransferService: itemTransferService,
            performanceRecorder: new EditorCharacterAiPerformanceRecorder());
        equipmentItemGateway.Attach(runtime);
        return runtime;
    }

    private static string VerifyTypedPersistentItemIds()
    {
        WorldItemStackRuntime source = CreateRuntime(
            out _,
            out CombatEquipmentRuntime equipment);
        string itemId = PhysicalItemIds.ForEquipment("weapon:dagger");
        CombatEquipmentInstance equipmentInstance = equipment.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Loose);
        Require(source.SpawnExistingUniqueItemAt(
                itemId,
                (ItemInstanceId)equipmentInstance.instanceId,
                new Vector2Int(3, 4),
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId)
                && equipment.TryLinkToWorldStack(
                    equipmentInstance.instanceId,
                    stackId,
                    CombatEquipmentWorldState.Loose),
            "failed to spawn the unique equipment item");

        WorldItemStackSnapshot created = source.GetAllStacks()
            .Single(stack => stack.StackId == stackId);
        Require(((ItemStackId)created.StackId).IsValid,
            "spawned stack did not receive a typed stack ID");
        Require(((ItemInstanceId)created.ItemInstanceId).IsValid,
            "unique equipment did not receive an item-instance ID");

        DungeonPhysicalItemSaveData save = source.Capture();
        WorldItemStackRuntime restoredRuntime = CreateRuntime(
            out _,
            out CombatEquipmentRuntime restoredEquipment);
        restoredRuntime.Restore(save);
        restoredEquipment.PublishRestoreCandidate(
            restoredEquipment.BuildRestoreCandidate(equipment.Capture()));
        WorldItemStackSnapshot restored = restoredRuntime.GetAllStacks()
            .Single(stack => stack.StackId == created.StackId);
        Require(restored.ItemInstanceId == created.ItemInstanceId,
            "item-instance ID changed during save round-trip");

        return $"stack={created.StackId}; instance={created.ItemInstanceId}";
    }

    private static string VerifyEquipmentInstancePhysicalAuthority()
    {
        WorldItemStackRuntime source = CreateRuntime(
            out WorldItemRepository sourceRepository,
            out CombatEquipmentRuntime sourceEquipment);
        CombatEquipmentInstance created = sourceEquipment.CreateInstance(
            "weapon:greatsword",
            CombatEquipmentQuality.Good,
            CombatEquipmentWorldState.Stored);
        Require(((ItemInstanceId)created.instanceId).IsValid,
            "equipment did not receive a typed physical item-instance ID");
        string physicalItemId = PhysicalItemIds.ForEquipment(created.definitionId);
        Require(source.SpawnExistingUniqueItemAt(
                physicalItemId,
                (ItemInstanceId)created.instanceId,
                new Vector2Int(2, 2),
                WorldItemStackState.Stored,
                "warehouse:building:physical-authority",
                out string sourceStackId)
            && sourceEquipment.TryLinkToWorldStack(
                created.instanceId,
                sourceStackId,
                CombatEquipmentWorldState.Stored),
            "equipment fixture did not create its authoritative physical stack");

        const string ModuleId = "equipment-module-instance:physical-authority";
        sourceRepository.EquipmentModules[ModuleId] = new EquipmentModuleInstance
        {
            instanceId = ModuleId,
            definitionId = "module:weapon:balanced-core",
            grade = 2,
            condition = 0.91f,
            identified = true,
            state = EquipmentModuleProcessState.Installed,
            attachedEquipmentInstanceId = created.instanceId
        };
        sourceRepository.EquipmentInstances[created.instanceId].moduleSlots =
            new List<EquipmentModuleSlotState>
            {
                new EquipmentModuleSlotState
                {
                    slotIndex = 0,
                    moduleInstanceId = ModuleId
                }
            };

        DungeonPhysicalItemSaveData physicalSave = source.Capture();
        DungeonCombatEquipmentSaveData combatSave = sourceEquipment.Capture();
        string combatJson = JsonUtility.ToJson(combatSave);
        Require(!combatJson.Contains("\"instances\"", StringComparison.Ordinal)
                && !combatJson.Contains("\"moduleInstances\"", StringComparison.Ordinal),
            "combat save still writes duplicate equipment or module instance authority");
        Require(physicalSave.version == DungeonPhysicalItemSaveData.CurrentVersion
                && physicalSave.uniqueItems.Count == 1,
            "physical save did not capture exactly one unique equipment item");

        WorldItemStackRuntime restoredItems = CreateRuntime(
            out _,
            out CombatEquipmentRuntime restoredEquipment);
        restoredItems.Restore(physicalSave);
        restoredEquipment.PublishRestoreCandidate(
            restoredEquipment.BuildRestoreCandidate(combatSave));
        Require(restoredEquipment.TryGetInstance(
                created.instanceId,
                out CombatEquipmentInstance restored)
                && restored.quality == CombatEquipmentQuality.Good
                && restored.moduleSlots.Any(slot => slot != null
                    && slot.moduleInstanceId == ModuleId),
            "equipment state did not round-trip through the physical item section");
        Require(restoredEquipment.ModuleInstances.Any(module => module != null
                && module.instanceId == ModuleId
                && Mathf.Approximately(module.condition, 0.91f)),
            "installed module did not round-trip through its owning equipment item");

        source.Dispose();
        restoredItems.Dispose();
        return $"itemInstance={created.instanceId}; physicalVersion={physicalSave.version}";
    }

    private static string VerifyEquipmentModulePhysicalAuthority()
    {
        List<GameObject> facilityObjects = new List<GameObject>();
        WorldItemStackRuntime items = CreateRuntime(
            out WorldItemRepository repository,
            out CombatEquipmentRuntime equipment);
        WorldItemStackRuntime restoredItems = null;
        try
        {
            BuildableObject appraisal = CreateEquipmentFacility(
                AppraisalFacilityPath,
                "PhysicalModuleAppraisal",
                new Vector2Int(10, 10),
                EquipmentProgressionWorkstationTags.Appraisal,
                facilityObjects);
            BuildableObject restoration = CreateEquipmentFacility(
                RestorationFacilityPath,
                "PhysicalModuleRestoration",
                new Vector2Int(12, 10),
                EquipmentProgressionWorkstationTags.Restoration,
                facilityObjects);
            BuildableObject precision = CreateEquipmentFacility(
                PrecisionFittingFacilityPath,
                "PhysicalModulePrecision",
                new Vector2Int(14, 10),
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                facilityObjects);

            string appraisalDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(appraisal);
            EquipmentModuleInstance module = equipment.CreateExpeditionModule(
                "module:weapon:balanced-core",
                3,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination);
            WorldItemStackSnapshot moduleStack = items.GetAllStacks().Single(stack =>
                stack.StackId == module.sourceStackId);
            Require(moduleStack.ItemId == PhysicalItemIds.ForEquipmentModule()
                    && moduleStack.ItemInstanceId == module.instanceId
                    && moduleStack.State == WorldItemStackState.FacilityBuffer
                    && moduleStack.DestinationId == appraisalDestination
                    && moduleStack.Components.Any(component => component != null
                        && component.componentTypeId
                        == ItemInstanceComponentIds.EquipmentModule),
                "expedition module was not materialized as one authored physical item");

            Require(!equipment.TryAppraiseModule(
                    module.instanceId,
                    restoration,
                    out DomainFailure wrongFacilityFailure)
                    && wrongFacilityFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
                "module appraisal accepted a restoration workstation");
            Require(equipment.TryAppraiseModule(
                    module.instanceId,
                    appraisal,
                    out DomainFailure appraiseFailure),
                $"module appraisal failed: {appraiseFailure.Code}");

            string restorationDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(restoration);
            Require(items.TryRouteStackToDestination(
                    module.sourceStackId,
                    WorldItemStackState.FacilityBuffer,
                    restorationDestination,
                    restoration.centerPos,
                    out string routeRestoreFailure),
                $"module could not enter the restoration local buffer: {routeRestoreFailure}");
            Require(equipment.TryRestoreModule(
                    module.instanceId,
                    restoration,
                    out DomainFailure restoreFailure),
                $"module restoration failed: {restoreFailure.Code}");

            CombatEquipmentInstance weapon = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:steel");
            string precisionDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(precision);
            Require(items.SpawnExistingUniqueItemAt(
                    PhysicalItemIds.ForEquipment(weapon.definitionId),
                    (ItemInstanceId)weapon.instanceId,
                    precision.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    precisionDestination,
                    out string weaponStackId)
                    && equipment.TryLinkToWorldStack(
                        weapon.instanceId,
                        weaponStackId,
                        CombatEquipmentWorldState.MaintenanceBuffer),
                "equipment could not enter the precision-fitting local buffer");
            Require(items.TryRouteStackToDestination(
                    module.sourceStackId,
                    WorldItemStackState.FacilityBuffer,
                    precisionDestination,
                    precision.centerPos,
                    out string routePrecisionFailure),
                $"module could not enter the precision-fitting local buffer: {routePrecisionFailure}");
            Require(equipment.TryInstallModule(
                    weapon.instanceId,
                    module.instanceId,
                    0,
                    precision,
                    out DomainFailure installFailure),
                $"module installation failed: {installFailure.Code}");
            Require(items.GetAllStacks().All(stack =>
                    stack.ItemInstanceId != module.instanceId)
                    && repository.EquipmentModules.TryGetValue(
                        module.instanceId,
                        out EquipmentModuleInstance installedModule)
                    && installedModule.state == EquipmentModuleProcessState.Installed
                    && string.IsNullOrWhiteSpace(installedModule.sourceStackId),
                "installed module retained a duplicate independent physical stack");

            Require(equipment.TryRemoveModule(
                    weapon.instanceId,
                    0,
                    precision,
                    out EquipmentModuleInstance removed,
                    out DomainFailure removeFailure),
                $"module removal failed: {removeFailure.Code}");
            Require(removed.state == EquipmentModuleProcessState.IdentifiedDamaged
                    && removed.condition <= 0.7f
                    && !string.IsNullOrWhiteSpace(removed.sourceStackId),
                "removed module was not returned as a damaged physical item");
            WorldItemStackSnapshot returnedStack = items.GetAllStacks().Single(stack =>
                stack.StackId == removed.sourceStackId);
            Require(returnedStack.State == WorldItemStackState.FacilityBuffer
                    && returnedStack.DestinationId == precisionDestination,
                "removed module was not returned to the precision facility local buffer");

            DungeonPhysicalItemSaveData physicalSave = items.Capture();
            restoredItems = CreateRuntime(
                out WorldItemRepository restoredRepository,
                out CombatEquipmentRuntime restoredEquipment);
            restoredItems.Restore(physicalSave);
            EquipmentModuleInstance restoredModule = restoredEquipment.ModuleInstances
                .Single(candidate => candidate != null
                    && candidate.instanceId == module.instanceId);
            Require(restoredModule.sourceStackId == removed.sourceStackId
                    && restoredModule.state
                    == EquipmentModuleProcessState.IdentifiedDamaged
                    && restoredItems.GetAllStacks().Any(stack =>
                        stack.StackId == restoredModule.sourceStackId
                        && stack.ItemInstanceId == restoredModule.instanceId),
                "independent module did not round-trip with its physical stack linkage");

            Require(restoredItems.DeleteStack(restoredModule.sourceStackId),
                "restored independent module stack could not be destroyed");
            Require(restoredRepository.EquipmentModules.TryGetValue(
                    restoredModule.instanceId,
                    out EquipmentModuleInstance lostModule)
                    && lostModule.state == EquipmentModuleProcessState.Lost
                    && Mathf.Approximately(lostModule.condition, 0f)
                    && string.IsNullOrWhiteSpace(lostModule.sourceStackId),
                "destructive stack removal did not mark the independent module lost");

            restoredItems.Restore(physicalSave);
            EquipmentModuleInstance consumedModule = restoredEquipment.ModuleInstances
                .Single(candidate => candidate != null
                    && candidate.instanceId == module.instanceId);
            Require(restoredItems.TryConsumeStackQuantity(
                    consumedModule.sourceStackId,
                    1,
                    out WorldItemStackSnapshot consumedStack)
                    && consumedStack.ItemInstanceId == consumedModule.instanceId
                    && restoredRepository.EquipmentModules.TryGetValue(
                        consumedModule.instanceId,
                        out EquipmentModuleInstance consumedLostModule)
                    && consumedLostModule.state == EquipmentModuleProcessState.Lost
                    && Mathf.Approximately(consumedLostModule.condition, 0f)
                    && string.IsNullOrWhiteSpace(consumedLostModule.sourceStackId),
                "full stack consumption did not mark the independent module lost");

            return $"module={module.instanceId}; stack={removed.sourceStackId}; "
                + "wrongFacilityRejected=true; saveRoundTrip=true; lost=delete+consume";
        }
        finally
        {
            restoredItems?.Dispose();
            items.Dispose();
            foreach (GameObject facilityObject in facilityObjects)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static string VerifyEquipmentLineageTransferPhysicalAuthority()
    {
        List<GameObject> facilityObjects = new List<GameObject>();
        WorldItemStackRuntime items = CreateRuntime(
            out WorldItemRepository repository,
            out CombatEquipmentRuntime equipment);
        try
        {
            BuildableObject lineageFacility = CreateEquipmentFacility(
                LineageArchiveFacilityPath,
                "PhysicalLineageArchive",
                new Vector2Int(20, 10),
                EquipmentProgressionWorkstationTags.LineageArchive,
                facilityObjects);
            string lineageDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(lineageFacility);
            CombatEquipmentInstance source = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:steel");
            CombatEquipmentInstance target = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Masterwork,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:blacksteel");
            repository.EquipmentInstances[source.instanceId].durabilityRatio = 0.43f;
            repository.EquipmentInstances[target.instanceId].durabilityRatio = 0.87f;

            string physicalItemId = PhysicalItemIds.ForEquipment("weapon:greatsword");
            Require(items.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)source.instanceId,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out string sourceStackId)
                && equipment.TryLinkToWorldStack(
                    source.instanceId,
                    sourceStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer),
                "lineage source was not materialized in the archive local buffer");
            Require(items.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)target.instanceId,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out string targetStackId)
                && equipment.TryLinkToWorldStack(
                    target.instanceId,
                    targetStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer),
                "lineage target was not materialized in the archive local buffer");

            EquipmentEvolutionState inherited = new EquipmentEvolutionState
            {
                generation = 3,
                mastery = 42f,
                pendingDirection = EquipmentEvolutionDirection.Protection,
                pendingHistoryHash = "history:lineage-source",
                reforgeReady = true,
                activeHistoricalNodeIds = new List<string>
                {
                    "history-node:first-owner"
                }
            };
            Require(equipment.TryUpdateEvolutionState(source.instanceId, inherited),
                "lineage source evolution could not be seeded");

            Require(items.SpawnItemAt(
                    EquipmentProgressionItemIds.LineageSeal,
                    2,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out int sealCount)
                && sealCount == 2,
                "lineage seal could not enter the archive local buffer");
            WorldItemStackSnapshot seal = items.GetAllStacks().Single(stack =>
                stack.ItemId == EquipmentProgressionItemIds.LineageSeal);

            Require(equipment.TryQueueHistoryTransfer(
                    source.instanceId,
                    target.instanceId,
                    seal.StackId,
                    lineageFacility,
                    out EquipmentHistoryTransferOrder order,
                    out DomainFailure queueFailure),
                $"lineage transfer queue failed: {queueFailure.Code}");
            Require(equipment.ApplyHistoryTransferWork(
                    order.orderId,
                    order.requiredWork,
                    lineageFacility,
                    out bool completed,
                    out DomainFailure workFailure)
                && completed,
                $"lineage transfer work failed: {workFailure.Code}");

            WorldItemStackSnapshot remainingSeal = items.GetAllStacks()
                .SingleOrDefault(stack => stack.StackId == seal.StackId);
            Require(!equipment.TryGetInstance(source.instanceId, out _)
                    && items.GetAllStacks().All(stack =>
                        stack.StackId != sourceStackId)
                    && remainingSeal != null
                    && remainingSeal.ItemId
                        == EquipmentProgressionItemIds.LineageSeal
                    && remainingSeal.Quantity == 1,
                "lineage transfer did not consume the source equipment and exactly one seal");
            Require(equipment.TryGetInstance(
                    target.instanceId,
                    out CombatEquipmentInstance transferred),
                "lineage target disappeared after transfer");
            Require(transferred.definitionId == "weapon:greatsword"
                    && transferred.materialId == "material:blacksteel"
                    && transferred.quality == CombatEquipmentQuality.Masterwork
                    && Mathf.Approximately(transferred.durabilityRatio, 0.87f)
                    && transferred.sourceStackId == targetStackId,
                "lineage target did not retain its authored physical properties");
            Require(transferred.evolution.generation == inherited.generation
                    && Mathf.Approximately(transferred.evolution.mastery, inherited.mastery)
                    && transferred.evolution.pendingHistoryHash == inherited.pendingHistoryHash
                    && transferred.evolution.activeHistoricalNodeIds.SequenceEqual(
                        inherited.activeHistoricalNodeIds),
                "lineage target did not receive the source evolution history");
            return $"sourceConsumed={source.instanceId}; target={target.instanceId}; "
                + $"sealStack={seal.StackId}; sealRemaining={remainingSeal.Quantity}; "
                + $"facility={lineageFacility.PersistentInstanceId.Value}";
        }
        finally
        {
            items.Dispose();
            foreach (GameObject facilityObject in facilityObjects)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static string VerifyEquipmentIdentityAcrossCarryAndStorage()
    {
        GameObject actorObject = new GameObject("UniqueEquipmentCarrier");
        GameObject warehouseObject = new GameObject("UniqueEquipmentWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            runtime = CreateRuntime(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipment);
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            warehouse.BindPhysicalStock(
                new PhysicalStockQuery(repository, runtime.CatalogProvider));
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);

            CombatEquipmentInstance created = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.Loose);
            string physicalItemId = PhysicalItemIds.ForEquipment(created.definitionId);
            Require(runtime.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)created.instanceId,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string sourceStackId)
                    && equipment.TryLinkToWorldStack(
                        created.instanceId,
                        sourceStackId,
                        CombatEquipmentWorldState.Loose),
                "failed to materialize the repository equipment instance");
            WorldItemStackSnapshot source = runtime.GetAllStacks()
                .Single(stack => stack.StackId == sourceStackId);

            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            Require(carry.TryAddPartialStack(
                    source.StackId,
                    source.ItemInstanceId,
                    source.ItemId,
                    1,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    source.WasteOrigin,
                    source.Contamination,
                    source.Components,
                    out int accepted,
                    out string carryFailure)
                    && accepted == 1,
                $"failed to carry unique equipment: {carryFailure}");
            Require(equipment.TrySetWorldStateBySourceStack(
                    sourceStackId,
                    CombatEquipmentWorldState.Carried)
                    && runtime.DeleteStack(sourceStackId),
                "failed to remove the picked-up physical stack");
            Require(carry.Items.Single().itemInstanceId == created.instanceId,
                "carry inventory changed the equipment item-instance ID");

            Require(runtime.TryDepositCarriedItems(
                    actor,
                    carry,
                    warehouse,
                    out string depositFailure),
                $"failed to store carried equipment: {depositFailure}");
            WorldItemStackSnapshot stored = runtime.GetAllStacks()
                .Single(stack => stack.State == WorldItemStackState.Stored
                    && stack.ItemId == physicalItemId);
            Require(stored.ItemInstanceId == created.instanceId
                    && repository.EquipmentInstances.Count == 1
                    && equipment.TryGetInstance(created.instanceId, out CombatEquipmentInstance restored)
                    && restored.sourceStackId == stored.StackId,
                "equipment identity forked during carry/storage transfer");
            return $"instance={stored.ItemInstanceId}; source={sourceStackId}; stored={stored.StackId}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static BuildableObject CreateEquipmentFacility(
        string assetPath,
        string objectName,
        Vector2Int position,
        string expectedWorkstationTag,
        ICollection<GameObject> createdObjects)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        Require(building != null, $"equipment facility asset missing: {assetPath}");
        Require(building.GetAbility<BuildingProductionBufferAbility>() != null,
            $"equipment facility has no local production buffer: {assetPath}");
        Require(string.Equals(
                building.GetAbility<BuildingProductionWorkstationAbility>()?
                    .WorkstationTag,
                expectedWorkstationTag,
                StringComparison.Ordinal),
            $"equipment facility workstation tag mismatch: {assetPath}");

        GameObject facilityObject = new GameObject(objectName);
        createdObjects?.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        facility.ConstructBuildableObject(
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingResearchWorkPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingFacilityStateChangePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingRoomPolicyPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingEquipmentCraftingRuntimePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingWorldRegistryPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingItemStackPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingAbilityRuntimeDispatcher>(),
            new UnityGameClock(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingPaidFacilityContractPort>(),
            new FacilityEvolutionStateComponentFactory());
        facility.Initialization(building, position);
        Require(EquipmentProgressionFacilityContract.Matches(
                facility,
                expectedWorkstationTag),
            $"equipment facility runtime contract mismatch: {assetPath}");
        return facility;
    }

    private static void Run(
        string name,
        Func<string> test,
        List<string> lines,
        List<string> errors)
    {
        try
        {
            string details = test();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception ex)
        {
            lines.Add($"{name}\tFAIL\t{ex.Message}");
            errors.Add($"{name}: {ex.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestCatalogProvider : IDungeonItemCatalogProvider
    {
        private readonly Dictionary<string, DungeonItemDefinition> definitions =
            new Dictionary<string, DungeonItemDefinition>(StringComparer.Ordinal);

        public TestCatalogProvider()
        {
            foreach (DungeonItemDefinition definition in EditorItemCatalogFactory.Create().All)
            {
                definitions[definition.ItemId] = definition;
            }

            definitions["item:cheap"] = new DungeonItemDefinition("item:cheap", "Cheap Ore", "Cheap test item", StockCategory.General, 1, null, 0.5f, 75);
            definitions["item:rich"] = new DungeonItemDefinition("item:rich", "Rich Gem", "Rich test item", StockCategory.General, 50, null, 0.2f, 75);
            definitions["item:buffer"] = new DungeonItemDefinition("item:buffer", "Buffer Item", "Buffer test item", StockCategory.General, 5, null, 1f, 75);
            definitions["item:stored"] = new DungeonItemDefinition("item:stored", "Stored Item", "Stored test item", StockCategory.General, 100, null, 1f, 75);
            definitions["item:heavy"] = new DungeonItemDefinition("item:heavy", "Heavy Ingot", "Heavy test item", StockCategory.Weapon, 3, null, 2.25f, 75);
        }

        public IReadOnlyList<DungeonItemDefinition> All => definitions.Values.ToArray();

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            return TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : throw new KeyNotFoundException($"Unknown test item '{itemId}'.");
        }

        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
        {
            return definitions.TryGetValue(itemId ?? string.Empty, out definition);
        }
    }

    private sealed class TestHaulingSettings : IItemHaulingSettingsProvider
    {
        public TestHaulingSettings(float maxCarryMultiplier)
        {
            MaxCarryMultiplier = Mathf.Clamp(maxCarryMultiplier, 1f, 2.5f);
        }

        public float MaxCarryMultiplier { get; private set; }

        public ItemHaulingSettingsSnapshot Capture()
        {
            return new ItemHaulingSettingsSnapshot { maxCarryMultiplier = MaxCarryMultiplier };
        }

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.Normalize();
            MaxCarryMultiplier = snapshot.maxCarryMultiplier;
        }
    }

    private sealed class NoGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class TestWarehouseFacility : MonoBehaviour, IWarehouseFacility
    {
        private readonly WarehouseInventory inventory = new WarehouseInventory(200);

        public WarehouseInventory Inventory => inventory;
        public BuildingInstanceId PersistentInstanceId =>
            (BuildingInstanceId)"building:test-physical-item-warehouse";
        public bool HasWarehouseInventory => true;

        public void BindPhysicalStock(IStockQuery stockQuery)
        {
            inventory.BindPhysicalStock(
                stockQuery,
                PersistentInstanceId,
                CharacterAiEditorTestDependencies.AuthoredGameplay);
        }
    }

    private sealed class TestIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            persistentId = actor != null ? $"test:{actor.GetInstanceID()}" : string.Empty;
            return actor != null;
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            return actor != null ? $"test:{actor.GetInstanceID()}" : "test:null";
        }
    }

    private sealed class NoSpawnerProvider : ICharacterSpawnerProvider
    {
        public bool TryGetSpawner(out CharacterSpawner spawner)
        {
            spawner = null;
            return false;
        }
    }

    private sealed class NoDropZoneQuery : IWorldDropZoneQuery
    {
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }
}
#endif
