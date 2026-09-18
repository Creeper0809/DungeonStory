using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Controlled real-adapter test, not a natural AI or whole-world restore test.</summary>
public static class Wim004LightAdaptationRuntimeDebugScenarios
{
    public static void RunProtectedPlay(DungeonRuntimeLifetimeScope scope)
    {
        Require(EditorApplication.isPlaying && Time.timeScale == 0f,
            "Run only in the paused, autosave-disabled protected Play session.");
        Require(scope != null && scope.Container != null, "Live composition root missing.");
        IObjectResolver services = scope.Container;
        RunSpecies(services, "Assets/Resources/SO/Character/Customer_Vampire.asset", 35f);
        RunSpecies(services, "Assets/Resources/SO/Character/ExpandedSpecies/Customer_Myconid.asset", 55f);
        System.IO.Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        System.IO.File.WriteAllLines("Artifacts/QA/wim-implementation/wim-004-light-adaptation-runtime.txt", new[]
        {
            "scope=protected-play;controlled-field-and-clock;real-live-and-predictive-adapters",
            "authored-vampire-myconid-live-query-mood-work=PASS",
            "repeated-tick-single-mood-factor-and-comfort-removal=PASS",
            "no-new-low-light-visual-strain-live-and-prediction=PASS",
            "current-format-exposure-roundtrip-retains-legacy-strain-and-recovers-gradually=PASS",
            "light-query-read-only-and-no-movement-accuracy-penalty=PASS",
            "actual-status-presenter-refresh-without-carry-capability=PASS",
            "not-covered=natural-ai;whole-world-save-restore;natural-player-click",
            "result=PASS"
        });
    }

