using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Operation
{
public interface IEventAlertChoiceActionDispatcher
{
    bool TryDispatch(string actionId, out DomainFailure failure);
}

public sealed class NullEventAlertChoiceActionDispatcher :
    IEventAlertChoiceActionDispatcher
{
    public static readonly NullEventAlertChoiceActionDispatcher Instance = new();

    private NullEventAlertChoiceActionDispatcher()
    {
    }

    public bool TryDispatch(string actionId, out DomainFailure failure)
    {
        failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
        return false;
    }
}

public class EventAlertChoice
{
    public string Label { get; }
    public string Description { get; }
    public string ActionId { get; }
    public Action Callback { get; }

    public EventAlertChoice(
        string label,
        string description = "",
        Action callback = null,
        string actionId = "")
    {
        Label = string.IsNullOrWhiteSpace(label) ? "Choice" : label;
        Description = description ?? string.Empty;
        ActionId = actionId?.Trim() ?? string.Empty;
        Callback = callback;
    }

    public EventAlertChoice(string label, string description, string actionId)
        : this(label, description, null, actionId)
    {
    }
}

public class EventAlertRequest
{
    public string Title { get; }
    public string Detail { get; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public string SourceId { get; }
    public IReadOnlyList<EventAlertChoice> Choices { get; }

    public EventAlertRequest(
        string title,
        string detail,
        EventAlertImportance importance,
        string category = "",
        IEnumerable<EventAlertChoice> choices = null,
        string sourceId = "")
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Event" : title;
        Detail = detail ?? string.Empty;
        Importance = importance;
        Category = category ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
        Choices = NormalizeChoices(choices);
    }

    private static IReadOnlyList<EventAlertChoice> NormalizeChoices(IEnumerable<EventAlertChoice> choices)
    {
        EventAlertChoice[] normalized = choices?
            .Where((choice) => choice != null)
            .Take(4)
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
    public string SourceId { get; }
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
        SourceId = request.SourceId;
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
        IEnumerable<EventAlertChoice> choices = null,
        string sourceId = "")
        : this(id, new EventAlertRequest(
            title,
            detail,
            importance,
            category,
            choices,
            sourceId))
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
                choice.Callback,
                choice.ActionId)),
            SourceId);
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
            Choices,
            false,
            SourceId);
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
        bool isDismissed = false,
        string sourceId = "")
    {
        Id = id;
        Title = title ?? string.Empty;
        Detail = detail ?? string.Empty;
        Importance = importance;
        Category = category ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
        Count = Math.Max(1, count);
        Choices = EventPayloadSnapshot.Copy(choices);
        IsDismissed = isDismissed;
    }

    public int Id { get; }
    public string Title { get; }
    public string Detail { get; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public string SourceId { get; }
    public int Count { get; }
    public IReadOnlyList<EventAlertChoice> Choices { get; }
    public bool IsDismissed { get; }
    public string ButtonText => Count > 1 ? $"{Title} x{Count}" : Title;
}

}
