using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer.Unity;

internal interface IWorldFilthWorkTargetRuntime
{
    float GetRequiredCleaningWork(Vector2Int position);
    float GetCleanlinessPenalty(Vector2Int position, int radius = 0);
    bool CleanAt(Vector2Int position, float workAmount);
    IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position);
    void NotifyWorkTargetDestroyed(Vector2Int position, WorldFilthWorkTarget target);
}

public sealed class WorldFilthWorkTarget : Facility
{
    private bool registeredOnGrid;
    private bool removalRequested;
    private bool priorityCleaning;
    private IWorldFilthWorkTargetRuntime filthRuntime;

    public float RequiredCleaningWork => filthRuntime != null
        ? filthRuntime.GetRequiredCleaningWork(centerPos)
        : 5f;
    public bool IsPriorityCleaning => priorityCleaning;

    internal void InitializeRuntime(
        IWorldFilthWorkTargetRuntime filthRuntime,
        Grid grid,
        Vector2Int position)
    {
        this.filthRuntime = filthRuntime
            ?? throw new ArgumentNullException(nameof(filthRuntime));
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.name = $"filth-work:{position.x}:{position.y}";
        data.id = CreateStableId(position);
        data.objectName = "오염";
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Filth;
        data.category = BuildingCategory.Special;
        data.type = typeof(WorldFilthWorkTarget);
        data.unlocked = true;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 1f,
            requiredWorkers = 1,
            disabledWhenDamaged = false
        };
        data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Clean });
        data.FacilityAnchors.Add(FacilityAnchorPurposeIds.Work, Vector2.zero);

        SetGrid(grid);
        Initialization(data, position);
        SetCleanliness(0f);
        transform.position = grid != null ? grid.GetWorldPos(position) : Vector3.zero;
        registeredOnGrid = grid != null && grid.RegisterOccupant(this, GridLayer.Filth, buildPoses, false);
        DungeonRuntimeHierarchy.Parent(gameObject, DungeonRuntimeHierarchy.Survival);
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        if (workType != FacilityWorkType.Clean || filthRuntime == null)
        {
            return base.GetLegacyWorkUrgency(workType);
        }

        if (priorityCleaning)
        {
            return 100f;
        }

        return Mathf.Clamp(35f + filthRuntime.GetCleanlinessPenalty(centerPos) * 0.8f, 35f, 100f);
    }

    public void SetPriorityCleaning(bool priority)
    {
        priorityCleaning = priority;
        foreach (CharacterActor actor in WorldRegistry?.Characters ?? Array.Empty<CharacterActor>())
        {
            if (actor != null && actor.TryGetAbility(out AbilityWork _))
            {
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }
    }

    public void CompleteCleaning(float workAmount)
    {
        filthRuntime?.CleanAt(centerPos, workAmount);
        if (filthRuntime == null || filthRuntime.GetAt(centerPos).Count == 0)
        {
            SetCleanliness(100f);
            removalRequested = true;
        }
    }

    private void Update()
    {
        if (removalRequested && WorkerReservation == null)
        {
            Destroy(gameObject);
        }
    }

    protected override void OnDestroy()
    {
        if (registeredOnGrid && Grid != null)
        {
            Grid.RemoveOccupant(this, GridLayer.Filth, buildPoses, false);
            registeredOnGrid = false;
        }

        filthRuntime?.NotifyWorkTargetDestroyed(centerPos, this);
        BuildingSO runtimeData = BuildingData;
        base.OnDestroy();
        if (runtimeData != null)
        {
            Destroy(runtimeData);
        }
    }

    private static int CreateStableId(Vector2Int position)
    {
        unchecked
        {
            int hash = -1700000000 + position.x * 397 ^ position.y;
            return hash == 0 ? -1700000001 : hash;
        }
    }
}

