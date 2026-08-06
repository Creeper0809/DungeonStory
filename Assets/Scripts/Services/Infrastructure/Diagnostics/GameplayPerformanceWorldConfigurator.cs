using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

internal sealed class GameplayPerformanceWorldConfigurator : IDisposable
{
    private const int VisibleStressActorCount = 96;

    private readonly GameplayPerformanceOptions options;
    private readonly GameplayPerformanceReport report;
    private readonly List<ScriptableObject> runtimeDefinitions =
        new List<ScriptableObject>();
    private int nextHallwayOccupantId = -500000;
    private int nextStairOccupantId = -600000;

    public GameplayPerformanceWorldConfigurator(
        GameplayPerformanceOptions options,
        GameplayPerformanceReport report)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public void Dispose()
    {
        for (int index = 0; index < runtimeDefinitions.Count; index++)
        {
            ScriptableObject definition = runtimeDefinitions[index];
            if (definition != null)
            {
                UnityEngine.Object.Destroy(definition);
            }
        }

        runtimeDefinitions.Clear();
    }

public IEnumerator ConfigureMeasuredWorld()
{
    Scene scene = SceneManager.GetActiveScene();
    DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
    GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
    CharacterSpawner spawner = FindSceneComponent<CharacterSpawner>(scene);
    if (scope == null || scope.Container == null || gridSystem == null || spawner == null)
    {
        throw new InvalidOperationException("Required gameplay runtime services are missing.");
    }

    scope.Container.Resolve<IDungeonDebugModeService>().ResetTransientState();

    Grid grid = gridSystem.grid;
    if (options.GridWidth > grid.width || options.GridHeight > grid.height)
    {
        LogStage($"expand-grid:{grid.width}x{grid.height}");
        gridSystem.GridExpand(
            Mathf.Max(0, options.GridWidth - grid.width),
            Mathf.Max(0, options.GridHeight - grid.height));
        grid = gridSystem.grid;
        RebindExistingBuildings(scene, grid);
        LogStage($"expand-grid-complete:{grid.width}x{grid.height}");
    }

    if (options.FacilityCount > 0)
    {
        LogStage($"dense-dungeon:{options.FacilityCount}");
        yield return ConfigureDenseDungeon(scope, gridSystem, grid);
        LogStage($"dense-dungeon-complete:{report.actualDenseFacilityCount}");
    }

    LogStage($"spawn-actors:{options.ActorCount}");
    yield return SpawnStressCharacters(scope, spawner, grid);
    LogStage($"spawn-actors-complete:{report.actualStressActorCount}");
    if (options.LivestockCount > 0)
    {
        LogStage($"spawn-livestock:{options.LivestockCount}");
        yield return SpawnStressLivestock(scope, grid);
        LogStage(
            $"spawn-livestock-complete:{report.actualStressLivestockCount}");
    }

    if (options.NormalOperationSupplyDays > 0)
    {
        SeedNormalOperationSupplies(scope);
    }

    gridSystem.NotifyGridObjectChanged();
    scope.Container.Resolve<IFacilityCandidateCache>().Clear();
    yield return null;
}

private void SeedNormalOperationSupplies(
    DungeonRuntimeLifetimeScope scope)
{
    IWorldItemStackRuntime itemRuntime =
        scope.Container.Resolve<IWorldItemStackRuntime>();
    IWarehouseWorldQuery warehouseWorld =
        scope.Container.Resolve<IWarehouseWorldQuery>();
    int population = Mathf.Max(1, options.ActorCount);
    int requestedPerCategory = population
        * Mathf.Max(1, options.NormalOperationSupplyDays);

    List<IWarehouseFacility> warehouses = warehouseWorld.Warehouses
        .Where(warehouse =>
            warehouse?.Inventory != null
            && warehouse.HasWarehouseInventory)
        .ToList();
    int warehouseFoodAmount = SeedWarehouseStock(
        itemRuntime,
        warehouses,
        StockCategory.Food,
        requestedPerCategory);
    int warehouseWaterAmount = SeedWarehouseStock(
        itemRuntime,
        warehouses,
        StockCategory.Water,
        requestedPerCategory);
    int looseFoodAmount = 0;
    int looseWaterAmount = 0;
    int foodAmount = warehouseFoodAmount;
    int waterAmount = warehouseWaterAmount;

    if (foodAmount < requestedPerCategory
        && itemRuntime.SpawnStockAtDropoff(
            StockCategory.Food,
            requestedPerCategory - foodAmount,
            "성능 검증용 정상 배급",
            out looseFoodAmount))
    {
        foodAmount += looseFoodAmount;
    }

    if (waterAmount < requestedPerCategory
        && itemRuntime.SpawnStockAtDropoff(
            StockCategory.Water,
            requestedPerCategory - waterAmount,
            "성능 검증용 정상 배급",
            out looseWaterAmount))
    {
        waterAmount += looseWaterAmount;
    }

    report.normalOperationSupplyDays =
        options.NormalOperationSupplyDays;
    report.normalOperationWarehouseCount = warehouses.Count;
    report.seededWarehouseFoodAmount = warehouseFoodAmount;
    report.seededWarehouseWaterAmount = warehouseWaterAmount;
    report.seededLooseFoodAmount = looseFoodAmount;
    report.seededLooseWaterAmount = looseWaterAmount;
    report.seededFoodAmount = foodAmount;
    report.seededWaterAmount = waterAmount;
    LogStage(
        $"seed-supplies:food={foodAmount};water={waterAmount};"
        + $"warehouses={warehouses.Count};"
        + $"warehouseFood={warehouseFoodAmount};"
        + $"warehouseWater={warehouseWaterAmount};"
        + $"looseFood={looseFoodAmount};"
        + $"looseWater={looseWaterAmount};"
        + $"days={options.NormalOperationSupplyDays}");

    if (foodAmount < requestedPerCategory
        || waterAmount < requestedPerCategory)
    {
        throw new InvalidOperationException(
            "Normal-operation supplies were incomplete: "
            + $"food={foodAmount}/{requestedPerCategory}, "
            + $"water={waterAmount}/{requestedPerCategory}.");
    }
}

private static int SeedWarehouseStock(
    IWorldItemStackRuntime itemRuntime,
    IReadOnlyList<IWarehouseFacility> warehouses,
    StockCategory category,
    int requested)
{
    int spawned = 0;
    for (int index = 0;
         index < warehouses.Count && spawned < requested;
         index++)
    {
        int remainingWarehouses = warehouses.Count - index;
        int share = Mathf.CeilToInt(
            (requested - spawned)
            / (float)Mathf.Max(1, remainingWarehouses));
        itemRuntime.SpawnStockInWarehouse(
            warehouses[index],
            category,
            share,
            out int accepted);
        spawned += accepted;
    }

    return spawned;
}

public void ApplyDiagnosticIsolation()
{
    Scene scene = SceneManager.GetActiveScene();
    report.aiSchedulerDisabled = options.DisableAiScheduler;
    report.characterPresentationDisabled = options.DisableCharacterPresentation;
    report.characterStatsUpdatesDisabled = options.DisableCharacterStatsUpdates;

    foreach (OwnerSelectionPanel panel in
             FindSceneComponents<OwnerSelectionPanel>(scene))
    {
        if (panel == null)
        {
            continue;
        }

        panel.RefreshVisibility();
        if (panel.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                "Owner selection remained visible after the prepared run was applied.");
        }
    }

