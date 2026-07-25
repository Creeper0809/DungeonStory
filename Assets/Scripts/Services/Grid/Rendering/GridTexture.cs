using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DrawWithUnity]
public class GridTexture : SerializedMonoBehaviour, IGridBuildingVisual
{
    public enum TilemapLayer
    {
        HALLWAY,
        FRONT,
        MIDDLE,
        BACK
    }
    public Tilemap wallTilemap;
    public Dictionary<TilemapLayer, Tilemap> buildingTilemapEven;
    public Dictionary<TilemapLayer, Tilemap> buildingTilemapOdd;
    public Tile wall;
    public Tile floor;
    [SerializeField] private bool hideHallwayUnderBuildings = false;
    [SerializeField] private string hallwaySortingLayerName = "DungeonHallway";
    [SerializeField] private int hallwaySortingOrder = 100;
    [SerializeField] private Tilemap ceilingOverlayTilemap;
    [SerializeField] private string ceilingOverlaySortingLayerName = "Wall";
    [SerializeField] private int ceilingOverlaySortingOrder = 110;
    private readonly GridWallTileCalculator wallTileCalculator = new GridWallTileCalculator();
    private readonly Dictionary<SpriteTileKey, Tile> spriteTiles = new Dictionary<SpriteTileKey, Tile>();

    private void Awake()
    {
        ApplyTilemapSorting();
    }

    private void OnValidate()
    {
        ApplyTilemapSorting();
    }

