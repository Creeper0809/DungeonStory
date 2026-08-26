#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[InitializeOnLoad]
public static class V27PairedClutterPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-paired-run-rng.txt";
    public const string PairedCsvPath =
        "Artifacts/QA/v27-balance-paired-run-rng.csv";
    public const string ClutterCsvPath =
        "Artifacts/QA/v27-balance-floor-clutter.csv";
    public const string FocusedReportPath =
        "Temp/v27-balance-paired-clutter-focused.txt";
    private const string RequestPath = "Temp/v27-balance-paired-clutter.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    public static IReadOnlyList<string> EvidenceSourcePaths { get; } =
        Array.AsReadOnly(new[]
        {
            "Assets/Scripts/Services/Economy/Editor/V27PairedClutterPlayModeVerifier.cs",
            "Assets/Scripts/Services/Economy/V27PopulationCapacityModels.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs",
            "Assets/Scripts/Services/Items/PhysicalStockQuery.cs",
            "Assets/Scripts/Services/Foundation/Random/RandomStreamProvider.cs",
            "Assets/Scripts/Services/Character/AI/CharacterAiScheduler.cs",
            "Assets/Scripts/Services/Character/AI/AIBrain.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityMove.cs",
            "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs"
        });
    static V27PairedClutterPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/V27/Run Paired Clutter 4-Arm PlayMode (32 Seeds)")]
    public static void RequestRun() => RequestRun(32, 1);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused (1 Seed)")]
    public static void RequestFocusedRun() => RequestRun(1, 1);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused - Crop Harvest (Seed 2)")]
    public static void RequestFocusedCropHarvestRun() => RequestRun(1, 2);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused - Mining Burst (Seed 3)")]
    public static void RequestFocusedMiningRun() => RequestRun(1, 3);

    public static void RequestFocusedRun(int seed) => RequestRun(1, seed);

    public static void RequestRun(int seedCount) => RequestRun(seedCount, 1);

    public static string ComputeEvidenceSourceDigest()
    {
        StringBuilder builder = new();
        foreach (string path in EvidenceSourcePaths)
        {
            builder.Append(path).Append('\t')
                .Append(V27BalanceArtifactWriter.ComputeSha256(path))
                .Append('\n');
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(builder.ToString()));
        const string hex = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = hex[digest[index] >> 4];
            result[index * 2 + 1] = hex[digest[index] & 15];
        }
        return new string(result);
    }

    private static void RequestRun(int seedCount, int focusedSeed)
    {
        if (seedCount != 1 && seedCount is (< 32 or > 64))
            throw new ArgumentOutOfRangeException(nameof(seedCount));
        if (focusedSeed < 1)
            throw new ArgumentOutOfRangeException(nameof(focusedSeed));
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(RequestPath, $"{seedCount}|{focusedSeed}");
        if (EditorApplication.isPlaying)
        {
            StartRunner(seedCount, focusedSeed);
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.isDirty && !string.Equals(active.path, GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "V27 paired clutter refuses to replace a dirty scene.");
        if (!string.Equals(active.path, GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => TryStartPending();

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
            TryStartPending();
    }

    private static void TryStartPending()
    {
        if (!File.Exists(RequestPath))
            return;
        int seedCount = 32;
        int focusedSeed = 1;
        string[] tokens = File.ReadAllText(RequestPath).Trim().Split('|');
        int.TryParse(tokens[0], out seedCount);
        if (tokens.Length > 1)
            int.TryParse(tokens[1], out focusedSeed);
        File.Delete(RequestPath);
        StartRunner(
            seedCount == 1 ? 1 : Mathf.Clamp(seedCount, 32, 64),
            Mathf.Max(1, focusedSeed));
    }

    private static void StartRunner(int seedCount, int focusedSeed)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                V27PairedClutterPlayModeRunner>() != null)
            return;
        // Enter Play Mode Options may keep static fields while destroying every
        // scene object. The live runner object is the only duplicate authority.
        V27PairedClutterPlayModeRunner runner =
            new GameObject("V27 Paired Clutter PlayMode Runner")
                .AddComponent<V27PairedClutterPlayModeRunner>();
        runner.SeedCount = seedCount;
        runner.Focused = seedCount == 1;
        runner.StartSeed = focusedSeed;
    }
}

public sealed class V27PairedClutterPlayModeRunner : MonoBehaviour
{
    private const string FacilityBurstItemId = "survival:cooked_meal";
    private const string CropId = "crop:twilight-grain";
    private const float GameDaySeconds = 180f;
    private const float WarmupSeconds = 90f;
    private const float WindowSeconds = 45f;
    private const float PickupSearchAndSchedulingHeadroomSeconds = GameDaySeconds;
    private const float PickupCaptureDeltaTime = 1f / 120f;
    private const float RecoverySeconds = 90f;
    private const float WorkMilliWuPerGameSecond = 50000f / GameDaySeconds;
    private const float VerificationTimeScale = 32f;
    private const string ScenarioId = "v27.floor-clutter.paired";

    private readonly List<PairedRunWindowResult> rows = new();
    private readonly List<FloorRow> floorRows = new();
    private readonly List<string> failures = new();
    private readonly List<string> focusedDeferredFailures = new();
    private readonly List<string> consoleIssues = new();
    private readonly Dictionary<string, IReadOnlyList<RandomStreamDiagnosticSnapshot>>
        randomByArmWindow = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<string>> affectedActorsBySeed = new();
    private readonly Dictionary<string, string> armStartRandomHashes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> armStartSemanticHashes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> armStartSemanticTexts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> focusedFrameTraces =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> measuredActorIds =
        new(StringComparer.Ordinal);

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private IWorldItemStackRuntime items;
    private IDungeonItemCatalogProvider itemCatalog;
    private IWarehousePhysicalMassQueryPort warehouseMassQuery;
    private IItemTransferService itemTransfers;
    private IFloorClutterDiagnosticsQuery clutter;
    private IRandomStreamProvider randomProvider;
    private IRandomStreamDiagnosticsQuery randomDiagnostics;
    private ICharacterAiWorldRegistry world;
    private IWorldDropZoneQuery dropZones;
    private IWorldItemHaulPlanningService haulPlanning;
    private ISurvivalFoodQuery survivalFoodQuery;
    private ISurvivalFoodCommand survivalFood;
    private CropPlotRuntime cropPlots;
    private IResourceEconomyContentCatalog economyCatalog;
    private IWorldResourceRuntime worldResources;
    private ProgressionSceneRuntimeReferences progression;
    private IGameClock clock;
    private IGameClockDiagnosticsControl clockDiagnostics;
    private IGameSpeedController gameSpeed;
    private IDungeonDebugModeService debugMode;
    private IDungeonUserSettingsService userSettings;
    private ICharacterDeprivationRuntime deprivation;
    private CharacterAiScheduler scheduler;
    private CharacterSpawner characterSpawner;
    private GridBuildingPlacementService livePlacementService;
    private Grid grid;
    private DungeonGameSaveData originalSave;
    private string commonCheckpointJson = string.Empty;
    private float commonCheckpointTime;
    private int commonCheckpointFrame;
    private string warehouseId = string.Empty;
    private string overflowWarehouseId = string.Empty;
    private string productionInputWarehouseId = string.Empty;
    private string producerFacilityId = string.Empty;
    private string cropPlotId = string.Empty;
    private string miningRecipeId = string.Empty;
    private string miningNodeId = string.Empty;
    private string cropBurstItemId = string.Empty;
    private string miningBurstItemId = string.Empty;
    private string faultActorId = string.Empty;
    private Vector2Int burstCell;
    private Vector2Int cropBurstCell;
    private Vector2Int miningBurstCell;
    private Vector2Int overflowCell;
    private DungeonSpaceLayoutSnapshot layout;
    private Facility fixtureWarehouse;
    private Facility fixtureOverflowWarehouse;
    private Facility fixtureProducerFacility;
    private Facility fixtureCropPlot;
    private BuildingSO producerFacilityAsset;
    private float originalTimeScale;
    private bool originalRunInBackground;
    private bool originalFreezeNeeds;
    private bool originalFriendlyInvincible;
    private bool originalPauseWildlifeAi;
    private bool originalDeveloperMode;
    private int originalGameSpeed;
    private bool originalGamePause;
    private bool gameSpeedConfigured;
    private bool developerModeConfigured;
    private bool debugModeConfigured;
    private bool schedulerDiagnosticsConfigured;
    private bool spawnerDiagnosticsConfigured;
    private bool originalSchedulerDeterministicMode;
    private bool originalSpawnerDiagnosticsPaused;
    private float originalCaptureDeltaTime;
    private bool finished;
    private bool runCompleted;
    private int requiredSeedCount;
    private int productionBurstArmCount;
    private int facilityBurstArmCount;
    private int cropHarvestBurstArmCount;
    private int miningBurstArmCount;
    private int productionPriorityArmCount;
    private int postPickupFaultArmCount;
    private int lastRuntimeHeadroomErosionCount;
    private string lastRuntimeHeadroomErosionDetail = string.Empty;
    private PairedRunAttributionAssessment finalAssessment;
    private ArmBurstProbe currentBurstProbe;

    public int SeedCount { get; set; } = 32;
    public int StartSeed { get; set; } = 1;
    public bool Focused { get; set; }
    public string CurrentPhase { get; private set; } = "created";
    public int CompletedWindowCount => rows.Count;
    public int FailureCount => failures.Count;

    private IEnumerator Start()
    {
        CurrentPhase = "starting";
        originalTimeScale = Time.timeScale;
        originalCaptureDeltaTime = Time.captureDeltaTime;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        // Three exact game seconds per rendered frame divides the 90/45/90
        // measurement windows without a partial final tick.
        Time.captureDeltaTime = 3f / VerificationTimeScale;
        Time.timeScale = VerificationTimeScale;
        Application.logMessageReceived += CaptureIssue;
        yield return ExecuteGuarded(RunAll());
        Finish();
    }

    private IEnumerator RunAll()
    {
        yield return ResolveWorld();
        if (failures.Count > 0)
            yield break;
        yield return CreateFixtureAndCheckpoint();
        if (failures.Count > 0)
            yield break;

        int targetSeeds = SeedCount;
        requiredSeedCount = targetSeeds;
        int lastSeed = checked(StartSeed + targetSeeds - 1);
        for (int seed = StartSeed; seed <= lastSeed; seed++)
        {
            yield return RunSeed(seed);
            if (failures.Count > 0 && !Focused)
                yield break;
        }

        if (Focused)
        {
            if (failures.Count > 0)
            {
                runCompleted = true;
                yield break;
            }
            Check(rows.Count == 16, "PAIRED_FOCUSED_FOUR_ARMS",
                $"rows={rows.Count};seeds={rows.Select(value => value.Seed).Distinct().Count()}");
            ValidateProductionInterventionEvidence();
            ValidateFocusedCleanRepeatability();
            Check(floorRows.All(value => value.RuntimeHeadroomPermille >= 300),
                "PAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT",
                $"rows={floorRows.Count};minimumPermille="
                + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}");
            failures.AddRange(focusedDeferredFailures);
            runCompleted = true;
            yield break;
        }

        PairedRunAttributionAssessment assessment =
            PairedRunAttributionEvaluator.Evaluate(rows);
        if (assessment.RequiresExpandedSample && targetSeeds == 32)
        {
            targetSeeds = 64;
            requiredSeedCount = targetSeeds;
            for (int seed = 33; seed <= targetSeeds; seed++)
            {
                yield return RunSeed(seed);
                if (failures.Count > 0)
                    yield break;
            }
            assessment = PairedRunAttributionEvaluator.Evaluate(rows);
        }

        ValidateProductionInterventionEvidence();
        Check(assessment.Passed, "PAIRED_CLUTTER_ATTRIBUTION",
            $"samples={assessment.SampleCount};medianPermille={assessment.MedianClutterDeltaPermille};"
            + $"p95Permille={assessment.P95ClutterDeltaPermille};maxPermille={assessment.MaximumClutterDeltaPermille};"
            + $"madPermille={assessment.MadPermille};failure={assessment.FailureCode}");
        Check(floorRows.All(value => value.ImmediateFailures == 0),
            "FLOOR_CLUTTER_ACCESS_EGRESS_ZERO",
            $"rows={floorRows.Count};immediate={floorRows.Sum(value => value.ImmediateFailures)}");
        Check(floorRows.Where(value => value.IsRecovery)
                .All(value => value.Persistent == 0),
            "FLOOR_CLUTTER_RECOVERY_ZERO",
            $"recoveryRows={floorRows.Count(value => value.IsRecovery)};"
            + $"persistent={floorRows.Where(value => value.IsRecovery).Sum(value => value.Persistent)}");
        Check(floorRows.All(value => value.RuntimeHeadroomPermille >= 300),
            "PAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT",
            $"rows={floorRows.Count};minimumPermille="
            + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}");
        finalAssessment = assessment;
        runCompleted = true;
    }

    private IEnumerator ResolveWorld()
    {
        CurrentPhase = "resolve-world";
        float deadline = Time.realtimeSinceStartup + 30f;
        bool prepared = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value?.Container != null);
            if (scope?.Container != null && LiveActors().Length < 3 && !prepared)
            {
                prepared = true;
                _ = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            if (scope?.Container != null && LiveActors().Length >= 3)
                break;
            yield return null;
        }
        if (prepared)
        {
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            Time.timeScale = VerificationTimeScale;
        }

