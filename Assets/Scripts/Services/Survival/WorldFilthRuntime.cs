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

internal sealed class WorldFilthAggregateState
{
    internal readonly List<WorldFilthSaveData> Filth = new();
    internal readonly Dictionary<string, WorldFilthSaveData> ById =
        new(StringComparer.Ordinal);
    internal int NextSequence = 1;
    internal int StateVersion;
}

public sealed class WorldFilthRestoreCandidate
{
    internal WorldFilthRestoreCandidate(WorldFilthAggregateState state) =>
        State = state ?? throw new ArgumentNullException(nameof(state));

    internal WorldFilthAggregateState State { get; }
}

internal interface IWorldFilthRestoreCandidatePort
{
    WorldFilthRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldFilthSaveData> saveData,
        int nextSequence);
    void PublishRestoreCandidate(WorldFilthRestoreCandidate candidate);
}

public sealed class WorldFilthSpatialDependencies
{
    public WorldFilthSpatialDependencies(
        IGridSystemProvider gridSystemProvider,
        IExteriorZoneQuery exteriorZoneQuery,
        IBuildingFacilityStateChangePort facilityCandidateCache,
        IRoomFacilityPolicy roomFacilityPolicy)
    {
        GridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        ExteriorZoneQuery = exteriorZoneQuery
            ?? throw new ArgumentNullException(nameof(exteriorZoneQuery));
        FacilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        RoomFacilityPolicy = roomFacilityPolicy
            ?? throw new ArgumentNullException(nameof(roomFacilityPolicy));
    }

    public IGridSystemProvider GridSystemProvider { get; }
    public IExteriorZoneQuery ExteriorZoneQuery { get; }
    public IBuildingFacilityStateChangePort FacilityCandidateCache { get; }
    public IRoomFacilityPolicy RoomFacilityPolicy { get; }
}

public sealed class WorldFilthGameplayDependencies
{
    public WorldFilthGameplayDependencies(
        IBuildingResearchWorkPort blueprintResearchWorkService,
        Func<IBuildingEquipmentCraftingRuntimePort> combatEquipmentRuntime,
        IBuildingWorldRegistryPort worldRegistry,
        IBuildingItemStackPort worldItems,
        Func<IBuildingAbilityRuntimeDispatcher> abilityDispatcher,
        IBuildingEvolutionStatePort evolutionState)
    {
        BlueprintResearchWorkService = blueprintResearchWorkService
            ?? throw new ArgumentNullException(nameof(blueprintResearchWorkService));
        CombatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        WorldItems = worldItems ?? throw new ArgumentNullException(nameof(worldItems));
        AbilityDispatcher = abilityDispatcher
            ?? throw new ArgumentNullException(nameof(abilityDispatcher));
        EvolutionState = evolutionState
            ?? throw new ArgumentNullException(nameof(evolutionState));
    }

    public IBuildingResearchWorkPort BlueprintResearchWorkService { get; }
    public Func<IBuildingEquipmentCraftingRuntimePort> CombatEquipmentRuntime
    { get; }
    public IBuildingWorldRegistryPort WorldRegistry { get; }
    public IBuildingItemStackPort WorldItems { get; }
    public Func<IBuildingAbilityRuntimeDispatcher> AbilityDispatcher { get; }
    public IBuildingEvolutionStatePort EvolutionState { get; }
}

public sealed class WorldFilthRuntimeDependencies
{
    public WorldFilthRuntimeDependencies(
        IPaidFacilityContractRuntime paidFacilityContracts,
        IGameClock gameClock,
        IRuntimeBuildingArchetypeCatalog buildingArchetypes,
        IGameContentCatalog contentCatalog)
    {
        PaidFacilityContracts = paidFacilityContracts
            ?? throw new ArgumentNullException(nameof(paidFacilityContracts));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        BuildingArchetypes = buildingArchetypes
            ?? throw new ArgumentNullException(nameof(buildingArchetypes));
        ContentCatalog = contentCatalog
            ?? throw new ArgumentNullException(nameof(contentCatalog));
    }

    public IPaidFacilityContractRuntime PaidFacilityContracts { get; }
    public IGameClock GameClock { get; }
    public IRuntimeBuildingArchetypeCatalog BuildingArchetypes { get; }
    public IGameContentCatalog ContentCatalog { get; }
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
        Vector2Int position,
        BuildingSO definition)
    {
        this.filthRuntime = filthRuntime
            ?? throw new ArgumentNullException(nameof(filthRuntime));
        BuildingSO data = definition
            ?? throw new ArgumentNullException(nameof(definition));

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
        MarkFacilityDynamicStateDirty();
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
        base.OnDestroy();
    }
}

