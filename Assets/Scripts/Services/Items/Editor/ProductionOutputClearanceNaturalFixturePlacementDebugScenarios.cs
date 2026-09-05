#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pure focused checks for the joint natural-fixture planner. The caller supplies
/// actual recipe/crop assets and the live-grid-derived reachability snapshot; this
/// class neither materializes buildings nor changes Grid state.
/// </summary>
internal static class ProductionOutputClearanceNaturalFixturePlacementDebugScenarios
{
    /// <summary>
    /// Runs the common invariants against a natural request and an adversarial
    /// request whose stable first candidate must dead-end before a later candidate
    /// succeeds. Intended for the recipe and special-driver callsites.
    /// </summary>
    internal static NaturalFixturePlacementPlan VerifyOrThrow(
        NaturalFixturePlacementRequest naturalRequest,
        NaturalFixturePlacementRequest adversarialBacktrackingRequest)
    {
        if (naturalRequest == null)
        {
            throw new ArgumentNullException(nameof(naturalRequest));
        }
        if (adversarialBacktrackingRequest == null)
        {
            throw new ArgumentNullException(nameof(adversarialBacktrackingRequest));
        }

        GridSnapshot naturalBefore = GridSnapshot.Capture(naturalRequest.Grid);
        NaturalFixturePlacementResult natural =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(naturalRequest);
        Require(natural.Succeeded, Describe("natural plan", natural));
        naturalBefore.RequireUnchanged(naturalRequest.Grid, "natural plan");

        NaturalFixturePlacementRequest shuffled = Clone(
            naturalRequest,
            naturalRequest.Nodes.Reverse().ToArray(),
            (naturalRequest.UtilityEdges
                ?? Array.Empty<NaturalFixtureUtilityRequirement>()).Reverse().ToArray(),
            naturalRequest.CandidateAnchors.Reverse().ToArray(),
            naturalRequest.ReachableCells.Reverse().ToArray(),
            naturalRequest.MaximumVisitedNodes);
        NaturalFixturePlacementResult shuffledResult =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(shuffled);
        Require(shuffledResult.Succeeded, Describe("shuffled plan", shuffledResult));
        Require(
            string.Equals(
                natural.Plan.CanonicalKey,
                shuffledResult.Plan.CanonicalKey,
                StringComparison.Ordinal),
            "Input enumeration order changed the canonical placement plan.");
        naturalBefore.RequireUnchanged(naturalRequest.Grid, "shuffled plan");

        int insufficientBudget = Math.Max(1, natural.Plan.VisitedNodes - 1);
        NaturalFixturePlacementResult budget =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(Clone(
                naturalRequest,
                naturalRequest.Nodes,
                naturalRequest.UtilityEdges,
                naturalRequest.CandidateAnchors,
                naturalRequest.ReachableCells,
                insufficientBudget));
        Require(
            !budget.Succeeded
            && budget.FailureCode == NaturalFixturePlacementFailureCode.BudgetExceeded,
            Describe("budget failure", budget));
        naturalBefore.RequireUnchanged(naturalRequest.Grid, "budget failure");

        NaturalFixturePlacementRequest impossibleRequest = Clone(
            naturalRequest,
            naturalRequest.Nodes,
            naturalRequest.UtilityEdges,
            new[] { new Vector2Int(-1, -1) },
            naturalRequest.ReachableCells,
            naturalRequest.MaximumVisitedNodes);
        NaturalFixturePlacementResult impossible =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(impossibleRequest);
        Require(
            !impossible.Succeeded
            && impossible.FailureCode == NaturalFixturePlacementFailureCode.Impossible,
            Describe("impossible failure", impossible));
        naturalBefore.RequireUnchanged(naturalRequest.Grid, "impossible failure");

        GridSnapshot adversarialBefore =
            GridSnapshot.Capture(adversarialBacktrackingRequest.Grid);
        NaturalFixturePlacementResult backtracked =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(
                adversarialBacktrackingRequest);
        Require(backtracked.Succeeded, Describe("backtracking probe", backtracked));
        Require(
            backtracked.Plan.BacktrackCount > 0,
            "Adversarial probe did not prove an assigned-candidate backtrack.");
        adversarialBefore.RequireUnchanged(
            adversarialBacktrackingRequest.Grid,
            "backtracking probe");

        NaturalFixturePlacementRequest reversedProbe = Clone(
            adversarialBacktrackingRequest,
            adversarialBacktrackingRequest.Nodes.Reverse().ToArray(),
            (adversarialBacktrackingRequest.UtilityEdges
                ?? Array.Empty<NaturalFixtureUtilityRequirement>()).Reverse().ToArray(),
            adversarialBacktrackingRequest.CandidateAnchors.Reverse().ToArray(),
            adversarialBacktrackingRequest.ReachableCells.Reverse().ToArray(),
            adversarialBacktrackingRequest.MaximumVisitedNodes);
        NaturalFixturePlacementResult reversedBacktracked =
            ProductionOutputClearanceNaturalFixturePlacementPlanner.Plan(reversedProbe);
        Require(
            reversedBacktracked.Succeeded
            && string.Equals(
                backtracked.Plan.CanonicalKey,
                reversedBacktracked.Plan.CanonicalKey,
                StringComparison.Ordinal),
            Describe("reversed backtracking probe", reversedBacktracked));
        adversarialBefore.RequireUnchanged(
            adversarialBacktrackingRequest.Grid,
            "reversed backtracking probe");

        return natural.Plan;
    }

