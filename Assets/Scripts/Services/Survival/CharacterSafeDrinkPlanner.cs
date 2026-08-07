using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

internal enum CharacterSafeDrinkTargetKind
{
    None = 0,
    ItemStack = 1,
    Facility = 2,
    WorldSource = 3
}

internal enum CharacterSafeReliefApproachSearchStatus
{
    Invalid = 0,
    Pending = 1,
    Reachable = 2
}

internal readonly struct CharacterSafeDrinkPlan
{
    public CharacterSafeDrinkPlan(
        CharacterSafeDrinkTargetKind kind,
        Vector2Int targetPosition,
        Vector2Int approachPosition,
        Queue<GridMoveStep> path,
        string targetId = "",
        BuildableObject facility = null)
    {
        Kind = kind;
        TargetPosition = targetPosition;
        ApproachPosition = approachPosition;
        Path = path;
        TargetId = targetId ?? string.Empty;
        Facility = facility;
    }

    public CharacterSafeDrinkTargetKind Kind { get; }
    public Vector2Int TargetPosition { get; }
    public Vector2Int ApproachPosition { get; }
    public Queue<GridMoveStep> Path { get; }
    public string TargetId { get; }
    public BuildableObject Facility { get; }
    public bool IsValid => Kind != CharacterSafeDrinkTargetKind.None;
}

/// <summary>
/// Finds and reserves safe drinking targets without owning character need state.
/// </summary>
internal sealed class CharacterSafeDrinkPlanner
{
    private const float SafeReliefRetrySeconds = 1.25f;
    private static readonly ProfilerMarker SafeReliefPlanProfilerMarker =
        new ProfilerMarker("SafeRelief.Plan");
    private static readonly ProfilerMarker SafeReliefStackProfilerMarker =
        new ProfilerMarker("SafeRelief.StackCandidates");
    private static readonly ProfilerMarker SafeReliefFacilityProfilerMarker =
        new ProfilerMarker("SafeRelief.FacilityCandidates");
    private static readonly ProfilerMarker SafeReliefApproachProfilerMarker =
        new ProfilerMarker("SafeRelief.Approach");
    private static readonly ProfilerMarker SafeReliefDirectPathProfilerMarker =
        new ProfilerMarker("SafeRelief.DirectPath");
    private static readonly ProfilerMarker SafeReliefExactPathProfilerMarker =
        new ProfilerMarker("SafeRelief.ExactPath");

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly ISurvivalFoodQuery survivalFoodRuntime;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly CharacterDeprivationDiagnostics diagnostics;
    private readonly Dictionary<Vector2Int, string> approachOwners = new(128);
    private readonly Dictionary<string, Vector2Int> approachByActor =
        new(128, StringComparer.Ordinal);
    private readonly List<WorldItemStockCandidate> stockCandidates = new(64);

    public CharacterSafeDrinkPlanner(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldWaterQuery waterQuery,
        IFacilityCandidateCache facilityCandidateCache,
        ISurvivalFoodQuery survivalFoodRuntime,
        IDoorAccessQuery doorAccessQuery,
        CharacterDeprivationDiagnostics diagnostics)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.survivalFoodRuntime = survivalFoodRuntime
            ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
        this.doorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool HasReservation(string actorId) =>
        approachByActor.ContainsKey(actorId?.Trim() ?? string.Empty);

    public void ReleaseForActor(string actorId)
    {
        if (approachByActor.TryGetValue(
                actorId?.Trim() ?? string.Empty,
                out Vector2Int approach))
        {
            Release(actorId, approach);
        }
    }