    private static void RunSpecies(IObjectResolver services, string assetPath, float maximum)
    {
        GameObject obj = new GameObject("WIM004 controlled light witness");
        try
        {
            obj.AddComponent<AbilityWork>();
            CharacterActor actor = obj.AddComponent<CharacterActor>();
            actor.PrepareForComposition();
            CharacterAiEditorTestDependencies.Inject(obj);
            actor.data = AssetDatabase.LoadAssetAtPath<CharacterSO>(assetPath);
            Require(actor.data != null, "Authored test character missing: " + assetPath);
            actor.characterType = CharacterType.NPC;
            actor.RefreshAbilityCache();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new CharacterId("character:wim004:light-witness"));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            // The local WitnessWorld owns this test subject. Publishing it into
            // the live registry would create unrelated apparel policy state.
            Require(actor.IsUnpublishedComposition, "Witness unexpectedly entered the live registry.");
            foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
                actor.stats[condition] = 100f;
            CharacterId id = CharacterPersistentIdentity.Require(actor);
            Require(id.Value == "character:wim004:light-witness"
                && actor.SpeciesTag == (maximum == 35f ? "Vampire" : "Myconid"),
                "Fixture identity/species was not composed from the intended authored character.");
            ControlledField field = new() { Light = maximum + 20f };
            ControlledClock clock = new();
            WitnessWorld world = new(actor, services.Resolve<ICharacterLifetimeQuery>());
            ICharacterSpeciesEnvironmentCatalog species =
                services.Resolve<ICharacterSpeciesEnvironmentCatalog>();
            ICharacterEnvironmentProtectionResolver protection =
                services.Resolve<ICharacterEnvironmentProtectionResolver>();
            ICharacterPerformanceQuery performance = services.Resolve<ICharacterPerformanceQuery>();
            CharacterEnvironmentUnityAdapter runtime = new(
                field, world, world, species, protection,
                services.Resolve<IEnvironmentalWorkwearPersistence>(),
                services.Resolve<ICharacterApparelPersistence>(),
                services.Resolve<ICharacterApparelCommand>(),
                services.Resolve<IApparelWorkOrderPersistence>(),
                services.Resolve<ICharacterBodyHealthCommand>(), clock,
                new CharacterEnvironmentAggregateStateStore(new DungeonRuntimeAggregateRootStore()),
                performance,
                // Scope condition updates to the same unpublished witness world.
                // It owns no equipped items; never advance the live party here.
                new ApparelConditionRuntime(field, world,
                    services.Resolve<ICharacterApparelQuery>(),
                    services.Resolve<IWorldItemStackRuntime>(),
                    services.Resolve<IClimateQuery>(),
                    services.Resolve<IClimateDefinitionCatalog>(),
                    services.Resolve<IGridSystemProvider>(),
                    services.Resolve<IApparelDefinitionCatalog>(),
                    services.Resolve<ITextileMaterialCatalog>(),
                    services.Resolve<IApparelMaterialProjector>(),
                    services.Resolve<IAnatomyAttachmentQuery>()));
            EnvironmentWorkPolicyUnityAdapter policy = new(
                field, runtime, protection,
                services.Resolve<IEnvironmentalWorkwearQuery>(),
                services.Resolve<IEnvironmentalWorkwearCommand>(), species, performance);

            runtime.SetWorkContext(id, EnvironmentalWorkKind.Precision);
            clock.Advance();
            runtime.Tick();
            string stateBeforeQuery = JsonUtility.ToJson(runtime.Capture());
            for (int i = 0; i < 3; i++)
            {
                Require(runtime.TryGetLightAdaptation(id, out CharacterLightAdaptationSnapshot value)
                    && value.Enabled && Near(value.ComfortableMaximum, maximum)
                    && Near(value.Discomfort, 0.5f) && Near(value.MoodContribution, -1.5f)
                    && Near(value.WorkSpeedMultiplier, 0.975f), "Live authored light projection differs.");
            }
            Require(stateBeforeQuery == JsonUtility.ToJson(runtime.Capture()),
                "Readonly light query changed environment/apparel state.");
            Require(Near(runtime.GetWorkSpeedMultiplier(id), 0.975f)
                && Near(runtime.GetPrecisionWorkSpeedMultiplier(id), 0.975f),
                "General/precision work did not project the same light cost once.");
            Require(Near(runtime.GetMoveSpeedMultiplier(id), 1f)
                && Near(runtime.GetAccuracyPenaltyPoints(id), 0f),
                "Light adaptation leaked to movement/accuracy.");
            VerifyMood(actor, -1.5f);
            VerifyStatusPresenter(services, runtime, actor, maximum, maximum + 20f, 50f, -1.5f, -2.5f);
            for (int i = 0; i < 3; i++) { clock.Advance(); runtime.Tick(); }
            VerifyMood(actor, -1.5f);

            field.Light = 0f;
            runtime.SetWorkContext(id, EnvironmentalWorkKind.Precision);
            WorkEnvironmentAssessment forecast = policy.AssessStart(actor, actor.GetNowXY(),
                Array.Empty<GridMoveStep>(), 30f, EnvironmentalWorkKind.Precision, false);
            Require(Near(forecast.Projection.Visual.WorkEnd, 0f),
                "Predictive precision route added generic darkness strain to an adapted species.");
            clock.Advance();
            runtime.Tick();
            Require(Near(runtime.GetExposure(id).visualStrain, 0f)
                && Near(runtime.GetPrecisionWorkSpeedMultiplier(id), 1f),
                "Live darkness did not respect species comfort.");
            VerifyMood(actor, 0f);
            VerifyStatusPresenter(services, runtime, actor, maximum, 0f, 0f, 0f, 0f);

            // Author a valid CURRENT-format legacy exposure; do not modify live aggregates.
            DungeonCharacterEnvironmentSaveData captured = runtime.Capture();
            CharacterEnvironmentExposure legacy = captured.exposures.Single(e => e.characterId == id.Value);
            legacy.visualStrain = 60f;
            legacy.visualBand = EnvironmentalExposureBand.Impaired;
            DungeonCharacterEnvironmentSaveData serialized = JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(
                JsonUtility.ToJson(captured));
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(serialized));
            Require(Near(runtime.GetExposure(id).visualStrain, 60f)
                && runtime.GetVisualBand(id) == EnvironmentalExposureBand.Impaired,
                "Restore erased pre-existing visual strain.");
            string roundTrip = JsonUtility.ToJson(runtime.Capture());
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(roundTrip)));
            Require(JsonUtility.ToJson(runtime.Capture()) == roundTrip,
                "Current-format repeat restore changed captured exposure/apparel state.");
            runtime.SetWorkContext(id, EnvironmentalWorkKind.Precision);
            clock.Advance();
            runtime.Tick();
            Require(Near(runtime.GetExposure(id).visualStrain, 58.5f),
                "Existing strain was cleared abruptly or failed to recover by 1.5 in one second.");
            Require(runtime.GetPrecisionWorkSpeedMultiplier(id) < 1f,
                "Legacy strain penalty disappeared before recovery.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

    private static void VerifyStatusPresenter(
        IObjectResolver services, ICharacterEnvironmentStatusQuery runtime,
        CharacterActor actor, float maximum, float light, float discomfort,
        float mood, float work)
    {
        GameObject textObject = new("WIM004 profile projection", typeof(RectTransform),
            typeof(TMPro.TextMeshProUGUI));
        try
        {
            TMPro.TMP_Text text = textObject.GetComponent<TMPro.TMP_Text>();
            CharacterSummaryStatusPresenter presenter = new(
                services.Resolve<IDungeonItemCatalogProvider>(),
                services.Resolve<IItemHaulingSettingsProvider>(),
                services.Resolve<ISurvivalFoodQuery>(), runtime,
                services.Resolve<CharacterSummaryPopulationPresenter>(),
                services.Resolve<ICharacterApparelQuery>(),
                services.Resolve<ICharacterApparelCommand>());
            presenter.Bind(text, null, null);
            presenter.RefreshProfileAndVitals(actor, actor.Stats);
            string expected = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.LightAdaptation.Row", light, 0f, maximum,
                discomfort, mood, work);
            Require(!expected.Contains("CharacterSummary.Status.LightAdaptation.Row")
                && text.text.Contains(expected),
                "Actual profile presenter missed the localized, independently expected light values.");
        }
        finally { UnityEngine.Object.DestroyImmediate(textObject); }
    }

    private static void VerifyMood(CharacterActor actor, float expected)
    {
        CharacterMoodFactorSnapshot[] factors = actor.Mood.Factors
            .Where(f => f.Id == "environment:light-adaptation").ToArray();
        Require(expected == 0f ? factors.Length == 0
            : factors.Length == 1 && Near(factors[0].Value, expected),
            "Light mood factor duplicated, stale, or has the wrong value.");
    }

    private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < 0.00001f;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("WIM004: " + reason);
    }

    private sealed class WitnessWorld : ICharacterWorldQuery, ICharacterLifetimeQuery
    {
        internal WitnessWorld(CharacterActor actor, ICharacterLifetimeQuery live)
        {
            Characters = new[] { actor };
            AllCharacters = live.AllCharacters.Concat(Characters).Distinct().ToArray();
        }
        public int CharacterVersion => 1;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
        public IReadOnlyList<CharacterActor> AllCharacters { get; }
    }

    private sealed class ControlledClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance() { Time += 1f; FrameCount++; }
    }

    private sealed class ControlledField : IEnvironmentalFieldQuery
    {
        internal float Light;
        public int Version => 1;
        public bool IsInitialized => true;
        public bool TryGetCell(Vector2Int position, out EnvironmentalCellSnapshot snapshot)
        {
            snapshot = new EnvironmentalCellSnapshot(position, 20f, 100f, Light);
            return true;
        }
        public bool TryGetAverage(IReadOnlyList<Vector2Int> cells, out EnvironmentalCellSnapshot snapshot)
            => TryGetCell(cells.Count == 0 ? Vector2Int.zero : cells[0], out snapshot);
        public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int position) => false;
        public bool TryGetTargetTemperature(Vector2Int position, out float value)
        { value = 0f; return false; }
    }
}