    public void DrawBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)
    {
        if (tiles == null || tiles.Count == 0) return;

        foreach(var tile in tiles)
        {
            if (tile.Value == null || !TryGetTilemap(tile.Key, isEven, out Tilemap targetTilemap)) continue;

            targetTilemap.SetTile(GetTilePosition(selectPos),tile.Value);
        }
    }

    public void DrawBuilding(BuildingSO buildingData, Vector2Int selectPos)
    {
        if (buildingData == null) return;

        if (buildingData.UsesIndependentRenderer)
        {
            return;
        }

        if (buildingData.IsStructuralWall)
        {
            SetWallCell(selectPos, wall);
            return;
        }

        if (HasTileVisual(buildingData))
        {
            DrawBuilding(buildingData.tiles, selectPos, buildingData.IsEvenWidth);
            ClearHallwayUnderDungeonDoor(buildingData, selectPos);
            return;
        }

        DrawSpriteBuilding(buildingData, selectPos);
        ClearHallwayUnderDungeonDoor(buildingData, selectPos);
    }

    public void DeleteBuilding(Dictionary<TilemapLayer, Tile> tiles,Vector2Int selectPos,bool isEven)
    {
        if (tiles == null || tiles.Count == 0) return;

        foreach(var key in tiles.Keys)
        {
            if (!TryGetTilemap(key, isEven, out Tilemap targetTilemap)) continue;

            targetTilemap.SetTile(GetTilePosition(selectPos),null);
        }
    }

    public void DeleteBuilding(BuildingSO buildingData, Vector2Int selectPos)
    {
        if (buildingData == null) return;

        if (buildingData.UsesIndependentRenderer)
        {
            return;
        }

        if (buildingData.IsStructuralWall)
        {
            SetWallCell(selectPos, null);
            return;
        }

        if (HasTileVisual(buildingData))
        {
            DeleteBuilding(buildingData.tiles, selectPos, buildingData.IsEvenWidth);
            return;
        }

        DeleteSpriteBuilding(buildingData, selectPos);
    }

    private void DrawSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)
    {
        if (buildingData.sprite == null) return;
        if (!TryGetTilemap(GetSpriteTilemapLayer(buildingData), buildingData.IsEvenWidth, out Tilemap targetTilemap)) return;

        targetTilemap.SetTile(GetTilePosition(selectPos), GetSpriteTile(buildingData, targetTilemap));
    }

    private void DeleteSpriteBuilding(BuildingSO buildingData, Vector2Int selectPos)
    {
        if (buildingData.sprite == null) return;
        if (!TryGetTilemap(GetSpriteTilemapLayer(buildingData), buildingData.IsEvenWidth, out Tilemap targetTilemap)) return;

        targetTilemap.SetTile(GetTilePosition(selectPos), null);
    }

    private Tile GetSpriteTile(BuildingSO buildingData, Tilemap targetTilemap)
    {
        Sprite sprite = buildingData.sprite;
        int cellTileHeight = GridBuildingTileTransformCalculator.DefaultCellTileHeight;
        Vector2 tileAnchor = targetTilemap != null
            ? (Vector2)targetTilemap.tileAnchor
            : GridBuildingTileTransformCalculator.DefaultTileAnchor;
        SpriteTileKey key = new SpriteTileKey(
            sprite,
            buildingData.width,
            buildingData.height,
            cellTileHeight,
            tileAnchor,
            targetTilemap.transform.localPosition);
        if (!spriteTiles.TryGetValue(key, out Tile tile) || tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"{sprite.name}_{buildingData.width}x{buildingData.height}_RuntimeTile";
            tile.sprite = sprite;
            tile.transform = GridBuildingTileTransformCalculator.Calculate(
                buildingData,
                cellTileHeight,
                tileAnchor,
                targetTilemap.transform.localPosition);
            spriteTiles[key] = tile;
        }

        return tile;
    }

    private bool TryGetTilemap(TilemapLayer layer, bool isEven, out Tilemap targetTilemap)
    {
        targetTilemap = null;
        Dictionary<TilemapLayer, Tilemap> tilemaps = isEven ? buildingTilemapEven : buildingTilemapOdd;
        return tilemaps != null
            && tilemaps.TryGetValue(layer, out targetTilemap)
            && targetTilemap != null;
    }

    private static bool HasTileVisual(BuildingSO buildingData)
    {
        return buildingData.tiles != null && buildingData.tiles.Count > 0;
    }

    private static TilemapLayer GetSpriteTilemapLayer(BuildingSO buildingData)
    {
        GridBuildingPlacement placement = buildingData.Placement;
        return placement.Layer == GridLayer.Hallway || placement.IsMovement
            ? TilemapLayer.HALLWAY
            : TilemapLayer.BACK;
    }

    private static Vector3Int GetTilePosition(Vector2Int selectPos)
    {
        return new Vector3Int(-selectPos.x, 3 * selectPos.y, 0);
    }

    private void SetWallCell(Vector2Int selectPos, Tile tile)
    {
        if (wallTilemap == null) return;

        Vector3Int basePosition = GetTilePosition(selectPos);
        for (int y = 0; y < 3; y++)
        {
            Vector3Int tilePos = basePosition + Vector3Int.up * y;
            if (tile == null)
            {
                ResetWallTileTransform(tilePos);
                wallTilemap.SetTile(tilePos, null);
                continue;
            }

            wallTilemap.SetTile(tilePos, tile);
            ResetWallTileTransform(tilePos);
        }
    }

    private readonly struct SpriteTileKey : System.IEquatable<SpriteTileKey>
    {
        private readonly Sprite sprite;
        private readonly int width;
        private readonly int height;
        private readonly int cellTileHeight;
        private readonly Vector2 tileAnchor;
        private readonly Vector2 tilemapLocalOffset;

        public SpriteTileKey(Sprite sprite, int width, int height, int cellTileHeight, Vector2 tileAnchor, Vector2 tilemapLocalOffset)
        {
            this.sprite = sprite;
            this.width = width;
            this.height = height;
            this.cellTileHeight = cellTileHeight;
            this.tileAnchor = tileAnchor;
            this.tilemapLocalOffset = tilemapLocalOffset;
        }

        public bool Equals(SpriteTileKey other)
        {
            return sprite == other.sprite
                && width == other.width
                && height == other.height
                && cellTileHeight == other.cellTileHeight
                && tileAnchor == other.tileAnchor
                && tilemapLocalOffset == other.tilemapLocalOffset;
        }

        public override bool Equals(object obj)
        {
            return obj is SpriteTileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = sprite != null ? sprite.GetHashCode() : 0;
                hash = (hash * 397) ^ width;
                hash = (hash * 397) ^ height;
                hash = (hash * 397) ^ cellTileHeight;
                hash = (hash * 397) ^ tileAnchor.GetHashCode();
                hash = (hash * 397) ^ tilemapLocalOffset.GetHashCode();
                return hash;
            }
        }
    }

    public void DrawWall(Grid grid)
    {
        if (grid == null || wallTilemap == null) return;

        int gridHeight = grid.height;
        int gridWidth = grid.width;

        // 변경된 타일 위치를 추적하기 위한 임시 집합
        HashSet<Vector3Int> updatedTiles = new HashSet<Vector3Int>();

        HashSet<Vector2Int> poses = wallTileCalculator.GetWallTilePositions(grid);

        // 변경된 타일만 업데이트하기 위한 임시 타일맵 생성
        var tempTilemap = new HashSet<Vector3Int>();

        // 현재 벽 타일맵에서 모든 타일 위치를 임시 타일맵에 추가
        foreach (var pos in wallTilemap.cellBounds.allPositionsWithin)
        {
            if (wallTilemap.HasTile(pos) && wallTilemap.GetTile(pos) == wall)
            {
                tempTilemap.Add(pos);
            }
        }

        // 새로운 벽 위치 설정
        foreach (Vector2Int pos in poses)
        {
            Vector3Int tilePos = new Vector3Int(-pos.x, pos.y, 0);
            if (!tempTilemap.Contains(tilePos))
            {
                wallTilemap.SetTile(tilePos, wall);
            }
            ResetWallTileTransform(tilePos);
            tempTilemap.Remove(tilePos);
        }

        // 기존에 있던 타일 중에서 더 이상 필요 없는 타일 제거
        foreach (var pos in tempTilemap)
        {
            ResetWallTileTransform(pos);
            wallTilemap.SetTile(pos, null);
        }

        // 바닥 타일 업데이트 최적화
        for (int i = 0; i < gridHeight; i++)
        {
            for (int j = 0; j < gridWidth; j++)
            {
                Vector2Int gridPos = new Vector2Int(j, i);
                Vector3Int tilePos = new Vector3Int(-j, i*3 + 2, 0);
                // 이전에 업데이트된 타일은 건너뜀
                if (updatedTiles.Contains(tilePos)) continue;

                GridCell cell = grid.GetGridCell(gridPos);
                if (GridWallTileCalculator.IsWallVisualCell(cell))
                {
                    wallTilemap.SetTile(tilePos, wall);
                    ResetWallTileTransform(tilePos);
                    updatedTiles.Add(tilePos);
                    continue;
                }

                if (cell.HasOccupant())
                {
                    ResetWallTileTransform(tilePos);
                    wallTilemap.SetTile(tilePos, floor);
                    // 업데이트된 타일 집합에 추가
                    updatedTiles.Add(tilePos);
                }
                else if (wallTilemap.GetTile(tilePos) == floor)
                {
                    // 바닥 타일이지만 현재 건물이 없는 경우, 타일 제거
                    ResetWallTileTransform(tilePos);
                    wallTilemap.SetTile(tilePos, null);
                    updatedTiles.Add(tilePos);
                }
            }
        }

        SynchronizeHallwayVisuals(grid);
        SynchronizeCeilingOverlay(grid);
    }

    private void SynchronizeHallwayVisuals(Grid grid)
    {
        if (grid == null) return;

        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                GridCell cell = grid.GetGridCell(gridPos);
                BuildableObject hallway = cell?.GetOccupant(GridLayer.Hallway) as BuildableObject;
                BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
                if (!hideHallwayUnderBuildings && !IsDungeonDoor(building))
                {
                    continue;
                }

                bool shouldShowHallway = hallway != null
                    && hallway.BuildingData != null
                    && (building == null || CanShareHallwayVisual(building));

                ClearHallwayTile(gridPos);
                if (shouldShowHallway)
                {
                    DrawBuilding(hallway.BuildingData, gridPos);
                }
            }
        }
    }

    private void ClearHallwayTile(Vector2Int gridPos)
    {
        Vector3Int tilePos = GetTilePosition(gridPos);
        ClearTile(TilemapLayer.HALLWAY, false, tilePos);
        ClearTile(TilemapLayer.HALLWAY, true, tilePos);
    }

    private void ClearHallwayUnderDungeonDoor(BuildingSO buildingData, Vector2Int centerPos)
    {
        if (buildingData == null || !buildingData.IsDoor || buildingData.IsInteriorDoor)
        {
            return;
        }

        foreach (Vector2Int gridPos in buildingData.GetGridPosList(centerPos))
        {
            ClearHallwayTile(gridPos);
        }
    }

    private static bool CanShareHallwayVisual(BuildableObject building)
    {
        if (building == null)
        {
            return false;
        }

        BuildingSO buildingData = building.BuildingData;
        bool isDoor = building is Door || (buildingData != null && buildingData.IsDoor);
        if (isDoor)
        {
            return building is InteriorDoor
                || (buildingData != null && buildingData.IsInteriorDoor);
        }

        return building.IsGridMovement;
    }

    private static bool IsDungeonDoor(BuildableObject building)
    {
        if (building == null)
        {
            return false;
        }

        BuildingSO buildingData = building.BuildingData;
        bool isDoor = building is Door || (buildingData != null && buildingData.IsDoor);
        bool isInteriorDoor = building is InteriorDoor
            || (buildingData != null && buildingData.IsInteriorDoor);
        return isDoor && !isInteriorDoor;
    }

    private void ClearTile(TilemapLayer layer, bool isEven, Vector3Int tilePos)
    {
        if (TryGetTilemap(layer, isEven, out Tilemap targetTilemap))
        {
            targetTilemap.SetTile(tilePos, null);
        }
    }

    private void ApplyHallwaySorting()
    {
        HashSet<Tilemap> configured = new HashSet<Tilemap>();
        ConfigureHallwayTilemap(buildingTilemapEven, configured);
        ConfigureHallwayTilemap(buildingTilemapOdd, configured);
    }

    private void ApplyTilemapSorting()
    {
        ApplyHallwaySorting();
        ConfigureCeilingOverlayTilemap();
    }

    private void ConfigureHallwayTilemap(Dictionary<TilemapLayer, Tilemap> tilemaps, HashSet<Tilemap> configured)
    {
        if (tilemaps == null
            || !tilemaps.TryGetValue(TilemapLayer.HALLWAY, out Tilemap hallwayTilemap)
            || hallwayTilemap == null
            || configured == null
            || !configured.Add(hallwayTilemap))
        {
            return;
        }

        TilemapRenderer renderer = hallwayTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(hallwaySortingLayerName))
        {
            renderer.sortingLayerName = hallwaySortingLayerName;
        }

        renderer.sortingOrder = hallwaySortingOrder;
    }

    private void ConfigureCeilingOverlayTilemap()
    {
        if (ceilingOverlayTilemap == null)
        {
            return;
        }

        TilemapRenderer renderer = ceilingOverlayTilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(ceilingOverlaySortingLayerName))
        {
            renderer.sortingLayerName = ceilingOverlaySortingLayerName;
        }

        int wallOrder = GetWallTilemapSortingOrder(renderer.sortingLayerName);
        int sortingOrder = Mathf.Max(ceilingOverlaySortingOrder, wallOrder + 1);
        sortingOrder = Mathf.Max(sortingOrder, InteriorDoorVisualLayout.SortingOrder + 1);
        sortingOrder = Mathf.Max(sortingOrder, DungeonDoorVisualLayout.CeilingSortingOrder + 1);
        renderer.sortingOrder = sortingOrder;
    }

    private void EnsureCeilingOverlayTilemap()
    {
        if (ceilingOverlayTilemap != null || wallTilemap == null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("CeilingOverlay", typeof(Tilemap), typeof(TilemapRenderer));
        Transform wallTransform = wallTilemap.transform;
        Transform parent = wallTransform.parent != null ? wallTransform.parent : transform;
        overlayObject.transform.SetParent(parent, false);
        overlayObject.transform.localPosition = wallTransform.localPosition;
        overlayObject.transform.localRotation = wallTransform.localRotation;
        overlayObject.transform.localScale = wallTransform.localScale;

        ceilingOverlayTilemap = overlayObject.GetComponent<Tilemap>();
        ceilingOverlayTilemap.color = wallTilemap.color;
        ceilingOverlayTilemap.tileAnchor = wallTilemap.tileAnchor;
        ceilingOverlayTilemap.orientation = wallTilemap.orientation;
    }

    private int GetWallTilemapSortingOrder(string targetSortingLayer)
    {
        TilemapRenderer wallRenderer = wallTilemap != null
            ? wallTilemap.GetComponent<TilemapRenderer>()
            : null;
        if (wallRenderer == null || wallRenderer.sortingLayerName != targetSortingLayer)
        {
            return int.MinValue;
        }

        return wallRenderer.sortingOrder;
    }

    private void SynchronizeCeilingOverlay(Grid grid)
    {
        if (grid == null || wallTilemap == null)
        {
            return;
        }

        EnsureCeilingOverlayTilemap();
        if (ceilingOverlayTilemap == null)
        {
            return;
        }

        ConfigureCeilingOverlayTilemap();
        ceilingOverlayTilemap.ClearAllTiles();
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int gridPos = new Vector2Int(x, y);
                GridCell cell = grid.GetGridCell(gridPos);
                if (!ShouldDrawCeilingOverlay(grid, gridPos, cell))
                {
                    continue;
                }

                Vector3Int tilePos = new Vector3Int(-x, (y * GridBuildingTileTransformCalculator.DefaultCellTileHeight) + 2, 0);
                TileBase tile = floor != null ? floor : wallTilemap.GetTile(tilePos);
                if (tile == null)
                {
                    continue;
                }

                ceilingOverlayTilemap.SetTile(tilePos, tile);
            }
        }
    }

    private static bool ShouldDrawCeilingOverlay(Grid grid, Vector2Int gridPos, GridCell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (GridWallTileCalculator.IsWallVisualCell(cell))
        {
            return GridWallTileCalculator.IsInteriorWallVisualCell(grid, gridPos);
        }

        return GridWallTileCalculator.HasRoomContentForCeiling(cell);
    }

    private void ResetWallTileTransform(Vector3Int tilePos)
    {
        if (wallTilemap == null)
        {
            return;
        }

        wallTilemap.SetTileFlags(tilePos, TileFlags.None);
        wallTilemap.SetTransformMatrix(tilePos, Matrix4x4.identity);
    }
}

