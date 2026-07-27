using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer.Unity;

public sealed class WorldWaterRuntime :
    IWorldWaterQuery,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WorldWaterRuntime.Tick");

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IGameClock gameClock;
    private readonly List<WorldWaterSourceSaveData> sources = new List<WorldWaterSourceSaveData>();
    private readonly Dictionary<string, WorldWaterSourceSaveData> byId =
        new Dictionary<string, WorldWaterSourceSaveData>(StringComparer.Ordinal);
    private GameObject visualRoot;
    private Tilemap waterTilemap;
    private Tile waterTile;
    private Texture2D waterTexture;
    private Sprite waterSprite;
    private int nextSequence = 1;

    public WorldWaterRuntime(
        IGridSystemProvider gridSystemProvider,
        IGameClock gameClock)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public int NextWaterSequence => nextSequence;

    public void Start()
    {
        EnsureDefaultSources();
        EnsureVisuals();
        RefreshVisuals();
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        float delta = gameClock.DeltaTime;
        if (delta <= 0f)
        {
            return;
        }

        bool changed = false;
        foreach (WorldWaterSourceSaveData source in sources)
        {
            float next = Mathf.Min(source.capacity, source.remaining + source.regenerationPerSecond * delta);
            changed |= !Mathf.Approximately(next, source.remaining);
            source.remaining = next;
        }

        if (changed && gameClock.FrameCount % 60 == 0)
        {
            RefreshVisuals();
        }
    }

    public void Dispose()
    {
        ClearTerrain();
        if (visualRoot != null)
        {
            UnityEngine.Object.Destroy(visualRoot);
        }

        if (waterTile != null)
        {
            UnityEngine.Object.Destroy(waterTile);
        }

        if (waterSprite != null)
        {
            UnityEngine.Object.Destroy(waterSprite);
        }

        if (waterTexture != null)
        {
            UnityEngine.Object.Destroy(waterTexture);
        }
    }

    public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources()
    {
        return sources.Where(source => source != null).Select(ToSnapshot).ToArray();
    }

    public bool TryGetSource(string sourceId, out WorldWaterSourceSnapshot source)
    {
        if (!string.IsNullOrWhiteSpace(sourceId)
            && byId.TryGetValue(sourceId, out WorldWaterSourceSaveData entry)
            && entry != null)
        {
            source = ToSnapshot(entry);
            return true;
        }

        source = default;
        return false;
    }

    public bool TryFindDrinkSource(Vector2Int origin, bool allowFoul, out WorldWaterSourceSnapshot source)
    {
        WorldWaterSourceSaveData best = sources.Where(candidate => candidate != null
                && candidate.remaining > 0.05f
                && (allowFoul || candidate.quality != WorldWaterQuality.Foul))
            .OrderBy(candidate => candidate.quality)
            .ThenBy(candidate => Mathf.Abs(candidate.gridX - origin.x) + Mathf.Abs(candidate.gridY - origin.y))
            .FirstOrDefault();
        if (best == null)
        {
            source = default;
            return false;
        }

        source = ToSnapshot(best);
        return true;
    }

    public bool TryDrink(string sourceId, float amount, out WorldWaterQuality quality, out float consumed)
    {
        quality = WorldWaterQuality.Foul;
        consumed = 0f;
        if (string.IsNullOrWhiteSpace(sourceId)
            || !byId.TryGetValue(sourceId, out WorldWaterSourceSaveData source)
            || source == null)
        {
            return false;
        }

        quality = source.quality;
        consumed = Mathf.Min(Mathf.Max(0f, amount), source.remaining);
        source.remaining = Mathf.Max(0f, source.remaining - consumed);
        RefreshCell(source);
        return consumed > 0f;
    }

    public bool DebugCreateSource(
        Vector2Int position,
        WorldWaterQuality quality,
        float capacity,
        GridCellTerrainType terrainType,
        out string sourceId)
    {
        sourceId = string.Empty;
        if (!gridSystemProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(position))
        {
            return false;
        }

        WorldWaterSourceSaveData existing = sources.FirstOrDefault(source => source != null
            && source.gridX == position.x
            && source.gridY == position.y);
        if (existing != null)
        {
            DebugSetSource(existing.sourceId, quality, capacity, capacity);
            sourceId = existing.sourceId;
            return true;
        }

        WorldWaterSourceSaveData source = CreateSource(
            position,
            terrainType,
            quality,
            Mathf.Max(0.1f, capacity),
            0.03f);
        AddSource(source);
        ApplyTerrain();
        RefreshCell(source);
        sourceId = source.sourceId;
        return true;
    }

    public bool DebugSetSource(
        string sourceId,
        WorldWaterQuality quality,
        float capacity,
        float remaining)
    {
        if (string.IsNullOrWhiteSpace(sourceId)
            || !byId.TryGetValue(sourceId, out WorldWaterSourceSaveData source)
            || source == null)
        {
            return false;
        }

        source.quality = quality;
        source.capacity = Mathf.Max(0.1f, capacity);
        source.remaining = Mathf.Clamp(remaining, 0f, source.capacity);
        RefreshCell(source);
        return true;
    }

    public List<WorldWaterSourceSaveData> CaptureWaterSources()
    {
        return sources.Where(source => source != null).Select(Clone).ToList();
    }

    public void RestoreWaterSources(IEnumerable<WorldWaterSourceSaveData> saveData, int nextSequence)
    {
        ClearTerrain();
        sources.Clear();
        byId.Clear();
        this.nextSequence = Mathf.Max(1, nextSequence);
        foreach (WorldWaterSourceSaveData source in saveData ?? Array.Empty<WorldWaterSourceSaveData>())
        {
            if (source == null || string.IsNullOrWhiteSpace(source.sourceId))
            {
                continue;
            }

            AddSource(Clone(source));
        }

        if (sources.Count == 0)
        {
            EnsureDefaultSources();
        }

        ApplyTerrain();
        RefreshVisuals();
    }

    private void EnsureDefaultSources()
    {
        if (sources.Count > 0 || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        List<GridCell> exterior = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.ExteriorPath
                && WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .ToList();
        if (exterior.Count == 0)
        {
            return;
        }

        List<GridCell> pondCells = SelectPondCells(grid, exterior, 4);
        for (int i = 0; i < pondCells.Count; i++)
        {
            bool deepestBoundaryCell = i == 0;
            AddSource(CreateSource(
                pondCells[i].Position,
                deepestBoundaryCell ? GridCellTerrainType.DeepWater : GridCellTerrainType.ShallowWater,
                deepestBoundaryCell ? WorldWaterQuality.Foul : WorldWaterQuality.Unsafe,
                deepestBoundaryCell ? 40f : 18f,
                deepestBoundaryCell ? 0.035f : 0.06f));
        }

        ApplyTerrain();
    }

    private static List<GridCell> SelectPondCells(Grid grid, IReadOnlyList<GridCell> exterior, int desiredCount)
    {
        List<List<GridCell>> runs = new List<List<GridCell>>();
        foreach (IGrouping<int, GridCell> floor in exterior.GroupBy(cell => cell.Position.y))
        {
            List<GridCell> current = new List<GridCell>();
            foreach (GridCell cell in floor.OrderBy(entry => entry.Position.x))
            {
                if (current.Count > 0 && cell.Position.x != current[current.Count - 1].Position.x + 1)
                {
                    runs.Add(current);
                    current = new List<GridCell>();
                }

                current.Add(cell);
            }

            if (current.Count > 0)
            {
                runs.Add(current);
            }
        }

        List<GridCell> run = runs
            .OrderByDescending(candidate => candidate.Count)
            .ThenByDescending(candidate => candidate.Max(cell => cell.Position.x))
            .FirstOrDefault();
        if (run == null || run.Count == 0)
        {
            return new List<GridCell>();
        }

        int minX = run.Min(cell => cell.Position.x);
        int maxX = run.Max(cell => cell.Position.x);
        int y = run[0].Position.y;
        GridCell minNeighbour = grid.GetGridCell(new Vector2Int(minX - 1, y));
        GridCell maxNeighbour = grid.GetGridCell(new Vector2Int(maxX + 1, y));
        bool dungeonAtMin = IsDungeonBoundary(minNeighbour);
        bool dungeonAtMax = IsDungeonBoundary(maxNeighbour);
        bool outerEdgeIsMax = dungeonAtMin || (!dungeonAtMax && maxX >= grid.width / 2);
        IEnumerable<GridCell> outerToInner = outerEdgeIsMax
            ? run.OrderByDescending(cell => cell.Position.x)
            : run.OrderBy(cell => cell.Position.x);
        return outerToInner.Take(Mathf.Clamp(desiredCount, 1, run.Count)).ToList();
    }

    private static bool IsDungeonBoundary(GridCell cell)
    {
        return cell != null
            && cell.AreaType is GridCellAreaType.DungeonInterior or GridCellAreaType.Entrance;
    }

    private WorldWaterSourceSaveData CreateSource(
        Vector2Int position,
        GridCellTerrainType terrain,
        WorldWaterQuality quality,
        float capacity,
        float regeneration)
    {
        return new WorldWaterSourceSaveData
        {
            sourceId = $"water:{nextSequence++:D8}",
            gridX = position.x,
            gridY = position.y,
            terrainType = terrain,
            quality = quality,
            capacity = capacity,
            remaining = capacity,
            regenerationPerSecond = regeneration
        };
    }

    private void AddSource(WorldWaterSourceSaveData source)
    {
        source.capacity = Mathf.Max(0.1f, source.capacity);
        source.remaining = Mathf.Clamp(source.remaining, 0f, source.capacity);
        sources.Add(source);
        byId[source.sourceId] = source;
    }

    private void ApplyTerrain()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        foreach (WorldWaterSourceSaveData source in sources)
        {
            grid.SetTerrainType(
                new Vector2Int(source.gridX, source.gridY),
                source.terrainType);
        }
    }

    private void ClearTerrain()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        foreach (WorldWaterSourceSaveData source in sources)
        {
            grid.SetTerrainType(
                new Vector2Int(source.gridX, source.gridY),
                GridCellTerrainType.Dry);
        }
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        visualRoot = new GameObject("World Water Tilemap");
        DungeonRuntimeHierarchy.Parent(visualRoot, DungeonRuntimeHierarchy.Exterior);
        UnityEngine.Grid unityGrid = visualRoot.AddComponent<UnityEngine.Grid>();
        unityGrid.cellSize = new Vector3(1f, grid.CellWorldHeight, 0f);
        visualRoot.transform.position = grid.OriginPosition;
        GameObject tileObject = new GameObject("Water");
        tileObject.transform.SetParent(visualRoot.transform, false);
        tileObject.transform.localPosition = new Vector3(
            0f,
            0.25f - grid.CellWorldHeight * 0.5f,
            0f);
        waterTilemap = tileObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = tileObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Wall";
        renderer.sortingOrder = 2;
        waterTile = CreateRuntimeTile();
    }

    private Tile CreateRuntimeTile()
    {
        const int width = 16;
        const int height = 8;
        waterTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel;
                if (y == height - 1)
                {
                    pixel = new Color(1f, 1f, 1f, (x + 1) % 4 < 2 ? 0.42f : 0f);
                }
                else if (y == height - 2)
                {
                    pixel = new Color(1f, 1f, 1f, 1f);
                }
                else if (y == height - 3 && (x + 2) % 5 < 2)
                {
                    pixel = new Color(1f, 1f, 1f, 0.82f);
                }
                else
                {
                    float depthShade = Mathf.Lerp(0.68f, 0.96f, y / (float)(height - 1));
                    pixel = new Color(depthShade, depthShade, depthShade, 1f);
                }

                waterTexture.SetPixel(x, y, pixel);
            }
        }

        waterTexture.Apply();
        waterSprite = Sprite.Create(
            waterTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            width);
        waterSprite.hideFlags = HideFlags.HideAndDontSave;
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.hideFlags = HideFlags.HideAndDontSave;
        tile.sprite = waterSprite;
        tile.flags = TileFlags.None;
        return tile;
    }

    private void RefreshVisuals()
    {
        EnsureVisuals();
        waterTilemap?.ClearAllTiles();
        foreach (WorldWaterSourceSaveData source in sources)
        {
            RefreshCell(source);
        }
    }

    private void RefreshCell(WorldWaterSourceSaveData source)
    {
        EnsureVisuals();
        if (waterTilemap == null || waterTile == null || source == null)
        {
            return;
        }

        Vector3Int position = new Vector3Int(-source.gridX, source.gridY, 0);
        Color color = source.quality switch
        {
            WorldWaterQuality.Clean => new Color(0.22f, 0.72f, 1f, 0.96f),
            WorldWaterQuality.Unsafe => new Color(0.12f, 0.58f, 0.84f, 0.94f),
            _ => new Color(0.09f, 0.45f, 0.48f, 0.98f)
        };
        color *= Mathf.Lerp(0.5f, 1f, source.remaining / Mathf.Max(0.1f, source.capacity));
        color.a = source.terrainType == GridCellTerrainType.DeepWater ? 0.98f : 0.9f;
        waterTilemap.SetTile(position, waterTile);
        waterTilemap.SetColor(position, color);
    }

    private static WorldWaterSourceSnapshot ToSnapshot(WorldWaterSourceSaveData source)
    {
        return new WorldWaterSourceSnapshot(
            source.sourceId,
            new Vector2Int(source.gridX, source.gridY),
            source.terrainType,
            source.quality,
            source.capacity,
            source.remaining,
            source.regenerationPerSecond);
    }

    private static WorldWaterSourceSaveData Clone(WorldWaterSourceSaveData source)
    {
        return new WorldWaterSourceSaveData
        {
            sourceId = source.sourceId ?? string.Empty,
            gridX = source.gridX,
            gridY = source.gridY,
            terrainType = source.terrainType,
            quality = source.quality,
            capacity = source.capacity,
            remaining = source.remaining,
            regenerationPerSecond = source.regenerationPerSecond
        };
    }
}
