using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Real husbandry tick/restore/presenter with controlled pen, field and clock.</summary>
public static class Wim018ProductThermalRuntimeDebugScenarios
{
    private static readonly Vector2Int PenCell = new(8, 1);
    private const string PenId = "building:wim018:thermal-pen";
    private const string AnimalId = "wild:wim018:thermal-witness";
    private const float DaysPerTick = 5f / 180f;

    public static void RunProtectedPlay(DungeonRuntimeLifetimeScope scope)
    {
        Require(EditorApplication.isPlaying && Time.timeScale == 0f,
            "Protected, autosave-disabled Play session required.");
        List<string> evidence = new();
        VerifySpecies(scope.Container, "deep_goat", 5f, 30f, evidence);
        VerifySpecies(scope.Container, "frost_ram", -10f, 10f, evidence);
        evidence.Add("scope=real-public-Tick/Capture/BuildRestore/Restore/presenter;controlled-pen/field/clock");
        evidence.Add("not-covered=natural-capture/AI;whole-world-restore;colony-balance");
        evidence.Add("result=PASS");
        System.IO.Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        System.IO.File.WriteAllLines(
            "Artifacts/QA/wim-implementation/wim-018-product-thermal-runtime.txt", evidence);
    }

    private static void VerifySpecies(IObjectResolver services, string speciesId,
        float minimum, float maximum, List<string> evidence)
    {
        CapturedWildlifeState captured = new()
        {
            wildlifeId = AnimalId, speciesId = speciesId, penId = PenId,
            penPosition = PenCell, capturePosition = Vector2Int.zero,
            isTamed = true, transportState = CapturedWildlifeTransportState.Penned
        };
        string careBefore = JsonUtility.ToJson(captured);
        ControlledClock clock = new();
        ControlledField field = new() { Temperature = (minimum + maximum) / 2f };
        Season currentSeason = Season.Spring;
        IGameCalendar calendar = CreatePort<IGameCalendar>((method, args) =>
            method.Name == "get_Season" ? currentSeason : throw Unexpected(method));
        IWildlifeCaptureRuntime capture = CreatePort<IWildlifeCaptureRuntime>((method, args) =>
        {
            switch (method.Name)
            {
                case "CopyCapturedAnimalReferences":
                    var destination = (List<CapturedWildlifeState>)args[0];
                    destination.Clear(); destination.Add(captured); return null;
                case "TryGetCaptured":
                    bool found = (string)args[0] == AnimalId;
                    args[1] = found ? captured : null; return found;
                case "TryGetPenCapacity": args[1] = 8; return (string)args[0] == PenId;
                case "get_CapturedAnimals": return new[] { captured };
                default: throw Unexpected(method);
            }
        });
        IFacilityCapabilityQuery facilities = CreatePort<IFacilityCapabilityQuery>((method, args) =>
            method.Name == "FindOperational" ? Array.Empty<BuildableObject>() : throw Unexpected(method));
        IBuildingFacilityStateChangePort invalidations =
            CreatePort<IBuildingFacilityStateChangePort>((method, args) =>
                method.Name == "MarkDynamicStateDirty" ? null : throw Unexpected(method));
        IWildlifeSpeciesCatalogProvider catalog = services.Resolve<IWildlifeSpeciesCatalogProvider>();
        AnimalHusbandryRuntime runtime = new(capture, services.Resolve<IWildlifeRuntime>(), catalog,
            services.Resolve<IItemDefinitionCatalog>(), services.Resolve<IWorldItemStackRuntime>(),
            services.Resolve<IWildlifeCarcassService>(), clock, calendar, field,
            facilities, invalidations, new DungeonRuntimeAggregateRootStore());
        Require(catalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species)
            && species.Husbandry.Products.Count == 1, "Expected authored single-product species.");
        clock.Advance();
        runtime.Tick();
        DungeonAnimalHusbandrySaveData baseline = runtime.Capture();
        HusbandryAnimalSaveData saved = baseline.animals.Single();
        saved.sex = AnimalSex.Female;
        saved.ageDays = 10f;
        saved.pregnant = true;
        saved.pregnancyProgressDays = 0.2f;
        saved.otherParentAnimalInstanceId = string.Empty;
        saved.manureProgressDays = 0.3f;
        saved.readyManureCycles = 1;
        saved.products.Single().progressDays = 0.4f;
        saved.products.Single().readyCycles = 1;
        string baselineJson = JsonUtility.ToJson(baseline);

