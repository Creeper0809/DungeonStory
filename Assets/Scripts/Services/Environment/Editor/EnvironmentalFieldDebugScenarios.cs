using System;
using System.Collections.Generic;
using System.Linq;
using Stopwatch = System.Diagnostics.Stopwatch;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

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
        Verify("preservation thresholds", VerifyPreservationRules, failures);
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
            target.Runtime.Restore(save);
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
                "source-hvac",
                position,
                sourceEmitter));
            target.Add(target.CreateBuilding(
                "target-hvac",
                position,
                targetEmitter));
            source.Advance(1);
            target.Advance(1);
            Require(
                source.Runtime.TrySetTargetTemperature(
                    position,
                    6f,
                    out string failureReason),
                $"target setting failed: {failureReason}");
            DungeonEnvironmentalFieldSaveData save =
                source.Runtime.Capture();
            target.Runtime.Restore(save);
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

    private static bool VerifySlimeColdWork()
    {
        ThermalProtectionProfile none = ThermalProtectionProfile.None;
        SpeciesThermalProfile naked = SpeciesThermalProfile
            .ForSpecies("Slime")
            .Apply(none);
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
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
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
            8f,
            SpeciesThermalProfile.ForSpecies("Slime").Apply(coldSuit),
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
            SpeciesThermalProfile.ForSpecies("Slime").Apply(runeAndTrait);
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
            2f,
            protectedProfile,
            runeAndTrait,
            out float runeRate,
            out _,
            out bool lethalAtTwo);
        Require(
            Mathf.Approximately(runeRate, 0f) && !lethalAtTwo,
            "rune suit and trait do not support long 2C work");
        CharacterEnvironmentRuntime.CalculateTemperatureRates(
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
                SpeciesThermalProfile.ForSpecies("Human");
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
                    CharacterEnvironmentRuntime.CalculateTemperatureRates(
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
            Runtime = new EnvironmentalFieldRuntime(
                new TestGridProvider(Grid),
                buildings,
                new TestEnvironment(outdoorTemperature),
                new AlwaysPoweredRuntime(),
                clock);
        }

        public Grid Grid { get; }
        public EnvironmentalFieldRuntime Runtime { get; }

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
            data.type = typeof(BuildableObject);
            data.ReplaceAbilities(new BuildingAbilityCollection());
            data.AbilityModules.Add(ability);
            GameObject gameObject = new GameObject(name);
            BuildableObject building =
                gameObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(data, position);
            objects.Add(gameObject);
            objects.Add(data);
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

    private sealed class AlwaysPoweredRuntime : IElectricalNetworkRuntime
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

        public DungeonPowerInfrastructureSaveData Capture()
        {
            return new DungeonPowerInfrastructureSaveData();
        }

        public void Restore(DungeonPowerInfrastructureSaveData snapshot)
        {
        }
    }
}
