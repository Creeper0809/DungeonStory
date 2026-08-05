using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer.Unity;

internal sealed class WorldWaterAggregateState
{
    internal readonly List<WorldWaterSourceSaveData> Sources = new();
    internal readonly Dictionary<string, WorldWaterSourceSaveData> ById =
        new(StringComparer.Ordinal);
    internal int NextSequence = 1;
}

public sealed class WorldWaterRestoreCandidate
{
    internal WorldWaterRestoreCandidate(WorldWaterAggregateState state) =>
        State = state ?? throw new ArgumentNullException(nameof(state));

    internal WorldWaterAggregateState State { get; }
}

internal interface IWorldWaterRestoreCandidatePort
{
    WorldWaterRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldWaterSourceSaveData> saveData,
        int nextSequence);
    void PublishRestoreCandidate(WorldWaterRestoreCandidate candidate);
}

public sealed class WorldWaterRuntime :
    IWorldWaterQuery,
    IWorldWaterRestoreCandidatePort,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WorldWaterRuntime.Tick");

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IGameClock gameClock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly Tile waterTile;
    private GameObject visualRoot;
    private Tilemap waterTilemap;
    private WorldWaterAggregateState projectedState;
    private bool projectionDirty = true;

    private WorldWaterAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new WorldWaterAggregateState());
    private List<WorldWaterSourceSaveData> sources => State.Sources;
    private Dictionary<string, WorldWaterSourceSaveData> byId => State.ById;
    private int nextSequence
    {
        get => State.NextSequence;
        set => State.NextSequence = value;
    }

    public WorldWaterRuntime(
        IGridSystemProvider gridSystemProvider,
        IGameClock gameClock,
        IGameContentCatalog contentCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        if (contentCatalog == null)
        {
            throw new ArgumentNullException(nameof(contentCatalog));
        }

        waterTile = contentCatalog.WorldPresentation.WorldWaterTile != null
            ? contentCatalog.WorldPresentation.WorldWaterTile
            : throw new InvalidOperationException(
                "World presentation catalog has no authored water tile.");
    }

    public int NextWaterSequence => nextSequence;

    public void Start()
    {
        EnsureDefaultSources();
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        ClearTerrain(projectedState?.Sources);
        if (visualRoot != null)
        {
            UnityEngine.Object.Destroy(visualRoot);
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
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        PublishRestoreCandidate(BuildRestoreCandidate(saveData, nextSequence));
    }

    public WorldWaterRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldWaterSourceSaveData> saveData,
        int nextSequence)
    {
        if (nextSequence < 1)
        {
            throw new InvalidOperationException(
                "World-water restore sequence must be positive.");
        }
        WorldWaterAggregateState restored = new WorldWaterAggregateState
        {
            NextSequence = nextSequence
        };
        foreach (WorldWaterSourceSaveData source in saveData
                     ?? throw new ArgumentNullException(nameof(saveData)))
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "World-water restore candidate contains a null entry.");
            }
            WorldWaterSourceSaveData copy = Clone(source);
            restored.Sources.Add(copy);
            restored.ById.Add(copy.sourceId, copy);
        }

        return new WorldWaterRestoreCandidate(restored);
    }

    public void PublishRestoreCandidate(WorldWaterRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
        projectionDirty = true;
    }

    private void EnsureDefaultSources(bool applyTerrain = true)
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

        if (applyTerrain)
        {
            ApplyTerrain();
        }
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
        AddSource(State, source);
    }

    private static void AddSource(
        WorldWaterAggregateState state,
        WorldWaterSourceSaveData source)
    {
        source.capacity = Mathf.Max(0.1f, source.capacity);
        source.remaining = Mathf.Clamp(source.remaining, 0f, source.capacity);
        state.Sources.Add(source);
        state.ById[source.sourceId] = source;
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

    private void ClearTerrain(IEnumerable<WorldWaterSourceSaveData> stateSources)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        foreach (WorldWaterSourceSaveData source in stateSources ?? Array.Empty<WorldWaterSourceSaveData>())
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

    private void EnsureProjectionCurrent()
    {
        WorldWaterAggregateState current = State;
        if (!projectionDirty && ReferenceEquals(projectedState, current))
        {
            return;
        }

        ClearTerrain(projectedState?.Sources);
        projectedState = current;
        ApplyTerrain();
        RefreshVisuals();
        projectionDirty = false;
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
