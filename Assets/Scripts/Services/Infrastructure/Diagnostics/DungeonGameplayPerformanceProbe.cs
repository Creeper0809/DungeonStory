using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VContainer;
#if UNITY_EDITOR
using UnityEditor.Profiling;
using UnityEditorInternal;
#endif

public sealed class DungeonGameplayPerformanceProbe : MonoBehaviour
{
    private const string EnableArgument = "-gameplay-performance-profile";
    private const string GameplaySceneName = "GameplayScene";
    private const int DefaultWarmupFrames = 300;
    private const float DefaultSampleSeconds = 12f;
    private const int MaximumSamples = 7200;
    private const int VisibleStressActorCount = 96;
    private static readonly string[] RuntimeTickMarkerNames =
    {
        "CaptivityRuntime.Tick",
        "CircusRuntime.Tick",
        "ExteriorActivityRuntime.Tick",
        "CharacterSkillGenerationService.Tick",
        "WorkOrderRuntime.Tick",
        "CharacterBodyHealthRuntime.Tick",
        "CharacterMedicalRuntime.Tick",
        "EquipmentMaintenancePolicyRuntime.Tick",
        "DefenseEngagementRuntime.Tick",
        "OffenseReturnArrivalRuntime.Tick",
        "CharacterDeprivationRuntime.Tick",
        "WorldWaterRuntime.Tick",
        "WildlifeCaptureRuntime.Tick",
        "WildlifeEcosystemRuntime.Tick",
        "WildlifeRuntime.Tick",
        "AnimalHusbandryRuntime.Tick",
        "FirstRunObjectiveRuntime.Tick",
        "RoomLayoutCache.Rebuild"
    };
    private const float MixedPopulationSchedulerP95TargetMilliseconds = 4f;
    private const long MixedPopulationAverageGcTargetBytes = 64L * 1024L;
    private const long MixedPopulationMemoryGrowthTargetBytes = 16L * 1024L * 1024L;

    private static bool bootstrapped;
    private static GameplayPerformanceOptions pendingEditorOptions;

    private readonly List<ScriptableObject> runtimeDefinitions = new List<ScriptableObject>();
    private readonly List<string> capturedMessages = new List<string>();
    private readonly GameplayPerformanceReport report = new GameplayPerformanceReport();
#if UNITY_EDITOR
    private readonly List<SlowFrameProfile> slowFrameProfiles =
        new List<SlowFrameProfile>();
    private bool originalProfilerEnabled;
    private bool rawProfilerCaptureActive;
#endif

    private GameplayPerformanceOptions options;
    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gcAllocationRecorder;
    private ProfilerRecorder gcCollectRecorder;
    private ProfilerRecorder aiBudgetRecorder;
    private ProfilerRecorder characterStatsRecorder;
    private ProfilerRecorder aiDirectorRecorder;
    private ProfilerRecorder abilityMoveRecorder;
    private ProfilerRecorder abilityWorkRecorder;
    private ProfilerRecorder[] runtimeTickRecorders;
    private float[] frameSamples;
    private float[] mainThreadSamples;
    private float[] renderThreadSamples;
    private float[] gcCollectSamples;
    private float[] aiBudgetSamples;
    private float[] characterStatsSamples;
    private float[] aiDirectorSamples;
    private float[] abilityMoveSamples;
    private float[] abilityWorkSamples;
    private float[][] runtimeTickSamples;
    private long[] gcSamples;
    private long[] monoUsedSamples;
    private int[] rawProfilerFrameIndices;
    private int sampleCount;
    private int warningCount;
    private int errorCount;
    private int originalVSyncCount;
    private int originalTargetFrameRate;
    private float originalTimeScale;
    private float originalFixedDeltaTime;
    private bool finished;
    private bool editorSlowTraceEnabled;
    private bool playableRunSetupAttempted;
    private string profileException;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (bootstrapped || !HasCommandLineArgument(EnableArgument))
        {
            return;
        }

        bootstrapped = true;
        GameObject host = new GameObject(nameof(DungeonGameplayPerformanceProbe));
        DontDestroyOnLoad(host);
        host.AddComponent<DungeonGameplayPerformanceProbe>();
    }

#if UNITY_EDITOR
    public static string GetEditorReadinessDiagnostics()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        GridSystemManager gridSystem =
            FindSceneComponent<GridSystemManager>(scene);
        CharacterSpawner spawner =
            FindSceneComponent<CharacterSpawner>(scene);
        CharacterActor[] actors =
            FindSceneComponents<CharacterActor>(scene);
        int activeActors = actors.Count(actor =>
            actor != null
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active);
        return $"scene={scene.name}; "
            + $"scope={scope != null}; "
            + $"container={scope?.Container != null}; "
            + $"gridSystem={gridSystem != null}; "
            + $"grid={gridSystem?.grid != null}; "
            + $"spawner={spawner != null}; "
            + $"pool={spawner?.characterPool != null}; "
            + $"actors={actors.Length}; "
            + $"activeActors={activeActors}";
    }

    public static void StartEditorProfile(
        string profileId,
        int actorCount,
        int facilityCount,
        int gridWidth,
        int gridHeight,
        int activeFloors,
        int warmupFrames,
        float sampleSeconds,
        string reportPath,
        string screenshotPath,
        float simulationSpeed = 1f,
        bool disableAiScheduler = false,
        bool disableCharacterPresentation = false,
        bool disableCharacterStatsUpdates = false,
        bool captureRawProfiler = false,
        int livestockCount = 0,
        int normalOperationSupplyDays = 0)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "The gameplay performance profile requires PlayMode.");
        }

        if (FindAnyObjectByType<DungeonGameplayPerformanceProbe>() != null)
        {
            throw new InvalidOperationException(
                "A gameplay performance profile is already running.");
        }

        pendingEditorOptions = GameplayPerformanceOptions.CreateEditor(
            profileId,
            actorCount,
            facilityCount,
            gridWidth,
            gridHeight,
            activeFloors,
            warmupFrames,
            sampleSeconds,
            reportPath,
            screenshotPath,
            simulationSpeed,
            disableAiScheduler,
            disableCharacterPresentation,
            disableCharacterStatsUpdates,
            captureRawProfiler,
            livestockCount,
            normalOperationSupplyDays);
        bootstrapped = true;
        CharacterAiPerformanceCaptureControl.BeginDetailedCapture();
        bool enableSlowTrace = profileId?.IndexOf(
                "trace",
                StringComparison.OrdinalIgnoreCase) >= 0;
        if (enableSlowTrace)
        {
            CharacterAiPerformanceCaptureControl.BeginSlowTrace();
        }
        GameObject host = new GameObject(nameof(DungeonGameplayPerformanceProbe));
        DontDestroyOnLoad(host);
        DungeonGameplayPerformanceProbe probe =
            host.AddComponent<DungeonGameplayPerformanceProbe>();
        probe.editorSlowTraceEnabled = enableSlowTrace;
    }

    public static void StartEditorEconomyMixedPopulationProfile()
    {
        StartEditorProfile(
            profileId: "economy-100-staff-100-livestock-x5",
            actorCount: 100,
            facilityCount: 128,
            gridWidth: 256,
            gridHeight: 8,
            activeFloors: 4,
            warmupFrames: 900,
            sampleSeconds: 20f,
            reportPath:
                "docs/implementation-reports/economy-100-staff-100-livestock-x5-latest.json",
            screenshotPath:
                "docs/implementation-reports/economy-100-staff-100-livestock-x5-latest.png",
            simulationSpeed: 5f,
            livestockCount: 100,
            normalOperationSupplyDays: 5);
    }
