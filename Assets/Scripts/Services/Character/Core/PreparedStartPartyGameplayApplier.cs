using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IPreparedStartPartyGameplayApplier
{
    bool TryApply(PreparedStartPartySnapshot snapshot, out string message);
}

public interface IPreparedStartPartyDiagnosticsQuery
{
    string LastReport { get; }
}

public interface IPreparedStartPartyCommitService
{
    bool TryCommit(out string message);
}

public sealed class PreparedStartPartyCommitService :
    IPreparedStartPartyCommitService
{
    private readonly IStartPartyPreparationService preparationService;
    private readonly IPreparedStartPartyGameplayApplier gameplayApplier;
    private readonly RunVariableRuntime runVariables;
    private readonly InvasionThreatRuntime threatRuntime;

    public PreparedStartPartyCommitService(
        IStartPartyPreparationService preparationService,
        IPreparedStartPartyGameplayApplier gameplayApplier,
        DungeonSceneRuntimeReferences sceneRuntimes,
        InvasionSceneRuntimeReferences invasionRuntimes)
    {
        this.preparationService = preparationService
            ?? throw new ArgumentNullException(nameof(preparationService));
        this.gameplayApplier = gameplayApplier
            ?? throw new ArgumentNullException(nameof(gameplayApplier));
        runVariables = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(PreparedStartPartyCommitService)} requires a loaded {nameof(RunVariableRuntime)}.");
        threatRuntime = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Threat
            ?? throw new InvalidOperationException(
                $"{nameof(PreparedStartPartyCommitService)} requires a loaded {nameof(InvasionThreatRuntime)}.");
    }

    public bool TryCommit(out string message)
    {
        int runSeed = runVariables.RunSeed;
        DungeonDifficulty difficulty = DungeonDifficulty.Normal;
        if (threatRuntime.Settings != null)
        {
            difficulty = DungeonDifficultyRules.FromLegacy(
                threatRuntime.Settings.difficulty);
        }

        if (!preparationService.TryCreatePreparedSnapshot(
                difficulty,
                runSeed,
                out PreparedStartPartySnapshot snapshot,
                out message))
        {
            return false;
        }

        if (!gameplayApplier.TryApply(snapshot, out message))
        {
            return false;
        }

        preparationService.Cancel();
        return true;
    }
}

public sealed class PreparedStartPartyCharacterContext
{
    public PreparedStartPartyCharacterContext(
        IRunCharacterCatalog characterCatalog,
        IOwnerRunManagerProvider ownerRunManagerProvider,
        ICharacterSpawnerProvider characterSpawnerProvider,
        ICharacterSpawnObjectFactory characterObjectFactory,
        ICharacterLifetimeQuery characterLifetimeQuery)
    {
        CharacterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        OwnerRunManagerProvider = ownerRunManagerProvider
            ?? throw new ArgumentNullException(nameof(ownerRunManagerProvider));
        CharacterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        CharacterObjectFactory = characterObjectFactory
            ?? throw new ArgumentNullException(nameof(characterObjectFactory));
        CharacterLifetimeQuery = characterLifetimeQuery
            ?? throw new ArgumentNullException(nameof(characterLifetimeQuery));
    }

    public IRunCharacterCatalog CharacterCatalog { get; }
    public IOwnerRunManagerProvider OwnerRunManagerProvider { get; }
    public ICharacterSpawnerProvider CharacterSpawnerProvider { get; }
    public ICharacterSpawnObjectFactory CharacterObjectFactory { get; }
    public ICharacterLifetimeQuery CharacterLifetimeQuery { get; }
}

