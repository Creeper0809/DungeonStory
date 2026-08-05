using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Converts Unity scene actors, grids, and cells into the named environment
/// policy contracts. Gameplay decisions remain in EnvironmentWorkRules.
/// </summary>
public sealed class EnvironmentWorkPolicyUnityAdapter : IEnvironmentWorkPolicy
{
    private readonly IEnvironmentalFieldQuery field;
    private readonly ICharacterEnvironmentStatusQuery status;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly IEnvironmentalWorkwearCommand workwear;
    private readonly ICharacterSpeciesEnvironmentCatalog speciesEnvironment;

    public EnvironmentWorkPolicyUnityAdapter(
        IEnvironmentalFieldQuery field,
        ICharacterEnvironmentStatusQuery status,
        ICharacterEnvironmentProtectionResolver protection,
        IEnvironmentalWorkwearCommand workwear,
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.status = status ?? throw new ArgumentNullException(nameof(status));
        this.protection = protection
            ?? throw new ArgumentNullException(nameof(protection));
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
        this.speciesEnvironment = speciesEnvironment
            ?? throw new ArgumentNullException(nameof(speciesEnvironment));
    }

    public WorkEnvironmentAssessment Assess(
        CharacterActor actor,
        Vector2Int destination,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool forced)
    {
        return AssessStart(
            actor,
            destination,
            Array.Empty<GridMoveStep>(),
            expectedSeconds,
            workKind,
            forced);
    }

    public WorkEnvironmentAssessment AssessStart(
        CharacterActor actor,
        Vector2Int destination,
        IReadOnlyList<GridMoveStep> route,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool forced)
    {
        if (actor == null || !field.TryGetCell(destination, out _))
        {
            return new WorkEnvironmentAssessment(
                false,
                false,
                0f,
                1f,
                new DomainFailure(
                    FailureCode.EnvironmentWorkTargetUnavailable));
        }

        CharacterEnvironmentExposure current =
            status.GetExposure(new CharacterId(actor.Identity?.PersistentId));
        bool exception = DungeonStory.Environment.EnvironmentWorkRules
            .IsSafetyException(
                (DungeonStory.Environment.EnvironmentalWorkKind)workKind);
        UpdateColdCooldown(current);
        EnvironmentExposureProjection projection = Project(
            actor,
            destination,
            route,
            Mathf.Max(0f, expectedSeconds),
            workKind,
            protectionApplied: false,
            protectionFailure: DomainFailure.None);

        DomainFailure protectionFailure = DomainFailure.None;
        bool protectionApplied = false;
        if (projection.NeedsProtection
            && !forced
            && !exception
            && projection.Cold.WorkEnd >= 25f)
        {
            protectionApplied = workwear.TryAutoEquipForCold(
                actor,
                destination,
                out protectionFailure);
            if (protectionApplied)
            {
                projection = Project(
                    actor,
                    destination,
                    route,
                    Mathf.Max(0f, expectedSeconds),
                    workKind,
                    protectionApplied: true,
                    protectionFailure: DomainFailure.None);
            }
            else if (protectionFailure.IsFailure)
            {
                projection = Project(
                    actor,
                    destination,
                    route,
                    Mathf.Max(0f, expectedSeconds),
                    workKind,
                    protectionApplied: false,
                    protectionFailure: protectionFailure);
            }
        }

        bool coldCooldownBlocks = !forced
            && !exception
            && current?.coldWorkCooldownActive == true
            && projection.Cold.RouteHighestRate > 0f;
        DungeonStory.Environment.EnvironmentWorkDecision decision =
            DungeonStory.Environment.EnvironmentWorkRules.Decide(
                new DungeonStory.Environment.EnvironmentWorkRiskSnapshot(
                    (DungeonStory.Environment.ExposureBand)projection.WorstBand,
                    projection.HasLethalChannel,
                    projection.NeedsProtection,
                    protectionApplied,
                    current?.coldWorkCooldownActive == true,
                    projection.Cold.WorkEnd,
                    projection.Cold.RouteHighestRate),
                (DungeonStory.Environment.EnvironmentalWorkKind)workKind,
                forced);
        bool canStart = decision.CanStart;
        DomainFailure failure = BuildProjectionFailure(
            projection,
            coldCooldownBlocks,
            protectionFailure,
            forced,
            canStart);
        return new WorkEnvironmentAssessment(
            canStart,
            projection.NeedsProtection,
            Mathf.Max(
                projection.Cold.WorkEnd,
                Mathf.Max(
                    projection.Heat.WorkEnd,
                    Mathf.Max(
                        projection.Air.WorkEnd,
                        projection.Visual.WorkEnd))),
            decision.WorkSpeedMultiplier,
            failure,
            projection);
    }

