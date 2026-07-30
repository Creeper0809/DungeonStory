using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class FacilityRevenueSummary
{
    public FacilityRevenueSummary(string facilityName, int revenue)
    {
        this.facilityName = facilityName ?? string.Empty;
        this.revenue = Mathf.Max(0, revenue);
    }

    public string facilityName { get; }
    public int revenue { get; }
}

[Serializable]
public sealed class SpeciesVisitSummary
{
    public SpeciesVisitSummary(string speciesTag, int visitCount)
    {
        this.speciesTag = speciesTag ?? string.Empty;
        this.visitCount = Mathf.Max(0, visitCount);
    }

    public string speciesTag { get; }
    public int visitCount { get; }
}

[Serializable]
public sealed class StockConsumptionSummary
{
    public StockConsumptionSummary(StockCategory category, int amount)
    {
        this.category = category;
        this.amount = Mathf.Max(0, amount);
    }

    public StockCategory category { get; }
    public int amount { get; }
}

[Serializable]
public sealed class WarehouseStockSummary
{
    public WarehouseStockSummary(
        string warehouseName,
        int totalStock,
        int maxCapacity,
        IReadOnlyList<StockConsumptionSummary> stocks)
    {
        this.warehouseName = warehouseName ?? string.Empty;
        this.totalStock = Mathf.Max(0, totalStock);
        this.maxCapacity = Mathf.Max(0, maxCapacity);
        this.stocks = EventPayloadSnapshot.Copy(stocks);
    }

    public string warehouseName { get; }
    public int totalStock { get; }
    public int maxCapacity { get; }
    public IReadOnlyList<StockConsumptionSummary> stocks { get; }

    public string ToSummaryText()
    {
        string stockText = stocks == null || stocks.Count == 0
            ? "비어 있음"
            : string.Join(", ", stocks.Select((item) =>
                $"{StockCategoryCatalog.GetDisplayName(item.category)} {item.amount}"));
        return $"{warehouseName}: {totalStock}/{maxCapacity} ({stockText})";
    }
}

[Serializable]
public sealed class StaffWorkSummary
{
    public StaffWorkSummary(
        int staffCount,
        int workingCount,
        int offDutyCount,
        float averageSleep,
        float averageMood)
    {
        this.staffCount = Mathf.Max(0, staffCount);
        this.workingCount = Mathf.Max(0, workingCount);
        this.offDutyCount = Mathf.Max(0, offDutyCount);
        this.averageSleep = averageSleep;
        this.averageMood = averageMood;
    }

    public int staffCount { get; }
    public int workingCount { get; }
    public int offDutyCount { get; }
    public float averageSleep { get; }
    public float averageMood { get; }
}

[Serializable]
public sealed class OperatingDayReport
{
    private OperatingDayReport(
        int day,
        int totalRevenue,
        int totalVisits,
        float averageSatisfaction,
        int repairCost,
        int restockFailureCount,
        IReadOnlyList<FacilityRevenueSummary> facilityRevenues,
        IReadOnlyList<SpeciesVisitSummary> speciesVisits,
        IReadOnlyList<string> incidents,
        IReadOnlyList<string> damagedFacilities,
        IReadOnlyList<string> stockShortageFacilities,
        IReadOnlyList<string> staffComplaintEvents,
        IReadOnlyList<string> eventLog,
        IReadOnlyList<string> unlockedCodexInfo,
        StaffWorkSummary staffSummary,
        IReadOnlyList<WarehouseStockSummary> warehouseStocks,
        IReadOnlyList<StockConsumptionSummary> stockConsumed,
        IReadOnlyList<StockSupplyResult> stockSupplyResults,
        IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers,
        IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers,
        int maintenanceCost,
        int payrollCost,
        int previousDebt,
        int paidOperatingCost,
        int unpaidOperatingCost,
        int closingBalance,
        int consecutiveShortfallDays)
    {
        this.day = Mathf.Max(1, day);
        this.totalRevenue = Mathf.Max(0, totalRevenue);
        this.totalVisits = Mathf.Max(0, totalVisits);
        this.averageSatisfaction = averageSatisfaction;
        this.repairCost = Mathf.Max(0, repairCost);
        this.restockFailureCount = Mathf.Max(0, restockFailureCount);
        this.facilityRevenues = EventPayloadSnapshot.Copy(facilityRevenues);
        this.speciesVisits = EventPayloadSnapshot.Copy(speciesVisits);
        this.incidents = EventPayloadSnapshot.Copy(incidents);
        this.damagedFacilities = EventPayloadSnapshot.Copy(damagedFacilities);
        this.stockShortageFacilities = EventPayloadSnapshot.Copy(stockShortageFacilities);
        this.staffComplaintEvents = EventPayloadSnapshot.Copy(staffComplaintEvents);
        this.eventLog = EventPayloadSnapshot.Copy(eventLog);
        this.unlockedCodexInfo = EventPayloadSnapshot.Copy(unlockedCodexInfo);
        this.staffSummary = staffSummary ?? new StaffWorkSummary(0, 0, 0, 0f, 0f);
        this.warehouseStocks = EventPayloadSnapshot.Copy(warehouseStocks);
        this.stockConsumed = EventPayloadSnapshot.Copy(stockConsumed);
        this.stockSupplyResults = EventPayloadSnapshot.Copy(stockSupplyResults);
        this.refreshedDailyShopOffers = EventPayloadSnapshot.Copy(refreshedDailyShopOffers);
        this.refreshedFacilityShopOffers = EventPayloadSnapshot.Copy(refreshedFacilityShopOffers);
        this.maintenanceCost = Mathf.Max(0, maintenanceCost);
        this.payrollCost = Mathf.Max(0, payrollCost);
        this.previousDebt = Mathf.Max(0, previousDebt);
        this.paidOperatingCost = Mathf.Max(0, paidOperatingCost);
        this.unpaidOperatingCost = Mathf.Max(0, unpaidOperatingCost);
        this.closingBalance = Mathf.Max(0, closingBalance);
        this.consecutiveShortfallDays = Mathf.Max(0, consecutiveShortfallDays);
    }

