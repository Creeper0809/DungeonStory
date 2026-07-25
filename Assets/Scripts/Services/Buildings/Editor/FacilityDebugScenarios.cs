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
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
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
        RunScenario("창고 런타임 재고", VerifyWarehouseInventory, errors);
        RunScenario("창고에서 상점 보충", VerifyWarehouseRestocksShop, errors);
        RunScenario("운영일 납품 제안", VerifyDailyDeliveryOffers, errors);
        RunScenario("돈으로 창고 재고 구매", VerifyPurchaseDeliveryUsesMoneyAndWarehouseCapacity, errors);
        RunScenario("재고 구매 실패 조건", VerifyPurchaseDeliveryFailureConditions, errors);
        RunScenario("침입 보상 재고 입고", VerifyDefenseRewardStockGrant, errors);
        RunScenario("내부 생산 확장 슬롯", VerifyInternalProductionSlot, errors);

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

        return warehouseBuilding is IWarehouseFacility warehouse
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory.TotalStock == warehouseBuilding.GetInternalStockCapacity()
            && warehouse.Inventory.GetStock(StockCategory.Food) > 0
            && warehouse.Inventory.GetStock(StockCategory.Weapon) > 0
            && warehouse.Inventory.GetStock(StockCategory.Mana) > 0;
    }

    private static bool VerifyWarehouseRestocksShop()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject shopBuilding = world.Place("P1_LowFoodShop", new Vector2Int(1, 0));
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(7, 0));
        Shop shop = shopBuilding as Shop;
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        ClearShopStock(shopBuilding);

        int beforeWarehouseFood = warehouse.Inventory.GetStock(StockCategory.Food);
        int moved = shop.RestockFrom(new[] { warehouse }, 5, out string message);

        return moved == 5
            && message == "5개 보충"
            && shop.CurrentStock == 5
            && warehouse.Inventory.GetStock(StockCategory.Food) == beforeWarehouseFood - 5;
    }

    private static bool VerifyDailyDeliveryOffers()
    {
        IReadOnlyList<StockDeliveryOffer> offers = StockSupplyService.CreateDailyDeliveryOffers(1, DefaultStockCostMultiplier);

        return offers.Count >= 7
            && offers.Any((offer) => offer.category == StockCategory.Food && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.General && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Weapon && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Mana && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Water && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Medicine && offer.amount > 0 && offer.cost > 0)
            && offers.Any((offer) => offer.category == StockCategory.Fuel && offer.amount > 0 && offer.cost > 0);
    }

    private static bool VerifyPurchaseDeliveryUsesMoneyAndWarehouseCapacity()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        GameData gameData = CreateGameData(500);
        warehouse.Inventory.Withdraw(StockCategory.Food, 10);

        int beforeMoney = gameData.holdingMoney.Value;
        int beforeFood = warehouse.Inventory.GetStock(StockCategory.Food);
        StockDeliveryOffer offer = new StockDeliveryOffer(StockCategory.Food, 5, 40, "테스트 납품");
        bool success = StockSupplyService.TryPurchaseDelivery(
            gameData,
            new[] { warehouse },
            offer,
            out StockSupplyResult result);

        int afterMoney = gameData.holdingMoney.Value;
        Object.DestroyImmediate(gameData);
        return success
            && result.success
            && result.deliveredAmount == 5
            && warehouse.Inventory.GetStock(StockCategory.Food) == beforeFood + 5
            && warehouse.Inventory.TotalStock <= warehouse.Inventory.MaxCapacity
            && beforeMoney - afterMoney == 40;
    }

    private static bool VerifyPurchaseDeliveryFailureConditions()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        GameData poorData = CreateGameData(1);
        GameData richData = CreateGameData(500);
        StockDeliveryOffer offer = new StockDeliveryOffer(StockCategory.Food, 5, 40, "테스트 납품");

        bool noMoney = !StockSupplyService.TryPurchaseDelivery(
            poorData,
            new[] { warehouse },
            offer,
            out StockSupplyResult noMoneyResult)
            && noMoneyResult.reason == "자금 부족";

        bool noSpace = !StockSupplyService.TryPurchaseDelivery(
            richData,
            new[] { warehouse },
            offer,
            out StockSupplyResult noSpaceResult)
            && noSpaceResult.reason == "창고 공간 부족";

        Object.DestroyImmediate(poorData);
        Object.DestroyImmediate(richData);
        return noMoney && noSpace;
    }

    private static bool VerifyDefenseRewardStockGrant()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        warehouse.Inventory.Withdraw(StockCategory.Weapon, 5);

        int beforeWeapon = warehouse.Inventory.GetStock(StockCategory.Weapon);
        bool success = StockSupplyService.GrantReward(
            new[] { warehouse },
            StockCategory.Weapon,
            3,
            "침입 방어 보상",
            out StockSupplyResult result);

        return success
            && result.success
            && result.cost == 0
            && result.deliveredAmount == 3
            && warehouse.Inventory.GetStock(StockCategory.Weapon) == beforeWeapon + 3;
    }

    private static bool VerifyInternalProductionSlot()
    {
        using FacilityScenarioWorld world = new FacilityScenarioWorld();
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(5, 0));
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        warehouse.Inventory.Withdraw(StockCategory.Mana, 4);

        int beforeMana = warehouse.Inventory.GetStock(StockCategory.Mana);
        List<StockSupplyResult> results = StockSupplyService.RunInternalProduction(
            new[] { warehouse },
            new[] { new StockProductionRule(StockCategory.Mana, 2, "내부 생산") });

        return results.Count == 1
            && results[0].success
            && results[0].deliveredAmount == 2
            && warehouse.Inventory.GetStock(StockCategory.Mana) == beforeMana + 2;
    }

    private static void ClearShopStock(BuildableObject building)
    {
        FieldInfo field = typeof(Shop).GetField("stocks", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(building, new List<RemainStock>());
        }
    }

    private static GameData CreateGameData(int holdingMoney)
    {
        GameData gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.holdingMoney = new Data<int>();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class FacilityScenarioWorld : System.IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();
        private readonly IGameDataProvider gameDataProvider;

        public FacilityScenarioWorld()
        {
            GameData gameData = CreateGameData(1000);
            scriptableObjects.Add(gameData);
            gameDataProvider = new FixedGameDataProvider(gameData);
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
            buildingData.type = typeof(Door);
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
            building.ConstructBuildableObject(
                BlueprintResearchWorkService,
                WorldInfoClickSelector,
                FacilityCandidateCacheService,
                RoomFacilityPolicyService);
            building.ConstructBuildableObjectEventBus(
                CharacterAiEditorTestDependencies.GameEvents);
            if (building is Shop shop)
            {
                shop.ConstructShop(
                    gameDataProvider,
                    ShopStockCatalogService,
                    FloatingNumberFeedbackService,
                    WorkforceReplanService,
                    FacilityCrimeEditorTestDependencies.Evaluator,
                    new DungeonStory.Foundation.RandomStreamProvider(101));
            }
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
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

    private sealed class FixedGameDataProvider : IGameDataProvider
    {
        private readonly GameData gameData;

        public FixedGameDataProvider(GameData gameData)
        {
            this.gameData = gameData;
        }

        public bool TryGetGameData(out GameData resolvedGameData)
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
