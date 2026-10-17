using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DungeonStory.Foundation;

namespace DungeonStory.Operation
{
public sealed class OperatingDayFacilityLedgerEntry
{
    public OperatingDayFacilityLedgerEntry(
        string facilityId,
        string facilityName,
        int revenue,
        int visitCount,
        int operatingCost,
        IReadOnlyDictionary<int, int> consumedStock,
        IReadOnlyList<float> visitorMoodSamples)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException(
                "Facility identity is required.",
                nameof(facilityId));
        }
        FacilityId = facilityId.Trim();
        FacilityName = string.IsNullOrWhiteSpace(facilityName)
            ? FacilityId
            : facilityName.Trim();
        Revenue = Math.Max(0, revenue);
        VisitCount = Math.Max(0, visitCount);
        OperatingCost = Math.Max(0, operatingCost);
        ConsumedStock = new ReadOnlyDictionary<int, int>(
            new Dictionary<int, int>(consumedStock
                ?? throw new ArgumentNullException(nameof(consumedStock))));
        VisitorMoodSamples = Array.AsReadOnly(
            (visitorMoodSamples
             ?? throw new ArgumentNullException(nameof(visitorMoodSamples)))
            .Select(ClampMood)
            .ToArray());
    }

    public string FacilityId { get; }
    public string FacilityName { get; }
    public int Revenue { get; }
    public int VisitCount { get; }
    public int OperatingCost { get; }
    public IReadOnlyDictionary<int, int> ConsumedStock { get; }
    public IReadOnlyList<float> VisitorMoodSamples { get; }
    public float AverageSatisfaction => VisitorMoodSamples.Count == 0
        ? 0f
        : VisitorMoodSamples.Average();

    private static float ClampMood(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }
        return Math.Min(100f, Math.Max(0f, value));
    }
}

public sealed class OperatingDayLedgerSnapshot<TSupplyResult>
{
    internal OperatingDayLedgerSnapshot(
        IReadOnlyList<OperatingDayFacilityLedgerEntry> facilityDetails,
        IReadOnlyDictionary<string, int> speciesVisits,
        IReadOnlyDictionary<int, int> consumedStock,
        IReadOnlyList<float> visitorMoodSamples,
        IReadOnlyList<TSupplyResult> stockSupplyResults,
        IReadOnlyList<string> incidents,
        IReadOnlyList<string> eventLog,
        int totalRevenue,
        int totalVisits,
        int restockFailureCount)
    {
        FacilityDetails = Array.AsReadOnly(
            (facilityDetails
             ?? throw new ArgumentNullException(nameof(facilityDetails)))
            .ToArray());
        FacilityRevenue = CopyDictionary(
            FacilityDetails
                .Where(value => value.Revenue > 0)
                .GroupBy(value => value.FacilityName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(value => value.Revenue),
                    StringComparer.Ordinal));
        SpeciesVisits = CopyDictionary(speciesVisits);
        ConsumedStock = CopyDictionary(consumedStock);
        VisitorMoodSamples = Array.AsReadOnly(visitorMoodSamples.ToArray());
        StockSupplyResults = Array.AsReadOnly(stockSupplyResults.ToArray());
        Incidents = Array.AsReadOnly(incidents.ToArray());
        EventLog = Array.AsReadOnly(eventLog.ToArray());
        TotalRevenue = totalRevenue;
        TotalVisits = totalVisits;
        RestockFailureCount = restockFailureCount;
    }

    public IReadOnlyDictionary<string, int> FacilityRevenue { get; }
    public IReadOnlyList<OperatingDayFacilityLedgerEntry> FacilityDetails { get; }
    public IReadOnlyDictionary<string, int> SpeciesVisits { get; }
    public IReadOnlyDictionary<int, int> ConsumedStock { get; }
    public IReadOnlyList<float> VisitorMoodSamples { get; }
    public IReadOnlyList<TSupplyResult> StockSupplyResults { get; }
    public IReadOnlyList<string> Incidents { get; }
    public IReadOnlyList<string> EventLog { get; }
    public int TotalRevenue { get; }
    public int TotalVisits { get; }
    public int RestockFailureCount { get; }

    public float AverageSatisfaction => VisitorMoodSamples.Count == 0
        ? 0f
        : VisitorMoodSamples.Average();

