using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterEnvironmentRuntime :
    ICharacterEnvironmentRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickMarker =
        new ProfilerMarker("Environment.CharacterExposure.Tick");
    private const float TickInterval = 1f;
    private const float ComfortableRecoveryPerSecond = 1.5f;

    private readonly IEnvironmentalFieldRuntime field;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly IEnvironmentalWorkwearRuntime workwear;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly IGameClock clock;
    private readonly Dictionary<string, CharacterEnvironmentExposure> states =
        new Dictionary<string, CharacterEnvironmentExposure>(
            StringComparer.Ordinal);
    private readonly Dictionary<string, EnvironmentalWorkKind> workContexts =
        new Dictionary<string, EnvironmentalWorkKind>(
            StringComparer.Ordinal);
    private float accumulator;

    public CharacterEnvironmentRuntime(
        IEnvironmentalFieldRuntime field,
        ICharacterWorldQuery characters,
        ICharacterEnvironmentProtectionResolver protection,
        IEnvironmentalWorkwearRuntime workwear,
        ICharacterBodyHealthRuntime bodyHealth,
        IGameClock clock)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.protection = protection
            ?? throw new ArgumentNullException(nameof(protection));
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Tick()
    {
        if (clock.IsPaused || !field.IsInitialized)
        {
            return;
        }

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

    public CharacterEnvironmentExposure GetExposure(string characterId)
    {
        string id = characterId?.Trim() ?? string.Empty;
        return states.TryGetValue(id, out CharacterEnvironmentExposure state)
            ? state
            : null;
    }

    public EnvironmentalExposureBand GetPhysiologicalBand(
        string characterId)
    {
        return GetExposure(characterId)?.physiologicalBand
            ?? EnvironmentalExposureBand.Stable;
    }

    public EnvironmentalExposureBand GetVisualBand(string characterId)
    {
        return GetExposure(characterId)?.visualBand
            ?? EnvironmentalExposureBand.Stable;
    }

    public float GetWorkSpeedMultiplier(string characterId)
    {
        return GetPhysiologicalBand(characterId) switch
        {
            EnvironmentalExposureBand.Burden => 0.9f,
            EnvironmentalExposureBand.Impaired => 0.75f,
            EnvironmentalExposureBand.Critical => 0.5f,
            EnvironmentalExposureBand.Collapse => 0.1f,
            _ => 1f
        };
    }

    public float GetPrecisionWorkSpeedMultiplier(string characterId)
    {
        EnvironmentalExposureBand band = (EnvironmentalExposureBand)Mathf.Max(
            (int)GetPhysiologicalBand(characterId),
            (int)GetVisualBand(characterId));
        return band switch
        {
            EnvironmentalExposureBand.Burden => 0.85f,
            EnvironmentalExposureBand.Impaired => 0.6f,
            EnvironmentalExposureBand.Critical
                or EnvironmentalExposureBand.Collapse => 0.35f,
            _ => 1f
        };
    }

    public float GetMoveSpeedMultiplier(string characterId)
    {
        return GetPhysiologicalBand(characterId) switch
        {
            EnvironmentalExposureBand.Burden => 0.95f,
            EnvironmentalExposureBand.Impaired => 0.85f,
            EnvironmentalExposureBand.Critical => 0.7f,
            EnvironmentalExposureBand.Collapse => 0.1f,
            _ => 1f
        };
    }

    public float GetAccuracyPenaltyPoints(string characterId)
    {
        return GetPhysiologicalBand(characterId) switch
        {
            EnvironmentalExposureBand.Impaired => 10f,
            EnvironmentalExposureBand.Critical
                or EnvironmentalExposureBand.Collapse => 25f,
            _ => 0f
        };
    }

    public void SetWorkContext(
        string characterId,
        EnvironmentalWorkKind workKind)
    {
        string id = characterId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            return;
        }

        workContexts[id] = workKind;
    }

    public void ClearWorkContext(string characterId)
    {
        workContexts.Remove(characterId?.Trim() ?? string.Empty);
    }

    public DungeonCharacterEnvironmentSaveData Capture()
    {
        return new DungeonCharacterEnvironmentSaveData
        {
            exposures = states.Values
                .OrderBy(state => state.characterId, StringComparer.Ordinal)
                .Select(Clone)
                .ToList(),
            equippedWorkwear = workwear.CaptureEquipped().ToList(),
            workwearStock = workwear.CaptureStock().ToList()
        };
    }

    public void Restore(
        DungeonCharacterEnvironmentSaveData saveData,
        DungeonGameRestoreReport report = null)
    {
        states.Clear();
        workContexts.Clear();
        DungeonCharacterEnvironmentSaveData source =
            saveData ?? new DungeonCharacterEnvironmentSaveData();
        if (source.version != DungeonCharacterEnvironmentSaveData.CurrentVersion)
        {
            report?.AddError(
                $"Unsupported character environment version {source.version}.");
            return;
        }

        foreach (CharacterEnvironmentExposure entry in source.exposures
                     ?? Enumerable.Empty<CharacterEnvironmentExposure>())
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.characterId))
            {
                report?.AddWarning(
                    "An environment exposure entry with no character id was ignored.");
                continue;
            }

            CharacterEnvironmentExposure restored = Clone(entry);
            Clamp(restored);
            states[restored.characterId] = restored;
        }

        workwear.Restore(
            source.equippedWorkwear,
            source.workwearStock,
            report);
    }

    public void Reset()
    {
        states.Clear();
        workContexts.Clear();
        workwear.Reset();
        accumulator = 0f;
    }

    public static void CalculateTemperatureRates(
        float temperatureC,
        SpeciesThermalProfile thermal,
        ThermalProtectionProfile protection,
        out float coldRate,
        out float heatRate,
        out bool lethal)
    {
        coldRate = 0f;
        heatRate = 0f;
        lethal = false;
        float coldMultiplier = protection != null
            ? Mathf.Clamp(protection.coldExposureMultiplier, 0.05f, 2f)
            : 1f;
        float heatMultiplier = protection != null
            ? Mathf.Clamp(protection.heatExposureMultiplier, 0.05f, 2f)
            : 1f;

        if (temperatureC < thermal.ComfortMinimum)
        {
            coldRate = CalculateSideRate(
                thermal.ComfortMinimum,
                thermal.SafeMinimum,
                thermal.LethalMinimum,
                temperatureC);
            coldRate *= coldMultiplier;
            lethal = temperatureC <= thermal.LethalMinimum;
        }
        else if (temperatureC > thermal.ComfortMaximum)
        {
            heatRate = CalculateSideRate(
                -thermal.ComfortMaximum,
                -thermal.SafeMaximum,
                -thermal.LethalMaximum,
                -temperatureC);
            heatRate *= heatMultiplier;
            lethal = temperatureC >= thermal.LethalMaximum;
        }
    }

    private void Step(float deltaTime)
    {
        HashSet<string> activeIds = new HashSet<string>(
            StringComparer.Ordinal);
        IReadOnlyList<CharacterActor> actors =
            characters.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            string characterId = actor?.Identity?.PersistentId;
            if (actor == null
                || actor.IsDead
                || actor.IsOnExpedition
                || string.IsNullOrWhiteSpace(characterId)
                || !field.TryGetCell(
                    actor.GetNowXY(),
                    out EnvironmentalCellSnapshot environment))
            {
                continue;
            }

            activeIds.Add(characterId);
            CharacterEnvironmentExposure state =
                GetOrCreate(characterId);
            ThermalProtectionProfile resolvedProtection =
                protection.Resolve(actor);
            ApplySleepingInsulation(actor, resolvedProtection);
            SpeciesThermalProfile thermal =
                SpeciesThermalProfile
                    .ForSpecies(actor.SpeciesTag)
                    .Apply(resolvedProtection);
            CalculateTemperatureRates(
                environment.TemperatureC,
                thermal,
                resolvedProtection,
                out float coldRate,
                out float heatRate,
                out bool lethalTemperature);

            state.coldExposure = UpdateExposure(
                state.coldExposure,
                coldRate,
                environment.TemperatureC >= thermal.ComfortMinimum
                && environment.TemperatureC <= thermal.ComfortMaximum,
                deltaTime);
            state.heatExposure = UpdateExposure(
                state.heatExposure,
                heatRate,
                environment.TemperatureC >= thermal.ComfortMinimum
                && environment.TemperatureC <= thermal.ComfortMaximum,
                deltaTime);

            float airRate = CalculateAirExposureRate(
                environment.AirQuality);
            state.airborneExposure = UpdateExposure(
                state.airborneExposure,
                airRate,
                environment.AirQuality
                    >= EnvironmentalThresholdRules.NormalAirQuality,
                deltaTime);

            bool precisionContext = workContexts.TryGetValue(
                    characterId,
                    out EnvironmentalWorkKind workKind)
                && workKind is EnvironmentalWorkKind.Precision
                    or EnvironmentalWorkKind.Surgery
                    or EnvironmentalWorkKind.EmergencySurgery;
            float visualRate = precisionContext
                ? CalculateVisualStrainRate(environment.LightLevel)
                : 0f;
            state.visualStrain = UpdateExposure(
                state.visualStrain,
                visualRate,
                !precisionContext
                || environment.LightLevel
                    >= EnvironmentalThresholdRules.PrecisionMinimumLight,
                deltaTime);

            EnvironmentalExposureBand previous = state.physiologicalBand;
            float physiologicalExposure = Mathf.Max(
                state.coldExposure,
                Mathf.Max(state.heatExposure, state.airborneExposure));
            state.physiologicalBand =
                EnvironmentalThresholdRules.ResolveBand(
                    physiologicalExposure,
                    previous);
            state.visualBand = EnvironmentalThresholdRules.ResolveBand(
                state.visualStrain,
                state.visualBand);
            ApplyBandEffects(actor, state, previous, deltaTime);
            if (lethalTemperature
                || environment.AirQuality
                    < EnvironmentalThresholdRules.ToxicAirQuality)
            {
                actor.ApplyDamage(
                    actor.MaxHealth * 0.01f * deltaTime,
                    "치명적 환경 노출");
            }
        }

        foreach (string staleId in states.Keys
                     .Where(id => !activeIds.Contains(id))
                     .ToArray())
        {
            workContexts.Remove(staleId);
        }
    }

    private static void ApplySleepingInsulation(
        CharacterActor actor,
        ThermalProtectionProfile protectionProfile)
    {
        AbilityWork work = actor?.GetAbility<AbilityWork>();
        BuildableObject restFacility = work?.assignedShop;
        if (work?.AssignedWorkType != FacilityWorkType.Rest
            || restFacility?.Facility
                ?.SupportsRole(FacilityRole.Rest) != true)
        {
            return;
        }

        BuildingTemperatureAbility bedding =
            restFacility.BuildingData
                ?.GetAbility<BuildingTemperatureAbility>();
        if (bedding == null || bedding.coldProtection <= 0f)
        {
            return;
        }

        protectionProfile.Add(new ThermalProtectionProfile
        {
            comfortMinimumOffset = -bedding.coldProtection,
            safeMinimumOffset = -bedding.coldProtection * 0.5f,
            coldExposureMultiplier = 0.6f
        });
    }

    private void ApplyBandEffects(
        CharacterActor actor,
        CharacterEnvironmentExposure state,
        EnvironmentalExposureBand previous,
        float deltaTime)
    {
        if (state.physiologicalBand != previous)
        {
            switch (state.physiologicalBand)
            {
                case EnvironmentalExposureBand.Burden:
                    actor.ApplyMoodFactor(
                        "environment:burden",
                        "환경 부담",
                        -5f,
                        15f);
                    break;
                case EnvironmentalExposureBand.Impaired:
                    actor.ApplyMoodFactor(
                        "environment:impaired",
                        "환경 기능 저하",
                        -10f,
                        15f);
                    break;
                case EnvironmentalExposureBand.Critical:
                    actor.ApplyMoodFactor(
                        "environment:critical",
                        "환경 위급",
                        -20f,
                        15f);
                    break;
                case EnvironmentalExposureBand.Collapse:
                    bodyHealth.AddSuppression(actor, 100f);
                    actor.AddLog("환경 노출로 쓰러졌습니다.");
                    break;
            }
        }

        if (state.physiologicalBand >= EnvironmentalExposureBand.Critical)
        {
            state.criticalDamageTimer += deltaTime;
            if (state.criticalDamageTimer >= 10f)
            {
                state.criticalDamageTimer -= 10f;
                actor.ApplyBodyDamage(
                    actor.MaxHealth * 0.01f,
                    "환경 위급 노출");
            }
        }
        else
        {
            state.criticalDamageTimer = 0f;
        }
    }

    private CharacterEnvironmentExposure GetOrCreate(string characterId)
    {
        if (!states.TryGetValue(
            characterId,
            out CharacterEnvironmentExposure state))
        {
            state = new CharacterEnvironmentExposure
            {
                characterId = characterId
            };
            states.Add(characterId, state);
        }

        return state;
    }

    private static float UpdateExposure(
        float current,
        float rate,
        bool comfortable,
        float deltaTime)
    {
        return Mathf.Clamp(
            comfortable
                ? current - ComfortableRecoveryPerSecond * deltaTime
                : current + Mathf.Max(0f, rate) * deltaTime,
            0f,
            100f);
    }

    private static float CalculateSideRate(
        float comfortBoundary,
        float safeBoundary,
        float lethalBoundary,
        float value)
    {
        if (value >= comfortBoundary)
        {
            return 0f;
        }

        if (value >= safeBoundary)
        {
            float denominator = Mathf.Max(
                0.01f,
                comfortBoundary - safeBoundary);
            float normalized = Mathf.Clamp01(
                (comfortBoundary - value) / denominator);
            return 0.15f * Mathf.Pow(normalized, 1.5f);
        }

        if (value > lethalBoundary)
        {
            float denominator = Mathf.Max(
                0.01f,
                safeBoundary - lethalBoundary);
            float normalized = Mathf.Clamp01(
                (safeBoundary - value) / denominator);
            return 0.5f + 1.5f * Mathf.Pow(normalized, 1.5f);
        }

        return 2f;
    }

    internal static float CalculateAirExposureRate(float airQuality)
    {
        if (airQuality >= EnvironmentalThresholdRules.NormalAirQuality)
        {
            return 0f;
        }

        if (airQuality >= EnvironmentalThresholdRules.PollutedAirQuality)
        {
            float normalized = Mathf.InverseLerp(
                EnvironmentalThresholdRules.NormalAirQuality,
                EnvironmentalThresholdRules.PollutedAirQuality,
                airQuality);
            return 0.15f * Mathf.Pow(normalized, 1.5f);
        }

        if (airQuality >= EnvironmentalThresholdRules.ToxicAirQuality)
        {
            float normalized = Mathf.InverseLerp(
                EnvironmentalThresholdRules.PollutedAirQuality,
                EnvironmentalThresholdRules.ToxicAirQuality,
                airQuality);
            return 0.5f + 1.5f * Mathf.Pow(normalized, 1.5f);
        }

        return 2f;
    }

    internal static float CalculateVisualStrainRate(float lightLevel)
    {
        if (lightLevel >= EnvironmentalThresholdRules.PrecisionMinimumLight)
        {
            return 0f;
        }

        float normalized = 1f - Mathf.Clamp01(
            lightLevel
            / EnvironmentalThresholdRules.PrecisionMinimumLight);
        return Mathf.Lerp(0.15f, 1f, Mathf.Pow(normalized, 1.5f));
    }

    private static CharacterEnvironmentExposure Clone(
        CharacterEnvironmentExposure source)
    {
        return new CharacterEnvironmentExposure
        {
            characterId = source.characterId?.Trim() ?? string.Empty,
            coldExposure = source.coldExposure,
            heatExposure = source.heatExposure,
            airborneExposure = source.airborneExposure,
            visualStrain = source.visualStrain,
            physiologicalBand = source.physiologicalBand,
            visualBand = source.visualBand,
            criticalDamageTimer = source.criticalDamageTimer,
            coldWorkCooldownActive = source.coldWorkCooldownActive
        };
    }

    private static void Clamp(CharacterEnvironmentExposure state)
    {
        state.coldExposure = Mathf.Clamp(state.coldExposure, 0f, 100f);
        state.heatExposure = Mathf.Clamp(state.heatExposure, 0f, 100f);
        state.airborneExposure = Mathf.Clamp(
            state.airborneExposure,
            0f,
            100f);
        state.visualStrain = Mathf.Clamp(state.visualStrain, 0f, 100f);
        state.criticalDamageTimer = Mathf.Max(
            0f,
            state.criticalDamageTimer);
    }
}

