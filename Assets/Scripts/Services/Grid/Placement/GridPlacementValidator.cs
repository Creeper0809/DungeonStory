using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridPlacementValidator
{
    public bool AreInsideGrid(Grid grid, IReadOnlyList<Vector2Int> positions)
    {
        return grid != null
            && positions != null
            && positions.Count > 0
            && positions.All(grid.IsValidGridPos);
    }

    public bool AreInsideHorizontalBounds(Grid grid, IReadOnlyList<Vector2Int> positions, int sidePadding)
    {
        return AreInsideGrid(grid, positions)
            && positions.All((pos) => pos.x >= sidePadding && pos.x < grid.width - sidePadding);
    }

    public bool CanOccupy(Grid grid, GridLayer layer, IReadOnlyList<Vector2Int> positions)
    {
        return AreInsideGrid(grid, positions)
            && positions.All((pos) => grid.GetGridCell(pos).CanOccupy(layer));
    }

    public bool CanBuildInArea(Grid grid, BuildingSO buildingData, IReadOnlyList<Vector2Int> positions)
    {
        return AreInsideGrid(grid, positions)
            && buildingData != null
            && positions.All((pos) => grid.GetGridCell(pos).CanBuildInArea(buildingData));
    }

    public bool HasSupportBelow(Grid grid, IReadOnlyList<Vector2Int> positions)
    {
        if (!AreInsideGrid(grid, positions)) return false;

        int bottomY = positions.Min((pos) => pos.y);
        foreach (Vector2Int pos in positions)
        {
            if (pos.y != bottomY || pos.y == 0) continue;

            GridCell supportCell = grid.GetGridCell(pos + Vector2Int.down);
            if (supportCell == null || !supportCell.HasPlacementSupport())
            {
                return false;
            }
        }

        return true;
    }

    public bool CanRemoveOccupantWithoutUnsupportedAbove(
        Grid grid,
        IReadOnlyList<Vector2Int> positions,
        IGridOccupant occupantToRemove)
    {
        if (!AreInsideGrid(grid, positions)) return false;

        foreach (Vector2Int pos in positions)
        {
            if (pos.y >= grid.height - 1) continue;

            GridCell currentCell = grid.GetGridCell(pos);
            GridCell upperCell = grid.GetGridCell(pos + Vector2Int.up);
            if (currentCell == null || upperCell == null || !upperCell.HasOccupant()) continue;

            bool hasRemainingSupport = HasRemainingPlacementSupport(currentCell, occupantToRemove);
            if (!hasRemainingSupport)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRemainingPlacementSupport(GridCell cell, IGridOccupant occupantToRemove)
    {
        if (cell == null)
        {
            return false;
        }

        IGridOccupant hallway = cell.GetOccupant(GridLayer.Hallway);
        if (hallway != null && !ReferenceEquals(hallway, occupantToRemove))
        {
            return true;
        }

        IGridOccupant building = cell.GetOccupant(GridLayer.Building);
        return building != null && !ReferenceEquals(building, occupantToRemove);
    }
}
