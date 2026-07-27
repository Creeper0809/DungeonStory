using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class AiDirectorContextSummary
{
    public int characterCount;
    public float averageMood;
    public float averageSleep;
    public int stockShortageFacilityCount;
    public string[] topQueuedFacilities = Array.Empty<string>();
    public string[] repeatedFailureReasons = Array.Empty<string>();
    public string[] targetRecentEvents = Array.Empty<string>();

    public string ToPromptText(int maxCharacters)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"characterCount: {characterCount}");
        builder.AppendLine($"averageMood: {averageMood:0.0}");
        builder.AppendLine($"averageSleep: {averageSleep:0.0}");
        builder.AppendLine($"stockShortageFacilityCount: {stockShortageFacilityCount}");
        builder.AppendLine($"topQueuedFacilities: {string.Join(", ", topQueuedFacilities ?? Array.Empty<string>())}");
        builder.AppendLine($"repeatedFailureReasons: {string.Join(", ", repeatedFailureReasons ?? Array.Empty<string>())}");
        builder.AppendLine($"targetRecentEvents: {string.Join(" | ", targetRecentEvents ?? Array.Empty<string>())}");

        string text = builder.ToString();
        if (maxCharacters > 0 && text.Length > maxCharacters)
        {
            return text.Substring(0, maxCharacters);
        }

        return text;
    }
}

public static class AiDirectorContextAggregator
{
    private const int DefaultActorSampleLimit = 24;
    private const int DefaultFacilitySampleLimit = 64;

    public static AiDirectorContextSummary Build(
        CharacterActor target,
        AiDirectorContextSceneSnapshot snapshot,
        int maxTargetEvents = 5,
        int actorSampleLimit = DefaultActorSampleLimit,
        int facilitySampleLimit = DefaultFacilitySampleLimit)
    {
        IReadOnlyList<CharacterActor> actors = snapshot.Actors ?? Array.Empty<CharacterActor>();
        IReadOnlyList<BuildableObject> facilities = snapshot.Facilities ?? Array.Empty<BuildableObject>();
        int sampledActorCount = Mathf.Min(
            actors.Count,
            Mathf.Max(1, actorSampleLimit));
        int sampledFacilityCount = Mathf.Min(
            facilities.Count,
            Mathf.Max(1, facilitySampleLimit));

        return new AiDirectorContextSummary
        {
            characterCount = actors.Count,
            averageMood = AverageCondition(
                actors,
                sampledActorCount,
                CharacterCondition.MOOD),
            averageSleep = AverageCondition(
                actors,
                sampledActorCount,
                CharacterCondition.SLEEP),
            stockShortageFacilityCount = CountStockShortages(
                facilities,
                sampledFacilityCount),
            topQueuedFacilities = GetTopQueuedFacilities(
                facilities,
                sampledFacilityCount,
                3),
            repeatedFailureReasons = GetRepeatedFailureReasons(
                actors,
                sampledActorCount,
                5),
            targetRecentEvents = GetRecentEvents(target, maxTargetEvents)
        };
    }

    private static float AverageCondition(
        IReadOnlyList<CharacterActor> actors,
        int sampleCount,
        CharacterCondition condition)
    {
        if (actors == null || actors.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        int count = 0;
        int limit = Mathf.Min(actors.Count, Mathf.Max(0, sampleCount));
        for (int index = 0; index < limit; index++)
        {
            CharacterActor actor = actors[index];
            if (actor == null
                || actor.Stats == null
                || actor.Stats.Stats == null
                || !actor.Stats.Stats.TryGetValue(condition, out float value))
            {
                continue;
            }

            total += value;
            count++;
        }

        return count > 0 ? total / count : 0f;
    }

    private static int CountStockShortages(
        IReadOnlyList<BuildableObject> facilities,
        int sampleCount)
    {
        int count = 0;
        int limit = Mathf.Min(
            facilities?.Count ?? 0,
            Mathf.Max(0, sampleCount));
        for (int index = 0; index < limit; index++)
        {
            BuildableObject facility = facilities[index];
            if (facility == null
                || facility.Facility == null
                || !facility.BuildingData.RequiresStockForUse()
                || facility is not IStockedFacility stockedFacility)
            {
                continue;
            }

            if (!stockedFacility.HasAvailableStock)
            {
                count++;
            }
        }

        return count;
    }

    private static string[] GetTopQueuedFacilities(
        IReadOnlyList<BuildableObject> facilities,
        int sampleCount,
        int limit)
    {
        return (facilities ?? Array.Empty<BuildableObject>())
            .Take(Mathf.Max(0, sampleCount))
            .Where((facility) => facility != null && facility.ActiveVisitReservationCount > 0)
            .OrderByDescending((facility) => facility.ActiveVisitReservationCount)
            .ThenBy((facility) => facility.name)
            .Take(limit)
            .Select((facility) => $"{GetBuildingLabel(facility)}:{facility.ActiveVisitReservationCount}")
            .ToArray();
    }

    private static string[] GetRepeatedFailureReasons(
        IReadOnlyList<CharacterActor> actors,
        int sampleCount,
        int limit)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        int actorLimit = Mathf.Min(
            actors?.Count ?? 0,
            Mathf.Max(0, sampleCount));
        for (int index = 0; index < actorLimit; index++)
        {
            CharacterActor actor = actors[index];
            CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
            if (blackboard == null)
            {
                continue;
            }

            foreach (KeyValuePair<string, int> pair in blackboard.RecentFailureCounts)
            {
                counts[pair.Key] = counts.TryGetValue(pair.Key, out int current)
                    ? current + pair.Value
                    : pair.Value;
            }
        }

        return counts
            .OrderByDescending((pair) => pair.Value)
            .ThenBy((pair) => pair.Key)
            .Take(limit)
            .Select((pair) => $"{pair.Key}:{pair.Value}")
            .ToArray();
    }

    private static string[] GetRecentEvents(CharacterActor target, int limit)
    {
        if (target == null || target.Log == null)
        {
            return Array.Empty<string>();
        }

        return target.Log
            .Reverse()
            .Take(Mathf.Max(1, limit))
            .Reverse()
            .ToArray();
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }
}
