using System;
using System.Collections.Generic;
using System.Linq;

public interface IOperatingDaySettlementRuntimeProvider
{
    bool TryGetRuntime(out OperatingDaySettlementRuntime runtime);
}

public sealed class OperatingDaySettlementRuntimeProvider :
    IOperatingDaySettlementRuntimeProvider
{
    private readonly DungeonSceneRuntimeReferences runtimeReferences;

    public OperatingDaySettlementRuntimeProvider(
        DungeonSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out OperatingDaySettlementRuntime runtime)
    {
        runtime = runtimeReferences.Settlement;
        return runtime != null;
    }
}

public interface IOperatingDaySettlementSaveService
{
    DungeonOperatingDaySettlementSaveData Capture();
    void Restore(DungeonOperatingDaySettlementSaveData source, DungeonGameRestoreReport report);
}

[Serializable]
public sealed class DungeonOperatingDaySettlementSaveData
{
    public int currentDay = 1;
    public int totalRevenue;
    public int totalVisits;
    public int restockFailureCount;
    public List<DungeonStringIntSaveEntry> facilityRevenue = new List<DungeonStringIntSaveEntry>();
    public List<DungeonStringIntSaveEntry> speciesVisits = new List<DungeonStringIntSaveEntry>();
    public List<DungeonStockAmountSaveData> consumedStock = new List<DungeonStockAmountSaveData>();
    public List<float> visitorMoodSamples = new List<float>();
    public List<DungeonStockSupplyResultSaveData> stockSupplyResults = new List<DungeonStockSupplyResultSaveData>();
    public List<string> incidents = new List<string>();
    public List<string> eventLog = new List<string>();
    public List<DungeonOperatingDayReportSaveData> reportHistory = new List<DungeonOperatingDayReportSaveData>();
    public int outstandingDebt;
    public int consecutiveShortfallDays;
    public bool emergencyFundingUsed;
}

[Serializable]
public sealed class DungeonStockAmountSaveData
{
    public StockCategory category;
    public int amount;
}

[Serializable]
public sealed class DungeonStockSupplyResultSaveData
{
    public bool success;
    public StockCategory category;
    public int requestedAmount;
    public int deliveredAmount;
    public int cost;
    public string sourceLabel = string.Empty;
    public string reason = string.Empty;
}

[Serializable]
public sealed class DungeonStockDeliveryOfferSaveData
{
    public StockCategory category;
    public int amount;
    public int cost;
    public string sourceLabel = string.Empty;
}

[Serializable]
public sealed class DungeonFacilityShopOfferSummarySaveData
{
    public string offerTypeId = string.Empty;
    public string typeDisplayName = string.Empty;
    public FacilityShopRarity rarity;
    public string displayName = string.Empty;
    public int cost;
    public int star;
    public bool basicPurchase;
}

[Serializable]
public sealed class DungeonWarehouseStockSaveData
{
    public string warehouseName = string.Empty;
    public int totalStock;
    public int maxCapacity;
    public List<DungeonStockAmountSaveData> stocks = new List<DungeonStockAmountSaveData>();
}

[Serializable]
public sealed class DungeonOperatingDayReportSaveData
{
    public int day = 1;
    public int totalRevenue;
    public int totalVisits;
    public float averageSatisfaction;
    public int repairCost;
    public int maintenanceCost;
    public int payrollCost;
    public int previousDebt;
    public int paidOperatingCost;
    public int unpaidOperatingCost;
    public int closingBalance;
    public int consecutiveShortfallDays;
    public int restockFailureCount;
    public List<DungeonStringIntSaveEntry> facilityRevenues = new List<DungeonStringIntSaveEntry>();
    public List<DungeonStringIntSaveEntry> speciesVisits = new List<DungeonStringIntSaveEntry>();
    public List<string> incidents = new List<string>();
    public List<string> damagedFacilities = new List<string>();
    public List<string> stockShortageFacilities = new List<string>();
    public List<string> staffComplaintEvents = new List<string>();
    public List<string> eventLog = new List<string>();
    public List<string> unlockedCodexInfo = new List<string>();
    public int staffCount;
    public int workingCount;
    public int offDutyCount;
    public float averageSleep;
    public float averageMood;
    public List<DungeonWarehouseStockSaveData> warehouseStocks = new List<DungeonWarehouseStockSaveData>();
    public List<DungeonStockAmountSaveData> stockConsumed = new List<DungeonStockAmountSaveData>();
    public List<DungeonStockSupplyResultSaveData> stockSupplyResults = new List<DungeonStockSupplyResultSaveData>();
    public List<DungeonStockDeliveryOfferSaveData> refreshedDailyShopOffers = new List<DungeonStockDeliveryOfferSaveData>();
    public List<DungeonFacilityShopOfferSummarySaveData> refreshedFacilityShopOffers =
        new List<DungeonFacilityShopOfferSummarySaveData>();
}