    if (options.DisableAiScheduler)
    {
        foreach (CharacterAiScheduler scheduler in
                 FindSceneComponents<CharacterAiScheduler>(scene))
        {
            if (scheduler != null)
            {
                scheduler.enabled = false;
            }
        }
    }

    if (options.DisableCharacterPresentation)
    {
        foreach (WorldCharacterNameplate nameplate in
                 FindSceneComponents<WorldCharacterNameplate>(scene))
        {
            if (nameplate != null)
            {
                nameplate.enabled = false;
            }
        }

        foreach (CharacterDialogueRuntime dialogue in
                 FindSceneComponents<CharacterDialogueRuntime>(scene))
        {
            if (dialogue != null)
            {
                dialogue.enabled = false;
            }
        }
    }

    if (options.DisableCharacterStatsUpdates)
    {
        foreach (CharacterStats stats in
                 FindSceneComponents<CharacterStats>(scene))
        {
            if (stats != null)
            {
                stats.enabled = false;
            }
        }
    }

    if (options.HasDiagnosticIsolation)
    {
        LogStage(
            "diagnostic-isolation:"
            + $"ai={options.DisableAiScheduler},"
            + $"presentation={options.DisableCharacterPresentation},"
            + $"stats={options.DisableCharacterStatsUpdates}");
    }
}

