public static class GridCellAreaRules
{
    public static bool IsWalkableArea(GridCellAreaType areaType)
    {
        return areaType != GridCellAreaType.BlockedExterior;
    }

    public static bool IsBuildableArea(GridCellAreaType areaType)
    {
        return areaType == GridCellAreaType.DungeonInterior;
    }

    public static bool AllowsItemDrop(GridCellAreaType areaType)
    {
        return areaType != GridCellAreaType.BlockedExterior;
    }

    public static bool AllowsLayer(GridCellAreaType areaType, GridLayer layer)
    {
        if (!IsWalkableArea(areaType))
        {
            return false;
        }

        if (areaType == GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            return layer == GridLayer.Hallway
                || layer == GridLayer.Character
                || layer == GridLayer.Wildlife
                || layer == GridLayer.Item
                || layer == GridLayer.Construction
                || layer == GridLayer.Building
                || layer == GridLayer.WallFixture
                || layer == GridLayer.CeilingFixture
                || layer == GridLayer.FloorOverlay
                || layer == GridLayer.Filth
                || layer == GridLayer.Utility
                || layer == GridLayer.Conveyor
                || layer == GridLayer.DownedCharacter;
        }

        return layer == GridLayer.Hallway
            || layer == GridLayer.Building
            || layer == GridLayer.WallFixture
            || layer == GridLayer.CeilingFixture
            || layer == GridLayer.FloorOverlay
            || layer == GridLayer.Character
            || layer == GridLayer.Wildlife
            || layer == GridLayer.Item
            || layer == GridLayer.Construction
            || layer == GridLayer.Filth
            || layer == GridLayer.Utility
            || layer == GridLayer.Conveyor
            || layer == GridLayer.DownedCharacter;
    }

    public static bool CanBuildInArea(
        GridCellAreaType areaType,
        IGridBuildAreaCapability building)
    {
        if (building == null || !IsWalkableArea(areaType))
        {
            return false;
        }

        if (areaType == GridCellAreaType.DungeonInterior)
        {
            return true;
        }

        if (building.IsDoor && !building.IsInteriorDoor)
        {
            return true;
        }

        if (building.PlacementLayer == GridLayer.Hallway)
        {
            return areaType == GridCellAreaType.Entrance
                || areaType == GridCellAreaType.DropZone
                || areaType == GridCellAreaType.ExteriorPath;
        }

        if (building.PlacementLayer == GridLayer.Utility
            || building.PlacementLayer == GridLayer.Conveyor)
        {
            return areaType == GridCellAreaType.Entrance
                || areaType == GridCellAreaType.DropZone
                || areaType == GridCellAreaType.ExteriorPath;
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            return building.IsDoor || building.IsStructuralWall;
        }

        return false;
    }
}

