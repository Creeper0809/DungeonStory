using System;
using System.Linq;
using UnityEngine;

public sealed class CodexRecordSummaryApplicationAdapter :
    ICodexRecordSummaryQueryPort
{
    private readonly CodexRuntime codex;
    private readonly OperatingDaySettlementRuntime settlement;
    private readonly EventAlertRuntime alerts;

    public CodexRecordSummaryApplicationAdapter(
        FacilityFeatureSceneRuntimeReferences facilityRuntimes,
        DungeonSceneRuntimeReferences sceneRuntimes)
    {
        codex = (facilityRuntimes
                ?? throw new ArgumentNullException(nameof(facilityRuntimes)))
            .Codex
            ?? throw new InvalidOperationException(
                $"{nameof(CodexRecordSummaryService)} requires a loaded {nameof(CodexRuntime)}.");
        settlement = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .Settlement
            ?? throw new InvalidOperationException(
                $"{nameof(CodexRecordSummaryService)} requires a loaded {nameof(OperatingDaySettlementRuntime)}.");
        alerts = sceneRuntimes.Alerts
            ?? throw new InvalidOperationException(
                $"{nameof(CodexRecordSummaryService)} requires a loaded {nameof(EventAlertRuntime)}.");
    }

    public CodexRecordSummary Capture()
    {
        OperatingDayReport latestReport = settlement.LatestReport;
        return new CodexRecordSummary(
            codex.GetEntries(CodexEntryCategory.Monster).Count,
            codex.GetEntries(CodexEntryCategory.Invasion).Count,
            codex.GetEntries(CodexEntryCategory.Facility).Count,
            alerts.EventLog.Count,
            latestReport != null,
            latestReport != null ? latestReport.day : 0);
    }
}

public sealed class CodexSaveApplicationAdapter :
    ICodexSaveQueryPort,
    ICodexRestorePort,
    ICodexSaveSerializationPort
{
    private readonly CodexRuntime runtime;

    public CodexSaveApplicationAdapter(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .Codex
            ?? throw new InvalidOperationException(
                $"{nameof(CodexSaveSection)} requires a loaded {nameof(CodexRuntime)}.");
    }

    public DungeonCodexSaveData Capture()
    {
        return new DungeonCodexSaveData
        {
            entries = runtime.State.Entries
                .OrderBy(entry => entry.Category)
                .ThenBy(entry => entry.EntryId, StringComparer.Ordinal)
                .Select(entry => new DungeonCodexEntrySaveData
                {
                    category = entry.Category,
                    entryId = entry.EntryId,
                    title = entry.Title,
                    lines = entry.Lines
                        .OrderBy(line => line.Source)
                        .ThenBy(line => line.Text, StringComparer.Ordinal)
                        .Select(line => new DungeonCodexLineSaveData
                        {
                            text = line.Text,
                            source = line.Source
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public void Restore(DungeonCodexSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        CodexState restored = new CodexState();
        foreach (DungeonCodexEntrySaveData entry in source.entries)
        {
            restored.GetOrCreate(entry.category, entry.entryId, entry.title);
            foreach (DungeonCodexLineSaveData line in entry.lines)
            {
                restored.AddInfo(
                    entry.category,
                    entry.entryId,
                    entry.title,
                    line.text,
                    line.source);
            }
        }

        runtime.ReplaceStateFromRestore(restored);
    }

    public string Serialize(DungeonCodexSaveData payload)
    {
        return JsonUtility.ToJson(
            payload ?? throw new ArgumentNullException(nameof(payload)));
    }

    public DungeonCodexSaveData Deserialize(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{CodexSaveSection.Id} payload is empty.");
        }

        try
        {
            return JsonUtility.FromJson<DungeonCodexSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{CodexSaveSection.Id} payload deserialized to null.");
        }
        catch (Exception exception) when (
            exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{CodexSaveSection.Id} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }
}
