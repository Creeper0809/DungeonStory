using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ThermalProtectionProfile
{
    public float comfortMinimumOffset;
    public float comfortMaximumOffset;
    public float safeMinimumOffset;
    public float safeMaximumOffset;
    [Range(0.05f, 2f)] public float coldExposureMultiplier = 1f;
    [Range(0.05f, 2f)] public float heatExposureMultiplier = 1f;

    public static ThermalProtectionProfile None =>
        new ThermalProtectionProfile();

    public void Add(ThermalProtectionProfile other)
    {
        if (other == null)
        {
            return;
        }

        comfortMinimumOffset += other.comfortMinimumOffset;
        comfortMaximumOffset += other.comfortMaximumOffset;
        safeMinimumOffset += other.safeMinimumOffset;
        safeMaximumOffset += other.safeMaximumOffset;
        coldExposureMultiplier *= Mathf.Clamp(
            other.coldExposureMultiplier,
            0.05f,
            2f);
        heatExposureMultiplier *= Mathf.Clamp(
            other.heatExposureMultiplier,
            0.05f,
            2f);
    }

    public ThermalProtectionProfile Clone()
    {
        return new ThermalProtectionProfile
        {
            comfortMinimumOffset = comfortMinimumOffset,
            comfortMaximumOffset = comfortMaximumOffset,
            safeMinimumOffset = safeMinimumOffset,
            safeMaximumOffset = safeMaximumOffset,
            coldExposureMultiplier = coldExposureMultiplier,
            heatExposureMultiplier = heatExposureMultiplier
        };
    }
}