    public int day { get; }
    public int totalRevenue { get; }
    public int totalVisits { get; }
    public float averageSatisfaction { get; }
    public int repairCost { get; }
    public int restockFailureCount { get; }
    public IReadOnlyList<FacilityRevenueSummary> facilityRevenues { get; }
    public IReadOnlyList<SpeciesVisitSummary> speciesVisits { get; }
    public IReadOnlyList<string> incidents { get; }
    public IReadOnlyList<string> damagedFacilities { get; }
    public IReadOnlyList<string> stockShortageFacilities { get; }
    public IReadOnlyList<string> staffComplaintEvents { get; }
    public IReadOnlyList<string> eventLog { get; }
    public IReadOnlyList<string> unlockedCodexInfo { get; }
    public StaffWorkSummary staffSummary { get; }
    public IReadOnlyList<WarehouseStockSummary> warehouseStocks { get; }
    public IReadOnlyList<StockConsumptionSummary> stockConsumed { get; }
    public IReadOnlyList<StockSupplyResult> stockSupplyResults { get; }
    public IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers { get; }
    public IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers { get; }
    public int maintenanceCost { get; }
    public int payrollCost { get; }
    public int previousDebt { get; }
    public int totalOperatingCost => maintenanceCost + payrollCost + previousDebt;
    public int paidOperatingCost { get; }
    public int unpaidOperatingCost { get; }
    public int closingBalance { get; }
    public int consecutiveShortfallDays { get; }

    public static OperatingDayReport Create(
        int day,
        int totalRevenue = 0,
        int totalVisits = 0,
        float averageSatisfaction = 0f,
        int repairCost = 0,
        int restockFailureCount = 0,
        IReadOnlyList<FacilityRevenueSummary> facilityRevenues = null,
        IReadOnlyList<SpeciesVisitSummary> speciesVisits = null,
        IReadOnlyList<string> incidents = null,
        IReadOnlyList<string> damagedFacilities = null,
        IReadOnlyList<string> stockShortageFacilities = null,
        IReadOnlyList<string> staffComplaintEvents = null,
        IReadOnlyList<string> eventLog = null,
        IReadOnlyList<string> unlockedCodexInfo = null,
        StaffWorkSummary staffSummary = null,
        IReadOnlyList<WarehouseStockSummary> warehouseStocks = null,
        IReadOnlyList<StockConsumptionSummary> stockConsumed = null,
        IReadOnlyList<StockSupplyResult> stockSupplyResults = null,
        IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers = null,
        IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers = null,
        int maintenanceCost = 0,
        int payrollCost = 0,
        int previousDebt = 0,
        int paidOperatingCost = 0,
        int unpaidOperatingCost = 0,
        int closingBalance = 0,
        int consecutiveShortfallDays = 0)
    {
        return new OperatingDayReport(
            day,
            totalRevenue,
            totalVisits,
            averageSatisfaction,
            repairCost,
            restockFailureCount,
            facilityRevenues,
            speciesVisits,
            incidents,
            damagedFacilities,
            stockShortageFacilities,
            staffComplaintEvents,
            eventLog,
            unlockedCodexInfo,
            staffSummary,
            warehouseStocks,
            stockConsumed,
            stockSupplyResults,
            refreshedDailyShopOffers,
            refreshedFacilityShopOffers,
            maintenanceCost,
            payrollCost,
            previousDebt,
            paidOperatingCost,
            unpaidOperatingCost,
            closingBalance,
            consecutiveShortfallDays);
    }