    public void Reset()
    {
        approachOwners.Clear();
        approachByActor.Clear();
        stockCandidates.Clear();
    }
    public bool TryCreatePlan(
        CharacterActor actor,
        string actorId,
        out CharacterSafeDrinkPlan plan)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefPlanProfilerMarker.Auto();
        plan = default;
        if (actor == null
            || string.IsNullOrWhiteSpace(actorId)
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        if (TryFindReservableWaterFacility(
                grid,
                actor,
                actorId,
                out BuildableObject facility,
                out Vector2Int facilityApproach,
                out Queue<GridMoveStep> facilityPath,
                out bool facilitySearchPending))
        {
            plan = new CharacterSafeDrinkPlan(
                CharacterSafeDrinkTargetKind.Facility,
                facility.centerPos,
                facilityApproach,
                facilityPath,
                facility: facility);
            return true;
        }
        if (facilitySearchPending)
        {
            return false;
        }

        if (TryFindReservableWaterStack(
                grid,
                actor,
                actorId,
                out WorldItemStockCandidate stack,
                out Vector2Int stackApproach,
                out Queue<GridMoveStep> stackPath,
                out bool stackSearchPending))
        {
            plan = new CharacterSafeDrinkPlan(
                CharacterSafeDrinkTargetKind.ItemStack,
                stack.Position,
                stackApproach,
                stackPath,
                stack.StackId);
            return true;
        }
        if (stackSearchPending)
        {
            return false;
        }

        if (waterQuery.TryFindDrinkSource(
                actor.GetNowXY(),
                allowFoul: false,
                out WorldWaterSourceSnapshot source)
            && source.Quality == WorldWaterQuality.Clean
            && TryFindReachableSafeReliefApproach(
                grid,
                actor,
                actorId,
                source.Position,
                includeTarget:
                    source.TerrainType != GridCellTerrainType.DeepWater,
                allowPathSearch: true,
                out Vector2Int sourceApproach,
                out Queue<GridMoveStep> sourcePath)
                == CharacterSafeReliefApproachSearchStatus.Reachable)
        {
            ReserveSafeReliefApproach(actorId, sourceApproach);
            plan = new CharacterSafeDrinkPlan(
                CharacterSafeDrinkTargetKind.WorldSource,
                source.Position,
                sourceApproach,
                sourcePath,
                source.SourceId);
            return true;
        }

        return false;
    }

    public bool TryFindReservableWaterStack(
        Grid grid,
        CharacterActor actor,
        string actorId,
        out WorldItemStockCandidate selected,
        out Vector2Int selectedApproach,
        out Queue<GridMoveStep> selectedPath,
        out bool searchPending,
        bool countSafeReliefPlan = true)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefStackProfilerMarker.Auto();
        selected = default;
        selectedApproach = default;
        selectedPath = null;
        searchPending = false;
        itemStackRuntime.CopyAvailableStockCandidates(
            StockCategory.Water,
            stockCandidates);

        Vector2Int origin = actor.GetNowXY();
        bool hasSameFloorCandidate = false;
        for (int index = 0; index < stockCandidates.Count; index++)
        {
            WorldItemStockCandidate candidate =
                stockCandidates[index];
            if (candidate.IsValid && candidate.Position.y == origin.y)
            {
                hasSameFloorCandidate = true;
                break;
            }
        }

        for (int searchPass = 0;
             searchPass < 2 && !selected.IsValid;
             searchPass++)
        {
            bool allowPathSearch = searchPass > 0;
            bool sameFloorHasAvailableApproach = false;
            int previousDistance = -1;
            int previousIndex = -1;
            for (int rank = 0; rank < stockCandidates.Count; rank++)
            {
                int nextIndex = -1;
                int nextDistance = int.MaxValue;
                for (int index = 0;
                     index < stockCandidates.Count;
                     index++)
                {
                    WorldItemStockCandidate candidateEntry =
                        stockCandidates[index];
                    if (!candidateEntry.IsValid)
                    {
                        continue;
                    }

                    int distance = GetSafeReliefDistance(
                        origin,
                        candidateEntry.Position,
                        grid.width);
                    if (distance < previousDistance
                        || distance == previousDistance
                            && index <= previousIndex
                        || distance > nextDistance
                        || distance == nextDistance
                            && nextIndex >= 0
                            && index >= nextIndex)
                    {
                        continue;
                    }

                    nextIndex = index;
                    nextDistance = distance;
                }

                if (nextIndex < 0)
                {
                    break;
                }

                previousDistance = nextDistance;
                previousIndex = nextIndex;
                WorldItemStockCandidate selectedCandidate =
                    stockCandidates[nextIndex];
                bool isSameFloor =
                    selectedCandidate.Position.y == origin.y;
                if (!isSameFloor
                    && hasSameFloorCandidate
                    && !sameFloorHasAvailableApproach)
                {
                    break;
                }

                if (isSameFloor
                    && HasAvailableSafeReliefApproach(
                        grid,
                        actorId,
                        origin,
                        selectedCandidate.Position,
                        includeTarget:
                            selectedCandidate.State !=
                                WorldItemStackState.Stored))
                {
                    sameFloorHasAvailableApproach = true;
                }

                CharacterSafeReliefApproachSearchStatus approachStatus =
                    TryFindReachableSafeReliefApproach(
                        grid,
                        actor,
                        actorId,
                        selectedCandidate.Position,
                        includeTarget:
                            selectedCandidate.State !=
                                WorldItemStackState.Stored,
                        allowPathSearch: allowPathSearch,
                        out Vector2Int approach,
                        out Queue<GridMoveStep> path);
                if (approachStatus ==
                    CharacterSafeReliefApproachSearchStatus.Pending)
                {
                    searchPending = true;
                    return false;
                }

                if (approachStatus !=
                    CharacterSafeReliefApproachSearchStatus.Reachable)
                {
                    continue;
                }

                selected = selectedCandidate;
                selectedApproach = approach;
                selectedPath = path;
                break;
            }
        }

