using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Infrastructure
{
public sealed class EventAlertSaveService : IEventAlertSaveService
{
    private readonly EventAlertRuntime runtime;

    public EventAlertSaveService(DungeonSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .Alerts
            ?? throw new InvalidOperationException(
                $"{nameof(EventAlertSaveService)} requires a loaded {nameof(EventAlertRuntime)}.");
    }

    public DungeonEventAlertSaveData Capture()
    {
        DungeonEventAlertSaveData result = new DungeonEventAlertSaveData();
        result.records = runtime.EventLog
            .TakeLast(EventAlertPayloadValidation.MaxSavedRecords)
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

    public EventAlertRestoreCandidate PrepareRestore(
        DungeonEventAlertSaveData source)
    {
        IReadOnlyList<string> errors = EventAlertPayloadValidation.Validate(source);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Event-alert restore candidate is invalid: "
                + string.Join(" | ", errors));
        }

        return runtime.PrepareRestoreHistory(source.records
            .Select(record => new EventAlertRecordSnapshot(
                record.id,
                record.title,
                record.detail,
                record.importance,
                record.category,
                record.count,
                record.choices
                    .Select(choice => new EventAlertChoice(choice.label, choice.description))
                    .ToList(),
                record.dismissed))
            .ToList());
    }

    public void PublishRestore(EventAlertRestoreCandidate candidate) =>
        runtime.PublishRestoreHistory(candidate);
}

}
