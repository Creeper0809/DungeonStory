using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DungeonStory.Foundation;

namespace DungeonStory.Operation
{
public sealed class OperatingDayLedgerSnapshot<TSupplyResult>
{
    internal OperatingDayLedgerSnapshot(
        IReadOnlyDictionary<string, int> facilityRevenue,
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
        FacilityRevenue = CopyDictionary(facilityRevenue);
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
        IReadOnlyDictionary<string, int> facilityRevenue,
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
            facilityRevenue ?? throw new ArgumentNullException(nameof(facilityRevenue)),
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
        int closingBalance)
    {
        OpeningBalance = Math.Max(0, openingBalance);
        MaintenanceCost = Math.Max(0, maintenanceCost);
        MaintenancePaid = Math.Max(0, maintenancePaid);
        EmployeeWagesDue = Math.Max(0, employeeWagesDue);
        MercenaryFeesDue = Math.Max(0, mercenaryFeesDue);
        EmployeeWagesPaid = Math.Max(0, employeeWagesPaid);
        MercenaryFeesPaid = Math.Max(0, mercenaryFeesPaid);
        UnpaidEmployeeWages = Math.Max(0, unpaidEmployeeWages);
        ClosingBalance = Math.Max(0, closingBalance);
    }

    public int OpeningBalance { get; }
    public int MaintenanceCost { get; }
    public int MaintenancePaid { get; }
    public int EmployeeWagesDue { get; }
    public int MercenaryFeesDue { get; }
    public int EmployeeWagesPaid { get; }
    public int MercenaryFeesPaid { get; }
    public int UnpaidEmployeeWages { get; }
    public int ClosingBalance { get; }
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
        int closingBalance,
        int consecutiveShortfallDays)
    {
        OpeningBalance = openingBalance;
        MaintenanceCost = maintenanceCost;
        PayrollCost = payrollCost;
        PreviousDebt = previousDebt;
        PaidAmount = paidAmount;
        CarriedDebt = carriedDebt;
        ClosingBalance = closingBalance;
        ConsecutiveShortfallDays = consecutiveShortfallDays;
    }

    public int OpeningBalance { get; }
    public int MaintenanceCost { get; }
    public int PayrollCost { get; }
    public int PreviousDebt { get; }
    public int PaidAmount { get; }
    public int CarriedDebt { get; }
    public int ClosingBalance { get; }
    public int ConsecutiveShortfallDays { get; }
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
    internal Dictionary<string, int> FacilityRevenue { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
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
        Copy(FacilityRevenue, clone.FacilityRevenue);
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
        CopyPositive(source.Ledger.FacilityRevenue, restored.FacilityRevenue);
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

    public void RecordVisit(string speciesTag, float? mood)
    {
        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        state.TotalVisits++;
        string key = string.IsNullOrWhiteSpace(speciesTag)
            ? "Unknown"
            : speciesTag;
        Increment(state.SpeciesVisits, key, 1);
        if (mood.HasValue)
        {
            state.VisitorMoodSamples.Add(ClampMood(mood.Value));
        }
    }

    public void RecordRevenue(string facilityName, int revenue)
    {
        int normalized = Math.Max(0, revenue);
        if (normalized == 0)
        {
            return;
        }

        OperatingDaySettlementAggregateState<TReport, TSupplyResult> state =
            WritableForEvent();
        state.TotalRevenue += normalized;
        Increment(
            state.FacilityRevenue,
            string.IsNullOrWhiteSpace(facilityName) ? "Unknown" : facilityName,
            normalized);
    }

    public void RecordStockConsumed(int category, int amount)
    {
        int normalized = Math.Max(0, amount);
        if (normalized == 0)
        {
            return;
        }

        Increment(WritableForEvent().ConsumedStock, category, normalized);
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
        if (normalizedDay <= state.LastSettledDay
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
            economy.ClosingBalance,
            shortfallDays);
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
            state.FacilityRevenue,
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
            state.FacilityRevenue,
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
        state.FacilityRevenue.Clear();
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
