using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public interface IFacilityEvolutionRecordProvider
{
    FacilityEvolutionRecord GetRecord(BuildableObject facility);
}

public interface IFacilityEvolutionRecordComponentService : IFacilityEvolutionRecordProvider
{
    FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility);
    void ReplaceWith(BuildableObject facility, FacilityEvolutionRecord record);
}

public sealed class FacilityEvolutionRecord
{
    private readonly Dictionary<string, float> metrics = new Dictionary<string, float>();
    private readonly Dictionary<string, int> tokens = new Dictionary<string, int>();
    private readonly List<string> recentEvents = new List<string>();
    private readonly IReadOnlyDictionary<string, float> metricsView;
    private readonly IReadOnlyDictionary<string, int> tokensView;
    private readonly IReadOnlyList<string> recentEventsView;

    public FacilityEvolutionRecord()
    {
        metricsView = new ReadOnlyDictionary<string, float>(metrics);
        tokensView = new ReadOnlyDictionary<string, int>(tokens);
        recentEventsView = recentEvents.AsReadOnly();
    }

    public IReadOnlyDictionary<string, float> Metrics => metricsView;
    public IReadOnlyDictionary<string, int> Tokens => tokensView;
    public IReadOnlyList<string> RecentEvents => recentEventsView;

    public float GetMetric(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && metrics.TryGetValue(key, out float value) ? value : 0f;
    }

    public int GetToken(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && tokens.TryGetValue(key, out int value) ? value : 0;
    }

    public void AddMetric(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        metrics[key] = value;
    }

    public void AddToken(string key, int count)
    {
        if (string.IsNullOrWhiteSpace(key) || count == 0)
        {
            return;
        }

        tokens.TryGetValue(key, out int current);
        tokens[key] = Mathf.Max(0, current + count);
    }

    public void SetToken(string key, int count)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        tokens[key] = Mathf.Max(0, count);
    }

    public bool TryConsumeToken(string key, int count, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        int required = Mathf.Max(1, count);
        tokens.TryGetValue(key, out int current);
        if (current < required)
        {
            reason = $"{key} {current}/{required}";
            return false;
        }

        tokens[key] = Mathf.Max(0, current - required);
        return true;
    }

    public void AddEvent(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            recentEvents.Add(text);
        }
    }

    public bool TryConsumeTokens(
        IEnumerable<FacilityEvolutionTokenRequirement> requirements,
        out string reason)
    {
        reason = string.Empty;
        if (requirements == null)
        {
            return true;
        }

        List<FacilityEvolutionTokenRequirement> normalized = requirements
            .Where((requirement) => !string.IsNullOrWhiteSpace(requirement.key))
            .ToList();
        foreach (FacilityEvolutionTokenRequirement requirement in normalized)
        {
            int required = Mathf.Max(1, requirement.minCount);
            tokens.TryGetValue(requirement.key, out int current);
            if (current < required)
            {
                reason = $"{requirement.key} {current}/{required}";
                return false;
            }
        }

        foreach (FacilityEvolutionTokenRequirement requirement in normalized)
        {
            int required = Mathf.Max(1, requirement.minCount);
            tokens.TryGetValue(requirement.key, out int current);
            tokens[requirement.key] = Mathf.Max(0, current - required);
        }

        return true;
    }

    public FacilityEvolutionRecord Clone()
    {
        FacilityEvolutionRecord clone = new FacilityEvolutionRecord();
        foreach (KeyValuePair<string, float> pair in metrics)
        {
            clone.AddMetric(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, int> pair in tokens)
        {
            clone.AddToken(pair.Key, pair.Value);
        }

        foreach (string entry in recentEvents)
        {
            clone.AddEvent(entry);
        }

        return clone;
    }
}

public class FacilityEvolutionRecordComponent : MonoBehaviour, IFacilityEvolutionRecordProvider
{
    [SerializeField] private FacilityEvolutionValue[] metrics = Array.Empty<FacilityEvolutionValue>();
    [SerializeField] private FacilityEvolutionTokenValue[] tokens = Array.Empty<FacilityEvolutionTokenValue>();
    [SerializeField] private string[] recentEvents = Array.Empty<string>();

