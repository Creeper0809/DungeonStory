using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BuildingStateModuleIds
{
    public const string FacilityOperation = "facility.operation";
    public const string WarehouseInventory = "inventory.warehouse";
    public const string ShopStock = "inventory.shop";

    public static string ForAbility(string capability, string abilityId)
    {
        string safeCapability = NormalizeSegment(capability, nameof(capability));
        string safeAbilityId = NormalizeSegment(abilityId, nameof(abilityId));
        return $"ability.{safeCapability}:{safeAbilityId}";
    }

    private static string NormalizeSegment(string value, string parameterName)
    {
        string normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("State module ID segments cannot be blank.", parameterName);
        }

        if (normalized.Contains(':'))
        {
            throw new ArgumentException("State module ID segments cannot contain ':'.", parameterName);
        }

        return normalized;
    }
}

[Serializable]
public sealed class BuildingStateModuleSaveData
{
    public string moduleId;
    public int version;
    public string payload;
}

public interface IBuildingStateModule
{
    string ModuleId { get; }
    int CurrentVersion { get; }
    string CaptureState();
    bool TryRestoreState(int version, string payload, out string error);
}

public sealed class BuildingStateModuleRestoreResult
{
    public readonly List<string> warnings = new List<string>();
    public readonly List<string> errors = new List<string>();
    public readonly List<string> restoredModuleIds = new List<string>();
    public bool Success => errors.Count == 0;
}

public static class BuildingStateModulePersistence
{
    public static List<BuildingStateModuleSaveData> Capture(BuildableObject building)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        Dictionary<string, IBuildingStateModule> modules = BuildModuleMap(building, null);
        List<BuildingStateModuleSaveData> snapshots = new List<BuildingStateModuleSaveData>();
        foreach (KeyValuePair<string, IBuildingStateModule> pair in modules.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            IBuildingStateModule module = pair.Value;
            if (module.CurrentVersion <= 0)
            {
                throw new InvalidOperationException(
                    $"State module '{pair.Key}' on {Describe(building)} has invalid version {module.CurrentVersion}.");
            }

            string payload;
            try
            {
                payload = module.CaptureState();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"State module '{pair.Key}' on {Describe(building)} failed to capture: {ex.Message}",
                    ex);
            }

