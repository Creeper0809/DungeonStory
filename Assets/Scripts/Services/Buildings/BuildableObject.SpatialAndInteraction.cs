using System;
using System.Collections.Generic;
using UnityEngine;

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
