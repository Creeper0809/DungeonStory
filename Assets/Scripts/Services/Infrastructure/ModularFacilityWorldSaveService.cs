using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public interface IModularFacilityWorldSaveService
{
    ModularFacilityWorldSaveData CreateSnapshot(Grid grid, GameData gameData);
    string ToJson(ModularFacilityWorldSaveData snapshot, bool prettyPrint = false);
    ModularFacilityWorldSaveData FromJson(string json);
    bool TryRestoreSnapshot(
        Grid grid,
        GameData gameData,
        ModularFacilityWorldSaveData snapshot,
        out ModularFacilityWorldRestoreReport report);
    bool TryRestoreJson(
        Grid grid,
        GameData gameData,
        string json,
        out ModularFacilityWorldRestoreReport report);
}

public sealed class ModularFacilityWorldSaveService : IModularFacilityWorldSaveService
{
    public const int CurrentVersion = 2;

    private readonly Func<int, BuildingSO> findBuildingData;
    private readonly IGridBuildingObjectFactory objectFactory;
    private readonly IObjectResolver objectResolver;
    private readonly IGridTextureProvider gridTextureProvider;
    private IGridBuildingFactory buildingFactory;

    public ModularFacilityWorldSaveService(
        IBuildingDefinitionLookup buildingLookup,
        IGridBuildingObjectFactory objectFactory,
        IObjectResolver objectResolver,
        IGridTextureProvider gridTextureProvider)
    {
        findBuildingData = CreateBuildingLookup(buildingLookup);
        this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        this.objectResolver = objectResolver ?? throw new ArgumentNullException(nameof(objectResolver));
        this.gridTextureProvider = gridTextureProvider ?? throw new ArgumentNullException(nameof(gridTextureProvider));
    }

    public ModularFacilityWorldSaveService(
        Func<int, BuildingSO> findBuildingData,
        IGridBuildingFactory buildingFactory)
    {
        this.findBuildingData = findBuildingData
            ?? throw new ArgumentNullException(nameof(findBuildingData));
        this.buildingFactory = buildingFactory
            ?? throw new ArgumentNullException(nameof(buildingFactory));
    }

    private static Func<int, BuildingSO> CreateBuildingLookup(IBuildingDefinitionLookup buildingLookup)
    {
        if (buildingLookup == null)
        {
            throw new ArgumentNullException(nameof(buildingLookup));
        }

        return buildingLookup.GetBuilding;
    }

    private static IGridBuildingFactory CreateBuildingFactory(
        IGridBuildingObjectFactory objectFactory,
        IObjectResolver objectResolver,
        IGridTextureProvider gridTextureProvider)
    {
        if (objectFactory == null)
        {
            throw new ArgumentNullException(nameof(objectFactory));
        }

        if (objectResolver == null)
        {
            throw new ArgumentNullException(nameof(objectResolver));
        }

        if (gridTextureProvider == null)
        {
            throw new ArgumentNullException(nameof(gridTextureProvider));
        }

        return new GridBuildingFactory(
            gridTextureProvider.Texture,
            objectResolver.Inject,
            objectFactory);
    }

