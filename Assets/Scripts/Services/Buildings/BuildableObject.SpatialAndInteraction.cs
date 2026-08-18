using System;
using System.Collections.Generic;
using UnityEngine;

public static class BuildingWorkAccessRules
{
    public static bool CanShareOperationalAccess(
        IReadOnlyList<Vector2Int> leftCandidates,
        IReadOnlyList<Vector2Int> rightCandidates)
    {
        if (leftCandidates == null)
            throw new ArgumentNullException(nameof(leftCandidates));
        if (rightCandidates == null)
            throw new ArgumentNullException(nameof(rightCandidates));

        // Access cells are normally shared corridor space. The one unsafe case is
        // two facilities whose sole authored work stand is the same cell: either
        // facility can then reserve the only stand needed by the other one.
        if (leftCandidates.Count != 1 || rightCandidates.Count != 1)
            return true;
        return leftCandidates[0] != rightCandidates[0];
    }

    public static IReadOnlyList<Vector2Int> EnumerateCandidates(
        IReadOnlyList<Vector2Int> footprint,
        bool traversableFootprint)
    {
        if (footprint == null || footprint.Count == 0)
        {
            return Array.Empty<Vector2Int>();
        }

        List<Vector2Int> ordered = new List<Vector2Int>(
            traversableFootprint ? footprint.Count : footprint.Count * 2);
        HashSet<Vector2Int> footprintSet = traversableFootprint
            ? null
            : new HashSet<Vector2Int>(footprint);
        HashSet<Vector2Int> emitted = new HashSet<Vector2Int>();
        for (int index = 0; index < footprint.Count; index++)
        {
            Vector2Int occupied = footprint[index];
            if (traversableFootprint)
            {
                Add(occupied);
                continue;
            }

            // Grid y is a dungeon floor, so work access is horizontal.
            Add(new Vector2Int(occupied.x - 1, occupied.y));
            Add(new Vector2Int(occupied.x + 1, occupied.y));
        }

        return ordered;

        void Add(Vector2Int candidate)
        {
            if ((footprintSet == null || !footprintSet.Contains(candidate))
                && emitted.Add(candidate))
            {
                ordered.Add(candidate);
            }
        }
    }
}

internal sealed class BuildableObjectSpatialQuery
{
    private readonly Transform transform;
    private readonly IGridOccupant occupant;

    internal BuildableObjectSpatialQuery(
        Transform transform,
        IGridOccupant occupant)
    {
        this.transform = transform
            ?? throw new ArgumentNullException(nameof(transform));
        this.occupant = occupant
            ?? throw new ArgumentNullException(nameof(occupant));
    }

    internal bool TryFindNearestFacilityCell(
        Grid grid,
        IReadOnlyList<Vector2Int> buildPositions,
        Vector2Int centerPosition,
        Vector3 fromWorld,
        bool requireRegisteredOccupant,
        out Vector2Int result)
    {
        result = default;
        if (grid == null
            || buildPositions == null
            || buildPositions.Count == 0)
        {
            return false;
        }

        int floor = centerPosition.y;
        Vector2Int origin = grid.GetXY(fromWorld);
        origin.y = floor;
        bool found = false;
        Vector2Int best = default;
        int bestDistance = int.MaxValue;
        foreach (Vector2Int position in buildPositions)
        {
            if (position.y != floor || !grid.IsValidGridPos(position))
            {
                continue;
            }

            GridCell cell = grid.GetGridCell(position);
            if (requireRegisteredOccupant
                && (cell == null || !cell.ContainsOccupant(occupant)))
            {
                continue;
            }

            int distance = Mathf.Abs(position.x - origin.x);
            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            best = position;
            bestDistance = distance;
        }

        result = best;
        return found;
    }

    internal bool TryGetConfiguredFacilityAnchorWorldPosition(
        Grid grid,
        BuildingSO buildingData,
        Vector2Int centerPosition,
        string purposeId,
        Vector3 fromWorld,
        out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        if (grid == null
            || buildingData == null
            || buildingData.FacilityAnchors == null
            || string.IsNullOrWhiteSpace(purposeId))
        {
            return false;
        }

        bool found = false;
        float bestDistance = float.MaxValue;
        foreach (FacilityAnchorSlot slot in
                 buildingData.FacilityAnchors.Enumerate(purposeId))
        {
            Vector3 candidate = grid.GetWorldPos(new Vector2(
                centerPosition.x + slot.offset.x,
                centerPosition.y + slot.offset.y));
            float distance = (candidate - fromWorld).sqrMagnitude;
            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            worldPosition = candidate;
        }

        return found;
    }

    internal bool TryGetHorizontalFootprintAnchorWorldPosition(
        Grid grid,
        IReadOnlyList<Vector2Int> buildPositions,
        Vector2Int centerPosition,
        float normalizedX,
        out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        if (grid == null
            || buildPositions == null
            || buildPositions.Count == 0)
        {
            return false;
        }

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        foreach (Vector2Int position in buildPositions)
        {
            if (position.y != centerPosition.y)
            {
                continue;
            }

            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
        }

        if (minX == int.MaxValue || maxX == int.MinValue)
        {
            return false;
        }

        float clamped = Mathf.Clamp01(normalizedX);
        float x = Mathf.Lerp(minX, maxX, clamped);
        worldPosition = grid.GetWorldPos(new Vector2(x, centerPosition.y));
        return true;
    }
}