            snapshots.Add(new BuildingStateModuleSaveData
            {
                moduleId = pair.Key,
                version = module.CurrentVersion,
                payload = payload ?? string.Empty
            });
        }

        return snapshots;
    }

    public static BuildingStateModuleRestoreResult Restore(
        BuildableObject building,
        IEnumerable<BuildingStateModuleSaveData> snapshots)
    {
        BuildingStateModuleRestoreResult result = new BuildingStateModuleRestoreResult();
        if (building == null)
        {
            result.errors.Add("Building is null.");
            return result;
        }

        Dictionary<string, IBuildingStateModule> modules = BuildModuleMap(building, result);
        Dictionary<string, BuildingStateModuleSaveData> savedById = new Dictionary<string, BuildingStateModuleSaveData>(StringComparer.Ordinal);
        foreach (BuildingStateModuleSaveData snapshot in snapshots ?? Enumerable.Empty<BuildingStateModuleSaveData>())
        {
            if (snapshot == null)
            {
                result.errors.Add($"{Describe(building)} contains a null state-module snapshot.");
                continue;
            }

            string moduleId = snapshot.moduleId?.Trim();
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                result.errors.Add($"{Describe(building)} contains a state module with no moduleId.");
                continue;
            }

            if (!savedById.TryAdd(moduleId, snapshot))
            {
                result.errors.Add($"{Describe(building)} contains duplicate saved state module '{moduleId}'.");
            }
        }

        foreach (KeyValuePair<string, BuildingStateModuleSaveData> pair in savedById.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!modules.TryGetValue(pair.Key, out IBuildingStateModule module))
            {
                result.errors.Add($"{Describe(building)} has saved state for unknown module '{pair.Key}'.");
                continue;
            }

            BuildingStateModuleSaveData snapshot = pair.Value;
            if (snapshot.version <= 0)
            {
                result.errors.Add($"{Describe(building)} module '{pair.Key}' has invalid saved version {snapshot.version}.");
                continue;
            }

            bool restored;
            string error;
            try
            {
                restored = module.TryRestoreState(snapshot.version, snapshot.payload, out error);
            }
            catch (Exception ex)
            {
                restored = false;
                error = ex.Message;
            }

            if (!restored)
            {
                result.errors.Add(
                    $"{Describe(building)} module '{pair.Key}' v{snapshot.version} restore failed: "
                    + (string.IsNullOrWhiteSpace(error) ? "unknown error" : error));
                continue;
            }

            result.restoredModuleIds.Add(pair.Key);
        }

        foreach (string moduleId in modules.Keys.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!savedById.ContainsKey(moduleId))
            {
                result.warnings.Add(
                    $"{Describe(building)} has no saved state for current module '{moduleId}'; defaults were retained.");
            }
        }

        return result;
    }

    private static Dictionary<string, IBuildingStateModule> BuildModuleMap(
        BuildableObject building,
        BuildingStateModuleRestoreResult result)
    {
        Dictionary<string, IBuildingStateModule> modules = new Dictionary<string, IBuildingStateModule>(StringComparer.Ordinal);
        foreach (IBuildingStateModule module in building.GetStateModules())
        {
            if (module == null || (module is UnityEngine.Object unityObject && unityObject == null))
            {
                continue;
            }

            string moduleId = module.ModuleId?.Trim();
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                string message = $"{Describe(building)} contains a state module with no moduleId.";
                if (result != null)
                {
                    result.errors.Add(message);
                    continue;
                }

                throw new InvalidOperationException(message);
            }

            if (!modules.TryAdd(moduleId, module))
            {
                string message = $"{Describe(building)} contains duplicate state module '{moduleId}'.";
                if (result != null)
                {
                    result.errors.Add(message);
                    continue;
                }

                throw new InvalidOperationException(message);
            }
        }

        return modules;
    }

    private static string Describe(BuildableObject building)
    {
        string name = building.BuildingData != null
            ? building.BuildingData.objectName
            : building.name;
        return $"building '{name}' at {building.centerPos}";
    }
}

public sealed class FacilityRuntimeStateModule : IBuildingStateModule
{
    private readonly BuildableObject building;

    public FacilityRuntimeStateModule(BuildableObject building)
    {
        this.building = building ?? throw new ArgumentNullException(nameof(building));
    }

    public string ModuleId => BuildingStateModuleIds.FacilityOperation;
    public int CurrentVersion => 2;

