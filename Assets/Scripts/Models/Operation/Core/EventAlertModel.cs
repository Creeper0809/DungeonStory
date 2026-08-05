using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Operation
{
public class EventAlertChoice
{
    public string Label { get; }
    public string Description { get; }
    public Action Callback { get; }

    public EventAlertChoice(string label, string description = "", Action callback = null)
    {
        Label = string.IsNullOrWhiteSpace(label) ? "Choice" : label;
        Description = description ?? string.Empty;
        Callback = callback;
    }
}

public class EventAlertRequest
{
    public string Title { get; }
    public string Detail { get; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public IReadOnlyList<EventAlertChoice> Choices { get; }

    public EventAlertRequest(
        string title,
        string detail,
        EventAlertImportance importance,
        string category = "",
        IEnumerable<EventAlertChoice> choices = null)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Event" : title;
        Detail = detail ?? string.Empty;
        Importance = importance;
        Category = category ?? string.Empty;
        Choices = NormalizeChoices(choices);
    }

    private static IReadOnlyList<EventAlertChoice> NormalizeChoices(IEnumerable<EventAlertChoice> choices)
    {
        EventAlertChoice[] normalized = choices?
            .Where((choice) => choice != null)
            .Take(3)
            .ToArray()
            ?? Array.Empty<EventAlertChoice>();
        return Array.AsReadOnly(normalized);
    }
}

public class EventAlertRecord
{
    public int Id { get; }
    public string Title { get; }
    public string Detail { get; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public int Count { get; private set; }
    public IReadOnlyList<EventAlertChoice> Choices { get; }

    public EventAlertRecord(int id, EventAlertRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        Id = id;
        Title = request.Title;
        Detail = request.Detail;
        Importance = request.Importance;
        Category = request.Category;
        Choices = request.Choices;
        Count = 1;
    }

    public EventAlertRecord(
        int id,
        string title,
        string detail,
        EventAlertImportance importance,
        string category,
        int count,
        IEnumerable<EventAlertChoice> choices = null)
        : this(id, new EventAlertRequest(title, detail, importance, category, choices))
    {
        Count = Math.Max(1, count);
    }

    public void Increment()
    {
        Count++;
    }

    public EventAlertRecord DeepClone()
    {
        return new EventAlertRecord(
            Id,
            Title,
            Detail,
            Importance,
            Category,
            Count,
            Choices.Select(choice => new EventAlertChoice(
                choice.Label,
                choice.Description,
                choice.Callback)));
    }

    public EventAlertRecordSnapshot CreateSnapshot()
    {
        return new EventAlertRecordSnapshot(
            Id,
            Title,
            Detail,
            Importance,
            Category,
            Count,
            Choices);
    }

    public string ButtonText => Count > 1 ? $"{Title} x{Count}" : Title;

}

public sealed class EventAlertRecordSnapshot
{
    public EventAlertRecordSnapshot(
        int id,
        string title,
        string detail,
        EventAlertImportance importance,
        string category,
        int count,
        IReadOnlyList<EventAlertChoice> choices,
        bool isDismissed = false)
    {
        Id = id;
        Title = title ?? string.Empty;
        Detail = detail ?? string.Empty;
        Importance = importance;
        Category = category ?? string.Empty;
        Count = Math.Max(1, count);
        Choices = EventPayloadSnapshot.Copy(choices);
        IsDismissed = isDismissed;
    }

    public int Id { get; }
    public string Title { get; }
    public string Detail { get; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public int Count { get; }
    public IReadOnlyList<EventAlertChoice> Choices { get; }
    public bool IsDismissed { get; }
    public string ButtonText => Count > 1 ? $"{Title} x{Count}" : Title;
}

}