public sealed class OperatingDaySettlementSaveService : IOperatingDaySettlementSaveService
{
    private readonly IOperatingDaySettlementRuntimeProvider provider;

    public OperatingDaySettlementSaveService(IOperatingDaySettlementRuntimeProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public DungeonOperatingDaySettlementSaveData Capture()
    {
        if (!provider.TryGetRuntime(out OperatingDaySettlementRuntime runtime))
        {
            return new DungeonOperatingDaySettlementSaveData();
        }

        OperatingDaySettlementPersistenceState state = runtime.CapturePersistentState();
        return new DungeonOperatingDaySettlementSaveData
        {
            currentDay = state.CurrentDay,
            totalRevenue = state.TotalRevenue,
            totalVisits = state.TotalVisits,
            restockFailureCount = state.RestockFailureCount,
            facilityRevenue = ToStringIntEntries(state.FacilityRevenue),
            speciesVisits = ToStringIntEntries(state.SpeciesVisits),
            consumedStock = state.ConsumedStock
                .OrderBy(pair => pair.Key)
                .Select(pair => new DungeonStockAmountSaveData { category = pair.Key, amount = pair.Value })
                .ToList(),
            visitorMoodSamples = state.VisitorMoodSamples.ToList(),
            stockSupplyResults = state.StockSupplyResults.Select(ToSaveData).ToList(),
            incidents = state.Incidents.ToList(),
            eventLog = state.EventLog.ToList(),
            reportHistory = state.ReportHistory.Select(ToSaveData).ToList(),
            outstandingDebt = state.OutstandingDebt,
            consecutiveShortfallDays = state.ConsecutiveShortfallDays,
            emergencyFundingUsed = state.EmergencyFundingUsed
        };
    }

    public void Restore(DungeonOperatingDaySettlementSaveData source, DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (!provider.TryGetRuntime(out OperatingDaySettlementRuntime runtime))
        {
            report.AddWarning("Operating-day settlement runtime was not present; ledger history was skipped.");
            return;
        }

        source ??= new DungeonOperatingDaySettlementSaveData();
        runtime.RestorePersistentState(new OperatingDaySettlementPersistenceState(
            source.currentDay,
            source.totalRevenue,
            source.totalVisits,
            source.restockFailureCount,
            ToStringIntDictionary(source.facilityRevenue),
            ToStringIntDictionary(source.speciesVisits),
            (source.consumedStock ?? new List<DungeonStockAmountSaveData>())
                .Where(entry => entry != null)
                .GroupBy(entry => entry.category)
                .ToDictionary(group => group.Key, group => group.Last().amount),
            source.visitorMoodSamples ?? new List<float>(),
            (source.stockSupplyResults ?? new List<DungeonStockSupplyResultSaveData>())
                .Where(entry => entry != null)
                .Select(FromSaveData)
                .ToList(),
            source.incidents ?? new List<string>(),
            source.eventLog ?? new List<string>(),
            (source.reportHistory ?? new List<DungeonOperatingDayReportSaveData>())
                .Where(entry => entry != null)
                .Select(FromSaveData)
                .ToList(),
            source.outstandingDebt,
            source.consecutiveShortfallDays,
            source.emergencyFundingUsed));
    }

    private static List<DungeonStringIntSaveEntry> ToStringIntEntries(IReadOnlyDictionary<string, int> source)
    {
        return source
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DungeonStringIntSaveEntry { key = pair.Key, value = pair.Value })
            .ToList();
    }