    private static IReadOnlyDictionary<TKey, int> CopyDictionary<TKey>(
        IReadOnlyDictionary<TKey, int> source)
    {
        return new ReadOnlyDictionary<TKey, int>(
            new Dictionary<TKey, int>(source));
    }
}

public sealed class OperatingDaySettlementStateSnapshot<TReport, TSupplyResult>
    where TReport : class
{
    public OperatingDaySettlementStateSnapshot(
        int currentDay,
        IReadOnlyList<OperatingDayFacilityLedgerEntry> facilityDetails,
        IReadOnlyDictionary<string, int> speciesVisits,
        IReadOnlyDictionary<int, int> consumedStock,
        IReadOnlyList<float> visitorMoodSamples,
        IReadOnlyList<TSupplyResult> stockSupplyResults,
        IReadOnlyList<string> incidents,
        IReadOnlyList<string> eventLog,
        IReadOnlyList<TReport> reportHistory,
        int totalRevenue,
        int totalVisits,
        int restockFailureCount,
        int outstandingDebt,
        int consecutiveShortfallDays,
        bool emergencyFundingUsed,
        int lastSettledDay)
    {
        Ledger = new OperatingDayLedgerSnapshot<TSupplyResult>(
            facilityDetails ?? throw new ArgumentNullException(nameof(facilityDetails)),
            speciesVisits ?? throw new ArgumentNullException(nameof(speciesVisits)),
            consumedStock ?? throw new ArgumentNullException(nameof(consumedStock)),
            visitorMoodSamples ?? throw new ArgumentNullException(nameof(visitorMoodSamples)),
            stockSupplyResults ?? throw new ArgumentNullException(nameof(stockSupplyResults)),
            incidents ?? throw new ArgumentNullException(nameof(incidents)),
            eventLog ?? throw new ArgumentNullException(nameof(eventLog)),
            Math.Max(0, totalRevenue),
            Math.Max(0, totalVisits),
            Math.Max(0, restockFailureCount));
        CurrentDay = Math.Max(1, currentDay);
        ReportHistory = Array.AsReadOnly(
            (reportHistory ?? throw new ArgumentNullException(nameof(reportHistory)))
            .Where(report => report != null)
            .Take(OperatingDaySettlementDomain<TReport, TSupplyResult>.MaxReportHistory)
            .ToArray());
        OutstandingDebt = Math.Max(0, outstandingDebt);
        ConsecutiveShortfallDays = Math.Max(0, consecutiveShortfallDays);
        EmergencyFundingUsed = emergencyFundingUsed;
        LastSettledDay = Math.Max(0, lastSettledDay);
    }

    public int CurrentDay { get; }
    public OperatingDayLedgerSnapshot<TSupplyResult> Ledger { get; }
    public IReadOnlyList<TReport> ReportHistory { get; }
    public int OutstandingDebt { get; }
    public int ConsecutiveShortfallDays { get; }
    public bool EmergencyFundingUsed { get; }
    public int LastSettledDay { get; }
}

public readonly struct OperatingDaySettlementRequest<TSupplyResult>
{
    internal OperatingDaySettlementRequest(
        long token,
        int day,
        int previousDebt,
        int previousShortfallDays,
        OperatingDayLedgerSnapshot<TSupplyResult> ledger)
    {
        Token = token;
        Day = day;
        PreviousDebt = previousDebt;
        PreviousShortfallDays = previousShortfallDays;
        Ledger = ledger;
    }

    internal long Token { get; }
    public int Day { get; }
    public int PreviousDebt { get; }
    public int PreviousShortfallDays { get; }
    public OperatingDayLedgerSnapshot<TSupplyResult> Ledger { get; }
}

public enum OperatingDayEmploymentContractKind
{
    Founder = 0,
    Employee = 1,
    Mercenary = 2
}