public static class GridBuildingTileTransformCalculator
{
    public const int DefaultCellTileHeight = 3;
    public const float DefaultVerticalVisualInset = 0.5f;
    public static readonly Vector2 DefaultTileAnchor = new Vector2(0.5f, 0.5f);

    public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight = DefaultCellTileHeight)
    {
        return Calculate(buildingData, cellTileHeight, DefaultTileAnchor);
    }

    public static Matrix4x4 Calculate(BuildingSO buildingData, int cellTileHeight, Vector2 tileAnchor)
    {
        return Calculate(buildingData, cellTileHeight, tileAnchor, 0f);
    }

    public static Matrix4x4 Calculate(
        BuildingSO buildingData,
        int cellTileHeight,
        Vector2 tileAnchor,
        float tilemapLocalYOffset)
    {
        return Calculate(
            buildingData,
            cellTileHeight,
            tileAnchor,
            new Vector2(0f, tilemapLocalYOffset));
    }

    public static Matrix4x4 Calculate(
        BuildingSO buildingData,
        int cellTileHeight,
        Vector2 tileAnchor,
        Vector2 tilemapLocalOffset)
    {
        Sprite sprite = buildingData != null ? buildingData.sprite : null;
        if (sprite == null)
        {
            return Matrix4x4.identity;
        }

        Vector2 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return Matrix4x4.identity;
        }

        int safeCellTileHeight = Mathf.Max(1, cellTileHeight);
        Vector2 effectiveTileAnchor = tileAnchor;
        Vector2 footprintSize = new Vector2(
            Mathf.Max(1, buildingData.width),
            Mathf.Max(1, buildingData.height) * safeCellTileHeight);
        Vector2 visualSize = GetVisualFootprintSize(footprintSize);
        Vector3 scale = new Vector3(
            visualSize.x / spriteSize.x,
            visualSize.y / spriteSize.y,
            1f);
        float desiredWorldCenterX = 0.5f + (buildingData.IsEvenWidth ? 0.5f : 0f);
        Vector3 desiredCenter = new Vector3(
            desiredWorldCenterX - effectiveTileAnchor.x - tilemapLocalOffset.x,
            visualSize.y * 0.5f - effectiveTileAnchor.y - tilemapLocalOffset.y,
            0f);
        Vector3 scaledBoundsCenter = new Vector3(
            sprite.bounds.center.x * scale.x,
            sprite.bounds.center.y * scale.y,
            0f);
        Vector3 offset = desiredCenter - scaledBoundsCenter;

        return Matrix4x4.TRS(offset, Quaternion.identity, scale);
    }

    public static Vector2 GetVisualFootprintSize(Vector2 footprintSize)
    {
        float verticalInset = GetVerticalVisualInset(footprintSize.y);
        return new Vector2(
            footprintSize.x,
            Mathf.Max(0.1f, footprintSize.y - verticalInset * 2f));
    }

    private static float GetVerticalVisualInset(float footprintHeight)
    {
        if (footprintHeight <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(DefaultVerticalVisualInset, footprintHeight * 0.25f);
    }
}