    public string ToDetailText()
    {
        List<string> lines = new List<string>
        {
            $"Day {day} 운영 정산",
            string.Empty,
            $"총 매출: {totalRevenue}",
            $"방문 손님 수: {totalVisits}",
            $"평균 만족도: {averageSatisfaction:0.#}",
            $"시설 유지비: {maintenanceCost}",
            $"직원 급여: {payrollCost}",
            $"이전 미납금: {previousDebt}",
            $"운영비 납부: {paidOperatingCost}/{totalOperatingCost}",
            $"새 미납금: {unpaidOperatingCost}",
            $"마감 자금: {closingBalance}",
            $"수리 비용 예상: {repairCost}",
            $"보충 실패 횟수: {restockFailureCount}",
            string.Empty,
            FormatList("시설별 매출", facilityRevenues.Select((item) => $"{item.facilityName}: {item.revenue}")),
            FormatList("종족별 방문", speciesVisits.Select((item) => $"{TextOrDefault(item.speciesTag, "Unknown")}: {item.visitCount}")),
            FormatList("소비된 재고", stockConsumed.Select((item) => $"{GetStockCategoryName(item.category)}: {item.amount}")),
            FormatList("창고 재고", warehouseStocks.Select((item) => item.ToSummaryText())),
            FormatList("재고 부족 시설", stockShortageFacilities),
            FormatList("파손 시설", damagedFacilities),
            FormatList("발생한 사고", incidents),
            FormatList("이벤트 로그", eventLog),
            FormatList("직원 불만 사건", staffComplaintEvents),
            $"직원 요약: 총 {staffSummary.staffCount}, 근무 {staffSummary.workingCount}, 비번 {staffSummary.offDutyCount}, 평균 피로 {staffSummary.averageSleep:0.#}, 평균 기분 {staffSummary.averageMood:0.#}",
            FormatList("재고 수급 결과", stockSupplyResults.Select((result) => result.ToSummaryText())),
            FormatList("상점 판매 목록 갱신", refreshedDailyShopOffers.Select((offer) => $"{GetStockCategoryName(offer.category)} {offer.amount}개 / 비용 {offer.cost}")),
            FormatList("시설 상점 갱신", refreshedFacilityShopOffers.Select((offer) => offer.ToSummaryText())),
            FormatList("신규 도감 정보", unlockedCodexInfo)
        };

        return string.Join("\n", lines.Where((line) => line != null));
    }

    private static string FormatList(string title, IEnumerable<string> rows)
    {
        List<string> validRows = rows?
            .Where((row) => !string.IsNullOrWhiteSpace(row))
            .ToList()
            ?? new List<string>();

        if (validRows.Count == 0)
        {
            return $"{title}: 없음";
        }

        return $"{title}:\n- {string.Join("\n- ", validRows)}";
    }

    private static string TextOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string GetStockCategoryName(StockCategory category)
    {
        return StockCategoryCatalog.GetDisplayName(category);
    }

}

public struct OperatingDayStartedEvent
{
    public int day;

    public OperatingDayStartedEvent(int day)
    {
        this.day = day;
    }

}

public struct OperatingDayEndedEvent
{
    public int day;

    public OperatingDayEndedEvent(int day)
    {
        this.day = day;
    }

}

public readonly struct OperatingDayReportEvent
{
    public OperatingDayReport report { get; }

    public OperatingDayReportEvent(OperatingDayReport report)
    {
        this.report = report;
    }

}

public struct FacilityVisitEvent
{
    public CharacterActor visitorActor;
    public BuildableObject facility;

    public FacilityVisitEvent(CharacterActor visitor, BuildableObject facility)
    {
        visitorActor = visitor;
        this.facility = facility;
    }

}

public struct FacilityRevenueEvent
{
    public CharacterActor customerActor;
    public BuildableObject facility;
    public int revenue;

    public FacilityRevenueEvent(CharacterActor customer, BuildableObject facility, int revenue)
    {
        customerActor = customer;
        this.facility = facility;
        this.revenue = revenue;
    }

}

public struct FacilityStockConsumedEvent
{
    public CharacterActor consumerActor;
    public BuildableObject facility;
    public StockCategory category;
    public int amount;

    public FacilityStockConsumedEvent(CharacterActor consumer, BuildableObject facility, StockCategory category, int amount)
    {
        consumerActor = consumer;
        this.facility = facility;
        this.category = category;
        this.amount = amount;
    }

}

public enum FacilityCrimeKind
{
    Shoplifting
}

public struct FacilityCrimeEvent
{
    public CharacterActor actor;
    public BuildableObject facility;
    public FacilityCrimeKind kind;
    public string detail;
    public int lossValue;