public sealed class WorldFilthRuntime :
    IWorldFilthQuery,
    IWorldFilthWorkTargetRuntime,
    IWorldFilthRestoreCandidatePort,
    IStartable,
    IDisposable
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IExteriorZoneQuery exteriorZoneQuery;
    private readonly IBuildingResearchWorkPort blueprintResearchWorkService;
    private readonly IBuildingFacilityStateChangePort facilityCandidateCache;
    private readonly IRoomFacilityPolicy roomFacilityPolicy;
    private readonly Func<IBuildingEquipmentCraftingRuntimePort>
        combatEquipmentRuntime;
    private readonly IBuildingWorldRegistryPort worldRegistry;
    private readonly IBuildingItemStackPort worldItems;
    private readonly Func<IBuildingAbilityRuntimeDispatcher> abilityDispatcher;
    private readonly IBuildingEvolutionStatePort evolutionState;
    private readonly IPaidFacilityContractRuntime paidFacilityContracts;
    private readonly IGameClock gameClock;
    private readonly IRuntimeBuildingArchetypeCatalog buildingArchetypes;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly Tile filthTile;
    private readonly Dictionary<Vector2Int, WorldFilthWorkTarget> workTargets =
        new Dictionary<Vector2Int, WorldFilthWorkTarget>();
    private GameObject visualRoot;
    private Tilemap floorTilemap;
    private Tilemap wallTilemap;
    private WorldFilthAggregateState projectedState;
    private bool projectionDirty = true;

    private WorldFilthAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new WorldFilthAggregateState());
    private List<WorldFilthSaveData> filth => State.Filth;
    private Dictionary<string, WorldFilthSaveData> byId => State.ById;
    private int nextSequence
    {
        get => State.NextSequence;
        set => State.NextSequence = value;
    }

    public WorldFilthRuntime(
        WorldFilthSpatialDependencies spatial,
        WorldFilthGameplayDependencies gameplay,
        WorldFilthRuntimeDependencies runtime,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
        runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        gridSystemProvider = spatial.GridSystemProvider;
        exteriorZoneQuery = spatial.ExteriorZoneQuery;
        facilityCandidateCache = spatial.FacilityCandidateCache;
        roomFacilityPolicy = spatial.RoomFacilityPolicy;
        blueprintResearchWorkService = gameplay.BlueprintResearchWorkService;
        combatEquipmentRuntime = gameplay.CombatEquipmentRuntime;
        worldRegistry = gameplay.WorldRegistry;
        worldItems = gameplay.WorldItems;
        abilityDispatcher = gameplay.AbilityDispatcher;
        evolutionState = gameplay.EvolutionState;
        paidFacilityContracts = runtime.PaidFacilityContracts;
        gameClock = runtime.GameClock;
        buildingArchetypes = runtime.BuildingArchetypes;
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        filthTile = runtime.ContentCatalog.WorldPresentation.WorldFilthTile != null
            ? runtime.ContentCatalog.WorldPresentation.WorldFilthTile
            : throw new InvalidOperationException(
                "World presentation catalog has no authored filth tile.");
    }

    public int NextFilthSequence => nextSequence;
    public int StateVersion
    {
        get => State.StateVersion;
        private set => State.StateVersion = value;
    }

    public void Start()
    {
        EnsureProjectionCurrent();
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

    }

    public IReadOnlyList<WorldFilthSnapshot> GetAll()
    {
        EnsureProjectionCurrent();
        return filth.Where(entry => entry != null && entry.amount > 0f)
            .Select(ToSnapshot)
            .ToArray();
    }

    public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position)
    {
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        EnsureProjectionCurrent();
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
        PublishRestoreCandidate(BuildRestoreCandidate(saveData, nextSequence));
    }

    public WorldFilthRestoreCandidate BuildRestoreCandidate(
        IEnumerable<WorldFilthSaveData> saveData,
        int nextSequence)
    {
        if (nextSequence < 1)
        {
            throw new InvalidOperationException(
                "World-filth restore sequence must be positive.");
        }
        WorldFilthAggregateState restored = new WorldFilthAggregateState
        {
            NextSequence = nextSequence,
            StateVersion = StateVersion + 1
        };
        foreach (WorldFilthSaveData source in saveData
                     ?? throw new ArgumentNullException(nameof(saveData)))
        {
            if (source == null)
            {
                throw new InvalidOperationException(
                    "World-filth restore candidate contains a null entry.");
            }
            WorldFilthSaveData copy = Clone(source);
            restored.Filth.Add(copy);
            restored.ById.Add(copy.filthId, copy);
        }

        return new WorldFilthRestoreCandidate(restored);
    }

    public void PublishRestoreCandidate(WorldFilthRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
        projectionDirty = true;
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

        IBuildingEquipmentCraftingRuntimePort equipmentCrafting =
            combatEquipmentRuntime()
            ?? throw new InvalidOperationException(
                "World filth work-target construction requires the building equipment-crafting runtime.");
        IBuildingAbilityRuntimeDispatcher dispatcher = abilityDispatcher()
            ?? throw new InvalidOperationException(
                "World filth work-target construction requires the building ability dispatcher.");

        GameObject targetObject = new GameObject($"Filth Work ({position.x}, {position.y})");
        WorldFilthWorkTarget target = targetObject.AddComponent<WorldFilthWorkTarget>();
        target.RestorePersistentIdentity(new BuildingInstanceId(
            $"building:world-filth:{position.x}:{position.y}"));
        target.ConstructBuildableObject(
            blueprintResearchWorkService,
            facilityCandidateCache,
            roomFacilityPolicy,
            equipmentCrafting,
            worldRegistry,
            worldItems,
            dispatcher,
            gameClock,
            paidFacilityContracts,
            evolutionState);
        target.InitializeRuntime(
            this,
            grid,
            position,
            buildingArchetypes.WorldFilthWorkTarget);
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

    private void EnsureProjectionCurrent()
    {
        WorldFilthAggregateState current = State;
        if (!projectionDirty && ReferenceEquals(projectedState, current))
        {
            return;
        }

        projectedState = current;
        RefreshVisuals();
        RebuildWorkTargets();
        projectionDirty = false;
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
