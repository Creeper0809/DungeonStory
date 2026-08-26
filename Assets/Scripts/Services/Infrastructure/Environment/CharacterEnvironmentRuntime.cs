using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterEnvironmentUnityAdapter :
    ICharacterEnvironmentStatusQuery,
    ICharacterEnvironmentExposureCommand,
    ICharacterEnvironmentWorkContext,
    ICharacterEnvironmentPersistence,
    ITickable
{
    private static readonly ProfilerMarker TickMarker =
        new ProfilerMarker("Environment.CharacterExposure.Tick");
    private const float TickInterval = 1f;

    private readonly IEnvironmentalFieldQuery field;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly ICharacterSpeciesEnvironmentCatalog speciesEnvironment;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly IEnvironmentalWorkwearPersistence workwear;
    private readonly ICharacterApparelPersistence apparel;
    private readonly IApparelWorkOrderPersistence apparelWorkOrders;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly IGameClock clock;
    private readonly CharacterEnvironmentAggregateStateStore stateStore;
    private readonly ICharacterPerformanceQuery performance;

    private Dictionary<CharacterId, CharacterEnvironmentExposure> states =>
        stateStore.Current.Exposures;
    private Dictionary<CharacterId, EnvironmentalWorkKind> workContexts =>
        stateStore.Current.WorkContexts;

    public CharacterEnvironmentUnityAdapter(
        IEnvironmentalFieldQuery field,
        ICharacterWorldQuery characters,
        ICharacterLifetimeQuery characterLifetime,
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment,
        ICharacterEnvironmentProtectionResolver protection,
        IEnvironmentalWorkwearPersistence workwear,
        ICharacterApparelPersistence apparel,
        IApparelWorkOrderPersistence apparelWorkOrders,
        ICharacterBodyHealthCommand bodyHealthCommands,
        IGameClock clock,
        CharacterEnvironmentAggregateStateStore stateStore,
        ICharacterPerformanceQuery performance)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.speciesEnvironment = speciesEnvironment
            ?? throw new ArgumentNullException(nameof(speciesEnvironment));
        this.protection = protection
            ?? throw new ArgumentNullException(nameof(protection));
        this.workwear = workwear
            ?? throw new ArgumentNullException(nameof(workwear));
        this.apparel = apparel
            ?? throw new ArgumentNullException(nameof(apparel));
        this.apparelWorkOrders = apparelWorkOrders
            ?? throw new ArgumentNullException(nameof(apparelWorkOrders));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public void Tick()
    {
        if (clock.IsPaused || !field.IsInitialized)
        {
            return;
        }

        stateStore.Current.Accumulator += Mathf.Max(0f, clock.DeltaTime);
        while (stateStore.Current.Accumulator >= TickInterval)
        {
            stateStore.Current.Accumulator -= TickInterval;
            using (TickMarker.Auto())
            {
                Step(TickInterval);
            }
        }
    }

    public CharacterEnvironmentExposure GetExposure(CharacterId characterId)
    {
        return states.TryGetValue(characterId, out CharacterEnvironmentExposure state)
            ? state
            : null;
    }

    public EnvironmentalExposureBand GetPhysiologicalBand(
        CharacterId characterId)
    {
        return GetExposure(characterId)?.physiologicalBand
            ?? EnvironmentalExposureBand.Stable;
    }

    public EnvironmentalExposureBand GetVisualBand(CharacterId characterId)
    {
        return GetExposure(characterId)?.visualBand
            ?? EnvironmentalExposureBand.Stable;
    }

    public float GetWorkSpeedMultiplier(CharacterId characterId)
    {
        return DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)GetPhysiologicalBand(
                    characterId),
                DungeonStory.Environment.EnvironmentalWorkKind.General);
    }

    public float GetPrecisionWorkSpeedMultiplier(CharacterId characterId)
    {
        EnvironmentalExposureBand band = (EnvironmentalExposureBand)Mathf.Max(
            (int)GetPhysiologicalBand(characterId),
            (int)GetVisualBand(characterId));
        return DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)band,
                DungeonStory.Environment.EnvironmentalWorkKind.Precision);
    }

    public float GetMoveSpeedMultiplier(CharacterId characterId)
    {
        return DungeonStory.Environment.CharacterEnvironmentRules
            .ResolveMoveSpeedMultiplier(
                (DungeonStory.Environment.ExposureBand)GetPhysiologicalBand(
                    characterId));
    }

    public float GetAccuracyPenaltyPoints(CharacterId characterId)
    {
        return DungeonStory.Environment.CharacterEnvironmentRules
            .ResolveAccuracyPenaltyPoints(
                (DungeonStory.Environment.ExposureBand)GetPhysiologicalBand(
                    characterId));
    }

    public bool AddAirborneExposure(CharacterId characterId, float amount)
    {
        if (!characterId.IsValid || amount <= 0f)
        {
            return false;
        }
        bool actorExists = (characters.Characters
                ?? Array.Empty<CharacterActor>())
            .Any(actor => actor != null
                && string.Equals(
                    actor.Identity?.PersistentId,
                    characterId.Value,
                    StringComparison.Ordinal));
        if (!actorExists)
        {
            return false;
        }

        CharacterEnvironmentExposure state = GetOrCreate(characterId);
        state.airborneExposure = Mathf.Clamp(
            state.airborneExposure + amount,
            0f,
            100f);
        float physiologicalExposure = Mathf.Max(
            state.coldExposure,
            state.heatExposure,
            state.airborneExposure);
        state.physiologicalBand = (EnvironmentalExposureBand)
            DungeonStory.Environment.EnvironmentalThresholdRules.ResolveBand(
                physiologicalExposure,
                (DungeonStory.Environment.ExposureBand)
                    state.physiologicalBand);
        return true;
    }

    public void SetWorkContext(
        CharacterId characterId,
        EnvironmentalWorkKind workKind)
    {
        if (!characterId.IsValid)
        {
            return;
        }

        workContexts[characterId] = workKind;
    }

    public void ClearWorkContext(CharacterId characterId)
    {
        workContexts.Remove(characterId);
    }

    public DungeonCharacterEnvironmentSaveData Capture()
    {
        HashSet<CharacterId> persistentCharacterIds = new(
            (characterLifetime.AllCharacters ?? Array.Empty<CharacterActor>())
                .Where(CharacterWorldPersistenceRules.IsPersistentActor)
                .Select(actor => new CharacterId(actor.Identity.PersistentId))
                .Where(id => id.IsValid));
        return new DungeonCharacterEnvironmentSaveData
        {
            version = DungeonCharacterEnvironmentSaveData.CurrentVersion,
            exposures = states.Values
                .Where(state => state != null
                    && persistentCharacterIds.Contains(
                        new CharacterId(state.characterId)))
                .OrderBy(state => state.characterId, StringComparer.Ordinal)
                .Select(Clone)
                .ToArray(),
            equippedWorkwear = workwear.CaptureEquipped().ToArray(),
            equippedApparel = apparel.CaptureApparel().ToArray(),
            apparelWorkOrders = apparelWorkOrders.CaptureOrders(),
            apparelWorkOrderTerminalStates =
                apparelWorkOrders.CaptureTerminalStates()
        };
    }

    public CharacterEnvironmentRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterEnvironmentSaveData saveData)
    {
        DungeonCharacterEnvironmentSaveData source = saveData
            ?? throw new ArgumentNullException(nameof(saveData));
        if (source.version != DungeonCharacterEnvironmentSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported character environment version {source.version}.");
        }

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        CharacterEnvironmentSaveValidation.Validate(source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-environment restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        CharacterEnvironmentAggregateState restored = new()
        {
            WorkwearVersion = stateStore.Current.WorkwearVersion + 1
        };

        HashSet<CharacterId> availableCharacters = new(
            (characters.Characters ?? Array.Empty<CharacterActor>())
                .Where(actor => actor?.Identity != null)
                .Select(actor => new CharacterId(actor.Identity.PersistentId))
                .Where(id => id.IsValid));
        foreach (CharacterEnvironmentExposure entry in source.exposures)
        {
            CharacterId characterId = new(entry.characterId);
            if (!availableCharacters.Contains(characterId))
            {
                throw new InvalidOperationException(
                    $"Environment exposure references unknown character '{entry.characterId}'.");
            }

            CharacterEnvironmentExposure restoredExposure = Clone(entry);
            if (!restored.Exposures.TryAdd(characterId, restoredExposure))
            {
                throw new InvalidOperationException(
                    $"Duplicate environment exposure character '{entry.characterId}'.");
            }
        }

        IReadOnlyDictionary<CharacterId, ItemInstanceId> preparedWorkwear =
            workwear.PrepareRestoreEquipped(
                source.equippedWorkwear,
                report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-environment workwear candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        CharacterApparelRestoreCandidate preparedApparel =
            apparel.PrepareRestoreApparel(source.equippedApparel, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Character-environment apparel candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        // V22 keeps equippedWorkwear as a derived compatibility projection.
        // It is validated above but no longer copied into the environment state.
        _ = preparedWorkwear;
        ApparelWorkOrderRestoreCandidate preparedWorkOrders =
            apparelWorkOrders.PrepareRestoreState(
                source.apparelWorkOrders,
                source.apparelWorkOrderTerminalStates);
        return new CharacterEnvironmentRestoreCandidate(
            restored,
            preparedApparel,
            preparedWorkOrders);
    }

    public void PublishRestoreCandidate(
        CharacterEnvironmentRestoreCandidate candidate)
    {
        CharacterEnvironmentRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        stateStore.Replace(required.State);
        apparel.PublishRestoreApparel(required.Apparel);
        apparelWorkOrders.PublishRestoreState(required.ApparelWorkOrders);
    }

    public void Reset()
    {
        stateStore.Replace(new CharacterEnvironmentAggregateState
        {
            WorkwearVersion = stateStore.Current.WorkwearVersion + 1
        });
        apparel.ResetApparel();
        apparelWorkOrders.ResetOrders();
    }

    public static void CalculateTemperatureRates(
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

    private void Step(float deltaTime)
    {
        HashSet<CharacterId> activeIds = new();
        IReadOnlyList<CharacterActor> actors =
            characters.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            CharacterId characterId = new(actor?.Identity?.PersistentId);
            if (actor == null
                || actor.IsDead
                || actor.IsOnExpedition
                || !characterId.IsValid
                || !field.TryGetCell(
                    actor.GetNowXY(),
                    out EnvironmentalCellSnapshot environment))
            {
                continue;
            }

            CharacterSpeciesId speciesId = new CharacterSpeciesId(actor.SpeciesTag);
            if (!speciesId.IsValid)
            {
                continue;
            }
            activeIds.Add(characterId);
            CharacterEnvironmentExposure state =
                GetOrCreate(characterId);
            ThermalProtectionProfile resolvedProtection =
                protection.Resolve(actor);
            ApplySleepingInsulation(actor, resolvedProtection);
            CharacterThermalGameplayEffectProjection.Apply(
                actor,
                resolvedProtection,
                performance);
            SpeciesThermalProfile thermal =
                speciesEnvironment
                    .GetRequiredThermalProfile(
                        speciesId)
                    .Apply(resolvedProtection);
            CalculateTemperatureRates(
                environment.TemperatureC,
                thermal,
                resolvedProtection,
                out float coldRate,
                out float heatRate,
                out bool lethalTemperature);

            float airRate = CalculateAirExposureRate(
                environment.AirQuality);
            bool precisionContext = workContexts.TryGetValue(
                    characterId,
                    out EnvironmentalWorkKind workKind)
                && workKind is EnvironmentalWorkKind.Precision
                    or EnvironmentalWorkKind.Surgery
                    or EnvironmentalWorkKind.EmergencySurgery;
            float visualRate = precisionContext
                ? CalculateVisualStrainRate(environment.LightLevel)
                : 0f;
            DungeonStory.Environment.CharacterExposureStepResult step =
                DungeonStory.Environment.CharacterEnvironmentRules.StepExposure(
                    new DungeonStory.Environment.CharacterExposureStepInput(
                        state.coldExposure,
                        state.heatExposure,
                        state.airborneExposure,
                        state.visualStrain,
                        (DungeonStory.Environment.ExposureBand)
                            state.physiologicalBand,
                        (DungeonStory.Environment.ExposureBand)state.visualBand,
                        coldRate,
                        heatRate,
                        airRate,
                        visualRate,
                        environment.TemperatureC >= thermal.ComfortMinimum
                            && environment.TemperatureC <= thermal.ComfortMaximum,
                        environment.AirQuality
                            >= EnvironmentalThresholdRules.NormalAirQuality,
                        !precisionContext
                            || environment.LightLevel
                                >= EnvironmentalThresholdRules.PrecisionMinimumLight,
                        deltaTime));
            state.coldExposure = step.ColdExposure;
            state.heatExposure = step.HeatExposure;
            state.airborneExposure = step.AirborneExposure;
            state.visualStrain = step.VisualStrain;
            state.physiologicalBand =
                (EnvironmentalExposureBand)step.PhysiologicalBand;
            state.visualBand = (EnvironmentalExposureBand)step.VisualBand;
            ApplyBandEffects(
                actor,
                state,
                (EnvironmentalExposureBand)step.PreviousPhysiologicalBand,
                deltaTime);
            if (lethalTemperature
                || environment.AirQuality
                    < EnvironmentalThresholdRules.ToxicAirQuality)
            {
                bodyHealthCommands.ApplyLegacyDamage(
                    actor,
                    actor.MaxHealth * 0.01f * deltaTime,
                    "치명적 환경 노출",
                    allowDeath: true);
            }
        }

        foreach (CharacterId staleId in states.Keys
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
        AbilityWork work = null;
        actor?.TryGetAbility(out work);
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
                    bodyHealthCommands.AddSuppression(actor, 100f);
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
                bodyHealthCommands.ApplyLegacyDamage(
                    actor,
                    actor.MaxHealth * 0.01f,
                    "환경 위급 노출",
                    allowDeath: false);
            }
        }
        else
        {
            state.criticalDamageTimer = 0f;
        }
    }

    private CharacterEnvironmentExposure GetOrCreate(CharacterId characterId)
    {
        if (!states.TryGetValue(
            characterId,
            out CharacterEnvironmentExposure state))
        {
            state = new CharacterEnvironmentExposure
            {
                characterId = characterId.Value
            };
            states.Add(characterId, state);
        }

        return state;
    }

    internal static float CalculateAirExposureRate(float airQuality)
    {
        return DungeonStory.Environment.CharacterEnvironmentRules
            .CalculateAirExposureRate(airQuality);
    }

    internal static float CalculateVisualStrainRate(float lightLevel)
    {
        return DungeonStory.Environment.CharacterEnvironmentRules
            .CalculateVisualStrainRate(lightLevel);
    }

    internal static CharacterEnvironmentExposure Clone(
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

    internal static void Clamp(CharacterEnvironmentExposure state)
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
