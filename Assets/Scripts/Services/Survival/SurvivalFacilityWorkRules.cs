using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class SurvivalFacilityWorkRules
{
    public static BuildingPreservationAbility FindPreservationAbility(
        BuildableObject building)
    {
        if (building == null)
        {
            return null;
        }

        try
        {
            return building.GetRoomOperationalProfile()
                .Parts
                .OfType<BuildableObject>()
                .Where(part => part.BuildingData != null)
                .Select(part => part.BuildingData.GetAbility<BuildingPreservationAbility>())
                .FirstOrDefault(ability => ability != null);
        }
        catch (InvalidOperationException)
        {
            return building.BuildingData?.GetAbility<BuildingPreservationAbility>();
        }
    }

    public static bool CanDrawWater(BuildableObject building) =>
        building?.BuildingData?.GetAbility<BuildingWaterSourceAbility>() != null;

    public static bool TryApplyRefuel(
        IBuildingVisitorPort actor,
        BuildableObject building,
        SurvivalFoodStockRuntime stockRuntime,
        out int amount,
        out DomainFailure failure)
    {
        if (!stockRuntime.TryCommitFacilityFuel(building, out amount, out failure))
        {
            return false;
        }

        if (amount > 0)
        {
            actor?.RecordActivity(
                building,
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Work,
                    BuildingActivityOutcomes.Completed,
                    $"{GetBuildingName(building)} refueled.",
                    BuiltInWorkTypeIds.Refuel.Value,
                    string.Empty,
                    "survival-refueled",
                    0f,
                    amount,
                    false));
        }

        failure = DomainFailure.None;
        return true;
    }

    public static bool IsEnvironmentalFuelConsumer(BuildableObject building) =>
        building != null
        && !building.isDestroy
        && building.BuildingData != null
        && building.BuildingData.GetAbility<BuildingFuelConsumerAbility>() != null
        && (building.BuildingData.GetAbility<BuildingLightingAbility>() != null
            || building.BuildingData.GetAbility<BuildingTemperatureAbility>() != null
            || building.BuildingData.GetAbility<BuildingThermalEmitterAbility>() != null);

    public static string FormatWeather(SurvivalWeatherType weather)
    {
        return weather switch
        {
            SurvivalWeatherType.Rain => "비",
            SurvivalWeatherType.Fog => "안개",
            SurvivalWeatherType.HeatWave => "폭염",
            SurvivalWeatherType.ColdSnap => "한파",
            SurvivalWeatherType.Storm => "폭우",
            _ => "맑음"
        };
    }

    public static string GetBuildingName(BuildableObject building)
    {
        return string.IsNullOrWhiteSpace(building?.BuildingData?.objectName)
            ? building != null ? building.name : "시설"
            : building.BuildingData.objectName;
    }

    public static string GetActorName(CharacterActor actor, string fallback)
    {
        return actor != null && !string.IsNullOrWhiteSpace(actor.name)
            ? actor.name
            : string.IsNullOrWhiteSpace(fallback) ? "대상" : fallback;
    }
}

internal readonly struct SurvivalRiskEvaluation
{
    internal SurvivalRiskEvaluation(
        float sanitationRisk,
        float diseaseRisk,
        float exteriorNightDanger)
    {
        SanitationRisk = sanitationRisk;
        DiseaseRisk = diseaseRisk;
        ExteriorNightDanger = exteriorNightDanger;
    }

    internal float SanitationRisk { get; }
    internal float DiseaseRisk { get; }
    internal float ExteriorNightDanger { get; }
}