    public FacilityCrimeEvent(
        CharacterActor actor,
        BuildableObject facility,
        FacilityCrimeKind kind,
        string detail,
        int lossValue)
    {
        this.actor = actor;
        this.facility = facility;
        this.kind = kind;
        this.detail = detail ?? string.Empty;
        this.lossValue = Mathf.Max(0, lossValue);
    }

}

public struct FacilityRestockEvent
{
    public BuildableObject facility;
    public int requestedAmount;
    public int restockedAmount;
    public string message;

    public FacilityRestockEvent(BuildableObject facility, int requestedAmount, int restockedAmount, string message)
    {
        this.facility = facility;
        this.requestedAmount = requestedAmount;
        this.restockedAmount = restockedAmount;
        this.message = message;
    }

}

public class OperatingDaySettlementRuntime : MonoBehaviour
{
    private const int MaxReportHistory = 20;

    [SerializeField] private DungeonEconomySettings economySettings = new DungeonEconomySettings();

    private readonly Dictionary<string, int> facilityRevenue = new Dictionary<string, int>();
    private readonly Dictionary<string, int> speciesVisits = new Dictionary<string, int>();
    private readonly Dictionary<StockCategory, int> consumedStock = new Dictionary<StockCategory, int>();
    private readonly List<float> visitorMoodSamples = new List<float>();
    private readonly List<StockSupplyResult> stockSupplyResults = new List<StockSupplyResult>();
    private readonly List<string> incidents = new List<string>();
    private readonly List<string> eventLog = new List<string>();
    private int totalRevenue;
    private int totalVisits;
    private int restockFailureCount;
    private int currentDay = 1;
    private int outstandingDebt;
    private int consecutiveShortfallDays;
    private bool emergencyFundingUsed;
    private OperatingDayReport latestReport;
    private readonly List<OperatingDayReport> reportHistory = new List<OperatingDayReport>();
    private IReadOnlyList<OperatingDayReport> reportHistoryView;
    private IBuildingWorldQuery buildingQuery;
    private ICharacterWorldQuery characterQuery;
    private IFacilityShopCatalog facilityShopCatalog;
    private IRunVariableRuntimeReader runVariableReader;
    private IGameDataProvider gameDataProvider;
    private IGameEventBus gameEventBus;
    private IEmploymentContractRuntime employmentContracts;
    private IPaidFacilityContractRuntime paidFacilityContracts;
    private IGameMoneyRuntime moneyRuntime;
    private IDisposable stockSupplySubscription;
    private IDisposable facilityVisitSubscription;
    private IDisposable facilityRevenueSubscription;
    private IDisposable facilityStockConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable facilityRestockSubscription;
    private IDisposable operatingDayStartedSubscription;
    private IDisposable operatingDayEndedSubscription;
    private IDisposable eventAlertLoggedSubscription;

    public OperatingDayReport LatestReport => latestReport;
    public IReadOnlyList<OperatingDayReport> ReportHistory
    {
        get
        {
            if (reportHistoryView == null)
            {
                reportHistoryView = reportHistory.AsReadOnly();
            }

            return reportHistoryView;
        }
    }
    public int CurrentDay => currentDay;
    public int CurrentRevenue => totalRevenue;
    public int CurrentVisits => totalVisits;
    public int CurrentRestockFailureCount => restockFailureCount;
    public int CurrentConsumedStock => consumedStock.Values.Sum();
    public int CurrentIncidentCount => incidents.Count;
    public int CurrentEventCount => eventLog.Count;
    public float CurrentAverageSatisfaction => visitorMoodSamples.Count > 0
        ? visitorMoodSamples.Average()
        : 0f;
    public int OutstandingDebt => outstandingDebt;
    public int ConsecutiveShortfallDays => consecutiveShortfallDays;
    public bool EmergencyFundingUsed => emergencyFundingUsed;
    public bool CanTakeEmergencyFunding => false;
    public OperatingCostForecast CurrentOperatingCostForecast => BuildOperatingCostForecast();

    public OperatingDaySettlementPersistenceState CapturePersistentState()
    {
        return new OperatingDaySettlementPersistenceState(
            currentDay,
            totalRevenue,
            totalVisits,
            restockFailureCount,
            facilityRevenue,
            speciesVisits,
            consumedStock,
            visitorMoodSamples,
            stockSupplyResults,
            incidents,
            eventLog,
            reportHistory,
            outstandingDebt,
            consecutiveShortfallDays,
            emergencyFundingUsed);
    }

    public void RestorePersistentState(OperatingDaySettlementPersistenceState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ResetLedger();
        currentDay = Mathf.Max(1, state.CurrentDay);
        totalRevenue = Mathf.Max(0, state.TotalRevenue);
        totalVisits = Mathf.Max(0, state.TotalVisits);
        restockFailureCount = Mathf.Max(0, state.RestockFailureCount);
        foreach (KeyValuePair<string, int> pair in state.FacilityRevenue)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            {
                facilityRevenue[pair.Key] = pair.Value;
            }
        }

