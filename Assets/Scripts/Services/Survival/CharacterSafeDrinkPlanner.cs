using System;
using System.Collections.Generic;
using System.Linq;
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
        BuildableObject facility = null,
        WorldItemReservedStackQuantity itemReservation = default)
    {
        Kind = kind;
        TargetPosition = targetPosition;
        ApproachPosition = approachPosition;
        Path = path;
        TargetId = targetId ?? string.Empty;
        Facility = facility;
        ItemReservation = itemReservation;
    }

    public CharacterSafeDrinkTargetKind Kind { get; }
    public Vector2Int TargetPosition { get; }
    public Vector2Int ApproachPosition { get; }
    public Queue<GridMoveStep> Path { get; }
    public string TargetId { get; }
    public BuildableObject Facility { get; }
    public WorldItemReservedStackQuantity ItemReservation { get; }
    public bool IsValid => Kind != CharacterSafeDrinkTargetKind.None
        && (Kind != CharacterSafeDrinkTargetKind.ItemStack
            || ItemReservation.IsValid);
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
    private static readonly ProfilerMarker SafeReliefApproachProfilerMarker =
        new ProfilerMarker("SafeRelief.Approach");
    private static readonly ProfilerMarker SafeReliefDirectPathProfilerMarker =
        new ProfilerMarker("SafeRelief.DirectPath");
    private static readonly ProfilerMarker SafeReliefExactPathProfilerMarker =
        new ProfilerMarker("SafeRelief.ExactPath");

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldWaterQuery waterQuery;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IEnvironmentWorkPolicy environmentWorkPolicy;
    private readonly CharacterDeprivationDiagnostics diagnostics;
    private readonly Dictionary<Vector2Int, string> approachOwners = new(128);
    private readonly Dictionary<string, Vector2Int> approachByActor =
        new(128, StringComparer.Ordinal);
    private readonly Dictionary<string, WorldItemReservedStackQuantity>
        itemReservationByActor = new(128, StringComparer.Ordinal);
    private readonly List<WorldItemStockCandidate> stockCandidates = new(64);
    private string lastStackSearchFailureDetail = string.Empty;

    public CharacterSafeDrinkPlanner(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IWorldWaterQuery waterQuery,
        IItemQuantityReservationService quantityReservations,
        IDoorAccessQuery doorAccessQuery,
        IEnvironmentWorkPolicy environmentWorkPolicy,
        CharacterDeprivationDiagnostics diagnostics)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.waterQuery = waterQuery
            ?? throw new ArgumentNullException(nameof(waterQuery));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.doorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        this.environmentWorkPolicy = environmentWorkPolicy
            ?? throw new ArgumentNullException(nameof(environmentWorkPolicy));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool HasReservation(string actorId) =>
        approachByActor.ContainsKey(actorId?.Trim() ?? string.Empty)
        || itemReservationByActor.ContainsKey(actorId?.Trim() ?? string.Empty);

    public void ReleaseForActor(string actorId)
    {
        string normalizedActorId = actorId?.Trim() ?? string.Empty;
        if (approachByActor.TryGetValue(
                normalizedActorId,
                out Vector2Int approach))
        {
            Release(normalizedActorId, approach);
            return;
        }

        ReleaseItemReservation(normalizedActorId);
    }

    public void Reset()
    {
        approachOwners.Clear();
        approachByActor.Clear();
        itemReservationByActor.Clear();
        stockCandidates.Clear();
    }
    public bool TryCreatePlan(
        CharacterActor actor,
        string actorId,
        out CharacterSafeDrinkPlan plan,
        out bool searchPending)
    {
        using ProfilerMarker.AutoScope profile =
            SafeReliefPlanProfilerMarker.Auto();
        plan = default;
        searchPending = false;
        if (actor == null
            || string.IsNullOrWhiteSpace(actorId)
            || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            diagnostics.SafeReliefPlanNoSource++;
            diagnostics.LastSafeReliefPlanFailureDetail =
                $"invalid-request actorNull={actor == null}; actorId='{actorId}'; grid={gridSystemProvider.TryGetGrid(out _)}";
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
            WorldItemStackSnapshot stackSnapshot = null;
            IReadOnlyList<WorldItemStackSnapshot> stacks =
                itemStackRuntime.GetAllStacks();
            for (int index = 0; index < stacks.Count; index++)
            {
                WorldItemStackSnapshot candidate = stacks[index];
                if (candidate != null
                    && string.Equals(
                        candidate.StackId,
                        stack.StackId,
                        StringComparison.Ordinal))
                {
                    stackSnapshot = candidate;
                    break;
                }
            }

            string operationId =
                $"safe-drink:{actorId}:{(actor.Brain?.RuntimeActionEpoch ?? 0L):D16}";
            if (stackSnapshot == null
                || !quantityReservations.TryReserve(
                    operationId,
                    actorId,
                    ItemReservationPurpose.PersonalConsumption,
                    $"safe-drink:{actorId}",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stack.StackId),
                        1,
                        stackSnapshot.ReservationSignature),
                    out ItemQuantityLease lease,
                    out _))
            {
                diagnostics.SafeReliefPlanReservationRejected++;
                Release(actorId, stackApproach);
                return false;
            }

            WorldItemReservedStackQuantity reservation = new(
                stack.StackId,
                stackSnapshot.ItemId,
                1,
                stack.Position,
                WorldItemHaulDestinationKind.Warehouse,
                stackSnapshot.DestinationId,
                lease.leaseId,
                operationId);
            itemReservationByActor[actorId] = reservation;
            plan = new CharacterSafeDrinkPlan(
                CharacterSafeDrinkTargetKind.ItemStack,
                stack.Position,
                stackApproach,
                stackPath,
                stack.StackId,
                itemReservation: reservation);
            return true;
        }

        if (stackSearchPending)
        {
            diagnostics.SafeReliefPlanSearchPending++;
            searchPending = true;
            return false;
        }

        if (waterQuery.TryFindDrinkSource(
                actor.GetNowXY(),
                allowFoul: false,
                out WorldWaterSourceSnapshot source)
            && source.Quality == WorldWaterQuality.Clean)
        {
            CharacterSafeReliefApproachSearchStatus sourceStatus =
                TryFindReachableSafeReliefApproach(
                grid,
                actor,
                actorId,
                source.Position,
                includeTarget:
                    source.TerrainType != GridCellTerrainType.DeepWater,
                allowPathSearch: true,
                out Vector2Int sourceApproach,
                out Queue<GridMoveStep> sourcePath);
            if (sourceStatus == CharacterSafeReliefApproachSearchStatus.Pending)
            {
                diagnostics.SafeReliefPlanSearchPending++;
                searchPending = true;
                return false;
            }
            if (sourceStatus == CharacterSafeReliefApproachSearchStatus.Reachable)
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
        }

        diagnostics.SafeReliefPlanNoSource++;
        diagnostics.LastSafeReliefPlanFailureDetail =
            $"actor={actorId}; origin={actor.GetNowXY()}; stack={lastStackSearchFailureDetail}; worldSource=unavailable-or-unreachable";
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
        lastStackSearchFailureDetail = string.Empty;
        itemStackRuntime.CopyAvailableStockCandidates(
            StockCategory.Water,
            stockCandidates);

        int transitionalWaterStackCount = 0;
        int transitionalWaterQuantity = 0;
        WorldItemStackState lastTransitionalWaterState = default;
        IReadOnlyList<WorldItemStackSnapshot> allStacks =
            itemStackRuntime.GetAllStacks();
        for (int index = 0; index < allStacks.Count; index++)
        {
            WorldItemStackSnapshot snapshot = allStacks[index];
            if (snapshot == null
                || snapshot.Forbidden
                || snapshot.StockCategory != StockCategory.Water
                || snapshot.Quantity <= 0
                || snapshot.State is not (WorldItemStackState.Carried
                    or WorldItemStackState.InTransit))
            {
                continue;
            }

            transitionalWaterStackCount++;
            transitionalWaterQuantity += snapshot.Quantity;
            lastTransitionalWaterState = snapshot.State;
        }

        if (stockCandidates.Count == 0
            && transitionalWaterStackCount > 0)
        {
            lastStackSearchFailureDetail =
                $"transitional-water stacks={transitionalWaterStackCount}; quantity={transitionalWaterQuantity}; state={lastTransitionalWaterState}";
            searchPending = true;
            return false;
        }

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
            int validCandidateCount = 0;
            for (int index = 0; index < stockCandidates.Count; index++)
            {
                if (stockCandidates[index].IsValid)
                {
                    validCandidateCount++;
                }
            }

            lastStackSearchFailureDetail =
                $"no-selection candidates={stockCandidates.Count}; valid={validCandidateCount}; sameFloor={hasSameFloorCandidate}; origin={origin}; lastApproach={lastApproachFailureDetail}";
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

    // Compatibility endpoint for the breakdown runner. Production facilities
    // create inventory; they never grant hydration directly.
    internal bool TryFindReservableWaterFacility(
        Grid grid,
        CharacterActor actor,
        string actorId,
        out BuildableObject selected,
        out Vector2Int selectedApproach,
        out Queue<GridMoveStep> selectedPath,
        out bool searchPending)
    {
        selected = null;
        selectedApproach = default;
        selectedPath = null;
        searchPending = false;
        return false;
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
            lastApproachFailureDetail =
                $"invalid-context target={target}; grid={grid != null}; actor={actor != null}; actorId={actorId}; broker={actor?.PathSearchBroker != null}";
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
        bool temporarilyOwnedApproach = false;
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
                    || (candidatePosition != origin
                        && !grid.IsWalkable(candidatePosition)))
                {
                    continue;
                }

                if (approachOwners.TryGetValue(
                        candidatePosition,
                        out string owner)
                    && !string.Equals(
                        owner,
                        actorId,
                        StringComparison.Ordinal))
                {
                    temporarilyOwnedApproach = true;
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

        return temporarilyOwnedApproach
            ? CharacterSafeReliefApproachSearchStatus.Pending
            : CharacterSafeReliefApproachSearchStatus.Invalid;
    }

    private string lastApproachFailureDetail = string.Empty;

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
            lastApproachFailureDetail =
                $"invalid-destination origin={origin}; destination={destination}; valid={grid.IsValidGridPos(destination)}; walkable={grid.IsValidGridPos(destination) && grid.IsWalkable(destination)}; broker={actor.PathSearchBroker != null}";
            return CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        if (TryBuildDirectHorizontalSafeReliefPath(
                grid,
                actor,
                origin,
                destination,
                out path))
        {
            return IsEnvironmentallySafeApproach(
                    actor,
                    destination,
                    path)
                ? CharacterSafeReliefApproachSearchStatus.Reachable
                : CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        if (!allowPathSearch)
        {
            lastApproachFailureDetail =
                $"direct-path-unavailable origin={origin}; destination={destination}; exactSearch=not-requested";
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
            lastApproachFailureDetail =
                $"exact-path-pending origin={origin}; destination={destination}";
            return CharacterSafeReliefApproachSearchStatus.Pending;
        }

        if (requestStatus != GridPathRequestStatus.Reachable
            || path == null
            || path.Count == 0)
        {
            lastApproachFailureDetail =
                $"exact-path-{requestStatus} origin={origin}; destination={destination}; pathSteps={path?.Count ?? 0}";
        }

        if (requestStatus != GridPathRequestStatus.Reachable
            || path == null
            || path.Count == 0)
        {
            return CharacterSafeReliefApproachSearchStatus.Invalid;
        }

        return IsEnvironmentallySafeApproach(actor, destination, path)
            ? CharacterSafeReliefApproachSearchStatus.Reachable
            : CharacterSafeReliefApproachSearchStatus.Invalid;
    }

    private bool IsEnvironmentallySafeApproach(
        CharacterActor actor,
        Vector2Int destination,
        Queue<GridMoveStep> path)
    {
        GridMoveStep[] route = path?.ToArray()
            ?? Array.Empty<GridMoveStep>();
        float expectedSeconds = Mathf.Max(
            1f,
            route.Length / Mathf.Max(0.1f, actor.GetMoveSpeed()));
        WorkEnvironmentAssessment assessment =
            environmentWorkPolicy.AssessStart(
                actor,
                destination,
                route,
                expectedSeconds,
                EnvironmentalWorkKind.General,
                forced: false);
        if (assessment.CanStart)
        {
            return true;
        }

        lastApproachFailureDetail =
            $"environment-rejected destination={destination};"
            + $"routeSteps={route.Length};"
            + $"projected={assessment.ProjectedExposure:0.###};"
            + $"failure={assessment.Failure.Code}";
        return false;
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
            ReleaseItemReservation(actorId?.Trim() ?? string.Empty);
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

        ReleaseItemReservation(actorId);
    }

    public void CompleteItemReservation(
        string actorId,
        string expectedLeaseId)
    {
        string normalizedActorId = actorId?.Trim() ?? string.Empty;
        if (itemReservationByActor.TryGetValue(
                normalizedActorId,
                out WorldItemReservedStackQuantity reservation)
            && string.Equals(
                reservation.LeaseId,
                expectedLeaseId?.Trim(),
                StringComparison.Ordinal))
        {
            itemReservationByActor.Remove(normalizedActorId);
        }
    }

    private void ReleaseItemReservation(string actorId)
    {
        if (!itemReservationByActor.TryGetValue(
                actorId,
                out WorldItemReservedStackQuantity reservation))
        {
            return;
        }

        itemReservationByActor.Remove(actorId);
        if (!string.IsNullOrWhiteSpace(reservation.LeaseId))
        {
            quantityReservations.Release(
                reservation.LeaseId,
                ItemReservationReleaseReason.Cancelled);
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