    public FacilityEvolutionRecord GetRecord(BuildableObject facility)
    {
        FacilityEvolutionRecord record = new FacilityEvolutionRecord();
        if (metrics != null)
        {
            foreach (FacilityEvolutionValue metric in metrics)
            {
                record.AddMetric(metric.key, metric.value);
            }
        }

        if (tokens != null)
        {
            foreach (FacilityEvolutionTokenValue token in tokens)
            {
                record.AddToken(token.key, token.count);
            }
        }

        if (recentEvents != null)
        {
            foreach (string entry in recentEvents)
            {
                record.AddEvent(entry);
            }
        }

        return record;
    }

    public void SetMetric(string key, float value)
    {
        List<FacilityEvolutionValue> list = metrics?.ToList() ?? new List<FacilityEvolutionValue>();
        int index = list.FindIndex((entry) => entry.key == key);
        if (index >= 0)
        {
            list[index] = new FacilityEvolutionValue(key, value);
        }
        else
        {
            list.Add(new FacilityEvolutionValue(key, value));
        }

        metrics = list.ToArray();
    }

    public void AddToken(string key, int count)
    {
        List<FacilityEvolutionTokenValue> list = tokens?.ToList() ?? new List<FacilityEvolutionTokenValue>();
        int index = list.FindIndex((entry) => entry.key == key);
        if (index >= 0)
        {
            list[index] = new FacilityEvolutionTokenValue(key, Mathf.Max(0, list[index].count + count));
        }
        else
        {
            list.Add(new FacilityEvolutionTokenValue(key, Mathf.Max(0, count)));
        }

        tokens = list.ToArray();
    }

    public void AddRecentEvent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        List<string> list = recentEvents?.ToList() ?? new List<string>();
        list.Add(text);
        int skip = Mathf.Max(0, list.Count - 12);
        recentEvents = list.Skip(skip).ToArray();
    }

    public void ReplaceWith(FacilityEvolutionRecord record)
    {
        if (record == null)
        {
            metrics = Array.Empty<FacilityEvolutionValue>();
            tokens = Array.Empty<FacilityEvolutionTokenValue>();
            recentEvents = Array.Empty<string>();
            return;
        }

        metrics = record.Metrics
            .Select((entry) => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        tokens = record.Tokens
            .Select((entry) => new FacilityEvolutionTokenValue(entry.Key, entry.Value))
            .ToArray();
        List<string> events = record.RecentEvents
            .Where((entry) => !string.IsNullOrWhiteSpace(entry))
            .ToList();
        int skip = Mathf.Max(0, events.Count - 12);
        recentEvents = events.Skip(skip).ToArray();
    }
}

public sealed class ComponentFacilityEvolutionRecordProvider : IFacilityEvolutionRecordProvider
{
    public FacilityEvolutionRecord GetRecord(BuildableObject facility)
    {
        if (facility == null)
        {
            return new FacilityEvolutionRecord();
        }

        FacilityEvolutionRecordComponent component = facility.GetComponent<FacilityEvolutionRecordComponent>();
        return component != null ? component.GetRecord(facility) : new FacilityEvolutionRecord();
    }
}

public sealed class FacilityEvolutionRecordComponentService : IFacilityEvolutionRecordComponentService
{
    private readonly IFacilityEvolutionRecordComponentFactory recordComponentFactory;

    public FacilityEvolutionRecordComponentService(
        IFacilityEvolutionRecordComponentFactory recordComponentFactory)
    {
        this.recordComponentFactory = recordComponentFactory
            ?? throw new ArgumentNullException(nameof(recordComponentFactory));
    }

    public FacilityEvolutionRecord GetRecord(BuildableObject facility)
    {
        if (facility == null)
        {
            return new FacilityEvolutionRecord();
        }

        FacilityEvolutionRecordComponent component = facility.GetComponent<FacilityEvolutionRecordComponent>();
        return component != null ? component.GetRecord(facility) : new FacilityEvolutionRecord();
    }

    public FacilityEvolutionRecordComponent GetOrAdd(BuildableObject facility)
    {
        return recordComponentFactory.GetOrAdd(facility);
    }

    public void ReplaceWith(BuildableObject facility, FacilityEvolutionRecord record)
    {
        if (facility == null || record == null)
        {
            return;
        }

        bool hasData = record.Metrics.Count > 0
            || record.Tokens.Count > 0
            || record.RecentEvents.Count > 0;
        if (!hasData)
        {
            return;
        }

        GetOrAdd(facility)?.ReplaceWith(record);
    }
}
