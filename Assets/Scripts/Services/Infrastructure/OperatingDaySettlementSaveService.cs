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
    public List<DungeonOperatingDayFacilityDetailSaveData> facilityDetails =
        new List<DungeonOperatingDayFacilityDetailSaveData>();
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
public sealed class DungeonOperatingDayFacilityDetailSaveData
{
    public string facilityId = string.Empty;
    public string facilityName = string.Empty;
    public int revenue;
    public int visitCount;
    public int operatingCost;
    public List<DungeonStockAmountSaveData> consumedStock =
        new List<DungeonStockAmountSaveData>();
    public List<float> visitorMoodSamples = new List<float>();
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
    public string itemId = string.Empty;
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
    public long storedMassGrams;
    public long maxMassGrams;
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
    public int terminatedContractCost;
    public int closingBalance;
    public int consecutiveShortfallDays;
    public int restockFailureCount;
    public List<DungeonStringIntSaveEntry> facilityRevenues = new List<DungeonStringIntSaveEntry>();
    public List<DungeonOperatingDayFacilityDetailSaveData> facilityDetails =
        new List<DungeonOperatingDayFacilityDetailSaveData>();
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
    public List<DungeonEmploymentSettlementLineSaveData> employmentSettlements =
        new List<DungeonEmploymentSettlementLineSaveData>();
}

