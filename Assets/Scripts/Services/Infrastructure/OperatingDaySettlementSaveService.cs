using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Operation;

namespace DungeonStory.Infrastructure
{
public interface IOperatingDaySettlementSaveService
{
    DungeonOperatingDaySettlementSaveData Capture();
    OperatingDaySettlementRestoreCandidate PrepareRestore(
        DungeonOperatingDaySettlementSaveData source);
    void PublishRestore(OperatingDaySettlementRestoreCandidate candidate);
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

internal static class OperatingDaySettlementSaveValidation
{
    private const int MaxReportHistory = 20;

    public static void Validate(
        DungeonOperatingDaySettlementSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (payload == null)
        {
            report.AddError("Operating-day settlement payload is null.");
            return;
        }

        if (payload.currentDay < 1)
        {
            report.AddError("Operating-day settlement current day is invalid.");
        }
        ValidateNonNegative(payload.totalRevenue, "total revenue", report);
        ValidateNonNegative(payload.totalVisits, "total visits", report);
        ValidateNonNegative(
            payload.restockFailureCount,
            "restock failure count",
            report);
        ValidateNonNegative(payload.outstandingDebt, "outstanding debt", report);
        ValidateNonNegative(
            payload.consecutiveShortfallDays,
            "consecutive shortfall days",
            report);
        ValidateStringIntEntries(
            payload.facilityRevenue,
            "facility revenue",
            report);
        ValidateStringIntEntries(
            payload.speciesVisits,
            "species visits",
            report);
        ValidateStockAmounts(payload.consumedStock, "consumed stock", report);

        if (payload.visitorMoodSamples == null)
        {
            report.AddError("Operating-day settlement mood samples are missing.");
        }
        else
        {
            for (int index = 0; index < payload.visitorMoodSamples.Count; index++)
            {
                float mood = payload.visitorMoodSamples[index];
                if (!IsFinite(mood) || mood < 0f || mood > 100f)
                {
                    report.AddError(
                        $"Operating-day settlement mood sample {index} is invalid.");
                }
            }
        }

        ValidateSupplyResults(
            payload.stockSupplyResults,
            "current stock supply",
            report);
        ValidateTextList(payload.incidents, "incidents", report);
        ValidateTextList(payload.eventLog, "event log", report);
        if (payload.reportHistory == null)
        {
            report.AddError("Operating-day settlement report history is missing.");
            return;
        }

        if (payload.reportHistory.Count > MaxReportHistory)
        {
            report.AddError(
                $"Operating-day settlement exceeds the {MaxReportHistory}-report history limit.");
        }

        HashSet<int> reportDays = new HashSet<int>();
        for (int index = 0; index < payload.reportHistory.Count; index++)
        {
            DungeonOperatingDayReportSaveData saved = payload.reportHistory[index];
            if (saved == null)
            {
                report.AddError(
                    $"Operating-day settlement report {index} is null.");
                continue;
            }

            if (!reportDays.Add(saved.day))
            {
                report.AddError(
                    $"Operating-day settlement contains duplicate report day {saved.day}.");
            }

            ValidateReport(saved, index, report);
        }
    }

    private static void ValidateReport(
        DungeonOperatingDayReportSaveData saved,
        int index,
        DungeonGameRestoreReport report)
    {
        string prefix = $"Operating-day report {index}";
        if (saved.day < 1)
        {
            report.AddError($"{prefix} day is invalid.");
        }
        ValidateNonNegative(saved.totalRevenue, $"{prefix} revenue", report);
        ValidateNonNegative(saved.totalVisits, $"{prefix} visits", report);
        ValidateNonNegative(saved.repairCost, $"{prefix} repair cost", report);
        ValidateNonNegative(
            saved.restockFailureCount,
            $"{prefix} restock failures",
            report);
        ValidateNonNegative(saved.maintenanceCost, $"{prefix} maintenance", report);
        ValidateNonNegative(saved.payrollCost, $"{prefix} payroll", report);
        ValidateNonNegative(saved.previousDebt, $"{prefix} previous debt", report);
        ValidateNonNegative(saved.paidOperatingCost, $"{prefix} paid cost", report);
        ValidateNonNegative(saved.unpaidOperatingCost, $"{prefix} unpaid cost", report);
        ValidateNonNegative(saved.closingBalance, $"{prefix} closing balance", report);
        ValidateNonNegative(
            saved.consecutiveShortfallDays,
            $"{prefix} shortfall days",
            report);
        ValidateNonNegative(saved.staffCount, $"{prefix} staff count", report);
        ValidateNonNegative(saved.workingCount, $"{prefix} working count", report);
        ValidateNonNegative(saved.offDutyCount, $"{prefix} off-duty count", report);
        if (!IsFinite(saved.averageSatisfaction)
            || saved.averageSatisfaction < 0f
            || saved.averageSatisfaction > 100f
            || !IsFinite(saved.averageSleep)
            || !IsFinite(saved.averageMood))
        {
            report.AddError($"{prefix} contains invalid average values.");
        }

        ValidateStringIntEntries(saved.facilityRevenues, $"{prefix} facility revenue", report);
        ValidateStringIntEntries(saved.speciesVisits, $"{prefix} species visits", report);
        ValidateTextList(saved.incidents, $"{prefix} incidents", report);
        ValidateTextList(saved.damagedFacilities, $"{prefix} damaged facilities", report);
        ValidateTextList(saved.stockShortageFacilities, $"{prefix} shortages", report);
        ValidateTextList(saved.staffComplaintEvents, $"{prefix} complaints", report);
        ValidateTextList(saved.eventLog, $"{prefix} event log", report);
        ValidateTextList(saved.unlockedCodexInfo, $"{prefix} codex info", report);
        ValidateWarehouses(saved.warehouseStocks, prefix, report);
        ValidateStockAmounts(saved.stockConsumed, $"{prefix} consumed stock", report);
        ValidateSupplyResults(saved.stockSupplyResults, $"{prefix} stock supply", report);
        ValidateDeliveryOffers(saved.refreshedDailyShopOffers, prefix, report);
        ValidateFacilityOffers(saved.refreshedFacilityShopOffers, prefix, report);
    }

