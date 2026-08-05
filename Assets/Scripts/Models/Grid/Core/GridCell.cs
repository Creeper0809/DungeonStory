using System;
using System.Collections.Generic;
using UnityEngine;

public class GridCell
{
    private static readonly IReadOnlyList<GridTraversalLink> EmptyTraversalLinks =
        Array.Empty<GridTraversalLink>();

    private static readonly GridLayer[] SelectionPriority =
    {
        GridLayer.Character,
        GridLayer.DownedCharacter,
        GridLayer.Wildlife,
        GridLayer.Item,
        GridLayer.Construction,
        GridLayer.Conveyor,
        GridLayer.Utility,
        GridLayer.Filth,
        GridLayer.Building,
        GridLayer.WallFixture,
        GridLayer.CeilingFixture,
        GridLayer.FloorOverlay,
        GridLayer.Hallway
    };

    private Dictionary<GridLayer, IGridOccupant> occupants;
    private List<IGridOccupant> utilityOccupants;
    private List<GridTraversalLink> traversalLinks;
    private IReadOnlyList<GridTraversalLink> traversalLinksView;
    private bool isBuildable;

    public Vector2Int Position { get; }
    public GridCellAreaType AreaType { get; private set; }
    public GridCellTerrainType TerrainType { get; private set; }
    public IReadOnlyList<GridTraversalLink> TraversalLinks =>
        traversalLinksView ?? EmptyTraversalLinks;
    public bool IsWalkableArea => GridCellAreaRules.IsWalkableArea(AreaType)
        && TerrainType != GridCellTerrainType.DeepWater;
    public float TerrainMoveSpeedMultiplier => TerrainType == GridCellTerrainType.ShallowWater ? 0.65f : 1f;
    public bool IsBuildableArea => GridCellAreaRules.IsBuildableArea(AreaType);
    public bool AllowsItemDrop => GridCellAreaRules.AllowsItemDrop(AreaType);

    public GridCell(Vector2Int pos)
    {
        isBuildable = true;
        AreaType = GridCellAreaType.DungeonInterior;
        TerrainType = GridCellTerrainType.Dry;
        Position = pos;
    }
    public IGridOccupant GetOccupant(GridLayer layer = GridLayer.Building)
    {
        if (layer == GridLayer.Utility)
        {
            return utilityOccupants != null && utilityOccupants.Count > 0
                ? utilityOccupants[utilityOccupants.Count - 1]
                : null;
        }

        return occupants != null && occupants.TryGetValue(layer, out IGridOccupant occupant)
            ? occupant
            : null;
    }
    public IGridOccupant GetTopOccupant()
    {
        if ((occupants == null || occupants.Count == 0)
            && (utilityOccupants == null || utilityOccupants.Count == 0))
        {
            return null;
        }

        foreach (GridLayer layer in SelectionPriority)
        {
            if (layer == GridLayer.Utility
                && utilityOccupants != null
                && utilityOccupants.Count > 0)
            {
                return utilityOccupants[utilityOccupants.Count - 1];
            }

            if (occupants != null
                && occupants.TryGetValue(layer, out IGridOccupant occupant))
            {
                return occupant;
            }
        }

        return null;
    }
    public void ConnectFloor(IEnumerable<Vector2Int> poses)
    {
        ClearTraversalLinks();
        if (poses == null)
        {
            return;
        }

        IGridOccupant topOccupant = GetTopOccupant();
        foreach (Vector2Int pos in poses)
        {
            if (pos != Position)
            {
                EnsureTraversalLinks();
                traversalLinks.Add(new GridTraversalLink(pos, topOccupant, GridMoveType.Instant));
            }
        }
    }
    public void SetTraversalLinks(IEnumerable<GridTraversalLink> links)
    {
        ClearTraversalLinks();
        if (links == null)
        {
            return;
        }

        foreach (GridTraversalLink link in links)
        {
            if (link != null)
            {
                EnsureTraversalLinks();
                traversalLinks.Add(link);
            }
        }
    }
    public void RemoveOccupantByLayer(GridLayer layer)
    {
        if (layer == GridLayer.Utility)
        {
            utilityOccupants = null;
            return;
        }

        if (occupants == null || !occupants.Remove(layer)) return;
        if (occupants.Count == 0)
        {
            occupants = null;
        }
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

        if (occupants != null)
        {
            foreach (IGridOccupant building in occupants.Values)
            {
                result.Add(building);
            }
        }

        if (utilityOccupants != null)
        {
            result.AddRange(utilityOccupants);
        }
    }

