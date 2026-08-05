using System;
using System.Collections.Generic;

public sealed class DoorAccessSubjectAggregateState
{
    public readonly HashSet<string> CaptiveIds =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<string> CapturedWildlifeIds =
        new HashSet<string>(StringComparer.Ordinal);

    internal DoorAccessSubjectAggregateState Clone()
    {
        DoorAccessSubjectAggregateState clone = new();
        clone.CaptiveIds.UnionWith(CaptiveIds);
        clone.CapturedWildlifeIds.UnionWith(CapturedWildlifeIds);
        return clone;
    }
}

public sealed class DoorAccessSubjectAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    public DoorAccessSubjectAggregateStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public DoorAccessSubjectAggregateState State =>
        aggregateRootStore.GetOrCreate(
            () => new DoorAccessSubjectAggregateState());

    public int PublishedRestoreRevision =>
        aggregateRootStore.PublishedRestoreRevision;

    public bool IsRestoreStaging => aggregateRootStore.IsRestoreStaging;

    public bool ReplaceCaptives(IEnumerable<string> persistentIds)
    {
        DoorAccessSubjectAggregateState current = State;
        DoorAccessSubjectAggregateState replacement = current.Clone();
        replacement.CaptiveIds.Clear();
        AddNormalized(replacement.CaptiveIds, persistentIds);
        bool changed = !replacement.CaptiveIds.SetEquals(current.CaptiveIds);
        aggregateRootStore.Replace(replacement);
        return changed;
    }

    public bool ReplaceCapturedWildlife(IEnumerable<string> wildlifeIds)
    {
        DoorAccessSubjectAggregateState current = State;
        DoorAccessSubjectAggregateState replacement = current.Clone();
        replacement.CapturedWildlifeIds.Clear();
        AddNormalized(replacement.CapturedWildlifeIds, wildlifeIds);
        bool changed = !replacement.CapturedWildlifeIds.SetEquals(
            current.CapturedWildlifeIds);
        aggregateRootStore.Replace(replacement);
        return changed;
    }

    private static void AddNormalized(
        ISet<string> destination,
        IEnumerable<string> values)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length > 0)
            {
                destination.Add(normalized);
            }
        }
    }
}