internal sealed class SurvivalEnvironmentRiskEvaluator
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldThreatModifierQuery threatModifiers;
    private float cachedVentilationBonus;
    private float cachedLightSafety;

    internal SurvivalEnvironmentRiskEvaluator(
        IGridSystemProvider gridSystemProvider,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldThreatModifierQuery threatModifiers)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.threatModifiers = threatModifiers
            ?? throw new ArgumentNullException(nameof(threatModifiers));
    }

    internal SurvivalEnvironmentSnapshot GetSnapshot(
        DungeonSurvivalSaveData state,
        SurvivalWeatherType weather,
        float outdoorTemperature)
    {
        return new SurvivalEnvironmentSnapshot(
            weather,
            GetEffectiveOutdoorTemperature(outdoorTemperature),
            state.exteriorNightDanger,
            GetEffectiveSanitationRisk(state),
            GetEffectiveDiseaseRisk(state));
    }

    internal SurvivalRiskEvaluation Evaluate(
        DungeonSurvivalSaveData state,
        int rotStacks,
        SurvivalWeatherType weather)
    {
        RefreshBuildingContributions();
        // Stock shortage remains a planning/dashboard forecast. Personal
        // thirst harm and contaminated-water exposure are owned by their
        // respective character runtime paths, not this aggregate forecast.
        float sanitationRisk = Mathf.Clamp(
            (rotStacks * 12f)
            - cachedVentilationBonus
            + GetThreatStrength(OffenseThreatModifierKind.Sanitation) * 45f,
            0f,
            100f);
        float diseaseRisk = Mathf.Clamp(
            (sanitationRisk * 0.55f)
            + (state.consecutiveFoodShortageDays * 7f)
            + GetThreatStrength(OffenseThreatModifierKind.Disease) * 40f,
            0f,
            100f);
        float weatherDanger = weather switch
        {
            SurvivalWeatherType.Storm => 35f,
            SurvivalWeatherType.Fog => 25f,
            SurvivalWeatherType.Rain => 18f,
            SurvivalWeatherType.ColdSnap => 16f,
            _ => 10f
        };
        float exteriorNightDanger = Mathf.Clamp(
            weatherDanger
            + (rotStacks * 4f)
            - cachedLightSafety,
            0f,
            100f);
        return new SurvivalRiskEvaluation(
            sanitationRisk,
            diseaseRisk,
            exteriorNightDanger);
    }

    internal float GetEffectiveOutdoorTemperature(float outdoorTemperature)
    {
        return outdoorTemperature
            + GetThreatStrength(OffenseThreatModifierKind.Temperature) * 14f;
    }

    internal float GetThreatMultiplier(OffenseThreatModifierKind kind)
    {
        return threatModifiers.GetMultiplier(kind);
    }

    private float GetEffectiveSanitationRisk(DungeonSurvivalSaveData state)
    {
        return Mathf.Clamp(
            state.sanitationRisk
            + GetThreatStrength(OffenseThreatModifierKind.Sanitation) * 45f,
            0f,
            100f);
    }

    private float GetEffectiveDiseaseRisk(DungeonSurvivalSaveData state)
    {
        return Mathf.Clamp(
            state.diseaseRisk
            + GetThreatStrength(OffenseThreatModifierKind.Disease) * 40f,
            0f,
            100f);
    }

    private float GetThreatStrength(OffenseThreatModifierKind kind)
    {
        return threatModifiers.GetModifier(kind).EffectiveStrength;
    }

    private void RefreshBuildingContributions()
    {
        cachedVentilationBonus = 0f;
        cachedLightSafety = 0f;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        IReadOnlyList<BuildableObject> buildings = worldRegistry.Buildings;
        if (buildings.Count > 0)
        {
            for (int index = 0; index < buildings.Count; index++)
            {
                AccumulateBuildingContribution(buildings[index], grid);
            }
            return;
        }

        foreach (IGridOccupant occupant in grid.FindAllOccupants(null))
        {
            if (occupant is BuildableObject building)
            {
                AccumulateBuildingContribution(building, grid);
            }
        }
    }

    private void AccumulateBuildingContribution(
        BuildableObject building,
        Grid grid)
    {
        if (building == null
            || building.Grid != grid
            || building.isDestroy
            || building.BuildingData == null)
        {
            return;
        }

        BuildingVentilationAbility ventilation =
            building.BuildingData.GetAbility<BuildingVentilationAbility>();
        cachedVentilationBonus += ventilation?.hygieneRiskReduction ?? 0f;
        BuildingFuelConsumerAbility fuelConsumer =
            building.BuildingData.GetAbility<BuildingFuelConsumerAbility>();
        if (fuelConsumer != null
            && building.BuildingData.GetAbility<BuildingLightingAbility>() != null
            && building.isActiveAndEnabled
            && !building.IsDetachedRestoreCandidate
            && !(building.IsDamaged
                && building.Facility?.disabledWhenDamaged == true)
            && building.HasFacilityFuelSupply)
        {
            cachedLightSafety += fuelConsumer.lightSafety;
        }
    }
}