    public ModularFacilityWorldSaveData CreateSnapshot(Grid grid, GameData gameData)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        return new ModularFacilityWorldSaveData
        {
            version = CurrentVersion,
            gridWidth = grid.width,
            gridHeight = grid.height,
            gameData = ModularFacilityGameDataSaveData.From(gameData),
            buildings = grid.FindAllOccupants(null)
                .OfType<BuildableObject>()
                .Where(IsPersistentWorldBuilding)
                .OrderBy(building => (int)building.BuildingData.Placement.Layer)
                .ThenBy(building => building.centerPos.y)
                .ThenBy(building => building.centerPos.x)
                .ThenBy(building => building.id)
                .Select(ModularFacilityBuildingSaveData.From)
                .ToList()
        };
    }

    public string ToJson(ModularFacilityWorldSaveData snapshot, bool prettyPrint = false)
    {
        return JsonUtility.ToJson(snapshot ?? new ModularFacilityWorldSaveData(), prettyPrint);
    }

    public ModularFacilityWorldSaveData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ModularFacilityWorldSaveData();
        }

        ModularFacilitySaveVersionHeader header = JsonUtility.FromJson<ModularFacilitySaveVersionHeader>(json);
        if (header == null || header.version <= 1)
        {
            ModularFacilityWorldSaveDataV1 legacy = JsonUtility.FromJson<ModularFacilityWorldSaveDataV1>(json);
            return ModularFacilityWorldSaveMigrator.FromV1(legacy);
        }

        return JsonUtility.FromJson<ModularFacilityWorldSaveData>(json)
            ?? new ModularFacilityWorldSaveData();
    }

    public bool TryRestoreJson(
        Grid grid,
        GameData gameData,
        string json,
        out ModularFacilityWorldRestoreReport report)
    {
        return TryRestoreSnapshot(grid, gameData, FromJson(json), out report);
    }

    public bool TryRestoreSnapshot(
        Grid grid,
        GameData gameData,
        ModularFacilityWorldSaveData snapshot,
        out ModularFacilityWorldRestoreReport report)
    {
        report = new ModularFacilityWorldRestoreReport();
        if (grid == null)
        {
            report.AddError("Grid is null.");
            return false;
        }

        if (snapshot == null)
        {
            report.AddError("Snapshot is null.");
            return false;
        }

        foreach (string migrationWarning in snapshot.migrationWarnings ?? Enumerable.Empty<string>())
        {
            report.AddWarning(migrationWarning);
        }

        if (snapshot.version > CurrentVersion)
        {
            report.AddError($"Unsupported save version {snapshot.version}.");
            return false;
        }

        if (snapshot.version < CurrentVersion)
        {
            report.AddError(
                $"Save version {snapshot.version} must be migrated through {nameof(FromJson)} before restore.");
            return false;
        }

        ClearExistingBuildings(grid, report);
        ApplyGameData(gameData, snapshot.gameData);

        foreach (ModularFacilityBuildingSaveData entry in SortForRestore(snapshot.buildings))
        {
            RestoreBuilding(grid, entry, report);
        }

        report.restoredCount = report.restoredBuildings.Count;
        return report.errors.Count == 0;
    }

    private void ClearExistingBuildings(Grid grid, ModularFacilityWorldRestoreReport report)
    {
        List<BuildableObject> existing = grid.FindAllOccupants(null)
            .OfType<BuildableObject>()
            .Where(IsPersistentWorldBuilding)
            .Distinct()
            .OrderByDescending(building => (int)building.BuildingData.Placement.Layer)
            .ThenByDescending(building => building.centerPos.y)
            .ThenByDescending(building => building.centerPos.x)
            .ToList();

        foreach (BuildableObject building in existing)
        {
            BuildingSO data = building.BuildingData;
            bool removed = grid.RemoveOccupant(
                building,
                data.Placement.Layer,
                building.buildPoses,
                data.Placement.IsMovement);
            if (!removed)
            {
                report.AddError($"Failed to remove existing building id={building.id} at {building.centerPos}.");
                continue;
            }

            ResolveBuildingFactory().DeleteVisual(data, building.centerPos);
            building.DestroySelf();
            report.clearedCount++;
        }
    }

    private static bool IsPersistentWorldBuilding(BuildableObject building)
    {
        return building != null
            && !building.IsGridDestroyed
            && building.BuildingData != null
            && building.BuildingData.id >= 0
            && building is not ConstructionSite;
    }

    private void RestoreBuilding(
        Grid grid,
        ModularFacilityBuildingSaveData entry,
        ModularFacilityWorldRestoreReport report)
    {
        if (entry == null)
        {
            report.AddError("Encountered a null building save entry.");
            return;
        }

        BuildingSO data;
        try
        {
            data = findBuildingData(entry.buildingId);
        }
        catch (Exception ex)
        {
            report.AddError($"Building id={entry.buildingId} lookup failed: {ex.Message}");
            return;
        }

        if (data == null)
        {
            report.AddError($"Building id={entry.buildingId} was not found.");
            return;
        }

        if (data.Placement.Layer != entry.layer)
        {
            report.AddWarning(
                $"Building id={entry.buildingId} layer changed from saved {entry.layer} to asset {data.Placement.Layer}.");
        }

        Vector2Int center = new Vector2Int(entry.centerX, entry.centerY);
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(center);
        if (!CanRegister(grid, data.Placement.Layer, positions))
        {
            report.AddError($"Building id={entry.buildingId} cannot occupy {entry.layer} at {center}.");
            return;
        }

        BuildableObject building = ResolveBuildingFactory().Create(grid, data, center);
        if (building == null)
        {
            report.AddError($"Building id={entry.buildingId} object creation failed at {center}.");
            return;
        }

        building.SetGrid(grid);
        building.Initialization(data, center);
        bool registered = grid.RegisterOccupant(
            building,
            data.Placement.Layer,
            positions,
            data.Placement.IsMovement);
        if (!registered)
        {
            ResolveBuildingFactory().DeleteVisual(data, center);
            building.DestroySelf();
            report.AddError($"Building id={entry.buildingId} grid registration failed at {center}.");
            return;
        }

        building.SetDamaged(entry.isDamaged);
        building.SetFacilityLevel(entry.facilityLevel);
        BuildingStateModuleRestoreResult stateResult = BuildingStateModulePersistence.Restore(
            building,
            entry.stateModules);
        foreach (string warning in stateResult.warnings)
        {
            report.AddWarning(warning);
        }

        foreach (string error in stateResult.errors)
        {
            report.AddError(error);
        }

        report.restoredBuildings.Add(entry);
    }

    private IGridBuildingFactory ResolveBuildingFactory()
    {
        buildingFactory ??= CreateBuildingFactory(objectFactory, objectResolver, gridTextureProvider);
        return buildingFactory;
    }

    private static bool CanRegister(
        Grid grid,
        GridLayer layer,
        IReadOnlyList<Vector2Int> positions)
    {
        if (grid == null || positions == null || positions.Count == 0)
        {
            return false;
        }

        foreach (Vector2Int position in positions.Distinct())
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null || !cell.CanOccupy(layer))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<ModularFacilityBuildingSaveData> SortForRestore(
        IEnumerable<ModularFacilityBuildingSaveData> buildings)
    {
        return (buildings ?? Enumerable.Empty<ModularFacilityBuildingSaveData>())
            .Where(entry => entry != null)
            .OrderBy(entry => entry.layer == GridLayer.Hallway ? 0 : 1)
            .ThenBy(entry => entry.layer == GridLayer.Building ? 0 : 1)
            .ThenBy(entry => (int)entry.layer)
            .ThenBy(entry => entry.centerY)
            .ThenBy(entry => entry.centerX)
            .ThenBy(entry => entry.buildingId);
    }

    private static void ApplyGameData(GameData gameData, ModularFacilityGameDataSaveData data)
    {
        if (gameData == null || data == null)
        {
            return;
        }

        if (data.hasGameSpeed)
        {
            gameData.gameSpeed ??= new Data<int>();
            gameData.gameSpeed.Initialize(Mathf.Max(0, data.gameSpeed));
        }

        if (data.hasHoldingMoney)
        {
            gameData.holdingMoney ??= new Data<int>();
            gameData.holdingMoney.Initialize(Mathf.Max(0, data.holdingMoney));
        }

        if (data.hasDay)
        {
            gameData.day ??= new Data<int>();
            gameData.day.Initialize(Mathf.Max(1, data.day));
        }

        if (data.hasCurTime)
        {
            gameData.curTime ??= new Data<float>();
            gameData.curTime.Initialize(Mathf.Max(0f, data.curTime));
        }

        if (data.hasHour)
        {
            gameData.hour ??= new Data<int>();
            gameData.hour.Initialize(Mathf.Clamp(data.hour, 0, 23));
        }

        if (data.hasTimeOfDay)
        {
            gameData.timeOfDay ??= new Data<TimeOfDay>();
            gameData.timeOfDay.Initialize(data.timeOfDay);
        }
    }
}

