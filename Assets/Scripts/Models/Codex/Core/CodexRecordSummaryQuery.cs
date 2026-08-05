using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICodexRecordSummaryService
{
    CodexRecordSummary Capture();
}

public interface ICodexRecordSummaryQueryPort
{
    CodexRecordSummary Capture();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexRecordSummaryService : ICodexRecordSummaryService
{
    private readonly ICodexRecordSummaryQueryPort query;

    public CodexRecordSummaryService(ICodexRecordSummaryQueryPort query)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public CodexRecordSummary Capture() => query.Capture();
}