        void Reset(float temperature, Season season, bool available = true)
        {
            clock.Reset();
            field.Temperature = temperature; field.Available = available;
            currentSeason = season;
            runtime.Restore(runtime.BuildRestore(JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(baselineJson)));
            Require(JsonUtility.ToJson(runtime.Capture()) == baselineJson,
                "Controlled current-format checkpoint did not restore exactly.");
        }

        void Step(float expectedMultiplier)
        {
            HusbandryAnimalState before = runtime.Animals.Single();
            string readonlyBefore = JsonUtility.ToJson(runtime.Capture());
            for (int i = 0; i < 3; i++)
            {
                Require(runtime.TryGetProductThermalSnapshot(new WildlifeInstanceId(AnimalId), out var thermal)
                    && thermal.PenPosition == PenCell && thermal.HasTemperature == field.Available,
                    "Thermal query did not use the captured pen position/availability.");
                if (field.Available)
                    Require(Near(thermal.TemperatureC, field.Temperature)
                        && Near(thermal.ComfortMinimumTemperatureC, minimum)
                        && Near(thermal.ComfortMaximumTemperatureC, maximum)
                        && Near(thermal.ProductProgressMultiplier, expectedMultiplier),
                        "Authored thermal snapshot differs from independent expected values.");
                else
                    Require(thermal.StatusCode == AnimalProductThermalStatusCode.EnvironmentalFieldUnavailable
                        && Near(thermal.ProductProgressMultiplier, 0f),
                        "Missing field was mislabeled as missing pen or allowed product progress.");
            }
            Require(JsonUtility.ToJson(runtime.Capture()) == readonlyBefore,
                "Thermal query mutated the saved husbandry state.");
            clock.Advance(); runtime.Tick();
            HusbandryAnimalState after = runtime.Animals.Single();
            Require(Near(after.Products.Single().ProgressDays,
                    before.Products.Single().ProgressDays + DaysPerTick * expectedMultiplier),
                "Actual product tick did not apply exactly one thermal multiplier.");
            Require(Near(after.PregnancyProgressDays, before.PregnancyProgressDays + DaysPerTick)
                && Near(after.ManureProgressDays, before.ManureProgressDays + DaysPerTick)
                && after.Products.Single().ReadyCycles == before.Products.Single().ReadyCycles
                && after.ReadyManureCycles == before.ReadyManureCycles,
                "Temperature changed pregnancy/manure progression or existing ready output.");
            Require(JsonUtility.ToJson(captured) == careBefore,
                "Thermal product tick mutated capture/feed/health-related care state.");
        }

        Reset((minimum + maximum) / 2f, Season.Spring); Step(1f);
        float springProgress = runtime.Animals.Single().Products.Single().ProgressDays;
        Reset((minimum + maximum) / 2f, Season.Winter); Step(1f);
        Require(Near(runtime.Animals.Single().Products.Single().ProgressDays, springProgress),
            "Season added a second production penalty at the same actual temperature.");
        Reset(minimum - 10f, Season.Spring); Step(0.5f);
        Reset(maximum + 15f, Season.Spring); Step(0.25f);
        Reset(maximum + 10f, Season.Spring); Step(0.5f);
        string mid = JsonUtility.ToJson(runtime.Capture());
        runtime.Restore(runtime.BuildRestore(JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(mid)));
        Require(JsonUtility.ToJson(runtime.Capture()) == mid, "Mid-progress restore changed product/ready state.");
        field.Temperature = (minimum + maximum) / 2f;
        Step(1f);
        Reset(maximum, Season.Spring, available: false); Step(0f);
        field.Available = true;
        Step(1f);
        evidence.Add(speciesId + "=actual-pen/cold/hot/comfort/missing-recovery/season/readonly/restore/ready-invariants:PASS");

