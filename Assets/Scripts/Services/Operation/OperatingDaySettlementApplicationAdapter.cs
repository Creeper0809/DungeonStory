using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public sealed class OperatingDaySettlementRestoreCandidate
{
    internal OperatingDaySettlementRestoreCandidate(
        OperatingDaySettlementAggregateState<
            OperatingDayReport,
            StockSupplyResult> state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal OperatingDaySettlementAggregateState<
        OperatingDayReport,
        StockSupplyResult> State { get; }
}

public class OperatingDaySettlementApplicationAdapter : MonoBehaviour
{
    private sealed class BuildingReportData
    {
        public BuildingReportData(
            int maintenanceCost,
            int repairCost,
            IReadOnlyList<string> damagedFacilities,
            IReadOnlyList<string> stockShortageFacilities,
            IReadOnlyList<WarehouseStockSummary> warehouseStocks)
        {
            MaintenanceCost = maintenanceCost;
            RepairCost = repairCost;
            DamagedFacilities = damagedFacilities;
            StockShortageFacilities = stockShortageFacilities;
            WarehouseStocks = warehouseStocks;
        }

        public int MaintenanceCost { get; }
        public int RepairCost { get; }
        public IReadOnlyList<string> DamagedFacilities { get; }
        public IReadOnlyList<string> StockShortageFacilities { get; }
        public IReadOnlyList<WarehouseStockSummary> WarehouseStocks { get; }
    }

    private sealed class StaffReportData
    {
        public StaffReportData(
            StaffWorkSummary summary,
            IReadOnlyList<string> complaints)
        {
            Summary = summary;
            Complaints = complaints;
        }

        public StaffWorkSummary Summary { get; }
        public IReadOnlyList<string> Complaints { get; }
    }

    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private OperatingDaySettlementDomain<OperatingDayReport, StockSupplyResult>
        settlementDomain;
    private IBuildingWorldQuery buildingQuery;
    private ICharacterWorldQuery characterQuery;
    private IFacilityShopCatalog facilityShopCatalog;
    private IRunVariableRuntimeReader runVariableReader;
    private IGameEventBus gameEventBus;
    private IEmploymentContractRuntime employmentContracts;
    private IPaidFacilityContractRuntime paidFacilityContracts;
    private IGameMoneyAccount moneyAccount;
    private IStockCategoryDefinitionCatalog stockCategoryCatalog;
    private IBuildingCategoryDefinitionCatalog buildingCategoryCatalog;
    private IDisposable stockSupplySubscription;
    private IDisposable facilityVisitSubscription;
    private IDisposable facilityRevenueSubscription;
    private IDisposable facilityStockConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable facilityRestockSubscription;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable operatingDayEndedSubscription;
    private IDisposable eventAlertLoggedSubscription;

    public OperatingDayReport LatestReport => RequireDomain().LatestReport;
    public IReadOnlyList<OperatingDayReport> ReportHistory =>
        RequireDomain().ReportHistory;
    public int CurrentDay => RequireDomain().CurrentDay;
    public int CurrentRevenue => RequireDomain().CurrentRevenue;
    public int CurrentVisits => RequireDomain().CurrentVisits;
    public int CurrentRestockFailureCount =>
        RequireDomain().CurrentRestockFailureCount;
    public int CurrentConsumedStock => RequireDomain().CurrentConsumedStock;
    public int CurrentIncidentCount => RequireDomain().CurrentIncidentCount;
    public int CurrentEventCount => RequireDomain().CurrentEventCount;
    public float CurrentAverageSatisfaction =>
        RequireDomain().CurrentAverageSatisfaction;
    public int OutstandingDebt => RequireDomain().OutstandingDebt;
    public int ConsecutiveShortfallDays =>
        RequireDomain().ConsecutiveShortfallDays;
    public bool EmergencyFundingUsed => RequireDomain().EmergencyFundingUsed;
    public bool CanTakeEmergencyFunding => false;
    public OperatingCostForecast CurrentOperatingCostForecast =>
        BuildOperatingCostForecast();

    public OperatingDaySettlementPersistenceState CapturePersistentState()
    {
        OperatingDaySettlementStateSnapshot<
            OperatingDayReport,
            StockSupplyResult> snapshot = RequireDomain().Capture();
        return new OperatingDaySettlementPersistenceState(
            snapshot.CurrentDay,
            snapshot.Ledger.TotalRevenue,
            snapshot.Ledger.TotalVisits,
            snapshot.Ledger.RestockFailureCount,
            snapshot.Ledger.FacilityRevenue,
            snapshot.Ledger.SpeciesVisits,
            snapshot.Ledger.ConsumedStock.ToDictionary(
                pair => (StockCategory)pair.Key,
                pair => pair.Value),
            snapshot.Ledger.VisitorMoodSamples,
            snapshot.Ledger.StockSupplyResults,
            snapshot.Ledger.Incidents,
            snapshot.Ledger.EventLog,
            snapshot.ReportHistory,
            snapshot.OutstandingDebt,
            snapshot.ConsecutiveShortfallDays,
            snapshot.EmergencyFundingUsed);
    }

    public void RestorePersistentState(OperatingDaySettlementPersistenceState state)
    {
        PublishRestoreCandidate(PrepareRestoreCandidate(state));
    }

    public OperatingDaySettlementRestoreCandidate PrepareRestoreCandidate(
        OperatingDaySettlementPersistenceState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        OperatingDaySettlementStateSnapshot<
            OperatingDayReport,
            StockSupplyResult> snapshot = new(
                state.CurrentDay,
                state.FacilityRevenue,
                state.SpeciesVisits,
                state.ConsumedStock.ToDictionary(
                    pair => (int)pair.Key,
                    pair => pair.Value),
                state.VisitorMoodSamples,
                state.StockSupplyResults,
                state.Incidents,
                state.EventLog,
                state.ReportHistory,
                state.TotalRevenue,
                state.TotalVisits,
                state.RestockFailureCount,
                state.OutstandingDebt,
                state.ConsecutiveShortfallDays,
                state.EmergencyFundingUsed,
                state.ReportHistory.FirstOrDefault()?.day ?? 0);
        return new OperatingDaySettlementRestoreCandidate(
            RequireDomain().PrepareRestoreState(snapshot));
    }

    public void PublishRestoreCandidate(
        OperatingDaySettlementRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        RequireAggregateRoot().Replace(candidate.State);
    }

    [Inject]
    public void Construct(
        IBuildingWorldQuery buildingQuery,
        ICharacterWorldQuery characterQuery,
        IFacilityShopCatalog facilityShopCatalog,
        IRunVariableRuntimeReader runVariableReader,
        IGameSessionStateProvider gameDataProvider,
        IGameEventBus gameEventBus,
        IEmploymentContractRuntime employmentContracts,
        IGameMoneyAccount moneyAccount,
        IPaidFacilityContractRuntime paidFacilityContracts,
        IStockCategoryDefinitionCatalog stockCategoryCatalog,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.buildingQuery = buildingQuery
            ?? throw new ArgumentNullException(nameof(buildingQuery));
        this.characterQuery = characterQuery
            ?? throw new ArgumentNullException(nameof(characterQuery));
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        _ = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.employmentContracts = employmentContracts
            ?? throw new ArgumentNullException(nameof(employmentContracts));
        this.moneyAccount = moneyAccount
            ?? throw new ArgumentNullException(nameof(moneyAccount));
        this.paidFacilityContracts = paidFacilityContracts
            ?? throw new ArgumentNullException(nameof(paidFacilityContracts));
        this.stockCategoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
        this.buildingCategoryCatalog = buildingCategoryCatalog
            ?? throw new ArgumentNullException(nameof(buildingCategoryCatalog));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        settlementDomain = OperatingDaySettlementDomain<
            OperatingDayReport,
            StockSupplyResult>.Attach(aggregateRootStore);
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        RequireDomain().BeginDay(eventType.day);
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        OperatingDaySettlementDomain<OperatingDayReport, StockSupplyResult>
            domain = RequireDomain();
        if (!domain.TryBeginSettlement(
                eventType.day,
                out OperatingDaySettlementRequest<StockSupplyResult> request))
        {
            return;
        }

        OperatingDayEconomyApplication economy = ApplyOperatingCostPorts(
            request.Day);
        OperatingDayCostTransition costs = domain.ResolveCostTransition(
            request,
            economy);
        if (costs.HasWageShortfall)
        {
            gameEventBus.RaiseAlert(
                "\uC784\uAE08 \uCCB4\uBD88",
                $"\uBBF8\uC9C0\uAE09 \uC784\uAE08 {costs.CarriedDebt}\uC774 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uC9C1\uC6D0 \uBD88\uB9CC\uC774 \uC99D\uAC00\uD569\uB2C8\uB2E4.",
                EventAlertImportance.High,
                "\uACBD\uC81C");
        }
        request = domain.RefreshSettlementRequest(request);
        OperatingDayReport report = BuildReport(request, costs);
        OperatingDaySettlementEffect<OperatingDayReport> effect =
            domain.CompleteSettlement(request, report, costs);
        gameEventBus.Publish(new OperatingDayReportEvent(effect.Report));
        domain.FinishSettlement(request);
    }

    public bool TryTakeEmergencyFunding(out string message)
    {
        message = "\uC790\uB3D9 \uAE34\uAE09 \uC9C0\uC6D0\uAE08\uC740 \uB354 \uC774\uC0C1 \uC81C\uACF5\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
        return false;
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        CharacterActor actor = eventType.visitorActor;
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        if (actor == null
            || identity == null
            || identity.CharacterType != CharacterType.Customer)
        {
            return;
        }

        float? mood = null;
        CharacterStats stats = actor.Stats;
        if (stats != null
            && stats.Stats.TryGetValue(CharacterCondition.MOOD, out float value))
        {
            mood = value;
        }
        RequireDomain().RecordVisit(identity.SpeciesTag, mood);
    }

    public void OnTriggerEvent(FacilityRevenueEvent eventType)
    {
        RequireDomain().RecordRevenue(
            GetFacilityName(eventType.facility),
            eventType.revenue);
    }

    public void OnTriggerEvent(FacilityStockConsumedEvent eventType)
    {
        RequireDomain().RecordStockConsumed(
            (int)eventType.category,
            eventType.amount);
    }

    public void OnTriggerEvent(FacilityCrimeEvent eventType)
    {
        string detail = string.IsNullOrWhiteSpace(eventType.detail)
            ? $"{eventType.kind}: loss {Mathf.Max(0, eventType.lossValue)}"
            : eventType.detail;
        RequireDomain().RecordIncident(detail);
    }

    public void OnTriggerEvent(FacilityRestockEvent eventType)
    {
        RequireDomain().RecordRestockResult(
            eventType.requestedAmount,
            eventType.restockedAmount);
    }

    public void OnTriggerEvent(StockSupplyEvent eventType)
    {
        RequireDomain().RecordStockSupply(eventType.result);
    }

    public void OnTriggerEvent(EventAlertLoggedEvent eventType)
    {
        if (eventType.record != null)
        {
            RequireDomain().RecordEventLog(eventType.record.ButtonText);
        }
    }

    protected virtual void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    protected virtual void OnDisable()
    {
        DisposeSubscription(ref stockSupplySubscription);
        DisposeSubscription(ref facilityVisitSubscription);
        DisposeSubscription(ref facilityRevenueSubscription);
        DisposeSubscription(ref facilityStockConsumedSubscription);
        DisposeSubscription(ref facilityCrimeSubscription);
        DisposeSubscription(ref facilityRestockSubscription);
        DisposeSubscription(ref operatingDayStartedSubscription);
        DisposeSubscription(ref operatingDayEndedSubscription);
        DisposeSubscription(ref eventAlertLoggedSubscription);
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        stockSupplySubscription ??=
            gameEventBus.Subscribe<StockSupplyEvent>(OnTriggerEvent);
        facilityVisitSubscription ??=
            gameEventBus.Subscribe<FacilityVisitEvent>(OnTriggerEvent);
        facilityRevenueSubscription ??=
            gameEventBus.Subscribe<FacilityRevenueEvent>(OnTriggerEvent);
        facilityStockConsumedSubscription ??=
            gameEventBus.Subscribe<FacilityStockConsumedEvent>(OnTriggerEvent);
        facilityCrimeSubscription ??=
            gameEventBus.Subscribe<FacilityCrimeEvent>(OnTriggerEvent);
        facilityRestockSubscription ??=
            gameEventBus.Subscribe<FacilityRestockEvent>(OnTriggerEvent);
        operatingDayStartedSubscription ??=
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
        eventAlertLoggedSubscription ??=
            gameEventBus.Subscribe<EventAlertLoggedEvent>(OnTriggerEvent);
    }

    private OperatingDayReport BuildReport(
        OperatingDaySettlementRequest<StockSupplyResult> request,
        OperatingDayCostTransition costs)
    {
        OperatingDayLedgerSnapshot<StockSupplyResult> ledger = request.Ledger;
        BuildingReportData buildings = BuildBuildingSnapshot(
            RequireBuildingQuery().Buildings);
        StaffReportData staff = BuildStaffSnapshot(
            RequireCharacterQuery().Characters);
        OperatingCostForecast forecast = new(
            costs.OpeningBalance,
            costs.MaintenanceCost,
            costs.PayrollCost,
            costs.PreviousDebt);
        OperatingCostSettlement settlement = new(
            forecast,
            costs.PaidAmount,
            costs.ClosingBalance,
            costs.CarriedDebt,
            costs.ConsecutiveShortfallDays);
        return OperatingDayReport.Create(
            request.Day,
            ledger.TotalRevenue,
            ledger.TotalVisits,
            ledger.AverageSatisfaction,
            buildings.RepairCost,
            ledger.RestockFailureCount,
            ledger.FacilityRevenue
                .Select(pair => new FacilityRevenueSummary(pair.Key, pair.Value))
                .OrderByDescending(item => item.revenue)
                .ToList(),
            ledger.SpeciesVisits
                .Select(pair => new SpeciesVisitSummary(pair.Key, pair.Value))
                .OrderByDescending(item => item.visitCount)
                .ToList(),
            ledger.Incidents,
            buildings.DamagedFacilities,
            buildings.StockShortageFacilities,
            staff.Complaints,
            ledger.EventLog,
            null,
            staff.Summary,
            buildings.WarehouseStocks,
            ledger.ConsumedStock
                .Select(pair => new StockConsumptionSummary(
                    (StockCategory)pair.Key,
                    pair.Value))
                .OrderByDescending(item => item.amount)
                .ToList(),
            ledger.StockSupplyResults,
            StockSupplyService.CreateDailyDeliveryOffers(
                    request.Day + 1,
                    RequireRunVariableReader(),
                    stockCategoryCatalog)
                .ToList(),
            FacilityShopService.CreateDailyOffers(
                    request.Day + 1,
                    RequireFacilityShopCatalog(),
                    RequireRunVariableReader(),
                    buildingCategoryCatalog)
                .Select(offer => offer.ToSnapshot())
                .ToList(),
            settlement.Forecast.MaintenanceCost,
            settlement.Forecast.PayrollCost,
            settlement.Forecast.OutstandingDebt,
            settlement.PaidAmount,
            settlement.CarriedDebt,
            settlement.ClosingBalance,
            settlement.ConsecutiveShortfallDays);
    }

    private static BuildingReportData BuildBuildingSnapshot(
        IEnumerable<BuildableObject> buildings)
    {
        int maintenanceCost = 0;
        int repairCost = 0;
        List<string> damagedFacilities = new();
        List<string> stockShortageFacilities = new();
        List<WarehouseStockSummary> warehouseStocks = new();
        foreach (BuildableObject building in buildings.Where(value =>
                     value != null && !value.isDestroy))
        {
            BuildingSO data = building.BuildingData;
            if (data != null
                && !data.IsStructuralWall
                && !data.IsDoor
                && !data.IsGridMovement)
            {
                maintenanceCost += data.GetMaintenanceCost();
            }
            if (building.IsDamaged)
            {
                damagedFacilities.Add(GetFacilityName(building));
                repairCost += data.GetMaintenanceCost();
            }
            if (building is IRestockableFacility restockable
                && building.Facility != null
                && restockable.CurrentStock
                    <= building.GetRestockRequestThreshold())
            {
                stockShortageFacilities.Add(GetFacilityName(building));
            }
            if (building is IWarehouseFacility
                {
                    HasWarehouseInventory: not false
                } warehouse)
            {
                warehouseStocks.Add(new WarehouseStockSummary(
                    GetFacilityName(building),
                    warehouse.Inventory.TotalStock,
                    warehouse.Inventory.StoredMassGrams,
                    warehouse.Inventory.MaxMassGrams,
                    warehouse.Inventory.EnumerateStock()
                        .Select(pair => new StockConsumptionSummary(
                            pair.Key,
                            pair.Value))
                        .ToList()));
            }
        }
        return new BuildingReportData(
            maintenanceCost,
            repairCost,
            Array.AsReadOnly(damagedFacilities.ToArray()),
            Array.AsReadOnly(stockShortageFacilities.ToArray()),
            Array.AsReadOnly(warehouseStocks.ToArray()));
    }

    private static StaffReportData BuildStaffSnapshot(
        IEnumerable<CharacterActor> characters)
    {
        List<CharacterActor> staff = characters.Where(IsStaffCharacter).ToList();
        if (staff.Count == 0)
        {
            return new StaffReportData(
                new StaffWorkSummary(0, 0, 0, 0f, 0f),
                Array.Empty<string>());
        }

        StaffWorkSummary summary = new(
            staff.Count,
            staff.Count(actor =>
                CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                && work.isWorking),
            staff.Count(actor =>
                CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                && work.IsOffDuty),
            staff.Average(actor => GetStat(actor, CharacterCondition.SLEEP)),
            staff.Average(actor => GetStat(actor, CharacterCondition.MOOD)));
        List<string> complaints = staff
            .Where(actor => GetStat(actor, CharacterCondition.MOOD) <= 25f)
            .Select(actor => actor.name + ": \uC0AC\uAE30\uAC00 \uB9E4\uC6B0 \uB0AE\uC74C")
            .ToList();
        return new StaffReportData(summary, complaints);
    }

    private OperatingCostForecast BuildOperatingCostForecast()
    {
        return new OperatingCostForecast(
            moneyAccount.Balance,
            paidFacilityContracts.ForecastCost(1),
            employmentContracts.ForecastCost(1),
            0);
    }

    private OperatingDayEconomyApplication ApplyOperatingCostPorts(int day)
    {
        int openingBalance = moneyAccount.Balance;
        EmploymentDailySettlement employment =
            employmentContracts.SettleDay(day);
        int maintenanceCost = paidFacilityContracts.ForecastCost(1);
        int maintenancePaid = paidFacilityContracts.SettleDay(day);
        return new OperatingDayEconomyApplication(
            openingBalance,
            maintenanceCost,
            maintenancePaid,
            employment.employeeWagesDue,
            employment.mercenaryFeesDue,
            employment.employeeWagesPaid,
            employment.mercenaryFeesPaid,
            employment.unpaidEmployeeWages,
            moneyAccount.Balance);
    }

    private DungeonRuntimeAggregateRootStore RequireAggregateRoot()
    {
        return aggregateRootStore ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime has not been constructed with its Aggregate root.");
    }

    private OperatingDaySettlementDomain<OperatingDayReport, StockSupplyResult>
        RequireDomain()
    {
        return settlementDomain ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime has not been constructed with its Operation domain.");
    }

    private static void DisposeSubscription(ref IDisposable subscription)
    {
        subscription?.Dispose();
        subscription = null;
    }

    private static string GetFacilityName(BuildableObject facility)
    {
        if (facility == null)
        {
            return "Unknown";
        }
        if (facility.BuildingData != null
            && !string.IsNullOrWhiteSpace(facility.BuildingData.objectName))
        {
            return facility.BuildingData.objectName;
        }
        return facility.name;
    }

    private static float GetStat(
        CharacterActor actor,
        CharacterCondition condition)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        return stats != null
            && stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 0f;
    }

    private static bool IsStaffCharacter(CharacterActor actor)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        return identity != null
            && identity.CharacterType == CharacterType.NPC
            && CharacterWorkRoleUtility.TryGetWork(actor, out _);
    }

    private IBuildingWorldQuery RequireBuildingQuery()
    {
        return buildingQuery ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime requires IBuildingWorldQuery injection.");
    }

    private ICharacterWorldQuery RequireCharacterQuery()
    {
        return characterQuery ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime requires ICharacterWorldQuery injection.");
    }

    private IFacilityShopCatalog RequireFacilityShopCatalog()
    {
        return facilityShopCatalog ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime requires IFacilityShopCatalog injection.");
    }

    private IRunVariableRuntimeReader RequireRunVariableReader()
    {
        return runVariableReader ?? throw new InvalidOperationException(
            "OperatingDaySettlementRuntime requires IRunVariableRuntimeReader injection.");
    }
}
