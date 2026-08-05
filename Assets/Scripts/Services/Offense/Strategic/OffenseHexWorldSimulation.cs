using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IOffenseWorldSimulation
{
    int WorldSeed { get; }
    int WorldDay { get; }
    float WorldHour { get; }
    OffenseHexCoord DungeonCoord { get; }
    IReadOnlyCollection<OffenseHexTileState> Tiles { get; }
    IReadOnlyCollection<OffenseWorldSiteStateData> Sites { get; }
    IReadOnlyCollection<OffenseUrgentSiteStateData> UrgentSites { get; }
    event Action Changed;

    void Initialize(int runSeed);
    void AdvanceHours(float hours);
    bool TryGetTile(OffenseHexCoord coord, out OffenseHexTileState tile);
    bool TryGetSite(string siteId, out OffenseWorldSiteStateData site);
    bool TryGetUrgentSite(string siteId, out OffenseUrgentSiteStateData site);
    bool TryRevealSite(string siteId);
    bool TryResolveSite(string siteId);
    bool TryRegisterStrategicSite(OffenseWorldSiteStateData site);
    bool TrySpawnUrgentSite(string definitionId, OffenseHexCoord coord, out string siteId);
    bool TryMitigateUrgentSite(string siteId, float amount);
    bool TryDestroyUrgentSite(string siteId);
    bool TryFindPath(
        OffenseHexCoord start,
        OffenseHexCoord goal,
        OffenseTravelProfile profile,
        out IReadOnlyList<OffenseHexCoord> path,
        out float totalCost);
    int GetMinimumStepDistance(OffenseHexCoord start, OffenseHexCoord goal);
    OffenseWorldSaveData Capture();
}

public interface IWorldThreatModifierQuery
{
    OffenseThreatModifierSnapshot GetModifier(OffenseThreatModifierKind kind);
    float GetMultiplier(OffenseThreatModifierKind kind);
    IReadOnlyList<OffenseThreatModifierSnapshot> GetActiveModifiers();
}

internal sealed class OffenseHexWorldRestoreCandidate
{
    internal OffenseHexWorldRestoreCandidate(
        int worldSeed,
        int worldDay,
        float worldHour,
        Dictionary<OffenseHexCoord, OffenseHexTileState> tiles,
        Dictionary<string, OffenseWorldSiteStateData> sites,
        Dictionary<string, OffenseUrgentSiteStateData> urgentSites,
        int nextDynamicSiteSequence,
        int nextUrgentSiteSequence)
    {
        WorldSeed = worldSeed;
        WorldDay = worldDay;
        WorldHour = worldHour;
        Tiles = tiles;
        Sites = sites;
        UrgentSites = urgentSites;
        NextDynamicSiteSequence = nextDynamicSiteSequence;
        NextUrgentSiteSequence = nextUrgentSiteSequence;
    }

    internal int WorldSeed { get; }
    internal int WorldDay { get; }
    internal float WorldHour { get; }
    internal Dictionary<OffenseHexCoord, OffenseHexTileState> Tiles { get; }
    internal Dictionary<string, OffenseWorldSiteStateData> Sites { get; }
    internal Dictionary<string, OffenseUrgentSiteStateData> UrgentSites { get; }
    internal int NextDynamicSiteSequence { get; }
    internal int NextUrgentSiteSequence { get; }
}

