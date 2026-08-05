using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class GridTraversalHeuristicIndex
{
    private readonly List<int>[] verticalPortalXsByFloor;
    private bool hasArbitraryHorizontalTraversal;
    private bool hasArbitraryVerticalTraversal;
    private int minimumAdjacentVerticalTraversalCost;

    public GridTraversalHeuristicIndex(int floorCount)
    {
        verticalPortalXsByFloor = new List<int>[Mathf.Max(1, floorCount)];
        Reset();
    }

    public int EstimateRemainingCost(
        Vector2Int position,
        Vector2Int? exactDestination,
        IGridTraversalCostPolicy costPolicy)
    {
        if (!exactDestination.HasValue)
        {
            return 0;
        }

        long estimate = 0L;
        if (!hasArbitraryHorizontalTraversal)
        {
            int horizontalDistance = Mathf.Abs(position.x - exactDestination.Value.x);
            if (position.y != exactDestination.Value.y
                && TryGetNearestVerticalPortalDistance(
                    position.y,
                    position.x,
                    out int distanceToDeparturePortal)
                && TryGetNearestVerticalPortalDistance(
                    exactDestination.Value.y,
                    exactDestination.Value.x,
                    out int distanceFromArrivalPortal))
            {
                horizontalDistance = Mathf.Max(
                    horizontalDistance,
                    distanceToDeparturePortal + distanceFromArrivalPortal);
            }

            estimate += (long)horizontalDistance
                * Mathf.Max(0, costPolicy.MinimumHorizontalCost);
        }

        if (ReferenceEquals(costPolicy, DefaultGridTraversalCostPolicy.Instance)
            && !hasArbitraryVerticalTraversal
            && minimumAdjacentVerticalTraversalCost != int.MaxValue)
        {
            int verticalDistance = Mathf.Abs(position.y - exactDestination.Value.y);
            estimate += (long)verticalDistance * minimumAdjacentVerticalTraversalCost;
        }

        return estimate >= int.MaxValue ? int.MaxValue : (int)estimate;
    }

    public void Rebuild(Grid grid)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        Reset();
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int from = new Vector2Int(x, y);
                GridCell cell = grid.GetGridCell(from);
                foreach (GridTraversalLink link in cell.TraversalLinks)
                {
                    ObserveLink(grid, from, link);
                }
            }
        }
    }

    public void ObserveLink(Grid grid, Vector2Int from, GridTraversalLink link)
    {
        if (grid == null || link == null)
        {
            return;
        }

        if (link.MoveType == GridMoveType.Teleport && link.To.x != from.x)
        {
            hasArbitraryHorizontalTraversal = true;
        }

        int verticalDistance = Mathf.Abs(link.To.y - from.y);
        if (verticalDistance <= 0)
        {
            return;
        }

        RecordVerticalPortal(from);
        RecordVerticalPortal(link.To);
        if (verticalDistance != 1)
        {
            hasArbitraryVerticalTraversal = true;
            return;
        }

        GridTraversalStepData step = new GridTraversalStepData(
            from,
            link.To,
            link.Through,
            link.MoveType);
        int cost = DefaultGridTraversalCostPolicy.Instance.GetTraversalCost(
            grid,
            in step,
            default);
        if (cost > 0 && cost < minimumAdjacentVerticalTraversalCost)
        {
            minimumAdjacentVerticalTraversalCost = cost;
        }
    }

    private void Reset()
    {
        hasArbitraryHorizontalTraversal = false;
        hasArbitraryVerticalTraversal = false;
        minimumAdjacentVerticalTraversalCost = int.MaxValue;
        Array.Clear(verticalPortalXsByFloor, 0, verticalPortalXsByFloor.Length);
    }

    private void RecordVerticalPortal(Vector2Int position)
    {
        if (position.y < 0 || position.y >= verticalPortalXsByFloor.Length)
        {
            return;
        }

        List<int> portals = verticalPortalXsByFloor[position.y];
        if (portals == null)
        {
            portals = new List<int>(2);
            verticalPortalXsByFloor[position.y] = portals;
        }

        int index = portals.BinarySearch(position.x);
        if (index < 0)
        {
            portals.Insert(~index, position.x);
        }
    }

    private bool TryGetNearestVerticalPortalDistance(
        int floor,
        int x,
        out int distance)
    {
        distance = 0;
        if (floor < 0
            || floor >= verticalPortalXsByFloor.Length
            || verticalPortalXsByFloor[floor] == null
            || verticalPortalXsByFloor[floor].Count == 0)
        {
            return false;
        }

        List<int> portals = verticalPortalXsByFloor[floor];
        int index = portals.BinarySearch(x);
        if (index >= 0)
        {
            return true;
        }

        int insertionIndex = ~index;
        int lowerDistance = insertionIndex > 0
            ? x - portals[insertionIndex - 1]
            : int.MaxValue;
        int upperDistance = insertionIndex < portals.Count
            ? portals[insertionIndex] - x
            : int.MaxValue;
        distance = Mathf.Min(lowerDistance, upperDistance);
        return distance != int.MaxValue;
    }
}
