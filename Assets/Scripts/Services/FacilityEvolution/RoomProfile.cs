using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public interface IRoomProfileProvider
{
    RoomProfile Build(BuildableObject facility);
}

public sealed class RoomProfile
{
    private readonly Dictionary<string, float> scores = new Dictionary<string, float>();
    private readonly Dictionary<string, float> metrics = new Dictionary<string, float>();
    private readonly Dictionary<string, float> identityPressures = new Dictionary<string, float>();
    private readonly Dictionary<string, int> recordTokens = new Dictionary<string, int>();
    private readonly HashSet<string> tagLookup = new HashSet<string>();
    private readonly List<string> tags = new List<string>();
    private readonly List<BuildableObject> fixtures = new List<BuildableObject>();
    private readonly List<string> recentEvents = new List<string>();
    private readonly List<string> dominantSignals = new List<string>();
    private readonly List<string> conflictingSignals = new List<string>();
    private readonly IReadOnlyDictionary<string, float> scoresView;
    private readonly IReadOnlyDictionary<string, float> metricsView;
    private readonly IReadOnlyDictionary<string, float> identityPressuresView;
    private readonly IReadOnlyDictionary<string, int> recordTokensView;
    private readonly IReadOnlyCollection<string> tagsView;
    private readonly IReadOnlyList<BuildableObject> fixturesView;
    private readonly IReadOnlyList<string> recentEventsView;
    private readonly IReadOnlyList<string> dominantSignalsView;
    private readonly IReadOnlyList<string> conflictingSignalsView;

    public RoomProfile(BuildableObject facility, RoomInstance room)
    {
        Facility = facility;
        Room = room;
        scoresView = new ReadOnlyDictionary<string, float>(scores);
        metricsView = new ReadOnlyDictionary<string, float>(metrics);
        identityPressuresView = new ReadOnlyDictionary<string, float>(identityPressures);
        recordTokensView = new ReadOnlyDictionary<string, int>(recordTokens);
        tagsView = tags.AsReadOnly();
        fixturesView = fixtures.AsReadOnly();
        recentEventsView = recentEvents.AsReadOnly();
        dominantSignalsView = dominantSignals.AsReadOnly();
        conflictingSignalsView = conflictingSignals.AsReadOnly();
    }

    public BuildableObject Facility { get; }
    public RoomInstance Room { get; }
    public bool HasRoom => Room != null;
    public bool IsClosed => Room != null && Room.IsClosed;
    public bool HasDoor => Room != null && Room.HasDoor;
    public bool IsUsable => Room != null && Room.IsUsable;
    public int Area => Room != null ? Room.Cells.Count : 0;
    public IReadOnlyDictionary<string, float> Scores => scoresView;
    public IReadOnlyDictionary<string, float> Metrics => metricsView;
    public IReadOnlyDictionary<string, float> IdentityPressures => identityPressuresView;
    public IReadOnlyDictionary<string, int> RecordTokens => recordTokensView;
    public IReadOnlyCollection<string> Tags => tagsView;
    public IReadOnlyList<BuildableObject> Fixtures => fixturesView;
    public IReadOnlyList<string> RecentEvents => recentEventsView;
    public IReadOnlyList<string> DominantSignals => dominantSignalsView;
    public IReadOnlyList<string> ConflictingSignals => conflictingSignalsView;

    public float GetScore(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && scores.TryGetValue(key, out float value) ? value : 0f;
    }

    public float GetMetric(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && metrics.TryGetValue(key, out float value) ? value : 0f;
    }

    public bool HasMetric(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && metrics.ContainsKey(key);
    }

    public float GetIdentityPressure(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && identityPressures.TryGetValue(key, out float value) ? value : 0f;
    }

    public int GetToken(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && recordTokens.TryGetValue(key, out int value) ? value : 0;
    }

    public bool HasTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && tagLookup.Contains(tag);
    }

    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            if (tagLookup.Add(tag))
            {
                tags.Add(tag);
            }
        }
    }

    public void AddScore(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key) || Mathf.Approximately(value, 0f))
        {
            return;
        }

        scores.TryGetValue(key, out float current);
        scores[key] = current + value;
    }

    public void AddMetric(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        metrics[key] = value;
    }

    public void AddMetricDelta(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key) || Mathf.Approximately(value, 0f))
        {
            return;
        }

        metrics.TryGetValue(key, out float current);
        metrics[key] = current + value;
    }

    public void SetIdentityPressure(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        identityPressures[key] = Mathf.Clamp01(value);
    }

    public void AddRecordToken(string key, int count)
    {
        if (string.IsNullOrWhiteSpace(key) || count == 0)
        {
            return;
        }

        recordTokens.TryGetValue(key, out int current);
        recordTokens[key] = Mathf.Max(0, current + count);
    }

    public void AddFixture(BuildableObject fixture)
    {
        if (fixture != null && !fixtures.Contains(fixture))
        {
            fixtures.Add(fixture);
        }
    }

    public void AddRecentEvent(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            recentEvents.Add(text);
        }
    }

    public void AddDominantSignal(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !dominantSignals.Contains(text))
        {
            dominantSignals.Add(text);
        }
    }

    public void AddConflictingSignal(string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !conflictingSignals.Contains(text))
        {
            conflictingSignals.Add(text);
        }
    }
}

