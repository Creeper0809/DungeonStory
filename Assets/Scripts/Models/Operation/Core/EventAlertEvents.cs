using System;

namespace DungeonStory.Operation
{
public struct EventAlertRequestedEvent
{
    public EventAlertRequest request;

    public EventAlertRequestedEvent(EventAlertRequest request)
    {
        this.request = request;
    }
}

public struct EventAlertLoggedEvent
{
    public EventAlertRecordSnapshot record;

    public EventAlertLoggedEvent(EventAlertRecord record)
    {
        this.record = record?.CreateSnapshot();
    }

    public EventAlertLoggedEvent(EventAlertRecordSnapshot record)
    {
        this.record = record;
    }
}

public readonly struct EventAlertSourceResolvedEvent
{
    public EventAlertSourceResolvedEvent(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)
            || !string.Equals(sourceId, sourceId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Resolved event-alert source ID must be canonical.",
                nameof(sourceId));
        }
        SourceId = sourceId;
    }

    public string SourceId { get; }
}

}