[Serializable]
public sealed class OperatingDayEmploymentSettlementLine
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

    public OperatingDayEmploymentSettlementLine Clone() => new()
    {
        day = day,
        characterId = characterId ?? string.Empty,
        contractKind = contractKind,
        due = due,
        paid = paid,
        unpaidDebtBefore = unpaidDebtBefore,
        unpaidDebtAfter = unpaidDebtAfter,
        moodPenaltyApplied = moodPenaltyApplied,
        contractEnded = contractEnded,
        departureAttempted = departureAttempted,
        departureStarted = departureStarted,
        departurePending = departurePending,
        departureFailureReason = departureFailureReason ?? string.Empty,
        financeSourceId = financeSourceId ?? string.Empty,
        financeTransactionId = financeTransactionId ?? string.Empty,
        financeSucceeded = financeSucceeded
    };
}

public readonly struct OperatingDayEconomyApplication
{
    public OperatingDayEconomyApplication(
        int openingBalance,
        int maintenanceCost,
        int maintenancePaid,
        int employeeWagesDue,
        int mercenaryFeesDue,
        int employeeWagesPaid,
        int mercenaryFeesPaid,
        int unpaidEmployeeWages,
        int unpaidMercenaryFees,
        int closingBalance,
        IReadOnlyList<OperatingDayEmploymentSettlementLine> employmentLines)
    {
        OpeningBalance = Math.Max(0, openingBalance);
        MaintenanceCost = Math.Max(0, maintenanceCost);
        MaintenancePaid = Math.Max(0, maintenancePaid);
        EmployeeWagesDue = Math.Max(0, employeeWagesDue);
        MercenaryFeesDue = Math.Max(0, mercenaryFeesDue);
        EmployeeWagesPaid = Math.Max(0, employeeWagesPaid);
        MercenaryFeesPaid = Math.Max(0, mercenaryFeesPaid);
        UnpaidEmployeeWages = Math.Max(0, unpaidEmployeeWages);
        UnpaidMercenaryFees = Math.Max(0, unpaidMercenaryFees);
        ClosingBalance = Math.Max(0, closingBalance);
        EmploymentLines = Array.AsReadOnly(
            (employmentLines
             ?? throw new ArgumentNullException(nameof(employmentLines)))
            .Select(value => value?.Clone()
                ?? throw new ArgumentException(
                    "Employment settlement lines cannot contain null entries.",
                    nameof(employmentLines)))
            .ToArray());
    }

    public int OpeningBalance { get; }
    public int MaintenanceCost { get; }
    public int MaintenancePaid { get; }
    public int EmployeeWagesDue { get; }
    public int MercenaryFeesDue { get; }
    public int EmployeeWagesPaid { get; }
    public int MercenaryFeesPaid { get; }
    public int UnpaidEmployeeWages { get; }
    public int UnpaidMercenaryFees { get; }
    public int ClosingBalance { get; }
    public IReadOnlyList<OperatingDayEmploymentSettlementLine> EmploymentLines { get; }
}

public readonly struct OperatingDayCostTransition
{
    internal OperatingDayCostTransition(
        int openingBalance,
        int maintenanceCost,
        int payrollCost,
        int previousDebt,
        int paidAmount,
        int carriedDebt,
        int terminatedContractAmount,
        int closingBalance,
        int consecutiveShortfallDays,
        IReadOnlyList<OperatingDayEmploymentSettlementLine> employmentLines)
    {
        OpeningBalance = openingBalance;
        MaintenanceCost = maintenanceCost;
        PayrollCost = payrollCost;
        PreviousDebt = previousDebt;
        PaidAmount = paidAmount;
        CarriedDebt = carriedDebt;
        TerminatedContractAmount = terminatedContractAmount;
        ClosingBalance = closingBalance;
        ConsecutiveShortfallDays = consecutiveShortfallDays;
        EmploymentLines = Array.AsReadOnly(
            (employmentLines
             ?? throw new ArgumentNullException(nameof(employmentLines)))
            .Select(value => value.Clone())
            .ToArray());
    }

    public int OpeningBalance { get; }
    public int MaintenanceCost { get; }
    public int PayrollCost { get; }
    public int PreviousDebt { get; }
    public int PaidAmount { get; }
    public int CarriedDebt { get; }
    public int TerminatedContractAmount { get; }
    public int ClosingBalance { get; }
    public int ConsecutiveShortfallDays { get; }
    public IReadOnlyList<OperatingDayEmploymentSettlementLine> EmploymentLines { get; }
    public bool HasWageShortfall => CarriedDebt > 0;
}