    private static void ValidateStringIntEntries(
        IReadOnlyList<DungeonStringIntSaveEntry> entries,
        string label,
        DungeonGameRestoreReport report)
    {
        if (entries == null)
        {
            report.AddError($"{label} entries are missing.");
            return;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            DungeonStringIntSaveEntry entry = entries[index];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.key)
                || entry.value < 0
                || !keys.Add(entry.key))
            {
                report.AddError($"{label} entry {index} is invalid or duplicate.");
            }
        }
    }

    private static void ValidateStockAmounts(
        IReadOnlyList<DungeonStockAmountSaveData> entries,
        string label,
        DungeonGameRestoreReport report)
    {
        if (entries == null)
        {
            report.AddError($"{label} entries are missing.");
            return;
        }

        HashSet<StockCategory> categories = new HashSet<StockCategory>();
        for (int index = 0; index < entries.Count; index++)
        {
            DungeonStockAmountSaveData entry = entries[index];
            if (entry == null
                || !Enum.IsDefined(typeof(StockCategory), entry.category)
                || entry.amount < 0
                || !categories.Add(entry.category))
            {
                report.AddError($"{label} entry {index} is invalid or duplicate.");
            }
        }
    }

    private static void ValidateSupplyResults(
        IReadOnlyList<DungeonStockSupplyResultSaveData> entries,
        string label,
        DungeonGameRestoreReport report)
    {
        if (entries == null)
        {
            report.AddError($"{label} entries are missing.");
            return;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            DungeonStockSupplyResultSaveData entry = entries[index];
            if (entry == null
                || !Enum.IsDefined(typeof(StockCategory), entry.category)
                || entry.requestedAmount < 0
                || entry.deliveredAmount < 0
                || entry.deliveredAmount > entry.requestedAmount
                || entry.cost < 0
                || entry.sourceLabel == null
                || entry.reason == null)
            {
                report.AddError($"{label} entry {index} is invalid.");
            }
        }
    }