        foreach (CharacterActor actor in FindObjectsByType<CharacterActor>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                 .Select(CharacterActorCollection.GetCanonical)
                 .Where(value => value != null && value.CurrentLifecycleState is
                     CharacterLifecycleState.EnteringDungeon
                     or CharacterLifecycleState.SpawningOutside)
                 .Distinct())
        {
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-fixture-settle");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        float settlementDeadline = Time.realtimeSinceStartup + 5f;
        int stableFrames = 0;
        int previousCount = -1;
        while (Time.realtimeSinceStartup < settlementDeadline)
        {
            EnsureVerificationTimeScale();
            CharacterActor[] all = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(CharacterActorCollection.GetCanonical)
                .Where(value => value != null && !value.IsDead
                    && value.characterType is not CharacterType.Customer
                        and not CharacterType.Intruder)
                .Distinct()
                .ToArray();
            bool transition = all.Any(value => value.CurrentLifecycleState is
                CharacterLifecycleState.EnteringDungeon
                or CharacterLifecycleState.SpawningOutside);
            int activeCount = all.Count(value =>
                value.CurrentLifecycleState == CharacterLifecycleState.Active);
            stableFrames = !transition && activeCount >= 3 && activeCount == previousCount
                ? stableFrames + 1
                : 0;
            previousCount = activeCount;
            if (stableFrames >= 2)
                break;
            yield return null;
        }

        saves = Resolve<IDungeonGameSaveService>();
        items = Resolve<IWorldItemStackRuntime>();
        itemCatalog = items?.CatalogProvider;
        warehouseMassQuery = Resolve<IStockQuery>() as IWarehousePhysicalMassQueryPort;
        itemTransfers = Resolve<IItemTransferService>();
        clutter = Resolve<IFloorClutterDiagnosticsQuery>();
        randomProvider = Resolve<IRandomStreamProvider>();
        randomDiagnostics = Resolve<IRandomStreamDiagnosticsQuery>();
        world = Resolve<ICharacterAiWorldRegistry>();
        dropZones = Resolve<IWorldDropZoneQuery>();
        haulPlanning = Resolve<IWorldItemHaulPlanningService>();
        survivalFoodQuery = Resolve<ISurvivalFoodQuery>();
        survivalFood = Resolve<ISurvivalFoodCommand>();
        cropPlots = Resolve<CropPlotRuntime>();
        economyCatalog = Resolve<IResourceEconomyContentCatalog>();
        worldResources = Resolve<IWorldResourceRuntime>();
        progression = Resolve<ProgressionSceneRuntimeReferences>();
        clock = Resolve<IGameClock>();
        clockDiagnostics = clock as IGameClockDiagnosticsControl;
        gameSpeed = Resolve<IGameSpeedController>();
        debugMode = Resolve<IDungeonDebugModeService>();
        userSettings = Resolve<IDungeonUserSettingsService>();
        deprivation = Resolve<ICharacterDeprivationRuntime>();
        scheduler = FindFirstObjectByType<CharacterAiScheduler>(
            FindObjectsInactive.Include);
        characterSpawner = FindFirstObjectByType<CharacterSpawner>(
            FindObjectsInactive.Include);
        DungeonStoryGridBuildingController buildingController =
            FindFirstObjectByType<DungeonStoryGridBuildingController>(
                FindObjectsInactive.Include);
        livePlacementService = buildingController != null
            ? typeof(DungeonStoryGridBuildingController)
                .GetField(
                    "placementService",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(buildingController) as GridBuildingPlacementService
            : null;
        world?.TryGetGrid(out grid);
        bool ready = saves != null && items != null && itemCatalog != null
            && warehouseMassQuery != null && itemTransfers != null
            && clutter != null
            && randomProvider != null && randomDiagnostics != null
            && world != null && dropZones != null && haulPlanning != null
            && survivalFoodQuery != null && survivalFood != null
            && cropPlots != null && economyCatalog != null
            && worldResources != null && progression?.BlueprintResearch != null
            && clock != null && gameSpeed != null
            && clockDiagnostics != null
            && debugMode != null && userSettings != null
            && deprivation != null
            && scheduler != null
            && characterSpawner != null
            && livePlacementService != null
            && grid != null
            && LiveActors().Length >= 3;
        CharacterActor[] unresolvedTransitions = FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(CharacterActorCollection.GetCanonical)
            .Where(value => value != null && value.CurrentLifecycleState is
                CharacterLifecycleState.EnteringDungeon
                or CharacterLifecycleState.SpawningOutside)
            .Distinct()
            .ToArray();
        ready &= unresolvedTransitions.Length == 0;
        Check(ready, "PAIRED_AUTHORITIES_READY",
            $"save={saves != null};items={items != null};"
            + $"itemCatalog={itemCatalog != null};"
            + $"warehouseMass={warehouseMassQuery != null};"
            + $"transfers={itemTransfers != null};clutter={clutter != null};"
            + $"random={randomProvider != null}/{randomDiagnostics != null};"
            + $"clockDiagnostics={clockDiagnostics != null};"
            + $"world={world != null};dropZones={dropZones != null};"
            + $"haulPlanning={haulPlanning != null};speed={gameSpeed != null};"
            + $"survivalFood={survivalFoodQuery != null}/{survivalFood != null};"
            + $"crop={cropPlots != null};catalog={economyCatalog != null};"
            + $"worldResources={worldResources != null};"
            + $"research={progression?.BlueprintResearch != null};"
            + $"debug={debugMode != null};"
            + $"settings={userSettings != null};"
            + $"deprivation={deprivation != null};grid={grid != null};"
            + $"scheduler={scheduler != null};"
            + $"spawner={characterSpawner != null};"
            + $"placementService={livePlacementService != null};"
            + $"actors={LiveActors().Length};transitions={unresolvedTransitions.Length}");
        if (!ready)
            yield break;
        foreach (CharacterActor actor in EligibleActors()
                     .OrderBy(ActorId, StringComparer.Ordinal)
                     .Take(3))
        {
            measuredActorIds.Add(ActorId(actor));
        }
        Check(measuredActorIds.Count == 3,
            "PAIRED_MEASURED_ACTOR_SET_EXACT",
            $"count={measuredActorIds.Count};ids="
            + string.Join(",", measuredActorIds.OrderBy(value => value,
                StringComparer.Ordinal)));
        if (measuredActorIds.Count != 3)
            yield break;
        originalGameSpeed = gameSpeed.Speed;
        originalGamePause = gameSpeed.IsPaused;
        gameSpeedConfigured = true;
        originalSchedulerDeterministicMode =
            scheduler.DeterministicSimulationForDiagnostics;
        scheduler.ConfigureDeterministicSimulationForDiagnostics(true);
        schedulerDiagnosticsConfigured = true;
        originalSpawnerDiagnosticsPaused =
            characterSpawner.DeterministicSimulationPausedForDiagnostics;
        characterSpawner.ConfigureDeterministicSimulationForDiagnostics(true);
        spawnerDiagnosticsConfigured = true;
        originalSave = saves.Capture();

        originalDeveloperMode = userSettings.Current.developerMode;
        if (!originalDeveloperMode)
            userSettings.Update(value => value.developerMode = true);
        developerModeConfigured = true;
        originalFreezeNeeds = debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds);
        originalFriendlyInvincible = debugMode.IsCheatEnabled(
            DungeonDebugCheat.FriendlyInvincible);
        originalPauseWildlifeAi = debugMode.IsCheatEnabled(
            DungeonDebugCheat.PauseWildlifeAi);
        debugModeConfigured = true;
        ApplyMeasurementIsolation();
        scheduler.ResetDeterministicSimulationCheckpointForDiagnostics();
        for (int frame = 0; frame < 4; frame++)
            yield return null;
    }

