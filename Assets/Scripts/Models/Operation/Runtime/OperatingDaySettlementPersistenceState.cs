using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonStory.Operation
{
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
        if (facilityRevenue == null) throw new ArgumentNullException(nameof(facilityRevenue));
        if (speciesVisits == null) throw new ArgumentNullException(nameof(speciesVisits));
        if (consumedStock == null) throw new ArgumentNullException(nameof(consumedStock));
        if (visitorMoodSamples == null) throw new ArgumentNullException(nameof(visitorMoodSamples));
        if (stockSupplyResults == null) throw new ArgumentNullException(nameof(stockSupplyResults));
        if (incidents == null) throw new ArgumentNullException(nameof(incidents));
        if (eventLog == null) throw new ArgumentNullException(nameof(eventLog));
        if (reportHistory == null) throw new ArgumentNullException(nameof(reportHistory));

        CurrentDay = Math.Max(1, currentDay);
        TotalRevenue = Math.Max(0, totalRevenue);
        TotalVisits = Math.Max(0, totalVisits);
        RestockFailureCount = Math.Max(0, restockFailureCount);
        FacilityRevenue = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(facilityRevenue));
        SpeciesVisits = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(speciesVisits));
        ConsumedStock = new ReadOnlyDictionary<StockCategory, int>(
            new Dictionary<StockCategory, int>(consumedStock));
        VisitorMoodSamples = Array.AsReadOnly(visitorMoodSamples.ToArray());
        StockSupplyResults = Array.AsReadOnly(stockSupplyResults.ToArray());
        Incidents = Array.AsReadOnly(incidents.ToArray());
        EventLog = Array.AsReadOnly(eventLog.ToArray());
        ReportHistory = Array.AsReadOnly(reportHistory.ToArray());
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

}
