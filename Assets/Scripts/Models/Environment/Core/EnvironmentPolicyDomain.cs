using System;

namespace DungeonStory.Environment
{
    public readonly struct EnvironmentWorkRiskSnapshot
    {
        public EnvironmentWorkRiskSnapshot(
            ExposureBand worstBand,
            bool lethalChannel,
            bool needsProtection,
            bool protectionApplied,
            bool coldCooldownActive,
            float projectedExposure,
            float coldRouteRate)
        {
            WorstBand = worstBand;
            LethalChannel = lethalChannel;
            NeedsProtection = needsProtection;
            ProtectionApplied = protectionApplied;
            ColdCooldownActive = coldCooldownActive;
            ProjectedExposure = EnvironmentalMath.Clamp(projectedExposure, 0f, 100f);
            ColdRouteRate = Math.Max(0f, coldRouteRate);
        }

        public ExposureBand WorstBand { get; }
        public bool LethalChannel { get; }
        public bool NeedsProtection { get; }
        public bool ProtectionApplied { get; }
        public bool ColdCooldownActive { get; }
        public float ProjectedExposure { get; }
        public float ColdRouteRate { get; }
    }

    public readonly struct EnvironmentWorkDecision
    {
        public EnvironmentWorkDecision(bool canStart, float workSpeedMultiplier)
        {
            CanStart = canStart;
            WorkSpeedMultiplier = EnvironmentalMath.Clamp(workSpeedMultiplier, 0.1f, 1f);
        }

        public bool CanStart { get; }
        public float WorkSpeedMultiplier { get; }
    }

    public enum EnvironmentWorkFailureKind
    {
        None = 0,
        ColdCooldown = 1,
        ProtectionUnavailable = 2,
        CriticalExposure = 3
    }

    public readonly struct EnvironmentWorkFailureRiskSnapshot
    {
        public EnvironmentWorkFailureRiskSnapshot(
            ExposureBand worstBand,
            bool lethalChannel,
            bool coldCooldownBlocks,
            bool protectionFailure,
            bool forced,
            bool canStart)
        {
            WorstBand = worstBand;
            LethalChannel = lethalChannel;
            ColdCooldownBlocks = coldCooldownBlocks;
            ProtectionFailure = protectionFailure;
            Forced = forced;
            CanStart = canStart;
        }

        public ExposureBand WorstBand { get; }
        public bool LethalChannel { get; }
        public bool ColdCooldownBlocks { get; }
        public bool ProtectionFailure { get; }
        public bool Forced { get; }
        public bool CanStart { get; }
    }

    public static class EnvironmentWorkRules
    {
        public static bool IsSafetyException(EnvironmentalWorkKind workKind) =>
            workKind is EnvironmentalWorkKind.EmergencySurgery
                or EnvironmentalWorkKind.Defense
                or EnvironmentalWorkKind.Safety;

        public static bool ResolveColdCooldown(
            float coldExposure,
            bool currentlyActive)
        {
            if (coldExposure >= 15f)
            {
                return true;
            }
            if (coldExposure < 10f)
            {
                return false;
            }
            return currentlyActive;
        }

        public static EnvironmentWorkFailureKind ResolveFailure(
            EnvironmentWorkFailureRiskSnapshot risk)
        {
            if (risk.ColdCooldownBlocks)
            {
                return EnvironmentWorkFailureKind.ColdCooldown;
            }

            bool projectedHazard = risk.WorstBand >= ExposureBand.Critical
                || risk.LethalChannel;
            if (risk.CanStart && (!risk.Forced || !projectedHazard))
            {
                return EnvironmentWorkFailureKind.None;
            }
            if (risk.ProtectionFailure)
            {
                return EnvironmentWorkFailureKind.ProtectionUnavailable;
            }
            return EnvironmentWorkFailureKind.CriticalExposure;
        }