#endif

    private IEnumerator Start()
    {
        options = pendingEditorOptions
            ?? GameplayPerformanceOptions.Parse(Environment.GetCommandLineArgs());
        pendingEditorOptions = null;
        InitializeReport();
        Application.logMessageReceived += CaptureLog;
        originalVSyncCount = QualitySettings.vSyncCount;
        originalTargetFrameRate = Application.targetFrameRate;
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;

        Stopwatch totalStopwatch = Stopwatch.StartNew();
        yield return RunSafely(RunProfile());
        report.totalProfileMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds;
        report.valid = string.IsNullOrWhiteSpace(profileException) && ValidateReport();
        report.failureReason = report.valid
            ? string.Empty
            : !string.IsNullOrWhiteSpace(profileException)
                ? profileException
                : BuildFailureReason();

        yield return FinishProfile();
    }

    private IEnumerator RunSafely(IEnumerator root)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator currentEnumerator = stack.Peek();
            bool movedNext = false;
            object current = null;
            Exception failure = null;
            try
            {
                movedNext = currentEnumerator.MoveNext();
                if (movedNext)
                {
                    current = currentEnumerator.Current;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure != null)
            {
                profileException = failure.ToString();
                errorCount++;
                UnityEngine.Debug.LogException(failure, this);
                yield break;
            }

            if (!movedNext)
            {
                (currentEnumerator as IDisposable)?.Dispose();
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

    private IEnumerator RunProfile()
    {
        LogProfileStage("ensure-gameplay-run");
        yield return EnsureGameplayRun();
        LogProfileStage("wait-gameplay-ready");
        yield return WaitForGameplayReady();
#if UNITY_EDITOR
        if (options.IsEditorProfile)
        {
            UnpauseGameplay();
            LogProfileStage("editor-gc-baseline");
            yield return CaptureEditorGcBaseline();
        }
#endif

        Stopwatch setupStopwatch = Stopwatch.StartNew();
        LogProfileStage("configure-world");
        yield return ConfigureMeasuredWorld();
        ApplyDiagnosticIsolation();
        setupStopwatch.Stop();
        report.setupMilliseconds = setupStopwatch.Elapsed.TotalMilliseconds;

        ResetAiPerformanceRecorder();
        UnpauseGameplay();
#if UNITY_EDITOR
        BeginRawProfilerCapture();
#endif
        LogProfileStage("warmup");
        yield return WarmUp();
        LogProfileStage("capture");
        yield return CapturePerformanceSamples();
#if UNITY_EDITOR
        EndRawProfilerCapture();
#endif
        CaptureWorldSummary();
        LogProfileStage("capture-complete");
    }

#if UNITY_EDITOR
    private IEnumerator CaptureEditorGcBaseline()
    {
        const int BaselineWarmupFrames = 120;
        const int BaselineSampleFrames = 240;
        for (int frame = 0; frame < BaselineWarmupFrames; frame++)
        {
            yield return null;
        }

        ProfilerRecorder recorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory,
            "GC Allocated In Frame",
            1);
        long totalBytes = 0;
        int recordedFrames = 0;
        try
        {
            for (int frame = 0; frame < BaselineSampleFrames; frame++)
            {
                yield return new WaitForEndOfFrame();
                if (!recorder.Valid)
                {
                    continue;
                }

                totalBytes += Math.Max(0L, recorder.LastValue);
                recordedFrames++;
            }
        }
        finally
        {
            recorder.Dispose();
        }

        report.editorBaselineGcAverageBytes = recordedFrames > 0
            ? totalBytes / (double)recordedFrames
            : 0d;
    }
#endif

    private void OnDestroy()
    {
        Application.logMessageReceived -= CaptureLog;
        DisposeRecorders();
        RestoreFrameSettings();
#if UNITY_EDITOR
        if (options?.IsEditorProfile == true)
        {
            EndRawProfilerCapture();
            CharacterAiPerformanceCaptureControl.EndDetailedCapture();
            if (editorSlowTraceEnabled)
            {
                CharacterAiPerformanceCaptureControl.EndSlowTrace();
                editorSlowTraceEnabled = false;
            }
            bootstrapped = false;
        }
#endif
    }

    private IEnumerator EnsureGameplayRun()
    {
        if (SceneManager.GetActiveScene().name == GameplaySceneName)
        {
            yield break;
        }

        DungeonSceneNavigator navigator = new DungeonSceneNavigator();
        if (!navigator.StartNewGameDirectForDebug(DungeonDifficulty.Normal))
        {
            throw new InvalidOperationException(
                "The normal new-run transition could not be started.");
        }

        float deadline = Time.realtimeSinceStartup + 90f;
        while (SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                throw new TimeoutException("GameplayScene did not load within 90 seconds.");
            }

            yield return null;
        }
    }

    private IEnumerator WaitForGameplayReady()
    {
        float deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline)
        {
            Scene scene = SceneManager.GetActiveScene();
            DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
            GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
            CharacterSpawner spawner = FindSceneComponent<CharacterSpawner>(scene);
            CharacterActor[] actors = FindSceneComponents<CharacterActor>(scene);
            bool infrastructureReady = scope != null
                && scope.Container != null
                && gridSystem != null
                && gridSystem.grid != null
                && spawner != null
                && spawner.characterPool != null;
            if (infrastructureReady && !playableRunSetupAttempted)
            {
                playableRunSetupAttempted = true;
                EnsurePlayableRun(scope);
                yield return null;
                actors = FindSceneComponents<CharacterActor>(scene);
            }

            bool hasReadyActor = options.IsEditorProfile
                ? actors.Any(actor =>
                    actor != null
                    && actor.gameObject.activeInHierarchy)
                : actors.Any(actor =>
                    actor != null
                    && actor.CurrentLifecycleState
                        == CharacterLifecycleState.Active);
            if (infrastructureReady && hasReadyActor)
            {
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }

            yield return null;
        }

        throw new TimeoutException(
            "The normal gameplay run did not finish initializing within 90 seconds.");
    }

    private void EnsurePlayableRun(DungeonRuntimeLifetimeScope scope)
    {
        IStartPartyPreparationService preparation =
            scope.Container.Resolve<IStartPartyPreparationService>();
        IOwnerRunManagerProvider ownerProvider =
            scope.Container.Resolve<IOwnerRunManagerProvider>();
        IPreparedStartPartyGameplayApplier applier =
            scope.Container.Resolve<IPreparedStartPartyGameplayApplier>();
        if (!ownerProvider.TryGetManager(out OwnerRunManager manager)
            || manager == null)
        {
            throw new InvalidOperationException(
                "The gameplay profile could not resolve the owner run manager.");
        }

        if (manager.CurrentOwnerActor != null)
        {
            return;
        }

        List<string> failures = new List<string>();
        int runSeed = 17012026;
        foreach (CharacterSO owner in manager.OwnerCandidates.Where(candidate => candidate != null))
        {
            if (!preparation.Begin(owner, out string beginMessage))
            {
                failures.Add($"{owner.characterName}: begin={beginMessage}");
                continue;
            }

            try
            {
                if (!preparation.TryCreatePreparedSnapshot(
                        DungeonDifficulty.Normal,
                        runSeed,
                        out PreparedStartPartySnapshot snapshot,
                        out string snapshotMessage))
                {
                    failures.Add($"{owner.characterName}: snapshot={snapshotMessage}");
                    continue;
                }

                if (applier.TryApply(snapshot, out string applyMessage))
                {
                    LogProfileStage(
                        $"prepared-run-applied:{owner.characterName}:{applyMessage}");
                    return;
                }

                failures.Add($"{owner.characterName}: apply={applyMessage}");
            }
            finally
            {
                preparation.Cancel();
            }
        }

        throw new InvalidOperationException(
            "The gameplay profile could not create a playable start party. "
            + string.Join(" | ", failures));
    }

    private IEnumerator ConfigureMeasuredWorld()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
        CharacterSpawner spawner = FindSceneComponent<CharacterSpawner>(scene);
        if (scope == null || scope.Container == null || gridSystem == null || spawner == null)
        {
            throw new InvalidOperationException("Required gameplay runtime services are missing.");
        }

        scope.Container.Resolve<IDungeonDebugModeService>().ResetTransientState();

        Grid grid = gridSystem.grid;
        if (options.GridWidth > grid.width || options.GridHeight > grid.height)
        {
            LogProfileStage($"expand-grid:{grid.width}x{grid.height}");
            gridSystem.GridExpand(
                Mathf.Max(0, options.GridWidth - grid.width),
                Mathf.Max(0, options.GridHeight - grid.height));
            grid = gridSystem.grid;
            RebindExistingBuildings(scene, grid);
            LogProfileStage($"expand-grid-complete:{grid.width}x{grid.height}");
        }

        if (options.FacilityCount > 0)
        {
            LogProfileStage($"dense-dungeon:{options.FacilityCount}");
            yield return ConfigureDenseDungeon(scope, gridSystem, grid);
            LogProfileStage($"dense-dungeon-complete:{report.actualDenseFacilityCount}");
        }

        LogProfileStage($"spawn-actors:{options.ActorCount}");
        yield return SpawnStressCharacters(scope, spawner, grid);
        LogProfileStage($"spawn-actors-complete:{report.actualStressActorCount}");
        if (options.LivestockCount > 0)
        {
            LogProfileStage($"spawn-livestock:{options.LivestockCount}");
            yield return SpawnStressLivestock(scope, grid);
            LogProfileStage(
                $"spawn-livestock-complete:{report.actualStressLivestockCount}");
        }

        if (options.NormalOperationSupplyDays > 0)
        {
            SeedNormalOperationSupplies(scope);
        }

        gridSystem.NotifyGridObjectChanged();
        scope.Container.Resolve<IFacilityCandidateCache>().Clear();
        yield return null;
    }

    private void SeedNormalOperationSupplies(
        DungeonRuntimeLifetimeScope scope)
    {
        IWorldItemStackRuntime itemRuntime =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        IWarehouseWorldQuery warehouseWorld =
            scope.Container.Resolve<IWarehouseWorldQuery>();
        int population = Mathf.Max(1, options.ActorCount);
        int requestedPerCategory = population
            * Mathf.Max(1, options.NormalOperationSupplyDays);

        List<IWarehouseFacility> warehouses = warehouseWorld.Warehouses
            .Where(warehouse =>
                warehouse?.Inventory != null
                && warehouse.HasWarehouseInventory)
            .ToList();
        int warehouseFoodAmount = SeedWarehouseStock(
            itemRuntime,
            warehouses,
            StockCategory.Food,
            requestedPerCategory);
        int warehouseWaterAmount = SeedWarehouseStock(
            itemRuntime,
            warehouses,
            StockCategory.Water,
            requestedPerCategory);
        int looseFoodAmount = 0;
        int looseWaterAmount = 0;
        int foodAmount = warehouseFoodAmount;
        int waterAmount = warehouseWaterAmount;

        if (foodAmount < requestedPerCategory
            && itemRuntime.SpawnStockAtDropoff(
                StockCategory.Food,
                requestedPerCategory - foodAmount,
                "성능 검증용 정상 배급",
                out looseFoodAmount))
        {
            foodAmount += looseFoodAmount;
        }

        if (waterAmount < requestedPerCategory
            && itemRuntime.SpawnStockAtDropoff(
                StockCategory.Water,
                requestedPerCategory - waterAmount,
                "성능 검증용 정상 배급",
                out looseWaterAmount))
        {
            waterAmount += looseWaterAmount;
        }

        report.normalOperationSupplyDays =
            options.NormalOperationSupplyDays;
        report.normalOperationWarehouseCount = warehouses.Count;
        report.seededWarehouseFoodAmount = warehouseFoodAmount;
        report.seededWarehouseWaterAmount = warehouseWaterAmount;
        report.seededLooseFoodAmount = looseFoodAmount;
        report.seededLooseWaterAmount = looseWaterAmount;
        report.seededFoodAmount = foodAmount;
        report.seededWaterAmount = waterAmount;
        LogProfileStage(
            $"seed-supplies:food={foodAmount};water={waterAmount};"
            + $"warehouses={warehouses.Count};"
            + $"warehouseFood={warehouseFoodAmount};"
            + $"warehouseWater={warehouseWaterAmount};"
            + $"looseFood={looseFoodAmount};"
            + $"looseWater={looseWaterAmount};"
            + $"days={options.NormalOperationSupplyDays}");

        if (foodAmount < requestedPerCategory
            || waterAmount < requestedPerCategory)
        {
            throw new InvalidOperationException(
                "Normal-operation supplies were incomplete: "
                + $"food={foodAmount}/{requestedPerCategory}, "
                + $"water={waterAmount}/{requestedPerCategory}.");
        }
    }

    private static int SeedWarehouseStock(
        IWorldItemStackRuntime itemRuntime,
        IReadOnlyList<IWarehouseFacility> warehouses,
        StockCategory category,
        int requested)
    {
        int spawned = 0;
        for (int index = 0;
             index < warehouses.Count && spawned < requested;
             index++)
        {
            int remainingWarehouses = warehouses.Count - index;
            int share = Mathf.CeilToInt(
                (requested - spawned)
                / (float)Mathf.Max(1, remainingWarehouses));
            itemRuntime.SpawnStockInWarehouse(
                warehouses[index],
                category,
                share,
                out int accepted);
            spawned += accepted;
        }

        return spawned;
    }

    private void ApplyDiagnosticIsolation()
    {
        Scene scene = SceneManager.GetActiveScene();
        report.aiSchedulerDisabled = options.DisableAiScheduler;
        report.characterPresentationDisabled = options.DisableCharacterPresentation;
        report.characterStatsUpdatesDisabled = options.DisableCharacterStatsUpdates;

        foreach (OwnerSelectionPanel panel in
                 FindSceneComponents<OwnerSelectionPanel>(scene))
        {
            if (panel == null)
            {
                continue;
            }

            panel.RefreshVisibility();
            if (panel.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException(
                    "Owner selection remained visible after the prepared run was applied.");
            }
        }

        if (options.DisableAiScheduler)
        {
            foreach (CharacterAiScheduler scheduler in
                     FindSceneComponents<CharacterAiScheduler>(scene))
            {
                if (scheduler != null)
                {
                    scheduler.enabled = false;
                }
            }
        }

        if (options.DisableCharacterPresentation)
        {
            foreach (WorldCharacterNameplate nameplate in
                     FindSceneComponents<WorldCharacterNameplate>(scene))
            {
                if (nameplate != null)
                {
                    nameplate.enabled = false;
                }
            }

            foreach (CharacterDialogueRuntime dialogue in
                     FindSceneComponents<CharacterDialogueRuntime>(scene))
            {
                if (dialogue != null)
                {
                    dialogue.enabled = false;
                }
            }
        }

        if (options.DisableCharacterStatsUpdates)
        {
            foreach (CharacterStats stats in
                     FindSceneComponents<CharacterStats>(scene))
            {
                if (stats != null)
                {
                    stats.enabled = false;
                }
            }
        }

        if (options.HasDiagnosticIsolation)
        {
            LogProfileStage(
                "diagnostic-isolation:"
                + $"ai={options.DisableAiScheduler},"
                + $"presentation={options.DisableCharacterPresentation},"
                + $"stats={options.DisableCharacterStatsUpdates}");
        }
    }

    private IEnumerator ConfigureDenseDungeon(
        DungeonRuntimeLifetimeScope scope,
        GridSystemManager gridSystem,
        Grid grid)
    {
        int activeFloors = Mathf.Clamp(options.ActiveFloors, 1, grid.height);
        List<Vector2Int> missingHallwayCells =
            new List<Vector2Int>(grid.width * activeFloors);
        for (int y = 0; y < activeFloors; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GridCell cell = grid.GetGridCell(position);
                cell.SetAreaType(GridCellAreaType.DungeonInterior);
                if (!cell.HasOccupantInLayer(GridLayer.Hallway))
                {
                    missingHallwayCells.Add(position);
                }
            }
        }

        if (missingHallwayCells.Count > 0)
        {
            bool registered = grid.RegisterOccupant(
                new PerformanceHallwayOccupant(),
                GridLayer.Hallway,
                missingHallwayCells,
                false);
            if (!registered)
            {
                throw new InvalidOperationException(
                    "Dense gameplay floor cells could not be registered.");
            }
        }

        RegisterTraversalColumn(grid, 0, activeFloors);
        RegisterTraversalColumn(grid, grid.width - 1, activeFloors);
        grid.RefreshTraversalHeuristicMetadata();

        IDataCatalog catalog = scope.Container.Resolve<IDataCatalog>();
        List<BuildingSO> baseFacilityDefinitions =
            SelectDenseFacilityDefinitions(
                catalog.GetData<BuildingSO>().Values,
                12)
            .Select(CloneWithoutRoomRequirement)
            .ToList();
        List<BuildingSO> facilityDefinitions =
            BuildDenseFacilityPlacementSequence(baseFacilityDefinitions);
        BuildingSO doorDefinition = catalog.GetData<BuildingSO>()
            .Values
            .Where(definition => definition != null && definition.IsInteriorDoor)
            .OrderBy(definition => definition.id)
            .Select(CloneDefinition)
            .FirstOrDefault();

        if (facilityDefinitions.Count == 0)
        {
            throw new InvalidOperationException(
                "No independently rendered modular facility definitions were found.");
        }

        GridTexture gridTexture = FindSceneComponent<GridTexture>(
            SceneManager.GetActiveScene());
        GridBuildingFactory buildingFactory = new GridBuildingFactory(gridTexture);
        int placedDoors = 0;
        if (doorDefinition != null)
        {
            for (int floor = 0; floor < activeFloors; floor++)
            {
                for (int x = options.RoomSpan; x < grid.width; x += options.RoomSpan)
                {
                    if (TryPlaceBuilding(
                            scope,
                            buildingFactory,
                            grid,
                            doorDefinition,
                            new Vector2Int(x, floor),
                            out _))
                    {
                        placedDoors++;
                    }

                    if ((placedDoors & 255) == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        int placedFacilities = 0;
        int slotSequence = 0;
        int[] slotOffsets = { 2, 6, 10, 13 };
        int roomCount = Mathf.CeilToInt(grid.width / (float)options.RoomSpan);
        int baseFacilitiesPerFloor =
            options.FacilityCount / activeFloors;
        int facilityFloorRemainder =
            options.FacilityCount % activeFloors;
        for (int floor = 0; floor < activeFloors; floor++)
        {
            int floorTarget = baseFacilitiesPerFloor
                + (floor < facilityFloorRemainder ? 1 : 0);
            int placedOnFloor = 0;
            for (int pass = 0;
                 pass < slotOffsets.Length && placedOnFloor < floorTarget;
                 pass++)
            {
                for (int roomOrdinal = 0;
                     roomOrdinal < roomCount && placedOnFloor < floorTarget;
                     roomOrdinal++)
                {
                    int roomIndex =
                        (roomOrdinal * 5 + floor * 3) % roomCount;
                    int roomStart = roomIndex * options.RoomSpan;
                    int slotOffset = slotOffsets[
                        (pass + roomOrdinal + floor) % slotOffsets.Length];
                    int x = roomStart + slotOffset;
                    if (x + 1 >= Mathf.Min(grid.width, roomStart + options.RoomSpan))
                    {
                        continue;
                    }

                    BuildingSO definition =
                        facilityDefinitions[slotSequence % facilityDefinitions.Count];
                    slotSequence++;
                    if (TryPlaceBuilding(
                            scope,
                            buildingFactory,
                            grid,
                            definition,
                            new Vector2Int(x, floor),
                            out _))
                    {
                        placedFacilities++;
                        placedOnFloor++;
                    }

                    if ((slotSequence & 127) == 0)
                    {
                        yield return null;
                    }
                }
            }
        }

        // Existing scene fixtures can occupy a few of the regular stress slots.
        // Fill those holes without increasing the configured active floor count.
        for (int floor = 0;
             floor < activeFloors && placedFacilities < options.FacilityCount;
             floor++)
        {
            for (int roomStart = 0;
                 roomStart < grid.width && placedFacilities < options.FacilityCount;
                 roomStart += options.RoomSpan)
            {
                int x = roomStart + 4;
                if (x + 1 >= Mathf.Min(grid.width, roomStart + options.RoomSpan))
                {
                    continue;
                }

                BuildingSO definition =
                    facilityDefinitions[slotSequence % facilityDefinitions.Count];
                slotSequence++;
                if (TryPlaceBuilding(
                        scope,
                        buildingFactory,
                        grid,
                        definition,
                        new Vector2Int(x, floor),
                        out _))
                {
                    placedFacilities++;
                }

                if ((slotSequence & 127) == 0)
                {
                    yield return null;
                }
            }
        }

        report.actualDenseFacilityCount = placedFacilities;
        report.actualDenseDoorCount = placedDoors;
        if (placedFacilities < options.FacilityCount)
        {
            throw new InvalidOperationException(
                $"Dense facility capacity was exhausted: requested={options.FacilityCount}, "
                + $"placed={placedFacilities}.");
        }

        gridSystem.NotifyGridObjectChanged();
    }

    private IEnumerator SpawnStressCharacters(
        DungeonRuntimeLifetimeScope scope,
        CharacterSpawner spawner,
        Grid grid)
    {
        ICharacterSkillGenerationService skillGenerationService =
            scope.Container.Resolve<ICharacterSkillGenerationService>();
        CharacterActor[] existing = FindSceneComponents<CharacterActor>(
            SceneManager.GetActiveScene());
        int existingRequestsCancelled = 0;
        if (options.IsEditorProfile)
        {
            foreach (CharacterActor actor in existing)
            {
                if (actor == null || !actor.gameObject.activeInHierarchy)
                {
                    continue;
                }

                actor.SetLifecycleState(CharacterLifecycleState.Active);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
                skillGenerationService.CancelRequests(actor.Progression);
                existingRequestsCancelled++;
            }
        }
        report.preexistingSkillGenerationRequestsCancelled =
            existingRequestsCancelled;

        int requestedTotal = options.ActorCount <= 0
            ? existing.Count(actor => actor != null && actor.gameObject.activeInHierarchy)
            : options.ActorCount;
        int activeCount = existing.Count(actor =>
            actor != null && actor.gameObject.activeInHierarchy);
        if (activeCount >= requestedTotal)
        {
            report.actualStressActorCount = 0;
            yield break;
        }

        CharacterSO source = spawner.characters?.FirstOrDefault(character => character != null);
        if (source == null)
        {
            throw new InvalidOperationException(
                "CharacterSpawner has no real character definition for the gameplay profile.");
        }

        CharacterSO stressDefinition = Instantiate(source);
        stressDefinition.hideFlags = HideFlags.HideAndDontSave;
        stressDefinition.characterType = CharacterType.NPC;
        stressDefinition.characterName = "성능 측정 인원";
        runtimeDefinitions.Add(stressDefinition);
        ICharacterSpawnObjectFactory characterObjectFactory =
            scope.Container.Resolve<ICharacterSpawnObjectFactory>();

        int created = 0;
        while (activeCount < requestedTotal)
        {
            GameObject actorObject = spawner.characterPool.Get();
            if (actorObject != null && actorObject.GetComponent<AbilityWork>() == null)
            {
                actorObject.AddComponent<AbilityWork>();
            }

            characterObjectFactory.Inject(actorObject);
            CharacterActor actor = actorObject != null
                ? actorObject.GetComponent<CharacterActor>()
                : null;
            if (actor == null)
            {
                throw new InvalidOperationException(
                    "The real character pool returned an object without CharacterActor.");
            }

            actor.characterType = CharacterType.NPC;
            actor.RefreshAbilityCache();
            actor.Initialize(stressDefinition);
            skillGenerationService.CancelRequests(actor.Progression);
            actor.Identity?.SetPersistentId($"perf:{options.ProfileId}:{created:D5}");
            actor.Identity?.SetCharacterType(CharacterType.NPC);
            actor.Brain?.UseStaffWorkActions();
            actor.transform.position = grid.GetWorldPos(GetStressActorPosition(
                created,
                grid,
                Mathf.Clamp(options.ActiveFloors, 1, grid.height)));
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            created++;
            activeCount++;

            if ((created & 31) == 0)
            {
                if (created % 128 == 0)
                {
                    LogProfileStage($"spawn-actors-progress:{activeCount}/{requestedTotal}");
                }

                yield return null;
            }
        }

        report.actualStressActorCount = created;
        report.syntheticSkillGenerationRequestsCancelled = created > 0;
    }

    private IEnumerator SpawnStressLivestock(
        DungeonRuntimeLifetimeScope scope,
        Grid grid)
    {
        IDataCatalog dataCatalog = scope.Container.Resolve<IDataCatalog>();
        IWildlifeRuntime wildlife = scope.Container.Resolve<IWildlifeRuntime>();
        IWildlifeCaptureRuntime capture =
            scope.Container.Resolve<IWildlifeCaptureRuntime>();
        IAnimalHusbandryRuntime husbandry =
            scope.Container.Resolve<IAnimalHusbandryRuntime>();
        IWildlifeSpeciesCatalogProvider speciesCatalog =
            scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();

        BuildingSO penSource = dataCatalog.GetData<BuildingSO>()
            .Values
            .Where(definition => definition?.GetBeastPenAbility() != null)
            .OrderBy(definition => definition.id)
            .FirstOrDefault();
        if (penSource == null)
        {
            throw new InvalidOperationException(
                "The gameplay profile could not find a real livestock pen definition.");
        }

        BuildingSO penDefinition = CloneWithoutRoomRequirement(penSource);
        penDefinition.objectName = "성능 측정 대형 우리";
        BuildingBeastPenAbility penAbility =
            penDefinition.GetAbility<BuildingBeastPenAbility>();
        penAbility.capacity = Mathf.Max(
            options.LivestockCount,
            penAbility.capacity);
        penAbility.baseSecurity = 100f;
        penAbility.dailyFood = 0f;
        penAbility.dailyWater = 0f;

        GridBuildingFactory factory = new GridBuildingFactory();
        BuildableObject pen = null;
        foreach (GridCell cell in grid.GetCells()
                     .Where(cell =>
                         cell != null
                         && cell.Position.y < options.ActiveFloors)
                     .OrderBy(cell => cell.Position.y)
                     .ThenBy(cell => cell.Position.x))
        {
            if (TryPlaceBuilding(
                    scope,
                    factory,
                    grid,
                    penDefinition,
                    cell.Position,
                    out pen))
            {
                break;
            }
        }

        if (pen == null)
        {
            throw new InvalidOperationException(
                "The gameplay profile could not place its livestock pen.");
        }

        WildlifeSpeciesDefinition species = speciesCatalog.All
            .Where(candidate => candidate != null && candidate.CanEnterDungeon)
            .OrderBy(candidate => candidate.SpeciesId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (species == null)
        {
            throw new InvalidOperationException(
                "The gameplay profile needs at least one dungeon-capable livestock species.");
        }

        List<WildlifeActor> spawnedAnimals =
            new List<WildlifeActor>(options.LivestockCount);
        GridCell[] spawnCells = grid.GetCells()
            .Where(cell =>
                cell != null
                && cell.Position.y < options.ActiveFloors
                && cell.AreaType != GridCellAreaType.BlockedExterior
                && grid.IsWalkable(cell.Position))
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .ToArray();
        if (spawnCells.Length == 0)
        {
            throw new InvalidOperationException(
                "The gameplay profile could not find a walkable livestock spawn cell.");
        }

        int spawnCursor = 0;
        int attempts = 0;
        int maximumAttempts = Mathf.Max(
            options.LivestockCount * 8,
            spawnCells.Length * 2);
        while (spawnedAnimals.Count < options.LivestockCount
            && attempts < maximumAttempts)
        {
            Vector2Int position = spawnCells[
                spawnCursor++ % Mathf.Max(1, spawnCells.Length)].Position;
            attempts++;
            if (!wildlife.TrySpawnDomesticBirth(
                    species.SpeciesId,
                    position,
                    out WildlifeActor actor,
                    out _)
                || actor == null)
            {
                continue;
            }

            spawnedAnimals.Add(actor);
            if ((spawnedAnimals.Count & 15) == 0)
            {
                LogProfileStage(
                    $"spawn-livestock-progress:{spawnedAnimals.Count}/"
                    + $"{options.LivestockCount}");
                yield return null;
            }
        }

        if (spawnedAnimals.Count < options.LivestockCount)
        {
            throw new InvalidOperationException(
                $"The gameplay profile could only spawn {spawnedAnimals.Count}/"
                + $"{options.LivestockCount} real livestock actors.");
        }

        string penId = $"pen:{pen.id}:{pen.centerPos.x}:{pen.centerPos.y}";
        List<CapturedWildlifeState> capturedStates = capture.Capture()
            .Select(state => state.Clone())
            .ToList();
        HashSet<string> capturedIds = new HashSet<string>(
            capturedStates.Select(state => state.wildlifeId),
            StringComparer.Ordinal);
        foreach (WildlifeActor actor in spawnedAnimals)
        {
            if (actor == null || !capturedIds.Add(actor.WildlifeId))
            {
                continue;
            }

            capturedStates.Add(new CapturedWildlifeState
            {
                wildlifeId = actor.WildlifeId,
                speciesId = actor.SpeciesId,
                penId = penId,
                penPosition = actor.GridPosition,
                capturePosition = actor.GridPosition,
                transportState = CapturedWildlifeTransportState.Penned,
                isTamed = true,
                nextCareAt = Time.time + 5f,
                lastCareStatus = "대형 우리에서 생활 중"
            });
        }

        List<string> restoreWarnings = new List<string>();
        DungeonAnimalHusbandrySaveData husbandrySnapshot =
            husbandry.Capture();
        capture.Restore(capturedStates, restoreWarnings);
        if (restoreWarnings.Count > 0)
        {
            throw new InvalidOperationException(
                "Livestock capture restore reported: "
                + string.Join(" | ", restoreWarnings));
        }

        husbandry.Restore(husbandrySnapshot);
        AnimalPenPolicyData policy = husbandry.GetOrCreatePenPolicy(penId);
        policy.maximumAnimals = Mathf.Max(
            options.LivestockCount,
            policy.maximumAnimals);
        policy.allowCarnivores = true;
        policy.allowScavengers = true;
        policy.allowRiskyMixing = true;
        policy.adultFemaleLimit = options.LivestockCount;
        policy.adultMaleLimit = options.LivestockCount;
        policy.juvenileLimit = options.LivestockCount;
        if (!husbandry.SetPenPolicy(policy, out string policyFailure))
        {
            throw new InvalidOperationException(
                $"The performance livestock policy was rejected: {policyFailure}");
        }

        report.actualStressLivestockCount = spawnedAnimals.Count;
        yield return null;
    }

    private void LogProfileStage(string stage)
    {
        UnityEngine.Debug.Log(
            $"GAMEPLAY_PERFORMANCE_PROFILE_STAGE {options?.ProfileId ?? "unconfigured"} "
            + $"{stage}",
            this);
    }

    private IEnumerator WarmUp()
    {
        int frames = Mathf.Max(1, options.WarmupFrames);
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }

    private IEnumerator CapturePerformanceSamples()
    {
        frameSamples = new float[MaximumSamples];
        mainThreadSamples = new float[MaximumSamples];
        renderThreadSamples = new float[MaximumSamples];
        gcCollectSamples = new float[MaximumSamples];
        aiBudgetSamples = new float[MaximumSamples];
        characterStatsSamples = new float[MaximumSamples];
        aiDirectorSamples = new float[MaximumSamples];
        abilityMoveSamples = new float[MaximumSamples];
        abilityWorkSamples = new float[MaximumSamples];
        runtimeTickRecorders = new ProfilerRecorder[RuntimeTickMarkerNames.Length];
        runtimeTickSamples = new float[RuntimeTickMarkerNames.Length][];
        for (int markerIndex = 0;
            markerIndex < RuntimeTickMarkerNames.Length;
            markerIndex++)
        {
            runtimeTickRecorders[markerIndex] =
                StartRecorderByName(RuntimeTickMarkerNames[markerIndex]);
            runtimeTickSamples[markerIndex] = new float[MaximumSamples];
        }
        gcSamples = new long[MaximumSamples];
        monoUsedSamples = new long[MaximumSamples];
        rawProfilerFrameIndices = options.CaptureRawProfiler
            ? new int[MaximumSamples]
            : null;
        sampleCount = 0;

        mainThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal,
            "Main Thread",
            1);
        renderThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal,
            "Render Thread",
            1);
        gcAllocationRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory,
            "GC Allocated In Frame",
            1);
        gcCollectRecorder = StartRecorderByName("GC.Collect");
        aiBudgetRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "CharacterAiScheduler.ProcessAiBudget",
            1);
        characterStatsRecorder = StartRecorderByName(
            "CharacterStatMaintenanceRuntime.Tick");
        aiDirectorRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AiDirectorRuntime.Update() [Invoke]",
            1);
        abilityMoveRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AbilityMove.Move2PosBySpeedInternal() [Coroutine: MoveNext] [Invoke]",
            1);
        abilityWorkRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AbilityWork.Work() [Coroutine: MoveNext] [Invoke]",
            1);

        ForceManagedCollection();
        report.monoUsedBytesAfterStartCollection =
            Profiler.GetMonoUsedSizeLong();
        report.monoUsedBytesAtStart = Profiler.GetMonoUsedSizeLong();
        report.totalAllocatedBytesAtStart = Profiler.GetTotalAllocatedMemoryLong();
        float startedAt = Time.realtimeSinceStartup;
        while (sampleCount < MaximumSamples
            && Time.realtimeSinceStartup - startedAt < options.SampleSeconds)
        {
            yield return new WaitForEndOfFrame();
            int index = sampleCount++;
            frameSamples[index] = Time.unscaledDeltaTime * 1000f;
            mainThreadSamples[index] = ReadRecorderMilliseconds(mainThreadRecorder);
            renderThreadSamples[index] = ReadRecorderMilliseconds(renderThreadRecorder);
            gcCollectSamples[index] = ReadRecorderMilliseconds(gcCollectRecorder);
            aiBudgetSamples[index] = ReadRecorderMilliseconds(aiBudgetRecorder);
            characterStatsSamples[index] = ReadRecorderMilliseconds(characterStatsRecorder);
            aiDirectorSamples[index] = ReadRecorderMilliseconds(aiDirectorRecorder);
            abilityMoveSamples[index] = ReadRecorderMilliseconds(abilityMoveRecorder);
            abilityWorkSamples[index] = ReadRecorderMilliseconds(abilityWorkRecorder);
            for (int markerIndex = 0;
                markerIndex < runtimeTickRecorders.Length;
                markerIndex++)
            {
                runtimeTickSamples[markerIndex][index] =
                    ReadRecorderMilliseconds(runtimeTickRecorders[markerIndex]);
            }
            gcSamples[index] = gcAllocationRecorder.Valid
                ? Math.Max(0, gcAllocationRecorder.LastValue)
                : 0;
            monoUsedSamples[index] = Profiler.GetMonoUsedSizeLong();
#if UNITY_EDITOR
            if (rawProfilerFrameIndices != null)
            {
                rawProfilerFrameIndices[index] = ProfilerDriver.lastFrameIndex;
            }
#endif
        }

        report.sampleDurationSeconds = Time.realtimeSinceStartup - startedAt;
        report.sampleCount = sampleCount;
        report.monoUsedBytesAtEnd = Profiler.GetMonoUsedSizeLong();
        report.totalAllocatedBytesAtEnd = Profiler.GetTotalAllocatedMemoryLong();
        ForceManagedCollection();
        report.monoUsedBytesAfterEndCollection =
            Profiler.GetMonoUsedSizeLong();