public sealed class RoomProfileBuilder : IRoomProfileProvider
{
    private readonly IFacilityEvolutionRecordProvider recordProvider;
    private readonly IRoomLayoutCache roomLayoutCache;

    public RoomProfileBuilder(
        IFacilityEvolutionRecordProvider recordProvider,
        IRoomLayoutCache roomLayoutCache)
    {
        this.recordProvider = recordProvider
            ?? throw new ArgumentNullException(nameof(recordProvider));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
    }

    public RoomProfile Build(BuildableObject facility)
    {
        roomLayoutCache.TryGetRoom(facility, out RoomInstance room);
        RoomProfile profile = new RoomProfile(facility, room);
        profile.AddMetric(FacilityEvolutionTerms.RoomArea, profile.Area);
        profile.AddMetric(FacilityEvolutionTerms.DoorCount, room != null ? room.Doors.Count : 0);

        foreach (BuildableObject fixture in CollectFixtures(facility, room))
        {
            AddFixtureContribution(profile, fixture);
        }

        AddRecordContribution(profile, recordProvider.GetRecord(facility));
        CalculateDerivedMetrics(profile);
        FacilityIdentityPressureUtility.ApplyDefaultPressures(profile);
        return profile;
    }

    private static IEnumerable<BuildableObject> CollectFixtures(BuildableObject facility, RoomInstance room)
    {
        if (facility == null)
        {
            yield break;
        }

        if (facility.Grid == null || room == null)
        {
            yield return facility;
            yield break;
        }

        foreach (IGridOccupant occupant in facility.Grid.FindAllOccupants(null))
        {
            if (occupant is not BuildableObject building
                || building == null
                || building.isDestroy
                || building.buildPoses == null
                || !building.buildPoses.Any(room.ContainsCell))
            {
                continue;
            }

            yield return building;
        }
    }

    private static void AddFixtureContribution(RoomProfile profile, BuildableObject fixture)
    {
        if (profile == null || fixture == null || fixture.BuildingData == null)
        {
            return;
        }

        BuildingSO data = fixture.BuildingData;
        FacilityEvolutionContributionData contribution = data.Evolution;
        if (contribution != null && contribution.contributesToRoomProfile)
        {
            profile.AddFixture(fixture);
            if (contribution.tags != null)
            {
                foreach (string tag in contribution.tags)
                {
                    profile.AddTag(tag);
                }
            }

            if (contribution.scores != null)
            {
                foreach (FacilityEvolutionValue score in contribution.scores)
                {
                    profile.AddScore(score.key, score.value);
                }
            }

            if (contribution.metrics != null)
            {
                foreach (FacilityEvolutionValue metric in contribution.metrics)
                {
                    profile.AddMetricDelta(metric.key, metric.value);
                }
            }
        }

        AddRoleContribution(profile, data.Facility);
        AddDefenseContribution(profile, data.Defense);
    }

    private static void AddRoleContribution(RoomProfile profile, FacilityData facility)
    {
        if (facility == null)
        {
            return;
        }

        if (facility.SupportsRole(FacilityRole.Meal))
        {
            profile.AddTag(FacilityEvolutionTerms.Dining);
            profile.AddScore(FacilityEvolutionTerms.Dining, 20f);
            profile.AddScore(FacilityEvolutionTerms.Cooking, 10f);
        }

        if (facility.SupportsRole(FacilityRole.Purchase))
        {
            profile.AddTag("Shop");
            profile.AddScore("Shop", 20f);
        }

        if (facility.SupportsRole(FacilityRole.Rest))
        {
            profile.AddTag(FacilityEvolutionTerms.Rest);
            profile.AddScore(FacilityEvolutionTerms.Rest, 20f);
        }

        if (facility.SupportsRole(FacilityRole.Training))
        {
            profile.AddTag(FacilityEvolutionTerms.Training);
            profile.AddScore(FacilityEvolutionTerms.Training, 20f);
            profile.AddScore(FacilityEvolutionTerms.Combat, 8f);
        }

        if (facility.SupportsRole(FacilityRole.Research))
        {
            profile.AddTag(FacilityEvolutionTerms.Research);
            profile.AddScore(FacilityEvolutionTerms.Research, 20f);
        }

        if (facility.SupportsRole(FacilityRole.Mana))
        {
            profile.AddTag(FacilityEvolutionTerms.Mana);
            profile.AddScore(FacilityEvolutionTerms.Mana, 20f);
        }

        if (facility.SupportsRole(FacilityRole.Logistics))
        {
            profile.AddTag(FacilityEvolutionTerms.Logistics);
            profile.AddScore(FacilityEvolutionTerms.Storage, 20f);
        }

        if (facility.SupportsRole(FacilityRole.Hygiene))
        {
            profile.AddTag(FacilityEvolutionTerms.Hygiene);
            profile.AddScore(FacilityEvolutionTerms.Hygiene, 20f);
        }
    }

