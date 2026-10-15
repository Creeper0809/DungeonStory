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
    private IGameSessionStateProvider gameDataProvider;
    private OperatingDayFactJournal settlementFacts;
    private IGameSessionStateStore gameSessionState;
    private TreasuryEconomyAggregateStateStore treasuryState;
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
            snapshot.Ledger.FacilityDetails,
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
                state.FacilityDetails,
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
        this.gameDataProvider = gameDataProvider
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

    [Inject]
    public void ConstructSettlementFacts(
        OperatingDayFactJournal settlementFacts,
        IGameSessionStateStore gameSessionState,
        TreasuryEconomyAggregateStateStore treasuryState)
    {
        this.settlementFacts = settlementFacts
            ?? throw new ArgumentNullException(nameof(settlementFacts));
        this.gameSessionState = gameSessionState
            ?? throw new ArgumentNullException(nameof(gameSessionState));
        this.treasuryState = treasuryState
            ?? throw new ArgumentNullException(nameof(treasuryState));
    }

    public string LastObservationFailure => settlementFacts?.LastFailure
        ?? "operating-day-observer-not-configured";

    private void RequireRecoveredObservations()
    {
        if (settlementFacts == null)
            throw new InvalidOperationException("Operating-day fact journal is not configured.");
        if (!settlementFacts.RecoverPending(out string failure))
            throw new InvalidOperationException("Operating-day report observation pending: " + failure);
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        RequireRecoveredObservations();
        RequireDomain().BeginDay(eventType.day);
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        var domain = RequireDomain();
        // Retry only the frozen observation, even when this gameplay day is complete.
        RequireRecoveredObservations();
        if (domain.HasSettledDay(eventType.day)) return;
        if (gameSessionState == null || treasuryState == null || gameDataProvider == null
            || !gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.day == null)
            throw new InvalidOperationException(
                "Operating-day settlement requires session, treasury, and current day authorities.");

        int absoluteDay = Mathf.Max(0, gameData.day.Value);
        var proposal = new OperatingDaySettledFact(new OperatingDaySettledFactData
        {
            reportDay = Mathf.Max(1, eventType.day), absoluteDay = absoluteDay
        }, proposal: true);
        if (!settlementFacts.TryBegin(proposal, out var prepared, out string reserveFailure))
            throw new InvalidOperationException(
                "Operating-day report outcome reservation failed: " + reserveFailure);

        OperatingDaySettlementEffect<OperatingDayReport> effect;
        bool observationPending;
        using (prepared)
        {
            var domainBefore = domain.CaptureOwnerState();
            var sessionBefore = gameData.Capture();
            var treasuryBefore = treasuryState.Current.Copy();
            using var employmentEffects = employmentContracts.BeginSettlement()
                ?? throw new InvalidOperationException("Employment settlement scope is required.");
            bool gameplayCompleted = false;
            try
            {
                if (!domain.TryBeginSettlement(eventType.day, out var request)) return;
                OperatingDayEconomyApplication economy = ApplyOperatingCostPorts(request.Day, domain);
                OperatingDayCostTransition costs = domain.ResolveCostTransition(request, economy);
                request = domain.RefreshSettlementRequest(request);
                OperatingDayReport report = BuildReport(request, costs);
                var fact = new OperatingDaySettledFact(OperatingDaySettledFactData.From(report, absoluteDay));
                effect = domain.CompleteSettlement(request, report, costs);
                domain.FinishSettlement(request);
                gameplayCompleted = true;
                employmentEffects.Commit();
                // The owner is now committed. Observation retries cannot repeat effects.
                prepared.PublishApplied(fact);
                observationPending = prepared.ObservationPending;
            }
            catch
            {
                if (!gameplayCompleted)
                    RestoreSettlementOwners(domain, domainBefore, sessionBefore, treasuryBefore);
                throw;
            }
        }

        PublishPostCommit(() => gameEventBus.Publish(new OperatingDayReportEvent(effect.Report)),
            "operating-day-report-event");
        if (observationPending)
            throw new InvalidOperationException(
                "Operating-day report observation pending: " + settlementFacts.LastFailure);
    }

    private void RestoreSettlementOwners(
        OperatingDaySettlementDomain<OperatingDayReport, StockSupplyResult> domain,
        OperatingDaySettlementAggregateState<
            OperatingDayReport,
            StockSupplyResult> domainBefore,
        GameSessionSnapshot sessionBefore,
        TreasuryEconomyAggregateState treasuryBefore)
    {
        domain.RestoreOwnerState(domainBefore);
        gameSessionState.Restore(sessionBefore);
        treasuryState.Replace(treasuryBefore
            ?? throw new ArgumentNullException(nameof(treasuryBefore)));
    }

    private static void PublishPostCommit(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
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
        string facilityId = RequireFacilityId(eventType.facility);
        RequireDomain().RecordVisit(
            facilityId,
            GetFacilityName(eventType.facility),
            identity.SpeciesTag,
            mood);
    }

    public void OnTriggerEvent(FacilityRevenueEvent eventType)
    {
        string facilityId = RequireFacilityId(eventType.facility);
        RequireDomain().RecordRevenue(
            facilityId,
            GetFacilityName(eventType.facility),
            eventType.revenue);
    }

    public void OnTriggerEvent(FacilityStockConsumedEvent eventType)
    {
        string facilityId = RequireFacilityId(eventType.facility);
        RequireDomain().RecordStockConsumed(
            facilityId,
            GetFacilityName(eventType.facility),
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
            settlement.ConsecutiveShortfallDays,
            ledger.FacilityDetails,
            costs.TerminatedContractAmount,
            costs.EmploymentLines);
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

    private OperatingDayEconomyApplication ApplyOperatingCostPorts(
        int day,
        OperatingDaySettlementDomain<OperatingDayReport, StockSupplyResult>
            domain)
    {
        int openingBalance = moneyAccount.Balance;
        EmploymentDailySettlement employment =
            employmentContracts.SettleDay(day);
        IReadOnlyList<PaidFacilityContractState> contracts =
            paidFacilityContracts.Contracts
            ?? throw new InvalidOperationException(
                "Paid-facility contract query returned no collection.");
        PaidFacilityContractState[] dueContracts = contracts
            .Where(contract => contract != null
                && contract.active
                && contract.lastSettledDay < day)
            .OrderBy(contract => contract.contractId, StringComparer.Ordinal)
            .ToArray();
        int maintenanceCost = 0;
        foreach (PaidFacilityContractState contract in dueContracts)
        {
            int cost = paidFacilityContracts.GetDailyCost(
                contract.facilityPersistentId);
            maintenanceCost = checked(maintenanceCost + cost);
            domain.RecordFacilityCost(
                contract.facilityPersistentId,
                ResolveFacilityDisplayName(contract.facilityPersistentId),
                cost);
        }
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
            employment.unpaidMercenaryFees,
            moneyAccount.Balance,
            employment.lines.Select(ToOperatingDayEmploymentLine).ToArray());
    }

    private static OperatingDayEmploymentSettlementLine
        ToOperatingDayEmploymentLine(EmploymentSettlementLine source)
    {
        if (source == null)
        {
            throw new InvalidOperationException(
                "Employment settlement returned a null individual line.");
        }

        OperatingDayEmploymentContractKind contractKind = source.contractKind
            switch
            {
                EmploymentContractKind.Founder =>
                    OperatingDayEmploymentContractKind.Founder,
                EmploymentContractKind.Employee =>
                    OperatingDayEmploymentContractKind.Employee,
                EmploymentContractKind.Mercenary =>
                    OperatingDayEmploymentContractKind.Mercenary,
                _ => throw new InvalidOperationException(
                    "Employment settlement returned an unknown contract kind.")
            };
        return new OperatingDayEmploymentSettlementLine
        {
            day = source.day,
            characterId = source.characterId,
            contractKind = contractKind,
            due = source.due,
            paid = source.paid,
            unpaidDebtBefore = source.unpaidDebtBefore,
            unpaidDebtAfter = source.unpaidDebtAfter,
            moodPenaltyApplied = source.moodPenaltyApplied,
            contractEnded = source.contractEnded,
            departureAttempted = source.departureAttempted,
            departureStarted = source.departureStarted,
            departurePending = source.departurePending,
            departureFailureReason = source.departureFailureReason,
            financeSourceId = source.financeSourceId,
            financeTransactionId = source.financeTransactionId,
            financeSucceeded = source.financeSucceeded
        };
    }

    private string ResolveFacilityDisplayName(string facilityPersistentId)
    {
        IReadOnlyList<BuildableObject> buildings =
            RequireBuildingQuery().Buildings
            ?? throw new InvalidOperationException(
                "Building query returned no facility collection.");
        BuildableObject facility = buildings
            .FirstOrDefault(value => value != null
                && value.PersistentInstanceId.IsValid
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityPersistentId,
                    StringComparison.Ordinal));
        return facility == null
            ? facilityPersistentId
            : GetFacilityName(facility);
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

    private static string RequireFacilityId(BuildableObject facility)
    {
        if (facility == null)
        {
            throw new InvalidOperationException(
                "Operating-day facility event has no facility owner.");
        }
        return facility.RequirePersistentInstanceId().Value;
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
