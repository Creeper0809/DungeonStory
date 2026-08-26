using System;
using System.Collections.Generic;
using UnityEngine;

public static class SpeciesThermalProfileExtensions
{
    public static SpeciesThermalProfile Apply(
        this SpeciesThermalProfile profile,
        ThermalProtectionProfile protection)
    {
        if (protection == null)
        {
            return profile;
        }

        float safeMinimum = Mathf.Max(
            profile.LethalMinimum + 2f,
            profile.SafeMinimum + protection.safeMinimumOffset);
        float safeMaximum = Mathf.Min(
            profile.LethalMaximum - 2f,
            profile.SafeMaximum + protection.safeMaximumOffset);
        float comfortMinimum = Mathf.Clamp(
            profile.ComfortMinimum + protection.comfortMinimumOffset,
            safeMinimum,
            profile.LethalMaximum - 2f);
        float comfortMaximum = Mathf.Clamp(
            profile.ComfortMaximum + protection.comfortMaximumOffset,
            profile.LethalMinimum + 2f,
            safeMaximum);
        return new SpeciesThermalProfile(
            comfortMinimum,
            comfortMaximum,
            safeMinimum,
            safeMaximum,
            profile.LethalMinimum,
            profile.LethalMaximum);
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
    public string itemInstanceId = string.Empty;
}

[Serializable]
public sealed class DungeonCharacterEnvironmentSaveData
{
    public const int CurrentVersion = 9;

    public int version = CurrentVersion;
    // Arrays intentionally have no initializer. Unity JsonUtility preserves a
    // missing array as null, allowing the strict V3 boundary to distinguish a
    // malformed payload from a deliberately captured empty collection.
    public CharacterEnvironmentExposure[] exposures;
    public EnvironmentalWorkwearSaveData[] equippedWorkwear;
    public EquippedApparelSaveData[] equippedApparel;
    public ApparelWorkOrderSaveData[] apparelWorkOrders;
    public ApparelWorkOrderTerminalStateSaveData[] apparelWorkOrderTerminalStates;
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
        DomainFailure protectionFailure,
        DomainFailure blockingFailure)
    {
        Cold = cold;
        Heat = heat;
        Air = air;
        Visual = visual;
        WorstBand = worstBand;
        NeedsProtection = needsProtection;
        ProtectionApplied = protectionApplied;
        ProtectionFailure = protectionFailure;
        BlockingFailure = blockingFailure;
    }

    public EnvironmentExposureChannelProjection Cold { get; }
    public EnvironmentExposureChannelProjection Heat { get; }
    public EnvironmentExposureChannelProjection Air { get; }
    public EnvironmentExposureChannelProjection Visual { get; }
    public EnvironmentalExposureBand WorstBand { get; }
    public bool NeedsProtection { get; }
    public bool ProtectionApplied { get; }
    public DomainFailure ProtectionFailure { get; }
    public DomainFailure BlockingFailure { get; }
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
        DomainFailure failure,
        EnvironmentExposureProjection projection = default)
    {
        CanStart = canStart;
        NeedsProtection = needsProtection;
        ProjectedExposure = Mathf.Clamp(projectedExposure, 0f, 100f);
        WorkSpeedMultiplier = Mathf.Clamp(workSpeedMultiplier, 0.1f, 1f);
        Failure = failure;
        Projection = projection;
    }

    public bool CanStart { get; }
    public bool NeedsProtection { get; }
    public float ProjectedExposure { get; }
    public float WorkSpeedMultiplier { get; }
    public DomainFailure Failure { get; }
    public EnvironmentExposureProjection Projection { get; }
}

public interface ICharacterEnvironmentStatusQuery
{
    CharacterEnvironmentExposure GetExposure(CharacterId characterId);
    EnvironmentalExposureBand GetPhysiologicalBand(CharacterId characterId);
    EnvironmentalExposureBand GetVisualBand(CharacterId characterId);
    float GetWorkSpeedMultiplier(CharacterId characterId);
    float GetPrecisionWorkSpeedMultiplier(CharacterId characterId);
    float GetMoveSpeedMultiplier(CharacterId characterId);
    float GetAccuracyPenaltyPoints(CharacterId characterId);
}

public interface ICharacterEnvironmentExposureCommand
{
    bool AddAirborneExposure(CharacterId characterId, float amount);
}

public sealed class NoOpCharacterEnvironmentExposureCommand :
    ICharacterEnvironmentExposureCommand
{
    public static readonly NoOpCharacterEnvironmentExposureCommand Instance =
        new();

    private NoOpCharacterEnvironmentExposureCommand()
    {
    }

    public bool AddAirborneExposure(CharacterId characterId, float amount) =>
        false;
}

public interface ICharacterEnvironmentWorkContext
{
    EnvironmentalExposureBand GetPhysiologicalBand(CharacterId characterId);
    EnvironmentalExposureBand GetVisualBand(CharacterId characterId);
    void SetWorkContext(CharacterId characterId, EnvironmentalWorkKind workKind);
    void ClearWorkContext(CharacterId characterId);
}

public interface ICharacterEnvironmentPersistence
{
    DungeonCharacterEnvironmentSaveData Capture();
    CharacterEnvironmentRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterEnvironmentSaveData saveData);
    void PublishRestoreCandidate(CharacterEnvironmentRestoreCandidate candidate);
}

public sealed class CharacterEnvironmentRestoreCandidate
{
    public CharacterEnvironmentRestoreCandidate()
        : this(
            new CharacterEnvironmentAggregateState(),
            new CharacterApparelRestoreCandidate(
                new CharacterApparelAggregateState()),
            new ApparelWorkOrderRestoreCandidate(
                Array.Empty<ApparelWorkOrderSaveData>(),
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()))
    {
    }

    internal CharacterEnvironmentRestoreCandidate(
        CharacterEnvironmentAggregateState state,
        CharacterApparelRestoreCandidate apparel,
        ApparelWorkOrderRestoreCandidate apparelWorkOrders)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        ApparelWorkOrders = apparelWorkOrders
            ?? throw new ArgumentNullException(nameof(apparelWorkOrders));
    }

    internal CharacterEnvironmentAggregateState State { get; }
    internal CharacterApparelRestoreCandidate Apparel { get; }
    internal ApparelWorkOrderRestoreCandidate ApparelWorkOrders { get; }
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
        out DomainFailure failure);
}

/// <summary>
/// Diagnostics-only compatibility for the legacy work executor. The canonical
/// command boundary remains localization-neutral and typed.
/// </summary>
public static class EnvironmentWorkPolicyDiagnosticExtensions
{
    public static bool TryFindEvacuationCell(
        this IEnvironmentWorkPolicy policy,
        CharacterActor actor,
        Grid grid,
        out Vector2Int destination,
        out bool fullySafe,
        out string diagnosticCode)
    {
        bool succeeded = policy.TryFindEvacuationCell(
            actor,
            grid,
            out destination,
            out fullySafe,
            out DomainFailure failure);
        diagnosticCode = succeeded
            ? fullySafe
                ? string.Empty
                : "EnvironmentEvacuationPartialSafety"
            : failure.Code.ToString();
        return succeeded;
    }
}

public interface ICharacterEnvironmentProtectionResolver
{
    ThermalProtectionProfile Resolve(CharacterActor actor);
}