public sealed class PreparedStartPartyWorldContext
{
    public PreparedStartPartyWorldContext(
        IWorldItemStackRuntime itemStackRuntime,
        IWarehouseWorldQuery warehouseWorldQuery,
        IGridSystemProvider gridSystemProvider,
        IDungeonGridBuildingControllerProvider gridBuildingControllerProvider,
        DungeonSceneRuntimeReferences sceneRuntimes,
        IMainCameraProvider mainCameraProvider,
        IRuntimeBuildingArchetypeCatalog buildingCatalog)
    {
        ItemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        WarehouseWorldQuery = warehouseWorldQuery
            ?? throw new ArgumentNullException(nameof(warehouseWorldQuery));
        GridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        GridBuildingControllerProvider = gridBuildingControllerProvider
            ?? throw new ArgumentNullException(nameof(gridBuildingControllerProvider));
        RunVariables = (sceneRuntimes
                ?? throw new ArgumentNullException(nameof(sceneRuntimes)))
            .RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(PreparedStartPartyGameplayApplier)} requires a loaded {nameof(RunVariableRuntime)}.");
        MainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        BuildingCatalog = buildingCatalog
            ?? throw new ArgumentNullException(nameof(buildingCatalog));
    }

    public IWorldItemStackRuntime ItemStackRuntime { get; }
    public IWarehouseWorldQuery WarehouseWorldQuery { get; }
    public IGridSystemProvider GridSystemProvider { get; }
    public IDungeonGridBuildingControllerProvider GridBuildingControllerProvider { get; }
    public RunVariableRuntime RunVariables { get; }
    public IMainCameraProvider MainCameraProvider { get; }
    public IRuntimeBuildingArchetypeCatalog BuildingCatalog { get; }
}

