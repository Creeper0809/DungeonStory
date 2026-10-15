using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private const string LightAdaptationMoodFactorId =
        "environment:light-adaptation";
    private const float EnvironmentMoodFactorDurationSeconds = 15f;

    private readonly IEnvironmentalFieldQuery field;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly ICharacterSpeciesEnvironmentCatalog speciesEnvironment;
    private readonly ICharacterEnvironmentProtectionResolver protection;
    private readonly IEnvironmentalWorkwearPersistence workwear;
    private readonly ICharacterApparelPersistence apparel;
    private readonly ICharacterApparelCommand apparelCommands;
    private readonly IApparelWorkOrderPersistence apparelWorkOrders;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly IDomainFactPublisher domainFacts;
    private readonly CharacterEnvironmentAggregateStateStore stateStore;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ApparelConditionRuntime apparelConditions;
#if UNITY_EDITOR
    private HashSet<CharacterId> debugIsolatedPreparationExposureIds;
    private int debugHeldPreparationExposureSteps;
    public bool DebugIsolatedPreparationExposureHeld => debugIsolatedPreparationExposureIds != null;
    public int DebugHeldPreparationExposureSteps => debugHeldPreparationExposureSteps;

    // An owned isolated test-preparation lease, never saved or available in a
    // player. Existing outcome convergence and apparel processing still run.
    public IDisposable DebugHoldExposureForIsolatedPreparation(IReadOnlyCollection<string> actorIds)
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification
                || Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_PREPARATION_ENVIRONMENT_FIXTURE") != "1")
            throw new InvalidOperationException("Exposure hold requires isolated batch test preparation.");
        if (debugIsolatedPreparationExposureIds != null)
            throw new InvalidOperationException("Exposure preparation already has an owner.");
        HashSet<CharacterId> ids = new((actorIds ?? Array.Empty<string>()).Select(id => new CharacterId(id)));
        HashSet<CharacterId> available = new((characters.Characters ?? Array.Empty<CharacterActor>())
            .Where(actor => actor != null && !actor.IsDead)
            .Select(actor => new CharacterId(actor.Identity.PersistentId)));
        if (actorIds == null || actorIds.Count != 3 || ids.Count != 3
                || ids.Any(id => !id.IsValid || !available.Contains(id)))
            throw new InvalidOperationException("Exposure preparation requires exactly three unique live residents.");
        if (exposureOutcomes.Values.Any(value => value != null
                && ids.Contains(new CharacterId(value.characterId))))
            throw new InvalidOperationException("Exposure preparation cannot bypass a resident's pending exposure outcome.");
        debugIsolatedPreparationExposureIds = ids;
        debugHeldPreparationExposureSteps = 0;
        return new IsolatedPreparationExposureHold(this);
    }

    private sealed class IsolatedPreparationExposureHold : IDisposable
    {
        private CharacterEnvironmentUnityAdapter owner;
        public IsolatedPreparationExposureHold(CharacterEnvironmentUnityAdapter owner) => this.owner = owner;
        public void Dispose()
        {
            if (owner == null) return;
            owner.debugIsolatedPreparationExposureIds = null;
            owner = null;
        }
    }

    public void DebugNeutralizeHeldPreparationExposure()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification
                || debugIsolatedPreparationExposureIds == null)
            throw new InvalidOperationException("Neutral preparation input requires its owned isolated exposure lease.");
        foreach (CharacterId id in debugIsolatedPreparationExposureIds)
        {
            states[id] = new CharacterEnvironmentExposure
            {
                characterId = id.Value,
                physiologicalBand = EnvironmentalExposureBand.Stable,
                visualBand = EnvironmentalExposureBand.Stable
            };
            workContexts.Remove(id);
        }
        // Other actors, accumulator, workwear, revisions and outcome owners
        // are untouched. Do not use a full world-restore candidate here.
    }
