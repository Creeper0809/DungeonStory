using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ModularFacilitySaveLoadDebugScenarios
{
    public const string ReportPath = "Temp/modular-facility-save-load-report.tsv";

    [MenuItem("DungeonStory/Debug/Modular Facilities/Run Save Load Round Trip")]
    public static void RunSaveLoadRoundTrip()
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string>
        {
            "case\tresult\tdetails"
        };

        bool success = false;
        try
        {
            success = VerifyRoundTrip(lines);
        }
        catch (Exception ex)
        {
            lines.Add($"exception\tFAIL\t{Sanitize(ex)}");
            Debug.LogException(ex);
        }

        File.WriteAllLines(ReportPath, lines);
        if (success)
        {
            Debug.Log($"Modular facility save/load round trip PASS. Report: {ReportPath}");
        }
        else
        {
            Debug.LogError($"Modular facility save/load round trip FAIL. Report: {ReportPath}");
        }
    }

    private static bool VerifyRoundTrip(List<string> lines)
    {
        Dictionary<int, BuildingSO> catalog = LoadBuildingCatalog();
        BuildingSO hallway = RequireBuilding(catalog, 0, "Hallway");
        BuildingSO diningCore = RequireCode(catalog, "D01");
        BuildingSO foodStorage = RequireCode(catalog, "D10");
        BuildingSO shopCounter = RequireCode(catalog, "S01");
        BuildingSO shopShelf = RequireCode(catalog, "S02");
        BuildingSO alarmBell = RequireCode(catalog, "G02");
        BuildingSO wallFixture = RequireCode(catalog, "D11");
        BuildingSO ceilingFixture = RequireCode(catalog, "E03");
        BuildingSO floorOverlay = RequireCode(catalog, "E04");

        Grid sourceGrid = CreateSaveFixtureGrid();
        Grid targetGrid = CreateSaveFixtureGrid();
        GameSessionState targetGameData = CreateGameData(1, 1, 0f, 0, 1, TimeOfDay.Morning);

        List<BuildableObject> sourceBuildings = new List<BuildableObject>();
        List<BuildableObject> targetStaleBuildings = new List<BuildableObject>();
        List<BuildableObject> restoredBuildings = new List<BuildableObject>();
        GameObject textureObject = new GameObject("Facility Save Contract Texture");

        try
        {
            Place(sourceGrid, hallway, new Vector2Int(1, 0), sourceBuildings);
            Place(sourceGrid, hallway, new Vector2Int(2, 0), sourceBuildings);
            Place(sourceGrid, hallway, new Vector2Int(3, 0), sourceBuildings);
            BuildableObject dining = Place(sourceGrid, diningCore, new Vector2Int(5, 0), sourceBuildings);
            Facility warehouse = Place(sourceGrid, foodStorage, new Vector2Int(8, 0), sourceBuildings) as Facility;
            Shop shop = Place(sourceGrid, shopCounter, new Vector2Int(12, 0), sourceBuildings) as Shop;
            Place(sourceGrid, shopShelf, new Vector2Int(15, 0), sourceBuildings);
            Place(sourceGrid, wallFixture, new Vector2Int(18, 0), sourceBuildings);
            Place(sourceGrid, ceilingFixture, new Vector2Int(18, 0), sourceBuildings);
            Place(sourceGrid, floorOverlay, new Vector2Int(18, 0), sourceBuildings);

            dining.RestoreFacilityState(new FacilityRuntimeState
            {
                completedUses = 7,
                completedWorkCycles = 3,
                cleanliness = 31.5f
            });
            BuildingProductionAbility diningProductionAbility =
                dining.BuildingData.GetAbility<BuildingProductionAbility>();
            dining.RequireStateModule<BuildingProductionStateModule>(
                    BuildingStateModuleIds.ForAbility(
                        "production",
                        diningProductionAbility.AbilityId))
                .SetProducedStock(5);
            dining.SetDamaged(true);
            dining.SetFacilityLevel(3);

            Require(warehouse != null && warehouse.HasWarehouseInventory, "source warehouse exists");
            warehouse.Inventory.ApplySnapshot(new WarehouseInventorySnapshot
            {
                restrictCategory = true,
                acceptedCategoryId = StockCategoryPersistenceId.ToId(StockCategory.Food)
            });

            Require(shop != null, "source shop exists");
            ShopStockStateSnapshot shopStock = shop.CreateStockSnapshot();
            Require(shopStock.schemaVersion == ShopStockStateSnapshot.CurrentSchemaVersion
                && shopStock.lots != null
                && shopStock.lots.Count > 0,
                "source shop has exact lot snapshot");
            shopStock.lots[0].quantity = 2;
            shop.ApplyStockSnapshot(shopStock);

            BuildableObject alarm = Place(sourceGrid, alarmBell, new Vector2Int(20, 0), sourceBuildings);
            BuildingSecurityAbility alarmAbility = alarm.BuildingData.GetAbility<BuildingSecurityAbility>();
            alarm.RequireStateModule<BuildingSecurityStateModule>(
                    BuildingStateModuleIds.ForAbility("security", alarmAbility.AbilityId))
                .SetAlarmCharges(2);

            Place(targetGrid, hallway, new Vector2Int(25, 0), targetStaleBuildings);
            Place(targetGrid, diningCore, new Vector2Int(22, 0), targetStaleBuildings);
            int staleWorldDestructionEventCount = 0;
            foreach (BuildableObject staleBuilding in targetStaleBuildings)
            {
                staleBuilding.OnBuildingDestroyed += () =>
                    staleWorldDestructionEventCount++;
            }

            GridTexture texture = textureObject.AddComponent<GridTexture>();
            TestGridSystemPublisher gridPublisher = new(targetGrid);
            RestoreWorldCandidateIndex candidateIndex = new();
            ModularFacilityWorldSaveService service = new ModularFacilityWorldSaveService(
                id => catalog.TryGetValue(id, out BuildingSO data) ? data : null,
                new GridBuildingObjectFactory(),
                InjectBuilding,
                new StaticGridTextureProvider(texture),
                new NoopFacilityRelocationWorldService(),
                new TestGameSessionStateStore(targetGameData),
                gridPublisher,
                candidateIndex);

            ModularFacilityWorldSaveData snapshot = service.CreateSnapshot(sourceGrid);
            string json = service.ToJson(snapshot, prettyPrint: true);
            ModularFacilityWorldSaveData parsed = service.FromJson(json);
            ModularFacilityWorldSaveData invalid = service.FromJson(json);
            ModularFacilityBuildingSaveData overlap =
                JsonUtility.FromJson<ModularFacilityBuildingSaveData>(
                    JsonUtility.ToJson(invalid.buildings[0]));
            overlap.persistentInstanceId = "building:validation-overlap";
            invalid.buildings.Add(overlap);
            ModularFacilityWorldRestoreReport invalidReport =
                service.ValidateRestore(targetGrid, invalid);
            Check(
                lines,
                "invalid_candidate_preflight",
                !invalidReport.Success
                && invalidReport.errors.Any(error =>
                    error.Contains("overlap", StringComparison.OrdinalIgnoreCase)
                    || error.Contains("cannot occupy", StringComparison.OrdinalIgnoreCase))
                && targetStaleBuildings.All(item => item != null && !item.IsGridDestroyed),
                string.Join("|", invalidReport.errors));
            ModularFacilityWorldRestoreReport report =
                service.ValidateRestore(targetGrid, parsed);
            Grid rollbackGrid = targetGrid;
            ModularFacilityGameDataSaveData rollbackSession =
                ModularFacilityGameDataSaveData.From(targetGameData);
            ModularFacilityWorldRestoreCandidate rollbackCandidate =
                service.PrepareRestoreCandidate(
                    targetGrid,
                    parsed);
            Require(
                candidateIndex.TryGetBuildings(
                    out IReadOnlyList<BuildableObject> rollbackBuildings),
                "rollback candidate buildings are indexed");
            service.BeginRestoreCandidate();
            service.StageRestoreCandidate(rollbackCandidate);
            service.PublishRestoreCandidate();
            bool oldWorldPreservedDuringPublish =
                !ReferenceEquals(gridPublisher.CurrentGrid, rollbackGrid)
                && targetStaleBuildings.All(item =>
                    item != null && !item.IsGridDestroyed)
                && EqualGameData(
                    rollbackSession,
                    ModularFacilityGameDataSaveData.From(targetGameData))
                && candidateIndex.TryGetGrid(out Grid indexedGrid)
                && ReferenceEquals(indexedGrid, gridPublisher.CurrentGrid);
            service.RollbackPublishedRestoreCandidate();
            bool rollbackRestoredExactWorld =
                ReferenceEquals(gridPublisher.CurrentGrid, rollbackGrid)
                && targetStaleBuildings.All(item =>
                    item != null && !item.IsGridDestroyed)
                && EqualGameData(
                    rollbackSession,
                    ModularFacilityGameDataSaveData.From(targetGameData))
                && !candidateIndex.TryGetGrid(out _)
                && !candidateIndex.TryGetBuildings(out _)
                && rollbackBuildings.All(item => item == null);
            Check(
                lines,
                "late_participant_failure_rolls_back_facility_publication",
                oldWorldPreservedDuringPublish && rollbackRestoredExactWorld,
                $"preserved={oldWorldPreservedDuringPublish}; restored={rollbackRestoredExactWorld}");

            bool restored = false;
            ModularFacilityWorldRestoreCandidate candidate = null;
            try
            {
                candidate = service.PrepareRestoreCandidate(
                    targetGrid,
                    parsed);
                service.BeginRestoreCandidate();
                service.StageRestoreCandidate(candidate);
                service.PublishRestoreCandidate();
                service.CompleteRestoreCandidate();
                restored = true;
            }
            catch (Exception exception)
            {
                report.AddError(exception.Message);
                candidate?.Discard();
                service.DiscardRestoreCandidate();
            }
            targetGrid = gridPublisher.CurrentGrid;

            restoredBuildings.AddRange(targetGrid.FindAllOccupants(null).OfType<BuildableObject>());
            ModularFacilityWorldSaveData roundTrip = service.CreateSnapshot(targetGrid);

            Check(lines, "restore_success", restored && report.Success, $"cleared={report.clearedCount}; restored={candidate?.RestoredCount ?? 0}; errors={string.Join("|", report.errors)}");
            Check(lines, "stale_world_cleared", targetStaleBuildings.All(item => item == null || item.IsGridDestroyed), $"stale={targetStaleBuildings.Count}");
            Check(
                lines,
                "world_replacement_retirement_skips_gameplay_destruction",
                staleWorldDestructionEventCount == 0,
                $"destructionEvents={staleWorldDestructionEventCount}");
            Check(
                lines,
                "facility_restore_does_not_mutate_session",
                EqualGameData(
                    rollbackSession,
                    ModularFacilityGameDataSaveData.From(targetGameData)),
                FormatGameData(ModularFacilityGameDataSaveData.From(targetGameData)));
            Check(lines, "building_count_round_trip", snapshot.buildings.Count == roundTrip.buildings.Count, $"{snapshot.buildings.Count}->{roundTrip.buildings.Count}");
            Check(lines, "layer_counts_round_trip", EqualLayerCounts(snapshot, roundTrip), FormatLayerCounts(roundTrip));
            Check(lines, "building_state_round_trip", EqualBuildingState(snapshot, roundTrip, out string stateDetails), stateDetails);
            Check(lines, "registered_layers_round_trip", EntriesOccupySavedLayers(targetGrid, roundTrip, out string layerDetails), layerDetails);
            Check(lines, "json_round_trip", json.Contains("\"buildings\"") && parsed.buildings.Count == snapshot.buildings.Count, $"jsonLength={json.Length}; parsed={parsed.buildings.Count}");
        }
        finally
        {
            DestroyCreated(sourceBuildings);
            DestroyCreated(restoredBuildings);
            DestroyCreated(targetStaleBuildings);
            UnityEngine.Object.DestroyImmediate(textureObject);
        }

        return lines.Skip(1).All(line => line.Contains("\tPASS\t"));
    }

    private static IGridBuildingFactory CreateInjectedFactory()
    {
        return new GridBuildingFactory(InjectBuilding);
    }

    private static Grid CreateSaveFixtureGrid()
    {
        Grid grid = new Grid(
            DungeonSpaceExpansionCatalog.InitialInteriorColumns + 1,
            DungeonSpaceExpansionCatalog.SupportedGridHeight);
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GridCellAreaType area = x < DungeonSpaceExpansionCatalog.InitialInteriorColumns
                    ? x == 0 && y == 0
                        ? GridCellAreaType.Entrance
                        : GridCellAreaType.DungeonInterior
                    : GridCellAreaType.BlockedExterior;
                grid.SetAreaType(position, area);
            }
        }
        return grid;
    }

    private static void InjectBuilding(BuildableObject building)
    {
        CharacterAiEditorTestDependencies.Inject(building);
        if (building is Shop shop)
        {
            CharacterAiEditorTestDependencies.InjectShop(shop);
        }
    }

    private sealed class StaticGridTextureProvider : IGridTextureProvider
    {
        internal StaticGridTextureProvider(GridTexture texture)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        }

        public GridTexture Texture { get; }
    }

    private sealed class TestGameSessionStateStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;

        internal TestGameSessionStateStore(GameSessionState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
            if (snapshot.IsPaused)
            {
                throw new InvalidOperationException(
                    "Facility save contract fixture does not support a paused session.");
            }

            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class TestGridSystemPublisher : IGridSystemPublisher
    {
        internal TestGridSystemPublisher(Grid initialGrid)
        {
            CurrentGrid = initialGrid
                ?? throw new ArgumentNullException(nameof(initialGrid));
        }

        internal Grid CurrentGrid { get; private set; }

        public bool TryPublishGrid(
            Grid expectedCurrent,
            Grid replacement,
            out string failureReason)
        {
            if (!ReferenceEquals(CurrentGrid, expectedCurrent)
                || replacement == null)
            {
                failureReason = "Grid publication expectation changed.";
                return false;
            }

            CurrentGrid = replacement;
            failureReason = string.Empty;
            return true;
        }

        public void CompleteGridPublication()
        {
        }
    }

    private sealed class NoopFacilityRelocationWorldService :
        IFacilityRelocationWorldService
    {
        public bool CanRelocate(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is not part of this save contract.";
            return false;
        }

        public bool TryPackAtDestination(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = "Relocation is not part of this save contract.";
            return false;
        }

        public bool TryCompleteRelocation(
            BuildableObject packedSource,
            out BuildableObject relocated,
            out string failureReason)
        {
            relocated = null;
            failureReason = "Relocation is not part of this save contract.";
            return false;
        }

        public void RestorePackedPresentation(BuildableObject packedSource)
        {
        }
    }

    private static BuildableObject Place(
        Grid grid,
        BuildingSO data,
        Vector2Int position,
        List<BuildableObject> created)
    {
        BuildableObject building = CreateInjectedFactory().Create(grid, data, position);
        Require(building != null, $"created {data?.objectName}");
        building.SetGrid(grid);
        building.Initialization(data, position);
        bool registered = grid.RegisterOccupant(
            building,
            data.Placement.Layer,
            data.GetGridPosList(position),
            data.Placement.IsMovement);
        Require(registered, $"registered {data.objectName} at {position} on {data.Placement.Layer}");
        created.Add(building);
        return building;
    }

    private static Dictionary<int, BuildingSO> LoadBuildingCatalog()
    {
        return AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<BuildingSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null)
            .GroupBy(asset => asset.id)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static BuildingSO RequireBuilding(Dictionary<int, BuildingSO> catalog, int id, string label)
    {
        if (!catalog.TryGetValue(id, out BuildingSO building) || building == null)
        {
            throw new InvalidOperationException($"{label} building id {id} was not found.");
        }

        return building;
    }

    private static BuildingSO RequireCode(Dictionary<int, BuildingSO> catalog, string code)
    {
        BuildingSO building = catalog.Values.FirstOrDefault(
            candidate => candidate != null
                && string.Equals(candidate.GetFacilityCode(), code, StringComparison.Ordinal));
        if (building == null)
        {
            throw new InvalidOperationException($"Modular facility code {code} was not found.");
        }

        return building;
    }

    private static GameSessionState CreateGameData(
        int money,
        int day,
        float curTime,
        int hour,
        int speed,
        TimeOfDay timeOfDay)
    {
        GameSessionState gameData = new GameSessionState();
        gameData.holdingMoney.Initialize(money);
        gameData.day.Initialize(day);
        gameData.curTime.Initialize(curTime);
        gameData.hour.Initialize(hour);
        gameData.gameSpeed.Initialize(speed);
        gameData.timeOfDay.Initialize(timeOfDay);
        return gameData;
    }

    private static bool EqualGameData(
        ModularFacilityGameDataSaveData a,
        ModularFacilityGameDataSaveData b)
    {
        return a != null
            && b != null
            && a.hasGameSpeed == b.hasGameSpeed
            && a.gameSpeed == b.gameSpeed
            && a.hasHoldingMoney == b.hasHoldingMoney
            && a.holdingMoney == b.holdingMoney
            && a.hasDay == b.hasDay
            && a.day == b.day
            && a.hasCurTime == b.hasCurTime
            && Mathf.Approximately(a.curTime, b.curTime)
            && a.hasHour == b.hasHour
            && a.hour == b.hour
            && a.hasTimeOfDay == b.hasTimeOfDay
            && a.timeOfDay == b.timeOfDay;
    }

    private static bool EqualLayerCounts(
        ModularFacilityWorldSaveData a,
        ModularFacilityWorldSaveData b)
    {
        foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
        {
            int left = a.buildings.Count(entry => entry.layer == layer);
            int right = b.buildings.Count(entry => entry.layer == layer);
            if (left != right)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EqualBuildingState(
        ModularFacilityWorldSaveData a,
        ModularFacilityWorldSaveData b,
        out string details)
    {
        Dictionary<string, ModularFacilityBuildingSaveData> left = ToBuildingMap(a);
        Dictionary<string, ModularFacilityBuildingSaveData> right = ToBuildingMap(b);
        if (left.Count != right.Count)
        {
            details = $"entryCount={left.Count}->{right.Count}";
            return false;
        }

        foreach (KeyValuePair<string, ModularFacilityBuildingSaveData> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out ModularFacilityBuildingSaveData restored))
            {
                details = $"missing={pair.Key}";
                return false;
            }

            if (!EqualEntry(pair.Value, restored, out details))
            {
                details = $"{pair.Key}: {details}";
                return false;
            }
        }

        details = $"entries={left.Count}";
        return true;
    }

    private static bool EntriesOccupySavedLayers(
        Grid grid,
        ModularFacilityWorldSaveData snapshot,
        out string details)
    {
        foreach (ModularFacilityBuildingSaveData entry in snapshot.buildings)
        {
            GridCell cell = grid.GetGridCell(new Vector2Int(entry.centerX, entry.centerY));
            BuildableObject occupant = cell?.GetOccupant(entry.layer) as BuildableObject;
            if (occupant == null || occupant.id != entry.buildingId)
            {
                details = $"missing id={entry.buildingId} layer={entry.layer} center=({entry.centerX},{entry.centerY})";
                return false;
            }
        }

        details = $"checked={snapshot.buildings.Count}";
        return true;
    }

    private static bool EqualEntry(
        ModularFacilityBuildingSaveData a,
        ModularFacilityBuildingSaveData b,
        out string details)
    {
        bool stateModulesEqual = EqualStateModules(
            a.stateModules,
            b.stateModules,
            out string moduleDetails);
        if (a.buildingId != b.buildingId
            || a.layer != b.layer
            || a.centerX != b.centerX
            || a.centerY != b.centerY
            || a.isDamaged != b.isDamaged
            || a.facilityLevel != b.facilityLevel
            || !stateModulesEqual)
        {
            details =
                $"state mismatch id={a.buildingId} layer={a.layer}; {moduleDetails}";
            return false;
        }

        details = string.Empty;
        return true;
    }

    private static bool EqualStateModules(
        IEnumerable<BuildingStateModuleSaveData> a,
        IEnumerable<BuildingStateModuleSaveData> b,
        out string details)
    {
        List<BuildingStateModuleSaveData> left = (a ?? Enumerable.Empty<BuildingStateModuleSaveData>())
            .OrderBy(item => item.moduleId, StringComparer.Ordinal)
            .ToList();
        List<BuildingStateModuleSaveData> right = (b ?? Enumerable.Empty<BuildingStateModuleSaveData>())
            .OrderBy(item => item.moduleId, StringComparer.Ordinal)
            .ToList();
        if (left.Count != right.Count)
        {
            details = $"module count {left.Count}->{right.Count}";
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            BuildingStateModuleSaveData before = left[index];
            BuildingStateModuleSaveData after = right[index];
            if (!string.Equals(
                    before.moduleId,
                    after.moduleId,
                    StringComparison.Ordinal)
                || before.version != after.version
                || !string.Equals(
                    before.payload,
                    after.payload,
                    StringComparison.Ordinal))
            {
                details =
                    $"module {before.moduleId} v{before.version}->{after.moduleId} v{after.version}; "
                    + $"payload {before.payload}->{after.payload}";
                return false;
            }
        }

        details = "modules equal";
        return true;
    }

    private static Dictionary<string, ModularFacilityBuildingSaveData> ToBuildingMap(
        ModularFacilityWorldSaveData snapshot)
    {
        return snapshot.buildings.ToDictionary(
            entry => $"{entry.buildingId}:{entry.layer}:{entry.centerX}:{entry.centerY}",
            entry => entry);
    }

    private static string FormatLayerCounts(ModularFacilityWorldSaveData snapshot)
    {
        return string.Join(
            ",",
            Enum.GetValues(typeof(GridLayer))
                .Cast<GridLayer>()
                .Select(layer => $"{layer}={snapshot.buildings.Count(entry => entry.layer == layer)}"));
    }

    private static string FormatGameData(ModularFacilityGameDataSaveData data)
    {
        return $"money={data.holdingMoney}; day={data.day}; time={data.curTime}; hour={data.hour}; speed={data.gameSpeed}; tod={data.timeOfDay}";
    }

    private static void Check(List<string> lines, string name, bool passed, string details)
    {
        lines.Add($"{name}\t{(passed ? "PASS" : "FAIL")}\t{Sanitize(details)}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void DestroyCreated(IEnumerable<BuildableObject> buildings)
    {
        foreach (BuildableObject building in buildings ?? Enumerable.Empty<BuildableObject>())
        {
            if (building == null) continue;
            UnityEngine.Object.DestroyImmediate(building.gameObject);
        }
    }

    private static string Sanitize(object value)
    {
        return Convert.ToString(value)
            ?.Replace('\t', ' ')
            .Replace(Environment.NewLine, " ")
            ?? string.Empty;
    }
}
