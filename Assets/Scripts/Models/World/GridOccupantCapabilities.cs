public interface IGridBuildingOccupantCapability : IGridOccupant
{
    bool BlocksGridMovement { get; }
    bool AllowsInteriorWalkability { get; }
}

public interface IGridBuildAreaCapability
{
    bool IsDoor { get; }
    bool IsInteriorDoor { get; }
    bool IsStructuralWall { get; }
    GridLayer PlacementLayer { get; }
}
