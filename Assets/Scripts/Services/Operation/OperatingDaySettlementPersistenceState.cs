using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public sealed class OperatingDaySettlementPersistenceState
{
    public OperatingDaySettlementPersistenceState(
        int currentDay,
        int totalRevenue,
        int totalVisits,
        int restockFailureCount,
        IReadOnlyDictionary<string, int> facilityRevenue,
        IReadOnlyDictionary<string, int> speciesVisits,
        IReadOnlyDictionary<StockCategory, int> consumedStock,
        IReadOnlyList<float> visitorMoodSamples,
        IReadOnlyList<StockSupplyResult> stockSupplyResults,
        IReadOnlyList<string> incidents,
        IReadOnlyList<string> eventLog,
        IReadOnlyList<OperatingDayReport> reportHistory,
        int outstandingDebt = 0,
        int consecutiveShortfallDays = 0,
        bool emergencyFundingUsed = false)
    {
        CurrentDay = Math.Max(1, currentDay);
        TotalRevenue = Math.Max(0, totalRevenue);
        TotalVisits = Math.Max(0, totalVisits);
        RestockFailureCount = Math.Max(0, restockFailureCount);
        FacilityRevenue = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(facilityRevenue ?? new Dictionary<string, int>()));
        SpeciesVisits = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(speciesVisits ?? new Dictionary<string, int>()));
        ConsumedStock = new ReadOnlyDictionary<StockCategory, int>(
            new Dictionary<StockCategory, int>(consumedStock ?? new Dictionary<StockCategory, int>()));
        VisitorMoodSamples = Array.AsReadOnly(visitorMoodSamples?.ToArray() ?? Array.Empty<float>());
        StockSupplyResults = Array.AsReadOnly(stockSupplyResults?.ToArray() ?? Array.Empty<StockSupplyResult>());
        Incidents = Array.AsReadOnly(incidents?.Where(value => value != null).ToArray() ?? Array.Empty<string>());
        EventLog = Array.AsReadOnly(eventLog?.Where(value => value != null).ToArray() ?? Array.Empty<string>());
        ReportHistory = Array.AsReadOnly(reportHistory?.Where(report => report != null).ToArray()
            ?? Array.Empty<OperatingDayReport>());
        OutstandingDebt = Math.Max(0, outstandingDebt);
        ConsecutiveShortfallDays = Math.Max(0, consecutiveShortfallDays);
        EmergencyFundingUsed = emergencyFundingUsed;
    }

    public int CurrentDay { get; }
    public int TotalRevenue { get; }
    public int TotalVisits { get; }
    public int RestockFailureCount { get; }
    public IReadOnlyDictionary<string, int> FacilityRevenue { get; }
    public IReadOnlyDictionary<string, int> SpeciesVisits { get; }
    public IReadOnlyDictionary<StockCategory, int> ConsumedStock { get; }
    public IReadOnlyList<float> VisitorMoodSamples { get; }
    public IReadOnlyList<StockSupplyResult> StockSupplyResults { get; }
    public IReadOnlyList<string> Incidents { get; }
    public IReadOnlyList<string> EventLog { get; }
    public IReadOnlyList<OperatingDayReport> ReportHistory { get; }
    public int OutstandingDebt { get; }
    public int ConsecutiveShortfallDays { get; }
    public bool EmergencyFundingUsed { get; }
}