private IEnumerator ConfigureDenseDungeon(
    DungeonRuntimeLifetimeScope scope,
    GridSystemManager gridSystem,
    Grid grid)
{
    int activeFloors = Mathf.Clamp(options.ActiveFloors, 1, grid.height);
    List<Vector2Int> missingHallwayCells =
        new List<Vector2Int>(grid.width * activeFloors);
    for (int y = 0; y < activeFloors; y++)
    {
        for (int x = 0; x < grid.width; x++)
        {
            Vector2Int position = new Vector2Int(x, y);
            GridCell cell = grid.GetGridCell(position);
            cell.SetAreaType(GridCellAreaType.DungeonInterior);
            if (!cell.HasOccupantInLayer(GridLayer.Hallway))
            {
                missingHallwayCells.Add(position);
            }
        }
    }

    if (missingHallwayCells.Count > 0)
    {
        bool registered = grid.RegisterOccupant(
            new PerformanceHallwayOccupant(nextHallwayOccupantId--),
            GridLayer.Hallway,
            missingHallwayCells,
            false);
        if (!registered)
        {
            throw new InvalidOperationException(
                "Dense gameplay floor cells could not be registered.");
        }
    }

    RegisterTraversalColumn(grid, 0, activeFloors);
    RegisterTraversalColumn(grid, grid.width - 1, activeFloors);
    grid.RefreshTraversalHeuristicMetadata();

    IDataCatalog catalog = scope.Container.Resolve<IDataCatalog>();
    List<BuildingSO> baseFacilityDefinitions =
        SelectDenseFacilityDefinitions(
            catalog.GetData<BuildingSO>().Values,
            12)
        .Select(CloneWithoutRoomRequirement)
        .ToList();
    List<BuildingSO> facilityDefinitions =
        BuildDenseFacilityPlacementSequence(baseFacilityDefinitions);
    BuildingSO doorDefinition = catalog.GetData<BuildingSO>()
        .Values
        .Where(definition => definition != null && definition.IsInteriorDoor)
        .OrderBy(definition => definition.id)
        .Select(CloneDefinition)
        .FirstOrDefault();

    if (facilityDefinitions.Count == 0)
    {
        throw new InvalidOperationException(
            "No independently rendered modular facility definitions were found.");
    }

    GridTexture gridTexture = FindSceneComponent<GridTexture>(
        SceneManager.GetActiveScene());
    GridBuildingFactory buildingFactory = new GridBuildingFactory(gridTexture);
    int placedDoors = 0;
    if (doorDefinition != null)
    {
        for (int floor = 0; floor < activeFloors; floor++)
        {
            for (int x = options.RoomSpan; x < grid.width; x += options.RoomSpan)
            {
                if (TryPlaceBuilding(
                        scope,
                        buildingFactory,
                        grid,
                        doorDefinition,
                        new Vector2Int(x, floor),
                        out _))
                {
                    placedDoors++;
                }

                if ((placedDoors & 255) == 0)
                {
                    yield return null;
                }
            }
        }
    }

    int placedFacilities = 0;
    int slotSequence = 0;
    int[] slotOffsets = { 2, 6, 10, 13 };
    int roomCount = Mathf.CeilToInt(grid.width / (float)options.RoomSpan);
    int baseFacilitiesPerFloor =
        options.FacilityCount / activeFloors;
    int facilityFloorRemainder =
        options.FacilityCount % activeFloors;
    for (int floor = 0; floor < activeFloors; floor++)
    {
        int floorTarget = baseFacilitiesPerFloor
            + (floor < facilityFloorRemainder ? 1 : 0);
        int placedOnFloor = 0;
        for (int pass = 0;
             pass < slotOffsets.Length && placedOnFloor < floorTarget;
             pass++)
        {
            for (int roomOrdinal = 0;
                 roomOrdinal < roomCount && placedOnFloor < floorTarget;
                 roomOrdinal++)
            {
                int roomIndex =
                    (roomOrdinal * 5 + floor * 3) % roomCount;
                int roomStart = roomIndex * options.RoomSpan;
                int slotOffset = slotOffsets[
                    (pass + roomOrdinal + floor) % slotOffsets.Length];
                int x = roomStart + slotOffset;
                if (x + 1 >= Mathf.Min(grid.width, roomStart + options.RoomSpan))
                {
                    continue;
                }

                BuildingSO definition =
                    facilityDefinitions[slotSequence % facilityDefinitions.Count];
                slotSequence++;
                if (TryPlaceBuilding(
                        scope,
                        buildingFactory,
                        grid,
                        definition,
                        new Vector2Int(x, floor),
                        out _))
                {
                    placedFacilities++;
                    placedOnFloor++;
                }

                if ((slotSequence & 127) == 0)
                {
                    yield return null;
                }
            }
        }
    }

    // Existing scene fixtures can occupy a few of the regular stress slots.
    // Fill those holes without increasing the configured active floor count.
    for (int floor = 0;
         floor < activeFloors && placedFacilities < options.FacilityCount;
         floor++)
    {
        for (int roomStart = 0;
             roomStart < grid.width && placedFacilities < options.FacilityCount;
             roomStart += options.RoomSpan)
        {
            int x = roomStart + 4;
            if (x + 1 >= Mathf.Min(grid.width, roomStart + options.RoomSpan))
            {
                continue;
            }

            BuildingSO definition =
                facilityDefinitions[slotSequence % facilityDefinitions.Count];
            slotSequence++;
            if (TryPlaceBuilding(
                    scope,
                    buildingFactory,
                    grid,
                    definition,
                    new Vector2Int(x, floor),
                    out _))
            {
                placedFacilities++;
            }

            if ((slotSequence & 127) == 0)
            {
                yield return null;
            }
        }
    }

    report.actualDenseFacilityCount = placedFacilities;
    report.actualDenseDoorCount = placedDoors;
    if (placedFacilities < options.FacilityCount)
    {
        throw new InvalidOperationException(
            $"Dense facility capacity was exhausted: requested={options.FacilityCount}, "
            + $"placed={placedFacilities}.");
    }

    gridSystem.NotifyGridObjectChanged();
}