public sealed class WorldFilthRuntime :
    IWorldFilthQuery,
    IWorldFilthWorkTargetRuntime,
    IStartable,
    IDisposable
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IExteriorZoneQuery exteriorZoneQuery;
    private readonly IBlueprintResearchWorkService blueprintResearchWorkService;
    private readonly IWorldInfoClickSelector worldInfoClickSelector;
    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IRoomFacilityPolicy roomFacilityPolicy;
    private readonly IExpeditionEquipmentRuntime expeditionEquipmentRuntime;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly List<WorldFilthSaveData> filth = new List<WorldFilthSaveData>();
    private readonly Dictionary<string, WorldFilthSaveData> byId =
        new Dictionary<string, WorldFilthSaveData>(StringComparer.Ordinal);
    private readonly Dictionary<Vector2Int, WorldFilthWorkTarget> workTargets =
        new Dictionary<Vector2Int, WorldFilthWorkTarget>();
    private GameObject visualRoot;
    private Tilemap floorTilemap;
    private Tilemap wallTilemap;
    private Tile filthTile;
    private int nextSequence = 1;

    public WorldFilthRuntime(
        IGridSystemProvider gridSystemProvider,
        IExteriorZoneQuery exteriorZoneQuery,
        IBlueprintResearchWorkService blueprintResearchWorkService,
        IWorldInfoClickSelector worldInfoClickSelector,
        IFacilityCandidateCache facilityCandidateCache,
        IRoomFacilityPolicy roomFacilityPolicy,
        IExpeditionEquipmentRuntime expeditionEquipmentRuntime,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.exteriorZoneQuery = exteriorZoneQuery ?? throw new ArgumentNullException(nameof(exteriorZoneQuery));
        this.blueprintResearchWorkService = blueprintResearchWorkService
            ?? throw new ArgumentNullException(nameof(blueprintResearchWorkService));
        this.worldInfoClickSelector = worldInfoClickSelector
            ?? throw new ArgumentNullException(nameof(worldInfoClickSelector));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.roomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
        this.expeditionEquipmentRuntime = expeditionEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(expeditionEquipmentRuntime));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public int NextFilthSequence => nextSequence;
    public int StateVersion { get; private set; }

    public void Start()
    {
        EnsureVisuals();
        RefreshVisuals();
    }

    public void Dispose()
    {
        if (visualRoot != null)
        {
            UnityEngine.Object.Destroy(visualRoot);
        }

        foreach (WorldFilthWorkTarget target in workTargets.Values.Where(target => target != null).ToArray())
        {
            UnityEngine.Object.Destroy(target.gameObject);
        }
        workTargets.Clear();

        if (filthTile != null)
        {
            UnityEngine.Object.Destroy(filthTile);
        }
    }

    public IReadOnlyList<WorldFilthSnapshot> GetAll()
    {
        return filth.Where(entry => entry != null && entry.amount > 0f)
            .Select(ToSnapshot)
            .ToArray();
    }

    public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position)
    {
        return filth.Where(entry => entry != null
                && entry.amount > 0f
                && entry.gridX == position.x
                && entry.gridY == position.y)
            .Select(ToSnapshot)
            .ToArray();
    }

    public WorldFilthSnapshot AddFilth(
        WorldFilthType type,
        Vector2Int position,
        float amount,
        string sourceCharacterId,
        float infectionRisk,
        bool wallStain = false)
    {
        float safeAmount = Mathf.Max(0.1f, amount);
        WorldFilthSaveData existing = filth.FirstOrDefault(entry => entry != null
            && entry.type == type
            && entry.wallStain == wallStain
            && entry.gridX == position.x
            && entry.gridY == position.y
            && string.Equals(entry.sourceCharacterId ?? string.Empty, sourceCharacterId ?? string.Empty, StringComparison.Ordinal));
        if (existing == null)
        {
            existing = new WorldFilthSaveData
            {
                filthId = $"filth:{nextSequence++:D8}",
                type = type,
                amount = safeAmount,
                gridX = position.x,
                gridY = position.y,
                sourceCharacterId = sourceCharacterId ?? string.Empty,
                infectionRisk = Mathf.Clamp01(infectionRisk),
                wallStain = wallStain
            };
            filth.Add(existing);
            byId[existing.filthId] = existing;
        }
        else
        {
            existing.amount = Mathf.Min(100f, existing.amount + safeAmount);
            existing.infectionRisk = Mathf.Max(existing.infectionRisk, Mathf.Clamp01(infectionRisk));
        }

        ApplyWorldPenalty(position, safeAmount, infectionRisk);
        StateVersion++;
        EnsureWorkTarget(position);
        RefreshCell(position);
        return ToSnapshot(existing);
    }

    public bool Clean(string filthId, float workAmount, out float remainingAmount)
    {
        remainingAmount = 0f;
        if (string.IsNullOrWhiteSpace(filthId)
            || !byId.TryGetValue(filthId, out WorldFilthSaveData entry)
            || entry == null)
        {
            return false;
        }

        Vector2Int position = new Vector2Int(entry.gridX, entry.gridY);
        entry.amount = Mathf.Max(0f, entry.amount - Mathf.Max(0f, workAmount) / 12f);
        remainingAmount = entry.amount;
        if (entry.amount <= 0.001f)
        {
            byId.Remove(entry.filthId);
            filth.Remove(entry);
        }

        StateVersion++;
        RefreshCell(position);
        return true;
    }

    public float GetRequiredCleaningWork(Vector2Int position)
    {
        return GetAt(position).Sum(entry => entry.RequiredCleaningWork);
    }

    public bool CleanAt(Vector2Int position, float workAmount)
    {
        float remainingWork = Mathf.Max(0f, workAmount);
        bool cleanedAny = false;
        foreach (WorldFilthSnapshot entry in GetAt(position)
                     .OrderByDescending(entry => entry.InfectionRisk)
                     .ThenByDescending(entry => entry.Amount))
        {
            if (remainingWork <= 0f)
            {
                break;
            }

            float workForEntry = Mathf.Min(remainingWork, entry.RequiredCleaningWork);
            cleanedAny |= Clean(entry.FilthId, workForEntry, out _);
            remainingWork -= workForEntry;
        }

        return cleanedAny;
    }

    public void NotifyWorkTargetDestroyed(Vector2Int position, WorldFilthWorkTarget target)
    {
        if (workTargets.TryGetValue(position, out WorldFilthWorkTarget current) && current == target)
        {
            workTargets.Remove(position);
        }
    }

    public float GetCleanlinessPenalty(Vector2Int position, int radius = 0)
    {
        int safeRadius = Mathf.Max(0, radius);
        float total = 0f;
        foreach (WorldFilthSaveData entry in filth)
        {
            if (entry == null || entry.amount <= 0f)
            {
                continue;
            }

            int distance = Mathf.Abs(entry.gridX - position.x) + Mathf.Abs(entry.gridY - position.y);
            if (distance <= safeRadius)
            {
                total += entry.amount * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(entry.infectionRisk));
            }
        }

        return Mathf.Clamp(total, 0f, 100f);
    }

    public List<WorldFilthSaveData> CaptureFilth()
    {
        return filth.Where(entry => entry != null && entry.amount > 0f)
            .Select(Clone)
            .ToList();
    }

    public void RestoreFilth(IEnumerable<WorldFilthSaveData> saveData, int nextSequence)
    {
        filth.Clear();
        byId.Clear();
        this.nextSequence = Mathf.Max(1, nextSequence);
        foreach (WorldFilthSaveData source in saveData ?? Array.Empty<WorldFilthSaveData>())
        {
            if (source == null || source.amount <= 0f || string.IsNullOrWhiteSpace(source.filthId))
            {
                continue;
            }

            WorldFilthSaveData copy = Clone(source);
            filth.Add(copy);
            byId[copy.filthId] = copy;
        }

        StateVersion++;
        RefreshVisuals();
        RebuildWorkTargets();
    }

    private void RebuildWorkTargets()
    {
        foreach (WorldFilthWorkTarget target in workTargets.Values.Where(target => target != null).ToArray())
        {
            UnityEngine.Object.Destroy(target.gameObject);
        }
        workTargets.Clear();
        foreach (Vector2Int position in filth.Where(entry => entry != null && entry.amount > 0f)
                     .Select(entry => new Vector2Int(entry.gridX, entry.gridY))
                     .Distinct())
        {
            EnsureWorkTarget(position);
        }
    }

    private void EnsureWorkTarget(Vector2Int position)
    {
        if (workTargets.TryGetValue(position, out WorldFilthWorkTarget existing) && existing != null)
        {
            existing.SetCleanliness(0f);
            return;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid) || grid.GetGridCell(position) == null)
        {
            return;
        }

        GameObject targetObject = new GameObject($"Filth Work ({position.x}, {position.y})");
        WorldFilthWorkTarget target = targetObject.AddComponent<WorldFilthWorkTarget>();
        target.ConstructBuildableObject(
            blueprintResearchWorkService,
            worldInfoClickSelector,
            facilityCandidateCache,
            roomFacilityPolicy,
            expeditionEquipmentRuntime,
            worldRegistry,
            gameClock: gameClock);
        target.InitializeRuntime(this, grid, position);
        workTargets[position] = target;
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        visualRoot = new GameObject("World Filth Tilemaps");
        DungeonRuntimeHierarchy.Parent(visualRoot, DungeonRuntimeHierarchy.Exterior);
        UnityEngine.Grid unityGrid = visualRoot.AddComponent<UnityEngine.Grid>();
        unityGrid.cellSize = new Vector3(1f, grid.CellWorldHeight, 0f);
        visualRoot.transform.position = grid.OriginPosition;
        floorTilemap = CreateTilemap("Floor Filth", visualRoot.transform, -2);
        wallTilemap = CreateTilemap("Wall Stains", visualRoot.transform, 1);
        filthTile = CreateRuntimeTile();
    }

    private static Tilemap CreateTilemap(string name, Transform parent, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        Tilemap tilemap = child.AddComponent<Tilemap>();
        TilemapRenderer renderer = child.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "DungeonBackObject";
        renderer.sortingOrder = order;
        return tilemap;
    }

    private static Tile CreateRuntimeTile()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.hideFlags = HideFlags.HideAndDontSave;
        tile.sprite = sprite;
        tile.color = Color.white;
        return tile;
    }

    private void RefreshVisuals()
    {
        EnsureVisuals();
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        foreach (Vector2Int position in filth.Where(entry => entry != null && entry.amount > 0f)
                     .Select(entry => new Vector2Int(entry.gridX, entry.gridY)).Distinct())
        {
            RefreshCell(position);
        }
    }

    private void RefreshCell(Vector2Int position)
    {
        EnsureVisuals();
        if (filthTile == null || floorTilemap == null || wallTilemap == null)
        {
            return;
        }

        Vector3Int tilePosition = new Vector3Int(-position.x, position.y, 0);
        SetCellVisual(floorTilemap, tilePosition, filth.Where(entry => entry != null
            && !entry.wallStain && entry.gridX == position.x && entry.gridY == position.y));
        SetCellVisual(wallTilemap, tilePosition, filth.Where(entry => entry != null
            && entry.wallStain && entry.gridX == position.x && entry.gridY == position.y));
    }

    private void SetCellVisual(Tilemap tilemap, Vector3Int position, IEnumerable<WorldFilthSaveData> entries)
    {
        WorldFilthSaveData[] values = entries.Where(entry => entry.amount > 0f).ToArray();
        if (values.Length == 0)
        {
            tilemap.SetTile(position, null);
            return;
        }

        float amount = Mathf.Clamp01(values.Sum(entry => entry.amount) / 35f);
        float risk = Mathf.Clamp01(values.Max(entry => entry.infectionRisk));
        WorldFilthType type = values.OrderByDescending(entry => entry.amount).First().type;
        Color baseColor = type switch
        {
            WorldFilthType.Blood => new Color(0.35f, 0.015f, 0.025f, 1f),
            WorldFilthType.Rot => new Color(0.22f, 0.3f, 0.06f, 1f),
            WorldFilthType.Stain => new Color(0.24f, 0.13f, 0.06f, 1f),
            _ => new Color(0.28f, 0.2f, 0.04f, 1f)
        };
        baseColor.a = Mathf.Lerp(0.32f, 0.82f, Mathf.Max(amount, risk));
        tilemap.SetTile(position, filthTile);
        tilemap.SetColor(position, baseColor);
    }

    private static WorldFilthSnapshot ToSnapshot(WorldFilthSaveData entry)
    {
        return new WorldFilthSnapshot(
            entry.filthId,
            entry.type,
            entry.amount,
            new Vector2Int(entry.gridX, entry.gridY),
            entry.sourceCharacterId,
            entry.infectionRisk,
            entry.wallStain);
    }

    private static WorldFilthSaveData Clone(WorldFilthSaveData entry)
    {
        return new WorldFilthSaveData
        {
            filthId = entry.filthId ?? string.Empty,
            type = entry.type,
            amount = Mathf.Max(0f, entry.amount),
            gridX = entry.gridX,
            gridY = entry.gridY,
            sourceCharacterId = entry.sourceCharacterId ?? string.Empty,
            infectionRisk = Mathf.Clamp01(entry.infectionRisk),
            wallStain = entry.wallStain
        };
    }

    private void ApplyWorldPenalty(Vector2Int position, float amount, float infectionRisk)
    {
        if (gridSystemProvider.TryGetGrid(out Grid grid)
            && grid.GetGridCell(position)?.AreaType == GridCellAreaType.DungeonInterior)
        {
            return;
        }

        ExteriorZoneMarker nearest = exteriorZoneQuery.Zones
            .Where(zone => zone != null)
            .OrderBy(zone => Mathf.Abs(zone.GridPosition.x - position.x) + Mathf.Abs(zone.GridPosition.y - position.y))
            .FirstOrDefault();
        nearest?.ApplyExteriorWear(
            Mathf.Clamp(amount * (0.02f + infectionRisk * 0.03f), 0.01f, 0.3f),
            0f);
    }
}
