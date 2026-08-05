using System;
using System.Collections.Generic;
using System.Linq;
using Stopwatch = System.Diagnostics.Stopwatch;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using EnvironmentalFieldRestoreCandidate =
    DungeonStory.Environment.EnvironmentalFieldRestoreCandidate;
using EnvironmentalFieldAggregateStateStore =
    DungeonStory.Environment.EnvironmentalFieldAggregateStateStore;

public static class EnvironmentalFieldDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run Environmental Field Scenarios")]
    public static void RunAll()
    {
        List<string> failures = new List<string>();
        Verify("temperature source and exhaust", VerifyThermalSource, failures);
        Verify(
            "configurable thermostat save round trip",
            VerifyThermostatRoundTrip,
            failures);
        Verify("sparse save round trip", VerifySaveRoundTrip, failures);
        Verify(
            "narrow facets and atomic field restore",
            VerifyNarrowFacetsAndAtomicRestore,
            failures);
        Verify("preservation thresholds", VerifyPreservationRules, failures);
        Verify(
            "physical workwear authority",
            VerifyPhysicalWorkwearAuthority,
            failures);
        Verify(
            "strict typed character-environment save",
            VerifyStrictCharacterEnvironmentSave,
            failures);
        Verify("Slime cold-work balance", VerifySlimeColdWork, failures);
        Verify(
            "10000 cells and 500 exposure evaluations",
            VerifyPerformanceEnvelope,
            failures);
        if (!PlayerFairnessDebugScenarios.RunAll(logSuccess: false))
        {
            failures.Add("player fairness contracts failed");
        }
        if (failures.Count > 0)
        {
            string message =
                $"EnvironmentalFieldDebugScenarios failed:\n{string.Join("\n", failures)}";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        Debug.Log("EnvironmentalFieldDebugScenarios passed.");
    }

    private static bool VerifyThermalSource()
    {
        TestWorld world = new TestWorld(12, 3, 20f);
        try
        {
            BuildableObject cooler = world.CreateBuilding(
                "cooler",
                new Vector2Int(5, 1),
                new BuildingThermalEmitterAbility
                {
                    mode = ThermalEmitterMode.Cool,
                    targetTemperatureC = 8f,
                    degreesPerSecond = 4f,
                    radius = 2,
                    requiresPower = false,
                    exhaustOffset = Vector2Int.right,
                    exhaustHeatMultiplier = 1.15f
                });
            world.Add(cooler);
            world.Advance(6);
            Require(
                world.Runtime.TryGetCell(
                    new Vector2Int(5, 1),
                    out EnvironmentalCellSnapshot center),
                "center cell was unavailable");
            Require(center.TemperatureC < 12f,
                $"cooler did not reach the cold range: {center.TemperatureC:0.##}");
            Require(
                world.Runtime.TryGetCell(
                    new Vector2Int(6, 1),
                    out EnvironmentalCellSnapshot exhaust),
                "exhaust cell was unavailable");
            Require(exhaust.TemperatureC > center.TemperatureC,
                "cooler exhaust was not hotter than its intake");
            return true;
        }
        finally
        {
            world.Dispose();
        }
    }

    private static bool VerifySaveRoundTrip()
    {
        TestWorld source = new TestWorld(6, 2, 20f);
        TestWorld target = new TestWorld(6, 2, 20f);
        try
        {
            source.Add(source.CreateBuilding(
                "heater",
                new Vector2Int(2, 0),
                new BuildingThermalEmitterAbility
                {
                    mode = ThermalEmitterMode.Heat,
                    targetTemperatureC = 32f,
                    degreesPerSecond = 3f,
                    radius = 1
                }));
            source.Advance(4);
            DungeonEnvironmentalFieldSaveData save = source.Runtime.Capture();
            target.Runtime.Restore(target.Runtime.PrepareRestore(save));
            Require(source.Runtime.TryGetCell(
                    new Vector2Int(2, 0),
                    out EnvironmentalCellSnapshot before),
                "source cell was unavailable");
            Require(target.Runtime.TryGetCell(
                    new Vector2Int(2, 0),
                    out EnvironmentalCellSnapshot after),
                "restored cell was unavailable");
            Require(Mathf.Abs(before.TemperatureC - after.TemperatureC) < 0.01f,
                "temperature did not survive round trip");
            return true;
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    private static bool VerifyPhysicalWorkwearAuthority()
    {
        Require(
            typeof(EnvironmentalWorkwearRuntime).Assembly.GetType(
                "IEnvironmentalWorkwear" + "Runtime",
                throwOnError: false) == null,
            "broad environmental workwear runtime wrapper returned");
        Type[] exposedFacets = typeof(EnvironmentalWorkwearRuntime)
            .GetInterfaces();
        Require(
            exposedFacets.Contains(typeof(IEnvironmentalWorkwearQuery))
            && exposedFacets.Contains(typeof(IEnvironmentalWorkwearCommand))
            && exposedFacets.Contains(typeof(IEnvironmentalWorkwearPersistence)),
            "workwear runtime does not expose query/command/persistence facets");
        Type[] characterEnvironmentDependencies =
            typeof(CharacterEnvironmentUnityAdapter)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
        Require(
            characterEnvironmentDependencies.Contains(
                typeof(IEnvironmentalWorkwearPersistence))
            && !characterEnvironmentDependencies.Contains(
                typeof(EnvironmentalWorkwearRuntime)),
            "character environment restore bypasses the workwear persistence facet");
        Type[] workExecutorDependencies = typeof(WorkTaskExecutor)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Type[] workEnvironmentDependencies =
            typeof(WorkTaskEnvironmentDependencies)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
        Require(
            workExecutorDependencies.Contains(
                typeof(WorkTaskEnvironmentDependencies))
            && workEnvironmentDependencies.Contains(
                typeof(IEnvironmentalWorkwearCommand))
            && !workEnvironmentDependencies.Contains(
                typeof(IEnvironmentalWorkwearQuery)),
            "work executor must use only the workwear command facet");
        Require(
            typeof(DungeonCharacterEnvironmentSaveData)
                .GetField("workwearStock") == null,
            "environment save still owns a parallel workwear quantity");
        Require(
            typeof(EnvironmentalWorkwearSaveData)
                .GetField("itemInstanceId") != null
            && typeof(EnvironmentalWorkwearSaveData)
                .GetField("workwearId") == null,
            "equipped workwear save does not reference only ItemInstanceId");

        string[] guids = AssetDatabase.FindAssets(
            "t:EnvironmentalWorkwearSO",
            new[] { "Assets/Resources/SO/Environment/Workwear" });
        Require(guids.Length == 3,
            $"expected 3 authored workwear definitions, found {guids.Length}");
        foreach (string guid in guids)
        {
            EnvironmentalWorkwearSO workwear =
                AssetDatabase.LoadAssetAtPath<EnvironmentalWorkwearSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
            Require(workwear != null
                    && !string.IsNullOrWhiteSpace(workwear.ItemDefinitionId),
                "workwear has no physical item definition ID");

            string itemGuid = AssetDatabase.FindAssets(
                    $"t:ResourceItemDefinitionSO {workwear.ItemDefinitionId}",
                    new[] { "Assets/Resources/SO/Economy/Items" })
                .FirstOrDefault();
            ResourceItemDefinitionSO item = string.IsNullOrWhiteSpace(itemGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                    AssetDatabase.GUIDToAssetPath(itemGuid));
            if (item == null)
            {
                item = AssetDatabase.FindAssets(
                        "t:ResourceItemDefinitionSO",
                        new[] { "Assets/Resources/SO/Economy/Items" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
                    .FirstOrDefault(candidate => candidate != null
                        && string.Equals(
                            candidate.ItemId,
                            workwear.ItemDefinitionId,
                            StringComparison.Ordinal));
            }

            Require(item != null && item.MaxStack == 1,
                $"physical workwear '{workwear.ItemDefinitionId}' is not unique");
        }

        Type[] runtimeDependencies = typeof(EnvironmentalWorkwearRuntime)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Require(runtimeDependencies.Contains(typeof(IWorldItemStackRuntime))
                && runtimeDependencies.Contains(typeof(IStockQuery)),
            "workwear runtime is not backed by physical stack/query services");
        Type[] outputDependencies =
            typeof(EnvironmentalWorkwearProductionOutputHandler)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
        Require(outputDependencies.Contains(typeof(IWorldItemStackRuntime)),
            "workwear production does not create physical item stacks");
        return true;
    }

    public static bool VerifyStrictCharacterEnvironmentSave()
    {
        Require(
            typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(
                typeof(CharacterEnvironmentSaveSection))
            && !typeof(IOptionalDungeonSaveSection).IsAssignableFrom(
                typeof(CharacterEnvironmentSaveSection)),
            "character environment is not a required rollback-free section");

        Type aggregateType = typeof(CharacterEnvironmentAggregateStateStore)
            .Assembly.GetType("CharacterEnvironmentAggregateState", true);
        Type keyType = aggregateType
            .GetProperty(
                "Exposures",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            ?.PropertyType
            .GetGenericArguments()[0];
        Require(keyType == typeof(CharacterId),
            "character environment aggregate is not keyed by CharacterId");

        RecordingCharacterEnvironmentRuntime runtime = new();
        CharacterEnvironmentSaveSection section = new(runtime);
        DungeonCharacterEnvironmentSaveData invalid = new()
        {
            version = DungeonCharacterEnvironmentSaveData.CurrentVersion,
            exposures = new[]
            {
                new CharacterEnvironmentExposure
                {
                    characterId = "character:invalid",
                    coldExposure = 101f
                }
            },
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>()
        };
        RequireRejectedWithoutPublish(
            section,
            runtime,
            invalid,
            "invalid environment payload was accepted or published");

        DungeonCharacterEnvironmentSaveData missingCollections = new()
        {
            exposures = null,
            equippedWorkwear = null
        };
        RequireRejectedWithoutPublish(
            section,
            runtime,
            missingCollections,
            "missing environment collections were defaulted or published",
            $"{{\"version\":{DungeonCharacterEnvironmentSaveData.CurrentVersion}}}");

        DungeonCharacterEnvironmentSaveData valid = new()
        {
            version = DungeonCharacterEnvironmentSaveData.CurrentVersion,
            exposures = Array.Empty<CharacterEnvironmentExposure>(),
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>()
        };
        DungeonGameRestoreReport validValidation = new();
        CharacterEnvironmentSaveValidation.Validate(valid, validValidation);
        Require(validValidation.Success && runtime.RestoreCount == 0,
            "valid empty environment arrays failed preflight or published state");
        DungeonGameRestoreReport validReport = new();
        IDungeonSaveRestoreStage validStage = section.StageRestore(
            JsonUtility.ToJson(valid),
            section.SectionVersion,
            validReport);
        Require(validReport.Success && runtime.RestoreCount == 0,
            "environment candidate staging mutated runtime state");
        validStage.Commit(validReport);
        Require(validReport.Success && runtime.RestoreCount == 1,
            "valid environment payload was not restored exactly once");

        bool legacySectionRejected = false;
        try
        {
            section.StageRestore(
                JsonUtility.ToJson(valid),
                section.SectionVersion - 1,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            legacySectionRejected = true;
        }
        Require(legacySectionRejected && runtime.RestoreCount == 1,
            "legacy environment section version was accepted or mutated state");
        return true;
    }

    private static void RequireRejectedWithoutPublish(
        CharacterEnvironmentSaveSection section,
        RecordingCharacterEnvironmentRuntime runtime,
        DungeonCharacterEnvironmentSaveData payload,
        string failureMessage,
        string payloadJson = null)
    {
        int publishCountBefore = runtime.RestoreCount;
        DungeonGameRestoreReport validationReport = new();
        CharacterEnvironmentSaveValidation.Validate(payload, validationReport);
        Require(!validationReport.Success,
            $"{failureMessage}: public validation unexpectedly succeeded");

        bool rejected = false;
        try
        {
            section.StageRestore(
                payloadJson ?? JsonUtility.ToJson(payload),
                section.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected && runtime.RestoreCount == publishCountBefore,
            failureMessage);
    }

    private static bool VerifyThermostatRoundTrip()
    {
        TestWorld source = new TestWorld(6, 2, 20f);
        TestWorld target = new TestWorld(6, 2, 20f);
        Vector2Int position = new Vector2Int(2, 0);
        try
        {
            BuildingThermalEmitterAbility sourceEmitter =
                CreateConfigurableThermostat();
            BuildingThermalEmitterAbility targetEmitter =
                CreateConfigurableThermostat();
            source.Add(source.CreateBuilding(
                "hvac",
                position,
                sourceEmitter));
            target.Add(target.CreateBuilding(
                "hvac",
                position,
                targetEmitter));
            source.Advance(1);
            target.Advance(1);
            Require(
                source.Runtime.TrySetTargetTemperature(
                    position,
                    6f,
                    out DomainFailure failure),
                $"target setting failed: {failure.Code}");
            DungeonEnvironmentalFieldSaveData save =
                source.Runtime.Capture();
            Require(
                save.thermostats.Count == 1
                && string.Equals(
                    save.thermostats[0].buildingInstanceId,
                    "building:test:environment:hvac:2:0",
                    StringComparison.Ordinal),
                "thermostat save did not use its canonical BuildingInstanceId");
            target.Runtime.Restore(target.Runtime.PrepareRestore(save));
            Require(
                target.Runtime.TryGetTargetTemperature(
                    position,
                    out float restored)
                && Mathf.Approximately(restored, 6f),
                "configured target did not survive round trip");
            Require(
                target.Runtime.TrySetTargetTemperature(
                    position,
                    -20f,
                    out _)
                && target.Runtime.TryGetTargetTemperature(
                    position,
                    out float clamped)
                && Mathf.Approximately(clamped, 2f),
                "thermostat minimum was not enforced");
            Require(
                !target.Runtime.TrySetTargetTemperature(
                    new Vector2Int(5, 1),
                    20f,
                    out DomainFailure unsupported)
                && unsupported.Code
                    == FailureCode.EnvironmentThermostatUnsupported,
                "unsupported thermostat did not return its typed failure");
            return true;
        }
        finally
        {
            source.Dispose();
            target.Dispose();
        }
    }

    private static BuildingThermalEmitterAbility
        CreateConfigurableThermostat()
    {
        return new BuildingThermalEmitterAbility
        {
            mode = ThermalEmitterMode.Thermostat,
            targetTemperatureC = 22f,
            playerConfigurable = true,
            minimumTargetTemperatureC = 2f,
            maximumTargetTemperatureC = 30f,
            degreesPerSecond = 2f,
            radius = 2
        };
    }

    private static bool VerifyPreservationRules()
    {
        Require(Mathf.Approximately(
                EnvironmentalThresholdRules.GetFoodSpoilageMultiplier(20f),
                1f),
            "20C spoilage baseline changed");
        Require(Mathf.Approximately(
                EnvironmentalThresholdRules.GetFoodSpoilageMultiplier(30f),
                2f),
            "10C spoilage doubling changed");
        Require(
            EnvironmentalThresholdRules.IsOrganPreservationSafe(2f)
            && EnvironmentalThresholdRules.IsOrganPreservationSafe(8f)
            && !EnvironmentalThresholdRules.IsOrganPreservationSafe(8.1f),
            "organ preservation band changed");
        return true;
    }

    private static bool VerifyNarrowFacetsAndAtomicRestore()
    {
        Require(
            typeof(EnvironmentalFieldRuntimeApplicationAdapter).Assembly.GetType(
                "IEnvironmentalField" + "Runtime",
                throwOnError: false) == null,
            "broad environmental-field runtime wrapper returned");
        Type[] facets = typeof(EnvironmentalFieldRuntimeApplicationAdapter)
            .GetInterfaces();
        Require(
            facets.Contains(typeof(IEnvironmentalFieldQuery))
            && facets.Contains(typeof(IEnvironmentalFieldCommand))
            && facets.Contains(typeof(IEnvironmentalFieldPersistence)),
            "environmental field does not expose query/command/persistence facets");
        Require(
            typeof(IEnvironmentalFieldPersistence)
                .GetMethod(nameof(IEnvironmentalFieldPersistence.PrepareRestore))
                ?.ReturnType
                == typeof(EnvironmentalFieldRestoreCandidate)
            && typeof(EnvironmentalFieldRestoreCandidate).Assembly
                == typeof(DungeonStory.Environment.EnvironmentalFieldRestoreRules)
                    .Assembly,
            "environmental restore candidate is not owned by the Environment domain");

        Type[] saveDependencies = typeof(EnvironmentalFieldSaveSection)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Require(
            saveDependencies.SequenceEqual(
                new[] { typeof(IEnvironmentalFieldPersistence) }),
            "environmental-field save bypasses its persistence facet");
        Require(
            typeof(IDungeonSaveSectionPreflight).IsAssignableFrom(
                typeof(EnvironmentalFieldSaveSection))
            && typeof(IDungeonRollbackFreeSaveSection).IsAssignableFrom(
                typeof(EnvironmentalFieldSaveSection))
            && !typeof(IOptionalDungeonSaveSection).IsAssignableFrom(
                typeof(EnvironmentalFieldSaveSection)),
            "environmental-field save is not required, preflighted, and rollback-free");
        Require(
            DungeonEnvironmentalFieldSaveData.CurrentVersion == 2
            && typeof(EnvironmentalThermostatSaveData)
                .GetField("buildingInstanceId") != null
            && typeof(EnvironmentalThermostatSaveData).GetField("x") == null
            && typeof(EnvironmentalThermostatSaveData).GetField("y") == null,
            "environmental thermostat persistence is not canonical BuildingInstanceId V2");

        TestWorld world = new(4, 2, 20f);
        try
        {
            world.Advance(1);
            EnvironmentalCellSnapshot before;
            Require(
                world.Runtime.TryGetCell(new Vector2Int(0, 0), out before),
                "atomic-restore baseline cell was unavailable");
            DungeonEnvironmentalFieldSaveData invalid = world.Runtime.Capture();
            invalid.cells.Add(new EnvironmentalCellSaveData
            {
                x = 99,
                y = 99,
                temperatureC = 5f,
                airQuality = 100f,
                lightLevel = 50f
            });
            bool rejected = false;
            try
            {
                world.Runtime.PrepareRestore(invalid);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected, "invalid field candidate was accepted");
            Require(
                world.Runtime.TryGetCell(
                    new Vector2Int(0, 0),
                    out EnvironmentalCellSnapshot after)
                && Mathf.Approximately(
                    before.TemperatureC,
                    after.TemperatureC),
                "failed field preparation mutated live state");

            DungeonEnvironmentalFieldSaveData valid = world.Runtime.Capture();
            valid.cells.Add(new EnvironmentalCellSaveData
            {
                x = 0,
                y = 0,
                temperatureC = 5f,
                airQuality = 90f,
                lightLevel = 40f
            });
            EnvironmentalFieldRestoreCandidate candidate =
                world.Runtime.PrepareRestore(valid);
            Require(
                candidate.Width == valid.width
                && candidate.Height == valid.height
                && candidate.Cells.Any(cell =>
                    cell.Address.X == 0
                    && cell.Address.Y == 0
                    && Mathf.Approximately(cell.TemperatureC, 5f)),
                "named Environment candidate did not retain staged field data");
            int versionBeforeStage = world.Runtime.Version;
            EnvironmentalFieldSaveSection section = new(world.Runtime);
            DungeonGameRestoreReport restoreReport = new();
            IDungeonSaveRestoreStage stage = section.StageRestore(
                JsonUtility.ToJson(valid),
                section.SectionVersion,
                restoreReport);
            Require(
                restoreReport.Success
                && world.Runtime.Version == versionBeforeStage
                && world.Runtime.TryGetCell(
                    new Vector2Int(0, 0),
                    out EnvironmentalCellSnapshot stagedBaseline)
                && Mathf.Approximately(
                    before.TemperatureC,
                    stagedBaseline.TemperatureC),
                "valid field preparation mutated live state before publication");
            stage.Commit(restoreReport);
            Require(
                restoreReport.Success
                && world.Runtime.Version == versionBeforeStage + 1
                && world.Runtime.TryGetCell(
                    new Vector2Int(0, 0),
                    out EnvironmentalCellSnapshot published)
                && Mathf.Approximately(published.TemperatureC, 5f)
                && Mathf.Approximately(published.AirQuality, 90f)
                && Mathf.Approximately(published.LightLevel, 40f),
                "prepared field Aggregate was not published by one replacement");
            return true;
        }
        finally
        {
            world.Dispose();
        }
    }

    private static bool VerifySlimeColdWork()
    {
        WorkEnvironmentAssessment typedFailure = new(
            false,
            false,
            0f,
            1f,
            new DomainFailure(
                FailureCode.EnvironmentWorkTargetUnavailable));
        Require(
            typedFailure.Failure.Code
                == FailureCode.EnvironmentWorkTargetUnavailable,
            "work assessment did not preserve its typed failure code");

        ThermalProtectionProfile none = ThermalProtectionProfile.None;
        SpeciesThermalProfile naked = CreateSlimeThermalProfile().Apply(none);
        CharacterEnvironmentUnityAdapter.CalculateTemperatureRates(
            8f,
            naked,
            none,
            out float shortCarryRate,
            out _,
            out _);
        Require(
            shortCarryRate * 30f < 25f,
            "naked Slime cannot complete a 30-second 8C carry");
        Require(
            shortCarryRate * 300f >= 25f,
            "naked Slime does not enter burden during continuous 8C work");

        ThermalProtectionProfile coldSuit = new ThermalProtectionProfile
        {
            comfortMinimumOffset = -8f,
            safeMinimumOffset = -8f,
            coldExposureMultiplier = 0.35f
        };
        CharacterEnvironmentUnityAdapter.CalculateTemperatureRates(
            8f,
            CreateSlimeThermalProfile().Apply(coldSuit),
            coldSuit,
            out float suitedRate,
            out _,
            out _);
        Require(
            Mathf.Approximately(suitedRate, 0f),
            "cold-work suit does not make 8C comfortable");

        ThermalProtectionProfile runeAndTrait = new ThermalProtectionProfile
        {
            comfortMinimumOffset = -14f,
            safeMinimumOffset = -12f,
            coldExposureMultiplier = 0.12f
        };
        SpeciesThermalProfile protectedProfile =
            CreateSlimeThermalProfile().Apply(runeAndTrait);
        CharacterEnvironmentUnityAdapter.CalculateTemperatureRates(
            2f,
            protectedProfile,
            runeAndTrait,
            out float runeRate,
            out _,
            out bool lethalAtTwo);
        Require(
            Mathf.Approximately(runeRate, 0f) && !lethalAtTwo,
            "rune suit and trait do not support long 2C work");
        CharacterEnvironmentUnityAdapter.CalculateTemperatureRates(
            0f,
            protectedProfile,
            runeAndTrait,
            out _,
            out _,
            out bool lethalAtZero);
        Require(
            lethalAtZero,
            "protection incorrectly moved the Slime lethal minimum");
        return true;
    }

    private static bool VerifyPerformanceEnvelope()
    {
        double p95 = MeasurePerformanceP95();
        Require(
            p95 <= 25d,
            $"environment p95 exceeded the 25ms fixed-tick envelope: "
            + $"{p95:0.###}ms");
        UnityEngine.Debug.Log(
            $"Environment 10,000-cell + 500-agent p95: {p95:0.###}ms "
            + "(1Hz fixed tick).");
        return true;
    }

    public static double MeasurePerformanceP95()
    {
        TestWorld world = new TestWorld(100, 100, 20f);
        try
        {
            for (int warmup = 0; warmup < 5; warmup++)
            {
                world.Advance(1);
            }

            SpeciesThermalProfile profile =
                new SpeciesThermalProfile(15f, 27f, 0f, 40f, -10f, 48f);
            ThermalProtectionProfile protection =
                ThermalProtectionProfile.None;
            List<double> samples = new List<double>();
            Stopwatch stopwatch = new Stopwatch();
            for (int sample = 0; sample < 30; sample++)
            {
                stopwatch.Restart();
                world.Advance(1);
                for (int character = 0; character < 500; character++)
                {
                    float temperature = 4f + character % 38;
                    CharacterEnvironmentUnityAdapter.CalculateTemperatureRates(
                        temperature,
                        profile,
                        protection,
                        out _,
                        out _,
                        out _);
                }

                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            double[] ordered = samples.OrderBy(value => value).ToArray();
            return ordered[
                Mathf.Clamp(
                    Mathf.CeilToInt(ordered.Length * 0.95f) - 1,
                    0,
                    ordered.Length - 1)];
        }
        finally
        {
            world.Dispose();
        }
    }

    private static SpeciesThermalProfile CreateSlimeThermalProfile() =>
        new SpeciesThermalProfile(16f, 24f, 5f, 34f, 0f, 40f);

    private static void Verify(
        string label,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                failures.Add($"{label}: returned false");
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{label}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> objects =
            new List<UnityEngine.Object>();
        private readonly MutableClock clock = new MutableClock();
        private readonly TestBuildingWorld buildings =
            new TestBuildingWorld();

        public TestWorld(int width, int height, float outdoorTemperature)
        {
            Grid = new Grid(width, height);
            Runtime = new EnvironmentalFieldRuntimeApplicationAdapter(
                new TestGridProvider(Grid),
                buildings,
                new TestEnvironment(outdoorTemperature),
                new AlwaysPoweredRuntime(),
                clock,
                new EnvironmentalFieldAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()));
        }

        public Grid Grid { get; }
        public EnvironmentalFieldRuntimeApplicationAdapter Runtime { get; }

        public BuildableObject CreateBuilding(
            string name,
            Vector2Int position,
            BuildingAbility ability)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            data.name = name;
            data.objectName = name;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Shop;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            data.ReplaceAbilities(new BuildingAbilityCollection());
            data.AbilityModules.Add(ability);
            GameObject gameObject = new GameObject(name);
            objects.Add(gameObject);
            objects.Add(data);
            BuildableObject building =
                gameObject.AddComponent<BuildableObject>();
            building.RestorePersistentIdentity(new BuildingInstanceId(
                $"building:test:environment:{name}:{position.x}:{position.y}"));
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(data, position);
            return building;
        }

        public void Add(BuildableObject building)
        {
            buildings.Add(building);
            Runtime.MarkTopologyDirty();
        }

        public void Advance(int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                clock.Advance(1f);
                Runtime.Tick();
            }
        }

        public void Dispose()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(objects[i]);
                }
            }
        }
    }

    private sealed class RecordingCharacterEnvironmentRuntime :
        ICharacterEnvironmentPersistence
    {
        public int RestoreCount { get; private set; }

        public DungeonCharacterEnvironmentSaveData Capture() =>
            new DungeonCharacterEnvironmentSaveData
            {
                exposures = Array.Empty<CharacterEnvironmentExposure>(),
                equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>()
            };
        public CharacterEnvironmentRestoreCandidate BuildRestoreCandidate(
            DungeonCharacterEnvironmentSaveData saveData)
        {
            DungeonGameRestoreReport report = new();
            CharacterEnvironmentSaveValidation.Validate(saveData, report);
            if (!report.Success)
            {
                throw new InvalidOperationException(
                    string.Join(" | ", report.Errors));
            }
            return new CharacterEnvironmentRestoreCandidate();
        }

        public void PublishRestoreCandidate(
            CharacterEnvironmentRestoreCandidate candidate) => RestoreCount++;
    }

    private sealed class TestGridProvider : IGridSystemProvider
    {
        public TestGridProvider(Grid grid)
        {
            Grid = grid;
        }

        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }
    }

    private sealed class TestBuildingWorld : IBuildingWorldQuery
    {
        private readonly List<BuildableObject> buildings =
            new List<BuildableObject>();

        public int BuildingVersion { get; private set; }
        public IReadOnlyList<BuildableObject> Buildings => buildings;

        public void Add(BuildableObject building)
        {
            buildings.Add(building);
            BuildingVersion++;
        }
    }

    private sealed class TestEnvironment : ISurvivalEnvironmentQuery
    {
        private readonly float temperature;

        public TestEnvironment(float temperature)
        {
            this.temperature = temperature;
        }

        public SurvivalEnvironmentSnapshot GetEnvironmentSnapshot()
        {
            return new SurvivalEnvironmentSnapshot(
                SurvivalWeatherType.Clear,
                temperature,
                0f,
                0f,
                0f);
        }
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        public void Advance(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time += deltaTime;
            FrameCount++;
        }
    }

    private sealed class AlwaysPoweredRuntime : IPowerInfrastructureQuery
    {
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();

        public bool IsPowered(BuildableObject building) => true;

        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }
}
