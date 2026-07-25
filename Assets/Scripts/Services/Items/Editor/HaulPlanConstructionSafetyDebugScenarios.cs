#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class HaulPlanConstructionSafetyDebugScenarios
{
    private const string ReportPath = "Temp/haul-plan-construction-safety.tsv";

    [MenuItem("DungeonStory/Debug/Items/Run Haul Plan And Construction Safety Contracts")]
    public static void RunFromMenu()
    {
        RunAll(logSuccess: true);
    }

    public static bool RunAll(bool logSuccess)
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("multi_stack_haul_plan", VerifyMultiStackHaulPlan, lines, errors);
        Run("priority_haul_seed_beats_value", VerifyPriorityHaulSeedBeatsValue, lines, errors);
        Run("partial_heavy_stack_reservation", VerifyPartialHeavyStackReservation, lines, errors);
        Run("construction_safety_forced_warning", VerifyConstructionSafetyForcedWarning, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            Debug.LogError($"Haul plan / construction safety contracts FAIL. Report: {ReportPath}");
            return false;
        }

        if (logSuccess)
        {
            Debug.Log($"Haul plan / construction safety contracts PASS. Report: {ReportPath}");
        }

        return true;
    }

    private static string VerifyMultiStackHaulPlan()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            string stockId = DungeonItemCatalogSO.StockItemId(StockCategory.General);
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int first)
                && first == 5,
                "first stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(3, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int second)
                && second == 5,
                "second stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(4, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int third)
                && third == 5,
                "third stack spawn failed");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "haul plan failed: " + failureReason);

            int reserved = plan.ReservedStackQuantities.Sum(item => item.Quantity);
            Require(plan.PickupLegs.Count >= 2, $"expected multiple pickup legs, got {plan.PickupLegs.Count}");
            Require(plan.PrimaryDestination == WorldItemHaulDestinationKind.Warehouse,
                $"unexpected destination {plan.PrimaryDestination}");
            Require(reserved >= 10, $"expected at least two stacks reserved, got {reserved}");

            int picked = 0;
            foreach (WorldItemReservedStackQuantity reservation in plan.ReservedStackQuantities)
            {
                Require(scenario.Items.TryPickupReservedStackQuantity(
                        scenario.Actor,
                        scenario.Carry,
                        reservation,
                        out int pickedUp,
                        out string pickupReason),
                    "pickup failed: " + pickupReason);
                picked += pickedUp;
            }

            Require(picked == reserved, $"picked {picked}, reserved {reserved}");
            Require(scenario.Items.TryDepositCarriedItems(
                    scenario.Actor,
                    scenario.Carry,
                    scenario.Warehouse,
                    out string depositReason),
                "deposit failed: " + depositReason);
            Require(!scenario.Carry.HasItems, "carry inventory still has items");
            Require(scenario.Warehouse.Inventory.GetStock(StockCategory.General) >= picked,
                "warehouse did not receive hauled stock");

            return $"pickups={plan.PickupLegs.Count}; reserved={reserved}; deposited={picked}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyPartialHeavyStackReservation()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 10f);
        try
        {
            string stockId = DungeonItemCatalogSO.StockItemId(StockCategory.General);
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    10,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 10,
                "heavy stack spawn failed");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "heavy haul plan failed: " + failureReason);

            int reserved = plan.ReservedStackQuantities.Sum(item => item.Quantity);
            Require(reserved > 0 && reserved < 10, $"expected partial reservation, got {reserved}");
            Require(scenario.Items.TryPickupReservedStackQuantity(
                    scenario.Actor,
                    scenario.Carry,
                    plan.ReservedStackQuantities[0],
                    out int picked,
                    out string pickupReason),
                "partial pickup failed: " + pickupReason);
            Require(picked == reserved, $"picked {picked}, reserved {reserved}");

            int remaining = 10 - picked;
            Require(scenario.Items.GetAllStacks().Any(stack =>
                    stack.Quantity == remaining
                    && string.IsNullOrWhiteSpace(stack.ReservedByPersistentId)),
                "remaining stack was not released for another hauler");

            return $"reserved={reserved}; remaining={remaining}; load={scenario.Carry.GetCurrentWeight(scenario.Items.CatalogProvider):0.##}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyPriorityHaulSeedBeatsValue()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            string stockId = DungeonItemCatalogSO.StockItemId(StockCategory.General);
            Vector2Int regularPosition = new Vector2Int(2, 1);
            Vector2Int priorityPosition = new Vector2Int(4, 1);
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    20,
                    regularPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "regular stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    1,
                    priorityPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "priority stack spawn failed");

            WorldItemStackSnapshot priority = scenario.Items
                .GetStacksAt(priorityPosition)
                .Single();
            Require(scenario.Items.PrioritizeHaul(priority.StackId),
                "priority haul flag failed");
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "priority haul plan failed: " + failureReason);
            Require(plan.PickupLegs[0].Reservation.StackId == priority.StackId,
                $"priority stack was not the seed: {plan.PickupLegs[0].Reservation.StackId}");

            string actorId = scenario.Actor.Identity.PersistentId;
            foreach (WorldItemReservedStackQuantity reservation in plan.ReservedStackQuantities)
            {
                scenario.Items.ReleaseReservation(reservation.StackId, actorId);
            }

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan retriedPlan,
                    out failureReason),
                "priority haul retry failed: " + failureReason);
            Require(retriedPlan.PickupLegs[0].Reservation.StackId == priority.StackId,
                "priority was lost after a cancelled reservation");

            return $"prioritySeed={priority.StackId}; pickups={plan.PickupLegs.Count}; retryPreserved=True";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyConstructionSafetyForcedWarning()
    {
        Grid grid = CreateWalkableExteriorGrid();
        GameObject actorObject = null;
        GameObject siteObject = null;
        BuildingSO wallData = null;
        try
        {
            GridProvider gridProvider = new GridProvider(grid);
            actorObject = CreateActor("ConstructionSafetyActor", gridProvider, grid, new Vector2Int(0, 1));
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();

            wallData = CreateBuildingData(99002, "테스트 벽", BuildingCategory.Wall, GridLayer.Building);
            siteObject = new GameObject("ConstructionSafetySite");
            ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
            site.SetGrid(grid);
            site.Initialization(wallData, new Vector2Int(5, 1));
            grid.SetAreaType(new Vector2Int(5, 1), GridCellAreaType.ExteriorPath);

            ConstructionSafetyResult safety = ConstructionSafetyPlanner.Evaluate(site, actor, forced: false);
            Require(!safety.IsSafe && safety.Reason == ConstructionSafetyReason.EntranceBlocked,
                $"expected exterior path block, got {safety.Reason}");

            ConstructionSafetyResult forced = ConstructionSafetyPlanner.Evaluate(site, actor, forced: true);
            Require(forced.IsSafe && forced.IsForcedWarning && forced.Reason == ConstructionSafetyReason.Forced,
                "forced warning did not bypass with warning");

            return $"auto={safety.Message}; forced={forced.Message}";
        }
        finally
        {
            DestroyImmediateSafe(siteObject);
            DestroyImmediateSafe(actorObject);
            DestroyImmediateSafe(wallData);
        }
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        private ScenarioRuntime(
            Grid grid,
            GridProvider gridProvider,
            WorldItemStackRuntime items,
            TestWarehouseBuilding warehouse,
            CharacterActor actor,
            CharacterCarryInventory carry,
            ICharacterAiWorldRegistry worldRegistry)
        {
            Grid = grid;
            GridProvider = gridProvider;
            Items = items;
            Warehouse = warehouse;
            Actor = actor;
            Carry = carry;
            WorldRegistry = worldRegistry;
        }

        public Grid Grid { get; }
        public GridProvider GridProvider { get; }
        public WorldItemStackRuntime Items { get; }
        public TestWarehouseBuilding Warehouse { get; }
        public CharacterActor Actor { get; }
        public CharacterCarryInventory Carry { get; }
        public ICharacterAiWorldRegistry WorldRegistry { get; }

        public static ScenarioRuntime Create(float lightStockWeight)
        {
            Grid grid = CreateWalkableExteriorGrid();
            GridProvider gridProvider = new GridProvider(grid);
            ScenarioRuntime scenario = null;
            try
            {
                GameObject warehouseObject = new GameObject("HaulPlanWarehouse");
                TestWarehouseBuilding warehouse = warehouseObject.AddComponent<TestWarehouseBuilding>();
                BuildingSO warehouseData = CreateBuildingData(99001, "테스트 창고", BuildingCategory.Shop, GridLayer.Building);
                warehouse.SetGrid(grid);
                warehouse.Initialization(warehouseData, new Vector2Int(9, 1));
                grid.RegisterOccupant(warehouse, GridLayer.Building, warehouse.buildPoses, false);

                GameObject actorObject = CreateActor("HaulPlanActor", gridProvider, grid, new Vector2Int(0, 1));
                CharacterActor actor = actorObject.GetComponent<CharacterActor>();
                CharacterCarryInventory carry = actorObject.GetComponent<CharacterCarryInventory>();

                ICombatEquipmentCatalog combatCatalog =
                    new ResourceCombatEquipmentCatalog();
                IDungeonItemCatalogProvider itemCatalog =
                    new TestCatalogProvider(lightStockWeight);
                IItemHaulingSettingsProvider haulingSettings =
                    new TestHaulingSettings(1.5f);
                ICharacterIdRegistry idRegistry = new TestIdRegistry();
                IGridPathSearchBroker pathBroker =
                    new GridPathSearchBroker(new UnityGameClock());
                ICharacterAiWorldRegistry worldRegistry =
                    CharacterAiEditorTestDependencies.WorldRegistry;
                worldRegistry.RegisterBuilding(warehouse);
                worldRegistry.RegisterCharacter(actor);
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
                WorldItemStackRuntime items = new WorldItemStackRuntime(
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
                items.Start();

                scenario = new ScenarioRuntime(
                    grid,
                    gridProvider,
                    items,
                    warehouse,
                    actor,
                    carry,
                    worldRegistry);
                scenario.ownedObjects.Add(actorObject);
                scenario.ownedObjects.Add(warehouseObject);
                scenario.ownedObjects.Add(warehouseData);
                return scenario;
            }
            catch
            {
                scenario?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Items?.Dispose();
            WorldRegistry?.UnregisterCharacter(Actor);
            WorldRegistry?.UnregisterBuilding(Warehouse);
            foreach (UnityEngine.Object owned in ownedObjects)
            {
                DestroyImmediateSafe(owned);
            }
        }
    }

    private static Grid CreateWalkableExteriorGrid()
    {
        Grid grid = new Grid(12, 3);
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
            }
        }

        return grid;
    }

    private static GameObject CreateActor(
        string name,
        IGridSystemProvider gridProvider,
        Grid grid,
        Vector2Int position)
    {
        GameObject actorObject = new GameObject(name);
        actorObject.SetActive(false);
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        CharacterLifecycle lifecycle = actorObject.GetComponent<CharacterLifecycle>();
        actorObject.AddComponent<CharacterCarryInventory>();
        actorObject.AddComponent<AbilityHaul>();
        actorObject.GetComponent<CharacterIdentity>().SetPersistentId($"worker:{name}");
        lifecycle.ConstructCharacterLifecycle(gridProvider);
        actorObject.transform.position = grid.GetWorldPos(position);
        actorObject.SetActive(true);
        actor.EnsureRuntimeState();
        return actorObject;
    }

    private static BuildingSO CreateBuildingData(
        int id,
        string objectName,
        BuildingCategory category,
        GridLayer layer)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = 1;
        building.height = 1;
        building.layer = layer;
        building.category = category;
        building.unlocked = true;
        return building;
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

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(target);
    }

    private sealed class TestWarehouseBuilding : BuildableObject, IWarehouseFacility
    {
        private readonly WarehouseInventory inventory = new WarehouseInventory(200);

        public WarehouseInventory Inventory => inventory;
        public bool HasWarehouseInventory => true;
    }

    private sealed class GridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        public GridProvider(Grid grid)
        {
            this.grid = grid;
        }

        public GridSystemManager Manager => null;
        public Grid Grid => grid;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid result)
        {
            result = grid;
            return result != null;
        }
    }

    private sealed class TestCatalogProvider : IDungeonItemCatalogProvider
    {
        private readonly float stockWeight;

        public TestCatalogProvider(float stockWeight)
        {
            this.stockWeight = Mathf.Max(0.01f, stockWeight);
        }

        public DungeonItemCatalogSO Catalog => null;

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            if (DungeonItemCatalogSO.TryGetStockCategoryFromItemId(itemId, out StockCategory category))
            {
                return new DungeonItemDefinition(
                    itemId,
                    category.ToString(),
                    string.Empty,
                    category,
                    1,
                    null,
                    stockWeight,
                    75);
            }

            return new DungeonItemDefinition(
                itemId,
                itemId,
                string.Empty,
                StockCategory.General,
                1,
                null,
                stockWeight,
                75);
        }

        public DungeonItemDefinition GetDefinition(StockCategory category) =>
            GetDefinition(DungeonItemCatalogSO.StockItemId(category));

        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
        {
            definition = GetDefinition(itemId);
            return true;
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

            MaxCarryMultiplier = Mathf.Clamp(snapshot.maxCarryMultiplier, 1f, 2.5f);
        }
    }

    private sealed class TestIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            persistentId = actor != null && actor.Identity != null
                ? actor.Identity.PersistentId
                : string.Empty;
            return !string.IsNullOrWhiteSpace(persistentId);
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            if (TryGetPersistentId(actor, out string persistentId))
            {
                return persistentId;
            }

            return actor != null ? $"worker:{actor.GetInstanceID()}" : "worker:null";
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