    private static Dictionary<string, int> ToStringIntDictionary(IEnumerable<DungeonStringIntSaveEntry> source)
    {
        return (source ?? Array.Empty<DungeonStringIntSaveEntry>())
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.key))
            .GroupBy(entry => entry.key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().value, StringComparer.Ordinal);
    }

    private static DungeonStockSupplyResultSaveData ToSaveData(StockSupplyResult source)
    {
        return new DungeonStockSupplyResultSaveData
        {
            success = source.success,
            category = source.category,
            requestedAmount = source.requestedAmount,
            deliveredAmount = source.deliveredAmount,
            cost = source.cost,
            sourceLabel = source.sourceLabel,
            reason = source.reason
        };
    }

    private static StockSupplyResult FromSaveData(DungeonStockSupplyResultSaveData source)
    {
        return new StockSupplyResult(
            source.success,
            source.category,
            source.requestedAmount,
            source.deliveredAmount,
            source.cost,
            source.sourceLabel,
            source.reason);
    }

    private static DungeonOperatingDayReportSaveData ToSaveData(OperatingDayReport source)
    {
        return new DungeonOperatingDayReportSaveData
        {
            day = source.day,
            totalRevenue = source.totalRevenue,
            totalVisits = source.totalVisits,
            averageSatisfaction = source.averageSatisfaction,
            repairCost = source.repairCost,
            maintenanceCost = source.maintenanceCost,
            payrollCost = source.payrollCost,
            previousDebt = source.previousDebt,
            paidOperatingCost = source.paidOperatingCost,
            unpaidOperatingCost = source.unpaidOperatingCost,
            closingBalance = source.closingBalance,
            consecutiveShortfallDays = source.consecutiveShortfallDays,
            restockFailureCount = source.restockFailureCount,
            facilityRevenues = source.facilityRevenues.Select(item =>
                new DungeonStringIntSaveEntry { key = item.facilityName, value = item.revenue }).ToList(),
            speciesVisits = source.speciesVisits.Select(item =>
                new DungeonStringIntSaveEntry { key = item.speciesTag, value = item.visitCount }).ToList(),
            incidents = source.incidents.ToList(),
            damagedFacilities = source.damagedFacilities.ToList(),
            stockShortageFacilities = source.stockShortageFacilities.ToList(),
            staffComplaintEvents = source.staffComplaintEvents.ToList(),
            eventLog = source.eventLog.ToList(),
            unlockedCodexInfo = source.unlockedCodexInfo.ToList(),
            staffCount = source.staffSummary.staffCount,
            workingCount = source.staffSummary.workingCount,
            offDutyCount = source.staffSummary.offDutyCount,
            averageSleep = source.staffSummary.averageSleep,
            averageMood = source.staffSummary.averageMood,
            warehouseStocks = source.warehouseStocks.Select(warehouse => new DungeonWarehouseStockSaveData
            {
                warehouseName = warehouse.warehouseName,
                totalStock = warehouse.totalStock,
                maxCapacity = warehouse.maxCapacity,
                stocks = warehouse.stocks.Select(stock =>
                    new DungeonStockAmountSaveData { category = stock.category, amount = stock.amount }).ToList()
            }).ToList(),
            stockConsumed = source.stockConsumed.Select(stock =>
                new DungeonStockAmountSaveData { category = stock.category, amount = stock.amount }).ToList(),
            stockSupplyResults = source.stockSupplyResults.Select(ToSaveData).ToList(),
            refreshedDailyShopOffers = source.refreshedDailyShopOffers.Select(offer =>
                new DungeonStockDeliveryOfferSaveData
                {
                    category = offer.category,
                    amount = offer.amount,
                    cost = offer.cost,
                    sourceLabel = offer.sourceLabel
                }).ToList(),
            refreshedFacilityShopOffers = source.refreshedFacilityShopOffers.Select(offer =>
                new DungeonFacilityShopOfferSummarySaveData
                {
                    offerTypeId = offer.offerTypeId,
                    typeDisplayName = offer.typeDisplayName,
                    rarity = offer.rarity,
                    displayName = offer.displayName,
                    cost = offer.cost,
                    star = offer.star,
                    basicPurchase = offer.basicPurchase
                }).ToList()
        };
    }

    private static OperatingDayReport FromSaveData(DungeonOperatingDayReportSaveData source)
    {
        return OperatingDayReport.Create(
            day: source.day,
            totalRevenue: source.totalRevenue,
            totalVisits: source.totalVisits,
            averageSatisfaction: source.averageSatisfaction,
            repairCost: source.repairCost,
            restockFailureCount: source.restockFailureCount,
            facilityRevenues: (source.facilityRevenues ?? new List<DungeonStringIntSaveEntry>())
                .Where(entry => entry != null)
                .Select(entry => new FacilityRevenueSummary(entry.key, entry.value)).ToList(),
            speciesVisits: (source.speciesVisits ?? new List<DungeonStringIntSaveEntry>())
                .Where(entry => entry != null)
                .Select(entry => new SpeciesVisitSummary(entry.key, entry.value)).ToList(),
            incidents: source.incidents ?? new List<string>(),
            damagedFacilities: source.damagedFacilities ?? new List<string>(),
            stockShortageFacilities: source.stockShortageFacilities ?? new List<string>(),
            staffComplaintEvents: source.staffComplaintEvents ?? new List<string>(),
            eventLog: source.eventLog ?? new List<string>(),
            unlockedCodexInfo: source.unlockedCodexInfo ?? new List<string>(),
            staffSummary: new StaffWorkSummary(
                source.staffCount,
                source.workingCount,
                source.offDutyCount,
                source.averageSleep,
                source.averageMood),
            warehouseStocks: (source.warehouseStocks ?? new List<DungeonWarehouseStockSaveData>())
                .Where(warehouse => warehouse != null)
                .Select(warehouse => new WarehouseStockSummary(
                    warehouse.warehouseName,
                    warehouse.totalStock,
                    warehouse.maxCapacity,
                    (warehouse.stocks ?? new List<DungeonStockAmountSaveData>())
                        .Where(stock => stock != null)
                        .Select(stock => new StockConsumptionSummary(stock.category, stock.amount)).ToList()))
                .ToList(),
            stockConsumed: (source.stockConsumed ?? new List<DungeonStockAmountSaveData>())
                .Where(stock => stock != null)
                .Select(stock => new StockConsumptionSummary(stock.category, stock.amount)).ToList(),
            stockSupplyResults: (source.stockSupplyResults ?? new List<DungeonStockSupplyResultSaveData>())
                .Where(entry => entry != null)
                .Select(FromSaveData).ToList(),
            refreshedDailyShopOffers: (source.refreshedDailyShopOffers ?? new List<DungeonStockDeliveryOfferSaveData>())
                .Where(offer => offer != null)
                .Select(offer => new StockDeliveryOffer(
                    offer.category,
                    offer.amount,
                    offer.cost,
                    offer.sourceLabel)).ToList(),
            refreshedFacilityShopOffers: (source.refreshedFacilityShopOffers
                    ?? new List<DungeonFacilityShopOfferSummarySaveData>())
                .Where(offer => offer != null)
                .Select(offer => new FacilityShopOfferSnapshot(
                    offer.offerTypeId,
                    offer.typeDisplayName,
                    offer.rarity,
                    offer.displayName,
                    offer.cost,
                    offer.star,
                    offer.basicPurchase)).ToList(),
            maintenanceCost: source.maintenanceCost,
            payrollCost: source.payrollCost,
            previousDebt: source.previousDebt,
            paidOperatingCost: source.paidOperatingCost,
            unpaidOperatingCost: source.unpaidOperatingCost,
            closingBalance: source.closingBalance,
            consecutiveShortfallDays: source.consecutiveShortfallDays);
    }
}
