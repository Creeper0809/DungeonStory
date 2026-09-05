#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal enum NaturalFixtureNodeRole
{
    Facility = 0,
    Support = 1,
    UtilitySource = 2,
    Warehouse = 3
}

internal enum NaturalFixtureUtilityConnectionMode
{
    DirectAdjacent = 0,
    ConduitRoute = 1
}

internal enum NaturalFixturePlacementPolicy
{
    GameplayRules = 0,
    FixtureInfrastructureOccupancy = 1
}

internal enum NaturalFixturePlacementFailureCode
{
    None = 0,
    InvalidRequest = 1,
    Impossible = 2,
    BudgetExceeded = 3
}

/// <summary>
/// A typed building node in an Editor-only natural production fixture graph.
/// StableNodeId is scenario identity, not a content ID dispatch key.
/// </summary>
internal readonly struct NaturalFixtureBuildingRequirement
{
    internal NaturalFixtureBuildingRequirement(
        string stableNodeId,
        BuildingSO asset,
        NaturalFixtureNodeRole role,
        bool requireReachableWorkAccess,
        bool requireUsableRoom = false,
        string roomGroupId = "",
        NaturalFixturePlacementPolicy placementPolicy =
            NaturalFixturePlacementPolicy.GameplayRules)
    {
        StableNodeId = stableNodeId ?? string.Empty;
        Asset = asset;
        Role = role;
        RequireReachableWorkAccess = requireReachableWorkAccess;
        RequireUsableRoom = requireUsableRoom;
        RoomGroupId = roomGroupId ?? string.Empty;
        PlacementPolicy = placementPolicy;
    }

    internal string StableNodeId { get; }
    internal BuildingSO Asset { get; }
    internal NaturalFixtureNodeRole Role { get; }
    internal bool RequireReachableWorkAccess { get; }
    internal bool RequireUsableRoom { get; }
    internal string RoomGroupId { get; }
    internal NaturalFixturePlacementPolicy PlacementPolicy { get; }
}

/// <summary>
/// A typed utility edge. ConduitAsset is required only for ConduitRoute.
/// </summary>
internal readonly struct NaturalFixtureUtilityRequirement
{
    internal NaturalFixtureUtilityRequirement(
        string stableEdgeId,
        string sourceNodeId,
        string targetNodeId,
        UtilityChannel channel,
        NaturalFixtureUtilityConnectionMode connectionMode,
        BuildingSO conduitAsset = null)
    {
        StableEdgeId = stableEdgeId ?? string.Empty;
        SourceNodeId = sourceNodeId ?? string.Empty;
        TargetNodeId = targetNodeId ?? string.Empty;
        Channel = channel;
        ConnectionMode = connectionMode;
        ConduitAsset = conduitAsset;
    }

    internal string StableEdgeId { get; }
    internal string SourceNodeId { get; }
    internal string TargetNodeId { get; }
    internal UtilityChannel Channel { get; }
    internal NaturalFixtureUtilityConnectionMode ConnectionMode { get; }
    internal BuildingSO ConduitAsset { get; }
}

internal sealed class NaturalFixturePlacementRequest
{
    internal Grid Grid { get; set; }
    internal BuildingPlacementValidator PlacementValidator { get; set; }
    internal IRoomLayoutCache Rooms { get; set; }
    internal Vector2Int ActorOrigin { get; set; }
    internal IReadOnlyList<Vector2Int> CandidateAnchors { get; set; }
    internal IReadOnlyList<Vector2Int> ReachableCells { get; set; }
    internal IReadOnlyList<NaturalFixtureBuildingRequirement> Nodes { get; set; }
    internal IReadOnlyList<NaturalFixtureUtilityRequirement> UtilityEdges { get; set; }
    internal int MaximumVisitedNodes { get; set; } = 100000;
}

internal sealed class NaturalFixturePlacementChoice
{
    internal NaturalFixturePlacementChoice(
        NaturalFixtureBuildingRequirement requirement,
        Vector2Int anchor,
        IReadOnlyList<Vector2Int> footprint,
        IReadOnlyList<Vector2Int> workAccessCandidates)
    {
        Requirement = requirement;
        Anchor = anchor;
        Footprint = footprint;
        WorkAccessCandidates = workAccessCandidates;
    }

