using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Operation
{
public interface IEventAlertChoiceActionDispatcher
{
    bool TryDispatch(string actionId, out DomainFailure failure);
}

public enum EventAlertChoiceActionDisposition
{
    Terminal = 0,
    AcceptedPending = 1
}

public interface IEventAlertChoiceActionDispositionDispatcher
{
    bool TryDispatch(
        string actionId,
        out EventAlertChoiceActionDisposition disposition,
        out DomainFailure failure);
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
    public bool IsResolved { get; }
    public string ResultSummary { get; }

    public EventAlertRequest(
        string title,
        string detail,
        EventAlertImportance importance,
        string category = "",
        IEnumerable<EventAlertChoice> choices = null,
        string sourceId = "",
        bool isResolved = false,
        string resultSummary = "")
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Event" : title;
        Detail = detail ?? string.Empty;
        Importance = importance;
        Category = category ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
        Choices = NormalizeChoices(choices);
        IsResolved = isResolved;
        ResultSummary = resultSummary?.Trim() ?? string.Empty;
        if (IsResolved
            && (SourceId.Length == 0
                || ResultSummary.Length == 0
                || Choices.Count > 0)
            || !IsResolved && ResultSummary.Length > 0)
        {
            throw new ArgumentException(
                "Resolved event alerts require a source, a result summary, and no actions.",
                nameof(resultSummary));
        }
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
    public string Detail { get; private set; }
    public EventAlertImportance Importance { get; }
    public string Category { get; }
    public string SourceId { get; }
    public int Count { get; private set; }
    public IReadOnlyList<EventAlertChoice> Choices { get; private set; }
    public bool IsResolved { get; private set; }
    public string ResultSummary { get; private set; }
    public string ChoiceFailureDetail { get; private set; }

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
        IsResolved = request.IsResolved;
        ResultSummary = request.ResultSummary;
        ChoiceFailureDetail = string.Empty;
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
        string sourceId = "",
        bool isResolved = false,
        string resultSummary = "",
        string choiceFailureDetail = "")
        : this(id, new EventAlertRequest(
            title,
            detail,
            importance,
            category,
            choices,
            sourceId,
            isResolved,
            resultSummary))
    {
        Count = Math.Max(1, count);
        if (!string.IsNullOrEmpty(choiceFailureDetail))
        {
            ReplaceChoiceFailure(choiceFailureDetail);
        }
    }

    public void Increment()
    {
        Count++;
    }

    public bool TryRefreshSourceContent(EventAlertRequest request)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(SourceId)
            || string.IsNullOrWhiteSpace(request.SourceId)
            || !string.Equals(
                SourceId,
                request.SourceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (IsResolved)
        {
            return !request.IsResolved
                || request.Choices.Count == 0
                    && string.Equals(
                        ResultSummary,
                        request.ResultSummary,
                        StringComparison.Ordinal);
        }

        Detail = request.Detail;
        if (request.IsResolved)
        {
            IsResolved = true;
            ResultSummary = request.ResultSummary;
            Choices = Array.Empty<EventAlertChoice>();
            ChoiceFailureDetail = string.Empty;
            return true;
        }
        Choices = request.Choices;
        return true;
    }

    public void ReplaceChoiceFailure(string localizedFailure)
    {
        if (IsResolved
            || string.IsNullOrWhiteSpace(localizedFailure)
            || !string.Equals(
                localizedFailure,
                localizedFailure.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only an unresolved event action can retain a canonical failure detail.");
        }
        ChoiceFailureDetail = localizedFailure;
    }

    public void ClearChoiceFailure()
    {
        ChoiceFailureDetail = string.Empty;
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
            SourceId,
            IsResolved,
            ResultSummary,
            ChoiceFailureDetail);
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
            SourceId,
            IsResolved,
            ResultSummary,
            ChoiceFailureDetail);
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
        string sourceId = "",
        bool isResolved = false,
        string resultSummary = "",
        string choiceFailureDetail = "")
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
        IsResolved = isResolved;
        ResultSummary = resultSummary?.Trim() ?? string.Empty;
        ChoiceFailureDetail = choiceFailureDetail?.Trim() ?? string.Empty;
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
    public bool IsResolved { get; }
    public string ResultSummary { get; }
    public string ChoiceFailureDetail { get; }
    public string ButtonText => Count > 1 ? $"{Title} x{Count}" : Title;
}

}
