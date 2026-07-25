using System;
using System.Collections.Generic;
using UnityEngine;

public class GridCell
{
    private static readonly GridLayer[] SelectionPriority =
    {
        GridLayer.Character,
        GridLayer.DownedCharacter,
        GridLayer.Wildlife,
            GridLayer.Item,
            GridLayer.Construction,
            GridLayer.Filth,
            GridLayer.Building,
        GridLayer.WallFixture,
        GridLayer.CeilingFixture,
            GridLayer.FloorOverlay,
            GridLayer.Hallway
    };

    private readonly Dictionary<GridLayer, IGridOccupant> occupants;
    private List<GridTraversalLink> traversalLinks;
    private readonly IReadOnlyList<GridTraversalLink> traversalLinksView;
    private bool isBuildable;

    public Vector2Int Position { get; }
    public GridCellAreaType AreaType { get; private set; }
    public GridCellTerrainType TerrainType { get; private set; }
    public IReadOnlyList<GridTraversalLink> TraversalLinks => traversalLinksView;
    public bool IsWalkableArea => GridCellAreaRules.IsWalkableArea(AreaType)
        && TerrainType != GridCellTerrainType.DeepWater;
    public float TerrainMoveSpeedMultiplier => TerrainType == GridCellTerrainType.ShallowWater ? 0.65f : 1f;
    public bool IsBuildableArea => GridCellAreaRules.IsBuildableArea(AreaType);
    public bool AllowsItemDrop => GridCellAreaRules.AllowsItemDrop(AreaType);

    public GridCell(Vector2Int pos)
    {
        occupants = new Dictionary<GridLayer, IGridOccupant>();
        traversalLinks = new List<GridTraversalLink>();
        traversalLinksView = ReadOnlyView.List(traversalLinks);
        isBuildable = true;
        AreaType = GridCellAreaType.DungeonInterior;
        TerrainType = GridCellTerrainType.Dry;
        Position = pos;
    }
    public IGridOccupant GetOccupant(GridLayer layer = GridLayer.Building)
    {
        if(!HasOccupantInLayer(layer)) return null;
        return occupants[layer];
    }
    public IGridOccupant GetTopOccupant()
    {
        if (occupants.Count == 0) return null;

        foreach (GridLayer layer in SelectionPriority)
        {
            if (occupants.TryGetValue(layer, out IGridOccupant occupant))
            {
                return occupant;
            }
        }

        return null;
    }
    public void ConnectFloor(IEnumerable<Vector2Int> poses)
    {
        traversalLinks.Clear();
        if (poses == null)
        {
            return;
        }

        IGridOccupant topOccupant = GetTopOccupant();
        foreach (Vector2Int pos in poses)
        {
            if (pos != Position)
            {
                traversalLinks.Add(new GridTraversalLink(pos, topOccupant, GridMoveType.Instant));
            }
        }
    }
    public void SetTraversalLinks(IEnumerable<GridTraversalLink> links)
    {
        traversalLinks.Clear();
        if (links == null)
        {
            return;
        }

        foreach (GridTraversalLink link in links)
        {
            if (link != null)
            {
                traversalLinks.Add(link);
            }
        }
    }
    public void RemoveOccupantByLayer(GridLayer layer)
    {
        if (!HasOccupantInLayer(layer)) return;
        occupants.Remove(layer);
    }
    public List<IGridOccupant> GetAllOccupants()
    {
        List<IGridOccupant> result = new List<IGridOccupant>();
        FillAllOccupants(result);
        return result;
    }
    public void FillAllOccupants(List<IGridOccupant> result)
    {
        if (result == null)
        {
            return;
        }

        foreach (IGridOccupant building in occupants.Values)
        {
            result.Add(building);
        }
    }
    public bool ContainsOccupant(IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return false;
        }

        foreach (IGridOccupant candidate in occupants.Values)
        {
            if (candidate == occupant)
            {
                return true;
            }
        }

        return false;
    }
    public bool CanOccupy(GridLayer layer = GridLayer.Building)
    {
        return GridCellAreaRules.AllowsLayer(AreaType, layer)
            && !HasOccupantInLayer(layer)
            && isBuildable;
    }

    public bool CanBuildInArea(BuildingSO buildingData)
    {
        return GridCellAreaRules.CanBuildInArea(AreaType, buildingData);
    }

    public bool SetAreaType(GridCellAreaType areaType)
    {
        if (!Enum.IsDefined(typeof(GridCellAreaType), areaType))
        {
            areaType = GridCellAreaType.DungeonInterior;
        }

        if (AreaType == areaType)
        {
            return false;
        }

        AreaType = areaType;
        return true;
    }
    public bool SetTerrainType(GridCellTerrainType terrainType)
    {
        if (!Enum.IsDefined(typeof(GridCellTerrainType), terrainType))
        {
            terrainType = GridCellTerrainType.Dry;
        }

        if (TerrainType == terrainType)
        {
            return false;
        }

        TerrainType = terrainType;
        return true;
    }
    public bool HasOccupantInLayer(GridLayer layer = GridLayer.Building)
    {
        return occupants.ContainsKey(layer);
    }
    public bool HasOccupant()
    {
        return occupants.Count > 0;
    }
    public bool HasPlacementSupport()
    {
        return HasOccupantInLayer(GridLayer.Hallway)
            || HasOccupantInLayer(GridLayer.Building);
    }
    public bool TrySetOccupant(GridLayer layer, IGridOccupant occupant)
    {
        if (occupant == null || !CanOccupy(layer)) return false;

        occupants.Add(layer, occupant);
        return true;
    }

    public void SetOccupant(GridLayer layer,IGridOccupant occupant)
    {
        TrySetOccupant(layer, occupant);
    }

}
