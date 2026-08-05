using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using FacilityEvolutionDomain = DungeonStory.FacilityEvolution;

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

    public FacilityEvolutionDomain.FacilityEvolutionRecordSnapshot ToDomainSnapshot()
    {
        return new FacilityEvolutionDomain.FacilityEvolutionRecordSnapshot(
            metricsView,
            tokensView,
            recentEventsView);
    }

    public void ReplaceWith(
        FacilityEvolutionDomain.FacilityEvolutionRecordSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        metrics.Clear();
        tokens.Clear();
        recentEvents.Clear();
        foreach (KeyValuePair<string, float> pair in snapshot.Metrics)
        {
            metrics.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, int> pair in snapshot.Tokens)
        {
            tokens.Add(pair.Key, pair.Value);
        }
        recentEvents.AddRange(snapshot.RecentEvents);
    }
}

public class FacilityEvolutionRecordComponent : MonoBehaviour, IFacilityEvolutionRecordProvider
{
    public FacilityEvolutionRecord GetRecord(BuildableObject facility)
    {
        return ResolveState(facility).GetRecord();
    }

    public void SetMetric(string key, float value)
    {
        ResolveState(null).SetRecordMetric(key, value);
    }

    public void AddToken(string key, int count)
    {
        ResolveState(null).AddRecordToken(key, count);
    }

    public void AddRecentEvent(string text)
    {
        ResolveState(null).AddRecordRecentEvent(text);
    }

    public void ReplaceWith(FacilityEvolutionRecord record)
    {
        ResolveState(null).ReplaceRecord(record);
    }

    private FacilityEvolutionStateComponent ResolveState(BuildableObject facility)
    {
        FacilityEvolutionStateComponent state =
            GetComponent<FacilityEvolutionStateComponent>();
        if (state == null)
        {
            state = gameObject.AddComponent<FacilityEvolutionStateComponent>();
        }
        if (facility != null)
        {
            state.InitializeIfNeeded(facility);
        }
        return state;
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

        FacilityEvolutionStateComponent state =
            facility.GetComponent<FacilityEvolutionStateComponent>();
        return state != null ? state.GetRecord() : new FacilityEvolutionRecord();
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

        FacilityEvolutionStateComponent state =
            facility.GetComponent<FacilityEvolutionStateComponent>();
        return state != null ? state.GetRecord() : new FacilityEvolutionRecord();
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