    private static void ValidateTextList(
        IReadOnlyList<string> values,
        string label,
        DungeonGameRestoreReport report)
    {
        if (values == null)
        {
            report.AddError($"{label} is missing.");
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(values[index]))
            {
                report.AddError($"{label} entry {index} is empty.");
            }
        }
    }

    private static void ValidateWarehouses(
        IReadOnlyList<DungeonWarehouseStockSaveData> warehouses,
        string prefix,
        DungeonGameRestoreReport report)
    {
        if (warehouses == null)
        {
            report.AddError($"{prefix} warehouse list is missing.");
            return;
        }

        for (int index = 0; index < warehouses.Count; index++)
        {
            DungeonWarehouseStockSaveData warehouse = warehouses[index];
            if (warehouse == null
                || string.IsNullOrWhiteSpace(warehouse.warehouseName)
                || warehouse.totalStock < 0
                || warehouse.maxCapacity < 0
                || warehouse.totalStock > warehouse.maxCapacity)
            {
                report.AddError($"{prefix} warehouse {index} is invalid.");
                continue;
            }

            ValidateStockAmounts(
                warehouse.stocks,
                $"{prefix} warehouse {index} stock",
                report);
        }
    }

    private static void ValidateDeliveryOffers(
        IReadOnlyList<DungeonStockDeliveryOfferSaveData> offers,
        string prefix,
        DungeonGameRestoreReport report)
    {
        if (offers == null)
        {
            report.AddError($"{prefix} delivery offers are missing.");
            return;
        }

        for (int index = 0; index < offers.Count; index++)
        {
            DungeonStockDeliveryOfferSaveData offer = offers[index];
            if (offer == null
                || !Enum.IsDefined(typeof(StockCategory), offer.category)
                || offer.amount < 0
                || offer.cost < 0
                || offer.sourceLabel == null)
            {
                report.AddError($"{prefix} delivery offer {index} is invalid.");
            }
        }
    }

    private static void ValidateFacilityOffers(
        IReadOnlyList<DungeonFacilityShopOfferSummarySaveData> offers,
        string prefix,
        DungeonGameRestoreReport report)
    {
        if (offers == null)
        {
            report.AddError($"{prefix} facility offers are missing.");
            return;
        }

        for (int index = 0; index < offers.Count; index++)
        {
            DungeonFacilityShopOfferSummarySaveData offer = offers[index];
            if (offer == null
                || string.IsNullOrWhiteSpace(offer.offerTypeId)
                || string.IsNullOrWhiteSpace(offer.typeDisplayName)
                || string.IsNullOrWhiteSpace(offer.displayName)
                || !Enum.IsDefined(typeof(FacilityShopRarity), offer.rarity)
                || offer.cost < 0
                || offer.star < 0)
            {
                report.AddError($"{prefix} facility offer {index} is invalid.");
            }
        }
    }

    private static void ValidateNonNegative(
        int value,
        string label,
        DungeonGameRestoreReport report)
    {
        if (value < 0)
        {
            report.AddError($"Operating-day settlement {label} is invalid.");
        }
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class OperatingDaySettlementSaveService : IOperatingDaySettlementSaveService
{
    private readonly OperatingDaySettlementRuntime runtime;

    public OperatingDaySettlementSaveService(
        DungeonSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .Settlement
            ?? throw new InvalidOperationException(
                $"{nameof(OperatingDaySettlementSaveService)} requires a loaded {nameof(OperatingDaySettlementRuntime)}.");
    }

    public DungeonOperatingDaySettlementSaveData Capture()
    {
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

    public OperatingDaySettlementRestoreCandidate PrepareRestore(
        DungeonOperatingDaySettlementSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        OperatingDaySettlementPersistenceState restored =
            new OperatingDaySettlementPersistenceState(
            source.currentDay,
            source.totalRevenue,
            source.totalVisits,
            source.restockFailureCount,
            ToStringIntDictionary(source.facilityRevenue),
            ToStringIntDictionary(source.speciesVisits),
            source.consumedStock
                .GroupBy(entry => entry.category)
                .ToDictionary(group => group.Key, group => group.Last().amount),
            source.visitorMoodSamples,
            source.stockSupplyResults
                .Select(FromSaveData)
                .ToList(),
            source.incidents,
            source.eventLog,
            source.reportHistory
                .Select(FromSaveData)
                .ToList(),
            source.outstandingDebt,
            source.consecutiveShortfallDays,
            source.emergencyFundingUsed);
        return runtime.PrepareRestoreCandidate(restored);
    }

    public void PublishRestore(
        OperatingDaySettlementRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate
            ?? throw new ArgumentNullException(nameof(candidate)));
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
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return source
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
            facilityRevenues: source.facilityRevenues
                .Select(entry => new FacilityRevenueSummary(entry.key, entry.value)).ToList(),
            speciesVisits: source.speciesVisits
                .Select(entry => new SpeciesVisitSummary(entry.key, entry.value)).ToList(),
            incidents: source.incidents,
            damagedFacilities: source.damagedFacilities,
            stockShortageFacilities: source.stockShortageFacilities,
            staffComplaintEvents: source.staffComplaintEvents,
            eventLog: source.eventLog,
            unlockedCodexInfo: source.unlockedCodexInfo,
            staffSummary: new StaffWorkSummary(
                source.staffCount,
                source.workingCount,
                source.offDutyCount,
                source.averageSleep,
                source.averageMood),
            warehouseStocks: source.warehouseStocks
                .Select(warehouse => new WarehouseStockSummary(
                    warehouse.warehouseName,
                    warehouse.totalStock,
                    warehouse.maxCapacity,
                    warehouse.stocks
                        .Select(stock => new StockConsumptionSummary(stock.category, stock.amount)).ToList()))
                .ToList(),
            stockConsumed: source.stockConsumed
                .Select(stock => new StockConsumptionSummary(stock.category, stock.amount)).ToList(),
            stockSupplyResults: source.stockSupplyResults
                .Select(FromSaveData).ToList(),
            refreshedDailyShopOffers: source.refreshedDailyShopOffers
                .Select(offer => new StockDeliveryOffer(
                    offer.category,
                    offer.amount,
                    offer.cost,
                    offer.sourceLabel)).ToList(),
            refreshedFacilityShopOffers: source.refreshedFacilityShopOffers
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

}