private IEnumerator SpawnStressCharacters(
    DungeonRuntimeLifetimeScope scope,
    CharacterSpawner spawner,
    Grid grid)
{
    ICharacterSkillGenerationService skillGenerationService =
        scope.Container.Resolve<ICharacterSkillGenerationService>();
    CharacterActor[] existing = FindSceneComponents<CharacterActor>(
        SceneManager.GetActiveScene());
    int existingRequestsCancelled = 0;
    if (options.IsEditorProfile)
    {
        foreach (CharacterActor actor in existing)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy)
            {
                continue;
            }

            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            skillGenerationService.CancelRequests(actor.Progression);
            existingRequestsCancelled++;
        }
    }
    report.preexistingSkillGenerationRequestsCancelled =
        existingRequestsCancelled;

    int requestedTotal = options.ActorCount <= 0
        ? existing.Count(actor => actor != null && actor.gameObject.activeInHierarchy)
        : options.ActorCount;
    int activeCount = existing.Count(actor =>
        actor != null && actor.gameObject.activeInHierarchy);
    if (activeCount >= requestedTotal)
    {
        report.actualStressActorCount = 0;
        yield break;
    }

    CharacterSO source = spawner.characters?.FirstOrDefault(character => character != null);
    if (source == null)
    {
        throw new InvalidOperationException(
            "CharacterSpawner has no real character definition for the gameplay profile.");
    }

    CharacterSO stressDefinition = UnityEngine.Object.Instantiate(source);
    stressDefinition.hideFlags = HideFlags.HideAndDontSave;
    stressDefinition.characterType = CharacterType.NPC;
    stressDefinition.characterName = "성능 측정 인원";
    runtimeDefinitions.Add(stressDefinition);
    ICharacterSpawnObjectFactory characterObjectFactory =
        scope.Container.Resolve<ICharacterSpawnObjectFactory>();

    int created = 0;
    while (activeCount < requestedTotal)
    {
        GameObject actorObject = spawner.characterPool.Get();
        if (actorObject != null && actorObject.GetComponent<AbilityWork>() == null)
        {
            AbilityWork work = actorObject.AddComponent<AbilityWork>();
            characterObjectFactory.InjectAddedAbility(work);
        }

        CharacterActor actor = actorObject != null
            ? actorObject.GetComponent<CharacterActor>()
            : null;
        if (actor == null)
        {
            throw new InvalidOperationException(
                "The real character pool returned an object without CharacterActor.");
        }

        actor.characterType = CharacterType.NPC;
        actor.RefreshAbilityCache();
        actor.Initialize(stressDefinition);
        skillGenerationService.CancelRequests(actor.Progression);
        actor.Identity?.SetPersistentId($"character:perf:{options.ProfileId}:{created:D5}");
        actor.Identity?.SetCharacterType(CharacterType.NPC);
        actor.Brain?.UseStaffWorkActions();
        actor.transform.position = grid.GetWorldPos(GetStressActorPosition(
            created,
            grid,
            Mathf.Clamp(options.ActiveFloors, 1, grid.height)));
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        characterObjectFactory.Publish(actorObject);
        created++;
        activeCount++;

        if ((created & 31) == 0)
        {
            if (created % 128 == 0)
            {
                LogStage($"spawn-actors-progress:{activeCount}/{requestedTotal}");
            }

            yield return null;
        }
    }

    report.actualStressActorCount = created;
    report.syntheticSkillGenerationRequestsCancelled = created > 0;
}

