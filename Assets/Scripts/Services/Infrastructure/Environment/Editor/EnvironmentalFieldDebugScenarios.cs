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
    // Root-owned WIM016 witness. Actual field adapter and authored definitions;
    // clock/power inputs are controlled. This is not a built-farm/AI claim.
    public static bool RunWim016LightFieldFocused(out string report)
    {
        var lines = new List<string> {
            "WIM016 authored light and actual environment adapter",
            "scope=controlled calendar/power, authored lamp/crop, real field diffusion",
            "not-tested=main construction UI, actual power network, crop Tick, irrigation, six-adult balance" };
        try
        {
            var expected = new (string Path, float Stop, float Sufficient)[] {
                ("crop_cave_mushroom", 0, 0), ("crop_ember_root", 5, 50),
                ("crop_twilight_grain", 5, 50), ("crop_bloodleaf", 5, 40),
                ("crop_night_grape", 0, 25), ("crop_moonflower", 0, 25),
                ("crop_shade_fiber", 0, 25), ("crop_dreamleaf", 0, 25),
                ("V22Textiles/crop_ember-cotton", 5, 50),
                ("V22Textiles/crop_frost-flax", 5, 40),
                ("V22Textiles/crop_mire-reed", 5, 40),
                ("V22Textiles/crop_spore-hemp", 5, 40) };
            var crops = Resources.LoadAll<CropDefinitionSO>(CropDefinitionSO.ResourcePath);
            Require(crops.Length == expected.Length, "Authored crop fixture coverage changed; review explicit table.");
            foreach (var row in expected)
            {
                var crop = Resources.Load<CropDefinitionSO>(CropDefinitionSO.ResourcePath + "/" + row.Path);
                Require(crop != null, "Missing authored crop: " + row.Path);
                Require(crop.LightRequirement.StopLight == row.Stop
                    && crop.LightRequirement.SufficientLight == row.Sufficient,
                    "Authored light thresholds differ: " + crop.CropId);
                var low = CropGrowthCycleAuthority.EvaluateLight(crop, true, row.Stop);
                var mid = CropGrowthCycleAuthority.EvaluateLight(crop, true, (row.Stop + row.Sufficient) / 2);
                var full = CropGrowthCycleAuthority.EvaluateLight(crop, true, row.Sufficient);
                var excess = CropGrowthCycleAuthority.EvaluateLight(crop, true, 100);
                bool independent = row.Sufficient == 0;
                Require(low.GrowthMultiplier == (independent ? 1f : 0f)
                    && Mathf.Approximately(mid.GrowthMultiplier, independent ? 1f : .5f)
                    && full.GrowthMultiplier == 1 && excess.GrowthMultiplier == 1,
                    "Stop/half/full/capped growth differs: " + crop.CropId);
            }
            lines.Add("[PASS] all12 authored profiles; stop/half/full/excess boundaries; explicit fungus independence");
            var high = Resources.Load<CropDefinitionSO>(CropDefinitionSO.ResourcePath + "/crop_twilight_grain");
            Require(CropGrowthCycleAuthority.EvaluateLight(high, false, 100).GrowthMultiplier == 0,
                "Missing field was silently replaced by sufficient light.");
            Require(Mathf.Approximately(CropGrowthCycleAuthority.EvaluateLight(high, true, 30).GrowthMultiplier, 5f / 9f),
                "Night30 high-crop profile must produce5/9, not an additional .55 product.");

            using (var world = new TestWorld(5, 3, 20))
            {
                // Grid defaults to dungeon interior (constant ambient20).
                // This witness is explicitly about the exterior day/night input.
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 5; x++)
                        world.Grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
                world.Advance(1);
                var cell = new Vector2Int(2, 1);
                Require(world.Runtime.TryGetCell(cell, out var day), "Day cell unavailable.");
                world.Calendar.TimeOfDay = TimeOfDay.Night;
                world.Advance(40);
                Require(world.Runtime.TryGetCell(cell, out var night) && night.LightLevel < day.LightLevel - 10,
                    "Calendar night did not reduce actual ambient field.");
                world.Calendar.TimeOfDay = TimeOfDay.Morning;
                world.Advance(40);
                Require(world.Runtime.TryGetCell(cell, out var morning) && morning.LightLevel > night.LightLevel + 10,
                    "Morning did not restore ambient field.");
                lines.Add($"[PASS] actual field day={day.LightLevel:R}/night={night.LightLevel:R}/morning={morning.LightLevel:R}");
            }

            foreach (var entry in new (string Code, string Name, int Radius, int FullDistance, int Width, float Power)[] {
                ("I19", "태양등", 9, 5, 1, 4), ("I20", "인공태양", 17, 9, 3, 12) })
            {
                var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    $"Assets/Resources/SO/Building/Industrial/{entry.Code}_{entry.Name}.asset");
                Require(definition != null, "New lamp not authored: " + entry.Code);
                var light = definition.GetAbility<BuildingLightingAbility>();
                Require(light != null && light.intensity == 1 && light.radius == entry.Radius
                    && definition.width == entry.Width
                    && definition.GetAbility<BuildingPowerConsumerAbility>()?.demandPerSecond == entry.Power,
                    "Lamp authored geometry/power differs: " + entry.Code);
                using (var world = new TestWorld(45, 3, 20))
                {
                    world.Calendar.TimeOfDay = TimeOfDay.Night;
                    var lamp = world.CreateAuthoredBuilding(definition, new Vector2Int(18, 1));
                    world.Add(lamp);
                    world.Advance(1);
                    var target = lamp.centerPos + Vector2Int.right * entry.FullDistance;
                    Require(world.Runtime.TryGetCell(target, out var lit) && lit.LightLevel >= 49.999f,
                        "Authored lamp failed direct sufficient-light reach: " + entry.Code);
                    Require(CropGrowthCycleAuthority.EvaluateLight(high, true, lit.LightLevel).GrowthMultiplier >= .9999f,
                        "Actual lit field did not meet high crop requirement.");
                    Require(world.Runtime.TryGetCell(lamp.centerPos, out var peak) && peak.LightLevel <= 100,
                        "Lamp escaped existing100 field cap.");
                    world.Power.Powered = false;
                    world.Advance(40);
                    Require(world.Runtime.TryGetCell(target, out var dark) && dark.LightLevel < lit.LightLevel - 5,
                        "Power loss did not remove source from actual field.");
                    world.Power.Powered = true;
                    world.Advance(1);
                    Require(world.Runtime.TryGetCell(target, out var restored) && restored.LightLevel >= 49.999f,
                        "Power restoration did not recover authored crop coverage.");
                    string saved = JsonUtility.ToJson(world.Runtime.Capture());
                    world.Runtime.Restore(world.Runtime.PrepareRestore(JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(saved)));
                    Require(saved == JsonUtility.ToJson(world.Runtime.Capture()), "Existing field restore changed light.");
                    lines.Add($"[PASS] {entry.Code} distance={entry.FullDistance};lit={lit.LightLevel:R};off={dark.LightLevel:R};restored={restored.LightLevel:R};field-roundtrip exact");
                }
            }
            lines.Add("result=PASS");
            report = string.Join("\n", lines);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("result=FAIL\n" + error);
            report = string.Join("\n", lines);
            return false;
        }
    }

    public static void RunDoorContracts()
    {
        using (var world = new TestWorld(11, 1, 20f))
        {
            Door door = world.CreateDoor("door-state", new Vector2Int(5, 0));
            world.Add(door);
            world.Advance(1);
            int structure = world.Grid.StructuralVersion;
            var baseline = world.Runtime.Capture();
            baseline.cells.Clear();
            baseline.cells.Add(new EnvironmentalCellSaveData { x = 4, y = 0, temperatureC = 65, airQuality = 20, lightLevel = 30 });
            world.Runtime.Restore(world.Runtime.PrepareRestore(baseline));
            world.Advance(1);
            Require(world.Runtime.TryGetCell(door.centerPos, out var closed), "Closed cell missing.");
            string closedState = door.OperationStateModule.CaptureState();
            Require(!door.OperationStateModule.TryRestoreState(1, "{}", out _)
                && door.OperationStateModule.CaptureState() == closedState, "Invalid door payload mutated state.");
            Require(door.OperationStateModule.TryRestoreState(1, "{\"state\":3}", out _), "Held-open restore failed.");
            string heldState = door.OperationStateModule.CaptureState();
            Require(door.OperationStateModule.TryRestoreState(1, heldState, out _)
                && door.OperationStateModule.CaptureState() == heldState && door.IsHeldOpen, "Door state roundtrip changed.");
            world.Runtime.Restore(world.Runtime.PrepareRestore(baseline));
            world.Advance(1);
            Require(world.Runtime.TryGetCell(door.centerPos, out var opened)
                && opened.TemperatureC > closed.TemperatureC && opened.AirQuality < closed.AirQuality,
                "Door open state did not change thermal/air exchange.");
            Require(Math.Abs(opened.LightLevel - closed.LightLevel) < 0.0001f
                && world.Grid.StructuralVersion == structure, "Door operation changed light or structural boundary.");
            Require(door.OperationStateModule.TryRestoreState(1, closedState, out _), "Door closing restore failed.");
            world.Runtime.Restore(world.Runtime.PrepareRestore(baseline));
            world.Advance(1);
            Require(world.Runtime.TryGetCell(door.centerPos, out var again)
                && again.TemperatureC == closed.TemperatureC && again.AirQuality == closed.AirQuality,
                "Restored closed door retained stale environmental mask.");
            System.IO.Directory.CreateDirectory("Artifacts/QA/wim-implementation");
            System.IO.File.WriteAllLines("Artifacts/QA/wim-implementation/wim-045-012-door-contracts.txt", new[]
            {
                "state-roundtrip-invalid-payload-atomic=PASS",
                "actual-environment-adapter-open-close-restored-mask=PASS",
                "structural-revision-light-invariant=PASS",
                $"closed-temperature={closed.TemperatureC};open-temperature={opened.TemperatureC}",
                $"closed-air={closed.AirQuality};open-air={opened.AirQuality}",
                "scope=explicit-world-and-module-restore-fixture;main-movement-not-covered", "result=PASS"
            });
        }
    }

    public static bool RunFacilityOperatingFuelFocused(out string report)
    {
        var lines = new List<string>
        {
            "Facility-local operating fuel / actual environmental step",
            "scope=controlled initial facility state and clock; real field/Light2D/state module",
            "not-tested=physical delivery/sink ACK, full-world restore, daily event or six-adult balance"
        };
        try
        {
            using var world = new TestWorld(30, 3, 20f);
            var first = world.CreateBuilding("fuel-first", new Vector2Int(5, 1),
                new BuildingLightingAbility { intensity = 1f, radius = 1 },
                new BuildingFuelConsumerAbility());
            var second = world.CreateBuilding("fuel-second", new Vector2Int(15, 1),
                new BuildingLightingAbility { intensity = 1f, radius = 1 },
                new BuildingFuelConsumerAbility());
            var intrinsic = world.CreateBuilding("fuel-independent", new Vector2Int(25, 1),
                new BuildingLightingAbility { intensity = 1f, radius = 1 });
            world.Add(first); world.Add(second); world.Add(intrinsic);
            bool Visible(BuildableObject building) => building
                .GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true).enabled;
            float Remaining() => first.FacilityState.remainingFuelGameSeconds;
            void Expect(float expected, string reason) =>
                Require(Mathf.Abs(Remaining() - expected) < .001f, reason + "; actual=" + Remaining());
            world.Advance(1);
            Require(!Visible(first) && !Visible(second) && Visible(intrinsic),
                "Empty local fuel or independent light was misclassified.");
            // Initial state is an explicit fixture, not a claimed fuel-delivery result.
            first.RestoreFacilityState(new FacilityRuntimeState { remainingFuelGameSeconds = 180f });
            world.Advance(1);
            Expect(179f, "One real field step must consume one operating second");
            Require(Visible(first) && !Visible(second) && second.FacilityState.remainingFuelGameSeconds == 0f,
                "One fixture's charge enabled another fuel consumer.");
            world.Calendar.Day = 2;
            world.Advance(1);
            Expect(178f, "Changing calendar day expired or refilled a facility");
            for (int query = 0; query < 5; query++)
            {
                _ = world.Environment.HasFuelSupply(first);
                _ = world.Runtime.TryGetCell(first.centerPos, out _);
                _ = first.FacilityState.Clone();
            }
            Expect(178f, "Read-only observation consumed fuel");
            world.SetPaused(true);
            world.Advance(3);
            Expect(178f, "Paused frames consumed fuel");
            world.SetPaused(false);
            first.enabled = false;
            world.Advance(3);
            Expect(178f, "Disabled facility consumed fuel");
            Require(!Visible(first), "Disabled fuel light still emits.");
            first.enabled = true;
            world.Advance(1);
            Expect(177f, "Re-enabled facility did not resume its retained fuel");
            lines.Add("[PASS] local-only light; running180->179->178; calendar/query/pause/disable retain fuel; resume177");

            var module = new FacilityRuntimeStateModule(first);
            string saved = module.CaptureState();
            Require(module.TryRestoreState(module.CurrentVersion, saved, out string restoreFailure),
                "Current facility module failed: " + restoreFailure);
            Require(module.CaptureState() == saved, "Current module restore replayed a charge.");
            var invalid = JsonUtility.FromJson<FacilityRuntimeState>(saved);
            invalid.remainingFuelGameSeconds = -1f;
            Require(!module.TryRestoreState(module.CurrentVersion, JsonUtility.ToJson(invalid), out _)
                    && module.CaptureState() == saved,
                "Negative remaining fuel restored or partially changed the facility.");
            Require(!module.TryRestoreState(module.CurrentVersion + 1, saved, out _)
                    && module.CaptureState() == saved,
                "Unsupported module version changed the facility.");
            // The mutation is internal to the production assembly; reflection is
            // confined to this invalid-input boundary test, not gameplay preparation.
            var replaceFuel = typeof(BuildableObject).GetMethod("ReplaceFacilityFuelState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Require(replaceFuel != null, "Missing actual facility fuel mutation boundary.");
            bool mutationRejected = false;
            try
            {
                replaceFuel.Invoke(first, new object[]
                {
                    15f, 1, new FacilityFuelCommitState { phase = 999 }
                });
            }
            catch (System.Reflection.TargetInvocationException error)
            {
                mutationRejected = error.InnerException is InvalidOperationException;
            }
            Require(mutationRejected && module.CaptureState() == saved,
                "Invalid fuel mutation published partial state before rejection.");
            world.Advance(177);
            Expect(0f, "Fuel did not exhaust after the remaining operating time");
            Require(!first.HasFacilityFuelSupply && !second.HasFacilityFuelSupply,
                "Exhausted/never-charged facilities still have fuel supply.");
            world.Advance(1);
            Require(!Visible(first) && !Visible(second) && Visible(intrinsic),
                "Exhaustion did not remove only the fuel-bound light.");
            lines.Add("[PASS] exact current module round-trip; negative/version/invalid-mutation rejection unchanged; running exhaustion without refill/replay");
            lines.Add("result=PASS");
        }
        catch (Exception error) { lines.Add("result=FAIL\n" + error); }
        report = string.Join("\n", lines);
        return lines.Last() == "result=PASS";
    }

    public static void RunLightingOnly()
    {
        using (var world = new TestWorld(30, 3, 20f))
        {
            var electric = world.CreateBuilding("electric", new Vector2Int(4, 1),
                new BuildingLightingAbility { intensity = 1, radius = 1 }, new BuildingPowerConsumerAbility());
            var fuel = world.CreateBuilding("fuel", new Vector2Int(14, 1),
                new BuildingLightingAbility { intensity = 1, radius = 1 }, new BuildingFuelConsumerAbility());
            var intrinsic = world.CreateBuilding("intrinsic", new Vector2Int(24, 1),
                new BuildingLightingAbility { intensity = 1, radius = 1 });
            world.Add(electric); world.Add(fuel); world.Add(intrinsic);
            world.Power.Powered = false;
            world.Advance(2);
            bool Visible(BuildableObject building) => building.GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>(true).enabled;
            float Level(BuildableObject building)
            {
                Require(world.Runtime.TryGetCell(building.centerPos, out var cell), "Missing light cell.");
                return cell.LightLevel;
            }
            Require(!Visible(electric) && !Visible(fuel) && Visible(intrinsic), "Source availability did not reach Light2D.");
            float darkElectric = Level(electric), darkFuel = Level(fuel);
            Require(Level(intrinsic) > darkElectric, "Intrinsic light was incorrectly power/fuel gated.");
            world.Power.Powered = true;
            fuel.RestoreFacilityState(new FacilityRuntimeState { remainingFuelGameSeconds = 180f });
            world.Advance(2);
            Require(Visible(electric) && Visible(fuel) && Level(electric) > darkElectric
                && Level(fuel) > darkFuel, "Restored supply did not increase actual field light and visual.");
            float litElectric = Level(electric), litFuel = Level(fuel);
            world.Power.Powered = false;
            fuel.RestoreFacilityState(new FacilityRuntimeState());
            world.Advance(4);
            Require(!Visible(electric) && !Visible(fuel) && Level(electric) < litElectric
                && Level(fuel) < litFuel && Visible(intrinsic), "Supply loss did not remove emission.");
            intrinsic.enabled = false;
            world.Advance(1);
            Require(!Visible(intrinsic), "Disabled light source still emits.");
            intrinsic.enabled = true;
            world.Advance(1);
            Require(Visible(intrinsic), "Re-enabled intrinsic light did not recover.");
            string captured = JsonUtility.ToJson(world.Runtime.Capture());
            world.Runtime.Restore(world.Runtime.PrepareRestore(JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(captured)));
            Require(JsonUtility.ToJson(world.Runtime.Capture()) == captured, "Light field serialized round trip differs.");
        }
        using (var world = new TestWorld(12, 3, 20f))
        {
            var exhaust = world.CreateBuilding("powered-air", new Vector2Int(5, 1),
                new BuildingAirExchangeAbility { targetAirQuality = 20f, qualityPerSecond = 20f,
                    radius = 1, exchangesWithOutside = false, requiresPower = true });
            world.Add(exhaust);
            world.Power.Powered = false;
            world.Advance(2);
            Require(world.Runtime.TryGetCell(exhaust.centerPos, out var off), "Air cell missing.");
            world.Power.Powered = true;
            world.Advance(2);
            Require(world.Runtime.TryGetCell(exhaust.centerPos, out var on) && on.AirQuality < off.AirQuality,
                "Existing powered air source no longer follows supply.");
            world.Power.Powered = false;
            world.Advance(2);
            Require(world.Runtime.TryGetCell(exhaust.centerPos, out var recovered) && recovered.AirQuality >= on.AirQuality,
                "Unpowered air source still contributes.");
        }
        Require(VerifyThermalSource() && VerifySaveRoundTrip(), "Existing thermal source/save regression failed.");
        var authored = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition == null || definition.GetAbility<BuildingLightingAbility>() == null) continue;
            authored.Add(definition.name + ":power=" + (definition.GetAbility<BuildingPowerConsumerAbility>() != null)
                + ";fuel=" + (definition.GetAbility<BuildingFuelConsumerAbility>() != null));
        }
        authored.Sort(StringComparer.Ordinal);
        Require(authored.Count > 0, "No authored lighting capabilities found.");
        System.IO.Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        System.IO.File.WriteAllLines("Artifacts/QA/wim-implementation/wim-011-light-contracts.txt", new[]
        {
            "electric-and-fuel-off-on-field-and-Light2D=PASS",
            "unpowered-intrinsic-and-disabled-reenabled-source=PASS",
            "field-serialized-round-trip=PASS", "existing-thermal-air-and-save=PASS",
            "authored-capability-count=" + authored.Count, string.Join("\n", authored),
            "scope=real-environment-adapter-with-explicit-test-power-and-fuel-inputs", "result=PASS"
        });
    }

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
        Require(guids.Length == 4,
            $"expected 4 authored workwear definitions, found {guids.Length}");
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
        Require(
            outputDependencies.Contains(
                typeof(IFacilityBufferMassAdmissionService))
            && outputDependencies.Contains(
                typeof(IFacilityBufferPlannedOutputPublicationService))
            && outputDependencies.Contains(typeof(IPhysicalItemMassQuery))
            && outputDependencies.Contains(typeof(IItemInstanceRepository))
            && outputDependencies.Contains(
                typeof(IProductionFacilityHandleQuery))
            && outputDependencies.Contains(
                typeof(IProductionOutputBufferCapacityProjector))
            && !outputDependencies.Contains(typeof(IWorldItemStackRuntime))
            && !outputDependencies.Contains(typeof(IProductionAssemblyBridge))
            && !outputDependencies.Contains(
                typeof(IEnvironmentalWorkwearCatalog)),
            "workwear production does not use the common planned gram publication authority");
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
        CharacterEnvironmentSaveSection section = new(
            runtime,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly,
            new ApparelRejectedDismantleRestoreGuard(
                EmptyDispositionRestoreCandidateQuery.Instance,
                EmptyFacilityBufferPlannedOutputRestoreCandidateQuery.Instance),
            AcceptingApparelOutputDetachedCapacityRestoreGuard.Instance);
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
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
            equippedApparel = Array.Empty<EquippedApparelSaveData>(),
            apparelPolicies = Array.Empty<CharacterApparelPolicySaveData>(),
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
            apparelWorkOrderTerminalStates =
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
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
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
            equippedApparel = Array.Empty<EquippedApparelSaveData>(),
            apparelPolicies = Array.Empty<CharacterApparelPolicySaveData>(),
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
            apparelWorkOrderTerminalStates =
                Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
        };
        DungeonGameRestoreReport validValidation = new();
        CharacterEnvironmentSaveValidation.Validate(valid, validValidation);
        Require(validValidation.Success && runtime.RestoreCount == 0,
            "valid empty environment arrays failed preflight or published state");
        CharacterEnvironmentSaveSection unavailableCrossAggregateSection = new(
            runtime,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly,
            new ApparelRejectedDismantleRestoreGuard(
                UnavailableDispositionRestoreCandidateQuery.Instance,
                UnavailableFacilityBufferPlannedOutputRestoreCandidateQuery
                    .Instance),
            AcceptingApparelOutputDetachedCapacityRestoreGuard.Instance);
        DungeonGameRestoreReport detachedPreflight = new();
        unavailableCrossAggregateSection.ValidatePayload(
            JsonUtility.ToJson(valid),
            unavailableCrossAggregateSection.SectionVersion,
            detachedPreflight);
        Require(detachedPreflight.Success && runtime.RestoreCount == 0,
            "environment preflight required cross-aggregate restore candidates");
        bool unavailableStageRejected = false;
        try
        {
            unavailableCrossAggregateSection.StageRestore(
                JsonUtility.ToJson(valid),
                unavailableCrossAggregateSection.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException exception)
        {
            unavailableStageRejected = exception.Message.Contains(
                "physical restore candidate is unavailable",
                StringComparison.Ordinal);
        }
        Require(unavailableStageRejected && runtime.RestoreCount == 0,
            "environment staging did not require its detached physical candidate");
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
            Environment = new TestEnvironment(outdoorTemperature);
            Runtime = new EnvironmentalFieldRuntimeApplicationAdapter(
                new TestGridProvider(Grid),
                buildings,
                Environment,
                Power,
                clock,
                Calendar,
                new EnvironmentalFieldAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new RestoreWorldCandidateIndex());
        }

        public Grid Grid { get; }
        public TestEnvironment Environment { get; }
        public ControlledCalendar Calendar { get; } = new ControlledCalendar();
        public AlwaysPoweredRuntime Power { get; } = new AlwaysPoweredRuntime();
        public EnvironmentalFieldRuntimeApplicationAdapter Runtime { get; }

        public BuildableObject CreateBuilding(
            string name,
            Vector2Int position,
            params BuildingAbility[] abilities)
            => CreateBuildingCore(name, position, false, abilities);

        public Door CreateDoor(string name, Vector2Int position)
            => (Door)CreateBuildingCore(name, position, true, Array.Empty<BuildingAbility>());

        public BuildableObject CreateAuthoredBuilding(BuildingSO definition, Vector2Int position)
        {
            var gameObject = new GameObject("Wim016_" + definition.name);
            objects.Add(gameObject);
            var building = gameObject.AddComponent<BuildableObject>();
            building.RestorePersistentIdentity(new BuildingInstanceId("building:test:wim016:" + definition.id));
            CharacterAiEditorTestDependencies.Inject(building);
            building.SetGrid(Grid);
            building.Initialization(definition, position);
            return building;
        }

        private BuildableObject CreateBuildingCore(string name, Vector2Int position, bool door, BuildingAbility[] abilities)
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
            foreach (var ability in abilities) data.AbilityModules.Add(ability);
            GameObject gameObject = new GameObject(name);
            objects.Add(gameObject);
            objects.Add(data);
            BuildableObject building =
                door ? gameObject.AddComponent<InteriorDoor>() : gameObject.AddComponent<BuildableObject>();
            building.RestorePersistentIdentity(new BuildingInstanceId(
                $"building:test:environment:{name}:{position.x}:{position.y}"));
            CharacterAiEditorTestDependencies.Inject(building);
            building.SetGrid(Grid);
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

        public void SetPaused(bool paused) => clock.IsPaused = paused;

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
                equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
                equippedApparel = Array.Empty<EquippedApparelSaveData>(),
                apparelPolicies = Array.Empty<CharacterApparelPolicySaveData>(),
                apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                apparelWorkOrderTerminalStates =
                    Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
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

    private sealed class EmptyDispositionRestoreCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly EmptyDispositionRestoreCandidateQuery Instance =
            new();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions =>
                Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = null;
            return false;
        }
    }

    private sealed class UnavailableDispositionRestoreCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly
            UnavailableDispositionRestoreCandidateQuery Instance = new();

        public bool IsCandidateAvailable => false;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions =>
                Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = null;
            return false;
        }
    }

    private sealed class UnavailableFacilityBufferPlannedOutputRestoreCandidateQuery :
        IFacilityBufferPlannedOutputRestoreCandidateQuery
    {
        internal static readonly
            UnavailableFacilityBufferPlannedOutputRestoreCandidateQuery
                Instance = new();

        public bool IsCandidateAvailable => false;
        public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            Batches =>
                Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>();

        public bool TryGetBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
        {
            batch = null;
            return false;
        }
    }

    private sealed class AcceptingApparelOutputDetachedCapacityRestoreGuard :
        IApparelOutputDetachedCapacityRestoreGuard
    {
        internal static readonly
            AcceptingApparelOutputDetachedCapacityRestoreGuard Instance = new();

        public void Validate(
            IReadOnlyList<ApparelWorkOrderSaveData> liveOrders,
            IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalStates)
        {
        }
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
        public bool HasFuelSupply(BuildableObject building) =>
            building != null && building.HasFacilityFuelSupply;
        public float GetRemainingFuelGameSeconds(BuildableObject building) =>
            building != null ? building.FacilityState.remainingFuelGameSeconds : 0f;
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

    private sealed class ControlledCalendar : IGameCalendar
    {
        public int Day { get; set; } = 1;
        public int Hour => TimeOfDay == global::TimeOfDay.Night ? 22 : 12;
        public int Year => 1;
        public int DayOfYear => 1;
        public Season Season => global::Season.Spring;
        public int DayOfSeason => 1;
        public long AbsoluteHour => Hour;
        public float ElapsedSeconds => Hour * GameCalendarRules.SecondsPerGameHour;
        public TimeOfDay TimeOfDay { get; set; } = global::TimeOfDay.Noon;
        public bool IsRunning => true;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int offset) => GameCalendarRules.ProjectRegional(Day, Hour, offset);
        public void Start() { }
        public void SetDateTime(int day, int hour) => throw new NotSupportedException("Use the controlled phase input.");
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused { get; set; }

        public void Advance(float deltaTime)
        {
            DeltaTime = IsPaused ? 0f : deltaTime;
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class AlwaysPoweredRuntime : IPowerInfrastructureQuery
    {
        public bool Powered { get; set; } = true;
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();

        public bool IsPowered(BuildableObject building) => Powered;

        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }
}
