using UnityEngine;

public enum GridLayer
{
    Hallway = 0,
    Building = 1,
    Character = 2,
    WallFixture = 3,
    CeilingFixture = 4,
    FloorOverlay = 5,
    Item = 6,
    Wildlife = 7,
    Construction = 8,
    Filth = 9,
    DownedCharacter = 10
}

public enum GridCellAreaType
{
    DungeonInterior = 0,
    Entrance = 1,
    DropZone = 2,
    ExteriorPath = 3,
    BlockedExterior = 4
}

public enum GridCellTerrainType
{
    Dry = 0,
    ShallowWater = 1,
    DeepWater = 2
}

public enum GridMoveType
{
    Walk = 0,
    Instant = 1,
    Stair = 2,
    Elevator = 3,
    Teleport = 4
}

public interface IGridOccupant
{
    int GridId { get; }
    bool IsGridDestroyed { get; }
    bool IsGridVisitable { get; }
    bool IsGridMovement { get; }
}

public interface IGridMovementOccupant
{
    GridMoveType GridMoveType { get; }
}

public sealed class GridTraversalLink
{
    public Vector2Int To { get; }
    public IGridOccupant Through { get; }
    public GridMoveType MoveType { get; }

    public GridTraversalLink(
        Vector2Int to,
        IGridOccupant through,
        GridMoveType moveType)
    {
        To = to;
        Through = through;
        MoveType = moveType;
    }
}

public readonly struct GridMoveStep
{
    public Vector2Int From { get; }
    public Vector2Int To { get; }
    public IGridOccupant DestinationOccupant { get; }
    public IGridOccupant MovementOccupant { get; }
    public GridMoveType MoveType { get; }

    public bool IsValid { get; }
    public bool IsSpecialMove => MoveType != GridMoveType.Walk;

    public GridMoveStep(
        Vector2Int from,
        Vector2Int to,
        IGridOccupant destinationOccupant,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        From = from;
        To = to;
        DestinationOccupant = destinationOccupant;
        MovementOccupant = movementOccupant;
        MoveType = moveType;
        IsValid = true;
    }

    public GridMoveStep WithDestination(IGridOccupant destination)
    {
        return new GridMoveStep(
            From,
            To,
            destination,
            MovementOccupant,
            MoveType);
    }
}