        foreach (KeyValuePair<string, int> pair in state.SpeciesVisits)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            {
                speciesVisits[pair.Key] = pair.Value;
            }
        }

        foreach (KeyValuePair<StockCategory, int> pair in state.ConsumedStock)
        {
            if (pair.Value > 0)
            {
                consumedStock[pair.Key] = pair.Value;
            }
        }

        visitorMoodSamples.AddRange(state.VisitorMoodSamples.Select(value => Mathf.Clamp(value, 0f, 100f)));
        stockSupplyResults.AddRange(state.StockSupplyResults);
        incidents.AddRange(state.Incidents.Where(value => !string.IsNullOrWhiteSpace(value)));
        eventLog.AddRange(state.EventLog.Where(value => !string.IsNullOrWhiteSpace(value)));
        reportHistory.Clear();
        reportHistory.AddRange(state.ReportHistory.Take(MaxReportHistory));
        latestReport = reportHistory.FirstOrDefault();
        outstandingDebt = Mathf.Max(0, state.OutstandingDebt);
        consecutiveShortfallDays = Mathf.Max(0, state.ConsecutiveShortfallDays);
        emergencyFundingUsed = state.EmergencyFundingUsed;
    }

    [Inject]
    public void Construct(
        IBuildingWorldQuery buildingQuery,
        ICharacterWorldQuery characterQuery,
        IFacilityShopCatalog facilityShopCatalog,
        IRunVariableRuntimeReader runVariableReader,
        IGameDataProvider gameDataProvider,
        IGameEventBus gameEventBus,
        IEmploymentContractRuntime employmentContracts = null,
        IGameMoneyRuntime moneyRuntime = null,
        IPaidFacilityContractRuntime paidFacilityContracts = null)
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
        this.employmentContracts = employmentContracts;
        this.moneyRuntime = moneyRuntime;
        this.paidFacilityContracts = paidFacilityContracts;
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        currentDay = Mathf.Max(1, eventType.day);
        ResetLedger();
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        OperatingCostSettlement operatingCosts =
            SettleOperatingCosts(Mathf.Max(1, eventType.day));
        latestReport = BuildReport(Mathf.Max(1, eventType.day), operatingCosts);
        reportHistory.Insert(0, latestReport);
        if (reportHistory.Count > MaxReportHistory)
        {
            reportHistory.RemoveRange(MaxReportHistory, reportHistory.Count - MaxReportHistory);
        }

        gameEventBus.Publish(new OperatingDayReportEvent(latestReport));
        ResetLedger();
    }

    public bool TryTakeEmergencyFunding(out string message)
    {
        message = "자동 긴급 지원금은 더 이상 제공되지 않습니다.";
        return false;
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        CharacterActor visitor = eventType.visitorActor;
        CharacterIdentity identity = visitor != null ? visitor.Identity : null;
        if (visitor == null || identity == null || identity.CharacterType != CharacterType.Customer)
        {
            return;
        }

        totalVisits++;
        string species = string.IsNullOrWhiteSpace(identity.SpeciesTag) ? "Unknown" : identity.SpeciesTag;
        speciesVisits[species] = speciesVisits.TryGetValue(species, out int count) ? count + 1 : 1;

        CharacterStats stats = visitor.Stats;
        if (stats != null && stats.Stats.TryGetValue(CharacterCondition.MOOD, out float mood))
        {
            visitorMoodSamples.Add(mood);
        }
    }

    public void OnTriggerEvent(FacilityRevenueEvent eventType)
    {
        int revenue = Mathf.Max(0, eventType.revenue);
        if (revenue <= 0) return;

        totalRevenue += revenue;
        string facilityName = GetFacilityName(eventType.facility);
        facilityRevenue[facilityName] = facilityRevenue.TryGetValue(facilityName, out int current)
            ? current + revenue
            : revenue;
    }

    public void OnTriggerEvent(FacilityStockConsumedEvent eventType)
    {
        int amount = Mathf.Max(0, eventType.amount);
        if (amount <= 0) return;

        consumedStock[eventType.category] = consumedStock.TryGetValue(eventType.category, out int current)
            ? current + amount
            : amount;
    }

    public void OnTriggerEvent(FacilityCrimeEvent eventType)
    {
        string detail = string.IsNullOrWhiteSpace(eventType.detail)
            ? $"{eventType.kind}: loss {Mathf.Max(0, eventType.lossValue)}"
            : eventType.detail;
        incidents.Add(detail);
    }

    public void OnTriggerEvent(FacilityRestockEvent eventType)
    {
        if (eventType.requestedAmount > 0 && eventType.restockedAmount <= 0)
        {
            restockFailureCount++;
        }
    }

    public void OnTriggerEvent(StockSupplyEvent eventType)
    {
        stockSupplyResults.Add(eventType.result);
    }

    public void OnTriggerEvent(EventAlertLoggedEvent eventType)
    {
        if (eventType.record == null)
        {
            return;
        }

        eventLog.Add(eventType.record.ButtonText);
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        stockSupplySubscription?.Dispose();
        stockSupplySubscription = null;
        facilityVisitSubscription?.Dispose();
        facilityVisitSubscription = null;
        facilityRevenueSubscription?.Dispose();
        facilityRevenueSubscription = null;
        facilityStockConsumedSubscription?.Dispose();
        facilityStockConsumedSubscription = null;
        facilityCrimeSubscription?.Dispose();
        facilityCrimeSubscription = null;
        facilityRestockSubscription?.Dispose();
        facilityRestockSubscription = null;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
        eventAlertLoggedSubscription?.Dispose();
        eventAlertLoggedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        stockSupplySubscription ??= gameEventBus.Subscribe<StockSupplyEvent>(OnTriggerEvent);
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

    private OperatingDayReport BuildReport(int day, OperatingCostSettlement operatingCosts)
    {
        IReadOnlyList<BuildableObject> buildings = RequireBuildingQuery().Buildings;
        IReadOnlyList<CharacterActor> characters = RequireCharacterQuery().Characters;

        BuildingReportData buildingData = BuildBuildingSnapshot(buildings);
        StaffReportData staffData = BuildStaffSnapshot(characters);

        return OperatingDayReport.Create(
            day: day,
            totalRevenue: totalRevenue,
            totalVisits: totalVisits,
            averageSatisfaction: visitorMoodSamples.Count > 0 ? visitorMoodSamples.Average() : 0f,
            repairCost: buildingData.RepairCost,
            restockFailureCount: restockFailureCount,
            facilityRevenues: facilityRevenue
                .Select((pair) => new FacilityRevenueSummary(pair.Key, pair.Value))
                .OrderByDescending((item) => item.revenue)
                .ToList(),
            speciesVisits: speciesVisits
                .Select((pair) => new SpeciesVisitSummary(pair.Key, pair.Value))
                .OrderByDescending((item) => item.visitCount)
                .ToList(),
            incidents: incidents,
            damagedFacilities: buildingData.DamagedFacilities,
            stockShortageFacilities: buildingData.StockShortageFacilities,
            staffComplaintEvents: staffData.Complaints,
            eventLog: eventLog,
            staffSummary: staffData.Summary,
            warehouseStocks: buildingData.WarehouseStocks,
            stockConsumed: consumedStock
                .Select((pair) => new StockConsumptionSummary(pair.Key, pair.Value))
                .OrderByDescending((item) => item.amount)
                .ToList(),
            stockSupplyResults: stockSupplyResults,
            refreshedDailyShopOffers: StockSupplyService.CreateDailyDeliveryOffers(
                    day + 1,
                    RequireRunVariableReader())
                .ToList(),
            refreshedFacilityShopOffers: FacilityShopService.CreateDailyOffers(
                    day + 1,
                    RequireFacilityShopCatalog(),
                    RequireRunVariableReader())
                .Select((offer) => offer.ToSnapshot())
                .ToList(),
            maintenanceCost: operatingCosts.Forecast.MaintenanceCost,
            payrollCost: operatingCosts.Forecast.PayrollCost,
            previousDebt: operatingCosts.Forecast.OutstandingDebt,
            paidOperatingCost: operatingCosts.PaidAmount,
            unpaidOperatingCost: operatingCosts.CarriedDebt,
            closingBalance: operatingCosts.ClosingBalance,
            consecutiveShortfallDays: operatingCosts.ConsecutiveShortfallDays);
    }

    private static BuildingReportData BuildBuildingSnapshot(IEnumerable<BuildableObject> buildings)
    {
        BuildingReportData data = new BuildingReportData();
        foreach (BuildableObject building in buildings.Where((building) => building != null && !building.isDestroy))
        {
            BuildingSO definition = building.BuildingData;
            if (definition != null
                && !definition.IsStructuralWall
                && !definition.IsDoor
                && !definition.IsGridMovement)
            {
                data.MaintenanceCost += definition.GetMaintenanceCost();
            }

            if (building.IsDamaged)
            {
                data.DamagedFacilities.Add(GetFacilityName(building));
                data.RepairCost += building.BuildingData.GetMaintenanceCost();
            }

            if (building is IRestockableFacility restockable
                && building.Facility != null
                && restockable.CurrentStock <= building.GetRestockRequestThreshold())
            {
                data.StockShortageFacilities.Add(GetFacilityName(building));
            }

            if (building is IWarehouseFacility warehouse && warehouse.HasWarehouseInventory)
            {
                data.WarehouseStocks.Add(new WarehouseStockSummary(
                    GetFacilityName(building),
                    warehouse.Inventory.TotalStock,
                    warehouse.Inventory.HasCapacityLimit ? warehouse.Inventory.MaxCapacity : 0,
                    warehouse.Inventory.EnumerateStock()
                        .Select((pair) => new StockConsumptionSummary(pair.Key, pair.Value))
                        .ToList()));
            }
        }

        return data;
    }

    private static StaffReportData BuildStaffSnapshot(IEnumerable<CharacterActor> characters)
    {
        List<CharacterActor> staff = characters
            .Where(IsStaffCharacter)
            .ToList();

        if (staff.Count == 0)
        {
            return new StaffReportData(
                new StaffWorkSummary(0, 0, 0, 0f, 0f),
                Array.Empty<string>());
        }

        StaffWorkSummary summary = new StaffWorkSummary(
            staff.Count,
            staff.Count((actor) =>
                CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work) && work.isWorking),
            staff.Count((actor) =>
                CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work) && work.IsOffDuty),
            staff.Average((actor) => GetStat(actor, CharacterCondition.SLEEP)),
            staff.Average((actor) => GetStat(actor, CharacterCondition.MOOD)));
        List<string> complaints = staff
            .Where((actor) => GetStat(actor, CharacterCondition.MOOD) <= 25f)
            .Select((actor) => $"{actor.name}: 기분 낮음")
            .ToList();
        return new StaffReportData(summary, complaints);
    }

    private sealed class BuildingReportData
    {
        public int MaintenanceCost;
        public int RepairCost;
        public readonly List<string> DamagedFacilities = new List<string>();
        public readonly List<string> StockShortageFacilities = new List<string>();
        public readonly List<WarehouseStockSummary> WarehouseStocks = new List<WarehouseStockSummary>();
    }

    private OperatingCostForecast BuildOperatingCostForecast()
    {
        int payroll = employmentContracts?.ForecastCost(1) ?? 0;
        int paidFacilityContractsCost =
            paidFacilityContracts?.ForecastCost(1) ?? 0;
        int money = moneyRuntime?.Balance ?? 0;
        return new OperatingCostForecast(
            money,
            paidFacilityContractsCost,
            payroll,
            0);
    }

    private OperatingCostSettlement SettleOperatingCosts(int day)
    {
        int openingBalance = moneyRuntime?.Balance ?? 0;
        int previousDebt = Mathf.Max(0, outstandingDebt);
        EmploymentDailySettlement employment =
            employmentContracts?.SettleDay(day)
            ?? new EmploymentDailySettlement { day = day };
        int paidFacilityContractsDue =
            paidFacilityContracts?.ForecastCost(1) ?? 0;
        int paidFacilityContractsPaid =
            paidFacilityContracts?.SettleDay(day) ?? 0;
        int employmentDue = employment.employeeWagesDue
            + employment.mercenaryFeesDue;
        int currentEmploymentDue = Mathf.Max(
            0,
            employmentDue - previousDebt);
        int totalPaid = paidFacilityContractsPaid
            + employment.employeeWagesPaid
            + employment.mercenaryFeesPaid;
        outstandingDebt = Mathf.Max(0, employment.unpaidEmployeeWages);
        consecutiveShortfallDays = outstandingDebt > 0
            ? consecutiveShortfallDays + 1
            : 0;

        OperatingCostForecast forecast = new OperatingCostForecast(
            openingBalance,
            paidFacilityContractsDue,
            currentEmploymentDue,
            previousDebt);
        OperatingCostSettlement settlement = new OperatingCostSettlement(
            forecast,
            totalPaid,
            moneyRuntime?.Balance ?? 0,
            outstandingDebt,
            consecutiveShortfallDays);
        if (outstandingDebt > 0)
        {
            gameEventBus.RaiseAlert(
                "임금 체불",
                $"직원 임금 {outstandingDebt}골드가 체불되었습니다.",
                EventAlertImportance.High,
                "경영");
        }

        return settlement;
    }

    private OperatingCostSettlement SettleLegacyOperatingCosts()
    {
        OperatingCostForecast forecast = BuildOperatingCostForecast();
        if (!TryGetGameData(out GameData gameData))
        {
            return new OperatingCostSettlement(forecast, 0, 0, outstandingDebt, consecutiveShortfallDays);
        }

        OperatingCostSettlement settlement = DungeonEconomyCalculator.Settle(
            forecast,
            consecutiveShortfallDays);
        gameData.holdingMoney.Value = settlement.ClosingBalance;
        outstandingDebt = settlement.CarriedDebt;
        consecutiveShortfallDays = settlement.ConsecutiveShortfallDays;

        if (settlement.UnpaidAmount > 0)
        {
            ApplyShortfallConsequences(settlement);
            gameEventBus.RaiseAlert(
                "운영비 부족",
                $"운영비 {settlement.Forecast.TotalDue} 중 {settlement.PaidAmount}을 납부했습니다. 미납금 {settlement.CarriedDebt}이 다음 날로 넘어갑니다.",
                EventAlertImportance.High,
                "운영");
        }

        return settlement;
    }

    private void ApplyShortfallConsequences(OperatingCostSettlement settlement)
    {
        float moodPenalty = -Mathf.Max(0f, economySettings?.unpaidWageMoodPenaltyPerDay ?? 0f)
            * Mathf.Clamp(settlement.ConsecutiveShortfallDays, 1, 3);
        float duration = Mathf.Max(1f, economySettings?.unpaidWageMoodDurationSeconds ?? 180f);
        foreach (CharacterActor staff in RequireCharacterQuery().Characters.Where(IsStaffCharacter))
        {
            staff.ApplyMoodFactor(
                "economy:unpaid-wages",
                "임금 체불이 불안함",
                moodPenalty,
                duration,
                1);
        }

        int breakdownThreshold = Mathf.Max(1, economySettings?.breakdownAfterShortfallDays ?? 2);
        if (settlement.ConsecutiveShortfallDays < breakdownThreshold)
        {
            return;
        }

        BuildableObject breakdown = RequireBuildingQuery().Buildings
            .Where(building => building != null
                && !building.isDestroy
                && !building.IsDamaged
                && building.BuildingData != null
                && !building.BuildingData.IsStructuralWall
                && !building.BuildingData.IsDoor
                && !building.BuildingData.IsGridMovement
                && building.BuildingData.GetMaintenanceCost() > 0)
            .OrderByDescending(building => building.BuildingData.GetMaintenanceCost())
            .ThenBy(building => building.GetInstanceID())
            .FirstOrDefault();
        if (breakdown != null)
        {
            breakdown.SetDamaged(true);
            incidents.Add($"{GetFacilityName(breakdown)}: 유지비 미납으로 고장");
        }
    }

    private bool TryGetGameData(out GameData gameData)
    {
        gameData = null;
        return gameDataProvider != null
            && gameDataProvider.TryGetGameData(out gameData)
            && gameData?.holdingMoney != null;
    }

    private sealed class StaffReportData
    {
        public StaffReportData(StaffWorkSummary summary, IReadOnlyList<string> complaints)
        {
            Summary = summary;
            Complaints = complaints;
        }

        public StaffWorkSummary Summary { get; }
        public IReadOnlyList<string> Complaints { get; }
    }

    private void ResetLedger()
    {
        facilityRevenue.Clear();
        speciesVisits.Clear();
        consumedStock.Clear();
        visitorMoodSamples.Clear();
        stockSupplyResults.Clear();
        incidents.Clear();
        eventLog.Clear();
        totalRevenue = 0;
        totalVisits = 0;
        restockFailureCount = 0;
    }

    private static string GetFacilityName(BuildableObject facility)
    {
        if (facility == null) return "Unknown";
        if (facility.BuildingData != null && !string.IsNullOrWhiteSpace(facility.BuildingData.objectName))
        {
            return facility.BuildingData.objectName;
        }

        return facility.name;
    }

    private static float GetStat(CharacterActor actor, CharacterCondition condition)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null)
        {
            return 0f;
        }

        return stats.Stats.TryGetValue(condition, out float value) ? value : 0f;
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
        if (buildingQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(OperatingDaySettlementRuntime)} requires "
                + $"{nameof(IBuildingWorldQuery)} injection.");
        }

        return buildingQuery;
    }

    private ICharacterWorldQuery RequireCharacterQuery()
    {
        if (characterQuery == null)
        {
            throw new InvalidOperationException(
                $"{nameof(OperatingDaySettlementRuntime)} requires "
                + $"{nameof(ICharacterWorldQuery)} injection.");
        }

        return characterQuery;
    }

    private IFacilityShopCatalog RequireFacilityShopCatalog()
    {
        if (facilityShopCatalog == null)
        {
            throw new InvalidOperationException($"{nameof(OperatingDaySettlementRuntime)} requires {nameof(IFacilityShopCatalog)} injection.");
        }

        return facilityShopCatalog;
    }

    private IRunVariableRuntimeReader RequireRunVariableReader()
    {
        if (runVariableReader == null)
        {
            throw new InvalidOperationException($"{nameof(OperatingDaySettlementRuntime)} requires {nameof(IRunVariableRuntimeReader)} injection.");
        }

        return runVariableReader;
    }
}