    internal NaturalFixtureBuildingRequirement Requirement { get; }
    internal Vector2Int Anchor { get; }
    internal IReadOnlyList<Vector2Int> Footprint { get; }
    internal IReadOnlyList<Vector2Int> WorkAccessCandidates { get; }
}

internal sealed class NaturalFixtureUtilityRoute
{
    internal NaturalFixtureUtilityRoute(
        NaturalFixtureUtilityRequirement requirement,
        IReadOnlyList<Vector2Int> conduitAnchors)
    {
        Requirement = requirement;
        ConduitAnchors = conduitAnchors;
    }

    internal NaturalFixtureUtilityRequirement Requirement { get; }
    internal IReadOnlyList<Vector2Int> ConduitAnchors { get; }
}

internal sealed class NaturalFixturePlacementPlan
{
    private readonly Dictionary<string, NaturalFixturePlacementChoice> byNodeId;

    internal NaturalFixturePlacementPlan(
        IReadOnlyList<NaturalFixturePlacementChoice> choices,
        IReadOnlyList<NaturalFixtureUtilityRoute> routes,
        string canonicalKey,
        int visitedNodes,
        int backtrackCount)
    {
        Choices = choices;
        UtilityRoutes = routes;
        CanonicalKey = canonicalKey ?? string.Empty;
        VisitedNodes = visitedNodes;
        BacktrackCount = backtrackCount;
        byNodeId = choices.ToDictionary(
            value => value.Requirement.StableNodeId,
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<NaturalFixturePlacementChoice> Choices { get; }
    internal IReadOnlyList<NaturalFixtureUtilityRoute> UtilityRoutes { get; }
    internal string CanonicalKey { get; }
    internal int VisitedNodes { get; }
    internal int BacktrackCount { get; }

    internal bool TryGetChoice(
        string stableNodeId,
        out NaturalFixturePlacementChoice choice) =>
        byNodeId.TryGetValue(stableNodeId ?? string.Empty, out choice);
}

internal readonly struct NaturalFixturePlacementResult
{
    private NaturalFixturePlacementResult(
        NaturalFixturePlacementPlan plan,
        NaturalFixturePlacementFailureCode failureCode,
        string failureReason,
        int visitedNodes)
    {
        Plan = plan;
        FailureCode = failureCode;
        FailureReason = failureReason ?? string.Empty;
        VisitedNodes = visitedNodes;
    }

    internal bool Succeeded => Plan != null && FailureCode == NaturalFixturePlacementFailureCode.None;
    internal NaturalFixturePlacementPlan Plan { get; }
    internal NaturalFixturePlacementFailureCode FailureCode { get; }
    internal string FailureReason { get; }
    internal int VisitedNodes { get; }

    internal static NaturalFixturePlacementResult Success(NaturalFixturePlacementPlan plan) =>
        new(plan, NaturalFixturePlacementFailureCode.None, string.Empty, plan?.VisitedNodes ?? 0);

    internal static NaturalFixturePlacementResult Failure(
        NaturalFixturePlacementFailureCode code,
        string reason,
        int visitedNodes = 0) => new(null, code, reason, visitedNodes);
}

/// <summary>
/// Read-only, deterministic, bounded joint planner for natural production fixtures.
/// It never registers occupants and never creates/destroys live building instances.
/// </summary>
internal static class ProductionOutputClearanceNaturalFixturePlacementPlanner
{
    private static readonly Vector2Int[] Cardinal =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.up
    };

    internal static NaturalFixturePlacementResult Plan(
        NaturalFixturePlacementRequest request)
    {
        if (!TryValidate(request, out string invalidReason))
        {
            return NaturalFixturePlacementResult.Failure(
                NaturalFixturePlacementFailureCode.InvalidRequest,
                invalidReason);
        }

        SearchContext context = new(request);
        if (!context.TryBuildCandidates(out string candidateFailure))
        {
            return NaturalFixturePlacementResult.Failure(
                NaturalFixturePlacementFailureCode.Impossible,
                candidateFailure,
                context.VisitedNodes);
        }

        if (context.Search(0, out NaturalFixturePlacementPlan plan))
        {
            return NaturalFixturePlacementResult.Success(plan);
        }

        return NaturalFixturePlacementResult.Failure(
            context.BudgetExceeded
                ? NaturalFixturePlacementFailureCode.BudgetExceeded
                : NaturalFixturePlacementFailureCode.Impossible,
            context.BudgetExceeded
                ? $"Natural fixture placement exceeded the deterministic node budget ({request.MaximumVisitedNodes}). "
                    + context.LastSearchFailure + ";candidates="
                    + context.CandidateSummary
                : "No placement satisfies footprint, access, room, utility, and warehouse constraints. "
                    + context.LastSearchFailure,
            context.VisitedNodes);
    }

    private static bool TryValidate(
        NaturalFixturePlacementRequest request,
        out string reason)
    {
        if (request == null || request.Grid == null || request.PlacementValidator == null)
        {
            reason = "Grid and PlacementValidator are required.";
            return false;
        }
        if (request.MaximumVisitedNodes <= 0)
        {
            reason = "MaximumVisitedNodes must be positive.";
            return false;
        }
        if (request.Nodes == null || request.Nodes.Count == 0)
        {
            reason = "At least one typed node requirement is required.";
            return false;
        }
        if (request.CandidateAnchors == null || request.CandidateAnchors.Count == 0)
        {
            reason = "CandidateAnchors cannot be empty.";
            return false;
        }
        if (request.ReachableCells == null || request.ReachableCells.Count == 0)
        {
            reason = "ReachableCells cannot be empty.";
            return false;
        }

        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        foreach (NaturalFixtureBuildingRequirement node in request.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.StableNodeId)
                || node.Asset == null
                || !nodeIds.Add(node.StableNodeId))
            {
                reason = "Node IDs must be non-empty and unique, and every node needs an asset.";
                return false;
            }
            if ((node.RequireUsableRoom || !string.IsNullOrEmpty(node.RoomGroupId))
                && request.Rooms == null)
            {
                reason = $"Node '{node.StableNodeId}' requires a room cache.";
                return false;
            }
        }

