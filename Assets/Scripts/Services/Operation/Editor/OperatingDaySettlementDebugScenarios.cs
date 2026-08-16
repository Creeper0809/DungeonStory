using System.Collections.Generic;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OperatingDaySettlementDebugScenarios
{
    private static readonly IBlueprintResearchWorkService BlueprintResearchWorkService =
        new NoopBlueprintResearchWorkService();
    private static readonly IWorldInfoClickSelector WorldInfoClickSelector =
        new NoopWorldInfoClickSelector();
    private static readonly IBuildingDamageRulePort BuildingDamageRulePort =
        new AllowBuildingDamageRulePort();
    private static readonly IFacilityCandidateCache FacilityCandidateCache =
        new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null);
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
        RunScenario(
            "긴급 지원금 제거와 연속 체불 결과",
            VerifyEmergencyFundingAndShortfallConsequences,
            errors);
        RunScenario(
            "invalid settlement payload fails preflight",
            VerifyInvalidSettlementPayloadFailsPreflight,
            errors);
        RunScenario(
            "discarded settlement candidate preserves live ledger",
            VerifyDiscardedSettlementCandidatePreservesLiveLedger,
            errors);
        RunScenario(
            "settlement event order and money ledger are idempotent",
            VerifySettlementEventOrderAndMoneyIdempotence,
            errors);

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
        GameSessionState gameData = CreateGameData(5000);
        FixedMoneyRuntime money = new FixedMoneyRuntime(gameData.holdingMoney.Value);
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
            new DungeonStory.Foundation.GameEventBus(),
            new FixedArrearsEmploymentRuntime(0),
            money,
            CreateEmptyPaidFacilityContracts(gameData, money),
            stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
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

        Object.DestroyImmediate(shop.BuildingData);
        Object.DestroyImmediate(warehouse.BuildingData);
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
        GameSessionState gameData = null;
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
            FixedArrearsEmploymentRuntime employment =
                new FixedArrearsEmploymentRuntime(100);
            FixedMoneyRuntime money = new FixedMoneyRuntime(0);
            runtime.Construct(
                worldQuery,
                worldQuery,
                new EmptyFacilityShopCatalog(),
                new NeutralRunVariableReader(),
                new FixedGameDataProvider(gameData),
                new DungeonStory.Foundation.GameEventBus(),
                employment,
                money,
                CreateEmptyPaidFacilityContracts(gameData, money),
                stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
                buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRootStore: new DungeonRuntimeAggregateRootStore());

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

            return !firstFunding
                && !secondFunding
                && moneyAfterFunding == 0
                && debtAfterFunding == 0
                && firstReport != null
                && firstReport.maintenanceCost == 0
                && firstReport.payrollCost == 100
                && firstReport.previousDebt == 0
                && firstReport.paidOperatingCost == 0
                && firstDebt == firstReport.unpaidOperatingCost
                && firstDebt == 100
                && secondReport != null
                && secondReport.previousDebt == firstDebt
                && secondReport.payrollCost == 100
                && secondReport.paidOperatingCost == 0
                && secondReport.unpaidOperatingCost == secondReport.totalOperatingCost
                && secondReport.unpaidOperatingCost == 200
                && secondReport.consecutiveShortfallDays == 2
                && !facility.IsDamaged
                && wageFactorCount == 0
                && persisted.OutstandingDebt == secondReport.unpaidOperatingCost
                && persisted.ConsecutiveShortfallDays == 2
                && !persisted.EmergencyFundingUsed;
        }
        finally
        {
            if (facility != null && facility.BuildingData != null) Object.DestroyImmediate(facility.BuildingData);
            if (staff != null) Object.DestroyImmediate(staff.gameObject);
            if (facility != null) Object.DestroyImmediate(facility.gameObject);
            if (runtimeObject != null) Object.DestroyImmediate(runtimeObject);
        }
    }

    private static bool VerifyInvalidSettlementPayloadFailsPreflight()
    {
        TestSettlementSaveService saveService = new TestSettlementSaveService();
        OperatingDaySettlementSaveSection section =
            new OperatingDaySettlementSaveSection(saveService);
        DungeonOperatingDaySettlementSaveData payload =
            new DungeonOperatingDaySettlementSaveData
            {
                currentDay = 0,
                totalRevenue = -1,
                facilityRevenue = new List<DungeonStringIntSaveEntry>
                {
                    new DungeonStringIntSaveEntry { key = "shop", value = 4 },
                    new DungeonStringIntSaveEntry { key = "shop", value = 5 }
                },
                visitorMoodSamples = new List<float> { float.NaN }
            };
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool rejected = false;
        try
        {
            section.ValidatePayload(
                JsonUtility.ToJson(payload),
                section.SectionVersion,
                report);
        }
        catch (System.InvalidOperationException)
        {
            rejected = true;
        }
        return rejected && saveService.PrepareCount == 0;
    }

    private static bool VerifyDiscardedSettlementCandidatePreservesLiveLedger()
    {
        DungeonRuntimeAggregateRootStore sourceRoot =
            new DungeonRuntimeAggregateRootStore();
        DungeonRuntimeAggregateRootStore targetRoot =
            new DungeonRuntimeAggregateRootStore();
        GameObject sourceObject = new GameObject("Settlement_Discard_Source");
        GameObject targetObject = new GameObject("Settlement_Discard_Target");
        try
        {
            OperatingDaySettlementRuntime source = CreateSettlementRuntime(
                sourceObject,
                sourceRoot);
            source.RestorePersistentState(CreateSettlementState(
                day: 9,
                revenue: 222,
                visits: 7));
            string candidatePayload = CreateSaveSection(source).Capture();

            OperatingDaySettlementRuntime target = CreateSettlementRuntime(
                targetObject,
                targetRoot);
            target.RestorePersistentState(CreateSettlementState(
                day: 3,
                revenue: 11,
                visits: 2));
            OperatingDaySettlementSaveSection targetSection =
                CreateSaveSection(target);
            SettlementFailureSection failure = new SettlementFailureSection
            {
                RemainingCommitFailures = 1
            };
            SettlementDiscardObserver observer =
                new SettlementDiscardObserver(target);
            DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[] { targetSection, failure },
                targetRoot,
                new IDungeonRestoreTransactionParticipant[] { observer });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            envelopes.First(envelope => string.Equals(
                    envelope.sectionId,
                    OperatingDaySettlementSaveSection.Id,
                    System.StringComparison.Ordinal))
                .payloadJson = candidatePayload;

            bool restored = registry.RestoreAll(
                envelopes,
                new DungeonGameRestoreReport());
            return !restored
                && observer.DiscardCount == 1
                && observer.ObservedLiveLedger
                && target.CurrentDay == 3
                && target.CurrentRevenue == 11
                && target.CurrentVisits == 2
                && targetRoot.PublishedRestoreRevision == 1;
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    private static bool VerifySettlementEventOrderAndMoneyIdempotence()
    {
        GameObject runtimeObject = null;
        BuildableObject shop = null;
        try
        {
            runtimeObject = new GameObject("Settlement_Idempotence_Test");
            OperatingDaySettlementRuntime runtime =
                runtimeObject.AddComponent<OperatingDaySettlementRuntime>();
            shop = CreateBuilding("Idempotent Shop", false, 0);
            FixedWorldQuery world = new FixedWorldQuery(
                System.Array.Empty<CharacterActor>(),
                new[] { shop });
            GameSessionState gameData = CreateGameData(100);
            FixedMoneyRuntime money = new FixedMoneyRuntime(100);
            FixedArrearsEmploymentRuntime employment =
                new FixedArrearsEmploymentRuntime(10, money);
            runtime.Construct(
                world,
                world,
                new EmptyFacilityShopCatalog(),
                new NeutralRunVariableReader(),
                new FixedGameDataProvider(gameData),
                new DungeonStory.Foundation.GameEventBus(),
                employment,
                money,
                CreateEmptyPaidFacilityContracts(gameData, money),
                stockCategoryCatalog:
                    CharacterAiEditorTestDependencies.AuthoredGameplay,
                buildingCategoryCatalog:
                    CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRootStore: new DungeonRuntimeAggregateRootStore());

            runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            runtime.OnTriggerEvent(new FacilityRevenueEvent(null, shop, 15));
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
            OperatingDayReport first = runtime.LatestReport;
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));

            return first != null
                && first.totalRevenue == 15
                && first.payrollCost == 10
                && first.paidOperatingCost == 10
                && runtime.ReportHistory.Count == 1
                && employment.SettlementCount == 1
                && money.SuccessfulSpendCount == 1
                && money.Balance == 90;
        }
        finally
        {
            if (shop != null && shop.BuildingData != null)
            {
                Object.DestroyImmediate(shop.BuildingData);
            }
            if (shop != null)
            {
                Object.DestroyImmediate(shop.gameObject);
            }
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }

    private static OperatingDaySettlementRuntime CreateSettlementRuntime(
        GameObject host,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        OperatingDaySettlementRuntime runtime =
            host.AddComponent<OperatingDaySettlementRuntime>();
        FixedWorldQuery world = new FixedWorldQuery(
            System.Array.Empty<CharacterActor>(),
            System.Array.Empty<BuildableObject>());
        GameSessionState gameData = CreateGameData(0);
        FixedMoneyRuntime money = new FixedMoneyRuntime(0);
        runtime.Construct(
            world,
            world,
            new EmptyFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new FixedGameDataProvider(gameData),
            new DungeonStory.Foundation.GameEventBus(),
            new FixedArrearsEmploymentRuntime(0),
            money,
            CreateEmptyPaidFacilityContracts(gameData, money),
            stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore: aggregateRootStore);
        return runtime;
    }

    private static IPaidFacilityContractRuntime CreateEmptyPaidFacilityContracts(
        GameSessionState gameData,
        IGameMoneyAccount money)
    {
        return new PaidFacilityContractRuntime(
            new FixedGameDataProvider(gameData),
            money,
            new TreasuryEconomyAggregateStateStore(
                new DungeonRuntimeAggregateRootStore()));
    }

    private static OperatingDaySettlementPersistenceState CreateSettlementState(
        int day,
        int revenue,
        int visits)
    {
        return new OperatingDaySettlementPersistenceState(
            day,
            revenue,
            visits,
            restockFailureCount: 0,
            facilityRevenue: new Dictionary<string, int>(),
            speciesVisits: new Dictionary<string, int>(),
            consumedStock: new Dictionary<StockCategory, int>(),
            visitorMoodSamples: System.Array.Empty<float>(),
            stockSupplyResults: System.Array.Empty<StockSupplyResult>(),
            incidents: System.Array.Empty<string>(),
            eventLog: System.Array.Empty<string>(),
            reportHistory: System.Array.Empty<OperatingDayReport>());
    }

    private static OperatingDaySettlementSaveSection CreateSaveSection(
        OperatingDaySettlementRuntime runtime)
    {
        DungeonSceneRuntimeReferences references =
            new DungeonSceneRuntimeReferences(
                new DungeonSceneServiceReferences(null, runtime, null, null),
                new DungeonSceneViewReferences(
                    null, null, null, null, null, null, null, null));
        return new OperatingDaySettlementSaveSection(
            new OperatingDaySettlementSaveService(references));
    }

    private sealed class TestSettlementSaveService :
        IOperatingDaySettlementSaveService
    {
        public int PrepareCount { get; private set; }

        public DungeonOperatingDaySettlementSaveData Capture() =>
            new DungeonOperatingDaySettlementSaveData();

        public OperatingDaySettlementRestoreCandidate PrepareRestore(
            DungeonOperatingDaySettlementSaveData source)
        {
            PrepareCount++;
            throw new System.InvalidOperationException(
                "Invalid payload must fail before candidate preparation.");
        }

        public void PublishRestore(
            OperatingDaySettlementRestoreCandidate candidate)
        {
        }
    }

    private sealed class SettlementFailureSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        public string SectionId => "operation.settlement.debug.failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn =>
            new[] { OperatingDaySettlementSaveSection.Id };
        public int RemainingCommitFailures { get; set; }
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new System.InvalidOperationException(
                    $"Unexpected settlement failure version {sectionVersion}.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (RemainingCommitFailures <= 0)
                {
                    return;
                }

                RemainingCommitFailures--;
                throw new System.InvalidOperationException(
                    "Injected settlement restore failure.");
            });
        }
    }

    private sealed class SettlementDiscardObserver :
        IDungeonRestoreTransactionParticipant
    {
        private readonly OperatingDaySettlementRuntime runtime;
        private bool hasCandidate;

        public SettlementDiscardObserver(
            OperatingDaySettlementRuntime runtime)
        {
            this.runtime = runtime;
        }

        public string ParticipantId =>
            "operation.settlement.debug.discard-observer";
        public int DiscardCount { get; private set; }
        public bool ObservedLiveLedger { get; private set; }

        public void BeginRestoreCandidate()
        {
            hasCandidate = true;
        }

        public void PublishRestoreCandidate()
        {
            hasCandidate = false;
        }

        public void DiscardRestoreCandidate()
        {
            if (!hasCandidate)
            {
                return;
            }

            hasCandidate = false;
            DiscardCount++;
            ObservedLiveLedger = runtime.CurrentDay == 3
                && runtime.CurrentRevenue == 11
                && runtime.CurrentVisits == 2;
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
        CharacterSO data = AssetDatabase
            .FindAssets("t:CharacterSO", new[] { "Assets/Resources/SO/Character" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
            .FirstOrDefault(candidate => candidate != null
                && candidate.species != null
                && string.Equals(
                    candidate.SpeciesTag,
                    speciesTag,
                    System.StringComparison.OrdinalIgnoreCase));
        if (data == null)
        {
            Object.DestroyImmediate(obj);
            throw new System.InvalidOperationException(
                $"No authored character archetype exists for species '{speciesTag}'.");
        }
        character.data = data;
        character.characterType = type;
        ICharacterNeedDefinitionCatalog needCatalog = CharacterAiEditorTestDependencies.AuthoredGameplay;
        Dictionary<CharacterCondition, float> initialStats = needCatalog.All
            .ToDictionary((definition) => definition.Condition, (definition) => definition.DefaultValue);
        initialStats[CharacterCondition.HUNGER] = 100f;
        initialStats[CharacterCondition.FUN] = 50f;
        initialStats[CharacterCondition.MOOD] = mood;
        initialStats[CharacterCondition.SLEEP] = sleep;
        character.stats = initialStats;
        return character;
    }

    private sealed class FixedArrearsEmploymentRuntime :
        IEmploymentContractRuntime
    {
        private readonly int dailyWage;
        private readonly IGameMoneyAccount money;
        private int arrears;

        public FixedArrearsEmploymentRuntime(
            int dailyWage,
            IGameMoneyAccount money = null)
        {
            this.dailyWage = Mathf.Max(0, dailyWage);
            this.money = money;
        }

        public int SettlementCount { get; private set; }

        public IReadOnlyList<EmployeeWageState> WageStates =>
            System.Array.Empty<EmployeeWageState>();
        public IReadOnlyList<MercenaryContract> MercenaryContracts =>
            System.Array.Empty<MercenaryContract>();

        public int ForecastCost(int days)
        {
            return dailyWage * Mathf.Max(0, days) + arrears;
        }

        public int GetDailyCost(string characterId)
        {
            return dailyWage;
        }

        public int QuoteMercenaryDailyCost(
            string characterId,
            int level,
            int rolePremium)
        {
            return dailyWage;
        }

        public EmploymentDailySettlement SettleDay(int day)
        {
            SettlementCount++;
            int due = dailyWage + arrears;
            int paid = 0;
            if (money != null && due > 0)
            {
                int payable = Mathf.Min(money.Balance, due);
                if (payable > 0 && money.TrySpend(payable, out _))
                {
                    paid = payable;
                }
            }
            arrears = due - paid;
            return new EmploymentDailySettlement
            {
                day = Mathf.Max(1, day),
                employeeWagesDue = due,
                employeeWagesPaid = paid,
                unpaidEmployeeWages = arrears
            };
        }

        public bool TryHireMercenary(
            CharacterActor actor,
            int rolePremium,
            int day,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }

        public bool SetEmployeeRolePremium(
            string characterId,
            int premium,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }

        public EmploymentContractSaveData Capture()
        {
            return new EmploymentContractSaveData();
        }
    }

    private sealed class FixedMoneyRuntime : IGameMoneyAccount
    {
        private int balance;

        public FixedMoneyRuntime(int balance)
        {
            this.balance = Mathf.Max(0, balance);
        }

        public int Balance => balance;
        public int SuccessfulSpendCount { get; private set; }
        public bool CanSpend(int amount) =>
            balance >= Mathf.Max(0, amount);

        public bool TrySpend(int amount, out string reason)
        {
            return TrySpend(
                amount,
                default,
                out reason);
        }

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            int cost = Mathf.Max(0, amount);
            if (balance < cost)
            {
                reason = "insufficient funds";
                return false;
            }

            balance -= cost;
            SuccessfulSpendCount++;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount)
        {
            balance += Mathf.Max(0, amount);
        }

        public void Add(
            int amount,
            EconomyTransactionContext context)
        {
            Add(amount);
        }

        public void SetBalance(
            int amount,
            EconomyTransactionContext context)
        {
            balance = Mathf.Max(0, amount);
        }
    }

    private static BuildableObject CreateBuilding(string objectName, bool damaged, int maintenance)
    {
        GameObject obj = new GameObject(objectName);
        BuildableObject building = obj.AddComponent<BuildableObject>();
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        building.ConstructDebugRules(BuildingDamageRulePort);
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
        building.ConstructBuildableObject(
            new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
            FacilityCandidateCache,
            RoomFacilityPolicy, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
        building.Initialization(data, Vector2Int.zero);
        building.SetDamaged(damaged);
        return building;
    }

    private static GameSessionState CreateGameData(int holdingMoney)
    {
        GameSessionState data = new GameSessionState();
        data.holdingMoney.Initialize(Mathf.Max(0, holdingMoney));
        data.day.Initialize(1);
        data.hour.Initialize(0);
        data.gameSpeed.Initialize(1);
        return data;
    }

    private static Facility CreateWarehouse(string objectName, int capacity)
    {
        GameObject obj = new GameObject(objectName);
        Facility warehouse = obj.AddComponent<Facility>();
        warehouse.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        warehouse.ConstructDebugRules(BuildingDamageRulePort);
        warehouse.ConstructFacility(null, new EmptyStockQuery(), mealConsumptionRuntime: null, waterFixtureUseRuntime: null, wastewaterNetworkRuntime: PermissiveWastewaterTransaction.Instance, serviceSessionRuntime: null, serviceRoomLinkRuntime: null,
            stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay);
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
        warehouse.ConstructBuildableObject(
            new BuildingResearchWorkPortAdapter(BlueprintResearchWorkService),
            FacilityCandidateCache,
            RoomFacilityPolicy, combatEquipmentRuntime: null, worldRegistry: null, worldItemStackRuntime: null, abilityRuntimeDispatcher: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
        warehouse.Initialization(data, Vector2Int.zero);
        return warehouse;
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

    private sealed class AllowBuildingDamageRulePort : IBuildingDamageRulePort
    {
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
    }

    private sealed class PermissiveWastewaterTransaction : IFluidWastewaterTransaction
    {
        public static readonly PermissiveWastewaterTransaction Instance =
            new PermissiveWastewaterTransaction();

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
        public DungeonSurvivalPressure GetSurvivalPressure() =>
            DungeonSurvivalPressure.Standard;
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

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
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