#if UNITY_EDITOR
        CaptureRecentAllocationHotspot();
#endif
        CalculateMetrics();
        DisposeRecorders();
    }

#if UNITY_EDITOR
    private void CaptureRecentAllocationHotspot()
    {
        if (!options.CaptureRawProfiler
            || rawProfilerFrameIndices == null
            || sampleCount <= 0)
        {
            return;
        }

        const int RetainedProfilerFrameWindow = 180;
        int first = Mathf.Max(0, sampleCount - RetainedProfilerFrameWindow);
        List<int> rankedIndices = new List<int>(
            sampleCount - first);
        for (int index = first; index < sampleCount; index++)
        {
            rankedIndices.Add(index);
        }

        rankedIndices.Sort((left, right) =>
            gcSamples[right].CompareTo(gcSamples[left]));
        HashSet<int> capturedFrames = new HashSet<int>();
        const int MaximumCapturedAllocationFrames = 5;
        for (int rank = 0;
            rank < rankedIndices.Count
                && capturedFrames.Count < MaximumCapturedAllocationFrames;
            rank++)
        {
            int sampleIndex = rankedIndices[rank];
            int profilerFrameIndex = rawProfilerFrameIndices[sampleIndex];
            if (profilerFrameIndex <= 0
                || !capturedFrames.Add(profilerFrameIndex))
            {
                continue;
            }

            CaptureSlowProfilerFrame(
                frameSamples[sampleIndex],
                profilerFrameIndex);
        }
    }