    public WorkEnvironmentAssessment RecheckActive(
        CharacterActor actor,
        Vector2Int currentPosition,
        float remainingSeconds,
        EnvironmentalWorkKind workKind,
        bool forced)
    {
        return AssessStart(
            actor,
            currentPosition,
            Array.Empty<GridMoveStep>(),
            Mathf.Max(0f, remainingSeconds),
            workKind,
            forced);
    }

    public bool TryFindEvacuationCell(
        CharacterActor actor,
        Grid grid,
        out Vector2Int destination,
        out bool fullySafe,
        out DomainFailure failure)
    {
        destination = actor != null ? actor.GetNowXY() : default;
        fullySafe = false;
        if (actor == null || grid == null)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentEvacuationContextInvalid);
            return false;
        }

        CharacterEnvironmentExposure current =
            status.GetExposure(new CharacterId(actor.Identity?.PersistentId));
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        List<(Vector2Int position, float score, bool safe, int cost)> candidates =
            new List<(Vector2Int, float, bool, int)>();
        foreach (Vector2Int position in search.GetReachablePositions())
        {
            if (!field.TryGetCell(position, out EnvironmentalCellSnapshot cell))
            {
                continue;
            }

            EvaluateCellRates(
                actor,
                cell,
                EnvironmentalWorkKind.General,
                out float coldRate,
                out float heatRate,
                out float airRate,
                out _,
                out bool lethal);
            float score = Mathf.Max(
                coldRate,
                Mathf.Max(heatRate, airRate));
            bool recovering = !lethal
                && coldRate <= 0f
                && heatRate <= 0f
                && airRate <= 0f;
            int cost = search.GetMoveCostTo(position);
            candidates.Add((position, score, recovering, cost));
        }

        if (candidates.Count == 0)
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentEvacuationCellUnavailable,
                actor.Identity?.PersistentId ?? string.Empty);
            return false;
        }

        (Vector2Int position, float score, bool safe, int cost) selected =
            candidates
                .OrderByDescending(candidate => candidate.safe)
                .ThenBy(candidate => candidate.score)
                .ThenBy(candidate => candidate.cost)
                .ThenBy(candidate => candidate.position.y)
                .ThenBy(candidate => candidate.position.x)
                .First();
        destination = selected.position;
        fullySafe = selected.safe;
        failure = DomainFailure.None;
        return true;
    }

    private WorkEnvironmentAssessment AssessLegacy(
        CharacterActor actor,
        Vector2Int destination,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool forced)
    {
        if (actor == null
            || !field.TryGetCell(
                destination,
                out EnvironmentalCellSnapshot environment))
        {
            return new WorkEnvironmentAssessment(
                false,
                false,
                0f,
                1f,
                new DomainFailure(
                    FailureCode.EnvironmentWorkTargetUnavailable));
        }

        ThermalProtectionProfile resolvedProtection =
            protection.Resolve(actor);
        SpeciesThermalProfile thermal =
            speciesEnvironment
                .GetRequiredThermalProfile(
                    new CharacterSpeciesId(actor.SpeciesTag))
                .Apply(resolvedProtection);
        CalculateTemperatureRates(
            environment.TemperatureC,
            thermal,
            resolvedProtection,
            out float coldRate,
            out float heatRate,
            out _);
        CharacterEnvironmentExposure current =
            status.GetExposure(new CharacterId(actor.Identity?.PersistentId));
        float currentExposure = Mathf.Max(
            current?.coldExposure ?? 0f,
            Mathf.Max(
                current?.heatExposure ?? 0f,
                current?.airborneExposure ?? 0f));
        float thermalRate = Mathf.Max(coldRate, heatRate);
        float airRate =
            DungeonStory.Environment.CharacterEnvironmentRules
                .CalculateAirExposureRate(
                environment.AirQuality);
        float projected = Mathf.Clamp(
            currentExposure
            + Mathf.Max(thermalRate, airRate)
                * Mathf.Max(0f, expectedSeconds),
            0f,
            100f);
        EnvironmentalExposureBand projectedBand =
            EnvironmentalThresholdRules.ResolveBand(
                projected,
                current?.physiologicalBand
                    ?? EnvironmentalExposureBand.Stable);
        bool precision = workKind is EnvironmentalWorkKind.Precision
            or EnvironmentalWorkKind.Surgery
            or EnvironmentalWorkKind.EmergencySurgery;
        EnvironmentalExposureBand projectedVisualBand =
            EnvironmentalExposureBand.Stable;
        float projectedVisual = current?.visualStrain ?? 0f;
        if (precision)
        {
            projectedVisual = Mathf.Clamp(
                (current?.visualStrain ?? 0f)
                + DungeonStory.Environment.CharacterEnvironmentRules
                    .CalculateVisualStrainRate(
                    environment.LightLevel)
                    * Mathf.Max(0f, expectedSeconds),
                0f,
                100f);
            projectedVisualBand =
                EnvironmentalThresholdRules.ResolveBand(
                    projectedVisual,
                    current?.visualBand
                        ?? EnvironmentalExposureBand.Stable);
            projectedBand = (EnvironmentalExposureBand)Mathf.Max(
                (int)projectedBand,
                (int)projectedVisualBand);
            projected = Mathf.Max(projected, projectedVisual);
        }
        bool exception = workKind is EnvironmentalWorkKind.EmergencySurgery
            or EnvironmentalWorkKind.Defense
            or EnvironmentalWorkKind.Safety;
        if (!forced
            && !exception
            && current != null
            && current.coldExposure >= 15f
            && coldRate > 0f)
        {
            return new WorkEnvironmentAssessment(
                false,
                false,
                current.coldExposure,
                ResolveLegacyWorkSpeed(
                    current.physiologicalBand,
                    workKind),
                new DomainFailure(
                    FailureCode.EnvironmentColdWorkCooldownActive,
                    current.coldExposure.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture)));
        }

        bool canStart = forced
            || exception
            || projectedBand < EnvironmentalExposureBand.Critical;
        float projectedThermal = Mathf.Clamp(
            Mathf.Max(
                current?.coldExposure ?? 0f,
                current?.heatExposure ?? 0f)
            + thermalRate * Mathf.Max(0f, expectedSeconds),
            0f,
            100f);
        bool needsProtection = projectedThermal >= 25f
            && thermalRate > 0f;
        if (needsProtection && !forced && !exception)
        {
            if (workwear.TryAutoEquipForCold(
                actor,
                destination,
                out DomainFailure equipmentFailure))
            {
                resolvedProtection = protection.Resolve(actor);
                thermal = speciesEnvironment
                    .GetRequiredThermalProfile(
                        new CharacterSpeciesId(actor.SpeciesTag))
                    .Apply(resolvedProtection);
                CalculateTemperatureRates(
                    environment.TemperatureC,
                    thermal,
                    resolvedProtection,
                    out coldRate,
                    out heatRate,
                    out _);
                projected = Mathf.Clamp(
                    currentExposure
                    + Mathf.Max(
                        Mathf.Max(coldRate, heatRate),
                        airRate)
                        * Mathf.Max(0f, expectedSeconds),
                    0f,
                    100f);
                projectedBand = EnvironmentalThresholdRules.ResolveBand(
                    projected,
                    current?.physiologicalBand
                        ?? EnvironmentalExposureBand.Stable);
                projectedBand = (EnvironmentalExposureBand)Mathf.Max(
                    (int)projectedBand,
                    (int)projectedVisualBand);
                projectedThermal = Mathf.Clamp(
                    Mathf.Max(
                        current?.coldExposure ?? 0f,
                        current?.heatExposure ?? 0f)
                    + Mathf.Max(coldRate, heatRate)
                        * Mathf.Max(0f, expectedSeconds),
                    0f,
                    100f);
                needsProtection = projectedThermal >= 25f
                    && Mathf.Max(coldRate, heatRate) > 0f;
                canStart = projectedBand
                    < EnvironmentalExposureBand.Critical;
                if (!needsProtection)
                {
                    return new WorkEnvironmentAssessment(
                        canStart,
                        false,
                        projected,
                        ResolveLegacyWorkSpeed(
                            projectedBand,
                            workKind),
                        canStart
                            ? DomainFailure.None
                            : new DomainFailure(
                                FailureCode.EnvironmentProtectionInsufficient));
                }

                equipmentFailure = new DomainFailure(
                    FailureCode.EnvironmentWorkwearStockMissing,
                    actor.SpeciesTag ?? string.Empty);
            }

            return new WorkEnvironmentAssessment(
                false,
                true,
                projected,
                ResolveLegacyWorkSpeed(projectedBand, workKind),
                equipmentFailure);
        }

        float projectedCold = Mathf.Clamp(
            (current?.coldExposure ?? 0f)
                + coldRate * Mathf.Max(0f, expectedSeconds),
            0f,
            100f);
        float projectedHeat = Mathf.Clamp(
            (current?.heatExposure ?? 0f)
                + heatRate * Mathf.Max(0f, expectedSeconds),
            0f,
            100f);
        float projectedAir = Mathf.Clamp(
            (current?.airborneExposure ?? 0f)
                + airRate * Mathf.Max(0f, expectedSeconds),
            0f,
            100f);
        return new WorkEnvironmentAssessment(
            canStart,
            needsProtection,
            projected,
            ResolveLegacyWorkSpeed(projectedBand, workKind),
            canStart
                ? DomainFailure.None
                : new DomainFailure(
                    FailureCode.EnvironmentExposureCritical,
                    destination.x.ToString(CultureInfo.InvariantCulture),
                    destination.y.ToString(CultureInfo.InvariantCulture),
                    projectedBand.ToString(),
                    projectedCold.ToString("0.#", CultureInfo.InvariantCulture),
                    projectedHeat.ToString("0.#", CultureInfo.InvariantCulture),
                    projectedAir.ToString("0.#", CultureInfo.InvariantCulture),
                    projectedVisual.ToString(
                        "0.#",
                        CultureInfo.InvariantCulture)));
    }

    private EnvironmentExposureProjection Project(
        CharacterActor actor,
        Vector2Int destination,
        IReadOnlyList<GridMoveStep> route,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool protectionApplied,
        DomainFailure protectionFailure)
    {
        CharacterEnvironmentExposure current =
            status.GetExposure(new CharacterId(actor.Identity?.PersistentId));
        float cold = current?.coldExposure ?? 0f;
        float heat = current?.heatExposure ?? 0f;
        float air = current?.airborneExposure ?? 0f;
        float visual = current?.visualStrain ?? 0f;
        float routeCold = cold;
        float routeHeat = heat;
        float routeAir = air;
        float routeVisual = visual;
        float highColdRate = 0f;
        float highHeatRate = 0f;
        float highAirRate = 0f;
        float highVisualRate = 0f;
        Vector2Int highColdCell = destination;
        Vector2Int highHeatCell = destination;
        Vector2Int highAirCell = destination;
        Vector2Int highVisualCell = destination;
        bool coldLethal = false;
        bool heatLethal = false;
        bool airLethal = false;

        if (route != null)
        {
            for (int index = 0; index < route.Count; index++)
            {
                Vector2Int position = route[index].To;
                if (!field.TryGetCell(
                        position,
                        out EnvironmentalCellSnapshot routeCell))
                {
                    continue;
                }

                EvaluateCellRates(
                    actor,
                    routeCell,
                    workKind,
                    out float coldRate,
                    out float heatRate,
                    out float airRate,
                    out float visualRate,
                    out bool lethal);
                routeCold = Mathf.Clamp(routeCold + coldRate, 0f, 100f);
                routeHeat = Mathf.Clamp(routeHeat + heatRate, 0f, 100f);
                routeAir = Mathf.Clamp(routeAir + airRate, 0f, 100f);
                routeVisual = Mathf.Clamp(
                    routeVisual + visualRate,
                    0f,
                    100f);
                TrackHighest(
                    coldRate,
                    position,
                    ref highColdRate,
                    ref highColdCell);
                TrackHighest(
                    heatRate,
                    position,
                    ref highHeatRate,
                    ref highHeatCell);
                TrackHighest(
                    airRate,
                    position,
                    ref highAirRate,
                    ref highAirCell);
                TrackHighest(
                    visualRate,
                    position,
                    ref highVisualRate,
                    ref highVisualCell);
                if (lethal)
                {
                    coldLethal |= coldRate > 0f;
                    heatLethal |= heatRate > 0f;
                    airLethal |= routeCell.AirQuality
                        < EnvironmentalThresholdRules.ToxicAirQuality;
                }
            }
        }

        field.TryGetCell(
            destination,
            out EnvironmentalCellSnapshot destinationCell);
        EvaluateCellRates(
            actor,
            destinationCell,
            workKind,
            out float destinationColdRate,
            out float destinationHeatRate,
            out float destinationAirRate,
            out float destinationVisualRate,
            out bool destinationLethal);
        TrackHighest(
            destinationColdRate,
            destination,
            ref highColdRate,
            ref highColdCell);
        TrackHighest(
            destinationHeatRate,
            destination,
            ref highHeatRate,
            ref highHeatCell);
        TrackHighest(
            destinationAirRate,
            destination,
            ref highAirRate,
            ref highAirCell);
        TrackHighest(
            destinationVisualRate,
            destination,
            ref highVisualRate,
            ref highVisualCell);
        coldLethal |= destinationLethal && destinationColdRate > 0f;
        heatLethal |= destinationLethal && destinationHeatRate > 0f;
        airLethal |= destinationCell.AirQuality
            < EnvironmentalThresholdRules.ToxicAirQuality;

        float duration = Mathf.Max(0f, expectedSeconds);
        float workCold = Mathf.Clamp(
            routeCold + destinationColdRate * duration,
            0f,
            100f);
        float workHeat = Mathf.Clamp(
            routeHeat + destinationHeatRate * duration,
            0f,
            100f);
        float workAir = Mathf.Clamp(
            routeAir + destinationAirRate * duration,
            0f,
            100f);
        float workVisual = Mathf.Clamp(
            routeVisual + destinationVisualRate * duration,
            0f,
            100f);

        EnvironmentExposureChannelProjection coldProjection =
            CreateChannel(
                cold,
                routeCold,
                workCold,
                highColdRate,
                current?.physiologicalBand
                    ?? EnvironmentalExposureBand.Stable,
                highColdCell,
                coldLethal);
        EnvironmentExposureChannelProjection heatProjection =
            CreateChannel(
                heat,
                routeHeat,
                workHeat,
                highHeatRate,
                current?.physiologicalBand
                    ?? EnvironmentalExposureBand.Stable,
                highHeatCell,
                heatLethal);
        EnvironmentExposureChannelProjection airProjection =
            CreateChannel(
                air,
                routeAir,
                workAir,
                highAirRate,
                current?.physiologicalBand
                    ?? EnvironmentalExposureBand.Stable,
                highAirCell,
                airLethal);
        EnvironmentExposureChannelProjection visualProjection =
            CreateChannel(
                visual,
                routeVisual,
                workVisual,
                highVisualRate,
                current?.visualBand
                    ?? EnvironmentalExposureBand.Stable,
                highVisualCell,
                false);
        EnvironmentalExposureBand worst =
            (EnvironmentalExposureBand)Mathf.Max(
                (int)coldProjection.EndBand,
                Mathf.Max(
                    (int)heatProjection.EndBand,
                    Mathf.Max(
                        (int)airProjection.EndBand,
                        (int)visualProjection.EndBand)));
        bool needsProtection =
            (workCold >= 25f && highColdRate > 0f)
            || (workHeat >= 25f && highHeatRate > 0f);
        return new EnvironmentExposureProjection(
            coldProjection,
            heatProjection,
            airProjection,
            visualProjection,
            worst,
            needsProtection,
            protectionApplied,
            protectionFailure,
            DomainFailure.None);
    }

    private void EvaluateCellRates(
        CharacterActor actor,
        EnvironmentalCellSnapshot cell,
        EnvironmentalWorkKind workKind,
        out float coldRate,
        out float heatRate,
        out float airRate,
        out float visualRate,
        out bool lethal)
    {
        ThermalProtectionProfile resolvedProtection =
            protection.Resolve(actor);
        SpeciesThermalProfile thermal = speciesEnvironment
            .GetRequiredThermalProfile(
                new CharacterSpeciesId(actor.SpeciesTag))
            .Apply(resolvedProtection);
        CalculateTemperatureRates(
            cell.TemperatureC,
            thermal,
            resolvedProtection,
            out coldRate,
            out heatRate,
            out bool lethalTemperature);
        airRate = DungeonStory.Environment.CharacterEnvironmentRules
            .CalculateAirExposureRate(cell.AirQuality);
        visualRate = workKind is EnvironmentalWorkKind.Precision
            or EnvironmentalWorkKind.Surgery
            or EnvironmentalWorkKind.EmergencySurgery
                ? DungeonStory.Environment.CharacterEnvironmentRules
                    .CalculateVisualStrainRate(cell.LightLevel)
                : 0f;
        lethal = lethalTemperature
            || cell.AirQuality
                < EnvironmentalThresholdRules.ToxicAirQuality;
    }

    private static void CalculateTemperatureRates(
        float temperatureC,
        SpeciesThermalProfile thermal,
        ThermalProtectionProfile protection,
        out float coldRate,
        out float heatRate,
        out bool lethal)
    {
        DungeonStory.Environment.ThermalProtectionSnapshot protectionSnapshot =
            protection == null
                ? DungeonStory.Environment.ThermalProtectionSnapshot.None
                : new DungeonStory.Environment.ThermalProtectionSnapshot(
                    protection.comfortMinimumOffset,
                    protection.comfortMaximumOffset,
                    protection.safeMinimumOffset,
                    protection.safeMaximumOffset,
                    protection.coldExposureMultiplier,
                    protection.heatExposureMultiplier);
        DungeonStory.Environment.ThermalExposureRate result =
            DungeonStory.Environment.CharacterEnvironmentRules
                .CalculateTemperatureRates(
                    temperatureC,
                    new DungeonStory.Environment.ThermalRangeSnapshot(
                        thermal.ComfortMinimum,
                        thermal.ComfortMaximum,
                        thermal.SafeMinimum,
                        thermal.SafeMaximum,
                        thermal.LethalMinimum,
                        thermal.LethalMaximum),
                    protectionSnapshot);
        coldRate = result.ColdRate;
        heatRate = result.HeatRate;
        lethal = result.Lethal;
    }

    private static EnvironmentExposureChannelProjection CreateChannel(
        float current,
        float routeEnd,
        float workEnd,
        float highestRate,
        EnvironmentalExposureBand previousBand,
        Vector2Int highestCell,
        bool lethal)
    {
        return new EnvironmentExposureChannelProjection(
            current,
            routeEnd,
            workEnd,
            highestRate,
            EnvironmentalThresholdRules.ResolveBand(
                workEnd,
                previousBand),
            highestCell,
            lethal);
    }

    private static void TrackHighest(
        float rate,
        Vector2Int position,
        ref float highestRate,
        ref Vector2Int highestCell)
    {
        if (rate <= highestRate)
        {
            return;
        }

        highestRate = rate;
        highestCell = position;
    }

    private static void UpdateColdCooldown(
        CharacterEnvironmentExposure current)
    {
        if (current == null)
        {
            return;
        }

        current.coldWorkCooldownActive =
            DungeonStory.Environment.EnvironmentWorkRules.ResolveColdCooldown(
                current.coldExposure,
                current.coldWorkCooldownActive);
    }

    private static DomainFailure BuildProjectionFailure(
        EnvironmentExposureProjection projection,
        bool coldCooldownBlocks,
        DomainFailure protectionFailure,
        bool forced,
        bool canStart)
    {
        DungeonStory.Environment.EnvironmentWorkFailureKind failureKind =
            DungeonStory.Environment.EnvironmentWorkRules.ResolveFailure(
                new DungeonStory.Environment.EnvironmentWorkFailureRiskSnapshot(
                    (DungeonStory.Environment.ExposureBand)projection.WorstBand,
                    projection.HasLethalChannel,
                    coldCooldownBlocks,
                    protectionFailure.IsFailure,
                    forced,
                    canStart));
        if (failureKind
            == DungeonStory.Environment.EnvironmentWorkFailureKind.ColdCooldown)
        {
            return new DomainFailure(
                FailureCode.EnvironmentColdWorkCooldownActive,
                projection.Cold.Current.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture));
        }

        if (failureKind == DungeonStory.Environment.EnvironmentWorkFailureKind.None)
        {
            return DomainFailure.None;
        }

        if (failureKind
            == DungeonStory.Environment.EnvironmentWorkFailureKind.ProtectionUnavailable)
        {
            return protectionFailure;
        }

        EnvironmentExposureChannelProjection worst =
            new[]
                {
                    projection.Cold,
                    projection.Heat,
                    projection.Air,
                    projection.Visual
                }
                .OrderByDescending(channel => channel.EndBand)
                .ThenByDescending(channel => channel.WorkEnd)
                .First();
        return new DomainFailure(
            FailureCode.EnvironmentExposureCritical,
            worst.HighestRiskCell.x.ToString(CultureInfo.InvariantCulture),
            worst.HighestRiskCell.y.ToString(CultureInfo.InvariantCulture),
            worst.EndBand.ToString(),
            projection.Cold.WorkEnd.ToString(
                "0.#",
                CultureInfo.InvariantCulture),
            projection.Heat.WorkEnd.ToString(
                "0.#",
                CultureInfo.InvariantCulture),
            projection.Air.WorkEnd.ToString(
                "0.#",
                CultureInfo.InvariantCulture),
            projection.Visual.WorkEnd.ToString(
                "0.#",
                CultureInfo.InvariantCulture));
    }

    private static float ResolveLegacyWorkSpeed(
        EnvironmentalExposureBand band,
        EnvironmentalWorkKind workKind)
    {
        return DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)band,
                (DungeonStory.Environment.EnvironmentalWorkKind)workKind);
    }

}