    private static NaturalFixturePlacementRequest Clone(
        NaturalFixturePlacementRequest source,
        IReadOnlyList<NaturalFixtureBuildingRequirement> nodes,
        IReadOnlyList<NaturalFixtureUtilityRequirement> edges,
        IReadOnlyList<Vector2Int> anchors,
        IReadOnlyList<Vector2Int> reachable,
        int maximumVisitedNodes) => new()
    {
        Grid = source.Grid,
        PlacementValidator = source.PlacementValidator,
        Rooms = source.Rooms,
        ActorOrigin = source.ActorOrigin,
        CandidateAnchors = anchors,
        ReachableCells = reachable,
        Nodes = nodes,
        UtilityEdges = edges,
        MaximumVisitedNodes = maximumVisitedNodes
    };

    private static string Describe(
        string operation,
        NaturalFixturePlacementResult result) =>
        $"{operation} failed: code={result.FailureCode};"
        + $"visited={result.VisitedNodes};reason={result.FailureReason}";

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class GridSnapshot
    {
        private readonly int version;
        private readonly int structuralVersion;
        private readonly int navigationVersion;
        private readonly CellSnapshot[] cells;

        private GridSnapshot(
            int version,
            int structuralVersion,
            int navigationVersion,
            CellSnapshot[] cells)
        {
            this.version = version;
            this.structuralVersion = structuralVersion;
            this.navigationVersion = navigationVersion;
            this.cells = cells;
        }

        internal static GridSnapshot Capture(Grid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            List<CellSnapshot> result = new(grid.width * grid.height);
            for (int y = 0; y < grid.height; y++)
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int position = new(x, y);
                GridCell cell = grid.GetGridCell(position);
                result.Add(new CellSnapshot(
                    position,
                    cell.AreaType,
                    cell.TerrainType,
                    cell.GetAllOccupants().ToArray()));
            }
            return new GridSnapshot(
                grid.version,
                grid.StructuralVersion,
                grid.NavigationVersion,
                result.ToArray());
        }

        internal void RequireUnchanged(Grid grid, string operation)
        {
            Require(grid != null, $"{operation} replaced the grid with null.");
            Require(
                version == grid.version
                && structuralVersion == grid.StructuralVersion
                && navigationVersion == grid.NavigationVersion,
                $"{operation} mutated a grid version.");
            foreach (CellSnapshot expected in cells)
            {
                GridCell actual = grid.GetGridCell(expected.Position);
                Require(actual != null, $"{operation} removed grid cell {expected.Position}.");
                Require(
                    actual.AreaType == expected.AreaType
                    && actual.TerrainType == expected.TerrainType,
                    $"{operation} changed authored cell data at {expected.Position}.");
                IGridOccupant[] occupants = actual.GetAllOccupants().ToArray();
                Require(
                    occupants.Length == expected.Occupants.Length,
                    $"{operation} changed occupant count at {expected.Position}.");
                for (int index = 0; index < occupants.Length; index++)
                {
                    Require(
                        ReferenceEquals(occupants[index], expected.Occupants[index]),
                        $"{operation} changed occupant identity/order at {expected.Position}.");
                }
            }
        }
    }

    private readonly struct CellSnapshot
    {
        internal CellSnapshot(
            Vector2Int position,
            GridCellAreaType areaType,
            GridCellTerrainType terrainType,
            IGridOccupant[] occupants)
        {
            Position = position;
            AreaType = areaType;
            TerrainType = terrainType;
            Occupants = occupants;
        }
        internal Vector2Int Position { get; }
        internal GridCellAreaType AreaType { get; }
        internal GridCellTerrainType TerrainType { get; }
        internal IGridOccupant[] Occupants { get; }
    }
}
#endif