    private IEnumerator CreateFixtureAndCheckpoint()
    {
        CurrentPhase = "create-fixture";
        CharacterActor anchor = LiveActors()
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .First();
        CharacterActor fault = LiveActors()
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Skip(1).First();
        faultActorId = fault.Identity.PersistentId;

        ResearchProjectSO expansionProject = Resources
            .LoadAll<ResearchProjectSO>("SO/Research/Projects")
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.ProjectId.Value,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    StringComparison.Ordinal));
        IFacilityShopCatalog facilityCatalog = Resolve<IFacilityShopCatalog>();
        IGameEventBus gameEvents = Resolve<IGameEventBus>();
        bool expansionAuthorityReady = expansionProject != null
            && facilityCatalog != null
            && gameEvents != null;
        Check(expansionAuthorityReady,
            "PAIRED_MINING_EXPANSION_RESEARCH_AUTHORITY",
            $"project={expansionProject?.ProjectId.Value ?? "missing"};"
            + $"catalog={facilityCatalog != null};events={gameEvents != null}");
        if (!expansionAuthorityReady)
            yield break;

        BlueprintResearchUnlockResult expansionUnlock =
            BlueprintResearchService.ApplyCompletion(
                expansionProject,
                progression.BlueprintResearch.State,
                progression.BlueprintResearch.ShopUnlockState,
                facilityCatalog);
        gameEvents.Publish(new BlueprintResearchCompletedEvent(
            expansionProject,
            expansionUnlock));
        for (int frame = 0; frame < 4; frame++)
            yield return null;
        world.TryGetGrid(out grid);
        IDungeonSpaceExpansionQuery expansion = Resolve<IDungeonSpaceExpansionQuery>();
        DungeonInteriorLayoutSnapshot expandedLayout = default;
        string expansionFailure = "expansion authority missing";
        bool expansionApplied = expansion != null
            && expansion.TryCaptureLayout(
                out expandedLayout,
                out expansionFailure)
            && expandedLayout.ColumnCount
                >= DungeonSpaceExpansionCatalog.BasicSectorTargetColumns
            && string.Equals(
                expansion.LastResult.ResearchProjectId,
                DungeonSpaceExpansionCatalog.QuarryResearchId,
                StringComparison.Ordinal);
        Check(expansionApplied,
            "PAIRED_MINING_EXPANSION_RESEARCH_APPLIED",
            $"project={DungeonSpaceExpansionCatalog.QuarryResearchId};"
            + $"columns={(expansionApplied ? expandedLayout.ColumnCount : 0)};"
            + $"failure={(expansionApplied ? string.Empty : expansionFailure)};"
            + $"developerKeyUsed=False");
        if (!expansionApplied || grid == null)
            yield break;

        BuildingSO warehouseAsset = FindWarehouseAsset();
        BuildingSO producerAsset = FindCookFacilityAsset();
        producerFacilityAsset = producerAsset;
        Vector2Int[] reachable = grid.SearchPath(anchor.GetNowXY())
            .GetReachablePositions()
            .Where(value => grid.IsValidGridPos(value) && grid.IsWalkable(value))
            .Where(value => grid.GetGridCell(value)?.GetOccupant(GridLayer.Building) == null)
            .Where(value => !items.GetAllStacks().Any(stack =>
                stack != null && stack.Quantity > 0 && stack.Position == value))
            .Distinct()
            .OrderBy(value => Mathf.Abs(value.x - anchor.GetNowXY().x)
                + Mathf.Abs(value.y - anchor.GetNowXY().y))
            .Skip(2)
            .ToArray();
        Check(warehouseAsset != null && producerAsset != null && reachable.Length >= 8,
            "PAIRED_FIXTURE_CELLS",
            $"warehouse={warehouseAsset != null};producer={producerAsset != null};cells={reachable.Length}");
        if (warehouseAsset == null || producerAsset == null || reachable.Length < 8)
            yield break;

        IGameSessionStateProvider sessionState = Resolve<IGameSessionStateProvider>();
        IDungeonDebugRuleQuery debugRules = Resolve<IDungeonDebugRuleQuery>();
        BuildingPlacementValidator placement = new(
            new GridPlacementValidator(),
            () =>
            {
                GameSessionState gameData = null;
                sessionState?.TryGetSessionState(out gameData);
                return new BuildingConditionContext(
                    gameData,
                    progression.BlueprintResearch.State,
                    null,
                    debugRules ?? DisabledDungeonDebugRuleQuery.Instance);
            });
        Vector2Int? warehouseAnchor = reachable
            .Where(value => placement.CanBuild(
                grid, warehouseAsset, value, out _))
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        if (!warehouseAnchor.HasValue)
        {
            Fail("PAIRED_FIXTURE_PRIMARY_PLACEMENT", "no legal warehouse anchor");
            yield break;
        }
        Vector2Int warehouseCell = warehouseAnchor.Value;
        GameObject warehouseObject = new("QA_V27_Paired_Warehouse");
        fixtureWarehouse = warehouseObject.AddComponent<Facility>();
        Inject(warehouseObject);
        fixtureWarehouse.SetGrid(grid);
        fixtureWarehouse.Initialization(warehouseAsset, warehouseCell);
        warehouseObject.transform.position = grid.GetWorldPos(warehouseCell);
        warehouseId = fixtureWarehouse.RequirePersistentInstanceId().Value;
        yield return null;

        Vector2Int? overflowAnchor = reachable
            .Where(value => placement.CanBuild(
                grid, warehouseAsset, value, out _))
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        if (!overflowAnchor.HasValue)
        {
            Fail("PAIRED_FIXTURE_OVERFLOW_PLACEMENT", "no legal overflow warehouse anchor");
            yield break;
        }
        overflowCell = overflowAnchor.Value;
        GameObject overflowObject = new("QA_V27_Paired_Overflow_Warehouse");
        fixtureOverflowWarehouse = overflowObject.AddComponent<Facility>();
        Inject(overflowObject);
        fixtureOverflowWarehouse.SetGrid(grid);
        fixtureOverflowWarehouse.Initialization(warehouseAsset, overflowCell);
        overflowObject.transform.position = grid.GetWorldPos(overflowCell);
        overflowWarehouseId = fixtureOverflowWarehouse.RequirePersistentInstanceId().Value;
        for (int frame = 0; frame < 4; frame++)
            yield return null;

        Vector2Int? producerAnchor = reachable
            .Where(value => placement.CanBuild(
                grid, producerAsset, value, out _))
            .OrderByDescending(value => Mathf.Min(
                Manhattan(value, warehouseCell),
                Manhattan(value, overflowCell)))
            .ThenBy(value => value.x)
            .ThenBy(value => value.y)
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        if (!producerAnchor.HasValue)
        {
            Fail("PAIRED_FIXTURE_PRODUCER_PLACEMENT", "no legal cooking facility anchor");
            yield break;
        }
        burstCell = producerAnchor.Value;
        Check(grid.IsValidGridPos(burstCell)
                && placement.CanBuild(grid, producerAsset, burstCell, out _),
            "PAIRED_FIXTURE_PRODUCTION_CELL",
            $"cell={burstCell};asset={producerAsset.id};cook="
            + producerAsset.Facility.SupportsWork(BuiltInWorkTypeIds.Cook));
        if (!grid.IsValidGridPos(burstCell)
            || !placement.CanBuild(grid, producerAsset, burstCell, out _))
            yield break;

        yield return PrepareCropAndMiningFixtures(reachable, placement);
        if (failures.Count > 0)
            yield break;

        bool published = world.Warehouses.Any(value => value != null
            && value.PersistentInstanceId.Value == warehouseId);
        string overflowId = overflowWarehouseId;
        bool overflowPublished = world.Warehouses.Any(value => value != null
            && value.PersistentInstanceId.Value == overflowId);
        IWarehouseFacility productionInputWarehouse = world.Warehouses
            .Where(value => value?.Inventory != null
                && value.PersistentInstanceId.Value != warehouseId
                && value.PersistentInstanceId.Value != overflowWarehouseId
                && value.Inventory.Accepts(StockCategory.Food)
                && value.Inventory.CanStore(StockCategory.Food, 8))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        productionInputWarehouseId =
            productionInputWarehouse?.PersistentInstanceId.Value ?? string.Empty;
        Check(published && overflowPublished
                && !string.IsNullOrWhiteSpace(productionInputWarehouseId)
                && fixtureWarehouse.Inventory?.HasCapacityLimit == true
                && fixtureOverflowWarehouse.Inventory?.HasCapacityLimit == true,
            "PAIRED_WAREHOUSE_LIVE",
            $"id={warehouseId};published={published};capacity={fixtureWarehouse.Inventory?.MaxCapacity ?? -1};"
            + $"overflowId={overflowId};overflowPublished={overflowPublished};"
            + $"overflowCapacity={fixtureOverflowWarehouse.Inventory?.MaxCapacity ?? -1};"
            + $"producerAsset={producerAsset.id};producerCell={burstCell};"
            + $"inputWarehouse={productionInputWarehouseId}");
        if (!published || !overflowPublished
            || string.IsNullOrWhiteSpace(productionInputWarehouseId)
            || fixtureWarehouse.Inventory?.HasCapacityLimit != true
            || fixtureOverflowWarehouse.Inventory?.HasCapacityLimit != true)
            yield break;
        bool anyPlan = false;
        List<string> planDetails = new();
        foreach (CharacterActor actor in LiveActors().OrderBy(ActorId, StringComparer.Ordinal))
        {
            bool preview = haulPlanning.TryPreviewBestPlan(
                actor, out WorldItemHaulPlan plan, out string reason);
            anyPlan |= preview;
            planDetails.Add($"{ActorId(actor)}:{preview}:{plan?.PrimaryDestinationId}:{reason}");
        }
        Check(anyPlan, "PAIRED_INITIAL_HAUL_PLAN",
            string.Join(";", planDetails));
        if (!anyPlan)
            yield break;

        QuiesceActorsForCheckpoint();
        IsolatePreexistingLogistics();
        for (int frame = 0; frame < 2; frame++)
            yield return null;
        HashSet<Vector2Int> authorized = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .Select(value => value.Position)
            .ToHashSet();
        List<KeyValuePair<Vector2Int, SpatialCellRole>> roles = authorized
            .Select(value => new KeyValuePair<Vector2Int, SpatialCellRole>(
                value, SpatialCellRole.AuthorizedLooseSource))
            .ToList();
        foreach (GridCell dropZone in grid.GetCells()
                     .Where(value => value != null
                         && value.AreaType == GridCellAreaType.DropZone))
        {
            roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                dropZone.Position, SpatialCellRole.AuthorizedLooseSource));
        }
        if (dropZones.TryGetDeliveryDropoff(out Vector2Int deliveryDropoff))
        {
            roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                deliveryDropoff, SpatialCellRole.AuthorizedLooseSource));
        }
        foreach (IWarehouseFacility warehouse in world.Warehouses
                     .Where(value => value != null)
                     .OrderBy(value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            if (warehouse is not BuildableObject building)
                continue;
            foreach (Vector2Int cell in building.buildPoses)
            {
                roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                    cell, SpatialCellRole.StorageBuffer));
            }
        }
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            overflowCell, SpatialCellRole.OverflowContainment));
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            burstCell, SpatialCellRole.AuthorizedLooseSource));
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            cropBurstCell, SpatialCellRole.AuthorizedLooseSource));
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            miningBurstCell, SpatialCellRole.AuthorizedLooseSource));
        layout = new DungeonSpaceLayoutSnapshot(
            roles,
            Array.Empty<Vector2Int>(),
            cleanRunP95HaulDispatchAndDeliverySeconds: 15f,
            gameDaySeconds: GameDaySeconds);
        commonCheckpointJson = saves.ToJson(saves.Capture());
        commonCheckpointTime = clock.Time;
        commonCheckpointFrame = clock.FrameCount;
        Check(!string.IsNullOrWhiteSpace(commonCheckpointJson),
            "PAIRED_COMMON_CHECKPOINT", $"bytes={commonCheckpointJson.Length}");
    }

    private IEnumerator PrepareCropAndMiningFixtures(
        IReadOnlyList<Vector2Int> reachable,
        BuildingPlacementValidator placement)
    {
        CurrentPhase = "prepare-production-burst-authorities";
        BlueprintResearchRuntime research = progression.BlueprintResearch;
        research.State.Projects.Complete(
            new ResearchProjectId("research:agriculture:field"));
        research.State.Projects.Complete(
            new ResearchProjectId("research:agriculture:gathering"));
        research.State.Projects.Complete(
            new ResearchProjectId("research:mining:surface"));

        BuildingSO cropAsset = FindCropPlotAsset();
        bool cropBuildingUnlocked = cropAsset != null
            && research.State.UnlockBuilding(cropAsset.id);
        Check(cropAsset != null
                && (cropBuildingUnlocked
                    || research.State.IsBuildingUnlocked(cropAsset.id)),
            "PAIRED_CROP_BUILDING_RESEARCH_UNLOCKED",
            $"asset={cropAsset?.name ?? "missing"};"
            + $"buildingId={cropAsset?.id ?? -1};"
            + $"unlocked={cropAsset != null && research.State.IsBuildingUnlocked(cropAsset.id)}");
        HashSet<Vector2Int> reservedProducerCells = producerFacilityAsset
            .GetGridPosList(burstCell)
            .ToHashSet();
        List<Vector2Int> legalCropAnchors = new();
        Dictionary<string, int> cropPlacementFailures =
            new(StringComparer.Ordinal);
        int cropCandidates = 0;
        if (cropAsset != null)
        {
            foreach (Vector2Int candidate in reachable)
            {
                if (cropAsset.GetGridPosList(candidate)
                    .Any(cell => reservedProducerCells.Contains(cell)))
                    continue;
                cropCandidates++;
                if (placement.CanBuild(
                        grid, cropAsset, candidate, out string placementFailure))
                {
                    legalCropAnchors.Add(candidate);
                    continue;
                }

                string reason = string.IsNullOrWhiteSpace(placementFailure)
                    ? "unspecified"
                    : placementFailure.Trim();
                cropPlacementFailures[reason] =
                    cropPlacementFailures.TryGetValue(reason, out int count)
                        ? count + 1
                        : 1;
            }
        }
        Vector2Int? cropAnchor = legalCropAnchors
            .OrderByDescending(value => Mathf.Min(
                Manhattan(value, burstCell),
                Manhattan(value, overflowCell)))
            .ThenBy(value => value.x)
            .ThenBy(value => value.y)
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        string cropPlacementDetail = string.Join(
            "|",
            cropPlacementFailures
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => $"{value.Key}:{value.Value}"));
        Check(cropAsset != null && cropAnchor.HasValue,
            "PAIRED_CROP_PLOT_PLACEMENT",
            $"asset={cropAsset?.name ?? "missing"};anchor={cropAnchor};"
            + $"reachable={reachable.Count};candidates={cropCandidates};"
            + $"legal={legalCropAnchors.Count};rejections={cropPlacementDetail}");
        if (cropAsset == null || !cropAnchor.HasValue)
            yield break;

        bool cropPlotPlaced = livePlacementService.TryPlaceBuildingImmediateUnchecked(
            cropAsset,
            cropAnchor.Value,
            chargeCost: false,
            out string cropPlacementFailure);
        Check(cropPlotPlaced,
            "PAIRED_CROP_PLOT_PRODUCTION_PLACEMENT",
            $"asset={cropAsset.name};anchor={cropAnchor.Value};"
            + $"failure={cropPlacementFailure}");
        if (!cropPlotPlaced)
            yield break;
        for (int frame = 0; frame < 2; frame++)
            yield return null;
        fixtureCropPlot = world.Buildings
            .OfType<Facility>()
            .SingleOrDefault(value => value != null
                && value.BuildingData == cropAsset
                && value.centerPos == cropAnchor.Value);
        Check(fixtureCropPlot != null,
            "PAIRED_CROP_PLOT_LIVE_REGISTRATION",
            $"asset={cropAsset.name};anchor={cropAnchor.Value};"
            + $"registered={fixtureCropPlot != null}");
        if (fixtureCropPlot == null)
            yield break;
        cropPlotId = fixtureCropPlot.RequirePersistentInstanceId().Value;
        cropBurstCell = fixtureCropPlot.centerPos;
        for (int frame = 0; frame < 4; frame++)
            yield return null;

        cropPlots.Restore(cropPlots.BuildRestore(cropPlots.Capture()));
        bool cropSelected = cropPlots.TrySetCrop(
            fixtureCropPlot, CropId, out string cropMessage);
        cropPlots.Tick();
        CropPlotSnapshot waiting = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(value.PlotId, cropPlotId, StringComparison.Ordinal));
        Check(cropSelected && waiting != null
                && waiting.Phase == CropPlotPhase.WaitingForMaterials
                && waiting.RequiredMaterials.Count > 0,
            "PAIRED_CROP_CYCLE_SELECTED",
            $"selected={cropSelected};message={cropMessage};plot={cropPlotId};"
            + $"phase={waiting?.Phase};materials={waiting?.RequiredMaterials.Count ?? 0}");
        if (!cropSelected || waiting == null
            || waiting.Phase != CropPlotPhase.WaitingForMaterials
            || waiting.RequiredMaterials.Count == 0)
            yield break;

        bool cropDefined = economyCatalog.TryGetCrop(
            CropId, out CropDefinitionSO crop);
        SeedLotState fixtureSeedLot = cropDefined
            ? FindSeedLot(crop.SeedItemId, CropId)
            : null;
        Check(cropDefined && fixtureSeedLot != null,
            "PAIRED_CROP_SEED_LOT_AUTHORITY",
            $"crop={CropId};seedItem={crop?.SeedItemId ?? "missing"};"
            + $"seedCrop={fixtureSeedLot?.cropId ?? "missing"};"
            + $"genome={fixtureSeedLot?.cultivarGenomeId ?? "missing"}");
        if (!cropDefined || fixtureSeedLot == null)
            yield break;

        int releasedRequests = itemTransfers.ReleaseDestination(
            waiting.MaterialDestinationId,
            fixtureCropPlot.centerPos);
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            bool isSeedLot = string.Equals(
                material.Key,
                crop.SeedItemId,
                StringComparison.Ordinal);
            int spawnedQuantity;
            bool spawned = isSeedLot
                ? itemTransfers.TrySpawnItemWithComponents(
                    material.Key,
                    material.Value,
                    fixtureCropPlot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    new[] { SeedLotItemStateCodec.Encode(fixtureSeedLot) },
                    out spawnedQuantity)
                : items.SpawnItemAt(
                    material.Key,
                    material.Value,
                    fixtureCropPlot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    out spawnedQuantity);
            Check(spawned && spawnedQuantity == material.Value,
                "PAIRED_CROP_INPUT_PHYSICAL",
                $"item={material.Key};required={material.Value};"
                + $"spawned={spawnedQuantity};seedLot={isSeedLot};"
                + $"releasedRequests={releasedRequests}");
            if (!spawned || spawnedQuantity != material.Value)
                yield break;
        }

        cropPlots.Tick();
        bool sowAvailable = cropPlots.TryGetWork(
            fixtureCropPlot,
            BuiltInWorkTypeIds.Sow,
            out CropPlotWorkSnapshot sow) && sow.Available;
        bool sowed = sowAvailable && cropPlots.ApplyWork(
            fixtureCropPlot,
            BuiltInWorkTypeIds.Sow,
            sow.RequiredWork,
            out bool sowCompleted) && sowCompleted;
        Check(sowed, "PAIRED_CROP_SOW_PRODUCTION_COMMAND",
            $"available={sowAvailable};reason={sow.UnavailableReason};completed={sowed}");
        if (!sowed)
            yield break;

        DungeonCropPlotSaveData growingSave = cropPlots.Capture();
        CropPlotSaveData growing = growingSave.plots.FirstOrDefault(value =>
            string.Equals(value.buildingInstanceId, cropPlotId, StringComparison.Ordinal));
        Check(growing != null && growing.phase == CropPlotPhase.Growing,
            "PAIRED_CROP_GROWING_AUTHORITY",
            $"plot={cropPlotId};phase={growing?.phase}");
        if (growing == null || growing.phase != CropPlotPhase.Growing)
            yield break;
        growing.growthHours = crop.GrowthHours;
        cropPlots.Restore(cropPlots.BuildRestore(growingSave));
        cropPlots.Tick();

        bool harvestReady = cropPlots.TryGetWork(
            ResolveCropPlot(),
            BuiltInWorkTypeIds.Harvest,
            out CropPlotWorkSnapshot harvest) && harvest.Available;
        cropBurstItemId = crop?.HarvestItemId ?? string.Empty;
        Check(harvestReady && cropDefined
                && !string.IsNullOrWhiteSpace(cropBurstItemId),
            "PAIRED_CROP_HARVEST_READY_CHECKPOINT",
            $"ready={harvestReady};reason={harvest.UnavailableReason};"
            + $"cropDefined={cropDefined};item={cropBurstItemId};cell={cropBurstCell}");
        if (!harvestReady || !cropDefined
            || string.IsNullOrWhiteSpace(cropBurstItemId))
            yield break;

        HashSet<Vector2Int> reachablePositions = reachable.ToHashSet();
        var miningCandidate = worldResources.Nodes
            .Where(value => value != null)
            .Select(value =>
            {
                bool available = worldResources.TryGetWork(
                    value,
                    BuiltInWorkTypeIds.Quarry,
                    out WorldResourceWorkSnapshot snapshot) && snapshot.Available;
                return new
                {
                    Node = value,
                    Host = value.GetComponent<BuildableObject>(),
                    Snapshot = snapshot,
                    Available = available
                };
            })
            .Where(value => value.Available
                && value.Host != null
                && HasReachablePickupStand(
                    value.Host.centerPos,
                    reachablePositions))
            .OrderBy(value => value.Snapshot.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .FirstOrDefault();
        WorldResourceNode miningNode = miningCandidate?.Node;
        BuildableObject miningHost = miningCandidate?.Host;
        miningRecipeId = miningCandidate?.Snapshot.RecipeId ?? string.Empty;
        bool miningRecipeDefined = economyCatalog.TryGetRecipe(
            miningRecipeId, out ProductionRecipeSO miningRecipe);
        ProductionOutputDefinition deterministicMiningOutput = miningRecipe?.Outputs
            .Where(value => value != null
                && value.Probability >= 1f
                && value.Amount > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        miningNodeId = miningNode?.NodeId ?? string.Empty;
        miningBurstItemId = deterministicMiningOutput?.ItemId ?? string.Empty;
        miningBurstCell = miningHost?.centerPos ?? default;
        Check(miningNode != null && miningHost != null && miningRecipeDefined
                && deterministicMiningOutput != null
                && !string.IsNullOrWhiteSpace(miningBurstItemId),
            "PAIRED_MINING_BURST_READY_CHECKPOINT",
            $"node={miningNodeId};host={miningHost != null};recipe={miningRecipeId}:"
            + $"{miningRecipeDefined};"
            + $"item={miningBurstItemId};cell={miningBurstCell};candidates="
            + string.Join(",", worldResources.Nodes
                .Where(value => value != null)
                .Select(value => value.GetComponent<BuildableObject>())
                .Where(value => value != null)
                .OrderBy(value => value.centerPos.x)
                .ThenBy(value => value.centerPos.y)
                .Select(value => value.centerPos + ":"
                    + HasReachablePickupStand(value.centerPos, reachablePositions))));
    }

    private static bool HasReachablePickupStand(
        Vector2Int itemPosition,
        ISet<Vector2Int> reachable)
    {
        return reachable != null
            && (reachable.Contains(itemPosition)
                || reachable.Contains(itemPosition + Vector2Int.left)
                || reachable.Contains(itemPosition + Vector2Int.right));
    }

    private void IsolatePreexistingLogistics()
    {
        CurrentPhase = "isolate-preexisting-logistics";
        WorldItemStackSnapshot[] isolated = items.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && (value.State is WorldItemStackState.Loose
                        or WorldItemStackState.FacilityOutputBuffer
                    || value.State == WorldItemStackState.Stored
                        && value.HasDestinationPosition
                        && !string.IsNullOrWhiteSpace(value.DestinationId)
                        && !string.IsNullOrWhiteSpace(
                            value.SourceStorageDestinationId)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        int clearedReservations = 0;
        int forbidden = 0;
        foreach (WorldItemStackSnapshot stack in isolated)
        {
            if (stack.ReservedQuantity > 0 && items.TryClearReservation(stack.StackId))
                clearedReservations++;
            if (items.SetForbidden(stack.StackId, true))
                forbidden++;
        }
        int remainingReservations = items.GetAllStacks().Sum(value =>
            value?.ReservedQuantity ?? 0);
        Check(forbidden == isolated.Length && remainingReservations == 0,
            "PAIRED_PREEXISTING_LOGISTICS_ISOLATED",
            $"candidates={isolated.Length};forbidden={forbidden};"
            + $"clearedReservations={clearedReservations};"
            + $"remainingReservations={remainingReservations}");
    }

    private IEnumerator RunSeed(int seed)
    {
        CurrentPhase = $"seed-{seed}-checkpoint";
        yield return Restore(
            commonCheckpointJson,
            commonCheckpointTime,
            commonCheckpointFrame);
        if (failures.Count > 0)
            yield break;
        randomProvider.Reseed(seed);
        string seedCheckpoint = saves.ToJson(saves.Capture());
        float seedCheckpointTime = clock.Time;
        int seedCheckpointFrame = clock.FrameCount;
        foreach (string arm in new[]
                 {
                     "cleanRepeatA", "cleanRepeatB", "faultControl", "clutterStress"
                 })
        {
            int failuresBeforeRestore = failures.Count;
            yield return Restore(
                seedCheckpoint,
                seedCheckpointTime,
                seedCheckpointFrame);
            if (failures.Count > failuresBeforeRestore)
                yield break;
            string startKey = $"{seed}|{arm}";
            armStartRandomHashes[startKey] = CaptureRandomHash(
                randomDiagnostics.Capture());
            string startSemanticText = CaptureSemanticText();
            armStartSemanticTexts[startKey] = startSemanticText;
            armStartSemanticHashes[startKey] = HashText(startSemanticText);
            int failuresBeforeArm = failures.Count;
            yield return RunArm(seed, arm);
            if (failures.Count > failuresBeforeArm)
                yield break;
        }
        ValidateCausalCone(seed);
    }

    private IEnumerator RunArm(int seed, string arm)
    {
        currentBurstProbe = null;
        CurrentPhase = $"seed-{seed}-{arm}-warmup";
        Time.timeScale = VerificationTimeScale;
        yield return ObserveDuration(seed, arm, -1, WarmupSeconds, false);
        if (failures.Count > 0)
            yield break;

        bool faultArm = arm is "faultControl" or "clutterStress";
        PrepareActorsForArmMeasurementBoundary();
        if (failures.Count > 0)
            yield break;
        if (!faultArm)
            ResumeAllMeasuredActors();
        string eventHash = $"clean:{seed}:none";
        if (faultArm)
        {
            IWarehouseFacility warehouse = ResolveWarehouse();
            CharacterActor faultActor = ResolveActor(faultActorId);
            CounterfactualRandomKey key = new(
                seed, ScenarioId, "haul-burst-and-downed", faultActorId, 0, 0);
            DeterministicRandomSequence sequence = key.CreateSequence();
            BurstProducerKind producerKind = SelectBurstProducer(seed);
            int burstQuantity = producerKind == BurstProducerKind.FacilityOutput
                ? 6 + sequence.NextInt(0, 3)
                : 0;
            eventHash = HashText(
                $"{seed}|{ScenarioId}|{faultActorId}|{producerKind}|"
                + $"{burstQuantity}|{sequence.State}");
            Vector2Int interventionSourceCell = producerKind switch
            {
                BurstProducerKind.CropHarvest => cropBurstCell,
                BurstProducerKind.Mining => miningBurstCell,
                _ => burstCell
            };
            HashSet<string> interventionStackIdsBefore = items.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && value.Position == interventionSourceCell)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            if (arm == "clutterStress")
            {
                DungeonItemDefinition fillDefinition = itemCatalog.All
                    .Where(candidate => candidate != null
                        && candidate.StockCategory == StockCategory.General
                        && candidate.MaxStack > 1)
                    .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        "Paired clutter requires one stackable General item.");
                long unitMassGrams = warehouseMassQuery
                    .GetDefinitionUnitMassGrams(fillDefinition.ItemId);
                long targetMassGrams = warehouse.Inventory.MaxMassGrams * 9L / 10L;
                long missingMassGrams = Math.Max(
                    0L,
                    targetMassGrams - warehouse.Inventory.StoredMassGrams);
                int missingQuantity = missingMassGrams == 0L
                    ? 0
                    : checked((int)((missingMassGrams + unitMassGrams - 1L)
                        / unitMassGrams));
                if (missingQuantity > 0)
                {
                    bool filled = items.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.General,
                        missingQuantity,
                        out int spawned);
                    long storedMassGrams = warehouse.Inventory.StoredMassGrams;
                    Check(filled
                            && spawned == missingQuantity
                            && storedMassGrams >= targetMassGrams
                            && storedMassGrams <= warehouse.Inventory.MaxMassGrams,
                        "PAIRED_STORAGE_NINETY_PERCENT",
                        $"seed={seed};targetMassGrams={targetMassGrams};"
                        + $"unitMassGrams={unitMassGrams};"
                        + $"requested={missingQuantity};spawned={spawned};"
                        + $"storedMassGrams={storedMassGrams};"
                        + $"maxMassGrams={warehouse.Inventory.MaxMassGrams};"
                        + $"totalQuantity={warehouse.Inventory.TotalStock}");
                }
            }

            if (producerKind == BurstProducerKind.FacilityOutput)
            {
            BuildingPlacementValidator producerPlacement = new();
            string producerPlacementFailure = "producer asset missing";
            bool producerCanBuild = producerFacilityAsset != null
                && producerPlacement.CanBuild(
                    grid,
                    producerFacilityAsset,
                    burstCell,
                    out producerPlacementFailure);
            Check(producerCanBuild,
                "PAIRED_INTERVENTION_PRODUCER_PLACEMENT",
                $"seed={seed};arm={arm};cell={burstCell};failure={producerPlacementFailure}");
            if (!producerCanBuild)
                yield break;
            GameObject producerObject = new($"QA_V27_Paired_Food_Producer_{arm}");
            fixtureProducerFacility = producerObject.AddComponent<Facility>();
            Inject(producerObject);
            fixtureProducerFacility.SetGrid(grid);
            fixtureProducerFacility.Initialization(producerFacilityAsset, burstCell);
            producerObject.transform.position = grid.GetWorldPos(burstCell);
            producerFacilityId = fixtureProducerFacility.RequirePersistentInstanceId().Value;
            for (int publicationFrame = 0; publicationFrame < 4; publicationFrame++)
                yield return null;
            bool producerPublished = world.Buildings.Any(value => value != null
                && value.PersistentInstanceId.Value == producerFacilityId);
            Check(producerPublished,
                "PAIRED_INTERVENTION_PRODUCER_PUBLISHED",
                $"seed={seed};arm={arm};facility={producerFacilityId};cell={burstCell}");
            if (!producerPublished)
                yield break;

            IWarehouseFacility inputWarehouse = ResolveProductionInputWarehouse();
            Check(inputWarehouse?.Inventory != null
                    && inputWarehouse.Inventory.CanStore(StockCategory.Food, burstQuantity),
                "PAIRED_PRODUCTION_INPUT_CAPACITY",
                $"seed={seed};arm={arm};quantity={burstQuantity};"
                + $"warehouse={productionInputWarehouseId};stock={inputWarehouse?.Inventory?.TotalStock ?? -1}");
            if (inputWarehouse?.Inventory == null
                || !inputWarehouse.Inventory.CanStore(StockCategory.Food, burstQuantity))
                yield break;
            bool inputSeeded = items.SpawnStockInWarehouse(
                inputWarehouse,
                StockCategory.Food,
                burstQuantity,
                out int seededInput);
            Check(inputSeeded && seededInput == burstQuantity,
                "PAIRED_PRODUCTION_INPUT_PHYSICAL",
                $"seed={seed};arm={arm};requested={burstQuantity};seeded={seededInput}");
            if (!inputSeeded || seededInput != burstQuantity)
                yield break;

            for (int publicationFrame = 0; publicationFrame < 2; publicationFrame++)
                yield return null;
            BuildableObject producer = ResolveProducerFacility();
            bool productionReady = survivalFoodQuery.HasSurvivalWorkAvailable(
                producer,
                BuiltInWorkTypeIds.Cook);
            Check(productionReady,
                "PAIRED_PRODUCTION_INPUT_PUBLISHED",
                $"seed={seed};arm={arm};producer={producerFacilityId};"
                + $"inputWarehouse={productionInputWarehouseId};quantity={burstQuantity}");
            if (!productionReady)
                yield break;

            currentBurstProbe = new ArmBurstProbe(
                BurstProducerKind.FacilityOutput,
                FacilityBurstItemId,
                burstCell,
                burstQuantity,
                CountItemQuantity(FacilityBurstItemId),
                CountStoredItemQuantity(FacilityBurstItemId),
                CountCarriedItemQuantity(FacilityBurstItemId));
            int produced = 0;
            DomainFailure productionFailure = default;
            for (int unit = 0; unit < burstQuantity; unit++)
            {
                if (!survivalFood.TryApplySurvivalWork(
                        faultActor.BuildingVisitor,
                        producer,
                        BuiltInWorkTypeIds.Cook,
                        out int cooked,
                        out productionFailure))
                    break;
                produced = checked(produced + cooked);
            }
            int looseProduced = items.GetAllStacks()
                .Where(value => value != null
                    && value.Position == burstCell
                    && value.State == WorldItemStackState.Loose
                    && string.Equals(value.ItemId, FacilityBurstItemId, StringComparison.Ordinal))
                .Sum(value => value.Quantity);
            bool productionBurstExact = produced == burstQuantity
                    && CountItemQuantity(FacilityBurstItemId) - currentBurstProbe.TotalBefore
                        == burstQuantity
                    && looseProduced >= burstQuantity;
            Check(productionBurstExact,
                "PAIRED_KEYED_PRODUCTION_BURST_APPLIED",
                $"seed={seed};arm={arm};requested={burstQuantity};produced={produced};"
                + $"looseAtProducer={looseProduced};cell={burstCell};"
                + $"failure={productionFailure.Code}:"
                + string.Join(",", productionFailure.Parameters.ToArray()));
            if (!productionBurstExact)
                yield break;
            productionBurstArmCount++;
            facilityBurstArmCount++;
            }
            else
            {
                string itemId = producerKind == BurstProducerKind.CropHarvest
                    ? cropBurstItemId
                    : miningBurstItemId;
                Vector2Int sourceCell = producerKind == BurstProducerKind.CropHarvest
                    ? cropBurstCell
                    : miningBurstCell;
                int totalBefore = CountItemQuantity(itemId);
                int storedBefore = CountStoredItemQuantity(itemId);
                int carriedBefore = CountCarriedItemQuantity(itemId);
                int sourceLooseBefore = CountLooseAt(itemId, sourceCell);
                bool commandApplied;
                bool cycleCompleted = false;
                string commandDetail;
                if (producerKind == BurstProducerKind.CropHarvest)
                {
                    BuildableObject plot = ResolveCropPlot();
                    CropPlotWorkSnapshot harvest = default;
                    bool available = plot != null && cropPlots.TryGetWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        out harvest) && harvest.Available;
                    commandApplied = available && cropPlots.ApplyWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        harvest.RequiredWork,
                        faultActor,
                        out cycleCompleted);
                    commandDetail = $"plot={cropPlotId};available={available};"
                        + $"completed={cycleCompleted};reason={harvest.UnavailableReason}";
                }
                else
                {
                    WorldResourceNode node = ResolveMiningNode();
                    WorldResourceWorkSnapshot quarry = default;
                    bool available = node != null && worldResources.TryGetWork(
                        node,
                        BuiltInWorkTypeIds.Quarry,
                        out quarry) && quarry.Available;
                    commandApplied = available && worldResources.ApplyWork(
                        node,
                        BuiltInWorkTypeIds.Quarry,
                        quarry.RequiredWork,
                        out cycleCompleted);
                    commandDetail = $"node={miningNodeId};available={available};"
                        + $"completed={cycleCompleted};reason={quarry.UnavailableReason}";
                }

                int produced = CountItemQuantity(itemId) - totalBefore;
                int sourceLooseDelta = CountLooseAt(itemId, sourceCell)
                    - sourceLooseBefore;
                currentBurstProbe = new ArmBurstProbe(
                    producerKind,
                    itemId,
                    sourceCell,
                    produced,
                    totalBefore,
                    storedBefore,
                    carriedBefore);
                bool productionBurstExact = commandApplied && cycleCompleted
                    && produced > 0 && sourceLooseDelta == produced;
                Check(productionBurstExact,
                    producerKind == BurstProducerKind.CropHarvest
                        ? "PAIRED_CROP_HARVEST_BURST_PRODUCTION"
                        : "PAIRED_MINING_BURST_PRODUCTION",
                    $"seed={seed};arm={arm};item={itemId};produced={produced};"
                    + $"sourceLooseDelta={sourceLooseDelta};cell={sourceCell};"
                    + commandDetail);
                if (!productionBurstExact)
                    yield break;
                eventHash = HashText(
                    $"{seed}|{ScenarioId}|{faultActorId}|{producerKind}|"
                    + $"{produced}|{sequence.State}");
                productionBurstArmCount++;
                if (producerKind == BurstProducerKind.CropHarvest)
                    cropHarvestBurstArmCount++;
                else
                    miningBurstArmCount++;
            }

            WorldItemStackSnapshot[] producedStacks = items.GetAllStacks()
                .Where(value => value != null
                    && !interventionStackIdsBefore.Contains(value.StackId)
                    && value.Position == currentBurstProbe.SourceCell
                    && value.State == WorldItemStackState.Loose
                    && string.Equals(value.ItemId, currentBurstProbe.ItemId, StringComparison.Ordinal))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            WorldItemStackSnapshot[] ancillaryProducedStacks = items.GetAllStacks()
                .Where(value => value != null
                    && !interventionStackIdsBefore.Contains(value.StackId)
                    && value.Position == currentBurstProbe.SourceCell
                    && value.State == WorldItemStackState.Loose
                    && !string.Equals(
                        value.ItemId,
                        currentBurstProbe.ItemId,
                        StringComparison.Ordinal))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            bool ancillaryIsolated = true;
            foreach (WorldItemStackSnapshot ancillary in ancillaryProducedStacks)
                ancillaryIsolated &= items.SetForbidden(ancillary.StackId, true);
            Check(ancillaryIsolated,
                "PAIRED_PRODUCTION_ANCILLARY_OUTPUT_ISOLATED",
                $"seed={seed};arm={arm};producer={currentBurstProbe.ProducerKind};"
                + $"count={ancillaryProducedStacks.Length};"
                + string.Join(",", ancillaryProducedStacks.Select(value =>
                    $"{value.StackId}:{value.ItemId}:{value.Quantity}")));
            if (!ancillaryIsolated)
                yield break;
            bool prioritized = producedStacks.Length > 0;
            foreach (WorldItemStackSnapshot stack in producedStacks)
                prioritized &= items.PrioritizeHaul(stack.StackId);
            Check(prioritized,
                "PAIRED_PRODUCTION_BURST_HAUL_PRIORITY",
                $"seed={seed};arm={arm};stacks={producedStacks.Length};"
                + string.Join(",", producedStacks.Select(value => value.StackId)));
            if (!prioritized)
                yield break;
            productionPriorityArmCount++;

            HashSet<string> producedStackIds = producedStacks
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            bool previewAvailable = haulPlanning.TryPreviewBestPlan(
                faultActor,
                out WorldItemHaulPlan previewPlan,
                out string previewFailure);
            bool previewSelectsBurst = previewAvailable
                && previewPlan?.ReservedStackQuantities.Any(value =>
                    producedStackIds.Contains(value.StackId)) == true;
            IEnumerable<string> previewStackIds = previewPlan?.ReservedStackQuantities
                .Select(value => value.StackId)
                ?? Array.Empty<string>();
            Check(previewSelectsBurst,
                "PAIRED_PRODUCTION_BURST_HAUL_PLAN_PREFLIGHT",
                $"seed={seed};arm={arm};producer={currentBurstProbe.ProducerKind};"
                + $"available={previewAvailable};failure={previewFailure};"
                + $"planDestination={previewPlan?.PrimaryDestinationId};"
                + $"planStacks={string.Join(",", previewStackIds)};"
                + $"burstStacks={string.Join(",", producedStackIds.OrderBy(value => value, StringComparer.Ordinal))}");
            if (!previewSelectsBurst)
                yield break;

            faultActor.SetAiPaused(false);
            faultActor.Brain?.RequestImmediateReplan(clearFailures: true);
            scheduler.ResetDecisionQueueForDiagnostics();
            WorldItemHaulPlanLeg firstPickupLeg = previewPlan.PickupLegs
                .First(value => value.IsValid);
            float pickupMoveSpeed = Mathf.Max(0.1f, faultActor.GetMoveSpeed());
            float directPickupTravelSeconds = Vector3.Distance(
                    grid.GetWorldPos(faultActor.GetNowXY()),
                    grid.GetWorldPos(firstPickupLeg.PickupStandPosition))
                / pickupMoveSpeed;
            float pickupGameBudget = PickupSearchAndSchedulingHeadroomSeconds
                + directPickupTravelSeconds * 2f;
            float pickupGameDeadline = clock.Time + pickupGameBudget;
            float pickupRealtimeDeadline = Time.realtimeSinceStartup + 15f;
            bool haulExecutionObserved = false;
            float acceleratedCaptureDeltaTime = Time.captureDeltaTime;
            // The production path broker advances an urgent exact search in
            // bounded slices over as many as 240 rendered frames. Keep the
            // 32x simulation rate, but provide enough rendered-frame density
            // for the real Brain -> AIHaul -> broker -> movement path to
            // complete before injecting the hauler fault.
            Time.captureDeltaTime = PickupCaptureDeltaTime;
            while (clock.Time < pickupGameDeadline
                && Time.realtimeSinceStartup < pickupRealtimeDeadline
                && CountActorCarriedQuantity(
                    faultActor, currentBurstProbe.ItemId) == 0)
            {
                AbilityMove pickupMove = faultActor.GetComponent<AbilityMove>();
                AbilityHaul pickupHaul = faultActor.GetComponent<AbilityHaul>();
                if (!haulExecutionObserved && pickupHaul?.IsHauling == true)
                {
                    // Candidate discovery and the committed haul each own an
                    // incremental path-search boundary.  A remote quarry may
                    // legitimately consume the scheduling headroom before the
                    // AIHaul epoch starts; begin the physical route allowance
                    // at that typed ownership transition instead of letting
                    // candidate-search time erase movement time.
                    haulExecutionObserved = true;
                    pickupGameDeadline = Mathf.Max(
                        pickupGameDeadline,
                        clock.Time
                            + PickupSearchAndSchedulingHeadroomSeconds
                            + directPickupTravelSeconds * 2f);
                    // Candidate discovery and committed movement are two
                    // independent production phases. The game-time budget was
                    // already phased above; give the committed movement its
                    // own bounded realtime watchdog as well. Otherwise a
                    // candidate selected on the final discovery frame is
                    // cancelled by the harness immediately after its first
                    // coroutine yield. The post-fault 90-game-second recovery
                    // SLA remains unchanged.
                    pickupRealtimeDeadline = Time.realtimeSinceStartup + 15f;
                }
                bool followingResolvedPath = string.Equals(
                        pickupMove?.ActiveMovementOperationOwnerForDiagnostics,
                        "raw-path",
                        StringComparison.Ordinal)
                    && pickupHaul?.CurrentExecutionStage.StartsWith(
                        "경로 이동 중",
                        StringComparison.Ordinal) == true;
                Time.captureDeltaTime = followingResolvedPath
                    ? acceleratedCaptureDeltaTime
                    : PickupCaptureDeltaTime;
                EnsureVerificationTimeScale();
                yield return null;
            }
            Time.captureDeltaTime = acceleratedCaptureDeltaTime;
            int carriedAtFault = CountActorCarriedQuantity(
                faultActor, currentBurstProbe.ItemId);
            Check(carriedAtFault > 0,
                "PAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP",
                $"seed={seed};arm={arm};actor={faultActorId};carried={carriedAtFault};"
                + $"position={faultActor.GetNowXY()};clock={clock.Time:0.###};"
                + $"pickupBudget={pickupGameBudget:0.###};"
                + $"haulObserved={haulExecutionObserved};"
                + $"directTravel={directPickupTravelSeconds:0.###};"
                + $"moveSpeed={pickupMoveSpeed:0.###};"
                + $"action={faultActor.Brain?.CurrentActionDebugLabel};"
                + $"actors={DescribeActors()};"
                + $"stacks=" + string.Join(",", items.GetAllStacks()
                    .Where(value => value != null
                        && string.Equals(
                            value.ItemId,
                            currentBurstProbe.ItemId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => $"{value.StackId}:{value.Quantity}:{value.State}@{value.Position}")));
            if (carriedAtFault <= 0)
                yield break;
            postPickupFaultArmCount++;
            faultActor.SetLifecycleState(CharacterLifecycleState.Downed);
            foreach (CharacterActor actor in LiveActors())
            {
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }

        for (int window = 0; window < 4; window++)
        {
            CurrentPhase = $"seed-{seed}-{arm}-window-{window}";
            WindowAccumulator accumulator = new();
            yield return ObserveWindow(seed, arm, window, eventHash, accumulator);
            if (failures.Count > 0)
                yield break;
        }

        CharacterActor restoredFault = ResolveActor(faultActorId);
        if (restoredFault != null
            && restoredFault.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            restoredFault.SetLifecycleState(CharacterLifecycleState.Active);
            restoredFault.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        CurrentPhase = $"seed-{seed}-{arm}-recovery";
        yield return ObserveDuration(seed, arm, 4, RecoverySeconds, true);
        if (currentBurstProbe != null)
        {
            BurstState finalBurst = CaptureBurstState(currentBurstProbe);
            Check(finalBurst.QuantityConserved,
                "PAIRED_BURST_RECOVERY_CONSERVED",
                $"seed={seed};arm={arm};expected={currentBurstProbe.Quantity};"
                + $"totalDelta={finalBurst.TotalDelta};delivered={finalBurst.Delivered};"
                + $"outstanding={finalBurst.Outstanding}");
            Check(finalBurst.Delivered >= currentBurstProbe.Quantity
                    && finalBurst.Outstanding == 0,
                "PAIRED_BURST_RECOVERY_COMPLETED",
                $"seed={seed};arm={arm};expected={currentBurstProbe.Quantity};"
                + $"delivered={finalBurst.Delivered};outstanding={finalBurst.Outstanding};"
                + $"sourceLoose={finalBurst.SourceLoose};sourceReserved={finalBurst.SourceReserved};"
                + $"carried={finalBurst.CarriedDelta};actors={DescribeActors()}");
        }
        FloorClutterAssessment recovered = clutter.Capture(
            grid, layout, WarmupSeconds + 4 * WindowSeconds + RecoverySeconds);
        int recoveredHeadroom = CaptureRuntimeHeadroomPermille();
        floorRows.Add(new FloorRow(
            seed,
            arm,
            4,
            recovered,
            clutterCellSeconds: 0,
            runtimeHeadroomPermille: recoveredHeadroom,
            runtimeErosionCells: lastRuntimeHeadroomErosionCount,
            runtimeErosionDetail: lastRuntimeHeadroomErosionDetail,
            isRecovery: true));
        bool recoveredClean = recovered.PersistentCount == 0
            && recovered.ImmediateFailureCount == 0;
        string recoveryDetail = recoveredClean
            ? $"seed={seed};arm={arm};persistent=0;immediate=0"
            : $"seed={seed};arm={arm};persistent={recovered.PersistentCount};"
              + $"immediate={recovered.ImmediateFailureCount};loose={recovered.LooseStackCount};"
              + $"outside={DescribeOutside(recovered)};actors={DescribeActors()}";
        if (Focused && !recoveredClean)
        {
            focusedDeferredFailures.Add(
                "PAIRED_ARM_RECOVERED:" + recoveryDetail);
        }
        else
        {
            Check(recoveredClean, "PAIRED_ARM_RECOVERED", recoveryDetail);
        }
    }

    private IEnumerator ObserveWindow(
        int seed,
        string arm,
        int window,
        string eventHash,
        WindowAccumulator accumulator)
    {
        float elapsed = 0f;
        CharacterActor[] startActors = LiveActors();
        Dictionary<string, long> replanStart = startActors.ToDictionary(
            ActorId, value => value.Brain?.RuntimeImmediateReplanCount ?? 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> pathStart = startActors.ToDictionary(
            ActorId,
            value => value.GetComponent<AbilityMove>()?.RuntimeActionPathReplanCount ?? 0L,
            StringComparer.Ordinal);
        Dictionary<string, bool> stepAsideLive = new(StringComparer.Ordinal);
        while (elapsed < WindowSeconds)
        {
            EnsureVerificationTimeScale();
            float delta = Mathf.Min(Mathf.Max(clock.DeltaTime, 0f), WindowSeconds - elapsed);
            elapsed += delta;
            SampleActors(seed, arm, delta, accumulator, stepAsideLive);
            FloorClutterAssessment current = clutter.Capture(
                grid, layout, WarmupSeconds + window * WindowSeconds + elapsed);
            accumulator.ClutterCellSeconds += Mathf.RoundToInt(
                current.OutsideContainment.Count * delta);
            if (current.ImmediateFailureCount > 0)
                accumulator.ImmediateFailures += current.ImmediateFailureCount;
            if (Focused && arm is "cleanRepeatA" or "cleanRepeatB")
            {
                string key = $"{seed}|{arm}|{window}";
                if (!focusedFrameTraces.TryGetValue(key, out List<string> trace))
                {
                    trace = new List<string>();
                    focusedFrameTraces.Add(key, trace);
                }
                trace.Add(CaptureFocusedFrameTrace(elapsed));
            }
            yield return null;
        }

        CharacterActor[] endActors = LiveActors();
        accumulator.Replans = checked((int)endActors.Sum(actor =>
            Math.Max(0L, (actor.Brain?.RuntimeImmediateReplanCount ?? 0L)
                - (replanStart.TryGetValue(ActorId(actor), out long start) ? start : 0L))));
        accumulator.Replans += checked((int)endActors.Sum(actor =>
            Math.Max(0L,
                (actor.GetComponent<AbilityMove>()?.RuntimeActionPathReplanCount ?? 0L)
                - (pathStart.TryGetValue(ActorId(actor), out long start) ? start : 0L))));
        string semantic = CaptureSemanticHash();
        IReadOnlyList<RandomStreamDiagnosticSnapshot> random = randomDiagnostics.Capture();
        string randomHash = CaptureRandomHash(random);
        randomByArmWindow[$"{seed}|{arm}|{window}"] = random;
        PairedRunWindowResult row = new(
            seed,
            arm,
            window,
            accumulator.TravelMilliWu,
            accumulator.WaitMilliWu,
            accumulator.Replans,
            accumulator.StepAsideCount,
            accumulator.ClutterCellSeconds,
            semantic,
            randomHash,
            eventHash,
            accumulator.DispatchWaitMilliWu,
            accumulator.ReservationWaitMilliWu,
            accumulator.FacilityAccessWaitMilliWu,
            accumulator.NoPathMilliWu,
            accumulator.BurstDeliveredQuantity,
            accumulator.BurstOutstandingQuantity,
            accumulator.BurstQuantityConserved);
        rows.Add(row);
        FloorClutterAssessment end = clutter.Capture(
            grid, layout, WarmupSeconds + (window + 1) * WindowSeconds);
        int windowHeadroom = CaptureRuntimeHeadroomPermille();
        floorRows.Add(new FloorRow(
            seed,
            arm,
            window,
            end,
            accumulator.ClutterCellSeconds,
            windowHeadroom,
            lastRuntimeHeadroomErosionCount,
            lastRuntimeHeadroomErosionDetail,
            false));
        Check(accumulator.ImmediateFailures == 0,
            "PAIRED_WINDOW_ACCESS_CLEAR",
            $"seed={seed};arm={arm};window={window};immediate={accumulator.ImmediateFailures}");
    }

    private IEnumerator ObserveDuration(
        int seed,
        string arm,
        int phase,
        float duration,
        bool recovery)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            EnsureVerificationTimeScale();
            elapsed += Mathf.Min(Mathf.Max(clock.DeltaTime, 0f), duration - elapsed);
            if (Focused && phase == -1
                && arm is "cleanRepeatA" or "cleanRepeatB")
            {
                string key = $"{seed}|{arm}|{phase}";
                if (!focusedFrameTraces.TryGetValue(key, out List<string> trace))
                {
                    trace = new List<string>();
                    focusedFrameTraces.Add(key, trace);
                }
                trace.Add(CaptureFocusedFrameTrace(elapsed));
            }
            yield return null;
        }
        _ = seed;
        _ = arm;
        _ = phase;
        _ = recovery;
    }

    private void SampleActors(
        int seed,
        string arm,
        float delta,
        WindowAccumulator accumulator,
        IDictionary<string, bool> stepAsideLive)
    {
        CharacterActor[] actors = LiveActors();
        foreach (CharacterActor actor in actors)
        {
            string id = ActorId(actor);
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            bool moving = move?.HasActiveMovementRoutineForDiagnostics == true;
            bool hauling = haul?.IsHauling == true;
            if (moving)
                accumulator.TravelMilliWu += Mathf.RoundToInt(delta * WorkMilliWuPerGameSecond);
            bool stepAside = string.Equals(
                actor.Brain?.CurrentActionDebugLabel,
                "길 비켜주기",
                StringComparison.Ordinal);
            bool wasStepAside = stepAsideLive.TryGetValue(id, out bool previous) && previous;
            if (stepAside && !wasStepAside)
                accumulator.StepAsideCount++;
            stepAsideLive[id] = stepAside;
            if (arm is "faultControl" or "clutterStress"
                && (hauling || moving && currentBurstProbe != null
                    && Manhattan(actor.GetNowXY(), currentBurstProbe.SourceCell) <= 3))
            {
                if (!affectedActorsBySeed.TryGetValue(seed, out HashSet<string> affected))
                {
                    affected = new HashSet<string>(StringComparer.Ordinal);
                    affectedActorsBySeed.Add(seed, affected);
                }
                affected.Add(id);
            }
        }
        SampleBurstWait(delta, accumulator, actors);
    }

    private void SampleBurstWait(
        float delta,
        WindowAccumulator accumulator,
        IReadOnlyList<CharacterActor> actors)
    {
        if (currentBurstProbe == null)
            return;
        BurstState state = CaptureBurstState(currentBurstProbe);
        accumulator.BurstDeliveredQuantity = state.Delivered;
        accumulator.BurstOutstandingQuantity = state.Outstanding;
        accumulator.BurstQuantityConserved &= state.QuantityConserved;
        if (state.Outstanding <= 0)
            return;

        long milliWu = Mathf.RoundToInt(delta * WorkMilliWuPerGameSecond);
        bool anyMoving = actors.Any(actor =>
        {
            bool ownsBurst = CountActorCarriedQuantity(
                    actor, currentBurstProbe.ItemId) > 0
                || state.SourceReserved > 0
                    && actor.GetComponent<AbilityHaul>()?.IsHauling == true;
            return ownsBurst
                && actor.GetComponent<AbilityMove>()
                    ?.HasActiveMovementRoutineForDiagnostics == true;
        });
        bool anyHauling = actors.Any(actor =>
            actor.GetComponent<AbilityHaul>()?.IsHauling == true
            && (CountActorCarriedQuantity(actor, currentBurstProbe.ItemId) > 0
                || state.SourceReserved > 0));
        bool noPath = actors.Any(actor =>
            actor.Brain?.LastActionFailure.Kind == AIActionFailureKind.NoPath
            && (actor.GetComponent<AbilityHaul>()?.IsHauling == true
                || state.SourceReserved == 0 && state.CarriedDelta == 0));
        if (anyMoving)
            return;
        accumulator.WaitMilliWu = checked(accumulator.WaitMilliWu + milliWu);
        if (noPath)
            accumulator.NoPathMilliWu = checked(accumulator.NoPathMilliWu + milliWu);
        else if (state.CarriedDelta > 0 && anyHauling)
            accumulator.FacilityAccessWaitMilliWu = checked(
                accumulator.FacilityAccessWaitMilliWu + milliWu);
        else if (state.SourceReserved > 0)
            accumulator.ReservationWaitMilliWu = checked(
                accumulator.ReservationWaitMilliWu + milliWu);
        else
            accumulator.DispatchWaitMilliWu = checked(
                accumulator.DispatchWaitMilliWu + milliWu);
    }

    private BurstState CaptureBurstState(ArmBurstProbe probe)
    {
        WorldItemStackSnapshot[] stacks = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    probe.ItemId,
                    StringComparison.Ordinal))
            .ToArray();
        int totalDelta = stacks.Sum(value => value.Quantity) - probe.TotalBefore;
        int storedDelta = stacks.Where(value => value.State == WorldItemStackState.Stored)
            .Sum(value => value.Quantity) - probe.StoredBefore;
        int carriedDelta = stacks.Where(value => value.State == WorldItemStackState.Carried)
            .Sum(value => value.Quantity) - probe.CarriedBefore;
        int sourceLoose = stacks.Where(value => value.Position == probe.SourceCell
                && value.State == WorldItemStackState.Loose)
            .Sum(value => value.Quantity);
        int sourceReserved = stacks.Where(value => value.Position == probe.SourceCell
                && value.State == WorldItemStackState.Loose)
            .Sum(value => value.ReservedQuantity);
        int delivered = Mathf.Clamp(storedDelta, 0, probe.Quantity);
        return new BurstState(
            totalDelta,
            sourceLoose,
            sourceReserved,
            Mathf.Max(0, carriedDelta),
            delivered,
            Mathf.Max(0, probe.Quantity - delivered),
            totalDelta == probe.Quantity);
    }

    private int CountItemQuantity(string itemId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private int CountStoredItemQuantity(string itemId) => items.GetAllStacks()
        .Where(value => value != null
            && value.State == WorldItemStackState.Stored
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private int CountLooseAt(string itemId, Vector2Int position) =>
        items.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.Loose
                && value.Position == position
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

    private int CountCarriedItemQuantity(string itemId) => LiveActors()
        .Sum(actor => CountActorCarriedQuantity(actor, itemId));

    private static int CountActorCarriedQuantity(
        CharacterActor actor,
        string itemId) => actor?.GetComponent<CharacterCarryInventory>()?.Items
        .Where(value => value != null
            && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.quantity) ?? 0;

    private void ValidateCausalCone(int seed)
    {
        HashSet<string> affected = affectedActorsBySeed.TryGetValue(
            seed, out HashSet<string> found)
            ? found
            : new HashSet<string>(StringComparer.Ordinal);
        affected.Add(faultActorId);
        for (int window = 0; window < 4; window++)
        {
            IReadOnlyList<RandomStreamDiagnosticSnapshot> control =
                randomByArmWindow[$"{seed}|faultControl|{window}"];
            IReadOnlyList<RandomStreamDiagnosticSnapshot> stress =
                randomByArmWindow[$"{seed}|clutterStress|{window}"];
            Dictionary<string, RandomStreamDiagnosticSnapshot> right = stress
                .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
            foreach (RandomStreamDiagnosticSnapshot left in control)
            {
                if (IsAffectedActorStream(left.StreamId, affected))
                    continue;
                if (!right.TryGetValue(left.StreamId, out RandomStreamDiagnosticSnapshot other))
                {
                    Fail("RNG_CROSS_TALK", $"seed={seed};window={window};missing={left.StreamId}");
                    return;
                }
                if (left.State != other.State || left.DrawCount != other.DrawCount)
                {
                    Fail("RNG_CROSS_TALK",
                        $"seed={seed};window={window};stream={left.StreamId};"
                        + $"control={left.State}/{left.DrawCount};stress={other.State}/{other.DrawCount}");
                    return;
                }
            }
        }
        Check(true, "PAIRED_RNG_CAUSAL_CONE",
            $"seed={seed};affectedActors={string.Join(",", affected.OrderBy(value => value, StringComparer.Ordinal))}");
    }

    private IEnumerator Restore(
        string json,
        float checkpointTime,
        int checkpointFrame)
    {
        clockDiagnostics.RebaseDeterministicCheckpointTime(
            checkpointTime,
            checkpointFrame);
        DungeonGameSaveData candidate = saves.FromJson(json);
        bool restored = saves.TryRestore(candidate, out DungeonGameRestoreReport report);
        Check(restored, "PAIRED_CHECKPOINT_RESTORE",
            restored
                ? $"sections={candidate.sections.Count}"
                : report == null
                    ? "failed:report-null"
                    : $"errors={string.Join(" | ", report.Errors)};"
                    + $"warnings={string.Join(" | ", report.Warnings)}");
        if (!restored)
            yield break;
        ApplyMeasurementIsolation(activateMeasuredActors: false);
        for (int frame = 0; frame < 6; frame++)
            yield return null;
        clockDiagnostics.RebaseDeterministicCheckpointTime(
            checkpointTime,
            checkpointFrame);
        ApplyMeasurementIsolation(activateMeasuredActors: true);
        // A full-world restore republishes the runtime collaborators used by
        // the scheduler. Re-assert the diagnostics composition after that
        // publication so the paired arms cannot silently fall back to the
        // frame-budgeted path broker while the scheduler flag remains stale.
        scheduler.ConfigureDeterministicSimulationForDiagnostics(true);
        characterSpawner.ConfigureDeterministicSimulationForDiagnostics(true);
        scheduler.ResetDeterministicSimulationCheckpointForDiagnostics();
        Check(scheduler.DeterministicSimulationForDiagnostics,
            "PAIRED_DETERMINISTIC_SCHEDULER_REBOUND_AFTER_RESTORE",
            $"checkpoint={checkpointTime:0.###}/{checkpointFrame}");
        world.TryGetGrid(out grid);
    }

    private IWarehouseFacility ResolveWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == warehouseId);

    private IWarehouseFacility ResolveOverflowWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == overflowWarehouseId);

    private IWarehouseFacility ResolveProductionInputWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == productionInputWarehouseId);

    private BuildableObject ResolveProducerFacility() => world.Buildings
        .Single(value => value != null
            && value.PersistentInstanceId.Value == producerFacilityId);

    private CharacterActor ResolveActor(string id) => world.AllCharacters
        .Select(CharacterActorCollection.GetCanonical)
        .FirstOrDefault(value => value != null
            && string.Equals(ActorId(value), id, StringComparison.Ordinal));

    private string CaptureSemanticHash() => HashText(CaptureSemanticText());

    private string CaptureSemanticText()
    {
        StringBuilder builder = new();
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null)
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            builder.Append("S|").Append(stack.StackId).Append('|')
                .Append(stack.ItemId).Append('|').Append(stack.Quantity).Append('|')
                .Append((int)stack.State).Append('|').Append(stack.Position.x)
                .Append(',').Append(stack.Position.y).Append('|')
                .Append(stack.DestinationId).Append('\n');
        }
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            Vector2Int position = actor.GetNowXY();
            builder.Append("A|").Append(ActorId(actor)).Append('|')
                .Append((int)actor.CurrentLifecycleState).Append('|')
                .Append(position.x).Append(',').Append(position.y).Append('|')
                .Append(actor.Brain?.RuntimeActionStartCount ?? 0L).Append('|')
                .Append(actor.Brain?.RuntimeImmediateReplanCount ?? 0L).Append('\n');
        }
        return builder.ToString();
    }

    private static string CaptureRandomHash(
        IEnumerable<RandomStreamDiagnosticSnapshot> snapshots) =>
        HashText(string.Join("\n", snapshots
            .OrderBy(value => value.StreamId, StringComparer.Ordinal)
            .Select(value => $"{value.StreamId}|{value.State}|{value.DrawCount}")));

    private void Finish()
    {
        if (finished)
            return;
        finished = true;
        try
        {
            clockDiagnostics?.DisableDeterministicCheckpointTime();
            if (saves != null && originalSave != null)
            {
                bool restored = saves.TryRestore(originalSave, out DungeonGameRestoreReport report);
                if (!restored)
                    failures.Add("ORIGINAL_WORLD_RESTORE:errors="
                        + string.Join(" | ", report?.Errors ?? Array.Empty<string>())
                        + ";warnings="
                        + string.Join(" | ", report?.Warnings ?? Array.Empty<string>()));
            }
        }
        catch (Exception exception)
        {
            failures.Add("ORIGINAL_WORLD_RESTORE:" + exception.Message);
        }
        try
        {
            if (debugModeConfigured && debugMode != null)
            {
                debugMode.SetCheat(
                    DungeonDebugCheat.FreezeNeeds,
                    originalFreezeNeeds);
                debugMode.SetCheat(
                    DungeonDebugCheat.FriendlyInvincible,
                    originalFriendlyInvincible);
                debugMode.SetCheat(
                    DungeonDebugCheat.PauseWildlifeAi,
                    originalPauseWildlifeAi);
            }
            if (developerModeConfigured && userSettings != null
                && userSettings.Current.developerMode != originalDeveloperMode)
            {
                userSettings.Update(value => value.developerMode = originalDeveloperMode);
            }
            if (gameSpeedConfigured && gameSpeed != null)
            {
                gameSpeed.SetSpeed(originalGameSpeed);
                gameSpeed.SetPaused(originalGamePause);
            }
            if (schedulerDiagnosticsConfigured && scheduler != null)
            {
                foreach (CharacterActor actor in LiveActors())
                    actor.Brain?.ConfigureLogisticsMeasurementForDiagnostics(false);
                scheduler.ConfigureDeterministicSimulationForDiagnostics(
                    originalSchedulerDeterministicMode);
            }
            if (spawnerDiagnosticsConfigured && characterSpawner != null)
            {
                characterSpawner.ConfigureDeterministicSimulationForDiagnostics(
                    originalSpawnerDiagnosticsPaused);
            }
        }
        catch (Exception exception)
        {
            failures.Add("ORIGINAL_DEBUG_STATE_RESTORE:" + exception.Message);
        }
        Application.logMessageReceived -= CaptureIssue;
        CurrentPhase = "finished";
        Time.timeScale = originalTimeScale;
        Time.captureDeltaTime = originalCaptureDeltaTime;
        Application.runInBackground = originalRunInBackground;
        WriteArtifacts();
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    private void WriteArtifacts()
    {
        int completedSeeds = rows.Select(value => value.Seed).Distinct().Count();
        if (!runCompleted)
        {
            failures.Add(
                $"PAIRED_RUN_INCOMPLETE:requiredSeeds={requiredSeedCount};"
                + $"completedSeeds={completedSeeds};windows={rows.Count};floorRows={floorRows.Count}");
        }
        bool passed = runCompleted && failures.Count == 0 && consoleIssues.Count == 0;
        string pairedCsv = BuildPairedCsv();
        string floorCsv = BuildFloorCsv();
        string sourceDigest = V27PairedClutterPlayModeVerifier
            .ComputeEvidenceSourceDigest();
        string report = $"RESULT={(passed ? "PASS" : "FAIL")}; seeds={completedSeeds};"
            + $" windows={rows.Count}; floorRows={floorRows.Count}; failures={failures.Count};"
            + $" consoleIssues={consoleIssues.Count}; sourceDigest={sourceDigest};"
            + $" pairedCsvSha256={HashText(pairedCsv)};"
            + $" floorCsvSha256={HashText(floorCsv)};\n"
            + BuildSuccessEvidence(passed, completedSeeds)
            + string.Join("\n", failures.Select(value => "FAIL\t" + value))
            + (consoleIssues.Count == 0 ? string.Empty : "\n" + string.Join("\n",
                consoleIssues.Select(value => "CONSOLE\t" + value))) + "\n";
        if (Focused)
        {
            WriteText(V27PairedClutterPlayModeVerifier.FocusedReportPath, report);
            WriteText("Temp/v27-balance-floor-clutter-focused.csv", floorCsv);
            WriteText("Temp/v27-balance-paired-run-rng-focused.csv", pairedCsv);
        }
        else
        {
            WriteText(V27PairedClutterPlayModeVerifier.ReportPath, report);
            WriteText(V27PairedClutterPlayModeVerifier.PairedCsvPath, pairedCsv);
            WriteText(V27PairedClutterPlayModeVerifier.ClutterCsvPath, floorCsv);
        }
        Debug.Log(report);
    }

    private string BuildSuccessEvidence(bool passed, int completedSeeds)
    {
        if (!passed)
            return string.Empty;
        if (Focused)
        {
            return "PASS\tPAIRED_FOCUSED_FOUR_ARMS\tseeds=1;windows=16\n"
                + "PASS\tPAIRED_FOCUSED_BURST_QUANTITY_CONSERVED\tallRows=true\n"
                + $"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={productionBurstArmCount}\n"
                + BuildProducerBurstEvidence()
                + $"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={productionPriorityArmCount}\n"
                + $"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={postPickupFaultArmCount}\n"
                + $"PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\tminimumPermille="
                + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}\n";
        }

        PairedRunAttributionAssessment assessment = finalAssessment
            ?? PairedRunAttributionEvaluator.Evaluate(rows);
        return $"PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds={completedSeeds}\n"
            + "PASS\tPAIRED_RUN_EXOGENOUS_EVENTS_EXACT\tallWindows=true\n"
            + $"PASS\tPAIRED_CLUTTER_ATTRIBUTION\tsamples={assessment.SampleCount};"
            + $"medianPermille={assessment.MedianClutterDeltaPermille};"
            + $"p95Permille={assessment.P95ClutterDeltaPermille};"
            + $"maxPermille={assessment.MaximumClutterDeltaPermille};"
            + $"madPermille={assessment.MadPermille}\n"
            + "PASS\tPAIRED_BURST_QUANTITY_CONSERVED\tallRows=true\n"
            + $"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={productionBurstArmCount}\n"
            + BuildProducerBurstEvidence()
            + $"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={productionPriorityArmCount}\n"
            + $"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={postPickupFaultArmCount}\n"
            + "PASS\tFLOOR_CLUTTER_ACCESS_EGRESS_ZERO\timmediateFailures=0\n"
            + "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\tpersistent=0\n"
            + $"PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\tminimumPermille="
            + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}\n"
            + "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\toutsideConeDivergence=0\n";
    }

    private string BuildProducerBurstEvidence()
    {
        StringBuilder builder = new();
        if (facilityBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_FACILITY_OUTPUT_BURST_PRODUCTION\tarms=")
                .Append(facilityBurstArmCount).Append('\n');
        }
        if (cropHarvestBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_CROP_HARVEST_BURST_PRODUCTION\tarms=")
                .Append(cropHarvestBurstArmCount).Append('\n');
        }
        if (miningBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_MINING_BURST_PRODUCTION\tarms=")
                .Append(miningBurstArmCount).Append('\n');
        }
        return builder.ToString();
    }

    private void ValidateProductionInterventionEvidence()
    {
        int expectedFaultArms = checked(requiredSeedCount * 2);
        Check(productionBurstArmCount == expectedFaultArms,
            "PAIRED_KEYED_PRODUCTION_BURST_APPLIED",
            $"expectedArms={expectedFaultArms};actualArms={productionBurstArmCount}");
        if (Focused)
        {
            BurstProducerKind expected = SelectBurstProducer(StartSeed);
            int actual = expected switch
            {
                BurstProducerKind.FacilityOutput => facilityBurstArmCount,
                BurstProducerKind.CropHarvest => cropHarvestBurstArmCount,
                _ => miningBurstArmCount
            };
            Check(actual == expectedFaultArms,
                "PAIRED_FOCUSED_PRODUCER_KIND_EXACT",
                $"seed={StartSeed};producer={expected};"
                + $"expectedArms={expectedFaultArms};actualArms={actual}");
        }
        else
        {
            Check(facilityBurstArmCount > 0
                    && cropHarvestBurstArmCount > 0
                    && miningBurstArmCount > 0
                    && facilityBurstArmCount + cropHarvestBurstArmCount
                        + miningBurstArmCount == expectedFaultArms,
                "PAIRED_ALL_PRODUCTION_BURST_KINDS_EXACT",
                $"facility={facilityBurstArmCount};crop={cropHarvestBurstArmCount};"
                + $"mining={miningBurstArmCount};expected={expectedFaultArms}");
        }
        Check(productionPriorityArmCount == expectedFaultArms,
            "PAIRED_PRODUCTION_BURST_HAUL_PRIORITY",
            $"expectedArms={expectedFaultArms};actualArms={productionPriorityArmCount}");
        Check(postPickupFaultArmCount == expectedFaultArms,
            "PAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP",
            $"expectedArms={expectedFaultArms};actualArms={postPickupFaultArmCount}");
    }

    private string BuildPairedCsv()
    {
        StringBuilder builder = new(
            "seed,arm,window,travelMilliWu,waitMilliWu,dispatchWaitMilliWu,reservationWaitMilliWu,facilityAccessWaitMilliWu,noPathMilliWu,burstDeliveredQuantity,burstOutstandingQuantity,burstQuantityConserved,replanCount,stepAsideCount,clutterCellSeconds,semanticStateHash,randomStateHash,exogenousEventHash\r\n");
        foreach (PairedRunWindowResult row in rows
                     .OrderBy(value => value.Seed)
                     .ThenBy(value => value.Arm, StringComparer.Ordinal)
                     .ThenBy(value => value.WindowIndex))
        {
            builder.Append(row.Seed).Append(',').Append(row.Arm).Append(',')
                .Append(row.WindowIndex).Append(',').Append(row.TravelMilliWu).Append(',')
                .Append(row.WaitMilliWu).Append(',').Append(row.DispatchWaitMilliWu).Append(',')
                .Append(row.ReservationWaitMilliWu).Append(',')
                .Append(row.FacilityAccessWaitMilliWu).Append(',')
                .Append(row.NoPathMilliWu).Append(',')
                .Append(row.BurstDeliveredQuantity).Append(',')
                .Append(row.BurstOutstandingQuantity).Append(',')
                .Append(row.BurstQuantityConserved ? "true" : "false").Append(',')
                .Append(row.ReplanCount).Append(',')
                .Append(row.StepAsideCount).Append(',').Append(row.ClutterCellSeconds).Append(',')
                .Append(row.SemanticStateHash).Append(',').Append(row.RandomStateHash).Append(',')
                .Append(row.ExogenousEventHash).Append("\r\n");
        }
        return builder.ToString();
    }

    private string BuildFloorCsv()
    {
        StringBuilder builder = new(
            "seed,arm,window,isRecovery,graceSeconds,looseStacks,looseQuantity,outsideContainment,persistent,immediateFailures,clutterCellSeconds,runtimeHeadroomPermille,runtimeErosionCells,runtimeErosionDetail\r\n");
        foreach (FloorRow row in floorRows
                     .OrderBy(value => value.Seed)
                     .ThenBy(value => value.Arm, StringComparer.Ordinal)
                     .ThenBy(value => value.Window))
        {
            builder.Append(row.Seed).Append(',').Append(row.Arm).Append(',')
                .Append(row.Window).Append(',').Append(row.IsRecovery ? "true" : "false").Append(',')
                .Append(row.GraceSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.LooseStacks).Append(',').Append(row.LooseQuantity).Append(',')
                .Append(row.OutsideContainment).Append(',').Append(row.Persistent).Append(',')
                .Append(row.ImmediateFailures).Append(',').Append(row.ClutterCellSeconds)
                .Append(',').Append(row.RuntimeHeadroomPermille)
                .Append(',').Append(row.RuntimeErosionCells)
                .Append(',').Append(row.RuntimeErosionDetail)
                .Append("\r\n");
        }
        return builder.ToString();
    }

    private static void WriteText(string path, string text)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        });
    }

    private IEnumerator ExecuteGuarded(IEnumerator routine)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            bool moved;
            object current;
            try
            {
                IEnumerator active = stack.Peek();
                moved = active.MoveNext();
                current = moved ? active.Current : null;
            }
            catch (Exception exception)
            {
                failures.Add(exception.GetType().Name + ":" + exception.Message);
                yield break;
            }
            if (!moved)
            {
                stack.Pop();
                continue;
            }
            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return current;
        }
    }

    private void CaptureIssue(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)
            consoleIssues.Add(type + ":" + condition);
    }

    private bool Check(bool condition, string key, string detail)
    {
        if (!condition)
            failures.Add(key + ":" + detail);
        return condition;
    }

    private void Fail(string key, string detail) => failures.Add(key + ":" + detail);

    private T Resolve<T>() where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private void Inject(GameObject target)
    {
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
            scope.Container.Inject(component);
    }

    private static BuildingSO FindWarehouseAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null
                && value.GetStorageCapacity() > 0
                && value.StoresAllCategories())
            .OrderByDescending(value => value.GetStorageCapacity())
            .ThenBy(value => value.width * value.height)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static BuildingSO FindCookFacilityAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value?.Facility != null
                && value.Facility.SupportsWork(BuiltInWorkTypeIds.Cook)
                && value.GetAbility<BuildingCookingAbility>() is
                {
                    requiresFuel: false,
                    cookedMeals: 1
                })
            .OrderBy(value => value.width * value.height)
            .ThenBy(value => value.id)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private SeedLotState FindSeedLot(string seedItemId, string cropId)
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null
                         && value.Quantity > 0
                         && string.Equals(
                             value.ItemId,
                             seedItemId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            SeedLotState seedLot = SeedLotItemStateCodec.Decode(stack.Components);
            if (string.Equals(seedLot.cropId, cropId, StringComparison.Ordinal))
                return seedLot.Clone();
        }

        return null;
    }

    private static BuildingSO FindCropPlotAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value?.GetAbility<BuildingFacilityPartAbility>()?.code == "P23"
                && value.GetAbility<BuildingCropPlotAbility>() is { Indoor: false }
                && value.Facility?.SupportsWork(BuiltInWorkTypeIds.Sow) == true
                && value.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest))
            .SingleOrDefault();
    }

    private BuildableObject ResolveCropPlot() => world.Buildings
        .Where(value => value != null)
        .FirstOrDefault(value => string.Equals(
            value.PersistentInstanceId.Value,
            cropPlotId,
            StringComparison.Ordinal));

    private WorldResourceNode ResolveMiningNode() => worldResources.Nodes
        .Where(value => value != null)
        .FirstOrDefault(value => string.Equals(
            value.NodeId,
            miningNodeId,
            StringComparison.Ordinal));

    private CharacterActor[] EligibleActors() => world?.Characters
        .Select(CharacterActorCollection.GetCanonical)
        .Where(value => value != null && !value.IsDead
            && value.characterType is not CharacterType.Customer
                and not CharacterType.Intruder
            && value.CurrentLifecycleState == CharacterLifecycleState.Active)
        .Distinct()
        .ToArray() ?? FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(CharacterActorCollection.GetCanonical)
            .Where(value => value != null && !value.IsDead
                && value.characterType is not CharacterType.Customer
                    and not CharacterType.Intruder
                && value.CurrentLifecycleState == CharacterLifecycleState.Active)
            .Distinct().ToArray();

    private CharacterActor[] LiveActors()
    {
        CharacterActor[] eligible = EligibleActors();
        return measuredActorIds.Count == 0
            ? eligible
            : eligible.Where(value => measuredActorIds.Contains(ActorId(value)))
                .ToArray();
    }

    private static string ActorId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private void ApplyMeasurementIsolation(bool activateMeasuredActors = true)
    {
        gameSpeed.SetSpeed(5);
        gameSpeed.SetPaused(!activateMeasuredActors);
        Time.timeScale = activateMeasuredActors ? VerificationTimeScale : 0f;
        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
        debugMode.SetCheat(DungeonDebugCheat.PauseWildlifeAi, true);
        bool isolated = gameSpeed.IsPaused == !activateMeasuredActors
            && debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
            && debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible);
        isolated &= debugMode.IsCheatEnabled(DungeonDebugCheat.PauseWildlifeAi);
        Check(isolated, "PAIRED_DEBUG_ISOLATION",
            $"speed={gameSpeed.Speed};paused={gameSpeed.IsPaused};"
            + $"developer={debugMode.IsDeveloperModeEnabled};"
            + $"freeze={debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)};"
            + $"invincible={debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible)};"
            + $"wildlifePaused={debugMode.IsCheatEnabled(DungeonDebugCheat.PauseWildlifeAi)}");
        if (!isolated)
            return;

        CharacterActor[] actors = LiveActors()
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        HashSet<CharacterActor> measuredActors = actors.ToHashSet();
        foreach (CharacterActor unrelated in world.Characters
                     .Select(CharacterActorCollection.GetCanonical)
                     .Where(value => value != null
                         && !measuredActors.Contains(value))
                     .Distinct())
        {
            unrelated.SetAiPaused(true);
            unrelated.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-unrelated-actor-isolation");
            unrelated.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-unrelated-actor-isolation");
        }
        foreach (CharacterActor actor in actors)
        {
            actor.Brain?.ConfigureLogisticsMeasurementForDiagnostics(true);
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-checkpoint-reset");
        }
        foreach (CharacterActor actor in actors)
        {
            ResetActorForLogisticsMeasurement(actor);
            if (activateMeasuredActors)
            {
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }

        Check(actors.All(actor =>
                actor.Brain?.LogisticsMeasurementOnlyForDiagnostics == true),
            "PAIRED_LOGISTICS_ONLY_CANDIDATE_SCOPE",
            $"actors={actors.Length};enabled="
            + string.Join(",", actors.Select(actor =>
                $"{ActorId(actor)}:{actor.Brain?.LogisticsMeasurementOnlyForDiagnostics}")));
    }

    private void QuiesceActorsForCheckpoint()
    {
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-checkpoint-capture");
        }
    }

    private void PrepareActorsForArmMeasurementBoundary()
    {
        CharacterActor[] actors = LiveActors()
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterActor actor in actors)
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-arm-measurement-boundary");
        }

        bool isolated = actors.Length > 0
            && actors.All(actor => actor.IsAiPaused()
                && actor.Brain?.HasRunningAction != true
                && actor.GetComponent<AbilityMove>()
                    ?.HasActiveMovementRoutineForDiagnostics != true
                && actor.GetComponent<AbilityHaul>()?.IsHauling != true);
        Check(isolated,
            "PAIRED_ARM_MEASUREMENT_BOUNDARY_ISOLATED",
            $"actors={actors.Length};state={DescribeActors()}");
    }

    private void ResumeAllMeasuredActors()
    {
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            actor.SetAiPaused(false);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        scheduler.ResetDecisionQueueForDiagnostics();
    }

    private void ValidateFocusedCleanRepeatability()
    {
        PairedRunWindowResult[] left = rows
            .Where(value => value.Arm == "cleanRepeatA")
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        PairedRunWindowResult[] right = rows
            .Where(value => value.Arm == "cleanRepeatB")
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        bool exact = left.Length == 4 && right.Length == 4;
        string mismatch = string.Empty;
        int seed = left.FirstOrDefault()?.Seed
            ?? right.FirstOrDefault()?.Seed
            ?? 1;
        string leftStartRandom = armStartRandomHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatA", string.Empty);
        string rightStartRandom = armStartRandomHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatB", string.Empty);
        string leftStartSemantic = armStartSemanticHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatA", string.Empty);
        string rightStartSemantic = armStartSemanticHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatB", string.Empty);
        string startDifference = FindFirstLineDifference(
            armStartSemanticTexts.GetValueOrDefault(
                $"{seed}|cleanRepeatA", string.Empty),
            armStartSemanticTexts.GetValueOrDefault(
                $"{seed}|cleanRepeatB", string.Empty));
        bool startExact = string.Equals(leftStartRandom, rightStartRandom,
                StringComparison.Ordinal)
            && string.Equals(leftStartSemantic, rightStartSemantic,
                StringComparison.Ordinal);
        exact &= startExact;
        if (!startExact)
        {
            mismatch = $"startRandom={leftStartRandom}/{rightStartRandom};"
                + $"startSemantic={leftStartSemantic}/{rightStartSemantic};"
                + $"startFirstDifference={startDifference};";
        }
        bool windowsExact = left.Length == 4 && right.Length == 4;
        for (int index = 0; windowsExact && index < left.Length; index++)
        {
            PairedRunWindowResult a = left[index];
            PairedRunWindowResult b = right[index];
            windowsExact = a.TravelMilliWu == b.TravelMilliWu
                && a.WaitMilliWu == b.WaitMilliWu
                && a.DispatchWaitMilliWu == b.DispatchWaitMilliWu
                && a.ReservationWaitMilliWu == b.ReservationWaitMilliWu
                && a.FacilityAccessWaitMilliWu == b.FacilityAccessWaitMilliWu
                && a.NoPathMilliWu == b.NoPathMilliWu
                && a.BurstDeliveredQuantity == b.BurstDeliveredQuantity
                && a.BurstOutstandingQuantity == b.BurstOutstandingQuantity
                && a.BurstQuantityConserved == b.BurstQuantityConserved
                && a.ReplanCount == b.ReplanCount
                && a.StepAsideCount == b.StepAsideCount
                && a.ClutterCellSeconds == b.ClutterCellSeconds
                && string.Equals(a.SemanticStateHash, b.SemanticStateHash,
                    StringComparison.Ordinal)
                && string.Equals(a.RandomStateHash, b.RandomStateHash,
                    StringComparison.Ordinal)
                && string.Equals(a.ExogenousEventHash, b.ExogenousEventHash,
                    StringComparison.Ordinal);
            if (!windowsExact)
            {
                mismatch += $"window={index};travel={a.TravelMilliWu}/{b.TravelMilliWu};"
                    + $"wait={a.WaitMilliWu}/{b.WaitMilliWu};replan={a.ReplanCount}/{b.ReplanCount};"
                    + $"stepAside={a.StepAsideCount}/{b.StepAsideCount};"
                    + $"clutter={a.ClutterCellSeconds}/{b.ClutterCellSeconds};"
                    + $"semantic={a.SemanticStateHash}/{b.SemanticStateHash};"
                    + $"random={a.RandomStateHash}/{b.RandomStateHash};"
                    + $"randomFirstDifference={FindFirstRandomDifference(a.Seed, index)};"
                    + $"warmupFirstDifference={FindFirstFrameDifference(a.Seed, -1)};"
                    + $"frameFirstDifference={FindFirstFrameDifference(a.Seed, index)};"
                    + $"event={a.ExogenousEventHash}/{b.ExogenousEventHash}";
            }
        }
        exact &= windowsExact;
        Check(exact, "PAIRED_RUN_CLEAN_REPEATABILITY", mismatch.Length == 0
            ? "windows=4;exact=true"
            : mismatch);
    }

    private string FindFirstRandomDifference(int seed, int window)
    {
        IReadOnlyList<RandomStreamDiagnosticSnapshot> left =
            randomByArmWindow[$"{seed}|cleanRepeatA|{window}"];
        IReadOnlyList<RandomStreamDiagnosticSnapshot> right =
            randomByArmWindow[$"{seed}|cleanRepeatB|{window}"];
        Dictionary<string, RandomStreamDiagnosticSnapshot> rightById = right
            .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
        foreach (RandomStreamDiagnosticSnapshot value in left)
        {
            if (!rightById.TryGetValue(value.StreamId, out RandomStreamDiagnosticSnapshot other))
                return value.StreamId + ":missing-right";
            if (value.State != other.State || value.DrawCount != other.DrawCount)
                return $"{value.StreamId}:{value.State}/{value.DrawCount}!={other.State}/{other.DrawCount}";
        }
        HashSet<string> leftIds = left.Select(value => value.StreamId)
            .ToHashSet(StringComparer.Ordinal);
        string onlyRight = right.Select(value => value.StreamId)
            .FirstOrDefault(value => !leftIds.Contains(value));
        return onlyRight ?? "none";
    }

    private string FindFirstFrameDifference(int seed, int window)
    {
        List<string> left = focusedFrameTraces.GetValueOrDefault(
            $"{seed}|cleanRepeatA|{window}", new List<string>());
        List<string> right = focusedFrameTraces.GetValueOrDefault(
            $"{seed}|cleanRepeatB|{window}", new List<string>());
        int count = Math.Max(left.Count, right.Count);
        for (int index = 0; index < count; index++)
        {
            string leftValue = index < left.Count ? left[index] : "<missing>";
            string rightValue = index < right.Count ? right[index] : "<missing>";
            if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                return $"frame={index}:{leftValue}!={rightValue}";
        }
        return "none";
    }

    private string CaptureFocusedFrameTrace(float elapsed)
    {
        StringBuilder builder = new();
        builder.Append("elapsed=").Append(elapsed.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture))
            .Append("|clock=").Append(clock.Time.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(clock.FrameCount)
            .Append("|scheduler=").Append(scheduler.LastProcessedDecisionCount)
            .Append('/').Append(scheduler.LastBehaviorTreeTickCount);
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            Vector2Int position = actor.GetNowXY();
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            CharacterAiDecisionTickResult decision =
                scheduler.GetLastDecisionResultForDiagnostics(actor);
            builder.Append("|A:").Append(ActorId(actor)).Append(':')
                .Append(position.x).Append(',').Append(position.y).Append(':')
                .Append(actor.Brain?.CurrentActionDebugLabel).Append(':')
                .Append(actor.Brain?.RuntimeActionStartCount ?? 0L).Append(':')
                .Append(actor.Brain?.RuntimeImmediateReplanCount ?? 0L).Append(':')
                .Append(actor.CanRunAi ? 'R' : '-')
                .Append(actor.IsAiPaused() ? 'P' : '-')
                .Append(actor.Brain?.HasResumableDecisionPipeline == true ? 'D' : '-')
                .Append(':').Append(decision.Handled ? 'H' : '-')
                .Append('/').Append(decision.Branch)
                .Append('/').Append(decision.Task)
                .Append('/').Append(decision.Status)
                .Append(':').Append(actor.Blackboard?.LastDecisionTrace)
                .Append(':')
                .Append(move?.HasActiveMovementRoutineForDiagnostics == true ? 'M' : '-')
                .Append(haul?.IsHauling == true ? 'H' : '-');
        }
        foreach (RandomStreamDiagnosticSnapshot value in randomDiagnostics.Capture()
                     .Where(value => value.StreamId.StartsWith(
                         "character-", StringComparison.Ordinal))
                     .OrderBy(value => value.StreamId, StringComparer.Ordinal))
        {
            builder.Append("|R:").Append(value.StreamId).Append(':')
                .Append(value.State).Append(':').Append(value.DrawCount);
        }
        builder.Append("|W:").Append(CaptureSemanticHash());
        return builder.ToString();
    }

    private static string FindFirstLineDifference(string left, string right)
    {
        string[] leftLines = (left ?? string.Empty).Split('\n');
        string[] rightLines = (right ?? string.Empty).Split('\n');
        int count = Math.Max(leftLines.Length, rightLines.Length);
        for (int index = 0; index < count; index++)
        {
            string leftLine = index < leftLines.Length ? leftLines[index] : "<missing>";
            string rightLine = index < rightLines.Length ? rightLines[index] : "<missing>";
            if (!string.Equals(leftLine, rightLine, StringComparison.Ordinal))
                return $"line={index}:{leftLine}!={rightLine}";
        }
        return "none";
    }

    private int CaptureRuntimeHeadroomPermille()
    {
        FloorClutterAssessment current = clutter.Capture(
            grid,
            layout,
            Math.Max(0f, clock.Time - commonCheckpointTime));
        HashSet<Vector2Int> dynamicErosion = current.OutsideContainment
            .Where(value => value.Quantity > 0)
            .Select(value => value.Position)
            .ToHashSet();

        CharacterActor[] actors = LiveActors();
        dynamicErosion.UnionWith(actors
            .Where(value => string.Equals(
                value.Brain?.CurrentActionDebugLabel,
                "길 비켜주기",
                StringComparison.Ordinal))
            .Select(value => value.GetNowXY())
            .Where(value => (layout.GetRoles(value) & (
                SpatialCellRole.OperationalAccess
                | SpatialCellRole.QueueAccess
                | SpatialCellRole.SharedCorridor)) == 0));
        dynamicErosion.UnionWith(actors
            .Select(value => value.GetComponent<AbilityMove>()
                ?.ActiveSystemMoveDestinationForDiagnostics)
            .Where(value => value.HasValue)
            .Select(value => value.Value)
            .GroupBy(value => value)
            .Where(value => value.Count() > 1)
            .Select(value => value.Key)
            .Where(value => (layout.GetRoles(value) & (
                SpatialCellRole.OperationalAccess
                | SpatialCellRole.QueueAccess
                | SpatialCellRole.SharedCorridor)) == 0));

        if (!DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot currentInterior,
                out string layoutFailure))
        {
            throw new InvalidOperationException(
                "RUNTIME_HEADROOM_LAYOUT_INVALID:" + layoutFailure);
        }
        int currentStagePopulation = PopulationStagePortfolioCatalog
            .PopulationStages
            .Where(population => PopulationStagePortfolioCatalog
                .InteriorColumnsForPopulation(population)
                <= currentInterior.ColumnCount)
            .DefaultIfEmpty(PopulationStagePortfolioCatalog.PopulationStages[0])
            .Max();
        int minimum = V27PopulationStageSpatialBaseline
            .RuntimeHeadroomPermille(
                currentStagePopulation,
                dynamicErosion.Count);
        lastRuntimeHeadroomErosionCount = dynamicErosion.Count;
        lastRuntimeHeadroomErosionDetail = string.Join("|", dynamicErosion
            .OrderBy(value => value.x)
            .ThenBy(value => value.y)
            .Select(value => $"{value}:{layout.GetRoles(value)}"));
        if (minimum < 0 || minimum > 1000)
            throw new InvalidOperationException("RUNTIME_HEADROOM_AUTHORITY_INVALID");
        return minimum;
    }

    private void ResetActorForLogisticsMeasurement(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            Fail("PAIRED_ACTOR_NEUTRALIZATION", $"actor={ActorId(actor)};stats=false");
            return;
        }

        Dictionary<CharacterCondition, float> values = actor.Stats.StatSnapshot
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        values[CharacterCondition.HUNGER] = 85f;
        values[CharacterCondition.THIRST] = 100f;
        values[CharacterCondition.SLEEP] = 100f;
        values[CharacterCondition.FUN] = 80f;
        values[CharacterCondition.EXCRETION] = 100f;
        values[CharacterCondition.HYGIENE] = 100f;
        values[CharacterCondition.MOOD] = 75f;
        actor.Stats.RestorePersistentState(
            values,
            actor.CurrentHealth,
            actor.InjurySeverity,
            75f,
            Array.Empty<CharacterMoodFactorSnapshot>());
        bool reset = deprivation.DebugResetForDeterministicScenario(actor);
        Check(reset, "PAIRED_ACTOR_NEUTRALIZATION",
            $"actor={ActorId(actor)};deprivationReset={reset}");
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private void EnsureVerificationTimeScale()
    {
        if (gameSpeed?.IsPaused == true)
            gameSpeed.SetPaused(false);
        if (Time.timeScale < VerificationTimeScale)
            Time.timeScale = VerificationTimeScale;
    }

    private static bool IsAffectedActorStream(
        string streamId,
        ISet<string> affected)
    {
        foreach (string actorId in affected)
        {
            if (string.Equals(streamId, "character-ai:" + actorId, StringComparison.Ordinal)
                || string.Equals(streamId, "character-movement:" + actorId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private string DescribeOutside(FloorClutterAssessment assessment) =>
        string.Join(";", assessment.OutsideContainment.Select(value =>
            $"{value.StackId}/{items.GetAllStacks().FirstOrDefault(stack => stack.StackId == value.StackId)?.ItemId}"
            + $"@{value.Position}:q{value.Quantity}:age{value.AgeSeconds:0.##}:"
            + $"area={grid.GetGridCell(value.Position)?.AreaType}:"
            + $"roles={value.Roles}:persistent={value.Persistent}"));

    private string DescribeActors() => string.Join(";", LiveActors()
        .OrderBy(ActorId, StringComparer.Ordinal)
        .Select(actor =>
        {
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            WorldItemHaulPlan plan = null;
            string reason = string.Empty;
            bool preview = haul?.IsHauling != true && haulPlanning.TryPreviewBestPlan(
                actor, out plan, out reason);
            if (haul?.IsHauling == true)
            {
                plan = null;
                reason = "skipped-live-haul";
            }
            string continuation = string.Empty;
            string stopReason = string.Empty;
            bool canContinue = actor.Brain != null
                && actor.Brain.CanContinueCurrentAction(out continuation);
            bool shouldStop = actor.Brain != null
                && actor.Brain.ShouldStopCurrentActionForReplan(out stopReason);
            GridPathSearchBroker actorPaths =
                actor.PathSearchBroker as GridPathSearchBroker;
            bool samePathBroker = ReferenceEquals(
                actor.PathSearchBroker,
                scheduler?.PathSearchBrokerForDiagnostics);
            return $"{ActorId(actor)}:{actor.CurrentLifecycleState}:"
                + $"instance={actor.GetInstanceID()}:"
                + $"active={actor.gameObject.activeInHierarchy}/{actor.isActiveAndEnabled}:"
                + $"canRun={actor.CanRunAi}:published={actor.HasBeenPublished}:"
                + $"detached={actor.IsDetachedRestoreCandidate}:"
                + $"unpublished={actor.IsUnpublishedComposition}:"
                + $"pos={actor.GetNowXY()}:"
                + $"action={actor.Brain?.CurrentActionDebugLabel}:"
                + $"running={actor.Brain?.HasRunningAction}:"
                + $"ended={actor.Brain?.isBestActionEnd}:"
                + $"continue={canContinue}/{continuation}:"
                + $"stop={shouldStop}/{stopReason}:"
                + $"haul={haul?.IsHauling == true}:move={move?.HasActiveMovementRoutineForDiagnostics == true}:"
                + $"haulComponents={actor.GetComponents<AbilityHaul>().Length}:"
                + $"haulEnabled={haul?.enabled}/{haul?.isActiveAndEnabled}:"
                + $"haulRoutine={haul?.HasHaulingRoutineForDiagnostics}:"
                + $"haulUpdate={haul?.UpdateHeartbeatForDiagnostics}:"
                + $"haulStarts={haul?.RuntimeHaulStartCount}:"
                + $"haulTerminals={haul?.RuntimeHaulTerminalCount}:"
                + $"haulLastTerminal={haul?.LastTerminalDiagnostics}:"
                + $"haulStage={haul?.CurrentExecutionStage}:"
                + $"haulBeat={haul?.RoutineHeartbeat}:"
                + $"haulFailure={haul?.LastFailureReason}:"
                + $"haulPath={haul?.ActivePathDebug}:"
                + $"moveOwner={move?.ActiveMovementOperationOwnerForDiagnostics}:"
                + $"moveCancel={move?.LastMovementCancellationSourceForDiagnostics}:"
                + $"moveFailure={move?.LastGridMoveFailureReason}:"
                + $"movePreempt={move?.LastMovementOperationPreemptionForDiagnostics}:"
                + $"moveActionCancel={move?.LastActionMovementCancellationReasonForDiagnostics}:"
                + $"schedulerDeterministic={scheduler?.DeterministicSimulationForDiagnostics}:"
                + $"pathBrokerSame={samePathBroker}:"
                + $"pathDeterministic={actorPaths?.DeterministicSearchForDiagnostics}:"
                + $"pathFrame={actorPaths?.CacheFrameForDiagnostics}:"
                + $"pathSearches={actorPaths?.SearchesThisFrame}:"
                + $"pathDeferrals={actorPaths?.BudgetDeferralsThisFrame}:"
                + $"pathIncremental={actorPaths?.IncrementalExactSearchCountForDiagnostics}:"
                + $"preview={preview}/{plan?.PrimaryDestinationId}/{reason}";
        }));

    private static string HashText(string text)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(text));
        const string hex = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = hex[bytes[index] >> 4];
            result[index * 2 + 1] = hex[bytes[index] & 15];
        }
        return new string(result);
    }

    private sealed class WindowAccumulator
    {
        internal long TravelMilliWu;
        internal long WaitMilliWu;
        internal long DispatchWaitMilliWu;
        internal long ReservationWaitMilliWu;
        internal long FacilityAccessWaitMilliWu;
        internal long NoPathMilliWu;
        internal int BurstDeliveredQuantity;
        internal int BurstOutstandingQuantity;
        internal bool BurstQuantityConserved = true;
        internal int Replans;
        internal int StepAsideCount;
        internal int ClutterCellSeconds;
        internal int ImmediateFailures;
    }

    private enum BurstProducerKind
    {
        FacilityOutput = 0,
        CropHarvest = 1,
        Mining = 2
    }

    private static BurstProducerKind SelectBurstProducer(int seed) =>
        (BurstProducerKind)((Mathf.Max(1, seed) - 1) % 3);

    private sealed class ArmBurstProbe
    {
        internal ArmBurstProbe(
            BurstProducerKind producerKind,
            string itemId,
            Vector2Int sourceCell,
            int quantity,
            int totalBefore,
            int storedBefore,
            int carriedBefore)
        {
            ProducerKind = producerKind;
            ItemId = itemId ?? string.Empty;
            SourceCell = sourceCell;
            Quantity = quantity;
            TotalBefore = totalBefore;
            StoredBefore = storedBefore;
            CarriedBefore = carriedBefore;
        }

        internal BurstProducerKind ProducerKind { get; }
        internal string ItemId { get; }
        internal Vector2Int SourceCell { get; }
        internal int Quantity { get; }
        internal int TotalBefore { get; }
        internal int StoredBefore { get; }
        internal int CarriedBefore { get; }
    }

    private readonly struct BurstState
    {
        internal BurstState(
            int totalDelta,
            int sourceLoose,
            int sourceReserved,
            int carriedDelta,
            int delivered,
            int outstanding,
            bool quantityConserved)
        {
            TotalDelta = totalDelta;
            SourceLoose = sourceLoose;
            SourceReserved = sourceReserved;
            CarriedDelta = carriedDelta;
            Delivered = delivered;
            Outstanding = outstanding;
            QuantityConserved = quantityConserved;
        }

        internal int TotalDelta { get; }
        internal int SourceLoose { get; }
        internal int SourceReserved { get; }
        internal int CarriedDelta { get; }
        internal int Delivered { get; }
        internal int Outstanding { get; }
        internal bool QuantityConserved { get; }
    }

    private readonly struct FloorRow
    {
        internal FloorRow(
            int seed,
            string arm,
            int window,
            FloorClutterAssessment assessment,
            int clutterCellSeconds,
            int runtimeHeadroomPermille,
            int runtimeErosionCells,
            string runtimeErosionDetail,
            bool isRecovery)
        {
            Seed = seed;
            Arm = arm;
            Window = window;
            IsRecovery = isRecovery;
            GraceSeconds = assessment.GraceSeconds;
            LooseStacks = assessment.LooseStackCount;
            LooseQuantity = assessment.LooseQuantity;
            OutsideContainment = assessment.OutsideContainment.Count;
            Persistent = assessment.PersistentCount;
            ImmediateFailures = assessment.ImmediateFailureCount;
            ClutterCellSeconds = clutterCellSeconds;
            RuntimeHeadroomPermille = runtimeHeadroomPermille;
            RuntimeErosionCells = runtimeErosionCells;
            RuntimeErosionDetail = runtimeErosionDetail ?? string.Empty;
        }

        internal int Seed { get; }
        internal string Arm { get; }
        internal int Window { get; }
        internal bool IsRecovery { get; }
        internal float GraceSeconds { get; }
        internal int LooseStacks { get; }
        internal int LooseQuantity { get; }
        internal int OutsideContainment { get; }
        internal int Persistent { get; }
        internal int ImmediateFailures { get; }
        internal int ClutterCellSeconds { get; }
        internal int RuntimeHeadroomPermille { get; }
        internal int RuntimeErosionCells { get; }
        internal string RuntimeErosionDetail { get; }
    }
}
#endif
