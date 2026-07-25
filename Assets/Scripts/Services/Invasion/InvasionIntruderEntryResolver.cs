using System.Linq;
using UnityEngine;

public static class InvasionIntruderEntryResolver
{
    public static bool TryResolve(
        CharacterSpawner spawner,
        Grid grid,
        out InvasionIntruderEntry entry)
    {
        if (spawner != null && spawner.TryGetEntryGridPosition(out Vector2Int entryGridPosition))
        {
            entry = new InvasionIntruderEntry(
                entryGridPosition,
                spawner.GetOutsideSpawnWorldPosition(),
                spawner.GetEntryDoorWorldPosition());
            return true;
        }

        if (grid != null && TryFindEntrance(grid, out entryGridPosition))
        {
            Vector3 entryDoorPosition = grid.GetWorldPos(entryGridPosition);
            entry = new InvasionIntruderEntry(
                entryGridPosition,
                entryDoorPosition + new Vector3(2f, 0f, 0f),
                entryDoorPosition);
            return true;
        }

        if (grid != null && grid.TryFindNearestWalkablePosition(Vector2Int.zero, out entryGridPosition))
        {
            Vector3 entryDoorPosition = grid.GetWorldPos(entryGridPosition);
            entry = new InvasionIntruderEntry(
                entryGridPosition,
                entryDoorPosition + new Vector3(2f, 0f, 0f),
                entryDoorPosition);
            return true;
        }

        entry = default;
        return false;
    }

    private static bool TryFindEntrance(Grid grid, out Vector2Int entryGridPosition)
    {
        GridCell entranceCell = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.Entrance
                && grid.IsWalkable(cell.Position))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (entranceCell != null)
        {
            entryGridPosition = entranceCell.Position;
            return true;
        }

        entryGridPosition = default;
        return false;
    }
}