#endif

    private static ProfilerRecorder StartRecorderByName(string markerName)
    {
        List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        for (int i = 0; i < handles.Count; i++)
        {
            ProfilerRecorderHandle handle = handles[i];
            if (!handle.Valid)
            {
                continue;
            }

            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
            if (!string.Equals(description.Name, markerName, StringComparison.Ordinal))
            {
                continue;
            }

            return new ProfilerRecorder(
                handle,
                1,
                ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
        }

        return default;
    }

    private void CalculateMetrics()
    {
        report.frame = FrameMetric.From(frameSamples, sampleCount);
        report.mainThread = FrameMetric.FromPositive(mainThreadSamples, sampleCount);
        report.renderThread = FrameMetric.FromPositive(renderThreadSamples, sampleCount);
        report.gcCollect = FrameMetric.FromPositive(gcCollectSamples, sampleCount);
        report.aiBudget = FrameMetric.FromPositive(aiBudgetSamples, sampleCount);
        report.characterStats = FrameMetric.FromPositive(characterStatsSamples, sampleCount);
        report.aiDirector = FrameMetric.FromPositive(aiDirectorSamples, sampleCount);
        report.abilityMove = FrameMetric.FromPositive(abilityMoveSamples, sampleCount);
        report.abilityWork = FrameMetric.FromPositive(abilityWorkSamples, sampleCount);
        report.runtimeTicks = new NamedFrameMetric[RuntimeTickMarkerNames.Length];
        for (int markerIndex = 0;
            markerIndex < RuntimeTickMarkerNames.Length;
            markerIndex++)
        {
            report.runtimeTicks[markerIndex] = new NamedFrameMetric
            {
                name = RuntimeTickMarkerNames[markerIndex],
                metric = FrameMetric.FromPositive(
                    runtimeTickSamples[markerIndex],
                    sampleCount)
            };
        }
        report.gc = AllocationMetric.From(gcSamples, sampleCount);
        report.monoUsedFirstQuarterAverageBytes = AverageWindow(
            monoUsedSamples,
            sampleCount,
            0,
            Mathf.Max(1, sampleCount / 4));
        report.monoUsedLastQuarterAverageBytes = AverageWindow(
            monoUsedSamples,
            sampleCount,
            Mathf.Max(0, sampleCount - Mathf.Max(1, sampleCount / 4)),
            Mathf.Max(1, sampleCount / 4));
        report.sustainedMonoGrowthBytes =
            report.monoUsedLastQuarterAverageBytes
            - report.monoUsedFirstQuarterAverageBytes;
        report.retainedMonoGrowthBytes = Math.Max(
            0L,
            report.monoUsedBytesAfterEndCollection
                - report.monoUsedBytesAfterStartCollection);
        report.meetsSchedulerP95Target =
            report.aiBudget.p95 > 0f
            && report.aiBudget.p95
                <= MixedPopulationSchedulerP95TargetMilliseconds;
        report.gameplayIncrementalGcAverageBytes = Math.Max(
            0d,
            report.gc.averageBytes - report.editorBaselineGcAverageBytes);
#if UNITY_EDITOR
        report.usesEditorBaselineAdjustedGcTarget =
            options.IsEditorProfile;
#endif
        double evaluatedGcAverage = report.usesEditorBaselineAdjustedGcTarget
            ? report.gameplayIncrementalGcAverageBytes
            : report.gc.averageBytes;
        report.meetsAverageGcTarget =
            evaluatedGcAverage <= MixedPopulationAverageGcTargetBytes;
        report.meetsMemoryGrowthTarget =
            report.retainedMonoGrowthBytes
                <= MixedPopulationMemoryGrowthTargetBytes;
        report.meetsMixedPopulationTarget =
            report.meets60FpsP95
            && report.meetsSchedulerP95Target
            && report.meetsAverageGcTarget
            && report.meetsMemoryGrowthTarget;
#if UNITY_EDITOR
        report.slowFrames = slowFrameProfiles.ToArray();
#endif
        report.averageFps = report.frame.average > 0f
            ? 1000f / report.frame.average
            : 0f;
        report.onePercentLowFps = report.frame.p99 > 0f
            ? 1000f / report.frame.p99
            : 0f;
        report.framesOver16_67Ms = CountOver(frameSamples, sampleCount, 16.6667f);
        report.framesOver33_33Ms = CountOver(frameSamples, sampleCount, 33.3333f);
        report.meets60FpsP95 = report.frame.p95 <= 16.6667f;
        report.meets60FpsP99 = report.frame.p99 <= 16.6667f;
        report.meets60FpsEverySample = report.frame.maximum <= 16.6667f;
        report.meetsMixedPopulationTarget =
            report.meets60FpsP95
            && report.meetsSchedulerP95Target
            && report.meetsAverageGcTarget
            && report.meetsMemoryGrowthTarget;
    }

    private static void ForceManagedCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

#if UNITY_EDITOR
    private void BeginRawProfilerCapture()
    {
        if (options?.CaptureRawProfiler != true || rawProfilerCaptureActive)
        {
            return;
        }

        originalProfilerEnabled = ProfilerDriver.enabled;
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        rawProfilerCaptureActive = true;
        slowFrameProfiles.Clear();
    }

    private void EndRawProfilerCapture()
    {
        if (!rawProfilerCaptureActive)
        {
            return;
        }

        ProfilerDriver.enabled = originalProfilerEnabled;
        rawProfilerCaptureActive = false;
    }

    private void CaptureSlowProfilerFrame(
        float measuredFrameMilliseconds,
        int frameIndex)
    {
        using RawFrameDataView view =
            ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
        if (!view.valid || view.sampleCount <= 0)
        {
            return;
        }

        List<SlowFrameSample> samples = new List<SlowFrameSample>(64);
        for (int sampleIndex = 0; sampleIndex < view.sampleCount; sampleIndex++)
        {
            float sampleMilliseconds = view.GetSampleTimeMs(sampleIndex);
            if (sampleMilliseconds < 0.2f)
            {
                continue;
            }

            string sampleName = view.GetSampleName(sampleIndex);
            if (string.IsNullOrWhiteSpace(sampleName)
                || string.Equals(sampleName, "PlayerLoop", StringComparison.Ordinal)
                || string.Equals(sampleName, "Main Thread", StringComparison.Ordinal))
            {
                continue;
            }

            samples.Add(new SlowFrameSample
            {
                name = sampleName,
                milliseconds = sampleMilliseconds
            });
        }

        samples.Sort((left, right) =>
            right.milliseconds.CompareTo(left.milliseconds));
        if (samples.Count > 24)
        {
            samples.RemoveRange(24, samples.Count - 24);
        }

        Dictionary<string, long> allocationBytesByPath =
            new Dictionary<string, long>(StringComparer.Ordinal);
        List<string> samplePath = new List<string>(32);
        int allocationSampleIndex = 0;
        while (allocationSampleIndex < view.sampleCount)
        {
            CollectAllocationSamples(
                view,
                ref allocationSampleIndex,
                samplePath,
                allocationBytesByPath);
        }

        SlowFrameAllocation[] allocations = allocationBytesByPath
            .Select(pair => new SlowFrameAllocation
            {
                path = pair.Key,
                bytes = pair.Value
            })
            .OrderByDescending(entry => entry.bytes)
            .Take(24)
            .ToArray();

        slowFrameProfiles.Add(new SlowFrameProfile
        {
            measuredFrameMilliseconds = measuredFrameMilliseconds,
            profilerFrameMilliseconds = view.frameTimeMs,
            profilerFrameIndex = frameIndex,
            samples = samples.ToArray(),
            allocations = allocations
        });
    }

    private static void CollectAllocationSamples(
        RawFrameDataView view,
        ref int sampleIndex,
        List<string> samplePath,
        Dictionary<string, long> allocationBytesByPath)
    {
        if (sampleIndex < 0 || sampleIndex >= view.sampleCount)
        {
            return;
        }

        int currentIndex = sampleIndex++;
        string sampleName = view.GetSampleName(currentIndex);
        int childCount = view.GetSampleChildrenCount(currentIndex);
        bool isAllocation = string.Equals(
            sampleName,
            "GC.Alloc",
            StringComparison.Ordinal);
        if (isAllocation && view.GetSampleMetadataCount(currentIndex) > 0)
        {
            long bytes = Math.Max(
                0L,
                view.GetSampleMetadataAsLong(currentIndex, 0));
            string path = BuildAllocationPath(samplePath);
            if (allocationBytesByPath.TryGetValue(path, out long existing))
            {
                allocationBytesByPath[path] = existing + bytes;
            }
            else
            {
                allocationBytesByPath[path] = bytes;
            }
        }

        bool includeInPath = !string.IsNullOrWhiteSpace(sampleName)
            && !isAllocation
            && !string.Equals(sampleName, "PlayerLoop", StringComparison.Ordinal)
            && !string.Equals(sampleName, "Main Thread", StringComparison.Ordinal);
        if (includeInPath)
        {
            samplePath.Add(sampleName);
        }

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            CollectAllocationSamples(
                view,
                ref sampleIndex,
                samplePath,
                allocationBytesByPath);
        }

        if (includeInPath)
        {
            samplePath.RemoveAt(samplePath.Count - 1);
        }
    }

    private static string BuildAllocationPath(List<string> samplePath)
    {
        if (samplePath == null || samplePath.Count == 0)
        {
            return "<root>";
        }

        const int MaximumPathSegments = 6;
        int first = Mathf.Max(0, samplePath.Count - MaximumPathSegments);
        return string.Join(" > ", samplePath.Skip(first));
    }