private IEnumerator SpawnStressLivestock(
    DungeonRuntimeLifetimeScope scope,
    Grid grid)
{
    IDataCatalog dataCatalog = scope.Container.Resolve<IDataCatalog>();
    IWildlifeRuntime wildlife = scope.Container.Resolve<IWildlifeRuntime>();
    IWildlifeCaptureRuntime capture =
        scope.Container.Resolve<IWildlifeCaptureRuntime>();
    IAnimalHusbandryQuery husbandryQuery =
        scope.Container.Resolve<IAnimalHusbandryQuery>();
    IAnimalHusbandryCommand husbandryCommands =
        scope.Container.Resolve<IAnimalHusbandryCommand>();
    IAnimalHusbandryPersistence husbandryPersistence =
        scope.Container.Resolve<IAnimalHusbandryPersistence>();
    IWildlifeSpeciesCatalogProvider speciesCatalog =
        scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();

    BuildingSO penSource = dataCatalog.GetData<BuildingSO>()
        .Values
        .Where(definition => definition?.GetBeastPenAbility() != null)
        .OrderBy(definition => definition.id)
        .FirstOrDefault();
    if (penSource == null)
    {
        throw new InvalidOperationException(
            "The gameplay profile could not find a real livestock pen definition.");
    }

    BuildingSO penDefinition = CloneWithoutRoomRequirement(penSource);
    penDefinition.objectName = "성능 측정 대형 우리";
    BuildingBeastPenAbility penAbility =
        penDefinition.GetAbility<BuildingBeastPenAbility>();
    penAbility.capacity = Mathf.Max(
        options.LivestockCount,
        penAbility.capacity);
    penAbility.baseSecurity = 100f;
    penAbility.dailyFood = 0f;
    penAbility.dailyWater = 0f;

    GridBuildingFactory factory = new GridBuildingFactory();
    BuildableObject pen = null;
    foreach (GridCell cell in grid.GetCells()
                 .Where(cell =>
                     cell != null
                     && cell.Position.y < options.ActiveFloors)
                 .OrderBy(cell => cell.Position.y)
                 .ThenBy(cell => cell.Position.x))
    {
        if (TryPlaceBuilding(
                scope,
                factory,
                grid,
                penDefinition,
                cell.Position,
                out pen))
        {
            break;
        }
    }

    if (pen == null)
    {
        throw new InvalidOperationException(
            "The gameplay profile could not place its livestock pen.");
    }

    WildlifeSpeciesDefinition species = speciesCatalog.All
        .Where(candidate => candidate != null && candidate.CanEnterDungeon)
        .OrderBy(candidate => candidate.SpeciesId, StringComparer.Ordinal)
        .FirstOrDefault();
    if (species == null)
    {
        throw new InvalidOperationException(
            "The gameplay profile needs at least one dungeon-capable livestock species.");
    }

    List<WildlifeActor> spawnedAnimals =
        new List<WildlifeActor>(options.LivestockCount);
    GridCell[] spawnCells = grid.GetCells()
        .Where(cell =>
            cell != null
            && cell.Position.y < options.ActiveFloors
            && cell.AreaType != GridCellAreaType.BlockedExterior
            && grid.IsWalkable(cell.Position))
        .OrderBy(cell => cell.Position.y)
        .ThenBy(cell => cell.Position.x)
        .ToArray();
    if (spawnCells.Length == 0)
    {
        throw new InvalidOperationException(
            "The gameplay profile could not find a walkable livestock spawn cell.");
    }

    int spawnCursor = 0;
    int attempts = 0;
    int maximumAttempts = Mathf.Max(
        options.LivestockCount * 8,
        spawnCells.Length * 2);
    while (spawnedAnimals.Count < options.LivestockCount
        && attempts < maximumAttempts)
    {
        Vector2Int position = spawnCells[
            spawnCursor++ % Mathf.Max(1, spawnCells.Length)].Position;
        attempts++;
        if (!wildlife.TrySpawnDomesticBirth(
                species.SpeciesId,
                position,
                out WildlifeActor actor,
                out _)
            || actor == null)
        {
            continue;
        }

        spawnedAnimals.Add(actor);
        if ((spawnedAnimals.Count & 15) == 0)
        {
            LogStage(
                $"spawn-livestock-progress:{spawnedAnimals.Count}/"
                + $"{options.LivestockCount}");
            yield return null;
        }
    }

    if (spawnedAnimals.Count < options.LivestockCount)
    {
        throw new InvalidOperationException(
            $"The gameplay profile could only spawn {spawnedAnimals.Count}/"
            + $"{options.LivestockCount} real livestock actors.");
    }

    string penId = pen.RequirePersistentInstanceId().Value;
    HashSet<string> capturedIds = new HashSet<string>(
        capture.Capture().Select(state => state.wildlifeId),
        StringComparer.Ordinal);
    foreach (WildlifeActor actor in spawnedAnimals)
    {
        if (actor == null || !capturedIds.Add(actor.WildlifeId))
        {
            continue;
        }
        if (!capture.TryRegisterPenBorn(
                actor,
                penId,
                actor.GridPosition,
                out string registrationFailure))
        {
            throw new InvalidOperationException(
                $"Livestock '{actor.WildlifeId}' registration failed: "
                + registrationFailure);
        }
    }

    DungeonAnimalHusbandrySaveData husbandrySnapshot =
        husbandryPersistence.Capture();
    husbandryPersistence.Restore(
        husbandryPersistence.BuildRestore(husbandrySnapshot));
    AnimalPenPolicyData policy = husbandryQuery.GetPenPolicy(
        new BuildingInstanceId(penId));
    policy.maximumAnimals = Mathf.Max(
        options.LivestockCount,
        policy.maximumAnimals);
    policy.allowCarnivores = true;
    policy.allowScavengers = true;
    policy.allowRiskyMixing = true;
    policy.adultFemaleLimit = options.LivestockCount;
    policy.adultMaleLimit = options.LivestockCount;
    policy.juvenileLimit = options.LivestockCount;
    if (!husbandryCommands.SetPenPolicy(
            policy,
            out AnimalHusbandryFailure policyFailure))
    {
        throw new InvalidOperationException(
            $"The performance livestock policy was rejected: "
            + $"{policyFailure.Code} ({string.Join(",", policyFailure.Parameters)})");
    }

    report.actualStressLivestockCount = spawnedAnimals.Count;
    yield return null;
}


