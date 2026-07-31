using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class EnvironmentalFieldRuntime :
    IEnvironmentalFieldRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickMarker =
        new ProfilerMarker("Environment.Field.Tick");
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };

    private const float TickInterval = 1f;
    private const float IndoorTemperatureExchange = 0.08f;
    private const float ExteriorTemperatureExchange = 0.35f;
    private const float NormalCellExchange = 0.12f;
    private const float DoorCellExchange = 0.55f;

    private readonly IGridSystemProvider gridProvider;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ISurvivalEnvironmentQuery survivalEnvironment;
    private readonly IElectricalNetworkRuntime power;
    private readonly IGameClock clock;
    private readonly List<SourceDescriptor> sources =
        new List<SourceDescriptor>();
    private readonly Dictionary<Vector2Int, float> targetOverrides =
        new Dictionary<Vector2Int, float>();

    private Grid grid;
    private float[] temperature;
    private float[] nextTemperature;
    private float[] air;
    private float[] nextAir;
    private float[] light;
    private float[] nextLight;
    private bool[] barriers;
    private bool[] doors;
    private float[] ductExchange;
    private bool[] exterior;
    private int cachedStructuralVersion = -1;
    private int cachedBuildingVersion = -1;
    private float accumulator;
    private bool topologyDirty = true;

    public EnvironmentalFieldRuntime(
        IGridSystemProvider gridProvider,
        IBuildingWorldQuery buildingWorld,
        ISurvivalEnvironmentQuery survivalEnvironment,
        IElectricalNetworkRuntime power,
        IGameClock clock)
    {
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.survivalEnvironment = survivalEnvironment
            ?? throw new ArgumentNullException(nameof(survivalEnvironment));
        this.power = power
            ?? throw new ArgumentNullException(nameof(power));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public int Version { get; private set; }
    public bool IsInitialized => grid != null && temperature != null;

    public void Tick()
    {
        if (clock.IsPaused || !gridProvider.TryGetGrid(out Grid loadedGrid))
        {
            return;
        }

        EnsureInitialized(loadedGrid);
        accumulator += Mathf.Max(0f, clock.DeltaTime);
        while (accumulator >= TickInterval)
        {
            accumulator -= TickInterval;
            using (TickMarker.Auto())
            {
                Step(TickInterval);
            }
        }
    }

    public bool TryGetCell(
        Vector2Int position,
        out EnvironmentalCellSnapshot snapshot)
    {
        if (!IsInitialized
            || !grid.TryGetCellIndex(position, out int index))
        {
            snapshot = default;
            return false;
        }

        snapshot = new EnvironmentalCellSnapshot(
            position,
            temperature[index],
            air[index],
            light[index]);
        return true;
    }

    public bool TryGetAverage(
        IReadOnlyList<Vector2Int> positions,
        out EnvironmentalCellSnapshot snapshot)
    {
        if (!IsInitialized || positions == null || positions.Count == 0)
        {
            snapshot = default;
            return false;
        }

        float temperatureTotal = 0f;
        float airTotal = 0f;
        float lightTotal = 0f;
        int count = 0;
        Vector2Int anchor = positions[0];
        for (int i = 0; i < positions.Count; i++)
        {
            if (!grid.TryGetCellIndex(positions[i], out int index))
            {
                continue;
            }

            temperatureTotal += temperature[index];
            airTotal += air[index];
            lightTotal += light[index];
            count++;
        }

        if (count == 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = new EnvironmentalCellSnapshot(
            anchor,
            temperatureTotal / count,
            airTotal / count,
            lightTotal / count);
        return true;
    }

    public float GetFoodSpoilageMultiplier(Vector2Int position)
    {
        if (!TryGetCell(position, out EnvironmentalCellSnapshot snapshot))
        {
            throw new InvalidOperationException(
                $"Environmental cell {position} is unavailable.");
        }

        return EnvironmentalThresholdRules.GetFoodSpoilageMultiplier(
            snapshot.TemperatureC);
    }

    public bool IsOrganPreservationSafe(Vector2Int position)
    {
        return TryGetCell(position, out EnvironmentalCellSnapshot snapshot)
            && EnvironmentalThresholdRules.IsOrganPreservationSafe(
                snapshot.TemperatureC);
    }

    public bool TryGetTargetTemperature(
        Vector2Int buildingPosition,
        out float targetTemperatureC)
    {
        if (targetOverrides.TryGetValue(
            buildingPosition,
            out targetTemperatureC))
        {
            return true;
        }

        SourceDescriptor source = FindConfigurableThermalSource(
            buildingPosition);
        if (source?.Thermal == null)
        {
            targetTemperatureC = 0f;
            return false;
        }

        targetTemperatureC = source.Thermal.targetTemperatureC;
        return true;
    }

    public bool TrySetTargetTemperature(
        Vector2Int buildingPosition,
        float targetTemperatureC,
        out string failureReason)
    {
        SourceDescriptor source = FindConfigurableThermalSource(
            buildingPosition);
        if (source?.Thermal == null)
        {
            failureReason = "이 시설은 목표 온도를 설정할 수 없습니다.";
            return false;
        }

        BuildingThermalEmitterAbility emitter = source.Thermal;
        float minimum = Mathf.Min(
            emitter.minimumTargetTemperatureC,
            emitter.maximumTargetTemperatureC);
        float maximum = Mathf.Max(
            emitter.minimumTargetTemperatureC,
            emitter.maximumTargetTemperatureC);
        targetOverrides[buildingPosition] = Mathf.Clamp(
            targetTemperatureC,
            minimum,
            maximum);
        failureReason = string.Empty;
        Touch();
        return true;
    }

    public void MarkTopologyDirty()
    {
        topologyDirty = true;
    }

    public DungeonEnvironmentalFieldSaveData Capture()
    {
        DungeonEnvironmentalFieldSaveData result =
            new DungeonEnvironmentalFieldSaveData();
        if (!IsInitialized)
        {
            return result;
        }

        result.width = grid.width;
        result.height = grid.height;
        float outdoorTemperature = GetOutdoorTemperature();
        int count = grid.width * grid.height;
        for (int index = 0; index < count; index++)
        {
            Vector2Int position = grid.GetPositionFromCellIndex(index);
            float baseLight = GetBaseLight(exterior[index]);
            if (Mathf.Abs(temperature[index] - outdoorTemperature) < 0.05f
                && Mathf.Abs(air[index] - 100f) < 0.05f
                && Mathf.Abs(light[index] - baseLight) < 0.05f)
            {
                continue;
            }

            result.cells.Add(new EnvironmentalCellSaveData
            {
                x = position.x,
                y = position.y,
                temperatureC = temperature[index],
                airQuality = air[index],
                lightLevel = light[index]
            });
        }

        foreach (KeyValuePair<Vector2Int, float> thermostat in targetOverrides)
        {
            result.thermostats.Add(new EnvironmentalThermostatSaveData
            {
                x = thermostat.Key.x,
                y = thermostat.Key.y,
                targetTemperatureC = thermostat.Value
            });
        }

        return result;
    }

    public void Restore(
        DungeonEnvironmentalFieldSaveData saveData,
        DungeonGameRestoreReport report = null)
    {
        if (!gridProvider.TryGetGrid(out Grid loadedGrid))
        {
            report?.AddWarning(
                "Environment field restore was deferred because the grid is not ready.");
            return;
        }

        EnsureInitialized(loadedGrid, true);
        DungeonEnvironmentalFieldSaveData source =
            saveData ?? new DungeonEnvironmentalFieldSaveData();
        if (source.version != DungeonEnvironmentalFieldSaveData.CurrentVersion)
        {
            report?.AddError(
                $"Unsupported environment field version {source.version}.");
            return;
        }

        if ((source.width > 0 && source.width != grid.width)
            || (source.height > 0 && source.height != grid.height))
        {
            report?.AddWarning(
                "Environment field dimensions changed; valid saved cells were restored.");
        }

        foreach (EnvironmentalCellSaveData cell in source.cells
                     ?? Enumerable.Empty<EnvironmentalCellSaveData>())
        {
            Vector2Int position = new Vector2Int(cell.x, cell.y);
            if (!grid.TryGetCellIndex(position, out int index))
            {
                report?.AddWarning(
                    $"Environment cell {position} was outside the current grid.");
                continue;
            }

            temperature[index] = Mathf.Clamp(cell.temperatureC, -50f, 80f);
            air[index] = Mathf.Clamp(cell.airQuality, 0f, 100f);
            light[index] = Mathf.Clamp(cell.lightLevel, 0f, 100f);
        }

        targetOverrides.Clear();
        foreach (EnvironmentalThermostatSaveData thermostat
                 in source.thermostats
                 ?? Enumerable.Empty<EnvironmentalThermostatSaveData>())
        {
            Vector2Int position = new Vector2Int(
                thermostat.x,
                thermostat.y);
            if (!TrySetTargetTemperature(
                position,
                thermostat.targetTemperatureC,
                out string failureReason))
            {
                report?.AddWarning(
                    $"Thermostat at {position} was not restored: "
                    + failureReason);
            }
        }

        Array.Copy(temperature, nextTemperature, temperature.Length);
        Array.Copy(air, nextAir, air.Length);
        Array.Copy(light, nextLight, light.Length);
        Touch();
    }

    public void Reset()
    {
        grid = null;
        temperature = null;
        nextTemperature = null;
        air = null;
        nextAir = null;
        light = null;
        nextLight = null;
        barriers = null;
        doors = null;
        ductExchange = null;
        exterior = null;
        sources.Clear();
        targetOverrides.Clear();
        cachedStructuralVersion = -1;
        cachedBuildingVersion = -1;
        accumulator = 0f;
        topologyDirty = true;
        Touch();
    }

    private void EnsureInitialized(Grid loadedGrid, bool forceReset = false)
    {
        if (!forceReset
            && ReferenceEquals(grid, loadedGrid)
            && temperature != null
            && temperature.Length == loadedGrid.width * loadedGrid.height)
        {
            RefreshTopologyIfNeeded();
            return;
        }

        grid = loadedGrid;
        int count = grid.width * grid.height;
        temperature = new float[count];
        nextTemperature = new float[count];
        air = new float[count];
        nextAir = new float[count];
        light = new float[count];
        nextLight = new float[count];
        barriers = new bool[count];
        doors = new bool[count];
        ductExchange = new float[count];
        exterior = new bool[count];
        RebuildTopology();
        float outdoorTemperature = GetOutdoorTemperature();
        for (int index = 0; index < count; index++)
        {
            temperature[index] = outdoorTemperature;
            nextTemperature[index] = outdoorTemperature;
            air[index] = 100f;
            nextAir[index] = 100f;
            float baseLight = GetBaseLight(exterior[index]);
            light[index] = baseLight;
            nextLight[index] = baseLight;
        }

        Touch();
    }

    private void Step(float deltaTime)
    {
        RefreshTopologyIfNeeded();
        float outdoorTemperature = GetOutdoorTemperature();
        int width = grid.width;
        int height = grid.height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (barriers[index])
                {
                    nextTemperature[index] = temperature[index];
                    nextAir[index] = air[index];
                    nextLight[index] = 0f;
                    continue;
                }

                float temperatureDelta = 0f;
                float airDelta = 0f;
                float lightDelta = 0f;
                int neighborCount = 0;
                Vector2Int position = new Vector2Int(x, y);
                for (int directionIndex = 0;
                     directionIndex < CardinalDirections.Length;
                     directionIndex++)
                {
                    Vector2Int neighborPosition =
                        position + CardinalDirections[directionIndex];
                    if (!grid.TryGetCellIndex(neighborPosition, out int neighbor)
                        || barriers[neighbor])
                    {
                        continue;
                    }

                    float exchange = Mathf.Max(
                        doors[index] || doors[neighbor]
                            ? DoorCellExchange
                            : NormalCellExchange,
                        Mathf.Max(
                            ductExchange[index],
                            ductExchange[neighbor]));
                    temperatureDelta +=
                        (temperature[neighbor] - temperature[index]) * exchange;
                    airDelta += (air[neighbor] - air[index]) * exchange;
                    lightDelta += (light[neighbor] - light[index])
                        * exchange
                        * 0.6f;
                    neighborCount++;
                }

                if (neighborCount > 0)
                {
                    temperatureDelta /= neighborCount;
                    airDelta /= neighborCount;
                    lightDelta /= neighborCount;
                }

                float outdoorExchange = exterior[index]
                    ? ExteriorTemperatureExchange
                    : IndoorTemperatureExchange;
                temperatureDelta +=
                    (outdoorTemperature - temperature[index]) * outdoorExchange;
                airDelta += (100f - air[index])
                    * (exterior[index] ? 0.5f : 0.015f);
                float baseLight = GetBaseLight(exterior[index]);
                lightDelta += (baseLight - light[index])
                    * (exterior[index] ? 0.5f : 0.08f);

                nextTemperature[index] = Mathf.Clamp(
                    temperature[index] + temperatureDelta * deltaTime,
                    -50f,
                    80f);
                nextAir[index] = Mathf.Clamp(
                    air[index] + airDelta * deltaTime,
                    0f,
                    100f);
                nextLight[index] = Mathf.Clamp(
                    light[index] + lightDelta * deltaTime,
                    0f,
                    100f);
            }
        }

        ApplySources(deltaTime);
        Swap(ref temperature, ref nextTemperature);
        Swap(ref air, ref nextAir);
        Swap(ref light, ref nextLight);
        Touch();
    }

    private void ApplySources(float deltaTime)
    {
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            SourceDescriptor source = sources[sourceIndex];
            if (source.Building == null
                || source.Building.isDestroy
                || source.RequiresPower && !power.IsPowered(source.Building))
            {
                continue;
            }

            if (source.Thermal != null)
            {
                ApplyThermalSource(source, deltaTime);
            }

            if (source.Air != null)
            {
                ApplyAirSource(source, deltaTime);
            }

            if (source.Light != null)
            {
                ApplyLightSource(source);
            }
        }
    }

    private void ApplyThermalSource(SourceDescriptor source, float deltaTime)
    {
        BuildingThermalEmitterAbility emitter = source.Thermal;
        float targetTemperatureC = targetOverrides.TryGetValue(
            source.Position,
            out float configuredTarget)
                ? configuredTarget
                : emitter.targetTemperatureC;
        VisitRadius(
            source.Position,
            emitter.radius,
            (index, distance01) =>
            {
                float amount = emitter.degreesPerSecond
                    * (1f - distance01)
                    * deltaTime;
                switch (emitter.mode)
                {
                    case ThermalEmitterMode.Heat:
                        nextTemperature[index] = Mathf.Min(
                            targetTemperatureC,
                            nextTemperature[index] + amount);
                        break;
                    case ThermalEmitterMode.Cool:
                        nextTemperature[index] = Mathf.Max(
                            targetTemperatureC,
                            nextTemperature[index] - amount);
                        break;
                    default:
                        nextTemperature[index] = Mathf.MoveTowards(
                            nextTemperature[index],
                            targetTemperatureC,
                            amount);
                        break;
                }
            });

        if (emitter.mode == ThermalEmitterMode.Cool)
        {
            Vector2Int exhaustPosition =
                source.Position + emitter.exhaustOffset;
            if (grid.TryGetCellIndex(exhaustPosition, out int exhaustIndex)
                && !barriers[exhaustIndex])
            {
                nextTemperature[exhaustIndex] = Mathf.Clamp(
                    nextTemperature[exhaustIndex]
                    + emitter.degreesPerSecond
                    * emitter.exhaustHeatMultiplier
                    * deltaTime,
                    -50f,
                    80f);
            }
        }
    }

    private SourceDescriptor FindConfigurableThermalSource(
        Vector2Int buildingPosition)
    {
        if (IsInitialized)
        {
            RefreshTopologyIfNeeded();
        }

        return sources.FirstOrDefault(source =>
            source != null
            && source.Position == buildingPosition
            && source.Thermal?.playerConfigurable == true);
    }

    private void ApplyAirSource(SourceDescriptor source, float deltaTime)
    {
        BuildingAirExchangeAbility exchange = source.Air;
        float target = exchange.exchangesWithOutside
            ? 100f
            : exchange.targetAirQuality;
        VisitRadius(
            source.Position,
            exchange.radius,
            (index, distance01) =>
            {
                nextAir[index] = Mathf.MoveTowards(
                    nextAir[index],
                    target,
                    exchange.qualityPerSecond
                    * (1f - distance01)
                    * deltaTime);
            });
    }

    private void ApplyLightSource(SourceDescriptor source)
    {
        int radius = Mathf.Max(1, Mathf.CeilToInt(source.Light.radius));
        float peak = Mathf.Clamp(source.Light.intensity * 100f, 0f, 100f);
        VisitRadius(
            source.Position,
            radius,
            (index, distance01) =>
            {
                nextLight[index] = Mathf.Max(
                    nextLight[index],
                    peak * (1f - distance01));
            });
    }

    private void VisitRadius(
        Vector2Int center,
        int radius,
        Action<int, float> visitor)
    {
        int clampedRadius = Mathf.Max(0, radius);
        for (int y = -clampedRadius; y <= clampedRadius; y++)
        {
            for (int x = -clampedRadius; x <= clampedRadius; x++)
            {
                int distance = Mathf.Abs(x) + Mathf.Abs(y);
                if (distance > clampedRadius)
                {
                    continue;
                }

                Vector2Int position = center + new Vector2Int(x, y);
                if (!grid.TryGetCellIndex(position, out int index)
                    || barriers[index]
                    || !HasLineOfEffect(center, position))
                {
                    continue;
                }

                float distance01 = clampedRadius == 0
                    ? 0f
                    : (float)distance / (clampedRadius + 1);
                visitor(index, distance01);
            }
        }
    }

    private bool HasLineOfEffect(Vector2Int from, Vector2Int to)
    {
        int x = from.x;
        int y = from.y;
        int deltaX = Mathf.Abs(to.x - from.x);
        int deltaY = Mathf.Abs(to.y - from.y);
        int stepX = from.x < to.x ? 1 : -1;
        int stepY = from.y < to.y ? 1 : -1;
        int error = deltaX - deltaY;
        while (x != to.x || y != to.y)
        {
            int doubled = error * 2;
            if (doubled > -deltaY)
            {
                error -= deltaY;
                x += stepX;
            }

            if (doubled < deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            Vector2Int position = new Vector2Int(x, y);
            if (!grid.TryGetCellIndex(position, out int index))
            {
                return false;
            }

            if (barriers[index])
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshTopologyIfNeeded()
    {
        if (topologyDirty
            || cachedStructuralVersion != grid.StructuralVersion
            || cachedBuildingVersion != buildingWorld.BuildingVersion)
        {
            RebuildTopology();
        }
    }

    private void RebuildTopology()
    {
        sources.Clear();
        Array.Clear(barriers, 0, barriers.Length);
        Array.Clear(doors, 0, doors.Length);
        Array.Clear(ductExchange, 0, ductExchange.Length);
        for (int index = 0; index < exterior.Length; index++)
        {
            GridCell cell = grid.GetGridCell(
                grid.GetPositionFromCellIndex(index));
            exterior[index] = cell == null
                || cell.AreaType != GridCellAreaType.DungeonInterior;
        }

        IReadOnlyList<BuildableObject> buildings =
            buildingWorld.Buildings ?? Array.Empty<BuildableObject>();
        for (int buildingIndex = 0;
             buildingIndex < buildings.Count;
             buildingIndex++)
        {
            BuildableObject building = buildings[buildingIndex];
            if (building == null
                || building.isDestroy
                || building.BuildingData == null)
            {
                continue;
            }

            IReadOnlyList<Vector2Int> positions =
                building.buildPoses != null && building.buildPoses.Count > 0
                    ? building.buildPoses
                    : new[] { building.centerPos };
            bool wall = RoomDetector.IsWall(building);
            bool door = RoomDetector.IsDoor(building);
            BuildingAirDuctAbility duct =
                building.BuildingData.GetAbility<BuildingAirDuctAbility>();
            for (int positionIndex = 0;
                 positionIndex < positions.Count;
                 positionIndex++)
            {
                if (!grid.TryGetCellIndex(positions[positionIndex], out int index))
                {
                    continue;
                }

                barriers[index] |= wall && !door;
                doors[index] |= door;
                if (duct != null)
                {
                    ductExchange[index] = Mathf.Max(
                        ductExchange[index],
                        duct.exchangeRate);
                }
            }

            BuildingThermalEmitterAbility thermal =
                building.BuildingData
                    .GetAbility<BuildingThermalEmitterAbility>();
            BuildingAirExchangeAbility airExchange =
                building.BuildingData
                    .GetAbility<BuildingAirExchangeAbility>();
            BuildingLightingAbility lighting =
                building.BuildingData.GetAbility<BuildingLightingAbility>();
            BuildingTemperatureAbility legacyTemperature =
                building.BuildingData.GetAbility<BuildingTemperatureAbility>();
            if (thermal == null && legacyTemperature != null
                && building.Facility?.SupportsRole(FacilityRole.Rest) != true
                && !Mathf.Approximately(
                    legacyTemperature.roomTemperatureOffset,
                    0f))
            {
                thermal = new BuildingThermalEmitterAbility
                {
                    mode = legacyTemperature.roomTemperatureOffset >= 0f
                        ? ThermalEmitterMode.Heat
                        : ThermalEmitterMode.Cool,
                    targetTemperatureC =
                        legacyTemperature.roomTemperatureOffset >= 0f
                            ? 28f
                            : 8f,
                    degreesPerSecond = Mathf.Clamp(
                        Mathf.Abs(
                            legacyTemperature.roomTemperatureOffset)
                        * 0.25f,
                        0.25f,
                        3f),
                    radius = 2,
                    requiresPower = false
                };
            }

            if (thermal == null && airExchange == null && lighting == null)
            {
                continue;
            }

            sources.Add(new SourceDescriptor
            {
                Building = building,
                Position = building.centerPos,
                Thermal = thermal,
                Air = airExchange,
                Light = lighting,
                RequiresPower =
                    thermal?.requiresPower == true
                    || airExchange?.requiresPower == true
            });
        }

        cachedStructuralVersion = grid.StructuralVersion;
        cachedBuildingVersion = buildingWorld.BuildingVersion;
        topologyDirty = false;
    }

    private float GetOutdoorTemperature()
    {
        return survivalEnvironment.GetEnvironmentSnapshot().OutdoorTemperature;
    }

    private static float GetBaseLight(bool isExterior)
    {
        return isExterior ? 70f : 20f;
    }

    private void Touch()
    {
        unchecked
        {
            Version++;
        }
    }

    private static void Swap<T>(ref T left, ref T right)
    {
        (left, right) = (right, left);
    }

    private sealed class SourceDescriptor
    {
        public BuildableObject Building;
        public Vector2Int Position;
        public BuildingThermalEmitterAbility Thermal;
        public BuildingAirExchangeAbility Air;
        public BuildingLightingAbility Light;
        public bool RequiresPower;
    }
}
