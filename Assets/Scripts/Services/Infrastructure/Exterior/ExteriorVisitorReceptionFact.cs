using System;

/// <summary>Immutable observation of a visitor's completed entrance reception.</summary>
public sealed class ExteriorVisitorReceptionFact
{
    public ExteriorVisitorReceptionFact(string incidentId, string visitorId, string visitorName,
        string moodFactorId, int absoluteDay)
    {
        IncidentId = Required(incidentId, nameof(incidentId));
        VisitorId = Required(visitorId, nameof(visitorId));
        VisitorName = visitorName ?? string.Empty;
        MoodFactorId = Required(moodFactorId, nameof(moodFactorId));
        AbsoluteDay = absoluteDay > 0 ? absoluteDay
            : throw new ArgumentOutOfRangeException(nameof(absoluteDay));
    }
    public string IncidentId { get; }
    public string VisitorId { get; }
    public string VisitorName { get; }
    public string MoodFactorId { get; }
    public int AbsoluteDay { get; }
    private static string Required(string value, string parameter) =>
        string.IsNullOrWhiteSpace(value) || value != value.Trim()
            ? throw new ArgumentException("A canonical domain identity is required.", parameter) : value;
}