    public string CaptureState()
    {
        return JsonUtility.ToJson(building.FacilityState.Clone());
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        try
        {
            if (version == 1)
            {
                LegacyFacilityOperationalStateV1 legacy = JsonUtility.FromJson<LegacyFacilityOperationalStateV1>(payload);
                if (legacy == null)
                {
                    error = "payload did not contain legacy facility operational state";
                    return false;
                }

                building.RestoreLegacyFacilityStateV1(legacy);
                error = string.Empty;
                return true;
            }

            if (version != CurrentVersion)
            {
                error = $"unsupported version {version}; current version is {CurrentVersion}";
                return false;
            }

            FacilityRuntimeState state = JsonUtility.FromJson<FacilityRuntimeState>(payload);
            if (state == null)
            {
                error = "payload did not contain facility runtime state";
                return false;
            }

            building.RestoreFacilityState(state);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public sealed class WarehouseInventoryStateModule : IBuildingStateModule
{
    private readonly IWarehouseFacility warehouse;

    public WarehouseInventoryStateModule(IWarehouseFacility warehouse)
    {
        this.warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
    }

    public string ModuleId => BuildingStateModuleIds.WarehouseInventory;
    public int CurrentVersion => WarehouseInventorySnapshot.CurrentVersion;

    public string CaptureState()
    {
        if (!warehouse.HasWarehouseInventory || warehouse.Inventory == null)
        {
            throw new InvalidOperationException("warehouse inventory is not initialized");
        }

        return JsonUtility.ToJson(warehouse.Inventory.CreateSnapshot());
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (!warehouse.HasWarehouseInventory || warehouse.Inventory == null)
        {
            error = "warehouse inventory is not available on the restored building";
            return false;
        }

        try
        {
            WarehouseInventorySnapshot snapshot;
            if (version == 1)
            {
                WarehouseInventorySnapshotV1 legacy = JsonUtility.FromJson<WarehouseInventorySnapshotV1>(payload);
                snapshot = WarehouseInventorySnapshot.FromLegacy(legacy);
            }
            else if (version == CurrentVersion)
            {
                snapshot = JsonUtility.FromJson<WarehouseInventorySnapshot>(payload);
            }
            else
            {
                error = $"unsupported version {version}; current version is {CurrentVersion}";
                return false;
            }

            return warehouse.Inventory.TryApplySnapshot(snapshot, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public sealed class ShopStockStateModule : IBuildingStateModule
{
    private readonly IRetailStockStateOwner stockOwner;

    public ShopStockStateModule(IRetailStockStateOwner stockOwner)
    {
        this.stockOwner = stockOwner ?? throw new ArgumentNullException(nameof(stockOwner));
    }

    public string ModuleId => BuildingStateModuleIds.ShopStock;
    public int CurrentVersion => 1;

    public string CaptureState()
    {
        return JsonUtility.ToJson(stockOwner.CreateStockSnapshot());
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"unsupported version {version}; current version is {CurrentVersion}";
            return false;
        }

        try
        {
            ShopStockStateSnapshot snapshot = JsonUtility.FromJson<ShopStockStateSnapshot>(payload);
            if (snapshot == null)
            {
                error = "payload did not contain shop stock state";
                return false;
            }

            stockOwner.ApplyStockSnapshot(snapshot);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

[Serializable]
public sealed class BuildingProductionState
{
    [Min(0)] public int producedStock;
}

public sealed class BuildingProductionStateModule : IBuildingStateModule
{
    private readonly BuildingProductionState state = new BuildingProductionState();

    public BuildingProductionStateModule(BuildableObject building, BuildingProductionAbility ability)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        if (ability == null)
        {
            throw new ArgumentNullException(nameof(ability));
        }

        ModuleId = BuildingStateModuleIds.ForAbility("production", ability.AbilityId);
    }

    public string ModuleId { get; }
    public int CurrentVersion => 1;
    public int ProducedStock => state.producedStock;

    public int AddProducedStock(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        long total = (long)state.producedStock + safeAmount;
        state.producedStock = total >= int.MaxValue ? int.MaxValue : (int)total;
        return safeAmount;
    }

    public void SetProducedStock(int amount)
    {
        state.producedStock = Mathf.Max(0, amount);
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(state);
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"unsupported version {version}; current version is {CurrentVersion}";
            return false;
        }

        try
        {
            BuildingProductionState restored = JsonUtility.FromJson<BuildingProductionState>(payload);
            if (restored == null)
            {
                error = "payload did not contain production state";
                return false;
            }

            SetProducedStock(restored.producedStock);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

[Serializable]
public sealed class BuildingSecurityState
{
    [Min(0)] public int alarmCharges;
}

public sealed class BuildingSecurityStateModule : IBuildingStateModule
{
    private readonly BuildingSecurityState state = new BuildingSecurityState();

    public BuildingSecurityStateModule(BuildableObject building, BuildingSecurityAbility ability)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        if (ability == null)
        {
            throw new ArgumentNullException(nameof(ability));
        }

        ModuleId = BuildingStateModuleIds.ForAbility("security", ability.AbilityId);
    }

    public string ModuleId { get; }
    public int CurrentVersion => 1;
    public int AlarmCharges => state.alarmCharges;

    public int AddAlarmCharges(int amount, int maximum)
    {
        int previous = state.alarmCharges;
        state.alarmCharges = Mathf.Clamp(
            state.alarmCharges + Mathf.Max(0, amount),
            0,
            Mathf.Max(1, maximum));
        return state.alarmCharges - previous;
    }

    public void SetAlarmCharges(int amount)
    {
        state.alarmCharges = Mathf.Max(0, amount);
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(state);
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        if (version != CurrentVersion)
        {
            error = $"unsupported version {version}; current version is {CurrentVersion}";
            return false;
        }

        try
        {
            BuildingSecurityState restored = JsonUtility.FromJson<BuildingSecurityState>(payload);
            if (restored == null)
            {
                error = "payload did not contain security state";
                return false;
            }

            SetAlarmCharges(restored.alarmCharges);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