        if (!selected.IsValid)
        {
            return false;
        }

        ReserveSafeReliefApproach(actorId, selectedApproach);
        if (countSafeReliefPlan
            && selected.State == WorldItemStackState.Stored)
        {
            diagnostics.SafeReliefStoredStackPlans++;
        }
        return true;
    }

    private bool HasAvailableSafeReliefApproach(
        Grid grid,
        string actorId,
        Vector2Int origin,
        Vector2Int target,
        bool includeTarget)
    {
        for (int candidateIndex = 0; candidateIndex < 3; candidateIndex++)
        {
            if (candidateIndex == 0 && !includeTarget)
            {
                continue;
            }

            Vector2Int candidatePosition = candidateIndex switch
            {
                0 => target,
                1 => target + Vector2Int.left,
                _ => target + Vector2Int.right
            };
            if (!grid.IsValidGridPos(candidatePosition)
                || (candidatePosition != origin
                    && !grid.IsWalkable(candidatePosition))
                || (approachOwners.TryGetValue(
                        candidatePosition,
                        out string owner)
                    && !string.Equals(
                        owner,
                        actorId,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal bool TryFindReservableWaterFacility(
        Grid grid,
        CharacterActor actor,
        string actorId,
        out BuildableObject selected,
        out Vector2Int selectedApproach,
        out Queue<GridMoveStep> selectedPath,
        out bool searchPending)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefFacilityProfilerMarker.Auto();
        selected = null;
        selectedApproach = default;
        selectedPath = null;
        searchPending = false;
        Vector2Int origin = actor.GetNowXY();
        IReadOnlyList<BuildableObject> buildings =
            facilityCandidateCache.GetWorkCandidates(
                grid,
                FacilityWorkType.DrawWater);
        if (buildings.Count == 0
            && facilityCandidateCache.HasPendingIndexBuild)
        {
            searchPending = true;
            return false;
        }

        bool hasSameFloorCandidate = false;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject candidate = buildings[index];
            if (IsUsableWaterFacility(candidate)
                && candidate.centerPos.y == origin.y)
            {
                hasSameFloorCandidate = true;
                break;
            }
        }

        for (int searchPass = 0;
             searchPass < 2 && selected == null;
             searchPass++)
        {
            bool allowPathSearch = searchPass > 0;
            bool sameFloorHasAvailableApproach = false;
            int previousDistance = -1;
            int previousIndex = -1;
            for (int rank = 0; rank < buildings.Count; rank++)
            {
                int nextIndex = -1;
                int nextDistance = int.MaxValue;
                for (int index = 0; index < buildings.Count; index++)
                {
                    BuildableObject candidateEntry = buildings[index];
                    if (!IsUsableWaterFacility(candidateEntry))
                    {
                        continue;
                    }

                    int distance = GetSafeReliefDistance(
                        origin,
                        candidateEntry.centerPos,
                        grid.width);
                    if (distance < previousDistance
                        || distance == previousDistance
                            && index <= previousIndex
                        || distance > nextDistance
                        || distance == nextDistance
                            && nextIndex >= 0
                            && index >= nextIndex)
                    {
                        continue;
                    }

                    nextIndex = index;
                    nextDistance = distance;
                }

                if (nextIndex < 0)
                {
                    break;
                }

                previousDistance = nextDistance;
                previousIndex = nextIndex;
                BuildableObject selectedCandidate = buildings[nextIndex];
                bool isSameFloor =
                    selectedCandidate.centerPos.y == origin.y;
                if (!isSameFloor
                    && hasSameFloorCandidate
                    && !sameFloorHasAvailableApproach)
                {
                    break;
                }

                if (isSameFloor
                    && HasAvailableSafeReliefApproach(
                        grid,
                        actorId,
                        origin,
                        selectedCandidate.centerPos,
                        includeTarget: false))
                {
                    sameFloorHasAvailableApproach = true;
                }

                CharacterSafeReliefApproachSearchStatus approachStatus =
                    TryFindReachableSafeReliefApproach(
                        grid,
                        actor,
                        actorId,
                        selectedCandidate.centerPos,
                        includeTarget: false,
                        allowPathSearch: allowPathSearch,
                        out Vector2Int approach,
                        out Queue<GridMoveStep> path);
                if (approachStatus ==
                    CharacterSafeReliefApproachSearchStatus.Pending)
                {
                    searchPending = true;
                    return false;
                }

                if (approachStatus !=
                    CharacterSafeReliefApproachSearchStatus.Reachable)
                {
                    continue;
                }

                selected = selectedCandidate;
                selectedApproach = approach;
                selectedPath = path;
                break;
            }
        }

        if (selected == null)
        {
            return false;
        }

        ReserveSafeReliefApproach(actorId, selectedApproach);
        return true;
    }

    private bool IsUsableWaterFacility(BuildableObject candidate)
    {
        return candidate != null
            && candidate.gameObject.activeInHierarchy
            && !candidate.IsGridDestroyed
            && candidate.BuildingData?.GetAbility<BuildingWaterSourceAbility>() != null
            && survivalFoodRuntime.HasSurvivalWorkAvailable(
                candidate,
                BuiltInWorkTypeIds.DrawWater);
    }

    private CharacterSafeReliefApproachSearchStatus TryFindReachableSafeReliefApproach(
        Grid grid,
        CharacterActor actor,
        string actorId,
        Vector2Int target,
        bool includeTarget,
        bool allowPathSearch,
        out Vector2Int approach,
        out Queue<GridMoveStep> path)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefApproachProfilerMarker.Auto();
        approach = default;
        path = null;
        if (grid == null
            || actor == null
            || string.IsNullOrWhiteSpace(actorId)
            || actor.PathSearchBroker == null)
        {
            return CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        Vector2Int origin = actor.GetNowXY();
        if (approachByActor.TryGetValue(
                actorId,
                out Vector2Int existing))
        {
            return TryPrepareSafeReliefApproach(
                grid,
                actor,
                existing,
                allowPathSearch,
                out approach,
                out path);
        }

        int previousDistance = -1;
        int previousIndex = -1;
        for (int rank = 0; rank < 3; rank++)
        {
            int nextIndex = -1;
            int nextDistance = int.MaxValue;
            for (int candidateIndex = 0; candidateIndex < 3; candidateIndex++)
            {
                if (candidateIndex == 0 && !includeTarget)
                {
                    continue;
                }

                Vector2Int candidatePosition = candidateIndex switch
                {
                    0 => target,
                    1 => target + Vector2Int.left,
                    _ => target + Vector2Int.right
                };
                if (!grid.IsValidGridPos(candidatePosition)
                    || (candidatePosition != origin && !grid.IsWalkable(candidatePosition))
                    || (approachOwners.TryGetValue(
                            candidatePosition,
                            out string owner)
                        && !string.Equals(
                            owner,
                            actorId,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                int distance = Manhattan(origin, candidatePosition);
                if (distance < previousDistance
                    || distance == previousDistance && candidateIndex <= previousIndex
                    || distance > nextDistance
                    || distance == nextDistance
                        && nextIndex >= 0
                        && candidateIndex >= nextIndex)
                {
                    continue;
                }

                nextIndex = candidateIndex;
                nextDistance = distance;
            }

            if (nextIndex < 0)
            {
                break;
            }

            previousDistance = nextDistance;
            previousIndex = nextIndex;
            Vector2Int selectedPosition = nextIndex switch
            {
                0 => target,
                1 => target + Vector2Int.left,
                _ => target + Vector2Int.right
            };
            CharacterSafeReliefApproachSearchStatus searchStatus =
                TryPrepareSafeReliefApproach(
                    grid,
                    actor,
                    selectedPosition,
                    allowPathSearch,
                    out Vector2Int preparedApproach,
                    out Queue<GridMoveStep> preparedPath);
            if (searchStatus == CharacterSafeReliefApproachSearchStatus.Reachable)
            {
                approach = preparedApproach;
                path = preparedPath;
                return CharacterSafeReliefApproachSearchStatus.Reachable;
            }

            if (searchStatus == CharacterSafeReliefApproachSearchStatus.Pending)
            {
                approach = preparedApproach;
                path = preparedPath;
                return CharacterSafeReliefApproachSearchStatus.Pending;
            }

        }

        return CharacterSafeReliefApproachSearchStatus.Invalid;
    }

    private CharacterSafeReliefApproachSearchStatus TryPrepareSafeReliefApproach(
        Grid grid,
        CharacterActor actor,
        Vector2Int destination,
        bool allowPathSearch,
        out Vector2Int approach,
        out Queue<GridMoveStep> path)
    {
        approach = destination;
        path = null;
        Vector2Int origin = actor.GetNowXY();
        if (origin == destination)
        {
            return CharacterSafeReliefApproachSearchStatus.Reachable;
        }

        if (!grid.IsValidGridPos(destination)
            || !grid.IsWalkable(destination)
            || actor.PathSearchBroker == null)
        {
            return CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        if (TryBuildDirectHorizontalSafeReliefPath(
                grid,
                actor,
                origin,
                destination,
                out path))
        {
            return CharacterSafeReliefApproachSearchStatus.Reachable;
        }

        if (!allowPathSearch)
        {
            return CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        GridPathRequestStatus requestStatus;
        using (SafeReliefExactPathProfilerMarker.Auto())
        {
            requestStatus = actor.PathSearchBroker.RequestMovePathTo(
                grid,
                origin,
                destination,
                out path,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor),
                    movementIntent: GridMovementIntent.SafeChore));
        }
        if (requestStatus == GridPathRequestStatus.Pending)
        {
            return CharacterSafeReliefApproachSearchStatus.Pending;
        }

        return requestStatus == GridPathRequestStatus.Reachable
            && path != null
            && path.Count > 0
                ? CharacterSafeReliefApproachSearchStatus.Reachable
                : CharacterSafeReliefApproachSearchStatus.Invalid;
    }

    private bool TryBuildDirectHorizontalSafeReliefPath(
        Grid grid,
        CharacterActor actor,
        Vector2Int origin,
        Vector2Int destination,
        out Queue<GridMoveStep> path)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefDirectPathProfilerMarker.Auto();
        path = null;
        if (grid == null
            || actor == null
            || origin.y != destination.y
            || origin.x == destination.x)
        {
            return false;
        }

        int direction = destination.x > origin.x ? 1 : -1;
        int stepCount = Mathf.Abs(destination.x - origin.x);
        GridTraversalContext traversalContext =
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(actor),
                movementIntent: GridMovementIntent.SafeChore);
        Vector2Int current = origin;
        for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            Vector2Int next = new Vector2Int(
                current.x + direction,
                current.y);
            if (!grid.IsValidGridPos(next)
                || !grid.IsWalkable(next)
                || grid.IsMovementBlockedByWall(next)
                || (doorAccessQuery != null
                    && !doorAccessQuery.CanTraverse(
                        grid,
                        next,
                        traversalContext,
                        out _)))
            {
                return false;
            }

            current = next;
        }

        Queue<GridMoveStep> directPath = new Queue<GridMoveStep>(stepCount);
        current = origin;
        for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            Vector2Int next = new Vector2Int(
                current.x + direction,
                current.y);
            directPath.Enqueue(new GridMoveStep(
                current,
                next,
                grid.GetGridCell(next)?.GetTopOccupant(),
                null,
                GridMoveType.Walk));
            current = next;
        }

        path = directPath;
        return path.Count > 0;
    }

    private void ReserveSafeReliefApproach(
        string actorId,
        Vector2Int approach)
    {
        approachOwners[approach] = actorId;
        approachByActor[actorId] = approach;
    }

    public void Release(
        string actorId,
        Vector2Int expectedApproach)
    {
        if (string.IsNullOrWhiteSpace(actorId)
            || !approachByActor.TryGetValue(
                actorId,
                out Vector2Int approach))
        {
            return;
        }

        approachByActor.Remove(actorId);
        if (approach == expectedApproach
            && approachOwners.TryGetValue(
                approach,
                out string owner)
            && string.Equals(owner, actorId, StringComparison.Ordinal))
        {
            approachOwners.Remove(approach);
        }
    }

    public static float GetRetryDelay(string actorId)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < actorId.Length; index++)
            {
                hash = (hash * 31) + actorId[index];
            }

            int stagger = (hash & int.MaxValue) % 76;
            return SafeReliefRetrySeconds + stagger * 0.01f;
        }
    }

    private static int GetSafeReliefDistance(
        Vector2Int origin,
        Vector2Int target,
        int gridWidth)
    {
        int horizontal = Mathf.Abs(origin.x - target.x);
        int floorDistance = Mathf.Abs(origin.y - target.y);
        return horizontal + floorDistance * Mathf.Max(1, gridWidth);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