public class GridWallTileCalculator
{
    private readonly int cellTileHeight;

    public GridWallTileCalculator(int cellTileHeight = 3)
    {
        this.cellTileHeight = cellTileHeight;
    }

    public HashSet<Vector2Int> GetWallTilePositions(Grid grid)
    {
        HashSet<Vector2Int> wallTilePositions = new HashSet<Vector2Int>();
        if (grid == null) return wallTilePositions;

        foreach (GridCell cell in grid.GetCells())
        {
            if (IsWallVisualCell(cell))
            {
                AddWallCellTiles(wallTilePositions, cell.Position);
                continue;
            }

            if (HasAutomaticSideWallContent(cell)) continue;

            bool hasLeftNeighbor = HasAutomaticSideWallNeighbor(grid, cell.Position + Vector2Int.left);
            bool hasRightNeighbor = HasAutomaticSideWallNeighbor(grid, cell.Position + Vector2Int.right);
            if (hasLeftNeighbor == hasRightNeighbor)
            {
                continue;
            }

            AddWallCellTiles(wallTilePositions, cell.Position);
        }

        return wallTilePositions;
    }

    private static bool HasAutomaticSideWallNeighbor(Grid grid, Vector2Int position)
    {
        if (grid == null || !grid.IsValidGridPos(position))
        {
            return false;
        }

        return HasAutomaticSideWallContent(grid.GetGridCell(position));
    }

