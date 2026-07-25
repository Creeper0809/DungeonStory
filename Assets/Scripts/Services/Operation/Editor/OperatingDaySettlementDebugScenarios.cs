using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OperatingDaySettlementDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IFacilityCandidateCache FacilityCandidateCache =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    [MenuItem("DungeonStory/Debug/Operation/Run P1 Operating Day Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 operating day scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("운영일 정산 수집", VerifySettlementCollectsRuntimeEvents, errors);
        RunScenario("정산 상세 텍스트", VerifyReportDetailText, errors);
        RunScenario("operating report snapshot isolation", VerifyReportSnapshotIsolation, errors);
        RunScenario("운영비 계산과 부분 납부", VerifyOperatingCostCalculator, errors);
        RunScenario("긴급 융자와 연속 체불 결과", VerifyEmergencyFundingAndShortfallConsequences, errors);

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
            Debug.Log("P1 operating day scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        if (scenario()) return;

        errors.Add(name);
    }

    private static bool VerifySettlementCollectsRuntimeEvents()
    {
        GameObject runtimeObject = new GameObject("OperatingDaySettlementRuntime_Test");
        OperatingDaySettlementRuntime runtime = runtimeObject.AddComponent<OperatingDaySettlementRuntime>();

        CharacterActor customer = CreateCharacter("Customer_Test", CharacterType.Customer, "slime", 64f, 80f, false);
        CharacterActor staff = CreateCharacter("Staff_Test", CharacterType.NPC, "orc", 0f, 20f, true);
        BuildableObject shop = CreateBuilding("Food Shop", false, 10);
        Facility warehouse = CreateWarehouse("Warehouse", 24);
        GameData gameData = CreateGameData(5000);
        runtime.Construct(
            new FixedWorldQuery(
                new[] { customer, staff },
                new BuildableObject[] { shop, warehouse }),
            new FixedWorldQuery(
                new[] { customer, staff },
                new BuildableObject[] { shop, warehouse }),
            new EmptyFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new FixedGameDataProvider(gameData),
            new DungeonStory.Foundation.GameEventBus());
        float expectedSatisfaction = customer.Stats.Stats[CharacterCondition.MOOD];

        runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
        runtime.OnTriggerEvent(new FacilityVisitEvent(CharacterActor.From(customer), shop));
        runtime.OnTriggerEvent(new FacilityRevenueEvent(CharacterActor.From(customer), shop, 120));
        runtime.OnTriggerEvent(new FacilityStockConsumedEvent(CharacterActor.From(customer), shop, StockCategory.Food, 2));
        runtime.OnTriggerEvent(new FacilityCrimeEvent(CharacterActor.From(customer), shop, FacilityCrimeKind.Shoplifting, "Shoplifting test", 30));
        runtime.OnTriggerEvent(new FacilityRestockEvent(shop, 5, 0, "창고 재고 부족"));
        runtime.OnTriggerEvent(new StockSupplyEvent(new StockSupplyResult(true, StockCategory.Food, 5, 5, 20, "테스트 납품", string.Empty)));
        runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));

        OperatingDayReport report = runtime.LatestReport;
        bool valid = report != null
            && report.day == 1
            && report.totalVisits == 1
            && report.totalRevenue == 120
            && Mathf.Approximately(report.averageSatisfaction, expectedSatisfaction)
            && report.restockFailureCount == 1
            && report.facilityRevenues.Count == 1
            && report.facilityRevenues[0].revenue == 120
            && report.speciesVisits.Count == 1
            && report.stockConsumed.Count == 1
            && report.stockConsumed[0].amount == 2
            && report.incidents.Count == 1
            && report.incidents[0] == "Shoplifting test"
            && report.stockSupplyResults.Count == 1
            && report.warehouseStocks.Count >= 1
            && report.staffSummary.staffCount >= 1
            && report.staffComplaintEvents.Count >= 1
            && report.refreshedDailyShopOffers.Count >= 7;

        if (!valid)
        {
            Debug.LogError(
                "Operating day collection detail: "
                + $"report={(report != null)}, day={report?.day}, visits={report?.totalVisits}, "
                + $"revenue={report?.totalRevenue}, satisfaction={report?.averageSatisfaction}/{expectedSatisfaction}, "
                + $"restockFailures={report?.restockFailureCount}, facilityRevenue={report?.facilityRevenues.Count}, "
                + $"species={report?.speciesVisits.Count}, consumed={report?.stockConsumed.Count}, "
                + $"incidents={report?.incidents.Count}, supplies={report?.stockSupplyResults.Count}, "
                + $"warehouses={report?.warehouseStocks.Count}, staff={report?.staffSummary.staffCount}, "
                + $"complaints={report?.staffComplaintEvents.Count}, dailyOffers={report?.refreshedDailyShopOffers.Count}");
        }

        Object.DestroyImmediate(customer.data);
        Object.DestroyImmediate(staff.data);
        Object.DestroyImmediate(shop.BuildingData);
        Object.DestroyImmediate(warehouse.BuildingData);
        Object.DestroyImmediate(gameData);
        Object.DestroyImmediate(customer.gameObject);
        Object.DestroyImmediate(staff.gameObject);
        Object.DestroyImmediate(shop.gameObject);
        Object.DestroyImmediate(warehouse.gameObject);
        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifyReportDetailText()
    {
        OperatingDayReport report = OperatingDayReport.Create(
            day: 2,
            totalRevenue: 50,
            totalVisits: 3,
            averageSatisfaction: 70f,
            eventLog: new List<string> { "설계도 획득" });

        string detail = report.ToDetailText();
        return detail.Contains("Day 2")
            && detail.Contains("총 매출: 50")
            && detail.Contains("이벤트 로그")
            && detail.Contains("설계도 획득");
    }

    private static bool VerifyReportSnapshotIsolation()
    {
        List<string> sourceIncidents = new List<string> { "before" };
        FacilityShopOfferSnapshot sourceOffer = new FacilityShopOfferSnapshot(
            FacilityShopOfferTypeIds.Building,
            "시설",
            FacilityShopRarity.Common,
            "before",
            100,
            1,
            false);
        List<FacilityShopOfferSnapshot> sourceOffers = new List<FacilityShopOfferSnapshot> { sourceOffer };
        OperatingDayReport report = OperatingDayReport.Create(
            day: 1,
            incidents: sourceIncidents,
            refreshedFacilityShopOffers: sourceOffers);
        OperatingDayReportEvent reportEvent = new OperatingDayReportEvent(report);

        sourceIncidents[0] = "after";
        sourceOffers[0] = new FacilityShopOfferSnapshot(
            FacilityShopOfferTypeIds.Blueprint,
            "설계도",
            FacilityShopRarity.Special,
            "after",
            200,
            0,
            false);

        bool mutationRejected = false;
        if (report.incidents is IList<string> incidentList)
        {
            try
            {
                incidentList[0] = "mutated";
            }
            catch (System.NotSupportedException)
            {
                mutationRejected = true;
            }
        }

        return mutationRejected
            && report.incidents[0] == "before"
            && report.refreshedFacilityShopOffers[0].displayName == "before"
            && object.ReferenceEquals(reportEvent.report, report);
    }

    private static bool VerifyOperatingCostCalculator()
    {
        DungeonEconomySettings settings = new DungeonEconomySettings
        {
            baseStaffWage = 35,
            workingStaffBonus = 10
        };
        int payroll = DungeonEconomyCalculator.CalculatePayroll(3, 2, settings);
        OperatingCostForecast payable = new OperatingCostForecast(
            availableMoney: 500,
            maintenanceCost: 80,
            payrollCost: payroll,
            outstandingDebt: 20);
        OperatingCostSettlement paid = DungeonEconomyCalculator.Settle(payable, 2);
        OperatingCostForecast shortForecast = new OperatingCostForecast(
            availableMoney: 100,
            maintenanceCost: 80,
            payrollCost: payroll,
            outstandingDebt: 20);
        OperatingCostSettlement shortfall = DungeonEconomyCalculator.Settle(shortForecast, 2);

        return payroll == 125
            && payable.TotalDue == 225
            && paid.PaidAmount == 225
            && paid.ClosingBalance == 275
            && paid.CarriedDebt == 0
            && paid.ConsecutiveShortfallDays == 0
            && shortfall.PaidAmount == 100
            && shortfall.CarriedDebt == 125
            && shortfall.ConsecutiveShortfallDays == 3;
    }

    private static bool VerifyEmergencyFundingAndShortfallConsequences()
    {
        GameObject runtimeObject = null;
        CharacterActor staff = null;
        BuildableObject facility = null;
        GameData gameData = null;
        try
        {
            runtimeObject = new GameObject("OperatingDayEconomyRuntime_Test");
            OperatingDaySettlementRuntime runtime = runtimeObject.AddComponent<OperatingDaySettlementRuntime>();
            staff = CreateCharacter("EconomyStaff_Test", CharacterType.NPC, "slime", 70f, 80f, true);
            facility = CreateBuilding("Expensive Facility", false, 60);
            gameData = CreateGameData(0);
            FixedWorldQuery worldQuery = new FixedWorldQuery(
                new[] { staff },
                new[] { facility });
            runtime.Construct(
                worldQuery,
                worldQuery,
                new EmptyFacilityShopCatalog(),
                new NeutralRunVariableReader(),
                new FixedGameDataProvider(gameData),
                new DungeonStory.Foundation.GameEventBus());

            bool firstFunding = runtime.TryTakeEmergencyFunding(out _);
            bool secondFunding = runtime.TryTakeEmergencyFunding(out _);
            int moneyAfterFunding = gameData.holdingMoney.Value;
            int debtAfterFunding = runtime.OutstandingDebt;

            runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
            OperatingDayReport firstReport = runtime.LatestReport;
            int firstDebt = runtime.OutstandingDebt;

            runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(2));
            OperatingDayReport secondReport = runtime.LatestReport;
            CharacterMoodSnapshot mood = staff.Mood;
            int wageFactorCount = mood.Factors.Count(factor => factor.Id == "economy:unpaid-wages");
            OperatingDaySettlementPersistenceState persisted = runtime.CapturePersistentState();

            return firstFunding
                && !secondFunding
                && moneyAfterFunding == 1000
                && debtAfterFunding == 1200
                && firstReport != null
                && firstReport.maintenanceCost == 60
                && firstReport.payrollCost >= 35
                && firstReport.previousDebt == 1200
                && firstReport.paidOperatingCost == 1000
                && firstDebt == firstReport.unpaidOperatingCost
                && firstDebt > 0
                && secondReport != null
                && secondReport.previousDebt == firstDebt
                && secondReport.paidOperatingCost == 0
                && secondReport.unpaidOperatingCost == secondReport.totalOperatingCost
                && secondReport.consecutiveShortfallDays == 2
                && facility.IsDamaged
                && wageFactorCount == 1
                && persisted.OutstandingDebt == secondReport.unpaidOperatingCost
                && persisted.ConsecutiveShortfallDays == 2
                && persisted.EmergencyFundingUsed;
        }
        finally
        {
            if (staff != null && staff.data != null) Object.DestroyImmediate(staff.data);
            if (facility != null && facility.BuildingData != null) Object.DestroyImmediate(facility.BuildingData);
            if (gameData != null) Object.DestroyImmediate(gameData);
            if (staff != null) Object.DestroyImmediate(staff.gameObject);
            if (facility != null) Object.DestroyImmediate(facility.gameObject);
            if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
        }
    }

    private static CharacterActor CreateCharacter(
        string name,
        CharacterType type,
        string speciesTag,
        float mood,
        float sleep,
        bool withWork)
    {
        GameObject obj = new GameObject(name);
        CharacterActor character = obj.AddComponent<CharacterActor>();
        if (withWork)
        {
            obj.AddComponent<AbilityWork>();
        }

        CharacterAiEditorTestDependencies.Inject(obj);
        character.RefreshAbilityCache();
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.characterType = type;
        data.speciesTag = speciesTag;
        character.data = data;
        character.characterType = type;
        Dictionary<CharacterCondition, float> initialStats = CharacterNeedCatalog.All
            .ToDictionary((definition) => definition.Condition, (definition) => definition.DefaultValue);
        initialStats[CharacterCondition.HUNGER] = 100f;
        initialStats[CharacterCondition.FUN] = 50f;
        initialStats[CharacterCondition.MOOD] = mood;
        initialStats[CharacterCondition.SLEEP] = sleep;
        character.stats = initialStats;
        return character;
    }

    private static BuildableObject CreateBuilding(string objectName, bool damaged, int maintenance)
    {
        GameObject obj = new GameObject(objectName);
        BuildableObject building = obj.AddComponent<BuildableObject>();
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.objectName = objectName;
        data.Maintenance = maintenance;
        data.width = 1;
        data.height = 1;
        data.category = BuildingCategory.Shop;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.Meal,
            capacity = 1
        };
        data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Operate });
        building.Initialization(data, Vector2Int.zero);
        building.ConstructBuildableObject(
            BlueprintResearchWorkService,
            WorldInfoClickSelector,
            FacilityCandidateCache,
            RoomFacilityPolicy);
        building.SetDamaged(damaged);
        return building;
    }

    private static GameData CreateGameData(int holdingMoney)
    {
        GameData data = ScriptableObject.CreateInstance<GameData>();
        data.holdingMoney = new Data<int>();
        data.holdingMoney.Initialize(Mathf.Max(0, holdingMoney));
        data.day = new Data<int>();
        data.day.Initialize(1);
        data.hour = new Data<int>();
        data.hour.Initialize(0);
        data.gameSpeed = new Data<int>();
        data.gameSpeed.Initialize(1);
        data.curTime = new Data<float>();
        data.timeOfDay = new Data<TimeOfDay>();
        return data;
    }

    private static Facility CreateWarehouse(string objectName, int capacity)
    {
        GameObject obj = new GameObject(objectName);
        Facility warehouse = obj.AddComponent<Facility>();
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.objectName = objectName;
        data.width = 1;
        data.height = 1;
        data.category = BuildingCategory.Resource;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.Logistics,
            capacity = 1
        };
        data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Restock });
        data.AbilityModules.Add(new BuildingInternalStockAbility
        {
            capacity = capacity,
            restockRequestThreshold = Mathf.Max(0, capacity / 4)
        });
        warehouse.Initialization(data, Vector2Int.zero);
        warehouse.ConstructBuildableObject(
            BlueprintResearchWorkService,
            WorldInfoClickSelector,
            FacilityCandidateCache,
            RoomFacilityPolicy);
        return warehouse;
    }

    private sealed class EmptyFacilityShopCatalog : IFacilityShopCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => System.Array.Empty<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => System.Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) => null;
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => System.Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class FixedWorldQuery :
        ICharacterWorldQuery,
        IBuildingWorldQuery
    {
        private readonly IReadOnlyList<CharacterActor> characters;
        private readonly IReadOnlyList<BuildableObject> buildings;

        public FixedWorldQuery(
            IEnumerable<CharacterActor> characters,
            IEnumerable<BuildableObject> buildings)
        {
            this.characters = characters?
                .Where(character => character != null)
                .Distinct()
                .ToArray() ?? System.Array.Empty<CharacterActor>();
            this.buildings = buildings?
                .Where(building => building != null)
                .Distinct()
                .ToArray() ?? System.Array.Empty<BuildableObject>();
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
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

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility) => false;

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "No research runtime in operation fixture.");
        }
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick() => false;
        public bool TryTriggerCharacterUnderPointer() => false;

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor)
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
}