        public static EnvironmentWorkDecision Decide(
            EnvironmentWorkRiskSnapshot risk,
            EnvironmentalWorkKind workKind,
            bool forced)
        {
            bool safetyException = workKind is EnvironmentalWorkKind.Safety
                or EnvironmentalWorkKind.EmergencySurgery;
            bool cooldownBlocks = !forced
                && !safetyException
                && risk.ColdCooldownActive
                && risk.ColdRouteRate > 0f;
            bool canStart = forced
                || safetyException
                || (!cooldownBlocks
                    && risk.WorstBand < ExposureBand.Critical
                    && !risk.LethalChannel
                    && (!risk.NeedsProtection
                        || risk.ProtectionApplied
                        || risk.ProjectedExposure < 25f));
            return new EnvironmentWorkDecision(
                canStart,
                ResolveWorkSpeed(risk.WorstBand, workKind));
        }

        public static float ResolveWorkSpeed(ExposureBand band, EnvironmentalWorkKind workKind)
        {
            float multiplier = band switch
            {
                ExposureBand.Stable => 1f,
                ExposureBand.Burden => 0.9f,
                ExposureBand.Impaired => 0.7f,
                ExposureBand.Critical => 0.45f,
                _ => 0.1f
            };
            bool precision = workKind is EnvironmentalWorkKind.Precision
                or EnvironmentalWorkKind.Surgery
                or EnvironmentalWorkKind.EmergencySurgery;
            return precision ? Math.Max(0.1f, multiplier - 0.1f) : multiplier;
        }

        public static float ResolveLegacyWorkSpeed(
            ExposureBand band,
            EnvironmentalWorkKind workKind)
        {
            bool precision = workKind is EnvironmentalWorkKind.Precision
                or EnvironmentalWorkKind.Surgery
                or EnvironmentalWorkKind.EmergencySurgery;
            if (!precision)
            {
                return band switch
                {
                    ExposureBand.Burden => 0.9f,
                    ExposureBand.Impaired => 0.75f,
                    ExposureBand.Critical => 0.5f,
                    ExposureBand.Collapse => 0.1f,
                    _ => 1f
                };
            }

            return band switch
            {
                ExposureBand.Burden => 0.85f,
                ExposureBand.Impaired => 0.6f,
                ExposureBand.Critical or ExposureBand.Collapse => 0.35f,
                _ => 1f
            };
        }
    }

    public readonly struct ExternalInfluenceSnapshot
    {
        public ExternalInfluenceSnapshot(
            float renown,
            float dread,
            float hostileRumor,
            float ecologyPressure,
            float scoutingLabor,
            int currentOperatingDay,
            int lastRumorMitigationDay,
            bool dreadDefenseArmed,
            bool dreadDefenseActive,
            bool dreadDefenseBoss)
        {
            Renown = renown;
            Dread = dread;
            HostileRumor = hostileRumor;
            EcologyPressure = ecologyPressure;
            ScoutingLabor = scoutingLabor;
            CurrentOperatingDay = currentOperatingDay;
            LastRumorMitigationDay = lastRumorMitigationDay;
            DreadDefenseArmed = dreadDefenseArmed;
            DreadDefenseActive = dreadDefenseActive;
            DreadDefenseBoss = dreadDefenseBoss;
        }

        public float Renown { get; }
        public float Dread { get; }
        public float HostileRumor { get; }
        public float EcologyPressure { get; }
        public float ScoutingLabor { get; }
        public int CurrentOperatingDay { get; }
        public int LastRumorMitigationDay { get; }
        public bool DreadDefenseArmed { get; }
        public bool DreadDefenseActive { get; }
        public bool DreadDefenseBoss { get; }
    }

    public static class ExternalInfluenceRules
    {
        public static bool IsValid(ExternalInfluenceSnapshot state)
        {
            return InRange(state.Renown, 0f, 999f)
                && InRange(state.Dread, 0f, 999f)
                && InRange(state.HostileRumor, 0f, 100f)
                && InRange(state.EcologyPressure, 0f, 100f)
                && InRange(state.ScoutingLabor, 0f, 999f)
                && state.CurrentOperatingDay >= -1
                && state.LastRumorMitigationDay >= -1
                && state.LastRumorMitigationDay <= state.CurrentOperatingDay
                && !(state.DreadDefenseArmed && state.DreadDefenseActive)
                && (state.DreadDefenseActive
                    || !state.DreadDefenseBoss);
        }

        private static bool InRange(float value, float minimum, float maximum) =>
            !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum
            && value <= maximum;
    }
}
