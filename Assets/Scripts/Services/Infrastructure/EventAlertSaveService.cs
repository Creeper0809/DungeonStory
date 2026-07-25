using System;
using System.Collections.Generic;
using System.Linq;

public interface IEventAlertRuntimeProvider
{
    bool TryGetRuntime(out EventAlertRuntime runtime);
}

public sealed class EventAlertRuntimeProvider :
    IEventAlertRuntimeProvider
{
    private readonly DungeonSceneRuntimeReferences runtimeReferences;

    public EventAlertRuntimeProvider(
        DungeonSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out EventAlertRuntime runtime)
    {
        runtime = runtimeReferences.Alerts;
        return runtime != null;
    }
}

public interface IEventAlertSaveService
{
    DungeonEventAlertSaveData Capture();
    void Restore(DungeonEventAlertSaveData source, DungeonGameRestoreReport report);
}

[Serializable]
public sealed class DungeonEventAlertSaveData
{
    public List<DungeonEventAlertRecordSaveData> records = new List<DungeonEventAlertRecordSaveData>();
}

[Serializable]
public sealed class DungeonEventAlertRecordSaveData
{
    public int id;
    public string title = string.Empty;
    public string detail = string.Empty;
    public EventAlertImportance importance;
    public string category = string.Empty;
    public int count = 1;
    public bool dismissed;
    public List<DungeonEventAlertChoiceSaveData> choices = new List<DungeonEventAlertChoiceSaveData>();
}

[Serializable]
public sealed class DungeonEventAlertChoiceSaveData
{
    public string label = string.Empty;
    public string description = string.Empty;
}

public sealed class EventAlertSaveService : IEventAlertSaveService
{
    private const int MaxSavedRecords = 80;

    private readonly IEventAlertRuntimeProvider provider;

    public EventAlertSaveService(IEventAlertRuntimeProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public DungeonEventAlertSaveData Capture()
    {
        DungeonEventAlertSaveData result = new DungeonEventAlertSaveData();
        if (!provider.TryGetRuntime(out EventAlertRuntime runtime))
        {
            return result;
        }

        result.records = runtime.EventLog
            .TakeLast(MaxSavedRecords)
            .Select(record => new DungeonEventAlertRecordSaveData
            {
                id = record.Id,
                title = record.Title,
                detail = record.Detail,
                importance = record.Importance,
                category = record.Category,
                count = record.Count,
                dismissed = runtime.IsDismissed(record),
                choices = record.Choices.Select(choice => new DungeonEventAlertChoiceSaveData
                {
                    label = choice.Label,
                    description = choice.Description
                }).ToList()
            })
            .ToList();
        return result;
    }

    public void Restore(DungeonEventAlertSaveData source, DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (!provider.TryGetRuntime(out EventAlertRuntime runtime))
        {
            report.AddWarning("Event alert runtime was not present; alert history was skipped.");
            return;
        }

        source ??= new DungeonEventAlertSaveData();
        runtime.RestoreHistory((source.records ?? new List<DungeonEventAlertRecordSaveData>())
            .Where(record => record != null)
            .Select(record => new EventAlertRecordSnapshot(
                record.id,
                record.title,
                record.detail,
                record.importance,
                record.category,
                record.count,
                (record.choices ?? new List<DungeonEventAlertChoiceSaveData>())
                    .Where(choice => choice != null)
                    .Select(choice => new EventAlertChoice(choice.label, choice.description))
                    .ToList(),
                record.dismissed))
            .ToList());
    }
}