        if (speciesId == "frost_ram")
        {
            Reset(20f, Season.Winter);
            VerifyPanel(runtime, catalog, calendar, evidence);
        }
    }

    private static void VerifyPanel(AnimalHusbandryRuntime runtime,
        IWildlifeSpeciesCatalogProvider catalog, IGameCalendar calendar, List<string> evidence)
    {
        GameObject penObject = new("WIM018 detached UI pen");
        GameObject panel = new("WIM018 runtime panel", typeof(RectTransform));
        try
        {
            BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset");
            Require(asset != null && asset.GetBeastPenAbility() != null, "Authored pen missing.");
            BuildableObject pen = penObject.AddComponent<BuildableObject>();
            pen.PrepareForDetachedRestore();
            pen.RestorePersistentIdentity(new BuildingInstanceId(PenId));
            CharacterAiEditorTestDependencies.Inject(pen);
            pen.SetGrid(new Grid(20, 4));
            pen.Initialization(asset, PenCell);
            AnimalHusbandryBuildingPanelPresenter presenter = new(runtime, runtime, catalog, calendar);
            presenter.Render(panel.transform, pen, TMPro.TMP_Settings.defaultFontAsset,
                message => throw new InvalidOperationException("Unexpected UI action: " + message), () => { });
            string text = string.Join(" | ", panel.GetComponentsInChildren<TMPro.TMP_Text>()
                .Select(value => value.text));
            Require(text.Contains("20") && text.Contains("-10") && text.Contains("50")
                && text.Contains("산물"), "Actual husbandry panel omitted frost-ram thermal context.");
            evidence.Add("actual-panel=" + text.Replace('\r', ' ').Replace('\n', ' '));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panel);
            UnityEngine.Object.DestroyImmediate(penObject);
        }
    }

    private static T CreatePort<T>(Func<MethodInfo, object[], object> handler) where T : class
    {
        T result = DispatchProxy.Create<T, LocalPort>();
        ((LocalPort)(object)result).Handler = handler;
        return result;
    }
    public class LocalPort : DispatchProxy
    {
        internal Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) => Handler(method, args);
    }
    private static Exception Unexpected(MethodInfo method) =>
        new InvalidOperationException("Unexpected test port call: " + method.DeclaringType.Name + "." + method.Name);
    private static bool Near(float a, float b) => Mathf.Abs(a - b) <= 0.00001f;
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("WIM018: " + message); }
    private sealed class ControlledClock : IGameClock
    {
        public float DeltaTime => 5f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Reset() { Time = 0f; FrameCount = 0; }
        internal void Advance() { Time += 5f; FrameCount++; }
    }
    private sealed class ControlledField : IEnvironmentalFieldQuery
    {
        private float temperature;
        private bool available = true;
        internal float Temperature { get => temperature; set { temperature = value; Version++; } }
        internal bool Available { get => available; set { available = value; Version++; } }
        public int Version { get; private set; } = 1;
        public bool IsInitialized => true;
        public bool TryGetCell(Vector2Int position, out EnvironmentalCellSnapshot value)
        {
            value = new EnvironmentalCellSnapshot(position, Temperature, 100f, 75f);
            return Available && position == PenCell;
        }
        public bool TryGetAverage(IReadOnlyList<Vector2Int> cells, out EnvironmentalCellSnapshot value)
            => TryGetCell(cells.Count == 0 ? Vector2Int.zero : cells[0], out value);
        public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int position) => false;
        public bool TryGetTargetTemperature(Vector2Int position, out float value)
        { value = 0f; return false; }
    }
}
