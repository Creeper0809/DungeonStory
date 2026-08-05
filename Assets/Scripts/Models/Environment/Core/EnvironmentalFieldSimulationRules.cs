using System;
using System.Collections.Generic;

namespace DungeonStory.Environment
{
    public sealed class EnvironmentalFieldAggregateState
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public float[] Temperature { get; set; } = Array.Empty<float>();
        public float[] NextTemperature { get; set; } = Array.Empty<float>();
        public float[] Air { get; set; } = Array.Empty<float>();
        public float[] NextAir { get; set; } = Array.Empty<float>();
        public float[] Light { get; set; } = Array.Empty<float>();
        public float[] NextLight { get; set; } = Array.Empty<float>();
        public bool[] Barriers { get; set; } = Array.Empty<bool>();
        public bool[] Doors { get; set; } = Array.Empty<bool>();
        public float[] DuctExchange { get; set; } = Array.Empty<float>();
        public bool[] Exterior { get; set; } = Array.Empty<bool>();
        public Dictionary<BuildingInstanceId, float> TargetOverrides { get; } = new();
        public int CachedStructuralVersion { get; set; } = -1;
        public int CachedBuildingVersion { get; set; } = -1;
        public float Accumulator { get; set; }
        public bool TopologyDirty { get; set; } = true;
        public int Version { get; set; }
    }

    public sealed class EnvironmentalFieldAggregateStateStore
    {
        private readonly DungeonRuntimeAggregateRootStore rootStore;

        public EnvironmentalFieldAggregateStateStore(
            DungeonRuntimeAggregateRootStore rootStore)
        {
            this.rootStore = rootStore
                ?? throw new ArgumentNullException(nameof(rootStore));
        }

        public EnvironmentalFieldAggregateState Current =>
            rootStore.GetOrCreate(() => new EnvironmentalFieldAggregateState());

        public void Replace(EnvironmentalFieldAggregateState restored)
        {
            rootStore.Replace(
                restored ?? throw new ArgumentNullException(nameof(restored)));
        }
    }

    public static class EnvironmentalFieldSimulationRules
    {
        public const float TickInterval = 1f;
        public const float MinimumTemperature = -50f;
        public const float MaximumTemperature = 80f;
        public const float MinimumFieldLevel = 0f;
        public const float MaximumFieldLevel = 100f;

        private const float IndoorTemperatureExchange = 0.08f;
        private const float ExteriorTemperatureExchange = 0.35f;
        private const float NormalCellExchange = 0.12f;
        private const float DoorCellExchange = 0.55f;
        private const float ExteriorAirExchange = 0.5f;
        private const float IndoorAirExchange = 0.015f;
        private const float ExteriorLightExchange = 0.5f;
        private const float IndoorLightExchange = 0.08f;
        private const float LightNeighborExchange = 0.6f;

        public static EnvironmentalFieldAggregateState CreateInitialized(
            int width,
            int height,
            float outdoorTemperature,
            bool[] exterior,
            int version)
        {
            int count = width * height;
            if (width <= 0
                || height <= 0
                || exterior == null
                || exterior.Length != count)
            {
                throw new ArgumentException(
                    "Environmental field dimensions and exterior topology must match.");
            }

            EnvironmentalFieldAggregateState created = new()
            {
                Width = width,
                Height = height,
                Temperature = new float[count],
                NextTemperature = new float[count],
                Air = new float[count],
                NextAir = new float[count],
                Light = new float[count],
                NextLight = new float[count],
                Barriers = new bool[count],
                Doors = new bool[count],
                DuctExchange = new float[count],
                Exterior = (bool[])exterior.Clone(),
                Version = version,
                TopologyDirty = true
            };
            for (int index = 0; index < count; index++)
            {
                created.Temperature[index] = outdoorTemperature;
                created.NextTemperature[index] = outdoorTemperature;
                created.Air[index] = MaximumFieldLevel;
                created.NextAir[index] = MaximumFieldLevel;
                float baseLight = GetBaseLight(created.Exterior[index]);
                created.Light[index] = baseLight;
                created.NextLight[index] = baseLight;
            }

            return created;
        }

        public static void StepDiffusion(
            EnvironmentalFieldAggregateState state,
            float outdoorTemperature,
            float deltaTime)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int width = state.Width;
            int height = state.Height;
            float[] currentTemperature = state.Temperature;
            float[] outputTemperature = state.NextTemperature;
            float[] currentAir = state.Air;
            float[] outputAir = state.NextAir;
            float[] currentLight = state.Light;
            float[] outputLight = state.NextLight;
            bool[] blockedCells = state.Barriers;
            bool[] doorCells = state.Doors;
            float[] ductCells = state.DuctExchange;
            bool[] exteriorCells = state.Exterior;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (blockedCells[index])
                    {
                        outputTemperature[index] = currentTemperature[index];
                        outputAir[index] = currentAir[index];
                        outputLight[index] = 0f;
                        continue;
                    }

                    float temperatureDelta = 0f;
                    float airDelta = 0f;
                    float lightDelta = 0f;
                    int neighborCount = 0;
                    AccumulateNeighbor(x - 1, y);
                    AccumulateNeighbor(x + 1, y);
                    AccumulateNeighbor(x, y + 1);
                    AccumulateNeighbor(x, y - 1);

                    if (neighborCount > 0)
                    {
                        temperatureDelta /= neighborCount;
                        airDelta /= neighborCount;
                        lightDelta /= neighborCount;
                    }

                    float outdoorExchange = exteriorCells[index]
                        ? ExteriorTemperatureExchange
                        : IndoorTemperatureExchange;
                    temperatureDelta +=
                        (outdoorTemperature - currentTemperature[index])
                        * outdoorExchange;
                    airDelta += (MaximumFieldLevel - currentAir[index])
                        * (exteriorCells[index]
                            ? ExteriorAirExchange
                            : IndoorAirExchange);
                    float baseLight = GetBaseLight(exteriorCells[index]);
                    lightDelta += (baseLight - currentLight[index])
                        * (exteriorCells[index]
                            ? ExteriorLightExchange
                            : IndoorLightExchange);

                    outputTemperature[index] = Clamp(
                        currentTemperature[index] + temperatureDelta * deltaTime,
                        MinimumTemperature,
                        MaximumTemperature);
                    outputAir[index] = Clamp(
                        currentAir[index] + airDelta * deltaTime,
                        MinimumFieldLevel,
                        MaximumFieldLevel);
                    outputLight[index] = Clamp(
                        currentLight[index] + lightDelta * deltaTime,
                        MinimumFieldLevel,
                        MaximumFieldLevel);

                    void AccumulateNeighbor(int neighborX, int neighborY)
                    {
                        if (neighborX < 0
                            || neighborX >= width
                            || neighborY < 0
                            || neighborY >= height)
                        {
                            return;
                        }

                        int neighbor = neighborY * width + neighborX;
                        if (blockedCells[neighbor])
                        {
                            return;
                        }

                        float exchange = Math.Max(
                            doorCells[index] || doorCells[neighbor]
                                ? DoorCellExchange
                                : NormalCellExchange,
                            Math.Max(ductCells[index], ductCells[neighbor]));
                        temperatureDelta +=
                            (currentTemperature[neighbor] - currentTemperature[index])
                            * exchange;
                        airDelta += (currentAir[neighbor] - currentAir[index])
                            * exchange;
                        lightDelta += (currentLight[neighbor] - currentLight[index])
                            * exchange
                            * LightNeighborExchange;
                        neighborCount++;
                    }
                }
            }
        }

        public static float ApplyThermalSource(
            float currentTemperature,
            ThermalEmitterMode mode,
            float targetTemperature,
            float degreesPerSecond,
            float distance01,
            float deltaTime)
        {
            float amount = degreesPerSecond * (1f - distance01) * deltaTime;
            return mode switch
            {
                ThermalEmitterMode.Heat => Math.Min(
                    targetTemperature,
                    currentTemperature + amount),
                ThermalEmitterMode.Cool => Math.Max(
                    targetTemperature,
                    currentTemperature - amount),
                _ => MoveTowards(currentTemperature, targetTemperature, amount)
            };
        }

        public static float ApplyCoolerExhaust(
            float currentTemperature,
            float degreesPerSecond,
            float exhaustHeatMultiplier,
            float deltaTime)
        {
            return Clamp(
                currentTemperature
                    + degreesPerSecond * exhaustHeatMultiplier * deltaTime,
                MinimumTemperature,
                MaximumTemperature);
        }

        public static float ApplyAirSource(
            float currentAirQuality,
            float targetAirQuality,
            float qualityPerSecond,
            float distance01,
            float deltaTime)
        {
            return MoveTowards(
                currentAirQuality,
                targetAirQuality,
                qualityPerSecond * (1f - distance01) * deltaTime);
        }

        public static float ApplyLightSource(
            float currentLight,
            float intensity,
            float distance01)
        {
            float peak = Clamp(
                intensity * MaximumFieldLevel,
                MinimumFieldLevel,
                MaximumFieldLevel);
            return Math.Max(currentLight, peak * (1f - distance01));
        }

        public static float ClampThermostatTarget(
            float requested,
            float firstLimit,
            float secondLimit)
        {
            return Clamp(
                requested,
                Math.Min(firstLimit, secondLimit),
                Math.Max(firstLimit, secondLimit));
        }

        public static float GetBaseLight(bool isExterior)
        {
            return isExterior ? 70f : 20f;
        }

        public static void CompleteStep(EnvironmentalFieldAggregateState state)
        {
            (state.Temperature, state.NextTemperature) =
                (state.NextTemperature, state.Temperature);
            (state.Air, state.NextAir) = (state.NextAir, state.Air);
            (state.Light, state.NextLight) = (state.NextLight, state.Light);
            Touch(state);
        }

        public static void Touch(EnvironmentalFieldAggregateState state)
        {
            unchecked
            {
                state.Version++;
            }
        }

        private static float MoveTowards(
            float current,
            float target,
            float maximumDelta)
        {
            float delta = target - current;
            if (Math.Abs(delta) <= maximumDelta)
            {
                return target;
            }

            return current + Math.Sign(delta) * maximumDelta;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
        }
    }
}
