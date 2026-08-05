using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnvironmentalExposureBand
{
    Stable = 0,
    Burden = 1,
    Impaired = 2,
    Critical = 3,
    Collapse = 4
}

public enum EnvironmentalWorkKind
{
    General = 0,
    Precision = 1,
    Surgery = 2,
    EmergencySurgery = 3,
    Defense = 4,
    Safety = 5
}

public readonly struct EnvironmentalCellSnapshot
{
    public EnvironmentalCellSnapshot(
        Vector2Int position,
        float temperatureC,
        float airQuality,
        float lightLevel)
    {
        Position = position;
        TemperatureC = temperatureC;
        AirQuality = airQuality;
        LightLevel = lightLevel;
    }

    public Vector2Int Position { get; }
    public float TemperatureC { get; }
    public float AirQuality { get; }
    public float LightLevel { get; }
}

[Serializable]
public sealed class EnvironmentalCellSaveData
{
    public int x;
    public int y;
    public float temperatureC;
    public float airQuality = 100f;
    public float lightLevel;
}

[Serializable]
public sealed class EnvironmentalThermostatSaveData
{
    public string buildingInstanceId = string.Empty;
    public float targetTemperatureC;
}

[Serializable]
public sealed class DungeonEnvironmentalFieldSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int width;
    public int height;
    public List<EnvironmentalCellSaveData> cells =
        new List<EnvironmentalCellSaveData>();
    public List<EnvironmentalThermostatSaveData> thermostats =
        new List<EnvironmentalThermostatSaveData>();
}

public interface IEnvironmentalFieldQuery
{
    int Version { get; }
    bool IsInitialized { get; }
    bool TryGetCell(Vector2Int position, out EnvironmentalCellSnapshot snapshot);
    bool TryGetAverage(
        IReadOnlyList<Vector2Int> positions,
        out EnvironmentalCellSnapshot snapshot);
    float GetFoodSpoilageMultiplier(Vector2Int position);
    bool IsOrganPreservationSafe(Vector2Int position);
    bool TryGetTargetTemperature(
        Vector2Int buildingPosition,
        out float targetTemperatureC);
}

public interface IEnvironmentalFieldCommand
{
    bool TrySetTargetTemperature(
        Vector2Int buildingPosition,
        float targetTemperatureC,
        out DomainFailure failure);
    void MarkTopologyDirty();
    void Reset();
}

public interface IEnvironmentalFieldPersistence
{
    DungeonEnvironmentalFieldSaveData Capture();
    DungeonStory.Environment.EnvironmentalFieldRestoreCandidate PrepareRestore(
        DungeonEnvironmentalFieldSaveData saveData);
    void Restore(
        DungeonStory.Environment.EnvironmentalFieldRestoreCandidate candidate);
}

public sealed class NoEnvironmentalFieldQuery : IEnvironmentalFieldQuery
{
    public static NoEnvironmentalFieldQuery Instance { get; } = new();

    private NoEnvironmentalFieldQuery()
    {
    }

    public int Version => 0;
    public bool IsInitialized => false;

    public bool TryGetCell(
        Vector2Int position,
        out EnvironmentalCellSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }

    public bool TryGetAverage(
        IReadOnlyList<Vector2Int> positions,
        out EnvironmentalCellSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }

    public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
    public bool IsOrganPreservationSafe(Vector2Int position) => false;

    public bool TryGetTargetTemperature(
        Vector2Int buildingPosition,
        out float targetTemperatureC)
    {
        targetTemperatureC = 0f;
        return false;
    }
}

public static class EnvironmentalThresholdRules
{
    public const float NormalAirQuality =
        DungeonStory.Environment.EnvironmentalThresholdRules.NormalAirQuality;
    public const float PollutedAirQuality =
        DungeonStory.Environment.EnvironmentalThresholdRules.PollutedAirQuality;
    public const float ToxicAirQuality =
        DungeonStory.Environment.EnvironmentalThresholdRules.ToxicAirQuality;
    public const float PrecisionMinimumAirQuality =
        DungeonStory.Environment.EnvironmentalThresholdRules.PrecisionMinimumAirQuality;
    public const float PrecisionMinimumLight =
        DungeonStory.Environment.EnvironmentalThresholdRules.PrecisionMinimumLight;
    public const float SurgeryMinimumAirQuality =
        DungeonStory.Environment.EnvironmentalThresholdRules.SurgeryMinimumAirQuality;
    public const float SurgeryMinimumLight =
        DungeonStory.Environment.EnvironmentalThresholdRules.SurgeryMinimumLight;

    public static EnvironmentalExposureBand ResolveBand(
        float exposure,
        EnvironmentalExposureBand previousBand)
    {
        return (EnvironmentalExposureBand)
            DungeonStory.Environment.EnvironmentalThresholdRules.ResolveBand(
                exposure,
                (DungeonStory.Environment.ExposureBand)previousBand);
    }

    public static float GetFoodSpoilageMultiplier(float temperatureC)
    {
        return DungeonStory.Environment.EnvironmentalThresholdRules
            .GetFoodSpoilageMultiplier(temperatureC);
    }

    public static bool IsOrganPreservationSafe(float temperatureC)
    {
        return DungeonStory.Environment.EnvironmentalThresholdRules
            .IsOrganPreservationSafe(temperatureC);
    }
}