[Serializable]
public sealed class ModularFacilityWorldSaveData
{
    public int version = ModularFacilityWorldSaveService.CurrentVersion;
    public int gridWidth;
    public int gridHeight;
    public ModularFacilityGameDataSaveData gameData = new ModularFacilityGameDataSaveData();
    public List<ModularFacilityBuildingSaveData> buildings = new List<ModularFacilityBuildingSaveData>();
    [NonSerialized] public int migratedFromVersion;
    [NonSerialized] public List<string> migrationWarnings = new List<string>();
}

[Serializable]
public sealed class ModularFacilityGameDataSaveData
{
    public bool hasGameSpeed;
    public int gameSpeed;
    public bool hasHoldingMoney;
    public int holdingMoney;
    public bool hasDay;
    public int day;
    public bool hasCurTime;
    public float curTime;
    public bool hasHour;
    public int hour;
    public bool hasTimeOfDay;
    public TimeOfDay timeOfDay;

    public static ModularFacilityGameDataSaveData From(GameData gameData)
    {
        if (gameData == null)
        {
            return new ModularFacilityGameDataSaveData();
        }

        return new ModularFacilityGameDataSaveData
        {
            hasGameSpeed = gameData.gameSpeed != null,
            gameSpeed = gameData.gameSpeed != null ? gameData.gameSpeed.Value : 0,
            hasHoldingMoney = gameData.holdingMoney != null,
            holdingMoney = gameData.holdingMoney != null ? gameData.holdingMoney.Value : 0,
            hasDay = gameData.day != null,
            day = gameData.day != null ? gameData.day.Value : 1,
            hasCurTime = gameData.curTime != null,
            curTime = gameData.curTime != null ? gameData.curTime.Value : 0f,
            hasHour = gameData.hour != null,
            hour = gameData.hour != null ? gameData.hour.Value : 0,
            hasTimeOfDay = gameData.timeOfDay != null,
            timeOfDay = gameData.timeOfDay != null ? gameData.timeOfDay.Value : TimeOfDay.Morning
        };
    }
}

