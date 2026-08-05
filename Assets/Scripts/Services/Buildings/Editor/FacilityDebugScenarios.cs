using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FacilityDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCacheService =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicyService =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);
    private static readonly IShopStockCatalog ShopStockCatalogService =
        new AssetDatabaseShopStockCatalog();
    private static readonly IFloatingNumberFeedbackService FloatingNumberFeedbackService =
        new NoopFloatingNumberFeedbackService();
    private static readonly IWorkforceReplanService WorkforceReplanService =
        new NoopWorkforceReplanService();

    [MenuItem("DungeonStory/Debug/Facilities/Run P1 Facility Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 facility scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("P1 시설 에셋 수", VerifyP1AssetCounts, errors);
        RunScenario("방문 후보 판정", VerifyVisitability, errors);
        RunScenario("작업 후보 판정", VerifyWorkability, errors);
        RunScenario("재고/파손 제외", VerifyUnavailableFacilitiesAreExcluded, errors);
        RunScenario("재고 계열 설정", VerifyStockCategories, errors);
        RunScenario("창고 물리 재고 인덱스", VerifyWarehouseInventory, errors);
        RunScenario("물리 런타임 없는 상점 보충 차단", VerifyShopRestockRequiresPhysicalRuntime, errors);
        RunScenario("운영일 납품 제안", VerifyDailyDeliveryOffers, errors);
        RunScenario("물리 런타임 없는 납품 차단", VerifyDeliveryRequiresPhysicalRuntime, errors);
        RunScenario("재고 구매 실패 조건", VerifyPurchaseDeliveryFailureConditions, errors);
        RunScenario("물리 런타임 없는 보상 차단", VerifyDefenseRewardRequiresPhysicalRuntime, errors);
        RunScenario("물리 런타임 없는 내부 생산 차단", VerifyInternalProductionRequiresPhysicalRuntime, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 facility scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        if (scenario()) return;

        errors.Add(name);
    }

    private static bool VerifyP1AssetCounts()
    {
        List<BuildingSO> buildings = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building/P1" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where((building) => building != null)
            .ToList();
        int baseManagementBuildingCount = buildings
            .Count((building) => building != null
                && building.id < 30
                && (building.Defense == null || !building.Defense.IsDefenseFacility));
        int synthesisManagementBuildingCount = buildings
            .Count((building) => building != null
                && building.id >= 50
                && building.id < 60
                && (building.Defense == null || !building.Defense.IsDefenseFacility));
        string[] stocks = AssetDatabase.FindAssets("t:StockInfo", new[] { "Assets/Resources/SO/Stock/P1" });
        return baseManagementBuildingCount == 9
            && synthesisManagementBuildingCount == 4
            && stocks.Length >= 8;
    }

    private static bool VerifyVisitability()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(1, 0));
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(5, 0));
        BuildableObject warehouse = world.Place("P1_Warehouse", new Vector2Int(9, 0));

        List<BuildableObject> visitable = world.Grid.SearchPath(Vector2Int.zero).GetAllVisitableBuilding();
        return visitable.Contains(lowFood)
            && visitable.Contains(restRoom)
            && !visitable.Contains(warehouse);
    }

    private static bool VerifyWorkability()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(1, 0));
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(5, 0));
        BuildableObject warehouse = world.Place("P1_Warehouse", new Vector2Int(9, 0));

        return shop is IWorkableFacility shopWork
            && lab is IWorkableFacility labWork
            && warehouse is IWorkableFacility warehouseWork
            && shopWork.CanAssignWorker(null, out _)
            && labWork.CanAssignWorker(null, out _)
            && warehouseWork.CanAssignWorker(null, out _);
    }

    private static bool VerifyUnavailableFacilitiesAreExcluded()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(1, 0));
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(5, 0));

        ClearShopStock(shop);
        restRoom.SetDamaged(true);

        List<BuildableObject> visitable = world.Grid.SearchPath(Vector2Int.zero).GetAllVisitableBuilding();
        return !visitable.Contains(shop)
            && !visitable.Contains(restRoom)
            && !shop.CanVisit((CharacterActor)null, out string stockReason)
            && stockReason == "재고 없음"
            && !restRoom.CanVisit((CharacterActor)null, out string damageReason)
            && damageReason == "시설 파손";
    }

    private static bool VerifyStockCategories()
    {
        SaleItem food = AssetDatabase.LoadAssetAtPath<SaleItem>("Assets/Resources/SO/Stock/Item/햄버거.asset");
        SaleItem sword = AssetDatabase.LoadAssetAtPath<SaleItem>("Assets/Resources/SO/Stock/Item/도란검.asset");
        SaleItem shield = AssetDatabase.LoadAssetAtPath<SaleItem>("Assets/Resources/SO/Stock/Item/도란방패.asset");

        return food != null
            && sword != null
            && shield != null
            && food.category == StockCategory.Food
            && sword.category == StockCategory.Weapon
            && shield.category == StockCategory.Weapon;
    }

    private static bool VerifyWarehouseInventory()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));

        if (warehouseBuilding is not IWarehouseFacility warehouse
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory.TotalStock != 0)
        {
            return false;
        }

        warehouse.Inventory.SeedPhysicalStockForTest(StockCategory.Food, 3);
        warehouse.Inventory.SeedPhysicalStockForTest(StockCategory.Weapon, 2);
        warehouse.Inventory.SeedPhysicalStockForTest(StockCategory.Mana, 1);
        return warehouse.Inventory.TotalStock == 6
            && warehouse.Inventory.GetStock(StockCategory.Food) == 3
            && warehouse.Inventory.GetStock(StockCategory.Weapon) == 2
            && warehouse.Inventory.GetStock(StockCategory.Mana) == 1;
    }

    private static bool VerifyShopRestockRequiresPhysicalRuntime()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject shopBuilding = world.Place("P1_LowFoodShop", new Vector2Int(1, 0));
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(7, 0));
        Shop shop = shopBuilding as Shop;
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        ClearShopStock(shopBuilding);
        warehouse.Inventory.SeedPhysicalStockForTest(StockCategory.Food, 5);

        int beforeWarehouseFood = warehouse.Inventory.GetStock(StockCategory.Food);
        try
        {
            shop.RestockFrom(new[] { warehouse }, 5, out _);
            return false;
        }
        catch (System.InvalidOperationException error)
        {
            return error.Message == "Shop restocking requires physical item runtime."
                && shop.CurrentStock == 0
                && warehouse.Inventory.GetStock(StockCategory.Food) == beforeWarehouseFood;
        }
    }

    private static bool VerifyDailyDeliveryOffers()
    {
        IReadOnlyList<StockDeliveryOffer> offers = StockSupplyService.CreateDailyDeliveryOffers(
            1,
            DefaultStockCostMultiplier,
            CharacterAiEditorTestDependencies.AuthoredGameplay);

        return offers.Count >= 7
            && offers.Any((offer) => offer.category == StockCategory.Food && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.General && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Weapon && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Mana && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Water && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Medicine && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Fuel && offer.amount > 0 && offer.cost > 0);
    }

    private static bool VerifyDeliveryRequiresPhysicalRuntime()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        GameSessionState gameData = CreateGameData(500);
        warehouse.Inventory.ConsumePhysicalStockForTest(StockCategory.Food, 10);

        int beforeMoney = gameData.holdingMoney.Value;
        int beforeFood = warehouse.Inventory.GetStock(StockCategory.Food);
        StockDeliveryOffer offer = new StockDeliveryOffer(StockCategory.Food, 5, 40, "테스트 납품");
        bool success = StockSupplyService.TryPurchaseDelivery(
            new EditorGameMoneyAccount(gameData),
            new[] { warehouse },
            itemStackRuntime: null,
            offer,
            DisabledDungeonDebugRuleQuery.Instance,
            out StockSupplyResult result);

        int afterMoney = gameData.holdingMoney.Value;
        return !success
            && !result.success
            && result.deliveredAmount == 0
            && result.reason == "물리 아이템 런타임 없음"
            && warehouse.Inventory.GetStock(StockCategory.Food) == beforeFood
            && beforeMoney == afterMoney;
    }

    private static bool VerifyPurchaseDeliveryFailureConditions()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        GameSessionState poorData = CreateGameData(1);
        GameSessionState richData = CreateGameData(500);
        StockDeliveryOffer offer = new StockDeliveryOffer(StockCategory.Food, 5, 40, "테스트 납품");

        bool noMoney = !StockSupplyService.TryPurchaseDelivery(
            new EditorGameMoneyAccount(poorData),
            new[] { warehouse },
            itemStackRuntime: null,
            offer,
            DisabledDungeonDebugRuleQuery.Instance,
            out StockSupplyResult noMoneyResult)
            && noMoneyResult.reason == "자금 부족";

        bool noPhysicalRuntime = !StockSupplyService.TryPurchaseDelivery(
            new EditorGameMoneyAccount(richData),
            new[] { warehouse },
            itemStackRuntime: null,
            offer,
            DisabledDungeonDebugRuleQuery.Instance,
            out StockSupplyResult noPhysicalRuntimeResult)
            && noPhysicalRuntimeResult.reason == "물리 아이템 런타임 없음";

        return noMoney && noPhysicalRuntime;
    }

    private static bool VerifyDefenseRewardRequiresPhysicalRuntime()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        warehouse.Inventory.ConsumePhysicalStockForTest(StockCategory.Weapon, 5);

        int beforeWeapon = warehouse.Inventory.GetStock(StockCategory.Weapon);
        bool success = StockSupplyService.GrantReward(
            new[] { warehouse },
            itemStackRuntime: null,
            StockCategory.Weapon,
            3,
            "침입 방어 보상",
            out StockSupplyResult result);

        return !success
            && !result.success
            && result.cost == 0
            && result.deliveredAmount == 0
            && result.reason == "물리 아이템 런타임 없음"
            && warehouse.Inventory.GetStock(StockCategory.Weapon) == beforeWeapon;
    }

    private static bool VerifyInternalProductionRequiresPhysicalRuntime()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        warehouse.Inventory.ConsumePhysicalStockForTest(StockCategory.Mana, 4);

        int beforeMana = warehouse.Inventory.GetStock(StockCategory.Mana);
        List<StockSupplyResult> results = StockSupplyService.RunInternalProduction(
            new[] { warehouse },
            itemStackRuntime: null,
            new[] { new StockProductionRule(StockCategory.Mana, 2, "내부 생산") });

        return results.Count == 1
            && !results[0].success
            && results[0].deliveredAmount == 0
            && results[0].reason == "물리 아이템 런타임 없음"
            && warehouse.Inventory.GetStock(StockCategory.Mana) == beforeMana;
    }

    private static void ClearShopStock(BuildableObject building)
    {
        if (building is Shop shop)
        {
            shop.DebugClearStock();
        }
    }

    private static GameSessionState CreateGameData(int holdingMoney)
    {
        GameSessionState gameData = new GameSessionState();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class FacilityScenarioWorld : System.IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();
        private readonly IGameMoneyAccount moneyAccount;

        public FacilityScenarioWorld()
        {
            GameSessionState gameData = CreateGameData(1000);
            moneyAccount = new EditorGameMoneyAccount(gameData);
            Grid = new Grid(14, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }
        }

        public Grid Grid { get; }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            GridBuildingFactory factory = new GridBuildingFactory((building) =>
                InjectBuildableObject(building));
            BuildableObject building = factory.Create(Grid, buildingData, position);
            objects.Add(building.gameObject);
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (building.BuildingData.RequiresRoomRole())
            {
                PlaceRoomDoorsFor(building);
            }

            return building;
        }

        private void PlaceRoomDoorsFor(BuildableObject building)
        {
            if (building == null || building.buildPoses == null || building.buildPoses.Count == 0)
            {
                return;
            }

            int minX = building.buildPoses.Min((pos) => pos.x);
            int maxX = building.buildPoses.Max((pos) => pos.x);
            int y = building.centerPos.y;
            PlaceRuntimeDoor(new Vector2Int(minX - 1, y));
            PlaceRuntimeDoor(new Vector2Int(maxX + 1, y));
        }

        private void PlaceRuntimeDoor(Vector2Int position)
        {
            if (!Grid.IsValidGridPos(position))
            {
                return;
            }

            GridCell cell = Grid.GetGridCell(position);
            if (cell == null || cell.HasOccupantInLayer(GridLayer.Building))
            {
                return;
            }

            BuildingSO buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            scriptableObjects.Add(buildingData);
            buildingData.id = 901;
            buildingData.objectName = "문";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.layer = GridLayer.Building;
            buildingData.category = BuildingCategory.None;
            buildingData.runtimeArchetype = BuildingRuntimeArchetypeKind.Door;
            buildingData.unlocked = true;
            buildingData.Facility = new FacilityData();

            GameObject obj = new GameObject("Room Boundary Door");
            objects.Add(obj);
            BuildableObject door = obj.AddComponent<BuildableObject>();
            InjectBuildableObject(door);
            door.SetGrid(Grid);
            door.Initialization(buildingData, position);
            Grid.RegisterOccupant(
                door,
                GridLayer.Building,
                buildingData.GetGridPosList(position),
                false);
        }

        private void InjectBuildableObject(BuildableObject building)
        {
            building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            building.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
            building.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
                FacilityCandidateCacheService,
                RoomFacilityPolicyService, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
            building.ConstructBuildableObjectEventBus(
                CharacterAiEditorTestDependencies.GameEvents,
                new BuildingVisitEventPublisher(
                    CharacterAiEditorTestDependencies.GameEvents),
                new BuildingInfoPresentationAdapter(
                    CharacterAiEditorTestDependencies.GameEvents));
            if (building is Facility facility)
            {
                facility.ConstructFacility(
                    roomEnvironmentExperienceService: null,
                    new EmptyStockQuery(),
                    mealConsumptionRuntime: null,
                    waterFixtureUseRuntime: null,
                    wastewaterNetworkRuntime:
                        FacilityFixtureWastewaterTransaction.Instance,
                    serviceSessionRuntime: null,
                    serviceRoomLinkRuntime: null,
                    stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay);
            }
            if (building is Shop shop)
            {
                shop.ConstructShop(
                    moneyAccount,
                    ShopStockCatalogService,
                    FloatingNumberFeedbackService,
                    WorkforceReplanService,
                    FacilityCrimeEditorTestDependencies.Evaluator,
                    new DungeonStory.Foundation.RandomStreamProvider(101),
                    null,
                    null,
                    null);
            }
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class EmptyStockQuery : IStockQuery
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            System.Array.Empty<WorldItemStackSnapshot>();
        public int GetGlobalQuantity(string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class FacilityFixtureWastewaterTransaction :
        IFluidWastewaterTransaction
    {
        public static readonly FacilityFixtureWastewaterTransaction Instance =
            new FacilityFixtureWastewaterTransaction();

        public bool TryAddWastewater(
            BuildableObject fixture,
            float amount,
            out float accepted,
            out DomainFailure failure)
        {
            accepted = Mathf.Max(0f, amount);
            failure = default;
            return true;
        }

        public bool TryConsumeWastewater(
            BuildableObject processor,
            float amount,
            out float consumed)
        {
            consumed = Mathf.Max(0f, amount);
            return true;
        }

        public bool CanAcceptWastewater(
            BuildableObject fixture,
            float amount,
            out DomainFailure failure)
        {
            failure = default;
            return true;
        }
    }

    private static float DefaultStockCostMultiplier(StockCategory category)
    {
        return 1f;
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "Facility scenario fixture has no blueprint research runtime.");
        }
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick()
        {
            return false;
        }

        public bool TryTriggerCharacterUnderPointer()
        {
            return false;
        }

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }

    private sealed class AssetDatabaseShopStockCatalog : IShopStockCatalog
    {
        public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)
        {
            stockInfo = AssetDatabase.FindAssets("t:StockInfo", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StockInfo>)
                .FirstOrDefault(candidate => candidate != null && candidate.shopId == shopId);
            return stockInfo != null;
        }

        public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)
        {
            saleItem = AssetDatabase.FindAssets("t:SaleItem", new[] { "Assets/Resources/SO/Stock" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SaleItem>)
                .FirstOrDefault(candidate => candidate != null && candidate.id == saleItemId);
            return saleItem != null;
        }

        public StockCategory GetStockCategory(int saleItemId)
        {
            return TryGetSaleItem(saleItemId, out SaleItem saleItem)
                ? saleItem.category
                : StockCategory.General;
        }
    }

    private sealed class FixedGameDataProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState gameData;

        public FixedGameDataProvider(GameSessionState gameData)
        {
            this.gameData = gameData;
        }

        public bool TryGetSessionState(out GameSessionState resolvedGameData)
        {
            resolvedGameData = gameData;
            return resolvedGameData != null;
        }
    }

    private sealed class NoopFloatingNumberFeedbackService : IFloatingNumberFeedbackService
    {
        public bool TryShow(
            NumberCondition condition,
            Vector3 worldPosition,
            float value)
        {
            return false;
        }
    }

    private sealed class NoopWorkforceReplanService : IWorkforceReplanService
    {
        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

    }
}