public sealed class PreparedStartPartyGameplayApplier :
    IPreparedStartPartyGameplayApplier,
    IPreparedStartPartyDiagnosticsQuery
{
    private static readonly (StockCategory Category, int Amount)[] StarterSupplies =
    {
        (StockCategory.Food, 15),
        (StockCategory.Water, 15),
        (StockCategory.General, 40),
        (StockCategory.Fuel, 10),
        (StockCategory.Medicine, 5)
    };

    private readonly IRunCharacterCatalog characterCatalog;
    private readonly IOwnerRunManagerProvider ownerRunManagerProvider;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterObjectFactory;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWarehouseWorldQuery warehouseWorldQuery;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonGridBuildingControllerProvider gridBuildingControllerProvider;
    private readonly RunVariableRuntime runVariables;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IRuntimeBuildingArchetypeCatalog buildingCatalog;
    private readonly ICharacterLifetimeQuery characterLifetimeQuery;

    public string LastReport { get; private set; } = string.Empty;

    public PreparedStartPartyGameplayApplier(
        PreparedStartPartyCharacterContext characters,
        PreparedStartPartyWorldContext world)
    {
        characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        world = world ?? throw new ArgumentNullException(nameof(world));
        characterCatalog = characters.CharacterCatalog;
        ownerRunManagerProvider = characters.OwnerRunManagerProvider;
        characterSpawnerProvider = characters.CharacterSpawnerProvider;
        characterObjectFactory = characters.CharacterObjectFactory;
        characterLifetimeQuery = characters.CharacterLifetimeQuery;
        itemStackRuntime = world.ItemStackRuntime;
        warehouseWorldQuery = world.WarehouseWorldQuery;
        gridSystemProvider = world.GridSystemProvider;
        gridBuildingControllerProvider = world.GridBuildingControllerProvider;
        runVariables = world.RunVariables;
        mainCameraProvider = world.MainCameraProvider;
        buildingCatalog = world.BuildingCatalog;
    }

    public bool TryApply(PreparedStartPartySnapshot snapshot, out string message)
    {
        if (snapshot == null || !snapshot.IsValid)
        {
            message = "준비된 시작 파티 정보가 올바르지 않습니다.";
            return false;
        }

        Dictionary<int, CharacterSO> charactersById = characterCatalog.Characters
            .Where(data => data != null)
            .GroupBy(data => data.id)
            .ToDictionary(group => group.Key, group => group.First());
        if (!charactersById.TryGetValue(snapshot.owner.characterDataId, out CharacterSO ownerData)
            || ownerData == null
            || !ownerData.IsOwnerCandidate)
        {
            message = "준비한 사장 데이터를 찾을 수 없습니다.";
            return false;
        }

        if (!ownerRunManagerProvider.TryGetManager(out OwnerRunManager manager)
            || !characterSpawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner.characterPrefab == null)
        {
            message = "게임씬에 사장/직원 배치 구성이 없습니다.";
            return false;
        }

        runVariables.StartRun(
                snapshot.runSeed,
                ownerData,
                snapshot.difficulty,
                snapshot.survivalPressure);

        EnsureStarterDungeonShell();

        List<CharacterActor> preparedStaff = new List<CharacterActor>(snapshot.staff.Count);
        List<string> diagnostics = new List<string>();
        for (int i = 0; i < snapshot.staff.Count; i++)
        {
            PreparedStartPartyMemberSnapshot staffSnapshot = snapshot.staff[i];
            if (!charactersById.TryGetValue(staffSnapshot.characterDataId, out CharacterSO staffData))
            {
                DestroyPreparedStaff(preparedStaff);
                message = $"준비한 직원 데이터가 없습니다. id={staffSnapshot.characterDataId}";
                return false;
            }

            CharacterActor staff = CreateStaffActor(staffData, staffSnapshot, spawner);
            if (staff == null)
            {
                DestroyPreparedStaff(preparedStaff);
                message = $"직원 {i + 1}을(를) 게임씬에 만들지 못했습니다.";
                return false;
            }

            preparedStaff.Add(staff);
            diagnostics.Add(DescribeActor($"prepared-{i + 1}", staff));
        }

        if (!TrySpawnStarterSupplies(out string starterSupplyFailure))
        {
            DestroyPreparedStaff(preparedStaff);
            message = starterSupplyFailure;
            return false;
        }

        manager.SelectOwner(ownerData, snapshot.owner.displayName);
        CharacterActor owner = manager.CurrentOwnerActor;
        if (owner == null || owner.Progression == null)
        {
            DestroyPreparedStaff(preparedStaff);
            message = "사장 캐릭터를 게임씬에 만들지 못했습니다.";
            return false;
        }

        owner.Progression.RestorePersistentState(snapshot.owner.ToProgressionSnapshot());
        owner.gameObject.name = owner.Progression.GrowthState.displayName;
        PlaceParty(owner, preparedStaff, spawner);
        FocusCameraOnStarterDungeon();
        RemoveDuplicateStartingStaff(preparedStaff, diagnostics);
        foreach (CharacterActor staff in preparedStaff)
        {
            staff.PrepareForPersistentRestore();
            characterObjectFactory.Publish(staff.gameObject);
            staff.SetLifecycleState(CharacterLifecycleState.Active);
            staff.characterType = CharacterType.NPC;
            staff.Brain?.UseStaffWorkActions();
            staff.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        RemoveDuplicateStartingStaff(preparedStaff, diagnostics);
        diagnostics.AddRange(preparedStaff.Select(actor => DescribeActor("retained", actor)));
        LastReport = string.Join(" || ", diagnostics);
        message = "준비한 사장과 직원으로 새 런을 시작했습니다.";
        return true;
    }

    private bool TrySpawnStarterSupplies(out string failureReason)
    {
        DungeonPhysicalItemSaveData beforeSpawn = itemStackRuntime.Capture();
        foreach ((StockCategory category, int amount) in StarterSupplies)
        {
            if (itemStackRuntime.SpawnStockAtDropoff(
                    category,
                    amount,
                    "시작 보급품",
                    out int spawned)
                && spawned == amount)
            {
                continue;
            }

            itemStackRuntime.Restore(beforeSpawn);
            failureReason = $"시작 보급품을 하차장에 놓지 못했습니다. 항목: {category}";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private void EnsureStarterDungeonShell()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        DungeonStoryGridBuildingController controller;
        try
        {
            controller = gridBuildingControllerProvider.Controller;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Starter dungeon shell skipped: {exception.Message}");
            return;
        }

        if (controller == null)
        {
            return;
        }

        controller.HasAnyPlacedGridOccupants();
        if (HasUsableStarterDungeon(grid))
        {
            return;
        }

        List<InitialBuildInfo> placements = CreateStarterDungeonShell(grid);
        if (placements.Count == 0)
        {
            return;
        }

        if (controller.TryPlaceInitialBuildings(placements, out string message))
        {
            Debug.Log(message);
        }
    }

    private List<InitialBuildInfo> CreateStarterDungeonShell(Grid grid)
    {
        List<InitialBuildInfo> placements = new List<InitialBuildInfo>();
        BuildingSO hallway = buildingCatalog.RequireDefinition(StarterBuildingDefinitionIds.Hallway);
        BuildingSO wall = buildingCatalog.RequireDefinition(StarterBuildingDefinitionIds.Wall);
        BuildingSO door = buildingCatalog.RequireDefinition(StarterBuildingDefinitionIds.Door);

        if (grid == null || hallway == null || wall == null || door == null)
        {
            Debug.LogWarning("Starter dungeon shell resources are missing.");
            return placements;
        }

        GridCell entranceCell = grid.GetCells()
            .Where(cell => cell != null && cell.AreaType == GridCellAreaType.Entrance)
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        Vector2Int entrance = entranceCell != null
            ? entranceCell.Position
            : new Vector2Int(Mathf.Clamp(4, 0, grid.width - 1), 0);
        if (!grid.IsValidGridPos(entrance))
        {
            entrance = new Vector2Int(Mathf.Clamp(4, 0, grid.width - 1), 0);
        }

        int floorY = Mathf.Clamp(entrance.y, 0, grid.height - 1);
        int interiorStartX = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && cell.Position.y == floorY)
            .Select(cell => (int?)cell.Position.x)
            .OrderBy(x => x)
            .FirstOrDefault() ?? Mathf.Clamp(entrance.x, 0, grid.width - 1);
        int shellEndX = Mathf.Min(grid.width - 2, interiorStartX + 11);
        int topY = Mathf.Min(grid.height - 1, floorY + 1);
        for (int y = floorY; y <= topY; y++)
        {
            for (int x = interiorStartX; x <= shellEndX; x++)
            {
                placements.Add(new InitialBuildInfo
                {
                    Position = new Vector2Int(x, y),
                    Building = hallway
                });
            }
        }

        placements.Add(new InitialBuildInfo
        {
            Position = entrance,
            Building = door
        });

        int rightWallX = Mathf.Clamp(shellEndX + 1, 0, grid.width - 1);
        for (int y = floorY; y <= topY; y++)
        {
            if (!grid.IsValidGridPos(new Vector2Int(rightWallX, y)))
            {
                continue;
            }

            placements.Add(new InitialBuildInfo
            {
                Position = new Vector2Int(rightWallX, y),
                Building = wall
            });
        }

        return placements;
    }

    private static bool HasUsableStarterDungeon(Grid grid)
    {
        if (grid == null)
        {
            return false;
        }

        int interiorHallwayCount = 0;
        bool hasInteriorContent = false;
        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null || cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                continue;
            }

            if (cell.HasBuildingInLayer(GridLayer.Hallway))
            {
                interiorHallwayCount++;
            }

            BuildableObject building = cell.GetOccupant(GridLayer.Building) as BuildableObject;
            if (building != null
                && building.BuildingData != null
                && !building.BuildingData.IsStructuralWall
                && !building.BuildingData.IsDoor)
            {
                hasInteriorContent = true;
            }
        }

        return interiorHallwayCount >= 8 && hasInteriorContent;
    }

    private void FocusCameraOnStarterDungeon()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        Camera camera;
        try
        {
            camera = mainCameraProvider.Camera;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Starter dungeon camera focus skipped: {exception.Message}");
            return;
        }

        if (camera == null)
        {
            return;
        }

        if (!TryGetStarterDungeonCenter(grid, out Vector3 center))
        {
            return;
        }

        camera.transform.position = new Vector3(center.x, center.y, -10f);
        CameraManager cameraManager = camera.GetComponent<CameraManager>();
        cameraManager?.ClampToCurrentBounds();
    }

    private static bool TryGetStarterDungeonCenter(Grid grid, out Vector3 center)
    {
        center = Vector3.zero;
        if (grid == null)
        {
            return false;
        }

        List<Vector2Int> occupiedInterior = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && cell.HasOccupant())
            .Select(cell => cell.Position)
            .ToList();
        if (occupiedInterior.Count == 0)
        {
            occupiedInterior = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.DungeonInterior
                    && cell.Position.y == 0)
                .Select(cell => cell.Position)
                .ToList();
        }

        if (occupiedInterior.Count == 0)
        {
            return false;
        }

        float minX = occupiedInterior.Min(pos => pos.x);
        float maxX = occupiedInterior.Max(pos => pos.x);
        float maxY = occupiedInterior.Max(pos => pos.y);
        Vector3 world = grid.GetWorldPos(new Vector2((minX + maxX) * 0.5f, maxY));
        center = new Vector3(world.x, world.y + 2.2f, world.z);
        return true;
    }

    private CharacterActor CreateStaffActor(
        CharacterSO staffData,
        PreparedStartPartyMemberSnapshot snapshot,
        CharacterSpawner spawner)
    {
        GameObject staffObject = characterObjectFactory.CreateInactive(
            spawner.characterPrefab,
            EnsureStaffWorkAbility);
        CharacterActor actor = CharacterActorCollection.GetCanonical(
            staffObject.GetComponent<CharacterActor>());
        if (actor == null)
        {
            characterObjectFactory.Destroy(staffObject);
            return null;
        }

        actor.Initialize(staffData);
        actor.Identity.SetPersistentId(snapshot.persistentId);
        actor.characterType = CharacterType.NPC;
        actor.Progression.RestorePersistentState(snapshot.ToProgressionSnapshot());
        actor.gameObject.name = actor.Progression.GrowthState.displayName;
        actor.RefreshAbilityCache();
        if (actor.TryGetAbility(out AbilityWork _))
        {
            return actor;
        }

        characterObjectFactory.Destroy(staffObject);
        return null;
    }

    private static void EnsureStaffWorkAbility(GameObject staffObject)
    {
        if (staffObject != null && staffObject.GetComponent<AbilityWork>() == null)
        {
            staffObject.AddComponent<AbilityWork>();
        }
    }

    private void PlaceParty(
        CharacterActor owner,
        IReadOnlyList<CharacterActor> staff,
        CharacterSpawner spawner)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            for (int i = 0; i < staff.Count; i++)
            {
                staff[i].transform.position = spawner.GetEntryDoorWorldPosition() + Vector3.right * (i + 1);
            }

            return;
        }

        Vector2Int ownerPosition = grid.GetXY(owner.transform.position);
        HashSet<Vector2Int> used = new HashSet<Vector2Int> { ownerPosition };
        List<Vector2Int> walkable = grid.GetCells()
            .Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .Select(cell => cell.Position)
            .OrderBy(position => position.y == ownerPosition.y ? 0 : 1)
            .ThenBy(position => Mathf.Abs(position.x - ownerPosition.x) + Mathf.Abs(position.y - ownerPosition.y))
            .ThenBy(position => position.x)
            .ToList();
        foreach (CharacterActor actor in staff)
        {
            Vector2Int? available = walkable
                .Where(candidate => !used.Contains(candidate))
                .Select(candidate => (Vector2Int?)candidate)
                .FirstOrDefault();
            if (!available.HasValue)
            {
                actor.transform.position = spawner.GetEntryDoorWorldPosition();
                continue;
            }

            Vector2Int position = available.Value;
            used.Add(position);
            actor.transform.position = grid.GetWorldPos(position);
        }
    }

    private void RemoveDuplicateStartingStaff(
        IReadOnlyCollection<CharacterActor> preparedStaff,
        ICollection<string> diagnostics)
    {
        CharacterActor[] retained = preparedStaff?
            .Where(actor => actor != null)
            .ToArray() ?? Array.Empty<CharacterActor>();
        HashSet<int> retainedGameObjectIds = new HashSet<int>(retained
            .Select(actor => actor.gameObject.GetInstanceID()));
        HashSet<string> retainedIds = new HashSet<string>(retained
            .Select(actor => actor.Identity?.PersistentId)
            .Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        IReadOnlyList<CharacterActor> actors =
            CharacterActorCollection.DistinctByGameObject(
                characterLifetimeQuery.AllCharacters);
        foreach (CharacterActor actor in actors)
        {
            if (actor == null)
            {
                continue;
            }

            string persistentId = actor.Identity?.PersistentId ?? string.Empty;
            bool retainedActor = retainedGameObjectIds.Contains(actor.gameObject.GetInstanceID());
            if (persistentId.StartsWith("character:staff:", StringComparison.Ordinal))
            {
                diagnostics?.Add(
                    $"scan:name={actor.name};go={actor.gameObject.GetInstanceID()};id={persistentId};active={actor.gameObject.activeInHierarchy};retained={retainedActor}");
            }

            if (retainedActor
                || !retainedIds.Contains(persistentId)
                || !persistentId.StartsWith("character:staff:", StringComparison.Ordinal))
            {
                continue;
            }

            actor.gameObject.SetActive(false);
            characterObjectFactory.Destroy(actor.gameObject);
        }
    }

    private void DestroyPreparedStaff(IEnumerable<CharacterActor> preparedStaff)
    {
        foreach (CharacterActor actor in preparedStaff ?? Array.Empty<CharacterActor>())
        {
            if (actor != null)
            {
                characterObjectFactory.Destroy(actor.gameObject);
            }
        }
    }

    private static string DescribeActor(string phase, CharacterActor actor)
    {
        return actor == null
            ? $"{phase}:null"
            : $"{phase}:name={actor.name};go={actor.gameObject.GetInstanceID()};id={actor.Identity?.PersistentId};active={actor.gameObject.activeInHierarchy}";
    }
}
