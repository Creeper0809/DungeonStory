using System;
using System.Linq;
using UnityEngine;

public readonly struct WorldGridEntryPoint
{
    public WorldGridEntryPoint(Vector2Int gridPosition, Vector3 outsidePosition, Vector3 doorPosition)
    {
        GridPosition = gridPosition;
        OutsidePosition = outsidePosition;
        DoorPosition = doorPosition;
    }

    public Vector2Int GridPosition { get; }
    public Vector3 OutsidePosition { get; }
    public Vector3 DoorPosition { get; }
}

public interface IWorldDropZoneQuery
{
    bool TryGetDeliveryDropoff(out Vector2Int position);
    bool TryGetExpeditionLootDropoff(out Vector2Int position);
    bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint);
}

public sealed class WorldDropZoneQuery : IWorldDropZoneQuery
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;

    public WorldDropZoneQuery(
        IGridSystemProvider gridSystemProvider,
        ICharacterSpawnerProvider characterSpawnerProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.characterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
    }

    public bool TryGetDeliveryDropoff(out Vector2Int position)
    {
        return TryFindPreferredDropoff(out position);
    }

    public bool TryGetExpeditionLootDropoff(out Vector2Int position)
    {
        return TryFindPreferredDropoff(out position);
    }

    public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            entryPoint = default;
            return false;
        }

        CharacterSpawner spawner = null;
        characterSpawnerProvider.TryGetSpawner(out spawner);
        Vector2Int entryGridPosition;
        if (!TryResolveEntranceGridPosition(grid, out entryGridPosition)
            && (spawner == null || !spawner.TryGetEntryGridPosition(out entryGridPosition)))
        {
            if (!grid.TryFindNearestWalkablePosition(Vector2Int.zero, out entryGridPosition))
            {
                entryPoint = default;
                return false;
            }
        }

        Vector3 doorPosition = spawner != null
            ? spawner.GetEntryDoorWorldPosition()
            : grid.GetWorldPos(entryGridPosition);
        Vector3 outsidePosition = spawner != null
            ? spawner.GetOutsideSpawnWorldPosition()
            : doorPosition + new Vector3(2f, 0f, 0f);

        entryPoint = new WorldGridEntryPoint(entryGridPosition, outsidePosition, doorPosition);
        return true;
    }

    private bool TryFindPreferredDropoff(out Vector2Int position)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            position = default;
            return false;
        }

        Vector2Int entrance = default;
        bool hasEntrance = TryResolveEntranceGridPosition(grid, out entrance);
        GridCell dropCell = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DropZone
                && cell.AllowsItemDrop
                && grid.IsWalkable(cell.Position))
            .OrderBy(cell => hasEntrance
                ? Mathf.Abs(cell.Position.x - entrance.x) + Mathf.Abs(cell.Position.y - entrance.y)
                : cell.Position.y)
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (dropCell != null)
        {
            position = dropCell.Position;
            return true;
        }

        if (hasEntrance)
        {
            GridCell entranceCell = grid.GetGridCell(entrance);
            if (entranceCell != null && entranceCell.AllowsItemDrop && grid.IsWalkable(entrance))
            {
                position = entrance;
                return true;
            }
        }

        if (characterSpawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            && spawner.TryGetEntryGridPosition(out Vector2Int spawnerEntry))
        {
            GridCell spawnerCell = grid.GetGridCell(spawnerEntry);
            if (spawnerCell != null && spawnerCell.AllowsItemDrop && grid.IsWalkable(spawnerEntry))
            {
                position = spawnerEntry;
                return true;
            }
        }

        GridCell fallback = grid.GetCells()
            .Where(cell => cell != null && cell.AllowsItemDrop && grid.IsWalkable(cell.Position))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (fallback != null)
        {
            position = fallback.Position;
            return true;
        }

        position = default;
        return false;
    }

    private bool TryResolveEntranceGridPosition(Grid grid, out Vector2Int position)
    {
        if (gridSystemProvider.TryGetManager(out GridSystemManager manager)
            && manager.TryGetEntranceGridPosition(out position))
        {
            return true;
        }

        GridCell entranceCell = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.Entrance
                && grid.IsWalkable(cell.Position))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (entranceCell != null)
        {
            position = entranceCell.Position;
            return true;
        }

        position = default;
        return false;
    }
}
