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

    [MenuItem("DungeonStory/Debug/Items/Run Physical Item Contracts")]
    public static void RunAll()
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("catalog_stock_fallback", VerifyCatalogStockFallback, lines, errors);
        Run("catalog_equipment_fallback", VerifyCatalogEquipmentFallback, lines, errors);
        Run("carry_weight_penalty", VerifyCarryWeightPenalty, lines, errors);
        Run("pile_sort_and_detail", VerifyPileSortAndDetail, lines, errors);
        Run("facility_delivery_buffer", VerifyFacilityDeliveryBuffer, lines, errors);
        Run("loose_material_delivery_request", VerifyLooseMaterialDeliveryRequest, lines, errors);
        Run("physical_craft_material_gate", VerifyPhysicalCraftMaterialGate, lines, errors);
        Run("customer_floor_theft", VerifyCustomerFloorTheft, lines, errors);
        Run("stack_delete_fallback", VerifyStackDeleteFallback, lines, errors);
        Run("warehouse_aggregate_view", VerifyWarehouseAggregateView, lines, errors);
        Run("warehouse_stored_stack_mirror", VerifyWarehouseStoredStackMirror, lines, errors);
        Run("save_v10_contract", VerifySaveV10Contract, lines, errors);

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
        }
    }

    private static string VerifyCatalogStockFallback()
    {
        ResourceDungeonItemCatalogProvider catalog = new ResourceDungeonItemCatalogProvider();
        DungeonItemDefinition food = catalog.GetDefinition(StockCategory.Food);
        Require(!string.IsNullOrWhiteSpace(food.ItemId), "stock category item id was empty");
        Require(DungeonItemCatalogSO.TryGetStockCategoryFromItemId(food.ItemId, out StockCategory parsed),
            "stock item id did not parse");
        Require(parsed == StockCategory.Food, $"parsed category was {parsed}");
        Require(food.UnitWeight > 0f && food.MaxStack > 0, "fallback stock item had invalid physical data");
        return $"itemId={food.ItemId}; weight={food.UnitWeight:0.##}; maxStack={food.MaxStack}";
    }

    private static string VerifyCatalogEquipmentFallback()
    {
        string itemId = DungeonItemCatalogSO.EquipmentItemId("weapon:attack-iron");
        Require(DungeonItemCatalogSO.TryGetEquipmentIdFromItemId(itemId, out string equipmentId),
            "equipment item id did not parse");
        Require(equipmentId == "weapon:attack-iron", $"parsed equipment id was {equipmentId}");
        DungeonItemDefinition definition = DungeonItemDefinition.FromEquipmentId(equipmentId);
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
            warehouse.Inventory.Deposit(StockCategory.General, 8);
            runtime = CreateRuntime();
            runtime.Start();

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

            CharacterActor actor = carrierObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            Require(carry.TryAdd(
                    "test:delivery",
                    DungeonItemCatalogSO.StockItemId(StockCategory.General),
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
        ExpeditionEquipmentCatalogSO equipmentCatalog = ExpeditionEquipmentCatalogSO.CreateRuntimeDefaults();
        GameObject warehouseObject = new GameObject("PhysicalCraftWarehouse");
        GameObject facilityObject = new GameObject("PhysicalCraftFacility");
        WorldItemStackRuntime itemRuntime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            foreach (StockCategory category in Enum.GetValues(typeof(StockCategory)))
            {
                warehouse.Inventory.Deposit(category, 50);
            }

            itemRuntime = CreateRuntime();
            itemRuntime.Start();
            ExpeditionEquipmentRuntime equipmentRuntime = new ExpeditionEquipmentRuntime(
                new TestEquipmentCatalogProvider(equipmentCatalog),
                new StaticWarehouseInventoryQuery(warehouse.Inventory),
                itemRuntime);
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();

            Require(equipmentRuntime.TryQueueCraft("weapon:attack-iron", facility, out string queueReason),
                $"physical craft queue failed: {queueReason}");
            Require(equipmentRuntime.CraftQueue.Count == 1, "craft order missing");
            ExpeditionEquipmentCraftOrderSaveData order = equipmentRuntime.CraftQueue[0];
            Require(!order.materialsReady
                    && order.materialDestinationId.StartsWith(WorldItemStackRuntime.FacilityInputDestinationPrefix, StringComparison.Ordinal),
                "physical craft order did not wait for materials");
            Require(!equipmentRuntime.HasPendingCraftWork(new[] { "weapon:attack-iron" }),
                "craft work became available before materials arrived");

            foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks().ToArray())
            {
                itemRuntime.SpawnItemAt(
                    stack.ItemId,
                    stack.Quantity,
                    new Vector2Int(order.materialDestinationX, order.materialDestinationY),
                    WorldItemStackState.FacilityBuffer,
                    order.materialDestinationId,
                    out _);
                itemRuntime.DeleteStack(stack.StackId);
            }

            Require(equipmentRuntime.HasPendingCraftWork(new[] { "weapon:attack-iron" }),
                "craft work did not become available after materials arrived");
            int completed = equipmentRuntime.ApplyCraftWork(
                new[] { "weapon:attack-iron" },
                999f,
                out string completedEquipmentId);
            Require(completed == 1
                    && completedEquipmentId == "weapon:attack-iron"
                    && equipmentRuntime.GetAvailableCount("weapon:attack-iron") == 1,
                "physical craft order did not complete into inventory");
            return $"order={order.orderId}; completed={completedEquipmentId}";
        }
        finally
        {
            itemRuntime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
            UnityEngine.Object.DestroyImmediate(equipmentCatalog);
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
                nextStackSequence = 20,
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
        WarehouseInventory inventory = new WarehouseInventory(6, StockCategory.Food, restrictCategory: true);
        Require(inventory.Deposit(StockCategory.Weapon, 3) == 0, "restricted warehouse accepted wrong category");
        Require(inventory.Deposit(StockCategory.Food, 4) == 4, "food deposit failed");
        Require(inventory.Deposit(StockCategory.Food, 4) == 2, "capacity clamp failed");
        Require(inventory.TotalStock == 6 && inventory.RemainingCapacity == 0, "warehouse aggregate count mismatch");
        Require(inventory.Withdraw(StockCategory.Food, 2) == 2, "withdraw failed");
        WarehouseInventory restored = new WarehouseInventory();
        restored.ApplySnapshot(inventory.CreateSnapshot());
        Require(restored.GetStock(StockCategory.Food) == 4, "warehouse snapshot did not preserve stock");
        return $"stock={restored.GetStock(StockCategory.Food)}; capacity={restored.MaxCapacity}";
    }

    private static string VerifyWarehouseStoredStackMirror()
    {
        GameObject warehouseObject = new GameObject("PhysicalItemStoredMirrorWarehouse");
        GameObject carrierObject = new GameObject("PhysicalItemStoredMirrorCarrier");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime();
            runtime.Start();

            CharacterActor actor = carrierObject.AddComponent<CharacterActor>();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            string foodItemId = DungeonItemCatalogSO.StockItemId(StockCategory.Food);
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
                $"stored mirror was not created correctly: {storedHidden.Count}");

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
            warehouse.Inventory.AddStock(StockCategory.Food, 41);
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

    private static string VerifySaveV10Contract()
    {
        Require(DungeonGameSaveData.CurrentVersion == 15, $"save version is {DungeonGameSaveData.CurrentVersion}");
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
            string itemId = DungeonItemCatalogSO.StockItemId(StockCategory.General);
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
            nextStackSequence = 10,
            haulingSettings = new ItemHaulingSettingsSnapshot { maxCarryMultiplier = 1.5f },
            stacks = new List<WorldItemStackSaveData>
            {
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
                    stackId = "stack:buffer",
                    itemId = "item:buffer",
                    quantity = 5,
                    state = WorldItemStackState.FacilityBuffer,
                    reservedByPersistentId = "worker:reserved",
                    destinationId = "shop:1",
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:stored",
                    itemId = "item:stored",
                    quantity = 9,
                    state = WorldItemStackState.Stored,
                    destinationId = "warehouse:1",
                    gridX = 2,
                    gridY = 1
                }
            }
        };
    }

    private static WorldItemStackRuntime CreateRuntime()
    {
        ICombatEquipmentCatalog combatCatalog = new ResourceCombatEquipmentCatalog();
        IGridSystemProvider gridProvider = new NoGridProvider();
        IDungeonItemCatalogProvider itemCatalog = new TestCatalogProvider();
        IItemHaulingSettingsProvider haulingSettings =
            new TestHaulingSettings(1.5f);
        ICharacterIdRegistry idRegistry = new TestIdRegistry();
        IGridPathSearchBroker pathBroker =
            new GridPathSearchBroker(new UnityGameClock());
        ICharacterAiWorldRegistry worldRegistry =
            CharacterAiEditorTestDependencies.WorldRegistry;
        WorldItemRepository repository = new WorldItemRepository();
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
        return new WorldItemStackRuntime(
            gridProvider,
            itemCatalog,
            haulingSettings,
            idRegistry,
            new NoDropZoneQuery(),
            new NoSpawnerProvider(),
            pathBroker,
            worldRegistry,
            new CombatEquipmentRuntime(combatCatalog),
            combatCatalog,
            new GameEventBus(),
            new UnityGameClock(),
            repository,
            reservations,
            spawner,
            query,
            haulPlanning);
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
            new Dictionary<string, DungeonItemDefinition>(StringComparer.Ordinal)
            {
                ["item:cheap"] = new DungeonItemDefinition("item:cheap", "Cheap Ore", "Cheap test item", StockCategory.General, 1, null, 0.5f, 75),
                ["item:rich"] = new DungeonItemDefinition("item:rich", "Rich Gem", "Rich test item", StockCategory.General, 50, null, 0.2f, 75),
                ["item:buffer"] = new DungeonItemDefinition("item:buffer", "Buffer Item", "Buffer test item", StockCategory.General, 5, null, 1f, 75),
                ["item:stored"] = new DungeonItemDefinition("item:stored", "Stored Item", "Stored test item", StockCategory.General, 100, null, 1f, 75),
                ["item:heavy"] = new DungeonItemDefinition("item:heavy", "Heavy Ingot", "Heavy test item", StockCategory.Weapon, 3, null, 2.25f, 75)
            };

        public DungeonItemCatalogSO Catalog => null;

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            return TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : new DungeonItemDefinition(itemId, itemId, string.Empty, StockCategory.General, 1, null, 1f, 75);
        }

        public DungeonItemDefinition GetDefinition(StockCategory category)
        {
            return DungeonItemDefinition.FromStockCategory(category);
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
        public bool HasWarehouseInventory => true;
    }

    private sealed class StaticWarehouseInventoryQuery : IFacilityEvolutionWarehouseInventoryQuery
    {
        private readonly IReadOnlyList<WarehouseInventory> inventories;

        public StaticWarehouseInventoryQuery(params WarehouseInventory[] inventories)
        {
            this.inventories = inventories ?? Array.Empty<WarehouseInventory>();
        }

        public IReadOnlyList<WarehouseInventory> GetInventories()
        {
            return inventories;
        }
    }

    private sealed class TestEquipmentCatalogProvider : IExpeditionEquipmentCatalogProvider
    {
        public TestEquipmentCatalogProvider(ExpeditionEquipmentCatalogSO catalog)
        {
            Catalog = catalog;
        }

        public ExpeditionEquipmentCatalogSO Catalog { get; }
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
