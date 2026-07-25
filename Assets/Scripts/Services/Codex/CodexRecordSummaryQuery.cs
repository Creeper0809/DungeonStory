using System;

public readonly struct CodexRecordSummary
{
    public CodexRecordSummary(
        int monsterEntries,
        int invasionEntries,
        int facilityEntries,
        int eventLogCount,
        bool hasLatestReport,
        int latestReportDay)
    {
        MonsterEntries = monsterEntries;
        InvasionEntries = invasionEntries;
        FacilityEntries = facilityEntries;
        EventLogCount = eventLogCount;
        HasLatestReport = hasLatestReport;
        LatestReportDay = latestReportDay;
    }

    public int MonsterEntries { get; }
    public int InvasionEntries { get; }
    public int FacilityEntries { get; }
    public int EventLogCount { get; }
    public bool HasLatestReport { get; }
    public int LatestReportDay { get; }
}

public interface ICodexRecordSummaryService
{
    CodexRecordSummary Capture();
}

public sealed class CodexRecordSummaryService : ICodexRecordSummaryService
{
    private readonly ICodexRuntimeProvider codexProvider;
    private readonly IOperatingDaySettlementRuntimeProvider settlementProvider;
    private readonly IEventAlertRuntimeProvider alertProvider;

    public CodexRecordSummaryService(
        ICodexRuntimeProvider codexProvider,
        IOperatingDaySettlementRuntimeProvider settlementProvider,
        IEventAlertRuntimeProvider alertProvider)
    {
        this.codexProvider = codexProvider ?? throw new ArgumentNullException(nameof(codexProvider));
        this.settlementProvider = settlementProvider
            ?? throw new ArgumentNullException(nameof(settlementProvider));
        this.alertProvider = alertProvider ?? throw new ArgumentNullException(nameof(alertProvider));
    }

    public CodexRecordSummary Capture()
    {
        codexProvider.TryGetRuntime(out CodexRuntime codex);
        settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement);
        alertProvider.TryGetRuntime(out EventAlertRuntime alerts);
        OperatingDayReport latestReport = settlement != null ? settlement.LatestReport : null;

        return new CodexRecordSummary(
            codex != null ? codex.GetEntries(CodexEntryCategory.Monster).Count : 0,
            codex != null ? codex.GetEntries(CodexEntryCategory.Invasion).Count : 0,
            codex != null ? codex.GetEntries(CodexEntryCategory.Facility).Count : 0,
            alerts != null ? alerts.EventLog.Count : 0,
            latestReport != null,
            latestReport != null ? latestReport.day : 0);
    }
}