    private static bool HasAutomaticSideWallContent(GridCell cell)
    {
        return cell != null
            && !IsWallVisualCell(cell)
            && (cell.HasOccupantInLayer(GridLayer.Building)
                || cell.HasOccupantInLayer(GridLayer.Hallway));
    }

    internal static bool IsStructuralWallCell(GridCell cell)
    {
        BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
        if (building == null)
        {
            return false;
        }

        return building.BuildingData != null
            ? building.BuildingData.IsStructuralWall
            : building.category == BuildingCategory.Wall;
    }

    internal static bool IsWallVisualCell(GridCell cell)
    {
        BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
        if (building == null)
        {
            return false;
        }

        return IsStructuralWallCell(cell)
            || building is InteriorDoor
            || (building.BuildingData != null && building.BuildingData.IsInteriorDoor);
    }

    internal static bool IsInteriorWallVisualCell(Grid grid, Vector2Int cellPosition)
    {
        if (grid == null || !grid.IsValidGridPos(cellPosition))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(cellPosition);
        if (!IsWallVisualCell(cell))
        {
            return false;
        }

        return HasRoomContentNeighborForCeiling(grid, cellPosition + Vector2Int.left)
            && HasRoomContentNeighborForCeiling(grid, cellPosition + Vector2Int.right);
    }

    internal static bool HasRoomContentForCeiling(GridCell cell)
    {
        return cell != null
            && (cell.HasOccupantInLayer(GridLayer.Building)
                || cell.HasOccupantInLayer(GridLayer.Hallway)
                || cell.HasOccupantInLayer(GridLayer.WallFixture)
                || cell.HasOccupantInLayer(GridLayer.CeilingFixture)
                || cell.HasOccupantInLayer(GridLayer.FloorOverlay));
    }

    private static bool HasRoomContentNeighborForCeiling(Grid grid, Vector2Int position)
    {
        if (grid == null || !grid.IsValidGridPos(position))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(position);
        return cell != null
            && !IsWallVisualCell(cell)
            && HasRoomContentForCeiling(cell);
    }

    private void AddWallCellTiles(HashSet<Vector2Int> wallTilePositions, Vector2Int cellPosition)
    {
        Vector2Int floor = new Vector2Int(cellPosition.x, cellPosition.y * cellTileHeight);
        for (int y = 0; y < cellTileHeight; y++)
        {
            wallTilePositions.Add(floor + (Vector2Int.up * y));
        }
    }
}