public readonly struct OperatingDaySettlementEffect<TReport>
    where TReport : class
{
    internal OperatingDaySettlementEffect(
        TReport report,
        OperatingDayCostTransition costs)
    {
        Report = report;
        Costs = costs;
    }

    public TReport Report { get; }
    public OperatingDayCostTransition Costs { get; }
    public bool ShouldRaiseWageAlert => Costs.HasWageShortfall;
}

public sealed class OperatingDaySettlementAggregateState<TReport, TSupplyResult>
    where TReport : class
{
    internal Dictionary<string, OperatingDayFacilityLedgerMutable>
        FacilityDetails { get; } =
            new Dictionary<string, OperatingDayFacilityLedgerMutable>(
                StringComparer.Ordinal);
    internal Dictionary<string, int> SpeciesVisits { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    internal Dictionary<int, int> ConsumedStock { get; } =
        new Dictionary<int, int>();
    internal List<float> VisitorMoodSamples { get; } = new List<float>();
    internal List<TSupplyResult> StockSupplyResults { get; } =
        new List<TSupplyResult>();
    internal List<string> Incidents { get; } = new List<string>();
    internal List<string> EventLog { get; } = new List<string>();
    internal List<TReport> ReportHistory { get; } = new List<TReport>();
    internal int TotalRevenue { get; set; }
    internal int TotalVisits { get; set; }
    internal int RestockFailureCount { get; set; }
    internal int CurrentDay { get; set; } = 1;
    internal int OutstandingDebt { get; set; }
    internal int ConsecutiveShortfallDays { get; set; }
    internal bool EmergencyFundingUsed { get; set; }
    internal int LastSettledDay { get; set; }
    internal long NextSettlementToken { get; set; } = 1;
    internal long PendingSettlementToken { get; set; }
    internal int PendingSettlementDay { get; set; }

    internal OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        DeepClone()
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> clone =
            new OperatingDaySettlementAggregateState<TReport, TSupplyResult>
            {
                TotalRevenue = TotalRevenue,
                TotalVisits = TotalVisits,
                RestockFailureCount = RestockFailureCount,
                CurrentDay = CurrentDay,
                OutstandingDebt = OutstandingDebt,
                ConsecutiveShortfallDays = ConsecutiveShortfallDays,
                EmergencyFundingUsed = EmergencyFundingUsed,
                LastSettledDay = LastSettledDay,
                NextSettlementToken = NextSettlementToken,
                PendingSettlementToken = PendingSettlementToken,
                PendingSettlementDay = PendingSettlementDay
            };
        foreach (KeyValuePair<string, OperatingDayFacilityLedgerMutable> pair
                 in FacilityDetails)
        {
            clone.FacilityDetails.Add(pair.Key, pair.Value.DeepClone());
        }
        Copy(SpeciesVisits, clone.SpeciesVisits);
        Copy(ConsumedStock, clone.ConsumedStock);
        clone.VisitorMoodSamples.AddRange(VisitorMoodSamples);
        clone.StockSupplyResults.AddRange(StockSupplyResults);
        clone.Incidents.AddRange(Incidents);
        clone.EventLog.AddRange(EventLog);
        clone.ReportHistory.AddRange(ReportHistory);
        return clone;
    }

    private static void Copy<TKey>(
        IReadOnlyDictionary<TKey, int> source,
        IDictionary<TKey, int> destination)
    {
        foreach (KeyValuePair<TKey, int> pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }
}

internal sealed class OperatingDayFacilityLedgerMutable
{
    internal string FacilityId { get; set; } = string.Empty;
    internal string FacilityName { get; set; } = string.Empty;
    internal int Revenue { get; set; }
    internal int VisitCount { get; set; }
    internal int OperatingCost { get; set; }
    internal Dictionary<int, int> ConsumedStock { get; } = new();
    internal List<float> VisitorMoodSamples { get; } = new();

    internal OperatingDayFacilityLedgerMutable DeepClone()
    {
        OperatingDayFacilityLedgerMutable clone = new()
        {
            FacilityId = FacilityId,
            FacilityName = FacilityName,
            Revenue = Revenue,
            VisitCount = VisitCount,
            OperatingCost = OperatingCost
        };
        foreach (KeyValuePair<int, int> pair in ConsumedStock)
        {
            clone.ConsumedStock.Add(pair.Key, pair.Value);
        }
        clone.VisitorMoodSamples.AddRange(VisitorMoodSamples);
        return clone;
    }

    internal OperatingDayFacilityLedgerEntry Capture() => new(
        FacilityId,
        FacilityName,
        Revenue,
        VisitCount,
        OperatingCost,
        ConsumedStock,
        VisitorMoodSamples);

    internal static OperatingDayFacilityLedgerMutable Restore(
        OperatingDayFacilityLedgerEntry source)
    {
        OperatingDayFacilityLedgerMutable restored = new()
        {
            FacilityId = source.FacilityId,
            FacilityName = source.FacilityName,
            Revenue = source.Revenue,
            VisitCount = source.VisitCount,
            OperatingCost = source.OperatingCost
        };
        foreach (KeyValuePair<int, int> pair in source.ConsumedStock)
        {
            if (pair.Value > 0)
            {
                restored.ConsumedStock.Add(pair.Key, pair.Value);
            }
        }
        restored.VisitorMoodSamples.AddRange(source.VisitorMoodSamples);
        return restored;
    }
}

public sealed class OperatingDaySettlementDomain<TReport, TSupplyResult>
    where TReport : class
{
    public const int MaxReportHistory = 20;

    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private OperatingDaySettlementDomain(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public static OperatingDaySettlementDomain<TReport, TSupplyResult> Attach(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        return new OperatingDaySettlementDomain<TReport, TSupplyResult>(
            aggregateRootStore);
    }

    public int CurrentDay => Current.CurrentDay;
    public int CurrentRevenue => Current.TotalRevenue;
    public int CurrentVisits => Current.TotalVisits;
    public int CurrentRestockFailureCount => Current.RestockFailureCount;
    public int CurrentConsumedStock => Current.ConsumedStock.Values.Sum();
    public int CurrentIncidentCount => Current.Incidents.Count;
    public int CurrentEventCount => Current.EventLog.Count;
    public int OutstandingDebt => Current.OutstandingDebt;
    public int ConsecutiveShortfallDays => Current.ConsecutiveShortfallDays;
    public bool EmergencyFundingUsed => Current.EmergencyFundingUsed;
    public bool HasSettledDay(int day) => Math.Max(1, day) <= Current.LastSettledDay;
    public TReport LatestReport => Current.ReportHistory.FirstOrDefault();
    public IReadOnlyList<TReport> ReportHistory =>
        Array.AsReadOnly(Current.ReportHistory.ToArray());
    public float CurrentAverageSatisfaction =>
        Current.VisitorMoodSamples.Count == 0
            ? 0f
            : Current.VisitorMoodSamples.Average();

    public OperatingDaySettlementStateSnapshot<TReport, TSupplyResult>
        Capture()
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Current;
        return CreateSnapshot(state);
    }

    public OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        CaptureOwnerState()
    {
        return Current.DeepClone();
    }

    public void RestoreOwnerState(
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> restored)
    {
        aggregateRootStore.Replace(
            (restored ?? throw new ArgumentNullException(nameof(restored)))
            .DeepClone());
    }

    public OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        PrepareRestoreState(
            OperatingDaySettlementStateSnapshot<TReport, TSupplyResult> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        OperatingDaySettlementAggregateState<TReport, TSupplyResult> restored =
            new OperatingDaySettlementAggregateState<TReport, TSupplyResult>
            {
                CurrentDay = Math.Max(1, source.CurrentDay),
                TotalRevenue = Math.Max(0, source.Ledger.TotalRevenue),
                TotalVisits = Math.Max(0, source.Ledger.TotalVisits),
                RestockFailureCount = Math.Max(
                    0,
                    source.Ledger.RestockFailureCount),
                OutstandingDebt = Math.Max(0, source.OutstandingDebt),
                ConsecutiveShortfallDays = Math.Max(
                    0,
                    source.ConsecutiveShortfallDays),
                EmergencyFundingUsed = source.EmergencyFundingUsed,
                LastSettledDay = Math.Max(0, source.LastSettledDay)
            };
        foreach (OperatingDayFacilityLedgerEntry detail in
                 source.Ledger.FacilityDetails)
        {
            if (detail == null
                || restored.FacilityDetails.ContainsKey(detail.FacilityId))
            {
                throw new InvalidOperationException(
                    "Operating-day facility details contain a null or duplicate identity.");
            }
            restored.FacilityDetails.Add(
                detail.FacilityId,
                OperatingDayFacilityLedgerMutable.Restore(detail));
        }
        CopyPositive(source.Ledger.SpeciesVisits, restored.SpeciesVisits);
        CopyPositive(source.Ledger.ConsumedStock, restored.ConsumedStock);
        restored.VisitorMoodSamples.AddRange(
            source.Ledger.VisitorMoodSamples.Select(ClampMood));
        restored.StockSupplyResults.AddRange(source.Ledger.StockSupplyResults);
        restored.Incidents.AddRange(
            source.Ledger.Incidents.Where(value =>
                !string.IsNullOrWhiteSpace(value)));
        restored.EventLog.AddRange(
            source.Ledger.EventLog.Where(value =>
                !string.IsNullOrWhiteSpace(value)));
        restored.ReportHistory.AddRange(
            source.ReportHistory.Where(report => report != null)
                .Take(MaxReportHistory));
        return restored;
    }

    public void BeginDay(int day)
    {
        int normalizedDay = Math.Max(1, day);
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Writable;
        if (normalizedDay <= state.LastSettledDay
            || (normalizedDay == state.CurrentDay
                && state.PendingSettlementToken == 0))
        {
            return;
        }

        state.CurrentDay = normalizedDay;
        state.PendingSettlementToken = 0;
        state.PendingSettlementDay = 0;
        ResetLedger(state);
    }

    public void RecordVisit(
        string facilityId,
        string facilityName,
        string speciesTag,
        float? mood)
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        OperatingDayFacilityLedgerMutable facility = GetOrCreateFacility(
            state,
            facilityId,
            facilityName);
        state.TotalVisits++;
        facility.VisitCount++;
        string key = string.IsNullOrWhiteSpace(speciesTag)
            ? "Unknown"
            : speciesTag;
        Increment(state.SpeciesVisits, key, 1);
        if (mood.HasValue)
        {
            float normalizedMood = ClampMood(mood.Value);
            state.VisitorMoodSamples.Add(normalizedMood);
            facility.VisitorMoodSamples.Add(normalizedMood);
        }
    }

    public void RecordRevenue(
        string facilityId,
        string facilityName,
        int revenue)
    {
        int normalized = Math.Max(0, revenue);
        if (normalized == 0)
        {
            return;
        }

        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        state.TotalRevenue += normalized;
        GetOrCreateFacility(state, facilityId, facilityName).Revenue += normalized;
    }

    public void RecordStockConsumed(
        string facilityId,
        string facilityName,
        int category,
        int amount)
    {
        int normalized = Math.Max(0, amount);
        if (normalized == 0)
        {
            return;
        }

        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        Increment(state.ConsumedStock, category, normalized);
        Increment(
            GetOrCreateFacility(state, facilityId, facilityName).ConsumedStock,
            category,
            normalized);
    }

    public void RecordFacilityCost(
        string facilityId,
        string facilityName,
        int amount)
    {
        int normalized = Math.Max(0, amount);
        if (normalized == 0)
        {
            return;
        }
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        GetOrCreateFacility(state, facilityId, facilityName).OperatingCost +=
            normalized;
    }

    public void RecordIncident(string detail)
    {
        if (!string.IsNullOrWhiteSpace(detail))
        {
            WritableForEvent().Incidents.Add(detail);
        }
    }

    public void RecordRestockResult(int requestedAmount, int restockedAmount)
    {
        if (requestedAmount > 0 && restockedAmount <= 0)
        {
            WritableForEvent().RestockFailureCount++;
        }
    }

    public void RecordStockSupply(TSupplyResult result)
    {
        WritableForEvent().StockSupplyResults.Add(result);
    }

    public void RecordEventLog(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            WritableForEvent().EventLog.Add(text);
        }
    }

    public bool TryBeginSettlement(
        int day,
        out OperatingDaySettlementRequest<TSupplyResult> request)
    {
        int normalizedDay = Math.Max(1, day);
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Writable;
        if (HasSettledDay(normalizedDay)
            || state.PendingSettlementToken != 0)
        {
            request = default;
            return false;
        }

        state.CurrentDay = normalizedDay;
        long token = state.NextSettlementToken++;
        state.PendingSettlementToken = token;
        state.PendingSettlementDay = normalizedDay;
        request = new OperatingDaySettlementRequest<TSupplyResult>(
            token,
            normalizedDay,
            state.OutstandingDebt,
            state.ConsecutiveShortfallDays,
            CreateLedgerSnapshot(state));
        return true;
    }

    public OperatingDayCostTransition ResolveCostTransition(
        OperatingDaySettlementRequest<TSupplyResult> request,
        OperatingDayEconomyApplication economy)
    {
        RequirePending(request);
        int payrollDue = economy.EmployeeWagesDue
            + economy.MercenaryFeesDue;
        int payrollCost = Math.Max(0, payrollDue - request.PreviousDebt);
        int paidAmount = economy.MaintenancePaid
            + economy.EmployeeWagesPaid
            + economy.MercenaryFeesPaid;
        int carriedDebt = economy.UnpaidEmployeeWages;
        int terminatedContractAmount = economy.UnpaidMercenaryFees;
        int lineDue = economy.EmploymentLines.Sum(value => value.due);
        int linePaid = economy.EmploymentLines.Sum(value => value.paid);
        int lineDebt = economy.EmploymentLines
            .Where(value => value.contractKind
                == OperatingDayEmploymentContractKind.Employee)
            .Sum(value => value.unpaidDebtAfter);
        int lineTerminated = economy.EmploymentLines
            .Where(value => value.contractEnded)
            .Sum(value => value.due - value.paid);
        if (lineDue != payrollDue
            || linePaid != economy.EmployeeWagesPaid
                + economy.MercenaryFeesPaid
            || lineDebt != carriedDebt
            || lineTerminated != terminatedContractAmount
            || payrollDue != linePaid + lineDebt + lineTerminated)
        {
            throw new InvalidOperationException(
                "Operating-day employment totals do not match their finance child lines.");
        }
        int shortfallDays = carriedDebt > 0
            ? request.PreviousShortfallDays + 1
            : 0;
        return new OperatingDayCostTransition(
            economy.OpeningBalance,
            economy.MaintenanceCost,
            payrollCost,
            request.PreviousDebt,
            paidAmount,
            carriedDebt,
            terminatedContractAmount,
            economy.ClosingBalance,
            shortfallDays,
            economy.EmploymentLines);
    }

    public OperatingDaySettlementRequest<TSupplyResult>
        RefreshSettlementRequest(
            OperatingDaySettlementRequest<TSupplyResult> request)
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Current;
        RequirePending(state, request);
        return new OperatingDaySettlementRequest<TSupplyResult>(
            request.Token,
            request.Day,
            request.PreviousDebt,
            request.PreviousShortfallDays,
            CreateLedgerSnapshot(state));
    }

    public OperatingDaySettlementEffect<TReport> CompleteSettlement(
        OperatingDaySettlementRequest<TSupplyResult> request,
        TReport report,
        OperatingDayCostTransition costs)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Writable;
        RequirePending(state, request);
        state.OutstandingDebt = Math.Max(0, costs.CarriedDebt);
        state.ConsecutiveShortfallDays = Math.Max(
            0,
            costs.ConsecutiveShortfallDays);
        state.ReportHistory.Insert(0, report);
        if (state.ReportHistory.Count > MaxReportHistory)
        {
            state.ReportHistory.RemoveRange(
                MaxReportHistory,
                state.ReportHistory.Count - MaxReportHistory);
        }
        state.LastSettledDay = request.Day;
        return new OperatingDaySettlementEffect<TReport>(report, costs);
    }

    public void FinishSettlement(
        OperatingDaySettlementRequest<TSupplyResult> request)
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            Writable;
        RequirePending(state, request);
        state.PendingSettlementToken = 0;
        state.PendingSettlementDay = 0;
        ResetLedger(state);
    }

    private OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        Current => aggregateRootStore.GetOrCreate(
            () => new OperatingDaySettlementAggregateState<
                TReport,
                TSupplyResult>());

    private OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        Writable => aggregateRootStore.GetOrCreateWritable(
            () => new OperatingDaySettlementAggregateState<
                TReport,
                TSupplyResult>(),
            state => state.DeepClone());

    private OperatingDaySettlementAggregateState<TReport, TSupplyResult>
        WritableForEvent()
    {
        return Writable;
    }

    private void RequirePending(
        OperatingDaySettlementRequest<TSupplyResult> request)
    {
        RequirePending(Current, request);
    }

    private static void RequirePending(
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state,
        OperatingDaySettlementRequest<TSupplyResult> request)
    {
        if (request.Token == 0
            || state.PendingSettlementToken != request.Token
            || state.PendingSettlementDay != request.Day)
        {
            throw new InvalidOperationException(
                "Operating-day settlement request is stale or already applied.");
        }
    }

    private static OperatingDaySettlementStateSnapshot<TReport, TSupplyResult>
        CreateSnapshot(
            OperatingDaySettlementAggregateState<TReport, TSupplyResult> state)
    {
        return new OperatingDaySettlementStateSnapshot<TReport, TSupplyResult>(
            state.CurrentDay,
            CaptureFacilityDetails(state),
            state.SpeciesVisits,
            state.ConsumedStock,
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
            state.LastSettledDay);
    }

    private static OperatingDayLedgerSnapshot<TSupplyResult>
        CreateLedgerSnapshot(
            OperatingDaySettlementAggregateState<TReport, TSupplyResult> state)
    {
        return new OperatingDayLedgerSnapshot<TSupplyResult>(
            CaptureFacilityDetails(state),
            state.SpeciesVisits,
            state.ConsumedStock,
            state.VisitorMoodSamples,
            state.StockSupplyResults,
            state.Incidents,
            state.EventLog,
            state.TotalRevenue,
            state.TotalVisits,
            state.RestockFailureCount);
    }

    private static void ResetLedger(
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state)
    {
        state.FacilityDetails.Clear();
        state.SpeciesVisits.Clear();
        state.ConsumedStock.Clear();
        state.VisitorMoodSamples.Clear();
        state.StockSupplyResults.Clear();
        state.Incidents.Clear();
        state.EventLog.Clear();
        state.TotalRevenue = 0;
        state.TotalVisits = 0;
        state.RestockFailureCount = 0;
    }

    private static void Increment<TKey>(
        IDictionary<TKey, int> values,
        TKey key,
        int amount)
    {
        values[key] = values.TryGetValue(key, out int current)
            ? current + amount
            : amount;
    }

    private static OperatingDayFacilityLedgerMutable GetOrCreateFacility(
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state,
        string facilityId,
        string facilityName)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException(
                "Operating-day events require a facility persistent identity.",
                nameof(facilityId));
        }
        string id = facilityId.Trim();
        string name = string.IsNullOrWhiteSpace(facilityName)
            ? id
            : facilityName.Trim();
        if (!state.FacilityDetails.TryGetValue(
                id,
                out OperatingDayFacilityLedgerMutable detail))
        {
            detail = new OperatingDayFacilityLedgerMutable
            {
                FacilityId = id,
                FacilityName = name
            };
            state.FacilityDetails.Add(id, detail);
        }
        else if (!string.Equals(
                     detail.FacilityName,
                     name,
                     StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Facility '{id}' changed display identity within one operating day.");
        }
        return detail;
    }

    private static IReadOnlyList<OperatingDayFacilityLedgerEntry>
        CaptureFacilityDetails(
            OperatingDaySettlementAggregateState<TReport, TSupplyResult> state)
    {
        return Array.AsReadOnly(state.FacilityDetails.Values
            .OrderBy(value => value.FacilityId, StringComparer.Ordinal)
            .Select(value => value.Capture())
            .ToArray());
    }

    private static void CopyPositive<TKey>(
        IReadOnlyDictionary<TKey, int> source,
        IDictionary<TKey, int> destination)
    {
        foreach (KeyValuePair<TKey, int> pair in source)
        {
            if (pair.Value > 0
                && (!(pair.Key is string text)
                    || !string.IsNullOrWhiteSpace(text)))
            {
                destination[pair.Key] = pair.Value;
            }
        }
    }

    private static float ClampMood(float value)
    {
        if (float.IsNaN(value))
        {
            return 0f;
        }
        return Math.Min(100f, Math.Max(0f, value));
    }
}
}
