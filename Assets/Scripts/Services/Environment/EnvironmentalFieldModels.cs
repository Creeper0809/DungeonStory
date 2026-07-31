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
    public int x;
    public int y;
    public float targetTemperatureC;
}

[Serializable]
public sealed class DungeonEnvironmentalFieldSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int width;
    public int height;
    public List<EnvironmentalCellSaveData> cells =
        new List<EnvironmentalCellSaveData>();
    public List<EnvironmentalThermostatSaveData> thermostats =
        new List<EnvironmentalThermostatSaveData>();
}

public interface IEnvironmentalFieldRuntime
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
    bool TrySetTargetTemperature(
        Vector2Int buildingPosition,
        float targetTemperatureC,
        out string failureReason);
    void MarkTopologyDirty();
    DungeonEnvironmentalFieldSaveData Capture();
    void Restore(
        DungeonEnvironmentalFieldSaveData saveData,
        DungeonGameRestoreReport report = null);
    void Reset();
}

public static class EnvironmentalThresholdRules
{
    public const float NormalAirQuality = 70f;
    public const float PollutedAirQuality = 40f;
    public const float ToxicAirQuality = 20f;
    public const float PrecisionMinimumAirQuality = 50f;
    public const float PrecisionMinimumLight = 50f;
    public const float SurgeryMinimumAirQuality = 70f;
    public const float SurgeryMinimumLight = 70f;

    public static EnvironmentalExposureBand ResolveBand(
        float exposure,
        EnvironmentalExposureBand previousBand)
    {
        float value = Mathf.Clamp(exposure, 0f, 100f);
        float hysteresis = previousBand == EnvironmentalExposureBand.Stable
            ? 0f
            : 5f;
        if (value >= 100f)
        {
            return EnvironmentalExposureBand.Collapse;
        }

        if (value >= 75f - (previousBand >= EnvironmentalExposureBand.Critical
                ? hysteresis
                : 0f))
        {
            return EnvironmentalExposureBand.Critical;
        }

        if (value >= 50f - (previousBand >= EnvironmentalExposureBand.Impaired
                ? hysteresis
                : 0f))
        {
            return EnvironmentalExposureBand.Impaired;
        }

        if (value >= 25f - (previousBand >= EnvironmentalExposureBand.Burden
                ? hysteresis
                : 0f))
        {
            return EnvironmentalExposureBand.Burden;
        }

        return EnvironmentalExposureBand.Stable;
    }

    public static float GetFoodSpoilageMultiplier(float temperatureC)
    {
        return Mathf.Clamp(
            Mathf.Pow(2f, (temperatureC - 20f) / 10f),
            0.25f,
            4f);
    }

    public static bool IsOrganPreservationSafe(float temperatureC)
    {
        return temperatureC >= 2f && temperatureC <= 8f;
    }
}