#endif

    private void CaptureWorldSummary()
    {
        Scene scene = SceneManager.GetActiveScene();
        CharacterActor[] actors = FindSceneComponents<CharacterActor>(scene);
        BuildableObject[] buildings = FindSceneComponents<BuildableObject>(scene);
        Renderer[] renderers = FindSceneComponents<Renderer>(scene);
        Canvas[] canvases = FindSceneComponents<Canvas>(scene);
        WorldCharacterNameplate[] nameplates =
            FindSceneComponents<WorldCharacterNameplate>(scene);
        GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
        CharacterAiScheduler scheduler = FindSceneComponent<CharacterAiScheduler>(scene);
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);

        report.actualActorCount = actors.Count(actor =>
            actor != null && actor.gameObject.activeInHierarchy);
        report.actualBuildingCount = buildings.Count(building =>
            building != null && !building.isDestroy && building.gameObject.activeInHierarchy);
        report.activeRendererCount = renderers.Count(renderer =>
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy);
        report.visibleRendererCount = renderers.Count(renderer =>
            renderer != null
            && renderer.enabled
            && renderer.gameObject.activeInHierarchy
            && renderer.isVisible);
        report.activeCanvasCount = canvases.Count(canvas =>
            canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy);
        report.activeNameplateCount = nameplates.Count(nameplate =>
            nameplate != null && nameplate.gameObject.activeInHierarchy);
        if (scope?.Container != null)
        {
            DynamicFrameWorkSnapshot workSnapshot = scope.Container
                .Resolve<IDynamicFrameWorkBudget>()
                .GetSnapshot();
            report.dynamicWorkSmoothedFrameMilliseconds =
                workSnapshot.SmoothedFrameMilliseconds;
            report.dynamicWorkAvailableMilliseconds =
                workSnapshot.AvailableMilliseconds;
            report.dynamicWorkConsumedMilliseconds =
                workSnapshot.ConsumedMilliseconds;
            report.dynamicWorkBacklog = workSnapshot.TotalBacklog;
        }
        report.gridWidth = gridSystem?.grid?.width ?? 0;
        report.gridHeight = gridSystem?.grid?.height ?? 0;
        report.schedulerRegisteredCharacters =
            scheduler != null ? scheduler.RegisteredCharacterCount : 0;
        report.schedulerLastMilliseconds =
            scheduler != null ? scheduler.LastProcessingMilliseconds : 0d;
        report.schedulerLastDecisions =
            scheduler != null ? scheduler.LastProcessedDecisionCount : 0;
        report.schedulerLastLegacyFallbacks =
            scheduler != null ? scheduler.LastLegacyFallbackCount : 0;
        report.schedulerLastPathSearches =
            scheduler != null ? scheduler.LastPathSearchCount : 0;
        report.schedulerCurrentBudgetMilliseconds =
            scheduler != null ? scheduler.CurrentFrameBudgetMilliseconds : 0d;
        report.schedulerEstimatedDecisionMilliseconds =
            scheduler != null ? scheduler.EstimatedDecisionMilliseconds : 0d;
        report.schedulerEstimatedPathMilliseconds =
            scheduler != null ? scheduler.EstimatedPathSearchMilliseconds : 0d;
        report.schedulerSmoothedFrameMilliseconds =
            scheduler != null ? scheduler.SmoothedFrameMilliseconds : 0d;
        report.schedulerProcessedDecisions =
            scheduler != null ? scheduler.CumulativeProcessedDecisionCount : 0L;
        report.schedulerStarvedDecisions =
            scheduler != null ? scheduler.CumulativeStarvedDecisionCount : 0L;
        report.schedulerSkippedDecisions =
            scheduler != null ? scheduler.CumulativeSkippedDecisionCount : 0L;
        report.schedulerLegacyFallbacks =
            scheduler != null ? scheduler.CumulativeLegacyFallbackCount : 0L;
        report.schedulerOldestDeferralSeconds =
            scheduler != null ? scheduler.LastOldestDecisionDeferralSeconds : 0f;
        report.schedulerMaximumDeferralSeconds =
            scheduler != null ? scheduler.MaximumObservedDecisionDeferralSeconds : 0f;
        report.schedulerBudgetExhausted =
            scheduler != null && scheduler.LastBudgetExhausted;
        if (scope != null && scope.Container != null)
        {
            IFacilityCandidateCache facilityCache =
                scope.Container.Resolve<IFacilityCandidateCache>();
            report.facilityCandidateIndexPending =
                facilityCache.HasPendingIndexBuild;
            report.facilityCandidateIndexVersion =
                facilityCache.CandidateIndexVersion;
            report.aiPerformance = scope.Container
                .Resolve<ICharacterAiPerformanceRecorder>()
                .CaptureReport(report.schedulerRegisteredCharacters);
            ICharacterPresentationScheduler presentationScheduler =
                scope.Container.Resolve<ICharacterPresentationScheduler>();
            report.presentationRegisteredCharacters =
                presentationScheduler.RegisteredCount;
            report.presentationVisibleCharacters =
                presentationScheduler.VisibleCount;
            report.actualWildlifeCount = scope.Container
                .Resolve<IWildlifeRuntime>()
                .Wildlife
                .Count(actor => actor != null && actor.IsAlive);
            report.actualLivestockCount = scope.Container
                .Resolve<IAnimalHusbandryRuntime>()
                .Animals
                .Count;
            CaptureDeprivationSummary(scope, actors);
        }
        report.warningCount = warningCount;
        report.errorCount = errorCount;
        report.logMessages = capturedMessages.ToArray();
    }

    private void CaptureDeprivationSummary(
        DungeonRuntimeLifetimeScope scope,
        IReadOnlyList<CharacterActor> actors)
    {
        ICharacterDeprivationRuntime deprivationRuntime =
            scope.Container.Resolve<ICharacterDeprivationRuntime>();
        IWorldItemStackRuntime itemRuntime =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        var waterCandidates = new List<WorldItemStockCandidate>();
        itemRuntime.CopyAvailableStockCandidates(
            StockCategory.Water,
            waterCandidates);

        float totalThirst = 0f;
        int actorCount = 0;
        report.minimumThirst = 100f;
        report.maximumThirst = 0f;
        report.actorsBelowSafeDrinkThreshold = 0;
        report.actorsWithCriticalThirst = 0;
        report.actorsWithThirstWarningBurden = 0;
        report.actorsWithThirstBreakdownBurden = 0;
        report.activeDeprivationBreakdowns = 0;
        report.activeDesperateDrinkBreakdowns = 0;
        CharacterDeprivationDiagnosticsSnapshot deprivationDiagnostics =
            deprivationRuntime.GetDiagnostics();
        report.safeReliefRequests =
            deprivationDiagnostics.SafeReliefRequests;
        report.safeReliefPlanFailures =
            deprivationDiagnostics.SafeReliefPlanFailures;
        report.safeReliefActionsStarted =
            deprivationDiagnostics.SafeReliefActionsStarted;
        report.safeReliefStoredStackPlans =
            deprivationDiagnostics.SafeReliefStoredStackPlans;
        report.safeReliefMoveFailures =
            deprivationDiagnostics.SafeReliefMoveFailures;
        report.safeReliefBreakdownMoveFailures =
            deprivationDiagnostics.SafeReliefBreakdownMoveFailures;
        report.safeReliefBlockedMoveFailures =
            deprivationDiagnostics.SafeReliefBlockedMoveFailures;
        report.safeReliefOtherMoveFailures =
            deprivationDiagnostics.SafeReliefOtherMoveFailures;
        report.safeReliefStaleStartFailures =
            deprivationDiagnostics.SafeReliefStaleStartFailures;
        report.safeReliefWallBlockedFailures =
            deprivationDiagnostics.SafeReliefWallBlockedFailures;
        report.safeReliefDoorDeniedFailures =
            deprivationDiagnostics.SafeReliefDoorDeniedFailures;
        report.safeReliefDefenseReservationFailures =
            deprivationDiagnostics.SafeReliefDefenseReservationFailures;
        report.safeReliefTraversalChangedFailures =
            deprivationDiagnostics.SafeReliefTraversalChangedFailures;
        report.safeReliefArrivals =
            deprivationDiagnostics.SafeReliefArrivals;
        report.safeReliefInteractionAttempts =
            deprivationDiagnostics.SafeReliefInteractionAttempts;
        report.safeReliefSuccesses =
            deprivationDiagnostics.SafeReliefSuccesses;
        report.safeReliefRunningActions =
            deprivationDiagnostics.SafeReliefRunningActions;
        report.safeReliefActionsFinished =
            deprivationDiagnostics.SafeReliefActionsFinished;
        report.safeReliefPlannedPathSteps =
            deprivationDiagnostics.SafeReliefPlannedPathSteps;
        report.safeReliefAveragePlannedPathSteps =
            deprivationDiagnostics.SafeReliefActionsStarted > 0
                ? (float)deprivationDiagnostics.SafeReliefPlannedPathSteps
                    / deprivationDiagnostics.SafeReliefActionsStarted
                : 0f;
        report.safeReliefMaximumPlannedPathSteps =
            deprivationDiagnostics.SafeReliefMaximumPlannedPathSteps;
        report.safeReliefAverageDurationSeconds =
            deprivationDiagnostics.SafeReliefActionsFinished > 0
                ? deprivationDiagnostics.SafeReliefCompletedDurationSeconds
                    / deprivationDiagnostics.SafeReliefActionsFinished
                : 0f;
        report.safeReliefMaximumDurationSeconds =
            deprivationDiagnostics.SafeReliefMaximumDurationSeconds;
        report.safeReliefCancelledMoveFailures =
            deprivationDiagnostics.SafeReliefCancelledMoveFailures;
        report.safeReliefMissingPathFailures =
            deprivationDiagnostics.SafeReliefMissingPathFailures;
        report.safeReliefMissingMovementHandlerFailures =
            deprivationDiagnostics.SafeReliefMissingMovementHandlerFailures;
        report.safeReliefGridUnavailableFailures =
            deprivationDiagnostics.SafeReliefGridUnavailableFailures;
        report.safeReliefInvalidSpeedFailures =
            deprivationDiagnostics.SafeReliefInvalidSpeedFailures;
        report.safeReliefNoFailureReasonFailures =
            deprivationDiagnostics.SafeReliefNoFailureReasonFailures;
        report.safeReliefActorDeadMoveFailures =
            deprivationDiagnostics.SafeReliefActorDeadMoveFailures;
        report.safeReliefActorMissingMoveFailures =
            deprivationDiagnostics.SafeReliefActorMissingMoveFailures;
        report.safeReliefCrossFloorTargetPlans =
            deprivationDiagnostics.SafeReliefCrossFloorTargetPlans;
        report.safeReliefPathsWithVerticalTraversal =
            deprivationDiagnostics.SafeReliefPathsWithVerticalTraversal;
        report.safeReliefVerticalTraversalSteps =
            deprivationDiagnostics.SafeReliefVerticalTraversalSteps;
        report.desperateDrinkAttempts =
            deprivationDiagnostics.DesperateDrinkAttempts;
        report.desperateDrinkStackMoveFailures =
            deprivationDiagnostics.DesperateDrinkStackMoveFailures;
        report.desperateDrinkStackArrivals =
            deprivationDiagnostics.DesperateDrinkStackArrivals;
        report.desperateDrinkStackConsumptions =
            deprivationDiagnostics.DesperateDrinkStackConsumptions;
        report.waterStockCandidateCount = waterCandidates.Count;
        report.storedWaterCandidateCount = 0;
        report.looseWaterCandidateCount = 0;
        report.storedWaterQuantity = 0;
        report.looseWaterQuantity = 0;
        report.availableWaterQuantity = 0;
        report.waterCandidateCountByFloor =
            new int[Mathf.Max(1, report.gridHeight)];
        report.waterQuantityByFloor =
            new int[report.waterCandidateCountByFloor.Length];
        for (int index = 0; index < waterCandidates.Count; index++)
        {
            WorldItemStockCandidate candidate = waterCandidates[index];
            int quantity = Mathf.Max(0, candidate.Quantity);
            report.availableWaterQuantity += quantity;
            if (candidate.Position.y >= 0
                && candidate.Position.y
                    < report.waterCandidateCountByFloor.Length)
            {
                report.waterCandidateCountByFloor[candidate.Position.y]++;
                report.waterQuantityByFloor[candidate.Position.y] += quantity;
            }
            if (candidate.State == WorldItemStackState.Stored)
            {
                report.storedWaterCandidateCount++;
                report.storedWaterQuantity += quantity;
            }
            else if (candidate.State == WorldItemStackState.Loose)
            {
                report.looseWaterCandidateCount++;
                report.looseWaterQuantity += quantity;
            }
        }

        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor != null
                && actor.gameObject.activeInHierarchy
                && actor.IsDead)
            {
                report.deadActorCount++;
            }
            if (actor != null && actor.IsOwner)
            {
                report.ownerPresent = true;
                report.ownerAlive = !actor.IsDead;
            }
            if (actor == null
                || actor.IsDead
                || !actor.gameObject.activeInHierarchy
                || actor.Stats == null
                || !actor.Stats.TryGetConditionValue(
                    CharacterCondition.THIRST,
                    out float thirst))
            {
                continue;
            }

            actorCount++;
            totalThirst += thirst;
            report.minimumThirst = Mathf.Min(report.minimumThirst, thirst);
            report.maximumThirst = Mathf.Max(report.maximumThirst, thirst);
            if (thirst < 65f)
            {
                report.actorsBelowSafeDrinkThreshold++;
            }
            if (thirst < 20f)
            {
                report.actorsWithCriticalThirst++;
            }

            if (!deprivationRuntime.TryGetSnapshot(
                    actor,
                    out CharacterDeprivationSnapshot snapshot))
            {
                continue;
            }

            if (snapshot.Burdens != null
                && snapshot.Burdens.TryGetValue(
                    DeprivationKind.Thirst,
                    out float burden))
            {
                if (burden >= 40f)
                {
                    report.actorsWithThirstWarningBurden++;
                }
                if (burden >= 70f)
                {
                    report.actorsWithThirstBreakdownBurden++;
                }
            }

            if (snapshot.Breakdown?.active == true)
            {
                report.activeDeprivationBreakdowns++;
                if (snapshot.Breakdown.kind ==
                    CharacterBreakdownKind.DesperateDrink)
                {
                    report.activeDesperateDrinkBreakdowns++;
                }
            }
        }

        report.averageThirst = actorCount > 0
            ? totalThirst / actorCount
            : 0f;
        if (actorCount == 0)
        {
            report.minimumThirst = 0f;
        }
    }

    private void ResetAiPerformanceRecorder()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        if (scope != null && scope.Container != null)
        {
            scope.Container.Resolve<ICharacterAiPerformanceRecorder>().Reset();
            scope.Container.Resolve<ICharacterDeprivationRuntime>()
                .ResetDiagnostics();
        }
    }

    private IEnumerator FinishProfile()
    {
        if (finished)
        {
            yield break;
        }

        finished = true;
        CaptureWorldSummary();
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)
            ?? Application.persistentDataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(options.ScreenshotPath)
            ?? Application.persistentDataPath);
        File.WriteAllText(options.ReportPath, JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(options.ScreenshotPath);
        UnityEngine.Debug.Log(
            $"GAMEPLAY_PERFORMANCE_PROFILE_COMPLETE {options.ProfileId} "
            + $"valid={report.valid} p95={report.frame.p95:F3}ms "
            + $"p99={report.frame.p99:F3}ms actors={report.actualActorCount} "
            + $"livestock={report.actualLivestockCount} "
            + $"buildings={report.actualBuildingCount} report={options.ReportPath}");

        yield return new WaitForSecondsRealtime(Mathf.Max(2f, options.HoldSeconds));
        RestoreFrameSettings();
#if UNITY_EDITOR
        if (options.IsEditorProfile)
        {
            UnityEditor.EditorApplication.isPlaying = false;
            yield break;
        }
#endif
        Application.Quit(report.valid ? 0 : 2);
    }

    private void InitializeReport()
    {
        report.profileId = options.ProfileId;
        report.utcTimestamp = DateTime.UtcNow.ToString("O");
        report.applicationVersion = Application.version;
        report.unityVersion = Application.unityVersion;
        report.operatingSystem = SystemInfo.operatingSystem;
        report.processor = SystemInfo.processorType;
        report.processorCount = SystemInfo.processorCount;
        report.systemMemoryMb = SystemInfo.systemMemorySize;
        report.graphicsDevice = SystemInfo.graphicsDeviceName;
        report.graphicsMemoryMb = SystemInfo.graphicsMemorySize;
        report.screenWidth = Screen.width;
        report.screenHeight = Screen.height;
        report.requestedActorCount = options.ActorCount;
        report.requestedLivestockCount = options.LivestockCount;
        report.requestedFacilityCount = options.FacilityCount;
        report.requestedGridWidth = options.GridWidth;
        report.requestedGridHeight = options.GridHeight;
        report.requestedActiveFloors = options.ActiveFloors;
        report.requestedSimulationSpeed = options.SimulationSpeed;
        report.vSyncDisabled = true;
        report.targetFrameRate = -1;
        report.measurementIncludesRendering = true;
        report.measurementIncludesUi = true;
        report.measurementIncludesPhysics = true;
        report.measurementUsesNormalNewRun = true;
        report.measurementUsesRealCharacterPrefab = true;
        report.measurementUsesRealBuildingObjects = true;
        report.measurementUsesRealWildlifeActors = true;
        report.measurementUsesAnimalHusbandryRuntime = true;
    }

    private bool ValidateReport()
    {
        if (sampleCount < 120
            || report.errorCount > 0
            || report.gridWidth < options.GridWidth
            || report.gridHeight < options.GridHeight)
        {
            return false;
        }

        if (options.ActorCount > 0 && report.actualActorCount < options.ActorCount)
        {
            return false;
        }

        if (options.LivestockCount > 0
            && (report.actualLivestockCount < options.LivestockCount
                || report.actualStressLivestockCount < options.LivestockCount
                || !report.meetsMixedPopulationTarget))
        {
            return false;
        }

        return options.FacilityCount <= 0
            || report.actualDenseFacilityCount >= options.FacilityCount;
    }

    private string BuildFailureReason()
    {
        return $"samples={sampleCount}; errors={report.errorCount}; "
            + $"actors={report.actualActorCount}/{options.ActorCount}; "
            + $"livestock={report.actualLivestockCount}/{options.LivestockCount}; "
            + $"facilities={report.actualDenseFacilityCount}/{options.FacilityCount}; "
            + $"grid={report.gridWidth}x{report.gridHeight}; "
            + $"frameP95={report.frame?.p95 ?? 0f:0.###}; "
            + $"schedulerP95={report.aiBudget?.p95 ?? 0f:0.###}; "
            + $"avgGcBytes={report.gc?.averageBytes ?? 0d:0}; "
            + $"baselineGcBytes={report.editorBaselineGcAverageBytes:0}; "
            + $"incrementalGcBytes={report.gameplayIncrementalGcAverageBytes:0}; "
            + $"sustainedMonoGrowthBytes={report.sustainedMonoGrowthBytes}; "
            + $"retainedMonoGrowthBytes={report.retainedMonoGrowthBytes}";
    }

    private void UnpauseGameplay()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameManager gameManager = FindSceneComponent<GameManager>(scene);
        if (gameManager != null)
        {
            gameManager.isPause = false;
        }

        DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        if (scope != null
            && scope.Container != null
            && scope.Container.Resolve<IGameDataProvider>()
                .TryGetGameData(out GameData gameData))
        {
            gameData.gameSpeed.Value = Mathf.Clamp(
                Mathf.RoundToInt(options.SimulationSpeed),
                1,
                5);
        }

        DungeonRuntimeLifetimeScope runtimeScope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        if (runtimeScope?.Container != null)
        {
            runtimeScope.Container
                .Resolve<IGameTimeScaleController>()
                .Scale = options.SimulationSpeed;
        }
        else
        {
            Time.timeScale = options.SimulationSpeed;
            Time.fixedDeltaTime = originalFixedDeltaTime
                * options.SimulationSpeed;
        }
    }

    private static bool IsDenseFacilityDefinition(BuildingSO definition)
    {
        if (definition == null
            || definition.IsWall
            || definition.IsDoor
            || definition.sprite == null
            || (definition.Placement.Layer != GridLayer.Building
                && !definition.UsesIndependentRenderer)
            || definition.type == null
            || !typeof(BuildableObject).IsAssignableFrom(definition.type))
        {
            return false;
        }

        GridBuildingPlacement placement = definition.Placement;
        bool isFacilityLayer = placement.Layer == GridLayer.Building
            || placement.Layer == GridLayer.WallFixture
            || placement.Layer == GridLayer.CeilingFixture
            || placement.Layer == GridLayer.FloorOverlay;
        return isFacilityLayer
            && placement.Width >= 1
            && placement.Width <= 2
            && placement.Height == 1;
    }

    private static IReadOnlyList<BuildingSO> SelectDenseFacilityDefinitions(
        IEnumerable<BuildingSO> source,
        int requestedCount)
    {
        List<BuildingSO> all = (source ?? Enumerable.Empty<BuildingSO>())
            .Where(IsDenseFacilityDefinition)
            .OrderBy(definition => definition.id)
            .ToList();
        int targetCount = Mathf.Clamp(requestedCount, 0, all.Count);
        List<BuildingSO> selected = new List<BuildingSO>(targetCount);

        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingStorageAbility>() != null);
        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingWaterSourceAbility>() != null);
        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingCropPlotAbility>() != null);
        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingProductionAbility>() != null);
        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingNeedRecoveryAbility>() != null);
        AddFirstDenseFacility(
            all,
            selected,
            definition =>
                definition.GetAbility<BuildingButcherAbility>() != null);

        int sampleIndex = 0;
        while (selected.Count < targetCount && sampleIndex < all.Count * 2)
        {
            int index = targetCount <= 1
                ? 0
                : Mathf.RoundToInt(
                    (all.Count - 1)
                    * (sampleIndex % targetCount)
                    / (float)(targetCount - 1));
            BuildingSO candidate = all[index];
            if (!selected.Contains(candidate))
            {
                selected.Add(candidate);
            }

            sampleIndex++;
        }

        for (int index = 0;
             index < all.Count && selected.Count < targetCount;
             index++)
        {
            if (!selected.Contains(all[index]))
            {
                selected.Add(all[index]);
            }
        }

        return selected;
    }

    private static List<BuildingSO> BuildDenseFacilityPlacementSequence(
        IReadOnlyList<BuildingSO> source)
    {
        if (source == null || source.Count == 0)
        {
            return new List<BuildingSO>();
        }

        BuildingSO water = source.FirstOrDefault(definition =>
            definition?.GetAbility<BuildingWaterSourceAbility>() != null);
        BuildingSO storage = source.FirstOrDefault(definition =>
            definition?.GetAbility<BuildingStorageAbility>() != null);
        List<BuildingSO> remaining = source
            .Where(definition =>
                definition != null
                && definition != water
                && definition != storage)
            .ToList();
        if (remaining.Count == 0)
        {
            remaining.AddRange(source.Where(definition => definition != null));
        }

        const int SequenceLength = 16;
        List<BuildingSO> sequence =
            new List<BuildingSO>(SequenceLength);
        int remainingIndex = 0;
        for (int slot = 0; slot < SequenceLength; slot++)
        {
            bool waterSlot = slot == 0 || slot == 5 || slot == 10 || slot == 15;
            bool storageSlot = slot == 4 || slot == 12;
            if (waterSlot && water != null)
            {
                sequence.Add(water);
                continue;
            }

            if (storageSlot && storage != null)
            {
                sequence.Add(storage);
                continue;
            }

            sequence.Add(remaining[remainingIndex % remaining.Count]);
            remainingIndex++;
        }

        return sequence;
    }

    private static void AddFirstDenseFacility(
        IReadOnlyList<BuildingSO> source,
        ICollection<BuildingSO> destination,
        Func<BuildingSO, bool> predicate)
    {
        for (int index = 0; index < source.Count; index++)
        {
            BuildingSO candidate = source[index];
            if (predicate(candidate) && !destination.Contains(candidate))
            {
                destination.Add(candidate);
                return;
            }
        }
    }

    private BuildingSO CloneWithoutRoomRequirement(BuildingSO source)
    {
        BuildingSO clone = CloneDefinition(source);
        clone.AbilityModules.Remove<BuildingRoomRequirementAbility>();
        BuildingStorageAbility storage =
            clone.GetAbility<BuildingStorageAbility>();
        if (storage != null)
        {
            storage.allCategories = true;
            storage.capacity = Mathf.Max(storage.capacity, 512);
        }

        return clone;
    }

    private BuildingSO CloneDefinition(BuildingSO source)
    {
        BuildingSO clone = Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        runtimeDefinitions.Add(clone);
        return clone;
    }

    private static bool TryPlaceBuilding(
        DungeonRuntimeLifetimeScope scope,
        GridBuildingFactory factory,
        Grid grid,
        BuildingSO definition,
        Vector2Int position,
        out BuildableObject building)
    {
        building = null;
        IReadOnlyList<Vector2Int> footprint = definition.GetGridPosList(position);
        if (footprint == null
            || footprint.Count == 0
            || footprint.Any(cellPosition =>
                !grid.IsValidGridPos(cellPosition)
                || !grid.GetGridCell(cellPosition).CanOccupy(definition.Placement.Layer)))
        {
            return false;
        }

        building = factory.Create(grid, definition, position);
        if (building == null)
        {
            return false;
        }

        scope.Container.Inject(building);
        building.SetGrid(grid);
        building.Initialization(definition, position);
        if (building is IWarehouseFacility warehouse
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null)
        {
            WarehouseInventorySnapshot emptyInventory =
                warehouse.Inventory.CreateSnapshot();
            emptyInventory.stocks.Clear();
            warehouse.Inventory.ApplySnapshot(emptyInventory);
        }
        if (grid.RegisterOccupant(
                building,
                definition.Placement.Layer,
                footprint,
                definition.Placement.IsMovement))
        {
            return true;
        }

        Destroy(building.gameObject);
        building = null;
        return false;
    }

    private static void RebindExistingBuildings(Scene scene, Grid grid)
    {
        foreach (BuildableObject building in FindSceneComponents<BuildableObject>(scene))
        {
            if (building != null)
            {
                building.SetGrid(grid);
            }
        }
    }

    private static void RegisterTraversalColumn(Grid grid, int x, int floorCount)
    {
        PerformanceStairOccupant stair = new PerformanceStairOccupant();
        for (int y = 0; y < floorCount; y++)
        {
            List<GridTraversalLink> links = new List<GridTraversalLink>(2);
            if (y > 0)
            {
                links.Add(new GridTraversalLink(
                    new Vector2Int(x, y - 1),
                    stair,
                    GridMoveType.Stair));
            }

            if (y + 1 < floorCount)
            {
                links.Add(new GridTraversalLink(
                    new Vector2Int(x, y + 1),
                    stair,
                    GridMoveType.Stair));
            }

            grid.GetGridCell(new Vector2Int(x, y)).SetTraversalLinks(links);
        }
    }

    private static Vector2Int GetStressActorPosition(
        int index,
        Grid grid,
        int activeFloors)
    {
        if (index < VisibleStressActorCount)
        {
            int localWidth = Mathf.Min(32, Mathf.Max(1, grid.width - 2));
            return new Vector2Int(
                1 + index % localWidth,
                (index / localWidth) % Mathf.Min(3, activeFloors));
        }

        int distributedIndex = index - VisibleStressActorCount;
        int x = 1 + (distributedIndex * 37) % Mathf.Max(1, grid.width - 2);
        int y = distributedIndex % activeFloors;
        return new Vector2Int(x, y);
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        T[] components = FindSceneComponents<T>(scene);
        return components.FirstOrDefault(component => component != null);
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid())
        {
            return Array.Empty<T>();
        }

        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return result.ToArray();
    }

    private static float ReadRecorderMilliseconds(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.LastValue > 0
            ? recorder.LastValue / 1_000_000f
            : 0f;
    }

    private static int CountOver(float[] samples, int count, float threshold)
    {
        int result = 0;
        for (int i = 0; i < count; i++)
        {
            if (samples[i] > threshold)
            {
                result++;
            }
        }

        return result;
    }

    private static long AverageWindow(
        long[] values,
        int count,
        int start,
        int length)
    {
        if (values == null || count <= 0 || length <= 0)
        {
            return 0L;
        }

        int from = Mathf.Clamp(start, 0, count - 1);
        int to = Mathf.Clamp(from + length, from + 1, count);
        decimal sum = 0m;
        for (int index = from; index < to; index++)
        {
            sum += Math.Max(0L, values[index]);
        }

        return (long)(sum / Mathf.Max(1, to - from));
    }

    private static bool HasCommandLineArgument(string argument)
    {
        return Environment.GetCommandLineArgs().Any(value =>
            string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error
            || type == LogType.Assert
            || type == LogType.Exception)
        {
            errorCount++;
        }
        else if (type == LogType.Warning)
        {
            warningCount++;
        }

        if ((type == LogType.Warning
                || type == LogType.Error
                || type == LogType.Assert
                || type == LogType.Exception)
            && capturedMessages.Count < 20)
        {
            capturedMessages.Add($"{type}: {condition}");
        }
    }

    private void DisposeRecorders()
    {
        if (mainThreadRecorder.Valid)
        {
            mainThreadRecorder.Dispose();
        }

        if (renderThreadRecorder.Valid)
        {
            renderThreadRecorder.Dispose();
        }

        if (gcAllocationRecorder.Valid)
        {
            gcAllocationRecorder.Dispose();
        }

        DisposeRecorder(ref gcCollectRecorder);
        DisposeRecorder(ref aiBudgetRecorder);
        DisposeRecorder(ref characterStatsRecorder);
        DisposeRecorder(ref aiDirectorRecorder);
        DisposeRecorder(ref abilityMoveRecorder);
        DisposeRecorder(ref abilityWorkRecorder);
        if (runtimeTickRecorders != null)
        {
            for (int index = 0; index < runtimeTickRecorders.Length; index++)
            {
                DisposeRecorder(ref runtimeTickRecorders[index]);
            }
        }
    }

    private static void DisposeRecorder(ref ProfilerRecorder recorder)
    {
        if (recorder.Valid)
        {
            recorder.Dispose();
        }
    }

    private void RestoreFrameSettings()
    {
        QualitySettings.vSyncCount = originalVSyncCount;
        Application.targetFrameRate = originalTargetFrameRate;
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
    }

    private sealed class PerformanceHallwayOccupant : IGridOccupant
    {
        private static int nextId = -500000;
        private readonly int id = nextId--;

        public int GridId => id;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => false;
    }

    private sealed class PerformanceStairOccupant :
        IGridOccupant,
        IGridMovementOccupant,
        IGridMovementHandler,
        IGridTraversalCostProvider
    {
        private static int nextId = -600000;
        private readonly int id = nextId--;

        public int GridId => id;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Stair;

        public int GetTraversalCostUnits()
        {
            return DefaultGridTraversalCostPolicy.StairFallbackCost;
        }

        public IEnumerator Traverse(CharacterActor actor, GridMoveStep step)
        {
            if (actor == null || !step.IsValid)
            {
                yield break;
            }

            AbilityMove movement = actor.GetAbility<AbilityMove>();
            if (movement == null)
            {
                yield break;
            }

            actor.HideForTraversal(5f);
            try
            {
                yield return movement.Move2GridPosition(step.To);
            }
            finally
            {
                actor.RestoreTraversalVisibility();
            }
        }
    }

    [Serializable]
    private sealed class GameplayPerformanceReport
    {
        public string profileId;
        public string utcTimestamp;
        public string applicationVersion;
        public string unityVersion;
        public string operatingSystem;
        public string processor;
        public int processorCount;
        public int systemMemoryMb;
        public string graphicsDevice;
        public int graphicsMemoryMb;
        public int screenWidth;
        public int screenHeight;
        public bool aiSchedulerDisabled;
        public bool characterPresentationDisabled;
        public bool characterStatsUpdatesDisabled;
        public int requestedActorCount;
        public int requestedLivestockCount;
        public int requestedFacilityCount;
        public int requestedGridWidth;
        public int requestedGridHeight;
        public int requestedActiveFloors;
        public float requestedSimulationSpeed;
        public int normalOperationSupplyDays;
        public int normalOperationWarehouseCount;
        public int seededWarehouseFoodAmount;
        public int seededWarehouseWaterAmount;
        public int seededLooseFoodAmount;
        public int seededLooseWaterAmount;
        public int seededFoodAmount;
        public int seededWaterAmount;
        public int waterStockCandidateCount;
        public int storedWaterCandidateCount;
        public int looseWaterCandidateCount;
        public int storedWaterQuantity;
        public int looseWaterQuantity;
        public int availableWaterQuantity;
        public int[] waterCandidateCountByFloor;
        public int[] waterQuantityByFloor;
        public float minimumThirst;
        public float averageThirst;
        public float maximumThirst;
        public int actorsBelowSafeDrinkThreshold;
        public int actorsWithCriticalThirst;
        public int actorsWithThirstWarningBurden;
        public int actorsWithThirstBreakdownBurden;
        public int activeDeprivationBreakdowns;
        public int activeDesperateDrinkBreakdowns;
        public int safeReliefRequests;
        public int safeReliefPlanFailures;
        public int safeReliefActionsStarted;
        public int safeReliefStoredStackPlans;
        public int safeReliefMoveFailures;
        public int safeReliefBreakdownMoveFailures;
        public int safeReliefBlockedMoveFailures;
        public int safeReliefOtherMoveFailures;
        public int safeReliefStaleStartFailures;
        public int safeReliefWallBlockedFailures;
        public int safeReliefDoorDeniedFailures;
        public int safeReliefDefenseReservationFailures;
        public int safeReliefTraversalChangedFailures;
        public int safeReliefArrivals;
        public int safeReliefInteractionAttempts;
        public int safeReliefSuccesses;
        public int safeReliefRunningActions;
        public int safeReliefActionsFinished;
        public long safeReliefPlannedPathSteps;
        public float safeReliefAveragePlannedPathSteps;
        public int safeReliefMaximumPlannedPathSteps;
        public float safeReliefAverageDurationSeconds;
        public float safeReliefMaximumDurationSeconds;
        public int safeReliefCancelledMoveFailures;
        public int safeReliefMissingPathFailures;
        public int safeReliefMissingMovementHandlerFailures;
        public int safeReliefGridUnavailableFailures;
        public int safeReliefInvalidSpeedFailures;
        public int safeReliefNoFailureReasonFailures;
        public int safeReliefActorDeadMoveFailures;
        public int safeReliefActorMissingMoveFailures;
        public int safeReliefCrossFloorTargetPlans;
        public int safeReliefPathsWithVerticalTraversal;
        public long safeReliefVerticalTraversalSteps;
        public int desperateDrinkAttempts;
        public int desperateDrinkStackMoveFailures;
        public int desperateDrinkStackArrivals;
        public int desperateDrinkStackConsumptions;
        public int actualActorCount;
        public int deadActorCount;
        public bool ownerPresent;
        public bool ownerAlive;
        public int actualStressActorCount;
        public int preexistingSkillGenerationRequestsCancelled;
        public bool syntheticSkillGenerationRequestsCancelled;
        public int actualWildlifeCount;
        public int actualLivestockCount;
        public int actualStressLivestockCount;
        public int actualBuildingCount;
        public int actualDenseFacilityCount;
        public int actualDenseDoorCount;
        public int activeRendererCount;
        public int visibleRendererCount;
        public int activeCanvasCount;
        public int activeNameplateCount;
        public double dynamicWorkSmoothedFrameMilliseconds;
        public double dynamicWorkAvailableMilliseconds;
        public double dynamicWorkConsumedMilliseconds;
        public int dynamicWorkBacklog;
        public int gridWidth;
        public int gridHeight;
        public int schedulerRegisteredCharacters;
        public int presentationRegisteredCharacters;
        public int presentationVisibleCharacters;
        public double schedulerLastMilliseconds;
        public int schedulerLastDecisions;
        public int schedulerLastLegacyFallbacks;
        public int schedulerLastPathSearches;
        public double schedulerCurrentBudgetMilliseconds;
        public double schedulerEstimatedDecisionMilliseconds;
        public double schedulerEstimatedPathMilliseconds;
        public double schedulerSmoothedFrameMilliseconds;
        public long schedulerProcessedDecisions;
        public long schedulerStarvedDecisions;
        public long schedulerSkippedDecisions;
        public long schedulerLegacyFallbacks;
        public float schedulerOldestDeferralSeconds;
        public float schedulerMaximumDeferralSeconds;
        public bool schedulerBudgetExhausted;
        public bool facilityCandidateIndexPending;
        public int facilityCandidateIndexVersion;
        public int sampleCount;
        public float sampleDurationSeconds;
        public double setupMilliseconds;
        public double totalProfileMilliseconds;
        public float averageFps;
        public float onePercentLowFps;
        public int framesOver16_67Ms;
        public int framesOver33_33Ms;
        public long monoUsedBytesAtStart;
        public long monoUsedBytesAtEnd;
        public long monoUsedBytesAfterStartCollection;
        public long monoUsedBytesAfterEndCollection;
        public long totalAllocatedBytesAtStart;
        public long totalAllocatedBytesAtEnd;
        public long monoUsedFirstQuarterAverageBytes;
        public long monoUsedLastQuarterAverageBytes;
        public long sustainedMonoGrowthBytes;
        public long retainedMonoGrowthBytes;
        public double editorBaselineGcAverageBytes;
        public double gameplayIncrementalGcAverageBytes;
        public int warningCount;
        public int errorCount;
        public bool valid;
        public bool meets60FpsP95;
        public bool meets60FpsP99;
        public bool meets60FpsEverySample;
        public bool meetsSchedulerP95Target;
        public bool meetsAverageGcTarget;
        public bool meetsMemoryGrowthTarget;
        public bool meetsMixedPopulationTarget;
        public bool usesEditorBaselineAdjustedGcTarget;
        public bool vSyncDisabled;
        public int targetFrameRate;
        public bool measurementIncludesRendering;
        public bool measurementIncludesUi;
        public bool measurementIncludesPhysics;
        public bool measurementUsesNormalNewRun;
        public bool measurementUsesRealCharacterPrefab;
        public bool measurementUsesRealBuildingObjects;
        public bool measurementUsesRealWildlifeActors;
        public bool measurementUsesAnimalHusbandryRuntime;
        public string failureReason;
        public string[] logMessages;
        public FrameMetric frame;
        public FrameMetric mainThread;
        public FrameMetric renderThread;
        public FrameMetric gcCollect;
        public FrameMetric aiBudget;
        public FrameMetric characterStats;
        public FrameMetric aiDirector;
        public FrameMetric abilityMove;
        public FrameMetric abilityWork;
        public NamedFrameMetric[] runtimeTicks;
        public AllocationMetric gc;
        public CharacterAiPerformanceReport aiPerformance;
        public SlowFrameProfile[] slowFrames;
    }

    [Serializable]
    private sealed class SlowFrameProfile
    {
        public float measuredFrameMilliseconds;
        public float profilerFrameMilliseconds;
        public int profilerFrameIndex;
        public SlowFrameSample[] samples;
        public SlowFrameAllocation[] allocations;
    }

    [Serializable]
    private sealed class NamedFrameMetric
    {
        public string name;
        public FrameMetric metric;
    }

    [Serializable]
    private sealed class SlowFrameSample
    {
        public string name;
        public float milliseconds;
    }

    [Serializable]
    private sealed class SlowFrameAllocation
    {
        public string path;
        public long bytes;
    }

    [Serializable]
    private sealed class FrameMetric
    {
        public float average;
        public float p50;
        public float p95;
        public float p99;
        public float maximum;

        public static FrameMetric From(float[] samples, int count)
        {
            return Calculate(samples, count, includeZero: true);
        }

        public static FrameMetric FromPositive(float[] samples, int count)
        {
            return Calculate(samples, count, includeZero: false);
        }

        private static FrameMetric Calculate(
            float[] samples,
            int count,
            bool includeZero)
        {
            if (samples == null || count <= 0)
            {
                return new FrameMetric();
            }

            float[] sorted = new float[count];
            int validCount = 0;
            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                float value = samples[i];
                if (!includeZero && value <= 0f)
                {
                    continue;
                }

                sorted[validCount++] = value;
                sum += value;
            }

            if (validCount == 0)
            {
                return new FrameMetric();
            }

            Array.Sort(sorted, 0, validCount);
            return new FrameMetric
            {
                average = (float)(sum / validCount),
                p50 = Percentile(sorted, validCount, 0.50f),
                p95 = Percentile(sorted, validCount, 0.95f),
                p99 = Percentile(sorted, validCount, 0.99f),
                maximum = sorted[validCount - 1]
            };
        }

        private static float Percentile(float[] sorted, int count, float percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt(count * percentile) - 1,
                0,
                count - 1);
            return sorted[index];
        }
    }

    [Serializable]
    private sealed class AllocationMetric
    {
        public double averageBytes;
        public long p95Bytes;
        public long maximumBytes;

        public static AllocationMetric From(long[] samples, int count)
        {
            if (samples == null || count <= 0)
            {
                return new AllocationMetric();
            }

            long[] sorted = new long[count];
            long maximum = 0;
            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                long value = Math.Max(0, samples[i]);
                sorted[i] = value;
                sum += value;
                maximum = Math.Max(maximum, value);
            }

            Array.Sort(sorted);
            int p95Index = Mathf.Clamp(
                Mathf.CeilToInt(count * 0.95f) - 1,
                0,
                count - 1);
            return new AllocationMetric
            {
                averageBytes = sum / count,
                p95Bytes = sorted[p95Index],
                maximumBytes = maximum
            };
        }
    }

    private sealed class GameplayPerformanceOptions
    {
        public string ProfileId { get; private set; } = "actual-gameplay";
        public int ActorCount { get; private set; }
        public int LivestockCount { get; private set; }
        public int FacilityCount { get; private set; }
        public int GridWidth { get; private set; } = 60;
        public int GridHeight { get; private set; } = 3;
        public int ActiveFloors { get; private set; } = 3;
        public int NormalOperationSupplyDays { get; private set; }
        public float SimulationSpeed { get; private set; } = 1f;
        public int RoomSpan { get; private set; } = 16;
        public int WarmupFrames { get; private set; } = DefaultWarmupFrames;
        public float SampleSeconds { get; private set; } = DefaultSampleSeconds;
        public float HoldSeconds { get; private set; } = 4f;
        public bool DisableAiScheduler { get; private set; }
        public bool DisableCharacterPresentation { get; private set; }
        public bool DisableCharacterStatsUpdates { get; private set; }
        public bool CaptureRawProfiler { get; private set; }
        public bool HasDiagnosticIsolation =>
            DisableAiScheduler
            || DisableCharacterPresentation
            || DisableCharacterStatsUpdates;
        public string ReportPath { get; private set; }
        public string ScreenshotPath { get; private set; }
        public bool IsEditorProfile { get; private set; }

#if UNITY_EDITOR
        public static GameplayPerformanceOptions CreateEditor(
            string profileId,
            int actorCount,
            int facilityCount,
            int gridWidth,
            int gridHeight,
            int activeFloors,
            int warmupFrames,
            float sampleSeconds,
            string reportPath,
            string screenshotPath,
            float simulationSpeed,
            bool disableAiScheduler,
            bool disableCharacterPresentation,
            bool disableCharacterStatsUpdates,
            bool captureRawProfiler,
            int livestockCount,
            int normalOperationSupplyDays)
        {
            return new GameplayPerformanceOptions
            {
                ProfileId = string.IsNullOrWhiteSpace(profileId)
                    ? "editor-gameplay"
                    : profileId,
                ActorCount = Mathf.Clamp(actorCount, 0, 5000),
                LivestockCount = Mathf.Clamp(livestockCount, 0, 5000),
                FacilityCount = Mathf.Clamp(facilityCount, 0, 100000),
                GridWidth = Mathf.Clamp(gridWidth, 1, 1024),
                GridHeight = Mathf.Clamp(gridHeight, 1, 1024),
                ActiveFloors = Mathf.Clamp(activeFloors, 1, Mathf.Max(1, gridHeight)),
                NormalOperationSupplyDays = Mathf.Clamp(
                    normalOperationSupplyDays,
                    0,
                    30),
                SimulationSpeed = Mathf.Clamp(simulationSpeed, 0.1f, 5f),
                RoomSpan = 16,
                WarmupFrames = Mathf.Clamp(warmupFrames, 1, 3600),
                SampleSeconds = Mathf.Clamp(sampleSeconds, 2f, 120f),
                HoldSeconds = 2f,
                DisableAiScheduler = disableAiScheduler,
                DisableCharacterPresentation = disableCharacterPresentation,
                DisableCharacterStatsUpdates = disableCharacterStatsUpdates,
                CaptureRawProfiler = captureRawProfiler,
                ReportPath = Path.GetFullPath(reportPath),
                ScreenshotPath = Path.GetFullPath(screenshotPath),
                IsEditorProfile = true
            };
        }
#endif

        public static GameplayPerformanceOptions Parse(string[] arguments)
        {
            GameplayPerformanceOptions options = new GameplayPerformanceOptions();
            options.ProfileId = ReadString(
                arguments,
                "-performance-profile-id",
                options.ProfileId);
            options.ActorCount = ReadInt(
                arguments,
                "-performance-actors",
                0,
                0,
                5000);
            options.LivestockCount = ReadInt(
                arguments,
                "-performance-livestock",
                0,
                0,
                5000);
            options.FacilityCount = ReadInt(
                arguments,
                "-performance-facilities",
                0,
                0,
                100000);
            options.GridWidth = ReadInt(
                arguments,
                "-performance-grid-width",
                options.GridWidth,
                1,
                1024);
            options.GridHeight = ReadInt(
                arguments,
                "-performance-grid-height",
                options.GridHeight,
                1,
                1024);
            options.ActiveFloors = ReadInt(
                arguments,
                "-performance-active-floors",
                options.ActiveFloors,
                1,
                options.GridHeight);
            options.NormalOperationSupplyDays = ReadInt(
                arguments,
                "-performance-supply-days",
                0,
                0,
                30);
            options.SimulationSpeed = ReadFloat(
                arguments,
                "-performance-simulation-speed",
                options.SimulationSpeed,
                0.1f,
                5f);
            options.RoomSpan = ReadInt(
                arguments,
                "-performance-room-span",
                options.RoomSpan,
                8,
                128);
            options.WarmupFrames = ReadInt(
                arguments,
                "-performance-warmup-frames",
                options.WarmupFrames,
                1,
                3600);
            options.SampleSeconds = ReadFloat(
                arguments,
                "-performance-sample-seconds",
                options.SampleSeconds,
                2f,
                120f);
            options.HoldSeconds = ReadFloat(
                arguments,
                "-performance-hold-seconds",
                options.HoldSeconds,
                2f,
                60f);
            options.DisableAiScheduler = HasFlag(
                arguments,
                "-performance-disable-ai");
            options.DisableCharacterPresentation = HasFlag(
                arguments,
                "-performance-disable-character-presentation");
            options.DisableCharacterStatsUpdates = HasFlag(
                arguments,
                "-performance-disable-character-stats");

            string defaultDirectory = Path.Combine(
                Application.persistentDataPath,
                "Performance");
            options.ReportPath = Path.GetFullPath(ReadString(
                arguments,
                "-performance-report",
                Path.Combine(defaultDirectory, $"{options.ProfileId}.json")));
            options.ScreenshotPath = Path.GetFullPath(ReadString(
                arguments,
                "-performance-screenshot",
                Path.Combine(defaultDirectory, $"{options.ProfileId}.png")));
            return options;
        }

        private static bool HasFlag(string[] arguments, string key)
        {
            return arguments != null
                && arguments.Any(argument =>
                    string.Equals(argument, key, StringComparison.OrdinalIgnoreCase));
        }

        private static int ReadInt(
            string[] arguments,
            string key,
            int fallback,
            int minimum,
            int maximum)
        {
            string value = ReadValue(arguments, key);
            return int.TryParse(value, out int parsed)
                ? Mathf.Clamp(parsed, minimum, maximum)
                : Mathf.Clamp(fallback, minimum, maximum);
        }

        private static float ReadFloat(
            string[] arguments,
            string key,
            float fallback,
            float minimum,
            float maximum)
        {
            string value = ReadValue(arguments, key);
            return float.TryParse(value, out float parsed)
                ? Mathf.Clamp(parsed, minimum, maximum)
                : Mathf.Clamp(fallback, minimum, maximum);
        }

        private static string ReadString(
            string[] arguments,
            string key,
            string fallback)
        {
            string value = ReadValue(arguments, key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ReadValue(string[] arguments, string key)
        {
            if (arguments == null)
            {
                return null;
            }

            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }
    }
}
