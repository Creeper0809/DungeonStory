using System.Collections.Generic;

namespace DungeonStory.Operation
{
public sealed class EventAlertAggregateState
{
    public List<EventAlertRecord> Records { get; } =
        new List<EventAlertRecord>();

    public HashSet<int> DismissedRecordIds { get; } =
        new HashSet<int>();

    public int NextId { get; set; } = 1;

    public EventAlertAggregateState DeepClone()
    {
        EventAlertAggregateState clone = new EventAlertAggregateState
        {
            NextId = NextId
        };
        foreach (EventAlertRecord record in Records)
        {
            clone.Records.Add(record.DeepClone());
        }

        clone.DismissedRecordIds.UnionWith(DismissedRecordIds);
        return clone;
    }
}

public sealed class EventAlertRestoreCandidate
{
    public EventAlertRestoreCandidate(EventAlertAggregateState state)
    {
        State = state
            ?? throw new System.ArgumentNullException(nameof(state));
    }

    public EventAlertAggregateState State { get; }
}

}