    private static void AddDefenseContribution(RoomProfile profile, DefenseFacilityData defense)
    {
        if (defense == null || !defense.IsDefenseFacility)
        {
            return;
        }

        profile.AddTag(FacilityEvolutionTerms.Defense);
        profile.AddTag(FacilityEvolutionTerms.Combat);
        profile.AddScore(FacilityEvolutionTerms.Defense, 20f * Mathf.Max(1, defense.star));
        profile.AddScore(FacilityEvolutionTerms.Combat, 12f * Mathf.Max(1, defense.star));
    }

    private static void AddRecordContribution(RoomProfile profile, FacilityEvolutionRecord record)
    {
        if (profile == null || record == null)
        {
            return;
        }

        foreach (KeyValuePair<string, float> metric in record.Metrics)
        {
            if (FacilityEvolutionMetricOwnership.IsDerivedRoomMetric(metric.Key)
                || (FacilityEvolutionMetricOwnership.IsStructureOwned(metric.Key)
                    && profile.HasMetric(metric.Key)))
            {
                continue;
            }

            profile.AddMetric(metric.Key, metric.Value);
        }

        foreach (KeyValuePair<string, int> token in record.Tokens)
        {
            profile.AddRecordToken(token.Key, token.Value);
        }

        foreach (string entry in record.RecentEvents)
        {
            profile.AddRecentEvent(entry);
        }
    }

    private static void CalculateDerivedMetrics(RoomProfile profile)
    {
        float area = Mathf.Max(1f, profile.Area);
        float seatCount = profile.GetMetric(FacilityEvolutionTerms.SeatCount);
        float tableCount = profile.GetMetric(FacilityEvolutionTerms.TableCount);
        float largeTableCount = profile.GetMetric(FacilityEvolutionTerms.LargeTableCount);
        float counterCount = profile.GetMetric(FacilityEvolutionTerms.CounterCount);
        float privateSeatCount = profile.GetMetric(FacilityEvolutionTerms.PrivateSeatCount);
        float utilityScore = profile.GetScore(FacilityEvolutionTerms.Dining)
            + profile.GetScore(FacilityEvolutionTerms.Cooking)
            + profile.GetScore(FacilityEvolutionTerms.Service)
            + profile.GetScore(FacilityEvolutionTerms.Rest)
            + profile.GetScore(FacilityEvolutionTerms.Storage)
            + profile.GetScore(FacilityEvolutionTerms.Hygiene)
            + profile.GetScore(FacilityEvolutionTerms.Combat)
            + profile.GetScore(FacilityEvolutionTerms.Defense);
        float serviceScore = profile.GetScore(FacilityEvolutionTerms.Dining)
            + profile.GetScore(FacilityEvolutionTerms.Service);

        profile.AddMetric(FacilityEvolutionTerms.FurnitureCount, profile.Fixtures.Count);
        profile.AddMetric(FacilityEvolutionTerms.SeatDensity, seatCount / area);
        profile.AddMetric(FacilityEvolutionTerms.TableDensity, tableCount / area);
        profile.AddMetric(FacilityEvolutionTerms.SeatPerTable, tableCount > 0f ? seatCount / tableCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.LargeTableRatio, tableCount > 0f ? largeTableCount / tableCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.CounterRatio, tableCount + counterCount > 0f ? counterCount / (tableCount + counterCount) : 0f);
        profile.AddMetric(FacilityEvolutionTerms.PrivateSeatRatio, seatCount > 0f ? privateSeatCount / seatCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.AverageSeatSpacing, seatCount > 0f ? area / seatCount : area);
        profile.AddMetric(FacilityEvolutionTerms.LuxuryPerSeat, seatCount > 0f ? profile.GetScore(FacilityEvolutionTerms.Luxury) / seatCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.ServiceScorePerSeat, seatCount > 0f ? serviceScore / seatCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.CookingScorePerSeat, seatCount > 0f ? profile.GetScore(FacilityEvolutionTerms.Cooking) / seatCount : 0f);
        profile.AddMetric(FacilityEvolutionTerms.DecorToUtilityRatio, utilityScore > 0f ? profile.GetScore(FacilityEvolutionTerms.Luxury) / utilityScore : 0f);
        profile.AddMetric(FacilityEvolutionTerms.ClutterScore, profile.Fixtures.Count / area);
    }
}
