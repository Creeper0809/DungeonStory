using System;
using System.Collections.Generic;
using System.Diagnostics;
using DungeonStory.Foundation;
using UnityEngine;

public interface IFacilityCandidateCache : IBuildingFacilityStateChangePort
{
    int DynamicStateVersion { get; }
    IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role);
    bool TryGetNearestCandidates(
        Grid grid,
        FacilityRole role,
        Vector2Int origin,
        int maximumCount,
        double budgetMilliseconds,
        out IReadOnlyList<BuildableObject> candidates);
    IReadOnlyList<BuildableObject> GetWorkCandidates(
        Grid grid,
        FacilityWorkType workType);
    FacilityRole GetAvailableRoles(Grid grid);
    bool HasPendingIndexBuild { get; }
    int CandidateIndexVersion { get; }
    int AdvanceIndex(double budgetMilliseconds);
    void Clear();
}

public sealed class FacilityCandidateCacheStore :
    IFacilityCandidateCache,
    IBuildingFacilityStateChangePort
{
    private sealed class GridFacilityCache
    {
        public FacilityRole AvailableRoles;
        public bool FallbackBuilt;
        public int FallbackGridVersion = -1;
        public readonly HashSet<BuildableObject> IndexedBuildings =
            new HashSet<BuildableObject>();
        public readonly Dictionary<FacilityRole, IReadOnlyList<BuildableObject>> CandidatesByRole =
            new Dictionary<FacilityRole, IReadOnlyList<BuildableObject>>();
        public readonly Dictionary<FacilityWorkType, IReadOnlyList<BuildableObject>> CandidatesByWorkType =
            new Dictionary<FacilityWorkType, IReadOnlyList<BuildableObject>>();
        public readonly Dictionary<NearestCandidateKey, NearestCandidateScan> NearestCandidates =
            new Dictionary<NearestCandidateKey, NearestCandidateScan>();
    }

    private readonly struct NearestCandidateKey : IEquatable<NearestCandidateKey>
    {
        public NearestCandidateKey(
            FacilityRole role,
            Vector2Int origin,
            int maximumCount)
        {
            Role = role;
            BucketX = Mathf.FloorToInt((float)origin.x / NearestBucketWidth);
            BucketY = Mathf.FloorToInt((float)origin.y / NearestBucketHeight);
            MaximumCount = maximumCount;
        }

        public FacilityRole Role { get; }
        public int BucketX { get; }
        public int BucketY { get; }
        public int MaximumCount { get; }
        public Vector2Int RepresentativeOrigin => new Vector2Int(
            BucketX * NearestBucketWidth + NearestBucketWidth / 2,
            BucketY * NearestBucketHeight + NearestBucketHeight / 2);

        public bool Equals(NearestCandidateKey other)
        {
            return Role == other.Role
                && BucketX == other.BucketX
                && BucketY == other.BucketY
                && MaximumCount == other.MaximumCount;
        }

        public override bool Equals(object obj)
        {
            return obj is NearestCandidateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                (int)Role,
                BucketX,
                BucketY,
                MaximumCount);
        }
    }

    private sealed class NearestCandidateScan
    {
        public IReadOnlyList<BuildableObject> Source;
        public int CandidateIndexVersion;
        public int SourceIndex;
        public bool Complete;
        public IReadOnlyList<BuildableObject> Result =
            Array.Empty<BuildableObject>();
        public readonly List<BuildableObject> Selected =
            new List<BuildableObject>();
        public readonly List<int> Costs = new List<int>();
    }

    private readonly Dictionary<Grid, GridFacilityCache> cacheByGrid =
        new Dictionary<Grid, GridFacilityCache>();
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private int facilityStateVersion;
    private int indexedBuildingVersion = -1;
    private int candidateIndexVersion;
    private int buildingScanIndex;
    private bool indexBuildComplete;
    private bool deterministicQueriesForDiagnostics;

    private const double SynchronousQueryBudgetMilliseconds = 0.15;
    private const int MinimumIndexBatchSize = 16;
    private const int NearestCandidateMinimumBatchSize = 1;
    private const int NearestBucketWidth = 16;
    private const int NearestBucketHeight = 4;

    public FacilityCandidateCacheStore(
        ICharacterAiWorldRegistry worldRegistry,
        IDynamicFrameWorkBudget frameWorkBudget)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.frameWorkBudget = frameWorkBudget;
    }

    public int DynamicStateVersion => facilityStateVersion;
    public int CandidateIndexVersion => candidateIndexVersion;
    public bool HasPendingIndexBuild
    {
        get
        {
            EnsureIndexVersion();
            return !indexBuildComplete;
        }
    }

    public void ConfigureDeterministicQueriesForDiagnostics(bool enabled)
    {
        deterministicQueriesForDiagnostics = enabled;
    }

    public void ResetDeterministicCheckpointForDiagnostics()
    {
        Clear();
        while (HasPendingIndexBuild && AdvanceIndex(double.MaxValue) > 0)
        {
        }
    }

    public IReadOnlyList<BuildableObject> GetCandidates(Grid grid, FacilityRole role)
    {
        if (grid == null || role == FacilityRole.None)
        {
            return Array.Empty<BuildableObject>();
        }

        PrepareIndexForQuery(grid);
        GridFacilityCache cache = GetCache(grid);
        if (cache.CandidatesByRole.TryGetValue(
                role,
                out IReadOnlyList<BuildableObject> cachedCandidates))
        {
            return cachedCandidates;
        }

        if (IsSingleRole(role))
        {
            return GetSingleRoleCandidates(grid, cache, role);
        }

        List<BuildableObject> merged = new List<BuildableObject>();
        HashSet<BuildableObject> seen = new HashSet<BuildableObject>();
        foreach (FacilityRole singleRole in GetSingleRoles(role))
        {
            foreach (BuildableObject building in GetSingleRoleCandidates(grid, cache, singleRole))
            {
                if (building != null && seen.Add(building))
                {
                    merged.Add(building);
                }
            }
        }

        IReadOnlyList<BuildableObject> result = ReadOnlyView.List(merged);
        if (indexBuildComplete)
        {
            cache.CandidatesByRole[role] = result;
        }

        return result;
    }

    public FacilityRole GetAvailableRoles(Grid grid)
    {
        if (grid == null)
        {
            return FacilityRole.None;
        }

        PrepareIndexForQuery(grid);
        return GetCache(grid).AvailableRoles;
    }

    public bool TryGetNearestCandidates(
        Grid grid,
        FacilityRole role,
        Vector2Int origin,
        int maximumCount,
        double budgetMilliseconds,
        out IReadOnlyList<BuildableObject> candidates)
    {
        candidates = Array.Empty<BuildableObject>();
        if (grid == null
            || role == FacilityRole.None
            || maximumCount <= 0)
        {
            return true;
        }

        PrepareIndexForQuery(grid);
        IReadOnlyList<BuildableObject> source = GetCandidates(grid, role);
        if (indexBuildComplete
            && source.Count <= maximumCount)
        {
            candidates = source;
            return true;
        }

        GridFacilityCache cache = GetCache(grid);
        NearestCandidateKey key =
            new NearestCandidateKey(role, origin, maximumCount);
        if (!cache.NearestCandidates.TryGetValue(
                key,
                out NearestCandidateScan scan)
            || scan.CandidateIndexVersion != candidateIndexVersion)
        {
            scan = new NearestCandidateScan
            {
                Source = source,
                CandidateIndexVersion = candidateIndexVersion
            };
            cache.NearestCandidates[key] = scan;
        }
        else
        {
            scan.Source = source;
        }

        if (scan.Complete)
        {
            candidates = scan.Result;
            return true;
        }

        long started = Stopwatch.GetTimestamp();
        int processed = 0;
        int sourceCount = scan.Source?.Count ?? 0;
        Vector2Int representativeOrigin = key.RepresentativeOrigin;
        double budget = Math.Max(0.02, budgetMilliseconds);
        while (scan.SourceIndex < sourceCount)
        {
            BuildableObject building = scan.Source[scan.SourceIndex++];
            processed++;
            if (building != null
                && !building.isDestroy
                && building.Grid == grid
                && building.SupportsFacilityRole(role))
            {
                AddNearestCandidate(
                    scan,
                    building,
                    representativeOrigin,
                    maximumCount);
            }

            if (!deterministicQueriesForDiagnostics
                && processed >= NearestCandidateMinimumBatchSize
                && GetElapsedMilliseconds(started) >= budget)
            {
                break;
            }
        }

        sourceCount = scan.Source?.Count ?? 0;
        scan.Complete = scan.SourceIndex >= sourceCount
            && indexBuildComplete;
        if (!scan.Complete)
        {
            frameWorkBudget?.SetBacklog(
                DynamicFrameWorkDomain.WorldIndex,
                cache.NearestCandidates.Count);
            return false;
        }

        scan.Result = ReadOnlyView.List(
            new List<BuildableObject>(scan.Selected));
        candidates = scan.Result;
        return true;
    }

    public IReadOnlyList<BuildableObject> GetWorkCandidates(
        Grid grid,
        FacilityWorkType workType)
    {
        if (grid == null)
        {
            return Array.Empty<BuildableObject>();
        }

        PrepareIndexForQuery(grid);
        GridFacilityCache cache = GetCache(grid);
        if (cache.CandidatesByWorkType.TryGetValue(
                workType,
                out IReadOnlyList<BuildableObject> cachedCandidates))
        {
            return cachedCandidates;
        }

        List<BuildableObject> discovered = new List<BuildableObject>();
        cache.CandidatesByWorkType[workType] = discovered;
        return discovered;
    }

    public int AdvanceIndex(double budgetMilliseconds)
    {
        EnsureIndexVersion();
        if (indexBuildComplete || budgetMilliseconds <= 0.0)
        {
            return 0;
        }

        IReadOnlyList<BuildableObject> buildings = worldRegistry.Buildings;
        if (buildings.Count == 0)
        {
            indexBuildComplete = true;
            return 0;
        }

        long started = Stopwatch.GetTimestamp();
        int processed = 0;
        while (buildingScanIndex < buildings.Count)
        {
            IndexBuilding(buildings[buildingScanIndex]);
            buildingScanIndex++;
            processed++;
            if (!deterministicQueriesForDiagnostics
                && processed >= MinimumIndexBatchSize
                && GetElapsedMilliseconds(started) >= budgetMilliseconds)
            {
                break;
            }
        }

        indexBuildComplete = buildingScanIndex >= buildings.Count;

        return processed;
    }

    public void MarkDynamicStateDirty()
    {
        unchecked
        {
            facilityStateVersion++;
        }
    }

    public void Clear()
    {
        cacheByGrid.Clear();
        indexedBuildingVersion = -1;
        buildingScanIndex = 0;
        indexBuildComplete = false;
        unchecked
        {
            candidateIndexVersion++;
        }
        MarkDynamicStateDirty();
    }

    private GridFacilityCache GetCache(Grid grid)
    {
        if (!cacheByGrid.TryGetValue(grid, out GridFacilityCache cache))
        {
            cache = new GridFacilityCache();
            cacheByGrid[grid] = cache;
        }

        return cache;
    }

    private IReadOnlyList<BuildableObject> GetSingleRoleCandidates(
        Grid grid,
        GridFacilityCache cache,
        FacilityRole role)
    {
        if (!cache.CandidatesByRole.TryGetValue(
                role,
                out IReadOnlyList<BuildableObject> candidates))
        {
            List<BuildableObject> discovered = new List<BuildableObject>();
            candidates = discovered;
            cache.CandidatesByRole[role] = candidates;
        }

        return candidates;
    }

    private void PrepareIndexForQuery(Grid grid)
    {
        EnsureIndexVersion();
        GridFacilityCache cache = GetCache(grid);
        if (worldRegistry.Buildings.Count > 0)
        {
            // Runtime indexing is advanced once by the frame scheduler. Advancing
            // here would multiply index work by every actor and facility query.
            if (frameWorkBudget == null)
            {
                while (!indexBuildComplete
                    && AdvanceIndex(SynchronousQueryBudgetMilliseconds) > 0)
                {
                }
            }
            else
            {
                return;
            }

            if (cache.IndexedBuildings.Count > 0)
            {
                return;
            }
        }

        if (cache.FallbackBuilt && cache.FallbackGridVersion == grid.version)
        {
            return;
        }

        if (cache.FallbackBuilt)
        {
            cache = new GridFacilityCache();
            cacheByGrid[grid] = cache;
            unchecked
            {
                candidateIndexVersion++;
            }
        }

        foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
        {
            if (occupant is BuildableObject building)
            {
                IndexBuilding(building);
            }
        }

        cache.FallbackBuilt = true;
        cache.FallbackGridVersion = grid.version;
    }

    private void EnsureIndexVersion()
    {
        int buildingVersion = worldRegistry.BuildingVersion;
        if (indexedBuildingVersion == buildingVersion)
        {
            return;
        }

        cacheByGrid.Clear();
        indexedBuildingVersion = buildingVersion;
        buildingScanIndex = 0;
        indexBuildComplete = worldRegistry.Buildings.Count == 0;
        unchecked
        {
            candidateIndexVersion++;
        }
    }

    private void IndexBuilding(BuildableObject building)
    {
        if (building == null
            || building.Grid == null
            || building.isDestroy)
        {
            return;
        }

        GridFacilityCache cache = GetCache(building.Grid);
        if (!cache.IndexedBuildings.Add(building))
        {
            return;
        }

        FacilityRole roles = building.Facility?.roles ?? FacilityRole.None;
        cache.AvailableRoles |= roles;
        foreach (FacilityRole role in GetSingleRoles(roles))
        {
            GetOrCreateRoleList(cache, role).Add(building);
        }

        if (building is not IWorkableFacility)
        {
            return;
        }

        FacilityWorkType supportedTypes = GetSupportedWorkTypes(building);
        if (supportedTypes == FacilityWorkType.None)
        {
            return;
        }

        GetOrCreateWorkList(cache, FacilityWorkType.None).Add(building);
        foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(
                     supportedTypes))
        {
            GetOrCreateWorkList(
                cache,
                FacilityWorkTypeMap.GetRequired(definition)).Add(building);
        }
    }

    private static FacilityWorkType GetSupportedWorkTypes(
        BuildableObject building)
    {
        FacilityWorkType supportedTypes = building is ConstructionSite
            ? FacilityWorkType.Construct
            : building.Facility != null
                ? WildlifeButcherFacilityUtility.AddFallbackWorkTypes(
                    building,
                    building.Facility.supportedWorkTypes)
                : FacilityWorkType.None;
        supportedTypes = SurvivalFacilityUtility.AddFallbackWorkTypes(
            building,
            supportedTypes);
        supportedTypes =
            CombatEquipmentMaintenanceFacilityUtility.AddFallbackWorkTypes(
                building,
                supportedTypes);
        supportedTypes = FacilityEvolutionWorkUtility.AddFallbackWorkTypes(
            building,
            supportedTypes);
        supportedTypes = RuntimeWorkCapabilityUtility.AddFallbackWorkTypes(
            building,
            supportedTypes);
        return supportedTypes;
    }

    private static List<BuildableObject> GetOrCreateRoleList(
        GridFacilityCache cache,
        FacilityRole role)
    {
        if (cache.CandidatesByRole.TryGetValue(
                role,
                out IReadOnlyList<BuildableObject> candidates))
        {
            return (List<BuildableObject>)candidates;
        }

        List<BuildableObject> list = new List<BuildableObject>();
        cache.CandidatesByRole[role] = list;
        return list;
    }

    private static List<BuildableObject> GetOrCreateWorkList(
        GridFacilityCache cache,
        FacilityWorkType workType)
    {
        if (cache.CandidatesByWorkType.TryGetValue(
                workType,
                out IReadOnlyList<BuildableObject> candidates))
        {
            return (List<BuildableObject>)candidates;
        }

        List<BuildableObject> list = new List<BuildableObject>();
        cache.CandidatesByWorkType[workType] = list;
        return list;
    }

    private static double GetElapsedMilliseconds(long started)
    {
        return (Stopwatch.GetTimestamp() - started)
            * 1000.0
            / Stopwatch.Frequency;
    }

    private static void AddNearestCandidate(
        NearestCandidateScan scan,
        BuildableObject building,
        Vector2Int origin,
        int maximumCount)
    {
        int cost = EstimateCandidateDistance(origin, building);
        if (scan.Selected.Count < maximumCount)
        {
            scan.Selected.Add(building);
            scan.Costs.Add(cost);
            return;
        }

        int worstIndex = 0;
        int worstCost = scan.Costs[0];
        for (int index = 1; index < scan.Costs.Count; index++)
        {
            if (scan.Costs[index] > worstCost)
            {
                worstCost = scan.Costs[index];
                worstIndex = index;
            }
        }

        if (cost >= worstCost)
        {
            return;
        }

        scan.Selected[worstIndex] = building;
        scan.Costs[worstIndex] = cost;
    }

    private static int EstimateCandidateDistance(
        Vector2Int origin,
        BuildableObject building)
    {
        IReadOnlyList<Vector2Int> positions = building.buildPoses;
        int best = int.MaxValue;
        if (positions != null)
        {
            for (int index = 0; index < positions.Count; index++)
            {
                Vector2Int candidate = positions[index];
                int distance = Mathf.Abs(origin.x - candidate.x)
                    + Mathf.Abs(origin.y - candidate.y) * 8;
                if (distance < best)
                {
                    best = distance;
                }
            }
        }

        return best != int.MaxValue
            ? best
            : Mathf.Abs(origin.x - building.centerPos.x)
                + Mathf.Abs(origin.y - building.centerPos.y) * 8;
    }

    private static bool IsSingleRole(FacilityRole role)
    {
        int value = (int)role;
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static IEnumerable<FacilityRole> GetSingleRoles(FacilityRole roles)
    {
        foreach (FacilityRoleDefinition definition in FacilityRoleCatalog.Enumerate(roles))
        {
            yield return definition.Role;
        }
    }
}