    public void FillOccupantsInLayer(
        GridLayer layer,
        List<IGridOccupant> result)
    {
        if (result == null)
        {
            return;
        }

        if (layer == GridLayer.Utility)
        {
            if (utilityOccupants != null)
            {
                result.AddRange(utilityOccupants);
            }
            return;
        }

        IGridOccupant occupant = GetOccupant(layer);
        if (occupant != null)
        {
            result.Add(occupant);
        }
    }
    public bool ContainsOccupant(IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return false;
        }

        if (occupants != null)
        {
            foreach (IGridOccupant candidate in occupants.Values)
            {
                if (candidate == occupant)
                {
                    return true;
                }
            }
        }

        return utilityOccupants != null
            && utilityOccupants.Contains(occupant);
    }

    public bool ContainsOccupant(
        GridLayer layer,
        IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return false;
        }

        return layer == GridLayer.Utility
            ? utilityOccupants != null
                && utilityOccupants.Contains(occupant)
            : ReferenceEquals(GetOccupant(layer), occupant);
    }

    public bool RemoveOccupant(
        GridLayer layer,
        IGridOccupant occupant)
    {
        if (!ContainsOccupant(layer, occupant))
        {
            return false;
        }

        if (layer == GridLayer.Utility)
        {
            utilityOccupants.Remove(occupant);
            if (utilityOccupants.Count == 0)
            {
                utilityOccupants = null;
            }
            return true;
        }

        RemoveOccupantByLayer(layer);
        return true;
    }
    public bool CanOccupy(GridLayer layer = GridLayer.Building)
    {
        return GridCellAreaRules.AllowsLayer(AreaType, layer)
            && (layer == GridLayer.Utility || !HasOccupantInLayer(layer))
            && isBuildable;
    }

    public bool CanBuildInArea(IGridBuildAreaCapability building)
    {
        return GridCellAreaRules.CanBuildInArea(AreaType, building);
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
        if (layer == GridLayer.Utility)
        {
            return utilityOccupants != null && utilityOccupants.Count > 0;
        }

        return occupants != null && occupants.ContainsKey(layer);
    }
    public bool HasOccupant()
    {
        return occupants != null && occupants.Count > 0
            || utilityOccupants != null && utilityOccupants.Count > 0;
    }
    public bool HasPlacementSupport()
    {
        return HasOccupantInLayer(GridLayer.Hallway)
            || HasOccupantInLayer(GridLayer.Building);
    }
    public bool TrySetOccupant(GridLayer layer, IGridOccupant occupant)
    {
        if (occupant == null || !CanOccupy(layer)) return false;

        if (layer == GridLayer.Utility)
        {
            utilityOccupants ??= new List<IGridOccupant>(3);
            if (utilityOccupants.Contains(occupant))
            {
                return false;
            }

            utilityOccupants.Add(occupant);
            return true;
        }

        occupants ??= new Dictionary<GridLayer, IGridOccupant>();
        occupants.Add(layer, occupant);
        return true;
    }

    public void SetOccupant(GridLayer layer,IGridOccupant occupant)
    {
        TrySetOccupant(layer, occupant);
    }

    private void EnsureTraversalLinks()
    {
        if (traversalLinks != null)
        {
            return;
        }

        traversalLinks = new List<GridTraversalLink>(2);
        traversalLinksView = ReadOnlyView.List(traversalLinks);
    }

    private void ClearTraversalLinks()
    {
        traversalLinks = null;
        traversalLinksView = null;
    }
}
