using System;
using DungeonStory.Rooms;

namespace DungeonStory.Environment
{
    public enum ExposureBand
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

    public readonly struct EnvironmentalCellAddress : IEquatable<EnvironmentalCellAddress>
    {
        public EnvironmentalCellAddress(int x, int y, RoomId roomId = default)
        {
            X = x;
            Y = y;
            RoomId = roomId;
        }

        public int X { get; }
        public int Y { get; }
        public RoomId RoomId { get; }

        public bool Equals(EnvironmentalCellAddress other) =>
            X == other.X && Y == other.Y;

        public override bool Equals(object obj) =>
            obj is EnvironmentalCellAddress other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    public readonly struct EnvironmentalCellSnapshot
    {
        public EnvironmentalCellSnapshot(
            EnvironmentalCellAddress address,
            float temperatureC,
            float airQuality,
            float lightLevel)
        {
            Address = address;
            TemperatureC = temperatureC;
            AirQuality = airQuality;
            LightLevel = lightLevel;
        }

        public EnvironmentalCellAddress Address { get; }
        public float TemperatureC { get; }
        public float AirQuality { get; }
        public float LightLevel { get; }
    }

    public readonly struct ThermalProtectionSnapshot
    {
        public ThermalProtectionSnapshot(
            float comfortMinimumOffset,
            float comfortMaximumOffset,
            float safeMinimumOffset,
            float safeMaximumOffset,
            float coldExposureMultiplier,
            float heatExposureMultiplier)
        {
            ComfortMinimumOffset = comfortMinimumOffset;
            ComfortMaximumOffset = comfortMaximumOffset;
            SafeMinimumOffset = safeMinimumOffset;
            SafeMaximumOffset = safeMaximumOffset;
            ColdExposureMultiplier = EnvironmentalMath.Clamp(coldExposureMultiplier, 0.05f, 2f);
            HeatExposureMultiplier = EnvironmentalMath.Clamp(heatExposureMultiplier, 0.05f, 2f);
        }

        public float ComfortMinimumOffset { get; }
        public float ComfortMaximumOffset { get; }
        public float SafeMinimumOffset { get; }
        public float SafeMaximumOffset { get; }
        public float ColdExposureMultiplier { get; }
        public float HeatExposureMultiplier { get; }

        public static ThermalProtectionSnapshot None =>
            new ThermalProtectionSnapshot(0f, 0f, 0f, 0f, 1f, 1f);

        public ThermalProtectionSnapshot Combine(ThermalProtectionSnapshot other) =>
            new ThermalProtectionSnapshot(
                ComfortMinimumOffset + other.ComfortMinimumOffset,
                ComfortMaximumOffset + other.ComfortMaximumOffset,
                SafeMinimumOffset + other.SafeMinimumOffset,
                SafeMaximumOffset + other.SafeMaximumOffset,
                ColdExposureMultiplier * other.ColdExposureMultiplier,
                HeatExposureMultiplier * other.HeatExposureMultiplier);
    }

    public sealed class EnvironmentalWorkwearDefinitionSnapshot
    {
        private readonly string[] allowedSpecies;

        public EnvironmentalWorkwearDefinitionSnapshot(
            string definitionId,
            string itemDefinitionId,
            string displayName,
            string description,
            string[] allowedSpecies,
            ThermalProtectionSnapshot protection,
            string requiredResearchId)
        {
            DefinitionId = definitionId?.Trim() ?? string.Empty;
            ItemDefinitionId = itemDefinitionId?.Trim() ?? string.Empty;
            DisplayName = displayName?.Trim() ?? string.Empty;
            Description = description?.Trim() ?? string.Empty;
            this.allowedSpecies = allowedSpecies ?? Array.Empty<string>();
            Protection = protection;
            RequiredResearchId = requiredResearchId?.Trim() ?? string.Empty;
        }

        public string DefinitionId { get; }
        public string ItemDefinitionId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public ThermalProtectionSnapshot Protection { get; }
        public string RequiredResearchId { get; }

        public bool AllowsSpecies(string speciesTag)
        {
            if (allowedSpecies.Length == 0) return true;
            string candidate = speciesTag?.Trim() ?? string.Empty;
            for (int index = 0; index < allowedSpecies.Length; index++)
            {
                if (string.Equals(
                    allowedSpecies[index]?.Trim(),
                    candidate,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public readonly struct ThermalRangeSnapshot
    {
        public ThermalRangeSnapshot(
            float comfortMinimum,
            float comfortMaximum,
            float safeMinimum,
            float safeMaximum,
            float lethalMinimum,
            float lethalMaximum)
        {
            ComfortMinimum = comfortMinimum;
            ComfortMaximum = comfortMaximum;
            SafeMinimum = safeMinimum;
            SafeMaximum = safeMaximum;
            LethalMinimum = lethalMinimum;
            LethalMaximum = lethalMaximum;
        }

        public float ComfortMinimum { get; }
        public float ComfortMaximum { get; }
        public float SafeMinimum { get; }
        public float SafeMaximum { get; }
        public float LethalMinimum { get; }
        public float LethalMaximum { get; }

        public ThermalRangeSnapshot Apply(ThermalProtectionSnapshot protection)
        {
            float safeMinimum = Math.Max(
                LethalMinimum + 2f,
                SafeMinimum + protection.SafeMinimumOffset);
            float safeMaximum = Math.Min(
                LethalMaximum - 2f,
                SafeMaximum + protection.SafeMaximumOffset);
            return new ThermalRangeSnapshot(
                EnvironmentalMath.Clamp(
                    ComfortMinimum + protection.ComfortMinimumOffset,
                    safeMinimum,
                    LethalMaximum - 2f),
                EnvironmentalMath.Clamp(
                    ComfortMaximum + protection.ComfortMaximumOffset,
                    LethalMinimum + 2f,
                    safeMaximum),
                safeMinimum,
                safeMaximum,
                LethalMinimum,
                LethalMaximum);
        }
    }

    public readonly struct ThermalExposureRate
    {
        public ThermalExposureRate(float coldRate, float heatRate, bool lethal)
        {
            ColdRate = Math.Max(0f, coldRate);
            HeatRate = Math.Max(0f, heatRate);
            Lethal = lethal;
        }

        public float ColdRate { get; }
        public float HeatRate { get; }
        public bool Lethal { get; }
    }

    public readonly struct CharacterExposureStepInput
    {
        public CharacterExposureStepInput(
            float coldExposure,
            float heatExposure,
            float airborneExposure,
            float visualStrain,
            ExposureBand physiologicalBand,
            ExposureBand visualBand,
            float coldRate,
            float heatRate,
            float airRate,
            float visualRate,
            bool thermalComfortable,
            bool airComfortable,
            bool visualComfortable,
            float deltaTime)
        {
            ColdExposure = EnvironmentalMath.Clamp(coldExposure, 0f, 100f);
            HeatExposure = EnvironmentalMath.Clamp(heatExposure, 0f, 100f);
            AirborneExposure = EnvironmentalMath.Clamp(
                airborneExposure,
                0f,
                100f);
            VisualStrain = EnvironmentalMath.Clamp(visualStrain, 0f, 100f);
            PhysiologicalBand = physiologicalBand;
            VisualBand = visualBand;
            ColdRate = Math.Max(0f, coldRate);
            HeatRate = Math.Max(0f, heatRate);
            AirRate = Math.Max(0f, airRate);
            VisualRate = Math.Max(0f, visualRate);
            ThermalComfortable = thermalComfortable;
            AirComfortable = airComfortable;
            VisualComfortable = visualComfortable;
            DeltaTime = Math.Max(0f, deltaTime);
        }

        public float ColdExposure { get; }
        public float HeatExposure { get; }
        public float AirborneExposure { get; }
        public float VisualStrain { get; }
        public ExposureBand PhysiologicalBand { get; }
        public ExposureBand VisualBand { get; }
        public float ColdRate { get; }
        public float HeatRate { get; }
        public float AirRate { get; }
        public float VisualRate { get; }
        public bool ThermalComfortable { get; }
        public bool AirComfortable { get; }
        public bool VisualComfortable { get; }
        public float DeltaTime { get; }
    }

    public readonly struct CharacterExposureStepResult
    {
        public CharacterExposureStepResult(
            float coldExposure,
            float heatExposure,
            float airborneExposure,
            float visualStrain,
            ExposureBand physiologicalBand,
            ExposureBand visualBand,
            ExposureBand previousPhysiologicalBand)
        {
            ColdExposure = coldExposure;
            HeatExposure = heatExposure;
            AirborneExposure = airborneExposure;
            VisualStrain = visualStrain;
            PhysiologicalBand = physiologicalBand;
            VisualBand = visualBand;
            PreviousPhysiologicalBand = previousPhysiologicalBand;
        }

        public float ColdExposure { get; }
        public float HeatExposure { get; }
        public float AirborneExposure { get; }
        public float VisualStrain { get; }
        public ExposureBand PhysiologicalBand { get; }
        public ExposureBand VisualBand { get; }
        public ExposureBand PreviousPhysiologicalBand { get; }
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

        public static ExposureBand ResolveBand(float exposure, ExposureBand previousBand)
        {
            float value = EnvironmentalMath.Clamp(exposure, 0f, 100f);
            float hysteresis = previousBand == ExposureBand.Stable ? 0f : 5f;
            if (value >= 100f) return ExposureBand.Collapse;
            if (value >= 75f - (previousBand >= ExposureBand.Critical ? hysteresis : 0f))
                return ExposureBand.Critical;
            if (value >= 50f - (previousBand >= ExposureBand.Impaired ? hysteresis : 0f))
                return ExposureBand.Impaired;
            if (value >= 25f - (previousBand >= ExposureBand.Burden ? hysteresis : 0f))
                return ExposureBand.Burden;
            return ExposureBand.Stable;
        }

        public static float GetFoodSpoilageMultiplier(float temperatureC) =>
            EnvironmentalMath.Clamp(
                (float)Math.Pow(2d, (temperatureC - 20f) / 10f),
                0.25f,
                4f);

        public static bool IsOrganPreservationSafe(float temperatureC) =>
            temperatureC >= 2f && temperatureC <= 8f;
    }

    public static class CharacterEnvironmentRules
    {
        public const float ComfortableRecoveryPerSecond = 1.5f;

        public static ThermalExposureRate CalculateTemperatureRates(
            float temperatureC,
            ThermalRangeSnapshot thermal,
            ThermalProtectionSnapshot protection)
        {
            if (temperatureC < thermal.ComfortMinimum)
            {
                return new ThermalExposureRate(
                    CalculateSideRate(
                        thermal.ComfortMinimum,
                        thermal.SafeMinimum,
                        thermal.LethalMinimum,
                        temperatureC) * protection.ColdExposureMultiplier,
                    0f,
                    temperatureC <= thermal.LethalMinimum);
            }

            if (temperatureC > thermal.ComfortMaximum)
            {
                return new ThermalExposureRate(
                    0f,
                    CalculateSideRate(
                        -thermal.ComfortMaximum,
                        -thermal.SafeMaximum,
                        -thermal.LethalMaximum,
                        -temperatureC) * protection.HeatExposureMultiplier,
                    temperatureC >= thermal.LethalMaximum);
            }

            return new ThermalExposureRate(0f, 0f, false);
        }

        public static float UpdateExposure(float current, float rate, bool comfortable, float deltaTime) =>
            EnvironmentalMath.Clamp(
                comfortable
                    ? current - ComfortableRecoveryPerSecond * Math.Max(0f, deltaTime)
                    : current + Math.Max(0f, rate) * Math.Max(0f, deltaTime),
                0f,
                100f);

        public static CharacterExposureStepResult StepExposure(
            CharacterExposureStepInput input)
        {
            float cold = UpdateExposure(
                input.ColdExposure,
                input.ColdRate,
                input.ThermalComfortable,
                input.DeltaTime);
            float heat = UpdateExposure(
                input.HeatExposure,
                input.HeatRate,
                input.ThermalComfortable,
                input.DeltaTime);
            float air = UpdateExposure(
                input.AirborneExposure,
                input.AirRate,
                input.AirComfortable,
                input.DeltaTime);
            float visual = UpdateExposure(
                input.VisualStrain,
                input.VisualRate,
                input.VisualComfortable,
                input.DeltaTime);
            return new CharacterExposureStepResult(
                cold,
                heat,
                air,
                visual,
                EnvironmentalThresholdRules.ResolveBand(
                    Math.Max(cold, Math.Max(heat, air)),
                    input.PhysiologicalBand),
                EnvironmentalThresholdRules.ResolveBand(
                    visual,
                    input.VisualBand),
                input.PhysiologicalBand);
        }

        public static float ResolveMoveSpeedMultiplier(ExposureBand band) =>
            band switch
            {
                ExposureBand.Burden => 0.95f,
                ExposureBand.Impaired => 0.85f,
                ExposureBand.Critical => 0.7f,
                ExposureBand.Collapse => 0.1f,
                _ => 1f
            };

        public static float ResolveAccuracyPenaltyPoints(ExposureBand band) =>
            band switch
            {
                ExposureBand.Impaired => 10f,
                ExposureBand.Critical or ExposureBand.Collapse => 25f,
                _ => 0f
            };

        public static float CalculateAirExposureRate(float airQuality)
        {
            if (airQuality >= EnvironmentalThresholdRules.NormalAirQuality) return 0f;
            if (airQuality >= EnvironmentalThresholdRules.PollutedAirQuality)
            {
                float normalized = EnvironmentalMath.InverseLerp(
                    EnvironmentalThresholdRules.NormalAirQuality,
                    EnvironmentalThresholdRules.PollutedAirQuality,
                    airQuality);
                return 0.15f * (float)Math.Pow(normalized, 1.5d);
            }
            if (airQuality >= EnvironmentalThresholdRules.ToxicAirQuality)
            {
                float normalized = EnvironmentalMath.InverseLerp(
                    EnvironmentalThresholdRules.PollutedAirQuality,
                    EnvironmentalThresholdRules.ToxicAirQuality,
                    airQuality);
                return 0.5f + 1.5f * (float)Math.Pow(normalized, 1.5d);
            }
            return 2f;
        }

        public static float CalculateVisualStrainRate(float lightLevel)
        {
            if (lightLevel >= EnvironmentalThresholdRules.PrecisionMinimumLight) return 0f;
            float normalized = 1f - EnvironmentalMath.Clamp(
                lightLevel / EnvironmentalThresholdRules.PrecisionMinimumLight,
                0f,
                1f);
            return EnvironmentalMath.Lerp(0.15f, 1f, (float)Math.Pow(normalized, 1.5d));
        }

        private static float CalculateSideRate(
            float comfortBoundary,
            float safeBoundary,
            float lethalBoundary,
            float value)
        {
            if (value >= comfortBoundary) return 0f;
            if (value >= safeBoundary)
            {
                float normalized = EnvironmentalMath.Clamp(
                    (comfortBoundary - value) / Math.Max(0.01f, comfortBoundary - safeBoundary),
                    0f,
                    1f);
                return 0.15f * (float)Math.Pow(normalized, 1.5d);
            }
            if (value > lethalBoundary)
            {
                float normalized = EnvironmentalMath.Clamp(
                    (safeBoundary - value) / Math.Max(0.01f, safeBoundary - lethalBoundary),
                    0f,
                    1f);
                return 0.5f + 1.5f * (float)Math.Pow(normalized, 1.5d);
            }
            return 2f;
        }
    }

    internal static class EnvironmentalMath
    {
        internal static float Clamp(float value, float minimum, float maximum) =>
            Math.Min(maximum, Math.Max(minimum, value));

        internal static float Lerp(float from, float to, float amount) =>
            from + (to - from) * Clamp(amount, 0f, 1f);

        internal static float InverseLerp(float from, float to, float value)
        {
            float denominator = to - from;
            return Math.Abs(denominator) < 0.0001f
                ? 0f
                : Clamp((value - from) / denominator, 0f, 1f);
        }
    }
}