#endif

    private Dictionary<CharacterId, CharacterEnvironmentExposure> states =>
        stateStore.Current.Exposures;
    private Dictionary<CharacterId, EnvironmentalWorkKind> workContexts =>
        stateStore.Current.WorkContexts;
    private Dictionary<string, EnvironmentalExposureOutcomeSaveData>
        exposureOutcomes => stateStore.Current.ExposureOutcomes;

    public CharacterEnvironmentUnityAdapter(
        IEnvironmentalFieldQuery field,
        ICharacterWorldQuery characters,
        ICharacterLifetimeQuery characterLifetime,
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment,
        ICharacterEnvironmentProtectionResolver protection,
        IEnvironmentalWorkwearPersistence workwear,
        ICharacterApparelPersistence apparel,
        ICharacterApparelCommand apparelCommands,
        IApparelWorkOrderPersistence apparelWorkOrders,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterBodyHealthCommand bodyHealthCommands,
        IGameClock clock,
        CharacterEnvironmentAggregateStateStore stateStore,
        ICharacterPerformanceQuery performance,
        ApparelConditionRuntime apparelConditions,
        IGameCalendar calendar,
        IDomainFactPublisher domainFacts)
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
        this.apparelCommands = apparelCommands
            ?? throw new ArgumentNullException(nameof(apparelCommands));
        this.apparelWorkOrders = apparelWorkOrders
            ?? throw new ArgumentNullException(nameof(apparelWorkOrders));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.apparelConditions = apparelConditions
            ?? throw new ArgumentNullException(nameof(apparelConditions));
        this.domainFacts = domainFacts
            ?? throw new ArgumentNullException(nameof(domainFacts));
    }

    public void Tick()
    {
        AdvanceExposureOutcomes();
        if (clock.IsPaused || !field.IsInitialized)
        {
            return;
        }

        stateStore.Current.Accumulator += Mathf.Max(0f, clock.DeltaTime);
        // The aggregate compares the actual item/apparel revisions and an
        // ordered candidate fingerprint before it evaluates a change. This
        // polling edge therefore cannot repeat the same-purpose change per tick.
        apparelCommands.RefreshAutomaticSelections();
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

    public bool TryGetLightAdaptation(
        CharacterId characterId,
        out CharacterLightAdaptationSnapshot snapshot)
    {
        snapshot = default;
        CharacterActor actor = FindCharacter(characterId);
        if (actor == null
            || !field.TryGetCell(
                actor.GetNowXY(),
                out EnvironmentalCellSnapshot environment))
        {
            return false;
        }

        snapshot = ToLightAdaptationSnapshot(
            ResolveLightAdaptation(actor, environment.LightLevel));
        return true;
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
        float physiological = DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)GetPhysiologicalBand(
                    characterId),
                DungeonStory.Environment.EnvironmentalWorkKind.General);
        float light = TryGetLightAdaptation(
                characterId,
                out CharacterLightAdaptationSnapshot adaptation)
            ? adaptation.WorkSpeedMultiplier
            : 1f;
        return DungeonStory.Environment.CharacterEnvironmentRules
            .ResolveLightAdaptedWorkSpeed(
                physiological,
                physiological,
                light,
                precision: false);
    }

    public float GetPrecisionWorkSpeedMultiplier(CharacterId characterId)
    {
        EnvironmentalExposureBand physiologicalBand =
            GetPhysiologicalBand(characterId);
        EnvironmentalExposureBand combinedBand =
            (EnvironmentalExposureBand)Mathf.Max(
                (int)physiologicalBand,
                (int)GetVisualBand(characterId));
        float physiological = DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)physiologicalBand,
                DungeonStory.Environment.EnvironmentalWorkKind.Precision);
        float combined = DungeonStory.Environment.EnvironmentWorkRules
            .ResolveLegacyWorkSpeed(
                (DungeonStory.Environment.ExposureBand)combinedBand,
                DungeonStory.Environment.EnvironmentalWorkKind.Precision);
        float light = TryGetLightAdaptation(
                characterId,
                out CharacterLightAdaptationSnapshot adaptation)
            ? adaptation.WorkSpeedMultiplier
            : 1f;
        return DungeonStory.Environment.CharacterEnvironmentRules
            .ResolveLightAdaptedWorkSpeed(
                physiological,
                combined,
                light,
                precision: true);
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

    public bool AddHeatExposure(CharacterId characterId, float amount)
    {
        if (!characterId.IsValid
            || float.IsNaN(amount)
            || float.IsInfinity(amount)
            || amount <= 0f)
            return false;
        CharacterActor actor = (characters.Characters
                ?? Array.Empty<CharacterActor>())
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.Identity?.PersistentId,
                    characterId.Value,
                    StringComparison.Ordinal));
        if (actor == null)
            return false;

        ThermalProtectionProfile resolved = protection.Resolve(actor);
        float multiplier = resolved != null
            ? Mathf.Clamp(resolved.heatExposureMultiplier, 0.05f, 2f)
            : 1f;
        CharacterEnvironmentExposure state = GetOrCreate(characterId);
        state.heatExposure = Mathf.Clamp(
            state.heatExposure + amount * multiplier,
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
        apparelCommands.TrySetSelectionPurpose(
            characterId,
            ApparelSelectionPurpose.Work,
            out _);
    }

    public void ClearWorkContext(CharacterId characterId)
    {
        // Work contexts are transient. After restore, retiring actors may still
        // cancel their old routines against the newly published apparel owner.
        // No owned context means no authority to reset its saved/manual policy.
        if (!workContexts.Remove(characterId))
            return;
        apparelCommands.TrySetSelectionPurpose(
            characterId,
            ApparelSelectionPurpose.Daily,
            out _);
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
            equippedWorkwear = workwear.CaptureEquipped()
                .Where(value => persistentCharacterIds.Contains(
                    new CharacterId(value.characterId)))
                .ToArray(),
            equippedApparel = apparel.CaptureApparel()
                .Where(value => persistentCharacterIds.Contains(
                    new CharacterId(value.characterId)))
                .ToArray(),
            apparelPolicies = apparel.CaptureApparelPolicies()
                .Where(value => persistentCharacterIds.Contains(
                    new CharacterId(value.characterId)))
                .ToArray(),
            apparelWorkOrders = apparelWorkOrders.CaptureOrders(),
            apparelWorkOrderTerminalStates =
                apparelWorkOrders.CaptureTerminalStates(),
            nextExposureOutcomeSequence =
                stateStore.Current.NextExposureOutcomeSequence,
            exposureOutcomes = exposureOutcomes.Values
                .Where(value => value != null
                    && persistentCharacterIds.Contains(
                        new CharacterId(value.characterId)))
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(CloneExposureOutcome)
                .ToArray()
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
            WorkwearVersion = stateStore.Current.WorkwearVersion + 1,
            NextExposureOutcomeSequence = source.nextExposureOutcomeSequence
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
        foreach (EnvironmentalExposureOutcomeSaveData pending
                 in source.exposureOutcomes)
        {
            CharacterId characterId = new(pending.characterId);
            if (!availableCharacters.Contains(characterId))
            {
                throw new InvalidOperationException(
                    $"Environment exposure outcome references unknown character '{pending.characterId}'.");
            }
            if (!restored.ExposureOutcomes.TryAdd(
                    pending.operationId,
                    CloneExposureOutcome(pending)))
            {
                throw new InvalidOperationException(
                    $"Duplicate environment exposure outcome '{pending.operationId}'.");
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
            apparel.PrepareRestoreApparel(
                source.equippedApparel,
                source.apparelPolicies,
                report);
        workwear.ValidatePreparedProjection(
            preparedWorkwear,
            preparedApparel,
            report);
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
        if (!apparelConditions.TryAdvance(
                deltaTime,
                workContexts,
                out DomainFailure apparelFailure))
        {
            throw new InvalidOperationException(
                $"Apparel condition step failed: {apparelFailure}");
        }
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
#if UNITY_EDITOR
            if (debugIsolatedPreparationExposureIds?.Contains(characterId) == true)
            {
                debugHeldPreparationExposureSteps++;
                continue;
            }
#endif
            if (exposureOutcomes.Values.Any(value => value != null
                    && string.Equals(
                        value.characterId,
                        characterId.Value,
                        StringComparison.Ordinal)))
            {
                continue;
            }
            CharacterEnvironmentExposure state =
                GetOrCreate(characterId);
            float coldBefore = state.coldExposure;
            float heatBefore = state.heatExposure;
            float airborneBefore = state.airborneExposure;
            EnvironmentalExposureBand bandBefore = state.physiologicalBand;
            float criticalTimerBefore = state.criticalDamageTimer;
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
            DungeonStory.Environment.LightAdaptationProjection
                lightAdaptation = ResolveLightAdaptation(
                    actor,
                    environment.LightLevel);
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
                && !lightAdaptation.Enabled
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
                        lightAdaptation.Enabled
                            || !precisionContext
                            || environment.LightLevel
                                >= EnvironmentalThresholdRules.PrecisionMinimumLight,
                        deltaTime));
            EnvironmentalExposureBand bandAfter =
                (EnvironmentalExposureBand)step.PhysiologicalBand;
            bool periodicDamage = bandAfter >= EnvironmentalExposureBand.Critical
                && criticalTimerBefore + deltaTime >= 10f;
            bool lethalDamage = lethalTemperature
                || environment.AirQuality
                    < EnvironmentalThresholdRules.ToxicAirQuality;
            CharacterVitalsSnapshot vitalsBefore = bodyHealth.GetVitals(actor);
            float healthBefore = vitalsBefore.CurrentHealth;
            float plannedDamage = (periodicDamage
                    ? vitalsBefore.MaximumHealth * 0.01f
                    : 0f)
                + (lethalDamage
                    ? vitalsBefore.MaximumHealth * 0.01f * deltaTime
                    : 0f);
            var reservations = new List<ExposureOutcomeReservation>(2);
            int nextSequence = stateStore.Current.NextExposureOutcomeSequence;
            ExposureOutcomeReservation bandReservation = default;
            if (bandAfter != bandBefore
                && !TryReserveExposureOutcome(
                    actor,
                    characterId,
                    environment,
                    coldBefore,
                    step.ColdExposure,
                    heatBefore,
                    step.HeatExposure,
                    airborneBefore,
                    step.AirborneExposure,
                    bandBefore,
                    bandAfter,
                    0f,
                    healthBefore,
                    healthBefore,
                    "band-transition",
                    "physiological-band-transition",
                    nextSequence++,
                    out bandReservation))
            {
                continue;
            }
            else if (bandAfter != bandBefore)
            {
                reservations.Add(bandReservation);
            }
            ExposureOutcomeReservation damageReservation = default;
            if (plannedDamage > 0f
                && !TryReserveExposureOutcome(
                    actor,
                    characterId,
                    environment,
                    coldBefore,
                    step.ColdExposure,
                    heatBefore,
                    step.HeatExposure,
                    airborneBefore,
                    step.AirborneExposure,
                    bandBefore,
                    bandAfter,
                    plannedDamage,
                    healthBefore,
                    healthBefore,
                    "damage-tick",
                    lethalDamage && periodicDamage
                        ? "critical-and-lethal-environment"
                        : lethalDamage
                            ? "lethal-environment"
                            : "critical-environment",
                    nextSequence++,
                    out damageReservation))
            {
                foreach (ExposureOutcomeReservation reservation in reservations)
                    domainFacts.Cancel(reservation.Reserved);
                continue;
            }
            else if (plannedDamage > 0f)
            {
                reservations.Add(damageReservation);
            }
            state.coldExposure = step.ColdExposure;
            state.heatExposure = step.HeatExposure;
            state.airborneExposure = step.AirborneExposure;
            state.visualStrain = step.VisualStrain;
            state.physiologicalBand =
                bandAfter;
            state.visualBand = (EnvironmentalExposureBand)step.VisualBand;
            ApplyLightAdaptationMood(actor, lightAdaptation);
            ApplyBandEffects(actor, state, bandBefore);
            if (bandAfter >= EnvironmentalExposureBand.Critical)
            {
                state.criticalDamageTimer = criticalTimerBefore + deltaTime;
                if (periodicDamage)
                    state.criticalDamageTimer -= 10f;
            }
            else
                state.criticalDamageTimer = 0f;
            if (plannedDamage > 0f)
            {
                bodyHealthCommands.ApplyLegacyDamage(
                    actor,
                    plannedDamage,
                    lethalDamage
                        ? "치명적 환경 노출"
                        : "환경 위급 노출",
                    allowDeath: lethalDamage);
            }
            float healthAfter = bodyHealth.GetVitals(actor).CurrentHealth;
            stateStore.Current.NextExposureOutcomeSequence = nextSequence;
            foreach (ExposureOutcomeReservation reservation in reservations)
            {
                EnvironmentalExposureOutcomeSaveData pending =
                    reservation.Pending;
                if (string.Equals(
                        pending.eventKind,
                        "damage-tick",
                        StringComparison.Ordinal))
                {
                    pending.damage = Mathf.Max(0f, healthBefore - healthAfter);
                    pending.healthBefore = healthBefore;
                    pending.healthAfter = healthAfter;
                    if (pending.damage <= 0f)
                    {
                        domainFacts.Cancel(reservation.Reserved);
                        continue;
                    }
                }
                TryCommitExposureOutcome(reservation.Reserved, pending);
            }
        }

        foreach (CharacterId staleId in states.Keys
                     .Where(id => !activeIds.Contains(id))
                     .ToArray())
        {
            workContexts.Remove(staleId);
        }
    }

    private readonly struct ExposureOutcomeReservation
    {
        public ExposureOutcomeReservation(
            DomainFactLease reserved,
            EnvironmentalExposureOutcomeSaveData pending)
        {
            Reserved = reserved;
            Pending = pending ?? throw new ArgumentNullException(nameof(pending));
        }

        public DomainFactLease Reserved { get; }
        public EnvironmentalExposureOutcomeSaveData Pending { get; }
    }

    private bool TryReserveExposureOutcome(
        CharacterActor actor,
        CharacterId characterId,
        EnvironmentalCellSnapshot environment,
        float coldBefore,
        float coldAfter,
        float heatBefore,
        float heatAfter,
        float airborneBefore,
        float airborneAfter,
        EnvironmentalExposureBand bandBefore,
        EnvironmentalExposureBand bandAfter,
        float damage,
        float healthBefore,
        float healthAfter,
        string eventKind,
        string reasonCode,
        int sequence,
        out ExposureOutcomeReservation reservation)
    {
        reservation = default;
        if (actor == null
            || !characterId.IsValid
            || sequence < 0
            || (eventKind != "band-transition" && eventKind != "damage-tick")
            || string.IsNullOrWhiteSpace(reasonCode))
        {
            return false;
        }

        string displayName = actor.Identity?.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = actor.name?.Trim();
        if (string.IsNullOrEmpty(displayName))
            return false;

        string operationId = "environment-exposure:" + characterId.Value + ":"
            + sequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)
            + ":" + eventKind;
        long ownerRevision = checked((long)sequence + 1L);
        var pending = new EnvironmentalExposureOutcomeSaveData
        {
            operationId = operationId,
            eventKind = eventKind,
            characterId = characterId.Value,
            characterDisplayName = displayName,
            x = environment.Position.x,
            y = environment.Position.y,
            absoluteDay = Mathf.Max(0, calendar.Day),
            ownerRevision = ownerRevision,
            temperatureC = environment.TemperatureC,
            airQuality = environment.AirQuality,
            lightLevel = environment.LightLevel,
            coldBefore = coldBefore,
            coldAfter = coldAfter,
            heatBefore = heatBefore,
            heatAfter = heatAfter,
            airborneBefore = airborneBefore,
            airborneAfter = airborneAfter,
            bandBefore = bandBefore,
            bandAfter = bandAfter,
            damage = Mathf.Max(0f, damage),
            healthBefore = Mathf.Max(0f, healthBefore),
            healthAfter = Mathf.Max(0f, healthAfter),
            reasonCode = reasonCode.Trim()
        };
        if (!domainFacts.TryReserve(
                new CharacterEnvironmentExposureFact(pending),
                out DomainFactLease reserved,
                out _))
        {
            return false;
        }
        reservation = new ExposureOutcomeReservation(reserved, pending);
        return true;
    }

    private void TryCommitExposureOutcome(
        in DomainFactLease reserved,
        EnvironmentalExposureOutcomeSaveData pending)
    {
        exposureOutcomes[pending.operationId] = CloneExposureOutcome(pending);
        DomainFactDelivery committed = domainFacts.Publish(
            reserved, new CharacterEnvironmentExposureFact(pending));
        if (committed.Completed)
            exposureOutcomes.Remove(pending.operationId);
    }

    private void AdvanceExposureOutcomes()
    {
        foreach (KeyValuePair<string, EnvironmentalExposureOutcomeSaveData> pair
                 in exposureOutcomes.ToArray())
            TryAdvanceExposureOutcome(pair.Key, pair.Value);
    }

    private bool TryAdvanceExposureOutcome(
        string operationId,
        EnvironmentalExposureOutcomeSaveData pending)
    {
        DomainFactDelivery existing = domainFacts.Replay(new CharacterEnvironmentExposureFact(pending));
        if (!existing.Completed)
            return false;
        exposureOutcomes.Remove(operationId);
        return true;
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
        EnvironmentalExposureBand previous)
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

    private CharacterActor FindCharacter(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            return null;
        }

        IReadOnlyList<CharacterActor> actors =
            characters.Characters ?? Array.Empty<CharacterActor>();
        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor != null
                && string.Equals(
                    actor.Identity?.PersistentId,
                    characterId.Value,
                    StringComparison.Ordinal))
            {
                return actor;
            }
        }
        return null;
    }

    private DungeonStory.Environment.LightAdaptationProjection
        ResolveLightAdaptation(CharacterActor actor, float actualLight)
    {
        SpeciesLightAdaptationProfile profile = speciesEnvironment
            .GetRequiredLightAdaptationProfile(
                new CharacterSpeciesId(actor?.SpeciesTag));
        return DungeonStory.Environment.CharacterEnvironmentRules
            .ResolveLightAdaptation(
                profile.Enabled,
                actualLight,
                profile.ComfortableMinimum,
                profile.ComfortableMaximum,
                profile.Sensitivity);
    }

    private static CharacterLightAdaptationSnapshot ToLightAdaptationSnapshot(
        DungeonStory.Environment.LightAdaptationProjection projection)
    {
        return new CharacterLightAdaptationSnapshot(
            projection.Enabled,
            projection.ActualLight,
            projection.ComfortableMinimum,
            projection.ComfortableMaximum,
            projection.Sensitivity,
            projection.Discomfort,
            projection.MoodContribution,
            projection.WorkSpeedContribution);
    }

    private static void ApplyLightAdaptationMood(
        CharacterActor actor,
        DungeonStory.Environment.LightAdaptationProjection adaptation)
    {
        if (actor?.Stats == null)
        {
            return;
        }
        if (!adaptation.Enabled
            || Mathf.Approximately(adaptation.MoodContribution, 0f))
        {
            actor.Stats.RemoveMoodFactor(LightAdaptationMoodFactorId);
            return;
        }

        CharacterMoodFactorSnapshot current = actor.Mood.Factors
            .FirstOrDefault(factor => factor != null
                && string.Equals(
                    factor.Id,
                    LightAdaptationMoodFactorId,
                    StringComparison.Ordinal));
        if (current != null
            && Mathf.Approximately(
                current.Value,
                adaptation.MoodContribution)
            && current.RemainingSeconds > TickInterval * 2f)
        {
            return;
        }

        actor.ApplyMoodFactor(
            LightAdaptationMoodFactorId,
            "밝기 적응 불편",
            adaptation.MoodContribution,
            EnvironmentMoodFactorDurationSeconds,
            1);
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

    private static EnvironmentalExposureOutcomeSaveData CloneExposureOutcome(
        EnvironmentalExposureOutcomeSaveData source) => new()
    {
        schemaVersion = source.schemaVersion,
        operationId = source.operationId,
        eventKind = source.eventKind,
        characterId = source.characterId,
        characterDisplayName = source.characterDisplayName,
        x = source.x,
        y = source.y,
        absoluteDay = source.absoluteDay,
        ownerRevision = source.ownerRevision,
        temperatureC = source.temperatureC,
        airQuality = source.airQuality,
        lightLevel = source.lightLevel,
        coldBefore = source.coldBefore,
        coldAfter = source.coldAfter,
        heatBefore = source.heatBefore,
        heatAfter = source.heatAfter,
        airborneBefore = source.airborneBefore,
        airborneAfter = source.airborneAfter,
        bandBefore = source.bandBefore,
        bandAfter = source.bandAfter,
        damage = source.damage,
        healthBefore = source.healthBefore,
        healthAfter = source.healthAfter,
        reasonCode = source.reasonCode
    };

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
