using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Environment
{
    public readonly struct EnvironmentalThermostatRecord
    {
        public EnvironmentalThermostatRecord(
            BuildingInstanceId buildingId,
            float targetTemperatureC)
        {
            BuildingId = buildingId;
            TargetTemperatureC = targetTemperatureC;
        }

        public BuildingInstanceId BuildingId { get; }
        public float TargetTemperatureC { get; }
    }

    public sealed class EnvironmentalFieldRestoreCandidate
    {
        internal EnvironmentalFieldRestoreCandidate(
            int width,
            int height,
            IReadOnlyList<EnvironmentalCellSnapshot> cells,
            IReadOnlyList<EnvironmentalThermostatRecord> thermostats)
        {
            Width = width;
            Height = height;
            Cells = cells;
            Thermostats = thermostats;
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<EnvironmentalCellSnapshot> Cells { get; }
        public IReadOnlyList<EnvironmentalThermostatRecord> Thermostats { get; }
    }

    public sealed class EnvironmentalFieldState
    {
        public EnvironmentalFieldState(
            int width,
            int height,
            IReadOnlyList<EnvironmentalCellSnapshot> cells,
            IReadOnlyDictionary<BuildingInstanceId, float> thermostats)
        {
            Width = width;
            Height = height;
            Cells = cells ?? Array.Empty<EnvironmentalCellSnapshot>();
            Thermostats = thermostats
                ?? new Dictionary<BuildingInstanceId, float>();
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<EnvironmentalCellSnapshot> Cells { get; }
        public IReadOnlyDictionary<BuildingInstanceId, float> Thermostats { get; }
    }

    public sealed class EnvironmentalFieldStateStore
    {
        public EnvironmentalFieldStateStore(EnvironmentalFieldState initial)
        {
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public EnvironmentalFieldState Current { get; private set; }

        public void Commit(EnvironmentalFieldRestoreCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            Current = new EnvironmentalFieldState(
                candidate.Width,
                candidate.Height,
                candidate.Cells.ToArray(),
                candidate.Thermostats.ToDictionary(
                    entry => entry.BuildingId,
                    entry => entry.TargetTemperatureC));
        }
    }

    public static class EnvironmentalFieldRestoreRules
    {
        public static EnvironmentalFieldRestoreCandidate Prepare(
            int width,
            int height,
            IReadOnlyList<EnvironmentalCellSnapshot> cells,
            IReadOnlyList<EnvironmentalThermostatRecord> thermostats)
        {
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Environmental field dimensions must be positive.");
            if (cells == null || thermostats == null)
                throw new InvalidOperationException("Environmental field collections are required.");

            HashSet<EnvironmentalCellAddress> addresses = new();
            foreach (EnvironmentalCellSnapshot cell in cells)
            {
                if (cell.Address.X < 0 || cell.Address.X >= width
                    || cell.Address.Y < 0 || cell.Address.Y >= height
                    || !addresses.Add(cell.Address)
                    || !IsFinite(cell.TemperatureC)
                    || !InRange(cell.AirQuality, 0f, 100f)
                    || !InRange(cell.LightLevel, 0f, 100f))
                {
                    throw new InvalidOperationException("Environmental field contains an invalid or duplicate cell.");
                }
            }

            HashSet<BuildingInstanceId> owners = new();
            foreach (EnvironmentalThermostatRecord thermostat in thermostats)
            {
                if (!thermostat.BuildingId.IsValid
                    || !owners.Add(thermostat.BuildingId)
                    || !InRange(thermostat.TargetTemperatureC, -20f, 45f))
                {
                    throw new InvalidOperationException("Environmental field contains an invalid or duplicate thermostat.");
                }
            }

            return new EnvironmentalFieldRestoreCandidate(
                width,
                height,
                cells.ToArray(),
                thermostats.OrderBy(entry => entry.BuildingId.Value, StringComparer.Ordinal).ToArray());
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool InRange(float value, float minimum, float maximum) =>
            IsFinite(value) && value >= minimum && value <= maximum;
    }
}
