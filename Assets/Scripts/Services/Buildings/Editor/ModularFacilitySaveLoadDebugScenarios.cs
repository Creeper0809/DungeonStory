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

        Grid sourceGrid = new Grid(28, 3);
        Grid targetGrid = new Grid(28, 3);
        GameData sourceGameData = CreateGameData(4321, 7, 88.5f, 11, 2, TimeOfDay.Noon);
        GameData targetGameData = CreateGameData(1, 1, 0f, 0, 1, TimeOfDay.Morning);

        List<BuildableObject> sourceBuildings = new List<BuildableObject>();
        List<BuildableObject> targetStaleBuildings = new List<BuildableObject>();
        List<BuildableObject> restoredBuildings = new List<BuildableObject>();

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

            dining.RestoreLegacyFacilityStateV1(new LegacyFacilityOperationalStateV1
            {
                completedUses = 7,
                completedWorkCycles = 3,
                producedStock = 5,
                alarmCharges = 0,
                cleanliness = 31.5f
            });
            dining.SetDamaged(true);
            dining.SetFacilityLevel(3);

            Require(warehouse != null && warehouse.HasWarehouseInventory, "source warehouse exists");
            warehouse.Inventory.ApplySnapshot(new WarehouseInventorySnapshot
            {
                maxCapacity = 18,
                restrictCategory = true,
                acceptedCategoryId = StockCategoryPersistenceId.ToId(StockCategory.Food),
                stocks = new List<StockAmountSnapshot>
                {
                    StockAmountSnapshot.From(StockCategory.Food, 9)
                }
            });

            Require(shop != null, "source shop exists");
            ShopStockStateSnapshot shopStock = shop.CreateStockSnapshot();
            Require(shopStock.items != null && shopStock.items.Count > 0, "source shop has stock snapshot");
            shopStock.items[0].amount = 2;
            shop.ApplyStockSnapshot(shopStock);

            BuildableObject alarm = Place(sourceGrid, alarmBell, new Vector2Int(20, 0), sourceBuildings);
            BuildingSecurityAbility alarmAbility = alarm.BuildingData.GetAbility<BuildingSecurityAbility>();
            alarm.RequireStateModule<BuildingSecurityStateModule>(
                    BuildingStateModuleIds.ForAbility("security", alarmAbility.AbilityId))
                .SetAlarmCharges(2);

            Place(targetGrid, hallway, new Vector2Int(25, 0), targetStaleBuildings);
            Place(targetGrid, diningCore, new Vector2Int(22, 0), targetStaleBuildings);

            ModularFacilityWorldSaveService service = new ModularFacilityWorldSaveService(
                id => catalog.TryGetValue(id, out BuildingSO data) ? data : null,
                CreateInjectedFactory());

            ModularFacilityWorldSaveData snapshot = service.CreateSnapshot(sourceGrid, sourceGameData);
            string json = service.ToJson(snapshot, prettyPrint: true);
            ModularFacilityWorldSaveData parsed = service.FromJson(json);
            bool restored = service.TryRestoreSnapshot(
                targetGrid,
                targetGameData,
                parsed,
                out ModularFacilityWorldRestoreReport report);

            restoredBuildings.AddRange(targetGrid.FindAllOccupants(null).OfType<BuildableObject>());
            ModularFacilityWorldSaveData roundTrip = service.CreateSnapshot(targetGrid, targetGameData);

            Check(lines, "restore_success", restored && report.Success, $"cleared={report.clearedCount}; restored={report.restoredCount}; errors={string.Join("|", report.errors)}");
            Check(lines, "stale_world_cleared", report.clearedCount == targetStaleBuildings.Count && targetStaleBuildings.All(item => item == null || item.IsGridDestroyed), $"cleared={report.clearedCount}; stale={targetStaleBuildings.Count}");
            Check(lines, "game_data_round_trip", EqualGameData(snapshot.gameData, roundTrip.gameData), FormatGameData(roundTrip.gameData));
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
            UnityEngine.Object.DestroyImmediate(sourceGameData);
            UnityEngine.Object.DestroyImmediate(targetGameData);
        }

        return lines.Skip(1).All(line => line.Contains("\tPASS\t"));
    }

    private static IGridBuildingFactory CreateInjectedFactory()
    {
        return new GridBuildingFactory(building =>
        {
            CharacterAiEditorTestDependencies.Inject(building);
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
        });
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

    private static GameData CreateGameData(
        int money,
        int day,
        float curTime,
        int hour,
        int speed,
        TimeOfDay timeOfDay)
    {
        GameData gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.hideFlags = HideFlags.HideAndDontSave;
        gameData.holdingMoney = new Data<int>();
        gameData.day = new Data<int>();
        gameData.curTime = new Data<float>();
        gameData.hour = new Data<int>();
        gameData.gameSpeed = new Data<int>();
        gameData.timeOfDay = new Data<TimeOfDay>();
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
        if (a.buildingId != b.buildingId
            || a.layer != b.layer
            || a.centerX != b.centerX
            || a.centerY != b.centerY
            || a.isDamaged != b.isDamaged
            || a.facilityLevel != b.facilityLevel
            || !EqualStateModules(a.stateModules, b.stateModules))
        {
            details = $"state mismatch id={a.buildingId} layer={a.layer}";
            return false;
        }

        details = string.Empty;
        return true;
    }

    private static bool EqualStateModules(
        IEnumerable<BuildingStateModuleSaveData> a,
        IEnumerable<BuildingStateModuleSaveData> b)
    {
        List<BuildingStateModuleSaveData> left = (a ?? Enumerable.Empty<BuildingStateModuleSaveData>())
            .OrderBy(item => item.moduleId, StringComparer.Ordinal)
            .ToList();
        List<BuildingStateModuleSaveData> right = (b ?? Enumerable.Empty<BuildingStateModuleSaveData>())
            .OrderBy(item => item.moduleId, StringComparer.Ordinal)
            .ToList();
        return left.Count == right.Count
            && !left.Where((item, index) => !string.Equals(item.moduleId, right[index].moduleId, StringComparison.Ordinal)
                || item.version != right[index].version
                || !string.Equals(item.payload, right[index].payload, StringComparison.Ordinal)).Any();
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