        HashSet<string> edgeIds = new(StringComparer.Ordinal);
        foreach (NaturalFixtureUtilityRequirement edge in
                 request.UtilityEdges ?? Array.Empty<NaturalFixtureUtilityRequirement>())
        {
            int channelBits = (int)edge.Channel;
            bool singleChannel = channelBits != 0
                && (channelBits & (channelBits - 1)) == 0;
            if (string.IsNullOrWhiteSpace(edge.StableEdgeId)
                || !edgeIds.Add(edge.StableEdgeId)
                || !nodeIds.Contains(edge.SourceNodeId)
                || !nodeIds.Contains(edge.TargetNodeId)
                || string.Equals(edge.SourceNodeId, edge.TargetNodeId, StringComparison.Ordinal)
                || !singleChannel
                || edge.ConnectionMode == NaturalFixtureUtilityConnectionMode.ConduitRoute
                && edge.ConduitAsset == null)
            {
                reason = $"Utility edge '{edge.StableEdgeId}' is invalid.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private sealed class SearchContext
    {
        private readonly NaturalFixturePlacementRequest request;
        private NaturalFixtureBuildingRequirement[] orderedNodes;
        private readonly NaturalFixtureUtilityRequirement[] orderedEdges;
        private readonly Vector2Int[] orderedAnchors;
        private readonly HashSet<Vector2Int> reachable;
        private readonly Dictionary<string, Candidate[]> candidatesByNode = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Candidate> assigned = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> roomByGroup = new(StringComparer.Ordinal);

        internal SearchContext(NaturalFixturePlacementRequest request)
        {
            this.request = request;
            orderedNodes = request.Nodes
                .OrderBy(value => value.Role)
                .ThenBy(value => value.StableNodeId, StringComparer.Ordinal)
                .ToArray();
            orderedEdges = (request.UtilityEdges
                    ?? Array.Empty<NaturalFixtureUtilityRequirement>())
                .OrderBy(value => value.StableEdgeId, StringComparer.Ordinal)
                .ToArray();
            orderedAnchors = request.CandidateAnchors
                .Where(request.Grid.IsValidGridPos)
                .Distinct()
                .OrderBy(value => Manhattan(request.ActorOrigin, value))
                .ThenBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            reachable = new HashSet<Vector2Int>(
                request.ReachableCells.Where(request.Grid.IsValidGridPos));
        }

        internal int VisitedNodes { get; private set; }
        internal int BacktrackCount { get; private set; }
        internal bool BudgetExceeded { get; private set; }
        internal string LastSearchFailure { get; private set; } =
            "search-exhausted-without-specific-rejection";
        internal string CandidateSummary => string.Join(
            ",",
            orderedNodes.Select(value => value.StableNodeId + "="
                + (candidatesByNode.TryGetValue(
                        value.StableNodeId,
                        out Candidate[] candidates)
                    ? candidates.Length
                    : 0)));

        internal bool TryBuildCandidates(out string reason)
        {
            foreach (NaturalFixtureBuildingRequirement node in orderedNodes)
            {
                List<Candidate> candidates = new();
                foreach (Vector2Int anchor in orderedAnchors)
                {
                    if (!CanPlaceNode(node, anchor))
                    {
                        continue;
                    }

                    Vector2Int[] footprint = node.Asset.GetGridPosList(anchor)
                        .Distinct()
                        .OrderBy(value => value.y)
                        .ThenBy(value => value.x)
                        .ToArray();
                    if (!TryResolveRoom(node, footprint, out int? roomId))
                    {
                        continue;
                    }

                    Vector2Int[] access = BuildingWorkAccessRules
                        .EnumerateCandidates(footprint, node.Asset.IsGridMovement)
                        .Where(value => request.Grid.IsValidGridPos(value)
                            && request.Grid.IsWalkable(value)
                            && reachable.Contains(value))
                        .Distinct()
                        .OrderBy(value => Manhattan(request.ActorOrigin, value))
                        .ThenBy(value => value.y)
                        .ThenBy(value => value.x)
                        .ToArray();
                    if (node.RequireReachableWorkAccess && access.Length == 0)
                    {
                        continue;
                    }
                    candidates.Add(new Candidate(node, anchor, footprint, access, roomId));
                }

                if (candidates.Count == 0)
                {
                    reason = $"Node '{node.StableNodeId}' has no validator-approved candidate.";
                    return false;
                }
                candidatesByNode[node.StableNodeId] = candidates.ToArray();
            }

            // Minimum-remaining-values ordering is deterministic and removes
            // the facility/support/utility Cartesian explosion. Role and ID
            // remain stable tie-breaks; room-group assignment is symmetric, so
            // whichever constrained member is chosen first establishes the
            // exact shared room for the rest.
            orderedNodes = orderedNodes
                .OrderBy(value => candidatesByNode[value.StableNodeId].Length)
                .ThenBy(value => value.Role)
                .ThenBy(value => value.StableNodeId, StringComparer.Ordinal)
                .ToArray();

            reason = string.Empty;
            return true;
        }

        private bool CanPlaceNode(
            NaturalFixtureBuildingRequirement node,
            Vector2Int anchor)
        {
            if (node.PlacementPolicy ==
                NaturalFixturePlacementPolicy.GameplayRules)
            {
                return request.PlacementValidator.CanBuild(
                    request.Grid,
                    node.Asset,
                    anchor,
                    out _);
            }

            // Fixture infrastructure is not the subject of the portfolio and
            // may have world-feature authoring conditions (for example, the
            // only fuel-free generator is a water wheel). It still obeys the
            // exact authored footprint, grid layer and live occupancy rules;
            // this policy is unavailable to gameplay facilities, supports or
            // warehouses.
            if (node.Role != NaturalFixtureNodeRole.UtilitySource)
                return false;
            IReadOnlyList<Vector2Int> footprint = node.Asset.GetGridPosList(anchor);
            return footprint.Count > 0
                && footprint.All(request.Grid.IsValidGridPos)
                && footprint.All(cell => request.Grid.GetGridCell(cell)?.CanOccupy(
                    node.Asset.Placement.Layer) == true);
        }

        internal bool Search(int index, out NaturalFixturePlacementPlan plan)
        {
            plan = null;
            if (BudgetExceeded)
            {
                return false;
            }
            if (index >= orderedNodes.Length)
            {
                if (!TryResolveRoutes(out NaturalFixtureUtilityRoute[] routes))
                {
                    return false;
                }
                NaturalFixturePlacementChoice[] choices = assigned.Values
                    .OrderBy(value => value.Node.StableNodeId, StringComparer.Ordinal)
                    .Select(value => value.ToChoice())
                    .ToArray();
                plan = new NaturalFixturePlacementPlan(
                    choices,
                    routes,
                    BuildCanonicalKey(choices, routes),
                    VisitedNodes,
                    BacktrackCount);
                return true;
            }

            NaturalFixtureBuildingRequirement node = orderedNodes[index];
            foreach (Candidate candidate in OrderCandidatesForAssignedEdges(node))
            {
                if (!TryVisit())
                {
                    LastSearchFailure = "node-search-budget:"
                        + node.StableNodeId;
                    return false;
                }
                if (!CanAssign(candidate))
                {
                    LastSearchFailure = "assignment-overlap-or-room-group:"
                        + node.StableNodeId;
                    continue;
                }

                assigned.Add(node.StableNodeId, candidate);
                bool addedRoom = TryAddRoomGroup(candidate);
                bool accessValid = HasValidAssignedAccess();
                bool forwardValid = accessValid && HasForwardCandidate(index + 1);
                if (accessValid
                    && forwardValid
                    && Search(index + 1, out plan))
                {
                    return true;
                }
                assigned.Remove(node.StableNodeId);
                BacktrackCount = checked(BacktrackCount + 1);
                if (addedRoom)
                {
                    roomByGroup.Remove(node.RoomGroupId);
                }
                if (BudgetExceeded)
                {
                    return false;
                }
            }
            return false;
        }

        private IEnumerable<Candidate> OrderCandidatesForAssignedEdges(
            NaturalFixtureBuildingRequirement node)
        {
            Candidate[] candidates = candidatesByNode[node.StableNodeId];
            Candidate[] connectedAssigned = orderedEdges
                .Where(edge => string.Equals(
                        edge.SourceNodeId,
                        node.StableNodeId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        edge.TargetNodeId,
                        node.StableNodeId,
                        StringComparison.Ordinal))
                .Select(edge => string.Equals(
                        edge.SourceNodeId,
                        node.StableNodeId,
                        StringComparison.Ordinal)
                    ? edge.TargetNodeId
                    : edge.SourceNodeId)
                .Where(assigned.ContainsKey)
                .Select(value => assigned[value])
                .Distinct()
                .ToArray();
            if (connectedAssigned.Length == 0)
                return candidates;

            Candidate[] ordered = candidates
                .OrderBy(candidate => connectedAssigned.Sum(other =>
                    candidate.Footprint.Min(left => other.Footprint.Min(right =>
                        Manhattan(left, right)))))
                .ThenBy(candidate => Manhattan(request.ActorOrigin, candidate.Anchor))
                .ThenBy(candidate => candidate.Anchor.y)
                .ThenBy(candidate => candidate.Anchor.x)
                .ToArray();
            // Utility sources are verifier scaffolding rather than authored
            // portfolio subjects. A deterministic beam around their already
            // assigned consumers prevents three independent utility sources
            // from multiplying the exact gameplay-node search by every remote
            // infrastructure cell. Gameplay facilities/supports/warehouses are
            // never truncated.
            return node.PlacementPolicy ==
                    NaturalFixturePlacementPolicy.FixtureInfrastructureOccupancy
                ? ordered.Take(8).ToArray()
                : ordered;
        }

        private bool CanAssign(Candidate candidate)
        {
            if (!string.IsNullOrEmpty(candidate.Node.RoomGroupId)
                && roomByGroup.TryGetValue(candidate.Node.RoomGroupId, out int roomId)
                && candidate.RoomId != roomId)
            {
                return false;
            }

            foreach (Candidate other in assigned.Values)
            {
                if (candidate.Node.Asset.Placement.Layer == other.Node.Asset.Placement.Layer
                    && candidate.Footprint.Any(other.FootprintSet.Contains))
                {
                    return false;
                }
            }
            return true;
        }

        private bool HasForwardCandidate(int nextIndex)
        {
            for (int i = nextIndex; i < orderedNodes.Length; i++)
            {
                bool any = candidatesByNode[orderedNodes[i].StableNodeId]
                    .Any(CanAssign);
                if (!any)
                {
                    LastSearchFailure = "no-forward-candidate:"
                        + orderedNodes[i].StableNodeId
                        + ":candidate-count="
                        + candidatesByNode[orderedNodes[i].StableNodeId].Length;
                    return false;
                }
            }
            return true;
        }

        private bool HasValidAssignedAccess()
        {
            HashSet<Vector2Int> blocked = new();
            foreach (Candidate candidate in assigned.Values)
            {
                if (!candidate.Node.Asset.IsGridMovement
                    && candidate.Node.Asset.Placement.Layer != GridLayer.Utility)
                {
                    blocked.UnionWith(candidate.Footprint);
                }
            }

            HashSet<Vector2Int> shadowReachable = BuildShadowReachable(blocked);
            List<Vector2Int[]> accessSets = new();
            foreach (Candidate candidate in assigned.Values
                         .Where(value => value.Node.RequireReachableWorkAccess)
                         .OrderBy(value => value.Node.StableNodeId, StringComparer.Ordinal))
            {
                Vector2Int[] available = candidate.Access
                    .Where(value => !blocked.Contains(value) && shadowReachable.Contains(value))
                    .ToArray();
                if (available.Length == 0)
                {
                    LastSearchFailure = "no-residual-work-access:"
                        + candidate.Node.StableNodeId
                        + ":authored-access=" + candidate.Access.Length;
                    return false;
                }
                foreach (Vector2Int[] other in accessSets)
                {
                    if (!BuildingWorkAccessRules.CanShareOperationalAccess(other, available))
                    {
                        LastSearchFailure = "exclusive-single-access-conflict:"
                            + candidate.Node.StableNodeId;
                        return false;
                    }
                }
                accessSets.Add(available);
            }
            return true;
        }

        private HashSet<Vector2Int> BuildShadowReachable(HashSet<Vector2Int> blocked)
        {
            HashSet<Vector2Int> result = new();
            if (!reachable.Contains(request.ActorOrigin))
            {
                return result;
            }
            Queue<Vector2Int> queue = new();
            if (!blocked.Contains(request.ActorOrigin))
            {
                result.Add(request.ActorOrigin);
                queue.Enqueue(request.ActorOrigin);
            }
            else
            {
                // A verifier fixture may be placed on the actor's current
                // building-layer cell while the actor still occupies the
                // independent character layer. Grid pathfinding permits that
                // actor to step horizontally out of the newly blocked start
                // cell. Seed those exact exits instead of declaring the whole
                // residual graph unreachable.
                foreach (Vector2Int exit in Cardinal
                    .Select(offset => request.ActorOrigin + offset)
                    .Where(value => reachable.Contains(value)
                        && !blocked.Contains(value))
                    .OrderBy(value => value.y)
                    .ThenBy(value => value.x))
                {
                    if (result.Add(exit))
                        queue.Enqueue(exit);
                }
            }
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int offset in Cardinal)
                {
                    Vector2Int next = current + offset;
                    if (reachable.Contains(next)
                        && !blocked.Contains(next)
                        && result.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
            return result;
        }

        private bool TryResolveRoutes(out NaturalFixtureUtilityRoute[] routes)
        {
            List<NaturalFixtureUtilityRoute> result = new();
            foreach (NaturalFixtureUtilityRequirement edge in orderedEdges)
            {
                Candidate source = assigned[edge.SourceNodeId];
                Candidate target = assigned[edge.TargetNodeId];
                if (!HasUtilityChannel(source.Node.Asset, edge.Channel)
                    || !HasUtilityChannel(target.Node.Asset, edge.Channel))
                {
                    routes = null;
                    return false;
                }

                if (AreSameOrAdjacent(source.Footprint, target.Footprint))
                {
                    result.Add(new NaturalFixtureUtilityRoute(edge, Array.Empty<Vector2Int>()));
                    continue;
                }
                if (edge.ConnectionMode == NaturalFixtureUtilityConnectionMode.DirectAdjacent
                    || edge.ConduitAsset == null
                    || edge.ConduitAsset.Placement.Layer != GridLayer.Utility
                    || !HasUtilityChannel(edge.ConduitAsset, edge.Channel)
                    || !TryFindConduitRoute(
                        edge,
                        source,
                        target,
                        out Vector2Int[] route))
                {
                    routes = null;
                    return false;
                }
                result.Add(new NaturalFixtureUtilityRoute(edge, route));
            }
            routes = result.ToArray();
            return true;
        }

        private bool TryFindConduitRoute(
            NaturalFixtureUtilityRequirement edge,
            Candidate source,
            Candidate target,
            out Vector2Int[] route)
        {
            List<ConduitCandidate> all = new();
            for (int y = 0; y < request.Grid.height; y++)
            for (int x = 0; x < request.Grid.width; x++)
            {
                Vector2Int anchor = new(x, y);
                if (!TryVisit())
                {
                    LastSearchFailure = "conduit-candidate-scan-budget:"
                        + edge.StableEdgeId;
                    route = null;
                    return false;
                }
                Vector2Int[] footprint = edge.ConduitAsset.GetGridPosList(anchor)
                    .Distinct()
                    .OrderBy(value => value.y)
                    .ThenBy(value => value.x)
                    .ToArray();
                // Conduits are explicit verifier infrastructure, just like
                // their source node. Preserve authored footprint/layer and
                // live occupancy, but do not apply progression/world-feature
                // build conditions to the QA wiring itself.
                if (footprint.Length == 0
                    || footprint.Any(cell => !request.Grid.IsValidGridPos(cell))
                    || footprint.Any(cell => request.Grid.GetGridCell(cell)
                        ?.CanOccupy(edge.ConduitAsset.Placement.Layer) != true))
                {
                    continue;
                }
                all.Add(new ConduitCandidate(anchor, footprint));
            }
            all = all.OrderBy(value => Manhattan(request.ActorOrigin, value.Anchor))
                .ThenBy(value => value.Anchor.y)
                .ThenBy(value => value.Anchor.x)
                .ToList();

            Dictionary<Vector2Int, List<int>> indicesByFootprintCell = new();
            for (int index = 0; index < all.Count; index++)
            {
                foreach (Vector2Int cell in all[index].Footprint)
                {
                    if (!indicesByFootprintCell.TryGetValue(
                            cell,
                            out List<int> indices))
                    {
                        indices = new List<int>();
                        indicesByFootprintCell.Add(cell, indices);
                    }
                    indices.Add(index);
                }
            }

            Queue<int> queue = new();
            int[] previous = Enumerable.Repeat(-2, all.Count).ToArray();
            for (int i = 0; i < all.Count; i++)
            {
                if (AreSameOrAdjacent(source.Footprint, all[i].Footprint))
                {
                    previous[i] = -1;
                    queue.Enqueue(i);
                }
            }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!TryVisit())
                {
                    LastSearchFailure = "conduit-bfs-budget:"
                        + edge.StableEdgeId;
                    route = null;
                    return false;
                }
                if (AreSameOrAdjacent(all[current].Footprint, target.Footprint))
                {
                    List<Vector2Int> anchors = new();
                    for (int cursor = current; cursor >= 0; cursor = previous[cursor])
                    {
                        anchors.Add(all[cursor].Anchor);
                    }
                    anchors.Reverse();
                    route = anchors.ToArray();
                    return true;
                }
                HashSet<int> adjacentIndices = new();
                foreach (Vector2Int cell in all[current].Footprint)
                {
                    foreach (Vector2Int offset in Cardinal)
                    {
                        if (indicesByFootprintCell.TryGetValue(
                                cell + offset,
                                out List<int> indices))
                        {
                            adjacentIndices.UnionWith(indices);
                        }
                    }
                }
                foreach (int next in adjacentIndices.OrderBy(value => value))
                {
                    if (!TryVisit())
                    {
                        LastSearchFailure = "conduit-adjacency-budget:"
                            + edge.StableEdgeId;
                        route = null;
                        return false;
                    }
                    if (previous[next] == -2
                        && !all[current].Footprint.Any(
                            all[next].FootprintSet.Contains))
                    {
                        previous[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }
            route = null;
            return false;
        }

        private bool TryResolveRoom(
            NaturalFixtureBuildingRequirement node,
            IReadOnlyList<Vector2Int> footprint,
            out int? roomId)
        {
            roomId = null;
            if (!node.RequireUsableRoom && string.IsNullOrEmpty(node.RoomGroupId))
            {
                return true;
            }
            RoomInstance common = null;
            foreach (Vector2Int cell in footprint)
            {
                if (!request.Rooms.TryGetRoom(request.Grid, cell, out RoomInstance room)
                    || room == null
                    || !room.IsUsable
                    || common != null && room.Id != common.Id)
                {
                    return false;
                }
                common ??= room;
            }
            roomId = common?.Id;
            return roomId.HasValue;
        }

        private bool TryAddRoomGroup(Candidate candidate)
        {
            string group = candidate.Node.RoomGroupId;
            if (string.IsNullOrEmpty(group) || roomByGroup.ContainsKey(group))
            {
                return false;
            }
            roomByGroup.Add(group, candidate.RoomId.Value);
            return true;
        }

        private bool TryVisit()
        {
            if (BudgetExceeded)
            {
                return false;
            }
            VisitedNodes = checked(VisitedNodes + 1);
            if (VisitedNodes <= request.MaximumVisitedNodes)
            {
                return true;
            }
            BudgetExceeded = true;
            return false;
        }
    }

    private static bool HasUtilityChannel(BuildingSO asset, UtilityChannel channel)
    {
        if (asset == null)
        {
            return false;
        }
        UtilityChannel channels = asset.GetAbility<BuildingUtilityConnectionAbility>()?.channels
            ?? UtilityChannel.None;
        if (asset.GetAbility<BuildingPowerProducerAbility>() != null
            || asset.GetAbility<BuildingPowerConsumerAbility>() != null
            || asset.GetAbility<BuildingPowerStorageAbility>() != null
            || asset.GetAbility<BuildingCircuitBreakerAbility>() != null
            || asset.GetAbility<BuildingAutomationAbility>() != null
            || asset.GetProductionSupportAbility() is
                BuildingProductionSupportAbility support
                && support.requiresPower)
        {
            channels |= UtilityChannel.Power;
        }
        if (asset.GetAbility<BuildingWaterProducerAbility>() != null
            || asset.GetAbility<BuildingWaterFixtureAbility>() != null)
        {
            channels |= UtilityChannel.CleanWater;
        }
        BuildingWaterStorageAbility storage = asset.GetAbility<BuildingWaterStorageAbility>();
        if (storage != null)
        {
            channels |= storage.channels;
        }
        if (asset.GetAbility<BuildingWaterFixtureAbility>() != null
            || asset.GetAbility<BuildingWastewaterProcessorAbility>() != null)
        {
            channels |= UtilityChannel.Wastewater;
        }
        return (channels & channel) != 0;
    }

    private static bool AreSameOrAdjacent(
        IReadOnlyList<Vector2Int> left,
        IReadOnlyList<Vector2Int> right)
    {
        for (int i = 0; i < left.Count; i++)
        for (int j = 0; j < right.Count; j++)
        {
            if (Manhattan(left[i], right[j]) <= 1)
            {
                return true;
            }
        }
        return false;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private static string BuildCanonicalKey(
        IReadOnlyList<NaturalFixturePlacementChoice> choices,
        IReadOnlyList<NaturalFixtureUtilityRoute> routes)
    {
        StringBuilder builder = new();
        foreach (NaturalFixturePlacementChoice choice in choices)
        {
            builder.Append(choice.Requirement.StableNodeId)
                .Append('@').Append(choice.Anchor.x).Append(',').Append(choice.Anchor.y)
                .Append(';');
        }
        foreach (NaturalFixtureUtilityRoute route in routes)
        {
            builder.Append(route.Requirement.StableEdgeId).Append('=');
            foreach (Vector2Int anchor in route.ConduitAnchors)
            {
                builder.Append(anchor.x).Append(',').Append(anchor.y).Append('|');
            }
            builder.Append(';');
        }
        return builder.ToString();
    }

    private sealed class Candidate
    {
        internal Candidate(
            NaturalFixtureBuildingRequirement node,
            Vector2Int anchor,
            Vector2Int[] footprint,
            Vector2Int[] access,
            int? roomId)
        {
            Node = node;
            Anchor = anchor;
            Footprint = footprint;
            FootprintSet = new HashSet<Vector2Int>(footprint);
            Access = access;
            RoomId = roomId;
        }
        internal NaturalFixtureBuildingRequirement Node { get; }
        internal Vector2Int Anchor { get; }
        internal Vector2Int[] Footprint { get; }
        internal HashSet<Vector2Int> FootprintSet { get; }
        internal Vector2Int[] Access { get; }
        internal int? RoomId { get; }
        internal NaturalFixturePlacementChoice ToChoice() =>
            new(Node, Anchor, Footprint, Access);
    }

    private sealed class ConduitCandidate
    {
        internal ConduitCandidate(Vector2Int anchor, Vector2Int[] footprint)
        {
            Anchor = anchor;
            Footprint = footprint;
            FootprintSet = new HashSet<Vector2Int>(footprint);
        }
        internal Vector2Int Anchor { get; }
        internal Vector2Int[] Footprint { get; }
        internal HashSet<Vector2Int> FootprintSet { get; }
    }
}
#endif