public sealed class OffenseHexWorldSimulation :
    IOffenseWorldSimulation,
    IWorldThreatModifierQuery,
    IStartable,
    IDisposable
{
    public const int DefaultRadius = 9;
    public const int InitialRevealedSiteCount = 2;
    public const string RivalDungeonSiteId = "rival_dungeon";
    public const string TruthCoreSiteId = "truth_core";

    private const float SignalHours = 12f;
    private const float WarningHours = 18f;
    private const float CrisisHours = 24f;
    private const float WithdrawingHours = 6f;

    private readonly RunVariableRuntime runVariables;
    private readonly IOffenseContentCatalog content;
    private readonly IGameEventBus events;
    private Dictionary<OffenseHexCoord, OffenseHexTileState> tiles =
        new Dictionary<OffenseHexCoord, OffenseHexTileState>();
    private Dictionary<string, OffenseWorldSiteStateData> sites =
        new Dictionary<string, OffenseWorldSiteStateData>(StringComparer.Ordinal);
    private Dictionary<string, OffenseUrgentSiteStateData> urgentSites =
        new Dictionary<string, OffenseUrgentSiteStateData>(StringComparer.Ordinal);
    private IDisposable daySubscription;
    private int nextDynamicSiteSequence;
    private int nextUrgentSiteSequence;

    public OffenseHexWorldSimulation(
        DungeonSceneRuntimeReferences sceneRuntimes,
        IOffenseContentCatalog content,
        IGameEventBus events)
    {
        runVariables = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseHexWorldSimulation)} requires a loaded {nameof(RunVariableRuntime)}.");
        this.content = content
            ?? throw new ArgumentNullException(nameof(content));
        this.events = events
            ?? throw new ArgumentNullException(nameof(events));
    }

    public int WorldSeed { get; private set; }
    public int WorldDay { get; private set; } = 1;
    public float WorldHour { get; private set; }
    public OffenseHexCoord DungeonCoord => new OffenseHexCoord(0, 0);
    public IReadOnlyCollection<OffenseHexTileState> Tiles => tiles.Values;
    public IReadOnlyCollection<OffenseWorldSiteStateData> Sites => sites.Values;
    public IReadOnlyCollection<OffenseUrgentSiteStateData> UrgentSites =>
        urgentSites.Values;
    public event Action Changed;

    public void Start()
    {
        int seed = runVariables.RunSeed;
        if (WorldSeed == 0)
        {
            Initialize(seed);
        }

        daySubscription = events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);
    }

    public void Dispose()
    {
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Initialize(int runSeed)
    {
        WorldSeed = runSeed == 0 ? 1 : runSeed;
        WorldDay = 1;
        WorldHour = 0f;
        nextDynamicSiteSequence = 0;
        nextUrgentSiteSequence = 0;
        tiles.Clear();
        sites.Clear();
        urgentSites.Clear();

        GenerateTiles(DefaultRadius);
        BuildRoadTo(new OffenseHexCoord(6, -3));
        BuildRoadTo(new OffenseHexCoord(-7, 4));
        AddFixedBossSites();
        PopulateDynamicSites(10, WorldDay);
        RevealInitialApproachSites();
        Changed?.Invoke();
    }

    public void AdvanceHours(float hours)
    {
        float remaining = Mathf.Max(0f, hours);
        if (remaining <= 0f)
        {
            return;
        }

        WorldHour += remaining;
        while (WorldHour >= 24f)
        {
            WorldHour -= 24f;
            WorldDay++;
            TickDynamicSitesForDay();
        }

        AdvanceUrgentSites(remaining);
        Changed?.Invoke();
    }

    public bool TryGetTile(OffenseHexCoord coord, out OffenseHexTileState tile) =>
        tiles.TryGetValue(coord, out tile);

    public bool TryGetSite(string siteId, out OffenseWorldSiteStateData site)
    {
        site = null;
        return !string.IsNullOrWhiteSpace(siteId)
            && sites.TryGetValue(siteId, out site);
    }

    public bool TryGetUrgentSite(
        string siteId,
        out OffenseUrgentSiteStateData site)
    {
        site = null;
        return !string.IsNullOrWhiteSpace(siteId)
            && urgentSites.TryGetValue(siteId, out site);
    }

    public bool TryRevealSite(string siteId)
    {
        if (!TryGetSite(siteId, out OffenseWorldSiteStateData site)
            || site.state != OffenseWorldSiteState.Hidden)
        {
            return false;
        }

        site.state = OffenseWorldSiteState.Revealed;
        Changed?.Invoke();
        return true;
    }

    public bool TryResolveSite(string siteId)
    {
        if (!TryGetSite(siteId, out OffenseWorldSiteStateData site)
            || !site.IsActive)
        {
            return false;
        }

        site.state = OffenseWorldSiteState.Resolved;
        Changed?.Invoke();
        return true;
    }

    public bool TryRegisterStrategicSite(OffenseWorldSiteStateData site)
    {
        if (site == null
            || string.IsNullOrWhiteSpace(site.siteId)
            || !tiles.TryGetValue(site.Coord, out OffenseHexTileState tile)
            || tile.blocked)
        {
            return false;
        }

        if (sites.ContainsKey(site.siteId))
        {
            return true;
        }

        OffenseWorldSiteStateData registered = CloneSite(site);
        registered.regionId = string.IsNullOrWhiteSpace(registered.regionId)
            ? tile.regionId
            : registered.regionId;
        registered.createdDay = Mathf.Max(1, registered.createdDay);
        registered.expiresDay = registered.expiresDay <= 0
            ? int.MaxValue
            : registered.expiresDay;
        registered.state = registered.state == OffenseWorldSiteState.Hidden
            ? OffenseWorldSiteState.Revealed
            : registered.state;
        sites.Add(registered.siteId, registered);
        Changed?.Invoke();
        return true;
    }

    public bool TrySpawnUrgentSite(
        string definitionId,
        OffenseHexCoord coord,
        out string siteId)
    {
        siteId = string.Empty;
        OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
            .FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.urgentSiteId,
                    definitionId,
                    StringComparison.Ordinal));
        if (definition == null
            || !tiles.TryGetValue(coord, out OffenseHexTileState tile)
            || tile.blocked
            || GetMinimumStepDistance(DungeonCoord, coord) < 1)
        {
            return false;
        }

        int minimumSteps = GetMinimumStepDistance(DungeonCoord, coord);
        if (minimumSteps == int.MaxValue || minimumSteps > 12)
        {
            return false;
        }

        siteId = $"urgent:{WorldDay}:{nextUrgentSiteSequence++}";
        urgentSites.Add(siteId, new OffenseUrgentSiteStateData
        {
            siteId = siteId,
            definitionId = definition.urgentSiteId,
            displayName = definition.displayName,
            q = coord.Q,
            r = coord.R,
            modifierKind = definition.modifierKind,
            stage = OffenseUrgentSiteStage.Signal,
            stageElapsedHours = 0f,
            mitigation = 0f
        });
        Changed?.Invoke();
        return true;
    }

    public bool TryMitigateUrgentSite(string siteId, float amount)
    {
        if (!urgentSites.TryGetValue(
                siteId ?? string.Empty,
                out OffenseUrgentSiteStateData site)
            || !site.IsActive)
        {
            return false;
        }

        OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
            .FirstOrDefault(candidate =>
                candidate != null
                && candidate.urgentSiteId == site.definitionId);
        float maximumMitigation = definition != null
            ? Mathf.Clamp(definition.maximumMitigation, 0f, 0.6f)
            : 0.6f;
        site.mitigation = Mathf.Clamp(
            site.mitigation + Mathf.Max(0f, amount),
            0f,
            maximumMitigation);
        Changed?.Invoke();
        return true;
    }

    public bool TryDestroyUrgentSite(string siteId)
    {
        if (!urgentSites.TryGetValue(
                siteId ?? string.Empty,
                out OffenseUrgentSiteStateData site)
            || !site.IsActive)
        {
            return false;
        }

        site.stage = OffenseUrgentSiteStage.Destroyed;
        site.stageElapsedHours = 0f;
        Changed?.Invoke();
        return true;
    }

    public bool TryFindPath(
        OffenseHexCoord start,
        OffenseHexCoord goal,
        OffenseTravelProfile profile,
        out IReadOnlyList<OffenseHexCoord> path,
        out float totalCost)
    {
        path = Array.Empty<OffenseHexCoord>();
        totalCost = 0f;
        if (!IsPassable(start) || !IsPassable(goal))
        {
            return false;
        }

        MinHeap frontier = new MinHeap();
        Dictionary<OffenseHexCoord, float> costs =
            new Dictionary<OffenseHexCoord, float> { [start] = 0f };
        Dictionary<OffenseHexCoord, OffenseHexCoord> previous =
            new Dictionary<OffenseHexCoord, OffenseHexCoord>();
        frontier.Enqueue(start, 0f);

        while (frontier.Count > 0)
        {
            OffenseHexCoord current = frontier.Dequeue();
            if (current == goal)
            {
                List<OffenseHexCoord> result = ReconstructPath(previous, start, goal);
                path = result;
                totalCost = costs[goal];
                return true;
            }

            for (int direction = 0; direction < 6; direction++)
            {
                OffenseHexCoord next = current.Neighbor(direction);
                if (!tiles.TryGetValue(next, out OffenseHexTileState nextTile)
                    || nextTile.blocked)
                {
                    continue;
                }

                float newCost = costs[current] + GetTravelCost(nextTile, profile);
                if (costs.TryGetValue(next, out float knownCost)
                    && newCost >= knownCost - 0.0001f)
                {
                    continue;
                }

                costs[next] = newCost;
                previous[next] = current;
                float priority = newCost + next.DistanceTo(goal);
                frontier.Enqueue(next, priority);
            }
        }

        return false;
    }

    public int GetMinimumStepDistance(OffenseHexCoord start, OffenseHexCoord goal)
    {
        if (!IsPassable(start) || !IsPassable(goal))
        {
            return int.MaxValue;
        }

        Queue<OffenseHexCoord> frontier = new Queue<OffenseHexCoord>();
        Dictionary<OffenseHexCoord, int> distance =
            new Dictionary<OffenseHexCoord, int> { [start] = 0 };
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            OffenseHexCoord current = frontier.Dequeue();
            if (current == goal)
            {
                return distance[current];
            }

            for (int direction = 0; direction < 6; direction++)
            {
                OffenseHexCoord next = current.Neighbor(direction);
                if (!IsPassable(next) || distance.ContainsKey(next))
                {
                    continue;
                }

                distance.Add(next, distance[current] + 1);
                frontier.Enqueue(next);
            }
        }

        return int.MaxValue;
    }

    public OffenseThreatModifierSnapshot GetModifier(
        OffenseThreatModifierKind kind)
    {
        List<OffenseUrgentSiteStateData> sources = urgentSites.Values
            .Where(site => site != null
                && site.IsActive
                && site.modifierKind == kind)
            .OrderByDescending(site => site.Intensity)
            .ThenBy(site => site.siteId, StringComparer.Ordinal)
            .ToList();

        float raw = 0f;
        float weightedMitigation = 0f;
        for (int index = 0; index < sources.Count; index++)
        {
            float stacking = index switch
            {
                0 => 1f,
                1 => 0.5f,
                _ => 0.25f
            };
            float sourceStrength = sources[index].Intensity * stacking;
            raw += sourceStrength;
            weightedMitigation += sourceStrength * Mathf.Clamp01(sources[index].mitigation);
        }

        float cap = GetModifierCap(kind);
        raw = Mathf.Min(raw, cap);
        float mitigation = raw > 0f
            ? Mathf.Clamp(weightedMitigation / raw, 0f, 0.6f)
            : 0f;
        return new OffenseThreatModifierSnapshot(
            kind,
            raw,
            mitigation,
            raw * (1f - mitigation),
            sources.Count);
    }

    public float GetMultiplier(OffenseThreatModifierKind kind)
    {
        OffenseThreatModifierSnapshot snapshot = GetModifier(kind);
        return kind is OffenseThreatModifierKind.AutomatedDefense
            or OffenseThreatModifierKind.Mood
            or OffenseThreatModifierKind.Rest
            or OffenseThreatModifierKind.Sanitation
            or OffenseThreatModifierKind.Lighting
            or OffenseThreatModifierKind.Accuracy
            or OffenseThreatModifierKind.InvasionWarning
            ? Mathf.Max(0.1f, 1f - snapshot.EffectiveStrength)
            : 1f + snapshot.EffectiveStrength;
    }

    public IReadOnlyList<OffenseThreatModifierSnapshot> GetActiveModifiers()
    {
        List<OffenseThreatModifierSnapshot> result =
            new List<OffenseThreatModifierSnapshot>();
        Array values = Enum.GetValues(typeof(OffenseThreatModifierKind));
        foreach (OffenseThreatModifierKind kind in values)
        {
            OffenseThreatModifierSnapshot snapshot = GetModifier(kind);
            if (snapshot.SourceCount > 0)
            {
                result.Add(snapshot);
            }
        }

        return result;
    }

    public OffenseWorldSaveData Capture()
    {
        return new OffenseWorldSaveData
        {
            version = OffenseWorldSaveData.CurrentVersion,
            worldSeed = WorldSeed,
            worldDay = WorldDay,
            worldHour = WorldHour,
            tiles = tiles.Values
                .OrderBy(tile => tile.Coord)
                .Select(CloneTile)
                .ToList(),
            sites = sites.Values
                .OrderBy(site => site.siteId, StringComparer.Ordinal)
                .Select(CloneSite)
                .ToList(),
            urgentSites = urgentSites.Values
                .OrderBy(site => site.siteId, StringComparer.Ordinal)
                .Select(CloneUrgentSite)
                .ToList()
        };
    }

    internal OffenseHexWorldRestoreCandidate PrepareRestore(
        OffenseWorldSaveData saveData)
    {
        if (saveData == null
            || saveData.version != OffenseWorldSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported offense world payload version {saveData?.version.ToString() ?? "null"}; expected {OffenseWorldSaveData.CurrentVersion}.");
        }

        if (saveData.worldSeed == 0
            || saveData.worldDay < 1
            || saveData.worldHour < 0f
            || saveData.worldHour >= 24f
            || float.IsNaN(saveData.worldHour)
            || float.IsInfinity(saveData.worldHour))
        {
            throw new InvalidOperationException(
                "Offense world time or seed is non-canonical.");
        }

        Dictionary<OffenseHexCoord, OffenseHexTileState> restoredTiles = new();
        Dictionary<string, OffenseWorldSiteStateData> restoredSites =
            new(StringComparer.Ordinal);
        Dictionary<string, OffenseUrgentSiteStateData> restoredUrgentSites =
            new(StringComparer.Ordinal);

        foreach (OffenseHexTileState tile in saveData.tiles)
        {
            if (tile == null || restoredTiles.ContainsKey(tile.Coord))
            {
                throw new InvalidOperationException(
                    $"Duplicate or null offense world tile '{tile?.Coord.ToString() ?? "null"}'.");
            }

            restoredTiles.Add(tile.Coord, CloneTile(tile));
        }

        foreach (OffenseWorldSiteStateData site in saveData.sites)
        {
            if (site == null
                || string.IsNullOrWhiteSpace(site.siteId)
                || !restoredTiles.ContainsKey(site.Coord)
                || restoredSites.ContainsKey(site.siteId))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense site '{site?.siteId ?? "null"}'.");
            }

            restoredSites.Add(site.siteId, CloneSite(site));
        }

        foreach (OffenseUrgentSiteStateData site in saveData.urgentSites)
        {
            if (site == null
                || string.IsNullOrWhiteSpace(site.siteId)
                || !restoredTiles.ContainsKey(site.Coord)
                || restoredUrgentSites.ContainsKey(site.siteId))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate urgent offense site '{site?.siteId ?? "null"}'.");
            }

            restoredUrgentSites.Add(site.siteId, CloneUrgentSite(site));
        }

        int restoredDynamicSequence = ResolveNextSequence(
            restoredSites.Keys,
            "dynamic:");
        int restoredUrgentSequence = ResolveNextSequence(
            restoredUrgentSites.Keys,
            "urgent:");

        return new OffenseHexWorldRestoreCandidate(
            saveData.worldSeed,
            saveData.worldDay,
            saveData.worldHour,
            restoredTiles,
            restoredSites,
            restoredUrgentSites,
            restoredDynamicSequence,
            restoredUrgentSequence);
    }

    internal void PublishRestore(OffenseHexWorldRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        WorldSeed = candidate.WorldSeed;
        WorldDay = candidate.WorldDay;
        WorldHour = candidate.WorldHour;
        tiles = candidate.Tiles;
        sites = candidate.Sites;
        urgentSites = candidate.UrgentSites;
        nextDynamicSiteSequence = candidate.NextDynamicSiteSequence;
        nextUrgentSiteSequence = candidate.NextUrgentSiteSequence;
    }

    private void OnDayStarted(OperatingDayStartedEvent day)
    {
        int targetDay = Mathf.Max(WorldDay, day.day);
        int elapsedDays = Mathf.Max(0, targetDay - WorldDay);
        if (elapsedDays > 0)
        {
            AdvanceHours(elapsedDays * 24f);
        }
    }

    private void GenerateTiles(int radius)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int minimumR = Mathf.Max(-radius, -q - radius);
            int maximumR = Mathf.Min(radius, -q + radius);
            for (int r = minimumR; r <= maximumR; r++)
            {
                OffenseHexCoord coord = new OffenseHexCoord(q, r);
                uint hash = Hash(WorldSeed, q, r, 17);
                OffenseHexTerrain terrain = ResolveTerrain(hash, coord);
                bool river = IsRiver(coord, hash);
                tiles.Add(coord, new OffenseHexTileState
                {
                    q = q,
                    r = r,
                    terrain = river ? OffenseHexTerrain.River : terrain,
                    regionId = ResolveRegionId(coord),
                    hasRoad = false,
                    hasRiver = river,
                    blocked = terrain == OffenseHexTerrain.Mountain
                        && (hash & 7u) == 0u
                });
            }
        }

        tiles[DungeonCoord].blocked = false;
        tiles[DungeonCoord].terrain = OffenseHexTerrain.Plains;
    }

    private void AddFixedBossSites()
    {
        AddFixedSite(
            RivalDungeonSiteId,
            "fixed:rival-dungeon",
            "경쟁 던전 전초권",
            new OffenseHexCoord(6, -3),
            OffenseRegionRuntime.RivalOutpostRegionId,
            OffenseRegionRuntime.RivalFactionId,
            StrategicPressureAxis.Manpower,
            20f,
            5);
        AddFixedSite(
            TruthCoreSiteId,
            "fixed:truth-core",
            "봉인 기록 심장부",
            new OffenseHexCoord(-7, 4),
            OffenseRegionRuntime.SealedZoneRegionId,
            OffenseRegionRuntime.SealFactionId,
            StrategicPressureAxis.None,
            0f,
            8);
    }

    private void AddFixedSite(
        string siteId,
        string archetypeId,
        string displayName,
        OffenseHexCoord coord,
        string regionId,
        string factionId,
        StrategicPressureAxis axis,
        float pressureAmount,
        int strength)
    {
        if (tiles.TryGetValue(coord, out OffenseHexTileState tile))
        {
            tile.blocked = false;
        }

        sites.Add(siteId, new OffenseWorldSiteStateData
        {
            siteId = siteId,
            archetypeId = archetypeId,
            displayName = displayName,
            q = coord.Q,
            r = coord.R,
            regionId = regionId,
            factionId = factionId,
            state = OffenseWorldSiteState.Revealed,
            fixedBoss = true,
            strength = strength,
            createdDay = 1,
            expiresDay = 0,
            pressureAxis = axis,
            pressureAmount = pressureAmount
        });
    }

    private void PopulateDynamicSites(int desiredCount, int createdDay)
    {
        OffenseSiteArchetypeSO[] archetypes = content.SiteArchetypes
            .Where(archetype =>
                archetype != null && archetype.dynamicSpawnEligible)
            .ToArray();
        if (archetypes.Length == 0)
        {
            return;
        }

        List<OffenseHexTileState> candidates = tiles.Values
            .Where(tile => !tile.blocked
                && tile.Coord.DistanceTo(DungeonCoord) >= 2
                && !HasActiveSiteAt(tile.Coord))
            .OrderBy(tile => Hash(WorldSeed, tile.q, tile.r, createdDay + 911))
            .ToList();

        int spawnCount = Mathf.Min(desiredCount, candidates.Count);
        for (int index = 0; index < spawnCount; index++)
        {
            OffenseSiteArchetypeSO archetype = archetypes[
                PositiveModulo(
                    unchecked((int)Hash(WorldSeed, index, createdDay, 31)),
                    archetypes.Length)];
            OffenseHexTileState tile = candidates[index];
            int lifetimeRange = Mathf.Max(
                1,
                archetype.maximumLifetimeDays - archetype.minimumLifetimeDays + 1);
            int lifetime = archetype.minimumLifetimeDays
                + PositiveModulo(
                    unchecked((int)Hash(WorldSeed, tile.q, tile.r, 701)),
                    lifetimeRange);
            int strengthRange = Mathf.Max(
                1,
                archetype.maximumStrength - archetype.minimumStrength + 1);
            int strength = archetype.minimumStrength
                + PositiveModulo(
                    unchecked((int)Hash(WorldSeed, tile.r, tile.q, 409)),
                    strengthRange);
            string siteId = $"dynamic:{createdDay}:{nextDynamicSiteSequence++}";
            sites.Add(siteId, new OffenseWorldSiteStateData
            {
                siteId = siteId,
                archetypeId = archetype.siteTypeId,
                displayName = archetype.displayName,
                q = tile.q,
                r = tile.r,
                regionId = tile.regionId,
                factionId = archetype.factionId,
                state = archetype.hiddenUntilDiscovered
                    ? OffenseWorldSiteState.Hidden
                    : OffenseWorldSiteState.Revealed,
                fixedBoss = false,
                strength = strength,
                createdDay = createdDay,
                expiresDay = createdDay + lifetime,
                pressureAxis = archetype.pressureAxis,
                pressureAmount = archetype.pressureAmount
            });
        }
    }

    private void RevealInitialApproachSites()
    {
        OffenseWorldSiteStateData[] approachSites = sites.Values
            .Where(site => site != null
                && !site.fixedBoss
                && site.IsActive)
            .OrderBy(site => site.strength > 4 ? 1 : 0)
            .ThenBy(site => site.Coord.DistanceTo(DungeonCoord))
            .ThenBy(site => Hash(
                WorldSeed,
                site.q,
                site.r,
                1701))
            .ThenBy(site => site.siteId, StringComparer.Ordinal)
            .Take(InitialRevealedSiteCount)
            .ToArray();

        foreach (OffenseWorldSiteStateData site in approachSites)
        {
            site.strength = Mathf.Min(site.strength, 4);
            site.state = OffenseWorldSiteState.Revealed;
        }
    }

    private void TickDynamicSitesForDay()
    {
        foreach (OffenseWorldSiteStateData site in sites.Values)
        {
            if (site == null
                || site.fixedBoss
                || !site.IsActive
                || site.expiresDay <= 0
                || WorldDay < site.expiresDay)
            {
                continue;
            }

            site.state = OffenseWorldSiteState.Expired;
        }

        int activeDynamicCount = sites.Values.Count(site =>
            site != null && !site.fixedBoss && site.IsActive);
        if (activeDynamicCount < 8)
        {
            PopulateDynamicSites(10 - activeDynamicCount, WorldDay);
        }

        if (WorldDay % 3 == 0
            && urgentSites.Values.Count(site => site != null && site.IsActive) < 2)
        {
            TrySpawnScheduledUrgentSite();
        }
    }

    private void TrySpawnScheduledUrgentSite()
    {
        IReadOnlyList<OffenseUrgentSiteDefinitionSO> definitions =
            content.UrgentSites;
        if (definitions.Count == 0)
        {
            return;
        }

        OffenseUrgentSiteDefinitionSO definition = definitions[
            PositiveModulo(WorldSeed + WorldDay, definitions.Count)];
        OffenseHexTileState tile = tiles.Values
            .Where(candidate => !candidate.blocked
                && candidate.Coord.DistanceTo(DungeonCoord) >= 3
                && candidate.Coord.DistanceTo(DungeonCoord) <= 8
                && !HasActiveSiteAt(candidate.Coord))
            .OrderBy(candidate => Hash(
                WorldSeed,
                candidate.q,
                candidate.r,
                WorldDay * 53))
            .FirstOrDefault();
        if (tile != null)
        {
            TrySpawnUrgentSite(definition.urgentSiteId, tile.Coord, out _);
        }
    }

    private void AdvanceUrgentSites(float hours)
    {
        foreach (OffenseUrgentSiteStateData site in urgentSites.Values)
        {
            if (site == null || !site.IsActive)
            {
                continue;
            }

            float remaining = hours;
            while (remaining > 0f && site.IsActive)
            {
                float duration = GetStageDuration(site.stage);
                float available = Mathf.Max(0f, duration - site.stageElapsedHours);
                float consumed = Mathf.Min(remaining, available);
                site.stageElapsedHours += consumed;
                remaining -= consumed;
                if (site.stageElapsedHours + 0.0001f < duration)
                {
                    break;
                }

                site.stageElapsedHours = 0f;
                site.stage = GetNextStage(site.stage);
            }
        }
    }

    private bool HasActiveSiteAt(OffenseHexCoord coord)
    {
        return sites.Values.Any(site => site != null && site.IsActive && site.Coord == coord)
            || urgentSites.Values.Any(site => site != null && site.IsActive && site.Coord == coord);
    }

    private bool IsPassable(OffenseHexCoord coord)
    {
        return tiles.TryGetValue(coord, out OffenseHexTileState tile)
            && !tile.blocked;
    }

    private static float GetTravelCost(
        OffenseHexTileState tile,
        OffenseTravelProfile profile)
    {
        float terrain = tile.terrain switch
        {
            OffenseHexTerrain.Forest => 1.35f,
            OffenseHexTerrain.Hills => 1.6f,
            OffenseHexTerrain.Marsh => 1.85f,
            OffenseHexTerrain.Mountain => 2.5f,
            OffenseHexTerrain.River => 2.1f,
            _ => 1f
        };
        if (tile.hasRoad)
        {
            terrain *= profile.RoadMultiplier;
        }

        return Mathf.Max(
            0.1f,
            terrain
            * profile.WeatherMultiplier
            * profile.InjuryMultiplier
            * profile.LoadMultiplier);
    }

    private void BuildRoadTo(OffenseHexCoord goal)
    {
        OffenseHexCoord current = DungeonCoord;
        int guard = 0;
        while (current != goal && guard++ < 64)
        {
            if (tiles.TryGetValue(current, out OffenseHexTileState currentTile))
            {
                currentTile.hasRoad = true;
                currentTile.blocked = false;
            }

            OffenseHexCoord next = current;
            int bestDistance = current.DistanceTo(goal);
            for (int direction = 0; direction < 6; direction++)
            {
                OffenseHexCoord candidate = current.Neighbor(direction);
                int distance = candidate.DistanceTo(goal);
                if (tiles.ContainsKey(candidate) && distance < bestDistance)
                {
                    next = candidate;
                    bestDistance = distance;
                }
            }

            if (next == current)
            {
                break;
            }

            current = next;
        }

        if (tiles.TryGetValue(goal, out OffenseHexTileState goalTile))
        {
            goalTile.hasRoad = true;
            goalTile.blocked = false;
        }
    }

    private static List<OffenseHexCoord> ReconstructPath(
        IReadOnlyDictionary<OffenseHexCoord, OffenseHexCoord> previous,
        OffenseHexCoord start,
        OffenseHexCoord goal)
    {
        List<OffenseHexCoord> result = new List<OffenseHexCoord>();
        OffenseHexCoord current = goal;
        while (current != start)
        {
            result.Add(current);
            current = previous[current];
        }

        result.Reverse();
        return result;
    }

    private static OffenseHexTerrain ResolveTerrain(uint hash, OffenseHexCoord coord)
    {
        if (coord.DistanceTo(new OffenseHexCoord(0, 0)) <= 1)
        {
            return OffenseHexTerrain.Plains;
        }

        int roll = (int)(hash % 100u);
        if (roll < 43) return OffenseHexTerrain.Plains;
        if (roll < 65) return OffenseHexTerrain.Forest;
        if (roll < 80) return OffenseHexTerrain.Hills;
        if (roll < 91) return OffenseHexTerrain.Marsh;
        return OffenseHexTerrain.Mountain;
    }

    private static bool IsRiver(OffenseHexCoord coord, uint hash)
    {
        return coord.DistanceTo(new OffenseHexCoord(0, 0)) > 1
            && ((coord.Q + coord.R * 2 + 19) % 7 == 0)
            && (hash & 3u) != 0u;
    }

    private static string ResolveRegionId(OffenseHexCoord coord)
    {
        if (coord.Q >= 3)
        {
            return OffenseRegionRuntime.RivalOutpostRegionId;
        }

        if (coord.Q <= -3)
        {
            return OffenseRegionRuntime.SealedZoneRegionId;
        }

        return OffenseRegionRuntime.BorderTradeRegionId;
    }

    private static float GetStageDuration(OffenseUrgentSiteStage stage)
    {
        return stage switch
        {
            OffenseUrgentSiteStage.Signal => SignalHours,
            OffenseUrgentSiteStage.Warning => WarningHours,
            OffenseUrgentSiteStage.Crisis => CrisisHours,
            OffenseUrgentSiteStage.Withdrawing => WithdrawingHours,
            _ => float.PositiveInfinity
        };
    }

    private static OffenseUrgentSiteStage GetNextStage(OffenseUrgentSiteStage stage)
    {
        return stage switch
        {
            OffenseUrgentSiteStage.Signal => OffenseUrgentSiteStage.Warning,
            OffenseUrgentSiteStage.Warning => OffenseUrgentSiteStage.Crisis,
            OffenseUrgentSiteStage.Crisis => OffenseUrgentSiteStage.Withdrawing,
            OffenseUrgentSiteStage.Withdrawing => OffenseUrgentSiteStage.Expired,
            _ => stage
        };
    }

    private static float GetModifierCap(OffenseThreatModifierKind kind)
    {
        return kind switch
        {
            OffenseThreatModifierKind.Temperature => 1.5f,
            OffenseThreatModifierKind.FuelConsumption => 1.5f,
            OffenseThreatModifierKind.Disease => 1.25f,
            OffenseThreatModifierKind.DefenseEvasion => 0.75f,
            _ => 0.8f
        };
    }

    private static uint Hash(int seed, int a, int b, int salt)
    {
        unchecked
        {
            uint value = (uint)seed;
            value ^= (uint)a * 0x9E3779B9u;
            value = (value << 13) | (value >> 19);
            value ^= (uint)b * 0x85EBCA6Bu;
            value ^= (uint)salt * 0xC2B2AE35u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static int ResolveNextSequence(
        IEnumerable<string> ids,
        string prefix)
    {
        int next = 0;
        foreach (string id in ids.Where(value => value != null
                     && value.StartsWith(prefix, StringComparison.Ordinal)))
        {
            int separator = id.LastIndexOf(':');
            if (separator >= 0
                && int.TryParse(id.Substring(separator + 1), out int sequence)
                && sequence >= 0)
            {
                next = Math.Max(next, sequence + 1);
            }
        }
        return next;
    }

    private static OffenseHexTileState CloneTile(OffenseHexTileState source)
    {
        return new OffenseHexTileState
        {
            q = source.q,
            r = source.r,
            terrain = source.terrain,
            regionId = source.regionId,
            hasRoad = source.hasRoad,
            hasRiver = source.hasRiver,
            blocked = source.blocked
        };
    }

    private static OffenseWorldSiteStateData CloneSite(
        OffenseWorldSiteStateData source)
    {
        return new OffenseWorldSiteStateData
        {
            siteId = source.siteId,
            archetypeId = source.archetypeId,
            displayName = source.displayName,
            q = source.q,
            r = source.r,
            regionId = source.regionId,
            factionId = source.factionId,
            state = source.state,
            fixedBoss = source.fixedBoss,
            strength = source.strength,
            createdDay = source.createdDay,
            expiresDay = source.expiresDay,
            pressureAxis = source.pressureAxis,
            pressureAmount = source.pressureAmount
        };
    }

    private static OffenseUrgentSiteStateData CloneUrgentSite(
        OffenseUrgentSiteStateData source)
    {
        return new OffenseUrgentSiteStateData
        {
            siteId = source.siteId,
            definitionId = source.definitionId,
            displayName = source.displayName,
            q = source.q,
            r = source.r,
            modifierKind = source.modifierKind,
            stage = source.stage,
            stageElapsedHours = source.stageElapsedHours,
            mitigation = source.mitigation
        };
    }

    private sealed class MinHeap
    {
        private readonly List<Entry> entries = new List<Entry>();
        private long sequence;

        public int Count => entries.Count;

        public void Enqueue(OffenseHexCoord coord, float priority)
        {
            entries.Add(new Entry(coord, priority, sequence++));
            int index = entries.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(entries[parent], entries[index]) <= 0)
                {
                    break;
                }

                (entries[parent], entries[index]) = (entries[index], entries[parent]);
                index = parent;
            }
        }

        public OffenseHexCoord Dequeue()
        {
            Entry result = entries[0];
            int lastIndex = entries.Count - 1;
            entries[0] = entries[lastIndex];
            entries.RemoveAt(lastIndex);
            int index = 0;
            while (index < entries.Count)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                if (left >= entries.Count)
                {
                    break;
                }

                int smallest = right < entries.Count
                    && Compare(entries[right], entries[left]) < 0
                        ? right
                        : left;
                if (Compare(entries[index], entries[smallest]) <= 0)
                {
                    break;
                }

                (entries[index], entries[smallest]) =
                    (entries[smallest], entries[index]);
                index = smallest;
            }

            return result.Coord;
        }

        private static int Compare(Entry left, Entry right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            int coord = left.Coord.CompareTo(right.Coord);
            return coord != 0 ? coord : left.Sequence.CompareTo(right.Sequence);
        }

        private readonly struct Entry
        {
            public Entry(OffenseHexCoord coord, float priority, long sequence)
            {
                Coord = coord;
                Priority = priority;
                Sequence = sequence;
            }

            public OffenseHexCoord Coord { get; }
            public float Priority { get; }
            public long Sequence { get; }
        }
    }
}