[Serializable]
public sealed class ModularFacilityBuildingSaveData
{
    public int buildingId;
    public string code;
    public string objectName;
    public GridLayer layer;
    public int centerX;
    public int centerY;
    public int width;
    public int height;
    public bool isDamaged;
    public int facilityLevel = 1;
    public List<BuildingStateModuleSaveData> stateModules = new List<BuildingStateModuleSaveData>();

    public static ModularFacilityBuildingSaveData From(BuildableObject building)
    {
        BuildingSO data = building.BuildingData;
        ModularFacilityBuildingSaveData result = new ModularFacilityBuildingSaveData
        {
            buildingId = data.id,
            code = data.GetFacilityCode(),
            objectName = data.objectName,
            layer = data.Placement.Layer,
            centerX = building.centerPos.x,
            centerY = building.centerPos.y,
            width = data.Placement.Width,
            height = data.Placement.Height,
            isDamaged = building.IsDamaged,
            facilityLevel = building.FacilityLevel,
            stateModules = BuildingStateModulePersistence.Capture(building)
        };

        return result;
    }
}

[Serializable]
internal sealed class ModularFacilitySaveVersionHeader
{
    public int version;
}

[Serializable]
internal sealed class ModularFacilityWorldSaveDataV1
{
    public int version = 1;
    public int gridWidth;
    public int gridHeight;
    public ModularFacilityGameDataSaveData gameData = new ModularFacilityGameDataSaveData();
    public List<ModularFacilityBuildingSaveDataV1> buildings = new List<ModularFacilityBuildingSaveDataV1>();
}