private static bool IsDenseFacilityDefinition(BuildingSO definition)
{
    if (definition == null
        || definition.IsWall
        || definition.IsDoor
        || definition.sprite == null
        || (definition.Placement.Layer != GridLayer.Building
            && !definition.UsesIndependentRenderer)
        || !definition.runtimeArchetype.IsDefined())
    {
        return false;
    }

    GridBuildingPlacement placement = definition.Placement;
    bool isFacilityLayer = placement.Layer == GridLayer.Building
        || placement.Layer == GridLayer.WallFixture
        || placement.Layer == GridLayer.CeilingFixture
        || placement.Layer == GridLayer.FloorOverlay;
    return isFacilityLayer
        && placement.Width >= 1
        && placement.Width <= 2
        && placement.Height == 1;
}

private static IReadOnlyList<BuildingSO> SelectDenseFacilityDefinitions(
    IEnumerable<BuildingSO> source,
    int requestedCount)
{
    List<BuildingSO> all = (source ?? Enumerable.Empty<BuildingSO>())
        .Where(IsDenseFacilityDefinition)
        .OrderBy(definition => definition.id)
        .ToList();
    int targetCount = Mathf.Clamp(requestedCount, 0, all.Count);
    List<BuildingSO> selected = new List<BuildingSO>(targetCount);

    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingStorageAbility>() != null);
    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingWaterSourceAbility>() != null);
    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingCropPlotAbility>() != null);
    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingProductionAbility>() != null);
    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingNeedRecoveryAbility>() != null);
    AddFirstDenseFacility(
        all,
        selected,
        definition =>
            definition.GetAbility<BuildingButcherAbility>() != null);

    int sampleIndex = 0;
    while (selected.Count < targetCount && sampleIndex < all.Count * 2)
    {
        int index = targetCount <= 1
            ? 0
            : Mathf.RoundToInt(
                (all.Count - 1)
                * (sampleIndex % targetCount)
                / (float)(targetCount - 1));
        BuildingSO candidate = all[index];
        if (!selected.Contains(candidate))
        {
            selected.Add(candidate);
        }

        sampleIndex++;
    }

    for (int index = 0;
         index < all.Count && selected.Count < targetCount;
         index++)
    {
        if (!selected.Contains(all[index]))
        {
            selected.Add(all[index]);
        }
    }

    return selected;
}

