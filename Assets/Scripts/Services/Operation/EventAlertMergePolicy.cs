using System.Collections.Generic;
using System.Linq;

public static class EventAlertMergePolicy
{
    public static EventAlertRecord FindMergeTarget(IEnumerable<EventAlertRecord> records, EventAlertRequest request)
    {
        if (records == null || request == null || request.Choices.Count > 0)
        {
            return null;
        }

        return records.LastOrDefault((record) => CanMerge(record, request));
    }

    private static bool CanMerge(EventAlertRecord record, EventAlertRequest request)
    {
        return record != null
            && record.Title == request.Title
            && record.Category == request.Category
            && record.Importance == request.Importance
            && record.Detail == request.Detail;
    }
}