[Serializable]
internal sealed class ModularFacilityBuildingSaveDataV1
{
    public int buildingId;
    public string code;
    public string objectName;
    public GridLayer layer;
    public int centerX;
    public int centerY;
    public int width;
    public int height;
    public bool isDamaged;
    public int facilityLevel = 1;
    public LegacyFacilityOperationalStateV1 operationalState = new LegacyFacilityOperationalStateV1();
    public bool hasWarehouseSnapshot;
    public WarehouseInventorySnapshotV1 warehouseSnapshot;
    public bool hasShopStockSnapshot;
    public ShopStockStateSnapshot shopStockSnapshot;
}

internal static class ModularFacilityWorldSaveMigrator
{
    public static ModularFacilityWorldSaveData FromV1(ModularFacilityWorldSaveDataV1 legacy)
    {
        legacy ??= new ModularFacilityWorldSaveDataV1();
        ModularFacilityWorldSaveData migrated = new ModularFacilityWorldSaveData
        {
            version = ModularFacilityWorldSaveService.CurrentVersion,
            gridWidth = legacy.gridWidth,
            gridHeight = legacy.gridHeight,
            gameData = legacy.gameData ?? new ModularFacilityGameDataSaveData(),
            migratedFromVersion = 1,
            buildings = (legacy.buildings ?? new List<ModularFacilityBuildingSaveDataV1>())
                .Where(entry => entry != null)
                .Select(MigrateBuilding)
                .ToList()
        };
        migrated.migrationWarnings.Add(
            $"Migrated modular facility save from version 1 to {ModularFacilityWorldSaveService.CurrentVersion}.");
        return migrated;
    }

    private static ModularFacilityBuildingSaveData MigrateBuilding(ModularFacilityBuildingSaveDataV1 legacy)
    {
        ModularFacilityBuildingSaveData migrated = new ModularFacilityBuildingSaveData
        {
            buildingId = legacy.buildingId,
            code = legacy.code,
            objectName = legacy.objectName,
            layer = legacy.layer,
            centerX = legacy.centerX,
            centerY = legacy.centerY,
            width = legacy.width,
            height = legacy.height,
            isDamaged = legacy.isDamaged,
            facilityLevel = legacy.facilityLevel,
            stateModules = new List<BuildingStateModuleSaveData>
            {
                new BuildingStateModuleSaveData
                {
                    moduleId = BuildingStateModuleIds.FacilityOperation,
                    version = 1,
                    payload = JsonUtility.ToJson(legacy.operationalState ?? new LegacyFacilityOperationalStateV1())
                }
            }
        };

        if (legacy.hasWarehouseSnapshot)
        {
            WarehouseInventorySnapshot warehouse = WarehouseInventorySnapshot.FromLegacy(legacy.warehouseSnapshot);
            migrated.stateModules.Add(new BuildingStateModuleSaveData
            {
                moduleId = BuildingStateModuleIds.WarehouseInventory,
                version = WarehouseInventorySnapshot.CurrentVersion,
                payload = JsonUtility.ToJson(warehouse)
            });
        }

        if (legacy.hasShopStockSnapshot)
        {
            migrated.stateModules.Add(new BuildingStateModuleSaveData
            {
                moduleId = BuildingStateModuleIds.ShopStock,
                version = 1,
                payload = JsonUtility.ToJson(legacy.shopStockSnapshot ?? new ShopStockStateSnapshot())
            });
        }

        return migrated;
    }
}

public sealed class ModularFacilityWorldRestoreReport
{
    public int clearedCount;
    public int restoredCount;
    public readonly List<ModularFacilityBuildingSaveData> restoredBuildings =
        new List<ModularFacilityBuildingSaveData>();
    public readonly List<string> warnings = new List<string>();
    public readonly List<string> errors = new List<string>();

    public bool Success => errors.Count == 0;

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            warnings.Add(message);
        }
    }

    public void AddError(string message)
    {
        errors.Add(string.IsNullOrWhiteSpace(message) ? "Unknown restore error." : message);
    }
}