private static List<BuildingSO> BuildDenseFacilityPlacementSequence(
    IReadOnlyList<BuildingSO> source)
{
    if (source == null || source.Count == 0)
    {
        return new List<BuildingSO>();
    }

    BuildingSO water = source.FirstOrDefault(definition =>
        definition?.GetAbility<BuildingWaterSourceAbility>() != null);
    BuildingSO storage = source.FirstOrDefault(definition =>
        definition?.GetAbility<BuildingStorageAbility>() != null);
    List<BuildingSO> remaining = source
        .Where(definition =>
            definition != null
            && definition != water
            && definition != storage)
        .ToList();
    if (remaining.Count == 0)
    {
        remaining.AddRange(source.Where(definition => definition != null));
    }

    const int SequenceLength = 16;
    List<BuildingSO> sequence =
        new List<BuildingSO>(SequenceLength);
    int remainingIndex = 0;
    for (int slot = 0; slot < SequenceLength; slot++)
    {
        bool waterSlot = slot == 0 || slot == 5 || slot == 10 || slot == 15;
        bool storageSlot = slot == 4 || slot == 12;
        if (waterSlot && water != null)
        {
            sequence.Add(water);
            continue;
        }

        if (storageSlot && storage != null)
        {
            sequence.Add(storage);
            continue;
        }

        sequence.Add(remaining[remainingIndex % remaining.Count]);
        remainingIndex++;
    }

    return sequence;
}

private static void AddFirstDenseFacility(
    IReadOnlyList<BuildingSO> source,
    ICollection<BuildingSO> destination,
    Func<BuildingSO, bool> predicate)
{
    for (int index = 0; index < source.Count; index++)
    {
        BuildingSO candidate = source[index];
        if (predicate(candidate) && !destination.Contains(candidate))
        {
            destination.Add(candidate);
            return;
        }
    }
}

private BuildingSO CloneWithoutRoomRequirement(BuildingSO source)
{
    BuildingSO clone = CloneDefinition(source);
    clone.AbilityModules.Remove<BuildingRoomRequirementAbility>();
    BuildingStorageAbility storage =
        clone.GetAbility<BuildingStorageAbility>();
    if (storage != null)
    {
        storage.allCategories = true;
        storage.capacity = Mathf.Max(storage.capacity, 512);
    }

    return clone;
}

private BuildingSO CloneDefinition(BuildingSO source)
{
    BuildingSO clone = UnityEngine.Object.Instantiate(source);
    clone.hideFlags = HideFlags.HideAndDontSave;
    runtimeDefinitions.Add(clone);
    return clone;
}

