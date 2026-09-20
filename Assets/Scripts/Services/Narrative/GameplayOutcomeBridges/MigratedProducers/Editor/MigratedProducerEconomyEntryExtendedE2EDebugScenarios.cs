#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.AutoProcurementResult,
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult,
            MigratedProducerOutcomeKind.FacilityCrimeEvent,
            MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
            MigratedProducerOutcomeKind.OperatingDayReportEvent,
            MigratedProducerOutcomeKind.StockSupplyResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyAutoProcurementEntry();
            VerifyBuildingRetailPurchaseEntry();
            VerifyFacilityCrimeEntry();
            VerifyFacilityShopPurchaseEntry();
            VerifyOperatingDayReportEntry();
            VerifyStockSupplyEntry();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyAutoProcurementEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.AutoProcurementResult;

        using (var scenario = new AutoProcurementScenario(Kind))
        {
            scenario.Process();
            Require(
                scenario.LastProcessedDay == 1
                && scenario.ResultCount == 1,
                "The auto-procurement production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new AutoProcurementScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.Process);
            Require(
                scenario.LastProcessedDay == 0
                && scenario.ResultCount == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected auto-procurement result left a partial owner mutation.");
        }

        using (var scenario = new AutoProcurementScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.Process();
            int day = scenario.LastProcessedDay;
            int count = scenario.ResultCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.LastProcessedDay == day
                && scenario.ResultCount == count,
                "Delivery retry duplicated the auto-procurement mutation.");
        }
    }

    private static void VerifyBuildingRetailPurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;

        using (var scenario = new RetailScenario(Kind, crime: false))
        {
            scenario.Purchase();
            Require(
                scenario.PurchaseCommitted
                && scenario.StockCount == scenario.InitialStockCount - 1
                && scenario.HoldingMoney == scenario.InitialMoney - scenario.Cost,
                "The retail-purchase production entry did not mutate its owners.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.Purchase);
            Require(
                scenario.StockCount == scenario.InitialStockCount
                && scenario.HoldingMoney == scenario.InitialMoney
                && scenario.CommittedPurchaseCount == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected retail purchase left stock, money, or buyer mutation.");
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   deliveryFault: true))
        {
            scenario.Purchase();
            int stock = scenario.StockCount;
            int money = scenario.HoldingMoney;
            int purchaseCount = scenario.CommittedPurchaseCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.StockCount == stock
                && scenario.HoldingMoney == money
                && scenario.CommittedPurchaseCount == purchaseCount,
                "Delivery retry duplicated the retail purchase.");
        }
    }

    private static void VerifyFacilityCrimeEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityCrimeEvent;

        using (var scenario = new RetailScenario(Kind, crime: true))
        {
            Require(
                scenario.CommitCrime()
                && scenario.StockCount == scenario.InitialStockCount - 1,
                "The facility-crime production entry did not remove the stolen lot.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: true,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(() => scenario.CommitCrime());
            Require(
                scenario.StockCount == scenario.InitialStockCount
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility crime left the stolen lot removed.");
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: true,
                   deliveryFault: true))
        {
            Require(scenario.CommitCrime(), "The durably pending crime did not commit.");
            int stock = scenario.StockCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.StockCount == stock,
                "Delivery retry duplicated the facility crime mutation.");
        }
    }

    private static void VerifyFacilityShopPurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityShopPurchaseResult;

        using (var scenario = new FacilityShopScenario(Kind))
        {
            Require(
                scenario.Purchase()
                && scenario.Money < scenario.InitialMoney,
                "The facility-shop production entry did not spend money.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase()
                && scenario.Money == scenario.InitialMoney
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility-shop purchase left money mutation.");
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(scenario.Purchase(), "The durably pending facility-shop purchase failed.");
            int money = scenario.Money;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Money == money,
                "Delivery retry charged the facility-shop purchase twice.");
        }
    }

    private static void VerifyOperatingDayReportEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.OperatingDayReportEvent;

        using (var scenario = new OperatingDayScenario(Kind))
        {
            scenario.Settle();
            Require(
                scenario.ReportCount == 1
                && scenario.LatestReportDay == 1,
                "The operating-day production entry did not store its report.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new OperatingDayScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.Settle);
            Require(
                scenario.ReportCount == 0
                && scenario.Money == scenario.InitialMoney
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected operating-day report left report or money mutation.");
        }

        using (var scenario = new OperatingDayScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.Settle();
            int count = scenario.ReportCount;
            int money = scenario.Money;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.ReportCount == count && scenario.Money == money,
                "Delivery retry duplicated the operating-day settlement.");
        }
    }

    private static void VerifyStockSupplyEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.StockSupplyResult;

        using (var scenario = new StockSupplyScenario(Kind))
        {
            Require(
                scenario.Purchase().Succeeded
                && scenario.Quantity == scenario.Offer.amount
                && scenario.Money == scenario.InitialMoney - scenario.Offer.cost,
                "The stock-supply production entry did not mutate physical stock and money.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        }

        using (var scenario = new StockSupplyScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase().Succeeded
                && scenario.Quantity == 0
                && scenario.Money == scenario.InitialMoney
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected stock supply left physical stock or money mutation.");
        }

        using (var scenario = new StockSupplyScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(scenario.Purchase().Succeeded, "The durably pending stock supply failed.");
            int quantity = scenario.Quantity;
            int money = scenario.Money;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Quantity == quantity && scenario.Money == money,
                "Delivery retry duplicated the stock supply.");
        }
    }

    private abstract class ScenarioBase : IDisposable
    {
        protected ScenarioBase(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode,
            bool deliveryFault,
            string label)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-" + label + "-" + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        public virtual void Dispose()
        {
        }
    }

    private sealed class AutoProcurementScenario : ScenarioBase
    {
        private readonly AutoProcurementRuntime runtime;

        internal AutoProcurementScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "auto-procurement")
        {
            var state = new GameSessionState(100, 1);
            var session = new TestGameSessionStateStore(state);
            var physical = StatefulWorldItemStackProxy.Create(out _);
            var treasury = new TreasuryEconomyAggregateStateStore(
                Outcomes.AggregateRootStore);
            runtime = new AutoProcurementRuntime(
                session,
                new AutoProcurementFinancialDependencies(
                    new EditorGameMoneyAccount(state),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IEmploymentContractRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IPaidFacilityContractRuntime>()),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                new AutoProcurementStockDependencies(
                    physical,
                    CharacterAiEditorTestDependencies.AuthoredGameplay),
                treasury,
                Probe,
                session);
            runtime.ConfigureBudget(0, 0);
        }

        internal int LastProcessedDay => runtime.Capture().lastProcessedDay;
        internal int ResultCount => runtime.LastResults.Count;
        internal void Process() => runtime.ProcessShopRefresh(
            1,
            Array.Empty<FacilityShopOffer>(),
            shopRuntime: null);
    }

    private sealed class RetailScenario : ScenarioBase
    {
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly Shop shop;
        private readonly CharacterActor customer;
        private readonly AbilityShopping shopping;
        private readonly RemainStock stock;
        private readonly AIAction action;
        private readonly BuildingRetailPurchaseCommitResult purchaseResult = new();
        private readonly bool crime;

        internal RetailScenario(
            MigratedProducerOutcomeKind target,
            bool crime,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault,
                crime ? "facility-crime" : "retail-purchase")
        {
            this.crime = crime;
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            shop = world.Place("S01_판매카운터", new Vector2Int(4, 0)) as Shop
                ?? throw new InvalidOperationException("The authored shop fixture is missing.");
            customer = world.CreateCustomer(
                "Slime",
                Vector2Int.zero,
                90f,
                90f,
                10f,
                20f);
            shopping = customer.GetAbility<AbilityShopping>()
                ?? throw new InvalidOperationException("The customer shopping ability is missing.");
            shopping.ConstructRetailPurchaseOutcome(
                Probe,
                new FixedGameSessionStateProvider(7));
            shop.ConstructShopOutcomeTransactions(
                Probe,
                new FixedGameSessionStateProvider(7));
            shop.ConfigureCrimeForDiagnostics(
                AlwaysCrimeRiskEvaluator.Instance,
                ZeroRandomStream.Instance);

            RetailStockLotSnapshot source = shop.CreateStockSnapshot().lots
                .FirstOrDefault(lot => lot != null
                    && lot.quantity > 0
                    && string.IsNullOrEmpty(lot.itemInstanceId))
                ?.Clone()
                ?? throw new InvalidOperationException(
                    "The authored shop has no stackable exact retail lot.");
            source.quantity = 1;
            ShopStockStateSnapshot authored = shop.CreateStockSnapshot();
            shop.ApplyStockSnapshot(new ShopStockStateSnapshot
            {
                schemaVersion = ShopStockStateSnapshot.CurrentSchemaVersion,
                activatedAuthoredSaleItemIds = new List<int>(
                    authored.activatedAuthoredSaleItemIds),
                lots = new List<RetailStockLotSnapshot> { source }
            });
            RetailProductSnapshot product = shop.ProductSnapshots.Single(value =>
                value.Id == source.saleItemId);
            Cost = 25;
            stock = new RemainStock(
                product.Id,
                product.Name,
                Cost,
                1,
                Array.Empty<OnBuyItemSO>());
            shopping.RestorePersistentState(2, 0, 500);
            AIActionSet actionSet = Resources.Load<AIActionSet>(
                "SO/AI/Action/Shopping")
                ?? throw new InvalidOperationException(
                    "The authored shopping action fixture is missing.");
            action = new AIAction(actionSet, AIActionPlan.AtDestination(shop));
            customer.ai.bestAction = action;
            customer.ai.isBestActionEnd = false;
            InitialStockCount = shop.GetStockCount();
            InitialMoney = shopping.HoldingMoney;
        }

        internal int Cost { get; }
        internal int InitialStockCount { get; }
        internal int InitialMoney { get; }
        internal int StockCount => shop.GetStockCount();
        internal int HoldingMoney => shopping.HoldingMoney;
        internal int CommittedPurchaseCount =>
            shopping.CommittedPurchaseCountForDiagnostics;
        internal bool PurchaseCommitted => purchaseResult.Committed;

        internal void Purchase()
        {
            if (crime)
                throw new InvalidOperationException("This scenario is configured for crime.");
            IEnumerator routine = shopping.BuyItem(
                stock,
                Cost,
                action,
                shop,
                purchaseResult);
            Require(routine.MoveNext(), "The retail purchase did not reach its commit delay.");
            Require(!routine.MoveNext(), "The retail purchase did not reach a terminal state.");
        }

        internal bool CommitCrime()
        {
            if (!crime)
                throw new InvalidOperationException("This scenario is configured for purchase.");
            return shop.TryResolveCheckoutCrime(
                customer.BuildingVisitor,
                new[] { stock });
        }

        public override void Dispose() => world.Dispose();
    }

    private sealed class FacilityShopScenario : ScenarioBase
    {
        private readonly GameObject host;
        private readonly BuildingSO building;
        private readonly GameSessionState state;
        private readonly DailyFacilityShopRuntime runtime;

        internal FacilityShopScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "facility-shop")
        {
            state = new GameSessionState(100000, 5);
            var session = new TestGameSessionStateStore(state);
            var root = Outcomes.AggregateRootStore;
            var treasury = new TreasuryEconomyAggregateStateStore(root);
            building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset")
                ?? throw new InvalidOperationException(
                    "The authored facility-shop building fixture is missing.");
            host = new GameObject("Migrated Facility Shop E2E");
            runtime = host.AddComponent<DailyFacilityShopRuntime>();
            runtime.ConstructDailyFacilityShopRuntime(
                new SingleFacilityShopCatalog(building),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IMetaProgressionRuntimeReader>(),
                new DungeonStory.Foundation.GameEventBus(),
                new EditorGameMoneyAccount(state),
                autoProcurement: null,
                buildingCategoryCatalog:
                    CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRootStore: root,
                debugRules: DisabledDungeonDebugRuleQuery.Instance);
            runtime.ConstructPurchaseOutcomeTransactions(
                Probe,
                session,
                treasury);
            runtime.Refresh(5, raiseAlert: false);
            Require(
                runtime.CurrentDailyOffers.Count > 0,
                "The authored facility-shop fixture produced no daily offer.");
            InitialMoney = state.holdingMoney.Value;
        }

        internal int InitialMoney { get; }
        internal int Money => state.holdingMoney.Value;

        internal bool Purchase() => runtime.TryPurchaseDailyOffer(
            0,
            state,
            out _);

        public override void Dispose()
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    private sealed class OperatingDayScenario : ScenarioBase
    {
        private readonly GameObject host;
        private readonly GameSessionState state;
        private readonly OperatingDaySettlementRuntime runtime;

        internal OperatingDayScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "operating-day")
        {
            state = new GameSessionState(100, 1);
            var session = new TestGameSessionStateStore(state);
            var money = new EditorGameMoneyAccount(state);
            var root = Outcomes.AggregateRootStore;
            host = new GameObject("Migrated Operating Day E2E");
            runtime = host.AddComponent<OperatingDaySettlementRuntime>();
            runtime.Construct(
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IBuildingWorldQuery>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<ICharacterWorldQuery>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IFacilityShopCatalog>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                session,
                new DungeonStory.Foundation.GameEventBus(),
                new ZeroEmploymentRuntime(),
                money,
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IPaidFacilityContractRuntime>(),
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                root);
            runtime.ConstructOutcomeTransactions(
                Probe,
                session,
                new TreasuryEconomyAggregateStateStore(root));
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            InitialMoney = state.holdingMoney.Value;
        }

        internal int InitialMoney { get; }
        internal int Money => state.holdingMoney.Value;
        internal int ReportCount => runtime.ReportHistory.Count;
        internal int LatestReportDay => runtime.LatestReport?.day ?? 0;
        internal void Settle() => runtime.OnTriggerEvent(
            new OperatingDayEndedEvent(1));

        public override void Dispose()
        {
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    private sealed class StockSupplyScenario : ScenarioBase
    {
        private readonly GameSessionState state;
        private readonly StatefulWorldItemStackProxy physical;
        private readonly WarehouseFeatureCommandService commands;

        internal StockSupplyScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "stock-supply")
        {
            state = new GameSessionState(100, 3);
            var session = new TestGameSessionStateStore(state);
            IWorldItemStackRuntime physicalRuntime =
                StatefulWorldItemStackProxy.Create(out physical);
            var root = Outcomes.AggregateRootStore;
            commands = new WarehouseFeatureCommandService(
                new WarehouseCommandSessionContext(
                    session,
                    new EditorGameMoneyAccount(state),
                    new DungeonStory.Foundation.GameEventBus(),
                    DisabledDungeonDebugRuleQuery.Instance,
                    Probe,
                    session,
                    new TreasuryEconomyAggregateStateStore(root)),
                new WarehouseCommandWorldContext(
                    physicalRuntime,
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IWarehouseWorldQuery>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IBuildingWorldQuery>()),
                new WarehouseCommandPlanningContext(
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IResourceStockPolicyRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IRegionalSupplyContractRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IGrandProjectRuntime>()));
            Offer = new StockDeliveryOffer(
                StockCategory.General,
                "material:lumber",
                3,
                12,
                "원장 검증 납품");
            InitialMoney = state.holdingMoney.Value;
        }

        internal StockDeliveryOffer Offer { get; }
        internal int InitialMoney { get; }
        internal int Money => state.holdingMoney.Value;
        internal int Quantity => physical.Quantity;
        internal WarehouseFeatureCommandResult Purchase() =>
            commands.PurchaseDelivery(Offer);
    }

    private sealed class TestGameSessionStateStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;

        internal TestGameSessionStateStore(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class SingleFacilityShopCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO building;

        internal SingleFacilityShopCatalog(BuildingSO building) =>
            this.building = building
                ?? throw new ArgumentNullException(nameof(building));

        public IReadOnlyCollection<BuildingSO> Buildings => new[] { building };
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) =>
            building.id == buildingId ? building : null;
    }

    private sealed class ZeroEmploymentRuntime : IEmploymentContractRuntime
    {
        public IReadOnlyList<EmployeeWageState> WageStates =>
            Array.Empty<EmployeeWageState>();
        public IReadOnlyList<MercenaryContract> MercenaryContracts =>
            Array.Empty<MercenaryContract>();
        public int ForecastCost(int days) => 0;
        public int GetDailyCost(string characterId) => 0;
        public int QuoteMercenaryDailyCost(
            string characterId,
            int level,
            int rolePremium) => 0;
        public EmploymentDailySettlement SettleDay(int day) => new()
        {
            day = Math.Max(1, day)
        };
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
        public EmploymentContractSaveData Capture() => new();
    }

    private sealed class AlwaysCrimeRiskEvaluator : IFacilityCrimeRiskEvaluator
    {
        internal static readonly AlwaysCrimeRiskEvaluator Instance = new();
        public float CalculateShopliftingChance(FacilityCrimeRiskContext context) => 1f;
        public float CalculateOperationalRisk(FacilityCrimeRiskContext context) => 1f;
        public bool ShouldTriggerCrime(float chance, float roll) => true;
    }

    private sealed class ZeroRandomStream : IRandomStream
    {
        internal static readonly ZeroRandomStream Instance = new();
        public ulong State => 0UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => probability > 0f;
        public void Restore(ulong state)
        {
        }
    }

    public class StatefulWorldItemStackProxy : DispatchProxy
    {
        private readonly Dictionary<DungeonPhysicalItemSaveData, int> snapshots =
            new();

        internal int Quantity { get; private set; }

        internal static IWorldItemStackRuntime Create(
            out StatefulWorldItemStackProxy proxy)
        {
            IWorldItemStackRuntime runtime = DispatchProxy.Create<
                IWorldItemStackRuntime,
                StatefulWorldItemStackProxy>();
            proxy = (StatefulWorldItemStackProxy)(object)runtime;
            return runtime;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            switch (targetMethod.Name)
            {
                case nameof(IWorldItemStackRuntime.Capture):
                {
                    var snapshot = new DungeonPhysicalItemSaveData();
                    snapshots[snapshot] = Quantity;
                    return snapshot;
                }
                case nameof(IWorldItemStackRuntime.Restore):
                {
                    var snapshot = arguments[0] as DungeonPhysicalItemSaveData;
                    Quantity = snapshot != null
                        && snapshots.TryGetValue(snapshot, out int quantity)
                            ? quantity
                            : 0;
                    return null;
                }
                case nameof(IWorldItemStackRuntime.SpawnItemAtDropoff):
                {
                    int amount = Math.Max(0, (int)arguments[1]);
                    Quantity = checked(Quantity + amount);
                    arguments[3] = amount;
                    return true;
                }
            }

            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameters[index].IsOut && !parameterType.IsByRef)
                    continue;
                Type valueType = parameterType.GetElementType();
                arguments[index] = valueType != null && valueType.IsValueType
                    ? Activator.CreateInstance(valueType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            if (returnType.IsArray)
                return Array.CreateInstance(returnType.GetElementType(), 0);
            if (returnType.IsGenericType)
            {
                Type generic = returnType.GetGenericTypeDefinition();
                if (generic == typeof(IReadOnlyList<>)
                    || generic == typeof(IReadOnlyCollection<>)
                    || generic == typeof(IEnumerable<>))
                {
                    return Array.CreateInstance(
                        returnType.GetGenericArguments()[0],
                        0);
                }
            }
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool RejectionReachedCommit(
        MigratedProducerOutcomeProbeTransaction probe,
        MigratedProducerOutcomeKind kind) =>
        probe.ReservationCount(kind) > 0
        && probe.CommitAttemptCount(kind) > 0
        && probe.CommitCount(kind) == 0;

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "The production entry did not surface the injected "
            + typeof(TException).Name + ".");
    }
}
#endif