[Serializable]
public sealed class DungeonEmploymentSettlementLineSaveData
{
    public int day;
    public string characterId = string.Empty;
    public OperatingDayEmploymentContractKind contractKind;
    public int due;
    public int paid;
    public int unpaidDebtBefore;
    public int unpaidDebtAfter;
    public bool moodPenaltyApplied;
    public bool contractEnded;
    public bool departureAttempted;
    public bool departureStarted;
    public bool departurePending;
    public string departureFailureReason = string.Empty;
    public string financeSourceId = string.Empty;
    public string financeTransactionId = string.Empty;
    public bool financeSucceeded;
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
        ValidateFacilityDetails(
            payload.facilityDetails,
            "current facility detail",
            payload.totalRevenue,
            payload.totalVisits,
            expectedOperatingCost: 0,
            expectedRevenueByName: payload.facilityRevenue,
            expectedStock: payload.consumedStock,
            expectedMoodSamples: payload.visitorMoodSamples,
            report: report);
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
        ValidateNonNegative(
            saved.terminatedContractCost,
            $"{prefix} terminated contract cost",
            report);
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
        ValidateFacilityDetails(
            saved.facilityDetails,
            $"{prefix} facility detail",
            saved.totalRevenue,
            saved.totalVisits,
            saved.maintenanceCost,
            saved.facilityRevenues,
            saved.stockConsumed,
            expectedMoodSamples: null,
            report: report);
        ValidateFacilitySatisfaction(
            saved.facilityDetails,
            saved.averageSatisfaction,
            prefix,
            report);
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
        ValidateEmploymentSettlements(saved, prefix, report);
    }

    private static void ValidateEmploymentSettlements(
        DungeonOperatingDayReportSaveData saved,
        string prefix,
        DungeonGameRestoreReport report)
    {
        if (saved.employmentSettlements == null)
        {
            report.AddError($"{prefix} employment settlements are missing.");
            return;
        }

        HashSet<string> characters = new HashSet<string>(StringComparer.Ordinal);
        int due = 0;
        int paid = 0;
        int debt = 0;
        int terminated = 0;
        for (int index = 0; index < saved.employmentSettlements.Count; index++)
        {
            DungeonEmploymentSettlementLineSaveData line =
                saved.employmentSettlements[index];
            string label = $"{prefix} employment settlement {index}";
            if (line == null
                || line.day != saved.day
                || string.IsNullOrWhiteSpace(line.characterId)
                || !string.Equals(
                    line.characterId,
                    line.characterId.Trim(),
                    StringComparison.Ordinal)
                || !characters.Add(line.characterId)
                || !Enum.IsDefined(
                    typeof(OperatingDayEmploymentContractKind),
                    line.contractKind)
                || line.due < 0
                || line.paid < 0
                || line.paid > line.due
                || line.unpaidDebtBefore < 0
                || line.unpaidDebtAfter < 0
                || line.departureStarted && line.departurePending
                || line.departurePending
                    && string.IsNullOrWhiteSpace(
                        line.departureFailureReason)
                || !line.departurePending
                    && !string.IsNullOrEmpty(
                        line.departureFailureReason)
                || (line.due > 0 || line.contractEnded)
                    && (string.IsNullOrWhiteSpace(line.financeSourceId)
                        || string.IsNullOrWhiteSpace(
                            line.financeTransactionId))
                || line.contractKind
                    == OperatingDayEmploymentContractKind.Employee
                    && (line.contractEnded
                        || line.departureAttempted
                        || line.departureStarted
                        || line.departurePending)
                || line.contractKind
                    == OperatingDayEmploymentContractKind.Mercenary
                    && (line.unpaidDebtBefore != 0
                        || line.unpaidDebtAfter != 0
                        || line.moodPenaltyApplied)
                || line.contractEnded
                    && (!line.departureAttempted
                        || line.departureStarted == line.departurePending)
                || !line.contractEnded
                    && (line.departureAttempted
                        || line.departureStarted
                        || line.departurePending)
                || line.due > 0
                    && line.financeSucceeded != (line.paid == line.due))
            {
                report.AddError($"{label} is invalid or duplicated.");
                continue;
            }

            due = checked(due + line.due);
            paid = checked(paid + line.paid);
            if (line.contractKind
                == OperatingDayEmploymentContractKind.Employee)
            {
                debt = checked(debt + line.unpaidDebtAfter);
            }
            if (line.contractEnded)
            {
                terminated = checked(terminated + line.due - line.paid);
            }
        }

        if (due != saved.payrollCost + saved.previousDebt
            || paid + debt + terminated != due
            || debt != saved.unpaidOperatingCost
            || terminated != saved.terminatedContractCost
            || paid > saved.paidOperatingCost)
        {
            report.AddError(
                $"{prefix} employment settlement totals do not match the report.");
        }
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
                || warehouse.storedMassGrams < 0L
                || warehouse.maxMassGrams <= 0L)
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
                || string.IsNullOrWhiteSpace(offer.itemId)
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

    private static void ValidateFacilityDetails(
        IReadOnlyList<DungeonOperatingDayFacilityDetailSaveData> entries,
        string label,
        int expectedRevenue,
        int expectedVisits,
        int expectedOperatingCost,
        IReadOnlyList<DungeonStringIntSaveEntry> expectedRevenueByName,
        IReadOnlyList<DungeonStockAmountSaveData> expectedStock,
        IReadOnlyList<float> expectedMoodSamples,
        DungeonGameRestoreReport report)
    {
        if (entries == null)
        {
            report.AddError($"{label} entries are missing.");
            return;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            DungeonOperatingDayFacilityDetailSaveData entry = entries[index];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.facilityId)
                || string.IsNullOrWhiteSpace(entry.facilityName)
                || entry.revenue < 0
                || entry.visitCount < 0
                || entry.operatingCost < 0
                || !ids.Add(entry.facilityId))
            {
                report.AddError($"{label} entry {index} is invalid or duplicate.");
                continue;
            }
            ValidateStockAmounts(
                entry.consumedStock,
                $"{label} entry {index} stock",
                report);
            if (entry.visitorMoodSamples == null)
            {
                report.AddError($"{label} entry {index} mood samples are missing.");
                continue;
            }
            for (int moodIndex = 0;
                 moodIndex < entry.visitorMoodSamples.Count;
                 moodIndex++)
            {
                float mood = entry.visitorMoodSamples[moodIndex];
                if (!IsFinite(mood) || mood < 0f || mood > 100f)
                {
                    report.AddError(
                        $"{label} entry {index} mood sample {moodIndex} is invalid.");
                }
            }
        }

        if (entries.Any(entry => entry == null
                || entry.consumedStock == null
                || entry.visitorMoodSamples == null
                || entry.consumedStock.Any(stock => stock == null)))
        {
            return;
        }
        if (entries.Sum(entry => entry.revenue) != expectedRevenue
            || entries.Sum(entry => entry.visitCount) != expectedVisits
            || entries.Sum(entry => entry.operatingCost) != expectedOperatingCost)
        {
            report.AddError($"{label} totals do not match the operating-day totals.");
        }

        Dictionary<string, int> projectedRevenue = entries
            .Where(entry => entry.revenue > 0)
            .GroupBy(entry => entry.facilityName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.revenue),
                StringComparer.Ordinal);
        Dictionary<string, int> expectedRevenueMap = expectedRevenueByName?
            .Where(entry => entry != null
                && !string.IsNullOrWhiteSpace(entry.key))
            .GroupBy(entry => entry.key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().value,
                StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (!DictionaryEquals(projectedRevenue, expectedRevenueMap))
        {
            report.AddError($"{label} revenue projection does not match.");
        }

        Dictionary<int, int> projectedStock = entries
            .SelectMany(entry => entry.consumedStock)
            .GroupBy(entry => (int)entry.category)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(entry => entry.amount));
        Dictionary<int, int> expectedStockMap = expectedStock?
            .Where(entry => entry != null)
            .GroupBy(entry => (int)entry.category)
            .ToDictionary(group => group.Key, group => group.Last().amount)
            ?? new Dictionary<int, int>();
        if (!DictionaryEquals(projectedStock, expectedStockMap))
        {
            report.AddError($"{label} stock projection does not match.");
        }

        if (expectedMoodSamples != null)
        {
            float[] projectedMood = entries
                .SelectMany(entry => entry.visitorMoodSamples)
                .OrderBy(value => value)
                .ToArray();
            float[] expectedMood = expectedMoodSamples
                .OrderBy(value => value)
                .ToArray();
            if (projectedMood.Length != expectedMood.Length
                || projectedMood.Where((value, index) =>
                    Math.Abs(value - expectedMood[index]) > 0.0001f).Any())
            {
                report.AddError($"{label} mood projection does not match.");
            }
        }
    }

    private static void ValidateFacilitySatisfaction(
        IReadOnlyList<DungeonOperatingDayFacilityDetailSaveData> entries,
        float expectedAverage,
        string prefix,
        DungeonGameRestoreReport report)
    {
        if (entries == null || entries.Any(entry => entry == null))
        {
            return;
        }
        float[] samples = entries
            .SelectMany(entry => entry.visitorMoodSamples
                ?? Enumerable.Empty<float>())
            .ToArray();
        float actual = samples.Length == 0 ? 0f : samples.Average();
        if (Math.Abs(actual - expectedAverage) > 0.0001f)
        {
            report.AddError($"{prefix} facility satisfaction does not match.");
        }
    }

    private static bool DictionaryEquals<TKey>(
        IReadOnlyDictionary<TKey, int> left,
        IReadOnlyDictionary<TKey, int> right)
    {
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out int value)
                && value == pair.Value);
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
            facilityDetails = state.FacilityDetails
                .Select(ToSaveData)
                .ToList(),
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
            source.facilityDetails.Select(FromSaveData).ToList(),
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
            terminatedContractCost = source.terminatedContractCost,
            closingBalance = source.closingBalance,
            consecutiveShortfallDays = source.consecutiveShortfallDays,
            restockFailureCount = source.restockFailureCount,
            facilityRevenues = source.facilityRevenues.Select(item =>
                new DungeonStringIntSaveEntry { key = item.facilityName, value = item.revenue }).ToList(),
            facilityDetails = source.facilityDetails
                .Select(ToSaveData)
                .ToList(),
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
                storedMassGrams = warehouse.storedMassGrams,
                maxMassGrams = warehouse.maxMassGrams,
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
                    itemId = offer.itemId,
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
                }).ToList(),
            employmentSettlements = source.employmentSettlements
                .Select(ToSaveData)
                .ToList()
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
                    warehouse.storedMassGrams,
                    warehouse.maxMassGrams,
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
                    offer.itemId,
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
            consecutiveShortfallDays: source.consecutiveShortfallDays,
            facilityDetails: source.facilityDetails
                .Select(FromSaveData)
                .ToList(),
            terminatedContractCost: source.terminatedContractCost,
            employmentSettlements: source.employmentSettlements
                .Select(FromSaveData)
                .ToList());
    }

    private static DungeonEmploymentSettlementLineSaveData ToSaveData(
        OperatingDayEmploymentSettlementLine source) => new()
    {
        day = source.day,
        characterId = source.characterId,
        contractKind = source.contractKind,
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

    private static OperatingDayEmploymentSettlementLine FromSaveData(
        DungeonEmploymentSettlementLineSaveData source) => new()
    {
        day = source.day,
        characterId = source.characterId,
        contractKind = source.contractKind,
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

    private static DungeonOperatingDayFacilityDetailSaveData ToSaveData(
        OperatingDayFacilityLedgerEntry source)
    {
        return new DungeonOperatingDayFacilityDetailSaveData
        {
            facilityId = source.FacilityId,
            facilityName = source.FacilityName,
            revenue = source.Revenue,
            visitCount = source.VisitCount,
            operatingCost = source.OperatingCost,
            consumedStock = source.ConsumedStock
                .OrderBy(pair => pair.Key)
                .Select(pair => new DungeonStockAmountSaveData
                {
                    category = (StockCategory)pair.Key,
                    amount = pair.Value
                })
                .ToList(),
            visitorMoodSamples = source.VisitorMoodSamples.ToList()
        };
    }

    private static OperatingDayFacilityLedgerEntry FromSaveData(
        DungeonOperatingDayFacilityDetailSaveData source)
    {
        return new OperatingDayFacilityLedgerEntry(
            source.facilityId,
            source.facilityName,
            source.revenue,
            source.visitCount,
            source.operatingCost,
            source.consumedStock.ToDictionary(
                entry => (int)entry.category,
                entry => entry.amount),
            source.visitorMoodSamples);
    }
}

}