public readonly struct SpeciesThermalProfile
{
    public SpeciesThermalProfile(
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

    public static SpeciesThermalProfile ForSpecies(string speciesTag)
    {
        if (CharacterSpeciesResourceLookup.TryGet(
                speciesTag,
                out CharacterSpeciesSO species)
            && species.environment != null)
        {
            return species.environment.ToThermalProfile();
        }

        if (string.Equals(speciesTag, "Slime", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeciesThermalProfile(16f, 24f, 5f, 34f, 0f, 40f);
        }

        if (string.Equals(speciesTag, "Orc", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeciesThermalProfile(12f, 30f, -5f, 42f, -15f, 50f);
        }

        if (string.Equals(speciesTag, "Vampire", StringComparison.OrdinalIgnoreCase))
        {
            return new SpeciesThermalProfile(8f, 22f, 0f, 34f, -10f, 42f);
        }

        return new SpeciesThermalProfile(15f, 27f, 0f, 40f, -10f, 48f);
    }

    public SpeciesThermalProfile Apply(ThermalProtectionProfile protection)
    {
        if (protection == null)
        {
            return this;
        }

        float safeMinimum = Mathf.Max(
            LethalMinimum + 2f,
            SafeMinimum + protection.safeMinimumOffset);
        float safeMaximum = Mathf.Min(
            LethalMaximum - 2f,
            SafeMaximum + protection.safeMaximumOffset);
        float comfortMinimum = Mathf.Clamp(
            ComfortMinimum + protection.comfortMinimumOffset,
            safeMinimum,
            LethalMaximum - 2f);
        float comfortMaximum = Mathf.Clamp(
            ComfortMaximum + protection.comfortMaximumOffset,
            LethalMinimum + 2f,
            safeMaximum);
        return new SpeciesThermalProfile(
            comfortMinimum,
            comfortMaximum,
            safeMinimum,
            safeMaximum,
            LethalMinimum,
            LethalMaximum);
    }
}

[Serializable]
public sealed class CharacterEnvironmentExposure
{
    public string characterId = string.Empty;
    [Range(0f, 100f)] public float coldExposure;
    [Range(0f, 100f)] public float heatExposure;
    [Range(0f, 100f)] public float airborneExposure;
    [Range(0f, 100f)] public float visualStrain;
    public EnvironmentalExposureBand physiologicalBand;
    public EnvironmentalExposureBand visualBand;
    public float criticalDamageTimer;
    public bool coldWorkCooldownActive;
}

[Serializable]
public sealed class EnvironmentalWorkwearSaveData
{
    public string characterId = string.Empty;
    public string workwearId = string.Empty;
}

[Serializable]
public sealed class EnvironmentalWorkwearStockSaveData
{
    public string workwearId = string.Empty;
    public int amount;
}

[Serializable]
public sealed class DungeonCharacterEnvironmentSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<CharacterEnvironmentExposure> exposures =
        new List<CharacterEnvironmentExposure>();
    public List<EnvironmentalWorkwearSaveData> equippedWorkwear =
        new List<EnvironmentalWorkwearSaveData>();
    public List<EnvironmentalWorkwearStockSaveData> workwearStock =
        new List<EnvironmentalWorkwearStockSaveData>();
}

public readonly struct EnvironmentExposureChannelProjection
{
    public EnvironmentExposureChannelProjection(
        float current,
        float routeEnd,
        float workEnd,
        float routeHighestRate,
        EnvironmentalExposureBand endBand,
        Vector2Int highestRiskCell,
        bool lethal)
    {
        Current = Mathf.Clamp(current, 0f, 100f);
        RouteEnd = Mathf.Clamp(routeEnd, 0f, 100f);
        WorkEnd = Mathf.Clamp(workEnd, 0f, 100f);
        RouteHighestRate = Mathf.Max(0f, routeHighestRate);
        EndBand = endBand;
        HighestRiskCell = highestRiskCell;
        Lethal = lethal;
    }

    public float Current { get; }
    public float RouteEnd { get; }
    public float WorkEnd { get; }
    public float RouteHighestRate { get; }
    public EnvironmentalExposureBand EndBand { get; }
    public Vector2Int HighestRiskCell { get; }
    public bool Lethal { get; }
}

public readonly struct EnvironmentExposureProjection
{
    public EnvironmentExposureProjection(
        EnvironmentExposureChannelProjection cold,
        EnvironmentExposureChannelProjection heat,
        EnvironmentExposureChannelProjection air,
        EnvironmentExposureChannelProjection visual,
        EnvironmentalExposureBand worstBand,
        bool needsProtection,
        bool protectionApplied,
        string protectionFailureReason,
        string blockingReason)
    {
        Cold = cold;
        Heat = heat;
        Air = air;
        Visual = visual;
        WorstBand = worstBand;
        NeedsProtection = needsProtection;
        ProtectionApplied = protectionApplied;
        ProtectionFailureReason = protectionFailureReason ?? string.Empty;
        BlockingReason = blockingReason ?? string.Empty;
    }

    public EnvironmentExposureChannelProjection Cold { get; }
    public EnvironmentExposureChannelProjection Heat { get; }
    public EnvironmentExposureChannelProjection Air { get; }
    public EnvironmentExposureChannelProjection Visual { get; }
    public EnvironmentalExposureBand WorstBand { get; }
    public bool NeedsProtection { get; }
    public bool ProtectionApplied { get; }
    public string ProtectionFailureReason { get; }
    public string BlockingReason { get; }
    public bool HasLethalChannel =>
        Cold.Lethal || Heat.Lethal || Air.Lethal;
}

public readonly struct WorkEnvironmentAssessment
{
    public WorkEnvironmentAssessment(
        bool canStart,
        bool needsProtection,
        float projectedExposure,
        float workSpeedMultiplier,
        string reason,
        EnvironmentExposureProjection projection = default)
    {
        CanStart = canStart;
        NeedsProtection = needsProtection;
        ProjectedExposure = Mathf.Clamp(projectedExposure, 0f, 100f);
        WorkSpeedMultiplier = Mathf.Clamp(workSpeedMultiplier, 0.1f, 1f);
        Reason = reason ?? string.Empty;
        Projection = projection;
    }

    public bool CanStart { get; }
    public bool NeedsProtection { get; }
    public float ProjectedExposure { get; }
    public float WorkSpeedMultiplier { get; }
    public string Reason { get; }
    public EnvironmentExposureProjection Projection { get; }
}

public interface ICharacterEnvironmentStatusQuery
{
    CharacterEnvironmentExposure GetExposure(string characterId);
    EnvironmentalExposureBand GetPhysiologicalBand(string characterId);
    EnvironmentalExposureBand GetVisualBand(string characterId);
    float GetWorkSpeedMultiplier(string characterId);
    float GetPrecisionWorkSpeedMultiplier(string characterId);
    float GetMoveSpeedMultiplier(string characterId);
    float GetAccuracyPenaltyPoints(string characterId);
}

public interface ICharacterEnvironmentRuntime :
    ICharacterEnvironmentStatusQuery
{
    void SetWorkContext(string characterId, EnvironmentalWorkKind workKind);
    void ClearWorkContext(string characterId);
    DungeonCharacterEnvironmentSaveData Capture();
    void Restore(
        DungeonCharacterEnvironmentSaveData saveData,
        DungeonGameRestoreReport report = null);
    void Reset();
}

public interface IEnvironmentWorkPolicy
{
    WorkEnvironmentAssessment Assess(
        CharacterActor actor,
        Vector2Int destination,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool forced);
    WorkEnvironmentAssessment AssessStart(
        CharacterActor actor,
        Vector2Int destination,
        IReadOnlyList<GridMoveStep> route,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool forced);
    WorkEnvironmentAssessment RecheckActive(
        CharacterActor actor,
        Vector2Int currentPosition,
        float remainingSeconds,
        EnvironmentalWorkKind workKind,
        bool forced);
    bool TryFindEvacuationCell(
        CharacterActor actor,
        Grid grid,
        out Vector2Int destination,
        out bool fullySafe,
        out string failureReason);
}

public interface ICharacterEnvironmentProtectionResolver
{
    ThermalProtectionProfile Resolve(CharacterActor actor);
}