private static bool TryPlaceBuilding(
    DungeonRuntimeLifetimeScope scope,
    GridBuildingFactory factory,
    Grid grid,
    BuildingSO definition,
    Vector2Int position,
    out BuildableObject building)
{
    building = null;
    IReadOnlyList<Vector2Int> footprint = definition.GetGridPosList(position);
    if (footprint == null
        || footprint.Count == 0
        || footprint.Any(cellPosition =>
            !grid.IsValidGridPos(cellPosition)
            || !grid.GetGridCell(cellPosition).CanOccupy(definition.Placement.Layer)))
    {
        return false;
    }

    building = factory.Create(grid, definition, position);
    if (building == null)
    {
        return false;
    }

    scope.Container.Inject(building);
    building.SetGrid(grid);
    building.Initialization(definition, position);
    if (grid.RegisterOccupant(
            building,
            definition.Placement.Layer,
            footprint,
            definition.Placement.IsMovement))
    {
        return true;
    }

    UnityEngine.Object.Destroy(building.gameObject);
    building = null;
    return false;
}

private static void RebindExistingBuildings(Scene scene, Grid grid)
{
    foreach (BuildableObject building in FindSceneComponents<BuildableObject>(scene))
    {
        if (building != null)
        {
            building.SetGrid(grid);
        }
    }
}

private void RegisterTraversalColumn(Grid grid, int x, int floorCount)
{
    PerformanceStairOccupant stair =
        new PerformanceStairOccupant(nextStairOccupantId--);
    for (int y = 0; y < floorCount; y++)
    {
        List<GridTraversalLink> links = new List<GridTraversalLink>(2);
        if (y > 0)
        {
            links.Add(new GridTraversalLink(
                new Vector2Int(x, y - 1),
                stair,
                GridMoveType.Stair));
        }

        if (y + 1 < floorCount)
        {
            links.Add(new GridTraversalLink(
                new Vector2Int(x, y + 1),
                stair,
                GridMoveType.Stair));
        }

        grid.GetGridCell(new Vector2Int(x, y)).SetTraversalLinks(links);
    }
}

private static Vector2Int GetStressActorPosition(
    int index,
    Grid grid,
    int activeFloors)
{
    if (index < VisibleStressActorCount)
    {
        int localWidth = Mathf.Min(32, Mathf.Max(1, grid.width - 2));
        return new Vector2Int(
            1 + index % localWidth,
            (index / localWidth) % Mathf.Min(3, activeFloors));
    }

    int distributedIndex = index - VisibleStressActorCount;
    int x = 1 + (distributedIndex * 37) % Mathf.Max(1, grid.width - 2);
    int y = distributedIndex % activeFloors;
    return new Vector2Int(x, y);
}

private static T FindSceneComponent<T>(Scene scene) where T : Component
{
    T[] components = FindSceneComponents<T>(scene);
    return components.FirstOrDefault(component => component != null);
}

private static T[] FindSceneComponents<T>(Scene scene) where T : Component
{
    if (!scene.IsValid())
    {
        return Array.Empty<T>();
    }

    List<T> result = new List<T>();
    foreach (GameObject root in scene.GetRootGameObjects())
    {
        result.AddRange(root.GetComponentsInChildren<T>(true));
    }

    return result.ToArray();
}


private sealed class PerformanceHallwayOccupant : IGridOccupant
{
    private readonly int id;

    public PerformanceHallwayOccupant(int id)
    {
        this.id = id;
    }

    public int GridId => id;
    public bool IsGridDestroyed => false;
    public bool IsGridVisitable => true;
    public bool IsGridMovement => false;
}

private sealed class PerformanceStairOccupant :
    IGridOccupant,
    IGridMovementOccupant,
    IGridMovementHandler,
    IGridTraversalCostProvider
{
    private readonly int id;

    public PerformanceStairOccupant(int id)
    {
        this.id = id;
    }

    public int GridId => id;
    public bool IsGridDestroyed => false;
    public bool IsGridVisitable => true;
    public bool IsGridMovement => true;
    public GridMoveType GridMoveType => GridMoveType.Stair;

    public int GetTraversalCostUnits()
    {
        return DefaultGridTraversalCostPolicy.StairFallbackCost;
    }

    public IEnumerator Traverse(IBuildingVisitorPort actor, GridMoveStep step)
    {
        if (actor == null || !step.IsValid)
        {
            yield break;
        }

        if (!actor.VisitorSnapshot.CanMove)
        {
            yield break;
        }

        actor.HideForTraversal(5f);
        try
        {
            yield return actor.MoveToGrid(step.To);
        }
        finally
        {
            actor.RestoreTraversalVisibility();
        }
    }
}


    private static void LogStage(string stage)
    {
        UnityEngine.Debug.Log("[GameplayPerformanceProbe] " + stage);
    }
}