public sealed class EnvironmentWorkPolicy : IEnvironmentWorkPolicy
{
    private readonly IEnvironmentalFieldRuntime field;
    private readonly ICharacterEnvironmentStatusQuery status;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly IEnvironmentalWorkwearRuntime workwear;

    public EnvironmentWorkPolicy(
        IEnvironmentalFieldRuntime field,
        ICharacterEnvironmentStatusQuery status,
        ICharacterEnvironmentProtectionResolver protection,
        IEnvironmentalWorkwearRuntime workwear)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.status = status ?? throw new ArgumentNullException(nameof(status));
        this.protection = protection
            ?? throw new ArgumentNullException(nameof(protection));
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
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
                "작업 대상의 환경 정보를 확인할 수 없습니다.");
        }

        CharacterEnvironmentExposure current =
            status.GetExposure(actor.Identity?.PersistentId);
        bool exception = IsSafetyException(workKind);
        UpdateColdCooldown(current);
        EnvironmentExposureProjection projection = Project(
            actor,
            destination,
            route,
            Mathf.Max(0f, expectedSeconds),
            workKind,
            protectionApplied: false,
            protectionFailure: string.Empty);

        string protectionFailure = string.Empty;
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
                    protectionFailure: string.Empty);
            }
        }

        bool coldCooldownBlocks = !forced
            && !exception
            && current?.coldWorkCooldownActive == true
            && projection.Cold.RouteHighestRate > 0f;
        bool canStart = forced
            || exception
            || (!coldCooldownBlocks
                && projection.WorstBand
                    < EnvironmentalExposureBand.Critical
                && !projection.HasLethalChannel
                && (!projection.NeedsProtection
                    || protectionApplied
                    || projection.Cold.WorkEnd < 25f));
        string reason = BuildProjectionReason(
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
            ResolvePrecisionMultiplier(
                projection.WorstBand,
                workKind),
            reason,
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
        out string failureReason)
    {
        destination = actor != null ? actor.GetNowXY() : default;
        fullySafe = false;
        if (actor == null || grid == null)
        {
            failureReason = "대피할 캐릭터 또는 그리드가 없습니다.";
            return false;
        }

        CharacterEnvironmentExposure current =
            status.GetExposure(actor.Identity?.PersistentId);
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
            failureReason = "도달 가능한 대피 셀이 없습니다.";
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
        failureReason = fullySafe
            ? string.Empty
            : "안전한 대피 경로 없음: 도달 가능한 셀 중 노출 증가가 가장 낮은 위치로 이동합니다.";
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
                "작업 대상의 환경 정보를 확인할 수 없습니다.");
        }

        ThermalProtectionProfile resolvedProtection =
            protection.Resolve(actor);
        SpeciesThermalProfile thermal =
            SpeciesThermalProfile
                .ForSpecies(actor.SpeciesTag)
                .Apply(resolvedProtection);
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
            environment.TemperatureC,
            thermal,
            resolvedProtection,
            out float coldRate,
            out float heatRate,
            out _);
        CharacterEnvironmentExposure current =
            status.GetExposure(actor.Identity?.PersistentId);
        float currentExposure = Mathf.Max(
            current?.coldExposure ?? 0f,
            Mathf.Max(
                current?.heatExposure ?? 0f,
                current?.airborneExposure ?? 0f));
        float thermalRate = Mathf.Max(coldRate, heatRate);
        float airRate =
            CharacterEnvironmentRuntime.CalculateAirExposureRate(
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
        if (precision)
        {
            float projectedVisual = Mathf.Clamp(
                (current?.visualStrain ?? 0f)
                + CharacterEnvironmentRuntime.CalculateVisualStrainRate(
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
                ResolvePrecisionMultiplier(
                    current.physiologicalBand,
                    workKind),
                "냉기 노출이 10 미만으로 회복될 때까지 새 냉장 작업을 배정하지 않습니다.");
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
                out string equipmentFailure))
            {
                resolvedProtection = protection.Resolve(actor);
                thermal = SpeciesThermalProfile
                    .ForSpecies(actor.SpeciesTag)
                    .Apply(resolvedProtection);
                CharacterEnvironmentRuntime.CalculateTemperatureRates(
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
                        ResolvePrecisionMultiplier(
                            projectedBand,
                            workKind),
                        canStart
                            ? string.Empty
                            : "보호장비를 착용해도 예상 노출이 위급합니다.");
                }

                equipmentFailure =
                    "가용 보호장비로 예상 노출을 안정 구간까지 낮출 수 없습니다.";
            }

            return new WorkEnvironmentAssessment(
                false,
                true,
                projected,
                ResolvePrecisionMultiplier(projectedBand, workKind),
                $"방한 장비 대기: {equipmentFailure}");
        }

        return new WorkEnvironmentAssessment(
            canStart,
            needsProtection,
            projected,
            ResolvePrecisionMultiplier(projectedBand, workKind),
            canStart
                ? string.Empty
                : "환경 노출이 위급 단계에 도달할 것으로 예상됩니다.");
    }

    private EnvironmentExposureProjection Project(
        CharacterActor actor,
        Vector2Int destination,
        IReadOnlyList<GridMoveStep> route,
        float expectedSeconds,
        EnvironmentalWorkKind workKind,
        bool protectionApplied,
        string protectionFailure)
    {
        CharacterEnvironmentExposure current =
            status.GetExposure(actor.Identity?.PersistentId);
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
            string.Empty);
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
        SpeciesThermalProfile thermal = SpeciesThermalProfile
            .ForSpecies(actor.SpeciesTag)
            .Apply(resolvedProtection);
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
            cell.TemperatureC,
            thermal,
            resolvedProtection,
            out coldRate,
            out heatRate,
            out bool lethalTemperature);
        airRate = CharacterEnvironmentRuntime.CalculateAirExposureRate(
            cell.AirQuality);
        visualRate = workKind is EnvironmentalWorkKind.Precision
            or EnvironmentalWorkKind.Surgery
            or EnvironmentalWorkKind.EmergencySurgery
                ? CharacterEnvironmentRuntime.CalculateVisualStrainRate(
                    cell.LightLevel)
                : 0f;
        lethal = lethalTemperature
            || cell.AirQuality
                < EnvironmentalThresholdRules.ToxicAirQuality;
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

        if (current.coldExposure >= 15f)
        {
            current.coldWorkCooldownActive = true;
        }
        else if (current.coldExposure < 10f)
        {
            current.coldWorkCooldownActive = false;
        }
    }

    private static bool IsSafetyException(EnvironmentalWorkKind workKind)
    {
        return workKind is EnvironmentalWorkKind.EmergencySurgery
            or EnvironmentalWorkKind.Defense
            or EnvironmentalWorkKind.Safety;
    }

    private static string BuildProjectionReason(
        EnvironmentExposureProjection projection,
        bool coldCooldownBlocks,
        string protectionFailure,
        bool forced,
        bool canStart)
    {
        if (coldCooldownBlocks)
        {
            return $"냉기 노출 재진입 잠금: 현재 {projection.Cold.Current:0.#}, "
                + "10 미만으로 회복해야 다시 배정됩니다.";
        }

        if (canStart && !forced)
        {
            return string.Empty;
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
        string protection = string.IsNullOrWhiteSpace(protectionFailure)
            ? string.Empty
            : $" 보호구 확보 실패: {protectionFailure}";
        string prefix = forced
            ? "강제 작업 위험 확인 필요"
            : "환경 위험으로 작업 차단";
        return $"{prefix}: 위험 셀 ({worst.HighestRiskCell.x},{worst.HighestRiskCell.y}), "
            + $"예상 단계 {worst.EndBand}, 냉기 {projection.Cold.WorkEnd:0.#}, "
            + $"열기 {projection.Heat.WorkEnd:0.#}, 공기 {projection.Air.WorkEnd:0.#}, "
            + $"시각 {projection.Visual.WorkEnd:0.#}.{protection}";
    }

    private static float ResolvePrecisionMultiplier(
        EnvironmentalExposureBand band,
        EnvironmentalWorkKind workKind)
    {
        if (workKind is not EnvironmentalWorkKind.Precision
            and not EnvironmentalWorkKind.Surgery
            and not EnvironmentalWorkKind.EmergencySurgery)
        {
            return band switch
            {
                EnvironmentalExposureBand.Burden => 0.9f,
                EnvironmentalExposureBand.Impaired => 0.75f,
                EnvironmentalExposureBand.Critical => 0.5f,
                EnvironmentalExposureBand.Collapse => 0.1f,
                _ => 1f
            };
        }

        return band switch
        {
            EnvironmentalExposureBand.Burden => 0.85f,
            EnvironmentalExposureBand.Impaired => 0.6f,
            EnvironmentalExposureBand.Critical
                or EnvironmentalExposureBand.Collapse => 0.35f,
            _ => 1f
        };
    }
}
